import { computed, nextTick } from 'vue';
import { describe, expect, it } from 'vitest';
import { useRulesetActionChooser } from '~/composables/useRulesetActionChooser';
import type { RulesetDefinition } from '~/types/api';
import { parseCharacterStats } from '~/utils/dice';
import { availableSkillsForClass, buildDiceRollContext, describeSkillCheck } from '~/utils/rulesets';

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

  it('builds action roll contexts from nested character stats', () => {
    const d6PoolRuleset: RulesetDefinition = {
      ...testRuleset,
      schemaVersion: 2,
      diceRollerKey: 'd6-pool',
      dice: [{ key: 'd6Pool', label: 'D6 Pool', notation: '1d6', successTarget: 6 }],
      actions: [{
        key: 'shoot',
        label: 'Shoot',
        allowedClasses: ['marine'],
        roll: {
          dice: 'd6Pool',
          dicePoolMode: 'attribute+skill',
          attribute: 'agility',
          skill: 'rangedCombat',
          modifiers: [{ source: 'gameValue', key: 'stress', dicePerPoint: 1, isStressDice: true }],
          successRule: 'Each 6 is a success.',
        },
      }],
    };
    const stats = parseCharacterStats(JSON.stringify({
      attributes: { agility: 4 },
      skills: { rangedCombat: 2 },
      gameValues: { stress: 3 },
    }));

    const context = buildDiceRollContext({
      definition: d6PoolRuleset,
      mode: 'action',
      actionKey: 'shoot',
      skillKey: '',
      attributeKey: '',
      attributes: stats.attributes,
      skills: stats.skills,
      gameValues: stats.gameValues,
    });

    expect(context).toMatchObject({
      rollerKey: 'd6-pool',
      label: 'Shoot',
      config: {
        kind: 'd6-pool',
        baseDiceCount: 6,
        stressDiceCount: 3,
        sides: 6,
        successTarget: 6,
      },
    });
    expect(context?.poolBreakdown).toEqual([
      'Agility 4',
      'Ranged Combat 2',
      '+3 stress (stress 3)',
    ]);
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

  it('builds stat check payloads without an action key', async () => {
    const chooser = useRulesetActionChooser(computed(() => testRuleset), computed(() => 'marine'));

    chooser.actionMode.value = 'stat-check';
    await nextTick();
    chooser.selectedStatKey.value = 'skill:rangedCombat';

    expect(chooser.suggestedRollSummary.value).toBe('Agility + Ranged Combat');
    expect(chooser.buildSubmitPayload('I line up the shot.')).toEqual({
      actionKey: undefined,
      actionText: 'Skill check: Ranged Combat',
      description: 'I line up the shot.',
    });
  });
});
