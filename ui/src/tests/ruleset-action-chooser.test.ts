import { computed, nextTick } from 'vue';
import { describe, expect, it } from 'vitest';
import { useRulesetActionChooser } from '~/composables/useRulesetActionChooser';
import type { RulesetDefinition } from '~/types/api';
import { availableSkillsForClass, describeSkillCheck } from '~/utils/rulesets';

const testRuleset: RulesetDefinition = {
  schemaVersion: 1,
  code: 'test-rules',
  displayName: 'Test Rules',
  description: 'Rules used by frontend tests.',
  diceNotation: 'd6 pool',
  dice: [{ key: 'd6Pool', label: 'D6 Pool', notation: 'attribute + skill d6' }],
  character: {
    vitals: {},
    attributes: [
      { key: 'agility', label: 'Agility', default: 2 },
      { key: 'strength', label: 'Strength', default: 2 },
    ],
    gameValues: [],
    classes: [
      {
        key: 'marine',
        label: 'Marine',
        availableSkills: ['rangedCombat'],
        startingSkillPoints: 10,
      },
    ],
    skills: [
      { key: 'rangedCombat', label: 'Ranged Combat', attribute: 'agility', default: 0 },
      { key: 'heavyMachinery', label: 'Heavy Machinery', attribute: 'strength', default: 0 },
    ],
  },
  actions: [
    {
      key: 'shoot',
      label: 'Shoot',
      allowedClasses: ['marine'],
      roll: {
        dice: 'd6Pool',
        attribute: 'agility',
        skill: 'rangedCombat',
        modifiers: [],
        successRule: 'Each 6 is a success.',
      },
    },
  ],
  npcTemplates: [],
};

describe('ruleset action utilities', () => {
  it('filters skills by class availability', () => {
    const skills = availableSkillsForClass(testRuleset, 'marine');

    expect(skills.map(skill => skill.key)).toEqual(['rangedCombat']);
  });

  it('describes skill checks with their governing attribute', () => {
    const skill = testRuleset.character.skills[0];

    expect(describeSkillCheck(skill, testRuleset)).toEqual({
      actionText: 'Skill check: Ranged Combat',
      rollSummary: 'Agility + Ranged Combat',
    });
  });
});

describe('useRulesetActionChooser', () => {
  it('builds predefined action payloads with the action key', () => {
    const chooser = useRulesetActionChooser(computed(() => testRuleset), computed(() => 'marine'));

    chooser.selectedActionKey.value = 'shoot';

    expect(chooser.buildSubmitPayload('Covering fire')).toEqual({
      actionKey: 'shoot',
      actionText: 'Shoot',
      description: 'Covering fire',
    });
  });

  it('builds skill check payloads without an action key', async () => {
    const chooser = useRulesetActionChooser(computed(() => testRuleset), computed(() => 'marine'));

    chooser.actionMode.value = 'skill';
    await nextTick();
    chooser.selectedSkillKey.value = 'rangedCombat';

    expect(chooser.buildSubmitPayload('I line up the shot.')).toEqual({
      actionKey: undefined,
      actionText: 'Skill check: Ranged Combat',
      description: 'Suggested roll: Agility + Ranged Combat.\nI line up the shot.',
    });
  });
});
