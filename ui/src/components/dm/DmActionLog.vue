<script setup lang="ts">
import type {
  ActionQueueItemResponse,
  CombatEncounterResponse,
  GameResponse,
  RulesetDefinition,
} from '~/types/api';
import { groupActionsForDisplay } from '~/utils/actionLog';

interface Props {
  actions: ActionQueueItemResponse[];
  combatEncounters?: CombatEncounterResponse[];
  expandedActions: Set<string>;
  expandedGroups: Set<string>;
  game?: GameResponse | null;
  rulesetDefinition?: RulesetDefinition | null;
}

const props = withDefaults(defineProps<Props>(), {
  combatEncounters: () => [],
  game: null,
  rulesetDefinition: null,
});

const emit = defineEmits<{
  toggleAction: [id: string];
  toggleGroup: [key: string];
  expandAll: [];
  collapseAll: [];
}>();

const groupCount = computed(() =>
  groupActionsForDisplay(props.actions, props.combatEncounters).length,
);
</script>

<template>
  <div class="panel dashboard-primary-panel action-log-panel">
    <div class="panel-title">
      <div>
        <h2>Action Log</h2>
        <p class="text-sm">Grouped by skill checks and combat encounters. Most recent groups appear first.</p>
      </div>
      <div class="btn-row">
        <span v-if="actions.length" class="badge published">{{ actions.length }} action{{ actions.length === 1 ? '' : 's' }}</span>
        <span v-if="groupCount > 1" class="badge" style="background: var(--panel-alt); color: var(--muted-light); border: 1px solid var(--border);">
          {{ groupCount }} groups
        </span>
        <button v-if="actions.length" class="btn ghost sm" type="button" @click="emit('expandAll')">Expand all</button>
        <button v-if="actions.length" class="btn ghost sm" type="button" @click="emit('collapseAll')">Collapse all</button>
      </div>
    </div>
    <div class="action-log-scroll">
      <ActionLogGrouped
        :actions="actions"
        :combat-encounters="combatEncounters"
        :expanded-actions="expandedActions"
        :expanded-groups="expandedGroups"
        :game="game"
        :ruleset-definition="rulesetDefinition"
        action-prefix="used"
        @toggle-action="emit('toggleAction', $event)"
        @toggle-group="emit('toggleGroup', $event)"
      />
    </div>
  </div>
</template>
