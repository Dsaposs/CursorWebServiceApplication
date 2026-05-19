<script setup lang="ts">
import type {
  NpcResponse,
  RulesetAttributeDefinition,
  RulesetDefinition,
  RulesetSkillDefinition,
} from '~/types/api';
import { parseNpcInventory, type InventoryEntry } from '~/utils/inventory';

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
  npc?: NpcResponse | null;
  isBusy?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  npc: null,
  isBusy: false,
});

const emit = defineEmits<{
  created: [npc: NpcResponse];
  updated: [npc: NpcResponse];
  cancel: [];
}>();

const { api } = useApi();
const { success: toastSuccess, error: toastError } = useToast();

const isSubmitting = ref(false);
const localName = ref('');
const localKind = ref('NPC');
const localMaxHealth = ref(10);
const localHealth = ref(10);
const localArmor = ref(0);
const localAttrs = ref<Record<string, number>>({});
const localSkills = ref<Record<string, number>>({});
const localInventory = ref<InventoryEntry[]>([]);

const isEditMode = computed(() => Boolean(props.npc));

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
  localInventory.value = [];
}

function populateFromNpc(npc: NpcResponse) {
  localName.value = npc.name;
  localKind.value = npc.kind || 'NPC';
  localMaxHealth.value = npc.maxHealth;
  localHealth.value = npc.health;
  localArmor.value = npc.armor;
  try {
    const block = JSON.parse(npc.statBlockJson) as {
      attributes?: Record<string, number>;
      skills?: Record<string, number>;
    };
    localAttrs.value = { ...defaultAttrs(), ...(block.attributes ?? {}) };
    localSkills.value = { ...defaultSkills(), ...(block.skills ?? {}) };
    localInventory.value = parseNpcInventory(npc.statBlockJson).map(entry => ({ ...entry }));
  } catch {
    localAttrs.value = defaultAttrs();
    localSkills.value = defaultSkills();
    localInventory.value = [];
  }
}

watch(
  () => props.npc,
  (npc) => {
    if (npc) populateFromNpc(npc);
    else resetForm();
  },
  { immediate: true },
);

watch(
  () => props.rulesetDefinition,
  () => {
    if (!props.npc) {
      localAttrs.value = defaultAttrs();
      localSkills.value = defaultSkills();
    }
  },
);

function buildStatBlockJson(): string {
  const hasAttrs = attributes.value.length > 0;
  const hasSkills = skills.value.length > 0;
  const hasInventory = localInventory.value.length > 0;
  if (!hasAttrs && !hasSkills && !hasInventory) return '{}';
  const block: Record<string, unknown> = {};
  if (hasAttrs) block.attributes = { ...localAttrs.value };
  if (hasSkills) block.skills = { ...localSkills.value };
  if (hasInventory) {
    block.inventory = localInventory.value.map(entry => ({
      itemKey: entry.itemKey,
      quantity: entry.quantity,
    }));
  }
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

const localInventoryJson = computed(() =>
  JSON.stringify(localInventory.value.map(entry => ({
    itemKey: entry.itemKey,
    quantity: entry.quantity,
  }))),
);

function onInventorySave(inventory: InventoryEntry[]) {
  localInventory.value = inventory.map(entry => ({ ...entry }));
}

const formBusy = computed(() => props.isBusy || isSubmitting.value);

async function submit() {
  if (!localName.value.trim()) {
    toastError('Name is required.');
    return;
  }

  isSubmitting.value = true;
  try {
    const payload = buildPayload();
    if (isEditMode.value && props.npc) {
      const npc = await api<NpcResponse>(`/api/games/${props.gameId}/npcs/${props.npc.id}`, {
        method: 'PUT',
        body: payload,
      });
      toastSuccess(`${npc.name} updated.`);
      emit('updated', npc);
    } else {
      const npc = await api<NpcResponse>(`/api/games/${props.gameId}/npcs`, {
        method: 'POST',
        body: payload,
      });
      toastSuccess(`${npc.name} added.`);
      emit('created', npc);
      resetForm();
    }
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isSubmitting.value = false;
  }
}

function cancel() {
  if (isEditMode.value) {
    emit('cancel');
    return;
  }
  resetForm();
  emit('cancel');
}
</script>

<template>
  <form class="dm-npc-form" @submit.prevent="submit">
    <p v-if="isEditMode" class="text-sm muted" style="margin: 0 0 0.75rem;">
      Editing <strong>{{ npc?.name }}</strong>
    </p>

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

    <InventoryEditor
      v-if="rulesetDefinition"
      embedded
      :inventory-json="localInventoryJson"
      :ruleset-definition="rulesetDefinition"
      style="margin-top: 1rem;"
      @save="onInventorySave"
    />

    <div class="btn-row" style="margin-top: 0.75rem;">
      <button class="btn" type="submit" :disabled="formBusy">
        {{ formBusy ? 'Saving…' : isEditMode ? 'Save Changes' : 'Create NPC' }}
      </button>
      <button class="btn ghost" type="button" :disabled="formBusy" @click="cancel">
        Cancel
      </button>
    </div>
  </form>
</template>

<style scoped>
.dm-npc-form {
  margin-bottom: 1rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid var(--border);
}
</style>
