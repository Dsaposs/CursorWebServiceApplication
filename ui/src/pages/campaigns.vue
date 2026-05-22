<script setup lang="ts">
definePageMeta({ middleware: 'auth' });

import type { CampaignResponse, GameResponse, CreateCampaignRequest } from '~/types/api';

const { api } = useApi();
const { listCampaigns, createCampaign, deleteCampaign } = useCampaigns();
const { success: toastSuccess, error: toastError } = useToast();

const campaigns = ref<CampaignResponse[]>([]);
const games = ref<GameResponse[]>([]);
const isLoading = ref(true);
const showCreateForm = ref(false);
const deleteConfirmId = ref<string | null>(null);

const form = ref<CreateCampaignRequest>({ name: '', description: '', gameId: '' });
const isSubmitting = ref(false);

async function loadData() {
  isLoading.value = true;
  try {
    [campaigns.value, games.value] = await Promise.all([
      listCampaigns(),
      api<GameResponse[]>('/api/games'),
    ]);
  } catch {
    toastError('Failed to load campaigns.');
  } finally {
    isLoading.value = false;
  }
}

async function submitCreate() {
  if (!form.value.name.trim() || !form.value.gameId) return;
  isSubmitting.value = true;
  try {
    await createCampaign(form.value);
    toastSuccess('Campaign created!');
    showCreateForm.value = false;
    form.value = { name: '', description: '', gameId: '' };
    await loadData();
  } catch {
    toastError('Failed to create campaign.');
  } finally {
    isSubmitting.value = false;
  }
}

async function confirmDelete(id: string) {
  try {
    await deleteCampaign(id);
    toastSuccess('Campaign deleted.');
    campaigns.value = campaigns.value.filter(c => c.id !== id);
  } catch {
    toastError('Failed to delete campaign.');
  } finally {
    deleteConfirmId.value = null;
  }
}

onMounted(loadData);
</script>

<template>
  <div class="max-w-5xl mx-auto px-4 py-8">
    <div class="flex items-center justify-between mb-6">
      <h1 class="text-2xl font-bold text-white">Campaigns</h1>
      <button
        class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-sm font-medium transition-colors"
        @click="showCreateForm = true"
      >
        + New Campaign
      </button>
    </div>

    <!-- Create form -->
    <div v-if="showCreateForm" class="mb-6 bg-gray-800 border border-gray-700 rounded-xl p-5 space-y-4">
      <h2 class="text-lg font-semibold text-white">New Campaign</h2>
      <input
        v-model="form.name"
        class="w-full bg-gray-900 border border-gray-600 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        placeholder="Campaign name"
        maxlength="120"
      />
      <textarea
        v-model="form.description"
        class="w-full bg-gray-900 border border-gray-600 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 resize-none"
        placeholder="Description (optional)"
        rows="2"
        maxlength="1000"
      />
      <select
        v-model="form.gameId"
        class="w-full bg-gray-900 border border-gray-600 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
      >
        <option value="">Select a game…</option>
        <option v-for="g in games" :key="g.id" :value="g.id">{{ g.name }}</option>
      </select>
      <div class="flex gap-2">
        <button
          :disabled="isSubmitting"
          class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-lg text-sm font-medium transition-colors"
          @click="submitCreate"
        >
          Create
        </button>
        <button
          class="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg text-sm font-medium transition-colors"
          @click="showCreateForm = false"
        >
          Cancel
        </button>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="isLoading" class="text-gray-400 text-sm py-8 text-center">Loading campaigns…</div>

    <!-- Empty state -->
    <div
      v-else-if="!campaigns.length"
      class="text-center py-16 text-gray-500"
    >
      <p class="text-lg">No campaigns yet.</p>
      <p class="text-sm mt-1">Create one to start scheduling sessions with your group.</p>
    </div>

    <!-- Campaign cards -->
    <div v-else class="grid gap-4 sm:grid-cols-2">
      <div
        v-for="c in campaigns"
        :key="c.id"
        class="bg-gray-800 border border-gray-700 rounded-xl p-5 flex flex-col gap-3 hover:border-indigo-500 transition-colors"
      >
        <div class="flex items-start justify-between gap-2">
          <NuxtLink
            :to="`/campaigns/${c.id}`"
            class="text-white font-semibold text-lg hover:text-indigo-400 transition-colors"
          >
            {{ c.name }}
          </NuxtLink>
          <button
            class="text-gray-500 hover:text-red-400 text-xs transition-colors shrink-0"
            @click="deleteConfirmId = c.id"
          >
            Delete
          </button>
        </div>
        <p v-if="c.description" class="text-gray-400 text-sm line-clamp-2">{{ c.description }}</p>
        <div class="flex gap-4 text-xs text-gray-500 mt-auto">
          <span>{{ c.gameName }}</span>
          <span>{{ c.memberCount }} member{{ c.memberCount !== 1 ? 's' : '' }}</span>
          <span>{{ c.scheduledSessionCount }} upcoming</span>
        </div>
      </div>
    </div>

    <!-- Delete confirm -->
    <div
      v-if="deleteConfirmId"
      class="fixed inset-0 bg-black/60 flex items-center justify-center z-50"
      @click.self="deleteConfirmId = null"
    >
      <div class="bg-gray-800 border border-gray-700 rounded-xl p-6 max-w-sm w-full mx-4 space-y-4">
        <p class="text-white font-semibold">Delete this campaign?</p>
        <p class="text-gray-400 text-sm">This will remove all scheduled sessions. This cannot be undone.</p>
        <div class="flex gap-2">
          <button
            class="px-4 py-2 bg-red-600 hover:bg-red-500 text-white rounded-lg text-sm font-medium transition-colors"
            @click="confirmDelete(deleteConfirmId!)"
          >
            Delete
          </button>
          <button
            class="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg text-sm font-medium transition-colors"
            @click="deleteConfirmId = null"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
