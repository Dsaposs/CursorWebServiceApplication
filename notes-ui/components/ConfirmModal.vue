<script setup lang="ts">
interface Props {
  open: boolean;
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  isBusy?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  confirmLabel: 'Confirm',
  cancelLabel: 'Cancel',
  isBusy: false,
});

const emit = defineEmits<{
  cancel: [];
  confirm: [];
  'update:open': [value: boolean];
}>();

const dialogRef = ref<HTMLDialogElement | null>(null);

watch(
  () => props.open,
  open => {
    const dialog = dialogRef.value;
    if (!dialog) return;

    if (open && !dialog.open) {
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  },
);

function cancel() {
  if (props.isBusy) return;
  emit('update:open', false);
  emit('cancel');
}

function confirm() {
  if (props.isBusy) return;
  emit('confirm');
}
</script>

<template>
  <Teleport to="body">
    <dialog ref="dialogRef" class="confirm-modal" @cancel.prevent="cancel" @close="emit('update:open', false)">
      <form class="confirm-modal-card" method="dialog" @submit.prevent="confirm">
        <div>
          <h2>{{ title }}</h2>
          <p>{{ message }}</p>
        </div>
        <div class="btn-row confirm-modal-actions">
          <button class="btn ghost" type="button" :disabled="isBusy" @click="cancel">
            {{ cancelLabel }}
          </button>
          <button class="btn danger" type="submit" :disabled="isBusy">
            {{ isBusy ? 'Working…' : confirmLabel }}
          </button>
        </div>
      </form>
    </dialog>
  </Teleport>
</template>
