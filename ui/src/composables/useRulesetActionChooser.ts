import { computed, ref, watch, type ComputedRef, type Ref } from 'vue';
import type { RulesetDefinition } from '~/types/api';
import { hasInventoryItem, type InventoryEntry } from '~/utils/inventory';
import {
  availableActionsForClass,
  describeAttributeCheck,
  describeRulesetAction,
  describeSkillCheck,
  findRulesetAction,
} from '~/utils/rulesets';

export type RulesetActionMode = 'action' | 'stat-check' | 'custom';

/**
 * A selectable entry in the unified stat-check picker (covers both skills and attributes).
 * The `key` is a composite: 'skill:<key>' or 'attribute:<key>'.
 */
export interface StatCheckOption {
  key: string;
  label: string;
  type: 'skill' | 'attribute';
  rollSummary: string;
  actionText: string;
}

export interface RulesetActionSubmitPayload {
  actionKey?: string;
  actionText: string;
  description?: string;
}

export function useRulesetActionChooser(
  definition: ComputedRef<RulesetDefinition | null>,
  classKey: ComputedRef<string | null | undefined>,
  inventory: ComputedRef<InventoryEntry[]> = computed(() => []),
  isEnabled: Ref<boolean> | ComputedRef<boolean> = computed(() => true),
) {
  const actionMode = ref<RulesetActionMode>('action');
  const selectedActionKey = ref('');
  /** Composite key: 'skill:<key>' or 'attribute:<key>'. */
  const selectedStatKey = ref('');
  const customActionText = ref('');

  // Predefined actions filtered by class + inventory (existing behaviour).
  const availableActions = computed(() => isEnabled.value
    ? availableActionsForClass(definition.value, classKey.value, inventory.value)
    : []);

  /**
   * All skills from the ruleset (equipment-gated via requiredItemKey) plus all attributes.
   * Zero-value stats are intentionally included — a character can attempt anything they're bad at.
   * Equipment gate: if a skill has `requiredItemKey` and the actor doesn't have that item, the
   * option is hidden (e.g. can't attempt to fire a pulse rifle without one).
   */
  const availableStatChecks = computed((): StatCheckOption[] => {
    if (!isEnabled.value || !definition.value) return [];
    const def = definition.value;

    const skillOptions: StatCheckOption[] = def.character.skills
      .filter(skill => !skill.requiredItemKey || hasInventoryItem(inventory.value, skill.requiredItemKey))
      .map(skill => {
        const detail = describeSkillCheck(skill, def);
        return { key: `skill:${skill.key}`, label: skill.label, type: 'skill', rollSummary: detail.rollSummary, actionText: detail.actionText };
      });

    const attributeOptions: StatCheckOption[] = def.character.attributes.map(attr => {
      const detail = describeAttributeCheck(attr);
      return { key: `attribute:${attr.key}`, label: attr.label, type: 'attribute', rollSummary: detail.rollSummary, actionText: detail.actionText };
    });

    return [...skillOptions, ...attributeOptions];
  });

  const selectedStatDetail = computed(
    () => availableStatChecks.value.find(s => s.key === selectedStatKey.value) ?? null,
  );

  /** Whether the selected stat is a skill or attribute — needed for dice context. */
  const selectedStatType = computed((): 'skill' | 'attribute' | null => {
    if (!selectedStatKey.value) return null;
    return selectedStatKey.value.startsWith('skill:') ? 'skill' : 'attribute';
  });

  /** The raw (non-prefixed) key for the selected stat. */
  const selectedStatRawKey = computed(() => {
    const idx = selectedStatKey.value.indexOf(':');
    return idx >= 0 ? selectedStatKey.value.slice(idx + 1) : '';
  });

  const selectedRulesetAction = computed(() => findRulesetAction(definition.value, selectedActionKey.value));

  const selectedActionDetail = computed(() =>
    selectedRulesetAction.value && definition.value
      ? describeRulesetAction(selectedRulesetAction.value, definition.value)
      : null,
  );

  const resolvedActionText = computed(() => {
    switch (actionMode.value) {
      case 'action': return selectedRulesetAction.value?.label ?? '';
      case 'stat-check': return selectedStatDetail.value?.actionText ?? '';
      case 'custom': return customActionText.value;
      default: return '';
    }
  });

  const suggestedRollSummary = computed(() =>
    actionMode.value === 'stat-check' ? (selectedStatDetail.value?.rollSummary ?? '') : '',
  );

  function firstAvailableActionMode(): RulesetActionMode {
    if (availableActions.value.length) return 'action';
    if (availableStatChecks.value.length) return 'stat-check';
    return 'custom';
  }

  function resetSelection() {
    selectedActionKey.value = '';
    selectedStatKey.value = '';
    customActionText.value = '';
  }

  function buildDescription(description?: string) {
    return description?.trim() || undefined;
  }

  function buildSubmitPayload(description?: string): RulesetActionSubmitPayload | null {
    const actionText = resolvedActionText.value.trim();
    if (!actionText) return null;
    return {
      actionKey: actionMode.value === 'action' ? selectedActionKey.value || undefined : undefined,
      actionText,
      description: buildDescription(description) || undefined,
    };
  }

  watch(actionMode, resetSelection);

  watch([availableActions, availableStatChecks], () => {
    const modeIsAvailable =
      (actionMode.value === 'action' && availableActions.value.length > 0)
      || (actionMode.value === 'stat-check' && availableStatChecks.value.length > 0)
      || actionMode.value === 'custom';
    if (!modeIsAvailable) actionMode.value = firstAvailableActionMode();
  }, { immediate: true });

  return {
    actionMode,
    selectedActionKey,
    selectedStatKey,
    selectedStatType,
    selectedStatRawKey,
    customActionText,
    availableActions,
    availableStatChecks,
    selectedActionDetail,
    selectedStatDetail,
    resolvedActionText,
    suggestedRollSummary,
    resetSelection,
    buildDescription,
    buildSubmitPayload,
  };
}
