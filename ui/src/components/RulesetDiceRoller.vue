<script setup lang="ts">
import type { DiceRollContext } from '~/dice-rollers/types';
import { getDiceRoller } from '~/dice-rollers/registry';

interface Props {
  context: DiceRollContext | null;
  modelValue?: string;
  showModifier?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: '',
  showModifier: false,
});

const emit = defineEmits<{ 'update:modelValue': [value: string] }>();

const roller = computed(() =>
  props.context ? getDiceRoller(props.context.rollerKey) : null,
);
</script>

<template>
  <component
    :is="roller?.component"
    v-if="context && roller"
    :key="context.rollerKey"
    v-bind="{
      config: context.config,
      label: context.label,
      poolBreakdown: context.poolBreakdown,
      successRule: context.successRule,
      resultKind: context.resultKind ?? 'PassFail',
      showModifier,
    }"
    :model-value="modelValue"
    @update:model-value="emit('update:modelValue', $event)"
  />
</template>
