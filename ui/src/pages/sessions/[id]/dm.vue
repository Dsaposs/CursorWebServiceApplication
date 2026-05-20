<script setup lang="ts">
import ActionCard from '~/components/ActionCard.vue';
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
import type { ActionQueueItemResponse, GameResponse, InitiativeEntryResponse, RulesetResponse, SessionStateResponse } from '~/types/api';
import { evaluateActionOutcome } from '~/utils/actionOutcome';
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
  parseActorClassKey,
  parseRulesetDefinition,
  type DiceRollMode,
} from '~/utils/rulesets';
import { useRulesetTheme } from '~/composables/useRulesetTheme';
import { useThemePreference } from '~/composables/useThemePreference';

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
const npcRollResult = ref('');
const npcDamageRollResult = ref('');
const showNpcActionForm = ref(false);

// Action expand state
const expandedActions = ref<Set<string>>(new Set());
const expandedPendingActions = ref<Set<string>>(new Set());
const isSaving = ref(false);
const showStopSessionConfirm = ref(false);

async function loadState() {
  if (!token.value) return null;
  const nextState = await api<SessionStateResponse>(`/api/sessions/${route.params.id}/dm`);
  if (!ruleset.value) {
    ruleset.value = await api<RulesetResponse>(`/api/rulesets/${nextState.game.rulesetCode}`);
  }
  return nextState;
}

const { state, pollingError, fatalError, connectionStatus, refresh, start } = useSessionPolling(loadState, 3000);
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

const pendingActions = computed(() => state.value?.actions.filter(a => a.status === 'Pending') ?? []);
const publishedActions = computed(() => [...(state.value?.actions.filter(a => a.status === 'Published') ?? [])].reverse());
const game = computed<GameResponse | null>(() => state.value?.game ?? null);
const combatEncounters = computed(() => state.value?.combatEncounters ?? []);

const {
  expandedGroups: expandedLogGroups,
  toggleGroup: toggleLogGroup,
  expandAllGroups,
  collapseAllGroups,
} = useActionLogGroupExpansion(publishedActions, combatEncounters);

const rulesetDefinition = computed(() => parseRulesetDefinition(ruleset.value));
const currentTurn = computed<InitiativeEntryResponse | null>(() => state.value?.initiative.find(e => e.isCurrentTurn) ?? null);
const isCombat = computed(() => state.value?.state === 'Combat');
const sessionEnded = computed(() => state.value && !state.value.isActive);
const joinLink = computed(() => {
  if (!state.value) return '';
  return import.meta.client ? `${window.location.origin}${state.value.joinUrl}` : state.value.joinUrl;
});
const currentTurnNpc = computed(() => {
  if (!game.value || currentTurn.value?.combatantType !== 'NpcOrMonster') return null;
  return game.value.npcsAndMonsters.find(npc => npc.id === currentTurn.value?.combatantId) ?? null;
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

// Track whether the target picker has a valid target (gates dice rollers)
const npcHasTarget = ref(false);

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
  customActionText: npcAction,
  availableActions: availableNpcActions,
  availableStatChecks: availableNpcStatChecks,
  selectedActionDetail: selectedNpcActionDetail,
  selectedStatDetail: selectedNpcStatDetail,
  resetSelection: resetNpcActionSelection,
  buildSubmitPayload: buildNpcActionSubmitPayload,
} = useRulesetActionChooser(rulesetDefinition, selectedNpcClassKey, selectedNpcInventory, isNpcActionActorSelected);
const combatSetupEntries = computed(() => {
  if (!state.value) return [];

  return [
    ...state.value.game.characters.map(character => ({
      type: 'Character',
      id: character.id,
      name: character.name,
      detail: character.playerName || 'Character',
    })),
    ...state.value.game.npcsAndMonsters.map(npc => ({
      type: 'NpcOrMonster',
      id: npc.id,
      name: npc.name,
      detail: npc.kind,
    })),
  ];
});

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
});


