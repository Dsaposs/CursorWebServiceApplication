<script setup lang="ts">
import type { ActionQueueItemResponse, CharacterResponse, RollPromptResponse, RulesetDefinition } from '~/types/api';
import { useRulesetActionChooser } from '~/composables/useRulesetActionChooser';
import { rollPromptCheckLabel, toApiCheckMode } from '~/utils/rollPrompt';

interface Props {
  action: ActionQueueItemResponse;
  characters: CharacterResponse[];
  rollPrompts: RollPromptResponse[];
  rulesetDefinition: RulesetDefinition | null;
  isBusy?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  isBusy: false,
});

const emit = defineEmits<{
  send: [payload: {
    prompts: Array<{
      targetCharacterId: string;
      checkMode: string;
      actionKey?: string;
      skillKey?: string;
      attributeKey?: string;
      customCheckText?: string;
      promptLabel?: string;
    }>;
  }];
  cancel: [promptId: string];
}>();

const selectedCharacterIds = ref<string[]>([]);
const promptLabel = ref('');

const promptClassKey = computed(() => {
  const firstId = selectedCharacterIds.value[0];
  return props.characters.find(c => c.id === firstId)?.classKey ?? '';
});

const isChooserEnabled = computed(() => selectedCharacterIds.value.length > 0);

const {
  actionMode,
  selectedActionKey,
  selectedSkillKey,
  selectedAttributeKey,
  customActionText,
  availableActions,
  availableSkills,
  availableAttributes,
  selectedActionDetail,
  selectedSkillDetail,
  selectedAttributeDetail,
} = useRulesetActionChooser(
  computed(() => props.rulesetDefinition),
  promptClassKey,
  isChooserEnabled,
);

const actionPrompts = computed(() =>
  props.rollPrompts.filter(p => p.actionRequestId === props.action.id),
);

function toggleCharacter(characterId: string) {
  const set = new Set(selectedCharacterIds.value);
  if (set.has(characterId)) set.delete(characterId);
  else set.add(characterId);
  selectedCharacterIds.value = [...set];
}

function canSend(): boolean {
  if (!selectedCharacterIds.value.length) return false;
  switch (actionMode.value) {
    case 'action':
      return Boolean(selectedActionKey.value);
    case 'skill':
      return Boolean(selectedSkillKey.value);
    case 'attribute':
      return Boolean(selectedAttributeKey.value);
    case 'custom':
      return Boolean(customActionText.value.trim());
    default:
      return false;
  }
}

function sendPrompts() {
  if (!canSend()) return;

  emit('send', {
    prompts: selectedCharacterIds.value.map(targetCharacterId => ({
      targetCharacterId,
      checkMode: toApiCheckMode(actionMode.value),
      actionKey: actionMode.value === 'action' ? selectedActionKey.value : undefined,
      skillKey: actionMode.value === 'skill' ? selectedSkillKey.value : undefined,
      attributeKey: actionMode.value === 'attribute' ? selectedAttributeKey.value : undefined,
      customCheckText: actionMode.value === 'custom' ? customActionText.value.trim() : undefined,
      promptLabel: promptLabel.value.trim() || undefined,
    })),
  });
}

function promptSummary(prompt: RollPromptResponse) {
  return rollPromptCheckLabel(prompt, props.rulesetDefinition);
}
</script>

<template>
  <div class="follow-up-roll-panel">
    <h3 class="follow-up-roll-title">Request follow-up roll</h3>
    <p class="text-sm follow-up-roll-hint">
      Prompt one or more players to roll before you publish the resolution. You can send multiple prompts.
    </p>

    <div v-if="actionPrompts.length" class="follow-up-roll-list">
      <div
        v-for="prompt in actionPrompts"
        :key="prompt.id"
        class="follow-up-roll-item"
      >
        <div>
          <strong>{{ prompt.targetCharacterName }}</strong>
          <span class="text-sm"> — {{ promptSummary(prompt) }}</span>
        </div>
        <div class="follow-up-roll-item-meta">
          <span class="badge" :class="prompt.status === 'Completed' ? 'active' : 'pending'">
            {{ prompt.status }}
          </span>
          <span v-if="prompt.rollSummary" class="text-sm roll-result">{{ prompt.rollSummary }}</span>
          <button
            v-if="prompt.status === 'Pending'"
            type="button"
            class="btn ghost sm"
            :disabled="isBusy"
            @click="emit('cancel', prompt.id)"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>

    <fieldset class="follow-up-roll-players">
      <legend>Players</legend>
      <div class="follow-up-roll-checkboxes">
        <label
          v-for="character in characters"
          :key="character.id"
          class="follow-up-roll-player"
        >
          <input
            type="checkbox"
            :checked="selectedCharacterIds.includes(character.id)"
            :disabled="isBusy"
            @change="toggleCharacter(character.id)"
          />
          <span>{{ character.name }}</span>
          <span v-if="character.playerName" class="text-sm">({{ character.playerName }})</span>
        </label>
      </div>
    </fieldset>

    <label>
      Short note for players (optional)
      <input v-model.trim="promptLabel" placeholder="e.g. Roll damage, Attack of opportunity…" :disabled="isBusy" />
    </label>

    <label>
      Roll type
      <select v-model="actionMode" :disabled="isBusy || !isChooserEnabled">
        <option v-if="availableActions.length" value="action">Predefined action</option>
        <option v-if="availableSkills.length" value="skill">Skill check</option>
        <option v-if="availableAttributes.length" value="attribute">Attribute check</option>
        <option value="custom">Custom check</option>
      </select>
    </label>

    <label v-if="actionMode === 'action' && availableActions.length">
      Action
      <select v-model="selectedActionKey" :disabled="isBusy">
        <option value="">Choose an action</option>
        <option v-for="actionOption in availableActions" :key="actionOption.key" :value="actionOption.key">
          {{ actionOption.label }}
        </option>
      </select>
    </label>

    <label v-else-if="actionMode === 'skill' && availableSkills.length">
      Skill
      <select v-model="selectedSkillKey" :disabled="isBusy">
        <option value="">Choose a skill</option>
        <option v-for="skill in availableSkills" :key="skill.key" :value="skill.key">
          {{ skill.label }}
        </option>
      </select>
    </label>

    <label v-else-if="actionMode === 'attribute' && availableAttributes.length">
      Attribute
      <select v-model="selectedAttributeKey" :disabled="isBusy">
        <option value="">Choose an attribute</option>
        <option v-for="attribute in availableAttributes" :key="attribute.key" :value="attribute.key">
          {{ attribute.label }}
        </option>
      </select>
    </label>

    <label v-else-if="actionMode === 'custom'">
      Custom check description
      <input v-model.trim="customActionText" placeholder="Damage roll, saving throw…" :disabled="isBusy" />
    </label>

    <div v-if="selectedActionDetail" class="alert info">
      <strong>{{ selectedActionDetail.dice }}</strong>
      <p class="text-sm">{{ selectedActionDetail.successRule }}</p>
    </div>
    <div v-else-if="selectedSkillDetail || selectedAttributeDetail" class="alert info">
      <p class="text-sm">
        {{ selectedSkillDetail?.rollSummary ?? selectedAttributeDetail?.rollSummary }}
      </p>
    </div>

    <button
      type="button"
      class="btn"
      :disabled="isBusy || !canSend()"
      @click="sendPrompts"
    >
      Send roll prompt{{ selectedCharacterIds.length > 1 ? 's' : '' }}
    </button>
  </div>
</template>
