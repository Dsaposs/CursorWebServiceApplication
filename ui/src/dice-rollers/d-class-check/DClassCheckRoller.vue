<script setup lang="ts">
import type { DClassCheckRollConfig, RollResultKind } from '~/dice-rollers/types';
import { rollDice } from '~/utils/dice';
import { useDiceMode } from '~/composables/useDiceMode';

interface Props {
  config: DClassCheckRollConfig;
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

const emit = defineEmits<{ 'update:modelValue': [value: string] }>();
const { mode, setMode } = useDiceMode();

const sides = computed(() => props.config.sides);
const modifier = computed(() => props.config.modifier ?? 0);
const bonusDiceCount = computed(() => props.config.bonusDiceCount ?? 0);
const bonusDiceLabel = computed(() => props.config.bonusDiceLabel || 'Bonus');
const dc = computed(() => props.config.difficultyClass ?? 0);

const mainRoll = ref<number | null>(null);
const bonusRolls = ref<number[]>([]);
const hasRolled = ref(false);
const manualTotal = ref('');

const bonusTotal = computed(() => bonusRolls.value.reduce((s, v) => s + v, 0));
const grandTotal = computed(() => (mainRoll.value ?? 0) + modifier.value + bonusTotal.value);
const isHit = computed(() => (dc.value > 0 && mainRoll.value !== null) ? grandTotal.value >= dc.value : null);
const manualIsHit = computed(() => {
  const n = parseInt(manualTotal.value, 10);
  return dc.value > 0 && !isNaN(n) ? n >= dc.value : null;
});

watch(
  [() => props.config.sides, () => props.config.modifier, () => props.config.bonusDiceCount],
  clear,
);
watch(mode, clear);

function clear() {
  mainRoll.value = null;
  bonusRolls.value = [];
  hasRolled.value = false;
  manualTotal.value = '';
  emit('update:modelValue', '');
}

function autoRoll() {
  mainRoll.value = rollDice(1, sides.value)[0];
  bonusRolls.value = bonusDiceCount.value > 0 ? rollDice(bonusDiceCount.value, 4) : [];
  hasRolled.value = true;
  emit('update:modelValue', buildResultString());
}

function buildResultString(): string {
  const die = mainRoll.value ?? 0;
  const modPart = modifier.value !== 0 ? ` + ${modifier.value}` : '';
  const bonusPart = bonusRolls.value.length
    ? ` + ${bonusDiceLabel.value} [${bonusRolls.value.join(',')}] (+${bonusTotal.value})`
    : '';
  return `1d${sides.value}: [${die}]${modPart}${bonusPart} = ${grandTotal.value}`;
}

function onManualChange() {
  const n = parseInt(manualTotal.value, 10);
  if (isNaN(n) || n < 1) {
    emit('update:modelValue', '');
    return;
  }
  emit('update:modelValue', `1d${sides.value}: (manual) = ${n}`);
}

/** Breakdown lines that describe the stat modifier (exclude bonus-dice entries). */
const statBreakdown = computed(() =>
  props.poolBreakdown.filter(p => !p.toLowerCase().includes('stress') && !p.toLowerCase().includes(bonusDiceLabel.value.toLowerCase())),
);
</script>

<template>
  <div class="dice-roller d-class-check-mode">
    <!-- Header -->
    <div class="dr-header">
      <span class="dr-label">🎲 {{ label }}</span>
      <div class="dr-mode-toggle" role="group" aria-label="Roll mode">
        <button type="button" class="dr-mode-btn" :class="{ active: mode === 'auto' }" :aria-pressed="mode === 'auto'" @click="setMode('auto')">Auto Roll</button>
        <button type="button" class="dr-mode-btn" :class="{ active: mode === 'manual' }" :aria-pressed="mode === 'manual'" @click="setMode('manual')">Manual</button>
      </div>
    </div>

    <!-- Roll formula summary -->
    <div class="dr-pool-summary">
      <span class="dr-dice-count">
        1d{{ sides }}
        <template v-if="modifier !== 0"> + {{ modifier }}</template>
        <template v-if="bonusDiceCount > 0">
          <span class="dcc-bonus-formula"> + {{ bonusDiceCount }}d4 {{ bonusDiceLabel }}</span>
        </template>
      </span>
      <ul v-if="poolBreakdown.length" class="dr-breakdown-list">
        <li v-for="(note, i) in poolBreakdown" :key="i">{{ note }}</li>
      </ul>
    </div>

    <!-- Success hint / DC -->
    <p v-if="dc > 0" class="dr-success-hint">
      Target: <strong>{{ dc }}+</strong>
      <template v-if="successRule"> · {{ successRule }}</template>
    </p>
    <p v-else-if="successRule" class="dr-success-hint">{{ successRule }}</p>

    <!-- AUTO ROLL panel -->
    <div v-if="mode === 'auto'" class="dr-panel">
      <!-- Pre-roll -->
      <template v-if="!hasRolled">
        <button type="button" class="btn dr-roll-btn" @click="autoRoll">
          Roll 1d{{ sides }}
          <template v-if="bonusDiceCount > 0"> + {{ bonusDiceCount }}d4</template>
        </button>
      </template>

