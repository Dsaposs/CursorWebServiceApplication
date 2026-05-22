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
      // Token exists but is expired — clear it proactively
      localStorage.removeItem('ttrpg_token');
      localStorage.removeItem('ttrpg_email');
      token.value = null;
      email.value = '';
    }
  }

  function setSession(nextToken: string, nextEmail: string) {
    token.value = nextToken;
    email.value = nextEmail;
    if (import.meta.client) {
      localStorage.setItem('ttrpg_token', nextToken);
      localStorage.setItem('ttrpg_email', nextEmail);
    }
  }

  function clearSession() {
    token.value = null;
    email.value = '';
    if (import.meta.client) {
      localStorage.removeItem('ttrpg_token');
      localStorage.removeItem('ttrpg_email');
    }
  }

  async function api<T>(path: string, options: ApiOptions = {}) {
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

  return { api, token, email, loadSession, setSession, clearSession };
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
