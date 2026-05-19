import type {
  RulesetActionDefinition,
  RulesetAttributeDefinition,
  RulesetCheckMechanics,
  RulesetDefinition,
  RulesetSkillDefinition,
} from '~/types/api';
import { calcModifierDice } from '~/utils/dice';

export interface PoolRollParts {
  baseDiceCount: number;
  stressDiceCount: number;
  successTarget: number;
  sides: number;
  poolBreakdown: string[];
  successRule?: string;
  label: string;
}

function parseSides(notation: string, fallback = 6): number {
  const match = notation.trim().match(/(\d+)d(\d+)/i);
  if (!match) return fallback;
  return parseInt(match[2], 10);
}

function findDice(definition: RulesetDefinition, diceKey: string) {
  return definition.dice.find(d => d.key === diceKey);
}

function attrLabel(definition: RulesetDefinition, key: string) {
  return definition.character.attributes.find(a => a.key === key)?.label ?? key;
}

function skillLabel(definition: RulesetDefinition, key: string) {
  return definition.character.skills.find(s => s.key === key)?.label ?? key;
}

function buildFromCheck(
  definition: RulesetDefinition,
  check: RulesetCheckMechanics | undefined,
  skillDef: RulesetSkillDefinition | null,
  attrDef: RulesetAttributeDefinition | null,
  attributes: Record<string, number>,
  skills: Record<string, number>,
  gameValues: Record<string, number>,
  label: string,
): PoolRollParts | null {
  const diceEntry = findDice(definition, check?.diceKey ?? '');
  if (!diceEntry?.successTarget) return null;

  const attrKey = skillDef?.attribute ?? attrDef?.key ?? '';
  const attrValue = attributes[attrKey] ?? 0;
  const skillValue = skillDef ? (skills[skillDef.key] ?? 0) : 0;
  const poolMode = check?.poolMode ?? 'fixed';

  let baseDiceCount = 1;
  const poolBreakdown: string[] = [];

  if (poolMode === 'attribute+skill' && skillDef) {
    baseDiceCount = attrValue + skillValue;
    poolBreakdown.push(`${attrLabel(definition, attrKey)} ${attrValue}`, `${skillDef.label} ${skillValue}`);
  } else if (poolMode === 'attribute' && attrDef) {
    baseDiceCount = attrValue;
    poolBreakdown.push(`${attrDef.label} ${attrValue}`);
  }

  const { extra, stressExtra, breakdown } = check
    ? calcModifierDice(check.modifiers, { ...attributes, ...gameValues }, skills)
    : { extra: 0, stressExtra: 0, breakdown: [] };

  return {
    baseDiceCount: baseDiceCount + extra,
    stressDiceCount: stressExtra,
    successTarget: diceEntry.successTarget,
    sides: parseSides(diceEntry.notation),
    poolBreakdown: [...poolBreakdown, ...breakdown, ...(check?.successRule ? [`Success: ${check.successRule}`] : [])],
    successRule: check?.successRule,
    label,
  };
}

export function buildActionPoolRoll(
  definition: RulesetDefinition,
  action: RulesetActionDefinition,
  attributes: Record<string, number>,
  skills: Record<string, number>,
  gameValues: Record<string, number>,
): PoolRollParts | null {
  const diceEntry = findDice(definition, action.roll.dice);
  if (!diceEntry?.successTarget) return null;

  const isPool = action.roll.dicePoolMode === 'attribute+skill';
  const attrValue = attributes[action.roll.attribute] ?? 0;
  const skillValue = skills[action.roll.skill] ?? 0;
  const baseDiceCount = isPool ? attrValue + skillValue : 1;

  const { extra, stressExtra, breakdown } = calcModifierDice(
    action.roll.modifiers,
    { ...attributes, ...gameValues },
    skills,
  );

  const poolBreakdown = isPool
    ? [
        `${attrLabel(definition, action.roll.attribute)} ${attrValue}`,
        `${skillLabel(definition, action.roll.skill)} ${skillValue}`,
        ...breakdown,
      ]
    : breakdown;

  return {
    baseDiceCount: baseDiceCount + extra,
    stressDiceCount: stressExtra,
    successTarget: diceEntry.successTarget,
    sides: parseSides(diceEntry.notation),
    poolBreakdown,
    successRule: action.roll.successRule,
    label: action.label,
  };
}

export function buildSkillPoolRoll(
  definition: RulesetDefinition,
  skillKey: string,
  attributes: Record<string, number>,
  skills: Record<string, number>,
  gameValues: Record<string, number>,
): PoolRollParts | null {
  const skillDef = definition.character.skills.find(s => s.key === skillKey);
  if (!skillDef) return null;

  return buildFromCheck(
    definition,
    definition.rollMechanics?.skillCheck,
    skillDef,
    null,
    attributes,
    skills,
    gameValues,
    `${skillDef.label} Check`,
  );
}

export function buildAttributePoolRoll(
  definition: RulesetDefinition,
  attributeKey: string,
  attributes: Record<string, number>,
  skills: Record<string, number>,
  gameValues: Record<string, number>,
): PoolRollParts | null {
  const attrDef = definition.character.attributes.find(a => a.key === attributeKey);
  if (!attrDef) return null;

  return buildFromCheck(
    definition,
    definition.rollMechanics?.attributeCheck,
    null,
    attrDef,
    attributes,
    skills,
    gameValues,
    `${attrDef.label} Check`,
  );
}
