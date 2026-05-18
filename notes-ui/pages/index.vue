<script setup lang="ts">
const { token, loadSession } = useApi();

onMounted(() => {
  loadSession();
  void navigateTo(token.value ? '/games' : '/login', { replace: true });
});
</script>

<template>
  <section class="page">
    <p class="muted">Loading…</p>
  </section>
</template>
