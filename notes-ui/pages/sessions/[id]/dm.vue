<script setup lang="ts">
import type { ActionQueueItemResponse, GameResponse, InitiativeEntryResponse, SessionStateResponse } from '~/types/api';

const route = useRoute();
const { api, token, loadSession } = useApi();
const { success: toastSuccess, error: toastError, info: toastInfo } = useToast();

// Per-action resolve form state
const resolutionText = ref<Record<string, string>>({});
const rollSummary = ref<Record<string, string>>({});
const additionalActions = ref<Record<string, string>>({});
const statChangeTarget = ref<Record<string, string>>({});
const statChangeHealthDelta = ref<Record<string, string>>({});
const statChangeSetHealth = ref<Record<string, string>>({});
const statChangeSetArmor = ref<Record<string, string>>({});

// NPC action form
const npcAction = ref('');
const selectedNpcId = ref('');
const npcTarget = ref('');
const showNpcActionForm = ref(false);

// Combat setup
const localInitiativeOrder = ref<InitiativeEntryResponse[] | null>(null);
const draggedInitiativeId = ref<string | null>(null);
const dragOverId = ref<string | null>(null);
const dragPosition = ref<{ x: number; y: number } | null>(null);
const activePointerId = ref<number | null>(null);

const draggedEntry = computed(() =>
  displayedInitiative.value.find(e => e.id === draggedInitiativeId.value) ?? null
);

// Resolved action expand state
const expandedActions = ref<Set<string>>(new Set());
const isSaving = ref(false);

async function loadState() {
  if (!token.value) return null;
  return await api<SessionStateResponse>(`/api/sessions/${route.params.id}/dm`);
}

const { state, pollingError, refresh, start } = useSessionPolling(loadState, 3000);

