<script setup lang="ts">
import type { ActionQueueItemResponse, ActionRollData } from '~/types/api';

interface Props {
  action: ActionQueueItemResponse;
  gameId: string;
}

const props = defineProps<Props>();
const emit = defineEmits<{ resolved: [updated: ActionQueueItemResponse]; rejected: []; closed: [] }>();

const { api } = useApi();
const { error: toastError, success: toastSuccess } = useToast();

// ── State ─────────────────────────────────────────────────────────────────

const isSaving = ref(false);
const isRejecting = ref(false);
const resolutionText = ref(props.action.resolutionText ?? '');
const rejectionReason = ref('');
const showRejectForm = ref(false);
const dmDiffMod = ref<number>(props.action.dmDifficultyModifier ?? 0);
const effectiveDc = ref<number | null>(props.action.effectiveDc ?? null);
const selectedOutcome = ref<'Pass' | 'Fail' | '' >('');

// Roll data parsed from JSON
const rollData = computed((): ActionRollData | null => {
  if (!props.action.rollDataJson) return null;
  try {
    return JSON.parse(props.action.rollDataJson) as ActionRollData;
  } catch {
    return null;
  }
});

const rollTotal = computed(() => {
  if (!rollData.value) return null;
  return rollData.value.total + dmDiffMod.value;
});

const autoOutcome = computed((): 'Pass' | 'Fail' | null => {
  if (rollTotal.value === null || !effectiveDc.value) return null;
  return rollTotal.value >= effectiveDc.value ? 'Pass' : 'Fail';
});

// Stat changes
interface StatChange { targetType: string; targetId: string; targetName: string; healthDelta?: number; setHealth?: number; }
const statChanges = ref<StatChange[]>([]);

const statusLabels: Record<string, string> = {
  Pending: 'Pending',
  DmReviewing: 'DM Reviewing',
  AwaitingRoll: 'Awaiting Roll',
  RollReceived: 'Roll Received',
  AwaitingReaction: 'Awaiting Reaction',
  ReactionPending: 'Reaction Pending',
  Resolving: 'Resolving',
  AwaitingFollowUpRoll: 'Awaiting Follow-up Roll',
  Published: 'Resolved',
  Rejected: 'Rejected',
};

// ── Actions ───────────────────────────────────────────────────────────────

async function beginReview() {
  if (props.action.status !== 'Pending') return;
  try {
    const updated = await api<ActionQueueItemResponse>(`/api/actions/${props.action.id}/review`, { method: 'PATCH' });
    emit('resolved', updated);
  } catch (err) {
    toastError(extractError(err));
  }
}

async function beginResolve() {
  try {
    const updated = await api<ActionQueueItemResponse>(`/api/actions/${props.action.id}/begin-resolve`, {
      method: 'PATCH',
      body: { difficultyModifier: dmDiffMod.value || undefined, effectiveDc: effectiveDc.value || undefined },
    });
    emit('resolved', updated);
  } catch (err) {
    toastError(extractError(err));
  }
}

async function confirmResolve() {
  isSaving.value = true;
  try {
    const body: Record<string, unknown> = {
      resolutionText: resolutionText.value,
      statChanges: statChanges.value,
    };
    const updated = await api<ActionQueueItemResponse>(`/api/actions/${props.action.id}/resolve`, {
      method: 'PUT',
      body,
    });
    toastSuccess('Action resolved.');
    emit('resolved', updated);
  } catch (err) {
    toastError(extractError(err));
  } finally {
    isSaving.value = false;
  }
}

async function rejectAction() {
  isRejecting.value = true;
  try {
    await api(`/api/actions/${props.action.id}/reject`, {
      method: 'PUT',
      body: { rejectionReason: rejectionReason.value || undefined },
    });
    toastSuccess('Action rejected.');
    emit('rejected');
  } catch (err) {
    toastError(extractError(err));
  } finally {
    isRejecting.value = false;
  }
}

// Auto-select outcome when DC + roll are both available
watch(autoOutcome, (val) => {
  if (val && !selectedOutcome.value) selectedOutcome.value = val;
});

// Watch prop changes to keep local state in sync
watch(() => props.action, (a) => {
  resolutionText.value = a.resolutionText ?? '';
  dmDiffMod.value = a.dmDifficultyModifier ?? 0;
  effectiveDc.value = a.effectiveDc ?? null;
}, { deep: true });
</script>

