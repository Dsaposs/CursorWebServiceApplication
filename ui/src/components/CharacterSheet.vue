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

