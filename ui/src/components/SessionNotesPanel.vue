<script setup lang="ts">
import type { SessionNoteResponse, SessionNotesContextResponse } from '~/types/api';

const props = defineProps<{
  mode: 'dm' | 'player';
  sessionId?: string;
  joinCode?: string;
  playerToken?: string | null;
}>();

const { api } = useApi();
const { error: toastError } = useToast();

const context = ref<SessionNotesContextResponse | null>(null);
const draftContent = ref('');
const isLoading = ref(false);
const isSaving = ref(false);
const loadError = ref('');
const lastSavedAt = ref<Date | null>(null);

const canEditCurrent = computed(() => context.value?.currentNote?.canEdit ?? false);

const notesEndpoint = computed(() => {
  if (props.mode === 'dm' && props.sessionId) {
    return `/api/sessions/${props.sessionId}/session-notes`;
  }
  if (props.mode === 'player' && props.joinCode) {
    return `/api/session-join/${props.joinCode}/session-notes`;
  }
  return null;
});

function formatSessionWhen(note: SessionNoteResponse) {
  const start = new Date(note.sessionStartedAt).toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
  if (note.sessionIsActive) {
    return `${start} · Active session`;
  }
  const end = note.sessionEndedAt
    ? new Date(note.sessionEndedAt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
    : 'Ended';
  return `${start} – ${end}`;
}

async function loadNotes() {
  if (!notesEndpoint.value) return;
  isLoading.value = true;
  loadError.value = '';
  try {
    const result = await api<SessionNotesContextResponse>(notesEndpoint.value, {
      playerToken: props.mode === 'player' ? props.playerToken : undefined,
    });
    context.value = result;
    draftContent.value = result.currentNote?.content ?? '';
    lastSavedAt.value = result.currentNote?.updatedAt
      ? new Date(result.currentNote.updatedAt)
      : null;
  } catch (err) {
    loadError.value = err instanceof Error ? err.message : String(err);
    context.value = null;
  } finally {
    isLoading.value = false;
  }
}

async function saveNotes() {
  if (!notesEndpoint.value || !canEditCurrent.value) return;
  isSaving.value = true;
  try {
    const saved = await api<SessionNoteResponse>(notesEndpoint.value, {
      method: 'PUT',
      body: { content: draftContent.value },
      playerToken: props.mode === 'player' ? props.playerToken : undefined,
    });
    lastSavedAt.value = new Date(saved.updatedAt);
    if (context.value) {
      context.value = {
        ...context.value,
        currentNote: saved,
      };
    }
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    toastError(message);
  } finally {
    isSaving.value = false;
  }
}

let saveTimer: ReturnType<typeof setTimeout> | null = null;

watch(draftContent, () => {
  if (!canEditCurrent.value) return;
  if (saveTimer) clearTimeout(saveTimer);
  saveTimer = setTimeout(() => {
    void saveNotes();
  }, 800);
});

onBeforeUnmount(() => {
  if (saveTimer) clearTimeout(saveTimer);
});

watch(
  () => [props.sessionId, props.joinCode, props.playerToken] as const,
  () => { void loadNotes(); },
  { immediate: true },
);
</script>

<template>
  <details class="panel session-notes-panel session-notes-collapsible" open>
    <summary class="session-notes-summary">
      <div class="session-notes-summary-text">
        <h2>Session Notes</h2>
        <p class="text-sm">Private notes for you only. Previous sessions are read-only.</p>
      </div>
      <div class="session-notes-summary-meta">
        <span v-if="isSaving" class="badge" style="background: var(--panel-alt); color: var(--muted-light); border: 1px solid var(--border);">
          Saving…
        </span>
        <span v-else-if="lastSavedAt && canEditCurrent" class="text-xs" style="color: var(--muted-light);">
          Saved {{ lastSavedAt.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' }) }}
        </span>
      </div>
    </summary>

    <div class="session-notes-body">
      <div v-if="isLoading" class="empty-state" style="padding: 1rem 0;">
        <p class="text-sm">Loading notes…</p>
      </div>

      <div v-else-if="loadError && !context" class="alert error">{{ loadError }}</div>

      <template v-else-if="context">
        <section class="session-notes-section">
          <h3 class="session-notes-heading">
            {{ context.isSessionActive ? 'This session' : 'This session (ended)' }}
          </h3>
          <textarea
            v-if="canEditCurrent"
            v-model="draftContent"
            class="session-notes-editor"
            placeholder="Track clues, NPCs, plans, reminders…"
            rows="6"
          />
          <div
            v-else-if="context.currentNote?.content"
            class="session-notes-readonly"
          >
            {{ context.currentNote.content }}
          </div>
          <p v-else class="text-sm" style="color: var(--muted-light); margin: 0;">
            No notes for this session.
          </p>
          <p v-if="!context.isSessionActive && context.currentNote?.content" class="text-xs" style="color: var(--muted-light); margin: 0.5rem 0 0;">
            This session has ended. Notes are stored and cannot be edited here.
          </p>
        </section>

        <section v-if="context.previousNotes.length" class="session-notes-section">
          <h3 class="session-notes-heading">Previous sessions</h3>
          <details
            v-for="note in context.previousNotes"
            :key="note.id"
            class="session-notes-previous"
          >
            <summary>{{ formatSessionWhen(note) }}</summary>
            <div class="session-notes-readonly">
              <p v-if="note.content">{{ note.content }}</p>
              <p v-else class="text-sm" style="color: var(--muted-light); margin: 0;">No notes recorded.</p>
            </div>
          </details>
        </section>
      </template>
    </div>
  </details>
</template>