const pendingActions = computed(() => state.value?.actions.filter(a => a.status === 'Pending') ?? []);
const publishedActions = computed(() => [...(state.value?.actions.filter(a => a.status === 'Published') ?? [])].reverse());
const game = computed<GameResponse | null>(() => state.value?.game ?? null);
const currentTurn = computed<InitiativeEntryResponse | null>(() => state.value?.initiative.find(e => e.isCurrentTurn) ?? null);
const isCombat = computed(() => state.value?.state === 'Combat');
const sessionEnded = computed(() => state.value && !state.value.isActive);
const displayedInitiative = computed(() => localInitiativeOrder.value ?? state.value?.initiative ?? []);
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
  isSaving.value = true;
  try {
    await api(`/api/sessions/${state.value.joinCode}/actions`, {
      method: 'POST',
      body: { actorNpcId: selectedNpcId.value, actionText: npcAction.value, targetName: npcTarget.value || undefined },
    });
    npcAction.value = '';
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

async function reorderInitiative(fromId: string, toId: string) {
  if (!state.value || fromId === toId) return;

  const entries = [...displayedInitiative.value].sort((a, b) => a.sortOrder - b.sortOrder);
  const fromIndex = entries.findIndex(entry => entry.id === fromId);
  const toIndex = entries.findIndex(entry => entry.id === toId);

  if (fromIndex < 0 || toIndex < 0) return;

  const [moved] = entries.splice(fromIndex, 1);
  entries.splice(toIndex, 0, moved);
  const reordered = entries.map((entry, index) => ({ ...entry, sortOrder: index + 1 }));
  localInitiativeOrder.value = reordered;
  await saveInitiativeOrder(reordered);
}

async function saveInitiativeOrder(entries: InitiativeEntryResponse[]) {
  if (!state.value) return;

  isSaving.value = true;
  try {
    const combatants = entries.map((entry, index) => ({
      type: entry.combatantType,
      id: entry.combatantId,
      initiative: entries.length - index,
    }));

    await api(`/api/sessions/${route.params.id}/combat`, { method: 'POST', body: { combatants } });
    await refresh();
    localInitiativeOrder.value = null;
    toastSuccess('Initiative order updated.');
  } catch (err) {
    localInitiativeOrder.value = null;
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

function startInitiativeDrag(entryId: string, event: PointerEvent) {
  if (!isCombat.value || isSaving.value || (event.pointerType === 'mouse' && event.button !== 0)) return;
  event.preventDefault();
  draggedInitiativeId.value = entryId;
  dragOverId.value = entryId;
  activePointerId.value = event.pointerId;
  window.addEventListener('pointermove', onInitiativeDragMove);
  window.addEventListener('pointerup', onInitiativeDragEnd);
  window.addEventListener('pointercancel', onInitiativeDragEnd);
}

function onInitiativeDragMove(event: PointerEvent) {
  if (activePointerId.value !== event.pointerId) return;
  dragPosition.value = { x: event.clientX, y: event.clientY };
  const hit = document
    .elementFromPoint(event.clientX, event.clientY)
    ?.closest<HTMLElement>('[data-initiative-id]')
    ?.dataset.initiativeId;
  if (hit) dragOverId.value = hit;
}

async function onInitiativeDragEnd(event: PointerEvent) {
  if (activePointerId.value !== event.pointerId) return;
  removeInitiativeDragListeners();

  const fromId = draggedInitiativeId.value;
  const toId = dragOverId.value;
  clearInitiativeDrag();

  if (fromId && toId && fromId !== toId) {
    await reorderInitiative(fromId, toId);
  }
}

function removeInitiativeDragListeners() {
  window.removeEventListener('pointermove', onInitiativeDragMove);
  window.removeEventListener('pointerup', onInitiativeDragEnd);
  window.removeEventListener('pointercancel', onInitiativeDragEnd);
}

function clearInitiativeDrag() {
  draggedInitiativeId.value = null;
  dragOverId.value = null;
  dragPosition.value = null;
  activePointerId.value = null;
}

onBeforeUnmount(() => {
  removeInitiativeDragListeners();
});

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
  if (!confirm('End this session? Players will be disconnected.')) return;
  isSaving.value = true;
  try {
    await api(`/api/sessions/${route.params.id}/stop`, { method: 'POST' });
    await refresh();
    toastInfo('Session ended.');
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
  const url = import.meta.client ? `${window.location.origin}${state.value.joinUrl}` : state.value.joinUrl;
  if (import.meta.client && navigator.clipboard) {
    await navigator.clipboard.writeText(url);
    toastSuccess('Join link copied!');
  }
}

function toggleAction(id: string) {
  if (expandedActions.value.has(id)) expandedActions.value.delete(id);
  else expandedActions.value.add(id);
}
</script>

<template>
  <section class="app-shell dm-app-shell">
    <!-- Topbar -->
    <header class="topbar">
      <div class="topbar-brand">
        <span>🎲</span>
        <div>
          <strong>DM Screen</strong>
          <div class="topbar-sub">{{ game?.name }}</div>
        </div>
      </div>
      <div class="topbar-actions" v-if="state">
        <span class="badge" :class="isCombat ? 'combat' : 'exploration'">{{ state.state }}</span>
        <NuxtLink class="btn ghost sm" to="/games">← Games</NuxtLink>
        <NuxtLink v-if="!state.isActive" class="btn ghost sm" :to="`/sessions/${route.params.id}/summary`">
          View Summary
        </NuxtLink>
        <button v-if="state.isActive" class="btn danger sm" type="button" :disabled="isSaving" @click="stopSession">
          Stop Session
        </button>
        <span v-else class="badge ended">Ended</span>
      </div>
    </header>

    <div v-if="!state" class="stack" style="place-items: center; padding-top: 4rem;">
      <p class="muted">Loading session…</p>
    </div>

    <main v-else class="stack dm-screen-main">
      <!-- Session info bar -->
      <div class="panel dm-session-info">
        <div class="flex items-center justify-between gap-3" style="flex-wrap: wrap; gap: 1rem;">
          <div>
            <div class="flex items-center gap-2 mb-1">
              <h2 style="margin: 0;">Session Join Link</h2>
            </div>
            <p style="margin: 0; font-size: 0.82rem;">Share with players to join from any device.</p>
          </div>
          <div class="flex items-center gap-2" style="flex: 1; max-width: 36rem;">
            <input
              :value="typeof window !== 'undefined' ? `${window.location.origin}${state.joinUrl}` : state.joinUrl"
              readonly
              class="flex-1"
              style="font-size: 0.8rem; font-family: monospace;"
            />
            <button class="btn ghost sm" type="button" @click="copyJoinLink">Copy</button>
          </div>
          <div v-if="state.isActive" class="flex items-center gap-2">
            <span class="text-xs muted">Mode</span>
            <span class="badge" :class="isCombat ? 'combat' : 'exploration'">{{ state.state }}</span>
          </div>
        </div>
      </div>

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
            </div>

            <div v-if="pendingActions.length === 0" class="empty-state" style="padding: 1.5rem 0;">
              <p class="text-sm">No actions waiting. Players can submit actions via their session link.</p>
            </div>

            <div class="pending-actions-list">
              <div v-for="action in pendingActions" :key="action.id" class="action-card pending-card">
                <div class="action-card-header">
                  <div>
                    <div class="action-card-actor">{{ action.actorName }}</div>
                    <div class="action-card-target">
                      uses <strong>{{ action.actionText }}</strong>
                      <span v-if="action.targetName"> on {{ action.targetName }}</span>
                    </div>
                    <div v-if="action.description" class="action-card-desc">{{ action.description }}</div>
                  </div>
                  <span class="badge pending">Pending</span>
                </div>

                <form @submit.prevent="resolveAction(action)">
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
                    ✓ Publish Resolution
                  </button>
                </form>
              </div>
            </div>
          </div>

          <!-- Published action log -->
          <div class="panel dashboard-primary-panel action-log-panel">
            <div class="panel-title">
              <div>
                <h2>Action Log</h2>
                <p class="text-sm">Most recent resolved actions appear first.</p>
              </div>
              <span v-if="publishedActions.length" class="badge published">{{ publishedActions.length }} resolved</span>
            </div>
            <div class="action-log-scroll">
              <div v-if="publishedActions.length === 0" class="empty-state" style="padding: 1rem 0;">
                <p class="text-sm">No resolved actions yet.</p>
              </div>
              <div
                v-for="action in publishedActions"
                :key="action.id"
                class="action-card published-card"
                style="cursor: pointer;"
                @click="toggleAction(action.id)"
              >
                <div class="action-card-header">
                  <div>
                    <div class="action-card-actor">{{ action.actorName }}</div>
                    <div class="action-card-target">
                      used <strong>{{ action.actionText }}</strong>
                      <span v-if="action.targetName"> on {{ action.targetName }}</span>
                    </div>
                  </div>
                  <span class="badge published">Done</span>
                </div>
                <div v-if="expandedActions.has(action.id)" class="action-resolution">
                  <div v-if="action.rollSummary" class="roll-summary">🎲 {{ action.rollSummary }}</div>
                  <p style="margin: 0; font-size: 0.875rem; color: var(--ink);">{{ action.resolutionText }}</p>
                  <p v-if="action.additionalActions" style="margin-top: 0.4rem; font-size: 0.82rem; color: var(--muted-light);">{{ action.additionalActions }}</p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Right: Combat + NPC -->
        <div class="session-support-column">
          <!-- Combat tracker -->
          <div class="panel">
            <div class="panel-title">
              <div>
                <h2>Combat</h2>
                <p class="text-sm">{{ isCombat ? 'Drag to change turn order.' : 'All characters and NPCs will be included.' }}</p>
              </div>
              <div class="btn-row">
                <button v-if="isCombat && state.initiative.length" class="btn sm" type="button" :disabled="isSaving" @click="advanceTurn">Next Turn →</button>
                <button v-if="!isCombat" class="btn ghost sm" type="button" :disabled="isSaving" @click="setupCombat">
                  {{ isSaving ? 'Starting…' : 'Set Initiative' }}
                </button>
              </div>
            </div>

            <!-- Current turn banner -->
            <div v-if="currentTurn && isCombat" class="alert info" style="margin-bottom: 1rem;">
              ⚔️ <strong>{{ currentTurn.combatantName }}'s turn</strong>
            </div>

            <!-- Draggable initiative list -->
            <ul v-if="isCombat && displayedInitiative.length" class="initiative-list" style="margin-bottom: 1rem;">
              <li
                v-for="(entry, idx) in displayedInitiative"
                :key="entry.id"
                class="initiative-item"
                :class="{
                  'current-turn': entry.isCurrentTurn,
                  'dragging': draggedInitiativeId === entry.id,
                  'draggable': isCombat && !isSaving,
                  'drag-over': dragOverId === entry.id && draggedInitiativeId !== null && draggedInitiativeId !== entry.id,
                }"
                :data-initiative-id="entry.id"
                @pointerdown="startInitiativeDrag(entry.id, $event)"
              >
                <span class="initiative-order">{{ idx + 1 }}</span>
                <span class="initiative-card-body">
                  <span class="initiative-name">{{ entry.combatantName }}</span>
                  <span class="initiative-type">{{ entry.combatantType }}</span>
                </span>
                <span v-if="entry.isCurrentTurn" class="badge active">Turn</span>
              </li>
            </ul>

            <button v-if="isCombat" class="btn danger w-full" type="button" :disabled="isSaving" @click="endCombat">
              End Combat
            </button>
          </div>

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
                <select v-model="selectedNpcId" required>
                  <option value="">Choose NPC / Monster</option>
                  <option v-for="npc in game.npcsAndMonsters" :key="npc.id" :value="npc.id">{{ npc.name }}</option>
                </select>
              </label>
              <label>Action<input v-model.trim="npcAction" placeholder="Attacks, casts, hides…" required /></label>
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

          <!-- Character stats -->
          <div class="panel">
            <div class="panel-title">
              <div>
                <h2>Characters</h2>
                <p class="text-sm">Open a character to review health, armor, and sheet stats.</p>
              </div>
              <span v-if="state.game.characters.length" class="badge active">{{ state.game.characters.length }}</span>
            </div>

            <div v-if="state.game.characters.length === 0" class="empty-state" style="padding: 1rem 0;">
              <p class="text-sm">No characters have joined this game yet.</p>
            </div>

            <div v-else class="entity-stat-list">
              <details v-for="ch in state.game.characters" :key="ch.id" class="entity-stat-details">
                <summary>
                  <span>
                    <strong>{{ ch.name }}</strong>
                    <small>{{ ch.playerName || 'No player name' }}</small>
                  </span>
                  <span class="entity-stat-summary">HP {{ ch.health }}/{{ ch.maxHealth }} · AC {{ ch.armor }}</span>
                </summary>
                <CharacterSheet :character="ch" />
              </details>
            </div>
          </div>

          <!-- NPC stats -->
          <div class="panel">
            <div class="panel-title">
              <div>
                <h2>NPCs / Monsters</h2>
                <p class="text-sm">Open an NPC to review health, armor, and stat block details.</p>
              </div>
              <span v-if="state.game.npcsAndMonsters.length" class="badge active">{{ state.game.npcsAndMonsters.length }}</span>
            </div>

            <div v-if="state.game.npcsAndMonsters.length === 0" class="empty-state" style="padding: 1rem 0;">
              <p class="text-sm">No NPCs or monsters have been added.</p>
            </div>

            <div v-else class="entity-stat-list">
              <details v-for="npc in state.game.npcsAndMonsters" :key="npc.id" class="entity-stat-details">
                <summary>
                  <span>
                    <strong>{{ npc.name }}</strong>
                    <small>{{ npc.kind }}</small>
                  </span>
                  <span class="entity-stat-actions">
                    <button
                      class="npc-visibility-btn"
                      :class="`visibility-${(npc.visibility ?? 'Visible').toLowerCase()}`"
                      type="button"
                      :title="`Click to cycle: Visible → Obscured → Hidden (currently ${npc.visibility ?? 'Visible'})`"
                      @click.stop="cycleNpcVisibility(npc.id, npc.visibility ?? 'Visible')"
                    >
                      {{ npc.visibility === 'Hidden' ? 'Hidden' : npc.visibility === 'Obscured' ? 'Obscured' : 'Visible' }}
                    </button>
                    <span class="entity-stat-summary">HP {{ npc.health }}/{{ npc.maxHealth }} · AC {{ npc.armor }}</span>
                  </span>
                </summary>
                <NpcSheet :npc="npc" />
              </details>
            </div>
          </div>
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
</template>
