<script setup lang="ts">
interface Props {
  label?: string;
  value: unknown;
}

defineProps<Props>();

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

function isPrimitive(value: unknown) {
  return value === null || typeof value !== 'object';
}

function formatLabel(key: string) {
  return key
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[-_]/g, ' ')
    .replace(/\b\w/g, char => char.toUpperCase());
}

function displayValue(value: unknown) {
  if (value === null || value === undefined || value === '') return '—';
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  return String(value);
}
</script>

<template>
  <span v-if="isPrimitive(value)" class="json-value-leaf">
    {{ displayValue(value) }}
  </span>

  <div v-else-if="Array.isArray(value)" class="json-value-list">
    <p v-if="value.length === 0" class="text-sm muted">None</p>
    <JsonValue
      v-for="(item, index) in value"
      :key="index"
      :label="`${label || 'Item'} ${index + 1}`"
      :value="item"
    />
  </div>

  <dl v-else-if="isRecord(value)" class="json-value-grid">
    <template v-for="(item, key) in value" :key="key">
      <dt>{{ formatLabel(String(key)) }}</dt>
      <dd>
        <JsonValue :label="formatLabel(String(key))" :value="item" />
      </dd>
    </template>
  </dl>
</template>

