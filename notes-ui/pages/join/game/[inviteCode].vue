<script setup lang="ts">
import type { JoinGameResponse } from '~/types/api';

const route = useRoute();
const { api } = useApi();
const { error: toastError, success: toastSuccess } = useToast();

const characterName = ref('');
const playerName = ref('');
const joined = ref<JoinGameResponse | null>(null);
const fieldError = ref('');
const isJoining = ref(false);

async function joinGame() {
  fieldError.value = '';
  isJoining.value = true;
  try {
    joined.value = await api<JoinGameResponse>(`/api/game-participants/join/${route.params.inviteCode}`, {
      method: 'POST',
      body: { characterName: characterName.value, playerName: playerName.value },
    });
    localStorage.setItem(`ttrpg_player_${joined.value.game.id}`, joined.value.participantToken);
    toastSuccess(`Welcome, ${joined.value.character.name}!`);
  } catch (err) {
    fieldError.value = err instanceof Error ? err.message : String(err);
    toastError(fieldError.value);
  } finally {
    isJoining.value = false;
  }
}
</script>

<template>
  <section class="page">
    <div class="card">
      <div class="text-center" style="margin-bottom: 1.5rem;">
        <div style="font-size: 2rem; margin-bottom: 0.5rem;">⚔️</div>
        <h1 style="margin: 0 0 0.25rem;">Join a Game</h1>
        <p style="margin: 0; font-size: 0.875rem;">Enter your character name to join the campaign.</p>
      </div>

      <!-- Success state -->
      <template v-if="joined">
        <div class="alert success" style="margin-bottom: 1.25rem;">
          <div>
            <div style="font-weight: 700; margin-bottom: 0.2rem;">You're in!</div>
            <p style="margin: 0; font-size: 0.875rem;">
              Welcome to <strong>{{ joined.game.name }}</strong> as <strong>{{ joined.character.name }}</strong>.
            </p>
          </div>
        </div>

        <div class="panel" style="margin-bottom: 1rem;">
          <h2 style="margin-bottom: 0.75rem;">{{ joined.character.name }}</h2>
          <HealthBar :current="joined.character.health" :max="joined.character.maxHealth" />
          <p class="text-sm muted" style="margin-top: 0.75rem;">
            Your DM will share a session join link when the game begins. Keep this page bookmarked!
          </p>
        </div>

        <button class="btn ghost w-full" type="button" @click="joined = null; characterName = ''; playerName = ''">
          Join with a different character
        </button>
      </template>

      <!-- Join form -->
      <form v-else @submit.prevent="joinGame">
        <label>
          Character name
          <input v-model.trim="characterName" placeholder="Your character's name" required />
        </label>
        <label>
          Your name <span class="muted text-xs">(optional)</span>
          <input v-model.trim="playerName" placeholder="Real name or nickname" />
        </label>

        <div v-if="fieldError" class="alert error">{{ fieldError }}</div>

        <button class="btn w-full" type="submit" :disabled="isJoining">
          {{ isJoining ? 'Joining…' : 'Join Game' }}
        </button>

        <p class="text-xs muted text-center" style="margin-bottom: 0;">
          If your character already exists it will be reopened with the same token.
        </p>
      </form>
    </div>
  </section>
</template>
