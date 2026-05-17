const TOKEN_KEY = "notes_token";
const EMAIL_KEY = "notes_email";

const state = {
  token: localStorage.getItem(TOKEN_KEY),
  email: localStorage.getItem(EMAIL_KEY),
  mode: "login",
  notes: [],
  selectedId: null,
  title: "",
  content: "",
  loading: false,
  saving: false,
  error: null,
};

const app = document.getElementById("app");

function escapeHtml(text) {
  const d = document.createElement("div");
  d.textContent = text;
  return d.innerHTML;
}

function parseErrors(data) {
  if (!data) return ["Request failed"];
  if (Array.isArray(data.errors)) return data.errors;
  if (data.errors && typeof data.errors === "object") {
    return Object.values(data.errors).flat();
  }
  if (data.title) return [data.title];
  return ["Request failed"];
}

async function api(path, options = {}) {
  const headers = { "Content-Type": "application/json", ...(options.headers || {}) };
  if (state.token) headers.Authorization = `Bearer ${state.token}`;

  const res = await fetch(path, { ...options, headers });
  const text = await res.text();
  const data = text ? JSON.parse(text) : null;

  if (!res.ok) {
    throw new Error(parseErrors(data).join(" "));
  }
  return data;
}

function setSession(token, email) {
  state.token = token;
  state.email = email;
  localStorage.setItem(TOKEN_KEY, token);
  localStorage.setItem(EMAIL_KEY, email);
}

function clearSession() {
  state.token = null;
  state.email = null;
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(EMAIL_KEY);
}

function preview(text) {
  const line = text.replace(/\s+/g, " ").trim();
  return line.length > 72 ? `${line.slice(0, 72)}…` : line || "Empty note";
}

function formatDate(iso) {
  return new Date(iso).toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
  });
}

async function loadNotes() {
  state.loading = true;
  state.error = null;
  render();
  try {
    state.notes = await api("/api/notes");
  } catch (e) {
    state.error = e.message;
  } finally {
    state.loading = false;
    render();
  }
}

