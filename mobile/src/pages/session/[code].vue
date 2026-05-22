<script setup lang="ts">
import {
  IonPage, IonContent, IonHeader, IonToolbar, IonTitle, IonButtons, IonBackButton,
  IonList, IonItem, IonLabel, IonButton, IonTextarea, IonNote, IonRefresher, IonRefresherContent,
  IonChip,
} from '@ionic/vue';
import { Haptics, ImpactStyle } from '@capacitor/haptics';

const route = useRoute();
const joinCode = route.params.code as string;
const { api } = useApi();
const { connect, disconnect, on, isConnected } = useSessionHub();

interface ActionItem {
  id: string;
  actorName: string;
  actionText: string;
  status: string;
  resolvedText?: string | null;
  createdAt: string;
}

interface SessionState {
  id: string;
  joinCode: string;
  mode: string;
  isActive: boolean;
}

const session = ref<SessionState | null>(null);
const actions = ref<ActionItem[]>([]);
const actionText = ref('');
const flavourText = ref('');
const isSubmitting = ref(false);
const error = ref('');
const showActionForm = ref(false);

const playerToken = import.meta.client ? (localStorage.getItem('ttrpg_player_token') ?? '') : '';

async function loadSession() {
  try {
    const data = await api<{ session: SessionState; actions: ActionItem[] }>(
      `/api/sessions/${joinCode}/player`,
      { method: 'GET' },
    );
    session.value = data.session;
    actions.value = (data.actions ?? []).slice().reverse();
  } catch {
    error.value = 'Could not load session.';
  }
}

async function submitAction() {
  if (!actionText.value.trim()) return;
  isSubmitting.value = true;
  try {
    await api('/api/actions', {
      method: 'POST',
      body: {
        joinCode,
        actionText: actionText.value.trim(),
        flavourText: flavourText.value.trim() || undefined,
        playerToken,
      },
    });
    await Haptics.impact({ style: ImpactStyle.Light });
    actionText.value = '';
    flavourText.value = '';
    showActionForm.value = false;
    await loadSession();
  } catch {
    error.value = 'Failed to submit action.';
  } finally {
    isSubmitting.value = false;
  }
}

async function refresh(e: CustomEvent) {
  await loadSession();
  (e.target as HTMLIonRefresherElement).complete();
}

function statusColor(status: string) {
  const map: Record<string, string> = {
    Pending: 'warning',
    DmReviewing: 'primary',
    AwaitingRoll: 'tertiary',
    RollReceived: 'tertiary',
    Resolving: 'secondary',
    Published: 'success',
    Rejected: 'danger',
    Cancelled: 'medium',
  };
  return map[status] ?? 'medium';
}

onMounted(async () => {
  await loadSession();
  await connect(session.value?.id ?? '', 'player', playerToken);
  on('action.resolved', async () => {
    await Haptics.impact({ style: ImpactStyle.Medium });
    await loadSession();
  });
  on('action.submitted', async () => { await loadSession(); });
});

onUnmounted(() => { disconnect(); });
</script>

<template>
  <IonPage>
    <IonHeader>
      <IonToolbar>
        <IonButtons slot="start">
          <IonBackButton default-href="/home" />
        </IonButtons>
        <IonTitle>Session {{ joinCode }}</IonTitle>
        <IonChip slot="end" :color="isConnected ? 'success' : 'medium'" class="mr-2 text-xs">
          {{ isConnected ? 'Live' : 'Offline' }}
        </IonChip>
      </IonToolbar>
    </IonHeader>

    <IonContent>
      <IonRefresher slot="fixed" @ionRefresh="refresh">
        <IonRefresherContent />
      </IonRefresher>

      <IonNote v-if="error" color="danger" class="ion-padding block text-sm">{{ error }}</IonNote>

      <!-- Action form -->
      <div v-if="showActionForm" class="ion-padding space-y-3 border-b border-gray-700">
        <IonList inset class="rounded-xl overflow-hidden m-0">
          <IonItem>
            <IonLabel position="stacked">Action</IonLabel>
            <IonTextarea
              v-model="actionText"
              placeholder="What do you do?"
              :auto-grow="true"
              :rows="2"
            />
          </IonItem>
          <IonItem>
            <IonLabel position="stacked">Flavour text <span class="text-gray-500 text-xs">(optional)</span></IonLabel>
            <IonTextarea
              v-model="flavourText"
              placeholder="Describe it dramatically…"
              :auto-grow="true"
              :rows="2"
            />
          </IonItem>
        </IonList>
        <div class="flex gap-2">
          <IonButton :disabled="isSubmitting" expand="block" class="flex-1" @click="submitAction">
            {{ isSubmitting ? 'Submitting…' : 'Submit' }}
          </IonButton>
          <IonButton fill="outline" @click="showActionForm = false">Cancel</IonButton>
        </div>
      </div>

      <!-- Action log -->
      <IonList v-if="actions.length">
        <IonItem v-for="a in actions" :key="a.id">
          <IonLabel>
            <h3 class="font-medium">{{ a.actorName }}: {{ a.actionText }}</h3>
            <p v-if="a.resolvedText" class="text-sm text-gray-300 mt-0.5">{{ a.resolvedText }}</p>
            <p class="text-xs text-gray-500 mt-1">
              <IonChip :color="statusColor(a.status)" style="height:1.25rem;font-size:0.7rem;">
                {{ a.status }}
              </IonChip>
            </p>
          </IonLabel>
        </IonItem>
      </IonList>

      <div v-else-if="!error" class="ion-padding text-center text-gray-500 py-12">
        No actions yet. Be the first to act!
      </div>
    </IonContent>

    <!-- FAB-style submit button -->
    <div
      v-if="!showActionForm && session?.isActive"
      class="fixed bottom-6 right-6 z-50"
      style="bottom: calc(1.5rem + var(--ion-safe-area-bottom))"
    >
      <IonButton shape="round" @click="showActionForm = true">
        + Act
      </IonButton>
    </div>
  </IonPage>
</template>
