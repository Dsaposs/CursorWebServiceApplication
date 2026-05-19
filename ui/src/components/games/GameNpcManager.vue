<script setup lang="ts">
import type { NpcResponse, RulesetAttributeDefinition, RulesetDefinition, RulesetSkillDefinition } from '~/types/api';
import { parseNpcInventory, type InventoryEntry } from '~/utils/inventory';
import { npcAttributeValue, npcHasStructuredStats, npcSkillValue } from '~/utils/npcStats';

export interface NpcFormPayload {
  name: string;
  kind: string;
  maxHealth: number;
  health: number;
  armor: number;
  statBlockJson: string;
}

interface Props {
  npcs: NpcResponse[];
  isSaving: boolean;
  editingNpcId: string | null;
  definition: RulesetDefinition | null;
}

const props = defineProps<Props>();

const emit = defineEmits<{
  submit: [payload: NpcFormPayload];
  edit: [npc: NpcResponse];
  delete: [npc: NpcResponse];
  reset: [];
}>();

// ── Internal form state ───────────────────────────────────────
const localName = ref('');
const localKind = ref('NPC');
const localMaxHealth = ref(10);
const localHealth = ref(10);
const localArmor = ref(0);
const localAttrs = ref<Record<string, number>>({});
const localSkills = ref<Record<string, number>>({});
const localInventory = ref<InventoryEntry[]>([]);

const attributes = computed<RulesetAttributeDefinition[]>(() => props.definition?.character.attributes ?? []);
const skills = computed<RulesetSkillDefinition[]>(() => props.definition?.character.skills ?? []);

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

// Populate or reset the form whenever the editing target changes.
watch(
  [() => props.editingNpcId, () => props.npcs],
  () => {
    if (!props.editingNpcId) {
      resetForm();
      return;
    }
    const npc = props.npcs.find(n => n.id === props.editingNpcId);
    if (npc) populateFromNpc(npc);
  },
  { immediate: true },
);

// Re-seed defaults when the definition loads or switches (only when not editing).
watch(
  () => props.definition,
  () => {
    if (!props.editingNpcId) {
      localAttrs.value = defaultAttrs();
      localSkills.value = defaultSkills();
    }
  },
);

// ── Submit ────────────────────────────────────────────────────
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

function onInventorySave(inventory: InventoryEntry[]) {
  localInventory.value = inventory.map(entry => ({ ...entry }));
}

const localInventoryJson = computed(() =>
  JSON.stringify(localInventory.value.map(entry => ({
    itemKey: entry.itemKey,
    quantity: entry.quantity,
  }))),
);

function handleSubmit() {
  emit('submit', {
    name: localName.value.trim(),
    kind: localKind.value,
    maxHealth: localMaxHealth.value,
    health: localHealth.value,
    armor: localArmor.value,
    statBlockJson: buildStatBlockJson(),
  });
}

function npcInventoryJson(npc: NpcResponse): string {
  return JSON.stringify(parseNpcInventory(npc.statBlockJson));
}
</script>

