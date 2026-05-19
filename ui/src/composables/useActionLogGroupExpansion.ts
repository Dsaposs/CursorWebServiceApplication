import type { ComputedRef } from 'vue';
import type { ActionQueueItemResponse, CombatEncounterResponse } from '~/types/api';
import { groupActionsForDisplay } from '~/utils/actionLog';

interface ActionLogGroupExpansionOptions {
  /** When true, every group starts expanded once; otherwise only the newest group. */
  expandAllOnFirstLoad?: boolean;
}

function storageKey(sessionId: string) {
  return `action-log-expanded-groups:${sessionId}`;
}

function loadExpandedFromStorage(sessionId: string): Set<string> | null {
  if (!import.meta.client) return null;

  try {
    const raw = sessionStorage.getItem(storageKey(sessionId));
    if (!raw) return null;
    const parsed = JSON.parse(raw) as string[];
    return Array.isArray(parsed) ? new Set(parsed) : null;
  } catch {
    return null;
  }
}

function saveExpandedToStorage(sessionId: string, keys: Set<string>) {
  if (!import.meta.client) return;
  sessionStorage.setItem(storageKey(sessionId), JSON.stringify([...keys]));
}

function groupKeys(
  actions: ActionQueueItemResponse[],
  combatEncounters: CombatEncounterResponse[],
): Set<string> {
  return new Set(groupActionsForDisplay(actions, combatEncounters).map(group => group.key));
}

function pruneExpandedKeys(expanded: Set<string>, validKeys: Set<string>): Set<string> {
  return new Set([...expanded].filter(key => validKeys.has(key)));
}

export function useActionLogGroupExpansion(
  actions: ComputedRef<ActionQueueItemResponse[]>,
  combatEncounters: ComputedRef<CombatEncounterResponse[]>,
  sessionId: ComputedRef<string | undefined> = computed(() => undefined),
  options: ActionLogGroupExpansionOptions = {},
) {
  const expandedGroups = ref<Set<string>>(new Set());
  const activeSessionId = ref<string | null>(null);
  const hasHydrated = ref(false);

  function persist() {
    const id = sessionId.value;
    if (id) saveExpandedToStorage(id, expandedGroups.value);
  }

  function applyDefaultExpansion(list: ActionQueueItemResponse[]) {
    const groups = groupActionsForDisplay(list, combatEncounters.value);
    expandedGroups.value = options.expandAllOnFirstLoad
      ? new Set(groups.map(group => group.key))
      : groups[0]
        ? new Set([groups[0].key])
        : new Set();
  }

  function syncForSession() {
    const id = sessionId.value;
    const list = actions.value;
    if (!id) return;

    if (activeSessionId.value !== id) {
      activeSessionId.value = id;
      hasHydrated.value = false;
      expandedGroups.value = new Set();
    }

    if (!list.length) return;

    if (!hasHydrated.value) {
      const saved = loadExpandedFromStorage(id);
      if (saved !== null) {
        expandedGroups.value = pruneExpandedKeys(saved, groupKeys(list, combatEncounters.value));
      } else {
        applyDefaultExpansion(list);
      }
      persist();
      hasHydrated.value = true;
      return;
    }

    const pruned = pruneExpandedKeys(expandedGroups.value, groupKeys(list, combatEncounters.value));
    if (pruned.size !== expandedGroups.value.size) {
      expandedGroups.value = pruned;
      persist();
    }
  }

  watch([actions, combatEncounters, sessionId], syncForSession, { immediate: true });

  function toggleGroup(key: string) {
    const next = new Set(expandedGroups.value);
    if (next.has(key)) next.delete(key);
    else next.add(key);
    expandedGroups.value = next;
    persist();
  }

  function expandAllGroups() {
    const groups = groupActionsForDisplay(actions.value, combatEncounters.value);
    expandedGroups.value = new Set(groups.map(group => group.key));
    persist();
  }

  function collapseAllGroups() {
    expandedGroups.value = new Set();
    persist();
  }

  return {
    expandedGroups,
    toggleGroup,
    expandAllGroups,
    collapseAllGroups,
  };
}
