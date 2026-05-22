# TTRPG Table — v2.0 Design Document

**Status:** Draft  
**Date:** May 2026  
**Scope:** Architecture, feature design, UI/UX, and mobile strategy for the v2.0 platform

---

## Table of Contents

1. [Goals & Guiding Principles](#1-goals--guiding-principles)
2. [System Architecture](#2-system-architecture)
3. [Data Flow Diagrams](#3-data-flow-diagrams)
4. [Backend Changes](#4-backend-changes)
5. [New Feature Designs](#5-new-feature-designs)
   - 5a. Campaign Management
   - 5b. Session Scheduler
   - 5c. Game-Aware LLM Assistant
6. [Action Submission & Resolution System](#6-action-submission--resolution-system)
   - 6a. Design Principles
   - 6b. Session Modes
   - 6c. Action State Machine
   - 6d. Combat Mode Flow
   - 6e. Exploration Mode Flow
   - 6f. Reaction & Follow-Up Roll System
   - 6g. Dice Roll Configuration
   - 6h. Data Model
   - 6i. WebSocket Event Catalogue
   - 6j. DM Resolution Workspace
   - 6k. Player Action Builder
   - 6l. Action Log — Timeline View
7. [Web Frontend (Nuxt 4)](#7-web-frontend-nuxt-4)
8. [Mobile Frontend (Ionic + Vue)](#8-mobile-frontend-ionic--vue)
9. [UI/UX Design System](#9-uiux-design-system)
10. [Infrastructure & Deployment](#10-infrastructure--deployment)
11. [Migration Path from v1.0](#11-migration-path-from-v10)
12. [Open Questions & Future Scope](#12-open-questions--future-scope)

---

## 1. Goals & Guiding Principles

### v2.0 Goals
- Replace polling with WebSocket real-time; eliminate the 3s round-trip lag
- Graduate from SQLite to PostgreSQL for multi-session concurrency
- Introduce Campaign Management as a first-class entity above sessions
- Add a self-hosted, ruleset-aware LLM assistant (Ollama + RAG)
- Deliver an Ionic mobile app (iOS/Android) with push notifications, offline character sheets, and haptic feedback
- Integrate a session scheduler with Google Calendar sync and Discord reminders
- Harden security across the board (rate limiting, token expiry, lockout, HTTPS)
- Design for future paid tiers without building billing in v2.0

### Guiding Principles
- **The DM is the authority** — server enforces structure, not dice outcomes (design stays honour-system but documents it explicitly)
- **Mobile is a first-class citizen** — every flow designed mobile-first, then adapted for desktop
- **Offline tolerance** — players should be able to view their character sheet and notes without a connection
- **Ruleset agnosticism** — all new features must work with any ruleset JSON, not just D&D 5e
- **Incremental migration** — v1.0 data must be importable into v2.0 without data loss

---

## 2. System Architecture

### Architecture Pattern: BFF + Microservices

The v2.0 architecture uses a **Backend For Frontend (BFF)** pattern with three independently deployable services, plus isolated infrastructure. This gives each client type exactly the interface it needs, isolates the GPU-heavy LLM workload, and keeps the core business logic in one authoritative backend.

**The four services:**

| Service | Tech | Owned by | Notes |
|---------|------|----------|-------|
| **Backend API** | ASP.NET Core 8 | All business logic, persistence, real-time | Single source of truth for data and rules |
| **Web BFF** | Nuxt 4 (server layer) | Web client optimisation, SSR, cookie auth | Aggregates API calls into page-shaped responses; issues httpOnly refresh cookies |
| **LLM Service** | Python + FastAPI + Ollama | RAG pipeline, ruleset embeddings, streaming | Isolated because of GPU compute requirements and Python LLM ecosystem |
| **Mobile clients** | Ionic + Vue + Capacitor | iOS/Android | Talks directly to the Backend API using mobile-optimised API versioning (`/api/mobile/v1/`); no separate Mobile BFF |

**WebSocket routing decision:** SignalR connections bypass all BFF layers and connect directly to the Backend API. Long-lived stateful connections do not benefit from a BFF intermediary and only gain deployment complexity from one. The Web BFF and Ionic app both establish their SignalR connections directly to the API's `/hubs/*` endpoints.

**Mobile-specific responses:** Instead of a separate Mobile BFF, the Backend API exposes a versioned mobile surface (`/api/mobile/v1/`) that returns leaner, projection-optimised payloads. This is lighter than a full BFF service and avoids an extra network hop.

---

### High-Level Architecture

```mermaid
flowchart TB
    subgraph clients [Clients]
        web["Nuxt 4 Browser\n(Desktop / Tablet)"]
        mobile["Ionic App\n(iOS / Android)"]
    end

    subgraph edge [Edge — Nginx / Caddy]
        tls["TLS termination\nWebSocket upgrade\nRoute by host/path"]
    end

    subgraph bff [Web BFF — Nuxt 4 Server]
        ssr["SSR page rendering\nPage-aggregation routes\nCookie-based auth\n/api/* REST proxy"]
    end

    subgraph api [Backend API — ASP.NET Core 8]
        rest["REST Controllers\n/api/v1/* and /api/mobile/v1/*"]
        hubs["SignalR Hubs\n/hubs/session\n/hubs/notifications"]
        bg["Background Services\nScheduler · Timeout · Push"]
    end

    subgraph llmSvc [LLM Service — Python / FastAPI]
        fastapi["FastAPI\nStreaming SSE endpoint\n/llm/query"]
        rag["RAG Pipeline\nChunker · Embedder"]
        ollama["Ollama\nModel server"]
    end

    subgraph data [Data]
        pg[("PostgreSQL + pgvector")]
        redis[("Redis\nSignalR backplane\nResponse cache")]
    end

    subgraph external [External]
        gcal["Google Calendar"]
        discord["Discord Webhooks"]
        fcm["FCM / APNs"]
    end

    web -->|"HTTPS (all traffic)"| tls
    mobile -->|"HTTPS REST + WS"| tls

    tls -->|"web.app/* → SSR"| bff
    tls -->|"api.app/* REST"| api
    tls -->|"api.app/hubs/* WS"| hubs
    tls -->|"llm.app/*"| llmSvc
    mobile -->|"Direct WS /hubs/*"| hubs

    bff -->|"REST aggregation"| rest
    bff -->|"LLM queries SSE"| fastapi

    rest --> pg
    rest --> redis
    hubs --> redis
    bg --> pg
    bg --> fcm
    bg --> discord
    bg --> gcal

    rag --> pg
    fastapi --> rag
    rag --> ollama
```

### Component Responsibilities

| Component | Responsibility | Scales independently? |
|-----------|---------------|----------------------|
| **Nginx / Caddy** | TLS termination, route by subdomain/path, WebSocket upgrade headers | Yes — stateless |
| **Web BFF (Nuxt server)** | SSR rendering; page-level API aggregation (one BFF call = multiple API calls); httpOnly cookie issuance for refresh tokens; proxies REST and LLM queries | Yes — stateless |
| **Backend API** | All business logic, data access, auth, SignalR hubs, background workers | Yes — stateless REST + Redis-backed SignalR |
| **LLM Service** | RAG ingestion on ruleset import; streaming rules queries; embedding management | Yes — GPU-bound; scale down to zero when idle |
| **PostgreSQL + pgvector** | Primary persistent store; vector embeddings in same DB | Yes — read replicas for heavy read paths |
| **Redis** | SignalR backplane (enables multiple API instances); short-TTL response cache; session state | Yes |
| **Mobile API surface** (`/api/mobile/v1/`) | Leaner JSON projections for bandwidth-sensitive devices; push token endpoints; same auth as web | Part of Backend API — no extra service |

### Inter-Service Communication

| From | To | Protocol | Auth |
|------|----|----------|------|
| Browser / Ionic | Web BFF | HTTPS | httpOnly cookie (web) / Bearer JWT (mobile) |
| Browser / Ionic | Backend API (WS) | WSS / SignalR | Bearer JWT in connection query param |
| Web BFF | Backend API | HTTPS (internal) | Service-to-service JWT (short-lived, signed with shared secret) |
| Web BFF | LLM Service | HTTPS + SSE (streaming) | Service-to-service JWT |
| Backend API | LLM Service | HTTPS (ruleset ingest trigger) | Service-to-service JWT |
| Backend API | PostgreSQL | TCP | Connection string credential |
| Backend API | Redis | TCP | Redis AUTH password |
| LLM Service | PostgreSQL | TCP (pgvector queries) | Connection string credential |

---

## 3. Data Flow Diagrams

### 3a. Web BFF — Page Aggregation Flow

The Web BFF's primary value is collapsing multiple API calls into a single SSR response. A player joining a session page would otherwise need 4+ separate REST calls from the browser; the BFF does them in parallel on the server and returns one assembled payload.

```mermaid
sequenceDiagram
    participant Browser as DM Browser
    participant BFF as Web BFF (Nuxt server)
    participant API as Backend API
    participant PG as PostgreSQL

    Browser->>BFF: GET /sessions/abc123/dm (page request)
    Note over BFF: SSR route handler — aggregates in parallel

    par Parallel API calls from BFF
        BFF->>API: GET /api/v1/sessions/abc123
        BFF->>API: GET /api/v1/sessions/abc123/participants
        BFF->>API: GET /api/v1/sessions/abc123/actions?status=pending
        BFF->>API: GET /api/v1/campaigns/{campaignId}/notes?limit=5
    end

    API->>PG: queries (all 4 run while BFF waits)
    PG-->>API: results
    API-->>BFF: 4 responses

    BFF->>BFF: assemble single DmPageData object
    BFF-->>Browser: SSR HTML + hydration payload (one round trip)

    Note over Browser: Page renders immediately with full data
    Browser->>API: WSS /hubs/session (direct — bypasses BFF)
    Note over Browser: Real-time updates from here on
```

### 3b. Real-Time Session Flow (WebSocket direct to API)

WebSocket connections bypass the BFF entirely and connect directly to the Backend API. This avoids stateful proxying complexity and means the BFF is never in the hot path for real-time events.

```mermaid
sequenceDiagram
    participant DMWeb as DM Browser
    participant PlayerMobile as Player Mobile (Ionic)
    participant API as Backend API + SignalR
    participant Redis

    DMWeb->>API: WSS /hubs/session?token=JWT
    API->>Redis: subscribe channel session:X

    PlayerMobile->>API: WSS /hubs/session?playerToken=ABC
    API->>Redis: subscribe channel session:X

    Note over DMWeb: Player submits action via REST (through BFF for web, direct for mobile)
    DMWeb->>API: POST /api/v1/actions (via BFF proxy)
    API->>API: validate, persist action
    API->>Redis: PUBLISH session:X { event: action.submitted }
    Redis-->>API: deliver to all session subscribers
    API-->>DMWeb: WS push action.submitted
    API-->>PlayerMobile: WS push action.submitted

    Note over DMWeb: DM resolves via REST
    DMWeb->>API: POST /api/v1/actions/{id}/resolve (via BFF proxy)
    API->>API: apply stat changes, persist resolution
    API->>Redis: PUBLISH session:X { event: action.resolved, payload }
    Redis-->>API: deliver
    API-->>DMWeb: WS push action.resolved + character.stats_updated
    API-->>PlayerMobile: WS push action.resolved + character.stats_updated
```

### 3c. LLM Rules Query Flow

LLM queries from the web client travel through the Web BFF (which adds session context and auth), then to the LLM Service as a streaming SSE response. Mobile clients query the LLM Service via the Backend API as a relay (keeping the LLM Service internal-only).

```mermaid
sequenceDiagram
    participant User as DM or Player (web)
    participant BFF as Web BFF
    participant LLMSvc as LLM Service (FastAPI)
    participant RAG as RAG Pipeline
    participant Vec as pgvector
    participant Ollama

    User->>BFF: POST /api/llm/query { question, rulesetId }
    BFF->>BFF: attach sessionContext from BFF session store
    BFF->>LLMSvc: POST /llm/query { question, rulesetId, sessionContext }

    LLMSvc->>RAG: embed question + similarity search
    RAG->>Vec: cosine search ruleset_embeddings WHERE ruleset_id = X
    Vec-->>RAG: top-5 relevant chunks

    RAG->>Ollama: prompt (system + chunks + question)
    Ollama-->>LLMSvc: stream tokens

    LLMSvc-->>BFF: SSE stream
    BFF-->>User: SSE stream (passthrough)
    User->>User: display tokens as they arrive
```

### 3d. Mobile Push Notification Flow

```mermaid
sequenceDiagram
    participant SchedulerWorker as Background Worker
    participant PG
    participant FCM as FCM / APNs
    participant Device as Player Device

    SchedulerWorker->>PG: query sessions in next 24h
    PG-->>SchedulerWorker: upcoming sessions + participant device tokens
    SchedulerWorker->>FCM: send "Session tomorrow" push
    FCM->>Device: native push notification
    Device->>Device: user taps → deep link opens session page
```

### 3e. Auth & Token Lifecycle (v2.0)

The Web BFF owns the httpOnly cookie layer for web clients. Mobile clients receive a Bearer JWT directly from the Backend API and store it in Capacitor's secure storage (not localStorage).

```mermaid
sequenceDiagram
    participant WebBrowser as Web Browser
    participant BFF as Web BFF
    participant API as Backend API
    participant MobileApp as Ionic App

    Note over WebBrowser,BFF: Web auth flow (httpOnly cookie)
    WebBrowser->>BFF: POST /auth/login { username, password }
    BFF->>API: POST /api/v1/auth/login
    API-->>BFF: { accessToken (15 min), refreshToken (7 days) }
    BFF->>BFF: set refreshToken as httpOnly cookie
    BFF-->>WebBrowser: { accessToken } (JS-readable, memory-only)

    Note over MobileApp,API: Mobile auth flow (direct to API)
    MobileApp->>API: POST /api/v1/auth/login
    API-->>MobileApp: { accessToken (15 min), refreshToken (7 days) }
    MobileApp->>MobileApp: store both in Capacitor Preferences (secure)
```

### 3f. Refresh Token Rotation

For web clients the BFF intercepts the 401 and performs the silent refresh transparently. For mobile, the Ionic app handles it directly.

```mermaid
sequenceDiagram
    participant Browser
    participant BFF as Web BFF
    participant API as Backend API
    participant PG

    Browser->>BFF: GET /api/sessions/123 (expired accessToken in memory)
    BFF->>API: forward request with expired token
    API-->>BFF: 401 Unauthorized

    BFF->>API: POST /api/v1/auth/refresh (httpOnly cookie sent automatically)
    API->>PG: validate + rotate refresh token
    API-->>BFF: new { accessToken, refreshToken }
    BFF->>BFF: update in-memory accessToken;\nset new refreshToken as httpOnly cookie
    BFF->>API: retry original request with new accessToken
    API-->>BFF: 200 response
    BFF-->>Browser: transparent — browser never saw the 401
```

---

## 4. Service Designs

### 4a. Backend API — ASP.NET Core 8

The Backend API is the single source of truth for all business logic, data persistence, and real-time events. It exposes two REST surfaces and the SignalR hub:

- `/api/v1/*` — standard API for web clients (via Web BFF proxy) and direct mobile calls
- `/api/mobile/v1/*` — mobile-optimised projections (leaner payloads; push token endpoints)
- `/hubs/*` — SignalR WebSocket endpoints (direct connection from all clients)

#### Database — PostgreSQL Migration

**Schema changes from v1.0:**
- Replace `EnsureCreated` + hand-rolled `ALTER TABLE` SQL with proper EF Core migrations
- All `TEXT` JSON columns stay as PostgreSQL `jsonb` columns — gains GIN index support for ruleset queries
- Add `pgvector` extension for embedding storage

**New tables / key additions:**

| Table | Purpose |
|-------|---------|
| `campaigns` | Umbrella entity grouping multiple games and sessions |
| `campaign_notes` | DM world notes, quest logs, lore entries |
| `scheduled_sessions` | Calendar entries linked to sessions |
| `calendar_integrations` | Per-user Google Calendar OAuth tokens |
| `push_device_tokens` | FCM/APNs tokens per user device |
| `ruleset_embeddings` | pgvector rows — chunk text + `vector(1536)` column |
| `llm_query_logs` | Audit log of LLM questions (for moderation/cost tracking) |
| `refresh_tokens` | Rotating refresh token store |

**`GameParticipant` additions:**
- `PlayerTokenExpiresAt` — expire player tokens on session end
- `DeviceToken` (moved to separate `push_device_tokens` table for multi-device)

#### Real-Time — SignalR Hubs

Replace all polling endpoints with SignalR. Redis backplane allows horizontal scaling to multiple API instances.

| Hub | URL | Who connects | Events pushed |
|-----|-----|-------------|--------------|
| `SessionHub` | `/hubs/session` | DM + all players in a session | All action lifecycle events (see §6i), combat turn changes, note updates, presence |
| `NotificationHub` | `/hubs/notifications` | Any authenticated user | Session scheduled, reminders, invite received |

**Connection auth:**
- DM / web: Bearer JWT in `Authorization` header on WS upgrade
- Players (mobile): `X-Player-Token` query param
- Graceful degradation: SignalR falls back to SSE long-poll automatically for clients that cannot upgrade to WebSocket

#### Auth — Refresh Tokens + Expiry

| Property | v1.0 | v2.0 |
|----------|------|------|
| Access token TTL | 60 min (configurable) | 15 min (short-lived) |
| Refresh token | None | 7-day rotating |
| Web storage | `localStorage` | Access token: BFF in-memory; Refresh: httpOnly cookie |
| Mobile storage | `localStorage` | Access token + refresh token: `@capacitor/preferences` (encrypted) |
| XSS risk | Token stealable via JS | Web: cookie inaccessible to JS; Mobile: secure enclave storage |
| 401 handling | Per-page manual | Web BFF intercepts and silently refreshes; mobile app-level interceptor |
| Account lockout | Disabled | 5 failed attempts → 15 min lockout |

### 4b. Web BFF — Nuxt 4 Server Layer

The Web BFF is the Nuxt server layer. It is not a separate codebase — it is the server-side half of the existing Nuxt app, significantly expanded.

**Responsibilities:**
- SSR page rendering with pre-fetched, aggregated data (one server → server request per page instead of multiple browser → server requests)
- Issues and rotates httpOnly refresh cookies on behalf of the browser
- Proxies REST calls to the Backend API with service-to-service JWT (the browser's access token is never forwarded directly — the BFF exchanges it for a service credential)
- Proxies LLM SSE streams from the LLM Service to the browser
- Does **not** handle WebSocket connections — these go directly from the browser to the API

**Page aggregation examples:**

| Page | API calls aggregated | v1.0 browser calls | v2.0 BFF calls |
|------|---------------------|--------------------|----------------|
| `/sessions/[id]/dm` | session + participants + pending actions + recent log | 4 sequential | 1 parallel BFF call |
| `/campaigns/[id]` | campaign + notes + games + upcoming schedule | 4 sequential | 1 parallel BFF call |
| `/games` | games list + campaigns list + active sessions | 3 sequential | 1 parallel BFF call |

**BFF route structure:**
```
ui/src/server/
  api/
    auth/
      login.post.ts         ← receives credentials, calls API, sets httpOnly cookie
      refresh.post.ts       ← reads httpOnly cookie, calls API refresh, rotates cookie
      logout.post.ts        ← clears cookie
    sessions/
      [id]/dm.get.ts        ← aggregates session + participants + actions for DM page
      [id]/summary.get.ts   ← aggregates session summary data
    campaigns/
      index.get.ts
      [id]/index.get.ts     ← aggregates campaign dashboard data
    llm/
      query.post.ts         ← proxies to LLM Service with SSE passthrough
    [...path].ts            ← catch-all proxy for non-aggregated API calls
```

### 4c. LLM Service — Python / FastAPI

The LLM Service is an independent Python microservice. It is the only service that requires GPU compute and the Python ecosystem (LangChain, sentence-transformers, Ollama SDK).

**Tech stack:**
- FastAPI — async HTTP server with native SSE streaming support
- LangChain — RAG pipeline orchestration
- `sentence-transformers` via Ollama `nomic-embed-text` — embedding generation
- Ollama — local model server (`mistral:7b-instruct` or `llama3:8b`)
- `psycopg2` / `asyncpg` — direct pgvector queries

**Endpoints:**

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/llm/query` | Stream a rules question answer (SSE) |
| `POST` | `/llm/ingest/{rulesetId}` | Chunk, embed, and store a ruleset (called by Backend API on import) |
| `DELETE` | `/llm/ingest/{rulesetId}` | Remove embeddings for a deleted ruleset |
| `GET` | `/health` | Liveness check |

**Service isolation benefits:**
- GPU VM can be scaled down to zero when no sessions are active (cost saving for small SaaS)
- Model can be swapped (e.g. llama3 → mistral) without touching the API or BFF
- Python dependency tree (torch, transformers) never pollutes the .NET or Node environments
- Rate limiting and cost controls can be applied at this service boundary independently

### 4d. Security Hardening (from audit)

- **Rate limiting:** `AddRateLimiter` with fixed-window on `/auth/*` (10 req/min); sliding-window on poll/query endpoints
- **HTTPS + HSTS:** `UseHttpsRedirection` + `UseHsts` in production pipeline
- **`AllowedHosts`:** Set to actual hostname in production config
- **Response compression:** Brotli + Gzip via `AddResponseCompression`
- **Ruleset cache:** `IMemoryCache` for `GET /api/rulesets`; invalidated on admin import

### 4e. Performance Fixes (from audit)

- Admin N+1 → single `GroupBy` query
- Stat-change DB round-trips → batched `SaveChangesAsync`
- Sync NPC EF call → `FirstOrDefaultAsync`
- Ownership check → lightweight query (id + hostId only), separate full load
- `SeedRulesetsAsync` → SHA-256 hash check per file before write
- `SeedDefaultUsersAsync` → `FindByNameAsync` single lookup

### 4f. Eliminated Patterns

- Hand-rolled `ApplySchemaUpdatesAsync` SQL block (883 lines) → replaced by EF Core migrations
- `JoinUrl` identity helper → removed
- `RulesetDetailResponse` empty subclass → collapsed to `RulesetResponse`
- Unused `StartSessionRequest.Title`, `ActionRequest.TargetCharacterId`, `ActionResolution.AdditionalActions` → removed

---

## 5. New Feature Designs

### 5a. Campaign Management

**Concept:** A Campaign is a named world that owns Games. Multiple game groups can play in the same Campaign world. Sessions belong to Games, which belong to a Campaign.

**Entity hierarchy:**
```
Campaign
  ├── CampaignNotes (DM world lore, freeform)
  ├── Game (one per player group)
  │     ├── GameParticipants (characters)
  │     ├── NPCs
  │     └── Sessions
  │           └── Actions / Notes / Combat
  └── ScheduledSessions (linked to a Game, may be pre-session)
```

**Campaign Dashboard UI (web):**

```
┌──────────────────────────────────────────────────────────┐
│  [Campaign: The Shattered Realms]          DM: you       │
│  ──────────────────────────────────────────────────────  │
│  ┌───────────────┐  ┌───────────────┐  ┌─────────────┐  │
│  │  Game Groups  │  │  World Notes  │  │  Scheduler  │  │
│  │  2 active     │  │  12 entries   │  │  Next: Sat  │  │
│  └───────────────┘  └───────────────┘  └─────────────┘  │
│                                                          │
│  Recent Sessions ──────────────────────────────────────  │
│  ● Session 14 — The Goblin Fort     [Summary] [Replay]  │
│  ● Session 13 — Market Trouble      [Summary]           │
└──────────────────────────────────────────────────────────┘
```

**New API endpoints:**

| Method | Path | Description |
|--------|------|-------------|
| GET/POST | `/api/campaigns` | List / create campaigns |
| GET/PUT/DELETE | `/api/campaigns/{id}` | Campaign CRUD |
| GET/POST | `/api/campaigns/{id}/notes` | World notes |
| GET | `/api/campaigns/{id}/sessions` | All sessions across games in campaign |

**World Notes** are freeform markdown documents with tags. Planned types: `lore`, `quest`, `npc-profile`, `location`, `session-recap`.

---

### 5b. Session Scheduler

**Concept:** A ScheduledSession has a proposed date/time, an RSVP list, and syncs to Google Calendar and Discord.

**Flow:**

```mermaid
flowchart LR
    DM[DM creates\nScheduledSession] --> DB[(PostgreSQL)]
    DB --> GCal[Google Calendar\nEvent created]
    DB --> Discord[Discord Webhook\n#session-reminders]
    DB --> Push[Push notification\nto all participants]
    DB --> InApp[In-app calendar\nentry]
    Player[Player RSVPs] --> DB
    DB --> DM2[DM sees\nRSVP summary]
```

**New API endpoints:**

| Method | Path | Description |
|--------|------|-------------|
| GET/POST | `/api/campaigns/{id}/schedule` | List / propose sessions |
| PUT | `/api/schedule/{id}/rsvp` | Player RSVP (yes / no / maybe) |
| POST | `/api/schedule/{id}/sync-calendar` | Push to Google Calendar |
| DELETE | `/api/schedule/{id}` | Cancel scheduled session |

**Google Calendar integration:**
- DM grants calendar access via OAuth 2.0 (separate from login — uses `google-auth-library`)
- Token stored encrypted in `calendar_integrations` table
- Background worker re-syncs on any schedule change

**Discord integration:**
- DM enters a webhook URL per game/campaign
- Scheduler worker POSTs a formatted embed when session is created and 24h before start
- No Discord bot required — just incoming webhooks

**RSVP UI widget:**
```
┌────────────────────────────────────────────┐
│  Session 15 — "The Bridge of Souls"        │
│  Saturday, June 7 · 7:00 PM               │
│                                            │
│  ✓ Alice (Vaelith)     Going               │
│  ? Bob (Thrain)        Maybe               │
│  ✗ Carol (Seraphine)   Can't make it       │
│  ○ Dave (Mira)         No response         │
│                                            │
│  [Add to Calendar]   [Remind via Discord]  │
└────────────────────────────────────────────┘
```

---

### 5c. Game-Aware LLM Assistant (Ruleset Oracle)

**Concept:** A self-hosted Ollama instance answers rules questions about the active game's ruleset. Uses RAG to ground answers in the actual loaded ruleset JSON rather than general training data. Available as a side panel to both DM and players during a session.

**LLM Architecture:**

```mermaid
flowchart TB
    subgraph ingest [Ruleset Ingestion — runs on import]
        json["Ruleset JSON"] --> chunk["Chunker\n(section-aware splits)"]
        chunk --> embed["Embedder\n(Ollama nomic-embed-text)"]
        embed --> pgvec["pgvector store\n(ruleset_embeddings)"]
    end

    subgraph query [Query — runs on user question]
        q["User question"] --> qembed["Embed question\n(same model)"]
        qembed --> search["pgvector\ncosine similarity search\ntop-5 chunks"]
        search --> prompt["Build prompt:\nsystem + chunks + question\n+ session context"]
        prompt --> ollama["Ollama\n(llama3 or mistral)"]
        ollama --> stream["Streaming answer\nvia SSE"]
    end
```

**System prompt structure (per query):**
```
You are a rules referee for a tabletop RPG session.
The active ruleset is: {rulesetName}.

Relevant ruleset excerpts:
---
{top_5_chunks_from_rag}
---

Current session context:
- Active characters: {names and classes}
- Current combat: {yes/no, round}

Answer the following rules question. If the answer is not in the ruleset excerpts, say so clearly.
Do not invent rules. Cite the relevant section if possible.

Question: {user_question}
```

**UI — Oracle Panel:**

```
┌──────────────────────────────────┐
│  Ruleset Oracle  ▸ D&D 5e       │
│  ─────────────────────────────── │
│  [Can a rogue use Cunning Action │
│   to hide behind an ally?      ] │
│                            [Ask] │
│  ─────────────────────────────── │
│  ▸ Session context: Combat Rd 2  │
│                                  │
│  Based on the PHB stealth rules: │
│  Yes — a Rogue can use Cunning   │
│  Action (Bonus Action) to Hide   │
│  provided there is adequate      │
│  cover or concealment (PHB p.96) │
│                                  │
│  [👍 Helpful]  [👎 Incorrect]    │
└──────────────────────────────────┘
```

**New API endpoints:**

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/llm/query` | Ask a rules question (streams response) |
| GET | `/api/llm/history?sessionId=X` | Past questions in this session |
| POST | `/api/rulesets/{id}/reindex` | Admin — re-embed ruleset chunks |

**Feedback loop:** thumbs-up / thumbs-down stored in `llm_query_logs`; used to evaluate model quality over time and identify ruleset gaps.

**Model recommendation:** Start with `mistral:7b-instruct` or `llama3:8b` via Ollama. The `nomic-embed-text` model handles embeddings. Total VRAM requirement: ~6 GB (can run on a small GPU VPS or CPU-only at reduced speed).

---

## 6. Action Submission & Resolution System

This section is a ground-up redesign. The v1.0 action model had fundamental issues: self-reported dice with no visibility, no clear state machine, conflated combat and exploration queues, and no reaction or follow-up roll system. v2.0 replaces it entirely.

---

### 6a. Design Principles

- **The ruleset defines the action vocabulary.** Players choose from structured action types defined in the ruleset JSON; they do not type free-form commands.
- **The DM is the final authority on outcomes.** The server enforces state transitions; the DM controls resolution content.
- **Dice mode is a session-level choice.** The DM sets it once at session start. All players in the session share the same dice input mode (app-rolled or manual entry), eliminating per-player inconsistency.
- **Actions have an explicit state machine.** No action can skip states. The UI at every surface reflects the exact current state.
- **Reactions pause, not replace.** When the DM triggers a reaction from another player, the original action is held in a paused state until the reaction resolves. The queue is not skipped or reordered.
- **Full table transparency.** After resolution, every participant sees the roll, modifiers, DC (if the DM sets one), outcome, stat changes, and narrative. Nothing is hidden from players in the base model.
- **Combat and Exploration are separate modes** with different turn-gating rules, but share the same underlying action data model and resolution pipeline.

---

### 6b. Session Modes

The DM switches the session between three modes at any point. The active mode is broadcast to all clients via WebSocket.

| Mode | Turn gating | Who can submit | Resolution order |
|------|-------------|---------------|-----------------|
| **Combat** | Strict — only the active combatant's turn is open. Determined by ruleset-specific initiative order. | One player at a time (the active combatant) | DM resolves the active combatant's action before advancing to next turn |
| **Exploration** | None — the queue is always open | Any player at any time | DM picks any pending action from the queue in any order |
| **Downtime** | None — purely narrative / bookkeeping | Any player | No dice rolls required; DM may approve or reject freely |

**Mode transition rules:**
- DM switches mode explicitly (button in session header)
- Switching from Combat → Exploration does not clear the queue; pending actions carry over
- Switching from Exploration → Combat prompts the DM to set the initiative order
- The session mode is persisted on the `Session` entity and broadcast on reconnect so joining players immediately see the correct UI state

---

### 6c. Action State Machine

Every submitted action follows this state machine. State transitions are enforced server-side; the client reflects the current state but cannot advance the state unilaterally.

```mermaid
stateDiagram-v2
    [*] --> Draft : Player opens action builder
    Draft --> Submitted : Player submits action
    Submitted --> DmReviewing : DM opens action in resolution workspace
    DmReviewing --> AwaitingRoll : DM requests roll from player
    DmReviewing --> AwaitingReaction : DM triggers reaction from another player
    DmReviewing --> Resolving : DM skips to resolution (no roll needed)
    AwaitingRoll --> RollReceived : Player submits roll
    RollReceived --> AwaitingReaction : DM triggers reaction
    RollReceived --> Resolving : DM proceeds to resolution
    AwaitingReaction --> ReactionPending : Reaction sub-action created
    ReactionPending --> ReactionRollReceived : Reacting player submits roll
    ReactionRollReceived --> Resolving : DM resumes original action
    Resolving --> AwaitingFollowUpRoll : DM requests follow-up roll from player
    AwaitingFollowUpRoll --> FollowUpRollReceived : Player submits follow-up roll
    FollowUpRollReceived --> Resolving : DM continues resolution
    Resolving --> Resolved : DM confirms final resolution
    DmReviewing --> Rejected : DM rejects action
    Submitted --> Rejected : DM rejects without reviewing
    Resolved --> [*]
    Rejected --> [*]
```

**State visibility to participants:**

| State | Player sees | DM sees | Other players see |
|-------|------------|---------|-------------------|
| Draft | Action builder form | Nothing | Nothing |
| Submitted | "Awaiting DM" card | Action in pending queue | Action in queue (summary only) |
| DmReviewing | "DM is reviewing your action" | Full resolution workspace | "DM is resolving [PlayerName]'s action" |
| AwaitingRoll | Roll prompt overlay (full screen on mobile) | "Waiting for roll" spinner | "Waiting for [PlayerName] to roll" |
| AwaitingReaction | "DM is waiting for a reaction" | Both action + reaction context | Reaction prompt appears for the specific player |
| Resolving | "DM is applying effects" | Resolution workspace with all controls | "DM is finalizing" |
| Resolved | Full resolution card in timeline | Full resolution card | Full resolution card |
| Rejected | Rejection card with reason | Log entry | Not shown to other players |

---

### 6d. Combat Mode Flow

```mermaid
sequenceDiagram
    participant DM
    participant Server
    participant ActivePlayer as Active Player
    participant OtherPlayers as Other Players

    DM->>Server: AdvanceTurn (or start combat)
    Server-->>ActivePlayer: WS event turn.opened { turnId, yourCharacterId }
    Server-->>OtherPlayers: WS event turn.opened { activeCharacterName }
    Server-->>DM: WS event turn.opened { activeParticipantId }

    Note over ActivePlayer: Action builder unlocked

    ActivePlayer->>Server: POST /api/sessions/{id}/actions (action payload)
    Server-->>DM: WS action.submitted { actionCard }
    Server-->>ActivePlayer: WS action.submitted { status: Submitted }
    Server-->>OtherPlayers: WS action.submitted { summary }

    DM->>Server: PATCH /api/actions/{id}/review
    Server-->>DM: WS action.dm_reviewing (workspace data)
    Server-->>ActivePlayer: WS action.dm_reviewing

    alt DM requests roll
        DM->>Server: PATCH /api/actions/{id}/request-roll { rollSpec, dc, difficultyModifier }
        Server-->>ActivePlayer: WS action.roll_requested { rollSpec, context }
        Note over ActivePlayer: Roll overlay opens
        ActivePlayer->>Server: PATCH /api/actions/{id}/submit-roll { rollData }
        Server-->>DM: WS action.roll_submitted { rollData }
    end

    alt DM triggers reaction
        DM->>Server: POST /api/actions/{id}/reactions { reactingParticipantId, reactionType, rollSpec }
        Server-->>OtherPlayers: WS action.reaction_requested { to: reactingPlayerId, context }
        Note over OtherPlayers: Reaction overlay appears for that player only
        OtherPlayers->>Server: PATCH /api/reactions/{id}/submit { rollData }
        Server-->>DM: WS action.reaction_submitted { reactionData }
        Note over DM: Original action workspace resumes
    end

    DM->>Server: POST /api/actions/{id}/resolve { outcome, statChanges, followUpRolls, narrative }
    Server->>Server: Apply stat changes to characters/NPCs
    Server-->>ActivePlayer: WS action.resolved { fullCard }
    Server-->>OtherPlayers: WS action.resolved { fullCard }
    Server-->>DM: WS action.resolved + turn.ready_to_advance
```

**Turn gating enforcement:**
- Server rejects `POST /api/sessions/{id}/actions` from any participant who is not the `activeTurnParticipantId`
- The action builder UI is disabled (not just hidden) for non-active players
- If a player's connection drops during their turn, the DM can skip or extend with a manual control

---

### 6e. Exploration Mode Flow

```mermaid
sequenceDiagram
    participant DM
    participant Server
    participant AnyPlayer as Any Player(s)

    Note over DM,AnyPlayer: Session is in Exploration mode — queue always open

    AnyPlayer->>Server: POST /api/sessions/{id}/actions (from any player)
    Server-->>DM: WS action.submitted (appended to queue)
    Server-->>AnyPlayer: WS action.submitted { status: Submitted }

    Note over DM: Queue shows all pending actions, oldest at top

    DM->>Server: PATCH /api/actions/{chosenId}/review (DM picks any action)
    Note over DM: Resolution workspace opens for chosen action (same pipeline as combat)

    Note over DM,AnyPlayer: Multiple actions can be in Submitted state simultaneously

    DM->>Server: POST /api/actions/{id}/resolve { ... }
    Server-->>AnyPlayer: WS action.resolved
```

**Exploration queue rules:**
- Any player can submit at any time regardless of other pending actions
- The queue is ordered by submission timestamp (oldest first)
- DM can open any action for review; the queue order is not enforced on the DM
- An action opened for DM review is visually flagged in the queue as "In Progress" to prevent the DM accidentally opening two at once
- Two players can be simultaneously in `AwaitingRoll` state if the DM has opened both (e.g., a group check)

---

### 6f. Reaction & Follow-Up Roll System

#### Reactions (triggered mid-resolution)

A reaction interrupts the resolution of an in-progress action and requires input from a different player before the DM can finalise the original action.

**Reaction lifecycle:**

```
DM opens original action for resolution
   ↓
DM clicks "Trigger Reaction" → selects reacting player + reaction type
   ↓
Server creates a child ActionRequest with:
  - parentActionId = original action id
  - followUpType = "reaction"
  - status = ReactionPending
   ↓
Original action status → AwaitingReaction (blocked)
   ↓
Reacting player receives WS push: reaction overlay appears
Player builds their reaction (simplified form — target pre-filled, type pre-filled)
Player rolls and submits
   ↓
Server sets reaction status → ReactionRollReceived
DM sees reaction result in the original action workspace
   ↓
DM resumes resolution of original action
Original action status → Resolving
```

**DM reaction trigger form:**
```
┌──── Trigger Reaction ──────────────────────────────────┐
│  Reacting player:  [● Thrain (Bob)]                    │
│  Reaction type:    [Opportunity Attack ▾]              │
│  Roll required:    [Attack Roll — STR vs AC 15]        │
│  Context note:     "Vaelith is moving through your     │
│                     threatened square"                  │
│                               [Cancel]  [Send Prompt]  │
└────────────────────────────────────────────────────────┘
```

#### Follow-Up Rolls (triggered after first roll)

The DM can request one or more follow-up rolls during resolution. Each follow-up:
- Is a child `ActionRequest` with `followUpType = "chain"`
- Specifies the roll spec and optional context (e.g. "You hit — now roll damage: 1d8 + STR")
- The player sees a new roll prompt immediately after submitting the first
- Can be chained: the DM can trigger another follow-up after receiving the second roll
- Ruleset-defined chains are pre-configured in the action type definition and automatically queued by the server without DM intervention

**Ruleset-defined chain example (in ruleset JSON):**
```json
{
  "actionKey": "melee_attack",
  "label": "Melee Attack",
  "rollChain": [
    {
      "step": 1,
      "label": "Attack Roll",
      "rollSpec": "1d20 + {str_modifier}",
      "passCondition": "gte_dc",
      "onPass": "proceed_to_step_2",
      "onFail": "resolve_as_miss"
    },
    {
      "step": 2,
      "label": "Damage Roll",
      "rollSpec": "1d8 + {str_modifier}",
      "alwaysRolled": false
    }
  ]
}
```

---

### 6g. Dice Roll Configuration

The DM sets the dice mode for the entire session when starting it. This is stored on the `Session` entity and cannot be changed mid-session without a DM control (which re-prompts all active roll overlays).

| Mode | Behaviour |
|------|-----------|
| **App Roller** | The app presents an animated dice roll. The player taps "Roll" and the server generates a cryptographically random result. The result is sent directly to the server — the player sees the result but cannot alter it before submission. |
| **Manual Entry** | The player rolls physical dice and types the numeric result into a field. The app validates the input is within the dice range (e.g. 1–20 for 1d20) but does not verify the value. Modifiers are still calculated by the app. |
| **Hybrid** | App roller is the default but the player has a "Use manual roll" toggle per action. This allows players with a lucky physical d20 to use it when they want. |

**Modifier handling (all modes):**
- The player's base modifier for the relevant ability is fetched from their character sheet (server-authoritative)
- The player selects any additional situational modifiers from a checklist defined by the ruleset (e.g. "Advantage", "Flanking Bonus", "Bless")
- The server calculates the effective total: `roll + base_modifier + selected_modifiers`
- The DM sees the breakdown, not just the total
- The DM applies their own difficulty modifier (cover, environmental penalty, etc.) in the resolution workspace — this is added after submission

**App Roller technical note:**
- Rolls generated server-side using `System.Security.Cryptography.RandomNumberGenerator`
- The roll request includes the dice spec (`1d20`, `2d6`, etc.)
- The server returns both the individual die values and the total
- Roll result is signed and stored in the action record — the player cannot resubmit a different number

---

### 6h. Data Model

#### `ActionRequest` table

| Column | Type | Description |
|--------|------|-------------|
| `id` | `uuid` | Primary key |
| `session_id` | `uuid FK` | Parent session |
| `game_participant_id` | `uuid FK` | The acting character's participant record |
| `target_ref` | `varchar` | `"character:{id}"` \| `"npc:{id}"` \| `"environment:{label}"` |
| `action_type_key` | `varchar` | Key from ruleset action definition |
| `ability_key` | `varchar` | Ability/stat used (from ruleset) |
| `item_key` | `varchar?` | Inventory item used (nullable) |
| `modifier_keys` | `text[]` | Array of claimed modifier keys from ruleset |
| `flavour_text` | `text?` | Player's optional description |
| `roll_mode` | `varchar` | `"app"` \| `"manual"` \| `"hybrid"` |
| `roll_data` | `jsonb?` | `{ spec, dice, individualRolls, baseModifier, modifierBreakdown, total }` |
| `dm_difficulty_modifier` | `int?` | DM-applied modifier (set during resolution) |
| `effective_dc` | `int?` | Final DC used (nullable — not all actions have a DC) |
| `outcome` | `varchar?` | `"success"` \| `"failure"` \| `"critical_success"` \| `"critical_failure"` \| `"partial"` |
| `stat_changes` | `jsonb?` | `[{ targetRef, statKey, delta, newValue }]` |
| `narrative` | `text?` | DM's written resolution |
| `status` | `varchar` | See state machine above |
| `submitted_at` | `timestamptz` | When player submitted |
| `resolved_at` | `timestamptz?` | When DM confirmed resolution |
| `parent_action_id` | `uuid FK?` | Non-null for reactions and follow-up chain steps |
| `follow_up_type` | `varchar?` | `"reaction"` \| `"chain"` |
| `chain_step` | `int?` | Step index within a multi-roll chain |
| `session_mode_at_submit` | `varchar` | `"combat"` \| `"exploration"` \| `"downtime"` — snapshot of mode when submitted |
| `combat_round` | `int?` | Combat round number when submitted (null in exploration) |

#### `Session` additions

| Column | Type | Description |
|--------|------|-------------|
| `mode` | `varchar` | `"combat"` \| `"exploration"` \| `"downtime"` |
| `dice_roll_mode` | `varchar` | `"app"` \| `"manual"` \| `"hybrid"` — session-level setting |
| `active_turn_participant_id` | `uuid FK?` | Non-null only in combat mode |
| `active_combat_round` | `int` | Current round (0 when not in combat) |

---

### 6i. WebSocket Event Catalogue

All events are delivered via SignalR `SessionHub`. Every event carries a `sessionId` and a `timestamp`.

| Event name | Direction | Who receives | Payload summary |
|-----------|-----------|-------------|-----------------|
| `session.mode_changed` | server → all | All participants | `{ newMode, initiativeOrder? }` |
| `turn.opened` | server → all | All | `{ turnId, activeParticipantId, characterName, round }` |
| `turn.extended` | server → all | All | `{ turnId, reason }` (DM extended the timer) |
| `turn.skipped` | server → all | All | `{ participantId, reason }` |
| `action.submitted` | server → all | All | `{ actionId, participantId, characterName, actionTypeLabel, targetLabel, status }` |
| `action.dm_reviewing` | server → all | All | `{ actionId }` |
| `action.roll_requested` | server → player | Specific player | `{ actionId, rollSpec, dc, context, mode }` |
| `action.roll_received` | server → dm | DM only | `{ actionId, rollData }` |
| `action.reaction_requested` | server → player | Specific player | `{ reactionId, parentActionId, reactionType, rollSpec, context }` |
| `action.reaction_received` | server → dm | DM only | `{ reactionId, rollData }` |
| `action.followup_roll_requested` | server → player | Specific player | `{ actionId, step, rollSpec, context }` |
| `action.followup_roll_received` | server → dm | DM only | `{ actionId, step, rollData }` |
| `action.resolved` | server → all | All | `{ actionId, outcome, rollData, statChanges, narrative, resolvedAt }` |
| `action.rejected` | server → player | Specific player | `{ actionId, reason }` |
| `character.stats_updated` | server → all | All | `{ characterId, updatedStats }` (triggers live HP bar updates) |
| `npc.stats_updated` | server → dm | DM only | `{ npcId, updatedStats }` |

---

### 6j. DM Resolution Workspace

The DM workspace opens when the DM clicks a pending action. It is a full panel (right side on desktop, full-screen sheet on mobile) showing all information needed to resolve the action without switching context.

**Workspace layout (desktop):**

```
┌──────────────────────────────────────────────────────────────┐
│  Resolving: Vaelith's Melee Attack  ●  Combat Round 2        │
│  ────────────────────────────────────────────────────────── │
│  ┌── Action ──────────────────┐  ┌── Roll ─────────────────┐│
│  │ Type:   Melee Attack       │  │ Spec:   1d20 + STR      ││
│  │ Target: Goblin Boss (NPC)  │  │ Roll:   [14]            ││
│  │ Ability: Strength          │  │ STR mod: +3             ││
│  │ Item:   Longsword          │  │ Modifiers: Flanking +2  ││
│  │ Mods:   Flanking Bonus     │  │ Subtotal: 19            ││
│  │ Flavour: "I drive my blade │  │                         ││
│  │  deep into its chest"      │  │ DC:   [17      ] (edit) ││
│  └────────────────────────────┘  │ Diff mod: [+0   ] (edit)││
│                                  │ Effective: 19 vs 17 ✓   ││
│                                  └─────────────────────────┘│
│  ┌── Outcome ─────────────────────────────────────────────┐ │
│  │  [✓ Success]  [✗ Failure]  [★ Critical]  [~ Partial]  │ │
│  └────────────────────────────────────────────────────────┘ │
│  ┌── Stat Changes ─────────────────────────────────────────┐ │
│  │  Target: Goblin Boss    HP: 32 → [24] (−8)  [+ Add]    │ │
│  │  Status: [add status effect...]                         │ │
│  └─────────────────────────────────────────────────────────┘ │
│  ┌── Narrative ────────────────────────────────────────────┐ │
│  │  Vaelith's blade finds a gap in the goblin's guard,     │ │
│  │  drawing a deep wound. The boss staggers back.          │ │
│  │  [Oracle: Suggest narrative ▾]                          │ │
│  └─────────────────────────────────────────────────────────┘ │
│  ┌── DM Actions ───────────────────────────────────────────┐ │
│  │  [⚡ Trigger Reaction]  [↩ Request Follow-Up Roll]      │ │
│  │                      [✗ Reject]    [✓ Confirm & Post]  │ │
│  └─────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

**Key workspace behaviours:**
- The workspace is locked to the DM — no other user can simultaneously resolve the same action
- The Oracle button opens an inline LLM prompt pre-seeded with action context to suggest narrative text
- Confirming resolution broadcasts `action.resolved` and `character.stats_updated` to all clients simultaneously — HP bars update live without a page refresh
- The DM can save a partial resolution draft and come back (status stays `DmReviewing`; draft stored server-side)

---

### 6k. Player Action Builder

The action builder is the player-facing form for composing and submitting an action. It is only unlocked when:
- In **Combat mode**: it is the player's turn (`active_turn_participant_id` matches their participant)
- In **Exploration mode**: always unlocked (queue is always open)
- In **Downtime mode**: always unlocked (no roll required)

**Builder flow (step wizard on mobile, single panel on desktop):**

```
Step 1 — Choose Action Type
┌───────────────────────────────────────────────────────┐
│  What does Vaelith do?                                │
│                                                       │
│  ⚔  Melee Attack          ↳ STR or DEX, 1d20        │
│  🏹  Ranged Attack         ↳ DEX, 1d20               │
│  ✨  Cast Spell            ↳ INT/WIS/CHA, varies     │
│  🛡  Defend / Dodge                                   │
│  🏃  Move / Disengage                                 │
│  🎲  Skill Check           ↳ Choose ability          │
│  💬  Speak / Persuade      ↳ CHA, 1d20              │
│  ✏  Custom action...                                  │
└───────────────────────────────────────────────────────┘

Step 2 — Choose Target
┌───────────────────────────────────────────────────────┐
│  Who is the target?                                   │
│                                                       │
│  Characters                  NPCs                    │
│  ○ Thrain (Bob)              ● Goblin Boss            │
│  ○ Mira (Carol)              ○ Goblin Grunt x3        │
│  ─────────────────────────────────────────────────── │
│  ○ Environment / Object (describe below)             │
└───────────────────────────────────────────────────────┘

Step 3 — Modifiers & Item
┌───────────────────────────────────────────────────────┐
│  Are any of these applying?                           │
│                                                       │
│  ☑ Flanking (+2 to attack)                           │
│  ☐ Advantage (roll twice, take higher)               │
│  ☐ Disadvantage (roll twice, take lower)             │
│  ☐ Bless (+1d4)                                      │
│                                                       │
│  Item used:  [Longsword (1d8 slashing) ▾]            │
└───────────────────────────────────────────────────────┘

Step 4 — Roll & Flavour
┌───────────────────────────────────────────────────────┐
│  Roll: 1d20 + STR (+3) + Flanking (+2)               │
│                                                       │
│  [        🎲 Roll Dice        ]   ← app roller       │
│  — or —                                              │
│  I rolled: [   ] on my physical dice                 │
│                                                       │
│  What do you do? (optional)                          │
│  ┌──────────────────────────────────────────────────┐│
│  │ "I drive my blade deep into its chest"           ││
│  └──────────────────────────────────────────────────┘│
│                                                       │
│  Your total: 19                                      │
│                                  [Submit Action]     │
└───────────────────────────────────────────────────────┘
```

**After submission — waiting state:**
```
┌───────────────────────────────────────────────────────┐
│  ⏳ Awaiting DM                                       │
│                                                       │
│  Vaelith: Melee Attack → Goblin Boss                 │
│  Roll: 14 + 3 + 2 = 19                               │
│                                                       │
│  The DM is reviewing your action...                   │
└───────────────────────────────────────────────────────┘
```

**Roll request overlay (interrupts waiting state):**
```
┌───────────────────────────────────────────────────────┐
│  🎲 Roll Required                                     │
│                                                       │
│  DM requests: Damage Roll                            │
│  "You hit! Roll for damage."                         │
│                                                       │
│  1d8 + STR (+3)                                      │
│                                                       │
│  [        🎲 Roll Dice        ]                       │
│  — or —                                              │
│  I rolled: [   ]                                     │
│                                                       │
│  Your total: —                   [Submit Roll]       │
└───────────────────────────────────────────────────────┘
```

**Reaction overlay (full-screen interrupt on mobile):**
```
┌───────────────────────────────────────────────────────┐
│  ⚡ REACTION OPPORTUNITY                              │
│                                                       │
│  Thrain — Opportunity Attack                        │
│  "Vaelith is moving through your threatened square"  │
│                                                       │
│  Attack Roll: 1d20 + STR vs AC 15                    │
│                                                       │
│  [        🎲 Roll Dice        ]                       │
│  — or —                                              │
│  I rolled: [   ]                                     │
│                                                       │
│  [Decline Reaction]           [Submit Roll]          │
└───────────────────────────────────────────────────────┘
```

---

### 6l. Action Log — Timeline View

The action log is presented as a round-by-round timeline. In Exploration mode, beats replace rounds.

**Timeline structure:**
```
┌──────────────────────────────────────────────────────────────┐
│  SESSION LOG                                                 │
│                                                              │
│  ── Combat Round 2 ─────────────────────────────────── ▾ ── │
│                                                              │
│  ┌── Vaelith (19) ──────────────────────────────── 8:43pm ┐ │
│  │  ⚔ Melee Attack → Goblin Boss                          │ │
│  │  Roll: 14 + 3 + 2 = 19   DC: 17   ✓ SUCCESS            │ │
│  │  Goblin Boss: −8 HP (32 → 24)                          │ │
│  │  "Vaelith's blade finds a gap in the goblin's guard,   │ │
│  │   drawing a deep wound. The boss staggers back."        │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
│  ┌── Thrain (12) ───────────────────────────── ⚡ 8:45pm ┐  │
│  │  🛡 Opportunity Attack → Goblin Boss  [Reaction]       │  │
│  │  Roll: 11 + 4 = 15   DC: 15   ~ PARTIAL               │  │
│  │  Goblin Boss: −3 HP (24 → 21)                         │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                              │
│  ── Combat Round 1 ──────────────────────── [collapsed] ▸ ─ │
│                                                              │
│  ── Exploration: The Dark Forest ───────── [collapsed] ▸ ─  │
└──────────────────────────────────────────────────────────────┘
```

**Log card colour coding:**
- Left border: `--success` green for success, `--danger` red for failure, `--warning` amber for partial, `--info` blue for reactions
- Reaction entries are visually indented under their parent action
- Follow-up chain steps are collapsed by default; expand to see full roll sequence
- The DM sees a small gear icon on each resolved card to re-open and amend narrative (narrative-only edits allowed post-resolution; stat changes are not reversible without a new action)

---

## 7. Web Frontend (Nuxt 4)

### 7a. Auth & Navigation Changes

**Nuxt middleware (replaces per-page `onMounted` guards):**

```
ui/src/middleware/
  auth.ts              ← requires valid DM JWT; decodes exp; redirects /login
  player-auth.ts       ← requires player session token; redirects /join/[code]
  guest-only.ts        ← redirects /login → /games if already authenticated
```

**JWT expiry decode in `useApi.ts`:**
- On `loadSession()`: decode the JWT payload (base64), read `exp` unix timestamp
- If within 60 seconds of expiry: attempt silent refresh before continuing
- If expired: clear session, push to `/login`
- On any `401` response: same clear + redirect path

**Access token in memory; refresh in httpOnly cookie:**
- `useState('auth-token')` holds the short-lived access token (lost on page reload — triggers silent refresh from cookie)
- No `localStorage` for the DM JWT in v2.0

### 7b. Page Structure Changes

**New pages:**

| Route | Description |
|-------|-------------|
| `/campaigns` | Campaign list + create |
| `/campaigns/[id]` | Campaign dashboard (notes, games, schedule) |
| `/campaigns/[id]/notes` | World notes editor |
| `/campaigns/[id]/schedule` | Session scheduler calendar |
| `/schedule/[id]/rsvp` | Player RSVP page (accessible without DM auth) |

**Modified pages:**
- `/games` — now scoped under a Campaign; shows campaign picker at top
- `/sessions/[id]/dm` — adds Oracle Panel side drawer; splits into smaller components
- `/sessions/[code]/player` — adds Oracle Panel; push notification subscription prompt on first visit

### 7c. Component Architecture Changes

**`dm.vue` decomposition** (currently 1571 lines):

```
pages/sessions/[id]/dm.vue  (~200 lines — orchestrator only)
  ├── DmSessionShell.vue         (layout, sidebar, header)
  ├── DmActionWorkspace.vue      (action queue, log, resolve flow)
  ├── DmCombatWorkspace.vue      (combat + initiative — was DmCombatWorkflow.vue)
  ├── DmParticipantSidebar.vue   (character/NPC panels)
  └── OraclePanel.vue            (LLM assistant — shared with player)
```

**Removed components (orphans from audit):**
- `ActionForm.vue`
- `ActionEvaluationPanel.vue`
- `DmTurnPromptOverlay.vue`
- `RollChainStepRow.vue`
- `useDiceRollContext.ts`
- `DiceRoller.vue` (deprecated)

---

## 8. Mobile Frontend (Ionic + Vue)

### 8a. Architecture

The mobile app is a separate Ionic project that **shares Vue component logic** with the web app via a shared package, but uses Ionic's native UI components and Capacitor plugins for native device access.

```
/
├── api/                    ← ASP.NET Core (unchanged)
├── ui/src/                 ← Nuxt 4 web app
├── mobile/                 ← NEW: Ionic + Vue mobile app
│   ├── src/
│   │   ├── pages/          ← Ionic page components (mirror web routes)
│   │   ├── components/     ← Ionic-native UI wrappers
│   │   └── composables/    ← symlinked / re-exported from shared/
├── shared/                 ← NEW: shared business logic
│   ├── composables/        ← useApi, useSessionPolling (moved here from ui/src)
│   ├── types/              ← api.ts types (single source of truth)
│   └── utils/              ← ruleset, actionLog, rollPrompt utils
```

**Why not Nuxt in Ionic?** Ionic's router (`@ionic/vue-router`) and Nuxt's router conflict. The cleaner split: share composables + types via the `shared/` package; Ionic pages consume them directly without Nuxt overhead.

### 8b. Native Features (Capacitor Plugins)

| Feature | Plugin | Implementation |
|---------|--------|---------------|
| Push notifications | `@capacitor/push-notifications` | Register device token on login → stored in `push_device_tokens`; handle foreground + background |
| Haptics | `@capacitor/haptics` | Trigger `ImpactLight` on dice roll, `ImpactMedium` on combat turn change |
| Offline mode | `@capacitor/preferences` + service worker | Cache character sheet, notes, ruleset data; queue note edits when offline |
| Biometric (future) | `capacitor-biometric-authentication` | Optional — not in v2.0 scope |
| QR code join | `@capacitor/camera` + `jsQR` | Scan QR code from DM's screen to join session |

### 8c. Push Notification Flows

**Trigger events that dispatch pushes:**

| Event | Who receives | Message |
|-------|-------------|---------|
| Session scheduled by DM | All game participants | "Session scheduled for Saturday at 7 PM" |
| 24h before session | All RSVP'd participants | "Your session starts tomorrow at 7 PM" |
| It's your turn (combat) | Active player | "It's [character]'s turn in combat" |
| DM ends session | All players | "Session ended — view summary" |

**Device token lifecycle:**
1. On app launch after login: Capacitor requests permission
2. If granted: token registered with `POST /api/notifications/register`
3. Token rotated on each app launch (FCM/APNs requirement)
4. Token deleted on logout

### 8d. Offline Mode Design

**What is cached:**
- Character sheet (stats, inventory, abilities) — synced at session start
- Session notes written by this player
- Current ruleset definition (for dice rollers)

**Offline indicators:**
- Banner: "You're offline — changes will sync when reconnected"
- Note editor: local draft saved to `@capacitor/preferences`; synced on reconnect

**What is NOT available offline:**
- Live session play (action submission, combat)
- DM tools
- Oracle LLM queries

### 8e. Mobile Screen Designs

**Player home screen:**
```
┌────────────────────────────────┐
│  ◀ TTRPG Table       [🔔] [⚙] │
│                                │
│  Your Games                    │
│  ┌──────────────────────────┐  │
│  │ The Shattered Realms     │  │
│  │ Vaelith · Wood Elf Ranger│  │
│  │ Next session: Sat 7 PM  →│  │
│  └──────────────────────────┘  │
│  ┌──────────────────────────┐  │
│  │ Space Opera: Broken Stars│  │
│  │ Kai · Human Pilot        │  │
│  │ No session scheduled    →│  │
│  └──────────────────────────┘  │
│                                │
│  [Join a Session]              │
│  [Scan QR Code]                │
└────────────────────────────────┘
```

**Active session (player) — mobile:**
```
┌────────────────────────────────┐
│  ◀ Session  COMBAT RD 2   [⚡] │
│                                │
│  ┌──────────────────────────┐  │
│  │ YOUR TURN                │  │
│  │ Vaelith — Round 2        │  │
│  └──────────────────────────┘  │
│                                │
│  ┌──── Choose Action ────────┐ │
│  │  ⚔ Attack                 │ │
│  │  🏃 Disengage             │ │
│  │  🎯 Precise Shot          │ │
│  │  ✨ Custom action...      │ │
│  └───────────────────────────┘ │
│                                │
│  [Character]  [Notes]  [Oracle]│
└────────────────────────────────┘
```

**DM view — mobile (condensed):**
```
┌────────────────────────────────┐
│  ◀ DM View           [END]     │
│                                │
│  ┌──── Initiative ───────────┐ │
│  │ 1. Vaelith (18) ← current │ │
│  │ 2. Goblin Boss (15)       │ │
│  │ 3. Thrain (12)            │ │
│  └───────────────────────────┘ │
│                                │
│  Pending Actions (2)           │
│  ● Vaelith: "I attack the boss"│
│  ● Thrain: "I cast Shield"     │
│                                │
│  [Resolve] [Skip] [Add NPC]    │
│                                │
│  [Participants][Log][Oracle]   │
└────────────────────────────────┘
```

---

## 9. UI/UX Design System

### 9a. Design Direction: Hybrid Thematic

**Philosophy:** Clean, neutral base (works for any genre); thematic accents driven by the active ruleset's colour palette (already partially supported by `useRulesetTheme`). Dark mode by default; light mode optional.

### 9b. Design Tokens

```
Base palette (neutral dark):
  --surface-0: #0d0d0f        (app background)
  --surface-1: #16161a        (card / panel background)
  --surface-2: #1e1e24        (elevated element)
  --surface-3: #2a2a33        (hover / selected)
  --border:    #2e2e38        (dividers)
  --text-1:    #f0f0f5        (primary text)
  --text-2:    #9898a8        (secondary / muted)
  --text-3:    #5a5a6e        (placeholder / disabled)

Accent (ruleset-injected via CSS vars — default D&D gold):
  --accent:       #c9a227     (primary action)
  --accent-hover: #e0b53a
  --accent-dim:   rgba(201,162,39,0.15)

Semantic colours:
  --success: #3d9970
  --warning: #e07b39
  --danger:  #e05252
  --info:    #4a90d9

Combat / status ring colours:
  --hp-full:    #3d9970
  --hp-medium:  #e07b39
  --hp-low:     #e05252
  --turn-active: var(--accent)
```

### 9c. Typography

```
Headings:  "Cinzel" (serif, thematic) — h1, h2 session/campaign titles only
Body:      "Inter" (sans-serif) — all UI text, forms, notes
Mono:      "JetBrains Mono" — dice rolls, stat values, code/JSON views
```

### 9d. Key UI Patterns

**Cards:** Consistent `--surface-1` cards with `1px solid var(--border)` border, `8px` radius, `--shadow-sm`. No heavy gradients except on ruleset-accent headings.

**Status indicators:** Coloured left-border on cards (green = active session, amber = scheduled, red = ended, grey = draft).

**Action Queue:** Horizontal scrollable pill list on mobile; vertical sidebar on desktop. Each pill shows character avatar + truncated action text + resolve button.

**Toast notifications:** Bottom-right on desktop; bottom-center on mobile; auto-dismiss in 4 s; role-based colours.

**Loading states:** Skeleton shimmer blocks (existing `SkeletonBlock.vue`) — expand to cover all data-heavy panels.

**Oracle Panel:** Right-side drawer; 380px wide on desktop; full-screen bottom sheet on mobile. Character-themed header colour from ruleset accent.

### 9e. Ruleset Theming (extended from v1.0)

The ruleset JSON will support an optional `theme` block:

```json
{
  "name": "D&D 5e",
  "theme": {
    "accent": "#c9a227",
    "accentText": "#0d0d0f",
    "backgroundPattern": "parchment",
    "diceFaceColor": "#8b1a1a",
    "combatBorderColor": "#8b1a1a"
  }
}
```

`useRulesetTheme` injects these as CSS custom properties on the `:root` at session start, reverting when the session ends.

---

## 10. Infrastructure & Deployment

### 10a. Repository Structure

```
/
├── api/                        ← Backend API (ASP.NET Core 8)
│   ├── src/NotesApi/
│   └── tests/
├── ui/                         ← Web BFF + Frontend (Nuxt 4)
│   └── src/
│       ├── pages/
│       ├── components/
│       ├── composables/
│       └── server/             ← BFF server layer (Nuxt server routes)
│           └── api/
├── llm/                        ← LLM Service (Python / FastAPI)
│   ├── main.py
│   ├── rag/
│   ├── requirements.txt
│   └── Dockerfile
├── mobile/                     ← Ionic mobile app
│   └── src/
├── shared/                     ← Shared types and composables (npm package)
│   ├── types/                  ← api.ts — single source of truth for DTOs
│   └── composables/            ← useApi, useSessionPolling (consumed by ui + mobile)
├── docker-compose.yml          ← Dev orchestration
├── .env.example
└── DESIGN_V2.md
```

### 10b. Docker Compose (dev)

```yaml
services:
  # ── Backend API ──────────────────────────────────────────
  api:
    build: ./api
    ports: ["5294:5294"]
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=ttrpg;Username=ttrpg;Password=${POSTGRES_PASSWORD}
      - Jwt__Key=${JWT_KEY}
      - Services__LlmBaseUrl=http://llm:8000
      - Services__RedisConnection=redis:6379
    depends_on: [postgres, redis]

  # ── Web BFF (Nuxt 4) ─────────────────────────────────────
  ui:
    build: ./ui
    ports: ["3000:3000"]
    environment:
      - NUXT_API_BASE_URL=http://api:5294
      - NUXT_LLM_BASE_URL=http://llm:8000
      - NUXT_SERVICE_JWT_SECRET=${SERVICE_JWT_SECRET}

  # ── LLM Service ──────────────────────────────────────────
  llm:
    build: ./llm
    ports: ["8000:8000"]
    environment:
      - OLLAMA_BASE_URL=http://ollama:11434
      - DATABASE_URL=postgresql://ttrpg:${POSTGRES_PASSWORD}@postgres:5432/ttrpg
      - SERVICE_JWT_SECRET=${SERVICE_JWT_SECRET}
    depends_on: [postgres, ollama]

  # ── Ionic mobile (dev server only) ───────────────────────
  mobile-dev:
    image: node:22
    volumes: ["./mobile:/app", "./shared:/shared"]
    working_dir: /app
    command: npm run dev
    ports: ["8100:8100"]
    environment:
      - VITE_API_BASE_URL=http://localhost:5294

  # ── Infrastructure ────────────────────────────────────────
  postgres:
    image: pgvector/pgvector:pg16
    environment:
      POSTGRES_DB: ttrpg
      POSTGRES_USER: ttrpg
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes: ["postgres_data:/var/lib/postgresql/data"]
    ports: ["5432:5432"]

  redis:
    image: redis:7-alpine
    command: redis-server --appendonly yes --requirepass ${REDIS_PASSWORD}
    ports: ["6379:6379"]

  ollama:
    image: ollama/ollama
    volumes: ["ollama_models:/root/.ollama"]
    ports: ["11434:11434"]
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]

volumes:
  postgres_data:
  ollama_models:
```

### 10c. Production Deployment

Each service deploys independently. WebSocket-capable hosting is required for the Backend API.

| Service | Recommended host | Reason |
|---------|-----------------|--------|
| Backend API | Fly.io (2 shared CPU VMs, `fly scale count 2`) | Native WebSocket / SignalR support; easy horizontal scale |
| Web BFF (Nuxt) | Fly.io or Vercel (Node runtime) | SSR needs a Node server runtime; Vercel edge has cold-start trade-offs |
| LLM Service | Hetzner GPU VPS (CAX41 + L4 GPU, ~€60/mo) | Persistent GPU; Ollama models cached to disk; scale down when idle |
| PostgreSQL | Neon or Supabase (managed PG 16 + pgvector) | Managed backups; pgvector supported on both; connection pooling via PgBouncer |
| Redis | Upstash Redis (serverless) | Free tier viable; auto-sleep; Redis 7 compatible |
| FCM / APNs | Google Firebase (free) | Push notifications |

**Subdomain routing (Caddy / Nginx):**
```
app.ttrpg.io        → Web BFF (Nuxt SSR)        port 3000
api.ttrpg.io        → Backend API REST           port 5294
api.ttrpg.io/hubs   → Backend API SignalR        port 5294 (WS upgrade)
llm.ttrpg.io        → LLM Service (internal only, not public)
```

The LLM Service is **not exposed publicly**. Only the Web BFF and Backend API can reach it via internal network. The `llm.ttrpg.io` subdomain is DNS-private.

### 10d. Secrets Management

- All secrets in environment variables — never in source
- `dotnet user-secrets` for local Backend API dev
- `.env` file at project root (gitignored) for Docker Compose dev; `.env.example` committed
- Managed secrets (Fly.io secrets / Vercel env) for production
- **Required secrets:**
  - `JWT_KEY` — minimum 64 random characters; rotate quarterly
  - `SERVICE_JWT_SECRET` — used for BFF → API and BFF → LLM inter-service auth
  - `POSTGRES_PASSWORD` — random 32 chars
  - `REDIS_PASSWORD` — random 32 chars
  - `GOOGLE_CALENDAR_CLIENT_SECRET` — from Google Cloud Console
  - `DISCORD_WEBHOOK_SECRETS` — per-game, stored in DB encrypted at rest

---

## 11. Migration Path from v1.0

### Phase 1 — Security & Polish (1–2 weeks)
Apply all v1.0 audit fixes. No new features; no architecture changes yet.
- Enable account lockout, rate limiting, HTTPS + HSTS
- Fix N+1 query in AdminController; batch stat-change saves; async NPC query
- Remove dead code (orphan components, unused vars, `JoinUrl`, `RulesetDetailResponse`)
- Add Nuxt route middleware (`auth.ts`, `player-auth.ts`)
- Add JWT expiry decode in `useApi.ts`; fix `localStorage` token storage
- Standardise error response shape across all controllers

### Phase 2 — Action & Session Redesign (2–3 weeks)
Rebuild the action submission and resolution system (Section 6) on the existing codebase before migrating infrastructure.
- Redesign `ActionRequest` table schema (new columns: `status`, `chain_step`, `parent_action_id`, etc.)
- Implement server-side dice roll endpoint (`POST /api/dice/roll`)
- Implement action state machine in `ActionsController`
- Build new `ActionBuilder` Vue component (step wizard)
- Build `DmResolutionWorkspace` component
- Build reaction and follow-up roll flows

### Phase 3 — Database & Real-Time (2–3 weeks)
- Write EF Core migrations to replace `ApplySchemaUpdatesAsync`
- Add PostgreSQL support; run migrations against dev SQLite first to verify
- Add `refresh_tokens` table; implement rotate-on-refresh
- Replace all polling endpoints with SignalR `SessionHub` and `NotificationHub`
- Update client composables to use SignalR SDK instead of polling
- Stand up Redis for SignalR backplane in Docker Compose

### Phase 4 — BFF Extraction & Service Split (1–2 weeks)
Extract the Web BFF server layer and spin up the LLM service as a separate container.
- Expand Nuxt `server/api/` into the full BFF route structure (aggregation routes, auth cookie handling)
- Add service-to-service JWT between BFF ↔ API and BFF ↔ LLM Service
- Create `llm/` Python service; wire Ollama + FastAPI + pgvector RAG
- Add `shared/` package; move `types/api.ts` and core composables there
- Update Docker Compose to four-service topology

### Phase 5 — Campaign & Scheduler (2–3 weeks)
- Add `campaigns`, `campaign_notes`, `scheduled_sessions` migrations
- Build Campaign CRUD API and dashboard UI
- Build Scheduler API; add Google Calendar OAuth flow
- Add Discord webhook dispatcher in background worker
- Build in-app RSVP UI
- Surface Oracle Panel in DM and player session views

### Phase 6 — Ionic Mobile App (3–4 weeks)
- Create `mobile/` project; wire Capacitor plugins (push, haptics)
- Build core mobile pages consuming `shared/` composables: home, session player, character sheet
- Implement push notification device token registration
- Build offline character sheet cache with sync-on-reconnect
- Add haptic feedback on dice roll and combat turn events
- iOS/Android build + App Store / Play Store submission

### Data Migration (runs alongside Phase 3)
- Export v1.0 SQLite to SQL dump
- Migration script wraps all existing games in a default "Imported Campaign" per user
- Existing player tokens marked `ExpiresAt = NOW()` (forced re-join on first access)
- Ruleset JSON files re-ingested through LLM Service to generate embeddings

---

## 12. Open Questions & Future Scope

| Question | Decision needed |
|----------|----------------|
| Will the LLM answer player questions without DM oversight, or should DM approve? | Design choice |
| Should Oracle query logs be visible to DM (show what players asked)? | Privacy consideration |
| Is Discord integration via webhooks sufficient or do you want a full bot (slash commands, player commands in Discord)? | Scope decision |
| Should iCal export be added alongside Google Calendar in v2.0? | Easy add if desired |
| Offline note sync conflict resolution strategy (last-write-wins vs. diff/merge)? | UX decision |
| v2.0 auth still local-only — is social login (Google / Discord) planned for v3? | Roadmap |

### Future Scope (v3 ideas)
- **Battle map:** tactical grid with fog of war; token drag-and-drop
- **Session replay:** cinematic playback of the action log with dice animations
- **Spectator mode:** read-only live view for audience/stream
- **Co-DM support:** delegate session ownership to a second DM
- **Ruleset marketplace:** share/publish custom rulesets to community
- **Paid tiers:** campaign limits, AI query quotas, private rulesets
