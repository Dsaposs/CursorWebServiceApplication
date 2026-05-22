/**
 * BFF route: POST /api/bff/oracle
 *
 * Forwards a ruleset query to the LLM microservice, injecting the current session
 * context (session ID, ruleset code) from the server side to avoid the client
 * needing to pass it.
 *
 * The LLM service URL is set via NUXT_LLM_BASE_URL environment variable.
 * Falls back to a "service unavailable" response when the variable is not set.
 */
import { defineEventHandler, readBody, getHeader, createError, setResponseStatus } from 'h3';

interface OracleRequest {
  question: string;
  sessionId?: string;
  rulesetCode?: string;
}

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  const llmBase = config.llmBaseUrl;

  if (!llmBase) {
    throw createError({
      statusCode: 503,
      statusMessage: 'Oracle service is not configured in this environment.',
    });
  }

  const body = await readBody<OracleRequest>(event);
  if (!body?.question?.trim()) {
    throw createError({ statusCode: 400, statusMessage: 'question is required.' });
  }

  const authHeader = getHeader(event, 'authorization') ?? '';
  const playerToken = getHeader(event, 'x-player-token') ?? '';

  try {
    const result = await $fetch<{ answer: string; sources?: string[] }>(`${llmBase}/query`, {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        ...(authHeader ? { authorization: authHeader } : {}),
        ...(playerToken ? { 'x-player-token': playerToken } : {}),
      },
      body: {
        question: body.question,
        session_id: body.sessionId,
        ruleset_code: body.rulesetCode,
      },
    });

    setResponseStatus(event, 200);
    return result;
  } catch (error) {
    const err = error as { status?: number; data?: unknown };
    throw createError({
      statusCode: err.status ?? 502,
      statusMessage: 'Oracle service request failed.',
      data: err.data,
    });
  }
});
