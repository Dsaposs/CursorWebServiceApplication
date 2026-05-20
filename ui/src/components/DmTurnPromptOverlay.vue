<script setup lang="ts">
interface Props {
  npcName: string;
  isOpen: boolean;
  isBusy?: boolean;
  actionQueued?: boolean;
}

withDefaults(defineProps<Props>(), {
  isBusy: false,
  actionQueued: false,
});
</script>

<template>
  <Teleport to="body">
    <div
      v-if="isOpen"
      class="player-focus-overlay turn-prompt-overlay dm-turn-overlay"
      role="dialog"
      aria-modal="true"
      :aria-label="`${npcName}'s turn`"
    >
      <div class="roll-prompt-card panel turn-prompt-card player-focus-card">
        <div class="roll-prompt-header">
          <span class="roll-prompt-badge combat-turn">NPC turn</span>
          <h2>{{ npcName }}'s turn</h2>
          <p v-if="actionQueued" class="text-sm roll-prompt-sub">
            Action queued. Resolve it from the pending queue — the turn advances when you publish.
          </p>
          <p v-else class="text-sm roll-prompt-sub">
            Queue this NPC's action below. The turn advances when you publish the resolution.
          </p>
        </div>
        <slot />
      </div>
    </div>
  </Teleport>
</template>
