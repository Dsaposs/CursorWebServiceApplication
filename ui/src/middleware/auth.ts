import { isTokenExpired } from '~/composables/useApi';

/**
 * Requires a valid, non-expired DM JWT.
 * Clears stale session data and redirects to /login if the token is missing or expired.
 * Apply with definePageMeta({ middleware: 'auth' }) on DM-only pages.
 */
export default defineNuxtRouteMiddleware(() => {
  if (!import.meta.client) return;

  const { token, loadSession, clearSession } = useApi();
  loadSession();

  if (!token.value || isTokenExpired(token.value)) {
    clearSession();
    return navigateTo('/login');
  }
});
