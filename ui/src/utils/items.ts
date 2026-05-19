import type {
  RulesetActionDefinition,
  RulesetDefinition,
  RulesetItemDefinition,
  RulesetModifierDefinition,
} from '~/types/api';
import { attributeModifier } from '~/utils/dice';
import { findRulesetItem } from '~/utils/rulesets';

export interface EffectiveActionRoll {
  roll: RulesetActionDefinition['roll'];
  damageRoll?: RulesetItemDefinition['damageRoll'];
  item?: RulesetItemDefinition;
  itemAttackBonus: number;
}

export function resolveEffectiveActionRoll(
  definition: RulesetDefinition | null,
  action: RulesetActionDefinition | null | undefined,
): EffectiveActionRoll | null {
  if (!definition || !action) return null;

  const item = action.requiredItemKey
    ? findRulesetItem(definition, action.requiredItemKey)
    : undefined;

  const baseRoll = item?.attackRoll ?? action.roll;
  const mergedModifiers = [
    ...(baseRoll.modifiers ?? []),
    ...(item?.modifiers ?? []),
  ];

  const itemAttackBonus = sumAttackBonus(mergedModifiers);

  return {
    roll: { ...baseRoll, modifiers: mergedModifiers },
    damageRoll: item?.damageRoll,
    item,
    itemAttackBonus,
  };
}

export function sumAttackBonus(modifiers: RulesetModifierDefinition[]): number {
  return modifiers.reduce((sum, mod) => sum + (mod.attackBonus ?? 0), 0);
}

export function sumFlatDice(modifiers: RulesetModifierDefinition[]): number {
  return modifiers.reduce((sum, mod) => sum + (mod.flatDice ?? 0), 0);
}

export function describeDamageRoll(
  damageRoll: RulesetItemDefinition['damageRoll'],
  definition: RulesetDefinition,
  attributes: Record<string, number>,
): string {
  if (!damageRoll) return '';
  const parts = [damageRoll.notation];
  if (damageRoll.bonusAttribute) {
    const attr = definition.character.attributes.find(a => a.key === damageRoll.bonusAttribute);
    const mod = attributeModifier(attributes[damageRoll.bonusAttribute] ?? 10);
    if (mod !== 0) parts.push(`${mod >= 0 ? '+' : ''}${mod} ${attr?.label ?? damageRoll.bonusAttribute}`);
  }
  if (damageRoll.flatBonus) parts.push(`${damageRoll.flatBonus >= 0 ? '+' : ''}${damageRoll.flatBonus}`);
  return parts.join(' ');
}
