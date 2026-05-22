import type { CharacterResponse, NpcResponse } from '~/types/api';
import type { MaybeRefOrGetter } from 'vue';
import { toValue } from 'vue';

export interface ActionTargetSubmitFields {
  targetCharacterId?: string;
  targetNpcId?: string;
  targetName?: string;
}

const CHARACTER_PREFIX = 'character:';
const NPC_PREFIX = 'npc:';

export function useActionTarget(
  characters: MaybeRefOrGetter<CharacterResponse[]>,
  npcs: MaybeRefOrGetter<NpcResponse[]> = () => [],
) {
  const selection = ref('');
  const otherText = ref('');

  const sortedCharacters = computed(() =>
    [...toValue(characters)].sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })),
  );

  const sortedNpcs = computed(() =>
    [...toValue(npcs)].sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })),
  );

  function isValid(options?: { required?: boolean }): boolean {
    if (options?.required && !selection.value) {
      return false;
    }
    if (selection.value === '__other__') {
      return otherText.value.trim().length > 0;
    }
    return true;
  }

  function reset() {
    selection.value = '';
    otherText.value = '';
  }

  function toSubmitFields(): ActionTargetSubmitFields {
    if (!selection.value) {
      return {};
    }
    if (selection.value === '__other__') {
      const name = otherText.value.trim();
      return name ? { targetName: name } : {};
    }
    if (selection.value.startsWith(NPC_PREFIX)) {
      const id = selection.value.slice(NPC_PREFIX.length);
      const npc = sortedNpcs.value.find(n => n.id === id);
      if (!npc) {
        return {};
      }
      return {
        targetNpcId: npc.id,
        targetName: npc.name,
      };
    }
    if (selection.value.startsWith(CHARACTER_PREFIX)) {
      const id = selection.value.slice(CHARACTER_PREFIX.length);
      const character = sortedCharacters.value.find(c => c.id === id);
      if (!character) {
        return {};
      }
      return {
        targetCharacterId: character.id,
        targetName: character.name,
      };
    }
    return {};
  }

  return {
    selection,
    otherText,
    sortedCharacters,
    sortedNpcs,
    characterOptionValue: (id: string) => `${CHARACTER_PREFIX}${id}`,
    npcOptionValue: (id: string) => `${NPC_PREFIX}${id}`,
    isValid,
    reset,
    toSubmitFields,
  };
}
