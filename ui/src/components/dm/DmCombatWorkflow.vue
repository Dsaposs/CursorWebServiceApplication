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
  /** Current round number from the active CombatEncounter. */
  round?: number;
  /** The character ID that has been prompted to act this turn, if any. */
  promptedTurnCharacterId?: string | null;
}

const props = withDefaults(defineProps<Props>(), {
  round: 1,
  promptedTurnCharacterId: null,
});

const emit = defineEmits<{
  setupCombat: [];
  advanceTurn: [];
  endCombat: [];
  startDrag: [entryId: string, event: PointerEvent];
  moveKeyboard: [entryId: string, offset: -1 | 1];
  activateEntry: [entry: InitiativeEntryResponse];
  promptTurn: [entry: InitiativeEntryResponse];
}>();


function entryButtonLabel(entry: InitiativeEntryResponse): string {
  if (entry.id === props.expandedEntryId) return 'Close';
  if (entry.combatantType === 'Character') {
    return props.promptedTurnCharacterId === entry.combatantId ? 'View' : 'Prompt';
  }
  return 'Act';
}

function handleEntryButton(entry: InitiativeEntryResponse) {
  const shouldPromptPlayer = entry.combatantType === 'Character'
    && props.promptedTurnCharacterId !== entry.combatantId;

  if (shouldPromptPlayer) {
    emit('promptTurn', entry);
  }

  emit('activateEntry', entry);
}
</script>

<template>
  <div class="panel">
    <div class="panel-title">
      <div>
        <h2>Combat</h2>
        <p class="text-sm">
          {{ isCombat
            ? 'Drag to reorder. Activate a turn to act or prompt.'
            : 'Rolls initiative for all characters and NPCs per the ruleset.' }}
        </p>
      </div>
      <button
        v-if="!isCombat"
        class="btn"
        type="button"
        :disabled="isSaving"
        @click="emit('setupCombat')"
      >
        {{ isSaving ? 'Starting…' : 'Start combat' }}
      </button>
      <div v-if="isCombat" class="btn-row" style="align-items: center; gap: 0.5rem;">
        <!-- Round badge -->
        <span
          class="badge"
          style="font-weight: 600; font-size: 0.8rem;"
        >
          Round {{ round }}
        </span>

        <button
          v-if="displayedInitiative.length"
          class="btn ghost sm"
          type="button"
          :disabled="isSaving"
          @click="emit('advanceTurn')"
        >
          Next Turn →
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
          'locked-turn': !entry.isCurrentTurn,
        }"
        :data-initiative-id="entry.id"
        :aria-disabled="!entry.isCurrentTurn ? 'true' : undefined"
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
            @click="handleEntryButton(entry)"
          >
            {{ entryButtonLabel(entry) }}
          </button>
        </div>

        <!-- Inline form — shown when DM has activated this entry -->
        <div v-if="entry.id === expandedEntryId" class="initiative-turn-body">
          <slot name="entry-action" :entry="entry" />
        </div>
      </li>
    </ul>

    <button
      v-if="isCombat"
      class="btn danger w-full"
      type="button"
      :disabled="isSaving"
      @click="emit('endCombat')"
    >
      End Combat
    </button>
  </div>
</template>
