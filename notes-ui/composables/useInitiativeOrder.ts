import type { ComputedRef } from 'vue';
import type { InitiativeEntryResponse } from '~/types/api';

interface UseInitiativeOrderOptions {
  canReorder: ComputedRef<boolean>;
  saveOrder: (entries: InitiativeEntryResponse[]) => Promise<void>;
  onSaveError?: (error: unknown) => void;
}

export function reorderEntriesById(
  entries: InitiativeEntryResponse[],
  fromId: string,
  toId: string,
) {
  if (fromId === toId) return entries;

  const ordered = [...entries].sort((a, b) => a.sortOrder - b.sortOrder);
  const fromIndex = ordered.findIndex(entry => entry.id === fromId);
  const toIndex = ordered.findIndex(entry => entry.id === toId);

  if (fromIndex < 0 || toIndex < 0) return entries;

  const [moved] = ordered.splice(fromIndex, 1);
  ordered.splice(toIndex, 0, moved);

  return ordered.map((entry, index) => ({ ...entry, sortOrder: index + 1 }));
}

export function moveEntryByOffset(
  entries: InitiativeEntryResponse[],
  entryId: string,
  offset: -1 | 1,
) {
  const ordered = [...entries].sort((a, b) => a.sortOrder - b.sortOrder);
  const fromIndex = ordered.findIndex(entry => entry.id === entryId);
  const toIndex = fromIndex + offset;

  if (fromIndex < 0 || toIndex < 0 || toIndex >= ordered.length) {
    return ordered;
  }

  const [moved] = ordered.splice(fromIndex, 1);
  ordered.splice(toIndex, 0, moved);

  return ordered.map((entry, index) => ({ ...entry, sortOrder: index + 1 }));
}

export function useInitiativeOrder(
  sourceEntries: ComputedRef<InitiativeEntryResponse[]>,
  options: UseInitiativeOrderOptions,
) {
  const localInitiativeOrder = ref<InitiativeEntryResponse[] | null>(null);
  const draggedInitiativeId = ref<string | null>(null);
  const dragOverId = ref<string | null>(null);
  const dragPosition = ref<{ x: number; y: number } | null>(null);
  const activePointerId = ref<number | null>(null);

  const displayedInitiative = computed(() => localInitiativeOrder.value ?? sourceEntries.value);
  const draggedEntry = computed(() =>
    displayedInitiative.value.find(entry => entry.id === draggedInitiativeId.value) ?? null,
  );

  async function reorderById(fromId: string, toId: string) {
    if (!options.canReorder.value || fromId === toId) return;

    const previousOrder = localInitiativeOrder.value;
    const reordered = reorderEntriesById(displayedInitiative.value, fromId, toId);

    if (reordered === displayedInitiative.value) return;

    localInitiativeOrder.value = reordered;
    try {
      await options.saveOrder(reordered);
      localInitiativeOrder.value = null;
    } catch (error) {
      localInitiativeOrder.value = previousOrder;
      options.onSaveError?.(error);
    }
  }

  async function moveByKeyboard(entryId: string, offset: -1 | 1) {
    if (!options.canReorder.value) return;

    const reordered = moveEntryByOffset(displayedInitiative.value, entryId, offset);
    const currentIndex = displayedInitiative.value.findIndex(entry => entry.id === entryId);
    const nextIndex = reordered.findIndex(entry => entry.id === entryId);

    if (currentIndex === nextIndex) return;

    const previousOrder = localInitiativeOrder.value;
    localInitiativeOrder.value = reordered;
    try {
      await options.saveOrder(reordered);
      localInitiativeOrder.value = null;
    } catch (error) {
      localInitiativeOrder.value = previousOrder;
      options.onSaveError?.(error);
    }
  }

  function startDrag(entryId: string, event: PointerEvent) {
    if (!options.canReorder.value || (event.pointerType === 'mouse' && event.button !== 0)) return;

    event.preventDefault();
    draggedInitiativeId.value = entryId;
    dragOverId.value = entryId;
    activePointerId.value = event.pointerId;
    dragPosition.value = { x: event.clientX, y: event.clientY };

    window.addEventListener('pointermove', onDragMove);
    window.addEventListener('pointerup', onDragEnd);
    window.addEventListener('pointercancel', onDragEnd);
  }

  function onDragMove(event: PointerEvent) {
    if (activePointerId.value !== event.pointerId) return;

    dragPosition.value = { x: event.clientX, y: event.clientY };
    const hit = document
      .elementFromPoint(event.clientX, event.clientY)
      ?.closest<HTMLElement>('[data-initiative-id]')
      ?.dataset.initiativeId;

    if (hit) dragOverId.value = hit;
  }

  async function onDragEnd(event: PointerEvent) {
    if (activePointerId.value !== event.pointerId) return;

    removeDragListeners();
    const fromId = draggedInitiativeId.value;
    const toId = dragOverId.value;
    clearDrag();

    if (fromId && toId && fromId !== toId) {
      await reorderById(fromId, toId);
    }
  }

  function removeDragListeners() {
    window.removeEventListener('pointermove', onDragMove);
    window.removeEventListener('pointerup', onDragEnd);
    window.removeEventListener('pointercancel', onDragEnd);
  }

  function clearDrag() {
    draggedInitiativeId.value = null;
    dragOverId.value = null;
    dragPosition.value = null;
    activePointerId.value = null;
  }

  onBeforeUnmount(removeDragListeners);

  return {
    displayedInitiative,
    draggedEntry,
    draggedInitiativeId,
    dragOverId,
    dragPosition,
    startDrag,
    moveByKeyboard,
  };
}
