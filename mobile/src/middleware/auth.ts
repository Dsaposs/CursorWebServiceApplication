export default defineNuxtRouteMiddleware(() => {
  const { token, loadSession } = useApi();
  if (import.meta.client) loadSession();
  if (!token.value) return navigateTo('/login', { replace: true });
});
