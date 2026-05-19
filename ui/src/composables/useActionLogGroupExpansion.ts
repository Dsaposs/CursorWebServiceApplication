import type { ComputedRef } from 'vue';
import type { ActionQueueItemResponse, CombatEncounterResponse } from '~/types/api';
import { groupActionsForDisplay } from '~/utils/actionLog';

interface ActionLogGroupExpansionOptions {
  /** When true, every group starts expanded once; otherwise only the newest group. */
  expandAllOnFirstLoad?: boolean;
}

export function useActionLogGroupExpansion(
  actions: ComputedRef<ActionQueueItemResponse[]>,
  combatEncounters: ComputedRef<CombatEncounterResponse[]>,
  options: ActionLogGroupExpansionOptions = {},
) {
  const expandedGroups = ref<Set<string>>(new Set());
  const hasAutoExpandedGroups = ref(false);

  watch(
    actions,
    list => {
      if (!list.length) {
        expandedGroups.value = new Set();
        hasAutoExpandedGroups.value = false;
        return;
      }

      if (!hasAutoExpandedGroups.value) {
        const groups = groupActionsForDisplay(list, combatEncounters.value);
        expandedGroups.value = options.expandAllOnFirstLoad
          ? new Set(groups.map(group => group.key))
          : groups[0]
            ? new Set([groups[0].key])
            : new Set();
        hasAutoExpandedGroups.value = true;
      }
    },
    { immediate: true },
  );

  function toggleGroup(key: string) {
    const next = new Set(expandedGroups.value);
    if (next.has(key)) next.delete(key);
    else next.add(key);
    expandedGroups.value = next;
  }

  function expandAllGroups() {
    const groups = groupActionsForDisplay(actions.value, combatEncounters.value);
    expandedGroups.value = new Set(groups.map(group => group.key));
  }

  function collapseAllGroups() {
    expandedGroups.value = new Set();
  }

  return {
    expandedGroups,
    toggleGroup,
    expandAllGroups,
    collapseAllGroups,
  };
}
