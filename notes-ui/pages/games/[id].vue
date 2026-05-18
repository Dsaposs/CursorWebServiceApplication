<script setup lang="ts">
const route = useRoute();
// Redirect back to the main games hub; the sidebar will let the DM re-open
// any specific game. This page is kept only for backwards-compat with old links.
onMounted(() => navigateTo('/games', { replace: true }));
</script>

<template>
  <div class="page"><p class="muted">Redirecting…</p></div>
</template>
