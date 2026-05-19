<script setup lang="ts">
import type { CharacterResponse, RollPromptResponse, RulesetDefinition } from '~/types/api';
import { buildRollPromptContext, rollPromptCheckLabel } from '~/utils/rollPrompt';

interface Props {
  prompt: RollPromptResponse;
  character: CharacterResponse;
  rulesetDefinition: RulesetDefinition | null;
  isSubmitting?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  isSubmitting: false,
});

const emit = defineEmits<{
  submit: [rollSummary: string];
}>();

const rollResult = ref('');

const rollContext = computed(() => {
  if (!props.rulesetDefinition) return null;
  return buildRollPromptContext(props.prompt, props.rulesetDefinition, props.character);
});

const heading = computed(() => rollPromptCheckLabel(props.prompt, props.rulesetDefinition));

const queueHint = computed(() => {
  const note = props.prompt.promptLabel?.trim();
  return note || 'The DM is waiting for your roll.';
});

function submitRoll() {
  if (!rollResult.value.trim()) return;
  emit('submit', rollResult.value.trim());
}

watch(
  () => props.prompt.id,
  () => {
    rollResult.value = '';
  },
);
</script>

<template>
  <Teleport to="body">
    <div class="roll-prompt-overlay" role="dialog" aria-modal="true" :aria-label="heading">
      <div class="roll-prompt-card panel">
        <div class="roll-prompt-header">
          <span class="roll-prompt-badge">DM roll request</span>
          <h2>{{ heading }}</h2>
          <p class="text-sm roll-prompt-sub">{{ queueHint }}</p>
          <p v-if="!prompt.isSessionPrompt && prompt.actionSequence" class="text-sm" style="color: var(--muted-light);">
            Related to action #{{ prompt.actionSequence }}
          </p>
        </div>

        <RulesetDiceRoller
          v-if="rollContext"
          v-model="rollResult"
          :context="rollContext"
        />

        <div v-else class="alert info">
          <p class="text-sm">Could not load dice roller for this check. Describe your roll result below.</p>
          <label>
            Roll result
            <input v-model.trim="rollResult" placeholder="Enter your roll result…" />
          </label>
        </div>

        <button
          type="button"
          class="btn roll-prompt-submit"
          :disabled="isSubmitting || !rollResult.trim()"
          @click="submitRoll"
        >
          {{ isSubmitting ? 'Submitting…' : 'Submit roll to DM' }}
        </button>
      </div>
    </div>
  </Teleport>
</template>
