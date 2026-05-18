<script setup lang="ts">
import type { NpcResponse } from '~/types/api';
import { isEmptyValue, parseJsonValue } from '~/utils/jsonDisplay';

interface Props {
  npc: NpcResponse;
}

const props = defineProps<Props>();

const statBlock = computed(() => parseJsonValue(props.npc.statBlockJson));
const hasStatBlock = computed(() => !isEmptyValue(statBlock.value));
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

