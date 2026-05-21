import { describe, expect, it } from 'vitest';
import type { ActionQueueItemResponse, RulesetDefinition } from '~/types/api';
import { actionNeedsPlayerRoll } from '~/utils/actionRolls';
import { describeSkillCheck } from '~/utils/rulesets';
import { isStatCheckAction, parseStatCheckFromAction } from '~/utils/statCheckAction';

const testRuleset: RulesetDefinition = {
  schemaVersion: 1,
  code: 'test-rules',
  displayName: 'Test Rules',
  description: 'Rules used by frontend tests.',
  diceNotation: 'd6 pool',
  dice: [{ key: 'd6Pool', label: 'D6 Pool', notation: 'attribute + skill d6' }],
  character: {
    vitals: {},
    attributes: [{ key: 'agility', label: 'Agility', default: 2 }],
    gameValues: [],
    classes: [{ key: 'marine', label: 'Marine', availableSkills: ['rangedCombat'], startingSkillPoints: 10 }],
    skills: [{ key: 'rangedCombat', label: 'Ranged Combat', attribute: 'agility', default: 0 }],
  },
  actions: [],
  items: [],
  rollMechanics: {},
};

const skillCheckText = describeSkillCheck(testRuleset.character.skills[0], testRuleset).actionText;

describe('statCheckAction', () => {
  it('detects player-submitted stat checks', () => {
    const action = {
      actionText: skillCheckText,
      actorCharacterId: 'char-1',
    } as ActionQueueItemResponse;
    expect(isStatCheckAction(action)).toBe(true);
    expect(actionNeedsPlayerRoll(action, testRuleset)).toBe(true);
    expect(parseStatCheckFromAction(action, testRuleset)).toEqual({
      checkMode: 'Skill',
      skillKey: 'rangedCombat',
    });
  });
});
