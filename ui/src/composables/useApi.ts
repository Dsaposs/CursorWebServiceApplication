type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE';

interface ApiOptions {
  method?: HttpMethod;
  body?: unknown;
  playerToken?: string | null;
}

const authToken = () => useState<string | null>('auth-token', () => null);
const authEmail = () => useState<string>('auth-email', () => '');

export function useApi() {
  const token = authToken();
  const email = authEmail();

  function loadSession() {
    if (import.meta.client) {
      token.value = localStorage.getItem('ttrpg_token');
      email.value = localStorage.getItem('ttrpg_email') || '';
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
      throw new Error(extractError(fetchError));
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
