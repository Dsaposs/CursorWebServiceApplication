<script setup lang="ts">
definePageMeta({ middleware: 'auth' });

import type {
  CampaignMemberResponse,
  ScheduledSessionResponse,
  CreateScheduledSessionRequest,
} from '~/types/api';

const route = useRoute();
const campaignId = route.params.id as string;
const { getCampaign, addMember, removeMember, addScheduledSession, updateScheduledSession } = useCampaigns();
const { success: toastSuccess, error: toastError } = useToast();

interface CampaignDetail {
  id: string;
  name: string;
  description?: string | null;
  ownerId: string;
  gameId: string;
  gameName: string;
  createdAt: string;
  members: CampaignMemberResponse[];
  upcomingSchedule: ScheduledSessionResponse[];
}

const campaign = ref<CampaignDetail | null>(null);
const isLoading = ref(true);
type ActiveTab = 'schedule' | 'members';
const activeTab = ref<ActiveTab>('schedule');

// Member invite
const inviteEmail = ref('');
const isInviting = ref(false);

// Schedule form
const showScheduleForm = ref(false);
const scheduleForm = ref<CreateScheduledSessionRequest>({
  title: '',
  scheduledAt: '',
  durationMinutes: 120,
  recurrence: 'None',
});
const isScheduling = ref(false);

async function load() {
  isLoading.value = true;
  try {
    campaign.value = await getCampaign(campaignId) as CampaignDetail;
  } catch {
    toastError('Failed to load campaign.');
  } finally {
    isLoading.value = false;
  }
}

async function invite() {
  if (!inviteEmail.value.trim()) return;
  isInviting.value = true;
  try {
    await addMember(campaignId, { userEmail: inviteEmail.value.trim() });
    toastSuccess('Member added!');
    inviteEmail.value = '';
    await load();
  } catch {
    toastError('Could not invite user. Check the email address.');
  } finally {
    isInviting.value = false;
  }
}

async function kickMember(memberId: string) {
  try {
    await removeMember(campaignId, memberId);
    toastSuccess('Member removed.');
    await load();
  } catch {
    toastError('Failed to remove member.');
  }
}

async function submitSchedule() {
  if (!scheduleForm.value.title.trim() || !scheduleForm.value.scheduledAt) return;
  isScheduling.value = true;
  try {
    await addScheduledSession(campaignId, scheduleForm.value);
    toastSuccess('Session scheduled!');
    showScheduleForm.value = false;
    scheduleForm.value = { title: '', scheduledAt: '', durationMinutes: 120, recurrence: 'None' };
    await load();
  } catch {
    toastError('Failed to schedule session.');
  } finally {
    isScheduling.value = false;
  }
}

async function cancelScheduled(scheduleId: string) {
  try {
    await updateScheduledSession(campaignId, scheduleId, { isCancelled: true });
    toastSuccess('Session cancelled.');
    await load();
  } catch {
    toastError('Failed to cancel session.');
  }
}

function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

onMounted(load);
</script>

