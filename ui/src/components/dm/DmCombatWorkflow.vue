<script setup lang="ts">
import type { InitiativeEntryResponse } from '~/types/api';

interface Props {
  isCombat: boolean;
  isSaving: boolean;
  currentTurn: InitiativeEntryResponse | null;
  displayedInitiative: InitiativeEntryResponse[];
  draggedInitiativeId: string | null;
  dragOverId: string | null;
  expandedEntryId: string | null;
}

defineProps<Props>();

const emit = defineEmits<{
  setupCombat: [];
  advanceTurn: [];
  endCombat: [];
  startDrag: [entryId: string, event: PointerEvent];
  moveKeyboard: [entryId: string, offset: -1 | 1];
  activateEntry: [entry: InitiativeEntryResponse];
}>();
</script>

<template>
  <div class="panel">
    <div class="panel-title">
      <div>
        <h2>Combat</h2>
        <p class="text-sm">{{ isCombat ? 'Drag to reorder. Click Act / Prompt to take a turn.' : 'Rolls initiative for all characters and NPCs per the ruleset.' }}</p>
      </div>
      <div class="btn-row">
        <button v-if="isCombat && displayedInitiative.length" class="btn ghost sm" type="button" :disabled="isSaving" @click="emit('advanceTurn')">Next Turn →</button>
        <button v-if="!isCombat" class="btn ghost sm" type="button" :disabled="isSaving" @click="emit('setupCombat')">
          {{ isSaving ? 'Starting…' : 'Start combat' }}
        </button>
      </div>
    </div>

    <ul v-if="isCombat && displayedInitiative.length" class="initiative-list" style="margin-bottom: 1rem;">
      <li
        v-for="(entry, idx) in displayedInitiative"
        :key="entry.id"
        class="initiative-item"
        :class="{
          'current-turn': entry.isCurrentTurn,
          'current-turn-expanded': entry.id === expandedEntryId,
          'dragging': draggedInitiativeId === entry.id,
          'draggable': isCombat && !isSaving,
          'drag-over': dragOverId === entry.id && draggedInitiativeId !== null && draggedInitiativeId !== entry.id,
        }"
        :data-initiative-id="entry.id"
      >
        <div class="initiative-item-header">
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
            <span v-if="entry.initiativeScore" class="initiative-score text-sm">{{ entry.initiativeScore }}</span>
          </span>
          <span v-if="entry.isCurrentTurn" class="badge active">Turn</span>
          <button
            v-if="entry.isCurrentTurn"
            class="btn sm"
            :class="entry.id === expandedEntryId ? 'ghost' : ''"
            type="button"
            :disabled="isSaving"
            @click="emit('activateEntry', entry)"
          >
            {{ entry.id === expandedEntryId ? 'Close' : entry.combatantType === 'Character' ? 'Prompt' : 'Act' }}
          </button>
        </div>

        <!-- Inline form — shown when DM has activated this entry -->
        <div v-if="entry.id === expandedEntryId" class="initiative-turn-body">
          <slot name="entry-action" :entry="entry" />
        </div>
      </li>
    </ul>

    <button v-if="isCombat" class="btn danger w-full" type="button" :disabled="isSaving" @click="emit('endCombat')">
      End Combat
    </button>
  </div>
</template>
