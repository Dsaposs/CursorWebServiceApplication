<script setup lang="ts">
const { toasts, remove } = useToast();

const icons: Record<string, string> = {
  success: '✓',
  error: '✕',
  info: 'i',
  warn: '!',
};
</script>

<template>
  <Teleport to="body">
    <div class="toast-container">
      <TransitionGroup name="toast">
        <div
          v-for="toast in toasts"
          :key="toast.id"
          class="toast"
          :class="toast.type"
        >
          <span class="toast-icon font-bold" aria-hidden="true">{{ icons[toast.type] }}</span>
          <span class="toast-message">{{ toast.message }}</span>
          <button class="toast-close" type="button" aria-label="Dismiss" @click="remove(toast.id)">×</button>
        </div>
      </TransitionGroup>
    </div>
  </Teleport>
</template>

<style scoped>
.toast-enter-active { animation: toast-in 0.2s ease; }
.toast-leave-active { animation: toast-in 0.15s ease reverse; }

.toast-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.35rem;
  height: 1.35rem;
  border-radius: 50%;
  font-size: 0.7rem;
  flex-shrink: 0;
  background: currentColor;
  color: var(--surface);
}
</style>
