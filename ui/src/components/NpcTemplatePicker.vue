<script setup lang="ts">
import type { RulesetDefinition } from '~/types/api';
import { groupNpcTemplates, parseNpcTemplates } from '~/utils/npcTemplates';

interface Props {
  definition: RulesetDefinition | null;
  modelValue: string;
  disabled?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: '',
  disabled: false,
});

const emit = defineEmits<{
  'update:modelValue': [value: string];
}>();

const templateGroups = computed(() => groupNpcTemplates(parseNpcTemplates(props.definition)));

const selectedTemplate = computed(() =>
  parseNpcTemplates(props.definition).find(template => template.key === props.modelValue) ?? null,
);

function onChange(event: Event) {
  emit('update:modelValue', (event.target as HTMLSelectElement).value);
}
</script>

<template>
  <div v-if="templateGroups.length" class="npc-template-picker">
    <label>
      Pregenerated template
      <select :value="modelValue" :disabled="disabled" @change="onChange">
        <option value="">Custom (manual entry)</option>
        <optgroup v-for="group in templateGroups" :key="group.label" :label="group.label">
          <option v-for="template in group.templates" :key="template.key" :value="template.key">
            {{ template.label }}{{ template.kind === 'Monster' ? ' (Monster)' : '' }}
          </option>
        </optgroup>
      </select>
    </label>
    <p v-if="selectedTemplate?.description" class="text-xs muted template-hint">
      {{ selectedTemplate.description }}
    </p>
  </div>
</template>

<style scoped>
.npc-template-picker {
  margin-bottom: 0.75rem;
}

.template-hint {
  margin: 0.35rem 0 0;
}
</style>
