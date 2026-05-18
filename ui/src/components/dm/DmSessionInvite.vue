<script setup lang="ts">
import type { SessionStateResponse } from '~/types/api';

interface Props {
  state: SessionStateResponse;
  joinLink: string;
  isCombat: boolean;
}

defineProps<Props>();

const emit = defineEmits<{
  copy: [];
}>();
</script>

<template>
  <div class="panel dm-session-info">
    <div class="flex items-center justify-between gap-3" style="flex-wrap: wrap; gap: 1rem;">
      <div>
        <div class="flex items-center gap-2 mb-1">
          <h2 style="margin: 0;">Session Join Link</h2>
        </div>
        <p style="margin: 0; font-size: 0.82rem;">Share with players to join from any device.</p>
      </div>
      <div class="flex items-center gap-2" style="flex: 1; max-width: 36rem;">
        <input
          :value="joinLink"
          readonly
          class="flex-1 font-mono text-sm"
        />
        <button class="btn ghost sm" type="button" @click="emit('copy')">Copy</button>
      </div>
      <div v-if="state.isActive" class="flex items-center gap-2">
        <span class="text-xs muted">Mode</span>
        <span class="badge" :class="isCombat ? 'combat' : 'exploration'">{{ state.state }}</span>
      </div>
    </div>
  </div>
</template>
