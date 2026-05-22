<script setup lang="ts">
import type { RollPromptResponse, RulesetDefinition, RulesetRollChainStepDefinition } from '~/types/api';
import { rollPromptCheckLabel } from '~/utils/rollPrompt';
import { formatAutoResolveLabel } from '~/utils/rollResult';

interface Props {
  step: RulesetRollChainStepDefinition;
  /** The roll prompt linked to this step, if any. */
  prompt?: RollPromptResponse | null;
  isCurrent: boolean;
  isCompleted: boolean;
  isSkipped?: boolean;
  /** When true, show DM roll controls (prompt player). */
  isDmMode?: boolean;
  /** Player character who should roll this step. */
  actorName?: string;
  allowManualPrompt?: boolean;
  isBusy?: boolean;
  rulesetDefinition: RulesetDefinition | null;
}

const props = withDefaults(defineProps<Props>(), {
  prompt: null,
  isSkipped: false,
  isDmMode: false,
  actorName: '',
  allowManualPrompt: false,
  isBusy: false,
});

const emit = defineEmits<{
  promptPlayer: [stepKey: string];
  cancelPrompt: [promptId: string];
}>();

const stepLabel = computed(() => props.step.label ?? props.step.step);

const outcomeBadge = computed(() => {
  if (!props.prompt) return null;
  if (props.prompt.status !== 'Completed') return null;
  const outcome = props.prompt.autoResolveOutcome;
  if (outcome === 'success') return { label: 'Hit', cls: 'pass' };
  if (outcome === 'failure') return { label: 'Miss', cls: 'fail' };
  return { label: 'Rolled', cls: 'active' };
});

const promptCheckLabel = computed(() =>
  props.prompt && props.rulesetDefinition
    ? rollPromptCheckLabel(props.prompt, props.rulesetDefinition)
    : stepLabel.value,
);

const statusBadge = computed(() => {
  if (props.isSkipped) return { label: 'Skipped', cls: 'muted' };
  if (props.isCompleted && outcomeBadge.value) return outcomeBadge.value;
  if (props.isCurrent && props.prompt?.status === 'Pending') return { label: 'Awaiting roll', cls: 'pending' };
  if (props.isCurrent) return { label: 'Active', cls: 'active' };
  return { label: 'Locked', cls: 'locked' };
});
</script>

<template>
  <div
    class="roll-chain-step"
    :class="{
      'roll-chain-step--current': isCurrent,
      'roll-chain-step--completed': isCompleted,
      'roll-chain-step--pending': !isCurrent && !isCompleted && !isSkipped,
      'roll-chain-step--skipped': isSkipped,
    }"
  >
    <div class="roll-chain-step-header">
      <span class="roll-chain-step-label">{{ stepLabel }}</span>

      <span
        class="badge"
        :class="statusBadge.cls === 'locked' ? '' : statusBadge.cls"
        :style="statusBadge.cls === 'locked' ? { opacity: 0.45 } : undefined"
      >
        {{ statusBadge.label }}
      </span>
    </div>

    <div v-if="isCompleted && prompt" class="roll-chain-step-result">
      <span class="text-sm muted">{{ promptCheckLabel }}</span>
      <span v-if="prompt.rollSummary" class="text-sm roll-result" style="margin-left: 0.5rem;">
        🎲 {{ prompt.rollSummary }}
      </span>
      <span v-if="prompt.dc" class="badge" style="margin-left: 0.35rem; font-size: 0.72rem;">
        DC {{ prompt.dc }}
      </span>
      <span
        v-if="prompt.autoResolveOutcome"
        class="badge"
        :class="prompt.autoResolveOutcome === 'success' ? 'pass' : 'fail'"
        style="margin-left: 0.35rem; font-size: 0.72rem;"
      >
        {{ formatAutoResolveLabel(prompt.autoResolveOutcome) }}
      </span>
    </div>

    <div v-if="isDmMode && isCurrent && !isCompleted && !isSkipped" class="roll-chain-step-controls">
      <template v-if="prompt?.status === 'Pending'">
        <span class="text-sm muted">
          Waiting for {{ actorName || 'the player' }} to roll on their screen…
        </span>
        <button
          type="button"
          class="btn ghost sm"
          :disabled="isBusy"
          @click="emit('cancelPrompt', prompt.id)"
        >
          Cancel prompt
        </button>
      </template>
      <template v-else-if="allowManualPrompt">
        <button
          type="button"
          class="btn sm"
          :disabled="isBusy"
          @click="emit('promptPlayer', step.step)"
        >
          Prompt {{ actorName || 'player' }}
        </button>
        <p v-if="step.guidanceText" class="text-sm muted" style="margin: 0.25rem 0 0; flex-basis: 100%;">
          {{ step.guidanceText }}
        </p>
      </template>
    </div>
  </div>
</template>
