import { describe, expect, it } from 'vitest';
import type { ActionQueueItemResponse } from '~/types/api';
import { groupActionsByEncounter } from '~/utils/actionLog';

function makeAction(overrides: Partial<ActionQueueItemResponse>): ActionQueueItemResponse {
  return {
    id: overrides.id ?? 'action-1',
    sequence: overrides.sequence ?? 1,
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

describe('groupActionsByEncounter', () => {
  it('groups combat stat checks with other actions in the same encounter', () => {
    const encounterId = 'encounter-1';
    const groups = groupActionsByEncounter([
      makeAction({
        id: 'combat-action',
        sequence: 1,
        actionText: 'Fire Bolt',
        combatEncounterId: encounterId,
      }),
      makeAction({
        id: 'stat-check',
        sequence: 2,
        actionText: 'Perception check',
        combatEncounterId: encounterId,
        skillCheckBatchId: 'batch-1',
        isSkillCheckResponse: true,
      }),
    ], [{
      id: encounterId,
      sequence: 1,
      round: 1,
      startedAt: '2026-05-22T12:00:00.000Z',
      isActive: true,
    }]);

    expect(groups).toHaveLength(1);
    expect(groups[0]?.kind).toBe('combat');
    expect(groups[0]?.actions.map(a => a.id)).toEqual(['combat-action', 'stat-check']);
  });

  it('keeps exploration skill checks in their own group', () => {
    const groups = groupActionsByEncounter([
      makeAction({
        id: 'skill-check',
        sequence: 1,
        actionText: 'Perception check',
        skillCheckBatchId: 'batch-1',
        isSkillCheckResponse: true,
      }),
    ]);

    expect(groups).toHaveLength(1);
    expect(groups[0]?.kind).toBe('skillCheck');
  });
});
