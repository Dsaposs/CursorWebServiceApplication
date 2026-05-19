<script setup lang="ts">
import type {
  NpcResponse,
  RulesetAttributeDefinition,
  RulesetDefinition,
  RulesetSkillDefinition,
} from '~/types/api';

export interface NpcFormPayload {
  name: string;
  kind: string;
  maxHealth: number;
  health: number;
  armor: number;
  statBlockJson: string;
}

interface Props {
  gameId: string;
  rulesetDefinition: RulesetDefinition | null;
  isBusy?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  isBusy: false,
});

const emit = defineEmits<{
  created: [npc: NpcResponse];
}>();

const { api } = useApi();
const { success: toastSuccess, error: toastError } = useToast();

const showForm = ref(false);
const isSubmitting = ref(false);
const localName = ref('');
const localKind = ref('NPC');
const localMaxHealth = ref(10);
const localHealth = ref(10);
const localArmor = ref(0);
const localAttrs = ref<Record<string, number>>({});
const localSkills = ref<Record<string, number>>({});

const attributes = computed<RulesetAttributeDefinition[]>(
  () => props.rulesetDefinition?.character.attributes ?? [],
);
const skills = computed<RulesetSkillDefinition[]>(
  () => props.rulesetDefinition?.character.skills ?? [],
);

function defaultAttrs(): Record<string, number> {
  return Object.fromEntries(attributes.value.map(a => [a.key, a.default ?? 0]));
}

function defaultSkills(): Record<string, number> {
  return Object.fromEntries(skills.value.map(s => [s.key, s.default ?? 0]));
}

function resetForm() {
  localName.value = '';
  localKind.value = 'NPC';
  localMaxHealth.value = 10;
  localHealth.value = 10;
  localArmor.value = 0;
  localAttrs.value = defaultAttrs();
  localSkills.value = defaultSkills();
}

watch(
  () => props.rulesetDefinition,
  () => {
    localAttrs.value = defaultAttrs();
    localSkills.value = defaultSkills();
  },
  { immediate: true },
);

function buildStatBlockJson(): string {
  const hasAttrs = attributes.value.length > 0;
  const hasSkills = skills.value.length > 0;
  if (!hasAttrs && !hasSkills) return '{}';
  const block: Record<string, unknown> = {};
  if (hasAttrs) block.attributes = { ...localAttrs.value };
  if (hasSkills) block.skills = { ...localSkills.value };
  return JSON.stringify(block);
}

function buildPayload(): NpcFormPayload {
  return {
    name: localName.value.trim(),
    kind: localKind.value,
    maxHealth: localMaxHealth.value,
    health: localHealth.value,
    armor: localArmor.value,
    statBlockJson: buildStatBlockJson(),
  };
}

async function submit() {
  if (!localName.value.trim()) {
    toastError('Name is required.');
    return;
  }

  isSubmitting.value = true;
  try {
    const npc = await api<NpcResponse>(`/api/games/${props.gameId}/npcs`, {
      method: 'POST',
      body: buildPayload(),
    });
    toastSuccess(`${npc.name} added.`);
    emit('created', npc);
    resetForm();
    showForm.value = false;
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSubmitting.value = false;
  }
}

const formBusy = computed(() => props.isBusy || isSubmitting.value);

function cancel() {
  resetForm();
  showForm.value = false;
}
</script>

<template>
  <div class="panel">
    <div class="panel-title">
      <div>
        <h2>Add NPC / Monster</h2>
        <p class="text-sm">Create a new foe or ally during the session.</p>
      </div>
      <button
        v-if="!showForm"
        class="btn"
        type="button"
        :disabled="formBusy"
        @click="showForm = true"
      >
        Add NPC
      </button>
    </div>

    <form v-if="showForm" @submit.prevent="submit">
      <label>
        Name
        <input v-model.trim="localName" placeholder="Xenomorph, Goblin, Guard…" required :disabled="formBusy" />
      </label>

      <label>
        Type
        <select v-model="localKind" :disabled="formBusy">
          <option value="NPC">NPC</option>
          <option value="Monster">Monster</option>
        </select>
      </label>

      <div class="inline-fields">
        <label>
          Max HP
          <input v-model.number="localMaxHealth" type="number" min="1" required :disabled="formBusy" />
        </label>
        <label>
          Current HP
          <input v-model.number="localHealth" type="number" min="0" required :disabled="formBusy" />
        </label>
        <label>
          Armor
          <input v-model.number="localArmor" type="number" min="0" :disabled="formBusy" />
        </label>
      </div>

      <template v-if="attributes.length">
        <p class="text-xs muted" style="margin: 0.75rem 0 0.25rem;">Attributes</p>
        <div class="inline-fields">
          <label v-for="attr in attributes" :key="attr.key">
            {{ attr.label }}
            <input
              v-model.number="localAttrs[attr.key]"
              type="number"
              :min="attr.min ?? 0"
              :max="attr.max ?? undefined"
              :disabled="formBusy"
            />
          </label>
        </div>
      </template>

      <template v-if="skills.length">
        <p class="text-xs muted" style="margin: 0.75rem 0 0.25rem;">Skills</p>
        <div class="inline-fields">
          <label v-for="skill in skills" :key="skill.key">
            {{ skill.label }}
            <input v-model.number="localSkills[skill.key]" type="number" min="0" :disabled="formBusy" />
          </label>
        </div>
      </template>

      <div class="btn-row" style="margin-top: 0.75rem;">
        <button class="btn" type="submit" :disabled="formBusy">
          {{ formBusy ? 'Saving…' : 'Create NPC' }}
        </button>
        <button class="btn ghost" type="button" :disabled="formBusy" @click="cancel">
          Cancel
        </button>
      </div>
    </form>
  </div>
</template>
