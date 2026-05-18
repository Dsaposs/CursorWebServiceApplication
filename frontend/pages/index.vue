<script setup lang="ts">
import type { AdminUserReportResponse, AuthResponse, NoteResponse } from '~/types/api';

type AuthMode = 'login' | 'register';
type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE';

interface ApiOptions {
  method?: HttpMethod;
  body?: unknown;
}

const mode = ref<AuthMode>('login');
const token = ref<string | null>(null);
const email = ref('');
const password = ref('');
const confirmPassword = ref('');
const showLoginPassword = ref(false);
const notes = ref<NoteResponse[]>([]);
const adminUsers = ref<AdminUserReportResponse[] | null>(null);
const selectedId = ref<string | null>(null);
const noteTitle = ref('');
const noteContent = ref('');
const error = ref('');

const isLogin = computed(() => mode.value === 'login');
const selectedNote = computed(() => notes.value.find((note) => note.id === selectedId.value) ?? null);

onMounted(async () => {
  token.value = localStorage.getItem('notes_token');
  email.value = localStorage.getItem('notes_email') || '';

  if (token.value) {
    await loadDashboard();
  }
});

async function api<T>(path: string, options: ApiOptions = {}) {
  const headers = new Headers();

  if (token.value) {
    headers.set('Authorization', `Bearer ${token.value}`);
  }

  try {
    return await $fetch<T>(path, {
      method: options.method,
      body: options.body,
      headers,
    });
  } catch (fetchError) {
    throw new Error(extractError(fetchError));
  }
}

function extractError(fetchError: unknown) {
  const errorResponse = fetchError as {
    data?: { errors?: string[] | Record<string, string[]>; title?: string };
    status?: number;
    statusCode?: number;
    statusMessage?: string;
  };
  const data = errorResponse.data;

  if (Array.isArray(data?.errors)) {
    return data.errors.join(' ');
  }

  if (data?.errors && typeof data.errors === 'object') {
    return Object.values(data.errors).flat().join(' ');
  }

  return data?.title || errorResponse.statusMessage || `Request failed (${errorResponse.status ?? errorResponse.statusCode ?? 'unknown'})`;
}

function setMode(nextMode: AuthMode) {
  mode.value = nextMode;
  error.value = '';
  password.value = '';
  confirmPassword.value = '';
}

function setSession(nextToken: string, nextEmail: string) {
  token.value = nextToken;
  email.value = nextEmail;
  localStorage.setItem('notes_token', nextToken);
  localStorage.setItem('notes_email', nextEmail);
}

function clearSession() {
  token.value = null;
  email.value = '';
  notes.value = [];
  adminUsers.value = null;
  selectedId.value = null;
  noteTitle.value = '';
  noteContent.value = '';
  localStorage.removeItem('notes_token');
  localStorage.removeItem('notes_email');
}

