<script setup lang="ts">
definePageMeta({ middleware: 'auth' });
import ActionEvaluationPanel from '~/components/ActionEvaluationPanel.vue';
import ConfirmModal from '~/components/ConfirmModal.vue';
import DmActionLog from '~/components/dm/DmActionLog.vue';
import DmCombatWorkflow from '~/components/dm/DmCombatWorkflow.vue';
import DmParticipantPanels from '~/components/dm/DmParticipantPanels.vue';
import DmSessionInvite from '~/components/dm/DmSessionInvite.vue';
import DmFollowUpRollPanel from '~/components/DmFollowUpRollPanel.vue';
import DmStatChangePanel from '~/components/DmStatChangePanel.vue';
import DmPlayerSkillCheckPanel from '~/components/DmPlayerSkillCheckPanel.vue';

import SessionConnectionStatus from '~/components/SessionConnectionStatus.vue';
import SkeletonBlock from '~/components/SkeletonBlock.vue';
import { useRulesetActionChooser } from '~/composables/useRulesetActionChooser';
import type { ActionQueueItemResponse, GameResponse, InitiativeEntryResponse, RulesetResponse, SessionLiveResponse, SessionStateResponse, SessionVersionResponse } from '~/types/api';
import { evaluateActionOutcome, resolveTargetArmor } from '~/utils/actionOutcome';
import {
  actionRollFlowBadgeClass,
  actionRollFlowLabel,
  evaluateActionOutcomeFromRolls,
  getActionRollFlowStatus,
} from '~/utils/actionRolls';
import { parseCharacterStats, parseStatMap } from '~/utils/dice';
import { parseNpcInventory } from '~/utils/inventory';
import { resolveEffectiveActionRoll } from '~/utils/items';
import {
  buildDiceRollContext,
  describeRulesetAction,
  findRulesetAction,
  isCombatAction,
  actionRequiresTarget,
  parseActorClassKey,
  parseRulesetDefinition,
  type DiceRollMode,
} from '~/utils/rulesets';
import { useRulesetTheme } from '~/composables/useRulesetTheme';
import { useThemePreference } from '~/composables/useThemePreference';
import { isStatCheckAction } from '~/utils/statCheckAction';
import { replaceActionInState } from '~/utils/sessionStateMerge';

const route = useRoute();
const { api, token, loadSession, clearSession } = useApi();
const { success: toastSuccess, error: toastError, info: toastInfo } = useToast();
const ruleset = ref<RulesetResponse | null>(null);

// Per-action resolve form state
const resolutionText = ref<Record<string, string>>({});
const statChangeTarget = ref<Record<string, string>>({});
const statChangeHealthDelta = ref<Record<string, string>>({});
const statChangeSetHealth = ref<Record<string, string>>({});
const statChangeSetArmor = ref<Record<string, string>>({});
// Game-value absolute overrides (actor only, kept for backward compat but UI removed)
const gameValueChanges = ref<Record<string, Record<string, string>>>({});
// Stat-change section: per-action delta maps for game values, attributes, inventory
const statChangeGvDeltas = ref<Record<string, Record<string, string>>>({});
const statChangeAttrDeltas = ref<Record<string, Record<string, string>>>({});
const statChangeInventoryDeltas = ref<Record<string, Record<string, string>>>({});
// Status effect changes: per-action add/remove key lists
const statChangeStatusChanges = ref<Record<string, { addKeys: string[]; removeKeys: string[] }>>({});
const rejectReason = ref<Record<string, string>>({});

// NPC action form
const selectedNpcId = ref('');
const npcActionTargetPickerRef = ref<{ isValid: () => boolean; reset: () => void; toSubmitFields: () => { targetCharacterId?: string; targetNpcId?: string; targetName?: string } } | null>(null);
const npcTargetPickerRevision = ref(0);
const npcRollResult = ref('');
const npcDamageRollResult = ref('');
const showNpcActionForm = ref(false);

// Action expand state
const expandedActions = ref<Set<string>>(new Set());
const expandedPendingActions = ref<Set<string>>(new Set());
const isSaving = ref(false);
const showStopSessionConfirm = ref(false);

function sessionQuery(sinceSequence: number) {
  return sinceSequence > 0 ? `?sinceSequence=${sinceSequence}` : '';
}

async function fetchFullState({ sinceSequence }: { sinceSequence: number }) {
  if (!token.value) return null;
  const nextState = await api<SessionStateResponse>(`/api/sessions/${route.params.id}/dm${sessionQuery(sinceSequence)}`);
  if (!ruleset.value) {
    ruleset.value = await api<RulesetResponse>(`/api/rulesets/${nextState.game.rulesetCode}`);
  }
  return nextState;
}

async function fetchLiveState({ sinceSequence }: { sinceSequence: number }) {
  if (!token.value) return null;
  return api<SessionLiveResponse>(`/api/sessions/${route.params.id}/live${sessionQuery(sinceSequence)}`);
}

async function checkVersion(knownVersion: number) {
  if (!token.value) return false;
  const snapshot = await api<SessionVersionResponse>(`/api/sessions/${route.params.id}/version`);
  return snapshot.version === knownVersion;
}

const { state, pollingError, fatalError, connectionStatus, refresh, refreshInBackground, hubConnected, start } = useLiveSession({
  fetchFullState,
  fetchLiveState,
  checkVersion,
  getDmToken: () => token.value,
});
const { enabled: rulesetThemeEnabled, toggle: toggleRulesetTheme } = useThemePreference();
const _rulesetThemeStyle = useRulesetTheme(ruleset);
const rulesetThemeStyle = computed(() => rulesetThemeEnabled.value ? _rulesetThemeStyle.value : {});

watch(
  () => state.value?.isActive,
  (isActive) => {
    if (isActive === false) {
      navigateTo(`/sessions/${route.params.id}/summary`);
    }
  },
);

watch(fatalError, (err) => {
  if (!err) return;
  if (err.status === 401) {
    clearSession();
    navigateTo('/login');
  } else {
    // 403 or 404 — session is gone or this DM no longer owns it.
    navigateTo('/sessions/ended');
  }
});

const activeStatuses = ['Pending', 'DmReviewing', 'AwaitingRoll', 'RollReceived', 'AwaitingReaction', 'ReactionPending', 'Resolving', 'AwaitingFollowUpRoll'];
const pendingActions = computed(() => state.value?.actions.filter(a => activeStatuses.includes(a.status)) ?? []);

/** Pending queue is exploration-only; combat resolves inline from initiative. */
const explorationPendingActions = computed(() =>
  isCombat.value ? [] : pendingActions.value,
);
const publishedActions = computed(() => [...(state.value?.actions.filter(a => a.status === 'Published') ?? [])].reverse());

const game = computed<GameResponse | null>(() => state.value?.game ?? null);
const combatEncounters = computed(() => state.value?.combatEncounters ?? []);
const activeEncounter = computed(() =>
  combatEncounters.value.find(e => e.isActive) ?? null,
);
const currentRound = computed(() => activeEncounter.value?.round ?? 1);

const {
  expandedGroups: expandedLogGroups,
  toggleGroup: toggleLogGroup,
  expandAllGroups,
  collapseAllGroups,
} = useActionLogGroupExpansion(publishedActions, combatEncounters);

const rulesetDefinition = computed(() => parseRulesetDefinition(ruleset.value));
const currentTurn = computed<InitiativeEntryResponse | null>(() => state.value?.initiative.find(e => e.isCurrentTurn) ?? null);
const isCombat = computed(() => state.value?.state === 'Combat');
const joinLink = computed(() => {
  if (!state.value) return '';
  return import.meta.client ? `${window.location.origin}${state.value.joinUrl}` : state.value.joinUrl;
});
// Which initiative entry has its inline form expanded (DM manually activates per-entry)
const activeCombatEntryId = ref<string | null>(null);

const activeCombatEntry = computed(() =>
  displayedInitiative.value.find(e => e.id === activeCombatEntryId.value) ?? null,
);

