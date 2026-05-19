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
import { parseCharacterStats, parseStatMap } from '~/utils/dice';
import { parseNpcInventory } from '~/utils/inventory';
import { resolveEffectiveActionRoll } from '~/utils/items';
import {
  buildDiceRollContext,
  describeRulesetAction,
  findRulesetAction,
  parseActorClassKey,
  parseRulesetDefinition,
} from '~/utils/rulesets';

const route = useRoute();
const { api, token, loadSession, clearSession } = useApi();
const { success: toastSuccess, error: toastError, info: toastInfo } = useToast();
const ruleset = ref<RulesetResponse | null>(null);

// Per-action resolve form state
const resolutionText = ref<Record<string, string>>({});
const additionalActions = ref<Record<string, string>>({});
const statChangeTarget = ref<Record<string, string>>({});
const statChangeHealthDelta = ref<Record<string, string>>({});
const statChangeSetHealth = ref<Record<string, string>>({});
const statChangeSetArmor = ref<Record<string, string>>({});
// Game-value absolute overrides (actor only, kept for backward compat but UI removed)
const gameValueChanges = ref<Record<string, Record<string, string>>>({});
// Stat-change section: per-action delta maps for game values and attributes
const statChangeGvDeltas = ref<Record<string, Record<string, string>>>({});
const statChangeAttrDeltas = ref<Record<string, Record<string, string>>>({});
const statChangeInventoryDeltas = ref<Record<string, Record<string, string>>>({});
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
const selectedNpc = computed(() => game.value?.npcsAndMonsters.find(npc => npc.id === selectedNpcId.value) ?? null);
const selectedNpcClassKey = computed(() => parseActorClassKey(selectedNpc.value?.statBlockJson));
const selectedNpcInventory = computed(() => parseNpcInventory(selectedNpc.value?.statBlockJson));
const isNpcActionActorSelected = computed(() => Boolean(selectedNpcId.value));
const {
  actionMode: npcActionMode,
  selectedActionKey: selectedNpcActionKey,
  selectedSkillKey: selectedNpcSkillKey,
  selectedAttributeKey: selectedNpcAttributeKey,
  customActionText: npcAction,
  availableActions: availableNpcActions,
  availableSkills: availableNpcSkills,
  availableAttributes: availableNpcAttributes,
  selectedActionDetail: selectedNpcActionDetail,
  selectedSkillDetail: selectedNpcSkillDetail,
  selectedAttributeDetail: selectedNpcAttributeDetail,
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
    showNpcActionForm.value = false;
    await refresh();
    toastSuccess('NPC action submitted.');
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
  return evaluateActionOutcome(rulesetDefinition.value, action.actionKey, action.description);
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
    for (const map of [resolutionText, additionalActions, rejectReason, statChangeTarget, statChangeHealthDelta, statChangeSetHealth, statChangeSetArmor, gameValueChanges, statChangeGvDeltas, statChangeAttrDeltas, statChangeInventoryDeltas]) {
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
  if (!String(resolutionText.value[action.id] ?? '').trim()) {
    toastError('Resolution text is required.');
    return;
  }
  isSaving.value = true;
  try {
    await api(`/api/actions/${action.id}/resolve`, {
      method: 'PUT',
      body: {
        resolutionText: resolutionText.value[action.id],
        additionalActions: additionalActions.value[action.id] || undefined,
        statChanges: buildStatChanges(action),
      },
    });
    for (const map of [resolutionText, additionalActions, rejectReason, statChangeTarget, statChangeHealthDelta, statChangeSetHealth, statChangeSetArmor, gameValueChanges, statChangeGvDeltas, statChangeAttrDeltas, statChangeInventoryDeltas]) {
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
  };
  const changes: StatChange[] = [];

  // Health / armor + stat deltas for the selected target
  const target = statChangeTarget.value[actionId];
  if (target) {
    const [targetType, targetId] = target.split(':');
    const healthDelta = optNum(statChangeHealthDelta.value[actionId]);
    const setHealth = optNum(statChangeSetHealth.value[actionId]);
    const setArmor = optNum(statChangeSetArmor.value[actionId]);

    // Game value deltas (Character targets only)
    let gameValueDeltas: Record<string, number> | undefined;
    let attributeDeltas: Record<string, number> | undefined;
    let inventoryDeltas: Record<string, number> | undefined;
    if (targetType === 'Character') {
      gameValueDeltas = toNonZeroIntMap(statChangeGvDeltas.value[actionId]);
      attributeDeltas = toNonZeroIntMap(statChangeAttrDeltas.value[actionId]);
      inventoryDeltas = toNonZeroIntMap(statChangeInventoryDeltas.value[actionId]);
    }

    const hasAny = healthDelta !== undefined || setHealth !== undefined || setArmor !== undefined
      || (gameValueDeltas && Object.keys(gameValueDeltas).length > 0)
      || (attributeDeltas && Object.keys(attributeDeltas).length > 0)
      || (inventoryDeltas && Object.keys(inventoryDeltas).length > 0);

    if (hasAny) {
      changes.push({ targetType, targetId, healthDelta, setHealth, setArmor, gameValueDeltas, attributeDeltas, inventoryDeltas });
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
  const sourceEntries = displayedInitiative.value.length
    ? displayedInitiative.value.map(entry => ({ type: entry.combatantType, id: entry.combatantId }))
    : combatSetupEntries.value.map(entry => ({ type: entry.type, id: entry.id }));
  const combatants = sourceEntries.map((entry, index) => ({
    ...entry,
    initiative: sourceEntries.length - index,
  }));

  try {
    await api(`/api/sessions/${route.params.id}/combat`, { method: 'POST', body: { combatants } });
    await refresh();
    toastSuccess('Combat started.');
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

function takeCurrentNpcAction(npcId: string) {
  selectedNpcId.value = npcId;
  resetNpcActionSelection();
  npcTarget.value = '';
  showNpcActionForm.value = true;
}

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

  return buildDiceRollContext({
    definition: def,
    mode: npcActionMode.value,
    actionKey: selectedNpcActionKey.value,
    skillKey: selectedNpcSkillKey.value,
    attributeKey: selectedNpcAttributeKey.value,
    attributes: npcStats.value.attributes,
    skills: npcStats.value.skills,
    gameValues: npcStats.value.gameValues,
  });
});
</script>

<template>
  <section class="app-shell dm-app-shell">
    <!-- Topbar -->
    <header class="topbar">
      <div class="topbar-brand">
        <span aria-hidden="true">🎲</span>
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

    <main v-else class="stack dm-screen-main">
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
            :current-turn-npc="currentTurnNpc"
            @setup-combat="setupCombat"
            @advance-turn="advanceTurn"
            @end-combat="endCombat"
            @start-drag="startInitiativeDrag"
            @move-keyboard="moveInitiativeByKeyboard"
            @take-current-npc-action="takeCurrentNpcAction"
          />

          <DmPlayerSkillCheckPanel
            v-if="game?.characters.length"
            :characters="game.characters"
            :roll-prompts="sessionRollPrompts"
            :ruleset-definition="rulesetDefinition"
            :is-busy="isSaving"
            @send="sendSessionRollPrompts"
            @cancel="cancelRollPrompt"
          />

          <!-- NPC quick action -->
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
              <label>
                Action type
                <select v-model="npcActionMode" :disabled="!selectedNpcId">
                  <option v-if="availableNpcActions.length" value="action">Predefined action</option>
                  <option v-if="availableNpcSkills.length" value="skill">Skill check</option>
                  <option v-if="availableNpcAttributes.length" value="attribute">Attribute check</option>
                  <option value="custom">Custom action</option>
                </select>
              </label>
              <label v-if="npcActionMode === 'action' && availableNpcActions.length">
                Predefined action
                <select v-model="selectedNpcActionKey" required>
                  <option value="">Choose a predefined action</option>
                  <option v-for="action in availableNpcActions" :key="action.key" :value="action.key">
                    {{ action.label }}
                  </option>
                </select>
              </label>
              <label v-else-if="npcActionMode === 'skill' && availableNpcSkills.length">
                Skill
                <select v-model="selectedNpcSkillKey" required>
                  <option value="">Choose a skill</option>
                  <option v-for="skill in availableNpcSkills" :key="skill.key" :value="skill.key">
                    {{ skill.label }}
                  </option>
                </select>
              </label>
              <label v-else-if="npcActionMode === 'attribute' && availableNpcAttributes.length">
                Attribute
                <select v-model="selectedNpcAttributeKey" required>
                  <option value="">Choose an attribute</option>
                  <option v-for="attribute in availableNpcAttributes" :key="attribute.key" :value="attribute.key">
                    {{ attribute.label }}
                  </option>
                </select>
              </label>
              <label v-else>
                Custom action
                <input
                  v-model.trim="npcAction"
                  :disabled="!selectedNpcId"
                  :placeholder="selectedNpcId ? 'Describe a custom action' : 'Choose an NPC first'"
                  required
                />
              </label>
              <div v-if="selectedNpcActionDetail" class="alert info">
                <div>
                  <strong>{{ selectedNpcActionDetail.dice }}</strong>
                  <p class="text-sm">
                    Roll {{ selectedNpcActionDetail.attribute }} + {{ selectedNpcActionDetail.skill }}.
                    Modifiers: {{ selectedNpcActionDetail.modifiers }}.
                  </p>
                  <p class="text-sm">{{ selectedNpcActionDetail.successRule }}</p>
                </div>
              </div>
              <div v-else-if="selectedNpcSkillDetail || selectedNpcAttributeDetail" class="alert info">
                <div>
                  <strong>{{ selectedNpcSkillDetail?.actionText ?? selectedNpcAttributeDetail?.actionText }}</strong>
                  <p class="text-sm">
                    Suggested roll: {{ selectedNpcSkillDetail?.rollSummary ?? selectedNpcAttributeDetail?.rollSummary }}.
                  </p>
                </div>
              </div>
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
                <p class="text-sm">Review player actions, skill check responses, and NPC actions. Resolve each entry individually.</p>
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
                      v-if="derivedActionOutcome(action)"
                      class="badge"
                      :class="derivedActionOutcome(action) === 'Pass' ? 'pass' : 'fail'"
                      style="margin-top: 0.35rem;"
                    >
                      Roll: {{ derivedActionOutcome(action) }}
                    </span>
                  </div>
                  <span class="badge pending">{{ expandedPendingActions.has(action.id) ? 'Hide' : 'Resolve' }}</span>
                </button>

                <form v-if="expandedPendingActions.has(action.id)" @submit.prevent="resolveAction(action)">
                  <div v-if="rulesetActionDetail(action)" class="alert info">
                    <div>
                      <strong>{{ rulesetActionDetail(action)?.dice }}</strong>
                      <p class="text-sm">
                        Roll {{ rulesetActionDetail(action)?.attribute }} + {{ rulesetActionDetail(action)?.skill }}.
                        Modifiers: {{ rulesetActionDetail(action)?.modifiers }}.
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
                    @send="payload => sendRollPrompts(action.id, payload)"
                    @cancel="cancelRollPrompt"
                  />

                  <p v-if="derivedActionOutcome(action)" class="text-sm" style="margin: 0.5rem 0;">
                    Initial roll result:
                    <span class="badge" :class="derivedActionOutcome(action) === 'Pass' ? 'pass' : 'fail'">
                      {{ derivedActionOutcome(action) }}
                    </span>
                    <span style="color: var(--muted-light);"> (from player roll, set on publish)</span>
                  </p>

                  <label>
                    Resolution <span style="color: var(--danger);">*</span>
                    <textarea v-model="resolutionText[action.id]" placeholder="Describe what happens…" required style="min-height: 3.5rem;" />
                  </label>
                  <label>
                    Additional actions / counter-actions
                    <textarea v-model="additionalActions[action.id]" placeholder="The orc counter-attacks…" style="min-height: 2.5rem;" />
                  </label>

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
                    @update:target="statChangeTarget[action.id] = $event"
                    @update:health-delta="statChangeHealthDelta[action.id] = $event"
                    @update:set-health="statChangeSetHealth[action.id] = $event"
                    @update:set-armor="statChangeSetArmor[action.id] = $event"
                    @update:gv-deltas="statChangeGvDeltas[action.id] = $event"
                    @update:attr-deltas="statChangeAttrDeltas[action.id] = $event"
                    @update:inventory-deltas="statChangeInventoryDeltas[action.id] = $event"
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
