<script setup lang="ts">
import type { D6PoolRollConfig, RollResultKind } from '~/dice-rollers/types';
import { classifyRolls, rollDice } from '~/utils/dice';
import { useDiceMode } from '~/composables/useDiceMode';

interface Props {
  config: D6PoolRollConfig;
  poolBreakdown?: string[];
  label?: string;
  successRule?: string;
  resultKind?: RollResultKind;
  modelValue?: string;
  showModifier?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  poolBreakdown: () => [],
  label: 'Dice Roll',
  resultKind: 'PassFail',
  modelValue: '',
  showModifier: false,
});

const isTotalMode = computed(() => props.resultKind === 'Total');

const emit = defineEmits<{ 'update:modelValue': [value: string] }>();
const { mode, setMode } = useDiceMode();

const sides = computed(() => props.config.sides);
const successTarget = computed(() => props.config.successTarget);
const stressDiceCount = computed(() => props.config.stressDiceCount ?? 0);
const totalBaseDice = computed(() => Math.max(1, props.config.baseDiceCount));
const totalDice = computed(() => totalBaseDice.value + stressDiceCount.value);

const baseRolls = ref<number[]>([]);
const stressRolls = ref<number[]>([]);
const hasRolled = ref(false);
const manualSuccesses = ref('');
const modifier = ref(0);

const classification = computed(() =>
  hasRolled.value ? classifyRolls(baseRolls.value, stressRolls.value, successTarget.value) : null,
);
const adjustedSuccesses = computed(() =>
  classification.value ? classification.value.totalSuccesses + modifier.value : 0,
);

watch(
  [
    () => props.config.baseDiceCount,
    () => props.config.stressDiceCount,
    () => props.config.successTarget,
    () => props.config.sides,
  ],
  clear,
);
watch(mode, clear);

function clear() {
  baseRolls.value = [];
  stressRolls.value = [];
  hasRolled.value = false;
  manualSuccesses.value = '';
  modifier.value = 0;
  emit('update:modelValue', '');
}

function autoRoll() {
  baseRolls.value = rollDice(totalBaseDice.value, sides.value);
  stressRolls.value = stressDiceCount.value > 0 ? rollDice(stressDiceCount.value, sides.value) : [];
  hasRolled.value = true;
  emit('update:modelValue', buildResultString());
}

function buildResultString(): string {
  const result = classification.value;
  if (!result) return '';
  const panic = result.panicDice.length ? ` ⚠️ PANIC (${result.panicDice.length} stress 1s)` : '';
  const stressNote = stressRolls.value.length
    ? ` [base: ${baseRolls.value.join(',')} | stress: ${stressRolls.value.join(',')}]`
    : ` [${[...baseRolls.value, ...stressRolls.value].join(', ')}]`;

  if (isTotalMode.value) {
    const allRolls = [...baseRolls.value, ...stressRolls.value];
    const rawTotal = allRolls.reduce((sum, value) => sum + value, 0);
    const total = rawTotal + modifier.value;
    const modNote = modifier.value !== 0 ? ` (${rawTotal} + ${modifier.value} modifier)` : '';
    return `${totalBaseDice.value + stressDiceCount.value}d${sides.value}${stressNote} → total ${total}${modNote}${panic}`;
  }

  const { totalSuccesses } = result;
  const adj = adjustedSuccesses.value;
  const modNote = modifier.value !== 0
    ? `${totalSuccesses} + ${modifier.value} modifier = ${adj} success${adj !== 1 ? 'es' : ''}`
    : `${totalSuccesses} success${totalSuccesses !== 1 ? 'es' : ''}`;
  return `${totalBaseDice.value + stressDiceCount.value}d${sides.value}${stressNote} → ${modNote}${panic}`;
}

watch(modifier, () => {
  if (hasRolled.value) emit('update:modelValue', buildResultString());
  else if (manualSuccesses.value) onManualChange();
});

