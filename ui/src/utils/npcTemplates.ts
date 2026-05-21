import type { RulesetAttributeDefinition, RulesetDefinition, RulesetSkillDefinition } from '~/types/api';
import { parseNpcInventory, type InventoryEntry } from '~/utils/inventory';

export interface RulesetNpcTemplate {
  key: string;
  label: string;
  scenario?: string;
  description?: string;
  kind?: string;
  maxHealth?: number;
  health?: number;
  armor?: number;
  defaultStats?: Record<string, unknown>;
}

export interface NpcTemplateFormState {
  name: string;
  kind: string;
  maxHealth: number;
  health: number;
  armor: number;
  attrs: Record<string, number>;
  skills: Record<string, number>;
  inventory: InventoryEntry[];
}

export interface NpcTemplateGroup {
  label: string;
  templates: RulesetNpcTemplate[];
}

const SCENARIO_LABELS: Record<string, string> = {
  'hopes-last-day': "Hope's Last Day",
};

export function parseNpcTemplates(definition: RulesetDefinition | null): RulesetNpcTemplate[] {
  if (!definition?.npcTemplates?.length) return [];

  return definition.npcTemplates
    .map(entry => normalizeNpcTemplate(entry))
    .filter((template): template is RulesetNpcTemplate => Boolean(template));
}

function normalizeNpcTemplate(entry: Record<string, unknown>): RulesetNpcTemplate | null {
  const key = typeof entry.key === 'string' ? entry.key : '';
  const label = typeof entry.label === 'string' ? entry.label : '';
  if (!key || !label) return null;

  return {
    key,
    label,
    scenario: typeof entry.scenario === 'string' ? entry.scenario : undefined,
    description: typeof entry.description === 'string' ? entry.description : undefined,
    kind: typeof entry.kind === 'string' ? entry.kind : undefined,
    maxHealth: typeof entry.maxHealth === 'number' ? entry.maxHealth : undefined,
    health: typeof entry.health === 'number' ? entry.health : undefined,
    armor: typeof entry.armor === 'number' ? entry.armor : undefined,
    defaultStats: entry.defaultStats && typeof entry.defaultStats === 'object'
      ? entry.defaultStats as Record<string, unknown>
      : undefined,
  };
}

export function groupNpcTemplates(templates: RulesetNpcTemplate[]): NpcTemplateGroup[] {
  const general = templates.filter(template => !template.scenario);
  const scenarios = new Map<string, RulesetNpcTemplate[]>();

  for (const template of templates) {
    if (!template.scenario) continue;
    const bucket = scenarios.get(template.scenario) ?? [];
    bucket.push(template);
    scenarios.set(template.scenario, bucket);
  }

  const groups: NpcTemplateGroup[] = [];
  if (general.length) {
    groups.push({ label: 'General', templates: general });
  }

  for (const [scenario, scenarioTemplates] of scenarios) {
    groups.push({
      label: SCENARIO_LABELS[scenario] ?? scenario,
      templates: scenarioTemplates,
    });
  }

  return groups;
}

function defaultAttrs(attributes: RulesetAttributeDefinition[]): Record<string, number> {
  return Object.fromEntries(attributes.map(attr => [attr.key, attr.default ?? 0]));
}

function defaultSkills(skills: RulesetSkillDefinition[]): Record<string, number> {
  return Object.fromEntries(skills.map(skill => [skill.key, skill.default ?? 0]));
}

function readRulesetDefaultMaxHealth(definition: RulesetDefinition | null): number {
  const health = definition?.character?.vitals?.health as { defaultMax?: number } | undefined;
  return typeof health?.defaultMax === 'number' ? health.defaultMax : 10;
}

function readRulesetDefaultArmor(definition: RulesetDefinition | null): number {
  const armor = definition?.character?.vitals?.armor as { default?: number } | undefined;
  return typeof armor?.default === 'number' ? armor.default : 0;
}

export function applyNpcTemplateToForm(
  template: RulesetNpcTemplate,
  definition: RulesetDefinition | null,
  attributes: RulesetAttributeDefinition[],
  skills: RulesetSkillDefinition[],
): NpcTemplateFormState {
  const stats = template.defaultStats ?? {};
  const statAttrs = stats.attributes as Record<string, number> | undefined;
  const statSkills = stats.skills as Record<string, number> | undefined;
  const defaultMax = readRulesetDefaultMaxHealth(definition);
  const defaultArmor = readRulesetDefaultArmor(definition);
  const maxHealth = template.maxHealth ?? defaultMax;

  return {
    name: template.label,
    kind: template.kind ?? 'NPC',
    maxHealth,
    health: template.health ?? maxHealth,
    armor: template.armor ?? defaultArmor,
    attrs: { ...defaultAttrs(attributes), ...(statAttrs ?? {}) },
    skills: { ...defaultSkills(skills), ...(statSkills ?? {}) },
    inventory: parseNpcInventory(JSON.stringify(stats)),
  };
}

export function buildStatBlockJsonFromForm(
  attrs: Record<string, number>,
  skills: Record<string, number>,
  inventory: InventoryEntry[],
  template?: RulesetNpcTemplate | null,
): string {
  const block: Record<string, unknown> = {};

  if (template?.defaultStats?.classKey) {
    block.classKey = template.defaultStats.classKey;
  }

  if (Object.keys(attrs).length) block.attributes = { ...attrs };
  if (Object.keys(skills).length) block.skills = { ...skills };

  const gameValues = template?.defaultStats?.gameValues;
  if (gameValues && typeof gameValues === 'object') {
    block.gameValues = { ...(gameValues as Record<string, unknown>) };
  }

  if (inventory.length) {
    block.inventory = inventory.map(entry => ({
      itemKey: entry.itemKey,
      quantity: entry.quantity,
    }));
  }

  return JSON.stringify(block);
}

export function findNpcTemplate(
  definition: RulesetDefinition | null,
  templateKey: string,
): RulesetNpcTemplate | null {
  return parseNpcTemplates(definition).find(template => template.key === templateKey) ?? null;
}
