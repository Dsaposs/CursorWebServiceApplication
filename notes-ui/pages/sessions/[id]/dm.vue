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

// Combat setup
const combatantInitiative = ref<Record<string, number>>({});

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
  const combatants = [
    ...state.value.game.characters.map(c => ({ type: 'Character', id: c.id, initiative: combatantInitiative.value[c.id] || 0 })),
    ...state.value.game.npcsAndMonsters.map(n => ({ type: 'NpcOrMonster', id: n.id, initiative: combatantInitiative.value[n.id] || 0 })),
  ].filter(c => c.initiative > 0);

  try {
    await api(`/api/sessions/${route.params.id}/combat`, { method: 'POST', body: { combatants } });
    await refresh();
    toastSuccess('Initiative order set.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
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
  <section class="app-shell">
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

    <main v-else class="stack">
      <!-- Session info bar -->
      <div class="panel">
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
          <div class="btn-row" v-if="state.isActive">
            <button class="btn ghost sm" :class="{ active: !isCombat }" type="button" :disabled="isSaving" @click="setState('Exploration')">Exploration</button>
            <button class="btn ghost sm" :class="{ active: isCombat }" type="button" :disabled="isSaving" @click="setState('Combat')">Combat</button>
          </div>
        </div>
      </div>

      <div class="grid-2">
        <!-- Left: Action queue -->
        <div>
          <!-- Pending actions -->
          <div class="panel mb-2" style="margin-bottom: 1rem;">
            <div class="panel-title">
              <h2>
                Pending Actions
                <span v-if="pendingActions.length" class="badge pending" style="margin-left: 0.4rem;">{{ pendingActions.length }}</span>
              </h2>
            </div>

            <div v-if="pendingActions.length === 0" class="empty-state" style="padding: 1.5rem 0;">
              <p class="text-sm">No actions waiting. Players can submit actions via their session link.</p>
            </div>

            <div style="display: grid; gap: 0.75rem;">
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
          <div class="panel">
            <div class="panel-title">
              <h2>Action Log</h2>
            </div>
            <div v-if="publishedActions.length === 0" class="empty-state" style="padding: 1rem 0;">
              <p class="text-sm">No resolved actions yet.</p>
            </div>
            <div style="display: grid; gap: 0.5rem;">
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
        <div style="display: grid; gap: 1rem; align-content: start;">
          <!-- Combat tracker -->
          <div class="panel">
            <div class="panel-title">
              <h2>Combat</h2>
              <div class="btn-row" v-if="state.initiative.length && isCombat">
                <button class="btn sm" type="button" :disabled="isSaving" @click="advanceTurn">Next Turn →</button>
              </div>
            </div>

            <!-- Current turn banner -->
            <div v-if="currentTurn && isCombat" class="alert info" style="margin-bottom: 1rem;">
              ⚔️ <strong>{{ currentTurn.combatantName }}'s turn</strong>
            </div>

            <!-- Initiative order -->
            <ul v-if="state.initiative.length" class="initiative-list" style="margin-bottom: 1rem;">
              <li
                v-for="(entry, idx) in state.initiative"
                :key="entry.id"
                class="initiative-item"
                :class="{ 'current-turn': entry.isCurrentTurn }"
              >
                <span class="initiative-order">{{ idx + 1 }}</span>
                <span class="initiative-name">{{ entry.combatantName }}</span>
                <span class="initiative-type">{{ entry.combatantType }}</span>
              </li>
            </ul>

            <!-- Set initiative form -->
            <details>
              <summary style="cursor: pointer; font-size: 0.82rem; color: var(--muted-light); padding: 0.25rem 0; margin-bottom: 0.5rem;">
                {{ state.initiative.length ? 'Update Initiative' : 'Set Initiative' }}
              </summary>
              <form @submit.prevent="setupCombat">
                <div style="display: grid; gap: 0.4rem; margin-bottom: 0.75rem;">
                  <div
                    v-for="entity in [...state.game.characters, ...state.game.npcsAndMonsters]"
                    :key="entity.id"
                    class="flex items-center gap-2"
                  >
                    <span class="flex-1 text-sm">{{ entity.name }}</span>
                    <input
                      v-model.number="combatantInitiative[entity.id]"
                      type="number"
                      min="0"
                      placeholder="0"
                      style="width: 4rem; text-align: center;"
                    />
                  </div>
                </div>
                <button class="btn ghost sm w-full" type="submit" :disabled="isSaving">Set Order</button>
              </form>
            </details>
          </div>

          <!-- NPC quick action -->
          <div v-if="game?.npcsAndMonsters.length" class="panel">
            <h2>NPC Action</h2>
            <form @submit.prevent="submitNpcAction">
              <label>
                NPC
                <select v-model="selectedNpcId" required>
                  <option value="">Choose NPC / Monster</option>
                  <option v-for="npc in game.npcsAndMonsters" :key="npc.id" :value="npc.id">{{ npc.name }}</option>
                </select>
              </label>
              <label>Action<input v-model.trim="npcAction" placeholder="Attacks, casts, hides…" required /></label>
              <label>Target<input v-model.trim="npcTarget" placeholder="Optional" /></label>
              <button class="btn ghost sm" type="submit" :disabled="isSaving">Submit NPC Action</button>
            </form>
          </div>

          <!-- Active combatant health -->
          <div v-if="state.game.characters.length || state.game.npcsAndMonsters.length" class="panel">
            <h2>Combatant Health</h2>
            <div style="display: grid; gap: 0.75rem;">
              <div v-for="ch in state.game.characters" :key="ch.id">
                <div class="flex justify-between text-xs muted" style="margin-bottom: 0.2rem;">
                  <span>{{ ch.name }}</span><span>AC {{ ch.armor }}</span>
                </div>
                <HealthBar :current="ch.health" :max="ch.maxHealth" />
              </div>
              <div v-for="npc in state.game.npcsAndMonsters" :key="npc.id">
                <div class="flex justify-between text-xs muted" style="margin-bottom: 0.2rem;">
                  <span>{{ npc.name }}</span><span class="muted text-xs">{{ npc.kind }}</span>
                </div>
                <HealthBar :current="npc.health" :max="npc.maxHealth" />
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-if="pollingError" class="alert error">{{ pollingError }}</div>
    </main>
  </section>
</template>