async function handleAuthSubmit(e) {
  e.preventDefault();
  const email = document.getElementById("email").value.trim();
  const password = document.getElementById("password").value;
  state.error = null;

  if (state.mode === "register") {
    const confirmPassword = document.getElementById("confirm-password").value;
    if (password !== confirmPassword) {
      state.error = "Passwords do not match.";
      render();
      return;
    }
  }

  state.loading = true;
  render();

  try {
    if (state.mode === "register") {
      await api("/api/auth/register", {
        method: "POST",
        body: JSON.stringify({ email, password }),
      });
    }
    const auth = await api("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    });
    setSession(auth.token, email);
    state.selectedId = null;
    await loadNotes();
  } catch (e) {
    state.error = e.message;
    state.loading = false;
    render();
  }
}

async function saveNote(e) {
  e.preventDefault();
  if (!state.content.trim()) return;

  state.saving = true;
  state.error = null;
  render();

  const payload = {
    title: state.title.trim() || undefined,
    content: state.content.trim(),
  };

  try {
    if (state.selectedId === "new") {
      const created = await api("/api/notes", {
        method: "POST",
        body: JSON.stringify(payload),
      });
      state.notes.unshift(created);
      state.selectedId = created.id;
    } else {
      const updated = await api(`/api/notes/${state.selectedId}`, {
        method: "PUT",
        body: JSON.stringify(payload),
      });
      state.notes = state.notes.map((n) => (n.id === updated.id ? updated : n));
      state.title = updated.title ?? "";
      state.content = updated.content;
    }
  } catch (e) {
    state.error = e.message;
  } finally {
    state.saving = false;
    render();
  }
}

async function deleteNote() {
  if (state.selectedId === "new" || !confirm("Delete this note?")) return;

  state.saving = true;
  state.error = null;
  render();

  try {
    await api(`/api/notes/${state.selectedId}`, { method: "DELETE" });
    state.notes = state.notes.filter((n) => n.id !== state.selectedId);
    state.selectedId = null;
    state.title = "";
    state.content = "";
  } catch (e) {
    state.error = e.message;
  } finally {
    state.saving = false;
    render();
  }
}

function selectNote(note) {
  state.selectedId = note.id;
  state.title = note.title ?? "";
  state.content = note.content;
  state.error = null;
  render();
}

function startNewNote() {
  state.selectedId = "new";
  state.title = "";
  state.content = "";
  state.error = null;
  render();
}

function renderAuth() {
  const isLogin = state.mode === "login";
  app.innerHTML = `
    <div class="shell auth-shell">
      <div class="auth-card">
        <header class="auth-header">
          <p class="eyebrow">Notes</p>
          <h1>${isLogin ? "Welcome back" : "Create your account"}</h1>
          <p class="muted">${
            isLogin
              ? "Sign in to view and edit your notes."
              : "Password needs 7+ characters, one uppercase letter, and one number."
          }</p>
        </header>
        <form class="auth-form" id="auth-form">
          <label>Email<input id="email" type="email" required autocomplete="email" placeholder="you@example.com" /></label>
          ${
            isLogin
              ? `<label class="password-field">Password
            <div class="password-input-wrap">
              <input id="password" type="password" required minlength="7" autocomplete="current-password" placeholder="•••••••" />
              <button type="button" class="password-toggle" id="password-toggle" aria-label="Show password">Show</button>
            </div>
          </label>`
              : `<label>Password
            <input id="password" type="text" required minlength="7" autocomplete="new-password" placeholder="Enter password" />
          </label>
          <label>Confirm password
            <input id="confirm-password" type="text" required minlength="7" autocomplete="new-password" placeholder="Re-enter password" />
          </label>`
          }
          ${state.error ? `<p class="error-banner">${escapeHtml(state.error)}</p>` : ""}
          <button type="submit" class="btn primary" ${state.loading ? "disabled" : ""}>${state.loading ? "Please wait…" : isLogin ? "Sign in" : "Create account"}</button>
        </form>
        <p class="auth-switch">${isLogin ? "No account yet?" : "Already registered?"} <button type="button" class="link-btn" id="toggle-mode">${isLogin ? "Register" : "Sign in"}</button></p>
      </div>
    </div>`;

  document.getElementById("auth-form").onsubmit = handleAuthSubmit;
  const passwordToggle = document.getElementById("password-toggle");
  if (passwordToggle) {
    const passwordInput = document.getElementById("password");
    passwordToggle.onclick = () => {
      const show = passwordInput.type === "password";
      passwordInput.type = show ? "text" : "password";
      passwordToggle.textContent = show ? "Hide" : "Show";
      passwordToggle.setAttribute("aria-label", show ? "Hide password" : "Show password");
    };
  }
  document.getElementById("toggle-mode").onclick = () => {
    state.mode = isLogin ? "register" : "login";
    state.error = null;
    render();
  };
}

function renderNotes() {
  const isNew = state.selectedId === "new";
  const listHtml = state.loading
    ? `<p class="muted pad">Loading…</p>`
    : state.notes.length === 0
      ? `<p class="muted pad">No notes yet. Create one.</p>`
      : `<ul class="note-list">${state.notes
          .map(
            (n) => `
        <li><button type="button" class="note-item ${state.selectedId === n.id ? "active" : ""}" data-id="${n.id}">
          <strong>${escapeHtml(n.title?.trim() || "Untitled")}</strong>
          <span>${escapeHtml(preview(n.content))}</span>
          <time>${formatDate(n.updatedAt)}</time>
        </button></li>`,
          )
          .join("")}</ul>`;

  const editorHtml = !state.selectedId
    ? `<div class="empty-editor"><h2>Select a note</h2><p class="muted">Choose a note from the list or create a new one.</div>`
    : `<form class="editor-form" id="editor-form">
        <input class="title-input" id="note-title" type="text" maxlength="200" placeholder="Title" value="${escapeHtml(state.title)}" />
        <textarea class="content-input" id="note-content" required placeholder="Write your note…">${escapeHtml(state.content)}</textarea>
        ${state.error ? `<p class="error-banner">${escapeHtml(state.error)}</p>` : ""}
        <div class="editor-actions">
          <button type="submit" class="btn primary" ${state.saving ? "disabled" : ""}>${state.saving ? "Saving…" : isNew ? "Create note" : "Save changes"}</button>
          ${!isNew ? `<button type="button" class="btn danger" id="delete-btn" ${state.saving ? "disabled" : ""}>Delete</button>` : ""}
        </div>
      </form>`;

  app.innerHTML = `
    <div class="shell notes-shell">
      <header class="top-bar">
        <div class="brand"><span class="eyebrow">Notes</span><span class="user-email">${escapeHtml(state.email || "")}</span></div>
        <button type="button" class="btn ghost" id="logout-btn">Sign out</button>
      </header>
      <div class="notes-layout">
        <aside class="sidebar">
          <div class="sidebar-head"><h2>Your notes</h2><button type="button" class="btn primary small" id="new-btn">+ New</button></div>
          ${listHtml}
        </aside>
        <main class="editor-pane">${editorHtml}</main>
      </div>
    </div>`;

  document.getElementById("logout-btn").onclick = () => {
    clearSession();
    state.notes = [];
    state.selectedId = null;
    render();
  };
  document.getElementById("new-btn").onclick = startNewNote;
  app.querySelectorAll(".note-item").forEach((btn) => {
    btn.onclick = () => {
      const note = state.notes.find((n) => n.id === btn.dataset.id);
      if (note) selectNote(note);
    };
  });

  const form = document.getElementById("editor-form");
  if (form) {
    form.onsubmit = saveNote;
    document.getElementById("note-title").oninput = (e) => {
      state.title = e.target.value;
    };
    document.getElementById("note-content").oninput = (e) => {
      state.content = e.target.value;
    };
    const del = document.getElementById("delete-btn");
    if (del) del.onclick = deleteNote;
  }
}

function render() {
  if (!state.token) {
    renderAuth();
  } else {
    renderNotes();
  }
}

if (state.token) {
  loadNotes();
} else {
  render();
}
