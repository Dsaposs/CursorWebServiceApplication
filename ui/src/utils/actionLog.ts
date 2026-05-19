import type {
  ActionQueueItemResponse,
  CombatEncounterResponse,
  GameResponse,
  RulesetDefinition,
} from '~/types/api';
import { rollPromptCheckLabel } from '~/utils/rollPrompt';
import type { RollPromptResponse } from '~/types/api';
import { findRulesetAttribute } from '~/utils/rulesets';

export interface ParsedActionDescription {
  playerRoll: string | null;
  body: string | null;
}

export interface StatChangeRecord {
  targetType: string;
  targetId: string;
  healthDelta?: number;
  setHealth?: number;
  setArmor?: number;
  gameValueDeltas?: Record<string, number>;
  setGameValues?: Record<string, number>;
  attributeDeltas?: Record<string, number>;
}

export function splitActionDescription(description?: string | null): ParsedActionDescription {
  if (!description?.trim()) {
    return { playerRoll: null, body: null };
  }

  const rollParts: string[] = [];
  const bodyLines: string[] = [];

  for (const line of description.split('\n')) {
    const rollIndex = line.indexOf('🎲 Roll:');
    if (rollIndex >= 0) {
      const rollText = line.slice(rollIndex + '🎲 Roll:'.length).trim();
      if (rollText) rollParts.push(rollText);
    } else if (line.trim()) {
      bodyLines.push(line.trim());
    }
  }

  return {
    playerRoll: rollParts.length ? rollParts.join('; ') : null,
    body: bodyLines.length ? bodyLines.join('\n') : null,
  };
}

