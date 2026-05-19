<script setup lang="ts">
import type { RulesetResponse, SessionStateResponse } from '~/types/api';
import { parseRulesetDefinition } from '~/utils/rulesets';

const route = useRoute();
const { api, token, loadSession } = useApi();

const state = ref<SessionStateResponse | null>(null);
const ruleset = ref<RulesetResponse | null>(null);
const rulesetDefinition = computed(() => parseRulesetDefinition(ruleset.value));
const isLoading = ref(true);
const loadError = ref('');

onMounted(async () => {
  loadSession();
  if (!token.value) { await navigateTo('/login'); return; }

  try {
    state.value = await api<SessionStateResponse>(`/api/sessions/${route.params.id}/dm`);
    if (state.value) {
      ruleset.value = await api<RulesetResponse>(`/api/rulesets/${state.value.game.rulesetCode}`);
    }
  } catch (err) {
    loadError.value = err instanceof Error ? err.message : String(err);
  } finally {
    isLoading.value = false;
  }
});

// Duration helpers
const duration = computed(() => {
  if (!state.value?.startedAt) return null;
  const start = new Date(state.value.startedAt).getTime();
  const end = state.value.endedAt ? new Date(state.value.endedAt).getTime() : Date.now();
  const ms = end - start;
  const h = Math.floor(ms / 3_600_000);
  const m = Math.floor((ms % 3_600_000) / 60_000);
  if (h > 0) return `${h}h ${m}m`;
  return `${m}m`;
});

const startedAt = computed(() => {
  if (!state.value?.startedAt) return '';
  return new Date(state.value.startedAt).toLocaleString(undefined, {
    dateStyle: 'medium', timeStyle: 'short',
  });
});

const endedAt = computed(() => {
  if (!state.value?.endedAt) return 'Still active';
  return new Date(state.value.endedAt).toLocaleString(undefined, {
    dateStyle: 'medium', timeStyle: 'short',
  });
});

// Derive which character names actually submitted actions this session
const activeActorNames = computed(() => {
  if (!state.value) return new Set<string>();
  return new Set(state.value.actions.map(a => a.actorName));
});

const publishedActions = computed(() =>
  state.value?.actions.filter(a => a.status === 'Published') ?? [],
);
const pendingActions = computed(() =>
  state.value?.actions.filter(a => a.status === 'Pending') ?? [],
);
const allActions = computed(() => [...(state.value?.actions ?? [])].sort((a, b) => a.sequence - b.sequence));
const summaryCombatEncounters = computed(() => state.value?.combatEncounters ?? []);
const expandedSummaryActions = ref<Set<string>>(new Set());

const {
  expandedGroups: expandedSummaryGroups,
  toggleGroup: toggleSummaryGroup,
} = useActionLogGroupExpansion(allActions, summaryCombatEncounters, { expandAllOnFirstLoad: true });

function toggleSummaryAction(id: string) {
  if (expandedSummaryActions.value.has(id)) expandedSummaryActions.value.delete(id);
  else expandedSummaryActions.value.add(id);
}

// Action actor unique names (for participation derived from actor name when no characterId)
const participantNames = computed(() => {
  if (!state.value) return [];
  const names = new Set(state.value.actions.map(a => a.actorName));
  return [...names];
});

function formatTime(iso: string) {
  return new Date(iso).toLocaleTimeString(undefined, { timeStyle: 'short' });
}
</script>

