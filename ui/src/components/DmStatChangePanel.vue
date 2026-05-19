<script setup lang="ts">
import type { CharacterResponse, NpcResponse, RulesetDefinition } from '~/types/api';
import { parseNestedStatSection } from '~/utils/dice';
import { inventoryQuantity, parseInventory } from '~/utils/inventory';

interface Props {
  characters: CharacterResponse[];
  npcs: NpcResponse[];
  rulesetDefinition: RulesetDefinition | null;
  target?: string;
  healthDelta?: string;
  setHealth?: string;
  setArmor?: string;
  gvDeltas?: Record<string, string>;
  attrDeltas?: Record<string, string>;
  inventoryDeltas?: Record<string, string>;
}

const props = defineProps<Props>();

const emit = defineEmits<{
  'update:target': [value: string];
  'update:healthDelta': [value: string];
  'update:setHealth': [value: string];
  'update:setArmor': [value: string];
  'update:gvDeltas': [value: Record<string, string>];
  'update:attrDeltas': [value: Record<string, string>];
  'update:inventoryDeltas': [value: Record<string, string>];
}>();

const targetModel = computed({
  get: () => props.target ?? '',
  set: (value: string) => emit('update:target', value),
});

const healthDeltaModel = computed({
  get: () => props.healthDelta ?? '',
  set: (value: string) => emit('update:healthDelta', value),
});

const setHealthModel = computed({
  get: () => props.setHealth ?? '',
  set: (value: string) => emit('update:setHealth', value),
});

const setArmorModel = computed({
  get: () => props.setArmor ?? '',
  set: (value: string) => emit('update:setArmor', value),
});

const gvDeltasModel = computed({
  get: () => props.gvDeltas ?? {},
  set: (value: Record<string, string>) => emit('update:gvDeltas', value),
});

const attrDeltasModel = computed({
  get: () => props.attrDeltas ?? {},
  set: (value: Record<string, string>) => emit('update:attrDeltas', value),
});

const inventoryDeltasModel = computed({
  get: () => props.inventoryDeltas ?? {},
  set: (value: Record<string, string>) => emit('update:inventoryDeltas', value),
});

const rulesetItems = computed(() => props.rulesetDefinition?.items ?? []);

const targetCharacter = computed(() => {
  const target = targetModel.value;
  if (!target?.startsWith('Character:')) return null;
  const charId = target.split(':')[1];
  return props.characters.find(c => c.id === charId) ?? null;
});

const targetInventory = computed(() =>
  targetCharacter.value ? parseInventory(targetCharacter.value.inventoryJson) : [],
);

function onTargetChange() {
  emit('update:gvDeltas', {});
  emit('update:attrDeltas', {});
  emit('update:inventoryDeltas', {});
}

function stepInventoryDelta(itemKey: string, delta: number, currentQty: number) {
  const map = { ...inventoryDeltasModel.value };
  const cur = parseInt(map[itemKey] || '0', 10);
  const next = cur + delta;
  if (currentQty + next < 0) return;
  map[itemKey] = String(next);
  inventoryDeltasModel.value = map;
}

function addInventoryItem(itemKey: string) {
  stepInventoryDelta(itemKey, 1, inventoryQuantity(targetInventory.value, itemKey));
}

function stepGvDelta(key: string, delta: number, currentValue: number) {
  const map = { ...gvDeltasModel.value };
  const cur = parseInt(map[key] || '0', 10);
  const next = cur + delta;
  if (currentValue + next < 0) return;
  map[key] = String(next);
  gvDeltasModel.value = map;
}

function stepAttrDelta(key: string, delta: number, currentValue: number) {
  const map = { ...attrDeltasModel.value };
  const cur = parseInt(map[key] || '0', 10);
  const next = cur + delta;
  if (currentValue + next < 0) return;
  map[key] = String(next);
  attrDeltasModel.value = map;
}

function previewValue(current: number, deltaStr: string | undefined) {
  const d = parseInt(deltaStr || '0', 10);
  return Math.max(0, current + (Number.isNaN(d) ? 0 : d));
}
</script>

