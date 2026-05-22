<script setup lang="ts">
import type { RulesetActionDefinition, RulesetDefinition } from '~/types/api';

interface Props {
  sessionJoinCode: string;
  rulesetDefinition: RulesetDefinition | null;
  sessionMode: 'Exploration' | 'Combat' | 'Downtime';
  diceRollMode: 'App' | 'Manual' | 'Hybrid';
  isMyTurn: boolean;
  /** Current character's class key — used to filter available actions. */
  characterClassKey?: string;
  /** Participant token for the player making the request. */
  playerToken?: string | null;
  /** Pre-fill actor as NPC (DM submitting NPC actions). */
  actorNpcId?: string | null;
}

const props = defineProps<Props>();
const emit = defineEmits<{ submitted: []; cancelled: [] }>();

const { api } = useApi();
const { error: toastError } = useToast();

type Step = 'choose-action' | 'choose-target' | 'add-flavour' | 'choose-roll' | 'review';

const step = ref<Step>('choose-action');
const selectedAction = ref<RulesetActionDefinition | null>(null);
const flavourText = ref('');
const targetCharacterId = ref('');
const targetNpcId = ref('');
const targetName = ref('');
const isSubmitting = ref(false);
const submitError = ref('');

// Dice roll state
const manualRollValue = ref<number | null>(null);
const appRolledDice = ref<number[] | null>(null);
const isRolling = ref(false);
const useManualOverride = ref(false);

const availableActions = computed((): RulesetActionDefinition[] => {
  if (!props.rulesetDefinition) return [];
  const actions = props.rulesetDefinition.actions ?? [];
  return actions.filter(a => {
    if (props.sessionMode === 'Combat' && a.context !== 'combat') return false;
    if (props.characterClassKey && a.allowedClasses?.length) {
      return a.allowedClasses.includes(props.characterClassKey);
    }
    return true;
  });
});

const isActionBuilderOpen = computed(() =>
  props.sessionMode === 'Exploration' ||
  props.sessionMode === 'Downtime' ||
  (props.sessionMode === 'Combat' && props.isMyTurn),
);

const effectiveDiceMode = computed(() => {
  if (props.diceRollMode === 'Hybrid') return useManualOverride.value ? 'Manual' : 'App';
  return props.diceRollMode;
});

const needsRoll = computed(() => !!selectedAction.value?.roll);

function selectAction(action: RulesetActionDefinition) {
  selectedAction.value = action;
  step.value = needsRoll.value ? 'choose-target' : 'add-flavour';
}

function resetToStart() {
  step.value = 'choose-action';
  selectedAction.value = null;
  flavourText.value = '';
  targetCharacterId.value = '';
  targetNpcId.value = '';
  targetName.value = '';
  manualRollValue.value = null;
  appRolledDice.value = null;
  isRolling.value = false;
  submitError.value = '';
}

async function rollAppDice() {
  if (!selectedAction.value?.roll) return;
  isRolling.value = true;
  try {
    const spec = selectedAction.value.roll.dice || '1d20';
    const result = await api<{ spec: string; rolls: number[]; total: number }>('/api/dice/roll', {
      method: 'POST',
      body: { spec },
    });
    appRolledDice.value = result.rolls;
  } catch (err) {
    toastError(extractError(err));
  } finally {
    isRolling.value = false;
  }
}

async function submitAction() {
  if (!selectedAction.value) return;
  isSubmitting.value = true;
  submitError.value = '';

  try {
    const body: Record<string, unknown> = {
      actionKey: selectedAction.value.key,
      actionText: selectedAction.value.label,
      description: flavourText.value || undefined,
      targetCharacterId: targetCharacterId.value || undefined,
      targetNpcId: targetNpcId.value || undefined,
      targetName: targetName.value || undefined,
      actorNpcId: props.actorNpcId || undefined,
    };

    await api(`/api/sessions/${props.sessionJoinCode}/actions`, {
      method: 'POST',
      body,
      playerToken: props.playerToken,
    });

    resetToStart();
    emit('submitted');
  } catch (err) {
    submitError.value = extractError(err);
  } finally {
    isSubmitting.value = false;
  }
}
</script>

