<script setup lang="ts">
import {
  IonPage, IonContent, IonHeader, IonToolbar, IonTitle,
  IonList, IonItem, IonLabel, IonInput, IonButton, IonNote, IonBackButton, IonButtons,
} from '@ionic/vue';

const { api } = useApi();

const code = ref('');
const displayName = ref('');
const isLoading = ref(false);
const error = ref('');

const defaultCharacterBuild = {
  classKey: 'scientist',
  skillAllocations: {
    observation: 2,
    survival: 2,
    comtech: 3,
    medicalAid: 3,
  },
  startingItemKey: 'medkit',
};

async function join() {
  error.value = '';
  const trimmedCode = code.value.trim().toLowerCase();
  const trimmedName = displayName.value.trim();

  if (!trimmedCode) { error.value = 'Enter a session code.'; return; }
  if (!trimmedName) { error.value = 'Enter a display name.'; return; }

  isLoading.value = true;
  try {
    const res = await api<{ participantToken: string; character: { id: string }; game: { id: string } }>(
      `/api/session-join/${trimmedCode}`,
      {
        method: 'POST',
        body: {
          characterName: trimmedName,
          playerName: trimmedName,
          ...defaultCharacterBuild,
        },
      },
    );
    if (import.meta.client) {
      localStorage.setItem('ttrpg_player_token', res.participantToken);
      localStorage.setItem('ttrpg_participant_id', res.character.id);
    }
    await navigateTo(`/session/${trimmedCode}`);
  } catch {
    error.value = 'Session not found or already ended.';
  } finally {
    isLoading.value = false;
  }
}
</script>

<template>
  <IonPage>
    <IonHeader>
      <IonToolbar>
        <IonButtons slot="start">
          <IonBackButton default-href="/home" />
        </IonButtons>
        <IonTitle>Join Session</IonTitle>
      </IonToolbar>
    </IonHeader>

    <IonContent class="ion-padding">
      <div class="flex flex-col gap-5 pt-4 max-w-sm mx-auto">
        <p class="text-gray-400 text-sm">Enter the code your DM shared to join the session.</p>

        <IonList inset class="rounded-xl overflow-hidden">
          <IonItem>
            <IonLabel position="stacked">Session Code</IonLabel>
            <IonInput
              v-model="code"
              autocapitalize="characters"
              placeholder="ABC123"
              maxlength="12"
              @keyup.enter="join"
            />
          </IonItem>
          <IonItem>
            <IonLabel position="stacked">Your Name</IonLabel>
            <IonInput
              v-model="displayName"
              placeholder="Adventurer"
              maxlength="60"
              @keyup.enter="join"
            />
          </IonItem>
        </IonList>

        <IonNote v-if="error" color="danger" class="text-center text-sm">{{ error }}</IonNote>

        <IonButton expand="block" :disabled="isLoading" @click="join">
          {{ isLoading ? 'Joining…' : 'Join Session' }}
        </IonButton>
      </div>
    </IonContent>
  </IonPage>
</template>
