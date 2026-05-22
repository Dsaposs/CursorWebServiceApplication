<script setup lang="ts">
import type {
  ActionQueueItemResponse,
  CharacterResponse,
  RollPromptResponse,
  RulesetDefinition,
} from '~/types/api';
import PlayerRollPromptOverlay from '~/components/PlayerRollPromptOverlay.vue';
import RollChainStepRow from '~/components/RollChainStepRow.vue';
import { getPlayerRollChainView, rollPromptsForAction } from '~/utils/actionRolls';

interface Props {
  action: ActionQueueItemResponse;
  prompt: RollPromptResponse;
  character: CharacterResponse;
  rollPrompts: RollPromptResponse[];
  rulesetDefinition: RulesetDefinition | null;
  isSubmitting?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  isSubmitting: false,
});

const emit = defineEmits<{
  submit: [payload: { rollSummary: string; rollResultJson?: string; pushed?: boolean }];
}>();

const chainView = computed(() =>
  getPlayerRollChainView(props.rulesetDefinition, props.action, props.rollPrompts),
);

const actionPrompts = computed(() =>
  rollPromptsForAction(props.action.id, props.rollPrompts, props.action.followUpRolls ?? []),
);

function latestPromptForStep(stepKey: string) {
  return [...actionPrompts.value]
    .reverse()
    .find(prompt => prompt.chainStepKey === stepKey) ?? null;
}

function isStepCompleted(stepKey: string) {
  return actionPrompts.value.some(
    prompt => prompt.chainStepKey === stepKey && prompt.status === 'Completed',
  );
}

const canSubmitCurrentPrompt = computed(() =>
  props.prompt.status === 'Pending' && !props.isSubmitting,
);

const statusMessage = computed(() => {
  if (chainView.value?.isComplete) {
    return chainView.value.terminatedEarly
      ? 'Attack missed — your rolls were sent to the DM for review.'
      : 'All rolls complete — waiting for the DM to publish the outcome.';
  }

  if (chainView.value?.awaitingNextPrompt) {
    return 'Preparing your next roll…';
  }

  if (!canSubmitCurrentPrompt.value && props.prompt.status === 'Completed') {
    return 'Roll submitted.';
  }

  return '';
});
</script>

<template>
  <Teleport to="body">
    <div class="roll-prompt-overlay" role="dialog" aria-modal="true" aria-label="Action roll chain">
      <div class="roll-prompt-card panel player-roll-chain-card">
        <div class="roll-prompt-header">
          <span class="roll-prompt-badge">Your turn — roll chain</span>
          <h2>{{ action.actionText }}</h2>
          <p v-if="action.targetName" class="text-sm muted" style="margin: 0.35rem 0 0;">
            Target: <strong>{{ action.targetName }}</strong>
          </p>
          <p class="text-sm roll-prompt-sub" style="margin-top: 0.5rem;">
            Complete each roll in order before the DM reviews your action.
          </p>
        </div>

        <div v-if="chainView" class="roll-chain-steps" style="margin-bottom: 0.75rem;">
          <RollChainStepRow
            v-for="step in chainView.steps"
            :key="step.step"
            :step="step"
            :prompt="latestPromptForStep(step.step)"
            :is-current="step.step === chainView.currentStepKey"
            :is-completed="isStepCompleted(step.step)"
            :is-skipped="chainView.skippedStepKeys.has(step.step)"
            :ruleset-definition="rulesetDefinition"
          />
        </div>

        <p v-if="statusMessage" class="text-sm dm-action-roll-hint" style="margin: 0 0 0.75rem;">
          {{ statusMessage }}
        </p>

        <PlayerRollPromptOverlay
          v-if="canSubmitCurrentPrompt"
          :key="prompt.id"
          :prompt="prompt"
          :character="character"
          :ruleset-definition="rulesetDefinition"
          :is-submitting="isSubmitting"
          inline
          @submit="emit('submit', $event)"
        />
      </div>
    </div>
  </Teleport>
</template>
