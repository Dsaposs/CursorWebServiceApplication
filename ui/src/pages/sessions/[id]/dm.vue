<script setup lang="ts">
import ActionCard from '~/components/ActionCard.vue';
import ConfirmModal from '~/components/ConfirmModal.vue';
import DmActionLog from '~/components/dm/DmActionLog.vue';
import DmCombatWorkflow from '~/components/dm/DmCombatWorkflow.vue';
import DmParticipantPanels from '~/components/dm/DmParticipantPanels.vue';
import DmSessionInvite from '~/components/dm/DmSessionInvite.vue';
import SessionConnectionStatus from '~/components/SessionConnectionStatus.vue';
import SkeletonBlock from '~/components/SkeletonBlock.vue';
import { useRulesetActionChooser } from '~/composables/useRulesetActionChooser';
import type { ActionQueueItemResponse, GameResponse, InitiativeEntryResponse, RulesetResponse, SessionStateResponse } from '~/types/api';
import {
  buildRollSummary,
  describeRulesetAction,
  findRulesetAction,
  parseActorClassKey,
  parseRulesetDefinition,
} from '~/utils/rulesets';

const route = useRoute();
const { api, token, loadSession } = useApi();
const { success: toastSuccess, error: toastError, info: toastInfo } = useToast();
const ruleset = ref<RulesetResponse | null>(null);

// Per-action resolve form state
const resolutionText = ref<Record<string, string>>({});
const rollSummary = ref<Record<string, string>>({});
const additionalActions = ref<Record<string, string>>({});
const statChangeTarget = ref<Record<string, string>>({});
const statChangeHealthDelta = ref<Record<string, string>>({});
const statChangeSetHealth = ref<Record<string, string>>({});
const statChangeSetArmor = ref<Record<string, string>>({});

// NPC action form
const selectedNpcId = ref('');
const npcTarget = ref('');
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

const { state, pollingError, connectionStatus, refresh, start } = useSessionPolling(loadState, 3000);

const pendingActions = computed(() => state.value?.actions.filter(a => a.status === 'Pending') ?? []);
const publishedActions = computed(() => [...(state.value?.actions.filter(a => a.status === 'Published') ?? [])].reverse());
const game = computed<GameResponse | null>(() => state.value?.game ?? null);
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
} = useRulesetActionChooser(rulesetDefinition, selectedNpcClassKey, isNpcActionActorSelected);
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

  isSaving.value = true;
  try {
    await api(`/api/sessions/${state.value.joinCode}/actions`, {
      method: 'POST',
      body: {
        actorNpcId: selectedNpcId.value,
        actionKey: payload.actionKey,
        actionText: payload.actionText,
        targetName: npcTarget.value || undefined,
        description: payload.description,
      },
    });
    resetNpcActionSelection();
    npcTarget.value = '';
    showNpcActionForm.value = false;
    await refresh();
    toastSuccess('NPC action submitted.');
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
        rollSummary: rollSummary.value[action.id] || undefined,
        additionalActions: additionalActions.value[action.id] || undefined,
        statChanges: buildStatChanges(action.id),
      },
    });
    // Clear form fields for this action
    for (const map of [resolutionText, rollSummary, additionalActions, statChangeTarget, statChangeHealthDelta, statChangeSetHealth, statChangeSetArmor]) {
      delete map.value[action.id];
    }
    await refresh();
    toastSuccess('Resolution published to players.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

function buildStatChanges(actionId: string) {
  const target = statChangeTarget.value[actionId];
  if (!target) return [];
  const [targetType, targetId] = target.split(':');
  const change = {
    targetType, targetId,
    healthDelta: optNum(statChangeHealthDelta.value[actionId]),
    setHealth: optNum(statChangeSetHealth.value[actionId]),
    setArmor: optNum(statChangeSetArmor.value[actionId]),
  };
  if (change.healthDelta === undefined && change.setHealth === undefined && change.setArmor === undefined) return [];
  return [change];
}

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
  const cycle: Record<string, string> = { Visible: 'Obscured', Obscured: 'Hidden', Hidden: 'Visible' };
  const next = cycle[current] ?? 'Obscured';
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
  if (expandedPendingActions.value.has(id)) expandedPendingActions.value.delete(id);
  else expandedPendingActions.value.add(id);
}

function expandAllPendingActions() {
  expandedPendingActions.value = new Set(pendingActions.value.map(action => action.id));
}

function collapseAllPendingActions() {
  expandedPendingActions.value = new Set();
}

