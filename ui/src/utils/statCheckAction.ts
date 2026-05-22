import type { ActionQueueItemResponse, RollPromptResponse, RulesetDefinition } from '~/types/api';
import { describeAttributeCheck, describeSkillCheck } from '~/utils/rulesets';

export interface StatCheckRollRequest {
  checkMode: 'Skill' | 'Attribute';
  skillKey?: string;
  attributeKey?: string;
}

export function isStatCheckAction(action: ActionQueueItemResponse): boolean {
  if (action.isSkillCheckResponse) return true;
  const text = action.actionText?.trim() ?? '';
  return /^Skill check:/i.test(text) || /^Attribute check:/i.test(text);
}

export function parseStatCheckFromAction(
  action: ActionQueueItemResponse,
  definition: RulesetDefinition | null,
): StatCheckRollRequest | null {
  if (!definition) return null;

  const text = action.actionText?.trim() ?? '';
  if (!text) return null;

  for (const skill of definition.character.skills) {
    if (text === describeSkillCheck(skill, definition).actionText || text === `${skill.label} check`) {
      return { checkMode: 'Skill', skillKey: skill.key };
    }
  }

  for (const attribute of definition.character.attributes) {
    if (text === describeAttributeCheck(attribute).actionText || text === `${attribute.label} check`) {
      return { checkMode: 'Attribute', attributeKey: attribute.key };
    }
  }

  return null;
}

export function sessionPromptMatchesStatCheck(
  prompt: RollPromptResponse,
  statCheck: StatCheckRollRequest,
): boolean {
  if (statCheck.checkMode === 'Skill') {
    return prompt.checkMode === 'Skill' && prompt.skillKey === statCheck.skillKey;
  }

  return prompt.checkMode === 'Attribute' && prompt.attributeKey === statCheck.attributeKey;
}
