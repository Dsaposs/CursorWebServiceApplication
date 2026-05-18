<script setup lang="ts">
import type { AuthResponse } from '~/types/api';

const { api, setSession, loadSession, token } = useApi();
const { error: toastError, success: toastSuccess } = useToast();

const mode = ref<'login' | 'register'>('login');
const email = ref('');
const password = ref('');
const confirmPassword = ref('');
const fieldError = ref('');
const isSubmitting = ref(false);
const isLogin = computed(() => mode.value === 'login');

onMounted(() => {
  loadSession();
  if (token.value) void navigateTo('/games');
});

function switchMode(next: 'login' | 'register') {
  mode.value = next;
  fieldError.value = '';
}

async function submit() {
  fieldError.value = '';
  isSubmitting.value = true;

  try {
    if (!isLogin.value) {
      if (password.value !== confirmPassword.value) {
        fieldError.value = 'Passwords do not match.';
        return;
      }
      await api('/api/auth/register', {
        method: 'POST',
        body: { email: email.value, password: password.value },
      });
    }

    const auth = await api<AuthResponse>('/api/auth/login', {
      method: 'POST',
      body: { email: email.value, password: password.value },
    });

    setSession(auth.token, email.value);
    toastSuccess('Welcome back!');
    await navigateTo('/games');
  } catch (err) {
    fieldError.value = err instanceof Error ? err.message : String(err);
    toastError(fieldError.value);
  } finally {
    isSubmitting.value = false;
  }
}
</script>

<template>
  <section class="page">
    <div class="card">
      <div class="text-center mb-2" style="margin-bottom: 1.5rem;">
        <div style="font-size: 2.5rem; margin-bottom: 0.5rem;">⚔️</div>
        <h1 style="font-size: 1.6rem; margin: 0 0 0.25rem;">TTRPG Table</h1>
        <p style="font-size: 0.875rem;">{{ isLogin ? 'Sign in to host your games.' : 'Create a Dungeon Master account.' }}</p>
      </div>

      <div class="tabs" style="margin-bottom: 1.25rem;">
        <button class="btn ghost flex-1" :class="{ active: isLogin }" type="button" @click="switchMode('login')">
          Sign In
        </button>
        <button class="btn ghost flex-1" :class="{ active: !isLogin }" type="button" @click="switchMode('register')">
          Register
        </button>
      </div>

      <form @submit.prevent="submit">
        <label>
          Email
          <input
            v-model.trim="email"
            type="email"
            autocomplete="username"
            placeholder="dm@example.com"
            required
          />
        </label>

        <label>
          Password
          <input
            v-model="password"
            :type="isLogin ? 'password' : 'text'"
            :autocomplete="isLogin ? 'current-password' : 'new-password'"
            placeholder="Min. 7 characters, 1 uppercase, 1 digit"
            minlength="7"
            required
          />
        </label>

        <label v-if="!isLogin">
          Confirm password
          <input
            v-model="confirmPassword"
            type="text"
            autocomplete="new-password"
            placeholder="Re-enter password"
            minlength="7"
            required
          />
        </label>

        <div v-if="fieldError" class="alert error" role="alert">{{ fieldError }}</div>

        <button class="btn w-full" type="submit" :disabled="isSubmitting" style="margin-top: 0.25rem;">
          {{ isSubmitting ? 'Please wait…' : isLogin ? 'Sign In' : 'Create Account & Sign In' }}
        </button>
      </form>

      <p class="text-center text-xs muted" style="margin-top: 1.25rem; margin-bottom: 0;">
        Players don't need an account — they join via invite link.
      </p>
    </div>
  </section>
</template>
