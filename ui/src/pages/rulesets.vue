<script setup lang="ts">
import type { RulesetImportResponse, RulesetResponse } from '~/types/api';
import { parseRulesetDefinition } from '~/utils/rulesets';

const { api, token, loadSession } = useApi();
const { success: toastSuccess, error: toastError } = useToast();

const rulesets = ref<RulesetResponse[]>([]);
const selectedCode = ref('');
const importJson = ref('');
const importErrors = ref<string[]>([]);
const isLoading = ref(true);
const isImporting = ref(false);

const selectedRuleset = computed(() => rulesets.value.find(ruleset => ruleset.code === selectedCode.value) ?? null);
const selectedDefinition = computed(() => parseRulesetDefinition(selectedRuleset.value));

onMounted(async () => {
  loadSession();
  if (!token.value) { await navigateTo('/login'); return; }
  await loadRulesets();
});

async function loadRulesets() {
  isLoading.value = true;
  try {
    rulesets.value = await api<RulesetResponse[]>('/api/rulesets');
    selectedCode.value ||= rulesets.value[0]?.code ?? '';
  } catch (err) {
    toastError(err instanceof Error ? err.message : String(err));
  } finally {
    isLoading.value = false;
  }
}

async function importRuleset() {
  importErrors.value = [];
  isImporting.value = true;
  try {
    const result = await api<RulesetImportResponse>('/api/rulesets/import', {
      method: 'POST',
      body: { definitionJson: importJson.value },
    });
    toastSuccess(`${result.ruleset.displayName} ${result.created ? 'imported' : 'updated'}.`);
    importJson.value = '';
    await loadRulesets();
    selectedCode.value = result.ruleset.code;
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    importErrors.value = [message];
    toastError(message);
  } finally {
    isImporting.value = false;
  }
}
</script>

<template>
  <section class="app-shell">
    <header class="topbar">
      <div class="topbar-brand">
        <span aria-hidden="true">📚</span>
        <div>
          <strong>Rulesets</strong>
          <div class="topbar-sub">Browse and import global game rules</div>
        </div>
      </div>
      <div class="topbar-actions">
        <NuxtLink class="btn ghost sm" to="/games">Games</NuxtLink>
      </div>
    </header>

    <main class="stack">
      <div v-if="isLoading" class="grid-2">
        <SkeletonBlock :lines="6" />
        <SkeletonBlock :lines="8" />
      </div>

      <template v-else>
        <div class="grid-2">
          <div class="panel">
            <div class="panel-title">
              <div>
                <h1>Available Rulesets</h1>
                <p class="text-sm">Global rulesets available when creating games.</p>
              </div>
            </div>
            <div class="ruleset-card-list">
              <button
                v-for="ruleset in rulesets"
                :key="ruleset.code"
                class="ruleset-card"
                :class="{ active: selectedCode === ruleset.code }"
                type="button"
                @click="selectedCode = ruleset.code"
              >
                <strong>{{ ruleset.displayName }}</strong>
                <span>{{ ruleset.description }}</span>
                <small>{{ ruleset.diceNotation }}</small>
              </button>
            </div>
          </div>

          <div class="panel">
            <template v-if="selectedRuleset && selectedDefinition">
              <h2>{{ selectedRuleset.displayName }}</h2>
              <p>{{ selectedRuleset.description }}</p>
              <div class="ruleset-summary-grid mt-2">
                <div class="stat-cell"><dt>Attributes</dt><dd>{{ selectedDefinition.character.attributes.length }}</dd></div>
                <div class="stat-cell"><dt>Skills</dt><dd>{{ selectedDefinition.character.skills.length }}</dd></div>
                <div class="stat-cell"><dt>Classes</dt><dd>{{ selectedDefinition.character.classes.length }}</dd></div>
                <div class="stat-cell"><dt>Actions</dt><dd>{{ selectedDefinition.actions.length }}</dd></div>
              </div>

              <h3 class="mt-3">Classes</h3>
              <div class="ruleset-chip-list">
                <span v-for="item in selectedDefinition.character.classes" :key="item.key" class="ruleset-chip">{{ item.label }}</span>
              </div>

              <h3 class="mt-3">Actions</h3>
              <div class="ruleset-action-list">
                <article v-for="action in selectedDefinition.actions" :key="action.key" class="ruleset-action-card">
                  <strong>{{ action.label }}</strong>
                  <span>{{ action.description }}</span>
                  <small>{{ action.roll.attribute }} + {{ action.roll.skill }} via {{ action.roll.dice }}</small>
                </article>
              </div>
            </template>
            <div v-else class="alert error">Ruleset definition could not be parsed.</div>
          </div>
        </div>

        <div class="panel">
          <div class="panel-title">
            <div>
              <h2>Import Ruleset JSON</h2>
              <p class="text-sm">Admin-only. Paste a ruleset definition to create or update by code.</p>
            </div>
          </div>
          <form @submit.prevent="importRuleset">
            <label>
              Ruleset JSON
              <textarea v-model="importJson" class="json-import-textarea" placeholder="{ ... }" required />
            </label>
            <div v-if="importErrors.length" class="alert error">
              <ul>
                <li v-for="error in importErrors" :key="error">{{ error }}</li>
              </ul>
            </div>
            <button class="btn" type="submit" :disabled="isImporting">
              {{ isImporting ? 'Importing…' : 'Import Ruleset' }}
            </button>
          </form>
        </div>
      </template>
    </main>
  </section>
</template>
