import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import type { SessionHubEvent } from '~/composables/useSessionHub';

export type { SessionHubEvent };

export interface RollRequestedPayload {
  actionId: string;
  diceSpec?: string;
  label?: string | null;
  guidanceText?: string | null;
  dc?: number | null;
  mode?: string;
  promptIds?: string[];
}

interface UseSessionHubOptions {
  getJoinCode: () => string;
  getDmToken?: () => string | null | undefined;
  getPlayerToken?: () => string | null | undefined;
  /** Debounced refresh when any session lifecycle event arrives. */
  onSessionChange?: () => void;
}

const SESSION_CHANGE_EVENTS: SessionHubEvent[] = [
  'session.mode_changed',
  'turn.opened',
  'turn.skipped',
  'action.submitted',
  'action.dm_reviewing',
  'action.roll_requested',
  'action.roll_received',
  'action.reaction_requested',
  'action.reaction_received',
  'action.followup_roll_requested',
  'action.followup_roll_received',
  'action.resolved',
  'action.rejected',
  'character.stats_updated',
  'npc.stats_updated',
];

/**
 * Manages the SignalR WebSocket connection to `/api-ws/hubs/session`.
 * Call `connect()` once the session join code is known; call `disconnect()` on unmount.
 */
export function useSessionHub(options: UseSessionHubOptions) {
  const { token: authToken } = useApi();

  const isConnected = ref(false);
  const connectionError = ref<string | null>(null);

  const connection = new HubConnectionBuilder()
    .withUrl('/api-ws/hubs/session', {
      accessTokenFactory: () => options.getDmToken?.() ?? authToken.value ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  for (const eventName of SESSION_CHANGE_EVENTS) {
    connection.on(eventName, () => {
      options.onSessionChange?.();
    });
  }

  connection.onreconnecting(() => {
    isConnected.value = false;
  });

  connection.onreconnected(async () => {
    isConnected.value = true;
    await joinGroups();
    options.onSessionChange?.();
  });

  connection.onclose(() => {
    isConnected.value = false;
  });

  async function joinGroups() {
    const joinCode = options.getJoinCode();
    if (!joinCode) return;

    const dmToken = options.getDmToken?.() ?? authToken.value;
    if (dmToken) {
      await connection.invoke('JoinSessionAsDm', joinCode);
      return;
    }

    const playerToken = options.getPlayerToken?.();
    if (playerToken) {
      await connection.invoke('JoinSessionAsPlayer', joinCode, playerToken);
    }
  }

  async function connect() {
    if (connection.state !== HubConnectionState.Disconnected) return;
    if (!options.getJoinCode()) return;

    try {
      await connection.start();
      isConnected.value = true;
      connectionError.value = null;
      await joinGroups();
    } catch (err) {
      connectionError.value = err instanceof Error ? err.message : 'SignalR connection failed';
      isConnected.value = false;
    }
  }

  async function disconnect() {
    if (connection.state !== HubConnectionState.Disconnected) {
      await connection.stop();
    }
    isConnected.value = false;
  }

  return { isConnected, connectionError, connect, disconnect, connection };
}
