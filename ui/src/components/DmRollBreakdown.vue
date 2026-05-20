<script setup lang="ts">
import type { RollPromptResponse } from '~/types/api';
import { formatAutoResolveLabel, parseRollResultJson } from '~/utils/rollResult';

const props = defineProps<{ prompt: RollPromptResponse }>();

const structured = computed(() => parseRollResultJson(props.prompt.rollResultJson));
const autoLabel = computed(() => formatAutoResolveLabel(props.prompt.autoResolveOutcome));
</script>

<template>
  <div v-if="structured?.groups?.length || prompt.autoResolveMessage" class="dm-roll-breakdown">
    <div v-if="prompt.autoResolveMessage || autoLabel" class="dm-roll-auto-resolve">
      <span
        v-if="autoLabel"
        class="badge"
        :class="prompt.autoResolveOutcome === 'success' ? 'pass' : prompt.autoResolveOutcome === 'failure' ? 'fail' : 'pending'"
      >
        {{ autoLabel }}
      </span>
      <span v-if="prompt.autoResolveMessage" class="text-sm">{{ prompt.autoResolveMessage }}</span>
    </div>

    <div v-if="structured?.groups?.length" class="dm-roll-dice-grid">
      <div
        v-for="(group, idx) in structured.groups"
        :key="idx"
        class="dm-roll-die-group"
        :class="{ stress: group.isStress }"
      >
        <span class="dm-roll-die-label">{{ group.label || group.notation }}</span>
        <div class="dm-roll-die-values">
          <span v-for="(value, vi) in group.values" :key="vi" class="dm-roll-die-face">{{ value }}</span>
        </div>
      </div>
    </div>

    <p v-if="structured?.successes !== undefined && structured.resultKind === 'PassFail'" class="text-sm dm-roll-primary">
      <strong>{{ structured.successes }}</strong> success{{ structured.successes !== 1 ? 'es' : '' }}
    </p>
    <p v-else-if="structured?.total !== undefined" class="text-sm dm-roll-primary">
      Total: <strong>{{ structured.total }}</strong>
    </p>

    <p v-if="structured?.pushed" class="text-sm muted">Player pushed the roll (+{{ structured.stressGained ?? 1 }} stress).</p>
  </div>
</template>
