import type { BuildRollContextParams, DiceRollContext } from '~/dice-rollers/types';
import { getDiceRoller, resolveDiceRollerKey } from '~/dice-rollers/registry';

export function buildDiceRollContext(params: BuildRollContextParams): DiceRollContext | null {
  const rollerKey = resolveDiceRollerKey(params.definition);
  return getDiceRoller(rollerKey).buildRollContext(params);
}

export function parsePlayerRollFromDescription(rollerKey: string, description?: string | null) {
  const rollLine = description?.split('\n').find(line => line.includes('🎲 Roll:')) ?? '';
  return getDiceRoller(rollerKey).parsePlayerRoll(rollLine);
}
