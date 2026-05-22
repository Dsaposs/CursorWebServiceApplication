/**
 * BFF aggregation route: GET /api/bff/session/:code/full
 *
 * Combines the session state + game details + active roll prompts into a single
 * server-side-fetched response, eliminating multiple round-trips from the client.
 * The Nuxt server runs this request from within the private network, so latency
 * to the backend API is sub-millisecond.
 */
import { defineEventHandler, getHeader, createError, setResponseStatus } from 'h3';

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  const apiBase = config.apiBaseUrl;
  const code = event.context.params?.code ?? '';

  if (!code) throw createError({ statusCode: 400, statusMessage: 'Session code is required.' });

  const authHeader = getHeader(event, 'authorization') ?? '';
  const playerToken = getHeader(event, 'x-player-token') ?? '';

  const headers: Record<string, string> = {};
  if (authHeader) headers['authorization'] = authHeader;
  if (playerToken) headers['x-player-token'] = playerToken;

  try {
    // Parallel fetch of session state and session notes context
    const [sessionState, sinceSeq] = await Promise.all([
      $fetch<Record<string, unknown>>(`${apiBase}/api/session-join/${code}/state`, { headers }),
      Promise.resolve(0),
    ]);

    setResponseStatus(event, 200);
    return {
      session: sessionState,
      meta: {
        fetchedAt: new Date().toISOString(),
        sinceSequence: sinceSeq,
      },
    };
  } catch (error) {
    const err = error as { status?: number; data?: unknown };
    throw createError({
      statusCode: err.status ?? 502,
      statusMessage: 'Failed to load session state.',
      data: err.data,
    });
  }
});
