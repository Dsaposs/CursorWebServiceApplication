<script setup lang="ts">
import ConfirmModal from '~/components/ConfirmModal.vue';
import GameNpcManager from '~/components/games/GameNpcManager.vue';
import GameSessionNotesPanel from '~/components/games/GameSessionNotesPanel.vue';
import type { NpcFormPayload } from '~/components/games/GameNpcManager.vue';
import GameOverview from '~/components/games/GameOverview.vue';
import GamePlayersPanel from '~/components/games/GamePlayersPanel.vue';
import GameSidebar from '~/components/games/GameSidebar.vue';
import type { CharacterResponse, GameResponse, NpcResponse, RulesetResponse, SessionSummaryResponse } from '~/types/api';
import { parseRulesetDefinition } from '~/utils/rulesets';

type GameTab = 'overview' | 'players' | 'npcs' | 'notes';

const { api, email, token, loadSession, clearSession } = useApi();
const { success: toastSuccess, error: toastError, info: toastInfo } = useToast();

const games = ref<GameResponse[]>([]);
const rulesets = ref<RulesetResponse[]>([]);
const selectedGame = ref<GameResponse | null>(null);
const activeTab = ref<GameTab>('overview');
const showCreateForm = ref(false);

// Create game form
const createName = ref('');
const createDesc = ref('');
const createRulesetCode = ref('alien-rpg');

// NPC management
const editingNpcId = ref<string | null>(null);

const isLoading = ref(false);
const isSaving = ref(false);
const showDeleteGameConfirm = ref(false);
const npcPendingDelete = ref<NpcResponse | null>(null);

const hasGames = computed(() => games.value.length > 0);
const activeSession = computed(() => selectedGame.value?.sessions.find(s => s.isActive) ?? null);
const selectedCreateRuleset = computed(() => rulesets.value.find(ruleset => ruleset.code === createRulesetCode.value) ?? null);
const selectedCreateDefinition = computed(() => parseRulesetDefinition(selectedCreateRuleset.value));
const selectedGameRuleset = computed(() => rulesets.value.find(r => r.code === selectedGame.value?.rulesetCode) ?? null);
const selectedGameDefinition = computed(() => parseRulesetDefinition(selectedGameRuleset.value));

onMounted(async () => {
  loadSession();
  if (!token.value) { await navigateTo('/login'); return; }
  await loadData();
});

async function loadData() {
  isLoading.value = true;
  try {
    [rulesets.value, games.value] = await Promise.all([
      api<RulesetResponse[]>('/api/rulesets'),
      api<GameResponse[]>('/api/games'),
    ]);
    if (rulesets.value.length && !rulesets.value.some(r => r.code === createRulesetCode.value)) {
      createRulesetCode.value = rulesets.value[0].code;
    }
    if (selectedGame.value) {
      const refreshed = games.value.find(g => g.id === selectedGame.value!.id);
      selectedGame.value = refreshed ?? null;
    }
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isLoading.value = false;
  }
}

async function openGame(gameId: string) {
  showCreateForm.value = false;
  try {
    selectedGame.value = await api<GameResponse>(`/api/games/${gameId}`);
    activeTab.value = 'overview';
    resetNpcForm();
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  }
}

