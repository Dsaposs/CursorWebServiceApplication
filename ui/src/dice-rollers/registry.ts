/**
 * Dice roller registry.
 *
 * To add a new roller:
 *  1. Create `dice-rollers/<key>/index.ts` exporting a `DiceRollerDefinition`.
 *  2. Create `dice-rollers/<key>/<YourRoller>.vue` implementing the roller UI.
 *  3. Import and add it to the `diceRollers` array below.
 *  4. Add the key to `api/src/NotesApi/Rulesets/DiceRollers/dice-rollers.json`.
 *  5. Add the key to `KnownDiceRollerKeys` in `RulesetDefinitionValidator.cs`.
 */

import type { DiceRollerDefinition } from '~/dice-rollers/types';
import type { RulesetDefinition } from '~/types/api';
import { d6PoolRoller } from '~/dice-rollers/d6-pool';
import { d20CheckRoller } from '~/dice-rollers/d20-check';
import { dClassCheckRoller } from '~/dice-rollers/d-class-check';

const diceRollers: DiceRollerDefinition[] = [d6PoolRoller, d20CheckRoller, dClassCheckRoller];

const byKey = new Map(diceRollers.map(roller => [roller.key, roller]));

export const diceRollerCatalog = diceRollers;

export function getDiceRoller(key: string): DiceRollerDefinition {
  const roller = byKey.get(key);
  if (!roller) {
    throw new Error(`Unknown dice roller "${key}". Registered rollers: ${diceRollers.map(r => r.key).join(', ')}`);
  }
  return roller;
}

/** Resolves which shared roller a ruleset uses (explicit key, or legacy schema fallback). */
export function resolveDiceRollerKey(definition: Pick<RulesetDefinition, 'diceRollerKey' | 'schemaVersion'>): string {
  if (definition.diceRollerKey) return definition.diceRollerKey;
  return definition.schemaVersion >= 2 ? 'd6-pool' : 'd20-check';
}
