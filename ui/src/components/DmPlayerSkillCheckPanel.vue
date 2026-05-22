<script setup lang="ts">
import type { CharacterResponse, RollPromptResponse, RulesetDefinition } from '~/types/api';
import type { StatCheckOption } from '~/composables/useRulesetActionChooser';
import { rollPromptCheckLabel, rollPromptResultKindLabel, normalizeRollResultKind, toApiCheckMode } from '~/utils/rollPrompt';
import type { RollResultKind } from '~/dice-rollers/types';
import { describeAttributeCheck, describeSkillCheck } from '~/utils/rulesets';

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
      attributeKey?: string;
      promptLabel?: string;
      resultKind: string;
    }>;
  }];
  cancel: [promptId: string];
}>();

const showForm = ref(false);
const selectedCharacterIds = ref<string[]>([]);
/** Composite key: 'skill:<key>' or 'attribute:<key>'. */
const selectedStatKey = ref('');
const promptLabel = ref('');
const resultKind = ref<RollResultKind>('PassFail');

const awaitingPlayerPrompts = computed(() =>
  props.rollPrompts.filter(p => p.status === 'Pending'),
);

const availableStatChecks = computed((): StatCheckOption[] => {
  const def = props.rulesetDefinition;
  if (!def) return [];

  const skillOptions: StatCheckOption[] = def.character.skills.map(skill => {
    const detail = describeSkillCheck(skill, def);
    return {
      key: `skill:${skill.key}`,
      label: skill.label,
      type: 'skill',
      rollSummary: detail.rollSummary,
      actionText: detail.actionText,
    };
  });

  const attributeOptions: StatCheckOption[] = def.character.attributes.map(attr => {
    const detail = describeAttributeCheck(attr);
    return {
      key: `attribute:${attr.key}`,
      label: attr.label,
      type: 'attribute',
      rollSummary: detail.rollSummary,
      actionText: detail.actionText,
    };
  });

  return [...skillOptions, ...attributeOptions];
});

const skillStatChecks = computed(() =>
  availableStatChecks.value.filter(stat => stat.type === 'skill'),
);

const attributeStatChecks = computed(() =>
  availableStatChecks.value.filter(stat => stat.type === 'attribute'),
);

const selectedStatDetail = computed(
  () => availableStatChecks.value.find(stat => stat.key === selectedStatKey.value) ?? null,
);

const allSelected = computed(() =>
  props.characters.length > 0
  && selectedCharacterIds.value.length === props.characters.length,
);

function toggleCharacter(characterId: string) {
  const set = new Set(selectedCharacterIds.value);
  if (set.has(characterId)) set.delete(characterId);
  else set.add(characterId);
  selectedCharacterIds.value = [...set];
}

function toggleAllPlayers() {
  if (allSelected.value) {
    selectedCharacterIds.value = [];
  } else {
    selectedCharacterIds.value = props.characters.map(c => c.id);
  }
}

function canSend(): boolean {
  return selectedCharacterIds.value.length > 0 && Boolean(selectedStatKey.value);
}

function buildPromptFields(stat: StatCheckOption) {
  if (stat.type === 'skill') {
    const skillKey = stat.key.slice('skill:'.length);
    return {
      checkMode: toApiCheckMode('skill'),
      skillKey,
    };
  }

  const attributeKey = stat.key.slice('attribute:'.length);
  return {
    checkMode: toApiCheckMode('attribute'),
    attributeKey,
  };
}

function sendPrompts() {
  const stat = selectedStatDetail.value;
  if (!stat || !canSend()) return;

  const fields = buildPromptFields(stat);

  emit('send', {
    prompts: selectedCharacterIds.value.map(targetCharacterId => ({
      targetCharacterId,
      ...fields,
      promptLabel: promptLabel.value.trim() || undefined,
      resultKind: resultKind.value,
    })),
  });

  promptLabel.value = '';
  selectedStatKey.value = '';
  resultKind.value = 'PassFail';
  showForm.value = false;
}

function promptSummary(prompt: RollPromptResponse) {
  return rollPromptCheckLabel(prompt, props.rulesetDefinition);
}

function resetForm() {
  selectedCharacterIds.value = [];
  selectedStatKey.value = '';
  promptLabel.value = '';
  showForm.value = false;
}
</script>

<template>
  <div class="panel">
    <div class="panel-title">
      <div>
        <h2>Player Stat Checks</h2>
        <p class="text-sm">Prompt players to roll a skill or attribute; responses appear in Pending Actions for you to resolve individually.</p>
      </div>
      <button
        v-if="!showForm"
        class="btn"
        type="button"
        :disabled="isBusy || !characters.length || !availableStatChecks.length"
        @click="showForm = true"
      >
        Request Stat Check
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
          <span class="badge" style="margin-left: 0.25rem;">
            {{ rollPromptResultKindLabel(normalizeRollResultKind(prompt.resultKind)) }}
          </span>
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
        Result type
        <select v-model="resultKind" :disabled="isBusy">
          <option value="PassFail">Pass / fail (successes or vs DC)</option>
          <option value="Total">Dice total (sum values, e.g. damage)</option>
        </select>
      </label>

      <label>
        Stat
        <select v-model="selectedStatKey" required :disabled="isBusy || !selectedCharacterIds.length">
          <option value="">Choose a stat</option>
          <optgroup v-if="skillStatChecks.length" label="Skills">
            <option
              v-for="stat in skillStatChecks"
              :key="stat.key"
              :value="stat.key"
            >
              {{ stat.label }}
            </option>
          </optgroup>
          <optgroup v-if="attributeStatChecks.length" label="Attributes">
            <option
              v-for="stat in attributeStatChecks"
              :key="stat.key"
              :value="stat.key"
            >
              {{ stat.label }}
            </option>
          </optgroup>
        </select>
      </label>

      <div v-if="selectedStatDetail" class="alert info">
        <strong>{{ selectedStatDetail.actionText }}</strong>
        <p class="text-sm muted">{{ selectedStatDetail.rollSummary }}</p>
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
