<script setup lang="ts">
import type { GameResponse } from '~/types/api';

interface Props {
  games: GameResponse[];
  selectedGameId?: string | null;
  isLoading: boolean;
}

const props = defineProps<Props>();

const emit = defineEmits<{
  create: [];
  open: [gameId: string];
}>();

const hasGames = computed(() => props.games.length > 0);
</script>

<template>
  <aside class="sidebar">
    <div class="sidebar-header">
      <h2>My Games</h2>
      <button class="btn sm" type="button" @click="emit('create')">
        + New
      </button>
    </div>

    <ul class="game-list">
      <li v-if="!hasGames && !isLoading">
        <div class="empty-state" style="padding: 1.5rem 0.5rem;">
          <div class="empty-state-icon" aria-hidden="true">🎲</div>
          <p class="text-xs">No games yet. Create your first!</p>
        </div>
      </li>
      <li v-for="game in games" :key="game.id">
        <button
          class="game-list-item"
          :class="{ active: selectedGameId === game.id }"
          type="button"
          @click="emit('open', game.id)"
        >
          <strong>{{ game.name }}</strong>
          <span>{{ game.rulesetName }}</span>
        </button>
      </li>
    </ul>
  </aside>
</template>
