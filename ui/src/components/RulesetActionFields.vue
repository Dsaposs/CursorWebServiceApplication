<script setup lang="ts">
import type { RulesetActionMode, StatCheckOption } from '~/composables/useRulesetActionChooser';
import type { RulesetActionDefinition } from '~/types/api';
import { groupActions } from '~/utils/rulesets';

interface ActionDetail {
  dice: string;
  attribute: string;
  skill: string;
  modifiers?: string;
  successRule: string;
}

interface Props {
  actionMode: RulesetActionMode;
  selectedActionKey: string;
  selectedStatKey: string;
  availableActions: RulesetActionDefinition[];
  availableStatChecks: StatCheckOption[];
  selectedActionDetail: ActionDetail | null;
  selectedStatDetail: { actionText: string; rollSummary: string } | null;
  disabled?: boolean;
  rollTypeLabel?: string;
  showModifiers?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  disabled: false,
  rollTypeLabel: 'Roll Type',
  showModifiers: false,
});

const emit = defineEmits<{
  'update:actionMode': [value: RulesetActionMode];
  'update:selectedActionKey': [value: string];
  'update:selectedStatKey': [value: string];
}>();

const grouped = computed(() => groupActions(props.availableActions));

const showRollTypePicker = computed(() =>
  props.availableActions.length > 0 && props.availableStatChecks.length > 0,
);

const actionModeModel = computed({
  get: () => props.actionMode,
  set: value => emit('update:actionMode', value),
});

const selectedActionKeyModel = computed({
  get: () => props.selectedActionKey,
  set: value => emit('update:selectedActionKey', value),
});

const selectedStatKeyModel = computed({
  get: () => props.selectedStatKey,
  set: value => emit('update:selectedStatKey', value),
});
</script>

<template>
  <label v-if="showRollTypePicker">
    {{ rollTypeLabel }}
    <select v-model="actionModeModel" :disabled="disabled">
      <option value="action">Action</option>
      <option value="stat-check">Stat Check</option>
    </select>
  </label>

  <template v-if="actionMode === 'action'">
    <label>
      Action
      <select v-model="selectedActionKeyModel" required :disabled="disabled">
        <option value="">Choose an action</option>
        <optgroup v-if="grouped.normal.length" label="Normal Actions">
          <option v-for="action in grouped.normal" :key="action.key" :value="action.key">
            {{ action.label }}
          </option>
        </optgroup>
        <optgroup v-if="grouped.combat.length" label="Combat Actions">
          <option v-for="action in grouped.combat" :key="action.key" :value="action.key">
            {{ action.label }}
          </option>
        </optgroup>
      </select>
    </label>

    <div v-if="selectedActionDetail" class="alert info">
      <strong>{{ selectedActionDetail.dice }}</strong>
      <p class="text-sm muted">
        Roll {{ selectedActionDetail.attribute }} + {{ selectedActionDetail.skill }}.
        <template v-if="showModifiers && selectedActionDetail.modifiers">
          Modifiers: {{ selectedActionDetail.modifiers }}.
        </template>
      </p>
      <p class="text-sm">{{ selectedActionDetail.successRule }}</p>
    </div>
  </template>

  <template v-else-if="actionMode === 'stat-check'">
    <label>
      Stat
      <select v-model="selectedStatKeyModel" required :disabled="disabled">
        <option value="">Choose a stat</option>
        <optgroup label="Skills">
          <option
            v-for="stat in availableStatChecks.filter(s => s.type === 'skill')"
            :key="stat.key"
            :value="stat.key"
          >
            {{ stat.label }}
          </option>
        </optgroup>
        <optgroup label="Attributes">
          <option
            v-for="stat in availableStatChecks.filter(s => s.type === 'attribute')"
            :key="stat.key"
            :value="stat.key"
          >
            {{ stat.label }}
          </option>
        </optgroup>
      </select>
    </label>

    <div v-if="selectedStatDetail" class="alert info">
      <strong>{{ selectedStatDetail.actionText }}</strong>
      <p class="text-sm muted">{{ selectedStatDetail.rollSummary }}</p>
    </div>
  </template>
</template>