async function handleAuthSubmit() {
  error.value = '';

  try {
    if (mode.value === 'register') {
      if (password.value !== confirmPassword.value) {
        throw new Error('Passwords do not match.');
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
    password.value = '';
    confirmPassword.value = '';
    await loadDashboard();
  } catch (submitError) {
    error.value = getMessage(submitError);
  }
}

async function loadDashboard() {
  try {
    adminUsers.value = await api<AdminUserReportResponse[]>('/api/admin/users');
    notes.value = [];
    selectedId.value = null;
    error.value = '';
    return;
  } catch (adminError) {
    if (!isAccessDenied(adminError)) {
      clearSession();
      error.value = getMessage(adminError);
      return;
    }
  }

  await loadNotes();
}

async function loadNotes() {
  try {
    notes.value = await api<NoteResponse[]>('/api/notes');
    adminUsers.value = null;
    error.value = '';
    if (selectedId.value && !notes.value.some((note) => note.id === selectedId.value)) {
      selectedId.value = null;
    }
    syncEditor();
  } catch (loadError) {
    clearSession();
    error.value = getMessage(loadError);
  }
}

function syncEditor() {
  noteTitle.value = selectedNote.value?.title || '';
  noteContent.value = selectedNote.value?.content || '';
}

function selectNote(id: string) {
  selectedId.value = id;
  error.value = '';
  syncEditor();
}

function newNote() {
  selectedId.value = null;
  error.value = '';
  syncEditor();
}

async function saveNote() {
  error.value = '';
  const payload = {
    title: noteTitle.value || undefined,
    content: noteContent.value,
  };

  try {
    if (selectedId.value) {
      await api(`/api/notes/${selectedId.value}`, {
        method: 'PUT',
        body: payload,
      });
    } else {
      const created = await api<NoteResponse>('/api/notes', {
        method: 'POST',
        body: payload,
      });
      selectedId.value = created.id;
    }

    await loadNotes();
  } catch (saveError) {
    error.value = getMessage(saveError);
  }
}

async function deleteNote() {
  if (!selectedId.value || !confirm('Delete this note?')) {
    return;
  }

  try {
    await api(`/api/notes/${selectedId.value}`, { method: 'DELETE' });
    selectedId.value = null;
    await loadNotes();
  } catch (deleteError) {
    error.value = getMessage(deleteError);
  }
}

function signOut() {
  clearSession();
  mode.value = 'login';
}

function isAccessDenied(accessError: unknown) {
  return getMessage(accessError).includes('403') || getMessage(accessError).toLowerCase().includes('forbidden');
}

function getMessage(value: unknown) {
  return value instanceof Error ? value.message : String(value);
}
</script>

<template>
  <section v-if="!token" class="page">
    <div class="card">
      <h1>Notes</h1>
      <p>{{ isLogin ? 'Sign in to manage your notes.' : 'Create an account. Passwords are visible while registering.' }}</p>

      <div class="tabs">
        <button class="btn secondary" :class="{ active: isLogin }" type="button" @click="setMode('login')">
          Sign in
        </button>
        <button class="btn secondary" :class="{ active: !isLogin }" type="button" @click="setMode('register')">
          Register
        </button>
      </div>

      <form @submit.prevent="handleAuthSubmit">
        <label>
          Email or username
          <input v-model.trim="email" type="text" autocomplete="username" required />
        </label>

        <label>
          Password
          <div v-if="isLogin" class="password-row">
            <input v-model="password" :type="showLoginPassword ? 'text' : 'password'" autocomplete="current-password" required />
            <button class="btn secondary" type="button" @click="showLoginPassword = !showLoginPassword">
              {{ showLoginPassword ? 'Hide' : 'Show' }}
            </button>
          </div>
          <input v-else v-model="password" type="text" autocomplete="new-password" minlength="7" required />
        </label>

        <label v-if="!isLogin">
          Confirm password
          <input v-model="confirmPassword" type="text" autocomplete="new-password" minlength="7" required />
        </label>

        <div v-if="error" class="error">{{ error }}</div>

        <button class="btn" type="submit">{{ isLogin ? 'Sign in' : 'Register' }}</button>
      </form>
    </div>
  </section>

  <section v-else-if="adminUsers" class="app-shell">
    <header class="topbar">
      <div>
        <strong>Admin Dashboard</strong>
        <span class="muted">{{ email }}</span>
      </div>
      <button class="btn secondary" type="button" @click="signOut">Sign out</button>
    </header>

    <section class="admin-panel">
      <h2>Admin: users</h2>
      <p class="muted">Password hashes are not displayed. The table only shows whether a password hash exists.</p>
      <table>
        <thead>
          <tr>
            <th>Username</th>
            <th>Email</th>
            <th>Password hash stored</th>
            <th>Notes created</th>
            <th>Notes deleted</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="user in adminUsers" :key="user.userId">
            <td>{{ user.userName }}</td>
            <td>{{ user.email || '' }}</td>
            <td>{{ user.hasPasswordHash ? 'Yes' : 'No' }}</td>
            <td>{{ user.notesCreatedCount }}</td>
            <td>{{ user.notesDeletedCount }}</td>
          </tr>
        </tbody>
      </table>
    </section>
  </section>

  <section v-else class="app-shell">
    <header class="topbar">
      <div>
        <strong>Notes</strong>
        <span class="muted">{{ email }}</span>
      </div>
      <button class="btn secondary" type="button" @click="signOut">Sign out</button>
    </header>

    <div class="workspace">
      <aside class="sidebar">
        <div class="actions">
          <h2>Your notes</h2>
          <button class="btn secondary" type="button" @click="newNote">New</button>
        </div>
        <ul class="note-list">
          <li v-if="notes.length === 0" class="muted">No notes yet.</li>
          <li v-for="note in notes" v-else :key="note.id">
            <button :class="{ active: note.id === selectedId }" type="button" @click="selectNote(note.id)">
              <strong>{{ note.title || 'Untitled' }}</strong><br />
              <span class="muted">{{ note.content.slice(0, 80) }}</span>
            </button>
          </li>
        </ul>
      </aside>

      <main class="editor">
        <h2>{{ selectedNote ? 'Edit note' : 'Create note' }}</h2>
        <form @submit.prevent="saveNote">
          <label>
            Title
            <input v-model="noteTitle" maxlength="200" />
          </label>
          <label>
            Content
            <textarea v-model="noteContent" required />
          </label>
          <div v-if="error" class="error">{{ error }}</div>
          <div class="actions">
            <button class="btn" type="submit">{{ selectedNote ? 'Save changes' : 'Create note' }}</button>
            <button v-if="selectedNote" class="btn danger" type="button" @click="deleteNote">Delete</button>
          </div>
        </form>
      </main>
    </div>
  </section>
</template>
