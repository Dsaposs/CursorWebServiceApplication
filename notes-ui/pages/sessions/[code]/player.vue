<script setup lang="ts">
import type { InitiativeEntryResponse, RulesetResponse, SessionStateResponse } from '~/types/api';
import { useRulesetActionChooser } from '~/composables/useRulesetActionChooser';
import { parseRulesetDefinition } from '~/utils/rulesets';

const route = useRoute();
const { api } = useApi();
const { getSessionPlayerToken, getGamePlayerToken, setSessionPlayerToken } = usePlayerTokens();
const { error: toastError, success: toastSuccess } = useToast();

const playerToken = ref<string | null>(null);
const ruleset = ref<RulesetResponse | null>(null);
const showCharacterSheet = ref(true);
const targetName = ref('');
const description = ref('');
const isSubmitting = ref(false);
const showActionForm = ref(false);

async function loadState() {
  if (!playerToken.value) return null;
  const nextState = await api<SessionStateResponse>(`/api/session-join/${route.params.code}/state`, {
    playerToken: playerToken.value,
  });
  if (!ruleset.value) {
    ruleset.value = await api<RulesetResponse>(`/api/rulesets/${nextState.game.rulesetCode}`);
  }
  return nextState;
}

const { state, pollingError, connectionStatus, refresh, start } = useSessionPolling(loadState, 3000);

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
const rulesetDefinition = computed(() => parseRulesetDefinition(ruleset.value));
const actingClassKey = computed(() => state.value?.character?.classKey ?? '');
const {
  actionMode,
  selectedActionKey,
  selectedSkillKey,
  selectedAttributeKey,
  customActionText: actionText,
  availableActions,
  availableSkills,
  availableAttributes,
  selectedActionDetail,
  selectedSkillDetail,
  selectedAttributeDetail,
  resetSelection: resetActionSelection,
  buildSubmitPayload,
} = useRulesetActionChooser(rulesetDefinition, actingClassKey);
const pendingPlayerActions = computed(() =>
  state.value?.actions.filter(action =>
    action.status === 'Pending' && action.actorName === state.value?.character?.name,
  ) ?? [],
);

onMounted(async () => {
  playerToken.value = getSessionPlayerToken(route.params.code);
  if (!playerToken.value) {
    try {
      const options = await api<{ session: { gameId: string } }>(`/api/session-join/${route.params.code}`);
      const gameToken = getGamePlayerToken(options.session.gameId);
      if (gameToken) {
        playerToken.value = gameToken;
        setSessionPlayerToken(route.params.code, gameToken);
      }
    } catch {
      // The join page will surface a helpful message if the session lookup fails.
    }
  }
  if (!playerToken.value) {
    await navigateTo(`/join/${route.params.code}`);
    return;
  }
  start();
});

