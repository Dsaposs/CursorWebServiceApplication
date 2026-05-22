<script setup lang="ts">
import type { RulesetDefinition, RulesetItemDefinition } from '~/types/api';
import { attributeModifier, buildRollResult, parseDiceNotation, rollDice } from '~/utils/dice';
import { describeDamageRoll } from '~/utils/items';

interface Props {
  damageRoll: NonNullable<RulesetItemDefinition['damageRoll']>;
  definition: RulesetDefinition;
  attributes: Record<string, number>;
  modelValue?: string;
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: '',
});

const emit = defineEmits<{ 'update:modelValue': [value: string] }>();

const summary = computed(() =>
  describeDamageRoll(props.damageRoll, props.definition, props.attributes),
);

const hasResult = computed(() => Boolean(props.modelValue?.trim()));

function rollDamage() {
  const parsed = parseDiceNotation(props.damageRoll.notation);
  if (!parsed) {
    emit('update:modelValue', `${props.damageRoll.notation}: invalid notation`);
    return;
  }

  const rolls = rollDice(parsed.count, parsed.sides);
  let total = rolls.reduce((sum, value) => sum + value, 0);

  if (props.damageRoll.bonusAttribute) {
    total += attributeModifier(props.attributes[props.damageRoll.bonusAttribute] ?? 10);
  }
  if (props.damageRoll.flatBonus) total += props.damageRoll.flatBonus;

  const base = buildRollResult(rolls, parsed.notation);
  const bonusNote = total !== base.total ? ` → ${total} total` : '';
  emit('update:modelValue', `🎲 Damage: ${base.summary}${bonusNote}`);
}

function clear() {
  emit('update:modelValue', '');
}
</script>

<template>
  <div class="dice-roller damage-roll-mode">
    <div class="dr-header">
      <span class="dr-label">Damage roll</span>
    </div>

    <template v-if="!hasResult">
      <p v-if="damageRoll.description" class="dr-success-hint">{{ damageRoll.description }}</p>
      <p v-else class="dr-success-hint">Roll {{ summary }} on a hit.</p>
      <div class="dr-panel">
        <button type="button" class="btn dr-roll-btn" @click="rollDamage">Roll damage</button>
      </div>
    </template>

    <div v-else class="damage-roll-result-card">
      <span class="damage-roll-result-label">Damage result</span>
      <p class="damage-roll-result-value">{{ modelValue }}</p>
      <div class="damage-roll-result-actions">
        <button type="button" class="btn ghost sm" @click="rollDamage">Re-roll</button>
        <button type="button" class="btn ghost sm" @click="clear">Clear</button>
      </div>
    </div>
  </div>
</template>

<style src="~/assets/css/dice-roller.css"></style>
