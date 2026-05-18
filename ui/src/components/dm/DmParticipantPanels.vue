<script setup lang="ts">
import type { GameResponse } from '~/types/api';

interface Props {
  game: GameResponse;
}

defineProps<Props>();

const emit = defineEmits<{
  cycleNpcVisibility: [npcId: string, currentVisibility: string];
}>();
</script>

<template>
  <div class="panel">
    <div class="panel-title">
      <div>
        <h2>Characters</h2>
        <p class="text-sm">Open a character to review health, armor, and sheet stats.</p>
      </div>
      <span v-if="game.characters.length" class="badge active">{{ game.characters.length }}</span>
    </div>

    <div v-if="game.characters.length === 0" class="empty-state" style="padding: 1rem 0;">
      <p class="text-sm">No characters have joined this game yet.</p>
    </div>

    <div v-else class="entity-stat-list">
      <details v-for="ch in game.characters" :key="ch.id" class="entity-stat-details">
        <summary>
          <span>
            <strong>{{ ch.name }}</strong>
            <small>{{ ch.playerName || 'No player name' }}</small>
          </span>
          <span class="entity-stat-summary">HP {{ ch.health }}/{{ ch.maxHealth }} · AC {{ ch.armor }}</span>
        </summary>
        <CharacterSheet :character="ch" />
      </details>
    </div>
  </div>

  <div class="panel">
    <div class="panel-title">
      <div>
        <h2>NPCs / Monsters</h2>
        <p class="text-sm">Open an NPC to review health, armor, and stat block details.</p>
      </div>
      <span v-if="game.npcsAndMonsters.length" class="badge active">{{ game.npcsAndMonsters.length }}</span>
    </div>

    <div v-if="game.npcsAndMonsters.length === 0" class="empty-state" style="padding: 1rem 0;">
      <p class="text-sm">No NPCs or monsters have been added.</p>
    </div>

    <div v-else class="entity-stat-list">
      <details v-for="npc in game.npcsAndMonsters" :key="npc.id" class="entity-stat-details">
        <summary>
          <span>
            <strong>{{ npc.name }}</strong>
            <small>{{ npc.kind }}</small>
          </span>
          <span class="entity-stat-actions">
            <button
              class="npc-visibility-btn"
              :class="`visibility-${(npc.visibility ?? 'Visible').toLowerCase()}`"
              type="button"
              :title="`Click to cycle: Visible -> Obscured -> Hidden (currently ${npc.visibility ?? 'Visible'})`"
              @click.stop="emit('cycleNpcVisibility', npc.id, npc.visibility ?? 'Visible')"
            >
              {{ npc.visibility === 'Hidden' ? 'Hidden' : npc.visibility === 'Obscured' ? 'Obscured' : 'Visible' }}
            </button>
            <span class="entity-stat-summary">HP {{ npc.health }}/{{ npc.maxHealth }} · AC {{ npc.armor }}</span>
          </span>
        </summary>
        <NpcSheet :npc="npc" />
      </details>
    </div>
  </div>
</template>
