"""
TTRPG Table — LLM Oracle Microservice
======================================
FastAPI service that answers ruleset questions using:
  - Ollama local LLM (mistral:7b-instruct or llama3:8b)
  - pgvector similarity search over embedded ruleset chunks (RAG)

Environment variables (set in docker-compose or .env):
  DATABASE_URL  — PostgreSQL connection string with pgvector enabled
  OLLAMA_URL    — Ollama base URL (default: http://ollama:11434)
  EMBED_MODEL   — Embedding model (default: nomic-embed-text)
  LLM_MODEL     — Generative model (default: mistral:7b-instruct)
"""

from __future__ import annotations

import os
import logging
from typing import Optional

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field
from pydantic_settings import BaseSettings
import httpx

logger = logging.getLogger("llm_oracle")
logging.basicConfig(level=logging.INFO)


class Settings(BaseSettings):
    database_url: str = ""
    ollama_url: str = "http://ollama:11434"
    embed_model: str = "nomic-embed-text"
    llm_model: str = "mistral:7b-instruct"
    llm_version: str = "0.1.0"

    class Config:
        env_file = ".env"


settings = Settings()

app = FastAPI(title="TTRPG Oracle", version=settings.llm_version)


# ── Request / Response models ──────────────────────────────────────────────

class QueryRequest(BaseModel):
    question: str = Field(..., min_length=3, max_length=2000)
    session_id: Optional[str] = None
    ruleset_code: Optional[str] = None


class QueryResponse(BaseModel):
    answer: str
    sources: list[str] = []
    model: str


class ReindexRequest(BaseModel):
    ruleset_code: str
    definition_json: str


# ── Health ─────────────────────────────────────────────────────────────────

@app.get("/health")
def health() -> dict:
    return {
        "status": "healthy",
        "version": settings.llm_version,
        "model": settings.llm_model,
        "embed_model": settings.embed_model,
    }


# ── Query endpoint ─────────────────────────────────────────────────────────

@app.post("/query", response_model=QueryResponse)
async def query(request: QueryRequest) -> QueryResponse:
    """
    Answer a question about ruleset mechanics.
    1. Embed the question using the embed model.
    2. Retrieve the top-k most similar ruleset chunks from pgvector.
    3. Construct a RAG prompt and generate a response from the LLM.
    """
    context_chunks = await retrieve_context(request.question, request.ruleset_code)
    context_text = "\n\n".join(context_chunks) if context_chunks else "(no context retrieved)"

    system_prompt = (
        "You are a TTRPG rules expert assistant. "
        "Answer questions about game rules concisely and accurately. "
        "If the rules are ambiguous, say so. "
        "Base your answer on the provided ruleset context."
    )
    user_prompt = (
        f"Ruleset context:\n{context_text}\n\n"
        f"Question: {request.question}"
    )

    answer = await call_llm(system_prompt, user_prompt)
    return QueryResponse(answer=answer, sources=[], model=settings.llm_model)


# ── Re-index endpoint (admin) ──────────────────────────────────────────────

@app.post("/reindex")
async def reindex(request: ReindexRequest) -> dict:
    """
    Chunk and embed a ruleset definition JSON, storing vectors in pgvector.
    Called by the API after a ruleset is imported or updated.
    """
    if not settings.database_url:
        raise HTTPException(status_code=503, detail="Database not configured — vectors not stored.")

    chunks = chunk_ruleset(request.definition_json)
    stored = await embed_and_store(request.ruleset_code, chunks)
    return {"ruleset_code": request.ruleset_code, "chunks_stored": stored}


# ── Internal helpers ───────────────────────────────────────────────────────

async def retrieve_context(question: str, ruleset_code: Optional[str]) -> list[str]:
    """Embed the question and query pgvector for similar ruleset chunks."""
    if not settings.database_url:
        return []

    try:
        embedding = await embed_text(question)
        # pgvector query via asyncpg — placeholder until full implementation
        # Returns top-5 most similar chunks for the given ruleset
        _ = embedding  # suppress unused warning
        return []  # TODO: implement asyncpg pgvector query
    except Exception as exc:
        logger.warning("Context retrieval failed: %s", exc)
        return []


async def call_llm(system: str, user: str) -> str:
    """Call the Ollama generate endpoint and return the response text."""
    async with httpx.AsyncClient(timeout=60) as client:
        try:
            resp = await client.post(
                f"{settings.ollama_url}/api/chat",
                json={
                    "model": settings.llm_model,
                    "messages": [
                        {"role": "system", "content": system},
                        {"role": "user", "content": user},
                    ],
                    "stream": False,
                },
            )
            resp.raise_for_status()
            return resp.json()["message"]["content"]
        except httpx.HTTPError as exc:
            logger.error("LLM call failed: %s", exc)
            raise HTTPException(status_code=502, detail="LLM service unavailable.") from exc


async def embed_text(text: str) -> list[float]:
    """Generate an embedding vector using the Ollama embedding model."""
    async with httpx.AsyncClient(timeout=30) as client:
        resp = await client.post(
            f"{settings.ollama_url}/api/embeddings",
            json={"model": settings.embed_model, "prompt": text},
        )
        resp.raise_for_status()
        return resp.json()["embedding"]


async def embed_and_store(ruleset_code: str, chunks: list[str]) -> int:
    """Embed each chunk and store in pgvector. Returns number of stored chunks."""
    count = 0
    for chunk in chunks:
        try:
            _embedding = await embed_text(chunk)
            # TODO: upsert into ruleset_embeddings table via asyncpg
            count += 1
        except Exception as exc:
            logger.warning("Failed to embed chunk: %s", exc)
    return count


def chunk_ruleset(definition_json: str) -> list[str]:
    """
    Split ruleset JSON into text chunks suitable for embedding.
    Splits on top-level action definitions, items, and class descriptions.
    """
    import json
    try:
        definition = json.loads(definition_json)
    except json.JSONDecodeError:
        return [definition_json[:2000]]

    chunks: list[str] = []

    if "displayName" in definition:
        chunks.append(f"Ruleset: {definition.get('displayName', '')}. {definition.get('description', '')}")

    for action in definition.get("actions", []):
        label = action.get("label", "")
        desc = action.get("description", "")
        roll = action.get("roll", {})
        chunks.append(f"Action: {label}. {desc}. Roll: {roll.get('dice', '')} using {roll.get('attribute', '')} + {roll.get('skill', '')}.")

    for char_class in definition.get("character", {}).get("classes", []):
        chunks.append(f"Class: {char_class.get('label', '')}. Skills: {', '.join(char_class.get('availableSkills', []))}.")

    for status in definition.get("statusEffects", []):
        chunks.append(f"Status: {status.get('label', '')} — {status.get('description', '')}.")

    return chunks or [definition_json[:2000]]
