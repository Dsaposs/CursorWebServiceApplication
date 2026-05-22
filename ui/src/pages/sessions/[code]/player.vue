<script setup lang="ts">
definePageMeta({ middleware: 'player-auth' });
import type { InitiativeEntryResponse, RulesetResponse, SessionLiveResponse, SessionStateResponse, SessionVersionResponse } from '~/types/api';
import { parseCharacterStats } from '~/utils/dice';
import { parseRulesetDefinition } from '~/utils/rulesets';
import { useRulesetTheme } from '~/composables/useRulesetTheme';
import { useThemePreference } from '~/composables/useThemePreference';
import ActionForm from '~/components/ActionForm.vue';
import PlayerRollPromptOverlay from '~/components/PlayerRollPromptOverlay.vue';
import PlayerRollChainOverlay from '~/components/PlayerRollChainOverlay.vue';
import PlayerCombatTurnOverlay from '~/components/PlayerCombatTurnOverlay.vue';
import { isPlayerActionLogEntry, isUnresolvedActionStatus } from '~/utils/actionLog';
import { actionHasRollChain } from '~/utils/actionRolls';
import { findActivePlayerRollPrompt, isSameGuid } from '~/utils/rollPrompt';
import type { RollPromptResponse } from '~/types/api';

const route = useRoute();
const { api } = useApi();
const { getSessionPlayerToken, getGamePlayerToken, setSessionPlayerToken } = usePlayerTokens();
const { error: toastError, success: toastSuccess } = useToast();

const playerToken = ref<string | null>(null);
const ruleset = ref<RulesetResponse | null>(null);
const showCharacterSheet = ref(false);
const actionFormRef = ref<{ reset: () => void } | null>(null);
const isSubmitting = ref(false);
const isSubmittingRollPrompt = ref(false);
const isSkippingTurn = ref(false);
const showActionForm = ref(false);

function sessionQuery(sinceSequence: number) {
  return sinceSequence > 0 ? `?sinceSequence=${sinceSequence}` : '';
}

async function fetchFullState({ sinceSequence }: { sinceSequence: number }) {
  if (!playerToken.value) return null;
  const nextState = await api<SessionStateResponse>(`/api/session-join/${route.params.code}/state${sessionQuery(sinceSequence)}`, {
    playerToken: playerToken.value,
  });
  if (!ruleset.value) {
    ruleset.value = await api<RulesetResponse>(`/api/rulesets/${nextState.game.rulesetCode}`);
  }
  return nextState;
}

async function fetchLiveState({ sinceSequence }: { sinceSequence: number }) {
  if (!playerToken.value) return null;
  return api<SessionLiveResponse>(`/api/session-join/${route.params.code}/live${sessionQuery(sinceSequence)}`, {
    playerToken: playerToken.value,
  });
}

async function checkVersion(knownVersion: number) {
  if (!playerToken.value) return false;
  const snapshot = await api<SessionVersionResponse>(`/api/session-join/${route.params.code}/version`, {
    playerToken: playerToken.value,
  });
  return snapshot.version === knownVersion;
}

const { state, pollingError, fatalError, connectionStatus, refresh, refreshInBackground, hubConnected, start } = useLiveSession({
  fetchFullState,
  fetchLiveState,
  checkVersion,
  getJoinCode: () => route.params.code as string,
  getPlayerToken: () => playerToken.value,
});
const { enabled: rulesetThemeEnabled, toggle: toggleRulesetTheme } = useThemePreference();
const _rulesetThemeStyle = useRulesetTheme(ruleset);
const rulesetThemeStyle = computed(() => rulesetThemeEnabled.value ? _rulesetThemeStyle.value : {});

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
const pendingPlayerActions = computed(() =>
  state.value?.actions.filter(action =>
    action.status === 'Pending'
    && action.actorCharacterId === state.value?.character?.id,
  ) ?? [],
);

/** Unresolved combat actions for this player in the active encounter. */
const unresolvedPlayerCombatActions = computed(() => {
  const characterId = state.value?.character?.id;
  const encounterId = activeEncounter.value?.id;
  if (!characterId || !encounterId) return [];

  return (state.value?.actions ?? []).filter(action =>
    isUnresolvedActionStatus(action.status)
    && action.actorCharacterId === characterId
    && action.combatEncounterId === encounterId,
  );
});