<template>
  <div class="max-w-4xl mx-auto px-4 py-8">
    <!-- Back -->
    <NuxtLink to="/campaigns" class="text-gray-400 hover:text-white text-sm mb-4 inline-flex items-center gap-1 transition-colors">
      ← Campaigns
    </NuxtLink>

    <div v-if="isLoading" class="text-gray-400 text-sm py-12 text-center">Loading…</div>

    <template v-else-if="campaign">
      <div class="mb-6">
        <h1 class="text-2xl font-bold text-white">{{ campaign.name }}</h1>
        <p v-if="campaign.description" class="text-gray-400 mt-1 text-sm">{{ campaign.description }}</p>
        <p class="text-gray-500 text-xs mt-1">Game: {{ campaign.gameName }}</p>
      </div>

      <!-- Tabs -->
      <div class="flex gap-1 mb-6 border-b border-gray-700">
        <button
          v-for="tab in (['schedule', 'members'] as const)"
          :key="tab"
          :class="[
            'px-4 py-2 text-sm font-medium transition-colors capitalize',
            activeTab === tab
              ? 'text-indigo-400 border-b-2 border-indigo-400'
              : 'text-gray-400 hover:text-white',
          ]"
          @click="activeTab = tab"
        >
          {{ tab }}
          <span v-if="tab === 'members'" class="ml-1 text-xs text-gray-500">({{ campaign.members.length }})</span>
          <span v-if="tab === 'schedule'" class="ml-1 text-xs text-gray-500">({{ campaign.upcomingSchedule.length }})</span>
        </button>
      </div>

      <!-- Schedule tab -->
      <div v-if="activeTab === 'schedule'" class="space-y-4">
        <div class="flex justify-end">
          <button
            class="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-sm font-medium transition-colors"
            @click="showScheduleForm = true"
          >
            + Schedule Session
          </button>
        </div>

        <!-- Schedule form -->
        <div v-if="showScheduleForm" class="bg-gray-800 border border-gray-700 rounded-xl p-4 space-y-3">
          <h3 class="text-white font-semibold text-sm">New Scheduled Session</h3>
          <input
            v-model="scheduleForm.title"
            class="w-full bg-gray-900 border border-gray-600 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            placeholder="Session title"
          />
          <textarea
            v-model="scheduleForm.notes"
            class="w-full bg-gray-900 border border-gray-600 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 resize-none"
            placeholder="Notes (optional)"
            rows="2"
          />
          <div class="flex gap-3">
            <div class="flex-1">
              <label class="block text-gray-400 text-xs mb-1">Date & Time</label>
              <input
                v-model="scheduleForm.scheduledAt"
                type="datetime-local"
                class="w-full bg-gray-900 border border-gray-600 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
            <div class="w-32">
              <label class="block text-gray-400 text-xs mb-1">Duration (min)</label>
              <input
                v-model.number="scheduleForm.durationMinutes"
                type="number"
                min="30"
                max="600"
                class="w-full bg-gray-900 border border-gray-600 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
          </div>
          <div>
            <label class="block text-gray-400 text-xs mb-1">Recurrence</label>
            <select
              v-model="scheduleForm.recurrence"
              class="w-full bg-gray-900 border border-gray-600 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <option value="None">One-time</option>
              <option value="Weekly">Weekly</option>
              <option value="Biweekly">Biweekly</option>
              <option value="Monthly">Monthly</option>
            </select>
          </div>
          <div class="flex gap-2">
            <button
              :disabled="isScheduling"
              class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-lg text-sm font-medium transition-colors"
              @click="submitSchedule"
            >
              Schedule
            </button>
            <button
              class="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg text-sm font-medium transition-colors"
              @click="showScheduleForm = false"
            >
              Cancel
            </button>
          </div>
        </div>

        <!-- Scheduled sessions list -->
        <div v-if="!campaign.upcomingSchedule.length" class="text-gray-500 text-sm text-center py-8">
          No upcoming sessions scheduled.
        </div>
        <div
          v-for="s in campaign.upcomingSchedule"
          :key="s.id"
          :class="[
            'bg-gray-800 border rounded-xl p-4 flex items-start justify-between gap-4',
            s.isCancelled ? 'border-gray-700 opacity-50' : 'border-gray-700',
          ]"
        >
          <div>
            <p class="text-white font-medium">{{ s.title }}</p>
            <p class="text-indigo-300 text-sm mt-0.5">{{ formatDateTime(s.scheduledAt) }}</p>
            <p class="text-gray-500 text-xs mt-1">
              {{ s.durationMinutes }} min
              <span v-if="s.recurrence !== 'None'"> · {{ s.recurrence }}</span>
              <span v-if="s.linkedSessionId" class="text-green-400"> · Live session linked</span>
              <span v-if="s.isCancelled" class="text-red-400"> · Cancelled</span>
            </p>
            <p v-if="s.notes" class="text-gray-400 text-xs mt-1">{{ s.notes }}</p>
          </div>
          <button
            v-if="!s.isCancelled"
            class="text-gray-500 hover:text-red-400 text-xs shrink-0 transition-colors"
            @click="cancelScheduled(s.id)"
          >
            Cancel
          </button>
        </div>
      </div>

      <!-- Members tab -->
      <div v-if="activeTab === 'members'" class="space-y-4">
        <!-- Invite -->
        <div class="flex gap-2">
          <input
            v-model="inviteEmail"
            type="email"
            class="flex-1 bg-gray-900 border border-gray-600 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            placeholder="Invite by email address"
            @keyup.enter="invite"
          />
          <button
            :disabled="isInviting"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-lg text-sm font-medium transition-colors"
            @click="invite"
          >
            Invite
          </button>
        </div>

        <div v-if="!campaign.members.length" class="text-gray-500 text-sm text-center py-8">
          No members yet. Invite players via their email address.
        </div>
        <div
          v-for="m in campaign.members"
          :key="m.id"
          class="bg-gray-800 border border-gray-700 rounded-xl px-4 py-3 flex items-center justify-between"
        >
          <div>
            <p class="text-white text-sm font-medium">{{ m.userName }}</p>
            <p class="text-gray-500 text-xs">{{ m.userEmail }}</p>
          </div>
          <button
            class="text-gray-500 hover:text-red-400 text-xs transition-colors"
            @click="kickMember(m.id)"
          >
            Remove
          </button>
        </div>
      </div>
    </template>
  </div>
</template>
