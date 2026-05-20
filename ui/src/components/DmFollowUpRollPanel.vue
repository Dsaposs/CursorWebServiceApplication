<script setup lang="ts">
import type { ActionQueueItemResponse, CharacterResponse, RollPromptResponse, RulesetDefinition } from '~/types/api';
import {
  actionNeedsPlayerRoll,
  actionRollFlowBadgeClass,
  actionRollFlowLabel,
  formatRollFlowHint,
  getActionRollFlowStatus,
  rollPromptsForAction,
} from '~/utils/actionRolls';
import { normalizeRollResultKind, rollPromptCheckLabel, rollPromptResultKindLabel } from '~/utils/rollPrompt';
import { findRulesetAction } from '~/utils/rulesets';
import { formatAutoResolveLabel } from '~/utils/rollResult';

interface Props {
  action: ActionQueueItemResponse;
  characters: CharacterResponse[];
  rollPrompts: RollPromptResponse[];
  rulesetDefinition: RulesetDefinition | null;
  isBusy?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  isBusy: false,
});

const emit = defineEmits<{
  (e: 'startChain'): void;
  (e: 'send', payload: { prompts: Array<{ targetCharacterId: string; checkMode: string; actionKey?: string; resultKind: string; dc?: number | null }> }): void;
  (e: 'cancel', promptId: string): void;
  (e: 'dmRoll', payload: { actionId: string; rollSummary: string; dc?: number | null }): void;
}>();

const actionPrompts = computed(() =>
  rollPromptsForAction(props.action.id, props.rollPrompts),
);

const rollFlowStatus = computed(() =>
  getActionRollFlowStatus(props.action, props.rollPrompts, props.rulesetDefinition),
);

const rollFlowHint = computed(() =>
  formatRollFlowHint(rollFlowStatus.value, props.action.actorName),
);

const needsPlayerRoll = computed(() =>
  actionNeedsPlayerRoll(props.action, props.rulesetDefinition),
);

const actorCharacter = computed(() =>
  props.characters.find(c => c.id === props.action.actorCharacterId) ?? null,
);

const hasRollChain = computed(() => {
  if (!props.rulesetDefinition || !props.action.actionKey) return false;
  const actionDef = findRulesetAction(props.rulesetDefinition, props.action.actionKey);
  return Boolean(actionDef?.rollChain?.length);
});

const hasPendingPrompt = computed(() =>
  actionPrompts.value.some(p => p.status === 'Pending'),
);

const canRequestActorRoll = computed(() =>
  Boolean(actorCharacter.value)
  && props.action.actionKey
  && !hasPendingPrompt.value
  && !hasRollChain.value,
);

const showStartChain = computed(() =>
  hasRollChain.value && !hasPendingPrompt.value && Boolean(actorCharacter.value),
);

// DC + DM roll state
const dc = ref<number | null>(null);
const showDmRollForm = ref(false);
const dmRollInput = ref('');

function requestActorRoll() {
  if (!actorCharacter.value || !props.action.actionKey) return;

  emit('send', {
    prompts: [{
      targetCharacterId: actorCharacter.value.id,
      checkMode: 'Action',
      actionKey: props.action.actionKey,
      resultKind: 'PassFail',
      dc: dc.value,
    }],
  });
}

function submitDmRoll() {
  if (!dmRollInput.value.trim()) return;
  emit('dmRoll', {
    actionId: props.action.id,
    rollSummary: dmRollInput.value.trim(),
    dc: dc.value,
  });
  dmRollInput.value = '';
  showDmRollForm.value = false;
}

function promptSummary(prompt: RollPromptResponse) {
  return rollPromptCheckLabel(prompt, props.rulesetDefinition);
}
</script>

