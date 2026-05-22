<script setup lang="ts">
import type { JoinGameResponse, SessionJoinOptionsResponse, SessionSummaryResponse } from '~/types/api';
import { parseRulesetDefinition } from '~/utils/rulesets';

const route = useRoute();
const { api } = useApi();
const { getSessionPlayerToken, getGamePlayerToken, setPlayerTokenForSessionAndGame } = usePlayerTokens();
const { error: toastError } = useToast();

const session = ref<SessionSummaryResponse | null>(null);
const availableCharacters = ref<SessionJoinOptionsResponse['availableCharacters']>([]);
const ruleset = ref<SessionJoinOptionsResponse['ruleset'] | null>(null);
const joinMode = ref<'existing' | 'new'>('existing');
const selectedCharacterId = ref('');
const selectedClassKey = ref('');
const skillAllocations = ref<Record<string, number>>({});
const startingItemKey = ref('');
const characterName = ref('');
const playerName = ref('');
const fieldError = ref('');
const isLoading = ref(true);
const isJoining = ref(false);

// Try to detect an existing token for this session
const existingToken = ref<string | null>(null);
const rulesetDefinition = computed(() => parseRulesetDefinition(ruleset.value));

onMounted(async () => {
  try {
    const options = await api<SessionJoinOptionsResponse>(`/api/session-join/${route.params.code}`);
    session.value = options.session;
    ruleset.value = options.ruleset;
    existingToken.value = getSessionPlayerToken(route.params.code) ?? getGamePlayerToken(options.session.gameId);
    availableCharacters.value = options.availableCharacters;
    selectedCharacterId.value = options.availableCharacters[0]?.id ?? '';
    joinMode.value = options.availableCharacters.length ? 'existing' : 'new';
    selectedClassKey.value = rulesetDefinition.value?.character.classes[0]?.key ?? '';
  } catch (err) {
    fieldError.value = extractError(err);
  } finally {
    isLoading.value = false;
  }
});

async function joinSession() {
  fieldError.value = '';
  isJoining.value = true;
  try {
    const joined = await api<JoinGameResponse>(`/api/session-join/${route.params.code}`, {
      method: 'POST',
      body: joinMode.value === 'existing'
        ? { characterId: selectedCharacterId.value, playerName: playerName.value }
        : {
            characterName: characterName.value,
            playerName: playerName.value,
            classKey: selectedClassKey.value,
            skillAllocations: skillAllocations.value,
            startingItemKey: startingItemKey.value || undefined,
          },
    });
    setPlayerTokenForSessionAndGame(route.params.code, joined.game.id, joined.participantToken);
    await navigateTo(`/sessions/${route.params.code}/player`);
  } catch (err) {
    fieldError.value = err instanceof Error ? err.message : String(err);
    toastError(fieldError.value);
  } finally {
    isJoining.value = false;
  }
}

async function rejoin() {
  await navigateTo(`/sessions/${route.params.code}/player`);
}
</script>

<template>
  <section class="page">
    <div class="card">
      <div class="text-center" style="margin-bottom: 1.5rem;">
        <div style="font-size: 2rem; margin-bottom: 0.5rem;" aria-hidden="true">🎲</div>
        <h1 style="margin: 0 0 0.25rem;">Join Session</h1>
        <p v-if="session" style="margin: 0; font-size: 0.875rem;">
          <span class="badge" :class="session.state === 'Combat' ? 'combat' : 'exploration'">{{ session.state }}</span>
        </p>
      </div>

      <div v-if="isLoading" class="text-center muted">
        <p>Looking up session…</p>
      </div>

      <div v-else-if="fieldError && !session" class="alert error" style="margin-bottom: 1rem;">
        {{ fieldError }}
        <p class="text-sm" style="margin-top: 0.5rem; margin-bottom: 0;">Ask your DM for an active session link.</p>
      </div>

      <template v-else-if="session">
        <!-- Returning player shortcut -->
        <div v-if="existingToken" class="alert info" style="margin-bottom: 1.25rem;">
          <div>
            <div style="font-weight: 700; margin-bottom: 0.3rem;">Returning player?</div>
            <p style="margin: 0 0 0.6rem; font-size: 0.85rem;">You have a saved session token.</p>
            <button class="btn sm" type="button" @click="rejoin">Resume as saved character</button>
          </div>
        </div>

        <form @submit.prevent="joinSession">
          <div class="join-choice-grid">
            <button
              v-if="availableCharacters.length"
              class="join-choice"
              :class="{ active: joinMode === 'existing' }"
              type="button"
              @click="joinMode = 'existing'"
            >
              Pick Existing
            </button>
            <button
              class="join-choice"
              :class="{ active: joinMode === 'new' }"
              type="button"
              @click="joinMode = 'new'"
            >
              Create New
            </button>
          </div>

          <label v-if="joinMode === 'existing' && availableCharacters.length">
            Existing character
            <select v-model="selectedCharacterId" required>
              <option v-for="character in availableCharacters" :key="character.id" :value="character.id">
                {{ character.name }}{{ character.playerName ? ` (${character.playerName})` : '' }}
              </option>
            </select>
          </label>

          <div v-else-if="joinMode === 'existing'" class="alert info">
            No existing characters are available for this session. Create a new character to join.
          </div>

          <label v-if="joinMode === 'new'">
            New character name
            <input v-model.trim="characterName" placeholder="Your character's name" required />
          </label>

          <CharacterCreationSetup
            v-if="joinMode === 'new' && rulesetDefinition"
            :definition="rulesetDefinition"
            :class-key="selectedClassKey"
            :skill-allocations="skillAllocations"
            :starting-item-key="startingItemKey"
            @update:class-key="selectedClassKey = $event"
            @update:skill-allocations="skillAllocations = $event"
            @update:starting-item-key="startingItemKey = $event"
          />

          <label>
            Your name <span class="muted text-xs">(optional)</span>
            <input v-model.trim="playerName" placeholder="Real name or nickname" />
          </label>

          <div v-if="fieldError" class="alert error">{{ fieldError }}</div>

          <button class="btn w-full" type="submit" :disabled="isJoining">
            {{ isJoining ? 'Joining…' : 'Join Session' }}
          </button>
        </form>

        <p class="text-xs muted text-center" style="margin-top: 1rem; margin-bottom: 0;">
          Existing characters already claimed for this game are hidden from the list.
        </p>
      </template>
    </div>
  </section>
</template>
