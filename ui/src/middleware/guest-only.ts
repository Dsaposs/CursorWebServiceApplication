import { isTokenExpired } from '~/composables/useApi';

/**
 * Redirects already-authenticated users away from guest-only pages (e.g. /login).
 * Apply with definePageMeta({ middleware: 'guest-only' }) on auth pages.
 */
export default defineNuxtRouteMiddleware(() => {
  if (!import.meta.client) return;

  const { token, loadSession } = useApi();
  loadSession();

  if (token.value && !isTokenExpired(token.value)) {
    return navigateTo('/games');
  }
});
