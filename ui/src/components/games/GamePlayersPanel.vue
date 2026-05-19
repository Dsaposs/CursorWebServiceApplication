<script setup lang="ts">
import type { CharacterResponse, RulesetDefinition } from '~/types/api';
import type { InventoryEntry } from '~/utils/inventory';

interface Props {
  gameId: string;
  characters: CharacterResponse[];
  rulesetDefinition: RulesetDefinition | null;
  isSaving?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  isSaving: false,
});

const emit = defineEmits<{
  'inventory-saved': [characterId: string, character: CharacterResponse];
}>();

const { api } = useApi();
const { error: toastError, success: toastSuccess } = useToast();

async function saveInventory(characterId: string, inventory: InventoryEntry[]) {
  try {
    const updated = await api<CharacterResponse>(`/api/games/${props.gameId}/characters/${characterId}/inventory`, {
      method: 'PUT',
      body: {
        inventory: inventory.map(entry => ({ itemKey: entry.itemKey, quantity: entry.quantity })),
      },
    });
    emit('inventory-saved', characterId, updated);
    toastSuccess('Character inventory updated.');
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  }
}
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
      <CharacterSheet :character="ch" :ruleset-definition="rulesetDefinition" />
      <InventoryEditor
        v-if="rulesetDefinition"
        :inventory-json="ch.inventoryJson"
        :ruleset-definition="rulesetDefinition"
        :is-saving="isSaving"
        style="margin-top: 1rem;"
        @save="saveInventory(ch.id, $event)"
      />
    </article>
  </div>
</template>
