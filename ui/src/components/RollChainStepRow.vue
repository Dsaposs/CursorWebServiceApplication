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
  /** When true, show DM roll controls (prompt player / roll for player). */
  isDmMode?: boolean;
  isBusy?: boolean;
  rulesetDefinition: RulesetDefinition | null;
}

const props = withDefaults(defineProps<Props>(), {
  prompt: null,
  isDmMode: false,
  isBusy: false,
});

const emit = defineEmits<{
  promptPlayer: [stepKey: string];
  dmRoll: [stepKey: string];
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
</script>

<template>
  <div
    class="roll-chain-step"
    :class="{
      'roll-chain-step--current': isCurrent,
      'roll-chain-step--completed': isCompleted,
      'roll-chain-step--pending': !isCurrent && !isCompleted,
    }"
  >
    <div class="roll-chain-step-header">
      <span class="roll-chain-step-label">{{ stepLabel }}</span>

      <span v-if="isCompleted && outcomeBadge" class="badge" :class="outcomeBadge.cls">
        {{ outcomeBadge.label }}
      </span>
      <span v-else-if="isCurrent && prompt?.status === 'Pending'" class="badge pending">
        Awaiting roll
      </span>
      <span v-else-if="isCurrent" class="badge active">Active</span>
      <span v-else class="badge" style="opacity: 0.45;">Locked</span>
    </div>

    <!-- Completed step: show roll summary -->
    <div v-if="isCompleted && prompt" class="roll-chain-step-result">
      <span class="text-sm muted">{{ promptCheckLabel }}</span>
      <span v-if="prompt.rollSummary" class="text-sm roll-result" style="margin-left: 0.5rem;">
        🎲 {{ prompt.rollSummary }}
      </span>
      <span v-if="prompt.dc" class="badge" style="margin-left: 0.35rem; font-size: 0.72rem;">
        DC {{ prompt.dc }}
      </span>
      <span v-if="prompt.autoResolveOutcome" class="badge" :class="prompt.autoResolveOutcome === 'success' ? 'pass' : 'fail'" style="margin-left: 0.35rem; font-size: 0.72rem;">
        {{ formatAutoResolveLabel(prompt.autoResolveOutcome) }}
      </span>
    </div>

    <!-- DM controls for the current active step -->
    <div v-if="isDmMode && isCurrent && !isCompleted" class="roll-chain-step-controls">
      <template v-if="!prompt || prompt.status !== 'Pending'">
        <button
          type="button"
          class="btn sm"
          :disabled="isBusy"
          @click="emit('promptPlayer', step.step)"
        >
          Prompt player
        </button>
        <button
          type="button"
          class="btn ghost sm"
          :disabled="isBusy"
          @click="emit('dmRoll', step.step)"
        >
          Roll for player
        </button>
        <p v-if="step.guidanceText" class="text-sm muted" style="margin: 0.25rem 0 0; flex-basis: 100%;">
          {{ step.guidanceText }}
        </p>
      </template>
      <template v-else-if="prompt.status === 'Pending'">
        <span class="text-sm muted">Waiting for {{ prompt.targetCharacterName }}…</span>
        <button
          type="button"
          class="btn ghost sm"
          :disabled="isBusy"
          @click="emit('cancelPrompt', prompt.id)"
        >
          Cancel
        </button>
      </template>
    </div>
  </div>
</template>
