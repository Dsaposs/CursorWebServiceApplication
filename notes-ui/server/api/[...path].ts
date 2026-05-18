import {
  createError,
  defineEventHandler,
  getMethod,
  getRequestHeaders,
  getRequestURL,
  readRawBody,
  setResponseHeader,
  setResponseStatus,
} from 'h3';

const hopByHopHeaders = new Set([
  'connection',
  'content-length',
  'host',
  'keep-alive',
  'proxy-authenticate',
  'proxy-authorization',
  'te',
  'trailer',
  'transfer-encoding',
  'upgrade',
]);

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  const apiBaseUrl = config.apiBaseUrl;
  const path = event.context.params?.path ?? '';
  const targetUrl = new URL(`/api/${path}`, apiBaseUrl);
  targetUrl.search = getRequestURL(event).search;

  const method = getMethod(event);
  const headers = new Headers();
  const requestHeaders = getRequestHeaders(event);

  for (const [key, value] of Object.entries(requestHeaders)) {
    if (!value || hopByHopHeaders.has(key.toLowerCase())) {
      continue;
    }

    headers.set(key, Array.isArray(value) ? value.join(',') : value);
  }

  const body = method === 'GET' || method === 'HEAD'
    ? undefined
    : await readRawBody(event, 'utf8');

  try {
    const response = await $fetch.raw(targetUrl.toString(), {
      method,
      headers,
      body,
    });

    setResponseStatus(event, response.status);
    copyResponseHeaders(event, response.headers);
    return response._data;
  } catch (error) {
    const response = getFetchErrorResponse(error);
    if (!response) {
      throw createError({
        statusCode: 502,
        statusMessage: 'The API service is unavailable.',
      });
    }

    setResponseStatus(event, response.status);
    copyResponseHeaders(event, response.headers);
    return response._data;
  }
});

function copyResponseHeaders(event: Parameters<typeof setResponseHeader>[0], headers: Headers) {
  const contentType = headers.get('content-type');
  if (contentType) {
    setResponseHeader(event, 'content-type', contentType);
  }
}

function getFetchErrorResponse(error: unknown): { status: number; headers: Headers; _data: unknown } | null {
  if (typeof error !== 'object' || error === null || !('response' in error)) {
    return null;
  }

  const response = (error as { response?: { status?: number; headers?: Headers; _data?: unknown } }).response;
  if (!response?.status || !response.headers) {
    return null;
  }

  return {
    status: response.status,
    headers: response.headers,
    _data: response._data,
  };
}
