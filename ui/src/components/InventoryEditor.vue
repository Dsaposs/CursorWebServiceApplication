<script setup lang="ts">
import type { RulesetDefinition } from '~/types/api';
import { inventoryQuantity, parseInventory, type InventoryEntry } from '~/utils/inventory';
import { findRulesetItem } from '~/utils/rulesets';

interface Props {
  inventoryJson: string;
  rulesetDefinition: RulesetDefinition | null;
  isSaving?: boolean;
  /** When true, changes emit immediately and the save button is hidden. */
  embedded?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  isSaving: false,
  embedded: false,
});

const emit = defineEmits<{
  save: [inventory: InventoryEntry[]];
}>();

const localInventory = ref<InventoryEntry[]>([]);
const rulesetItems = computed(() => props.rulesetDefinition?.items ?? []);

watch(
  () => props.inventoryJson,
  (json) => {
    localInventory.value = parseInventory(json).map(entry => ({ ...entry }));
  },
  { immediate: true },
);

function itemLabel(itemKey: string) {
  return findRulesetItem(props.rulesetDefinition, itemKey)?.label ?? itemKey;
}

function quantityOf(itemKey: string) {
  return inventoryQuantity(localInventory.value, itemKey);
}

function stepQuantity(itemKey: string, delta: number) {
  const entries = [...localInventory.value];
  const index = entries.findIndex(entry => entry.itemKey === itemKey);
  const current = index >= 0 ? entries[index].quantity : 0;
  const next = current + delta;
  if (next <= 0) {
    if (index >= 0) entries.splice(index, 1);
  } else if (index >= 0) {
    entries[index] = { itemKey, quantity: next };
  } else {
    entries.push({ itemKey, quantity: next });
  }
  localInventory.value = entries.sort((a, b) => a.itemKey.localeCompare(b.itemKey));
  if (props.embedded) {
    emit('save', localInventory.value);
  }
}

function addItem(itemKey: string) {
  if (!itemKey) return;
  stepQuantity(itemKey, 1);
}

function save() {
  emit('save', localInventory.value);
}
</script>

<template>
  <div class="inventory-editor">
    <span class="stat-delta-group-label">Inventory</span>
    <ul v-if="localInventory.length" class="inventory-list" style="margin: 0.75rem 0;">
      <li v-for="entry in localInventory" :key="entry.itemKey" class="inventory-list-item">
        <span class="inventory-item-name">{{ itemLabel(entry.itemKey) }}</span>
        <div class="roll-adj-stepper">
          <button type="button" class="adj-btn" @click="stepQuantity(entry.itemKey, -1)">−</button>
          <span class="adj-input delta-input" style="min-width: 2rem; text-align: center;">{{ entry.quantity }}</span>
          <button type="button" class="adj-btn" @click="stepQuantity(entry.itemKey, 1)">+</button>
        </div>
      </li>
    </ul>
    <p v-else class="text-sm muted">No items.</p>

    <div v-if="rulesetItems.length" class="inventory-quick-add">
      <label class="text-xs muted">Add item</label>
      <select @change="(e) => { addItem((e.target as HTMLSelectElement).value); (e.target as HTMLSelectElement).value = ''; }">
        <option value="">Choose item…</option>
        <option v-for="item in rulesetItems" :key="`add-${item.key}`" :value="item.key">
          {{ item.label }}
        </option>
      </select>
    </div>

    <button
      v-if="!embedded"
      class="btn sm"
      type="button"
      style="margin-top: 0.75rem;"
      :disabled="isSaving"
      @click="save"
    >
      {{ isSaving ? 'Saving…' : 'Save inventory' }}
    </button>
  </div>
</template>
