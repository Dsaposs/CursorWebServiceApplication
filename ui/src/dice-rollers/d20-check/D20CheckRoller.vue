<script setup lang="ts">
import type { D20CheckRollConfig, RollResultKind } from '~/dice-rollers/types';
import { rollDice } from '~/utils/dice';
import { useDiceMode } from '~/composables/useDiceMode';

interface Props {
  config: D20CheckRollConfig;
  poolBreakdown?: string[];
  label?: string;
  successRule?: string;
  resultKind?: RollResultKind;
  modelValue?: string;
}

const props = withDefaults(defineProps<Props>(), {
  poolBreakdown: () => [],
  label: 'Dice Roll',
  resultKind: 'PassFail',
  modelValue: '',
});

const isTotalMode = computed(() => props.resultKind === 'Total');

const emit = defineEmits<{ 'update:modelValue': [value: string] }>();
const { mode, setMode } = useDiceMode();

const sides = computed(() => props.config.sides);
const roll = ref<number | null>(null);
const hasRolled = ref(false);
const manualTotal = ref('');
const modifier = ref(props.config.attackBonus ?? 0);

const displayTotal = computed(() => (roll.value ?? 0) + modifier.value);

watch(() => props.config.sides, clear);
watch(mode, clear);

function clear() {
  roll.value = null;
  hasRolled.value = false;
  manualTotal.value = '';
  modifier.value = props.config.attackBonus ?? 0;
  emit('update:modelValue', '');
}

watch(() => props.config.attackBonus, (bonus) => {
  modifier.value = bonus ?? 0;
});

function autoRoll() {
  roll.value = rollDice(1, sides.value)[0];
  hasRolled.value = true;
  emitResult();
}

function emitResult() {
  if (roll.value === null && !manualTotal.value) {
    emit('update:modelValue', '');
    return;
  }
  const base = roll.value ?? parseInt(manualTotal.value, 10);
  if (Number.isNaN(base)) {
    emit('update:modelValue', '');
    return;
  }
  const total = base + modifier.value;
  const modNote = modifier.value !== 0 ? ` + ${modifier.value} = ${total}` : '';
  const source = roll.value !== null ? `[${roll.value}]` : `(manual ${base})`;
  if (isTotalMode.value) {
    emit('update:modelValue', `1d${sides.value}: ${source}${modNote} → total ${total}`);
  } else {
    emit('update:modelValue', `1d${sides.value}: ${source}${modNote}`);
  }
}

watch(modifier, emitResult);

function onManualChange() {
  const n = parseInt(manualTotal.value, 10);
  if (isNaN(n) || n < 1 || n > sides.value) {
    emit('update:modelValue', '');
    return;
  }
  roll.value = null;
  emitResult();
}
</script>

<template>
  <div class="dice-roller d20-check-mode">
    <div class="dr-header">
      <span class="dr-label">🎲 {{ label }}</span>
      <div class="dr-mode-toggle" role="group" aria-label="Roll mode">
        <button type="button" class="dr-mode-btn" :class="{ active: mode === 'auto' }" @click="setMode('auto')">Auto Roll</button>
        <button type="button" class="dr-mode-btn" :class="{ active: mode === 'manual' }" @click="setMode('manual')">Manual</button>
      </div>
    </div>

    <div class="dr-pool-summary">
      <span class="dr-dice-count">1d{{ sides }}</span>
      <ul v-if="poolBreakdown.length" class="dr-breakdown-list">
        <li v-for="(note, i) in poolBreakdown" :key="i">{{ note }}</li>
      </ul>
    </div>

    <p v-if="isTotalMode" class="dr-success-hint">Roll 1d{{ sides }} and report the total value of the die (plus any modifier).</p>
    <p v-else-if="successRule" class="dr-success-hint">{{ successRule }}</p>
    <p v-else class="dr-success-hint">Roll 1d{{ sides }} and add modifiers; compare the total to the target DC or AC.</p>

    <div v-if="mode === 'auto'" class="dr-panel">
      <template v-if="!hasRolled">
        <button type="button" class="btn dr-roll-btn" @click="autoRoll">Roll 1d{{ sides }}</button>
      </template>
      <template v-else>
        <div class="dr-pip-group">
          <span class="dr-pip-label">Die result</span>
          <div class="dr-pips">
            <span class="dr-pip d20-pip">{{ roll }}</span>
          </div>
        </div>
        <div class="dr-result-line">
          <span class="dr-total">= {{ displayTotal }}</span>
          <span v-if="modifier !== 0" class="dr-modifier-note">({{ roll }} {{ modifier > 0 ? '+' : '' }}{{ modifier }})</span>
        </div>
        <div class="dr-actions">
          <button type="button" class="btn ghost sm" @click="autoRoll">Re-roll</button>
          <button type="button" class="btn ghost sm" @click="clear">Clear</button>
        </div>
      </template>
    </div>

    <div v-else class="dr-panel dr-manual">
      <p class="dr-instruction">Roll <strong>1d{{ sides }}</strong> with physical dice, then enter the result:</p>
      <div class="dr-manual-row">
        <input
          v-model="manualTotal"
          type="number"
          :min="1"
          :max="sides"
          placeholder="Result…"
          class="dr-input"
          @input="onManualChange"
        />
      </div>
    </div>

    <div v-if="modelValue" class="dr-result-preview">{{ modelValue }}</div>
  </div>
</template>

<style src="~/assets/css/dice-roller.css"></style>
<style scoped>
.d20-pip {
  width: 2.6rem;
  height: 2.6rem;
  font-size: 1.2rem;
}
</style>
