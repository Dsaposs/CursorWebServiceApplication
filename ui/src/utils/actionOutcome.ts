import type { RulesetDefinition, GameResponse, ActionQueueItemResponse, RollPromptResponse } from '~/types/api';
import { resolveEffectiveActionRoll } from '~/utils/items';
import { findRulesetAction, parsePlayerRollFromDescription, resolveDiceRollerKey } from '~/utils/rulesets';

export type ActionOutcomeValue = 'Pass' | 'Fail';

const dcPattern = /\bDC\s+(\d+)/i;
const acPattern = /\bAC\s+(\d+)/i;
const successCountPattern = /(\d+)\s+success(?:es)?/gi;
const naturalDiePattern = /\[(\d+)\]/;
const totalAfterEqualsPattern = /=\s*(\d+)/;
const modifierPattern = /\+\s*(\d+)/;

function extractRollLine(description?: string | null): string {
  if (!description) return '';
  for (const line of description.split('\n')) {
    const index = line.indexOf('🎲 Roll:');
    if (index >= 0) return line.slice(index + '🎲 Roll:'.length).trim();
  }
  return '';
}

function parseSuccessCount(rollLine: string): number {
  const matches = [...rollLine.matchAll(successCountPattern)];
  if (!matches.length) return 0;
  return parseInt(matches[matches.length - 1][1], 10);
}

function minSuccessesFromRule(successRule?: string): number {
  if (!successRule) return 1;
  if (/one\s+or\s+more\s+success/i.test(successRule)) return 1;
  if (/\bone\s+success\b/i.test(successRule)) return 1;
  const numbered = successRule.match(/\b(\d+)\s+success(?:es)?\b/i);
  if (numbered) return parseInt(numbered[1], 10);
  return 1;
}

function isAutomaticSuccess(successRule?: string): boolean {
  if (!successRule) return false;
  return /automatically hits/i.test(successRule)
    || /no attack roll required/i.test(successRule);
}

function parseD20Roll(rollLine: string): { natural: number | null; total: number | null } {
  const naturalMatch = rollLine.match(naturalDiePattern);
  const natural = naturalMatch ? parseInt(naturalMatch[1], 10) : null;

  const totalMatch = rollLine.match(totalAfterEqualsPattern);
  if (totalMatch) return { natural, total: parseInt(totalMatch[1], 10) };

  const manualMatch = rollLine.match(/\(manual\s+(\d+)\)/i);
  if (manualMatch) {
    const base = parseInt(manualMatch[1], 10);
    const modMatch = rollLine.match(modifierPattern);
    const mod = modMatch ? parseInt(modMatch[1], 10) : 0;
    return { natural: base, total: base + mod };
  }

  if (natural === null) return { natural: null, total: null };
  const modMatch = rollLine.match(modifierPattern);
  const mod = modMatch ? parseInt(modMatch[1], 10) : 0;
  return { natural, total: natural + mod };
}

function parseDifficulty(
  successRule?: string,
  actionDifficultyClass?: number,
  rollMechanics?: RulesetDefinition['rollMechanics'],
  targetArmor?: number | null,
): number | null {
  if (actionDifficultyClass !== undefined) return actionDifficultyClass;

  if (successRule) {
    const dcMatch = successRule.match(dcPattern);
    if (dcMatch) return parseInt(dcMatch[1], 10);

    const acMatch = successRule.match(acPattern);
    if (acMatch) return parseInt(acMatch[1], 10);

    if (/vs target AC/i.test(successRule) || /meet or beat AC/i.test(successRule)) {
      return targetArmor ?? null;
    }
  }

  return rollMechanics?.skillCheck?.difficultyClass
    ?? rollMechanics?.attributeCheck?.difficultyClass
    ?? 15;
}

function resolveD6Pool(rollLine: string, successRule?: string): ActionOutcomeValue | null {
  if (!rollLine.includes('success') && !rollLine.includes('d6')) return null;
  const successes = parseSuccessCount(rollLine);
  const minSuccesses = minSuccessesFromRule(successRule);
  return successes >= minSuccesses ? 'Pass' : 'Fail';
}

