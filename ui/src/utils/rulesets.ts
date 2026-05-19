import type {
  CharacterResponse,
  RulesetActionDefinition,
  RulesetAttributeDefinition,
  RulesetCheckMechanics,
  RulesetDefinition,
  RulesetResponse,
  RulesetSkillDefinition,
} from '~/types/api';
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
  return availableActionsForClass(definition, character?.classKey);
}

export function availableActionsForClass(definition: RulesetDefinition | null, classKey?: string | null) {
  if (!definition) return [];

  const normalizedClassKey = classKey ?? '';
  return definition.actions.filter(action => {
    const allowedClasses = action.allowedClasses ?? [];
    return !allowedClasses.length || allowedClasses.includes(normalizedClassKey);
  });
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
  const attribute = definition.character.attributes.find(item => item.key === action.roll.attribute);
  const skill = definition.character.skills.find(item => item.key === action.roll.skill);
  const dice = definition.dice.find(item => item.key === action.roll.dice);
  const modifierText = action.roll.modifiers.length
    ? action.roll.modifiers.map(modifier => `${modifier.key} (${modifier.source})`).join(', ')
    : 'No listed modifiers';

  return {
    dice: dice?.label ?? action.roll.dice,
    notation: dice?.notation ?? definition.diceNotation,
    attribute: attribute?.label ?? action.roll.attribute,
    skill: skill?.label ?? action.roll.skill,
    modifiers: modifierText,
    successRule: action.roll.successRule,
  };
}

export function buildRollSummary(action: RulesetActionDefinition, definition: RulesetDefinition) {
  const detail = describeRulesetAction(action, definition);
  return `${detail.dice}: ${detail.attribute} + ${detail.skill}`;
}

