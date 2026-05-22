import type { CharacterResponse, RollPromptResponse, RulesetDefinition } from '~/types/api';
import type { DiceRollContext, DiceRollMode, RollResultKind } from '~/dice-rollers/types';
import { buildDiceRollContext } from '~/dice-rollers/buildRollContext';
import { resolveDiceRollerKey } from '~/dice-rollers/registry';
import { parseCharacterStats } from '~/utils/dice';
import {
  describeRulesetAction,
  findRulesetAction,
  findRulesetAttribute,
  findRulesetSkill,
} from '~/utils/rulesets';

function parseDiceSides(notation: string): number {
  const match = notation.trim().match(/(\d+)d(\d+)/i);
  return match ? parseInt(match[2], 10) : 20;
}

function defaultDiceSides(definition: RulesetDefinition): number {
  const notation = definition.dice[0]?.notation ?? '1d20';
  return parseDiceSides(notation);
}

export function isSameGuid(a?: string | null, b?: string | null): boolean {
  const left = (a ?? '').trim().toLowerCase();
  const right = (b ?? '').trim().toLowerCase();
  return Boolean(left && right && left === right);
}

/** Pending roll prompt for a player character, preferring the newest request. */
export function findActivePlayerRollPrompt(
  rollPrompts: RollPromptResponse[],
  characterId?: string | null,
  options?: { requireMyTurn?: boolean; isMyTurn?: boolean },
): RollPromptResponse | null {
  if (!characterId) return null;

  const candidates = rollPrompts
    .filter(prompt =>
      prompt.status === 'Pending'
      && isSameGuid(prompt.targetCharacterId, characterId)
      && (!options?.requireMyTurn || options.isMyTurn === true),
    )
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt));

  return candidates[0] ?? null;
}

export function normalizeRollResultKind(raw?: string | null): RollResultKind {
  return raw === 'Total' ? 'Total' : 'PassFail';
}

export function rollPromptResultKindLabel(kind: RollResultKind): string {
  return kind === 'Total' ? 'Dice total' : 'Pass / fail';
}

export function rollPromptCheckLabel(
  prompt: RollPromptResponse,
  definition: RulesetDefinition | null,
): string {
  if (prompt.promptLabel?.trim()) return prompt.promptLabel.trim();
  if (!definition) return prompt.customCheckText ?? 'Roll';

  switch (prompt.checkMode) {
    case 'Action': {
      const action = findRulesetAction(definition, prompt.actionKey ?? '');
      return action?.label ?? prompt.customCheckText ?? 'Action roll';
    }
    case 'Skill': {
      const skill = findRulesetSkill(definition, prompt.skillKey ?? '');
      return skill ? `${skill.label} check` : 'Skill check';
    }
    case 'Attribute': {
      const attr = findRulesetAttribute(definition, prompt.attributeKey ?? '');
      return attr ? `${attr.label} check` : 'Attribute check';
    }
    default:
      return prompt.customCheckText ?? 'Custom roll';
  }
}

export function buildRollPromptContext(
  prompt: RollPromptResponse,
  definition: RulesetDefinition,
  character: CharacterResponse,
): DiceRollContext | null {
  const resultKind = normalizeRollResultKind(prompt.resultKind);
  const { attributes, skills, gameValues } = parseCharacterStats(character.rulesetDataJson);

  if (prompt.checkMode === 'Custom') {
    const label = rollPromptCheckLabel(prompt, definition);
    const rollerKey = resolveDiceRollerKey(definition);
    const notationMatch = (prompt.customCheckText ?? label).match(/(\d+d\d+(?:\+\d+)?)/i);
    if (rollerKey === 'd20-check') {
      const sides = notationMatch ? parseDiceSides(notationMatch[1]) : defaultDiceSides(definition);
      return {
        rollerKey,
        label,
        poolBreakdown: [label],
        resultKind,
        config: { kind: 'd20-check', sides },
      };
    }

    const successTarget = definition.dice.find(d => d.successTarget)?.successTarget ?? 6;
    return {
      rollerKey: 'd6-pool',
      label,
      poolBreakdown: [label],
      resultKind,
      successRule: resultKind === 'Total'
        ? 'Add up the values on all dice rolled.'
        : `Each die showing ${successTarget}+ is a success.`,
      config: {
        kind: 'd6-pool',
        baseDiceCount: 1,
        stressDiceCount: 0,
        sides: 6,
        successTarget,
      },
    };
  }

  const mode = prompt.checkMode.toLowerCase() as DiceRollMode;
  const context = buildDiceRollContext({
    definition,
    mode,
    actionKey: prompt.actionKey ?? '',
    skillKey: prompt.skillKey ?? '',
    attributeKey: prompt.attributeKey ?? '',
    attributes,
    skills,
    gameValues,
  });

  if (!context) return null;

  const withKind: DiceRollContext = { ...context, resultKind };

  if (prompt.checkMode === 'Action' && prompt.actionKey) {
    const action = findRulesetAction(definition, prompt.actionKey);
    if (action) {
      const detail = describeRulesetAction(action, definition);
      return {
        ...withKind,
        label: rollPromptCheckLabel(prompt, definition),
        poolBreakdown: context.poolBreakdown.length ? context.poolBreakdown : [`${detail.attribute} + ${detail.skill}`],
        successRule: resultKind === 'Total'
          ? 'Add up the values on all dice rolled.'
          : context.successRule,
      };
    }
  }

  return {
    ...withKind,
    label: rollPromptCheckLabel(prompt, definition),
    successRule: resultKind === 'Total'
      ? 'Add up the values on all dice rolled.'
      : context.successRule,
  };
}

export function toApiCheckMode(mode: string): string {
  return mode.charAt(0).toUpperCase() + mode.slice(1);
}
