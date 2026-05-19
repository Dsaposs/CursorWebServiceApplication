import type { BuildRollContextParams, DiceRollerDefinition, ParsedPlayerRoll } from '~/dice-rollers/types';
import {
  buildActionPoolRoll,
  buildAttributePoolRoll,
  buildSkillPoolRoll,
} from '~/dice-rollers/shared/poolRollBuilder';
import { findRulesetAction } from '~/utils/rulesets';
import { resolveEffectiveActionRoll } from '~/utils/items';
import D6PoolRoller from '~/dice-rollers/d6-pool/D6PoolRoller.vue';

function buildContext(params: BuildRollContextParams) {
  const { definition, mode, actionKey, skillKey, attributeKey, attributes, skills, gameValues } = params;

  let parts = null;
  if (mode === 'action' && actionKey) {
    const action = findRulesetAction(definition, actionKey);
    const effective = resolveEffectiveActionRoll(definition, action);
    if (action && effective) {
      parts = buildActionPoolRoll(definition, action, attributes, skills, gameValues, effective.roll);
    }
  } else if (mode === 'skill' && skillKey) {
    parts = buildSkillPoolRoll(definition, skillKey, attributes, skills, gameValues);
  } else if (mode === 'attribute' && attributeKey) {
    parts = buildAttributePoolRoll(definition, attributeKey, attributes, skills, gameValues);
  }

  if (!parts) return null;

  return {
    rollerKey: 'd6-pool',
    label: parts.label,
    poolBreakdown: parts.poolBreakdown,
    successRule: parts.successRule,
    config: {
      kind: 'd6-pool' as const,
      baseDiceCount: parts.baseDiceCount,
      stressDiceCount: parts.stressDiceCount,
      sides: parts.sides,
      successTarget: parts.successTarget,
    },
  };
}

function parsePlayerRoll(rollLine: string): ParsedPlayerRoll {
  const successMatches = [...rollLine.matchAll(/(\d+)\s+success(?:es)?/g)];
  const primary = successMatches.length
    ? parseInt(successMatches[successMatches.length - 1][1], 10)
    : 0;

  const panicMatch = rollLine.match(/PANIC[^(]*\((\d+)\s+stress\s+1s\)/);
  const secondary = panicMatch ? parseInt(panicMatch[1], 10) : 0;

  return {
    hasRoll: rollLine.length > 0 && (rollLine.includes('success') || rollLine.includes('d6')),
    primary,
    secondary: secondary > 0 ? secondary : undefined,
    secondaryLabel: secondary > 0 ? 'panic check' : undefined,
  };
}

function formatAdjustedSummary(roll: ParsedPlayerRoll, modifier: number): string {
  const adjusted = Math.max(0, roll.primary + modifier);
  const modNote = modifier !== 0 ? ` (${roll.primary} raw ${modifier > 0 ? '+' : ''}${modifier})` : '';
  const panicNote = roll.secondary
    ? `, ${roll.secondary} panic check${roll.secondary !== 1 ? 's' : ''}`
    : '';
  return `${adjusted} success${adjusted !== 1 ? 'es' : ''}${modNote}${panicNote}`;
}

export const d6PoolRoller: DiceRollerDefinition = {
  key: 'd6-pool',
  label: 'D6 Dice Pool',
  description: 'Roll a pool of d6s; each die meeting the success target counts as one success. Supports stress dice.',
  component: D6PoolRoller,
  buildRollContext: buildContext,
  parsePlayerRoll,
  formatAdjustedSummary,
};
