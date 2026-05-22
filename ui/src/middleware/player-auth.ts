/**
 * Requires a player session token stored under the join code.
 * Redirects to the join page if the token is missing.
 * Apply with definePageMeta({ middleware: 'player-auth' }) on player session pages.
 */
export default defineNuxtRouteMiddleware((to) => {
  if (!import.meta.client) return;

  const { getSessionPlayerToken } = usePlayerTokens();
  const code = Array.isArray(to.params.code) ? to.params.code[0] : to.params.code;

  if (!code) return navigateTo('/');

  const playerToken = getSessionPlayerToken(code);
  if (!playerToken) {
    return navigateTo(`/join/${code}`);
  }
});
