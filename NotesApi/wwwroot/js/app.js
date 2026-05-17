const app = document.getElementById("app");
let token = localStorage.getItem("notes_token");

async function request(path, options = {}) {
  const headers = { "Content-Type": "application/json", ...(options.headers || {}) };
  if (token) headers.Authorization = `Bearer ${token}`;

  const response = await fetch(path, { ...options, headers });
  const text = await response.text();
  const data = text ? JSON.parse(text) : null;

  if (!response.ok) {
    const errors = Array.isArray(data?.errors)
      ? data.errors
      : data?.errors && typeof data.errors === "object"
        ? Object.values(data.errors).flat()
        : [data?.title || `Request failed (${response.status})`];
    throw new Error(errors.join(" "));
  }

  return data;
}

function renderAuth(error = "") {
  app.innerHTML = `
    <section class="card">
      <h1>Notes</h1>
      <p>Create an account or sign in to manage notes.</p>
      <form id="auth-form">
        <input id="email" type="email" placeholder="Email" required />
        <input id="password" type="text" placeholder="Password" minlength="7" required />
        <input id="confirm-password" type="text" placeholder="Confirm password (register only)" minlength="7" />
        ${error ? `<p class="error">${error}</p>` : ""}
        <div class="actions">
          <button type="submit" data-mode="login">Sign in</button>
          <button type="submit" data-mode="register">Register</button>
        </div>
      </form>
      <p><a href="/swagger">Open Swagger</a></p>
    </section>`;

  document.getElementById("auth-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    const mode = event.submitter.dataset.mode;
    const email = document.getElementById("email").value;
    const password = document.getElementById("password").value;
    const confirmPassword = document.getElementById("confirm-password").value;

    try {
      if (mode === "register") {
        if (password !== confirmPassword) throw new Error("Passwords do not match.");
        await request("/api/auth/register", { method: "POST", body: JSON.stringify({ email, password }) });
      }

      const auth = await request("/api/auth/login", { method: "POST", body: JSON.stringify({ email, password }) });
      token = auth.token;
      localStorage.setItem("notes_token", token);
      renderNotes();
    } catch (err) {
      renderAuth(err.message);
    }
  });
}

async function renderNotes(error = "") {
  let notes = [];
  try {
    notes = await request("/api/notes");
  } catch (err) {
    renderAuth(err.message);
    return;
  }

  app.innerHTML = `
    <section class="card wide">
      <div class="header">
        <h1>Your notes</h1>
        <button id="logout">Sign out</button>
      </div>
      <form id="note-form">
        <input id="title" placeholder="Title" maxlength="200" />
        <textarea id="content" placeholder="Write a note" required></textarea>
        ${error ? `<p class="error">${error}</p>` : ""}
        <button type="submit">Create note</button>
      </form>
      <ul class="notes">
        ${notes.map((note) => `<li><strong>${note.title || "Untitled"}</strong><p>${note.content}</p></li>`).join("")}
      </ul>
    </section>`;

  document.getElementById("logout").onclick = () => {
    localStorage.removeItem("notes_token");
    token = null;
    renderAuth();
  };

  document.getElementById("note-form").onsubmit = async (event) => {
    event.preventDefault();
    try {
      await request("/api/notes", {
        method: "POST",
        body: JSON.stringify({
          title: document.getElementById("title").value || undefined,
          content: document.getElementById("content").value,
        }),
      });
      renderNotes();
    } catch (err) {
      renderNotes(err.message);
    }
  };
}

if (token) {
  renderNotes();
} else {
  renderAuth();
}
