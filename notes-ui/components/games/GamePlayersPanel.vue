<script setup lang="ts">
import type { CharacterResponse } from '~/types/api';

interface Props {
  characters: CharacterResponse[];
}

defineProps<Props>();
</script>

<template>
  <div v-if="characters.length === 0" class="panel">
    <div class="empty-state">
      <div class="empty-state-icon" aria-hidden="true">🧙</div>
      <h3>No players yet</h3>
      <p>Players appear here after they join the game via the invite link.</p>
    </div>
  </div>
  <div v-else class="grid-2">
    <article v-for="ch in characters" :key="ch.id" class="panel">
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
</template>
