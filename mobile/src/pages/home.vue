<script setup lang="ts">
import {
  IonPage, IonContent, IonHeader, IonToolbar, IonTitle,
  IonList, IonItem, IonLabel, IonButton, IonRefresher, IonRefresherContent,
  IonChip, IonBadge,
} from '@ionic/vue';

interface GameSummary {
  id: string;
  name: string;
  rulesetName: string;
  description?: string | null;
  activeSession?: { id: string; joinCode: string } | null;
  sessionCount: number;
}

const { api, clearSession } = useApi();
const games = ref<GameSummary[]>([]);
const isLoading = ref(true);

async function load() {
  isLoading.value = true;
  try {
    games.value = await api<GameSummary[]>('/api/games');
  } catch {
    // Non-critical: leave list empty
  } finally {
    isLoading.value = false;
  }
}

async function refresh(e: CustomEvent) {
  await load();
  (e.target as HTMLIonRefresherElement).complete();
}

function signOut() {
  clearSession();
  navigateTo('/login', { replace: true });
}

onMounted(() => {
  load();
});
</script>

<template>
  <IonPage>
    <IonHeader>
      <IonToolbar>
        <IonTitle>My Games</IonTitle>
        <IonButton slot="end" fill="clear" size="small" @click="signOut">Sign Out</IonButton>
      </IonToolbar>
    </IonHeader>

    <IonContent>
      <IonRefresher slot="fixed" @ionRefresh="refresh">
        <IonRefresherContent />
      </IonRefresher>

      <div v-if="isLoading" class="ion-padding text-center text-gray-400">Loading…</div>

      <div v-else-if="!games.length" class="ion-padding text-center py-16">
        <p class="text-4xl mb-3">🎲</p>
        <p class="text-white font-semibold">No games yet</p>
        <p class="text-gray-400 text-sm mt-1">Create a game from the web app, then come back here to play.</p>
      </div>

      <IonList v-else>
        <IonItem
          v-for="g in games"
          :key="g.id"
          :router-link="`/games/${g.id}`"
          detail
        >
          <IonLabel>
            <h2 class="font-semibold">{{ g.name }}</h2>
            <p class="text-sm text-gray-400">{{ g.rulesetName }}</p>
          </IonLabel>
          <IonChip v-if="g.activeSession" color="success" slot="end">Live</IonChip>
          <IonBadge v-else slot="end" color="medium">{{ g.sessionCount }}</IonBadge>
        </IonItem>
      </IonList>

      <!-- Quick join by code -->
      <div class="ion-padding mt-4">
        <IonButton expand="block" fill="outline" router-link="/join">
          Join by Code
        </IonButton>
      </div>
    </IonContent>
  </IonPage>
</template>
