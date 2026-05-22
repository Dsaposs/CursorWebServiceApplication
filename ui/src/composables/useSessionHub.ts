import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import type { ActionQueueItemResponse } from '~/types/api';

export type SessionHubEvent =
  | 'session.mode_changed'
  | 'turn.opened'
  | 'turn.skipped'
  | 'action.submitted'
  | 'action.dm_reviewing'
  | 'action.roll_requested'
  | 'action.roll_received'
  | 'action.reaction_requested'
  | 'action.reaction_received'
  | 'action.followup_roll_requested'
  | 'action.followup_roll_received'
  | 'action.resolved'
  | 'action.rejected'
  | 'character.stats_updated'
  | 'npc.stats_updated';

export interface RollRequestedPayload {
  actionId: string;
  diceSpec: string;
  label?: string | null;
  guidanceText?: string | null;
  dc?: number | null;
  mode: string;
}

export interface ReactionRequestedPayload {
  reactionId: string;
  parentActionId: string;
  reactionType: string;
  diceSpec?: string | null;
  context?: string | null;
}

interface UseSessionHubOptions {
  joinCode: string;
  /** DM JWT token (when connecting as DM). */
  dmToken?: string | null;
  /** Player participant token (when connecting as player). */
  playerToken?: string | null;
  onActionUpdate?: (action: ActionQueueItemResponse) => void;
  onRollRequested?: (payload: RollRequestedPayload) => void;
  onReactionRequested?: (payload: ReactionRequestedPayload) => void;
  onModeChanged?: (payload: { newMode: string }) => void;
  onTurnOpened?: (payload: { activeParticipantId: string; characterName: string; round: number }) => void;
  onCharacterStatsUpdated?: (payload: { characterId: string; updatedStats: unknown }) => void;
}

/**
 * Composable that manages the SignalR WebSocket connection to `/hubs/session`.
 * Call `connect()` once the session join code is known; call `disconnect()` on unmount.
 */
export function useSessionHub(options: UseSessionHubOptions) {
  const { token: authToken } = useApi();

  const isConnected = ref(false);
  const connectionError = ref<string | null>(null);

  const connection = new HubConnectionBuilder()
    .withUrl(`/api-ws/hubs/session`, {
      accessTokenFactory: () => options.dmToken ?? authToken.value ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  // ── Event bindings ─────────────────────────────────────────────────────

  connection.on('action.submitted', (payload: ActionQueueItemResponse) => options.onActionUpdate?.(payload));
  connection.on('action.dm_reviewing', (payload: ActionQueueItemResponse) => options.onActionUpdate?.(payload));
  connection.on('action.roll_received', (payload: ActionQueueItemResponse) => options.onActionUpdate?.(payload));
  connection.on('action.resolved', (payload: ActionQueueItemResponse) => options.onActionUpdate?.(payload));
  connection.on('action.rejected', (payload: ActionQueueItemResponse) => options.onActionUpdate?.(payload));
  connection.on('action.roll_requested', (payload: RollRequestedPayload) => options.onRollRequested?.(payload));
  connection.on('action.reaction_requested', (payload: ReactionRequestedPayload) => options.onReactionRequested?.(payload));
  connection.on('session.mode_changed', (payload: { newMode: string }) => options.onModeChanged?.(payload));
  connection.on('turn.opened', (payload: { activeParticipantId: string; characterName: string; round: number }) => options.onTurnOpened?.(payload));
  connection.on('character.stats_updated', (payload: { characterId: string; updatedStats: unknown }) => options.onCharacterStatsUpdated?.(payload));

  connection.onreconnecting(() => { isConnected.value = false; });
  connection.onreconnected(() => { isConnected.value = true; });
  connection.onclose(() => { isConnected.value = false; });

  // ── Lifecycle ──────────────────────────────────────────────────────────

  async function connect() {
    if (connection.state !== HubConnectionState.Disconnected) return;

    try {
      await connection.start();
      isConnected.value = true;
      connectionError.value = null;

      if (options.dmToken || authToken.value) {
        await connection.invoke('JoinSessionAsDm', options.joinCode);
      } else if (options.playerToken) {
        await connection.invoke('JoinSessionAsPlayer', options.joinCode, options.playerToken);
      }
    } catch (err) {
      connectionError.value = err instanceof Error ? err.message : 'SignalR connection failed';
    }
  }

  async function disconnect() {
    if (connection.state !== HubConnectionState.Disconnected) {
      await connection.stop();
    }
  }

  return { isConnected, connectionError, connect, disconnect, connection };
}
