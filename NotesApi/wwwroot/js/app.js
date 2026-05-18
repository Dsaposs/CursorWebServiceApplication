const app = document.getElementById("app");

const state = {
  mode: "login",
  token: localStorage.getItem("notes_token"),
  email: localStorage.getItem("notes_email") || "",
  notes: [],
  adminUsers: null,
  isAdmin: false,
  selectedId: null,
  showLoginPassword: false,
  error: "",
};

async function api(path, options = {}) {
  const headers = new Headers(options.headers);
  if (options.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  if (state.token) {
    headers.set("Authorization", `Bearer ${state.token}`);
  }

  const response = await fetch(path, { ...options, headers });
  const text = await response.text();
  const data = text ? JSON.parse(text) : null;

  if (!response.ok) {
    throw new Error(extractError(data, response.status));
  }

  return data;
}

function extractError(data, status) {
  if (Array.isArray(data?.errors)) return data.errors.join(" ");
  if (data?.errors && typeof data.errors === "object") return Object.values(data.errors).flat().join(" ");
  return data?.title || `Request failed (${status})`;
}

function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value ?? "";
  return div.innerHTML;
}

function setSession(token, email) {
  state.token = token;
  state.email = email;
  localStorage.setItem("notes_token", token);
  localStorage.setItem("notes_email", email);
}

function clearSession() {
  state.token = null;
  state.email = "";
  state.notes = [];
  state.adminUsers = null;
  state.isAdmin = false;
  state.selectedId = null;
  localStorage.removeItem("notes_token");
  localStorage.removeItem("notes_email");
}

function renderAuth() {
  const isLogin = state.mode === "login";
  app.innerHTML = `
    <section class="page">
      <div class="card">
        <h1>Notes</h1>
        <p>${isLogin ? "Sign in to manage your notes." : "Create an account. Passwords are visible while registering."}</p>

        <div class="tabs">
          <button class="btn secondary ${isLogin ? "active" : ""}" id="login-tab" type="button">Sign in</button>
          <button class="btn secondary ${!isLogin ? "active" : ""}" id="register-tab" type="button">Register</button>
        </div>

        <form id="auth-form">
          <label>
            Email or username
            <input id="email" type="text" autocomplete="username" required value="${escapeHtml(state.email)}" />
          </label>

          ${isLogin ? renderLoginPassword() : renderRegisterPasswords()}

          ${state.error ? `<div class="error">${escapeHtml(state.error)}</div>` : ""}

          <button class="btn" type="submit">${isLogin ? "Sign in" : "Register"}</button>
        </form>
      </div>
    </section>
  `;

  document.getElementById("login-tab").onclick = () => {
    state.mode = "login";
    state.error = "";
    renderAuth();
  };
  document.getElementById("register-tab").onclick = () => {
    state.mode = "register";
    state.error = "";
    renderAuth();
  };

  const toggle = document.getElementById("toggle-login-password");
  if (toggle) {
    toggle.onclick = () => {
      state.showLoginPassword = !state.showLoginPassword;
      renderAuth();
    };
  }

  document.getElementById("auth-form").onsubmit = handleAuthSubmit;
}

function renderLoginPassword() {
  return `
    <label>
      Password
      <div class="password-row">
        <input id="password" type="${state.showLoginPassword ? "text" : "password"}" autocomplete="current-password" required />
        <button class="btn secondary" id="toggle-login-password" type="button">${state.showLoginPassword ? "Hide" : "Show"}</button>
      </div>
    </label>
  `;
}

function renderRegisterPasswords() {
  return `
    <label>
      Password
      <input id="password" type="text" autocomplete="new-password" minlength="7" required />
    </label>
    <label>
      Confirm password
      <input id="confirm-password" type="text" autocomplete="new-password" minlength="7" required />
    </label>
  `;
}

async function handleAuthSubmit(event) {
  event.preventDefault();
  state.error = "";

  const email = document.getElementById("email").value.trim();
  const password = document.getElementById("password").value;

  try {
    if (state.mode === "register") {
      const confirmPassword = document.getElementById("confirm-password").value;
      if (password !== confirmPassword) throw new Error("Passwords do not match.");
      await api("/api/auth/register", { method: "POST", body: JSON.stringify({ email, password }) });
    }

    const auth = await api("/api/auth/login", { method: "POST", body: JSON.stringify({ email, password }) });
    setSession(auth.token, email);
    await loadDashboard();
  } catch (error) {
    state.error = error.message;
    renderAuth();
  }
}

async function loadDashboard() {
  try {
    const adminUsers = await api("/api/admin/users");
    state.adminUsers = adminUsers;
    state.isAdmin = true;
    state.notes = [];
    state.error = "";
    renderAdminOnly();
    return;
  } catch (error) {
    if (!isAccessDenied(error)) {
      clearSession();
      state.error = error.message;
      renderAuth();
      return;
    }
  }

  state.isAdmin = false;
  await loadNotes();
}