// Pending action for whichever NPC is currently selected (drives the resolution form)
const currentNpcPendingAction = computed(() => {
  if (!selectedNpcId.value) return null;
  return pendingActions.value.find(a => a.actorNpcId === selectedNpcId.value) ?? null;
});

const npcTurnActionQueued = computed(() => Boolean(currentNpcPendingAction.value));

// Inline NPC action-resolution form refs (combat only — create + resolve in a single submit)
const npcInlineResolutionText = ref('');
const npcInlineStatTarget = ref<string | undefined>(undefined);
const npcInlineHealthDelta = ref<string | undefined>(undefined);
const npcInlineSetHealth = ref<string | undefined>(undefined);
const npcInlineSetArmor = ref<string | undefined>(undefined);
const npcInlineGvDeltas = ref<Record<string, string>>({});
const npcInlineAttrDeltas = ref<Record<string, string>>({});
const npcInlineInventoryDeltas = ref<Record<string, string>>({});
const npcInlineStatusChanges = ref<{ addKeys: string[]; removeKeys: string[] } | undefined>(undefined);

function resetNpcInlineFields() {
  npcInlineResolutionText.value = '';
  npcInlineStatTarget.value = undefined;
  npcInlineHealthDelta.value = undefined;
  npcInlineSetHealth.value = undefined;
  npcInlineSetArmor.value = undefined;
  npcInlineGvDeltas.value = {};
  npcInlineAttrDeltas.value = {};
  npcInlineInventoryDeltas.value = {};
  npcInlineStatusChanges.value = undefined;
}
const selectedNpc = computed(() => game.value?.npcsAndMonsters.find(npc => npc.id === selectedNpcId.value) ?? null);
const selectedNpcClassKey = computed(() => parseActorClassKey(selectedNpc.value?.statBlockJson));
const selectedNpcInventory = computed(() => parseNpcInventory(selectedNpc.value?.statBlockJson));
const isNpcActionActorSelected = computed(() => Boolean(selectedNpcId.value));
const {
  actionMode: npcActionMode,
  selectedActionKey: selectedNpcActionKey,
  selectedStatKey: selectedNpcStatKey,
  selectedStatType: selectedNpcStatType,
  selectedStatRawKey: selectedNpcStatRawKey,
  availableActions: availableNpcActions,
  availableStatChecks: availableNpcStatChecks,
  selectedActionDetail: selectedNpcActionDetail,
  selectedStatDetail: selectedNpcStatDetail,
  resetSelection: resetNpcActionSelection,
  buildSubmitPayload: buildNpcActionSubmitPayload,
} = useRulesetActionChooser(
  rulesetDefinition,
  selectedNpcClassKey,
  selectedNpcInventory,
  isNpcActionActorSelected,
  { allowAllActionsFallback: true },
);
const {
  displayedInitiative,
  draggedEntry,
  draggedInitiativeId,
  dragOverId,
  dragPosition,
  startDrag: startInitiativeDrag,
  moveByKeyboard: moveInitiativeByKeyboard,
} = useInitiativeOrder(computed(() => state.value?.initiative ?? []), {
  canReorder: computed(() => isCombat.value && !isSaving.value),
  saveOrder: saveInitiativeOrder,
  onSaveError: error => toastError(error instanceof Error ? error.message : String(error)),
});

onMounted(async () => {
  loadSession();
  if (!token.value) { await navigateTo('/login'); return; }
  start();
  if (import.meta.client) startIdleWatcher();
});


