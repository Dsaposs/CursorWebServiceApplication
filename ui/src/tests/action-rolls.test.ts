import { describe, expect, it } from 'vitest';
import type { ActionQueueItemResponse, RollPromptResponse, RulesetDefinition } from '~/types/api';
import {
  actionHasRollChain,
  actionNeedsPlayerRoll,
  canPublishActionResolution,
  getActionRollFlowStatus,
  getPlayerRollChainView,
  getRollChainProgress,
  relatedRollPromptsForAction,
  rollPromptsForAction,
} from '~/utils/actionRolls';
import { findActivePlayerRollPrompt } from '~/utils/rollPrompt';

function makePrompt(overrides: Partial<RollPromptResponse> & Pick<RollPromptResponse, 'id'>): RollPromptResponse {
  return {
    isSessionPrompt: false,
    targetCharacterId: 'char-1',
    targetCharacterName: 'Player',
    checkMode: 'Action',
    resultKind: 'PassFail',
    status: 'Pending',
    dmRolled: false,
    createdAt: '2026-05-22T12:00:00Z',
    ...overrides,
  };
}

function makeAction(overrides: Partial<ActionQueueItemResponse> = {}): ActionQueueItemResponse {
  return {
    id: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
    sequence: 1,
    actorName: 'Player',
    actionText: 'Attack',
    status: 'AwaitingRoll',
    submittedAt: '2026-05-22T12:00:00Z',
    ...overrides,
  };
}

const statCheckRuleset: RulesetDefinition = {
  schemaVersion: 1,
  code: 'test',
  displayName: 'Test',
  description: 'Test',
  diceNotation: '1d20',
  dice: [{ key: 'd20', label: 'D20', notation: '1d20' }],
  character: {
    vitals: {},
    attributes: [{ key: 'strength', label: 'Strength', default: 10 }],
    gameValues: [],
    classes: [],
    skills: [{ key: 'athletics', label: 'Athletics', attribute: 'strength', default: 0 }],
  },
  actions: [],
};

describe('rollPromptsForAction', () => {
  it('matches action prompts case-insensitively', () => {
    const actionId = 'AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE';
    const prompt = makePrompt({
      id: 'prompt-1',
      actionRequestId: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
      status: 'Completed',
      rollSummary: '1d20 (15) + 3 = 18',
    });

    expect(rollPromptsForAction(actionId, [prompt])).toEqual([prompt]);
  });

  it('merges completed follow-up rolls from the action payload', () => {
    const action = makeAction();
    const prompt = makePrompt({
      id: 'prompt-2',
      actionRequestId: action.id,
      status: 'Completed',
      rollSummary: 'Hit for 12',
    });

    expect(rollPromptsForAction(action.id, [], [prompt])).toEqual([prompt]);
  });
});

describe('getActionRollFlowStatus', () => {
  it('reports rolls-received when only followUpRolls are populated', () => {
    const actionId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
    const action = makeAction({
      id: actionId,
      actionKey: 'shoot',
      followUpRolls: [
        makePrompt({
          id: 'prompt-3',
          actionRequestId: actionId,
          status: 'Completed',
          rollSummary: '3 successes',
        }),
      ],
    });

    expect(getActionRollFlowStatus(action, [], null)).toBe('rolls-received');
  });

  it('hides prompt controls after an action roll prompt is completed', () => {
    const actionId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
    const action = makeAction({
      id: actionId,
      actionText: 'Skill check: Athletics',
      actorCharacterId: 'char-1',
      followUpRolls: [
        makePrompt({
          id: 'prompt-stat',
          actionRequestId: actionId,
          checkMode: 'Skill',
          skillKey: 'athletics',
          status: 'Completed',
          rollSummary: '1d20: [12] + 3 = 15',
        }),
      ],
    });

    expect(getActionRollFlowStatus(action, [], statCheckRuleset)).toBe('rolls-received');
  });

  it('tracks pending session stat-check prompts for a matching player action', () => {
    const action = makeAction({
      actionText: 'Skill check: Athletics',
      actorCharacterId: 'char-1',
    });
    const sessionPrompt = makePrompt({
      id: 'session-prompt',
      isSessionPrompt: true,
      targetCharacterId: 'char-1',
      checkMode: 'Skill',
      skillKey: 'athletics',
      status: 'Pending',
    });

    expect(getActionRollFlowStatus(action, [sessionPrompt], statCheckRuleset)).toBe('awaiting-player');
    expect(relatedRollPromptsForAction(action, [sessionPrompt], statCheckRuleset)).toEqual([sessionPrompt]);
  });
});

describe('findActivePlayerRollPrompt', () => {
  it('prefers the newest pending prompt for the player', () => {
    const characterId = 'char-1';
    const older = makePrompt({
      id: 'attack-prompt',
      targetCharacterId: characterId,
      chainStepKey: 'attack',
      createdAt: '2026-05-22T12:00:00Z',
    });
    const newer = makePrompt({
      id: 'damage-prompt',
      targetCharacterId: characterId,
      chainStepKey: 'damage',
      createdAt: '2026-05-22T12:01:00Z',
    });

    expect(findActivePlayerRollPrompt([older, newer], characterId)).toEqual(newer);
  });
});

