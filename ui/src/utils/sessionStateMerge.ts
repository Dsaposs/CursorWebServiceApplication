import type { ActionQueueItemResponse, SessionLiveResponse, SessionStateResponse } from '~/types/api';

/** Highest action sequence in the list (0 when empty). */
export function maxActionSequence(actions: ActionQueueItemResponse[]): number {
  return actions.reduce((max, action) => Math.max(max, action.sequence), 0);
}

/**
 * Merge an incremental session poll (actions with sequence > sinceSequence) into prior state.
 * Non-action fields always come from the latest response.
 */
export function mergeSessionState(
  prev: SessionStateResponse,
  next: SessionStateResponse,
): SessionStateResponse {
  if (next.actions.length === 0) {
    return {
      ...next,
      actions: prev.actions,
    };
  }

  const actionMap = new Map(prev.actions.map(action => [action.id, action]));
  for (const action of next.actions) {
    actionMap.set(action.id, action);
  }

  return {
    ...next,
    actions: [...actionMap.values()].sort((left, right) => left.sequence - right.sequence),
  };
}

/** Replace or append a single action after a mutation response. */
export function replaceActionInState(
  state: SessionStateResponse,
  action: ActionQueueItemResponse,
): SessionStateResponse {
  const hasAction = state.actions.some(existing => existing.id === action.id);
  const actions = hasAction
    ? state.actions.map(existing => (existing.id === action.id ? action : existing))
    : [...state.actions, action].sort((left, right) => left.sequence - right.sequence);

  return { ...state, actions };
}

/**
 * Apply a lightweight live poll onto the cached full session state.
 * Keeps ruleset metadata on game while refreshing combatants and board data.
 */
export function mergeLiveState(
  prev: SessionStateResponse,
  live: SessionLiveResponse,
): SessionStateResponse {
  const actions = live.actions.length === 0
    ? prev.actions
    : mergeSessionState(prev, { ...prev, actions: live.actions }).actions;

  return {
    ...prev,
    id: live.id,
    gameId: live.gameId,
    joinCode: live.joinCode,
    joinUrl: live.joinUrl,
    isActive: live.isActive,
    state: live.state,
    diceRollMode: live.diceRollMode,
    activeTurnParticipantId: live.activeTurnParticipantId,
    version: live.version,
    startedAt: live.startedAt,
    endedAt: live.endedAt,
    updatedAt: live.updatedAt,
    character: live.character ?? prev.character,
    actions,
    initiative: live.initiative,
    rollPrompts: live.rollPrompts,
    combatEncounters: live.combatEncounters,
    game: live.game
      ? {
          ...prev.game,
          updatedAt: live.game.updatedAt,
          characters: live.game.characters,
          npcsAndMonsters: live.game.npcsAndMonsters,
        }
      : prev.game,
  };
}

/** Poll faster during combat / roll prompts; slower when SignalR is connected. */
export function getAdaptivePollIntervalMs(
  state: SessionStateResponse | null,
  hubConnected: boolean,
): number {
  if (hubConnected) {
    return 8000;
  }

  const hasPendingRolls = (state?.rollPrompts?.length ?? 0) > 0;
  const isCombat = state?.state === 'Combat';
  const hasPendingActions = (state?.actions ?? []).some(action =>
    ['Pending', 'DmReviewing', 'AwaitingRoll', 'RollReceived', 'AwaitingFollowUpRoll'].includes(action.status),
  );

  if (hasPendingRolls || (isCombat && hasPendingActions)) {
    return 1000;
  }

  if (isCombat) {
    return 2500;
  }

  return 4000;
}
