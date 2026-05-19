<script setup lang="ts">
import type { InitiativeEntryResponse, RulesetResponse, SessionStateResponse } from '~/types/api';
import { useRulesetActionChooser } from '~/composables/useRulesetActionChooser';
import { evaluateActionOutcome } from '~/utils/actionOutcome';
import { parseCharacterStats } from '~/utils/dice';
import { parseInventory } from '~/utils/inventory';
import { resolveEffectiveActionRoll } from '~/utils/items';
import { findRulesetAction, parseRulesetDefinition } from '~/utils/rulesets';
import { useDiceRollContext } from '~/composables/useDiceRollContext';
import PlayerRollPromptOverlay from '~/components/PlayerRollPromptOverlay.vue';

const route = useRoute();
const { api } = useApi();
const { getSessionPlayerToken, getGamePlayerToken, setSessionPlayerToken } = usePlayerTokens();
const { error: toastError, success: toastSuccess } = useToast();

const playerToken = ref<string | null>(null);
const ruleset = ref<RulesetResponse | null>(null);
const showCharacterSheet = ref(true);
const actionTargetPickerRef = ref<{ isValid: () => boolean; reset: () => void; toSubmitFields: () => { targetCharacterId?: string; targetNpcId?: string; targetName?: string } } | null>(null);
const description = ref('');
const rollResult = ref('');
const damageRollResult = ref('');
const isSubmitting = ref(false);
const isSubmittingRollPrompt = ref(false);
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

const { state, pollingError, fatalError, connectionStatus, refresh, start } = useSessionPolling(loadState, 3000);

function playerSummaryLocation(session: SessionStateResponse) {
  return {
    path: `/sessions/${session.id}/summary`,
    query: {
      player: '1',
      gameId: session.gameId,
    },
  };
}

watch(
  () => state.value?.isActive,
  (isActive) => {
    if (isActive === false && state.value) {
      navigateTo(playerSummaryLocation(state.value));
    }
  },
);

watch(fatalError, (err) => {
  if (!err) return;
  const query: Record<string, string> = {};
  if (state.value?.id) query.sessionId = state.value.id;
  if (state.value?.gameId) query.gameId = state.value.gameId;
  navigateTo({ path: '/sessions/ended', query });
});

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
const playerInventory = computed(() => parseInventory(state.value?.character?.inventoryJson));
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
} = useRulesetActionChooser(rulesetDefinition, actingClassKey, playerInventory);
const pendingPlayerActions = computed(() =>
  state.value?.actions.filter(action =>
    action.status === 'Pending'
    && action.actorCharacterId === state.value?.character?.id,
  ) ?? [],
);

const expandedPendingPlayerActions = ref<Set<string>>(new Set());

const activeRollPrompt = computed(() =>
  (state.value?.rollPrompts ?? []).find(prompt => prompt.status === 'Pending') ?? null,
);

const publishedFeedActions = computed(() =>
  state.value?.actions.filter(action =>
    action.status === 'Published' || action.status === 'Rejected',
  ) ?? [],
);

const expandedFeedActions = ref<Set<string>>(new Set());
const feedCombatEncounters = computed(() => state.value?.combatEncounters ?? []);
const sessionId = computed(() => state.value?.id);

const {
  expandedGroups: expandedFeedGroups,
  toggleGroup: toggleFeedGroup,
  expandAllGroups: expandAllFeedGroups,
  collapseAllGroups: collapseAllFeedGroups,
} = useActionLogGroupExpansion(publishedFeedActions, feedCombatEncounters, sessionId);

function toggleFeedAction(id: string) {
  if (expandedFeedActions.value.has(id)) expandedFeedActions.value.delete(id);
  else expandedFeedActions.value.add(id);
}

function togglePendingPlayerAction(id: string) {
  if (expandedPendingPlayerActions.value.has(id)) expandedPendingPlayerActions.value.delete(id);
  else expandedPendingPlayerActions.value.add(id);
}

function expandAllFeed() {
  expandAllFeedGroups();
  expandedFeedActions.value = new Set(publishedFeedActions.value.map(action => action.id));
}

