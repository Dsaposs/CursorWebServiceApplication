<script setup lang="ts">
import {
  IonPage, IonContent, IonHeader, IonToolbar, IonTitle,
  IonList, IonItem, IonLabel, IonInput, IonButton,
  IonNote,
} from '@ionic/vue';

const { api, setSession } = useApi();

const email = ref('');
const password = ref('');
const isLoading = ref(false);
const error = ref('');

async function login() {
  error.value = '';
  if (!email.value || !password.value) {
    error.value = 'Email and password are required.';
    return;
  }

  isLoading.value = true;
  try {
    const res = await api<{ token: string; expiresAt: string; refreshToken: string }>(
      '/api/auth/login',
      { method: 'POST', body: { email: email.value, password: password.value } },
    );
    setSession(res.token, email.value, res.refreshToken);
    await navigateTo('/home', { replace: true });
  } catch {
    error.value = 'Invalid email or password.';
  } finally {
    isLoading.value = false;
  }
}
</script>

<template>
  <IonPage>
    <IonHeader>
      <IonToolbar>
        <IonTitle>TTRPG Table</IonTitle>
      </IonToolbar>
    </IonHeader>

    <IonContent class="ion-padding">
      <div class="flex flex-col gap-6 pt-8 max-w-sm mx-auto">
        <div class="text-center">
          <p class="text-4xl mb-2">🎲</p>
          <h1 class="text-xl font-bold text-white">Sign in</h1>
          <p class="text-gray-400 text-sm mt-1">Connect to your TTRPG session</p>
        </div>

        <IonList inset class="rounded-xl overflow-hidden">
          <IonItem>
            <IonLabel position="stacked">Email</IonLabel>
            <IonInput
              v-model="email"
              type="email"
              autocomplete="email"
              inputmode="email"
              placeholder="you@example.com"
              @keyup.enter="login"
            />
          </IonItem>
          <IonItem>
            <IonLabel position="stacked">Password</IonLabel>
            <IonInput
              v-model="password"
              type="password"
              placeholder="••••••••"
              @keyup.enter="login"
            />
          </IonItem>
        </IonList>

        <IonNote v-if="error" color="danger" class="text-center text-sm">
          {{ error }}
        </IonNote>

        <IonButton expand="block" :disabled="isLoading" @click="login">
          {{ isLoading ? 'Signing in…' : 'Sign In' }}
        </IonButton>
      </div>
    </IonContent>
  </IonPage>
</template>