async function createGame() {
  isSaving.value = true;
  try {
    const created = await api<GameResponse>('/api/games', {
      method: 'POST',
      body: { name: createName.value, description: createDesc.value || undefined, rulesetCode: createRulesetCode.value },
    });
    toastSuccess(`Game "${created.name}" created!`);
    await loadData();
    showCreateForm.value = false;
    createName.value = '';
    createDesc.value = '';
    await openGame(created.id);
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

async function startSession() {
  if (!selectedGame.value) return;
  isSaving.value = true;
  try {
    const session = await api<SessionSummaryResponse>(`/api/games/${selectedGame.value.id}/sessions`, {
      method: 'POST', body: {},
    });
    toastSuccess('Session started! Redirecting to DM screen…');
    await navigateTo(`/sessions/${session.id}/dm`);
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

function requestDeleteGame() {
  if (!selectedGame.value) return;
  showDeleteGameConfirm.value = true;
}

async function deleteGame() {
  if (!selectedGame.value) return;
  isSaving.value = true;
  try {
    await api(`/api/games/${selectedGame.value.id}`, { method: 'DELETE' });
    toastInfo(`"${selectedGame.value.name}" deleted.`);
    selectedGame.value = null;
    showDeleteGameConfirm.value = false;
    await loadData();
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

function onCharacterInventorySaved(characterId: string, character: CharacterResponse) {
  if (!selectedGame.value) return;
  const index = selectedGame.value.characters.findIndex(ch => ch.id === characterId);
  if (index >= 0) {
    selectedGame.value.characters[index] = character;
  }
}

async function onNpcSubmit(payload: NpcFormPayload) {
  if (!selectedGame.value) return;
  isSaving.value = true;
  try {
    if (editingNpcId.value) {
      await api<NpcResponse>(`/api/games/${selectedGame.value.id}/npcs/${editingNpcId.value}`, { method: 'PUT', body: payload });
      toastSuccess('NPC updated.');
    } else {
      await api<NpcResponse>(`/api/games/${selectedGame.value.id}/npcs`, { method: 'POST', body: payload });
      toastSuccess(`${payload.name} added.`);
    }
    editingNpcId.value = null;
    await openGame(selectedGame.value.id);
    activeTab.value = 'npcs';
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

function editNpc(npc: NpcResponse) {
  editingNpcId.value = npc.id;
  activeTab.value = 'npcs';
}

function resetNpcForm() {
  editingNpcId.value = null;
}

function requestDeleteNpc(npc: NpcResponse) {
  npcPendingDelete.value = npc;
}

async function deleteNpc() {
  if (!selectedGame.value || !npcPendingDelete.value) return;
  const npc = npcPendingDelete.value;
  isSaving.value = true;
  try {
    await api(`/api/games/${selectedGame.value.id}/npcs/${npc.id}`, { method: 'DELETE' });
    toastInfo(`${npc.name} removed.`);
    if (editingNpcId.value === npc.id) resetNpcForm();
    npcPendingDelete.value = null;
    await openGame(selectedGame.value.id);
    activeTab.value = 'npcs';
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

async function copyToClipboard(path: string) {
  const url = import.meta.client ? `${window.location.origin}${path}` : path;
  if (import.meta.client && navigator.clipboard) {
    await navigator.clipboard.writeText(url);
    toastSuccess('Copied to clipboard!');
  }
}

function absoluteUrl(path: string) {
  if (!import.meta.client || path.startsWith('http')) return path;
  return `${window.location.origin}${path}`;
}

function signOut() {
  clearSession();
  void navigateTo('/login');
}
</script>

<template>
  <section class="app-shell">
    <!-- Topbar -->
    <header class="topbar">
      <div class="topbar-brand">
        <span class="topbar-wordmark">TTRPG TABLE</span>
        <div class="topbar-sub">{{ email }}</div>
      </div>
      <div class="topbar-actions">
        <NuxtLink class="btn ghost sm" to="/rulesets">Rulesets</NuxtLink>
        <button class="btn ghost sm" type="button" @click="signOut">Sign out</button>
      </div>
    </header>

    <main class="workspace">
      <GameSidebar
        :games="games"
        :selected-game-id="selectedGame?.id"
        :is-loading="isLoading"
        @create="showCreateForm = true; selectedGame = null"
        @open="openGame"
      />

      <!-- Create game form -->
      <section v-if="showCreateForm" class="editor">
        <div class="panel" style="max-width: 36rem;">
          <div class="panel-title">
            <h1 style="font-size: 1.25rem;">Create a Game</h1>
            <button v-if="hasGames" class="btn ghost sm" type="button" @click="showCreateForm = false">Cancel</button>
          </div>
          <p>Name your campaign, pick a ruleset, then share the invite link with players.</p>
          <hr class="divider" />
          <form @submit.prevent="createGame">
            <label>
              Game name
              <input v-model.trim="createName" maxlength="160" placeholder="The Crimson Station…" required />
            </label>
            <label>
              Ruleset
              <select v-model="createRulesetCode" required>
                <option v-for="r in rulesets" :key="r.code" :value="r.code">
                  {{ r.displayName }}{{ r.isPlaceholder ? ' (placeholder)' : '' }}
                </option>
              </select>
            </label>
            <div v-if="selectedCreateRuleset && selectedCreateDefinition" class="ruleset-action-card">
              <strong>{{ selectedCreateRuleset.displayName }}</strong>
              <span>{{ selectedCreateRuleset.description }}</span>
              <small>
                {{ selectedCreateDefinition.character.classes.length }} classes ·
                {{ selectedCreateDefinition.character.skills.length }} skills ·
                {{ selectedCreateDefinition.actions.length }} actions
              </small>
            </div>
            <label>
              Description <span class="muted text-xs">(optional)</span>
              <textarea v-model="createDesc" maxlength="1000" placeholder="Campaign description…" style="min-height: 4rem;" />
            </label>
            <div class="btn-row">
              <button class="btn" type="submit" :disabled="isSaving">
                {{ isSaving ? 'Creating…' : 'Create Game' }}
              </button>
            </div>
          </form>
        </div>
      </section>

      <!-- Game dashboard -->
      <section v-else-if="selectedGame" class="editor">
        <div class="panel mb-2">
          <div class="flex items-center justify-between gap-3" style="flex-wrap: wrap;">
            <div>
              <h1 style="margin-bottom: 0.25rem;">{{ selectedGame.name }}</h1>
              <p style="margin: 0; font-size: 0.85rem;">
                {{ selectedGame.rulesetName }}
                <span v-if="selectedGame.description"> · {{ selectedGame.description }}</span>
              </p>
            </div>
            <div class="btn-row">
              <button v-if="!activeSession" class="btn" type="button" :disabled="isSaving" @click="startSession">
                <span aria-hidden="true">▶</span> Start Session
              </button>
              <NuxtLink v-else class="btn success" :to="`/sessions/${activeSession.id}/dm`">
                → Active Session
              </NuxtLink>
              <button class="btn danger" type="button" @click="requestDeleteGame">Delete</button>
            </div>
          </div>
        </div>

        <!-- Stats strip -->
        <div class="stats-strip">
          <div class="stats-strip-item">
            <span class="stats-strip-value">{{ selectedGame.characters.length }}</span>
            <span class="stats-strip-label">Players</span>
          </div>
          <div class="stats-strip-item">
            <span class="stats-strip-value">{{ selectedGame.sessions.length }}</span>
            <span class="stats-strip-label">Sessions</span>
          </div>
          <div class="stats-strip-item">
            <span class="stats-strip-value">{{ selectedGame.npcsAndMonsters.length }}</span>
            <span class="stats-strip-label">NPCs</span>
          </div>
        </div>

        <!-- Tabs -->
        <div class="pill-tabs">
          <button class="pill-tab" :class="{ active: activeTab === 'overview' }" type="button" @click="activeTab = 'overview'">Overview</button>
          <button class="pill-tab" :class="{ active: activeTab === 'players' }" type="button" @click="activeTab = 'players'">
            Players
            <span v-if="selectedGame.characters.length" class="badge active" style="font-size: 0.65rem; padding: 0.1rem 0.4rem;">{{ selectedGame.characters.length }}</span>
          </button>
          <button class="pill-tab" :class="{ active: activeTab === 'npcs' }" type="button" @click="activeTab = 'npcs'">
            NPCs / Monsters
            <span v-if="selectedGame.npcsAndMonsters.length" class="badge active" style="font-size: 0.65rem; padding: 0.1rem 0.4rem;">{{ selectedGame.npcsAndMonsters.length }}</span>
          </button>
          <button class="pill-tab" :class="{ active: activeTab === 'notes' }" type="button" @click="activeTab = 'notes'">
            Session Notes
          </button>
        </div>

        <!-- Overview tab -->
        <GameOverview
          v-if="activeTab === 'overview'"
          :game="selectedGame"
          :is-saving="isSaving"
          :invite-url="absoluteUrl(selectedGame.inviteUrl)"
          @start-session="startSession"
          @copy-invite="copyToClipboard(selectedGame.inviteUrl)"
        />

        <!-- Players tab -->
        <GamePlayersPanel
          v-if="activeTab === 'players' && selectedGame"
          :game-id="selectedGame.id"
          :characters="selectedGame.characters"
          :ruleset-definition="selectedGameDefinition"
          :is-saving="isSaving"
          @inventory-saved="onCharacterInventorySaved"
        />

        <!-- NPCs tab -->
        <GameNpcManager
          v-if="activeTab === 'npcs'"
          :npcs="selectedGame.npcsAndMonsters"
          :is-saving="isSaving"
          :editing-npc-id="editingNpcId"
          :definition="selectedGameDefinition"
          @submit="onNpcSubmit"
          @edit="editNpc"
          @delete="requestDeleteNpc"
          @reset="resetNpcForm"
        />

        <GameSessionNotesPanel
          v-if="activeTab === 'notes'"
          :game-id="selectedGame.id"
        />
      </section>

      <!-- Empty / no game selected -->
      <section v-else class="editor">
        <div class="empty-state" style="padding: 4rem 2rem;">
          <div class="empty-state-icon" style="font-size: 3rem;" aria-hidden="true">🎲</div>
          <h2>{{ hasGames ? 'Select a game' : 'Welcome, Dungeon Master' }}</h2>
          <p>{{ hasGames ? 'Choose a game from the sidebar to manage it.' : 'Create your first game to get started.' }}</p>
          <button v-if="!hasGames" class="btn" style="margin-top: 1rem;" type="button" @click="showCreateForm = true">
            Create My First Game
          </button>
        </div>
      </section>
    </main>
  </section>

  <ConfirmModal
    v-model:open="showDeleteGameConfirm"
    title="Delete game?"
    :message="selectedGame ? `Delete ${selectedGame.name} and all its sessions, characters, NPCs, and actions? This cannot be undone.` : ''"
    confirm-label="Delete Game"
    :is-busy="isSaving"
    @confirm="deleteGame"
  />

  <ConfirmModal
    :open="Boolean(npcPendingDelete)"
    title="Delete NPC?"
    :message="npcPendingDelete ? `Delete ${npcPendingDelete.name}? This cannot be undone.` : ''"
    confirm-label="Delete NPC"
    :is-busy="isSaving"
    @update:open="value => { if (!value) npcPendingDelete = null; }"
    @confirm="deleteNpc"
  />
</template>
