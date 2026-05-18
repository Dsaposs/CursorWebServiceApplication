<script setup lang="ts">
interface Props {
  lines?: number;
  compact?: boolean;
}

withDefaults(defineProps<Props>(), {
  lines: 3,
  compact: false,
});
</script>

<template>
  <div class="skeleton-panel" :class="{ compact }" aria-busy="true" aria-label="Loading">
    <div class="skeleton-line skeleton-title" />
    <div
      v-for="line in lines"
      :key="line"
      class="skeleton-line"
      :style="{ width: `${Math.max(42, 92 - line * 12)}%` }"
    />
  </div>
</template>
