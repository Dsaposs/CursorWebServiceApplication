export interface InventoryEntry {
  itemKey: string;
  quantity: number;
}

export function parseInventory(json?: string | null): InventoryEntry[] {
  if (!json) return [];
  try {
    const raw = JSON.parse(json);
    if (!Array.isArray(raw)) return [];
    return raw
      .filter((entry): entry is { itemKey: string; quantity: number } =>
        Boolean(entry)
        && typeof entry === 'object'
        && typeof entry.itemKey === 'string'
        && typeof entry.quantity === 'number'
        && entry.quantity > 0,
      )
      .map(entry => ({ itemKey: entry.itemKey, quantity: entry.quantity }));
  } catch {
    return [];
  }
}

export function parseNpcInventory(statBlockJson?: string | null): InventoryEntry[] {
  if (!statBlockJson) return [];
  try {
    const raw = JSON.parse(statBlockJson) as { inventory?: InventoryEntry[] };
    return parseInventory(JSON.stringify(raw.inventory ?? []));
  } catch {
    return [];
  }
}

export function hasInventoryItem(inventory: InventoryEntry[], itemKey?: string | null): boolean {
  if (!itemKey) return false;
  return inventory.some(entry =>
    entry.itemKey === itemKey && entry.quantity > 0,
  );
}

export function inventoryQuantity(inventory: InventoryEntry[], itemKey: string): number {
  return inventory.find(entry => entry.itemKey === itemKey)?.quantity ?? 0;
}