<template>
  <section class="app-shell">
    <!-- Topbar -->
    <header class="topbar">
      <div class="topbar-brand">
        <span aria-hidden="true">📜</span>
        <div>
          <strong>Session Summary</strong>
          <div v-if="state" class="topbar-sub">{{ state.game.name }}</div>
        </div>
      </div>
      <div class="topbar-actions">
        <span v-if="state && !state.isActive" class="badge ended">Ended</span>
        <span v-else-if="state" class="badge active">Active</span>
        <NuxtLink class="btn ghost sm" to="/games">← Games</NuxtLink>
        <NuxtLink v-if="state?.isActive" class="btn sm" :to="`/sessions/${route.params.id}/dm`">Open DM Screen</NuxtLink>
      </div>
    </header>

    <!-- Loading -->
    <div v-if="isLoading" class="stack">
      <SkeletonBlock :lines="4" />
      <div class="grid-2">
        <SkeletonBlock :lines="8" />
        <SkeletonBlock :lines="8" />
      </div>
    </div>

    <!-- Error -->
    <div v-else-if="loadError" class="stack">
      <div class="alert error">{{ loadError }}</div>
      <NuxtLink class="btn ghost sm" to="/games">← Back to games</NuxtLink>
    </div>

    <main v-else-if="state" class="stack">

      <!-- Header stats -->
      <div class="panel">
        <div class="flex justify-between items-center" style="flex-wrap: wrap; gap: 1rem; margin-bottom: 1.25rem;">
          <div>
            <h1 style="margin-bottom: 0.2rem;">{{ state.game.name }}</h1>
            <p style="margin: 0; font-size: 0.85rem;">{{ state.game.rulesetName }}</p>
          </div>
          <span class="badge" :class="state.isActive ? 'active' : 'ended'" style="font-size: 0.85rem; padding: 0.35rem 0.85rem;">
            {{ state.isActive ? 'Active' : 'Ended' }}
          </span>
        </div>

        <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr)); gap: 1rem;">
          <div class="stat-cell">
            <dt>Started</dt>
            <dd style="font-size: 0.85rem; font-weight: 600;">{{ startedAt }}</dd>
          </div>
          <div class="stat-cell">
            <dt>Ended</dt>
            <dd style="font-size: 0.85rem; font-weight: 600;">{{ endedAt }}</dd>
          </div>
          <div class="stat-cell">
            <dt>Duration</dt>
            <dd>{{ duration ?? '—' }}</dd>
          </div>
          <div class="stat-cell">
            <dt>Actions</dt>
            <dd>{{ allActions.length }}</dd>
          </div>
          <div class="stat-cell">
            <dt>Resolved</dt>
            <dd>{{ publishedActions.length }}</dd>
          </div>
          <div class="stat-cell">
            <dt>Participants</dt>
            <dd>{{ participantNames.length }}</dd>
          </div>
        </div>
      </div>

      <div class="grid-2">
        <!-- Left: Participants -->
        <div style="display: grid; gap: 1rem; align-content: start;">
          <div class="panel">
            <h2>Characters</h2>

            <div v-if="state.game.characters.length === 0" class="empty-state" style="padding: 1rem 0;">
              <p class="text-sm">No characters joined this game yet.</p>
            </div>

            <div style="display: grid; gap: 0.75rem;">
              <div v-for="ch in state.game.characters" :key="ch.id" class="action-card">
                <div class="flex justify-between items-center" style="margin-bottom: 0.5rem;">
                  <div>
                    <div class="flex items-center gap-2">
                      <span style="font-weight: 700; color: var(--ink-bright);">{{ ch.name }}</span>
                      <span v-if="activeActorNames.has(ch.name)" class="badge active" style="font-size: 0.65rem; padding: 0.1rem 0.4rem;">Acted</span>
                    </div>
                    <div class="text-xs muted">{{ ch.playerName || 'No player name' }}</div>
                  </div>
                  <span class="text-xs muted" style="font-family: monospace;">AC {{ ch.armor }}</span>
                </div>
                <CharacterSheet :character="ch" />
              </div>
            </div>
          </div>

          <!-- NPCs involved -->
          <div v-if="state.game.npcsAndMonsters.length" class="panel">
            <h2>NPCs / Monsters</h2>
            <div style="display: grid; gap: 0.6rem;">
              <div v-for="npc in state.game.npcsAndMonsters" :key="npc.id" class="action-card">
                <div class="flex justify-between items-center" style="margin-bottom: 0.4rem;">
                  <div>
                    <div style="font-weight: 700; color: var(--ink-bright);">{{ npc.name }}</div>
                    <div class="text-xs muted">{{ npc.kind }}</div>
                  </div>
                </div>
                <HealthBar :current="npc.health" :max="npc.maxHealth" />
              </div>
            </div>
          </div>

          <!-- Initiative order (if combat happened) -->
          <div v-if="state.initiative.length" class="panel">
            <h2>Initiative Order</h2>
            <ul class="initiative-list">
              <li
                v-for="(entry, idx) in state.initiative"
                :key="entry.id"
                class="initiative-item"
              >
                <span class="initiative-order">{{ idx + 1 }}</span>
                <span class="initiative-name">{{ entry.combatantName }}</span>
                <span class="initiative-type">{{ entry.combatantType }}</span>
              </li>
            </ul>
          </div>
        </div>

        <!-- Right: Action log -->
        <div class="panel">
          <div class="panel-title">
            <h2>Action Log</h2>
            <div class="flex gap-1">
              <span v-if="publishedActions.length" class="badge published">{{ publishedActions.length }} resolved</span>
              <span v-if="pendingActions.length" class="badge pending">{{ pendingActions.length }} unresolved</span>
            </div>
          </div>

          <div v-if="allActions.length === 0" class="empty-state" style="padding: 1.5rem 0;">
            <div class="empty-state-icon" aria-hidden="true">📋</div>
            <p class="text-sm">No actions were submitted this session.</p>
          </div>

          <ActionLogGrouped
            :actions="allActions"
            :combat-encounters="state.combatEncounters ?? []"
            :expanded-actions="expandedSummaryActions"
            :expanded-groups="expandedSummaryGroups"
            :game="state.game"
            :ruleset-definition="rulesetDefinition"
            :show-sequence="true"
            action-prefix="used"
            @toggle-action="toggleSummaryAction"
            @toggle-group="toggleSummaryGroup"
          >
            <template #action-meta="{ action }">
              <span class="text-xs muted">{{ formatTime(action.submittedAt) }}</span>
            </template>
          </ActionLogGrouped>
        </div>
      </div>

    </main>
  </section>
</template>
