<script setup lang="ts">
import type { CharacterResponse, RollPromptResponse, RulesetDefinition } from '~/types/api';
import { buildRollPromptContext, rollPromptCheckLabel, rollPromptResultKindLabel, normalizeRollResultKind } from '~/utils/rollPrompt';
import { buildRollResultJson } from '~/utils/rollResult';

interface Props {
  prompt: RollPromptResponse;
  character: CharacterResponse;
  rulesetDefinition: RulesetDefinition | null;
  isSubmitting?: boolean;
  /** When true, renders as an inline panel instead of a full-screen teleport overlay. */
  inline?: boolean;
  /**
   * Whether this roll previously failed and Push the Roll is available.
   * Only relevant for d6-pool rulesets. Shows a prominent stress warning.
   */
  canPushRoll?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  isSubmitting: false,
  inline: false,
  canPushRoll: false,
});

const emit = defineEmits<{
  submit: [payload: { rollSummary: string; rollResultJson?: string; pushed?: boolean }];
}>();

const rollResult = ref('');
const pushRoll = ref(false);
const hasPushed = ref(false);

/** Push the Roll is available only for d6-pool rulesets, after a previous failure, once per roll. */
const showPushOption = computed(() =>
  props.canPushRoll
  && props.rulesetDefinition?.diceRollerKey === 'd6-pool'
  && !hasPushed.value,
);

const rollContext = computed(() => {
  if (!props.rulesetDefinition) return null;
  const base = buildRollPromptContext(props.prompt, props.rulesetDefinition, props.character);
  if (!base || !pushRoll.value || base.config.kind !== 'd6-pool') return base;
  return {
    ...base,
    config: {
      ...base.config,
      stressDiceCount: (base.config.stressDiceCount ?? 0) + 1,
    },
  };
});

const heading = computed(() => rollPromptCheckLabel(props.prompt, props.rulesetDefinition));

const queueHint = computed(() => {
  const note = props.prompt.promptLabel?.trim();
  const kind = rollPromptResultKindLabel(normalizeRollResultKind(props.prompt.resultKind));
  if (note) return `${note} · Report: ${kind}`;
  return `The DM is waiting for your roll (${kind}).`;
});

function submitRoll() {
  if (!rollResult.value.trim() || !rollContext.value) return;
  const summary = rollResult.value.trim();
  const rollResultJson = buildRollResultJson(
    summary,
    rollContext.value.rollerKey,
    normalizeRollResultKind(props.prompt.resultKind),
    { pushed: pushRoll.value, stressGained: pushRoll.value ? 1 : 0 },
  );
  if (pushRoll.value) hasPushed.value = true;
  emit('submit', {
    rollSummary: summary,
    rollResultJson,
    pushed: pushRoll.value || undefined,
  });
}

watch(
  () => props.prompt.id,
  () => {
    rollResult.value = '';
    pushRoll.value = false;
    hasPushed.value = false;
  },
);
</script>

<template>
  <Teleport v-if="!inline" to="body">
    <div class="roll-prompt-overlay" role="dialog" aria-modal="true" :aria-label="heading">
      <div class="roll-prompt-card panel">
        <div class="roll-prompt-header">
          <span class="roll-prompt-badge">DM roll request</span>
          <h2>{{ heading }}</h2>
          <p class="text-sm roll-prompt-sub">{{ queueHint }}</p>
          <p v-if="prompt.guidanceText" class="roll-prompt-guidance">{{ prompt.guidanceText }}</p>
          <p v-if="!prompt.isSessionPrompt && prompt.actionSequence" class="text-sm" style="color: var(--muted-light);">
            Related to action #{{ prompt.actionSequence }}
          </p>
        </div>

        <!-- Push the Roll — post-failure only, once per roll -->
        <div v-if="showPushOption" class="push-the-roll-banner" role="note">
          <strong>Push the Roll?</strong>
          <p class="text-sm">
            Add 1 stress die and gain +1 Stress to reroll all blank dice.
            <em>One use only — cannot be undone.</em>
          </p>
          <label class="roll-prompt-push">
            <input v-model="pushRoll" type="checkbox" :disabled="isSubmitting" />
            <span>Yes, push the roll (adds Stress)</span>
          </label>
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

  <!-- Inline variant — rendered where placed, no full-screen overlay -->
  <div v-else class="roll-prompt-inline panel nested">
    <div class="roll-prompt-header">
      <span class="roll-prompt-badge">DM roll request</span>
      <h3>{{ heading }}</h3>
      <p class="text-sm roll-prompt-sub">{{ queueHint }}</p>
      <p v-if="prompt.guidanceText" class="roll-prompt-guidance">{{ prompt.guidanceText }}</p>
      <p v-if="!prompt.isSessionPrompt && prompt.actionSequence" class="text-sm" style="color: var(--muted-light);">
        Related to action #{{ prompt.actionSequence }}
      </p>
    </div>

    <!-- Push the Roll — post-failure only, once per roll, d6-pool systems only -->
    <div v-if="showPushOption" class="push-the-roll-banner" role="note">
      <strong>Push the Roll?</strong>
      <p class="text-sm">
        Add 1 stress die and gain +1 Stress to reroll all blank dice.
        <em>One use only — cannot be undone.</em>
      </p>
      <label class="roll-prompt-push">
        <input v-model="pushRoll" type="checkbox" :disabled="isSubmitting" />
        <span>Yes, push the roll (adds Stress)</span>
      </label>
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
      style="margin-top: 0.5rem;"
      :disabled="isSubmitting || !rollResult.trim()"
      @click="submitRoll"
    >
      {{ isSubmitting ? 'Submitting…' : 'Submit roll to DM' }}
    </button>
  </div>
</template>
