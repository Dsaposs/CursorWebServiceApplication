<script setup lang="ts">
import type { SessionNoteResponse, GameSessionNotesResponse } from '~/types/api';
const props = defineProps<{
  gameId: string;
}>();

const { api } = useApi();
const { error: toastError } = useToast();

const notes = ref<SessionNoteResponse[]>([]);
const drafts = ref<Record<string, string>>({});
const isLoading = ref(false);
const loadError = ref('');
const savingSessionId = ref<string | null>(null);
const savedSessionIds = ref<Set<string>>(new Set());

function formatSessionWhen(note: SessionNoteResponse) {
  const start = new Date(note.sessionStartedAt).toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
  if (note.sessionIsActive) {
    return `${start} · Active`;
  }
  const end = note.sessionEndedAt
    ? new Date(note.sessionEndedAt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
    : 'Ended';
  return `${start} – ${end}`;
}

async function loadNotes() {
  isLoading.value = true;
  loadError.value = '';
  try {
    const result = await api<GameSessionNotesResponse>(`/api/games/${props.gameId}/session-notes`);
    notes.value = result.notes;
    const nextDrafts: Record<string, string> = {};
    for (const note of result.notes) {
      nextDrafts[note.sessionId] = note.content;
    }
    drafts.value = nextDrafts;
  } catch (err) {
    loadError.value = err instanceof Error ? err.message : String(err);
    notes.value = [];
  } finally {
    isLoading.value = false;
  }
}

async function saveNote(sessionId: string) {
  savingSessionId.value = sessionId;
  try {
    const saved = await api<SessionNoteResponse>(
      `/api/games/${props.gameId}/sessions/${sessionId}/session-notes`,
      { method: 'PUT', body: { content: drafts.value[sessionId] ?? '' } },
    );
    const index = notes.value.findIndex(n => n.sessionId === sessionId);
    if (index >= 0) {
      notes.value[index] = saved;
    }
    savedSessionIds.value.add(sessionId);
    setTimeout(() => savedSessionIds.value.delete(sessionId), 2000);
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    savingSessionId.value = null;
  }
}

const saveTimers: Record<string, ReturnType<typeof setTimeout>> = {};

function onDraftInput(sessionId: string) {
  if (saveTimers[sessionId]) {
    clearTimeout(saveTimers[sessionId]);
  }
  saveTimers[sessionId] = setTimeout(() => { void saveNote(sessionId); }, 800);
}

watch(() => props.gameId, () => { void loadNotes(); }, { immediate: true });
</script>

<template>
  <div class="panel">
    <div class="panel-title">
      <div>
        <h2>Session Notes</h2>
        <p class="text-sm">Your private notes across all sessions for this game. Only you can see these.</p>
      </div>
    </div>

    <div v-if="isLoading" class="empty-state" style="padding: 1rem 0;">
      <p class="text-sm">Loading notes…</p>
    </div>

    <div v-else-if="loadError" class="alert error">{{ loadError }}</div>

    <div v-else-if="notes.length === 0" class="empty-state" style="padding: 1.5rem 0;">
      <p class="text-sm">No session notes yet. Notes you take during a session will appear here.</p>
    </div>

    <div v-else class="session-notes-game-list">
      <article v-for="note in notes" :key="note.id" class="session-notes-game-item">
        <div class="session-notes-game-item-header">
          <h3>{{ formatSessionWhen(note) }}</h3>
          <span v-if="savingSessionId === note.sessionId" class="text-xs" style="color: var(--muted-light);">Saving…</span>
          <span v-else-if="savedSessionIds.has(note.sessionId)" class="text-xs" style="color: var(--success);">Saved</span>
        </div>
        <textarea
          v-model="drafts[note.sessionId]"
          class="session-notes-editor"
          placeholder="Session notes…"
          rows="5"
          @input="onDraftInput(note.sessionId)"
        />
      </article>
    </div>
  </div>
</template>
