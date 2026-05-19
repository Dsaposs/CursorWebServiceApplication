import type { ComputedRef, Ref } from 'vue';
import type { RulesetDefinition } from '~/types/api';
import type { DiceRollMode } from '~/dice-rollers/types';
import { buildDiceRollContext } from '~/dice-rollers/buildRollContext';

export function useDiceRollContext(
  definition: ComputedRef<RulesetDefinition | null>,
  mode: Ref<DiceRollMode>,
  actionKey: Ref<string>,
  skillKey: Ref<string>,
  attributeKey: Ref<string>,
  attributes: ComputedRef<Record<string, number>>,
  skills: ComputedRef<Record<string, number>>,
  gameValues: ComputedRef<Record<string, number>>,
) {
  return computed(() => {
    const def = definition.value;
    if (!def) return null;

    return buildDiceRollContext({
      definition: def,
      mode: mode.value,
      actionKey: actionKey.value,
      skillKey: skillKey.value,
      attributeKey: attributeKey.value,
      attributes: attributes.value,
      skills: skills.value,
      gameValues: gameValues.value,
    });
  });
}