function expandAllActions() {
  expandedActions.value = new Set(publishedActions.value.map(action => action.id));
}

function collapseAllActions() {
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

function applyRollSuggestion(action: ActionQueueItemResponse) {
  const rulesetAction = findRulesetAction(rulesetDefinition.value, action.actionKey);
  if (!rulesetAction || !rulesetDefinition.value) return;
  rollSummary.value[action.id] = buildRollSummary(rulesetAction, rulesetDefinition.value);
}
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
      <div class="topbar-actions" v-if="state">
        <SessionConnectionStatus
          :status="connectionStatus"
          :error="pollingError"
          :started-at="state.startedAt"
          :ended-at="state.endedAt"
          :is-active="state.isActive"
        />
        <span class="badge" :class="isCombat ? 'combat' : 'exploration'">{{ state.state }}</span>
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
        :is-combat="isCombat"
        @copy="copyJoinLink"
      />

      <div class="session-dashboard-grid">
        <!-- Left: Action queue -->
        <div class="session-action-column">
          <!-- Pending actions -->
          <div class="panel dashboard-primary-panel pending-actions-panel mb-2" style="margin-bottom: 1rem;">
            <div class="panel-title">
              <div>
                <h2>
                  Pending Actions
                  <span v-if="pendingActions.length" class="badge pending" style="margin-left: 0.4rem;">{{ pendingActions.length }}</span>
                </h2>
                <p class="text-sm">Review player and NPC actions here first.</p>
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
                    <div class="action-card-actor">{{ action.actorName }}</div>
                    <div class="action-card-target">
                      uses <strong>{{ action.actionText }}</strong>
                      <span v-if="action.targetName"> on {{ action.targetName }}</span>
                    </div>
                    <div v-if="action.description" class="action-card-desc">{{ action.description }}</div>
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
                      <button class="btn ghost sm mt-1" type="button" @click="applyRollSuggestion(action)">
                        Use Roll Summary
                      </button>
                    </div>
                  </div>
                  <label>
                    Roll result
                    <input v-model="rollSummary[action.id]" placeholder="e.g. rolled a 19 on Ranged Combat" />
                  </label>
                  <label>
                    Resolution <span style="color: var(--danger);">*</span>
                    <textarea v-model="resolutionText[action.id]" placeholder="Describe what happens…" required style="min-height: 3.5rem;" />
                  </label>
                  <label>
                    Additional actions / counter-actions
                    <textarea v-model="additionalActions[action.id]" placeholder="The orc counter-attacks…" style="min-height: 2.5rem;" />
                  </label>

                  <details>
                    <summary style="cursor: pointer; font-size: 0.8rem; color: var(--muted-light); padding: 0.25rem 0;">Apply stat change (optional)</summary>
                    <div style="margin-top: 0.6rem; display: grid; gap: 0.5rem;">
                      <label>
                        Target
                        <select v-model="statChangeTarget[action.id]">
                          <option value="">No stat change</option>
                          <optgroup label="Characters">
                            <option v-for="ch in state.game.characters" :key="ch.id" :value="`Character:${ch.id}`">
                              {{ ch.name }}
                            </option>
                          </optgroup>
                          <optgroup label="NPCs / Monsters">
                            <option v-for="npc in state.game.npcsAndMonsters" :key="npc.id" :value="`NpcOrMonster:${npc.id}`">
                              {{ npc.name }}
                            </option>
                          </optgroup>
                        </select>
                      </label>
                      <div class="inline-fields">
                        <label>HP delta<input v-model="statChangeHealthDelta[action.id]" type="number" placeholder="-5 or +3" /></label>
                        <label>Set HP<input v-model="statChangeSetHealth[action.id]" type="number" min="0" /></label>
                        <label>Set AC<input v-model="statChangeSetArmor[action.id]" type="number" min="0" /></label>
                      </div>
                    </div>
                  </details>

                  <button class="btn success" type="submit" :disabled="isSaving">
                    <span aria-hidden="true">✓</span> Publish Resolution
                  </button>
                </form>
              </div>
            </div>
          </div>

          <DmActionLog
            :actions="publishedActions"
            :expanded-actions="expandedActions"
            @toggle="toggleAction"
            @expand-all="expandAllActions"
            @collapse-all="collapseAllActions"
          />
        </div>

        <!-- Right: Combat + NPC -->
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
              <label>Target<input v-model.trim="npcTarget" placeholder="Optional" /></label>
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
            :game="state.game"
            @cycle-npc-visibility="cycleNpcVisibility"
          />
        </div>
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
