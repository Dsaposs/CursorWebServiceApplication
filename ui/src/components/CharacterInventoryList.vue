<script setup lang="ts">
import type { RulesetDefinition } from '~/types/api';
import { parseInventory, type InventoryEntry } from '~/utils/inventory';
import { findRulesetItem } from '~/utils/rulesets';

interface Props {
  inventoryJson: string;
  rulesetDefinition?: RulesetDefinition | null;
}

const props = defineProps<Props>();

const entries = computed(() => parseInventory(props.inventoryJson));

function itemLabel(entry: InventoryEntry) {
  return findRulesetItem(props.rulesetDefinition ?? null, entry.itemKey)?.label ?? entry.itemKey;
}
</script>

<template>
  <ul v-if="entries.length" class="inventory-list">
    <li v-for="entry in entries" :key="entry.itemKey" class="inventory-list-item">
      <span class="inventory-item-name">{{ itemLabel(entry) }}</span>
      <span class="inventory-item-qty">×{{ entry.quantity }}</span>
    </li>
  </ul>
  <p v-else class="text-sm muted">No items.</p>
</template>
