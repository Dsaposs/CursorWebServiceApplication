import type { ActionQueueItemResponse, RollPromptResponse, RulesetDefinition, RulesetRollChainStepDefinition, GameResponse } from '~/types/api';
import type { ActionOutcomeValue } from '~/utils/actionOutcome';
import { evaluateActionOutcome, resolveTargetArmor } from '~/utils/actionOutcome';
import { isSameGuid } from '~/utils/rollPrompt';
import { findRulesetAction } from '~/utils/rulesets';
import { isStatCheckAction, parseStatCheckFromAction, sessionPromptMatchesStatCheck } from '~/utils/statCheckAction';

export type ActionRollFlowStatus =
  | 'not-applicable'
  | 'awaiting-dm-request'
  | 'awaiting-player'
  | 'rolls-received';

export function rollPromptsForAction(
  actionId: string,
  sessionRollPrompts: RollPromptResponse[],
  followUpRolls: RollPromptResponse[] = [],
): RollPromptResponse[] {
  const merged = new Map<string, RollPromptResponse>();

  for (const prompt of sessionRollPrompts) {
    if (
      isSameGuid(prompt.actionRequestId, actionId)
      || isSameGuid(prompt.resultActionRequestId, actionId)
    ) {
      merged.set(prompt.id.toLowerCase(), prompt);
    }
  }

  for (const prompt of followUpRolls) {
    merged.set(prompt.id.toLowerCase(), prompt);
  }

  return [...merged.values()].sort((a, b) => a.createdAt.localeCompare(b.createdAt));
}

/** Action-linked roll prompts plus matching session stat-check prompts for the same player/stat. */
export function relatedRollPromptsForAction(
  action: ActionQueueItemResponse,
  sessionRollPrompts: RollPromptResponse[],
  definition: RulesetDefinition | null,
): RollPromptResponse[] {
  const linked = rollPromptsForAction(action.id, sessionRollPrompts, action.followUpRolls ?? []);
  if (!isStatCheckAction(action) || action.isSkillCheckResponse || !action.actorCharacterId) {
    return linked;
  }

  const statCheck = parseStatCheckFromAction(action, definition);
  if (!statCheck) return linked;

  const merged = new Map<string, RollPromptResponse>();
  for (const prompt of linked) {
    merged.set(prompt.id.toLowerCase(), prompt);
  }

  for (const prompt of sessionRollPrompts) {
    if (!prompt.isSessionPrompt) continue;
    if (!isSameGuid(prompt.targetCharacterId, action.actorCharacterId)) continue;
    if (!sessionPromptMatchesStatCheck(prompt, statCheck)) continue;
    merged.set(prompt.id.toLowerCase(), prompt);
  }

  return [...merged.values()].sort((a, b) => a.createdAt.localeCompare(b.createdAt));
}

function statCheckRollAlreadyReceived(action: ActionQueueItemResponse): boolean {
  if (action.isSkillCheckResponse) return true;
  return Boolean(action.description?.includes('🎲 Roll:'));
}

export function actionNeedsPlayerRoll(
  action: ActionQueueItemResponse,
  definition: RulesetDefinition | null,
): boolean {
  if (isStatCheckAction(action) && action.actorCharacterId) {
    return !statCheckRollAlreadyReceived(action);
  }
  if (!action.actorCharacterId) return false;
  if (action.actionKey) {
    const def = findRulesetAction(definition, action.actionKey);
    return Boolean(def?.roll || def?.rollChain?.length);
  }
  return false;
}

export function getActionRollFlowStatus(
  action: ActionQueueItemResponse,
  sessionRollPrompts: RollPromptResponse[],
  definition: RulesetDefinition | null,
): ActionRollFlowStatus {
  const related = relatedRollPromptsForAction(action, sessionRollPrompts, definition);
  const pending = related.filter(p => p.status === 'Pending');
  const completed = related.filter(p => p.status === 'Completed');

  if (pending.length) return 'awaiting-player';
  if (completed.length) return 'rolls-received';
  if (!actionNeedsPlayerRoll(action, definition)) return 'not-applicable';
  return 'awaiting-dm-request';
}

export function actionRollFlowLabel(status: ActionRollFlowStatus): string {
  switch (status) {
    case 'awaiting-dm-request':
      return 'Awaiting roll request';
    case 'awaiting-player':
      return 'Waiting for player roll';
    case 'rolls-received':
      return 'Roll received';
    default:
      return '';
  }
}