function collapseAllFeed() {
  collapseAllFeedGroups();
  expandedFeedActions.value = new Set();
}

async function withdrawAction(actionId: string) {
  if (!playerToken.value) return;
  isSubmitting.value = true;
  try {
    await api(`/api/actions/${actionId}`, {
      method: 'DELETE',
      playerToken: playerToken.value,
    });
    expandedPendingPlayerActions.value.delete(actionId);
    await refresh();
    toastSuccess('Action withdrawn.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSubmitting.value = false;
  }
}

const playerStats = computed(() => parseCharacterStats(state.value?.character?.rulesetDataJson));
const playerAttributes = computed(() => playerStats.value.attributes);
const playerSkills = computed(() => playerStats.value.skills);
const playerGameValues = computed(() => playerStats.value.gameValues);

const selectedRulesetActionDef = computed(() =>
  findRulesetAction(rulesetDefinition.value, selectedActionKey.value),
);
const effectiveActionRoll = computed(() =>
  resolveEffectiveActionRoll(rulesetDefinition.value, selectedRulesetActionDef.value),
);
const attackOutcome = computed(() => {
  if (!rollResult.value || actionMode.value !== 'action') return null;
  return evaluateActionOutcome(
    rulesetDefinition.value,
    selectedActionKey.value,
    `🎲 Roll: ${rollResult.value}`,
  );
});
const showDamageRoll = computed(() =>
  Boolean(effectiveActionRoll.value?.damageRoll && attackOutcome.value === 'Pass'),
);

watch([selectedActionKey, rollResult], () => {
  if (!showDamageRoll.value) damageRollResult.value = '';
});

const rollContext = useDiceRollContext(
  rulesetDefinition,
  actionMode,
  selectedActionKey,
  selectedSkillKey,
  selectedAttributeKey,
  playerAttributes,
  playerSkills,
  playerGameValues,
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

async function submitRollPrompt(rollSummary: string) {
  if (!playerToken.value || !activeRollPrompt.value) return;

  isSubmittingRollPrompt.value = true;
  try {
    await api(`/api/roll-prompts/${activeRollPrompt.value.id}/submit`, {
      method: 'PUT',
      playerToken: playerToken.value,
      body: { rollSummary },
    });
    await refresh();
    toastSuccess('Roll sent to the DM.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSubmittingRollPrompt.value = false;
  }
}

async function submitAction() {
  if (!playerToken.value || !state.value) return;
  const payload = buildSubmitPayload(description.value);
  if (!payload) {
    toastError('Choose or describe an action first.');
    return;
  }
  if (!actionTargetPickerRef.value?.isValid()) {
    toastError('Enter a target name for Other.');
    return;
  }

  // Prepend the dice roll result to the description if the player rolled
  const fullDescription = [
    rollResult.value ? `🎲 Roll: ${rollResult.value}` : '',
    damageRollResult.value || '',
    payload.description ?? '',
  ].filter(Boolean).join('\n');

  isSubmitting.value = true;
  try {
    await api(`/api/sessions/${state.value.joinCode}/actions`, {
      method: 'POST',
      playerToken: playerToken.value,
      body: {
        actionKey: payload.actionKey,
        actionText: payload.actionText,
        ...actionTargetPickerRef.value.toSubmitFields(),
        description: fullDescription || undefined,
      },
    });
    resetActionSelection();
    actionTargetPickerRef.value?.reset();
    description.value = '';
    rollResult.value = '';
    damageRollResult.value = '';
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
  <PlayerRollPromptOverlay
    v-if="state?.character && activeRollPrompt"
    :prompt="activeRollPrompt"
    :character="state.character"
    :ruleset-definition="rulesetDefinition"
    :is-submitting="isSubmittingRollPrompt"
    @submit="submitRollPrompt"
  />

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
    </header>

    <div v-if="!state" class="stack session-screen-main">
      <SkeletonBlock :lines="4" />
      <SkeletonBlock :lines="6" />
      <SkeletonBlock :lines="5" />
    </div>

    <main v-else class="stack session-screen-main">
      <SessionNotesPanel
        mode="player"
        :join-code="String(route.params.code)"
        :player-token="playerToken"
      />

      <div class="session-dashboard-grid">
        <div class="session-primary-column">
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

        <CharacterSheet
          v-if="state.character && showCharacterSheet"
          :character="state.character"
          :ruleset-definition="rulesetDefinition"
        />
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

          <RulesetDiceRoller
            v-if="rollContext"
            v-model="rollResult"
            :context="rollContext"
          />

          <DamageRollRoller
            v-if="showDamageRoll && effectiveActionRoll?.damageRoll && rulesetDefinition"
            v-model="damageRollResult"
            :damage-roll="effectiveActionRoll.damageRoll"
            :definition="rulesetDefinition"
            :attributes="playerAttributes"
          />

          <ActionTargetPicker
            ref="actionTargetPickerRef"
            :characters="state.game.characters"
            :npcs="state.game.npcsAndMonsters"
            :disabled="isSubmitting"
          />
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

      <!-- Pending actions (withdraw) -->
      <div v-if="pendingPlayerActions.length" class="panel">
        <div class="panel-title">
          <div>
            <h2>Your Pending Actions</h2>
            <p class="text-sm">Waiting for the DM. You can withdraw an action before it is resolved.</p>
          </div>
        </div>
        <div class="pending-player-actions">
          <article
            v-for="action in pendingPlayerActions"
            :key="action.id"
            class="action-card pending-card"
          >
            <button
              type="button"
              class="action-card-header action-card-toggle"
              :aria-expanded="expandedPendingPlayerActions.has(action.id)"
              @click="togglePendingPlayerAction(action.id)"
            >
              <div>
                <div class="action-card-actor">{{ action.actorName }}</div>
                <div class="action-card-target">
                  <strong>{{ action.actionText }}</strong>
                  <span v-if="action.targetName"> on {{ action.targetName }}</span>
                </div>
              </div>
              <span class="badge pending">{{ expandedPendingPlayerActions.has(action.id) ? 'Hide' : 'Show' }}</span>
            </button>
            <div v-show="expandedPendingPlayerActions.has(action.id)" class="pending-player-action-body">
              <ActionCard
                :action="action"
                :game="state.game"
                :ruleset-definition="rulesetDefinition"
                prefix=""
                :collapsible="false"
                :expanded="true"
              />
              <button
                class="btn danger ghost sm"
                type="button"
                :disabled="isSubmitting"
                @click="withdrawAction(action.id)"
              >
                Withdraw action
              </button>
            </div>
          </article>
        </div>
      </div>

        </div>

        <aside class="session-feed-column">
          <div class="panel action-log-panel">
            <div class="panel-title">
              <div>
                <h2>Action Feed</h2>
                <p v-if="publishedFeedActions.length" class="text-sm">
                  Grouped by combat encounter. Expand actions to see full details.
                </p>
              </div>
              <div v-if="publishedFeedActions.length" class="btn-row">
                <button class="btn ghost sm" type="button" @click="expandAllFeed">Expand all</button>
                <button class="btn ghost sm" type="button" @click="collapseAllFeed">Collapse all</button>
              </div>
            </div>
            <div class="action-log-scroll">
              <div v-if="publishedFeedActions.length === 0" class="empty-state" style="padding: 1rem 0;">
                <p class="text-sm">No resolved actions yet this session.</p>
              </div>
              <ActionLogGrouped
                v-else
                :actions="publishedFeedActions"
                :combat-encounters="state.combatEncounters ?? []"
                :expanded-actions="expandedFeedActions"
                :expanded-groups="expandedFeedGroups"
                :game="state.game"
                :ruleset-definition="rulesetDefinition"
                action-prefix=""
                @toggle-action="toggleFeedAction"
                @toggle-group="toggleFeedGroup"
              />
            </div>
          </div>
        </aside>
      </div>

      <div v-if="pollingError" class="alert error">{{ pollingError }}</div>
    </main>
  </section>
</template>
