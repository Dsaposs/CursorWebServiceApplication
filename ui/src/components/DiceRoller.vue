<script setup lang="ts">
/**
 * @deprecated Use RulesetDiceRoller with a DiceRollContext from buildDiceRollContext().
 */
import type { D6PoolRollConfig } from '~/dice-rollers/types';
import D6PoolRoller from '~/dice-rollers/d6-pool/D6PoolRoller.vue';

interface Props {
  baseDiceCount: number;
  stressDiceCount?: number;
  sides?: number;
  successTarget?: number;
  poolBreakdown?: string[];
  label?: string;
  modelValue?: string;
  showModifier?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  stressDiceCount: 0,
  sides: 6,
  poolBreakdown: () => [],
  label: 'Dice Roll',
  modelValue: '',
  showModifier: false,
});

defineEmits<{ 'update:modelValue': [value: string] }>();

const poolConfig = computed<D6PoolRollConfig>(() => ({
  kind: 'd6-pool',
  baseDiceCount: props.baseDiceCount,
  stressDiceCount: props.stressDiceCount ?? 0,
  sides: props.sides ?? 6,
  successTarget: props.successTarget ?? 6,
}));
</script>

<template>
  <D6PoolRoller
    :config="poolConfig"
    :label="label"
    :pool-breakdown="poolBreakdown"
    :model-value="modelValue"
    :show-modifier="showModifier"
    @update:model-value="$emit('update:modelValue', $event)"
  />
</template>
