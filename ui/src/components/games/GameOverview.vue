<script setup lang="ts">
import type { GameResponse, SessionSummaryResponse } from '~/types/api';

interface Props {
  game: GameResponse;
  isSaving: boolean;
  inviteUrl: string;
}

defineProps<Props>();

const emit = defineEmits<{
  startSession: [];
  copyInvite: [];
}>();

function sessionBadge(session: SessionSummaryResponse) {
  if (session.isActive) return session.state === 'Combat' ? 'combat' : 'exploration';
  return 'ended';
}

function sessionBadgeText(session: SessionSummaryResponse) {
  if (!session.isActive) return 'Ended';
  return session.state;
}

function sessionRoute(session: SessionSummaryResponse) {
  return session.isActive ? `/sessions/${session.id}/dm` : `/sessions/${session.id}/summary`;
}
</script>

<template>
  <div class="grid-2">
    <div class="panel">
      <h2>Player Invite Link</h2>
      <p>Share this link so players can create or reopen their character before a session.</p>
      <div class="copy-row mt-2">
        <input :value="inviteUrl" readonly />
        <button class="btn ghost sm" type="button" @click="emit('copyInvite')">Copy</button>
      </div>
    </div>

    <div class="panel">
      <div class="panel-title">
        <h2>Sessions</h2>
        <button class="btn sm" type="button" :disabled="isSaving" @click="emit('startSession')">+ Start</button>
      </div>
      <div v-if="game.sessions.length === 0" class="empty-state" style="padding: 1rem 0;">
        <p class="text-sm">No sessions yet.</p>
      </div>
      <ul v-else class="session-list">
        <li v-for="session in game.sessions" :key="session.id">
          <NuxtLink :to="sessionRoute(session)" class="session-list-item">
            <span class="badge" :class="sessionBadge(session)">{{ sessionBadgeText(session) }}</span>
            <span class="flex-1 text-sm" style="color: var(--ink-bright);">
              {{ session.isActive ? 'Live session' : 'Session recap' }}
            </span>
            <span class="text-xs muted font-mono">{{ session.joinCode }}</span>
            <span class="text-xs muted">{{ session.isActive ? '→ DM Screen' : '→ Summary' }}</span>
          </NuxtLink>
        </li>
      </ul>
    </div>
  </div>
</template>
