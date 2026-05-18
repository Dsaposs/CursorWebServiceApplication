<script setup lang="ts">
import type { InitiativeEntryResponse, NpcResponse } from '~/types/api';

interface Props {
  isCombat: boolean;
  isSaving: boolean;
  currentTurn: InitiativeEntryResponse | null;
  displayedInitiative: InitiativeEntryResponse[];
  draggedInitiativeId: string | null;
  dragOverId: string | null;
  currentTurnNpc: NpcResponse | null;
}

defineProps<Props>();

const emit = defineEmits<{
  setupCombat: [];
  advanceTurn: [];
  endCombat: [];
  startDrag: [entryId: string, event: PointerEvent];
  moveKeyboard: [entryId: string, offset: -1 | 1];
  takeCurrentNpcAction: [npcId: string];
}>();
</script>

<template>
  <div class="panel">
    <div class="panel-title">
      <div>
        <h2>Combat</h2>
        <p class="text-sm">{{ isCombat ? 'Use the handle or arrow keys to change turn order.' : 'All characters and NPCs will be included.' }}</p>
      </div>
      <div class="btn-row">
        <button v-if="isCombat && displayedInitiative.length" class="btn sm" type="button" :disabled="isSaving" @click="emit('advanceTurn')">Next Turn →</button>
        <button v-if="!isCombat" class="btn ghost sm" type="button" :disabled="isSaving" @click="emit('setupCombat')">
          {{ isSaving ? 'Starting…' : 'Set Initiative' }}
        </button>
      </div>
    </div>

    <div v-if="currentTurn && isCombat" class="alert info" style="margin-bottom: 1rem;">
      <span aria-hidden="true">⚔️</span> <strong>{{ currentTurn.combatantName }}'s turn</strong>
    </div>

    <div v-if="currentTurnNpc && isCombat" class="npc-turn-action">
      <div>
        <strong>{{ currentTurnNpc.name }} is up.</strong>
        <p class="text-sm">Queue an action with this NPC preselected.</p>
      </div>
      <button class="btn sm" type="button" :disabled="isSaving" @click="emit('takeCurrentNpcAction', currentTurnNpc.id)">
        NPC Action
      </button>
    </div>

    <ul v-if="isCombat && displayedInitiative.length" class="initiative-list" style="margin-bottom: 1rem;">
      <li
        v-for="(entry, idx) in displayedInitiative"
        :key="entry.id"
        class="initiative-item"
        :class="{
          'current-turn': entry.isCurrentTurn,
          'dragging': draggedInitiativeId === entry.id,
          'draggable': isCombat && !isSaving,
          'drag-over': dragOverId === entry.id && draggedInitiativeId !== null && draggedInitiativeId !== entry.id,
        }"
        :data-initiative-id="entry.id"
      >
        <button
          class="initiative-drag-handle"
          type="button"
          :disabled="isSaving"
          :aria-label="`Reorder ${entry.combatantName}`"
          @pointerdown.stop="emit('startDrag', entry.id, $event)"
          @keydown.up.prevent="emit('moveKeyboard', entry.id, -1)"
          @keydown.down.prevent="emit('moveKeyboard', entry.id, 1)"
        >
          <span aria-hidden="true">↕</span>
        </button>
        <span class="initiative-order">{{ idx + 1 }}</span>
        <span class="initiative-card-body">
          <span class="initiative-name">{{ entry.combatantName }}</span>
          <span class="initiative-type">{{ entry.combatantType }}</span>
        </span>
        <span v-if="entry.isCurrentTurn" class="badge active">Turn</span>
      </li>
    </ul>

    <button v-if="isCombat" class="btn danger w-full" type="button" :disabled="isSaving" @click="emit('endCombat')">
      End Combat
    </button>
  </div>
</template>
