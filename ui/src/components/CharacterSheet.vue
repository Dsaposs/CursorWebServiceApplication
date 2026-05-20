<script setup lang="ts">
import type { CharacterResponse, RulesetDefinition } from '~/types/api';
import { buildSheetSection, type SheetSection } from '~/utils/jsonDisplay';

interface Props {
  character: CharacterResponse;
  rulesetDefinition?: RulesetDefinition | null;
}

const props = defineProps<Props>();

const sections = computed<SheetSection[]>(() => [
  buildSheetSection('ruleset', 'Ruleset Data', props.character.rulesetDataJson),
].filter(section => !section.isEmpty));

const activeStatuses = computed<string[]>(() => {
  try { return JSON.parse(props.character.statusEffectsJson ?? '[]') as string[]; } catch { return []; }
});

function statusDef(key: string) {
  return props.rulesetDefinition?.statusEffects?.find(s => s.key.toLowerCase() === key.toLowerCase());
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

    <!-- Active status effects -->
    <section v-if="activeStatuses.length" class="sheet-section">
      <h3>Status Effects</h3>
      <div class="status-effects-list">
        <span
          v-for="key in activeStatuses"
          :key="key"
          class="status-badge"
          :class="(statusDef(key)?.isNegative ?? true) ? 'negative' : 'positive'"
          :title="statusDef(key)?.description ?? undefined"
        >
          {{ statusDef(key)?.label ?? key }}
        </span>
      </div>
    </section>

    <section class="sheet-section">
      <h3>Inventory</h3>
      <CharacterInventoryList
        :inventory-json="character.inventoryJson"
        :ruleset-definition="rulesetDefinition"
      />
    </section>

    <div v-if="sections.length" class="character-sheet-sections">
      <section v-for="section in sections" :key="section.key" class="sheet-section">
        <h3>{{ section.label }}</h3>
        <JsonValue :value="section.value" :label="section.label" />
      </section>
    </div>

    <p v-else class="text-sm muted">No character sheet details have been added yet.</p>
  </div>
</template>

