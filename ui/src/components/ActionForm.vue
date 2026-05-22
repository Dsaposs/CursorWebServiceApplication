<script setup lang="ts">
import type { CharacterResponse, NpcResponse, RulesetDefinition } from '~/types/api';
import { useRulesetActionChooser } from '~/composables/useRulesetActionChooser';
import { useActionTarget } from '~/composables/useActionTarget';
import { parseInventory } from '~/utils/inventory';
import { groupActions, isCombatAction, findRulesetAction } from '~/utils/rulesets';

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
  customActionText,
  availableActions,
  availableStatChecks,
  selectedActionDetail,
  selectedStatDetail,
  resolvedActionText,
  resetSelection,
  buildSubmitPayload,
} = useRulesetActionChooser(definitionRef, classKeyRef, inventory, isEnabled);

const grouped = computed(() => groupActions(availableActions.value));

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
  props.requireTargetForCombat && isCombatActionSelected.value,
);

const hasValidTarget = computed(() => targetIsValid());

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
  resetSelection();
  resetTarget();
}

defineExpose({ reset });
</script>

<template>
  <form class="action-form" @submit.prevent="handleSubmit">
    <!-- Roll Type -->
    <label>
      Roll Type
      <select v-model="actionMode" :disabled="disabled">
        <option v-if="availableActions.length" value="action">Action</option>
        <option v-if="availableStatChecks.length" value="stat-check">Stat Check</option>
        <option value="custom">Custom</option>
      </select>
    </label>

    <!-- Action picker — grouped by Normal / Combat -->
    <template v-if="actionMode === 'action'">
      <label>
        Action
        <select v-model="selectedActionKey" required :disabled="disabled">
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
        </p>
        <p class="text-sm">{{ selectedActionDetail.successRule }}</p>
      </div>
    </template>

    <!-- Stat check picker -->
    <template v-else-if="actionMode === 'stat-check'">
      <label>
        Stat
        <select v-model="selectedStatKey" required :disabled="disabled">
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

    <!-- Custom action text -->
    <template v-else>
      <label>
        Describe your action
        <input
          v-model="customActionText"
          type="text"
          placeholder="e.g. Pick the lock, distract the guard…"
          :disabled="disabled"
          required
        />
      </label>
    </template>

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