function resolveD20Check(
  rollLine: string,
  successRule?: string,
  actionDifficultyClass?: number,
  rollMechanics?: RulesetDefinition['rollMechanics'],
  targetArmor?: number | null,
): ActionOutcomeValue | null {
  if (!rollLine.includes('1d')) return null;
  if (isAutomaticSuccess(successRule)) return 'Pass';

  const { natural, total } = parseD20Roll(rollLine);
  if (total === null) return null;

  if (natural === 1) return 'Fail';
  if (natural === 20) return 'Pass';

  const difficulty = parseDifficulty(successRule, actionDifficultyClass, rollMechanics, targetArmor);
  if (difficulty === null) return null;

  return total >= difficulty ? 'Pass' : 'Fail';
}

export function resolveTargetArmor(
  action: Pick<ActionQueueItemResponse, 'targetCharacterId' | 'targetNpcId'>,
  game?: Pick<GameResponse, 'characters' | 'npcsAndMonsters'> | null,
): number | null {
  if (!game) return null;
  if (action.targetCharacterId) {
    return game.characters.find(character => character.id === action.targetCharacterId)?.armor ?? null;
  }
  if (action.targetNpcId) {
    return game.npcsAndMonsters.find(npc => npc.id === action.targetNpcId)?.armor ?? null;
  }
  return null;
}

function resolveAgainstDc(rollSummary: string, dc: number): ActionOutcomeValue | null {
  const totalMatch = rollSummary.match(totalAfterEqualsPattern);
  const total = totalMatch ? parseInt(totalMatch[1], 10) : null;
  if (total === null) return null;
  return total >= dc ? 'Pass' : 'Fail';
}

/** Derives Pass/Fail from the player's initial roll in the action description. */
export function evaluateActionOutcome(
  definition: RulesetDefinition | null,
  actionKey: string | undefined,
  description: string | undefined | null,
  targetArmor?: number | null,
): ActionOutcomeValue | null {
  if (!definition) return null;

  const rulesetAction = findRulesetAction(definition, actionKey);
  const effective = resolveEffectiveActionRoll(definition, rulesetAction);
  const successRule = effective?.roll.successRule;

  if (isAutomaticSuccess(successRule)) {
    return 'Pass';
  }

  const rollLine = extractRollLine(description);
  if (!rollLine) return null;

  if (/→\s*total\s+\d+/i.test(rollLine)) return null;

  const rollerKey = resolveDiceRollerKey(definition);

  if (rollerKey === 'd6-pool') {
    return resolveD6Pool(rollLine, successRule);
  }

  if (rollerKey === 'd20-check') {
    return resolveD20Check(
      rollLine,
      successRule,
      effective?.roll.difficultyClass,
      definition.rollMechanics,
      targetArmor,
    );
  }

  const parsed = parsePlayerRollFromDescription(rollerKey, description);
  if (!parsed.hasRoll) return null;
  return parsed.primary > 0 ? 'Pass' : 'Fail';
}

/** Derives Pass/Fail for a published action when the stored outcome is missing. */
export function evaluatePublishedActionOutcome(
  definition: RulesetDefinition | null,
  action: ActionQueueItemResponse,
  game?: Pick<GameResponse, 'characters' | 'npcsAndMonsters'> | null,
): ActionOutcomeValue | null {
  if (action.outcome === 'Pass' || action.outcome === 'Fail') {
    return action.outcome;
  }

  const targetArmor = resolveTargetArmor(action, game);
  const completedPrompt = [...(action.followUpRolls ?? [])]
    .reverse()
    .find((prompt: RollPromptResponse) => prompt.resultKind !== 'Total' && prompt.rollSummary);

  if (completedPrompt?.autoResolveOutcome === 'success') return 'Pass';
  if (completedPrompt?.autoResolveOutcome === 'failure') return 'Fail';
  if (completedPrompt?.rollSummary && completedPrompt.dc != null) {
    const fromDc = resolveAgainstDc(completedPrompt.rollSummary, completedPrompt.dc);
    if (fromDc) return fromDc;
  }

  return evaluateActionOutcome(definition, action.actionKey, action.description, targetArmor);
}
