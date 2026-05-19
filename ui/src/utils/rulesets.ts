import type {
  CharacterResponse,
  RulesetActionDefinition,
  RulesetAttributeDefinition,
  RulesetCheckMechanics,
  RulesetDefinition,
  RulesetItemDefinition,
  RulesetResponse,
  RulesetSkillDefinition,
} from '~/types/api';
import { hasInventoryItem, parseInventory, parseNpcInventory, type InventoryEntry } from '~/utils/inventory';
export { buildDiceRollContext, parsePlayerRollFromDescription } from '~/dice-rollers/buildRollContext';
export { getDiceRoller, resolveDiceRollerKey } from '~/dice-rollers/registry';
export type { DiceRollContext, DiceRollMode } from '~/dice-rollers/types';

export function parseRulesetDefinition(ruleset?: Pick<RulesetResponse, 'definitionJson'> | null) {
  if (!ruleset?.definitionJson) return null;

  try {
    return JSON.parse(ruleset.definitionJson) as RulesetDefinition;
  } catch {
    return null;
  }
}

export function availableActionsForCharacter(definition: RulesetDefinition | null, character?: CharacterResponse | null) {
  return availableActionsForClass(definition, character?.classKey, inventoryForActor(character));
}

export function availableActionsForClass(
  definition: RulesetDefinition | null,
  classKey?: string | null,
  inventory: InventoryEntry[] = [],
) {
  if (!definition) return [];

  const normalizedClassKey = classKey ?? '';
  return definition.actions.filter(action => {
    const allowedClasses = action.allowedClasses ?? [];
    const classAllowed = !allowedClasses.length || allowedClasses.includes(normalizedClassKey);
    const itemAllowed = !action.requiredItemKey || hasInventoryItem(inventory, action.requiredItemKey);
    return classAllowed && itemAllowed;
  });
}

export function inventoryForActor(
  character?: CharacterResponse | null,
  statBlockJson?: string | null,
): InventoryEntry[] {
  if (character) return parseInventory(character.inventoryJson);
  if (statBlockJson) return parseNpcInventory(statBlockJson);
  return [];
}

export function findRulesetItem(definition: RulesetDefinition | null, itemKey?: string | null) {
  if (!definition || !itemKey) return null;
  return (definition.items ?? []).find(item => item.key === itemKey) ?? null;
}

export function classForKey(definition: RulesetDefinition | null, classKey?: string | null) {
  if (!definition || !classKey) return null;
  return definition.character.classes.find(c => c.key === classKey) ?? null;
}

export function startingItemOptionsForClass(definition: RulesetDefinition | null, classKey?: string | null) {
  const cls = classForKey(definition, classKey);
  if (!cls?.startingItemOptions?.length) return [];
  return cls.startingItemOptions
    .map(key => findRulesetItem(definition, key))
    .filter((item): item is RulesetItemDefinition => Boolean(item));
}

export function availableSkillsForClass(definition: RulesetDefinition | null, classKey?: string | null) {
  if (!definition) return [];

  const normalizedClassKey = classKey ?? '';
  const actorClass = definition.character.classes.find(item => item.key === normalizedClassKey);
  if (!actorClass) return definition.character.skills;

  const availableSkillKeys = new Set(actorClass.availableSkills);
  return definition.character.skills.filter(skill => availableSkillKeys.has(skill.key));
}

export function findRulesetAttribute(definition: RulesetDefinition | null, attributeKey?: string | null) {
  if (!definition || !attributeKey) return null;
  return definition.character.attributes.find(attribute => attribute.key === attributeKey) ?? null;
}

export function findRulesetSkill(definition: RulesetDefinition | null, skillKey?: string | null) {
  if (!definition || !skillKey) return null;
  return definition.character.skills.find(skill => skill.key === skillKey) ?? null;
}

export function describeSkillCheck(skill: RulesetSkillDefinition, definition: RulesetDefinition) {
  const attribute = findRulesetAttribute(definition, skill.attribute);

  return {
    actionText: `Skill check: ${skill.label}`,
    rollSummary: `${attribute?.label ?? skill.attribute} + ${skill.label}`,
  };
}

export function describeAttributeCheck(attribute: RulesetAttributeDefinition) {
  return {
    actionText: `Attribute check: ${attribute.label}`,
    rollSummary: attribute.label,
  };
}

export function parseActorClassKey(json?: string | null) {
  if (!json) return '';

  try {
    const parsed = JSON.parse(json) as { classKey?: unknown };
    return typeof parsed.classKey === 'string' ? parsed.classKey : '';
  } catch {
    return '';
  }
}

export function findRulesetAction(definition: RulesetDefinition | null, actionKey?: string | null) {
  if (!definition || !actionKey) return null;
  return definition.actions.find(action => action.key === actionKey) ?? null;
}

export function describeRulesetAction(action: RulesetActionDefinition, definition: RulesetDefinition) {
  const item = action.requiredItemKey ? findRulesetItem(definition, action.requiredItemKey) : null;
  const roll = item?.attackRoll ?? action.roll;
  const attribute = definition.character.attributes.find(itemDef => itemDef.key === roll.attribute);
  const skill = definition.character.skills.find(itemDef => itemDef.key === roll.skill);
  const dice = definition.dice.find(itemDef => itemDef.key === roll.dice);
  const allModifiers = [...(roll.modifiers ?? []), ...(item?.modifiers ?? [])];
  const modifierText = allModifiers.length
    ? allModifiers.map(modifier => `${modifier.key} (${modifier.source})`).join(', ')
    : 'No listed modifiers';

  return {
    dice: dice?.label ?? roll.dice,
    notation: dice?.notation ?? definition.diceNotation,
    attribute: attribute?.label ?? roll.attribute,
    skill: skill?.label ?? roll.skill,
    modifiers: modifierText,
    successRule: roll.successRule,
    itemLabel: item?.label,
    damageRoll: item?.damageRoll,
  };
}

export function buildRollSummary(action: RulesetActionDefinition, definition: RulesetDefinition) {
  const detail = describeRulesetAction(action, definition);
  return `${detail.dice}: ${detail.attribute} + ${detail.skill}`;
}

