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
  showSequence?: boolean;
  actionPrefix?: string;
  collapsibleActions?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  combatEncounters: () => [],
  game: null,
  rulesetDefinition: null,
  showSequence: false,
  actionPrefix: 'used',
  collapsibleActions: true,
});

const emit = defineEmits<{
  toggleAction: [id: string];
  toggleGroup: [key: string];
}>();

const groups = computed(() =>
  groupActionsForDisplay(props.actions, props.combatEncounters ?? []),
);
</script>

<template>
  <div v-if="groups.length === 0" class="empty-state" style="padding: 1rem 0;">
    <p class="text-sm">No actions to show.</p>
  </div>

  <div v-else class="action-log-groups">
    <section
      v-for="group in groups"
      :key="group.key"
      class="action-log-group"
      :class="{
        'action-log-group-combat': group.kind === 'combat',
        'action-log-group-exploration': group.kind === 'exploration',
        'action-log-group-skill-check': group.kind === 'skillCheck',
      }"
    >
      <button
        type="button"
        class="action-log-group-header"
        :aria-expanded="expandedGroups.has(group.key)"
        @click="emit('toggleGroup', group.key)"
      >
        <div class="action-log-group-heading">
          <span class="action-log-group-chevron" aria-hidden="true">
            {{ expandedGroups.has(group.key) ? '▼' : '▶' }}
          </span>
          <div>
            <h3 class="action-log-group-title">{{ group.label }}</h3>
            <p v-if="group.subtitle" class="action-log-group-subtitle text-sm">{{ group.subtitle }}</p>
          </div>
        </div>
        <span
          class="badge"
          :class="group.kind === 'combat' ? 'combat' : group.kind === 'skillCheck' ? 'pending' : 'published'"
        >
          {{ group.actions.length }} action{{ group.actions.length === 1 ? '' : 's' }}
        </span>
      </button>

      <div v-show="expandedGroups.has(group.key)" class="action-log-group-body">
        <ActionCard
          v-for="action in group.actions"
          :key="action.id"
          :action="action"
          :game="game"
          :ruleset-definition="rulesetDefinition"
          :prefix="actionPrefix"
          :show-sequence="showSequence"
          :expanded="expandedActions.has(action.id)"
          :collapsible="collapsibleActions"
          @toggle="emit('toggleAction', $event)"
        >
          <template v-if="showSequence" #meta>
            <slot name="action-meta" :action="action" />
          </template>
        </ActionCard>
      </div>
    </section>
  </div>
</template>
