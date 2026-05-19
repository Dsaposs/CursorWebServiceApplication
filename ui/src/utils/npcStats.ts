export interface ParsedNpcStatBlock {
  attributes?: Record<string, number>;
  skills?: Record<string, number>;
}

export function parseNpcStatBlock(statBlockJson?: string | null): ParsedNpcStatBlock | null {
  if (!statBlockJson) return null;
  try {
    const block = JSON.parse(statBlockJson) as ParsedNpcStatBlock;
    if (!block || typeof block !== 'object') return null;
    return block;
  } catch {
    return null;
  }
}

export function npcHasStructuredStats(statBlockJson?: string | null): boolean {
  const block = parseNpcStatBlock(statBlockJson);
  return Boolean(block?.attributes || block?.skills);
}

export function npcAttributeValue(
  statBlockJson: string | null | undefined,
  key: string,
): number | null {
  return parseNpcStatBlock(statBlockJson)?.attributes?.[key] ?? null;
}

export function npcSkillValue(
  statBlockJson: string | null | undefined,
  key: string,
): number | null {
  return parseNpcStatBlock(statBlockJson)?.skills?.[key] ?? null;
}
