import type { Component } from 'vue';
import type { RulesetDefinition } from '~/types/api';

export type DiceRollMode = 'action' | 'skill' | 'attribute';

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
}

export type DiceRollConfig = D6PoolRollConfig | D20CheckRollConfig;

export interface DiceRollContext {
  rollerKey: string;
  label: string;
  poolBreakdown: string[];
  successRule?: string;
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
