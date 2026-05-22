/**
 * Minimal API composable for the mobile client.
 * Uses $fetch against the public API base URL from runtimeConfig.
 */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

const TOKEN_KEY = 'ttrpg_token';
const REFRESH_KEY = 'ttrpg_refresh_token';
const PLAYER_TOKEN_KEY = 'ttrpg_player_token';

interface ApiCallOptions {
  method?: string;
  body?: unknown;
  playerToken?: boolean;
}

const authToken = () => useState<string | null>('auth-token', () => null);
const authEmail = () => useState<string>('auth-email', () => '');

export function decodeJwtExp(token: string): number | null {
  try {
    const payload = token.split('.')[1];
    const decoded = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
    return typeof decoded.exp === 'number' ? decoded.exp : null;
  } catch {
    return null;
  }
}

export function useApi() {
  const config = useRuntimeConfig();
  const base = config.public.apiBaseUrl as string;

  const token = authToken();
  const email = authEmail();

  function loadSession() {
    if (import.meta.client) {
      const stored = localStorage.getItem(TOKEN_KEY);
      if (stored) {
        token.value = stored;
      }
    }
  }

  function setSession(jwt: string, userEmail: string, refreshToken?: string) {
    token.value = jwt;
    email.value = userEmail;
    if (import.meta.client) {
      localStorage.setItem(TOKEN_KEY, jwt);
      if (refreshToken) localStorage.setItem(REFRESH_KEY, refreshToken);
    }
  }

  function clearSession() {
    token.value = null;
    email.value = '';
    if (import.meta.client) {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(REFRESH_KEY);
    }
  }

  async function api<T>(path: string, opts: ApiCallOptions = {}): Promise<T> {
    const headers: Record<string, string> = {};
    if (token.value) headers.Authorization = `Bearer ${token.value}`;
    if (opts.playerToken && import.meta.client) {
      const playerToken = localStorage.getItem(PLAYER_TOKEN_KEY);
      if (playerToken) headers['X-Player-Token'] = playerToken;
    }

    try {
      return await $fetch<T>(`${base}${path}`, {
        method: (opts.method as never) ?? 'GET',
        body: opts.body as never,
        headers,
      });
    } catch (err: unknown) {
      const fe = err as { status?: number; statusCode?: number; message?: string };
      const status = fe.status ?? fe.statusCode ?? 0;
      throw new ApiError(status, fe.message ?? 'Request failed');
    }
  }

  return { api, token, email, loadSession, setSession, clearSession };
}