const expandedPendingPlayerActions = ref<Set<string>>(new Set());

const activeRollPrompt = computed(() =>
  findActivePlayerRollPrompt(
    state.value?.rollPrompts ?? [],
    state.value?.character?.id,
    { requireMyTurn: isCombat.value, isMyTurn: isMyTurn.value },
  ),
);

const activeRollChainAction = computed(() => {
  const prompt = activeRollPrompt.value;
  if (!prompt?.actionRequestId) return null;

  const linkedAction = (state.value?.actions ?? []).find(action =>
    isSameGuid(action.id, prompt.actionRequestId),
  );
  if (linkedAction && actionHasRollChain(rulesetDefinition.value, linkedAction.actionKey)) {
    return linkedAction;
  }

  return unresolvedPlayerCombatActions.value.find(action =>
    actionHasRollChain(rulesetDefinition.value, action.actionKey),
  ) ?? null;
});

const showRollChainOverlay = computed(() =>
  Boolean(activeRollChainAction.value && activeRollPrompt.value),
);

const awaitingDmRollRequest = computed(() =>
  unresolvedPlayerCombatActions.value.some(action =>
    action.status === 'Pending'
    || action.status === 'DmReviewing',
  ),
);

const awaitingPlayerRollForAction = computed(() =>
  unresolvedPlayerCombatActions.value.some(action =>
    action.status === 'AwaitingRoll'
    || action.status === 'AwaitingFollowUpRoll',
  ),
);

const activeEncounter = computed(() =>
  state.value?.combatEncounters?.find(e => e.isActive) ?? null,
);

const hasUnresolvedPlayerCombatAction = computed(() =>
  unresolvedPlayerCombatActions.value.length > 0,
);

/** Only show the action form overlay once the DM has explicitly prompted this character to act. */
const showCombatTurnFocus = computed(() =>
  isCombat.value
  && isMyTurn.value
  && activeEncounter.value?.promptedTurnCharacterId === state.value?.character?.id
  && !activeRollPrompt.value
  && !hasUnresolvedPlayerCombatAction.value,
);

const isPlayerFocusActive = computed(() =>
  showCombatTurnFocus.value || Boolean(isCombat.value && activeRollPrompt.value),
);

const combatTurnWaiting = computed(() =>
  showCombatTurnFocus.value
  && !activeRollPrompt.value
  && awaitingDmRollRequest.value
  && !awaitingPlayerRollForAction.value,
);

const combatTurnFormKey = computed(() => {
  const encounter = activeEncounter.value;
  const turn = currentTurn.value;
  const characterId = state.value?.character?.id;
  if (!encounter || !turn || !characterId) return 'combat-form';

  return [
    encounter.id,
    encounter.round,
    turn.combatantId,
    encounter.promptedTurnCharacterId ?? 'none',
  ].join(':');
});

function resetCombatTurnForm() {
  actionFormRef.value?.reset();
}

watch(isMyTurn, (mine, wasMine) => {
  if (mine && !wasMine) {
    resetCombatTurnForm();
  }
});

watch(showCombatTurnFocus, (open, wasOpen) => {
  if (open && !wasOpen) {
    nextTick(() => resetCombatTurnForm());
  }
});

watch(combatTurnWaiting, (waiting, wasWaiting) => {
  if (wasWaiting && !waiting) {
    nextTick(() => resetCombatTurnForm());
  }
});

watch(
  () => state.value?.state,
  (mode) => {
    if (mode !== 'Combat') {
      showActionForm.value = false;
    }
  },
);

const publishedFeedActions = computed(() =>
  state.value?.actions.filter(isPlayerActionLogEntry) ?? [],
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
    refreshInBackground();
    toastSuccess('Action withdrawn.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSubmitting.value = false;
  }
}

const playerStats = computed(() => parseCharacterStats(state.value?.character?.rulesetDataJson));

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

async function submitRollPrompt(payload: { rollSummary: string; rollResultJson?: string; pushed?: boolean }) {
  if (!playerToken.value || !activeRollPrompt.value) return;

  const submittedPromptId = activeRollPrompt.value.id;
  isSubmittingRollPrompt.value = true;
  try {
    const response = await api<RollPromptResponse>(`/api/roll-prompts/${submittedPromptId}/submit`, {
      method: 'PUT',
      playerToken: playerToken.value,
      body: payload,
    });

    applyRollPromptTransition(response, submittedPromptId);
    refreshInBackground();

    if (response.nextPendingPrompt) {
      toastSuccess('Roll sent — next roll is ready.');
    } else {
      toastSuccess('Roll sent to the DM.');
    }
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSubmittingRollPrompt.value = false;
  }
}

