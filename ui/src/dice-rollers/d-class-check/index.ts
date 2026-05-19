/**
 * d-class-check roller — single class die + flat stat modifier + optional bonus d4s.
 *
 * Used by DIE RPG and any ruleset where characters roll their unique class die
 * (d4 / d6 / d8 / d10 / d12 / d20), add an attribute+skill modifier, and optionally
 * roll a pool of extra d4s (e.g. grief dice for the Grief Knight class).
 *
 * Roll formula:  1d[sides] + modifier [+ N×d4 bonus dice] vs difficultyClass
 */

import type { BuildRollContextParams, DiceRollerDefinition, ParsedPlayerRoll } from '~/dice-rollers/types';
import {
  buildActionSingleDie,
  buildAttributeSingleDie,
  buildSkillSingleDie,
} from '~/dice-rollers/shared/singleDieBuilder';
import DClassCheckRoller from '~/dice-rollers/d-class-check/DClassCheckRoller.vue';

function buildContext(params: BuildRollContextParams) {
  const { mode } = params;

  let parts = null;
  if (mode === 'action') parts = buildActionSingleDie(params);
  else if (mode === 'skill') parts = buildSkillSingleDie(params);
  else if (mode === 'attribute') parts = buildAttributeSingleDie(params);
  if (!parts) return null;

  return {
    rollerKey: 'd-class-check',
    label: parts.label,
    poolBreakdown: parts.poolBreakdown,
    successRule: parts.successRule,
    config: {
      kind: 'd-class-check' as const,
      sides: parts.sides,
      modifier: parts.statModifier,
      bonusDiceCount: parts.bonusDiceCount,
      bonusDiceLabel: parts.bonusDiceLabel,
      difficultyClass: parts.difficultyClass ?? 0,
    },
  };
}

function parsePlayerRoll(rollLine: string): ParsedPlayerRoll {
  // Format: "1d4: [3] + 5 + Grief [1,2] (3) = 11"
  const totalMatch = rollLine.match(/=\s*(\d+)\s*$/);
  const primary = totalMatch ? parseInt(totalMatch[1], 10) : 0;
  return {
    hasRoll: rollLine.length > 0 && rollLine.includes('1d'),
    primary,
  };
}

function formatAdjustedSummary(roll: ParsedPlayerRoll, modifier: number): string {
  const adjusted = Math.max(1, roll.primary + modifier);
  const modNote = modifier !== 0 ? ` (${roll.primary} + ${modifier > 0 ? '+' : ''}${modifier} adjustment)` : '';
  return `total ${adjusted}${modNote}`;
}

export const dClassCheckRoller: DiceRollerDefinition = {
  key: 'd-class-check',
  label: 'Class Die Check',
  description:
    'Roll one class die (d4–d20) and add a flat stat modifier. Optional bonus d4s (e.g. grief dice) are rolled and summed on top. Compare the total to a difficulty class.',
  component: DClassCheckRoller,
  buildRollContext: buildContext,
  parsePlayerRoll,
  formatAdjustedSummary,
};
