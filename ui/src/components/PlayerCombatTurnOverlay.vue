<script setup lang="ts">
interface Props {
  characterName: string;
  isOpen: boolean;
  waitingForDm?: boolean;
}

withDefaults(defineProps<Props>(), {
  waitingForDm: false,
});
</script>

<template>
  <Teleport to="body">
    <div
      v-if="isOpen"
      class="player-focus-overlay turn-prompt-overlay"
      role="dialog"
      aria-modal="true"
      aria-label="Your combat turn"
    >
      <div class="roll-prompt-card panel turn-prompt-card player-focus-card">
        <div class="roll-prompt-header">
          <span class="roll-prompt-badge combat-turn">Combat — your turn</span>
          <h2>It's your turn, {{ characterName }}</h2>
          <p v-if="waitingForDm" class="text-sm roll-prompt-sub">
            Your action was sent. Wait for the DM to call for a roll or advance the round.
          </p>
          <p v-else class="text-sm roll-prompt-sub">
            Submit your action below. You can't act again until the DM advances your turn.
          </p>
        </div>
        <slot />
      </div>
    </div>
  </Teleport>
</template>
