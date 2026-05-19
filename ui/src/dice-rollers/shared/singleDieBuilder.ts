/**
 * Shared context-building helpers for single-die roll systems.
 *
 * Both d20-check (fixed d20 + item bonus) and d-class-check (variable class die +
 * attribute/skill + optional bonus d4s) are "single die + modifier" systems that share
 * most of the context-resolution logic.  Each roller's index.ts calls these helpers and
 * maps the returned parts onto its own config type.
 *
 * Adding a new single-die roller:
 *  1. Create `dice-rollers/<your-key>/` with `index.ts` and `<YourRoller>.vue`.
 *  2. Call `buildActionSingleDie` / `buildSkillSingleDie` / `buildAttributeSingleDie`
 *     here to get a `SingleDieContextParts` object.
 *  3. Map the parts onto your own `DiceRollConfig` variant.
 *  4. Add your roller to `registry.ts` and `dice-rollers.json`.
 */

import type { BuildRollContextParams } from '~/dice-rollers/types';
import { findRulesetAction, findRulesetAttribute, findRulesetSkill } from '~/utils/rulesets';
import { resolveEffectiveActionRoll, sumAttackBonus } from '~/utils/items';
import { calcModifierDice } from '~/utils/dice';

// ─── Shared result shape ──────────────────────────────────────────────────────

export interface SingleDieContextParts {
  /** Number of faces on the die (e.g. 20 for a d20, 4 for a d4). */
  sides: number;
  /** Human-readable action/skill/attribute label. */
  label: string;
  /** Flat item attack bonus — used by d20-check as the displayed modifier. */
  itemAttackBonus: number;
  /** Combined attribute + skill value — used by d-class-check as the additive modifier. */
  statModifier: number;
  /** Number of bonus d4s to roll (e.g. grief dice); 0 when unused. */
  bonusDiceCount: number;
  /** Human-readable label for bonus dice (e.g. "Grief", "Stress"). */
  bonusDiceLabel: string;
  /** Breakdown lines shown in the pool-summary area. */
  poolBreakdown: string[];
  successRule?: string;
  /** Numeric difficulty target to display alongside the roll. */
  difficultyClass?: number;
}

// ─── Internal helpers ─────────────────────────────────────────────────────────

export function parseSides(notation: string, fallback = 20): number {
  const match = notation.trim().match(/(\d+)d(\d+)/i);
  return match ? parseInt(match[2], 10) : fallback;
}

export function resolveDiceSides(
  definition: Pick<BuildRollContextParams['definition'], 'dice'>,
  diceKey: string,
  fallback = 20,
): number {
  const entry = definition.dice.find(d => d.key === diceKey);
  return entry ? parseSides(entry.notation, fallback) : fallback;
}

function capitalize(s: string): string {
  return s.charAt(0).toUpperCase() + s.slice(1);
}

// ─── Context builders ─────────────────────────────────────────────────────────

/** Build context parts for an action roll (used by both d20-check and d-class-check). */
export function buildActionSingleDie(params: BuildRollContextParams): SingleDieContextParts | null {
  const { definition, actionKey, attributes, skills, gameValues } = params;
  if (!actionKey) return null;

  const action = findRulesetAction(definition, actionKey);
  const effective = resolveEffectiveActionRoll(definition, action);
  if (!action || !effective) return null;

  const roll = effective.roll;
  const sides = resolveDiceSides(definition, roll.dice);
  const attrDef = definition.character.attributes.find(a => a.key === roll.attribute);
  const skillDef = definition.character.skills.find(s => s.key === roll.skill);
  const attrValue = attributes[roll.attribute] ?? 0;
  const skillValue = skills[roll.skill] ?? 0;

  const { stressExtra, extra, breakdown } = calcModifierDice(roll.modifiers, attributes, skills, gameValues);

  // Derive bonus-dice label from the first stress-type modifier's key ("grief" → "Grief").
  const bonusModDef = roll.modifiers.find(m => m.isStressDice);
  const bonusDiceLabel = bonusModDef ? capitalize(bonusModDef.key) : 'Bonus';

  const poolBreakdown: string[] = [
    ...(attrDef ? [`${attrDef.label} ${attrValue}`] : []),
    ...(skillDef ? [`${skillDef.label} ${skillValue}`] : []),
    ...breakdown,
    ...(effective.itemAttackBonus ? [`+${effective.itemAttackBonus} item bonus`] : []),
  ];

  return {
    sides,
    label: action.label,
    itemAttackBonus: effective.itemAttackBonus,
    statModifier: attrValue + skillValue + extra,
    bonusDiceCount: stressExtra,
    bonusDiceLabel,
    poolBreakdown,
    successRule: roll.successRule,
    difficultyClass: roll.difficultyClass,
  };
}

/** Build context parts for a skill check. */
export function buildSkillSingleDie(params: BuildRollContextParams): SingleDieContextParts | null {
  const { definition, skillKey, attributes, skills } = params;
  if (!skillKey) return null;

  const skill = findRulesetSkill(definition, skillKey);
  if (!skill) return null;

  const attr = findRulesetAttribute(definition, skill.attribute);
  const check = definition.rollMechanics?.skillCheck;
  const diceKey = check?.diceKey ?? definition.dice[0]?.key ?? '';
  const sides = resolveDiceSides(definition, diceKey);
  const attrValue = attributes[skill.attribute] ?? 0;
  const skillValue = skills[skillKey] ?? 0;

  return {
    sides,
    label: `${skill.label} Check`,
    itemAttackBonus: 0,
    statModifier: attrValue + skillValue,
    bonusDiceCount: 0,
    bonusDiceLabel: 'Bonus',
    poolBreakdown: [
      `${attr?.label ?? skill.attribute} ${attrValue}`,
      `${skill.label} ${skillValue}`,
    ],
    successRule: check?.successRule,
    difficultyClass: check?.difficultyClass,
  };
}

/** Build context parts for a raw attribute check. */
export function buildAttributeSingleDie(params: BuildRollContextParams): SingleDieContextParts | null {
  const { definition, attributeKey, attributes } = params;
  if (!attributeKey) return null;

  const attr = findRulesetAttribute(definition, attributeKey);
  if (!attr) return null;

  const check = definition.rollMechanics?.attributeCheck;
  const diceKey = check?.diceKey ?? definition.dice[0]?.key ?? '';
  const sides = resolveDiceSides(definition, diceKey);
  const attrValue = attributes[attributeKey] ?? 0;

  return {
    sides,
    label: `${attr.label} Check`,
    itemAttackBonus: 0,
    statModifier: attrValue,
    bonusDiceCount: 0,
    bonusDiceLabel: 'Bonus',
    poolBreakdown: [`${attr.label} ${attrValue}`],
    successRule: check?.successRule,
    difficultyClass: check?.difficultyClass,
  };
}
