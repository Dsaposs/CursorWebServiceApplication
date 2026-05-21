import type { ActionQueueItemResponse, RulesetDefinition } from '~/types/api';
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
    if (text === describeSkillCheck(skill, definition).actionText) {
      return { checkMode: 'Skill', skillKey: skill.key };
    }
  }

  for (const attribute of definition.character.attributes) {
    if (text === describeAttributeCheck(attribute).actionText) {
      return { checkMode: 'Attribute', attributeKey: attribute.key };
    }
  }

  return null;
}
