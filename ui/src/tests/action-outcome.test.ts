import { describe, expect, it } from 'vitest';
import type { ActionQueueItemResponse, GameResponse, RulesetDefinition } from '~/types/api';
import { evaluatePublishedActionOutcome } from '~/utils/actionOutcome';

const dndRuleset: RulesetDefinition = {
  schemaVersion: 1,
  code: 'dnd-5e',
  displayName: 'D&D 5e',
  description: 'Test',
  diceNotation: '1d20',
  diceRollerKey: 'd20-check',
  dice: [{ key: 'd20', label: 'D20', notation: '1d20' }],
  character: {
    vitals: {},
    attributes: [{ key: 'intelligence', label: 'Intelligence', default: 10 }],
    gameValues: [],
    classes: [{ key: 'wizard', label: 'Wizard', availableSkills: ['arcana'], startingSkillPoints: 2 }],
    skills: [{ key: 'arcana', label: 'Arcana', attribute: 'intelligence', default: 0 }],
  },
  actions: [{
    key: 'fireBolt',
    label: 'Fire Bolt',
    allowedClasses: ['wizard'],
    roll: {
      dice: 'd20',
      attribute: 'intelligence',
      skill: 'arcana',
      modifiers: [],
      successRule: 'Ranged spell attack: roll 1d20 + Intelligence modifier + proficiency vs target AC. On a hit, deal 1d10 fire damage.',
    },
    context: 'combat',
  }, {
    key: 'magicMissile',
    label: 'Magic Missile',
    allowedClasses: ['wizard'],
    roll: {
      dice: 'd20',
      attribute: 'intelligence',
      skill: 'arcana',
      modifiers: [],
      successRule: 'Automatically hits. Three darts each deal 1d4 + 1 force damage (no attack roll required).',
    },
    context: 'combat',
  }],
  rollMechanics: {
    skillCheck: { difficultyClass: 15 },
    attributeCheck: { difficultyClass: 15 },
  },
};

function publishedAction(overrides: Partial<ActionQueueItemResponse>): ActionQueueItemResponse {
  return {
    id: 'action-1',
    sequence: 1,
    actorName: 'Jeff',
    actionText: 'Fire Bolt',
    status: 'Published',
    statChangesJson: '[]',
    pendingChainEffectsJson: '[]',
    followUpRolls: [],
    submittedAt: '2026-05-22T12:00:00.000Z',
    ...overrides,
  };
}

describe('evaluatePublishedActionOutcome', () => {
  it('derives Pass when NPC attack roll beats target NPC armor', () => {
    const targetNpcId = 'npc-susan';
    const game = {
      characters: [],
      npcsAndMonsters: [{ id: targetNpcId, name: 'Susan', armor: 16 }],
    } as GameResponse;

    const outcome = evaluatePublishedActionOutcome(
      dndRuleset,
      publishedAction({
        actionKey: 'fireBolt',
        targetNpcId,
        description: '🎲 Roll: 1d20: [18] + 4 = 22',
      }),
      game,
    );

    expect(outcome).toBe('Pass');
  });

  it('derives Fail when NPC attack roll misses target NPC armor', () => {
    const targetNpcId = 'npc-susan';
    const game = {
      characters: [],
      npcsAndMonsters: [{ id: targetNpcId, name: 'Susan', armor: 16 }],
    } as GameResponse;

    const outcome = evaluatePublishedActionOutcome(
      dndRuleset,
      publishedAction({
        actionKey: 'fireBolt',
        targetNpcId,
        description: '🎲 Roll: 1d20: [8] + 4 = 12',
      }),
      game,
    );

    expect(outcome).toBe('Fail');
  });

  it('derives Pass for automatic hit actions without a roll line', () => {
    const outcome = evaluatePublishedActionOutcome(
      dndRuleset,
      publishedAction({
        actionKey: 'magicMissile',
        description: null,
      }),
    );

    expect(outcome).toBe('Pass');
  });
});