function applyRollPromptTransition(response: RollPromptResponse, submittedPromptId: string) {
  if (!state.value) return;

  const remaining = (state.value.rollPrompts ?? []).filter(
    prompt => prompt.id !== submittedPromptId && prompt.status === 'Pending',
  );

  if (response.nextPendingPrompt) {
    state.value.rollPrompts = [...remaining, response.nextPendingPrompt];
    return;
  }

  state.value.rollPrompts = remaining;
}

interface ActionFormPayload {
  actionKey?: string;
  actionText: string;
  targetCharacterId?: string;
  targetNpcId?: string;
  targetName?: string;
}

async function submitActionFromForm(payload: ActionFormPayload) {
  if (!playerToken.value || !state.value) return;

  isSubmitting.value = true;
  try {
    await api(`/api/sessions/${state.value.joinCode}/actions`, {
      method: 'POST',
      playerToken: playerToken.value,
      body: {
        actionKey: payload.actionKey,
        actionText: payload.actionText,
        targetCharacterId: payload.targetCharacterId,
        targetNpcId: payload.targetNpcId,
        targetName: payload.targetName,
      },
    });
    actionFormRef.value?.reset();
    refreshInBackground();
    if (!isCombat.value) {
      showActionForm.value = false;
    }
    const usesRollChain = Boolean(
      payload.actionKey
      && actionHasRollChain(rulesetDefinition.value, payload.actionKey),
    );
    toastSuccess(
      usesRollChain && isCombat.value
        ? 'Action sent — complete your attack and damage rolls.'
        : 'Action sent to DM! The DM will call for your roll.',
    );
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSubmitting.value = false;
  }
}

