import type { SessionLiveResponse, SessionStateResponse } from '~/types/api';
import { useSessionHub } from '~/composables/useSessionHub';
import { useSessionPolling } from '~/composables/useSessionPolling';
import type { SessionRefreshOptions } from '~/composables/useSessionPolling';
import {
  getAdaptivePollIntervalMs,
  maxActionSequence,
  mergeLiveState,
  mergeSessionState,
} from '~/utils/sessionStateMerge';

export interface LiveSessionFetchArgs {
  sinceSequence: number;
  prev: SessionStateResponse | null;
  force?: boolean;
}

export interface UseLiveSessionOptions {
  fetchFullState: (args: LiveSessionFetchArgs) => Promise<SessionStateResponse | null>;
  fetchLiveState?: (args: LiveSessionFetchArgs) => Promise<SessionLiveResponse | null>;
  /** Return true when the server version matches the cached version (skip fetch). */
  checkVersion?: (knownVersion: number) => Promise<boolean>;
  /** Defaults to join code from the loaded session state. */
  getJoinCode?: () => string;
  getDmToken?: () => string | null | undefined;
  getPlayerToken?: () => string | null | undefined;
}

const FULL_REFRESH_EVERY = 15;

export function useLiveSession(options: UseLiveSessionOptions) {
  const lastActionSequence = ref(0);
  let pollCount = 0;
  let hubRefreshTimer: ReturnType<typeof setTimeout> | null = null;

  async function loadState(prev: SessionStateResponse | null, refreshOptions?: SessionRefreshOptions) {
    const force = refreshOptions?.force ?? false;

    if (prev && !force && options.checkVersion) {
      const isUnchanged = await options.checkVersion(prev.version);
      if (isUnchanged) {
        return prev;
      }
    }

    pollCount += 1;
    const forceFullRefresh = !prev || force || pollCount % FULL_REFRESH_EVERY === 0;
    const sinceSequence = forceFullRefresh ? 0 : lastActionSequence.value;
    const fetchArgs: LiveSessionFetchArgs = { sinceSequence, prev, force };

    if (prev && options.fetchLiveState && !forceFullRefresh) {
      const live = await options.fetchLiveState(fetchArgs);
      if (live) {
        const merged = mergeLiveState(prev, live);
        lastActionSequence.value = maxActionSequence(merged.actions);
        return merged;
      }
    }

    const next = await options.fetchFullState(fetchArgs);
    if (!next) return null;

    const merged = prev && sinceSequence > 0
      ? mergeSessionState(prev, next)
      : next;

    lastActionSequence.value = maxActionSequence(merged.actions);
    return merged;
  }

  const hubConnected = ref(false);

  const polling = useSessionPolling(
    loadState,
    currentState => getAdaptivePollIntervalMs(currentState, hubConnected.value),
  );

  function resolveJoinCode() {
    return options.getJoinCode?.() ?? polling.state.value?.joinCode ?? '';
  }

  const hub = useSessionHub({
    getJoinCode: resolveJoinCode,
    getDmToken: options.getDmToken,
    getPlayerToken: options.getPlayerToken,
    onSessionChange: () => scheduleHubRefresh(),
  });

  watch(hub.isConnected, (connected) => {
    hubConnected.value = connected;
  }, { immediate: true });

  function scheduleHubRefresh() {
    if (hubRefreshTimer) {
      clearTimeout(hubRefreshTimer);
    }

    hubRefreshTimer = setTimeout(() => {
      void polling.refresh({ force: true });
    }, 100);
  }

  function refreshInBackground() {
    void polling.refresh({ force: true });
  }

  watch(
    () => resolveJoinCode(),
    (joinCode) => {
      if (joinCode && import.meta.client) {
        void hub.connect();
      }
    },
    { immediate: true },
  );

  onBeforeUnmount(async () => {
    if (hubRefreshTimer) {
      clearTimeout(hubRefreshTimer);
    }
    await hub.disconnect();
  });

  return {
    ...polling,
    hubConnected: hub.isConnected,
    hubError: hub.connectionError,
    refreshInBackground,
  };
}
