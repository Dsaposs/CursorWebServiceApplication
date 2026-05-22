/**
 * BFF aggregation route: GET /api/bff/games
 *
 * Returns the games list with session summary counts pre-calculated server-side.
 * Adds a `sessionCount` and `activeSession` summary to each game so the dashboard
 * can render without a second round-trip per game.
 */
import { defineEventHandler, getHeader, createError } from 'h3';

interface GameResponse {
  id: string;
  name: string;
  sessions?: Array<{ isActive: boolean; joinCode: string; startedAt: string }>;
  [key: string]: unknown;
}

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  const apiBase = config.apiBaseUrl;

  const authHeader = getHeader(event, 'authorization') ?? '';
  if (!authHeader) throw createError({ statusCode: 401, statusMessage: 'Unauthorized.' });

  try {
    const games = await $fetch<GameResponse[]>(`${apiBase}/api/games`, {
      headers: { authorization: authHeader },
    });

    return games.map(game => ({
      ...game,
      sessionCount: game.sessions?.length ?? 0,
      activeSession: game.sessions?.find(s => s.isActive) ?? null,
    }));
  } catch (error) {
    const err = error as { status?: number; data?: unknown };
    throw createError({
      statusCode: err.status ?? 502,
      statusMessage: 'Failed to load games.',
      data: err.data,
    });
  }
});
