<script setup lang="ts">
import type { ConnectionStatus } from '~/composables/useSessionPolling';

interface Props {
  status: ConnectionStatus;
  error?: string;
  startedAt?: string | Date | null;
  endedAt?: string | Date | null;
  isActive?: boolean;
}

const props = defineProps<Props>();
const currentTime = ref(Date.now());
let timer: ReturnType<typeof setInterval> | null = null;

const label = computed(() => {
  switch (props.status) {
    case 'live':
    case 'refreshing':
      return 'Live';
    case 'reconnecting':
      return 'Reconnecting';
    case 'offline':
      return 'Offline';
    case 'paused':
      return 'Paused';
    default:
      return 'Live';
  }
});

const toneClass = computed(() => {
  switch (props.status) {
    case 'live':
    case 'refreshing':
      return 'success';
    case 'reconnecting':
    case 'paused':
      return 'info';
    case 'offline':
      return 'error';
    default:
      return 'success';
  }
});

const durationText = computed(() => {
  if (!props.startedAt) return '';

  const totalSeconds = sessionElapsedSeconds.value;
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  const paddedHours = String(hours).padStart(1, '0');
  const paddedMinutes = String(minutes).padStart(2, '0');
  const paddedSeconds = String(seconds).padStart(2, '0');

  return `${paddedHours}:${paddedMinutes}:${paddedSeconds}`;
});

function getTimestamp(value: string | Date | null | undefined) {
  if (!value) return Number.NaN;
  if (value instanceof Date) return value.getTime();

  const hasTimezone = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(value);
  const normalizedValue = hasTimezone ? value : `${value}Z`;

  return new Date(normalizedValue).getTime();
}

const sessionElapsedSeconds = computed(() => {
  if (!props.startedAt) return 0;

  const start = getTimestamp(props.startedAt);
  const end = props.isActive === false && props.endedAt
    ? getTimestamp(props.endedAt)
    : currentTime.value;

  if (Number.isNaN(start) || Number.isNaN(end)) {
    return 0;
  }

  return Math.max(0, Math.floor((end - start) / 1000));
});

onMounted(() => {
  currentTime.value = Date.now();
  timer = setInterval(() => {
    currentTime.value = Date.now();
  }, 1000);
});

onBeforeUnmount(() => {
  if (timer) clearInterval(timer);
});
</script>

<template>
  <span
    class="connection-status"
    :class="`connection-status-${toneClass}`"
    :title="error || (durationText ? `Session duration ${durationText}` : label)"
  >
    <span class="connection-status-dot" aria-hidden="true" />
    <span>{{ label }}</span>
    <span v-if="durationText" class="connection-status-time">
      {{ durationText }}
    </span>
  </span>
</template>