<template>
  <div class="workspace">
    <!-- Header -->
    <div class="workspace-header">
      <div class="workspace-title">
        <span>Resolving: <strong>{{ action.actorName }}</strong> — {{ action.actionText }}</span>
        <span v-if="action.combatRound" class="badge">Combat Round {{ action.combatRound }}</span>
        <span class="badge muted">{{ statusLabels[action.status] ?? action.status }}</span>
      </div>
      <button class="close-btn" title="Close" @click="emit('closed')">✕</button>
    </div>

    <!-- Begin review prompt if still Pending -->
    <div v-if="action.status === 'Pending'" class="workspace-section">
      <p class="text-sm muted">Open this action in the resolution workspace.</p>
      <button class="btn-primary" @click="beginReview">Open for Review</button>
    </div>

    <template v-else>
      <!-- Action Details + Roll Panel -->
      <div class="workspace-grid">
        <section class="workspace-section">
          <h4>Action</h4>
          <div class="detail-row"><span class="detail-label">Type</span><span>{{ action.actionText }}</span></div>
          <div v-if="action.targetName" class="detail-row"><span class="detail-label">Target</span><span>{{ action.targetName }}</span></div>
          <div v-if="action.actionKey" class="detail-row"><span class="detail-label">Key</span><span class="text-xs muted font-mono">{{ action.actionKey }}</span></div>
          <div v-if="action.description" class="detail-row"><span class="detail-label">Description</span><span class="text-sm italic muted">{{ action.description }}</span></div>
          <div v-if="action.flavourText" class="detail-row"><span class="detail-label">Flavour</span><span class="text-sm italic">{{ action.flavourText }}</span></div>
        </section>

        <section v-if="rollData" class="workspace-section">
          <h4>Roll</h4>
          <div class="detail-row">
            <span class="detail-label">Dice</span>
            <span class="dice-values">{{ rollData.individualRolls.join(', ') }}</span>
          </div>
          <div class="detail-row"><span class="detail-label">Base mod</span><span>+{{ rollData.baseModifier }}</span></div>
          <div v-if="rollData.modifierKeys?.length" class="detail-row">
            <span class="detail-label">Modifiers</span>
            <span class="text-xs">{{ rollData.modifierKeys.join(', ') }}</span>
          </div>
          <div class="detail-row"><span class="detail-label">Subtotal</span><strong>{{ rollData.total }}</strong></div>
          <hr class="divider" />
          <div class="detail-row">
            <span class="detail-label">DC</span>
            <input v-model.number="effectiveDc" class="field-sm" type="number" min="1" max="100" placeholder="—" />
          </div>
          <div class="detail-row">
            <span class="detail-label">Diff mod</span>
            <input v-model.number="dmDiffMod" class="field-sm" type="number" placeholder="0" />
          </div>
          <div v-if="rollTotal !== null" class="detail-row">
            <span class="detail-label">Effective</span>
            <strong :class="autoOutcome === 'Pass' ? 'text-success' : autoOutcome === 'Fail' ? 'text-danger' : ''">
              {{ rollTotal }}
              <template v-if="effectiveDc"> vs {{ effectiveDc }} {{ autoOutcome === 'Pass' ? '✓' : autoOutcome === 'Fail' ? '✗' : '' }}</template>
            </strong>
          </div>
        </section>
      </div>

      <!-- Outcome -->
      <section class="workspace-section">
        <h4>Outcome</h4>
        <div class="outcome-buttons">
          <button
            v-for="opt in ['Pass', 'Fail']"
            :key="opt"
            class="outcome-btn"
            :class="{ active: selectedOutcome === opt, success: opt === 'Pass', danger: opt === 'Fail' }"
            @click="selectedOutcome = (opt as 'Pass' | 'Fail')"
          >
            {{ opt === 'Pass' ? '✓ Success' : '✗ Failure' }}
          </button>
        </div>
      </section>

      <!-- Narrative -->
      <section class="workspace-section">
        <h4>Narrative</h4>
        <textarea
          v-model="resolutionText"
          class="field narrative-field"
          rows="4"
          placeholder="Describe what happens…"
        />
      </section>

      <!-- Move to Resolving if not yet there -->
      <div v-if="['DmReviewing', 'RollReceived'].includes(action.status)" class="workspace-section">
        <button class="btn-secondary w-full" @click="beginResolve">Move to Resolution Phase</button>
      </div>

      <!-- DM Actions -->
      <div class="workspace-actions">
        <template v-if="!showRejectForm">
          <button class="btn-ghost" @click="showRejectForm = true">✗ Reject</button>
          <button
            v-if="['Resolving', 'DmReviewing', 'RollReceived'].includes(action.status)"
            class="btn-primary flex-1"
            :disabled="isSaving"
            @click="confirmResolve"
          >
            {{ isSaving ? 'Saving…' : '✓ Confirm & Post' }}
          </button>
        </template>

        <template v-else>
          <div class="reject-form flex-1">
            <input
              v-model="rejectionReason"
              class="field"
              type="text"
              placeholder="Reason for rejection (optional)"
              maxlength="500"
            />
            <div class="flex gap-2 mt-1">
              <button class="btn-ghost" @click="showRejectForm = false">Cancel</button>
              <button class="btn-danger flex-1" :disabled="isRejecting" @click="rejectAction">
                {{ isRejecting ? 'Rejecting…' : 'Confirm Rejection' }}
              </button>
            </div>
          </div>
        </template>
      </div>
    </template>
  </div>
