<script setup lang="ts">
import type {
  ActionQueueItemResponse,
  CharacterResponse,
  NpcResponse,
  RollPromptResponse,
  RulesetDefinition,
  RulesetRollChainStepDefinition,
} from '~/types/api';
import DmFollowUpRollPanel from '~/components/DmFollowUpRollPanel.vue';
import DmStatChangePanel from '~/components/DmStatChangePanel.vue';
import RollChainStepRow from '~/components/RollChainStepRow.vue';
import {
  canPublishActionResolution,
  evaluateActionOutcomeFromRolls,
  getRollChainProgress,
  rollChainStatusHint,
  rollPromptsForAction,
} from '~/utils/actionRolls';
import { findRulesetAction, describeRulesetAction } from '~/utils/rulesets';

interface StatChangeModel {
  addKeys: string[];
  removeKeys: string[];
}

interface Props {
  action: ActionQueueItemResponse;
  characters: CharacterResponse[];
  npcs: NpcResponse[];
  rollPrompts: RollPromptResponse[];
  rulesetDefinition: RulesetDefinition | null;
  isBusy?: boolean;
  resolutionText?: string;
  statTarget?: string;
  statHealthDelta?: string;
  statSetHealth?: string;
  statSetArmor?: string;
  statGvDeltas?: Record<string, string>;
  statAttrDeltas?: Record<string, string>;
  statInventoryDeltas?: Record<string, string>;
  statStatusChanges?: StatChangeModel;
}

const props = withDefaults(defineProps<Props>(), {
  isBusy: false,
  resolutionText: '',
  statTarget: '',
  statHealthDelta: '',
  statSetHealth: '',
  statSetArmor: '',
  statGvDeltas: () => ({}),
  statAttrDeltas: () => ({}),
  statInventoryDeltas: () => ({}),
  statStatusChanges: () => ({ addKeys: [], removeKeys: [] }),
});

const emit = defineEmits<{
  resolve: [payload: {
    resolutionText: string;
    outcome?: string;
    statTarget: string;
    statHealthDelta: string;
    statSetHealth: string;
    statSetArmor: string;
    statGvDeltas: Record<string, string>;
    statAttrDeltas: Record<string, string>;
    statInventoryDeltas: Record<string, string>;
    statStatusChanges: StatChangeModel;
  }];
  reject: [reason: string];
  startChain: [];
  sendRollPrompts: [payload: { prompts: Array<{
    targetCharacterId: string;
    checkMode: string;
    actionKey?: string;
    skillKey?: string;
    attributeKey?: string;
    customCheckText?: string;
    promptLabel?: string;
    guidanceText?: string;
    resultKind: string;
    dc?: number | null;
    chainStepKey?: string;
  }> }];
  cancelPrompt: [promptId: string];
  dmRoll: [payload: { actionId: string; rollSummary: string; rollResultJson?: string; dc?: number | null; chainStepKey?: string }];
  'update:resolutionText': [value: string];
  'update:statTarget': [value: string];
  'update:statHealthDelta': [value: string];
  'update:statSetHealth': [value: string];
  'update:statSetArmor': [value: string];
  'update:statGvDeltas': [value: Record<string, string>];
  'update:statAttrDeltas': [value: Record<string, string>];
  'update:statInventoryDeltas': [value: Record<string, string>];
  'update:statStatusChanges': [value: StatChangeModel];
}>();

const resolutionTextModel = computed({
  get: () => props.resolutionText,
  set: (v) => emit('update:resolutionText', v),
});
const statTargetModel = computed({
  get: () => props.statTarget,
  set: (v) => emit('update:statTarget', v),
});
const statHealthDeltaModel = computed({
  get: () => props.statHealthDelta,
  set: (v) => emit('update:statHealthDelta', v),
});
const statSetHealthModel = computed({
  get: () => props.statSetHealth,
  set: (v) => emit('update:statSetHealth', v),
});
const statSetArmorModel = computed({
  get: () => props.statSetArmor,
  set: (v) => emit('update:statSetArmor', v),
});
const statGvDeltasModel = computed({
  get: () => props.statGvDeltas,
  set: (v) => emit('update:statGvDeltas', v),
});
const statAttrDeltasModel = computed({
  get: () => props.statAttrDeltas,
  set: (v) => emit('update:statAttrDeltas', v),
});
const statInventoryDeltasModel = computed({
  get: () => props.statInventoryDeltas,
  set: (v) => emit('update:statInventoryDeltas', v),
});
const statStatusChangesModel = computed({
  get: () => props.statStatusChanges,
  set: (v) => emit('update:statStatusChanges', v),
});

const actionDef = computed(() =>
  findRulesetAction(props.rulesetDefinition, props.action.actionKey),
);

const actionDetail = computed(() =>
  actionDef.value && props.rulesetDefinition
    ? describeRulesetAction(actionDef.value, props.rulesetDefinition)
    : null,
);

const rollChainSteps = computed((): RulesetRollChainStepDefinition[] =>
  actionDef.value?.rollChain ?? [],
);

