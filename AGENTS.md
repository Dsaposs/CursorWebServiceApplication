# AGENTS.md

## Cursor Cloud specific instructions

### Services overview

| Service | Tech | Dev command | Port |
|---------|------|-------------|------|
| API | ASP.NET Core 8 + EF Core + SQLite | `dotnet run --project api/src/NotesApi/NotesApi.csproj --urls "http://0.0.0.0:5294"` | 5294 |
| UI | Nuxt 4 (Vue 3, TypeScript) | `NUXT_API_BASE_URL=http://localhost:5294 npm run dev` (from `ui/src/`) | 3000 |
| Mobile | Nuxt 3 + Ionic + Capacitor | `NUXT_API_BASE_URL=http://localhost:5294 npm run dev` (from `mobile/src/`) | 3001 |

### Running services

- **API** must start before the UI since the Nuxt server-side proxy forwards `/api/*` requests to `http://localhost:5294`.
- SQLite DB is auto-created on first API startup — no migration commands needed.
- The API seeds an admin user on first run; dev defaults are in `appsettings.json` (JWT key and admin password are placeholders safe for local dev).
- Run `npx nuxi prepare` inside `ui/src/` before typechecking — Nuxt generates `.nuxt/tsconfig.json` which downstream tooling requires.

### Tests

- API: `dotnet test api/tests/NotesApi.Tests/NotesApi.Tests.csproj` (9 xUnit tests)
- UI: `npm test` from `ui/src/` (vitest). Note: tests currently fail with a pre-existing issue where `vitest.config.ts` is missing the `@vitejs/plugin-vue` plugin needed to parse `.vue` imports in the test dependency chain.
- E2E: `cd e2e && npm test` (Playwright against Docker stack; also run via `scripts/start-app.cmd`)
- Performance: `scripts/run-perf.cmd` or `cd perf && npm run pipeline` — k6 load probes, soak/memory sampling, UI latency; outputs `perf/reports/PERFORMANCE_REPORT.md`
- GCP (local): install CLI with `winget install Google.CloudSDK`, then `gcloud auth login`. Bootstrap Artifact Registry + IAM with `scripts/gcp-setup.ps1` (see script header for parameters).

### Key dev notes

- The .NET 8 SDK is installed at `/usr/local/dotnet` and added to PATH via `~/.bashrc`.
- Node.js v22 is managed via nvm (default in environment).
- No lockfile for UI dependencies beyond `package-lock.json`; use `npm install` (not yarn/pnpm).
- Health check: `GET http://localhost:5294/health` → "Healthy"
- Swagger UI available at `http://localhost:5294/swagger` in Development mode.
- The D&D 5e character creation flow requires `classKey`, `skillAllocations`, and `startingItemKey` fields — partial requests are rejected with validation errors.
- Mobile app runs with SSR disabled (Capacitor requirement). Capacitor native platforms (iOS/Android) are **not** committed; run `npx cap add ios` / `npx cap add android` locally after `npm install` in `mobile/src/`.
- All docker services can be started together with `docker compose up -d` from the repo root.
- **LAN access:** `scripts/start-app.cmd` auto-detects `LAN_HOST` in `.env` so UI (`:3000`), mobile (`:3001`), and API (`:5294`) are reachable from other devices on the same network. Set `LAN_HOST` manually if auto-detection picks the wrong adapter.
