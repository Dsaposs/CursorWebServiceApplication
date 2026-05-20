import type { RollResultData, RollResultDieGroup } from '~/types/rollResult';
import { getDiceRoller } from '~/dice-rollers/registry';

/** Build structured roll JSON from a summary line and roller metadata. */
export function buildRollResultJson(
  rollSummary: string,
  rollerKey: string,
  resultKind: string,
  options?: { pushed?: boolean; stressGained?: number },
): string {
  const data = buildRollResultData(rollSummary, rollerKey, resultKind, options);
  return JSON.stringify(data);
}

export function buildRollResultData(
  rollSummary: string,
  rollerKey: string,
  resultKind: string,
  options?: { pushed?: boolean; stressGained?: number },
): RollResultData {
  const parsed = getDiceRoller(rollerKey).parsePlayerRoll(rollSummary);
  const groups = parseDieGroups(rollSummary, rollerKey);
  const isTotal = resultKind === 'Total';

  const data: RollResultData = {
    rollerKey,
    resultKind,
    groups,
    pushed: options?.pushed,
    stressGained: options?.stressGained,
  };

  if (isTotal) {
    data.total = parsed.primary;
    const totalMatch = rollSummary.match(/=\s*(\d+)\s*$/);
    if (totalMatch) data.total = parseInt(totalMatch[1], 10);
    const arrowMatch = rollSummary.match(/→\s*total\s+(\d+)/i);
    if (arrowMatch) data.total = parseInt(arrowMatch[1], 10);
  } else {
    data.successes = parsed.primary;
  }

  const naturalMatch = rollSummary.match(/\[(\d+)\]/);
  if (naturalMatch) {
    data.naturalDie = parseInt(naturalMatch[1], 10);
  }

  return data;
}

export function parseRollResultJson(json?: string | null): RollResultData | null {
  if (!json?.trim()) return null;
  try {
    return JSON.parse(json) as RollResultData;
  } catch {
    return null;
  }
}

function parseDieGroups(rollSummary: string, rollerKey: string): RollResultDieGroup[] {
  const groups: RollResultDieGroup[] = [];

  if (rollerKey === 'd6-pool') {
    const baseMatch = rollSummary.match(/base:\s*([^\]|]+)/i);
    const stressMatch = rollSummary.match(/stress:\s*([^\]|]+)/i);
    if (baseMatch) {
      groups.push({
        notation: 'd6',
        label: 'Base dice',
        values: parseCommaList(baseMatch[1]),
        isStress: false,
      });
    }
    if (stressMatch) {
      groups.push({
        notation: 'd6',
        label: 'Stress dice',
        values: parseCommaList(stressMatch[1]),
        isStress: true,
      });
    }
    if (!groups.length) {
      const bracketMatch = rollSummary.match(/\[([^\]]+)\]/);
      if (bracketMatch) {
        groups.push({
          notation: 'd6',
          values: parseCommaList(bracketMatch[1]),
        });
      }
    }
    return groups;
  }

  if (rollerKey === 'd-class-check') {
    const mainMatch = rollSummary.match(/1d\d+:\s*\[(\d+)\]/i);
    if (mainMatch) {
      const sides = rollSummary.match(/1d(\d+)/i)?.[1] ?? '?';
      groups.push({
        notation: `d${sides}`,
        label: 'Class die',
        values: [parseInt(mainMatch[1], 10)],
      });
    }
    const bonusMatch = rollSummary.match(/\[([^\]]+)\]\s*\(\+\d+\)/);
    if (bonusMatch) {
      groups.push({
        notation: 'd4',
        label: 'Bonus dice',
        values: parseCommaList(bonusMatch[1]),
      });
    }
    return groups;
  }

  const d20Match = rollSummary.match(/\[(\d+)\]/);
  if (d20Match) {
    groups.push({
      notation: 'd20',
      values: [parseInt(d20Match[1], 10)],
    });
  }

  return groups;
}

function parseCommaList(raw: string): number[] {
  return raw
    .split(/[,\s]+/)
    .map(v => parseInt(v.trim(), 10))
    .filter(n => !Number.isNaN(n));
}

export function formatAutoResolveLabel(outcome?: string | null): string {
  switch (outcome) {
    case 'success':
      return 'Hit (auto)';
    case 'failure':
      return 'Miss (auto)';
    case 'needs_dm':
      return 'Needs DM';
    default:
      return '';
  }
}
