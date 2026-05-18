import type { SessionStateResponse } from '~/types/api';

export function useSessionPolling(loadState: () => Promise<SessionStateResponse | null>, intervalMs = 3000) {
  const state = ref<SessionStateResponse | null>(null);
  const pollingError = ref('');
  let timer: ReturnType<typeof setInterval> | null = null;

  async function refresh() {
    try {
      state.value = await loadState();
      pollingError.value = '';
    } catch (error) {
      pollingError.value = error instanceof Error ? error.message : String(error);
    }
  }

  function start() {
    if (!import.meta.client || timer) {
      return;
    }

    void refresh();
    timer = setInterval(() => void refresh(), intervalMs);
  }

  function stop() {
    if (timer) {
      clearInterval(timer);
      timer = null;
    }
  }

  onBeforeUnmount(stop);

  return { state, pollingError, refresh, start, stop };
}
