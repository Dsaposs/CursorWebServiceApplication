import type { NpcResponse } from '~/types/api';

export type NpcVisibility = 'Visible' | 'Hidden';

export function normalizeNpcVisibility(visibility?: string | null): NpcVisibility {
  return visibility === 'Visible' ? 'Visible' : 'Hidden';
}

export function isNpcVisible(npc: Pick<NpcResponse, 'visibility'>): boolean {
  return normalizeNpcVisibility(npc.visibility) === 'Visible';
}

/** NPCs the player may see in session UI (visible only). */
export function visibleNpcs(npcs: NpcResponse[]): NpcResponse[] {
  return npcs.filter(isNpcVisible);
}

/** NPCs the player may select as action targets. */
export function selectableTargetNpcs(npcs: NpcResponse[]): NpcResponse[] {
  return visibleNpcs(npcs);
}