export function parseStatChangesJson(json: string): StatChangeRecord[] {
  if (!json || json === '[]') return [];

  try {
    const parsed = JSON.parse(json) as StatChangeRecord[];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function resolveTargetName(game: GameResponse, change: StatChangeRecord): string {
  const id = change.targetId;
  if (change.targetType === 'Character') {
    return game.characters.find(c => c.id === id)?.name ?? 'Unknown character';
  }

  if (change.targetType === 'NpcOrMonster') {
    return game.npcsAndMonsters.find(n => n.id === id)?.name ?? 'Unknown NPC';
  }

  return 'Unknown target';
}

function gameValueLabel(definition: RulesetDefinition | null, key: string): string {
  return definition?.character.gameValues.find(gv => gv.key === key)?.label ?? key;
}

function attributeLabel(definition: RulesetDefinition | null, key: string): string {
  return findRulesetAttribute(definition, key)?.label ?? key;
}

function formatSignedDelta(value: number): string {
  return value > 0 ? `+${value}` : String(value);
}

function formatStatChange(
  change: StatChangeRecord,
  game: GameResponse,
  definition: RulesetDefinition | null,
): string[] {
  const target = resolveTargetName(game, change);
  const lines: string[] = [];

  if (change.healthDelta !== undefined && change.healthDelta !== 0) {
    lines.push(`${target}: HP ${formatSignedDelta(change.healthDelta)}`);
  }

  if (change.setHealth !== undefined) {
    lines.push(`${target}: HP set to ${change.setHealth}`);
  }

  if (change.setArmor !== undefined) {
    lines.push(`${target}: AC set to ${change.setArmor}`);
  }

  for (const [key, value] of Object.entries(change.setGameValues ?? {})) {
    lines.push(`${target}: ${gameValueLabel(definition, key)} set to ${value}`);
  }

  for (const [key, delta] of Object.entries(change.gameValueDeltas ?? {})) {
    if (delta !== 0) {
      lines.push(`${target}: ${gameValueLabel(definition, key)} ${formatSignedDelta(delta)}`);
    }
  }

  for (const [key, delta] of Object.entries(change.attributeDeltas ?? {})) {
    if (delta !== 0) {
      lines.push(`${target}: ${attributeLabel(definition, key)} ${formatSignedDelta(delta)}`);
    }
  }

  return lines;
}

export function formatActionStatChanges(
  action: ActionQueueItemResponse,
  game: GameResponse | null | undefined,
  definition: RulesetDefinition | null,
): string[] {
  if (!game) return [];

  return parseStatChangesJson(action.statChangesJson).flatMap(change =>
    formatStatChange(change, game, definition),
  );
}

export function formatFollowUpRollLabel(
  roll: RollPromptResponse,
  definition: RulesetDefinition | null,
): string {
  return rollPromptCheckLabel(roll, definition);
}

export function formatActionTimestamp(iso?: string | null): string | null {
  if (!iso) return null;

  try {
    return new Date(iso).toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  } catch {
    return null;
  }
}

export type ActionLogGroupKind = 'combat' | 'exploration' | 'skillCheck';

export interface ActionLogGroup {
  key: string;
  kind: ActionLogGroupKind;
  label: string;
  subtitle?: string | null;
  startedAt?: string | null;
  endedAt?: string | null;
  isActive?: boolean;
  actions: ActionQueueItemResponse[];
}

function encounterMeta(
  encounterId: string,
  encounters: CombatEncounterResponse[],
): CombatEncounterResponse | undefined {
  return encounters.find(e => e.id === encounterId);
}

function formatEncounterRange(startedAt?: string | null, endedAt?: string | null, isActive?: boolean): string | null {
  const start = formatActionTimestamp(startedAt);
  if (!start) return null;

  if (isActive) return `${start} – in progress`;
  const end = formatActionTimestamp(endedAt);
  return end ? `${start} – ${end}` : start;
}

function buildExplorationGroup(actions: ActionQueueItemResponse[], index: number): ActionLogGroup {
  return {
    key: `exploration-${index}`,
    kind: 'exploration',
    label: 'Exploration',
    subtitle: 'Actions outside of combat',
    actions,
  };
}

function skillCheckGroupKey(batchId: string) {
  return `skill-check-${batchId}`;
}

function buildSkillCheckGroup(
  batchId: string,
  actions: ActionQueueItemResponse[],
): ActionLogGroup {
  const label = actions[0]?.skillCheckGroupLabel?.trim() || 'Skill check';
  const playerCount = actions.length;

  return {
    key: skillCheckGroupKey(batchId),
    kind: 'skillCheck',
    label,
    subtitle: `${playerCount} player response${playerCount === 1 ? '' : 's'}`,
    actions,
  };
}

function buildCombatGroup(
  encounterId: string,
  actions: ActionQueueItemResponse[],
  encounters: CombatEncounterResponse[],
): ActionLogGroup {
  const meta = encounterMeta(encounterId, encounters);
  const sequence = meta?.sequence ?? actions[0]?.combatEncounterSequence ?? 0;

  return {
    key: encounterId,
    kind: 'combat',
    label: sequence > 0 ? `Combat encounter ${sequence}` : 'Combat encounter',
    subtitle: formatEncounterRange(meta?.startedAt, meta?.endedAt, meta?.isActive),
    startedAt: meta?.startedAt,
    endedAt: meta?.endedAt,
    isActive: meta?.isActive,
    actions,
  };
}

/** Groups actions by combat encounter in chronological order. */
export function groupActionsByEncounter(
  actions: ActionQueueItemResponse[],
  combatEncounters: CombatEncounterResponse[] = [],
): ActionLogGroup[] {
  const sorted = [...actions].sort((a, b) => a.sequence - b.sequence);
  const groups: ActionLogGroup[] = [];
  let explorationIndex = 0;

  for (const action of sorted) {
    const batchId = action.skillCheckBatchId ?? null;
    if (batchId) {
      const batchKey = skillCheckGroupKey(batchId);
      const last = groups[groups.length - 1];
      if (last?.kind === 'skillCheck' && last.key === batchKey) {
        last.actions.push(action);
        if (last.actions.length > 1) {
          last.subtitle = `${last.actions.length} player responses`;
        }
      } else {
        groups.push(buildSkillCheckGroup(batchId, [action]));
      }
      continue;
    }

    const encounterId = action.combatEncounterId ?? null;

    if (!encounterId) {
      const last = groups[groups.length - 1];
      if (last?.kind === 'exploration') {
        last.actions.push(action);
      } else {
        explorationIndex += 1;
        groups.push(buildExplorationGroup([action], explorationIndex));
      }
      continue;
    }

    const last = groups[groups.length - 1];
    if (last?.kind === 'combat' && last.key === encounterId) {
      last.actions.push(action);
    } else {
      groups.push(buildCombatGroup(encounterId, [action], combatEncounters));
    }
  }

  return groups;
}

/** Newest encounter / segment first; actions within each group are newest-first. */
export function groupActionsForDisplay(
  actions: ActionQueueItemResponse[],
  combatEncounters: CombatEncounterResponse[] = [],
): ActionLogGroup[] {
  return groupActionsByEncounter(actions, combatEncounters)
    .reverse()
    .map(group => ({
      ...group,
      actions: [...group.actions].reverse(),
    }));
}
