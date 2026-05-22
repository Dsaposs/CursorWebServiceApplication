<script setup lang="ts">
import type { CharacterResponse, NpcResponse, RulesetDefinition } from '~/types/api';
import { useRulesetActionChooser } from '~/composables/useRulesetActionChooser';
import { useActionTarget } from '~/composables/useActionTarget';
import { parseInventory } from '~/utils/inventory';
import { isCombatAction, actionRequiresTarget, findRulesetAction } from '~/utils/rulesets';

interface Props {
  rulesetDefinition: RulesetDefinition | null;
  classKey?: string | null;
  inventoryJson?: string | null;
  characters?: CharacterResponse[];
  npcs?: NpcResponse[];
  /** When true, combat actions require a valid target before submitting. */
  requireTargetForCombat?: boolean;
  isSubmitting?: boolean;
  disabled?: boolean;
}

interface ActionFormPayload {
  actionKey?: string;
  actionText: string;
  targetCharacterId?: string;
  targetNpcId?: string;
  targetName?: string;
}

const props = withDefaults(defineProps<Props>(), {
  classKey: null,
  inventoryJson: null,
  characters: () => [],
  npcs: () => [],
  requireTargetForCombat: true,
  isSubmitting: false,
  disabled: false,
});

const emit = defineEmits<{
  submit: [payload: ActionFormPayload];
  cancel: [];
}>();

const inventory = computed(() => parseInventory(props.inventoryJson));
const definitionRef = computed(() => props.rulesetDefinition);
const classKeyRef = computed(() => props.classKey);
const isEnabled = computed(() => !props.disabled);

const {
  actionMode,
  selectedActionKey,
  selectedStatKey,
  availableActions,
  availableStatChecks,
  selectedActionDetail,
  selectedStatDetail,
  resolvedActionText,
  resetSelection,
  resetForm,
  buildSubmitPayload,
} = useRulesetActionChooser(definitionRef, classKeyRef, inventory, isEnabled);

const selectedAction = computed(() =>
  findRulesetAction(props.rulesetDefinition, selectedActionKey.value),
);

const isCombatActionSelected = computed(() =>
  selectedAction.value ? isCombatAction(selectedAction.value) : false,
);

// Target picker
const {
  selection: targetSelection,
  otherText: targetOtherText,
  sortedCharacters,
  sortedNpcs,
  isValid: targetIsValid,
  reset: resetTarget,
  toSubmitFields: targetToSubmitFields,
} = useActionTarget(() => props.characters, () => props.npcs);

const needsTarget = computed(() =>
  props.requireTargetForCombat && selectedAction.value
    ? actionRequiresTarget(selectedAction.value)
    : false,
);

const hasValidTarget = computed(() => {
  if (!needsTarget.value) return true;
  if (!targetIsValid({ required: true })) return false;
  const fields = targetToSubmitFields();
  return Boolean(fields.targetCharacterId || fields.targetNpcId || fields.targetName);
});

const canSubmit = computed(() => {
  if (props.disabled || props.isSubmitting) return false;
  if (!resolvedActionText.value.trim()) return false;
  if (needsTarget.value && !hasValidTarget.value) return false;
  return true;
});

function handleSubmit() {
  if (!canSubmit.value) return;
  const base = buildSubmitPayload();
  if (!base) return;

  const target = (needsTarget.value || targetSelection.value) ? targetToSubmitFields() : {};

  emit('submit', {
    actionKey: base.actionKey,
    actionText: base.actionText,
    ...target,
  });
}

function reset() {
  resetForm();
  resetTarget();
}

defineExpose({ reset });
</script>

<template>
  <form class="action-form" @submit.prevent="handleSubmit">
    <RulesetActionFields
      v-model:action-mode="actionMode"
      v-model:selected-action-key="selectedActionKey"
      v-model:selected-stat-key="selectedStatKey"
      :available-actions="availableActions"
      :available-stat-checks="availableStatChecks"
      :selected-action-detail="selectedActionDetail"
      :selected-stat-detail="selectedStatDetail"
      :disabled="disabled"
    />

    <!-- Target picker — required for combat actions, optional for others -->
    <div v-if="isCombatActionSelected || (actionMode === 'action' && selectedActionKey)" class="action-form-target">
      <label>
        Target
        <select v-model="targetSelection" :disabled="disabled">
          <option value="">No target</option>
          <optgroup v-if="sortedCharacters.length" label="Characters">
            <option
              v-for="character in sortedCharacters"
              :key="character.id"
              :value="`character:${character.id}`"
            >
              {{ character.name }}<template v-if="character.playerName"> ({{ character.playerName }})</template>
            </option>
          </optgroup>
          <optgroup v-if="sortedNpcs.length" label="NPCs / Monsters">
            <option
              v-for="npc in sortedNpcs"
              :key="npc.id"
              :value="`npc:${npc.id}`"
            >
              {{ npc.name }}<template v-if="npc.kind"> ({{ npc.kind }})</template>
            </option>
          </optgroup>
          <option value="__other__">Other…</option>
        </select>
      </label>
      <label v-if="targetSelection === '__other__'">
        Target name
        <input
          v-model.trim="targetOtherText"
          :disabled="disabled"
          placeholder="Door, trap, object…"
          required
        />
      </label>
      <p v-if="needsTarget && !hasValidTarget" class="text-sm" style="color: var(--danger); margin: 0.25rem 0 0;">
        A target is required for combat actions.
      </p>
    </div>

    <div class="btn-row" style="margin-top: 0.75rem;">
      <button type="submit" class="btn" :disabled="!canSubmit">
        {{ isSubmitting ? 'Sending…' : 'Submit Action' }}
      </button>
      <button type="button" class="btn ghost" :disabled="disabled" @click="emit('cancel')">
        Cancel
      </button>
    </div>
  </form>
</template>
