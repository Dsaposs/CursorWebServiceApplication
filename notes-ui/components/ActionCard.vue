<script setup lang="ts">
import type { ActionQueueItemResponse } from '~/types/api';

interface Props {
  action: ActionQueueItemResponse;
  prefix?: string;
  collapsible?: boolean;
  expanded?: boolean;
  showSequence?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  prefix: 'used',
  collapsible: false,
  expanded: true,
  showSequence: false,
});

const emit = defineEmits<{
  toggle: [id: string];
}>();

const isPublished = computed(() => props.action.status === 'Published');

function toggle() {
  if (props.collapsible) {
    emit('toggle', props.action.id);
  }
}
</script>

<template>
  <component
    :is="collapsible ? 'button' : 'article'"
    class="action-card"
    :class="[isPublished ? 'published-card' : 'pending-card', { 'action-card-button': collapsible }]"
    :type="collapsible ? 'button' : undefined"
    :aria-expanded="collapsible ? expanded : undefined"
    @click="toggle"
  >
    <div class="action-card-header">
      <div>
        <div v-if="showSequence" class="flex items-center gap-2">
          <span class="text-xs muted font-mono">#{{ action.sequence }}</span>
          <slot name="meta" />
        </div>
        <div class="action-card-actor">{{ action.actorName }}</div>
        <div class="action-card-target">
          <span v-if="prefix">{{ prefix }} </span><strong>{{ action.actionText }}</strong>
          <span v-if="action.targetName"> on {{ action.targetName }}</span>
        </div>
        <div v-if="action.description" class="action-card-desc">{{ action.description }}</div>
      </div>
      <span class="badge" :class="isPublished ? 'published' : 'pending'">
        {{ isPublished ? 'Done' : action.status }}
      </span>
    </div>

    <template v-if="isPublished && (!collapsible || expanded)">
      <div class="action-resolution">
        <div v-if="action.rollSummary" class="roll-summary">
          <span aria-hidden="true">🎲</span> {{ action.rollSummary }}
        </div>
        <p class="action-resolution-text">{{ action.resolutionText }}</p>
        <p v-if="action.additionalActions" class="action-resolution-extra">{{ action.additionalActions }}</p>
        <slot name="resolution-extra" />
      </div>
    </template>

    <p v-else-if="!isPublished" class="text-xs muted">Waiting for DM to resolve…</p>
  </component>
</template>
