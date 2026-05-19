const STORAGE_KEY = 'ruleset-theme-enabled';

/**
 * Shared preference controlling whether the ruleset-driven theme is applied
 * to live session screens. Defaults to enabled; persisted in localStorage.
 *
 * Uses the `storage` event to sync across tabs on the same device, so the
 * DM toggling the preference in the games hub immediately updates any open
 * player or DM session screens running in other tabs.
 */
export function useThemePreference() {
  const enabled = useState<boolean>(STORAGE_KEY, () => true);

  // Hydrate from localStorage on first client render (handles hard reloads).
  if (import.meta.client) {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored !== null) {
      enabled.value = stored !== 'false';
    }
  }

  // React to changes made in other tabs (e.g. DM hub → player screen).
  onMounted(() => {
    function onStorage(event: StorageEvent) {
      if (event.key === STORAGE_KEY && event.newValue !== null) {
        enabled.value = event.newValue !== 'false';
      }
    }
    window.addEventListener('storage', onStorage);
    onUnmounted(() => window.removeEventListener('storage', onStorage));
  });

  function set(value: boolean) {
    enabled.value = value;
    if (import.meta.client) {
      localStorage.setItem(STORAGE_KEY, String(value));
    }
  }

  function toggle() {
    set(!enabled.value);
  }

  return { enabled, set, toggle };
}
