export interface ParsedDice {
  count: number;
  sides: number;
  notation: string;
}

export interface RollResult {
  rolls: number[];
  total: number;
  notation: string;
  summary: string;
}

export function parseDiceNotation(notation: string): ParsedDice | null {
  const match = notation.trim().match(/^(\d+)d(\d+)$/i);
  if (!match) return null;
  const count = parseInt(match[1], 10);
  const sides = parseInt(match[2], 10);
  if (count < 1 || sides < 1 || count > 200 || sides > 1000) return null;
  return { count, sides, notation };
}

export function rollDice(count: number, sides: number): number[] {
  return Array.from({ length: Math.max(1, count) }, () =>
    Math.floor(Math.random() * sides) + 1,
  );
}

/** D&D-style ability modifier from a score (10–11 → 0). */
export function attributeModifier(score: number): number {
  return Math.floor((score - 10) / 2);
}

export function buildRollResult(rolls: number[], notation: string): RollResult {
  const total = rolls.reduce((s, r) => s + r, 0);
  return {
    rolls,
    total,
    notation,
    summary: `${notation}: [${rolls.join(', ')}] = ${total}`,
  };
}

type CharacterStatSection = 'attributes' | 'skills' | 'gameValues';

/**
 * Parse a nested section from ruleset character data JSON.
 */
export function parseNestedStatSection(
  rulesetDataJson: string | null | undefined,
  section: CharacterStatSection,
): Record<string, number> {
  if (!rulesetDataJson) return {};
  try {
    const raw = JSON.parse(rulesetDataJson) as Record<string, unknown>;
    const source = raw[section];
    if (!source || typeof source !== 'object' || Array.isArray(source)) return {};
    return Object.fromEntries(
      Object.entries(source).filter(([, value]) => typeof value === 'number'),
    ) as Record<string, number>;
  } catch {
    return {};
  }
}

export function parseCharacterStats(rulesetDataJson?: string | null) {
  return {
    attributes: parseNestedStatSection(rulesetDataJson, 'attributes'),
    skills: parseNestedStatSection(rulesetDataJson, 'skills'),
    gameValues: parseNestedStatSection(rulesetDataJson, 'gameValues'),
  };
}

/**
 * Parse a JSON stat block into a flat `Record<string, number>`.
 * Handles flat `{ key: number }` format as well as nested formats:
 *   `{ attributes: {...}, skills: {...}, gameValues: {...} }`
 * All numeric values from the root and recognised sub-objects are merged.
 */
export function parseStatMap(json?: string | null): Record<string, number> {
  if (!json) return {};
  try {
    const raw = JSON.parse(json);
    if (typeof raw !== 'object' || raw === null) return {};

    const result: Record<string, number> = {};
    // Walk the root plus any well-known nested stat objects
    for (const source of [raw, raw.attributes, raw.skills, raw.gameValues]) {
      if (!source || typeof source !== 'object') continue;
      for (const [k, v] of Object.entries(source as Record<string, unknown>)) {
        if (typeof v === 'number') result[k] = v;
      }
    }
    return result;
  } catch {
    return {};
  }
}

/** Calculate extra dice contributed by action modifiers given resolved stat values. */
export function calcModifierDice(
  modifiers: Array<{ source: string; key: string; dicePerPoint?: number; isStressDice?: boolean }>,
  attributes: Record<string, number>,
  skills: Record<string, number>,
  gameValues: Record<string, number> = {},
): { extra: number; stressExtra: number; breakdown: string[] } {
  let extra = 0;
  let stressExtra = 0;
  const breakdown: string[] = [];

  for (const mod of modifiers) {
    if (mod.flatDice) {
      extra += mod.flatDice;
      breakdown.push(`+${mod.flatDice}d from ${mod.key}`);
      continue;
    }

    if (!mod.dicePerPoint) continue;

    let value = 0;
    if (mod.source === 'attribute') value = attributes[mod.key] ?? 0;
    else if (mod.source === 'skill') value = skills[mod.key] ?? 0;
    else if (mod.source === 'gameValue') value = gameValues[mod.key] ?? 0;

    const added = value * mod.dicePerPoint;
    if (added === 0) continue;

    if (mod.isStressDice) {
      stressExtra += added;
      breakdown.push(`+${added} stress (${mod.key} ${value})`);
    } else {
      extra += added;
      breakdown.push(`${added > 0 ? '+' : ''}${added}d from ${mod.key} (${value} × ${mod.dicePerPoint})`);
    }
  }

  return { extra, stressExtra, breakdown };
}

/** Count dice that meet or exceed a success threshold (e.g. each 6 = 1 success). */
export function countSuccesses(rolls: number[], target: number): number {
  return rolls.filter(r => r >= target).length;
}

/** Classify rolls as successes, stress-panics (1 on a stress die), or normal. */
export function classifyRolls(
  baseRolls: number[],
  stressRolls: number[],
  successTarget: number,
): {
  baseSuccesses: number;
  stressSuccesses: number;
  panicDice: number[];
  totalSuccesses: number;
} {
  const baseSuccesses = countSuccesses(baseRolls, successTarget);
  const stressSuccesses = countSuccesses(stressRolls, successTarget);
  const panicDice = stressRolls.filter(r => r === 1);
  return {
    baseSuccesses,
    stressSuccesses,
    panicDice,
    totalSuccesses: baseSuccesses + stressSuccesses,
  };
}
