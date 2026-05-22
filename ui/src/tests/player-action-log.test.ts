import { describe, expect, it } from 'vitest';
import type { ActionQueueItemResponse } from '~/types/api';
import { isPlayerActionLogEntry, isUnresolvedActionStatus } from '~/utils/actionLog';

function action(overrides: Partial<ActionQueueItemResponse>): ActionQueueItemResponse {
  return {
    id: 'action-1',
    sequence: 1,
    actorName: 'Ripley',
    actionText: 'Attack',
    status: 'Published',
    statChangesJson: '[]',
    pendingChainEffectsJson: '[]',
    followUpRolls: [],
    submittedAt: '2026-05-22T12:00:00.000Z',
    ...overrides,
  };
}

describe('isPlayerActionLogEntry', () => {
  it('includes published non-stat-check actions', () => {
    expect(isPlayerActionLogEntry(action({ status: 'Published' }))).toBe(true);
  });

  it('excludes rejected actions', () => {
    expect(isPlayerActionLogEntry(action({ status: 'Rejected' }))).toBe(false);
  });

  it('excludes published exploration stat checks', () => {
    expect(isPlayerActionLogEntry(action({
      status: 'Published',
      isSkillCheckResponse: true,
      actionText: 'Perception check',
    }))).toBe(false);
  });

  it('includes published combat-turn stat checks', () => {
    expect(isPlayerActionLogEntry(action({
      status: 'Published',
      isSkillCheckResponse: true,
      actionText: 'Perception check',
      combatEncounterId: 'encounter-1',
    }))).toBe(true);
  });

  it('excludes player-submitted exploration stat check actions', () => {
    expect(isPlayerActionLogEntry(action({
      status: 'Published',
      actionText: 'Skill check: Ranged Combat',
    }))).toBe(false);
  });
});

describe('isUnresolvedActionStatus', () => {
  it('treats in-progress DM workflow statuses as unresolved', () => {
    expect(isUnresolvedActionStatus('Pending')).toBe(true);
    expect(isUnresolvedActionStatus('DmReviewing')).toBe(true);
    expect(isUnresolvedActionStatus('AwaitingFollowUpRoll')).toBe(true);
  });

  it('treats published actions as resolved', () => {
    expect(isUnresolvedActionStatus('Published')).toBe(false);
    expect(isUnresolvedActionStatus('Rejected')).toBe(false);
  });
});
