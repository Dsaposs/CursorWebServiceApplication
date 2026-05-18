<script setup lang="ts">
import type { NpcResponse } from '~/types/api';

interface Props {
  npcs: NpcResponse[];
  isSaving: boolean;
  editingNpcId: string | null;
  npcName: string;
  npcKind: string;
  npcMaxHealth: number;
  npcHealth: number;
  npcArmor: number;
  npcStatBlockJson: string;
}

defineProps<Props>();

const emit = defineEmits<{
  submit: [];
  edit: [npc: NpcResponse];
  delete: [npc: NpcResponse];
  reset: [];
  'update:npcName': [value: string];
  'update:npcKind': [value: string];
  'update:npcMaxHealth': [value: number];
  'update:npcHealth': [value: number];
  'update:npcArmor': [value: number];
  'update:npcStatBlockJson': [value: string];
}>();

function inputValue(event: Event) {
  return (event.target as HTMLInputElement).value;
}

function inputNumber(event: Event) {
  return Number(inputValue(event));
}
</script>

<template>
  <div class="grid-2">
    <div class="panel">
      <h2>{{ editingNpcId ? 'Edit' : 'Add' }} NPC / Monster</h2>
      <form @submit.prevent="emit('submit')">
        <label>
          Name
          <input :value="npcName" placeholder="Xenomorph, Goblin, Guard…" required @input="emit('update:npcName', inputValue($event))" />
        </label>
        <label>
          Kind
          <input :value="npcKind" placeholder="NPC, Monster, Boss…" required @input="emit('update:npcKind', inputValue($event))" />
        </label>
        <div class="inline-fields">
          <label>
            Max HP
            <input :value="npcMaxHealth" type="number" min="1" required @input="emit('update:npcMaxHealth', inputNumber($event))" />
          </label>
          <label>
            Current HP
            <input :value="npcHealth" type="number" min="0" required @input="emit('update:npcHealth', inputNumber($event))" />
          </label>
          <label>
            Armor
            <input :value="npcArmor" type="number" min="0" required @input="emit('update:npcArmor', inputNumber($event))" />
          </label>
        </div>
        <label>
          Stat block JSON
          <textarea :value="npcStatBlockJson" required @input="emit('update:npcStatBlockJson', inputValue($event))" />
        </label>
        <div class="btn-row">
          <button class="btn" type="submit" :disabled="isSaving">
            {{ isSaving ? 'Saving…' : editingNpcId ? 'Save' : 'Add NPC' }}
          </button>
          <button v-if="editingNpcId" class="btn ghost" type="button" @click="emit('reset')">Cancel</button>
        </div>
      </form>
    </div>

    <div v-if="npcs.length === 0" class="panel">
      <div class="empty-state">
        <div class="empty-state-icon" aria-hidden="true">👾</div>
        <p>No NPCs or monsters yet.</p>
      </div>
    </div>

    <article v-for="npc in npcs" :key="npc.id" class="panel">
      <div class="flex justify-between items-center mb-1" style="margin-bottom: 0.75rem;">
        <div>
          <h3 style="margin: 0;">{{ npc.name }}</h3>
          <p class="text-xs muted" style="margin: 0;">{{ npc.kind }}</p>
        </div>
        <div class="btn-row">
          <button class="btn ghost sm" type="button" @click="emit('edit', npc)">Edit</button>
          <button class="btn danger sm" type="button" aria-label="Delete NPC" @click="emit('delete', npc)">✕</button>
        </div>
      </div>
      <HealthBar :current="npc.health" :max="npc.maxHealth" />
      <details style="margin-top: 0.75rem;">
        <summary style="cursor: pointer; font-size: 0.8rem; color: var(--muted-light);">Stat block</summary>
        <pre style="margin-top: 0.5rem; font-size: 0.72rem;">{{ npc.statBlockJson }}</pre>
      </details>
    </article>
  </div>
</template>
