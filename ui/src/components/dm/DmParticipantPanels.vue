<script setup lang="ts">
import type { GameResponse, NpcResponse, RulesetDefinition } from '~/types/api';

interface Props {
  game: GameResponse;
  gameId: string;
  rulesetDefinition: RulesetDefinition | null;
  isBusy?: boolean;
}

withDefaults(defineProps<Props>(), {
  isBusy: false,
});

const emit = defineEmits<{
  cycleNpcVisibility: [npcId: string, currentVisibility: string];
  npcCreated: [npc: NpcResponse];
  npcUpdated: [npc: NpcResponse];
}>();

const showNpcForm = ref(false);
const editingNpc = ref<NpcResponse | null>(null);

function onNpcCreated(npc: NpcResponse) {
  showNpcForm.value = false;
  emit('npcCreated', npc);
}

function onNpcUpdated(npc: NpcResponse) {
  editingNpc.value = null;
  emit('npcUpdated', npc);
}

function startEditNpc(npc: NpcResponse) {
  editingNpc.value = npc;
  showNpcForm.value = false;
}

function cancelNpcEdit() {
  editingNpc.value = null;
}
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
        <CharacterSheet :character="ch" :ruleset-definition="rulesetDefinition" />
      </details>
    </div>
  </div>

  <div class="panel">
    <div class="panel-title">
      <div>
        <h2>NPCs / Monsters</h2>
        <p class="text-sm">Open an NPC to review stats and inventory, or edit during the session.</p>
      </div>
      <div class="btn-row" style="flex-shrink: 0;">
        <span v-if="game.npcsAndMonsters.length" class="badge active">{{ game.npcsAndMonsters.length }}</span>
        <button
          v-if="!showNpcForm && !editingNpc"
          class="btn sm"
          type="button"
          :disabled="isBusy"
          @click="showNpcForm = true"
        >
          Add NPC
        </button>
      </div>
    </div>

    <DmNpcCreator
      v-if="showNpcForm"
      :game-id="gameId"
      :ruleset-definition="rulesetDefinition"
      :is-busy="isBusy"
      @created="onNpcCreated"
      @cancel="showNpcForm = false"
    />

    <DmNpcCreator
      v-if="editingNpc"
      :game-id="gameId"
      :ruleset-definition="rulesetDefinition"
      :npc="editingNpc"
      :is-busy="isBusy"
      @updated="onNpcUpdated"
      @cancel="cancelNpcEdit"
    />

    <div v-if="game.npcsAndMonsters.length === 0 && !showNpcForm && !editingNpc" class="empty-state" style="padding: 1rem 0;">
      <p class="text-sm">No NPCs or monsters yet.</p>
    </div>

    <div v-if="game.npcsAndMonsters.length" class="entity-stat-list">
      <details
        v-for="npc in game.npcsAndMonsters"
        :key="npc.id"
        class="entity-stat-details"
        :open="editingNpc?.id === npc.id"
      >
        <summary>
          <span>
            <strong>{{ npc.name }}</strong>
            <small>{{ npc.kind }}</small>
          </span>
          <span class="entity-stat-actions">
            <button
              class="btn ghost sm"
              type="button"
              :disabled="isBusy || Boolean(editingNpc && editingNpc.id !== npc.id)"
              @click.stop="startEditNpc(npc)"
            >
              Edit
            </button>
            <button
              class="npc-visibility-btn"
              :class="`visibility-${(npc.visibility ?? 'Hidden').toLowerCase()}`"
              type="button"
              :title="`Click to toggle visibility (currently ${npc.visibility ?? 'Hidden'})`"
              @click.stop="emit('cycleNpcVisibility', npc.id, npc.visibility ?? 'Hidden')"
            >
              {{ npc.visibility === 'Visible' ? 'Visible' : 'Hidden' }}
            </button>
            <span class="entity-stat-summary">HP {{ npc.health }}/{{ npc.maxHealth }} · AC {{ npc.armor }}</span>
          </span>
        </summary>
        <NpcSheet :npc="npc" :ruleset-definition="rulesetDefinition" />
      </details>
    </div>
  </div>
</template>
