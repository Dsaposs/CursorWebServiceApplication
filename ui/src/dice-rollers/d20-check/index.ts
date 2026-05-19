import type { BuildRollContextParams, DiceRollerDefinition, ParsedPlayerRoll } from '~/dice-rollers/types';
import {
  buildActionSingleDie,
  buildAttributeSingleDie,
  buildSkillSingleDie,
} from '~/dice-rollers/shared/singleDieBuilder';
import D20CheckRoller from '~/dice-rollers/d20-check/D20CheckRoller.vue';

function buildContext(params: BuildRollContextParams) {
  const { mode } = params;

  let parts = null;
  if (mode === 'action') parts = buildActionSingleDie(params);
  else if (mode === 'skill') parts = buildSkillSingleDie(params);
  else if (mode === 'attribute') parts = buildAttributeSingleDie(params);
  if (!parts) return null;

  return {
    rollerKey: 'd20-check',
    label: parts.label,
    poolBreakdown: parts.poolBreakdown,
    successRule: parts.successRule,
    config: {
      kind: 'd20-check' as const,
      sides: parts.sides,
      successRule: parts.successRule,
      attackBonus: parts.itemAttackBonus,
    },
  };
}

function parsePlayerRoll(rollLine: string): ParsedPlayerRoll {
  const totalMatch = rollLine.match(/=\s*(\d+)\s*(?:\(|$)/) ?? rollLine.match(/:\s*(\d+)\s*\(manual\)/);
  const primary = totalMatch ? parseInt(totalMatch[1], 10) : 0;
  return { hasRoll: rollLine.length > 0 && rollLine.includes('1d'), primary };
}

function formatAdjustedSummary(roll: ParsedPlayerRoll, modifier: number): string {
  const adjusted = Math.max(1, roll.primary + modifier);
  const modNote = modifier !== 0 ? ` (${roll.primary} raw ${modifier > 0 ? '+' : ''}${modifier})` : '';
  return `d20 result ${adjusted}${modNote}`;
}

export const d20CheckRoller: DiceRollerDefinition = {
  key: 'd20-check',
  label: 'D20 Check',
  description: 'Roll a single die (typically d20) and compare the total to a target or DC.',
  component: D20CheckRoller,
  buildRollContext: buildContext,
  parsePlayerRoll,
  formatAdjustedSummary,
};
