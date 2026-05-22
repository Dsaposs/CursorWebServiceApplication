# E2E tests (Playwright)

End-to-end tests run against a **live local Docker stack** (API on `:5294`, UI on `:3000`).

## Prerequisites

- Docker Desktop running
- Stack started (`docker compose up -d` or `scripts\start-app.cmd`)
- Node.js 22+

## Commands

From this directory:

```bash
npm install
npm run install:browsers
npm test
```

Environment overrides:

| Variable | Default |
|----------|---------|
| `E2E_BASE_URL` | `http://localhost:3000` |
| `E2E_MOBILE_BASE_URL` | `http://localhost:3001` |
| `E2E_API_URL` | `http://localhost:5294` |
| `E2E_ADMIN_EMAIL` | `admin@example.local` |
| `E2E_ADMIN_PASSWORD` | `Password1` |
| `E2E_WAIT_TIMEOUT_MS` | `180000` |
| `SKIP_E2E` | unset — set to `1` in `start-app.cmd` to skip |

## Coverage

| Spec | Flows |
|------|-------|
| `01-smoke` | API health, UI login page, Swagger |
| `02-auth-and-games` | DM login, create game, start session |
| `03-player-join` | Character creation, join code visibility |
| `04-exploration-action-flow` | Player action → DM roll prompt → roll → publish |
| `05-combat-mode` | Start combat, initiative, player combat banner |
| `06-session-sync-and-rulesets` | Version/live API, rulesets page |
| `07-session-lifecycle` | Stop session, player redirect |
| `08-user-registration` | Register UI, duplicate email, password validation |
| `09-npc-management` | Games hub + DM screen NPC CRUD |
| `10-stat-check-flow` | Session stat check → roll → publish |
| `mobile/01-mobile-smoke` | Mobile login, home, join, action submit |

Reports are written to `playwright-report/` after a run.