const hasRollChain = computed(() => rollChainSteps.value.length > 0);

const actionPrompts = computed(() =>
  rollPromptsForAction(props.action.id, props.rollPrompts, props.action.followUpRolls ?? []),
);

const rollChainProgress = computed(() =>
  getRollChainProgress(props.rulesetDefinition, props.action, props.rollPrompts),
);

const canPublish = computed(() =>
  canPublishActionResolution(props.rulesetDefinition, props.action, props.rollPrompts),
);

const chainStatusHint = computed(() =>
  rollChainStatusHint(rollChainProgress.value, props.action.actorName),
);

const derivedOutcome = computed(() =>
  evaluateActionOutcomeFromRolls(
    props.rulesetDefinition,
    props.action,
    props.rollPrompts,
    { characters: props.characters, npcsAndMonsters: props.npcs },
  ),
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

const chainTerminatedEarly = computed(() => rollChainProgress.value?.terminatedEarly ?? false);

const skippedStepKeys = computed(() => {
  if (!chainTerminatedEarly.value) return new Set<string>();

  const skipped = new Set<string>();
  let foundFailure = false;
  for (const step of rollChainSteps.value) {
    if (foundFailure) {
      skipped.add(step.step);
      continue;
    }
    const prompt = latestPromptForStep(step.step);
    if (prompt?.autoResolveOutcome === 'failure') {
      foundFailure = true;
    }
  }
  return skipped;
});

const currentChainStepKey = computed(() => {
  if (rollChainProgress.value?.hasPendingPrompt) {
    return rollChainProgress.value.pendingStepKey;
  }

  if (rollChainProgress.value?.needsChainStart) {
    return null;
  }

  if (rollChainProgress.value?.nextManualStepKey) {
    return rollChainProgress.value.nextManualStepKey;
  }

  if (rollChainProgress.value?.isComplete) {
    return null;
  }

  try {
    const json = props.action.rollChainStateJson;
    if (json) {
      const parsed = JSON.parse(json) as { stepIndex?: number };
      const idx = parsed.stepIndex ?? 0;
      return rollChainSteps.value[idx]?.step ?? null;
    }
  } catch {
    // ignore malformed state
  }

  return null;
});

const showStartChain = computed(() =>
  hasRollChain.value
  && Boolean(rollChainProgress.value?.needsChainStart)
  && Boolean(props.action.actorCharacterId),
);

function allowManualPromptForStep(stepKey: string) {
  if (!rollChainProgress.value || rollChainProgress.value.isComplete) return false;
  if (rollChainProgress.value.hasPendingPrompt) return false;
  if (showStartChain.value) return false;
  return currentChainStepKey.value === stepKey;
}

function promptForStep(stepKey: string) {
  const character = props.characters.find(
    c => c.id === props.action.actorCharacterId,
  );
  if (!character || !props.action.actionKey) return;

  if (showStartChain.value && stepKey === rollChainSteps.value[0]?.step) {
    emit('startChain');
    return;
  }

  const step = rollChainSteps.value.find(item => item.step === stepKey);
  if (!step) return;

  emit('sendRollPrompts', {
    prompts: [{
      targetCharacterId: character.id,
      checkMode: step.checkMode ?? 'Action',
      actionKey: step.checkMode === 'Action' ? props.action.actionKey : undefined,
      customCheckText: step.checkMode === 'Custom' ? step.label : undefined,
      promptLabel: step.label,
      guidanceText: step.guidanceText,
      resultKind: step.resultKind ?? 'PassFail',
      chainStepKey: step.step,
    }],
  });
}

const showRejectForm = ref(false);
const rejectReason = ref('');

function submitResolve() {
  if (!canPublish.value) return;
  emit('resolve', {
    resolutionText: resolutionTextModel.value,
    outcome: derivedOutcome.value ?? undefined,
    statTarget: statTargetModel.value,
    statHealthDelta: statHealthDeltaModel.value,
    statSetHealth: statSetHealthModel.value,
    statSetArmor: statSetArmorModel.value,
    statGvDeltas: statGvDeltasModel.value,
    statAttrDeltas: statAttrDeltasModel.value,
    statInventoryDeltas: statInventoryDeltasModel.value,
    statStatusChanges: statStatusChangesModel.value,
  });
}

function submitReject() {
  emit('reject', rejectReason.value.trim());
  rejectReason.value = '';
  showRejectForm.value = false;
}
</script>

<template>
  <div class="action-evaluation-panel">
    <div v-if="actionDetail" class="alert info" style="margin-bottom: 0.75rem;">
      <strong>{{ actionDetail.dice }}</strong>
      <p class="text-sm muted">
        {{ actionDetail.attribute }} + {{ actionDetail.skill }} — {{ actionDetail.successRule }}
      </p>
      <p v-if="action.targetName" class="text-sm" style="margin: 0.35rem 0 0;">
        Target: <strong>{{ action.targetName }}</strong>
      </p>
    </div>

    <div v-if="hasRollChain" class="roll-chain-steps" style="margin-bottom: 0.75rem;">
      <p class="text-sm muted" style="margin: 0 0 0.5rem;">Roll chain</p>

      <div v-if="showStartChain" class="dm-action-roll-primary" style="margin-bottom: 0.75rem;">
        <button type="button" class="btn" :disabled="isBusy || rollChainProgress?.hasPendingPrompt" @click="emit('startChain')">
          Request {{ rollChainSteps[0]?.label ?? 'first roll' }} from {{ action.actorName }}
        </button>
        <p class="text-sm muted" style="margin: 0.35rem 0 0;">
          Sends the first roll prompt to {{ action.actorName }}'s screen.
        </p>
      </div>

      <p v-else-if="chainStatusHint" class="text-sm dm-action-roll-hint" style="margin: 0 0 0.75rem;">
        {{ chainStatusHint }}
      </p>

      <RollChainStepRow
        v-for="step in rollChainSteps"
        :key="step.step"
        :step="step"
        :prompt="latestPromptForStep(step.step)"
        :is-current="step.step === currentChainStepKey"
        :is-completed="isStepCompleted(step.step)"
        :is-skipped="skippedStepKeys.has(step.step)"
        :is-dm-mode="true"
        :actor-name="action.actorName"
        :allow-manual-prompt="allowManualPromptForStep(step.step)"
        :is-busy="isBusy"
        :ruleset-definition="rulesetDefinition"
        @prompt-player="promptForStep"
        @cancel-prompt="id => emit('cancelPrompt', id)"
      />
    </div>

    <DmFollowUpRollPanel
      v-if="!hasRollChain"
      :action="action"
      :characters="characters"
      :roll-prompts="rollPrompts"
      :ruleset-definition="rulesetDefinition"
      :is-busy="isBusy"
      @start-chain="emit('startChain')"
      @send="payload => emit('sendRollPrompts', payload)"
      @cancel="id => emit('cancelPrompt', id)"
      @dm-roll="payload => emit('dmRoll', payload)"
    />

    <p v-if="derivedOutcome" class="roll-outcome-line" style="margin: 0.5rem 0;">
      Roll outcome:
      <span class="badge" :class="derivedOutcome === 'Pass' ? 'pass' : 'fail'">{{ derivedOutcome }}</span>
    </p>

    <label style="margin-top: 0.5rem;">
      Resolution note (optional)
      <input
        v-model="resolutionTextModel"
        type="text"
        placeholder="Narrate what happens…"
        :disabled="isBusy || !canPublish"
      />
    </label>

    <details class="dm-resolve-optional-card" style="margin-top: 0.75rem;">
      <summary>State changes <span class="optional-tag">(optional)</span></summary>
      <div class="dm-resolve-optional-body">
        <DmStatChangePanel
          :characters="characters"
          :npcs="npcs"
          :ruleset-definition="rulesetDefinition"
          :target="statTargetModel"
          :health-delta="statHealthDeltaModel"
          :set-health="statSetHealthModel"
          :set-armor="statSetArmorModel"
          :gv-deltas="statGvDeltasModel"
          :attr-deltas="statAttrDeltasModel"
          :inventory-deltas="statInventoryDeltasModel"
          :status-changes="statStatusChangesModel"
          @update:target="statTargetModel = $event"
          @update:health-delta="statHealthDeltaModel = $event"
          @update:set-health="statSetHealthModel = $event"
          @update:set-armor="statSetArmorModel = $event"
          @update:gv-deltas="statGvDeltasModel = $event"
          @update:attr-deltas="statAttrDeltasModel = $event"
          @update:inventory-deltas="statInventoryDeltasModel = $event"
          @update:status-changes="statStatusChangesModel = $event"
        />
      </div>
    </details>

    <div class="action-eval-footer" style="margin-top: 0.75rem; display: flex; gap: 0.5rem; flex-wrap: wrap; align-items: center;">
      <button
        type="button"
        class="btn"
        :class="{ success: canPublish }"
        :disabled="isBusy || !canPublish"
        @click="submitResolve"
      >
        {{ isBusy ? 'Publishing…' : 'Publish Resolution' }}
      </button>
      <button
        type="button"
        class="btn ghost"
        :disabled="isBusy"
        @click="showRejectForm = !showRejectForm"
      >
        {{ showRejectForm ? 'Cancel reject' : 'Reject' }}
      </button>
    </div>

    <p v-if="!canPublish && chainStatusHint" class="text-sm muted" style="margin: 0.5rem 0 0;">
      {{ chainStatusHint }}
    </p>

    <div v-if="showRejectForm" class="panel nested" style="margin-top: 0.5rem;">
      <label class="text-sm">
        Reason (optional)
        <input v-model.trim="rejectReason" placeholder="Why is this action rejected?" :disabled="isBusy" />
      </label>
      <button
        type="button"
        class="btn danger sm"
        style="margin-top: 0.4rem;"
        :disabled="isBusy"
        @click="submitReject"
      >
        Confirm reject
      </button>
    </div>
  </div>
</template>
