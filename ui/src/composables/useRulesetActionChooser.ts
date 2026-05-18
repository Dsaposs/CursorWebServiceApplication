import { computed, ref, watch, type ComputedRef, type Ref } from 'vue';
import type { RulesetDefinition } from '~/types/api';
import {
  availableActionsForClass,
  availableSkillsForClass,
  describeAttributeCheck,
  describeRulesetAction,
  describeSkillCheck,
  findRulesetAction,
  findRulesetAttribute,
  findRulesetSkill,
} from '~/utils/rulesets';

export type RulesetActionMode = 'action' | 'skill' | 'attribute' | 'custom';

export interface RulesetActionSubmitPayload {
  actionKey?: string;
  actionText: string;
  description?: string;
}

export function useRulesetActionChooser(
  definition: ComputedRef<RulesetDefinition | null>,
  classKey: ComputedRef<string | null | undefined>,
  isEnabled: Ref<boolean> | ComputedRef<boolean> = computed(() => true),
) {
  const actionMode = ref<RulesetActionMode>('action');
  const selectedActionKey = ref('');
  const selectedSkillKey = ref('');
  const selectedAttributeKey = ref('');
  const customActionText = ref('');

  const availableActions = computed(() => isEnabled.value
    ? availableActionsForClass(definition.value, classKey.value)
    : []);
  const availableSkills = computed(() => isEnabled.value
    ? availableSkillsForClass(definition.value, classKey.value)
    : []);
  const availableAttributes = computed(() => isEnabled.value
    ? definition.value?.character.attributes ?? []
    : []);

  const selectedRulesetAction = computed(() => findRulesetAction(definition.value, selectedActionKey.value));
  const selectedRulesetSkill = computed(() => findRulesetSkill(definition.value, selectedSkillKey.value));
  const selectedRulesetAttribute = computed(() => findRulesetAttribute(definition.value, selectedAttributeKey.value));

  const selectedActionDetail = computed(() =>
    selectedRulesetAction.value && definition.value
      ? describeRulesetAction(selectedRulesetAction.value, definition.value)
      : null,
  );
  const selectedSkillDetail = computed(() =>
    selectedRulesetSkill.value && definition.value
      ? describeSkillCheck(selectedRulesetSkill.value, definition.value)
      : null,
  );
  const selectedAttributeDetail = computed(() =>
    selectedRulesetAttribute.value ? describeAttributeCheck(selectedRulesetAttribute.value) : null,
  );

  const resolvedActionText = computed(() => {
    switch (actionMode.value) {
      case 'action':
        return selectedRulesetAction.value?.label ?? '';
      case 'skill':
        return selectedSkillDetail.value?.actionText ?? '';
      case 'attribute':
        return selectedAttributeDetail.value?.actionText ?? '';
      case 'custom':
        return customActionText.value;
      default:
        return '';
    }
  });

  const suggestedRollSummary = computed(() => {
    if (actionMode.value === 'skill') return selectedSkillDetail.value?.rollSummary ?? '';
    if (actionMode.value === 'attribute') return selectedAttributeDetail.value?.rollSummary ?? '';
    return '';
  });

  function firstAvailableActionMode(): RulesetActionMode {
    if (availableActions.value.length) return 'action';
    if (availableSkills.value.length) return 'skill';
    if (availableAttributes.value.length) return 'attribute';
    return 'custom';
  }

  function resetSelection() {
    selectedActionKey.value = '';
    selectedSkillKey.value = '';
    selectedAttributeKey.value = '';
    customActionText.value = '';
  }

  function buildDescription(description?: string) {
    return [
      suggestedRollSummary.value ? `Suggested roll: ${suggestedRollSummary.value}.` : '',
      description ?? '',
    ].filter(Boolean).join('\n');
  }

  function buildSubmitPayload(description?: string): RulesetActionSubmitPayload | null {
    const actionText = resolvedActionText.value.trim();
    if (!actionText) return null;

    const resolvedDescription = buildDescription(description);
    return {
      actionKey: actionMode.value === 'action' ? selectedActionKey.value || undefined : undefined,
      actionText,
      description: resolvedDescription || undefined,
    };
  }

  watch(actionMode, resetSelection);

  watch([availableActions, availableSkills, availableAttributes], () => {
    const modeIsAvailable =
      (actionMode.value === 'action' && availableActions.value.length > 0)
      || (actionMode.value === 'skill' && availableSkills.value.length > 0)
      || (actionMode.value === 'attribute' && availableAttributes.value.length > 0)
      || actionMode.value === 'custom';

    if (!modeIsAvailable) actionMode.value = firstAvailableActionMode();
  }, { immediate: true });

  return {
    actionMode,
    selectedActionKey,
    selectedSkillKey,
    selectedAttributeKey,
    customActionText,
    availableActions,
    availableSkills,
    availableAttributes,
    selectedActionDetail,
    selectedSkillDetail,
    selectedAttributeDetail,
    resolvedActionText,
    suggestedRollSummary,
    resetSelection,
    buildDescription,
    buildSubmitPayload,
  };
}
