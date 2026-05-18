<script setup lang="ts">
import type { GameResponse, NpcResponse, RulesetResponse, SessionSummaryResponse } from '~/types/api';

type GameTab = 'overview' | 'players' | 'npcs';

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

// NPC form
const npcName = ref('');
const npcKind = ref('Monster');
const npcMaxHealth = ref(10);
const npcHealth = ref(10);
const npcArmor = ref(0);
const npcStatBlockJson = ref('{\n  "attributes": {},\n  "skills": {},\n  "inventory": []\n}');
const editingNpcId = ref<string | null>(null);

const isLoading = ref(false);
const isSaving = ref(false);

const hasGames = computed(() => games.value.length > 0);
const activeSession = computed(() => selectedGame.value?.sessions.find(s => s.isActive) ?? null);

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

async function deleteGame() {
  if (!selectedGame.value) return;
  if (!confirm(`Delete "${selectedGame.value.name}" and all its sessions, characters, NPCs, and actions? This cannot be undone.`)) return;

  try {
    await api(`/api/games/${selectedGame.value.id}`, { method: 'DELETE' });
    toastInfo(`"${selectedGame.value.name}" deleted.`);
    selectedGame.value = null;
    await loadData();
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  }
}

async function createNpc() {
  if (!selectedGame.value) return;
  isSaving.value = true;
  try {
    await api<NpcResponse>(`/api/games/${selectedGame.value.id}/npcs`, { method: 'POST', body: npcPayload() });
    toastSuccess(`${npcName.value} added.`);
    resetNpcForm();
    await openGame(selectedGame.value.id);
    activeTab.value = 'npcs';
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

async function updateNpc() {
  if (!selectedGame.value || !editingNpcId.value) return;
  isSaving.value = true;
  try {
    await api<NpcResponse>(`/api/games/${selectedGame.value.id}/npcs/${editingNpcId.value}`, { method: 'PUT', body: npcPayload() });
    toastSuccess('NPC updated.');
    resetNpcForm();
    await openGame(selectedGame.value.id);
    activeTab.value = 'npcs';
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSaving.value = false;
  }
}

async function deleteNpc(npc: NpcResponse) {
  if (!selectedGame.value || !confirm(`Delete "${npc.name}"?`)) return;
  try {
    await api(`/api/games/${selectedGame.value.id}/npcs/${npc.id}`, { method: 'DELETE' });
    toastInfo(`${npc.name} removed.`);
    if (editingNpcId.value === npc.id) resetNpcForm();
    await openGame(selectedGame.value.id);
    activeTab.value = 'npcs';
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  }
}

function npcPayload() {
  return { name: npcName.value, kind: npcKind.value, maxHealth: npcMaxHealth.value, health: npcHealth.value, armor: npcArmor.value, statBlockJson: npcStatBlockJson.value };
}

function editNpc(npc: NpcResponse) {
  editingNpcId.value = npc.id;
  npcName.value = npc.name;
  npcKind.value = npc.kind;
  npcMaxHealth.value = npc.maxHealth;
  npcHealth.value = npc.health;
  npcArmor.value = npc.armor;
  npcStatBlockJson.value = npc.statBlockJson;
  activeTab.value = 'npcs';
}

function resetNpcForm() {
  editingNpcId.value = null;
  npcName.value = '';
  npcKind.value = 'Monster';
  npcMaxHealth.value = 10;
  npcHealth.value = 10;
  npcArmor.value = 0;
  npcStatBlockJson.value = '{\n  "attributes": {},\n  "skills": {},\n  "inventory": []\n}';
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

function sessionBadge(session: SessionSummaryResponse) {
  if (session.isActive) return session.state === 'Combat' ? 'combat' : 'exploration';
  return 'ended';
}

function sessionBadgeText(session: SessionSummaryResponse) {
  if (!session.isActive) return 'Ended';
  return session.state;
}

function sessionRoute(session: SessionSummaryResponse) {
  return session.isActive ? `/sessions/${session.id}/dm` : `/sessions/${session.id}/summary`;
}
</script>

<template>
  <section class="app-shell">
    <!-- Topbar -->
    <header class="topbar">
      <div class="topbar-brand">
        <span style="font-size: 1.1rem;">⚔️</span>
        <div>
          <strong>TTRPG Table</strong>
          <div class="topbar-sub">{{ email }}</div>
        </div>
      </div>
      <div class="topbar-actions">
        <button class="btn ghost sm" type="button" @click="signOut">Sign out</button>
      </div>
    </header>

    <main class="workspace">
      <!-- Sidebar -->
      <aside class="sidebar">
        <div class="sidebar-header">
          <h2>My Games</h2>
          <button class="btn sm" type="button" @click="showCreateForm = true; selectedGame = null">
            + New
          </button>
        </div>

        <ul class="game-list">
          <li v-if="!hasGames && !isLoading">
            <div class="empty-state" style="padding: 1.5rem 0.5rem;">
              <div class="empty-state-icon">🎲</div>
              <p class="text-xs">No games yet. Create your first!</p>
            </div>
          </li>
          <li v-for="game in games" :key="game.id">
            <button
              class="game-list-item"
              :class="{ active: selectedGame?.id === game.id }"
              type="button"
              @click="openGame(game.id)"
            >
              <strong>{{ game.name }}</strong>
              <span>{{ game.rulesetName }}</span>
            </button>
          </li>
        </ul>
      </aside>

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
        <!-- Game header -->
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
                ▶ Start Session
              </button>
              <NuxtLink v-else class="btn success" :to="`/sessions/${activeSession?.id}/dm`">
                → Active Session
              </NuxtLink>
              <button class="btn danger" type="button" @click="deleteGame">Delete</button>
            </div>
          </div>
        </div>

        <!-- Tabs -->
        <div class="tabs mb-2" style="margin-bottom: 1rem;">
          <button class="btn ghost" :class="{ active: activeTab === 'overview' }" type="button" @click="activeTab = 'overview'">Overview</button>
          <button class="btn ghost" :class="{ active: activeTab === 'players' }" type="button" @click="activeTab = 'players'">
            Players
            <span v-if="selectedGame.characters.length" class="badge active" style="font-size: 0.65rem; padding: 0.1rem 0.4rem;">{{ selectedGame.characters.length }}</span>
          </button>
          <button class="btn ghost" :class="{ active: activeTab === 'npcs' }" type="button" @click="activeTab = 'npcs'">
            NPCs / Monsters
            <span v-if="selectedGame.npcsAndMonsters.length" class="badge active" style="font-size: 0.65rem; padding: 0.1rem 0.4rem;">{{ selectedGame.npcsAndMonsters.length }}</span>
          </button>
        </div>

        <!-- Overview tab -->
        <div v-if="activeTab === 'overview'" class="grid-2">
          <div class="panel">
            <h2>Player Invite Link</h2>
            <p>Share this link so players can create or reopen their character before a session.</p>
            <div class="copy-row mt-2">
              <input :value="absoluteUrl(selectedGame.inviteUrl)" readonly />
              <button class="btn ghost sm" type="button" @click="copyToClipboard(selectedGame.inviteUrl)">Copy</button>
            </div>
          </div>

          <div class="panel">
            <div class="panel-title">
              <h2>Sessions</h2>
              <button class="btn sm" type="button" :disabled="isSaving" @click="startSession">+ Start</button>
            </div>
            <div v-if="selectedGame.sessions.length === 0" class="empty-state" style="padding: 1rem 0;">
              <p class="text-sm">No sessions yet.</p>
            </div>
            <ul v-else style="list-style: none; margin: 0; padding: 0; display: grid; gap: 0.4rem;">
              <li v-for="session in selectedGame.sessions" :key="session.id">
                <NuxtLink
                  :to="sessionRoute(session)"
                  class="flex items-center gap-2"
                  style="padding: 0.6rem 0.75rem; border: 1px solid var(--border); border-radius: var(--radius); display: flex; text-decoration: none; background: var(--surface); transition: border-color 0.15s;"
                >
                  <span class="badge" :class="sessionBadge(session)">{{ sessionBadgeText(session) }}</span>
                  <span class="flex-1 text-sm" style="color: var(--ink-bright);">
                    {{ session.isActive ? 'Live session' : 'Session recap' }}
                  </span>
                  <span class="text-xs muted font-mono">{{ session.joinCode }}</span>
                  <span class="text-xs muted">{{ session.isActive ? '→ DM Screen' : '→ Summary' }}</span>
                </NuxtLink>
              </li>
            </ul>
          </div>
        </div>

        <!-- Players tab -->
        <div v-if="activeTab === 'players'">
          <div v-if="selectedGame.characters.length === 0" class="panel">
            <div class="empty-state">
              <div class="empty-state-icon">🧙</div>
              <h3>No players yet</h3>
              <p>Players appear here after they join the game via the invite link.</p>
            </div>
          </div>
          <div v-else class="grid-2">
            <article v-for="ch in selectedGame.characters" :key="ch.id" class="panel">
              <div class="flex justify-between items-center mb-1" style="margin-bottom: 0.75rem;">
                <div>
                  <h3 style="margin: 0;">{{ ch.name }}</h3>
                  <p class="text-xs muted" style="margin: 0;">{{ ch.playerName || 'Player name not set' }}</p>
                </div>
                <span style="font-size: 0.7rem; color: var(--muted); text-transform: uppercase; letter-spacing: 0.06em;">AC {{ ch.armor }}</span>
              </div>
              <CharacterSheet :character="ch" />
            </article>
          </div>
        </div>

        <!-- NPCs tab -->
        <div v-if="activeTab === 'npcs'" class="grid-2">
          <div class="panel">
            <h2>{{ editingNpcId ? 'Edit' : 'Add' }} NPC / Monster</h2>
            <form @submit.prevent="editingNpcId ? updateNpc() : createNpc()">
              <label>Name<input v-model.trim="npcName" placeholder="Xenomorph, Goblin, Guard…" required /></label>
              <label>Kind<input v-model.trim="npcKind" placeholder="NPC, Monster, Boss…" required /></label>
              <div class="inline-fields">
                <label>Max HP<input v-model.number="npcMaxHealth" type="number" min="1" required /></label>
                <label>Current HP<input v-model.number="npcHealth" type="number" min="0" required /></label>
                <label>Armor<input v-model.number="npcArmor" type="number" min="0" required /></label>
              </div>
              <label>
                Stat block JSON
                <textarea v-model="npcStatBlockJson" required />
              </label>
              <div class="btn-row">
                <button class="btn" type="submit" :disabled="isSaving">
                  {{ isSaving ? 'Saving…' : editingNpcId ? 'Save' : 'Add NPC' }}
                </button>
                <button v-if="editingNpcId" class="btn ghost" type="button" @click="resetNpcForm">Cancel</button>
              </div>
            </form>
          </div>

          <div v-if="selectedGame.npcsAndMonsters.length === 0" class="panel">
            <div class="empty-state">
              <div class="empty-state-icon">👾</div>
              <p>No NPCs or monsters yet.</p>
            </div>
          </div>

          <article v-for="npc in selectedGame.npcsAndMonsters" :key="npc.id" class="panel">
            <div class="flex justify-between items-center mb-1" style="margin-bottom: 0.75rem;">
              <div>
                <h3 style="margin: 0;">{{ npc.name }}</h3>
                <p class="text-xs muted" style="margin: 0;">{{ npc.kind }}</p>
              </div>
              <div class="btn-row">
                <button class="btn ghost sm" type="button" @click="editNpc(npc)">Edit</button>
                <button class="btn danger sm" type="button" @click="deleteNpc(npc)">✕</button>
              </div>
            </div>
            <HealthBar :current="npc.health" :max="npc.maxHealth" />
            <details style="margin-top: 0.75rem;">
              <summary style="cursor: pointer; font-size: 0.8rem; color: var(--muted-light);">Stat block</summary>
              <pre style="margin-top: 0.5rem; font-size: 0.72rem;">{{ npc.statBlockJson }}</pre>
            </details>
          </article>
        </div>
      </section>

      <!-- Empty / no game selected -->
      <section v-else class="editor">
        <div class="empty-state" style="padding: 4rem 2rem;">
          <div class="empty-state-icon" style="font-size: 3rem;">🎲</div>
          <h2>{{ hasGames ? 'Select a game' : 'Welcome, Dungeon Master' }}</h2>
          <p>{{ hasGames ? 'Choose a game from the sidebar to manage it.' : 'Create your first game to get started.' }}</p>
          <button v-if="!hasGames" class="btn" style="margin-top: 1rem;" type="button" @click="showCreateForm = true">
            Create My First Game
          </button>
        </div>
      </section>
    </main>
  </section>
</template>
