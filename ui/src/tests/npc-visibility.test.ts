import { describe, expect, it } from 'vitest';
import type { NpcResponse } from '~/types/api';
import {
  isNpcVisible,
  normalizeNpcVisibility,
  selectableTargetNpcs,
  visibleNpcs,
} from '~/utils/npcVisibility';

function buildNpc(id: string, name: string, visibility: NpcResponse['visibility']): NpcResponse {
  return {
    id,
    name,
    kind: 'NPC',
    maxHealth: 10,
    health: 10,
    armor: 0,
    statBlockJson: '{}',
    visibility,
  };
}

describe('npc visibility utilities', () => {
  it('normalizes missing and unknown values to hidden', () => {
    expect(normalizeNpcVisibility('Visible')).toBe('Visible');
    expect(normalizeNpcVisibility('Hidden')).toBe('Hidden');
    expect(normalizeNpcVisibility('Obscured')).toBe('Hidden');
    expect(normalizeNpcVisibility(null)).toBe('Hidden');
    expect(normalizeNpcVisibility(undefined)).toBe('Hidden');
  });

  it('only exposes visible npcs for player lists and target selection', () => {
    const visible = buildNpc('visible-npc', 'Visible NPC', 'Visible');
    const hidden = buildNpc('hidden-npc', 'Hidden NPC', 'Hidden');
    const legacyHidden = {
      ...buildNpc('legacy-hidden-npc', 'Legacy Hidden NPC', 'Hidden'),
      visibility: 'Obscured' as NpcResponse['visibility'],
    };
    const npcs = [hidden, visible, legacyHidden];

    expect(isNpcVisible(visible)).toBe(true);
    expect(isNpcVisible(hidden)).toBe(false);
    expect(isNpcVisible(legacyHidden)).toBe(false);
    expect(visibleNpcs(npcs).map(npc => npc.id)).toEqual(['visible-npc']);
    expect(selectableTargetNpcs(npcs).map(npc => npc.id)).toEqual(['visible-npc']);
  });
});
