<script setup lang="ts">
const route = useRoute();
const reason = computed(() => route.query.reason as string | undefined);
const isExpired = computed(() => reason.value === 'inactivity');
const sessionId = computed(() => {
  const value = route.query.sessionId;
  return typeof value === 'string' ? value : '';
});
const gameId = computed(() => {
  const value = route.query.gameId;
  return typeof value === 'string' ? value : '';
});
const summaryLink = computed(() => {
  if (!sessionId.value) return null;
  const query: Record<string, string> = { player: '1' };
  if (gameId.value) query.gameId = gameId.value;
  return { path: `/sessions/${sessionId.value}/summary`, query };
});
</script>

<template>
  <div class="ended-page">
    <div class="ended-card">
      <div class="ended-icon">⚔️</div>
      <h1 class="ended-title">Session Ended</h1>
      <p v-if="isExpired" class="ended-body">
        This session was automatically ended due to inactivity. Thanks for playing!
      </p>
      <p v-else class="ended-body">
        This session is no longer available. The Dungeon Master may have ended it.
      </p>
      <div class="ended-actions">
        <NuxtLink v-if="summaryLink" :to="summaryLink" class="btn btn-primary">
          View Session Summary
        </NuxtLink>
        <NuxtLink v-else to="/" class="btn btn-primary">Return Home</NuxtLink>
      </div>
    </div>
  </div>
</template>

<style scoped>
.ended-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: var(--space-6);
  background: var(--color-bg);
}

.ended-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-xl);
  padding: var(--space-10) var(--space-8);
  text-align: center;
  max-width: 420px;
  width: 100%;
  box-shadow: var(--shadow-lg, 0 8px 32px rgba(0,0,0,.25));
}

.ended-icon {
  font-size: 3rem;
  margin-bottom: var(--space-4);
}

.ended-title {
  font-size: var(--text-2xl);
  font-weight: 700;
  color: var(--color-text);
  margin: 0 0 var(--space-3);
}

.ended-body {
  color: var(--color-text-muted);
  line-height: 1.6;
  margin: 0 0 var(--space-6);
}

.ended-actions {
  display: flex;
  gap: var(--space-3);
  justify-content: center;
}
</style>