export function actionRollFlowBadgeClass(status: ActionRollFlowStatus): string {
  switch (status) {
    case 'awaiting-dm-request':
      return 'pending';
    case 'awaiting-player':
      return 'pending';
    case 'rolls-received':
      return 'pass';
    default:
      return '';
  }
}

/** Prefer completed roll prompts; fall back to legacy roll line in description (NPC / old data). */
export function evaluateActionOutcomeFromRolls(
  definition: RulesetDefinition | null,
  action: ActionQueueItemResponse,
  sessionRollPrompts: RollPromptResponse[],
  game?: Pick<GameResponse, 'characters' | 'npcsAndMonsters'> | null,
): ActionOutcomeValue | null {
  const targetArmor = resolveTargetArmor(action, game);
  const related = rollPromptsForAction(action.id, sessionRollPrompts, action.followUpRolls ?? [])
    .filter(p => p.status === 'Completed' && p.rollSummary);

  const resolvedPrompt = [...related]
    .reverse()
    .find(p => p.autoResolveOutcome === 'success' || p.autoResolveOutcome === 'failure')
    ?? [...related].reverse().find(p => p.resultKind !== 'Total' && p.rollSummary);

  if (resolvedPrompt?.autoResolveOutcome === 'success') return 'Pass';
  if (resolvedPrompt?.autoResolveOutcome === 'failure') return 'Fail';
  if (resolvedPrompt?.rollSummary && resolvedPrompt.dc != null) {
    const totalMatch = resolvedPrompt.rollSummary.match(/=\s*(\d+)/);
    if (totalMatch) {
      const total = parseInt(totalMatch[1], 10);
      return total >= resolvedPrompt.dc ? 'Pass' : 'Fail';
    }
  }

  if (resolvedPrompt?.rollSummary) {
    const derivedFromRoll = evaluateActionOutcome(
      definition,
      resolvedPrompt.actionKey ?? action.actionKey,
      `🎲 Roll: ${resolvedPrompt.rollSummary}`,
      targetArmor,
    );
    if (derivedFromRoll) return derivedFromRoll;
  }

  return evaluateActionOutcome(definition, action.actionKey, action.description, targetArmor);
}

export function formatRollFlowHint(
  status: ActionRollFlowStatus,
  actorName: string,
  options?: { isStatCheck?: boolean },
): string {
  switch (status) {
    case 'awaiting-dm-request':
      return options?.isStatCheck
        ? `Prompt ${actorName} to roll for this stat check before publishing.`
        : `Ask ${actorName} to roll before you publish the outcome.`;
    case 'awaiting-player':
      return `${actorName} has been prompted — waiting for their roll.`;
    case 'rolls-received':
      return 'Review the roll below, then publish your resolution.';
    default:
      return '';
  }
}

export interface RollChainProgress {
  isComplete: boolean;
  hasPendingPrompt: boolean;
  pendingStepKey: string | null;
  terminatedEarly: boolean;
  needsChainStart: boolean;
  nextManualStepKey: string | null;
}

function promptForChainStep(
  stepKey: string,
  prompts: RollPromptResponse[],
): RollPromptResponse | null {
  return [...prompts]
    .reverse()
    .find(prompt => prompt.chainStepKey === stepKey) ?? null;
}

function stepEndsChainOnFailure(step: { onSuccess?: string; onFailure?: string }): boolean {
  if (step.onFailure && step.onFailure !== 'end') return false;
  return Boolean(step.onSuccess && step.onSuccess !== 'end');
}

/** Tracks whether every required roll-chain step has finished (including early miss). */
export function getRollChainProgress(
  definition: RulesetDefinition | null,
  action: ActionQueueItemResponse,
  sessionRollPrompts: RollPromptResponse[],
): RollChainProgress | null {
  const actionDef = findRulesetAction(definition, action.actionKey);
  const steps = actionDef?.rollChain ?? [];
  if (!steps.length) return null;

  const prompts = rollPromptsForAction(action.id, sessionRollPrompts, action.followUpRolls ?? []);
  const pending = prompts.find(prompt => prompt.status === 'Pending');
  if (pending) {
    return {
      isComplete: false,
      hasPendingPrompt: true,
      pendingStepKey: pending.chainStepKey ?? null,
      terminatedEarly: false,
      needsChainStart: false,
      nextManualStepKey: null,
    };
  }

  const chainStarted = Boolean(action.rollChainStateJson) || prompts.length > 0;
  if (!chainStarted) {
    return {
      isComplete: false,
      hasPendingPrompt: false,
      pendingStepKey: null,
      terminatedEarly: false,
      needsChainStart: true,
      nextManualStepKey: steps[0]?.step ?? null,
    };
  }

  for (const step of steps) {
    const completed = promptForChainStep(step.step, prompts.filter(prompt => prompt.status === 'Completed'));
    if (!completed) {
      return {
        isComplete: false,
        hasPendingPrompt: false,
        pendingStepKey: null,
        terminatedEarly: false,
        needsChainStart: false,
        nextManualStepKey: step.step,
      };
    }

    if (completed.autoResolveOutcome === 'failure' && stepEndsChainOnFailure(step)) {
      return {
        isComplete: true,
        hasPendingPrompt: false,
        pendingStepKey: null,
        terminatedEarly: true,
        needsChainStart: false,
        nextManualStepKey: null,
      };
    }
  }

  return {
    isComplete: true,
    hasPendingPrompt: false,
    pendingStepKey: null,
    terminatedEarly: false,
    needsChainStart: false,
    nextManualStepKey: null,
  };
}