      <!-- Post-roll -->
      <template v-else>
        <!-- Class die result -->
        <div class="dr-pip-group">
          <span class="dr-pip-label">Class Die (1d{{ sides }})</span>
          <div class="dr-pips">
            <span class="dr-pip dcc-main-pip">{{ mainRoll }}</span>
          </div>
        </div>

        <!-- Flat stat modifier -->
        <div v-if="modifier !== 0" class="dcc-modifier-line">
          <span class="dcc-modifier-value">+ {{ modifier }}</span>
          <span v-if="statBreakdown.length" class="dcc-modifier-note">
            ({{ statBreakdown.join(' + ') }})
          </span>
        </div>

        <!-- Bonus d4 dice (e.g. Grief) -->
        <div v-if="bonusRolls.length" class="dr-pip-group" style="margin-top: 0.5rem;">
          <span class="dr-pip-label dcc-bonus-label">{{ bonusDiceLabel }} ({{ bonusDiceCount }}d4)</span>
          <div class="dr-pips">
            <span
              v-for="(r, i) in bonusRolls"
              :key="`b${i}`"
              class="dr-pip dcc-bonus-pip"
            >{{ r }}</span>
          </div>
          <span class="dcc-bonus-subtotal">+{{ bonusTotal }} total</span>
        </div>

        <!-- Grand total + hit/miss -->
        <div class="dcc-result-row">
          <span class="dcc-total-label">Total</span>
          <span class="dcc-total-value">{{ grandTotal }}</span>
          <template v-if="dc > 0">
            <span class="badge" :class="isHit ? 'pass' : 'fail'">
              {{ isHit ? '✓ Hit' : '✗ Miss' }}
            </span>
            <span class="dcc-dc-note">DC {{ dc }}</span>
          </template>
        </div>

        <!-- Re-roll / clear -->
        <div class="dr-actions">
          <button type="button" class="btn ghost sm" @click="autoRoll">Re-roll</button>
          <button type="button" class="btn ghost sm" @click="clear">Clear</button>
        </div>
      </template>
    </div>

    <!-- MANUAL ROLL panel -->
    <div v-else class="dr-panel dr-manual">
      <p class="dr-instruction">
        Roll <strong>1d{{ sides }}</strong>
        <template v-if="modifier !== 0">, add <strong>{{ modifier }}</strong></template>
        <template v-if="bonusDiceCount > 0">
          , plus <strong>{{ bonusDiceCount }} {{ bonusDiceLabel }} d4{{ bonusDiceCount !== 1 ? 's' : '' }}</strong>
        </template>
        , then enter the total:
      </p>
      <div class="dr-manual-row">
        <input
          v-model="manualTotal"
          type="number"
          min="1"
          placeholder="Total…"
          class="dr-input"
          @input="onManualChange"
        />
        <span class="dr-input-unit">total</span>
      </div>
      <p v-if="manualIsHit !== null" class="dcc-manual-result">
        <span class="badge" :class="manualIsHit ? 'pass' : 'fail'">
          {{ manualIsHit ? '✓ Hit' : '✗ Miss' }} — DC {{ dc }}
        </span>
      </p>
    </div>

    <!-- Emitted result preview -->
    <div v-if="modelValue" class="dr-result-preview">{{ modelValue }}</div>
  </div>
</template>

<style src="~/assets/css/dice-roller.css"></style>
<style scoped>
/* Class die — larger pip with accent border */
.dcc-main-pip {
  width: 2.8rem;
  height: 2.8rem;
  font-size: 1.25rem;
  border: 2px solid var(--accent, #a060e0);
  background: var(--accentDim, rgba(160, 96, 224, 0.12));
  color: var(--inkBright, inherit);
}

/* Bonus dice (grief / stress d4s) — smaller, secondary colour */
.dcc-bonus-pip {
  width: 1.75rem;
  height: 1.75rem;
  font-size: 0.85rem;
  border: 1.5px solid var(--secondary, #7040c0);
  background: var(--secondaryDim, rgba(112, 64, 192, 0.10));
  color: var(--inkBright, inherit);
}

.dcc-bonus-label {
  color: var(--secondary, inherit);
}

.dcc-bonus-formula {
  color: var(--secondary, inherit);
  font-size: 0.9em;
}

.dcc-bonus-subtotal {
  display: block;
  font-size: 0.78rem;
  color: var(--secondary, var(--muted-light));
  margin-top: 0.2rem;
  font-style: italic;
}

/* Flat stat modifier line */
.dcc-modifier-line {
  display: flex;
  align-items: baseline;
  gap: 0.4rem;
  font-size: 0.9rem;
  margin: 0.2rem 0;
  color: var(--ink);
}

.dcc-modifier-value {
  font-weight: 600;
}

.dcc-modifier-note {
  font-size: 0.78rem;
  color: var(--muted-light);
}

/* Grand total row */
.dcc-result-row {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  margin-top: 0.6rem;
  padding-top: 0.55rem;
  border-top: 1px solid var(--border);
}

.dcc-total-label {
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--muted-light);
}

.dcc-total-value {
  font-size: 1.7rem;
  font-weight: 700;
  color: var(--inkBright, var(--ink));
  line-height: 1;
}

.dcc-dc-note {
  font-size: 0.75rem;
  color: var(--muted-light);
}

/* Manual hit/miss badge */
.dcc-manual-result {
  margin-top: 0.5rem;
}
</style>