<template>
  <div class="action-builder">
    <!-- Locked state in Combat -->
    <div v-if="!isActionBuilderOpen" class="action-builder-locked">
      <p class="text-sm muted">Waiting for your turn…</p>
    </div>

    <!-- Step: Choose action type -->
    <template v-else-if="step === 'choose-action'">
      <h3 class="action-builder-heading">What do you do?</h3>
      <div v-if="!availableActions.length" class="empty-state">
        <p class="text-sm muted">No actions available for this session mode.</p>
      </div>
      <div v-else class="action-grid">
        <button
          v-for="action in availableActions"
          :key="action.key"
          class="action-tile"
          @click="selectAction(action)"
        >
          <span class="action-tile-label">{{ action.label }}</span>
          <span v-if="action.roll" class="action-tile-dice muted text-xs">
            {{ action.roll.dice }}
          </span>
        </button>
      </div>
    </template>

    <!-- Step: Choose target -->
    <template v-else-if="step === 'choose-target'">
      <button class="back-btn" @click="step = 'choose-action'">← Back</button>
      <h3 class="action-builder-heading">Target (optional)</h3>
      <input
        v-model="targetName"
        class="field"
        type="text"
        placeholder="Who or what is the target?"
        maxlength="160"
      />
      <button class="btn-primary mt-2 w-full" @click="step = 'add-flavour'">Continue</button>
    </template>

    <!-- Step: Add flavour -->
    <template v-else-if="step === 'add-flavour'">
      <button class="back-btn" @click="step = selectedAction?.roll ? 'choose-target' : 'choose-action'">← Back</button>
      <h3 class="action-builder-heading">Describe your action <span class="text-xs muted">(optional)</span></h3>
      <textarea
        v-model="flavourText"
        class="field"
        rows="3"
        placeholder="Add narrative colour to your action…"
        maxlength="500"
      />
      <button class="btn-primary mt-2 w-full" @click="step = 'review'">Continue</button>
    </template>

    <!-- Step: Review & Submit -->
    <template v-else-if="step === 'review'">
      <button class="back-btn" @click="step = 'add-flavour'">← Back</button>
      <h3 class="action-builder-heading">Submit action</h3>

      <div class="review-card">
        <div class="review-row">
          <span class="review-label">Action</span>
          <span>{{ selectedAction?.label }}</span>
        </div>
        <div v-if="targetName" class="review-row">
          <span class="review-label">Target</span>
          <span>{{ targetName }}</span>
        </div>
        <div v-if="flavourText" class="review-row">
          <span class="review-label">Description</span>
          <span class="muted text-sm italic">{{ flavourText }}</span>
        </div>
        <div v-if="selectedAction?.roll" class="review-row">
          <span class="review-label">Roll</span>
          <span class="text-xs muted">{{ selectedAction.roll.dice }} — resolved by DM</span>
        </div>
      </div>

      <!-- Dice mode hint for Hybrid -->
      <div v-if="diceRollMode === 'Hybrid'" class="flex items-center gap-2 mt-2 text-sm">
        <input id="manual-override" v-model="useManualOverride" type="checkbox" />
        <label for="manual-override">Use manual dice roll</label>
      </div>

      <div v-if="submitError" class="alert error mt-2">{{ submitError }}</div>

      <div class="flex gap-2 mt-3">
        <button class="btn-ghost" @click="resetToStart(); emit('cancelled')">Cancel</button>
        <button class="btn-primary flex-1" :disabled="isSubmitting" @click="submitAction">
          {{ isSubmitting ? 'Submitting…' : 'Submit Action' }}
        </button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.action-builder {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.action-builder-locked {
  padding: 1rem;
  text-align: center;
  border: 1px dashed var(--border);
  border-radius: 8px;
}

.action-builder-heading {
  font-size: 0.95rem;
  font-weight: 600;
  margin: 0 0 0.5rem;
}

.action-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
  gap: 0.5rem;
}

.action-tile {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  padding: 0.75rem;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface-1);
  cursor: pointer;
  text-align: left;
  transition: border-color 0.15s, background 0.15s;
}

.action-tile:hover {
  border-color: var(--accent);
  background: var(--accent-dim);
}

.action-tile-label {
  font-weight: 500;
  font-size: 0.9rem;
}

.action-tile-dice {
  font-size: 0.75rem;
}

.back-btn {
  font-size: 0.8rem;
  color: var(--muted);
  cursor: pointer;
  background: none;
  border: none;
  padding: 0;
  margin-bottom: 0.25rem;
}

.back-btn:hover {
  color: var(--ink);
}

.review-card {
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 0.75rem;
  background: var(--surface-1);
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.review-row {
  display: flex;
  gap: 0.5rem;
  align-items: flex-start;
}

.review-label {
  font-size: 0.8rem;
  font-weight: 600;
  min-width: 5rem;
  color: var(--muted);
}

.field {
  width: 100%;
}

.mt-2 { margin-top: 0.5rem; }
.mt-3 { margin-top: 0.75rem; }
.flex { display: flex; }
.flex-1 { flex: 1; }
.gap-2 { gap: 0.5rem; }
.w-full { width: 100%; }
.items-center { align-items: center; }
</style>
