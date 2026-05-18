<script setup lang="ts">
import type { ActionQueueItemResponse } from '~/types/api';

interface Props {
  actions: ActionQueueItemResponse[];
  expandedActions: Set<string>;
}

defineProps<Props>();

const emit = defineEmits<{
  toggle: [id: string];
  expandAll: [];
  collapseAll: [];
}>();
</script>

<template>
  <div class="panel dashboard-primary-panel action-log-panel">
    <div class="panel-title">
      <div>
        <h2>Action Log</h2>
        <p class="text-sm">Most recent resolved actions appear first.</p>
      </div>
      <div class="btn-row">
        <span v-if="actions.length" class="badge published">{{ actions.length }} resolved</span>
        <button v-if="actions.length" class="btn ghost sm" type="button" @click="emit('expandAll')">Expand</button>
        <button v-if="actions.length" class="btn ghost sm" type="button" @click="emit('collapseAll')">Collapse</button>
      </div>
    </div>
    <div class="action-log-scroll">
      <div v-if="actions.length === 0" class="empty-state" style="padding: 1rem 0;">
        <p class="text-sm">No resolved actions yet.</p>
      </div>
      <ActionCard
        v-for="action in actions"
        :key="action.id"
        :action="action"
        :expanded="expandedActions.has(action.id)"
        collapsible
        @toggle="emit('toggle', $event)"
      />
    </div>
  </div>
</template>
