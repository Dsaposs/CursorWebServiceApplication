import type { BuildRollContextParams, DiceRollerDefinition, ParsedPlayerRoll } from '~/dice-rollers/types';
import {
  describeRulesetAction,
  findRulesetAction,
  findRulesetAttribute,
  findRulesetSkill,
} from '~/utils/rulesets';
import D20CheckRoller from '~/dice-rollers/d20-check/D20CheckRoller.vue';

function parseSides(notation: string): number {
  const match = notation.trim().match(/(\d+)d(\d+)/i);
  return match ? parseInt(match[2], 10) : 20;
}

function resolveDiceSides(definition: BuildRollContextParams['definition'], diceKey: string): number {
  const entry = definition.dice.find(d => d.key === diceKey);
  return entry ? parseSides(entry.notation) : 20;
}

function buildContext(params: BuildRollContextParams) {
  const { definition, mode, actionKey, skillKey, attributeKey } = params;

  if (mode === 'action' && actionKey) {
    const action = findRulesetAction(definition, actionKey);
    if (!action) return null;
    const detail = describeRulesetAction(action, definition);
    return {
      rollerKey: 'd20-check',
      label: action.label,
      poolBreakdown: [
        `${detail.attribute} + ${detail.skill}`,
        detail.dice,
      ],
      successRule: action.roll.successRule,
      config: {
        kind: 'd20-check' as const,
        sides: resolveDiceSides(definition, action.roll.dice),
        successRule: action.roll.successRule,
      },
    };
  }

  if (mode === 'skill' && skillKey) {
    const skill = findRulesetSkill(definition, skillKey);
    if (!skill) return null;
    const attr = findRulesetAttribute(definition, skill.attribute);
    const diceKey = definition.rollMechanics?.skillCheck?.diceKey ?? definition.dice[0]?.key ?? 'd20';
    return {
      rollerKey: 'd20-check',
      label: `${skill.label} Check`,
      poolBreakdown: [`${attr?.label ?? skill.attribute} + ${skill.label}`],
      successRule: definition.rollMechanics?.skillCheck?.successRule,
      config: {
        kind: 'd20-check' as const,
        sides: resolveDiceSides(definition, diceKey),
        successRule: definition.rollMechanics?.skillCheck?.successRule,
      },
    };
  }

  if (mode === 'attribute' && attributeKey) {
    const attr = findRulesetAttribute(definition, attributeKey);
    if (!attr) return null;
    const diceKey = definition.rollMechanics?.attributeCheck?.diceKey ?? definition.dice[0]?.key ?? 'd20';
    return {
      rollerKey: 'd20-check',
      label: `${attr.label} Check`,
      poolBreakdown: [attr.label],
      successRule: definition.rollMechanics?.attributeCheck?.successRule,
      config: {
        kind: 'd20-check' as const,
        sides: resolveDiceSides(definition, diceKey),
        successRule: definition.rollMechanics?.attributeCheck?.successRule,
      },
    };
  }

  return null;
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
