<script setup lang="ts">
import type { ActionQueueItemResponse, GameResponse, RulesetDefinition } from '~/types/api';
import {
  formatActionStatChanges,
  formatActionTimestamp,
  formatFollowUpRollLabel,
  splitActionDescription,
} from '~/utils/actionLog';
import { evaluatePublishedActionOutcome } from '~/utils/actionOutcome';

interface Props {
  action: ActionQueueItemResponse;
  prefix?: string;
  collapsible?: boolean;
  expanded?: boolean;
  showSequence?: boolean;
  game?: GameResponse | null;
  rulesetDefinition?: RulesetDefinition | null;
}

const props = withDefaults(defineProps<Props>(), {
  prefix: 'used',
  collapsible: false,
  expanded: true,
  showSequence: false,
  game: null,
  rulesetDefinition: null,
});

const emit = defineEmits<{
  toggle: [id: string];
}>();

const isPublished = computed(() => props.action.status === 'Published');
const isRejected = computed(() => props.action.status === 'Rejected');
const isResolved = computed(() => isPublished.value || isRejected.value);

const submission = computed(() => splitActionDescription(props.action.description));

const followUpRolls = computed(() => props.action.followUpRolls ?? []);

const statChangeLines = computed(() =>
  formatActionStatChanges(props.action, props.game, props.rulesetDefinition),
);

const submittedLabel = computed(() => formatActionTimestamp(props.action.submittedAt));

const publishedLabel = computed(() => formatActionTimestamp(props.action.publishedAt));

const outcomeBadge = computed(() => {
  const outcome = props.action.outcome
    ?? evaluatePublishedActionOutcome(props.rulesetDefinition ?? null, props.action, props.game);
  if (isPublished.value && outcome === 'Pass') {
    return { label: 'Pass', className: 'pass' };
  }
  if (isPublished.value && outcome === 'Fail') {
    return { label: 'Fail', className: 'fail' };
  }
  if (isRejected.value) {
    return { label: 'Rejected', className: 'rejected' };
  }
  return null;
});

const hasSubmissionDetails = computed(() =>
  Boolean(submission.value.body),
);

const hasResolutionDetails = computed(() =>
  Boolean(
    props.action.rollSummary
    || props.action.resolutionText
    || props.action.additionalActions
    || followUpRolls.value.length
    || statChangeLines.value.length,
  ),
);

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
    :class="[
      isPublished ? 'published-card' : '',
      isRejected ? 'rejected-card' : '',
      !isResolved ? 'pending-card' : '',
      { 'action-card-button': collapsible },
    ]"
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
        <p v-if="submittedLabel || publishedLabel" class="action-card-time text-xs muted">
          <span v-if="submittedLabel">Submitted {{ submittedLabel }}</span>
          <span v-if="submittedLabel && publishedLabel"> · </span>
          <span v-if="publishedLabel && isPublished">Resolved {{ publishedLabel }}</span>
          <span v-else-if="publishedLabel && isRejected">Rejected {{ publishedLabel }}</span>
        </p>
      </div>
      <span v-if="outcomeBadge" class="badge" :class="outcomeBadge.className">
        {{ outcomeBadge.label }}
      </span>
    </div>

    <div
      v-if="hasSubmissionDetails && (!collapsible || expanded || !isResolved)"
      class="action-detail-block"
    >
      <p v-if="submission.body" class="action-card-desc">{{ submission.body }}</p>
    </div>

    <div
      v-if="submission.playerRoll && (!collapsible || expanded || !isResolved)"
      class="action-detail-block"
    >
      <div class="action-detail-row">
        <span class="action-detail-label">Roll (recorded on submit)</span>
        <span class="roll-summary">🎲 {{ submission.playerRoll }}</span>
      </div>
    </div>

    <template v-if="isResolved && (!collapsible || expanded)">
      <div v-if="hasResolutionDetails" class="action-resolution">
        <div v-if="action.rollSummary" class="action-detail-row">
          <span class="action-detail-label">DM roll</span>
          <span class="roll-summary">🎲 {{ action.rollSummary }}</span>
        </div>

        <div v-if="followUpRolls.length" class="action-detail-section">
          <span class="action-detail-label">Player rolls</span>
          <ul class="action-detail-list">
            <li v-for="roll in followUpRolls" :key="roll.id" class="action-follow-up-item">
              <strong>{{ roll.targetCharacterName }}</strong>
              <span class="action-check-desc">{{ formatFollowUpRollLabel(roll, rulesetDefinition) }}</span>
              <span v-if="roll.rollSummary" class="roll-summary">🎲 {{ roll.rollSummary }}</span>
            </li>
          </ul>
        </div>

        <div v-if="action.resolutionText" class="action-detail-section">
          <span class="action-detail-label">{{ isRejected ? 'Rejection note' : 'Resolution' }}</span>
          <p class="action-resolution-text">{{ action.resolutionText }}</p>
        </div>

        <div v-if="action.additionalActions" class="action-detail-section">
          <span class="action-detail-label">Additional actions</span>
          <p class="action-resolution-extra">{{ action.additionalActions }}</p>
        </div>

        <div v-if="statChangeLines.length" class="action-detail-section">
          <span class="action-detail-label">Stat changes</span>
          <ul class="action-detail-list">
            <li v-for="(line, index) in statChangeLines" :key="index">{{ line }}</li>
          </ul>
        </div>

        <slot name="resolution-extra" />
      </div>
    </template>

    <p v-else-if="!isResolved" class="text-xs muted">Waiting for DM review — you may be prompted to roll.</p>
  </component>
</template>
