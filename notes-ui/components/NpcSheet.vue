<script setup lang="ts">
import type { NpcResponse } from '~/types/api';

interface Props {
  npc: NpcResponse;
}

const props = defineProps<Props>();

const statBlock = computed(() => parseJsonValue(props.npc.statBlockJson));
const hasStatBlock = computed(() => !isEmptyValue(statBlock.value));

function parseJsonValue(json: string) {
  try {
    return JSON.parse(json || '{}') as unknown;
  } catch {
    return json;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

function isEmptyValue(value: unknown): boolean {
  if (Array.isArray(value)) return value.length === 0;
  if (isRecord(value)) return Object.keys(value).length === 0;
  return value === null || value === undefined || value === '';
}
</script>

<template>
  <div class="character-sheet">
    <div class="character-sheet-vitals">
      <HealthBar :current="npc.health" :max="npc.maxHealth" />
      <div class="stat-cell">
        <dt>Armor</dt>
        <dd>{{ npc.armor }}</dd>
      </div>
    </div>

    <section v-if="hasStatBlock" class="sheet-section">
      <h3>Stat Block</h3>
      <JsonValue :value="statBlock" :label="npc.name" />
    </section>

    <p v-else class="text-sm muted">No NPC stats have been added yet.</p>
  </div>
</template>

