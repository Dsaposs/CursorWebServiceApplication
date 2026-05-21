import { describe, expect, it } from 'vitest';
import type { RulesetDefinition } from '~/types/api';
import {
  applyNpcTemplateToForm,
  buildStatBlockJsonFromForm,
  groupNpcTemplates,
  parseNpcTemplates,
} from '~/utils/npcTemplates';

const definition: RulesetDefinition = {
  schemaVersion: 2,
  code: 'alien-rpg',
  displayName: 'Alien RPG',
  description: 'test',
  diceNotation: 'Nd6',
  dice: [{ key: 'd6Pool', label: 'D6', notation: 'Nd6' }],
  character: {
    vitals: {
      health: { defaultMax: 3 },
      armor: { default: 0 },
    },
    attributes: [
      { key: 'strength', label: 'Strength', default: 2 },
      { key: 'agility', label: 'Agility', default: 2 },
    ],
    gameValues: [],
    classes: [],
    skills: [
      { key: 'closeCombat', label: 'Close Combat', attribute: 'strength', default: 0 },
    ],
  },
  actions: [],
  npcTemplates: [
    {
      key: 'guard',
      label: 'Guard',
      defaultStats: { classKey: 'survivorNpc', attributes: { strength: 3 }, skills: { closeCombat: 1 } },
    },
    {
      key: 'hldWes',
      label: 'Wes Osterman',
      scenario: 'hopes-last-day',
      kind: 'NPC',
      maxHealth: 3,
      health: 3,
      description: 'Facilities engineer',
      defaultStats: {
        classKey: 'survivorNpc',
        attributes: { strength: 3, agility: 3 },
        skills: { closeCombat: 1 },
        gameValues: { stress: 0 },
      },
    },
  ],
};

describe('npcTemplates', () => {
  it('parses and groups templates by scenario', () => {
    const templates = parseNpcTemplates(definition);
    expect(templates).toHaveLength(2);

    const groups = groupNpcTemplates(templates);
    expect(groups).toHaveLength(2);
    expect(groups[0].label).toBe('General');
    expect(groups[1].label).toBe("Hope's Last Day");
  });

  it('applies template vitals and stat block fields', () => {
    const template = parseNpcTemplates(definition).find(t => t.key === 'hldWes')!;
    const applied = applyNpcTemplateToForm(
      template,
      definition,
      definition.character.attributes,
      definition.character.skills,
    );

    expect(applied.name).toBe('Wes Osterman');
    expect(applied.maxHealth).toBe(3);
    expect(applied.attrs.strength).toBe(3);

    const statBlock = JSON.parse(buildStatBlockJsonFromForm(
      applied.attrs,
      applied.skills,
      applied.inventory,
      template,
    )) as { classKey?: string; gameValues?: { stress?: number } };

    expect(statBlock.classKey).toBe('survivorNpc');
    expect(statBlock.gameValues?.stress).toBe(0);
  });
});
