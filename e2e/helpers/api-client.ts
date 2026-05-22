import { readAdminAuthCache } from './auth-cache';
import { scientistCharacterBuild, uniqueLabel } from './test-data';

export interface ApiClientOptions {
  apiUrl: string;
  token?: string;
  playerToken?: string;
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  refreshToken?: string;
}

export interface GameResponse {
  id: string;
  name: string;
  rulesetCode: string;
  inviteCode: string;
  inviteUrl: string;
}

export interface SessionSummaryResponse {
  id: string;
  gameId: string;
  joinCode: string;
  joinUrl: string;
  isActive: boolean;
  state: string;
  version: number;
}

export interface JoinGameResponse {
  participantToken: string;
  character: { id: string; name: string; classKey: string };
  game: { id: string };
}

export interface ActionQueueItemResponse {
  id: string;
  actionText: string;
  status: string;
  actorName: string;
  actorCharacterId?: string;
  isSkillCheckResponse?: boolean;
  resolutionText?: string | null;
}

export interface NpcResponse {
  id: string;
  name: string;
  kind: string;
  maxHealth: number;
  health: number;
  armor: number;
  statBlockJson: string;
}

export interface GameDetailResponse {
  id: string;
  name: string;
  npcsAndMonsters: NpcResponse[];
  characters: Array<{ id: string; name: string }>;
}

export interface RollPromptResponse {
  id: string;
  targetCharacterId: string;
  targetCharacterName: string;
  status: string;
  resultActionRequestId?: string | null;
}

export interface RegisterResponse {
  userId: string;
  email: string;
}

export interface CreateNpcPayload {
  templateKey?: string;
  name: string;
  kind?: string;
  maxHealth?: number;
  health?: number;
  armor?: number;
  statBlockJson?: string;
}

export interface SessionRollPromptPayload {
  prompts: Array<{
    targetCharacterId: string;
    checkMode: string;
    skillKey?: string;
    attributeKey?: string;
    promptLabel?: string;
    resultKind?: string;
  }>;
}

export class ApiClient {
  constructor(private readonly options: ApiClientOptions) {}

  private headers(includeJson = true): HeadersInit {
    const headers: Record<string, string> = {};
    if (includeJson) headers['Content-Type'] = 'application/json';
    if (this.options.token) headers.Authorization = `Bearer ${this.options.token}`;
    if (this.options.playerToken) headers['X-Player-Token'] = this.options.playerToken;
    return headers;
  }

  private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const response = await fetch(`${this.options.apiUrl}${path}`, {
      ...init,
      headers: {
        ...this.headers(init.body !== undefined),
        ...(init.headers ?? {}),
      },
    });

    if (!response.ok) {
      const body = await response.text();
      throw new Error(`${init.method ?? 'GET'} ${path} failed (${response.status}): ${body}`);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return response.json() as Promise<T>;
  }

  static async login(apiUrl: string, email: string, password: string, attempt = 0): Promise<AuthResponse> {
    const response = await fetch(`${apiUrl}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });

    if (response.status === 429 && attempt < 5) {
      await new Promise(resolve => setTimeout(resolve, 8_000));
      return ApiClient.login(apiUrl, email, password, attempt + 1);
    }

    if (!response.ok) {
      const body = await response.text();
      throw new Error(`Login failed (${response.status}): ${body}`);
    }

    return response.json() as Promise<AuthResponse>;
  }

  static async register(apiUrl: string, email: string, password: string) {
    const response = await fetch(`${apiUrl}/api/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });

    if (!response.ok) {
      const body = await response.text();
      throw new Error(`Register failed (${response.status}): ${body}`);
    }

