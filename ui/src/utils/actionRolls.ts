import type { ActionQueueItemResponse, RollPromptResponse, RulesetDefinition } from '~/types/api';
import type { ActionOutcomeValue } from '~/utils/actionOutcome';
import { evaluateActionOutcome } from '~/utils/actionOutcome';
import { findRulesetAction } from '~/utils/rulesets';
import { isStatCheckAction } from '~/utils/statCheckAction';

export type ActionRollFlowStatus =
  | 'not-applicable'
  | 'awaiting-dm-request'
  | 'awaiting-player'
  | 'rolls-received';

export function rollPromptsForAction(
  actionId: string,
  sessionRollPrompts: RollPromptResponse[],
): RollPromptResponse[] {
  return sessionRollPrompts
    .filter(p => p.actionRequestId === actionId)
    .sort((a, b) => a.createdAt.localeCompare(b.createdAt));
}

export function actionNeedsPlayerRoll(
  action: ActionQueueItemResponse,
  definition: RulesetDefinition | null,
): boolean {
  if (isStatCheckAction(action) && action.actorCharacterId) return true;
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
  const related = rollPromptsForAction(action.id, sessionRollPrompts);
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
): ActionOutcomeValue | null {
  const related = rollPromptsForAction(action.id, sessionRollPrompts)
    .filter(p => p.status === 'Completed' && p.rollSummary);

  const passFailPrompt = [...related]
    .reverse()
    .find(p => p.resultKind !== 'Total' && p.rollSummary);

  if (passFailPrompt?.rollSummary) {
    return evaluateActionOutcome(
      definition,
      passFailPrompt.actionKey ?? action.actionKey,
      `🎲 Roll: ${passFailPrompt.rollSummary}`,
    );
  }

  return evaluateActionOutcome(definition, action.actionKey, action.description);
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
