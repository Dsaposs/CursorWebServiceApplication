import { scientistCharacterBuild } from './config.js';

export interface AuthResponse {
  token: string;
  expiresAt: string;
}

export interface GameResponse {
  id: string;
  name: string;
  inviteCode: string;
}

export interface SessionSummaryResponse {
  id: string;
  gameId: string;
  joinCode: string;
  version: number;
}

export interface JoinGameResponse {
  participantToken: string;
  character: { id: string; name: string };
}

export interface ActionQueueItemResponse {
  id: string;
  status: string;
  actionText: string;
}

export interface PlayerFixture {
  token: string;
  characterId: string;
  characterName: string;
}

export interface PerfFixture {
  createdAt: string;
  apiUrl: string;
  uiUrl: string;
  gameId: string;
  gameName: string;
  sessionId: string;
  joinCode: string;
  dmToken: string;
  players: PlayerFixture[];
}

async function request<T>(apiUrl: string, path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(`${apiUrl}${path}`, init);
  if (!response.ok) {
    const body = await response.text();
    throw new Error(`${init.method ?? 'GET'} ${path} failed (${response.status}): ${body}`);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export async function login(apiUrl: string, email: string, password: string): Promise<AuthResponse> {
  return request<AuthResponse>(apiUrl, '/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
}

export async function createGame(apiUrl: string, token: string, name: string) {
  return request<GameResponse>(apiUrl, '/api/games', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ name, rulesetCode: 'alien-rpg' }),
  });
}

export async function startSession(apiUrl: string, token: string, gameId: string) {
  return request<SessionSummaryResponse>(apiUrl, `/api/games/${gameId}/sessions`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({}),
  });
}

export async function joinSession(
  apiUrl: string,
  joinCode: string,
  characterName: string,
  playerName: string,
) {
  return request<JoinGameResponse>(apiUrl, `/api/session-join/${joinCode}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      characterName,
      playerName,
      ...scientistCharacterBuild,
    }),
  });
}

export async function submitPlayerAction(
  apiUrl: string,
  joinCode: string,
  playerToken: string,
  actionText: string,
) {
  return request<ActionQueueItemResponse>(apiUrl, `/api/sessions/${joinCode}/actions`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Player-Token': playerToken,
    },
    body: JSON.stringify({ actionText }),
  });
}

export async function resolveAction(
  apiUrl: string,
  dmToken: string,
  actionId: string,
  resolutionText: string,
) {
  return request<ActionQueueItemResponse>(apiUrl, `/api/actions/${actionId}/resolve`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${dmToken}`,
    },
    body: JSON.stringify({ resolutionText, statChanges: [] }),
  });
}

export async function getSessionVersion(apiUrl: string, sessionId: string, dmToken: string) {
  return request<{ version: number }>(apiUrl, `/api/sessions/${sessionId}/version`, {
    headers: { Authorization: `Bearer ${dmToken}` },
  });
}

export async function getSessionLive(
  apiUrl: string,
  sessionId: string,
  dmToken: string,
  sinceSequence = 0,
) {
  const query = sinceSequence > 0 ? `?sinceSequence=${sinceSequence}` : '';
  return request<Record<string, unknown>>(apiUrl, `/api/sessions/${sessionId}/live${query}`, {
    headers: { Authorization: `Bearer ${dmToken}` },
  });
}

export async function getPlayerVersion(apiUrl: string, joinCode: string, playerToken: string) {
  return request<{ version: number }>(apiUrl, `/api/session-join/${joinCode}/version`, {
    headers: { 'X-Player-Token': playerToken },
  });
}

export async function getPlayerLive(
  apiUrl: string,
  joinCode: string,
  playerToken: string,
  sinceSequence = 0,
) {
  const query = sinceSequence > 0 ? `?sinceSequence=${sinceSequence}` : '';
  return request<Record<string, unknown>>(apiUrl, `/api/session-join/${joinCode}/live${query}`, {
    headers: { 'X-Player-Token': playerToken },
  });
}

export async function createNpc(apiUrl: string, dmToken: string, gameId: string, name: string) {
  return request<{ id: string }>(apiUrl, `/api/games/${gameId}/npcs`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${dmToken}`,
    },
    body: JSON.stringify({
      templateKey: 'xenomorphDrone',
      name,
      maxHealth: 10,
      health: 10,
    }),
  });
}
