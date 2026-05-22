import { describe, expect, it } from 'vitest';
import type { ActionQueueItemResponse, SessionStateResponse } from '~/types/api';
import {
  getAdaptivePollIntervalMs,
  maxActionSequence,
  mergeLiveState,
  mergeSessionState,
  replaceActionInState,
} from '~/utils/sessionStateMerge';

function action(id: string, sequence: number, status = 'Pending'): ActionQueueItemResponse {
  return {
    id,
    sequence,
    status,
    actionText: 'test',
    actorName: 'Hero',
    submittedAt: '2026-01-01T00:00:00Z',
  } as ActionQueueItemResponse;
}

function session(actions: ActionQueueItemResponse[], overrides: Partial<SessionStateResponse> = {}): SessionStateResponse {
  return {
    id: 'session-1',
    gameId: 'game-1',
    joinCode: 'ABC123',
    joinUrl: '/join/ABC123',
    isActive: true,
    state: 'Exploration',
    version: 1,
    startedAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    game: { id: 'game-1', name: 'Test', rulesetCode: 'dnd5e' } as SessionStateResponse['game'],
    actions,
    initiative: [],
    rollPrompts: [],
    ...overrides,
  };
}

describe('maxActionSequence', () => {
  it('returns 0 for an empty list', () => {
    expect(maxActionSequence([])).toBe(0);
  });

  it('returns the highest sequence', () => {
    expect(maxActionSequence([action('a', 2), action('b', 7)])).toBe(7);
  });
});

describe('mergeSessionState', () => {
  it('updates changed actions and keeps prior actions when incremental', () => {
    const prev = session([action('a', 1), action('b', 2, 'Pending')]);
    const next = session([action('b', 2, 'Published')], { version: 2 });

    const merged = mergeSessionState(prev, next);
    expect(merged.actions).toHaveLength(2);
    expect(merged.actions.find(item => item.id === 'b')?.status).toBe('Published');
    expect(merged.version).toBe(2);
  });

  it('keeps prior actions when incremental response has no actions', () => {
    const prev = session([action('a', 1)]);
    const next = session([], { version: 2, rollPrompts: [{ id: 'prompt-1' } as SessionStateResponse['rollPrompts'][number]] });

    const merged = mergeSessionState(prev, next);
    expect(merged.actions).toHaveLength(1);
    expect(merged.rollPrompts).toHaveLength(1);
  });
});

describe('replaceActionInState', () => {
  it('replaces an existing action', () => {
    const prev = session([action('a', 1, 'Pending')]);
    const updated = action('a', 1, 'Published');

    const merged = replaceActionInState(prev, updated);
    expect(merged.actions[0]?.status).toBe('Published');
  });
});

describe('getAdaptivePollIntervalMs', () => {
  it('uses a long interval when the hub is connected', () => {
    expect(getAdaptivePollIntervalMs(session([]), true)).toBe(8000);
  });

  it('uses a short interval when roll prompts are pending', () => {
    expect(getAdaptivePollIntervalMs(session([], { rollPrompts: [{ id: 'p1' } as SessionStateResponse['rollPrompts'][number]] }), false)).toBe(1000);
  });
});

describe('mergeLiveState', () => {
  it('updates board data while preserving ruleset metadata on the game', () => {
    const prev = session([action('a', 1, 'Pending')], {
      game: {
        id: 'game-1',
        name: 'Test',
        rulesetCode: 'dnd5e',
        rulesetName: 'D&D 5e',
        inviteCode: 'GAME',
        inviteUrl: '/join/game/GAME',
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
        characters: [{ id: 'c1', name: 'Hero', playerName: 'P', maxHealth: 10, health: 10, armor: 10, inventoryJson: '[]', rulesetDataJson: '{}', statusEffectsJson: '[]', classKey: 'fighter' }],
        npcsAndMonsters: [],
        sessions: [],
      },
      version: 1,
    });

    const live = {
      ...prev,
      version: 2,
      actions: [action('a', 1, 'Published')],
      rollPrompts: [],
      initiative: [],
      game: {
        updatedAt: '2026-01-02T00:00:00Z',
        characters: [{ ...prev.game.characters[0]!, health: 7 }],
        npcsAndMonsters: [],
      },
    };

    const merged = mergeLiveState(prev, live);
    expect(merged.version).toBe(2);
    expect(merged.actions[0]?.status).toBe('Published');
    expect(merged.game.rulesetCode).toBe('dnd5e');
    expect(merged.game.characters[0]?.health).toBe(7);
  });
});
