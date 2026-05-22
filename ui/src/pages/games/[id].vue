<script setup lang="ts">
// Redirect back to the main games hub; kept only for backwards-compat with old links.
onMounted(() => navigateTo('/games', { replace: true }));
</script>

<template>
  <div class="page"><p class="muted">Redirecting…</p></div>
</template>
