<script setup lang="ts">
import type { RulesetDefinition } from '~/types/api';
import { findRulesetItem } from '~/utils/rulesets';

interface Props {
  definition: RulesetDefinition | null;
  classKey: string;
  skillAllocations: Record<string, number>;
  startingItemKey: string;
}

const props = defineProps<Props>();

const emit = defineEmits<{
  'update:classKey': [value: string];
  'update:skillAllocations': [value: Record<string, number>];
  'update:startingItemKey': [value: string];
}>();

const selectedClass = computed(() =>
  props.definition?.character.classes.find(c => c.key === props.classKey) ?? null,
);

const classSkills = computed(() => {
  const cls = selectedClass.value;
  if (!cls || !props.definition) return [];
  const allowed = new Set(cls.availableSkills);
  return props.definition.character.skills.filter(skill => allowed.has(skill.key));
});

const skillBudget = computed(() => selectedClass.value?.startingSkillPoints ?? 0);
const maxSkillRank = computed(() => selectedClass.value?.maxSkillRank ?? null);

const spentSkillPoints = computed(() =>
  Object.values(props.skillAllocations).reduce((sum, value) => sum + (value || 0), 0),
);

const remainingSkillPoints = computed(() => skillBudget.value - spentSkillPoints.value);

const startingItemOptions = computed(() => {
  const keys = selectedClass.value?.startingItemOptions ?? [];
  return keys
    .map(key => findRulesetItem(props.definition, key))
    .filter((item): item is NonNullable<typeof item> => Boolean(item));
});

watch(
  () => props.classKey,
  () => {
    resetSkillsForClass();
    resetStartingItem();
  },
);

function resetSkillsForClass() {
  const next: Record<string, number> = {};
  for (const skill of classSkills.value) {
    next[skill.key] = 0;
  }
  emit('update:skillAllocations', next);
}

function resetStartingItem() {
  const first = startingItemOptions.value[0]?.key ?? '';
  emit('update:startingItemKey', first);
}

function stepSkill(skillKey: string, delta: number) {
  const current = props.skillAllocations[skillKey] ?? 0;
  const max = maxSkillRank.value ?? skillBudget.value;
  const nextValue = current + delta;
  if (nextValue < 0) return;
  if (maxSkillRank.value !== null && maxSkillRank.value !== undefined && nextValue > maxSkillRank.value) return;
  if (delta > 0 && remainingSkillPoints.value <= 0) return;

  emit('update:skillAllocations', {
    ...props.skillAllocations,
    [skillKey]: nextValue,
  });
}

function onClassChange(event: Event) {
  emit('update:classKey', (event.target as HTMLSelectElement).value);
}

function onStartingItemChange(event: Event) {
  emit('update:startingItemKey', (event.target as HTMLSelectElement).value);
}

onMounted(() => {
  if (!Object.keys(props.skillAllocations).length) {
    resetSkillsForClass();
  }
  if (!props.startingItemKey && startingItemOptions.value.length) {
    resetStartingItem();
  }
});
</script>

<template>
  <div v-if="definition && selectedClass" class="character-creation-setup">
    <label v-if="definition.character.classes.length">
      Class / Career
      <select :value="classKey" required @change="onClassChange">
        <option v-for="characterClass in definition.character.classes" :key="characterClass.key" :value="characterClass.key">
          {{ characterClass.label }}
        </option>
      </select>
      <p v-if="selectedClass.description" class="text-sm muted">{{ selectedClass.description }}</p>
    </label>

    <div v-if="skillBudget > 0 && classSkills.length" class="stat-delta-group">
      <div class="flex justify-between items-center" style="margin-bottom: 0.5rem;">
        <span class="stat-delta-group-label">Skill points</span>
        <span class="text-sm muted">
          {{ spentSkillPoints }} / {{ skillBudget }} spent
        </span>
      </div>
      <p class="text-xs muted" style="margin: 0 0 0.75rem;">
        <template v-if="maxSkillRank">Assign {{ skillBudget }} points across your class skills (max {{ maxSkillRank }} each).</template>
        <template v-else>Assign {{ skillBudget }} points across your class skills.</template>
      </p>
      <div
        v-for="skill in classSkills"
        :key="skill.key"
        class="stat-delta-row"
      >
        <span class="stat-delta-name">{{ skill.label }}</span>
        <span class="stat-delta-current">{{ skillAllocations[skill.key] ?? 0 }}</span>
        <div class="roll-adj-stepper">
          <button type="button" class="adj-btn" @click="stepSkill(skill.key, -1)">−</button>
          <span class="adj-input delta-input" style="text-align: center;">{{ skillAllocations[skill.key] ?? 0 }}</span>
          <button type="button" class="adj-btn" @click="stepSkill(skill.key, 1)">+</button>
        </div>
      </div>
    </div>

    <label v-if="startingItemOptions.length">
      Starting equipment
      <select :value="startingItemKey" required @change="onStartingItemChange">
        <option v-for="item in startingItemOptions" :key="item.key" :value="item.key">
          {{ item.label }}
        </option>
      </select>
      <p v-if="findRulesetItem(definition, startingItemKey)?.description" class="text-sm muted">
        {{ findRulesetItem(definition, startingItemKey)?.description }}
      </p>
    </label>
  </div>
</template>
