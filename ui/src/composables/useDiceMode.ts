export type DiceMode = 'auto' | 'manual';

const STORAGE_KEY = 'ttrpg_dice_mode';

const mode = ref<DiceMode>(
  import.meta.client
    ? ((localStorage.getItem(STORAGE_KEY) as DiceMode | null) ?? 'auto')
    : 'auto',
);

export function useDiceMode() {
  function setMode(next: DiceMode) {
    mode.value = next;
    if (import.meta.client) localStorage.setItem(STORAGE_KEY, next);
  }

  return { mode, setMode };
}
