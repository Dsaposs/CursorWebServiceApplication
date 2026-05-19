<script setup lang="ts">
import type { CharacterResponse, RollPromptResponse, RulesetDefinition } from '~/types/api';
import { availableSkillsForClass } from '~/utils/rulesets';
import { rollPromptCheckLabel, toApiCheckMode } from '~/utils/rollPrompt';
import { describeSkillCheck, findRulesetSkill } from '~/utils/rulesets';

interface Props {
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
      skillKey?: string;
      promptLabel?: string;
    }>;
  }];
  cancel: [promptId: string];
}>();

const showForm = ref(false);
const selectedCharacterIds = ref<string[]>([]);
const selectedSkillKey = ref('');
const promptLabel = ref('');

const awaitingPlayerPrompts = computed(() =>
  props.rollPrompts.filter(p => p.status === 'Pending'),
);

const availableSkills = computed(() => {
  if (!props.rulesetDefinition) return [];

  const targets = selectedCharacterIds.value.length
    ? props.characters.filter(c => selectedCharacterIds.value.includes(c.id))
    : [];

  if (!targets.length) return [];

  const seen = new Set<string>();
  const skills = [];

  for (const character of targets) {
    for (const skill of availableSkillsForClass(props.rulesetDefinition, character.classKey)) {
      if (seen.has(skill.key)) continue;
      seen.add(skill.key);
      skills.push(skill);
    }
  }

  return skills.sort((a, b) => a.label.localeCompare(b.label));
});

const selectedSkillDetail = computed(() => {
  const skill = findRulesetSkill(props.rulesetDefinition, selectedSkillKey.value);
  if (!skill || !props.rulesetDefinition) return null;
  return describeSkillCheck(skill, props.rulesetDefinition);
});

const allSelected = computed(() =>
  props.characters.length > 0
  && selectedCharacterIds.value.length === props.characters.length,
);

function toggleCharacter(characterId: string) {
  const set = new Set(selectedCharacterIds.value);
  if (set.has(characterId)) set.delete(characterId);
  else set.add(characterId);
  selectedCharacterIds.value = [...set];
  if (!availableSkills.value.some(s => s.key === selectedSkillKey.value)) {
    selectedSkillKey.value = '';
  }
}

function toggleAllPlayers() {
  if (allSelected.value) {
    selectedCharacterIds.value = [];
  } else {
    selectedCharacterIds.value = props.characters.map(c => c.id);
  }
  selectedSkillKey.value = '';
}

function canSend(): boolean {
  return selectedCharacterIds.value.length > 0 && Boolean(selectedSkillKey.value);
}

function sendPrompts() {
  if (!canSend()) return;

  emit('send', {
    prompts: selectedCharacterIds.value.map(targetCharacterId => ({
      targetCharacterId,
      checkMode: toApiCheckMode('skill'),
      skillKey: selectedSkillKey.value,
      promptLabel: promptLabel.value.trim() || undefined,
    })),
  });

  promptLabel.value = '';
  selectedSkillKey.value = '';
  showForm.value = false;
}

function promptSummary(prompt: RollPromptResponse) {
  return rollPromptCheckLabel(prompt, props.rulesetDefinition);
}

function resetForm() {
  selectedCharacterIds.value = [];
  selectedSkillKey.value = '';
  promptLabel.value = '';
  showForm.value = false;
}
</script>

<template>
  <div class="panel">
    <div class="panel-title">
      <div>
        <h2>Player Skill Checks</h2>
        <p class="text-sm">Prompt players to roll; responses appear in Pending Actions for you to resolve individually.</p>
      </div>
      <button
        v-if="!showForm"
        class="btn"
        type="button"
        :disabled="isBusy || !characters.length"
        @click="showForm = true"
      >
        Request Skill Check
      </button>
    </div>

    <div v-if="awaitingPlayerPrompts.length" class="follow-up-roll-list" style="margin-bottom: 0.75rem;">
      <p class="text-sm" style="margin: 0 0 0.5rem; color: var(--muted-light);">Waiting for rolls</p>
      <div
        v-for="prompt in awaitingPlayerPrompts"
        :key="prompt.id"
        class="follow-up-roll-item"
      >
        <div>
          <strong>{{ prompt.targetCharacterName }}</strong>
          <span class="text-sm"> — {{ promptSummary(prompt) }}</span>
        </div>
        <div class="follow-up-roll-item-meta">
          <span class="badge pending">Awaiting roll</span>
          <button
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

    <form v-if="showForm" @submit.prevent="sendPrompts">
      <fieldset class="follow-up-roll-players">
        <legend>Players</legend>
        <div class="btn-row" style="margin-bottom: 0.5rem;">
          <button type="button" class="btn ghost sm" :disabled="isBusy" @click="toggleAllPlayers">
            {{ allSelected ? 'Clear all' : 'Select all players' }}
          </button>
        </div>
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
        Skill
        <select v-model="selectedSkillKey" required :disabled="isBusy || !selectedCharacterIds.length">
          <option value="">Choose a skill</option>
          <option v-for="skill in availableSkills" :key="skill.key" :value="skill.key">
            {{ skill.label }}
          </option>
        </select>
      </label>

      <div v-if="selectedSkillDetail" class="alert info">
        <p class="text-sm">{{ selectedSkillDetail.rollSummary }}</p>
      </div>

      <label>
        Note for players (optional)
        <input v-model.trim="promptLabel" placeholder="e.g. Perception to spot the ambush…" :disabled="isBusy" />
      </label>

      <div class="btn-row">
        <button class="btn" type="submit" :disabled="isBusy || !canSend()">
          {{ isBusy ? 'Sending…' : `Send to ${selectedCharacterIds.length || 0} player${selectedCharacterIds.length === 1 ? '' : 's'}` }}
        </button>
        <button class="btn ghost" type="button" :disabled="isBusy" @click="resetForm">
          Cancel
        </button>
      </div>
    </form>
  </div>
</template>
