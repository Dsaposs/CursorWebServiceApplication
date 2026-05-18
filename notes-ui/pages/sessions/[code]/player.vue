<script setup lang="ts">
import type { InitiativeEntryResponse, SessionStateResponse } from '~/types/api';

const route = useRoute();
const { api } = useApi();
const { error: toastError, success: toastSuccess } = useToast();

const playerToken = ref<string | null>(null);
const actionText = ref('');
const targetName = ref('');
const description = ref('');
const isSubmitting = ref(false);

async function loadState() {
  if (!playerToken.value) return null;
  return await api<SessionStateResponse>(`/api/session-join/${route.params.code}/state`, {
    playerToken: playerToken.value,
  });
}

const { state, pollingError, refresh, start } = useSessionPolling(loadState, 3000);

const currentTurn = computed<InitiativeEntryResponse | null>(
  () => state.value?.initiative.find(e => e.isCurrentTurn) ?? null,
);

// Only show "your turn" hint when in combat mode and a turn is assigned to this character
const isMyTurn = computed(() => {
  if (state.value?.state !== 'Combat') return false;
  if (!currentTurn.value) return false;
  return currentTurn.value.combatantId === state.value?.character?.id;
});

const isCombat = computed(() => state.value?.state === 'Combat');

onMounted(async () => {
  playerToken.value = localStorage.getItem(`ttrpg_player_${route.params.code}`);
  if (!playerToken.value) {
    await navigateTo(`/join/${route.params.code}`);
    return;
  }
  start();
});

async function submitAction() {
  if (!playerToken.value || !state.value) return;
  isSubmitting.value = true;
  try {
    await api(`/api/sessions/${state.value.joinCode}/actions`, {
      method: 'POST',
      playerToken: playerToken.value,
      body: {
        actionText: actionText.value,
        targetName: targetName.value || undefined,
        description: description.value || undefined,
      },
    });
    actionText.value = '';
    targetName.value = '';
    description.value = '';
    await refresh();
    toastSuccess('Action sent to DM!');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSubmitting.value = false;
  }
}
</script>