async function setState(nextState: 'Exploration' | 'Combat') {
  isSaving.value = true;
  try {
    await api(`/api/sessions/${route.params.id}/state`, { method: 'POST', body: { state: nextState } });
    await refresh();
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
    await refresh();
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
  const payload = buildNpcActionSubmitPayload();
  if (!payload) { toastError('Choose or describe an NPC action first.'); return; }
  if (!npcActionTargetPickerRef.value?.isValid()) { toastError('Enter a target name for Other.'); return; }
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
    npcHasTarget.value = false;
    npcRollResult.value = '';
    npcDamageRollResult.value = '';
    resetNpcInlineFields();
    activeCombatEntryId.value = null;
    await refresh();
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
    promptLabel?: string;
    resultKind: string;
  }>;
}) {
  if (!state.value) return;
  isSaving.value = true;
  try {
    await api(`/api/sessions/${state.value.id}/roll-prompts`, { method: 'POST', body: payload });
    await refresh();
    toastSuccess('Skill check sent to player(s).');
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
    await refresh();
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
      resultKind: string;
      dc?: number | null;
    }>;
  },
) {
  isSaving.value = true;
  try {
    await api(`/api/actions/${actionId}/roll-prompts`, { method: 'POST', body: payload });
    await refresh();
    toastSuccess('Roll prompt sent to player(s).');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

async function dmRollForPlayer(payload: { actionId: string; rollSummary: string; dc?: number | null }) {
  isSaving.value = true;
  try {
    await api(`/api/actions/${payload.actionId}/roll-prompts/dm-roll`, {
      method: 'POST',
      body: { rollSummary: payload.rollSummary, dc: payload.dc ?? null },
    });
    await refresh();
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
    await refresh();
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
    await refresh();
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
    await api(`/api/actions/${action.id}/resolve`, {
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
    await refresh();
    toastSuccess('Resolution published to players.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
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
    await refresh();
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
    await refresh();
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
    await refresh();
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

async function stopSession() {
  isSaving.value = true;
  try {
    await api(`/api/sessions/${route.params.id}/stop`, { method: 'POST' });
    await refresh();
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
    await refresh();
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  }
}

async function onNpcCreated() {
  await refresh();
}

async function onNpcUpdated() {
  await refresh();
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
  expandedPendingActions.value = new Set(pendingActions.value.map(action => action.id));
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
    npcHasTarget.value = false;
    npcRollResult.value = '';
    npcDamageRollResult.value = '';
    resetNpcInlineFields();
  }
}

watch(currentNpcPendingAction, (action) => {
  if (action) applyPendingChainEffects(action);
});

function rulesetActionDetail(action: ActionQueueItemResponse) {
  const rulesetAction = findRulesetAction(rulesetDefinition.value, action.actionKey);
  return rulesetAction && rulesetDefinition.value
    ? describeRulesetAction(rulesetAction, rulesetDefinition.value)
    : null;
}

/** Resolve actor stats for a pending action (character or NPC). */
function actorStats(action: ActionQueueItemResponse) {
  if (action.actorCharacterId) {
    const char = state.value?.game.characters.find(c => c.id === action.actorCharacterId);
    return char
      ? parseCharacterStats(char.rulesetDataJson)
      : null;
  }
  if (action.actorNpcId) {
    const npc = state.value?.game.npcsAndMonsters.find(n => n.id === action.actorNpcId);
    if (npc) {
      const stats = parseStatMap(npc.statBlockJson);
      return { attributes: stats, skills: stats, gameValues: {} as Record<string, number> };
    }
  }
  return null;
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
const npcAttackOutcome = computed(() => {
  if (!npcRollResult.value || npcActionMode.value !== 'action') return null;
  return evaluateActionOutcome(
    rulesetDefinition.value,
    selectedNpcActionKey.value,
    `🎲 Roll: ${npcRollResult.value}`,
  );
});
const showNpcDamageRoll = computed(() =>
  Boolean(effectiveNpcActionRoll.value?.damageRoll && npcAttackOutcome.value === 'Pass'),
);

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

      <div class="session-dashboard-grid">
        <div class="session-primary-column">
          <div class="session-support-column">
          <DmCombatWorkflow
            :is-combat="isCombat"
            :is-saving="isSaving"
            :current-turn="currentTurn"
            :displayed-initiative="displayedInitiative"
            :dragged-initiative-id="draggedInitiativeId"
            :drag-over-id="dragOverId"
            :expanded-entry-id="activeCombatEntryId"
            @setup-combat="setupCombat"
            @advance-turn="advanceTurn"
            @end-combat="endCombat"
            @start-drag="startInitiativeDrag"
            @move-keyboard="moveInitiativeByKeyboard"
            @activate-entry="handleActivateEntry"
          >
            <template v-if="state" #entry-action="{ entry }">
              <!-- Player card — waiting for them to act -->
              <div v-if="entry.combatantType === 'Character'" class="turn-inline-body">
                <p class="text-sm muted">
                  Waiting for <strong>{{ entry.combatantName }}</strong> to act.
                  Their turn prompt is open on their screen.
                </p>
                <button class="btn ghost sm" type="button" :disabled="isSaving" @click="advanceTurn">
                  Skip turn →
                </button>
              </div>

              <!-- NPC card — single combined action + resolution form -->
              <form v-else class="turn-inline-body" @submit.prevent="submitAndResolveNpcAction">
                <label>
                  Action type
                  <select v-model="npcActionMode" :disabled="!selectedNpcId">
                    <option v-if="availableNpcActions.length" value="action">Action</option>
                    <option value="stat-check">Stat check</option>
                  </select>
                </label>
                <label v-if="npcActionMode === 'action'">
                  Action
                  <select v-model="selectedNpcActionKey" required>
                    <option value="">Choose an action</option>
                    <option v-for="action in availableNpcActions" :key="action.key" :value="action.key">
                      {{ action.label }}
                    </option>
                  </select>
                </label>
                <label v-else>
                  Stat
                  <select v-model="selectedNpcStatKey" required>
                    <option value="">Choose a stat</option>
                    <optgroup label="Skills">
                      <option v-for="stat in availableNpcStatChecks.filter(s => s.type === 'skill')" :key="stat.key" :value="stat.key">
                        {{ stat.label }}
                      </option>
                    </optgroup>
                    <optgroup label="Attributes">
                      <option v-for="stat in availableNpcStatChecks.filter(s => s.type === 'attribute')" :key="stat.key" :value="stat.key">
                        {{ stat.label }}
                      </option>
                    </optgroup>
                  </select>
                </label>
                <div v-if="npcActionMode === 'action' && selectedNpcActionDetail" class="alert info">
                  <strong>{{ selectedNpcActionDetail.dice }}</strong>
                  <p class="text-sm muted">{{ selectedNpcActionDetail.attribute }} + {{ selectedNpcActionDetail.skill }} · {{ selectedNpcActionDetail.successRule }}</p>
                </div>
                <div v-else-if="npcActionMode === 'stat-check' && selectedNpcStatDetail" class="alert info">
                  <p class="text-sm muted">{{ selectedNpcStatDetail.rollSummary }}</p>
                </div>

                <!-- Target must be set before dice rollers appear -->
                <ActionTargetPicker
                  ref="npcActionTargetPickerRef"
                  :characters="state.game.characters"
                  :npcs="state.game.npcsAndMonsters"
                  :disabled="isSaving"
                  @change="npcHasTarget = $event"
                />

                <template v-if="npcHasTarget">
                  <p class="text-sm muted" style="margin: 0.25rem 0 0.25rem;">Roll on the table (optional).</p>
                  <RulesetDiceRoller v-if="npcRollContext" v-model="npcRollResult" :context="npcRollContext" />
                  <DamageRollRoller
                    v-if="showNpcDamageRoll && effectiveNpcActionRoll?.damageRoll && rulesetDefinition"
                    v-model="npcDamageRollResult"
                    :damage-roll="effectiveNpcActionRoll.damageRoll"
                    :definition="rulesetDefinition"
                    :attributes="npcStats.attributes"
                  />
                </template>
                <p v-else class="text-sm muted" style="margin: 0.25rem 0 0;">Select a target above to unlock dice rolls.</p>

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
                <button class="btn success" type="submit" :disabled="isSaving" style="margin-top: 0.75rem;">
                  {{ isSaving ? 'Resolving…' : 'Resolve NPC action' }}
                </button>
              </form>
            </template>
          </DmCombatWorkflow>

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
          <div v-if="game?.npcsAndMonsters.length && !isCombat" class="panel">
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
              <label>
                Action type
                <select v-model="npcActionMode" :disabled="!selectedNpcId">
                  <option v-if="availableNpcActions.length" value="action">Action</option>
                  <option value="stat-check">Stat check</option>
                </select>
              </label>
              <label v-if="npcActionMode === 'action'">
                Action
                <select v-model="selectedNpcActionKey" required>
                  <option value="">Choose an action</option>
                  <option v-for="action in availableNpcActions" :key="action.key" :value="action.key">
                    {{ action.label }}
                  </option>
                </select>
              </label>
              <label v-else>
                Stat
                <select v-model="selectedNpcStatKey" required>
                  <option value="">Choose a stat</option>
                  <optgroup label="Skills">
                    <option v-for="stat in availableNpcStatChecks.filter(s => s.type === 'skill')" :key="stat.key" :value="stat.key">
                      {{ stat.label }}
                    </option>
                  </optgroup>
                  <optgroup label="Attributes">
                    <option v-for="stat in availableNpcStatChecks.filter(s => s.type === 'attribute')" :key="stat.key" :value="stat.key">
                      {{ stat.label }}
                    </option>
                  </optgroup>
                </select>
              </label>
              <div v-if="npcActionMode === 'action' && selectedNpcActionDetail" class="alert info">
                <div>
                  <strong>{{ selectedNpcActionDetail.dice }}</strong>
                  <p class="text-sm">
                    Roll {{ selectedNpcActionDetail.attribute }} + {{ selectedNpcActionDetail.skill }}.
                    Modifiers: {{ selectedNpcActionDetail.modifiers }}.
                  </p>
                  <p class="text-sm">{{ selectedNpcActionDetail.successRule }}</p>
                </div>
              </div>
              <div v-else-if="npcActionMode === 'stat-check' && selectedNpcStatDetail" class="alert info">
                <div>
                  <strong>{{ selectedNpcStatDetail.actionText }}</strong>
                  <p class="text-sm">Suggested roll: {{ selectedNpcStatDetail.rollSummary }}.</p>
                </div>
              </div>
              <p class="text-sm muted" style="margin: 0 0 0.75rem;">
                Roll on the table (optional) — NPC actions are resolved by you; dice here are recorded in the queue, not sent to players.
              </p>
              <RulesetDiceRoller
                v-if="npcRollContext"
                v-model="npcRollResult"
                :context="npcRollContext"
              />

              <DamageRollRoller
                v-if="showNpcDamageRoll && effectiveNpcActionRoll?.damageRoll && rulesetDefinition"
                v-model="npcDamageRollResult"
                :damage-roll="effectiveNpcActionRoll.damageRoll"
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
                <button class="btn" type="submit" :disabled="isSaving">
                  {{ isSaving ? 'Sending…' : 'Send to Queue' }}
                </button>
                <button class="btn ghost" type="button" :disabled="isSaving" @click="showNpcActionForm = false">
                  Cancel
                </button>
              </div>
            </form>
          </div>

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
        </div>

        <aside class="session-feed-column">
          <div class="panel dashboard-primary-panel pending-actions-panel">
            <div class="panel-title">
              <div>
                <h2>
                  Pending Actions
                  <span v-if="pendingActions.length" class="badge pending" style="margin-left: 0.4rem;">{{ pendingActions.length }}</span>
                </h2>
                <p class="text-sm">Review submissions, request player rolls, then publish outcomes. Rolls happen after you prompt — not on submit.</p>
              </div>
              <div v-if="pendingActions.length" class="btn-row">
                <button class="btn ghost sm" type="button" @click="expandAllPendingActions">Expand</button>
                <button class="btn ghost sm" type="button" @click="collapseAllPendingActions">Collapse</button>
              </div>
            </div>

            <div v-if="pendingActions.length === 0" class="empty-state" style="padding: 1.5rem 0;">
              <p class="text-sm">No actions waiting. Players can submit actions via their session link.</p>
            </div>

            <div class="pending-actions-list">
              <div v-for="action in pendingActions" :key="action.id" class="action-card pending-card">
                <button
                  class="action-card-header action-card-toggle"
                  type="button"
                  :aria-expanded="expandedPendingActions.has(action.id)"
                  @click="togglePendingAction(action.id)"
                >
                  <div>
                    <div class="action-card-actor">
                      {{ action.actorName }}
                      <span v-if="action.isSkillCheckResponse" class="badge" style="margin-left: 0.35rem;">Skill check</span>
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

                <form v-if="expandedPendingActions.has(action.id)" @submit.prevent="resolveAction(action)">
                  <div v-if="rulesetActionDetail(action)" class="alert info">
                    <div>
                      <strong>{{ rulesetActionDetail(action)?.dice }}</strong>
                      <p class="text-sm muted">
                        Reference — prompt the player to roll: {{ rulesetActionDetail(action)?.attribute }} + {{ rulesetActionDetail(action)?.skill }}.
                      </p>
                      <p class="text-sm">{{ rulesetActionDetail(action)?.successRule }}</p>
                    </div>
                  </div>

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

                  <p
                    v-if="derivedActionOutcome(action)"
                    class="text-sm"
                    style="margin: 0.5rem 0;"
                  >
                    Roll outcome:
                    <span class="badge" :class="derivedActionOutcome(action) === 'Pass' ? 'pass' : 'fail'">
                      {{ derivedActionOutcome(action) }}
                    </span>
                  </p>

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

                  <div class="btn-row">
                    <button class="btn success" type="submit" :disabled="isSaving">
                      <span aria-hidden="true">✓</span> Publish Resolution
                    </button>
                    <button
                      class="btn danger ghost"
                      type="button"
                      :disabled="isSaving"
                      @click="rejectAction(action)"
                    >
                      Reject Action
                    </button>
                  </div>
                  <label>
                    Rejection note (optional, used if you reject instead)
                    <textarea v-model="rejectReason[action.id]" placeholder="Why this action does not succeed…" style="min-height: 2rem;" />
                  </label>
                </form>
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
        </aside>
      </div>

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
</template>