    return response.json() as Promise<RegisterResponse>;
  }

  getRulesets() {
    return this.request<Array<{ code: string; displayName: string }>>('/api/rulesets');
  }

  getGame(gameId: string) {
    return this.request<GameDetailResponse>(`/api/games/${gameId}`);
  }

  createGame(name: string, rulesetCode = 'alien-rpg') {
    return this.request<GameResponse>('/api/games', {
      method: 'POST',
      body: JSON.stringify({ name, rulesetCode }),
    });
  }

  startSession(gameId: string) {
    return this.request<SessionSummaryResponse>(`/api/games/${gameId}/sessions`, {
      method: 'POST',
      body: JSON.stringify({}),
    });
  }

  getDmSession(sessionId: string, sinceSequence = 0) {
    const query = sinceSequence > 0 ? `?sinceSequence=${sinceSequence}` : '';
    return this.request<Record<string, unknown>>(`/api/sessions/${sessionId}/dm${query}`);
  }

  getSessionVersion(sessionId: string) {
    return this.request<{ version: number; updatedAt: string }>(`/api/sessions/${sessionId}/version`);
  }

  getSessionLive(sessionId: string, sinceSequence = 0) {
    const query = sinceSequence > 0 ? `?sinceSequence=${sinceSequence}` : '';
    return this.request<Record<string, unknown>>(`/api/sessions/${sessionId}/live${query}`);
  }

  joinSession(joinCode: string, characterName: string, playerName: string) {
    return this.request<JoinGameResponse>(`/api/session-join/${joinCode}`, {
      method: 'POST',
      body: JSON.stringify({
        characterName,
        playerName,
        ...scientistCharacterBuild,
      }),
    });
  }

  submitPlayerAction(joinCode: string, actionText: string, actionKey?: string) {
    return this.request<ActionQueueItemResponse>(`/api/sessions/${joinCode}/actions`, {
      method: 'POST',
      body: JSON.stringify({ actionText, actionKey }),
    });
  }

  resolveAction(actionId: string, resolutionText: string) {
    return this.request<ActionQueueItemResponse>(`/api/actions/${actionId}/resolve`, {
      method: 'PUT',
      body: JSON.stringify({ resolutionText, statChanges: [] }),
    });
  }

  setSessionMode(sessionId: string, state: 'Exploration' | 'Combat') {
    return this.request<SessionSummaryResponse>(`/api/sessions/${sessionId}/state`, {
      method: 'POST',
      body: JSON.stringify({ state }),
    });
  }

  startCombat(sessionId: string) {
    return this.request<{ guidanceText?: string | null }>(`/api/sessions/${sessionId}/combat/start`, {
      method: 'POST',
      body: JSON.stringify({}),
    });
  }

  stopSession(sessionId: string) {
    return this.request<SessionSummaryResponse>(`/api/sessions/${sessionId}/stop`, {
      method: 'POST',
      body: JSON.stringify({}),
    });
  }

  createNpc(gameId: string, payload: CreateNpcPayload) {
    return this.request<NpcResponse>(`/api/games/${gameId}/npcs`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  }

  updateNpc(gameId: string, npcId: string, payload: CreateNpcPayload) {
    return this.request<NpcResponse>(`/api/games/${gameId}/npcs/${npcId}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  }

  deleteNpc(gameId: string, npcId: string) {
    return this.request<void>(`/api/games/${gameId}/npcs/${npcId}`, {
      method: 'DELETE',
    });
  }

  createSessionRollPrompts(sessionId: string, payload: SessionRollPromptPayload) {
    return this.request<RollPromptResponse[]>(`/api/sessions/${sessionId}/roll-prompts`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  }

  submitRollPrompt(promptId: string, rollSummary: string, rollResultJson = '{}') {
    return this.request<RollPromptResponse>(`/api/roll-prompts/${promptId}/submit`, {
      method: 'PUT',
      body: JSON.stringify({ rollSummary, rollResultJson, pushed: false }),
    });
  }

  cancelRollPrompt(promptId: string) {
    return this.request<void>(`/api/roll-prompts/${promptId}`, {
      method: 'DELETE',
    });
  }
}

export interface SessionFixture {
  gameName: string;
  gameId: string;
  sessionId: string;
  joinCode: string;
  joinUrl: string;
  dmToken: string;
}

export async function createSessionFixture(apiUrl: string, dmToken: string): Promise<SessionFixture> {
  const client = new ApiClient({ apiUrl, token: dmToken });
  const gameName = uniqueLabel('E2E Campaign');
  const game = await client.createGame(gameName, 'alien-rpg');
  const session = await client.startSession(game.id);

  return {
    gameName,
    gameId: game.id,
    sessionId: session.id,
    joinCode: session.joinCode,
    joinUrl: session.joinUrl,
    dmToken,
  };
}

export async function loginAdmin(apiUrl: string, email: string, password: string) {
  const cached = readAdminAuthCache();
  if (cached?.token) {
    return cached.token;
  }

  const auth = await ApiClient.login(apiUrl, email, password);
  return auth.token;
}