async function loadNotes() {
  try {
    state.notes = await api("/api/notes");
    state.adminUsers = null;
    state.error = "";
    renderNotes();
  } catch (error) {
    clearSession();
    state.error = error.message;
    renderAuth();
  }
}

function renderAdminOnly() {
  app.innerHTML = `
    <section class="app-shell">
      <header class="topbar">
        <div>
          <strong>Admin Dashboard</strong>
          <span class="muted">${escapeHtml(state.email)}</span>
        </div>
        <button class="btn secondary" id="logout" type="button">Sign out</button>
      </header>
      ${renderAdminPanel()}
    </section>
  `;

  document.getElementById("logout").onclick = () => {
    clearSession();
    renderAuth();
  };
}

async function loadAdminUsersIfAllowed() {
  try {
    return await api("/api/admin/users");
  } catch {
    return null;
  }
}

function isAccessDenied(error) {
  return error.message.includes("403") || error.message.toLowerCase().includes("forbidden");
}

function renderNotes() {
  const selected = state.notes.find((note) => note.id === state.selectedId);
  app.innerHTML = `
    <section class="app-shell">
      <header class="topbar">
        <div>
          <strong>Notes</strong>
          <span class="muted">${escapeHtml(state.email)}</span>
        </div>
        <button class="btn secondary" id="logout" type="button">Sign out</button>
      </header>

      <div class="workspace">
        <aside class="sidebar">
          <div class="actions">
            <h2>Your notes</h2>
            <button class="btn secondary" id="new-note" type="button">New</button>
          </div>
          <ul class="note-list">
            ${state.notes.map(renderNoteListItem).join("") || `<li class="muted">No notes yet.</li>`}
          </ul>
        </aside>

        <main class="editor">
          <h2>${selected ? "Edit note" : "Create note"}</h2>
          <form id="note-form">
            <label>
              Title
              <input id="title" maxlength="200" value="${escapeHtml(selected?.title || "")}" />
            </label>
            <label>
              Content
              <textarea id="content" required>${escapeHtml(selected?.content || "")}</textarea>
            </label>
            ${state.error ? `<div class="error">${escapeHtml(state.error)}</div>` : ""}
            <div class="actions">
              <button class="btn" type="submit">${selected ? "Save changes" : "Create note"}</button>
              ${selected ? `<button class="btn danger" id="delete-note" type="button">Delete</button>` : ""}
            </div>
          </form>
        </main>
      </div>
      ${state.adminUsers ? renderAdminPanel() : ""}
    </section>
  `;

  document.getElementById("logout").onclick = () => {
    clearSession();
    renderAuth();
  };
  document.getElementById("new-note").onclick = () => {
    state.selectedId = null;
    state.error = "";
    renderNotes();
  };
  document.querySelectorAll("[data-note-id]").forEach((button) => {
    button.onclick = () => {
      state.selectedId = button.dataset.noteId;
      state.error = "";
      renderNotes();
    };
  });
  document.getElementById("note-form").onsubmit = saveNote;

  const deleteButton = document.getElementById("delete-note");
  if (deleteButton) deleteButton.onclick = deleteNote;
}

function renderAdminPanel() {
  return `
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
          ${state.adminUsers.map((user) => `
            <tr>
              <td>${escapeHtml(user.userName)}</td>
              <td>${escapeHtml(user.email || "")}</td>
              <td>${user.hasPasswordHash ? "Yes" : "No"}</td>
              <td>${user.notesCreatedCount}</td>
              <td>${user.notesDeletedCount}</td>
            </tr>
          `).join("")}
        </tbody>
      </table>
    </section>
  `;
}

function renderNoteListItem(note) {
  const active = note.id === state.selectedId ? "active" : "";
  return `
    <li>
      <button class="${active}" type="button" data-note-id="${note.id}">
        <strong>${escapeHtml(note.title || "Untitled")}</strong><br />
        <span class="muted">${escapeHtml(note.content.slice(0, 80))}</span>
      </button>
    </li>
  `;
}

async function saveNote(event) {
  event.preventDefault();
  const payload = {
    title: document.getElementById("title").value || undefined,
    content: document.getElementById("content").value,
  };

  try {
    if (state.selectedId) {
      await api(`/api/notes/${state.selectedId}`, { method: "PUT", body: JSON.stringify(payload) });
    } else {
      const created = await api("/api/notes", { method: "POST", body: JSON.stringify(payload) });
      state.selectedId = created.id;
    }
    await loadNotes();
  } catch (error) {
    state.error = error.message;
    renderNotes();
  }
}

async function deleteNote() {
  if (!state.selectedId || !confirm("Delete this note?")) return;
  try {
    await api(`/api/notes/${state.selectedId}`, { method: "DELETE" });
    state.selectedId = null;
    await loadNotes();
  } catch (error) {
    state.error = error.message;
    renderNotes();
  }
}

if (state.token) {
  loadDashboard();
} else {
  renderAuth();
}