<template>
  <section v-if="needsPlayerRoll || actionPrompts.length" class="dm-action-roll-panel panel nested">
    <div class="dm-action-roll-header">
      <div>
        <h3>Player rolls</h3>
        <p v-if="rollFlowHint" class="text-sm dm-action-roll-hint">{{ rollFlowHint }}</p>
      </div>
      <span
        v-if="actionRollFlowLabel(rollFlowStatus)"
        class="badge"
        :class="actionRollFlowBadgeClass(rollFlowStatus)"
      >
        {{ actionRollFlowLabel(rollFlowStatus) }}
      </span>
    </div>

    <!-- DC row — shown whenever the DM can send a roll or roll directly -->
    <div v-if="(showStartChain || canRequestActorRoll) && !hasPendingPrompt" class="dm-action-roll-dc-row">
      <label class="text-sm" for="dm-roll-dc">DC (optional)</label>
      <input
        id="dm-roll-dc"
        v-model.number="dc"
        type="number"
        min="1"
        max="100"
        placeholder="—"
        class="input-small"
      />
      <span class="text-sm muted">Pass if roll ≥ DC. Leave blank for manual adjudication.</span>
    </div>

    <div v-if="showStartChain" class="dm-action-roll-primary">
      <button type="button" class="btn" :disabled="isBusy" @click="emit('startChain')">
        Request attack roll from {{ action.actorName }}
      </button>
      <p class="text-sm muted">Starts the attack → damage chain for this action.</p>
    </div>

    <div v-else-if="canRequestActorRoll" class="dm-action-roll-primary">
      <div class="dm-roll-actions">
        <button type="button" class="btn" :disabled="isBusy" @click="requestActorRoll">
          Send to {{ action.actorName }}
        </button>
        <button
          type="button"
          class="btn ghost"
          :disabled="isBusy"
          @click="showDmRollForm = !showDmRollForm"
        >
          Roll myself
        </button>
      </div>

      <div v-if="showDmRollForm" class="dm-manual-roll-form">
        <label class="text-sm" for="dm-roll-input">Roll result</label>
        <input
          id="dm-roll-input"
          v-model="dmRollInput"
          type="text"
          placeholder="e.g. 3 successes or 14"
          class="input"
        />
        <p class="text-sm muted">
          Enter the dice result.
          <span v-if="dc">DC {{ dc }} — {{ action.actorName }}'s roll will be checked against it automatically.</span>
          <span v-else>No DC set — outcome will be marked for your interpretation.</span>
        </p>
        <div class="dm-manual-roll-btns">
          <button type="button" class="btn sm" :disabled="isBusy || !dmRollInput.trim()" @click="submitDmRoll">
            Submit roll
          </button>
          <button type="button" class="btn ghost sm" @click="showDmRollForm = false; dmRollInput = ''">
            Cancel
          </button>
        </div>
      </div>
    </div>

    <div v-if="actionPrompts.length" class="follow-up-roll-list">
      <div
        v-for="prompt in actionPrompts"
        :key="prompt.id"
        class="follow-up-roll-item"
      >
        <div>
          <strong>{{ prompt.targetCharacterName }}</strong>
          <span class="text-sm"> — {{ promptSummary(prompt) }}</span>
          <span v-if="prompt.dmRolled" class="badge" style="margin-left: 0.35rem;">DM rolled</span>
          <span v-if="prompt.dc" class="badge" style="margin-left: 0.35rem;">DC {{ prompt.dc }}</span>
        </div>
        <div class="follow-up-roll-item-meta">
          <span class="badge" :class="prompt.status === 'Completed' ? 'active' : 'pending'">
            {{ prompt.status === 'Completed' ? 'Rolled' : 'Awaiting' }}
          </span>
          <span class="badge" style="margin-left: 0.25rem;">
            {{ rollPromptResultKindLabel(normalizeRollResultKind(prompt.resultKind)) }}
          </span>
          <span v-if="prompt.chainStepKey" class="badge">{{ prompt.chainStepKey }}</span>
          <span
            v-if="prompt.autoResolveOutcome"
            class="badge"
            :class="prompt.autoResolveOutcome === 'success' ? 'pass' : prompt.autoResolveOutcome === 'failure' ? 'fail' : 'pending'"
          >
            {{ formatAutoResolveLabel(prompt.autoResolveOutcome) }}
          </span>
          <span v-if="prompt.rollSummary" class="text-sm roll-result">🎲 {{ prompt.rollSummary }}</span>
          <DmRollBreakdown v-if="prompt.status === 'Completed' && prompt.rollResultJson" :prompt="prompt" />
          <button
            v-if="prompt.status === 'Pending'"
            type="button"
            class="btn ghost sm"
            :disabled="isBusy"
            @click="emit('cancel', prompt.id)"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>
  </section>
</template>