function onManualChange() {
  const n = parseInt(manualSuccesses.value, 10);
  if (isNaN(n) || n < 0) {
    emit('update:modelValue', '');
    return;
  }
  if (isTotalMode.value) {
    const total = n + modifier.value;
    const modNote = modifier.value !== 0 ? ` (${n} + ${modifier.value} modifier)` : '';
    emit('update:modelValue', `${totalDice.value}d${sides.value} → total ${total}${modNote} (manual)`);
    return;
  }
  const adj = n + modifier.value;
  const modNote = modifier.value !== 0 ? ` + ${modifier.value} modifier = ${adj}` : '';
  emit('update:modelValue', `${totalDice.value}d${sides.value}: ${n}${modNote} success${adj !== 1 ? 'es' : ''} (manual)`);
}

function isSuccess(value: number) {
  return value >= successTarget.value;
}

function isPanic(value: number, isStress: boolean) {
  return isStress && value === 1;
}
</script>

<template>
  <div class="dice-roller pool-mode">
    <!-- Header row -->
    <div class="dr-header">
      <span class="dr-label">🎲 {{ label }}</span>
      <div class="dr-mode-toggle" role="group" aria-label="Roll mode">
        <button type="button" class="dr-mode-btn" :class="{ active: mode === 'auto' }" :aria-pressed="mode === 'auto'" @click="setMode('auto')">Auto Roll</button>
        <button type="button" class="dr-mode-btn" :class="{ active: mode === 'manual' }" :aria-pressed="mode === 'manual'" @click="setMode('manual')">Manual</button>
      </div>
    </div>

    <!-- Pool summary line -->
    <div class="dr-pool-summary">
      <span class="dr-dice-count">
        {{ totalBaseDice }}d{{ sides }}
        <template v-if="stressDiceCount > 0">
          <span class="dr-plus"> + </span>
          <span class="dr-stress-count">{{ stressDiceCount }} stress</span>
        </template>
      </span>
      <ul v-if="poolBreakdown.length" class="dr-breakdown-list">
        <li v-for="(note, i) in poolBreakdown" :key="i">{{ note }}</li>
      </ul>
    </div>

    <!-- Success rule hint (pool mode only) -->
    <p v-if="isTotalMode" class="dr-success-hint">
      Add up the face values on every die you roll
      <template v-if="stressDiceCount > 0">· stress dice showing <strong>1</strong> still trigger a panic check</template>
    </p>
    <p v-else class="dr-success-hint">
      Count each <strong>{{ successTarget }}</strong> as one success
      <template v-if="stressDiceCount > 0">· stress dice: <strong>1 = panic check</strong></template>
    </p>
    <p v-if="successRule" class="dr-success-hint">{{ successRule }}</p>

    <!-- AUTO ROLL panel -->
    <div v-if="mode === 'auto'" class="dr-panel">
      <template v-if="!hasRolled">
        <button type="button" class="btn dr-roll-btn" @click="autoRoll">
          Roll {{ totalBaseDice }}d{{ sides }}<template v-if="stressDiceCount > 0"> + {{ stressDiceCount }} stress</template>
        </button>
      </template>

      <template v-else>
        <div class="dr-pip-group">
          <span class="dr-pip-label">Base dice</span>
          <div class="dr-pips">
            <span v-for="(r, i) in baseRolls" :key="`b${i}`" class="dr-pip" :class="{ success: isSuccess(r) }">{{ r }}</span>
          </div>
        </div>
        <div v-if="stressRolls.length" class="dr-pip-group">
          <span class="dr-pip-label stress">Stress dice</span>
          <div class="dr-pips">
            <span
              v-for="(r, i) in stressRolls"
              :key="`s${i}`"
              class="dr-pip stress-pip"
              :class="{ success: isSuccess(r), panic: isPanic(r, true) }"
            >{{ r }}</span>
          </div>
        </div>
        <div v-if="classification" class="dr-result-line">
          <template v-if="isTotalMode">
            <span class="dr-successes">
              Total {{ [...baseRolls, ...stressRolls].reduce((s, v) => s + v, 0) + modifier }}
            </span>
            <span v-if="modifier !== 0" class="dr-modifier-note">
              (dice {{ [...baseRolls, ...stressRolls].reduce((s, v) => s + v, 0) }} {{ modifier > 0 ? '+' : '' }}{{ modifier }})
            </span>
          </template>
          <template v-else>
            <span class="dr-successes" :class="{ zero: adjustedSuccesses === 0 }">
              {{ adjustedSuccesses }} success{{ adjustedSuccesses !== 1 ? 'es' : '' }}
            </span>
            <span v-if="modifier !== 0" class="dr-modifier-note">
              ({{ classification.totalSuccesses }} raw {{ modifier > 0 ? '+' : '' }}{{ modifier }})
            </span>
          </template>
          <span v-if="classification.panicDice.length" class="dr-panic-warning">
            ⚠️ Panic! Roll on the panic table ({{ classification.panicDice.length }} stress die showed 1)
          </span>
        </div>
        <div v-if="showModifier" class="dr-modifier-row">
          <label class="dr-modifier-label" :for="`mod-${label}`">Modifier</label>
          <div class="dr-modifier-controls">
            <button type="button" class="dr-mod-btn" @click="modifier--">−</button>
            <input :id="`mod-${label}`" v-model.number="modifier" type="number" class="dr-mod-input" aria-label="Roll modifier" />
            <button type="button" class="dr-mod-btn" @click="modifier++">+</button>
          </div>
        </div>
        <div class="dr-actions">
          <button type="button" class="btn ghost sm" @click="autoRoll">Re-roll</button>
          <button type="button" class="btn ghost sm" @click="clear">Clear</button>
        </div>
      </template>
    </div>

    <!-- MANUAL ROLL panel -->
    <div v-else class="dr-panel dr-manual">

        <p class="dr-instruction">
          Roll <strong>{{ totalBaseDice }}d{{ sides }}</strong>
          <template v-if="stressDiceCount > 0"> (plus <strong>{{ stressDiceCount }} stress dice</strong>)</template>
          with physical dice, then enter
          <template v-if="isTotalMode">the <strong>sum of all dice</strong>:</template>
          <template v-else>the number of <strong>{{ successTarget }}s</strong> rolled:</template>
        </p>
        <div class="dr-manual-row">
          <input
            v-model="manualSuccesses"
            type="number"
            min="0"
            :max="isTotalMode ? totalDice * sides : totalDice"
            :placeholder="isTotalMode ? 'Dice total…' : `# of ${successTarget}s…`"
            class="dr-input"
            @input="onManualChange"
          />
          <span class="dr-input-unit">{{ isTotalMode ? 'total' : 'successes' }}</span>
        </div>
        <p v-if="stressDiceCount > 0" class="dr-instruction" style="margin-top: 0.25rem; font-size: 0.8rem;">
          Also note any <strong>1s</strong> on stress dice — those trigger a panic check.
        </p>

      <!-- Modifier (manual mode, DM resolution only) -->
      <div v-if="showModifier && manualSuccesses" class="dr-modifier-row">
        <label class="dr-modifier-label" :for="`mod-m-${label}`">Modifier</label>
        <div class="dr-modifier-controls">
          <button type="button" class="dr-mod-btn" @click="modifier--; onManualChange()">−</button>
          <input
            :id="`mod-m-${label}`"
            v-model.number="modifier"
            type="number"
            class="dr-mod-input"
            aria-label="Roll modifier"
            @input="onManualChange"
          />
          <button type="button" class="dr-mod-btn" @click="modifier++; onManualChange()">+</button>
        </div>
      </div>
    </div>

    <!-- Emitted result preview -->
    <div v-if="modelValue" class="dr-result-preview">{{ modelValue }}</div>
  </div>
</template>


<style src="~/assets/css/dice-roller.css"></style>