async function skipTurn() {
  if (!playerToken.value || !state.value || !isMyTurn.value) return;
  isSkippingTurn.value = true;
  try {
    await api(`/api/session-join/${route.params.code}/skip-turn`, {
      method: 'POST',
      playerToken: playerToken.value,
    });
    refreshInBackground();
    toastSuccess('Turn skipped.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSkippingTurn.value = false;
  }
}
</script>

<template>
  <PlayerRollChainOverlay
    v-if="state?.character && showRollChainOverlay && activeRollChainAction && activeRollPrompt"
    :key="activeRollPrompt.id"
    :action="activeRollChainAction"
    :prompt="activeRollPrompt"
    :character="state.character"
    :roll-prompts="state.rollPrompts ?? []"
    :ruleset-definition="rulesetDefinition"
    :is-submitting="isSubmittingRollPrompt"
    @submit="submitRollPrompt"
  />

  <PlayerRollPromptOverlay
    v-else-if="state?.character && activeRollPrompt"
    :key="activeRollPrompt.id"
    :prompt="activeRollPrompt"
    :character="state.character"
    :ruleset-definition="rulesetDefinition"
    :is-submitting="isSubmittingRollPrompt"
    @submit="submitRollPrompt"
  />

  <PlayerCombatTurnOverlay
    v-if="state?.character"
    :character-name="state.character.name"
    :is-open="showCombatTurnFocus && !activeRollPrompt"
    :waiting-for-dm="combatTurnWaiting"
  >
    <template v-if="awaitingPlayerRollForAction && !activeRollPrompt">
      <p class="text-sm" style="margin: 0;">
        The DM sent your next roll — it should appear momentarily. If nothing shows up, wait a few seconds or refresh the page.
      </p>
    </template>
    <template v-else-if="!combatTurnWaiting">
      <div style="display: flex; justify-content: flex-end; margin-bottom: 0.75rem;">
        <button
          class="btn ghost sm"
          type="button"
          :disabled="isSkippingTurn || isSubmitting"
          @click="skipTurn"
        >
          {{ isSkippingTurn ? 'Skipping…' : 'Skip Turn' }}
        </button>
      </div>
      <ActionForm
        :key="combatTurnFormKey"
        ref="actionFormRef"
        :ruleset-definition="rulesetDefinition"
        :class-key="state.character?.classKey"
        :inventory-json="state.character?.inventoryJson"
        :characters="state.game.characters"
        :npcs="state.game.npcsAndMonsters"
        :is-submitting="isSubmitting"
        :require-target-for-combat="true"
        @submit="submitActionFromForm"
        @cancel="() => {}"
      />
    </template>
  </PlayerCombatTurnOverlay>

  <section class="app-shell" :style="rulesetThemeStyle">
    <!-- Topbar -->
    <header class="topbar" :class="{ 'combat-mode': isCombat }">
      <div class="topbar-brand">
        <span class="topbar-wordmark">TTRPG TABLE</span>
        <div>
          <strong>{{ state?.character?.name || 'Player' }}</strong>
          <div class="topbar-sub">{{ state?.game.name }}</div>
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
        <span class="badge" :class="isCombat ? 'combat' : 'exploration'">{{ isCombat ? 'In combat' : 'Session' }}</span>
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
    </header>

    <div v-if="!state" class="stack session-screen-main">
      <SkeletonBlock :lines="4" />
      <SkeletonBlock :lines="6" />
      <SkeletonBlock :lines="5" />
    </div>

    <main
      v-else
      class="stack session-screen-main"
      :class="{ 'session-screen-main--dimmed': isPlayerFocusActive }"
    >
      <div v-if="isCombat" class="combat-banner" role="status">
        <strong>Combat is active</strong>
        <span v-if="currentTurn"> — {{ currentTurn.combatantName }}'s turn</span>
      </div>

      <SessionNotesPanel
        mode="player"
        :join-code="String(route.params.code)"
        :player-token="playerToken"
      />

      <div class="session-dashboard-grid">
        <div class="session-primary-column">
      <div v-if="isCombat && !isMyTurn && currentTurn" class="panel" role="status">
        <p class="text-sm" style="margin: 0;">
          Waiting for <strong>{{ currentTurn.combatantName }}</strong>'s turn. You'll be prompted when it's yours.
        </p>
      </div>

      <!-- Submit action — exploration only -->
      <div v-if="!isCombat" class="panel">
        <div class="panel-title">
          <div>
            <h2>Actions</h2>
            <p v-if="pendingPlayerActions.length" class="text-sm">
              {{ pendingPlayerActions.length }} action{{ pendingPlayerActions.length === 1 ? '' : 's' }} pending DM review.
            </p>
            <p v-else class="text-sm">Submit your intent first — the DM will prompt you to roll when ready.</p>
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

        <ActionForm
          v-if="showActionForm"
          ref="actionFormRef"
          :ruleset-definition="rulesetDefinition"
          :class-key="state.character?.classKey"
          :inventory-json="state.character?.inventoryJson"
          :characters="state.game.characters"
          :npcs="state.game.npcsAndMonsters"
          :require-target-for-combat="false"
          :is-submitting="isSubmitting"
          @submit="submitActionFromForm"
          @cancel="showActionForm = false"
        />
      </div>

      <!-- Pending actions (withdraw) — exploration only; combat shows these in the turn overlay -->
      <div v-if="!isCombat && pendingPlayerActions.length" class="panel">
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
            <span v-if="entry.initiativeScore" class="initiative-score text-sm">{{ entry.initiativeScore }}</span>
            <span v-if="entry.combatantId === state.character?.id" class="badge active" style="font-size: 0.65rem; padding: 0.1rem 0.4rem;">You</span>
          </li>
        </ul>
      </div>

      <!-- Character sheet — collapsed by default; expand during session for reference -->
      <div class="panel">
        <div class="flex justify-between items-center" style="margin-bottom: 0.75rem; flex-wrap: wrap; gap: 0.75rem;">
          <div>
            <h2 style="margin: 0;">{{ state.character?.name }}</h2>
            <p style="margin: 0; font-size: 0.82rem;">{{ state.game.rulesetName }}</p>
          </div>
          <div class="btn-row">
            <div v-if="isCombat && currentTurn && !isMyTurn">
              <span class="badge" style="background: var(--panel-alt); color: var(--muted-light); border: 1px solid var(--border);">
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

        </div>

        <aside class="session-feed-column">
          <div class="panel action-log-panel">
            <div class="panel-title">
              <div>
                <h2>Action Feed</h2>
                <p v-if="publishedFeedActions.length" class="text-sm">
                  Grouped by combat encounter. Expand actions to see full details.
                  Newest actions appear at the top.
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
