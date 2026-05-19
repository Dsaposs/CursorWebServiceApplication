import type { Component } from 'vue';
import type { RulesetDefinition } from '~/types/api';

export type DiceRollMode = 'action' | 'skill' | 'attribute';

/** How the DM wants the roll result interpreted. */
export type RollResultKind = 'PassFail' | 'Total';

export interface BuildRollContextParams {
  definition: RulesetDefinition;
  mode: DiceRollMode;
  actionKey: string;
  skillKey: string;
  attributeKey: string;
  attributes: Record<string, number>;
  skills: Record<string, number>;
  gameValues: Record<string, number>;
}

/** Discriminated config passed to the roller UI component. */
export interface D6PoolRollConfig {
  kind: 'd6-pool';
  baseDiceCount: number;
  stressDiceCount: number;
  sides: number;
  successTarget: number;
}

export interface D20CheckRollConfig {
  kind: 'd20-check';
  sides: number;
  successRule?: string;
  attackBonus?: number;
}

/** Single class die + flat stat modifier + optional bonus d4s (e.g. grief dice). Used by DIE RPG. */
export interface DClassCheckRollConfig {
  kind: 'd-class-check';
  /** Sides of the class die (4, 6, 8, 10, 12, or 20). */
  sides: number;
  /** Combined attribute + skill value added flat to the roll. */
  modifier: number;
  /** Extra d4s rolled and summed on top (e.g. Grief Knight's grief dice). */
  bonusDiceCount: number;
  /** Display label for the bonus dice (e.g. "Grief"). */
  bonusDiceLabel: string;
  /** Target number to meet or beat (0 = no fixed DC, DM decides). */
  difficultyClass: number;
}

export type DiceRollConfig = D6PoolRollConfig | D20CheckRollConfig | DClassCheckRollConfig;

export interface DiceRollContext {
  rollerKey: string;
  label: string;
  poolBreakdown: string[];
  successRule?: string;
  resultKind?: RollResultKind;
  config: DiceRollConfig;
}

export interface ParsedPlayerRoll {
  hasRoll: boolean;
  /** Primary numeric result shown to the DM (successes for pools, total for d20). */
  primary: number;
  /** Secondary detail (e.g. panic checks on stress dice). */
  secondary?: number;
  secondaryLabel?: string;
}

export interface DiceRollerDefinition {
  key: string;
  label: string;
  description: string;
  component: Component;
  buildRollContext(params: BuildRollContextParams): DiceRollContext | null;
  parsePlayerRoll(rollLine: string): ParsedPlayerRoll;
  formatAdjustedSummary(roll: ParsedPlayerRoll, modifier: number): string;
}