</template>

<style scoped>
.workspace {
  display: flex;
  flex-direction: column;
  gap: 0;
  background: var(--surface-1);
  border: 1px solid var(--border);
  border-radius: 10px;
  overflow: hidden;
}

.workspace-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.75rem 1rem;
  background: var(--panel);
  border-bottom: 1px solid var(--border);
}

.workspace-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
  font-size: 0.9rem;
}

.close-btn {
  background: none;
  border: none;
  cursor: pointer;
  color: var(--muted);
  font-size: 1rem;
  padding: 0.25rem;
}

.close-btn:hover { color: var(--ink); }

.workspace-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0;
  border-bottom: 1px solid var(--border);
}

.workspace-section {
  padding: 0.75rem 1rem;
  border-bottom: 1px solid var(--border);
}

.workspace-section:last-child { border-bottom: none; }
.workspace-section h4 { font-size: 0.8rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.05em; color: var(--muted); margin: 0 0 0.5rem; }

.detail-row {
  display: flex;
  gap: 0.5rem;
  align-items: baseline;
  margin-bottom: 0.35rem;
  font-size: 0.875rem;
}

.detail-label {
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--muted);
  min-width: 5.5rem;
  flex-shrink: 0;
}

.dice-values {
  font-family: var(--font-mono, monospace);
  font-size: 0.9rem;
  font-weight: 600;
}

.divider {
  border: none;
  border-top: 1px solid var(--border);
  margin: 0.4rem 0;
}

.field-sm {
  width: 5rem;
  font-size: 0.875rem;
}

.outcome-buttons {
  display: flex;
  gap: 0.5rem;
}

.outcome-btn {
  flex: 1;
  padding: 0.5rem;
  border: 2px solid var(--border);
  border-radius: 6px;
  background: var(--surface-1);
  cursor: pointer;
  font-weight: 600;
  font-size: 0.85rem;
  transition: all 0.15s;
}

.outcome-btn.active.success { border-color: var(--success); background: var(--success-dim); color: var(--success); }
.outcome-btn.active.danger { border-color: var(--danger); background: var(--danger-dim); color: var(--danger); }
.outcome-btn:not(.active):hover { border-color: var(--accent); }

.narrative-field { width: 100%; resize: vertical; }

.workspace-actions {
  display: flex;
  gap: 0.5rem;
  padding: 0.75rem 1rem;
  background: var(--panel);
  border-top: 1px solid var(--border);
}

.reject-form { display: flex; flex-direction: column; width: 100%; }

.badge {
  font-size: 0.7rem;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
  background: var(--surface-1);
  border: 1px solid var(--border);
}

.text-success { color: var(--success); }
.text-danger { color: var(--danger); }
.flex { display: flex; }
.flex-1 { flex: 1; }
.gap-2 { gap: 0.5rem; }
.mt-1 { margin-top: 0.25rem; }
.w-full { width: 100%; }
</style>