<template>
  <section class="app-shell">
    <!-- Topbar -->
    <header class="topbar">
      <div class="topbar-brand">
        <span>🧙</span>
        <div>
          <strong>{{ state?.character?.name || 'Player' }}</strong>
          <div class="topbar-sub">{{ state?.game.name }}</div>
        </div>
      </div>
      <div class="topbar-actions">
        <span v-if="state" class="badge" :class="isCombat ? 'combat' : 'exploration'">{{ state.state }}</span>
        <NuxtLink class="btn ghost sm" :to="`/join/${route.params.code}`">Switch Character</NuxtLink>
      </div>
    </header>

    <div v-if="!state" class="stack" style="padding-top: 4rem; text-align: center;">
      <p class="muted">Connecting to session…</p>
    </div>

    <main v-else class="stack">
      <!-- Character card -->
      <div class="panel">
        <div class="flex justify-between items-center" style="margin-bottom: 1rem; flex-wrap: wrap; gap: 1rem;">
          <div>
            <h1 style="margin-bottom: 0.15rem;">{{ state.character?.name }}</h1>
            <p style="margin: 0; font-size: 0.85rem;">{{ state.game.rulesetName }}</p>
          </div>
          <div v-if="isCombat && currentTurn">
            <span v-if="isMyTurn" class="badge active" style="font-size: 0.85rem; padding: 0.35rem 0.8rem;">⚔️ Your Turn!</span>
            <span v-else class="badge" style="background: var(--panel-alt); color: var(--muted-light); border: 1px solid var(--border);">
              {{ currentTurn.combatantName }}'s turn
            </span>
          </div>
        </div>

        <div class="grid-2" style="margin-bottom: 1rem;">
          <HealthBar :current="state.character?.health ?? 0" :max="state.character?.maxHealth ?? 1" />
          <div class="stat-grid" style="grid-template-columns: 1fr;">
            <div class="stat-cell">
              <dt>Armor</dt>
              <dd>{{ state.character?.armor }}</dd>
            </div>
          </div>
        </div>

        <details>
          <summary style="cursor: pointer; font-size: 0.8rem; color: var(--muted-light);">Character sheet & inventory</summary>
          <div style="margin-top: 0.6rem; display: grid; gap: 0.5rem;">
            <pre style="font-size: 0.72rem;">{{ state.character?.rulesetDataJson }}</pre>
            <pre style="font-size: 0.72rem;">{{ state.character?.inventoryJson }}</pre>
          </div>
        </details>
      </div>

      <!-- Initiative tracker (combat only) -->
      <div v-if="isCombat && state.initiative.length" class="panel">
        <h2>Initiative Order</h2>
        <ul class="initiative-list">
          <li
            v-for="(entry, idx) in state.initiative"
            :key="entry.id"
            class="initiative-item"
            :class="{ 'current-turn': entry.isCurrentTurn }"
          >
            <span class="initiative-order">{{ idx + 1 }}</span>
            <span class="initiative-name">{{ entry.combatantName }}</span>
            <span v-if="entry.combatantId === state.character?.id" class="badge active" style="font-size: 0.65rem; padding: 0.1rem 0.4rem;">You</span>
          </li>
        </ul>
      </div>

      <!-- Submit action -->
      <div class="panel">
        <h2>Submit Action</h2>
        <p v-if="isCombat && currentTurn && !isMyTurn" style="font-size: 0.85rem;">
          Waiting for <strong>{{ currentTurn.combatantName }}</strong> — you can queue an action now.
        </p>
        <form @submit.prevent="submitAction">
          <label>
            Action <span style="color: var(--danger);">*</span>
            <input v-model.trim="actionText" placeholder="Swing sword, use medkit, lockpick door…" required />
          </label>
          <label>
            Target
            <input v-model.trim="targetName" placeholder="Orc, door, ally… (optional)" />
          </label>
          <label>
            Description
            <textarea v-model="description" placeholder="What are you trying to accomplish?" style="min-height: 3rem;" />
          </label>
          <button class="btn w-full" type="submit" :disabled="isSubmitting">
            {{ isSubmitting ? 'Sending…' : '→ Send to DM' }}
          </button>
        </form>
      </div>

      <!-- Action feed -->
      <div class="panel">
        <h2>Action Feed</h2>
        <div v-if="state.actions.length === 0" class="empty-state" style="padding: 1rem 0;">
          <p class="text-sm">No actions yet this session.</p>
        </div>
        <div style="display: grid; gap: 0.5rem;">
          <div
            v-for="action in [...state.actions].reverse()"
            :key="action.id"
            class="action-card"
            :class="action.status === 'Published' ? 'published-card' : 'pending-card'"
          >
            <div class="action-card-header">
              <div>
                <div class="action-card-actor">{{ action.actorName }}</div>
                <div class="action-card-target">
                  {{ action.actionText }}
                  <span v-if="action.targetName"> → {{ action.targetName }}</span>
                </div>
              </div>
              <span class="badge" :class="action.status === 'Published' ? 'published' : 'pending'">
                {{ action.status }}
              </span>
            </div>
            <template v-if="action.status === 'Published'">
              <div class="action-resolution">
                <div v-if="action.rollSummary" class="roll-summary">🎲 {{ action.rollSummary }}</div>
                <p style="margin: 0; font-size: 0.875rem; color: var(--ink);">{{ action.resolutionText }}</p>
                <p v-if="action.additionalActions" style="margin-top: 0.35rem; font-size: 0.82rem; color: var(--muted-light);">{{ action.additionalActions }}</p>
              </div>
            </template>
            <p v-else class="text-xs muted">Waiting for DM to resolve…</p>
          </div>
        </div>
      </div>

      <div v-if="pollingError" class="alert error">{{ pollingError }}</div>
    </main>
  </section>
</template>
