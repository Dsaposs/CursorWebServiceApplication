type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE';

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

interface ApiOptions {
  method?: HttpMethod;
  body?: unknown;
  playerToken?: string | null;
}

const authToken = () => useState<string | null>('auth-token', () => null);
const authEmail = () => useState<string>('auth-email', () => '');

const REFRESH_TOKEN_KEY = 'ttrpg_refresh_token';

/**
 * Decodes the `exp` claim from a JWT without any external library.
 * Returns the expiry as a Unix timestamp in seconds, or null if unparseable.
 */
export function decodeJwtExp(token: string): number | null {
  try {
    const payload = token.split('.')[1];
    if (!payload) return null;
    const padded = payload.replace(/-/g, '+').replace(/_/g, '/');
    const decoded = atob(padded);
    const parsed = JSON.parse(decoded);
    return typeof parsed.exp === 'number' ? parsed.exp : null;
  } catch {
    return null;
  }
}

/**
 * Returns true if the token is expired or within 60 seconds of expiry.
 */
export function isTokenExpired(token: string): boolean {
  const exp = decodeJwtExp(token);
  if (exp === null) return true;
  return Date.now() / 1000 >= exp - 60;
}

export function useApi() {
  const token = authToken();
  const email = authEmail();

  function loadSession() {
    if (!import.meta.client) return;
    const stored = localStorage.getItem('ttrpg_token');
    if (stored && !isTokenExpired(stored)) {
      token.value = stored;
      email.value = localStorage.getItem('ttrpg_email') || '';
    } else if (stored) {
      // Token is expired but we may have a refresh token — try a silent refresh
      localStorage.removeItem('ttrpg_token');
      token.value = null;
      email.value = '';
      const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
      if (refreshToken) {
        void silentRefresh(refreshToken);
      }
    }
  }

  async function silentRefresh(refreshToken: string): Promise<boolean> {
    if (!import.meta.client) return false;
    try {
      const result = await $fetch<{ token: string; expiresAt: string; refreshToken: string }>('/api/auth/refresh', {
        method: 'POST',
        body: { refreshToken },
      });
      const storedEmail = localStorage.getItem('ttrpg_email') || '';
      setSession(result.token, storedEmail, result.refreshToken);
      return true;
    } catch {
      clearSession();
      return false;
    }
  }

  function setSession(nextToken: string, nextEmail: string, nextRefreshToken?: string) {
    token.value = nextToken;
    email.value = nextEmail;
    if (import.meta.client) {
      localStorage.setItem('ttrpg_token', nextToken);
      localStorage.setItem('ttrpg_email', nextEmail);
      if (nextRefreshToken) {
        localStorage.setItem(REFRESH_TOKEN_KEY, nextRefreshToken);
      }
    }
  }

  function clearSession() {
    token.value = null;
    email.value = '';
    if (import.meta.client) {
      localStorage.removeItem('ttrpg_token');
      localStorage.removeItem('ttrpg_email');
      localStorage.removeItem(REFRESH_TOKEN_KEY);
    }
  }

  async function api<T>(path: string, options: ApiOptions = {}) {
    // Proactively refresh the JWT before making a request if it's about to expire
    if (import.meta.client && token.value && isTokenExpired(token.value)) {
      const rt = localStorage.getItem(REFRESH_TOKEN_KEY);
      if (rt) {
        const refreshed = await silentRefresh(rt);
        if (!refreshed) {
          await navigateTo('/login');
          throw new ApiError(401, 'Session expired. Please log in again.');
        }
      }
    }

    const headers = new Headers();

    if (token.value) {
      headers.set('Authorization', `Bearer ${token.value}`);
    }

    if (options.playerToken) {
      headers.set('X-Player-Token', options.playerToken);
    }

    try {
      return await $fetch<T>(path, {
        method: options.method,
        body: options.body,
        headers,
      });
    } catch (fetchError) {
      const err = fetchError as { status?: number; statusCode?: number };
      const status = err.status ?? err.statusCode ?? 0;

      if (status === 401 && import.meta.client) {
        clearSession();
        await navigateTo('/login');
      }

      throw new ApiError(status, extractError(fetchError));
    }
  }

  return { api, token, email, loadSession, setSession, clearSession, silentRefresh };
}

export function extractError(fetchError: unknown) {
  const errorResponse = fetchError as {
    data?: { errors?: string[] | Record<string, string[]>; title?: string; detail?: string };
    status?: number;
    statusCode?: number;
    statusMessage?: string;
  };
  const data = errorResponse.data;

  if (Array.isArray(data?.errors)) {
    return data.errors.join(' ');
  }

  if (data?.errors && typeof data.errors === 'object') {
    return Object.values(data.errors).flat().join(' ');
  }

  // Prefer detail (contains actual exception in dev) over the generic title
  return data?.detail || data?.title || errorResponse.statusMessage || `Request failed (${errorResponse.status ?? errorResponse.statusCode ?? 'unknown'})`;
}
