<script setup lang="ts">
import type { NpcResponse, RulesetDefinition } from '~/types/api';
import { parseNpcInventory } from '~/utils/inventory';
import { npcAttributeValue, npcHasStructuredStats, npcSkillValue } from '~/utils/npcStats';

interface Props {
  npc: NpcResponse;
  rulesetDefinition?: RulesetDefinition | null;
}

const props = defineProps<Props>();

const attributes = computed(() => props.rulesetDefinition?.character.attributes ?? []);
const skills = computed(() => props.rulesetDefinition?.character.skills ?? []);
const inventoryJson = computed(() =>
  JSON.stringify(parseNpcInventory(props.npc.statBlockJson)),
);
const hasStructuredStats = computed(() => npcHasStructuredStats(props.npc.statBlockJson));
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

    <section v-if="rulesetDefinition" class="sheet-section">
      <h3>Inventory</h3>
      <CharacterInventoryList
        :inventory-json="inventoryJson"
        :ruleset-definition="rulesetDefinition"
      />
    </section>

    <template v-if="hasStructuredStats && (attributes.length || skills.length)">
      <div v-if="attributes.length" class="npc-stats-section">
        <p class="text-xs muted" style="margin-bottom: 0.35rem;">Attributes</p>
        <div class="stat-grid">
          <div v-for="attr in attributes" :key="attr.key" class="stat-cell">
            <dt>{{ attr.label }}</dt>
            <dd>{{ npcAttributeValue(npc.statBlockJson, attr.key) ?? '–' }}</dd>
          </div>
        </div>
      </div>
      <div v-if="skills.length" class="npc-stats-section">
        <p class="text-xs muted" style="margin-bottom: 0.35rem;">Skills</p>
        <div class="stat-grid">
          <div v-for="skill in skills" :key="skill.key" class="stat-cell">
            <dt>{{ skill.label }}</dt>
            <dd>{{ npcSkillValue(npc.statBlockJson, skill.key) ?? '–' }}</dd>
          </div>
        </div>
      </div>
    </template>

    <p v-else-if="!rulesetDefinition" class="text-sm muted">No NPC stats have been added yet.</p>
  </div>
</template>

<style scoped>
.npc-stats-section {
  margin-top: 0.25rem;
}
</style>