<template>
  <details class="dm-resolve-optional-card">
    <summary>
      Apply stat change
      <span class="optional-tag">optional</span>
    </summary>
    <div class="dm-resolve-optional-body stat-change-body">
      <label>
        Target
        <select v-model="targetModel" @change="onTargetChange">
          <option value="">No stat change</option>
          <optgroup label="Characters">
            <option v-for="ch in characters" :key="ch.id" :value="`Character:${ch.id}`">
              {{ ch.name }}
            </option>
          </optgroup>
          <optgroup label="NPCs / Monsters">
            <option v-for="npc in npcs" :key="npc.id" :value="`NpcOrMonster:${npc.id}`">
              {{ npc.name }}
            </option>
          </optgroup>
        </select>
      </label>

      <div class="inline-fields">
        <label>HP Δ<input v-model="healthDeltaModel" type="number" placeholder="±" /></label>
        <label>Set HP<input v-model="setHealthModel" type="number" min="0" /></label>
        <label>Set AC<input v-model="setArmorModel" type="number" min="0" /></label>
      </div>

      <template v-if="targetCharacter">
        <div
          v-if="rulesetDefinition?.character?.gameValues?.length"
          class="stat-delta-group"
        >
          <span class="stat-delta-group-label">Game values</span>
          <div
            v-for="gv in rulesetDefinition.character.gameValues"
            :key="gv.key"
            class="stat-delta-row"
          >
            <span class="stat-delta-name">{{ gv.label }}</span>
            <span class="stat-delta-current">
              {{ parseNestedStatSection(targetCharacter.rulesetDataJson, 'gameValues')[gv.key] ?? 0 }}
            </span>
            <div class="roll-adj-stepper">
              <button
                type="button"
                class="adj-btn"
                @click="stepGvDelta(gv.key, -1, parseNestedStatSection(targetCharacter.rulesetDataJson, 'gameValues')[gv.key] ?? 0)"
              >−</button>
              <input
                v-model="gvDeltasModel[gv.key]"
                type="number"
                class="adj-input delta-input"
                placeholder="0"
              />
              <button
                type="button"
                class="adj-btn"
                @click="stepGvDelta(gv.key, 1, parseNestedStatSection(targetCharacter.rulesetDataJson, 'gameValues')[gv.key] ?? 0)"
              >+</button>
            </div>
            <span
              v-if="gvDeltasModel[gv.key]"
              class="stat-delta-preview"
            >
              → {{ previewValue(parseNestedStatSection(targetCharacter.rulesetDataJson, 'gameValues')[gv.key] ?? 0, gvDeltasModel[gv.key]) }}
            </span>
          </div>
        </div>

        <div
          v-if="rulesetDefinition?.character?.attributes?.length"
          class="stat-delta-group"
        >
          <span class="stat-delta-group-label">Attributes</span>
          <div
            v-for="attr in rulesetDefinition.character.attributes"
            :key="attr.key"
            class="stat-delta-row"
          >
            <span class="stat-delta-name">{{ attr.label }}</span>
            <span class="stat-delta-current">
              {{ parseNestedStatSection(targetCharacter.rulesetDataJson, 'attributes')[attr.key] ?? attr.default ?? 0 }}
            </span>
            <div class="roll-adj-stepper">
              <button
                type="button"
                class="adj-btn"
                @click="stepAttrDelta(attr.key, -1, parseNestedStatSection(targetCharacter.rulesetDataJson, 'attributes')[attr.key] ?? attr.default ?? 0)"
              >−</button>
              <input
                v-model="attrDeltasModel[attr.key]"
                type="number"
                class="adj-input delta-input"
                placeholder="0"
              />
              <button
                type="button"
                class="adj-btn"
                @click="stepAttrDelta(attr.key, 1, parseNestedStatSection(targetCharacter.rulesetDataJson, 'attributes')[attr.key] ?? attr.default ?? 0)"
              >+</button>
            </div>
            <span
              v-if="attrDeltasModel[attr.key]"
              class="stat-delta-preview"
            >
              → {{ previewValue(parseNestedStatSection(targetCharacter.rulesetDataJson, 'attributes')[attr.key] ?? attr.default ?? 0, attrDeltasModel[attr.key]) }}
            </span>
          </div>
        </div>

        <div
          v-if="rulesetItems.length"
          class="stat-delta-group"
        >
          <span class="stat-delta-group-label">Inventory</span>
          <div
            v-for="item in rulesetItems"
            :key="item.key"
            class="stat-delta-row"
          >
            <span class="stat-delta-name">{{ item.label }}</span>
            <span class="stat-delta-current">
              {{ inventoryQuantity(targetInventory, item.key) }}
            </span>
            <div class="roll-adj-stepper">
              <button
                type="button"
                class="adj-btn"
                @click="stepInventoryDelta(item.key, -1, inventoryQuantity(targetInventory, item.key))"
              >−</button>
              <input
                v-model="inventoryDeltasModel[item.key]"
                type="number"
                class="adj-input delta-input"
                placeholder="0"
              />
              <button
                type="button"
                class="adj-btn"
                @click="stepInventoryDelta(item.key, 1, inventoryQuantity(targetInventory, item.key))"
              >+</button>
            </div>
            <span
              v-if="inventoryDeltasModel[item.key]"
              class="stat-delta-preview"
            >
              → {{ Math.max(0, inventoryQuantity(targetInventory, item.key) + parseInt(inventoryDeltasModel[item.key] || '0', 10)) }}
            </span>
          </div>
          <div class="inventory-quick-add">
            <label class="text-xs muted">Grant item</label>
            <select @change="(e) => { const key = (e.target as HTMLSelectElement).value; if (key) { addInventoryItem(key); (e.target as HTMLSelectElement).value = ''; } }">
              <option value="">Choose item to add…</option>
              <option v-for="grantItem in rulesetItems" :key="`add-${grantItem.key}`" :value="grantItem.key">
                {{ grantItem.label }}
              </option>
            </select>
          </div>
        </div>
      </template>
    </div>
  </details>
</template>