describe('getPlayerRollChainView', () => {
  it('exposes the active pending prompt for the current chain step', () => {
    const actionId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
    const action = makeAction({
      id: actionId,
      actionKey: 'fireBolt',
      status: 'AwaitingFollowUpRoll',
      rollChainStateJson: JSON.stringify({ stepIndex: 1 }),
    });
    const prompts = [
      makePrompt({
        id: 'attack-prompt',
        actionRequestId: actionId,
        chainStepKey: 'attack',
        status: 'Completed',
        autoResolveOutcome: 'success',
      }),
      makePrompt({
        id: 'damage-prompt',
        actionRequestId: actionId,
        chainStepKey: 'damage',
        status: 'Pending',
      }),
    ];

    const view = getPlayerRollChainView(chainRuleset, action, prompts);
    expect(view?.currentStepKey).toBe('damage');
    expect(view?.activePrompt?.id).toBe('damage-prompt');
    expect(view?.isComplete).toBe(false);
    expect(actionHasRollChain(chainRuleset, 'fireBolt')).toBe(true);
  });
});

describe('actionNeedsPlayerRoll', () => {
  it('skips stat check responses that already include a roll', () => {
    const action = makeAction({
      actionText: 'Skill check: Athletics',
      actorCharacterId: 'char-1',
      isSkillCheckResponse: true,
      description: '🎲 Roll: 1d20: [14] + 3 = 17',
    });

    expect(actionNeedsPlayerRoll(action, statCheckRuleset)).toBe(false);
    expect(canPublishActionResolution(statCheckRuleset, action, [])).toBe(true);
  });
});

const chainRuleset: RulesetDefinition = {
  schemaVersion: 1,
  code: 'test',
  displayName: 'Test',
  description: 'Test',
  diceNotation: '1d20',
  dice: [{ key: 'd20', label: 'D20', notation: '1d20' }],
  character: {
    vitals: {},
    attributes: [],
    gameValues: [],
    classes: [],
    skills: [],
  },
  actions: [{
    key: 'fireBolt',
    label: 'Fire Bolt',
    allowedClasses: [],
    roll: {
      dice: 'd20',
      attribute: 'intelligence',
      skill: 'arcana',
      modifiers: [],
      successRule: 'vs AC',
    },
    rollChain: [
      {
        step: 'attack',
        label: 'Attack roll',
        checkMode: 'Action',
        resultKind: 'Total',
        onSuccess: 'damage',
        onFailure: 'end',
      },
      {
        step: 'damage',
        label: 'Damage roll',
        checkMode: 'Custom',
        resultKind: 'Total',
      },
    ],
  }],
};

describe('getRollChainProgress', () => {
  it('requires chain start before publish', () => {
    const action = makeAction({ actionKey: 'fireBolt' });
    const progress = getRollChainProgress(chainRuleset, action, []);

    expect(progress?.needsChainStart).toBe(true);
    expect(progress?.isComplete).toBe(false);
    expect(canPublishActionResolution(chainRuleset, action, [])).toBe(false);
  });

  it('treats a missed attack as a complete chain', () => {
    const actionId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
    const action = makeAction({
      id: actionId,
      actionKey: 'fireBolt',
      rollChainStateJson: JSON.stringify({ stepIndex: 0 }),
      followUpRolls: [
        makePrompt({
          id: 'attack-prompt',
          actionRequestId: actionId,
          chainStepKey: 'attack',
          status: 'Completed',
          autoResolveOutcome: 'failure',
          rollSummary: '1d20: [5] + 4 = 9',
        }),
      ],
    });

    const progress = getRollChainProgress(chainRuleset, action, []);
    expect(progress?.terminatedEarly).toBe(true);
    expect(progress?.isComplete).toBe(true);
    expect(canPublishActionResolution(chainRuleset, action, [])).toBe(true);
  });

  it('blocks publish while a follow-up damage prompt is pending', () => {
    const actionId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
    const action = makeAction({
      id: actionId,
      actionKey: 'fireBolt',
      status: 'AwaitingFollowUpRoll',
      rollChainStateJson: JSON.stringify({ stepIndex: 1 }),
    });
    const prompts = [
      makePrompt({
        id: 'damage-prompt',
        actionRequestId: actionId,
        chainStepKey: 'damage',
        status: 'Pending',
      }),
    ];

    const progress = getRollChainProgress(chainRuleset, action, prompts);
    expect(progress?.hasPendingPrompt).toBe(true);
    expect(progress?.pendingStepKey).toBe('damage');
    expect(canPublishActionResolution(chainRuleset, action, prompts)).toBe(false);
  });
});
