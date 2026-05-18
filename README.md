# TTRPG Table

A two-service web application for hosting tabletop RPG sessions. A DM can register, create games from a ruleset, share invite links, start live sessions, and resolve player actions into a shared action queue. Players join from any device without an account, create or reopen a character by name, submit actions, and follow exploration or combat turns in real time.

## Stack

| Layer | Technology |
|-------|-----------|
| API | ASP.NET Core 8, Entity Framework Core, SQLite, ASP.NET Identity, JWT |
| UI | Nuxt 4, Vue 3, TypeScript |
| Auth (DM) | JWT Bearer |
| Auth (Player) | Session-scoped opaque `X-Player-Token` |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) for local Nuxt development
- Docker Desktop for containerized local runs
- SQLite is used through Entity Framework Core — no separate DB server required

## Run Locally

SQLite is created automatically on first startup.

**Start the API:**

```powershell
cd "C:\Users\Dan\.cursor\projects\Cursor Web Service Application"
dotnet restore api\src\NotesApi\NotesApi.csproj
dotnet run --project api\src\NotesApi\NotesApi.csproj
```

**Start the UI (separate terminal):**

```powershell
cd "C:\Users\Dan\.cursor\projects\Cursor Web Service Application\ui\src"
npm install
$env:NUXT_API_BASE_URL = "http://localhost:5294"
npm run dev
```

- UI: `http://localhost:3000`
- Swagger: `http://localhost:5294/swagger` (dev only)
- Health: `http://localhost:5294/health`

## Docker (local)

```powershell
.\scripts\start-app.cmd   # build + start both containers
.\scripts\stop-app.cmd    # stop + remove containers
```

The script builds `ttrpg-api:local` and `ttrpg-ui:local`, creates the `ttrpg-network` and `ttrpg-data` volume, and wires `NUXT_API_BASE_URL` inside the Docker network.
For local Docker runs, the script generates a per-container JWT key unless `TTRPG_JWT_KEY` is set. Set `TTRPG_SEED_ADMIN_PASSWORD` to control the seeded admin password.

## Kubernetes

```powershell
docker build -t ttrpg-api:local .
docker build -t ttrpg-ui:local ui\src
kubectl apply -k k8s
kubectl rollout status deployment/ttrpg-api -n ttrpg
kubectl rollout status deployment/ttrpg-ui -n ttrpg
# Port-forward to access locally:
kubectl port-forward service/ttrpg-ui 3000:80 -n ttrpg
```

> Before deploying outside localhost, replace the placeholder `Jwt__Key` and `Seed__AdminPassword` values in [`k8s/secret.yaml`](k8s/secret.yaml). The API refuses to start in production with checked-in placeholder secrets.

SQLite is single-file — the deployment uses **1 replica** intentionally.

## Core Flows

1. DM registers or signs in → receives a JWT.
2. DM creates a game, picks a ruleset, and shares the invite link with players.
3. Players follow the invite link, enter a character name, and receive a persistent player token (stored in `localStorage`). No account required.
4. DM adds NPCs/monsters, then starts a live session.
5. DM shares the session join link. Players open it on their phone.
6. **Exploration** — players submit free-form actions; DM publishes resolutions with optional stat changes.
7. **Combat** — DM sets initiative order; turns advance one combatant at a time.
8. All published actions and resolutions appear in the shared action feed visible to all participants.

## API Reference

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/auth/register` | — | Create DM account |
| POST | `/api/auth/login` | — | Returns JWT |
| GET | `/api/rulesets` | — | List rulesets |
| GET | `/api/games` | Bearer | DM's games |
| POST | `/api/games` | Bearer | Create game |
| GET | `/api/games/{id}` | Bearer | Get one game |
| PUT | `/api/games/{id}` | Bearer | Update game |
| DELETE | `/api/games/{id}` | Bearer | Delete game + all data |
| POST | `/api/games/{id}/npcs` | Bearer | Add NPC/monster |
| PUT | `/api/games/{id}/npcs/{npcId}` | Bearer | Update NPC |
| DELETE | `/api/games/{id}/npcs/{npcId}` | Bearer | Delete NPC |
| POST | `/api/game-participants/join/{inviteCode}` | — | Join game / reopen character |
| POST | `/api/games/{id}/sessions` | Bearer | Start live session |
| GET | `/api/sessions/{id}/dm` | Bearer | DM polling endpoint |
| POST | `/api/sessions/{id}/state` | Bearer | Switch Exploration/Combat |
| POST | `/api/sessions/{id}/stop` | Bearer | End session |
| GET | `/api/session-join/{code}` | — | Public session info |
| POST | `/api/session-join/{code}` | — | Player joins session |
| GET | `/api/session-join/{code}/state` | `X-Player-Token` | Player polling state |
| GET | `/api/sessions/{code}/actions` | Bearer or `X-Player-Token` | Read action queue |
| POST | `/api/sessions/{code}/actions` | Bearer or `X-Player-Token` | Submit action |
| PUT | `/api/actions/{id}/resolve` | Bearer | Publish resolution + stat changes |
| POST | `/api/sessions/{id}/combat` | Bearer | Set initiative |
| POST | `/api/sessions/{id}/combat/advance` | Bearer | Advance turn |
| GET | `/api/admin/users` | Bearer (Admin role) | User report |

## Configuration

| Key | Where | Purpose |
|-----|-------|---------|
| `ConnectionStrings:DefaultConnection` | `appsettings.json` | SQLite path |
| `Jwt:Key` | env / user secrets | JWT signing key — **change in production** |
| `Jwt:Issuer` / `Jwt:Audience` | `appsettings.json` | JWT validation |
| `Seed:AdminPassword` | env / `appsettings.json` | Seeded admin password — **change in production** |
| `Cors:AllowedOrigins` | env / `appsettings.json` | Allowed UI origins array |
| `NUXT_API_BASE_URL` | env | API base URL for Nuxt server-side requests |

## Security and Performance Notes

- The Nuxt API proxy forwards only the request headers needed by this app: `Authorization`, `X-Player-Token`, `Accept`, and `Content-Type`.
- Production API startup rejects known placeholder JWT keys and weak/default seeded admin passwords.
- Player tokens are currently stored in `localStorage`; moving DM auth and player tokens to HttpOnly cookies would reduce XSS exposure in a future auth pass.
- Public invite/session codes should receive rate limiting before internet-facing deployment.
- Live sessions currently use polling. The polling implementation pauses when hidden and throttles participant presence writes, but SSE or WebSockets would scale better for high-traffic live play.

## Password Rules

- Valid email address
- At least 7 characters
- At least one uppercase letter
- At least one digit

## Tests

```powershell
dotnet test api\tests\NotesApi.Tests\NotesApi.Tests.csproj
npm --prefix ui\src test
```

Tests cover: DM-owned game scoping, participant token scoping, combat initiative tracking, game name uniqueness constraint, cascade delete behavior, and frontend ruleset/action chooser helpers.
