export interface SheetSection {
  key: string;
  label: string;
  value: unknown;
  isEmpty: boolean;
}

export function parseJsonValue(json: string) {
  try {
    return JSON.parse(json || '{}') as unknown;
  } catch {
    return json;
  }
}

export function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

export function isEmptyValue(value: unknown): boolean {
  if (Array.isArray(value)) return value.length === 0;
  if (isRecord(value)) return Object.keys(value).length === 0;
  return value === null || value === undefined || value === '';
}

export function buildSheetSection(key: string, label: string, json: string): SheetSection {
  const value = parseJsonValue(json);
  return {
    key,
    label,
    value,
    isEmpty: isEmptyValue(value),
  };
}
