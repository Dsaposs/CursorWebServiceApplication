<script setup lang="ts">
interface Props {
  current: number;
  max: number;
  label?: string;
}

const props = defineProps<Props>();

const pct = computed(() => (props.max > 0 ? Math.min(100, Math.max(0, (props.current / props.max) * 100)) : 0));

const fillColor = computed(() => {
  if (pct.value > 60) return '#4ddc7e';
  if (pct.value > 30) return '#e8a32a';
  return '#f47070';
});
</script>

<template>
  <div class="health-bar-wrap">
    <div class="health-bar-label">
      <span>{{ label ?? 'HP' }}</span>
      <strong>{{ current }}<span class="muted"> / {{ max }}</span></strong>
    </div>
    <div class="health-bar-track">
      <div
        class="health-bar-fill"
        :style="{ width: pct + '%', background: fillColor }"
      />
    </div>
  </div>
</template>