<template>
  <div class="npc-manager-layout">
    <!-- ── Left: Form panel ───────────────────────────────────── -->
    <div class="panel npc-form-panel">
      <h2>{{ editingNpcId ? 'Edit' : 'Add' }} NPC / Monster</h2>
      <form @submit.prevent="handleSubmit">
        <label>
          Name
          <input v-model.trim="localName" placeholder="Xenomorph, Goblin, Guard…" required />
        </label>

        <label>
          Type
          <select v-model="localKind">
            <option value="NPC">NPC</option>
            <option value="Monster">Monster</option>
          </select>
        </label>

        <div class="inline-fields">
          <label>
            Max HP
            <input v-model.number="localMaxHealth" type="number" min="1" required />
          </label>
          <label>
            Current HP
            <input v-model.number="localHealth" type="number" min="0" required />
          </label>
          <label>
            Armor
            <input v-model.number="localArmor" type="number" min="0" />
          </label>
        </div>

        <!-- Dynamic attributes from ruleset -->
        <template v-if="attributes.length">
          <p class="text-xs muted" style="margin: 0.75rem 0 0.25rem;">Attributes</p>
          <div class="inline-fields">
            <label v-for="attr in attributes" :key="attr.key">
              {{ attr.label }}
              <input
                v-model.number="localAttrs[attr.key]"
                type="number"
                :min="attr.min ?? 1"
                :max="attr.max ?? undefined"
              />
            </label>
          </div>
        </template>

        <!-- Dynamic skills from ruleset -->
        <template v-if="skills.length">
          <p class="text-xs muted" style="margin: 0.75rem 0 0.25rem;">Skills</p>
          <div class="inline-fields">
            <label v-for="skill in skills" :key="skill.key">
              {{ skill.label }}
              <input
                v-model.number="localSkills[skill.key]"
                type="number"
                min="0"
              />
            </label>
          </div>
        </template>

        <InventoryEditor
          v-if="definition"
          embedded
          :inventory-json="localInventoryJson"
          :ruleset-definition="definition"
          style="margin-top: 1rem;"
          @save="onInventorySave"
        />

        <div class="btn-row" style="margin-top: 1rem;">
          <button class="btn" type="submit" :disabled="isSaving">
            {{ isSaving ? 'Saving…' : editingNpcId ? 'Save Changes' : 'Add NPC' }}
          </button>
          <button v-if="editingNpcId" class="btn ghost" type="button" @click="emit('reset')">
            Cancel
          </button>
        </div>
      </form>
    </div>

    <!-- ── Right: NPC list (sticky) ──────────────────────────── -->
    <div class="npc-list-column">
      <!-- Empty state -->
      <div v-if="npcs.length === 0" class="panel">
        <div class="empty-state">
          <div class="empty-state-icon" aria-hidden="true">👾</div>
          <p>No NPCs or monsters yet.</p>
        </div>
      </div>

      <!-- NPC cards -->
      <details
        v-for="npc in npcs"
        :key="npc.id"
        class="panel npc-card"
        :class="{ 'panel-editing': npc.id === editingNpcId }"
      >
        <summary class="npc-card-summary">
          <span class="npc-card-identity">
            <span class="npc-card-name">{{ npc.name }}</span>
            <span class="npc-stat-bar">
              <span class="stat-chip">HP {{ npc.health }}/{{ npc.maxHealth }}</span>
              <span class="stat-chip">AC {{ npc.armor }}</span>
            </span>
          </span>
          <span class="btn-row" @click.stop>
            <button class="btn ghost sm" type="button" @click="emit('edit', npc)">Edit</button>
            <button class="btn danger sm" type="button" aria-label="Delete NPC" @click="emit('delete', npc)">✕</button>
          </span>
        </summary>

        <div class="npc-card-body">
          <HealthBar :current="npc.health" :max="npc.maxHealth" />

          <template v-if="npcHasStructuredStats(npc.statBlockJson) && (attributes.length || skills.length)">
            <div v-if="attributes.length" class="npc-stats-section">
              <p class="text-xs muted" style="margin-bottom: 0.35rem;">Attributes</p>
              <div class="stat-grid">
                <div v-for="attr in attributes" :key="attr.key" class="stat-cell">
                  <dt>{{ attr.label }}</dt>
                  <dd>{{ npcAttributeValue(npc.statBlockJson, attr.key) ?? '–' }}</dd>
                </div>
              </div>
            </div>
            <div v-if="skills.length" class="npc-stats-section">
              <p class="text-xs muted" style="margin-bottom: 0.35rem;">Skills</p>
              <div class="stat-grid">
                <div v-for="skill in skills" :key="skill.key" class="stat-cell">
                  <dt>{{ skill.label }}</dt>
                  <dd>{{ npcSkillValue(npc.statBlockJson, skill.key) ?? '–' }}</dd>
                </div>
              </div>
            </div>
          </template>

          <section v-if="definition" class="npc-stats-section">
            <p class="text-xs muted" style="margin-bottom: 0.35rem;">Inventory</p>
            <CharacterInventoryList
              :inventory-json="npcInventoryJson(npc)"
              :ruleset-definition="definition"
            />
          </section>

          <p
            v-if="!npcHasStructuredStats(npc.statBlockJson) && !parseNpcInventory(npc.statBlockJson).length"
            class="text-sm muted"
            style="margin-top: 0.5rem;"
          >
            No stats recorded.
          </p>
        </div>
      </details>
    </div>
  </div>
</template>

<style scoped>
/* ── Two-column layout ──────────────────────────── */
.npc-manager-layout {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.5rem;
  align-items: start;
}

.npc-list-column {
  position: sticky;
  top: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  max-height: calc(100vh - 10rem);
  overflow-y: auto;
  overflow-x: visible;
  padding-right: 2px;
}

.panel-editing {
  outline: 2px solid var(--accent);
}

/* ── Collapsible NPC card ───────────────────────── */
.npc-card {
  overflow: visible;
}

.npc-card-summary {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 0.75rem 0.85rem;
  cursor: pointer;
  list-style: none;
  user-select: none;
}

/* Hide the default disclosure triangle in all browsers */
.npc-card-summary::-webkit-details-marker { display: none; }
.npc-card-summary::marker { display: none; }

.npc-card-identity {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 0;
}

.npc-card-name {
  font-weight: 600;
  font-size: 0.95rem;
  color: var(--ink-bright);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.npc-stat-bar {
  display: flex;
  gap: 0.4rem;
  flex-wrap: wrap;
}

.stat-chip {
  font-size: 0.7rem;
  font-family: 'Menlo', 'Consolas', monospace;
  color: var(--muted-light);
  background: var(--panel-hover);
  border-radius: var(--radius-sm);
  padding: 0.1rem 0.4rem;
}

.npc-card-body {
  padding: 0 0.85rem 0.85rem;
  border-top: 1px solid var(--border);
}

.npc-stats-section {
  margin-top: 0.75rem;
}
</style>
