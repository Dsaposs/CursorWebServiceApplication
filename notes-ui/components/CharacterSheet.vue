<script setup lang="ts">
import type { CharacterResponse } from '~/types/api';

interface SheetSection {
  key: string;
  label: string;
  value: unknown;
  isEmpty: boolean;
}

interface Props {
  character: CharacterResponse;
}

const props = defineProps<Props>();

const sections = computed<SheetSection[]>(() => [
  buildSection('attributes', 'Attributes', props.character.attributesJson),
  buildSection('skills', 'Skills', props.character.skillsJson),
  buildSection('inventory', 'Inventory', props.character.inventoryJson),
  buildSection('ruleset', 'Ruleset Data', props.character.rulesetDataJson),
].filter(section => !section.isEmpty));

function buildSection(key: string, label: string, json: string): SheetSection {
  const value = parseJsonValue(json);
  return {
    key,
    label,
    value,
    isEmpty: isEmptyValue(value),
  };
}

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
      <HealthBar :current="character.health" :max="character.maxHealth" />
      <div class="stat-cell">
        <dt>Armor</dt>
        <dd>{{ character.armor }}</dd>
      </div>
    </div>

    <div v-if="sections.length" class="character-sheet-sections">
      <section v-for="section in sections" :key="section.key" class="sheet-section">
        <h3>{{ section.label }}</h3>
        <JsonValue :value="section.value" :label="section.label" />
      </section>
    </div>

    <p v-else class="text-sm muted">No character sheet details have been added yet.</p>
  </div>
</template>