/** True when the DM may publish or reject — all required rolls are done. */
export function canPublishActionResolution(
  definition: RulesetDefinition | null,
  action: ActionQueueItemResponse,
  sessionRollPrompts: RollPromptResponse[],
): boolean {
  const chainProgress = getRollChainProgress(definition, action, sessionRollPrompts);
  if (chainProgress) {
    return chainProgress.isComplete;
  }

  if (!actionNeedsPlayerRoll(action, definition)) {
    return true;
  }

  return getActionRollFlowStatus(action, sessionRollPrompts, definition) === 'rolls-received';
}

export function rollChainStatusHint(
  progress: RollChainProgress | null,
  actorName: string,
): string {
  if (!progress || progress.isComplete) return '';

  if (progress.needsChainStart) {
    return `Start the roll chain and send the first prompt to ${actorName} before publishing.`;
  }

  if (progress.hasPendingPrompt) {
    return `${actorName} has a roll waiting on their screen — publish is locked until they submit it.`;
  }

  if (progress.nextManualStepKey) {
    return `Send the next roll prompt to ${actorName} before publishing.`;
  }

  return `Complete all roll chain steps before publishing.`;
}

export function actionHasRollChain(
  definition: RulesetDefinition | null,
  actionKey?: string | null,
): boolean {
  if (!actionKey) return false;
  const actionDef = findRulesetAction(definition, actionKey);
  return Boolean(actionDef?.rollChain?.length);
}

export interface PlayerRollChainView {
  steps: RulesetRollChainStepDefinition[];
  currentStepKey: string | null;
  skippedStepKeys: Set<string>;
  isComplete: boolean;
  terminatedEarly: boolean;
  activePrompt: RollPromptResponse | null;
  awaitingNextPrompt: boolean;
}

/** Player-facing roll chain state for an in-progress combat action. */
export function getPlayerRollChainView(
  definition: RulesetDefinition | null,
  action: ActionQueueItemResponse | null,
  sessionRollPrompts: RollPromptResponse[],
): PlayerRollChainView | null {
  if (!action || !actionHasRollChain(definition, action.actionKey)) return null;

  const actionDef = findRulesetAction(definition, action.actionKey);
  const steps = actionDef?.rollChain ?? [];
  if (!steps.length) return null;

  const prompts = rollPromptsForAction(action.id, sessionRollPrompts, action.followUpRolls ?? []);
  const progress = getRollChainProgress(definition, action, sessionRollPrompts);
  if (!progress) return null;

  const skippedStepKeys = new Set<string>();
  if (progress.terminatedEarly) {
    let foundFailure = false;
    for (const step of steps) {
      if (foundFailure) {
        skippedStepKeys.add(step.step);
        continue;
      }
      const prompt = [...prompts].reverse().find(item => item.chainStepKey === step.step);
      if (prompt?.autoResolveOutcome === 'failure') {
        foundFailure = true;
      }
    }
  }

  const currentStepKey = progress.hasPendingPrompt
    ? progress.pendingStepKey
    : progress.nextManualStepKey ?? (progress.isComplete ? null : steps[0]?.step ?? null);

  const activePrompt = progress.hasPendingPrompt
    ? prompts.find(prompt => prompt.status === 'Pending') ?? null
    : null;

  const awaitingNextPrompt = !progress.isComplete
    && !progress.hasPendingPrompt
    && !progress.needsChainStart
    && Boolean(progress.nextManualStepKey);

  return {
    steps,
    currentStepKey,
    skippedStepKeys,
    isComplete: progress.isComplete,
    terminatedEarly: progress.terminatedEarly,
    activePrompt,
    awaitingNextPrompt,
  };
}
