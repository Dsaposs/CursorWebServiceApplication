import type { SessionStateResponse } from '~/types/api';

export type ConnectionStatus = 'live' | 'refreshing' | 'reconnecting' | 'offline' | 'paused';

export function useSessionPolling(loadState: () => Promise<SessionStateResponse | null>, intervalMs = 3000) {
  const state = ref<SessionStateResponse | null>(null);
  const pollingError = ref('');
  const connectionStatus = ref<ConnectionStatus>('refreshing');
  const lastRefreshedAt = ref<Date | null>(null);
  const isRefreshing = ref(false);
  let timer: ReturnType<typeof setTimeout> | null = null;
  let hasStarted = false;
  let failureCount = 0;

  const maxIntervalMs = 30000;

  async function refresh() {
    if (isRefreshing.value) return;

    isRefreshing.value = true;
    if (!state.value) {
      connectionStatus.value = 'reconnecting';
    }
    try {
      state.value = await loadState();
      pollingError.value = '';
      failureCount = 0;
      lastRefreshedAt.value = new Date();
      connectionStatus.value = 'live';
    } catch (error) {
      failureCount += 1;
      pollingError.value = error instanceof Error ? error.message : String(error);
      connectionStatus.value = failureCount > 1 ? 'offline' : 'reconnecting';
    } finally {
      isRefreshing.value = false;
    }
  }

  function nextInterval() {
    return Math.min(intervalMs * 2 ** Math.max(failureCount - 1, 0), maxIntervalMs);
  }

  function clearTimer() {
    if (timer) {
      clearTimeout(timer);
      timer = null;
    }
  }

  function scheduleNext() {
    if (!import.meta.client || !hasStarted || document.hidden) {
      return;
    }

    clearTimer();
    timer = setTimeout(() => {
      void refresh().finally(scheduleNext);
    }, nextInterval());
  }

  function handleVisibilityChange() {
    if (document.hidden) {
      clearTimer();
      connectionStatus.value = 'paused';
      return;
    }

    if (hasStarted) {
      void refresh().finally(scheduleNext);
    }
  }

  function start() {
    if (!import.meta.client || hasStarted) {
      return;
    }

    hasStarted = true;
    document.addEventListener('visibilitychange', handleVisibilityChange);
    void refresh().finally(scheduleNext);
  }

  function stop() {
    hasStarted = false;
    clearTimer();
    if (import.meta.client) {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
    }
  }

  onBeforeUnmount(stop);

  return {
    state,
    pollingError,
    connectionStatus,
    lastRefreshedAt,
    isRefreshing,
    refresh,
    start,
    stop,
  };
}
