<script setup lang="ts">
import { getDiceRoller } from '~/dice-rollers/registry';
import type { ParsedPlayerRoll } from '~/dice-rollers/types';

interface Props {
  rollerKey: string;
  description?: string | null;
  modifier: number;
}

const props = defineProps<Props>();
const emit = defineEmits<{ 'update:modifier': [value: number] }>();

const roller = computed(() => getDiceRoller(props.rollerKey));

const parsed = computed<ParsedPlayerRoll>(() => {
  const rollLine = props.description?.split('\n').find(line => line.includes('🎲 Roll:')) ?? '';
  return roller.value.parsePlayerRoll(rollLine);
});

const adjustedPrimary = computed(() =>
  Math.max(0, parsed.value.primary + props.modifier),
);

const summary = computed(() =>
  parsed.value.hasRoll || props.modifier !== 0
    ? roller.value.formatAdjustedSummary(parsed.value, props.modifier)
    : '',
);

defineExpose({ summary, parsed, adjustedPrimary });

function step(delta: number) {
  emit('update:modifier', props.modifier + delta);
}
</script>

<template>
  <div class="roll-adjuster">
    <span class="roll-adj-title">Roll Result</span>

    <div v-if="parsed.hasRoll" class="roll-adj-player-row">
      <span class="roll-adj-label">Player rolled</span>
      <span class="roll-adj-player-successes" :class="{ zero: parsed.primary === 0 }">
        {{ parsed.primary }}
        <template v-if="rollerKey === 'd6-pool'">
          success{{ parsed.primary !== 1 ? 'es' : '' }}
        </template>
        <template v-else> on d20</template>
      </span>
      <span v-if="parsed.secondary" class="roll-adj-panic">
        · {{ parsed.secondary }} {{ parsed.secondaryLabel }}{{ parsed.secondary !== 1 ? 's' : '' }}
      </span>
    </div>
    <p v-else class="text-sm muted">No dice roll recorded on this action.</p>

    <div class="roll-adj-modifier-row">
      <span class="roll-adj-label">DM Modifier</span>
      <div class="roll-adj-stepper">
        <button type="button" class="adj-btn" @click="step(-1)">−</button>
        <input
          :value="modifier"
          type="number"
          class="adj-input"
          placeholder="0"
          @input="emit('update:modifier', Number(($event.target as HTMLInputElement).value) || 0)"
        />
        <button type="button" class="adj-btn" @click="step(1)">+</button>
      </div>
    </div>

    <div v-if="modifier !== 0 && parsed.hasRoll" class="roll-adj-result">
      <span class="roll-adj-successes" :class="{ zero: adjustedPrimary === 0 }">
        <template v-if="rollerKey === 'd6-pool'">
          {{ adjustedPrimary }} success{{ adjustedPrimary !== 1 ? 'es' : '' }}
        </template>
        <template v-else>d20 result {{ adjustedPrimary }}</template>
      </span>
      <span class="roll-adj-mod-note">
        ({{ parsed.primary }} raw {{ modifier > 0 ? '+' : '' }}{{ modifier }})
      </span>
    </div>
  </div>
</template>