async function setState(nextState: 'Exploration' | 'Combat') {
  isSaving.value = true;
  try {
    await api(`/api/sessions/${route.params.id}/state`, { method: 'POST', body: { state: nextState } });
    refreshInBackground();
    toastSuccess(`Mode set to ${nextState}.`);
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

async function submitNpcAction() {
  if (!state.value || !selectedNpcId.value) return;
  const payload = buildNpcActionSubmitPayload();
  if (!payload) {
    toastError('Choose or describe an NPC action first.');
    return;
  }
  if (!npcActionTargetPickerRef.value?.isValid()) {
    toastError('Enter a target name for Other.');
    return;
  }
  if (!validateNpcRollRequirements()) {
    return;
  }

  const npcDescription = [
    npcRollResult.value ? `🎲 Roll: ${npcRollResult.value}` : '',
    npcDamageRollResult.value || '',
    payload.description ?? '',
  ].filter(Boolean).join('\n');

  isSaving.value = true;
  try {
    await api(`/api/sessions/${state.value.joinCode}/actions`, {
      method: 'POST',
      body: {
        actorNpcId: selectedNpcId.value,
        actionKey: payload.actionKey,
        actionText: payload.actionText,
        ...npcActionTargetPickerRef.value.toSubmitFields(),
        description: npcDescription || undefined,
      },
    });
    resetNpcActionSelection();
    npcActionTargetPickerRef.value?.reset();
    npcRollResult.value = '';
    npcDamageRollResult.value = '';
    refreshInBackground();
    toastSuccess('NPC action queued. Resolve from the pending queue.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

/** Build stat-change payload from the inline NPC combat form refs. */
function buildNpcInlineStatChanges() {
  type StatChange = {
    targetType: string; targetId: string;
    healthDelta?: number; setHealth?: number; setArmor?: number;
    gameValueDeltas?: Record<string, number>;
    attributeDeltas?: Record<string, number>;
    inventoryDeltas?: Record<string, number>;
    addStatusKeys?: string[]; removeStatusKeys?: string[];
  };
  const changes: StatChange[] = [];
  const target = npcInlineStatTarget.value;
  if (target) {
    const [targetType, targetId] = target.split(':');
    const healthDelta = optNum(npcInlineHealthDelta.value);
    const setHealth = optNum(npcInlineSetHealth.value);
    const setArmor = optNum(npcInlineSetArmor.value);
    let gameValueDeltas: Record<string, number> | undefined;
    let attributeDeltas: Record<string, number> | undefined;
    let inventoryDeltas: Record<string, number> | undefined;
    if (targetType === 'Character') {
      gameValueDeltas = toNonZeroIntMap(npcInlineGvDeltas.value);
      attributeDeltas = toNonZeroIntMap(npcInlineAttrDeltas.value);
      inventoryDeltas = toNonZeroIntMap(npcInlineInventoryDeltas.value);
    }
    const sc = npcInlineStatusChanges.value;
    const addStatusKeys = sc?.addKeys?.length ? sc.addKeys : undefined;
    const removeStatusKeys = sc?.removeKeys?.length ? sc.removeKeys : undefined;
    const hasAny = healthDelta !== undefined || setHealth !== undefined || setArmor !== undefined
      || (gameValueDeltas && Object.keys(gameValueDeltas).length > 0)
      || (attributeDeltas && Object.keys(attributeDeltas).length > 0)
      || (inventoryDeltas && Object.keys(inventoryDeltas).length > 0)
      || addStatusKeys !== undefined || removeStatusKeys !== undefined;
    if (hasAny)
      changes.push({ targetType, targetId, healthDelta, setHealth, setArmor, gameValueDeltas, attributeDeltas, inventoryDeltas, addStatusKeys, removeStatusKeys });
  }
  return changes;
}

/** Create the NPC action and immediately resolve it — used by the inline combat form. */
async function submitAndResolveNpcAction() {
  if (!state.value || !selectedNpcId.value) return;
  if (!canResolveNpcInlineAction.value) {
    if (!buildNpcActionSubmitPayload()) toastError('Choose or describe an NPC action first.');
    else if (npcTargetMissing.value) toastError('Select a target for this combat action.');
    else if (npcActionNeedsRoll.value && !npcHasPrimaryRoll.value) toastError('Roll the NPC action before resolving.');
    else if (npcNeedsFollowUpRoll.value && !npcHasFollowUpRoll.value) toastError('Roll damage before resolving this hit.');
    else if (!npcActionTargetPickerRef.value?.isValid()) toastError('Enter a target name for Other.');
    return;
  }
  const payload = buildNpcActionSubmitPayload();
  if (!payload) { toastError('Choose or describe an NPC action first.'); return; }
  if (!npcActionTargetPickerRef.value?.isValid()) { toastError('Enter a target name for Other.'); return; }
  if (!validateNpcRollRequirements()) return;
  const npcDescription = [
    npcRollResult.value ? `🎲 Roll: ${npcRollResult.value}` : '',
    npcDamageRollResult.value || '',
    payload.description ?? '',
  ].filter(Boolean).join('\n');

  isSaving.value = true;
  try {
    const created = await api<ActionQueueItemResponse>(`/api/sessions/${state.value.joinCode}/actions`, {
      method: 'POST',
      body: {
        actorNpcId: selectedNpcId.value,
        actionKey: payload.actionKey,
        actionText: payload.actionText,
        ...npcActionTargetPickerRef.value!.toSubmitFields(),
        description: npcDescription || undefined,
      },
    });
    await api(`/api/actions/${created.id}/resolve`, {
      method: 'PUT',
      body: {
        resolutionText: npcInlineResolutionText.value || undefined,
        statChanges: buildNpcInlineStatChanges(),
      },
    });
    resetNpcActionSelection();
    npcActionTargetPickerRef.value?.reset();
    npcRollResult.value = '';
    npcDamageRollResult.value = '';
    resetNpcInlineFields();
    activeCombatEntryId.value = null;
    refreshInBackground();
    toastSuccess('NPC action resolved.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

const sessionRollPrompts = computed(() =>
  (state.value?.rollPrompts ?? []).filter(p => p.isSessionPrompt),
);

async function sendSessionRollPrompts(payload: {
  prompts: Array<{
    targetCharacterId: string;
    checkMode: string;
    skillKey?: string;
    attributeKey?: string;
    promptLabel?: string;
    resultKind: string;
  }>;
}) {
  if (!state.value) return;
  isSaving.value = true;
  try {
    await api(`/api/sessions/${state.value.id}/roll-prompts`, { method: 'POST', body: payload });
    refreshInBackground();
    toastSuccess('Stat check sent to player(s).');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

async function startRollChain(actionId: string) {
  isSaving.value = true;
  try {
    await api(`/api/actions/${actionId}/roll-prompts/start-chain`, { method: 'POST' });
    refreshInBackground();
    toastSuccess('Attack roll sent to player.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

function applyPendingChainEffects(action: ActionQueueItemResponse) {
  const raw = action.pendingChainEffectsJson;
  if (!raw || raw === '[]') return;
  try {
    const pending = JSON.parse(raw) as Array<{
      targetType: string;
      targetId: string;
      healthDelta?: number;
    }>;
    if (!pending.length) return;
    const first = pending[0];
    statChangeTarget.value[action.id] = `${first.targetType}:${first.targetId}`;
    const totalDamage = pending.reduce((sum, c) => sum + (c.healthDelta ?? 0), 0);
    if (totalDamage < 0) {
      statChangeHealthDelta.value[action.id] = String(totalDamage);
    }
  } catch {
    // ignore malformed JSON
  }
}

async function sendRollPrompts(
  actionId: string,
  payload: {
    prompts: Array<{
      targetCharacterId: string;
      checkMode: string;
      actionKey?: string;
      skillKey?: string;
      attributeKey?: string;
      customCheckText?: string;
      promptLabel?: string;
      guidanceText?: string;
      resultKind: string;
      dc?: number | null;
      chainStepKey?: string;
    }>;
  },
) {
  isSaving.value = true;
  try {
    await api(`/api/actions/${actionId}/roll-prompts`, { method: 'POST', body: payload });
    refreshInBackground();
    toastSuccess('Roll prompt sent to player(s).');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

async function dmRollForPlayer(payload: {
  actionId: string;
  rollSummary: string;
  rollResultJson?: string;
  dc?: number | null;
  chainStepKey?: string;
}) {
  isSaving.value = true;
  try {
    await api(`/api/actions/${payload.actionId}/roll-prompts/dm-roll`, {
      method: 'POST',
      body: {
        rollSummary: payload.rollSummary,
        rollResultJson: payload.rollResultJson,
        dc: payload.dc ?? null,
        chainStepKey: payload.chainStepKey,
      },
    });
    refreshInBackground();
    toastSuccess('Roll recorded for player.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

async function cancelRollPrompt(promptId: string) {
  isSaving.value = true;
  try {
    await api(`/api/roll-prompts/${promptId}`, { method: 'DELETE' });
    refreshInBackground();
    toastInfo('Roll prompt cancelled.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

function derivedActionOutcome(action: ActionQueueItemResponse): 'Pass' | 'Fail' | null {
  return evaluateActionOutcomeFromRolls(
    rulesetDefinition.value,
    action,
    state.value?.rollPrompts ?? [],
    game.value,
  );
}

function pendingRollFlowStatus(action: ActionQueueItemResponse) {
  return getActionRollFlowStatus(action, state.value?.rollPrompts ?? [], rulesetDefinition.value);
}

async function rejectAction(action: ActionQueueItemResponse) {
  isSaving.value = true;
  try {
    await api(`/api/actions/${action.id}/reject`, {
      method: 'PUT',
      body: {
        rejectionReason: rejectReason.value[action.id]?.trim() || undefined,
      },
    });
    for (const map of [resolutionText, rejectReason, statChangeTarget, statChangeHealthDelta, statChangeSetHealth, statChangeSetArmor, gameValueChanges, statChangeGvDeltas, statChangeAttrDeltas, statChangeInventoryDeltas, statChangeStatusChanges]) {
      delete map.value[action.id];
    }
    expandedPendingActions.value.delete(action.id);
    refreshInBackground();
    toastInfo('Action rejected.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

async function resolveAction(action: ActionQueueItemResponse) {
  isSaving.value = true;
  try {
    const updated = await api<ActionQueueItemResponse>(`/api/actions/${action.id}/resolve`, {
      method: 'PUT',
      body: {
        resolutionText: resolutionText.value[action.id] || undefined,
        statChanges: buildStatChanges(action),
      },
    });
    for (const map of [resolutionText, rejectReason, statChangeTarget, statChangeHealthDelta, statChangeSetHealth, statChangeSetArmor, gameValueChanges, statChangeGvDeltas, statChangeAttrDeltas, statChangeInventoryDeltas, statChangeStatusChanges]) {
      delete map.value[action.id];
    }
    expandedPendingActions.value.delete(action.id);
    if (state.value) {
      state.value = replaceActionInState(state.value, updated);
    }
    refreshInBackground();
    toastSuccess('Resolution published to players.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

/** Called by ActionEvaluationPanel resolve event — merges panel state into local maps then resolves. */
async function resolveActionFromPanel(action: ActionQueueItemResponse, payload: {
  resolutionText: string;
  statTarget: string;
  statHealthDelta: string;
  statSetHealth: string;
  statSetArmor: string;
  statGvDeltas: Record<string, string>;
  statAttrDeltas: Record<string, string>;
  statInventoryDeltas: Record<string, string>;
  statStatusChanges: { addKeys: string[]; removeKeys: string[] };
}) {
  resolutionText.value[action.id] = payload.resolutionText;
  statChangeTarget.value[action.id] = payload.statTarget;
  statChangeHealthDelta.value[action.id] = payload.statHealthDelta;
  statChangeSetHealth.value[action.id] = payload.statSetHealth;
  statChangeSetArmor.value[action.id] = payload.statSetArmor;
  statChangeGvDeltas.value[action.id] = payload.statGvDeltas;
  statChangeAttrDeltas.value[action.id] = payload.statAttrDeltas;
  statChangeInventoryDeltas.value[action.id] = payload.statInventoryDeltas;
  statChangeStatusChanges.value[action.id] = payload.statStatusChanges;
  await resolveAction(action);
}

/** Called by ActionEvaluationPanel reject event. */
async function rejectActionWithReason(action: ActionQueueItemResponse, reason: string) {
  rejectReason.value[action.id] = reason;
  await rejectAction(action);
}

function buildStatChanges(action: ActionQueueItemResponse) {
  const actionId = action.id;

  type StatChange = {
    targetType: string;
    targetId: string;
    healthDelta?: number;
    setHealth?: number;
    setArmor?: number;
    gameValueDeltas?: Record<string, number>;
    attributeDeltas?: Record<string, number>;
    inventoryDeltas?: Record<string, number>;
    addStatusKeys?: string[];
    removeStatusKeys?: string[];
  };
  const changes: StatChange[] = [];

  // Health / armor + stat deltas + status changes for the selected target
  const target = statChangeTarget.value[actionId];
  if (target) {
    const [targetType, targetId] = target.split(':');
    const healthDelta = optNum(statChangeHealthDelta.value[actionId]);
    const setHealth = optNum(statChangeSetHealth.value[actionId]);
    const setArmor = optNum(statChangeSetArmor.value[actionId]);

    let gameValueDeltas: Record<string, number> | undefined;
    let attributeDeltas: Record<string, number> | undefined;
    let inventoryDeltas: Record<string, number> | undefined;
    if (targetType === 'Character') {
      gameValueDeltas = toNonZeroIntMap(statChangeGvDeltas.value[actionId]);
      attributeDeltas = toNonZeroIntMap(statChangeAttrDeltas.value[actionId]);
      inventoryDeltas = toNonZeroIntMap(statChangeInventoryDeltas.value[actionId]);
    }

    const statusChange = statChangeStatusChanges.value[actionId];
    const addStatusKeys = statusChange?.addKeys?.length ? statusChange.addKeys : undefined;
    const removeStatusKeys = statusChange?.removeKeys?.length ? statusChange.removeKeys : undefined;

    const hasAny = healthDelta !== undefined || setHealth !== undefined || setArmor !== undefined
      || (gameValueDeltas && Object.keys(gameValueDeltas).length > 0)
      || (attributeDeltas && Object.keys(attributeDeltas).length > 0)
      || (inventoryDeltas && Object.keys(inventoryDeltas).length > 0)
      || addStatusKeys !== undefined
      || removeStatusKeys !== undefined;

    if (hasAny) {
      changes.push({ targetType, targetId, healthDelta, setHealth, setArmor, gameValueDeltas, attributeDeltas, inventoryDeltas, addStatusKeys, removeStatusKeys });
    }
  }

  return changes;
}

/** Convert a string-value map to a Record<string, number>, keeping only non-zero entries. */
function toNonZeroIntMap(raw?: Record<string, string>): Record<string, number> | undefined {
  if (!raw) return undefined;
  const out: Record<string, number> = {};
  for (const [k, v] of Object.entries(raw)) {
    const n = optNum(v);
    if (n !== undefined && n !== 0) out[k] = n;
  }
  return Object.keys(out).length ? out : undefined;
}

// (gameValueChanges kept in state/cleanup for backward-compat but no longer shown in UI)

function optNum(v?: string | number) {
  const s = String(v ?? '').trim();
  return s !== '' && !Number.isNaN(Number(s)) ? Number(s) : undefined;
}

async function setupCombat() {
  if (!state.value) return;
  isSaving.value = true;

  try {
    const result = await api<{ guidanceText?: string | null }>(`/api/sessions/${route.params.id}/combat/start`, { method: 'POST' });
    refreshInBackground();
    const hint = result.guidanceText ? ` ${result.guidanceText}` : '';
    toastSuccess(`Combat started — initiative rolled.${hint}`);
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

async function saveInitiativeOrder(entries: InitiativeEntryResponse[]) {
  if (!state.value) return;

  isSaving.value = true;
  const combatants = entries.map((entry, index) => ({
    type: entry.combatantType,
    id: entry.combatantId,
    initiative: entries.length - index,
  }));

  try {
    await api(`/api/sessions/${route.params.id}/combat`, { method: 'POST', body: { combatants } });
    refreshInBackground();
    toastSuccess('Initiative order updated.');
  } finally {
    isSaving.value = false;
  }
}

async function endCombat() {
  await setState('Exploration');
}

async function advanceTurn() {
  isSaving.value = true;
  try {
    await api(`/api/sessions/${route.params.id}/combat/advance`, { method: 'POST' });
    refreshInBackground();
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

/** DM prompts the current-turn character to take their action — opens the action form on the player's device. */
async function promptPlayerTurn(characterId: string) {
  if (!state.value) return;
  isSaving.value = true;
  try {
    await api(`/api/sessions/${route.params.id}/combat/prompt-turn`, {
      method: 'POST',
      body: { characterId },
    });
    refreshInBackground();
    toastSuccess('Player has been prompted to take their action.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

/** Called when DM clicks Prompt/Act on a character's initiative entry. */
async function handlePromptTurn(entry: InitiativeEntryResponse) {
  await promptPlayerTurn(entry.combatantId);
}

async function stopSession() {
  isSaving.value = true;
  try {
    await api(`/api/sessions/${route.params.id}/stop`, { method: 'POST' });
    refreshInBackground();
    toastInfo('Session ended.');
    showStopSessionConfirm.value = false;
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

async function cycleNpcVisibility(npcId: string, current: string) {
  const next = current === 'Visible' ? 'Hidden' : 'Visible';
  try {
    await api(`/api/sessions/${route.params.id}/npc-visibility`, {
      method: 'POST',
      body: { npcId, visibility: next },
    });
    refreshInBackground();
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  }
}

async function onNpcCreated() {
  refreshInBackground();
}

async function onNpcUpdated() {
  refreshInBackground();
}

async function copyJoinLink() {
  if (!state.value) return;
  if (import.meta.client && navigator.clipboard) {
    await navigator.clipboard.writeText(joinLink.value);
    toastSuccess('Join link copied!');
  }
}

function toggleAction(id: string) {
  if (expandedActions.value.has(id)) expandedActions.value.delete(id);
  else expandedActions.value.add(id);
}

function togglePendingAction(id: string) {
  if (expandedPendingActions.value.has(id)) {
    expandedPendingActions.value.delete(id);
  } else {
    expandedPendingActions.value.add(id);
    const action = pendingActions.value.find(a => a.id === id);
    if (action) applyPendingChainEffects(action);
  }
}

function expandAllPendingActions() {
  expandedPendingActions.value = new Set(explorationPendingActions.value.map(action => action.id));
}

function pendingStatChecksForCharacter(characterId: string) {
  return pendingActions.value.filter(a =>
    isStatCheckAction(a)
    && a.actorCharacterId === characterId,
  );
}

/** Returns player-submitted combat actions (non-stat-check, in a combat encounter) awaiting DM evaluation. */
function pendingCombatActionsForCharacter(characterId: string) {
  return pendingActions.value.filter(a =>
    !isStatCheckAction(a)
    && a.actorCharacterId === characterId
    && a.combatEncounterId != null,
  );
}

function collapseAllPendingActions() {
  expandedPendingActions.value = new Set();
}

function expandAllActions() {
  expandAllGroups();
  expandedActions.value = new Set(publishedActions.value.map(action => action.id));
}

function collapseAllActions() {
  collapseAllGroups();
  expandedActions.value = new Set();
}


function handleActivateEntry(entry: InitiativeEntryResponse) {
  if (!entry.isCurrentTurn) return;

  // Toggle: clicking an already-expanded entry collapses it
  if (activeCombatEntryId.value === entry.id) {
    activeCombatEntryId.value = null;
    return;
  }
  activeCombatEntryId.value = entry.id;

  if (entry.combatantType === 'NpcOrMonster') {
    // Pre-select this NPC and reset both the action form and inline resolution fields
    selectedNpcId.value = entry.combatantId;
    resetNpcActionSelection();
    npcRollResult.value = '';
    npcDamageRollResult.value = '';
    resetNpcInlineFields();
  }
}

watch(currentNpcPendingAction, (action) => {
  if (action) applyPendingChainEffects(action);
});

watch(currentTurn, () => {
  const expanded = displayedInitiative.value.find(e => e.id === activeCombatEntryId.value);
  if (expanded && !expanded.isCurrentTurn) {
    activeCombatEntryId.value = null;
  }
});

function rulesetActionDetail(action: ActionQueueItemResponse) {
  const rulesetAction = findRulesetAction(rulesetDefinition.value, action.actionKey);
  return rulesetAction && rulesetDefinition.value
    ? describeRulesetAction(rulesetAction, rulesetDefinition.value)
    : null;
}

/** Roll context for the NPC action form (uses the selected NPC's stats). */
const npcStats = computed(() => {
  if (!selectedNpc.value) {
    return { attributes: {} as Record<string, number>, skills: {} as Record<string, number>, gameValues: {} as Record<string, number> };
  }
  const parsed = parseCharacterStats(selectedNpc.value.statBlockJson);
  const flat = parseStatMap(selectedNpc.value.statBlockJson);
  return {
    attributes: Object.keys(parsed.attributes).length ? parsed.attributes : flat,
    skills: Object.keys(parsed.skills).length ? parsed.skills : flat,
    gameValues: parsed.gameValues,
  };
});

const selectedNpcActionDef = computed(() =>
  findRulesetAction(rulesetDefinition.value, selectedNpcActionKey.value),
);
const effectiveNpcActionRoll = computed(() =>
  resolveEffectiveActionRoll(rulesetDefinition.value, selectedNpcActionDef.value),
);
const npcTargetArmor = computed(() => {
  void npcTargetPickerRevision.value;
  const fields = npcActionTargetPickerRef.value?.toSubmitFields();
  if (!fields || !game.value) return null;
  return resolveTargetArmor(
    {
      targetCharacterId: fields.targetCharacterId,
      targetNpcId: fields.targetNpcId,
    },
    game.value,
  );
});
const npcAttackOutcome = computed(() => {
  if (!npcRollResult.value || npcActionMode.value !== 'action') return null;
  return evaluateActionOutcome(
    rulesetDefinition.value,
    selectedNpcActionKey.value,
    `🎲 Roll: ${npcRollResult.value}`,
    npcTargetArmor.value,
  );
});
const showNpcDamageRoll = computed(() => {
  if (npcActionMode.value !== 'action' || npcAttackOutcome.value !== 'Pass') return false;
  return Boolean(
    effectiveNpcActionRoll.value?.damageRoll
    ?? selectedNpcActionDef.value?.damageRoll,
  );
});

const npcDamageRollDefinition = computed(() =>
  effectiveNpcActionRoll.value?.damageRoll
  ?? selectedNpcActionDef.value?.damageRoll
  ?? null,
);
const npcActionNeedsRoll = computed(() =>
  npcActionMode.value === 'action' && Boolean(selectedNpcActionDef.value?.roll),
);
const npcActionHasRollChain = computed(() =>
  npcActionMode.value === 'action' && Boolean(selectedNpcActionDef.value?.rollChain?.length),
);
const npcHasPrimaryRoll = computed(() => Boolean(npcRollResult.value.trim()));
const npcHasFollowUpRoll = computed(() => Boolean(npcDamageRollResult.value.trim()));
const npcNeedsFollowUpRoll = computed(() =>
  npcActionHasRollChain.value && showNpcDamageRoll.value,
);

function bumpNpcTargetPickerRevision() {
  npcTargetPickerRevision.value += 1;
}

function npcTargetFieldsSelected() {
  void npcTargetPickerRevision.value;
  const fields = npcActionTargetPickerRef.value?.toSubmitFields();
  return Boolean(fields?.targetCharacterId || fields?.targetNpcId || fields?.targetName);
}

const npcCombatActionSelected = computed(() => {
  if (npcActionMode.value !== 'action' || !selectedNpcActionDef.value) return false;
  return actionRequiresTarget(selectedNpcActionDef.value);
});

const npcTargetMissing = computed(() =>
  npcCombatActionSelected.value && !npcTargetFieldsSelected(),
);

const canResolveNpcInlineAction = computed(() => {
  void npcTargetPickerRevision.value;
  if (isSaving.value || !selectedNpcId.value) return false;
  if (!buildNpcActionSubmitPayload()) return false;
  if (!npcActionTargetPickerRef.value?.isValid()) return false;
  if (npcTargetMissing.value) return false;
  if (npcActionNeedsRoll.value && !npcHasPrimaryRoll.value) return false;
  if (npcNeedsFollowUpRoll.value && !npcHasFollowUpRoll.value) return false;
  return true;
});

function validateNpcRollRequirements() {
  if (!npcActionNeedsRoll.value) return true;
  if (!npcHasPrimaryRoll.value) {
    toastError('Roll the NPC action before submitting.');
    return false;
  }
  if (npcNeedsFollowUpRoll.value && !npcHasFollowUpRoll.value) {
    toastError('This NPC roll chain needs a follow-up damage roll before submitting.');
    return false;
  }
  return true;
}

const npcRollContext = computed(() => {
  const def = rulesetDefinition.value;
  if (!def || !selectedNpc.value) return null;

  let mode: DiceRollMode;
  let skillKey = '';
  let attributeKey = '';

  if (npcActionMode.value === 'action') {
    mode = 'action';
  } else if (npcActionMode.value === 'stat-check') {
    if (selectedNpcStatType.value === 'attribute') {
      mode = 'attribute';
      attributeKey = selectedNpcStatRawKey.value;
    } else {
      mode = 'skill';
      skillKey = selectedNpcStatRawKey.value;
    }
  } else {
    return null;
  }

  return buildDiceRollContext({
    definition: def,
    mode,
    actionKey: selectedNpcActionKey.value,
    skillKey,
    attributeKey,
    attributes: npcStats.value.attributes,
    skills: npcStats.value.skills,
    gameValues: npcStats.value.gameValues,
  });
});

// ─── Browser inactivity auto-end (15 min) ────────────────────────────────────
const BROWSER_IDLE_MS = 15 * 60 * 1000;
const IDLE_WARN_MS = 60 * 1000; // show warning 1 min before auto-end

const lastActivityAt = ref(Date.now());
const idleWarningVisible = ref(false);
const idleSecondsRemaining = ref(0);

function onUserActivity() {
  lastActivityAt.value = Date.now();
  idleWarningVisible.value = false;
}

let idleIntervalId: ReturnType<typeof setInterval> | null = null;

function startIdleWatcher() {
  const EVENTS = ['mousemove', 'mousedown', 'keydown', 'touchstart', 'scroll'] as const;
  EVENTS.forEach(e => window.addEventListener(e, onUserActivity, { passive: true }));

  idleIntervalId = setInterval(() => {
    if (!state.value?.isActive) return;
    const idleMs = Date.now() - lastActivityAt.value;
    const remaining = BROWSER_IDLE_MS - idleMs;
    if (remaining <= 0) {
      stopSession();
    } else if (remaining <= IDLE_WARN_MS) {
      idleSecondsRemaining.value = Math.ceil(remaining / 1000);
      idleWarningVisible.value = true;
    } else {
      idleWarningVisible.value = false;
    }
  }, 5000);
}

function stopIdleWatcher() {
  const EVENTS = ['mousemove', 'mousedown', 'keydown', 'touchstart', 'scroll'] as const;
  EVENTS.forEach(e => window.removeEventListener(e, onUserActivity));
  if (idleIntervalId !== null) {
    clearInterval(idleIntervalId);
    idleIntervalId = null;
  }
}

onUnmounted(() => {
  if (import.meta.client) stopIdleWatcher();
});
</script>

<template>

  <section class="app-shell dm-app-shell" :style="rulesetThemeStyle">
    <!-- Topbar -->
    <header class="topbar" :class="{ 'combat-mode': isCombat }">
      <div class="topbar-brand">
        <span class="topbar-wordmark">TTRPG TABLE</span>
        <div>
          <strong>DM Screen</strong>
          <div class="topbar-sub">{{ game?.name }}</div>
        </div>
      </div>
      <div v-if="state" class="topbar-status">
        <SessionConnectionStatus
          :status="connectionStatus"
          :hub-connected="hubConnected"
          :error="pollingError"
          :started-at="state.startedAt"
          :ended-at="state.endedAt"
          :is-active="state.isActive"
        />
        <span class="badge" :class="isCombat ? 'combat' : 'exploration'">{{ state.state }}</span>
        <button
          class="theme-toggle"
          :class="{ on: rulesetThemeEnabled }"
          type="button"
          :aria-pressed="rulesetThemeEnabled"
          :title="rulesetThemeEnabled ? 'Ruleset theme: on' : 'Ruleset theme: off'"
          @click="toggleRulesetTheme"
        >
          <span class="theme-toggle-track"><span class="theme-toggle-thumb" /></span>
          <span class="theme-toggle-label">Theme</span>
        </button>
      </div>
      <div class="topbar-actions" v-if="state">
        <NuxtLink class="btn ghost sm" to="/games">← Games</NuxtLink>
        <NuxtLink v-if="!state.isActive" class="btn ghost sm" :to="`/sessions/${route.params.id}/summary`">
          View Summary
        </NuxtLink>
        <button v-if="state.isActive" class="btn danger sm" type="button" :disabled="isSaving" @click="showStopSessionConfirm = true">
          Stop Session
        </button>
        <span v-else class="badge ended">Ended</span>
      </div>
    </header>

    <div v-if="!state" class="stack">
      <SkeletonBlock :lines="4" />
      <div class="session-dashboard-grid">
        <SkeletonBlock :lines="8" />
        <SkeletonBlock :lines="6" />
      </div>
    </div>

    <main
      v-else
      class="stack dm-screen-main"
    >
      <DmSessionInvite
        :state="state"
        :join-link="joinLink"
        @copy="copyJoinLink"
      />

      <SessionNotesPanel
        v-if="state"
        mode="dm"
        :session-id="state.id"
      />

      <div class="dm-two-col-grid">
        <!-- LEFT: Action column — combat, pending queue, action log -->
        <div class="dm-action-column">
          <!-- dm-combat-section groups combat + aux tools for portrait reordering -->
          <div class="dm-combat-section">
          <DmCombatWorkflow
            :is-combat="isCombat"
            :is-saving="isSaving"
            :current-turn="currentTurn"
            :displayed-initiative="displayedInitiative"
            :dragged-initiative-id="draggedInitiativeId"
            :drag-over-id="dragOverId"
            :expanded-entry-id="activeCombatEntryId"
            :round="currentRound"
            :prompted-turn-character-id="activeEncounter?.promptedTurnCharacterId"
            @setup-combat="setupCombat"
            @advance-turn="advanceTurn"
            @end-combat="endCombat"
            @start-drag="startInitiativeDrag"
            @move-keyboard="moveInitiativeByKeyboard"
            @activate-entry="handleActivateEntry"
            @prompt-turn="handlePromptTurn"
          >
            <template v-if="state" #entry-action="{ entry }">
              <!-- Player turn — evaluate submitted action or wait -->
              <div v-if="entry.combatantType === 'Character'" class="turn-inline-body">

                <!-- Player-submitted combat action: evaluate inline -->
                <template v-if="pendingCombatActionsForCharacter(entry.combatantId).length">
                  <ActionEvaluationPanel
                    v-for="action in pendingCombatActionsForCharacter(entry.combatantId)"
                    :key="action.id"
                    :action="action"
                    :characters="state.game.characters"
                    :npcs="state.game.npcsAndMonsters"
                    :roll-prompts="state.rollPrompts ?? []"
                    :ruleset-definition="rulesetDefinition"
                    :is-busy="isSaving"
                    :resolution-text="resolutionText[action.id] ?? ''"
                    :stat-target="statChangeTarget[action.id] ?? ''"
                    :stat-health-delta="statChangeHealthDelta[action.id] ?? ''"
                    :stat-set-health="statChangeSetHealth[action.id] ?? ''"
                    :stat-set-armor="statChangeSetArmor[action.id] ?? ''"
                    :stat-gv-deltas="statChangeGvDeltas[action.id] ?? {}"
                    :stat-attr-deltas="statChangeAttrDeltas[action.id] ?? {}"
                    :stat-inventory-deltas="statChangeInventoryDeltas[action.id] ?? {}"
                    :stat-status-changes="statChangeStatusChanges[action.id] ?? { addKeys: [], removeKeys: [] }"
                    @resolve="payload => resolveActionFromPanel(action, payload)"
                    @reject="reason => rejectActionWithReason(action, reason)"
                    @start-chain="startRollChain(action.id)"
                    @send-roll-prompts="payload => sendRollPrompts(action.id, payload)"
                    @cancel-prompt="cancelRollPrompt"
                    @dm-roll="dmRollForPlayer"
                    @update:resolution-text="resolutionText[action.id] = $event"
                    @update:stat-target="statChangeTarget[action.id] = $event"
                    @update:stat-health-delta="statChangeHealthDelta[action.id] = $event"
                    @update:stat-set-health="statChangeSetHealth[action.id] = $event"
                    @update:stat-set-armor="statChangeSetArmor[action.id] = $event"
                    @update:stat-gv-deltas="statChangeGvDeltas[action.id] = $event"
                    @update:stat-attr-deltas="statChangeAttrDeltas[action.id] = $event"
                    @update:stat-inventory-deltas="statChangeInventoryDeltas[action.id] = $event"
                    @update:stat-status-changes="statChangeStatusChanges[action.id] = $event"
                  />
                </template>
                <div
                  v-else-if="activeEncounter?.promptedTurnCharacterId === entry.combatantId"
                  class="alert success"
                  style="margin-bottom: 0.75rem;"
                >
                  <p class="text-sm" style="margin: 0;">
                    <strong>{{ entry.combatantName }}</strong> has been prompted — waiting for them to submit their action.
                  </p>
                </div>
                <div v-else class="alert info" style="margin-bottom: 0.75rem;">
                  <p class="text-sm" style="margin: 0;">
                    Close this panel and click <strong>Prompt</strong> on the initiative card to send {{ entry.combatantName }} their action form.
                  </p>
                </div>

                <!-- Resolve in-flight stat checks from earlier sessions -->
                <div
                  v-for="action in pendingStatChecksForCharacter(entry.combatantId)"
                  :key="action.id"
                  class="panel nested"
                  style="margin-bottom: 0.75rem;"
                >
                  <p class="text-sm" style="margin: 0 0 0.5rem;">
                    <strong>{{ action.actionText }}</strong>
                    <span v-if="action.targetName"> — {{ action.targetName }}</span>
                  </p>
                  <DmFollowUpRollPanel
                    v-if="state"
                    :action="action"
                    :characters="state.game.characters"
                    :roll-prompts="state.rollPrompts ?? []"
                    :ruleset-definition="rulesetDefinition"
                    :is-busy="isSaving"
                    @start-chain="startRollChain(action.id)"
                    @send="payload => sendRollPrompts(action.id, payload)"
                    @cancel="cancelRollPrompt"
                    @dm-roll="dmRollForPlayer"
                  />
                  <form style="margin-top: 0.75rem;" @submit.prevent="resolveAction(action)">
                    <DmStatChangePanel
                      :characters="state.game.characters"
                      :npcs="state.game.npcsAndMonsters"
                      :ruleset-definition="rulesetDefinition"
                      :target="statChangeTarget[action.id]"
                      :health-delta="statChangeHealthDelta[action.id]"
                      :set-health="statChangeSetHealth[action.id]"
                      :set-armor="statChangeSetArmor[action.id]"
                      :gv-deltas="statChangeGvDeltas[action.id]"
                      :attr-deltas="statChangeAttrDeltas[action.id]"
                      :inventory-deltas="statChangeInventoryDeltas[action.id]"
                      :status-changes="statChangeStatusChanges[action.id]"
                      @update:target="statChangeTarget[action.id] = $event"
                      @update:health-delta="statChangeHealthDelta[action.id] = $event"
                      @update:set-health="statChangeSetHealth[action.id] = $event"
                      @update:set-armor="statChangeSetArmor[action.id] = $event"
                      @update:gv-deltas="statChangeGvDeltas[action.id] = $event"
                      @update:attr-deltas="statChangeAttrDeltas[action.id] = $event"
                      @update:inventory-deltas="statChangeInventoryDeltas[action.id] = $event"
                      @update:status-changes="statChangeStatusChanges[action.id] = $event"
                    />
                    <button class="btn success" type="submit" :disabled="isSaving" style="margin-top: 0.5rem;">
                      Publish resolution
                    </button>
                  </form>
                </div>
              </div>

              <!-- NPC card — single combined action + resolution form -->
              <form v-else class="turn-inline-body" novalidate @submit.prevent="submitAndResolveNpcAction">
                <RulesetActionFields
                  v-model:action-mode="npcActionMode"
                  v-model:selected-action-key="selectedNpcActionKey"
                  v-model:selected-stat-key="selectedNpcStatKey"
                  :available-actions="availableNpcActions"
                  :available-stat-checks="availableNpcStatChecks"
                  :selected-action-detail="selectedNpcActionDetail"
                  :selected-stat-detail="selectedNpcStatDetail"
                  :disabled="!selectedNpcId || isSaving"
                />

                <!-- Target must be set before dice rollers appear -->
                <ActionTargetPicker
                  ref="npcActionTargetPickerRef"
                  :characters="state.game.characters"
                  :npcs="state.game.npcsAndMonsters"
                  :disabled="isSaving"
                  @change="bumpNpcTargetPickerRevision"
                />

                <p
                  v-if="npcTargetMissing"
                  class="text-sm"
                  style="color: var(--danger); margin: 0.5rem 0 0;"
                >
                  A target is required for combat actions.
                </p>

                <template v-if="npcRollContext">
                  <p class="text-sm muted" style="margin: 0.5rem 0 0.25rem;">
                    {{ npcActionNeedsRoll ? 'Roll before resolving this NPC action.' : 'Roll on the table (optional).' }}
                  </p>
                  <RulesetDiceRoller v-model="npcRollResult" :context="npcRollContext" />
                  <p v-if="npcActionMode === 'action' && npcAttackOutcome" class="roll-outcome-line">
                    Roll outcome:
                    <span class="badge" :class="npcAttackOutcome === 'Pass' ? 'pass' : 'fail'">{{ npcAttackOutcome }}</span>
                  </p>
                  <DamageRollRoller
                    v-if="showNpcDamageRoll && npcDamageRollDefinition && rulesetDefinition"
                    v-model="npcDamageRollResult"
                    :damage-roll="npcDamageRollDefinition"
                    :definition="rulesetDefinition"
                    :attributes="npcStats.attributes"
                  />
                </template>
                <p v-else-if="npcNeedsFollowUpRoll && !npcHasFollowUpRoll" class="text-sm muted" style="margin: 0.5rem 0 0;">
                  This attack hit — roll damage before resolving.
                </p>

                <hr style="margin: 0.75rem 0; border-color: var(--border);" />

                <DmStatChangePanel
                  :characters="state.game.characters"
                  :npcs="state.game.npcsAndMonsters"
                  :ruleset-definition="rulesetDefinition"
                  :target="npcInlineStatTarget"
                  :health-delta="npcInlineHealthDelta"
                  :set-health="npcInlineSetHealth"
                  :set-armor="npcInlineSetArmor"
                  :gv-deltas="npcInlineGvDeltas"
                  :attr-deltas="npcInlineAttrDeltas"
                  :inventory-deltas="npcInlineInventoryDeltas"
                  :status-changes="npcInlineStatusChanges"
                  @update:target="npcInlineStatTarget = $event"
                  @update:health-delta="npcInlineHealthDelta = $event"
                  @update:set-health="npcInlineSetHealth = $event"
                  @update:set-armor="npcInlineSetArmor = $event"
                  @update:gv-deltas="npcInlineGvDeltas = $event"
                  @update:attr-deltas="npcInlineAttrDeltas = $event"
                  @update:inventory-deltas="npcInlineInventoryDeltas = $event"
                  @update:status-changes="npcInlineStatusChanges = $event"
                />
                <button
                  class="btn"
                  :class="{ success: canResolveNpcInlineAction && !isSaving }"
                  type="submit"
                  :disabled="!canResolveNpcInlineAction || isSaving"
                  style="margin-top: 0.75rem;"
                >
                  {{ isSaving ? 'Resolving…' : 'Resolve NPC action' }}
                </button>
              </form>
            </template>
          </DmCombatWorkflow>

          </div><!-- /dm-combat-section -->

          <div v-if="!isCombat" class="panel dashboard-primary-panel pending-actions-panel">
            <div class="panel-title">
              <div>
                <h2>
                  Pending Actions
                  <span v-if="explorationPendingActions.length" class="badge pending" style="margin-left: 0.4rem;">{{ explorationPendingActions.length }}</span>
                </h2>
                <p class="text-sm">Review submissions, prompt player rolls, then publish outcomes. During combat, resolve from initiative instead.</p>
              </div>
              <div v-if="explorationPendingActions.length" class="btn-row">
                <button class="btn ghost sm" type="button" @click="expandAllPendingActions">Expand</button>
                <button class="btn ghost sm" type="button" @click="collapseAllPendingActions">Collapse</button>
              </div>
            </div>

            <div v-if="explorationPendingActions.length === 0" class="empty-state" style="padding: 1.5rem 0;">
              <p class="text-sm">No actions waiting. Players can submit actions via their session link.</p>
            </div>

            <div class="pending-actions-list">
              <div v-for="action in explorationPendingActions" :key="action.id" class="action-card pending-card">
                <button
                  class="action-card-header action-card-toggle"
                  type="button"
                  :aria-expanded="expandedPendingActions.has(action.id)"
                  @click="togglePendingAction(action.id)"
                >
                  <div>
                    <div class="action-card-actor">
                      {{ action.actorName }}
                      <span v-if="action.isSkillCheckResponse" class="badge" style="margin-left: 0.35rem;">Stat check</span>
                    </div>
                    <div class="action-card-target">
                      uses <strong>{{ action.actionText }}</strong>
                      <span v-if="action.targetName"> on {{ action.targetName }}</span>
                    </div>
                    <div v-if="action.description" class="action-card-desc">{{ action.description }}</div>
                    <span
                      v-if="pendingRollFlowStatus(action) !== 'not-applicable'"
                      class="badge"
                      :class="actionRollFlowBadgeClass(pendingRollFlowStatus(action))"
                      style="margin-top: 0.35rem;"
                    >
                      {{ actionRollFlowLabel(pendingRollFlowStatus(action)) }}
                    </span>
                  </div>
                  <span class="badge pending">{{ expandedPendingActions.has(action.id) ? 'Hide' : 'Resolve' }}</span>
                </button>

                <ActionEvaluationPanel
                  v-if="expandedPendingActions.has(action.id) && state"
                  :action="action"
                  :characters="state.game.characters"
                  :npcs="state.game.npcsAndMonsters"
                  :roll-prompts="state.rollPrompts ?? []"
                  :ruleset-definition="rulesetDefinition"
                  :is-busy="isSaving"
                  :resolution-text="resolutionText[action.id] ?? ''"
                  :stat-target="statChangeTarget[action.id] ?? ''"
                  :stat-health-delta="statChangeHealthDelta[action.id] ?? ''"
                  :stat-set-health="statChangeSetHealth[action.id] ?? ''"
                  :stat-set-armor="statChangeSetArmor[action.id] ?? ''"
                  :stat-gv-deltas="statChangeGvDeltas[action.id] ?? {}"
                  :stat-attr-deltas="statChangeAttrDeltas[action.id] ?? {}"
                  :stat-inventory-deltas="statChangeInventoryDeltas[action.id] ?? {}"
                  :stat-status-changes="statChangeStatusChanges[action.id] ?? { addKeys: [], removeKeys: [] }"
                  @resolve="payload => resolveActionFromPanel(action, payload)"
                  @reject="reason => rejectActionWithReason(action, reason)"
                  @start-chain="startRollChain(action.id)"
                  @send-roll-prompts="payload => sendRollPrompts(action.id, payload)"
                  @cancel-prompt="cancelRollPrompt"
                  @dm-roll="dmRollForPlayer"
                  @update:resolution-text="resolutionText[action.id] = $event"
                  @update:stat-target="statChangeTarget[action.id] = $event"
                  @update:stat-health-delta="statChangeHealthDelta[action.id] = $event"
                  @update:stat-set-health="statChangeSetHealth[action.id] = $event"
                  @update:stat-set-armor="statChangeSetArmor[action.id] = $event"
                  @update:stat-gv-deltas="statChangeGvDeltas[action.id] = $event"
                  @update:stat-attr-deltas="statChangeAttrDeltas[action.id] = $event"
                  @update:stat-inventory-deltas="statChangeInventoryDeltas[action.id] = $event"
                  @update:stat-status-changes="statChangeStatusChanges[action.id] = $event"
                />
              </div>
            </div>
          </div>


          <DmActionLog
            :actions="publishedActions"
            :combat-encounters="state.combatEncounters ?? []"
            :expanded-actions="expandedActions"
            :expanded-groups="expandedLogGroups"
            :game="state.game"
            :ruleset-definition="rulesetDefinition"
            @toggle-action="toggleAction"
            @toggle-group="toggleLogGroup"
            @expand-all="expandAllActions"
            @collapse-all="collapseAllActions"
          />
        </div><!-- /dm-action-column -->

        <!-- RIGHT: Reference column — participants, stat checks, exploration NPC form -->
        <div class="dm-reference-column">
          <div v-if="!isCombat" class="dm-exploration-tools">
            <DmPlayerSkillCheckPanel
              v-if="game?.characters.length"
              :characters="game.characters"
              :roll-prompts="sessionRollPrompts"
              :ruleset-definition="rulesetDefinition"
              :is-busy="isSaving"
              @send="sendSessionRollPrompts"
              @cancel="cancelRollPrompt"
            />

            <!-- NPC quick action — exploration only; combat drives this through the turn overlay -->
            <div v-if="game?.npcsAndMonsters.length" class="panel">
              <div class="panel-title">
                <div>
                  <h2>NPC Actions</h2>
                  <p class="text-sm">Queue an NPC or monster action for DM resolution.</p>
                </div>
                <button
                  v-if="!showNpcActionForm"
                  class="btn"
                  type="button"
                  @click="showNpcActionForm = true"
                >
                  Take NPC Action
                </button>
              </div>
              <form v-if="showNpcActionForm" @submit.prevent="submitNpcAction">
                <label>
                  NPC
                  <select v-model="selectedNpcId" required @change="resetNpcActionSelection">
                    <option value="">Choose NPC / Monster</option>
                    <option v-for="npc in game.npcsAndMonsters" :key="npc.id" :value="npc.id">{{ npc.name }}</option>
                  </select>
                </label>
                <RulesetActionFields
                  v-model:action-mode="npcActionMode"
                  v-model:selected-action-key="selectedNpcActionKey"
                  v-model:selected-stat-key="selectedNpcStatKey"
                  :available-actions="availableNpcActions"
                  :available-stat-checks="availableNpcStatChecks"
                  :selected-action-detail="selectedNpcActionDetail"
                  :selected-stat-detail="selectedNpcStatDetail"
                  :disabled="!selectedNpcId || isSaving"
                  show-modifiers
                />
                <p class="text-sm muted" style="margin: 0 0 0.75rem;">
                  {{ npcActionNeedsRoll
                    ? 'Roll before sending this NPC action. Dice are recorded in the queue for your review workflow.'
                    : 'Roll on the table (optional) — NPC actions are resolved by you; dice here are recorded in the queue, not sent to players.' }}
                </p>
                <RulesetDiceRoller
                  v-if="npcRollContext"
                  v-model="npcRollResult"
                  :context="npcRollContext"
                />
                <p v-if="npcActionMode === 'action' && npcAttackOutcome" class="roll-outcome-line">
                  Roll outcome:
                  <span class="badge" :class="npcAttackOutcome === 'Pass' ? 'pass' : 'fail'">{{ npcAttackOutcome }}</span>
                </p>

                  <DamageRollRoller
                    v-if="showNpcDamageRoll && npcDamageRollDefinition && rulesetDefinition"
                    v-model="npcDamageRollResult"
                    :damage-roll="npcDamageRollDefinition"
                    :definition="rulesetDefinition"
                    :attributes="npcStats.attributes"
                  />

                <ActionTargetPicker
                  v-if="game"
                  ref="npcActionTargetPickerRef"
                  :characters="game.characters"
                  :npcs="game.npcsAndMonsters"
                  :disabled="isSaving"
                />
                <div class="btn-row">
                  <button
                    class="btn"
                    type="submit"
                    :disabled="isSaving || (npcActionNeedsRoll && !npcHasPrimaryRoll) || (npcNeedsFollowUpRoll && !npcHasFollowUpRoll)"
                  >
                    {{ isSaving ? 'Sending…' : 'Send to Queue' }}
                  </button>
                  <button class="btn ghost" type="button" :disabled="isSaving" @click="showNpcActionForm = false">
                    Cancel
                  </button>
                </div>
              </form>
            </div>
          </div>

          <div class="dm-participant-section">
            <DmParticipantPanels
              v-if="state?.game"
              :game="state.game"
              :game-id="state.game.id"
              :ruleset-definition="rulesetDefinition"
              :is-busy="isSaving"
              @cycle-npc-visibility="cycleNpcVisibility"
              @npc-created="onNpcCreated"
              @npc-updated="onNpcUpdated"
            />
          </div>
        </div><!-- /dm-reference-column -->
      </div><!-- /dm-two-col-grid -->

      <div v-if="pollingError" class="alert error">{{ pollingError }}</div>
    </main>
  </section>

  <!-- Drag ghost: follows the cursor while dragging an initiative item -->
  <Teleport to="body">
    <div
      v-if="draggedEntry && dragPosition"
      class="initiative-drag-ghost"
      :style="{ transform: `translate(${dragPosition.x + 14}px, ${dragPosition.y - 24}px)` }"
    >
      <span class="initiative-card-body">
        <span class="initiative-name">{{ draggedEntry.combatantName }}</span>
        <span class="initiative-type">{{ draggedEntry.combatantType }}</span>
      </span>
    </div>
  </Teleport>

  <ConfirmModal
    v-model:open="showStopSessionConfirm"
    title="Stop session?"
    message="Players will be disconnected and this session will move to its summary state."
    confirm-label="Stop Session"
    :is-busy="isSaving"
    @confirm="stopSession"
  />

  <!-- Browser inactivity warning -->
  <Teleport to="body">
    <div v-if="idleWarningVisible && state?.isActive" class="idle-warning-overlay" role="alertdialog" aria-live="assertive">
      <div class="idle-warning-box">
        <p class="idle-warning-title">Session ending due to inactivity</p>
        <p class="idle-warning-body">
          No activity detected. The session will automatically end in
          <strong>{{ idleSecondsRemaining }}s</strong>.
        </p>
        <button class="btn primary" type="button" @click="onUserActivity">Stay Active</button>
      </div>
    </div>
  </Teleport>
</template>