async function submitAction() {
  if (!playerToken.value || !state.value) return;
  const payload = buildSubmitPayload(description.value);
  if (!payload) {
    toastError('Choose or describe an action first.');
    return;
  }

  isSubmitting.value = true;
  try {
    await api(`/api/sessions/${state.value.joinCode}/actions`, {
      method: 'POST',
      playerToken: playerToken.value,
      body: {
        actionKey: payload.actionKey,
        actionText: payload.actionText,
        targetName: targetName.value || undefined,
        description: payload.description,
      },
    });
    resetActionSelection();
    targetName.value = '';
    description.value = '';
    await refresh();
    showActionForm.value = false;
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
        <span aria-hidden="true">🧙</span>
        <div>
          <strong>{{ state?.character?.name || 'Player' }}</strong>
          <div class="topbar-sub">{{ state?.game.name }}</div>
        </div>
      </div>
      <div class="topbar-actions">
        <SessionConnectionStatus
          v-if="state"
          :status="connectionStatus"
          :error="pollingError"
          :started-at="state.startedAt"
          :ended-at="state.endedAt"
          :is-active="state.isActive"
        />
        <span v-if="state" class="badge" :class="isCombat ? 'combat' : 'exploration'">{{ state.state }}</span>
      </div>
    </header>

    <div v-if="!state" class="stack">
      <SkeletonBlock :lines="4" />
      <SkeletonBlock :lines="6" />
      <SkeletonBlock :lines="5" />
    </div>

    <main v-else class="stack">
      <!-- Character card -->
      <div class="panel">
        <div class="flex justify-between items-center" style="margin-bottom: 1rem; flex-wrap: wrap; gap: 1rem;">
          <div>
            <h1 style="margin-bottom: 0.15rem;">{{ state.character?.name }}</h1>
            <p style="margin: 0; font-size: 0.85rem;">{{ state.game.rulesetName }}</p>
          </div>
          <div class="btn-row">
            <div v-if="isCombat && currentTurn">
              <span v-if="isMyTurn" class="badge active" style="font-size: 0.85rem; padding: 0.35rem 0.8rem;">
                <span aria-hidden="true">⚔️</span> Your Turn!
              </span>
              <span v-else class="badge" style="background: var(--panel-alt); color: var(--muted-light); border: 1px solid var(--border);">
                {{ currentTurn.combatantName }}'s turn
              </span>
            </div>
            <button
              class="btn ghost sm"
              type="button"
              :aria-expanded="showCharacterSheet"
              @click="showCharacterSheet = !showCharacterSheet"
            >
              {{ showCharacterSheet ? 'Hide Sheet' : 'Show Sheet' }}
            </button>
          </div>
        </div>

        <CharacterSheet v-if="state.character && showCharacterSheet" :character="state.character" />
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
        <div class="panel-title">
          <div>
            <h2>Actions</h2>
            <p v-if="pendingPlayerActions.length" class="text-sm">
              {{ pendingPlayerActions.length }} action{{ pendingPlayerActions.length === 1 ? '' : 's' }} pending DM review.
            </p>
            <p v-else class="text-sm">Ready when you are. Actions stay hidden until you choose to take one.</p>
          </div>
          <button
            v-if="!showActionForm"
            class="btn"
            type="button"
            @click="showActionForm = true"
          >
            Take Action
          </button>
        </div>
        <p v-if="isCombat && currentTurn && !isMyTurn" style="font-size: 0.85rem;">
          Waiting for <strong>{{ currentTurn.combatantName }}</strong> — you can queue an action now.
        </p>
        <form v-if="showActionForm" @submit.prevent="submitAction">
          <label>
            Action type <span style="color: var(--danger);">*</span>
            <select v-model="actionMode">
              <option v-if="availableActions.length" value="action">Predefined action</option>
              <option v-if="availableSkills.length" value="skill">Skill check</option>
              <option v-if="availableAttributes.length" value="attribute">Attribute check</option>
              <option value="custom">Custom action</option>
            </select>
          </label>

          <label v-if="actionMode === 'action' && availableActions.length">
            Predefined action <span style="color: var(--danger);">*</span>
            <select v-model="selectedActionKey" required>
              <option value="">Choose a predefined action</option>
              <option v-for="action in availableActions" :key="action.key" :value="action.key">
                {{ action.label }}
              </option>
            </select>
          </label>

          <label v-else-if="actionMode === 'skill' && availableSkills.length">
            Skill <span style="color: var(--danger);">*</span>
            <select v-model="selectedSkillKey" required>
              <option value="">Choose a skill</option>
              <option v-for="skill in availableSkills" :key="skill.key" :value="skill.key">
                {{ skill.label }}
              </option>
            </select>
          </label>

          <label v-else-if="actionMode === 'attribute' && availableAttributes.length">
            Attribute <span style="color: var(--danger);">*</span>
            <select v-model="selectedAttributeKey" required>
              <option value="">Choose an attribute</option>
              <option v-for="attribute in availableAttributes" :key="attribute.key" :value="attribute.key">
                {{ attribute.label }}
              </option>
            </select>
          </label>

          <label v-else>
            Custom action <span style="color: var(--danger);">*</span>
            <input v-model.trim="actionText" placeholder="Swing sword, use medkit, lockpick door…" required />
          </label>

          <div v-if="selectedActionDetail" class="alert info">
            <div>
              <strong>{{ selectedActionDetail.dice }}</strong>
              <p class="text-sm">
                Roll {{ selectedActionDetail.attribute }} + {{ selectedActionDetail.skill }}.
                Modifiers: {{ selectedActionDetail.modifiers }}.
              </p>
              <p class="text-sm">{{ selectedActionDetail.successRule }}</p>
            </div>
          </div>
          <div v-else-if="selectedSkillDetail || selectedAttributeDetail" class="alert info">
            <div>
              <strong>{{ selectedSkillDetail?.actionText ?? selectedAttributeDetail?.actionText }}</strong>
              <p class="text-sm">
                Suggested roll: {{ selectedSkillDetail?.rollSummary ?? selectedAttributeDetail?.rollSummary }}.
              </p>
            </div>
          </div>
          <label>
            Target
            <input v-model.trim="targetName" placeholder="Orc, door, ally… (optional)" />
          </label>
          <label>
            Description
            <textarea v-model="description" placeholder="What are you trying to accomplish?" style="min-height: 3rem;" />
          </label>
          <div class="btn-row">
            <button class="btn" type="submit" :disabled="isSubmitting">
              {{ isSubmitting ? 'Sending…' : 'Send to DM' }}
            </button>
            <button class="btn ghost" type="button" :disabled="isSubmitting" @click="showActionForm = false">
              Cancel
            </button>
          </div>
        </form>
      </div>

      <!-- Action feed -->
      <div class="panel">
        <h2>Action Feed</h2>
        <div v-if="state.actions.length === 0" class="empty-state" style="padding: 1rem 0;">
          <p class="text-sm">No actions yet this session.</p>
        </div>
        <div style="display: grid; gap: 0.5rem;">
          <ActionCard
            v-for="action in [...state.actions].reverse()"
            :key="action.id"
            :action="action"
            prefix=""
          />
        </div>
      </div>

      <div v-if="pollingError" class="alert error">{{ pollingError }}</div>
    </main>
  </section>
</template>
