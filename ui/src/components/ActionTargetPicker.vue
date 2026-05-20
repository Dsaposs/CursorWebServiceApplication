<script setup lang="ts">
import type { CharacterResponse, NpcResponse } from '~/types/api';
import { useActionTarget } from '~/composables/useActionTarget';

const props = withDefaults(
  defineProps<{
    characters: CharacterResponse[];
    npcs?: NpcResponse[];
    disabled?: boolean;
  }>(),
  { npcs: () => [], disabled: false },
);

const emit = defineEmits<{ change: [isValid: boolean] }>();

const {
  selection,
  otherText,
  sortedCharacters,
  sortedNpcs,
  characterOptionValue,
  npcOptionValue,
  isValid,
  reset,
  toSubmitFields,
} = useActionTarget(() => props.characters, () => props.npcs);

watch(selection, () => emit('change', isValid.value));
watch(otherText, () => emit('change', isValid.value));

defineExpose({ isValid, reset, toSubmitFields });
</script>

<template>
  <div class="action-target-picker">
    <label>
      Target
      <select v-model="selection" :disabled="disabled">
        <option value="">No target</option>
        <optgroup v-if="sortedCharacters.length" label="Characters">
          <option
            v-for="character in sortedCharacters"
            :key="character.id"
            :value="characterOptionValue(character.id)"
          >
            {{ character.name }}<template v-if="character.playerName"> ({{ character.playerName }})</template>
          </option>
        </optgroup>
        <optgroup v-if="sortedNpcs.length" label="NPCs / Monsters">
          <option
            v-for="npc in sortedNpcs"
            :key="npc.id"
            :value="npcOptionValue(npc.id)"
          >
            {{ npc.name }}<template v-if="npc.kind"> ({{ npc.kind }})</template>
          </option>
        </optgroup>
        <option value="__other__">Other…</option>
      </select>
    </label>
    <label v-if="selection === '__other__'">
      Target name
      <input
        v-model.trim="otherText"
        :disabled="disabled"
        placeholder="Door, trap, object…"
        required
      />
    </label>
  </div>
</template>
