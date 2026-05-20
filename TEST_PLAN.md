# TTRPG Table Test Plan

## 1. Purpose and scope

This plan defines the testing strategy for TTRPG Table, a two-service tabletop RPG session application:

- API: ASP.NET Core 8, Entity Framework Core, SQLite, ASP.NET Identity, JWT auth.
- UI: Nuxt 4, Vue 3, TypeScript, server-side API proxy, polling-based live session state.
- Auth models:
  - Dungeon Masters (DMs) use JWT bearer tokens.
  - Players use session/game-scoped opaque `X-Player-Token` values stored in browser storage.

The goal is to verify correctness, security boundaries, ruleset-driven behavior, session reliability, and user-facing workflows through a balanced test pyramid:

1. Unit tests for deterministic business logic and frontend helpers.
2. Component and composable integration tests for UI state and interaction behavior.
3. API integration tests against realistic ASP.NET Core routing, auth, EF Core, and SQLite behavior.
4. End-to-end (E2E) tests that run the API and UI together and exercise DM/player browser workflows.
5. Targeted non-functional checks for accessibility, security, performance, and production configuration safety.

## 2. Current baseline

Existing tests and commands documented in the repository:

```bash
dotnet test api/tests/NotesApi.Tests/NotesApi.Tests.csproj
npm --prefix ui/src test
```

Existing coverage includes:

- Ruleset validation and import upsert behavior.
- DM-owned game scoping.
- Player join token scoping.
- Combat initiative current-turn tracking.
- Game name uniqueness constraint.
- Cascade deletion for game/session/character/action data.
- Frontend ruleset action chooser helpers.

Known test infrastructure note:

- UI tests use Vitest from `ui/src`.
- The repository guidance notes that Vitest may need Vue plugin support in `vitest.config.ts` before tests that import Vue components can run reliably.

## 3. Testing principles and standards

Use these standards across all test types:

- Prefer behavior-focused tests over implementation-detail tests.
- Use Arrange, Act, Assert structure and one primary behavioral assertion per test.
- Keep unit tests fast, deterministic, and isolated from network, clock, browser storage, and database unless explicitly mocked.
- Keep integration tests realistic by using the actual framework pipeline where possible.
- Use stable selectors for E2E tests, preferably accessible roles/names and explicit `data-testid` attributes only where roles are insufficient.
- Avoid brittle snapshots for complex UI. Use focused assertions on visible state, request payloads, and emitted events.
- Test security and ownership boundaries as first-class behavior.
- Validate both success paths and failure paths for every route and critical interaction.
- Seed only the data each test needs. Do not share mutable state between tests.
- Use generated unique names/codes in integration and E2E tests to avoid cross-test collisions.
- Mock randomness, clocks, timers, and dice rolls where deterministic assertions are needed.
- Add regression tests with every bug fix before changing code when feasible.
- Keep tests readable enough to serve as executable documentation for game/session flows.

## 4. Recommended tooling

### 4.1 API

Current:

- xUnit
- Microsoft.NET.Test.Sdk
- coverlet.collector
- SQLite in-memory tests

Recommended additions:

- `Microsoft.AspNetCore.Mvc.Testing` for `WebApplicationFactory<Program>` API integration tests.
- Explicit test authentication helpers for DM/admin/player-token scenarios.
- SQLite file-per-test or in-memory database per test fixture for realistic relational constraints.
- Optional `FluentAssertions` for clearer assertions if the project accepts the dependency.

### 4.2 UI unit and component tests

Current:

- Vitest

Recommended additions:

- `@vitejs/plugin-vue` for Vue SFC support in tests.
- `@vue/test-utils` for component tests.
- `happy-dom` or `jsdom` for DOM-dependent composables/components.
- `@testing-library/vue` for user-centric component tests where appropriate.
- `vitest-axe` or axe integration for selected accessibility checks.

### 4.3 E2E

Recommended:

- Playwright for cross-browser E2E tests.
- Playwright API helpers to seed users/games/sessions through public APIs.
- Dedicated test environment variables:
  - `ASPNETCORE_ENVIRONMENT=Development` or `Test`
  - isolated SQLite database path
  - strong test JWT key
  - deterministic seeded admin password
  - `NUXT_API_BASE_URL` pointing to the test API

## 5. Test environments

### 5.1 Local developer environment

Purpose:

- Fast feedback before commit.

Commands:

```bash
dotnet test api/tests/NotesApi.Tests/NotesApi.Tests.csproj
npm --prefix ui/src test
```

Recommended E2E local command after Playwright is added:

```bash
npm --prefix ui/src run test:e2e
```

### 5.2 CI environment

Recommended jobs:

1. API restore/build/test with coverage.
2. UI install/build/unit test.
3. E2E smoke suite on pull requests.
4. Full E2E suite on protected branches or release gates.
5. Docker build verification for API and UI images.

Suggested gates:

- API and UI unit/integration tests must pass.
- Nuxt production build must pass.
- API production startup safety tests must pass.
- E2E smoke tests must pass before merge.

### 5.3 Isolated E2E environment

Use:

- Fresh API database per run.
- Fresh browser context per test unless deliberately testing persistence.
- A unique test user per test file or test worker.
- No dependency on existing checked-in or developer-created data.

## 6. Test data strategy

### 6.1 Canonical data fixtures

Create small, explicit fixtures for:

- DM user:
  - valid email and password.
  - second DM for ownership boundary tests.
  - admin user for ruleset import/admin report tests.
- Rulesets:
  - D6 pool ruleset based on Alien RPG-style mechanics.
  - D20 check ruleset based on D&D/Pathfinder-style mechanics.
  - minimal ruleset for validation edge cases.
  - invalid ruleset variants for missing references and malformed JSON.
- Game:
  - one empty game.
  - one game with characters and NPCs.
  - one game with a live exploration session.
  - one game with an active combat session.
- Characters:
  - complete character creation request.
  - existing character available to join.
  - already-in-session character.
  - character with inventory, attributes, skills, and game values.
- NPCs:
  - visible NPC.
  - hidden NPC.
  - NPC with structured stat block and inventory.
- Sessions:
  - active exploration session.
  - active combat session with current turn.
  - ended session with summary data.
- Actions:
  - pending player action.
  - pending NPC action.
  - published action with stat changes.
  - rejected action.
  - withdrawn action.
  - action with roll prompts.

### 6.2 Data isolation

- API unit tests should create a fresh `ApplicationDbContext` per test.
- API integration tests should use a unique SQLite database per test class or per test.
- E2E tests should create a unique DM email and game name per test.
- Do not rely on test order.
- Clean up temporary SQLite files after each test run.

## 7. API unit test plan

### 7.1 Ruleset validation

Test `RulesetDefinitionValidator` and ruleset parsing:

- Accepts valid v1/v2 ruleset definitions.
- Rejects malformed JSON.
- Rejects missing required fields:
  - code
  - display name
  - dice definitions
  - character section
  - actions
- Rejects duplicate keys:
  - dice keys
  - attribute keys
  - skill keys
  - class keys
  - action keys
  - item keys where applicable
- Rejects references to missing:
  - dice
  - attributes
  - skills
  - classes
  - items required by actions
  - starting items
- Rejects invalid numeric values:
  - negative health defaults where unsupported
  - negative starting skill points
  - invalid dice counts
  - invalid success thresholds
- Builds expected `CharacterTemplateJson`.
- Preserves ruleset definition JSON consistently.
- Handles optional sections being absent or empty.
- Handles unknown future fields without breaking if schema allows them.

### 7.2 Character creation

Test `CharacterCreation.Build`:

- Creates a character from a valid ruleset class.
- Requires `classKey` when ruleset has classes.
- Rejects missing or unavailable class.
- Applies default attributes, skills, vitals, and game values.
- Applies valid skill allocations.
- Rejects skill allocations for skills not available to the class.
- Rejects negative skill allocation values.
- Rejects allocations above available starting points.
- Rejects allocations with unknown skill keys.
- Requires starting item when class has starting item options.
- Rejects starting item outside allowed options.
- Applies selected starting item with correct quantity.
- Handles rulesets without classes/items gracefully.
- Produces stable JSON shape for `RulesetDataJson` and `InventoryJson`.

### 7.3 Character inventory

Test `CharacterInventory`:

- Parses valid inventory JSON.
- Treats null, empty, and malformed JSON according to intended behavior.
- Serializes inventory in stable form.
- Rejects unknown item keys.
- Rejects quantities outside allowed range.
- Removes or normalizes zero quantity items consistently.
- Applies positive deltas.
- Applies negative deltas without producing negative quantity.
- Merges duplicate entries or rejects them based on intended invariant.
- Checks `HasItem` for present, absent, zero, and duplicate entries.
- Parses NPC inventory from structured stat blocks.

### 7.4 Action outcome resolution

Test `ActionOutcomeResolver`:

- D6 pool:
  - no roll line returns null.
  - one success passes for "one or more success" rules.
  - zero successes fails.
  - multiple success-count text variants parse correctly.
  - manual roll text parses correctly if supported.
  - irrelevant description text does not produce an outcome.
- D20:
  - natural 20 auto-pass where rules specify.
  - natural 1 auto-fail where rules specify.
  - total >= DC passes.
  - total < DC fails.
  - action-specific DC overrides default DC.
  - ruleset default skill/attribute DC is used when action DC is absent.
  - "vs target AC" uses target armor where available.
  - roll text with modifiers parses correctly.
  - malformed roll text returns null rather than throwing.
- Unknown dice roller returns null.
- Missing action key falls back to appropriate generic rules only when intended.

### 7.5 Controller helpers and response mapping

Test mapping helpers:

- `GameResponse` includes ruleset display name, invite URL, characters, NPCs, and sessions.
- `NpcResponse` includes visibility derived from session visibility map where relevant.
- `SessionStateResponse` includes:
  - current character for player state.
  - filtered/unfiltered action lists as intended for DM vs player.
  - initiative entries ordered by sort order.
  - roll prompts filtered to authorized viewer.
  - combat encounters ordered by sequence.
- `ActionQueueItemResponse` includes follow-up roll prompt state and outcome.
- Session notes responses compute `CanEdit` correctly.

### 7.6 Session timeout service

Test `SessionTimeoutService` logic with a fake clock or extracted timeout policy:

- Active sessions older than timeout are ended.
- Recently updated sessions remain active.
- Ended sessions are ignored.
- Session version increments on timeout if that is intended.
- Active combat encounter is closed when session times out if applicable.
- Service handles empty database without error.
- Service logs and continues after recoverable failures.

## 8. API integration test plan

Use `WebApplicationFactory<Program>` with test configuration and a fresh SQLite database. Exercise real middleware, routing, model validation, authorization, JSON serialization, and EF constraints.

### 8.1 Health and startup

- `GET /health` returns 200 and "Healthy".
- Development startup enables Swagger endpoints.
- Production startup rejects placeholder JWT signing keys.
- Production startup rejects weak/default seeded admin password.
- Valid production-like config starts successfully.
- Database is created on first startup.
- Rulesets seed on first startup.
- Schema update routines are idempotent.

### 8.2 Authentication

Routes:

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/admin/users`

Cases:

- Register valid DM returns user id and email.
- Register duplicate email returns 400.
- Register invalid email returns validation error.
- Register password too short returns validation error.
- Register password without uppercase returns validation error.
- Register password without digit returns validation error.
- Login valid credentials returns JWT and expiry.
- Login wrong password returns 401 with generic error.
- Login unknown user returns 401 with generic error.
- JWT can access authorized DM routes.
- Missing JWT returns 401 on authorized routes.
- Malformed JWT returns 401.
- Expired JWT returns 401.
- Non-admin JWT cannot access admin routes.
- Admin JWT can access admin user report.
- JWT role claims are honored.

### 8.3 Rulesets

Routes:

- `GET /api/rulesets`
- `GET /api/rulesets/{code}`
- `POST /api/rulesets/import`

Cases:

- Public ruleset list returns seeded rulesets.
- Public ruleset detail returns definition JSON and character template JSON.
- Unknown ruleset returns 404.
- Admin import creates new ruleset.
- Admin import updates existing ruleset by code.
- Non-admin import returns 403.
- Missing auth import returns 401.
- Invalid ruleset import returns 400 with validation errors.
- Malformed JSON import returns 400 with validation errors.
- Import preserves games linked to an existing ruleset code.

### 8.4 Games

Routes:

- `GET /api/games`
- `POST /api/games`
- `GET /api/games/{id}`
- `PUT /api/games/{id}`
- `DELETE /api/games/{id}`

Cases:

- Authenticated DM can list only owned games.
- Second DM cannot see another DM's games.
- Create game with valid ruleset succeeds.
- Create game with blank name returns 400.
- Create game with name over max length returns validation error.
- Create game with duplicate name returns 400.
- Create game with unknown ruleset returns 400.
- Created game returns invite code and invite URL.
- Update owned game succeeds.
- Update non-owned game returns 404.
- Update duplicate name returns 400.
- Delete owned game succeeds.
- Delete non-owned game returns 404.
- Delete game cascades sessions, characters, participants, NPCs, actions, roll prompts, notes, initiative, and combat encounters.
- Unauthorized requests return 401.

### 8.5 Game participants and character join

Routes:

- `GET /api/game-participants/join/{inviteCode}`
- `POST /api/game-participants/join/{inviteCode}`

Cases:

- Valid invite returns game name and ruleset options.
- Unknown invite returns 404.
- Join with new valid character returns participant token.
- Join trims and stores character/player names correctly.
- Join rejects blank character name.
- Join rejects duplicate character name in same game.
- Join allows same character name in different game if intended.
- Join rejects invalid class.
- Join rejects missing required class/skill/item data.
- Join rejects invalid skill allocation.
- Join rejects invalid starting item.
- Reopen existing character with valid data returns same or valid new scoped token according to intended behavior.
- Token is scoped to one game and character.
- Concurrent joins with same character name do not create duplicates.

### 8.6 NPC and monster management

Routes:

- `POST /api/games/{gameId}/npcs`
- `PUT /api/games/{gameId}/npcs/{npcId}`
- `DELETE /api/games/{gameId}/npcs/{npcId}`

Cases:

- DM can create NPC with valid data.
- NPC defaults are applied when optional values are omitted.
- Blank name returns validation error.
- Health greater than max health is handled according to intended invariant.
- Negative health, max health, or armor behavior is validated.
- Invalid stat block JSON behavior is validated.
- Update owned NPC succeeds.
- Update NPC from another game returns 404.
- Delete owned NPC succeeds.
- Delete NPC from another game returns 404.
- Deleting NPC removes or preserves historical action references according to intended behavior.

### 8.7 Character inventory management

Route:

- `PUT /api/games/{gameId}/characters/{characterId}/inventory`

Cases:

- DM can update owned character inventory.
- Non-owner cannot update inventory.
- Unknown game/character returns 404.
- Unknown item key returns 400.
- Negative quantity returns validation error.
- Quantity above max returns validation error.
- Zero quantity removes item or is serialized consistently.
- Duplicate items are normalized or rejected consistently.
- Inventory update preserves other character ruleset data.

### 8.8 Session lifecycle

Routes:

- `POST /api/games/{gameId}/sessions`
- `GET /api/sessions/{sessionId}/dm`
- `POST /api/sessions/{sessionId}/state`
- `POST /api/sessions/{sessionId}/stop`
- `GET /api/session-join/{code}`
- `POST /api/session-join/{code}`
- `GET /api/session-join/{code}/state`
- `GET /api/session-join/sessions/{sessionId}/summary`

Cases:

- DM starts a session from an owned game.
- Non-owner cannot start session.
- Starting a session returns join code and join URL.
- Starting multiple active sessions for one game behavior is validated.
- DM can poll full session state.
- Non-owner cannot poll DM state.
- Player can fetch public session join options.
- Player can join session with existing available character.
- Player can create character from session join flow.
- Player cannot join with a character already in the active session.
- Player state requires valid `X-Player-Token`.
- Player with token from different game receives 401.
- Player state includes only intended player-specific data.
- Ended session redirects/summary behavior is supported by API responses.
- Stopping active session marks inactive, sets ended time, increments version.
- Stopping already-ended session is idempotent or returns expected error.
- Changing state accepts only Exploration and Combat.
- Session version increments for state changes, action changes, combat changes, notes changes, and roll prompt changes.
- `sinceSequence` returns expected action filtering for DM poll if supported.

### 8.9 Actions

Routes:

- `GET /api/sessions/{joinCode}/actions`
- `POST /api/sessions/{joinCode}/actions`
- `PUT /api/actions/{actionId}/resolve`
- `PUT /api/actions/{actionId}/reject`
- `DELETE /api/actions/{actionId}`

Cases:

- DM can read actions for an owned session.
- Player can read actions with a valid token.
- Missing or invalid token cannot read player actions.
- Player can submit a valid action.
- DM can submit NPC action with `ActorNpcId`.
- Submitting without any actor returns 400.
- Player cannot spoof another character actor.
- DM cannot use NPC from another game.
- Unknown action key returns 400.
- Action unavailable to actor class returns 400.
- Action requiring missing inventory item returns 400.
- Target character and target NPC IDs are validated.
- Action sequence increments monotonically.
- Concurrent submissions produce unique sequence values.
- Pending action can be withdrawn by owning player.
- Player cannot withdraw another player's action.
- Published/rejected/cancelled action cannot be withdrawn.
- DM can resolve pending action.
- Non-owner DM cannot resolve action.
- Resolve publishes resolution text, roll summary, additional actions, stat changes, and outcome.
- Resolving non-pending action returns 400.
- DM can reject pending action.
- Rejecting non-pending action returns 400.
- Rejected actions are visible with rejection reason/status as intended.
- Action list ordering is stable.

### 8.10 Stat changes on action resolution

Cases:

- Character health delta applies correctly.
- Character set health overrides or combines with delta according to intended order.
- Health lower bound is enforced.
- Health upper bound/max health behavior is validated.
- Armor set applies correctly.
- NPC health and armor changes apply correctly.
- Character game value set applies correctly.
- Character game value delta applies correctly.
- Attribute delta applies correctly.
- Inventory delta applies correctly.
- Unknown target type returns 400.
- Unknown target ID returns 400.
- Stat changes for targets outside the game return 400/404.
- Multiple stat changes in one resolution apply atomically.
- Failed stat changes do not partially publish action.

### 8.11 Combat

Routes:

- `POST /api/sessions/{sessionId}/combat`
- `POST /api/sessions/{sessionId}/combat/advance`

Cases:

- DM can set up combat with characters and NPCs in owned game.
- Non-owner cannot set up combat.
- Unknown combatant returns 400.
- Combatants from another game return 400.
- Empty combatant list behavior is validated.
- Duplicate combatants are rejected or normalized according to intended behavior.
- Initiative ordering is correct.
- Exactly one current turn is set.
- Starting combat creates active combat encounter.
- Reordering initiative preserves current turn or resets as intended.
- Advancing turn cycles through all combatants.
- Advancing from last combatant wraps to first.
- Advancing without initiative returns 400.
- Ending combat changes session state to Exploration and closes active combat encounter.
- Published combat actions link to active combat encounter.
- New combat after ended combat creates a new encounter sequence.

### 8.12 Roll prompts

Routes:

- `POST /api/sessions/{sessionId}/roll-prompts`
- `POST /api/actions/{actionId}/roll-prompts`
- `PUT /api/roll-prompts/{promptId}/submit`
- `DELETE /api/roll-prompts/{promptId}`

Cases:

- DM can create session-level roll prompt.
- DM can create action follow-up roll prompt.
- Prompt requires active session.
- Prompt requires at least one target.
- Target character must belong to game.
- Check mode must be Action, Skill, Attribute, or Custom.
- Result kind must be PassFail or Total.
- Action prompt requires valid action key.
- Skill prompt requires valid skill key.
- Attribute prompt requires valid attribute key.
- Custom prompt requires custom text where intended.
- Player can submit own pending prompt.
- Player cannot submit prompt targeted to another player.
- Missing token returns 401.
- Completed prompt cannot be submitted again.
- Cancelled prompt cannot be submitted.
- DM can cancel pending prompt.
- Non-owner DM cannot cancel prompt.
- Completed prompt cannot be cancelled.
- Prompt submission creates or updates result action according to intended behavior.
- Session version increments on prompt create/submit/cancel.

### 8.13 Session notes

Routes:

- `GET /api/sessions/{sessionId}/session-notes`
- `PUT /api/sessions/{sessionId}/session-notes`
- `GET /api/games/{gameId}/session-notes`
- `PUT /api/games/{gameId}/sessions/{sessionId}/session-notes`
- `GET /api/session-join/{joinCode}/session-notes`
- `PUT /api/session-join/{joinCode}/session-notes`

Cases:

- DM can read active session note context.
- DM can save note while session is active.
- DM cannot edit active note after session has ended through active endpoint.
- DM can edit past note from game dashboard endpoint if intended.
- Player can read own notes with valid player token.
- Player cannot read notes without joining.
- Player cannot read another player's notes.
- Player can save notes while session active.
- Player cannot save notes after session ended.
- Note content max length is enforced.
- Empty note is allowed or rejected according to intended behavior.
- Notes are scoped to owner kind and owner id.
- Previous notes exclude current active note where intended.

### 8.14 NPC visibility

Route:

- `POST /api/sessions/{sessionId}/npc-visibility`

Cases:

- DM can toggle NPC visibility.
- Non-owner cannot toggle visibility.
- Unknown NPC returns error.
- NPC from another game returns error.
- Visibility accepts only Visible or Hidden.
- Player session state includes visible NPCs and excludes hidden NPCs where intended.
- DM session state includes all NPCs with visibility state.
- Visibility state persists across polling.

### 8.15 Nuxt server API proxy

Route:

- `ui/src/server/api/[...path].ts`

Cases:

- Forwards method, path, and query string correctly.
- Forwards allowed headers only:
  - `Accept`
  - `Authorization`
  - `Content-Type`
  - `X-Player-Token`
- Does not forward cookies or unrelated headers.
- Forwards raw body for POST/PUT/DELETE.
- Does not send body for GET/HEAD.
- Preserves API response status and content type.
- Preserves API error payloads.
- Returns 502 when API service is unavailable.

## 9. Frontend unit test plan

### 9.1 API composable

Test `useApi`:

- Loads DM token/email from localStorage on client.
- Does nothing unsafe during SSR/non-client execution.
- Stores token/email through `setSession`.
- Clears token/email through `clearSession`.
- Adds `Authorization` header when token exists.
- Adds `X-Player-Token` header when provided.
- Omits headers when values are absent.
- Sends method and body correctly.
- Wraps `$fetch` failures in `ApiError`.
- `extractError` handles:
  - `errors` as string array.
  - `errors` as model-state object.
  - RFC7807 `detail`.
  - RFC7807 `title`.
  - status message.
  - unknown errors.

### 9.2 Player token composable

Test `usePlayerTokens`:

- Builds stable storage keys for session join code.
- Handles route param arrays by using the first value.
- Builds stable storage keys for game id.
- Reads null when no token exists.
- Stores session player token.
- Stores game player token.
- Stores both session and game tokens in one call.
- Does nothing during SSR/non-client execution.
- Does not overwrite unrelated tokens.

### 9.3 Session polling composable

Test `useSessionPolling` with fake timers:

- Initial status is refreshing.
- `start` triggers immediate refresh on client.
- Successful refresh stores state, clears error, sets live, and schedules next poll.
- In-flight refresh prevents overlapping requests.
- Non-fatal failures set reconnecting on first failure.
- Repeated non-fatal failures set offline.
- Backoff doubles up to max interval.
- 401/403/404 set fatal error and stop polling.
- `stop` clears timer and removes visibility listener.
- Hidden document pauses polling and status becomes paused.
- Visibility restore triggers refresh and resumes schedule.
- `onBeforeUnmount` stops polling.

### 9.4 Toast composable/container

Test `useToast` and `ToastContainer`:

- Success, error, and info toasts are added with unique ids.
- Toasts auto-remove after timeout if implemented.
- Manual dismiss removes only selected toast.
- Multiple toasts render in order.
- ARIA/role behavior is appropriate for alerts/status messages.

### 9.5 Ruleset and dice helpers

Test:

- `parseRulesetDefinition` returns null for missing/invalid data.
- `availableActionsForClass` filters by class.
- `availableActionsForClass` filters by required inventory item.
- `availableSkillsForClass` filters correctly.
- `describeSkillCheck` and `describeAttributeCheck` handle unknown references.
- `describeActionRoll` includes dice, attribute, skill, modifiers, success rule, and damage roll.
- `resolveDiceRollerKey` maps known dice definitions.
- `parsePlayerRollFromDescription` parses D6 and D20 roll descriptions.
- `rollDice` returns values within range.
- `buildRollResult` formats totals.
- Success counting handles empty rolls and target thresholds.
- D6 stress/panic classification handles stress dice and base dice.
- D20 roll context building handles action, skill, attribute, custom, item attack roll, and damage roll.

### 9.6 Action log grouping

Test `groupActionsForDisplay` and related helpers:

- Empty actions return empty groups.
- Exploration actions group together.
- Combat actions group by encounter.
- Multiple combat encounters remain distinct and ordered.
- Skill check responses group by batch.
- Ungrouped skill checks fall back to action order.
- Published, pending, rejected, and cancelled statuses display expected metadata.
- Roll prompt summaries are included.
- Target names and actor names are rendered safely.
- Expand/collapse state composable handles action and group toggles.

### 9.7 Action outcome frontend resolver

Test frontend `resolveActionOutcome`:

- Mirrors backend D6 pass/fail behavior.
- Mirrors backend D20 pass/fail behavior.
- Returns null for DM adjusted final totals where automatic outcome should be skipped.
- Handles missing definition/action/description safely.
- Handles unknown dice roller safely.
- Handles malformed roll text safely.

### 9.8 Inventory and NPC stats helpers

Test:

- `parseInventory` handles valid, empty, null, malformed, and duplicate input.
- `parseNpcInventory` extracts inventory from stat block JSON.
- `hasInventoryItem` respects positive quantity only.
- `inventoryQuantity` returns correct values.
- NPC structured stat detection works.
- NPC attribute and skill lookups return correct values or null.
- JSON display helpers render nested objects, arrays, nulls, booleans, and long strings safely.

## 10. Frontend component and page integration test plan

Use Vue Test Utils or Testing Library with mocked `useApi`, navigation, route params, localStorage, and timers.

### 10.1 Login page

Cases:

- Defaults to sign-in mode.
- Switches to register mode and clears field errors.
- Register mode requires confirm password.
- Password mismatch shows local field error and does not call API.
- Successful register calls register then login.
- Successful login stores session and navigates to `/games`.
- Failed login displays API error and toast.
- Existing token redirects to `/games` on mount.
- Submit button disables while submitting.

### 10.2 Games page

Cases:

- Redirects unauthenticated users to login.
- Loads rulesets and games on mount.
- Defaults create ruleset to first available when configured default is absent.
- Shows empty states for no games.
- Creates game and opens created game.
- Displays duplicate-name/invalid-ruleset errors.
- Opens selected game and tabs.
- Starts session and navigates to DM session page.
- Deletes selected game after confirmation.
- Logs out and clears session.
- Creates, edits, and deletes NPCs.
- Notes tab loads game session notes.
- Loading and saving states disable buttons.

### 10.3 Rulesets page

Cases:

- Redirects unauthenticated users to login.
- Loads rulesets.
- Displays ruleset details and action summaries.
- Admin import form submits definition JSON.
- Import success distinguishes created vs updated.
- Import errors display validation messages.
- Non-admin import failure is surfaced clearly.

### 10.4 Game join page

Cases:

- Loads join options from invite code.
- Displays ruleset character creation requirements.
- Defaults to new character when no existing characters are available.
- Defaults to existing character when available.
- Joining existing character sends character id only.
- Joining new character sends name, player name, class, skill allocations, and starting item.
- Successful join stores game token and navigates to session/player page or game flow as applicable.
- Invalid class/skills/item errors display.
- Existing stored game token supports rejoin where intended.

### 10.5 Session join page

Cases:

- Loads session options from join code.
- Shows ended/not-found errors clearly.
- Existing character join sends selected id.
- New character join validates required fields.
- Stores both session and game player tokens after join.
- Rejoin button navigates to player session page.
- Already-in-session character error is displayed.

### 10.6 Player session page

Cases:

- Redirects to join when no session token exists.
- Falls back from game token to session token when possible.
- Polling loads session state and ruleset definition.
- Fatal polling errors navigate to login/join/ended as appropriate.
- Shows exploration mode action form.
- Shows combat mode current turn and initiative list.
- Shows "your turn" hint only for current character.
- Allows action mode selection:
  - predefined action
  - skill check
  - attribute check
  - custom/free-form
- Dice roller result is prepended to action description.
- Required item actions are unavailable or blocked when missing inventory.
- Submit action posts expected payload and clears form.
- Withdraw pending action sends DELETE and updates UI.
- Cannot withdraw published action.
- Active roll prompt overlay appears.
- Roll prompt submit posts expected payload.
- Session notes panel uses player endpoint and token.
- Published feed groups combat/exploration/skill check actions.
- Ended session navigates to summary.

### 10.7 DM session page

Cases:

- Redirects unauthenticated users to login.
- Polling loads DM session state and ruleset definition.
- Inactive session redirects to summary.
- Copies join link and handles clipboard failure.
- Switches exploration/combat state.
- Sets up combat from characters and visible NPCs.
- Reorders initiative via keyboard and pointer events.
- Advances combat turn.
- Ends combat.
- Stops session and navigates to summary.
- Toggles NPC visibility.
- Creates/edits NPC during session.
- Submits NPC action in action/skill/attribute/custom mode.
- Sends session-level roll prompts.
- Sends action follow-up roll prompts.
- Cancels pending roll prompts.
- Resolves pending action with:
  - resolution text
  - roll summary
  - additional actions
  - health deltas
  - armor changes
  - game value changes
  - attribute changes
  - inventory deltas
- Rejects pending action with optional reason.
- Displays automatic outcome preview from player roll.
- Clears per-action form state after resolve/reject.
- Action log expand/collapse all works.
- Busy states prevent duplicate submissions.

### 10.8 Summary page

Cases:

- DM can view summary with JWT.
- Player can view summary with game player token.
- Missing auth redirects appropriately.
- Active session shows return-to-session link.
- Ended session shows characters, NPCs, initiative, combat encounters, and resolved/unresolved actions.
- Published/pending action counts are correct.
- Expand/collapse details work.

### 10.9 Shared components

Components:

- `SessionNotesPanel`
- `InventoryEditor`
- `CharacterInventoryList`
- `CharacterSheet`
- `NpcSheet`
- `HealthBar`
- `RulesetDiceRoller`
- `D6PoolRoller`
- `D20CheckRoller`
- `DamageRollRoller`
- `ActionTargetPicker`
- `ConfirmModal`
- `PlayerRollPromptOverlay`
- DM and game panel components

Cases:

- Props render expected state.
- User actions emit expected events.
- Required/invalid input states are accessible.
- Disabled states prevent interaction.
- Keyboard interaction works for modals, action buttons, and initiative reorder.
- Health bars handle zero, full, overheal, negative, and missing max values.
- Inventory editor adds, updates, removes, and validates quantities.
- Dice rollers produce deterministic results when random source is mocked.
- Roll prompt overlay traps or manages focus appropriately if modal behavior is expected.

## 11. E2E integration test plan

Use Playwright with the real API and UI services. Tests should use isolated browser contexts for DM and players. Prefer API setup for expensive preconditions, then validate user-visible flows in the browser.

### 11.1 E2E smoke suite

Run on every pull request:

1. App health:
   - API health endpoint returns healthy.
   - UI loads `/login`.
2. DM auth:
   - Register unique DM.
   - Login succeeds.
   - Games page loads.
3. Basic game/session:
   - Create game.
   - Start session.
   - DM session page shows join link.
4. Player join and action:
   - Player opens join link in separate browser context.
   - Player creates character.
   - Player submits action.
   - DM sees pending action.
   - DM resolves action.
   - Player sees published action.
5. End session:
   - DM stops session.
   - DM and player can view summary.

### 11.2 Full DM lifecycle

Flow:

- Register/login DM.
- Create game with a selected ruleset.
- Add NPC with structured stats and inventory.
- Edit NPC health/armor/inventory.
- Toggle NPC visibility.
- Start session.
- Copy session link.
- Save DM session notes.
- Stop session.
- Verify game dashboard shows previous notes and ended session.
- Delete game.
- Verify it no longer appears.

Assertions:

- UI shows success/error toasts.
- API state matches visible UI.
- Deleted game data is inaccessible.
- Join/session links are well-formed.

### 11.3 Player character creation and rejoin

Flow:

- DM creates game and starts session.
- Player opens session link.
- Player creates new character with class, skills, and starting item.
- Player reloads page and remains joined via stored token.
- Player opens a new browser context without token and must join again.
- Player attempts duplicate character name and receives error.
- Player rejoins with existing available character when allowed.

Assertions:

- Token persistence works only within same browser context.
- Duplicate/already-in-session constraints are enforced.
- Character sheet shows correct class, stats, and inventory.

### 11.4 Exploration action resolution

Flow:

- Player submits a free-form exploration action.
- Player submits a skill check with dice roll.
- Player submits an action requiring inventory.
- DM resolves one action with stat changes.
- DM rejects one action.
- Player withdraws one pending action.

Assertions:

- Pending/published/rejected/withdrawn statuses are correct.
- Action sequence ordering is correct.
- Player cannot withdraw after DM resolution.
- Stat changes update character sheet.
- Inventory deltas update inventory display.
- Automatic outcome appears for supported rolls.

### 11.5 Combat flow

Flow:

- DM adds NPC.
- DM starts session and player joins.
- DM enters combat mode.
- DM sets initiative with player character and NPC.
- Player sees initiative list.
- DM advances turns several times.
- Player sees "your turn" only on their turn.
- DM submits NPC action on NPC turn.
- Player submits combat action.
- DM resolves combat action.
- DM ends combat.

Assertions:

- Exactly one current turn is visible.
- Turn order wraps correctly.
- Combat actions are grouped by encounter in DM/player feeds.
- New exploration actions after combat are not grouped into closed combat encounter.

### 11.6 Roll prompt flow

Flow:

- DM sends session-level roll prompt to player.
- Player sees roll prompt overlay.
- Player submits roll.
- DM sees completed prompt/result.
- DM creates action follow-up roll prompt.
- DM cancels another pending prompt.

Assertions:

- Only targeted player sees prompt.
- Prompt cannot be submitted twice.
- Cancelled prompt disappears or shows cancelled state.
- Completed prompt includes submitted roll summary.
- Session/action feed updates after prompt completion if intended.

### 11.7 Session notes flow

Flow:

- DM and player save notes during active session.
- Reload pages.
- Notes persist for each owner.
- Stop session.
- Attempt to edit active-session notes endpoint after end.
- DM edits past note from game dashboard if supported.

Assertions:

- DM notes and player notes are isolated.
- Autosave/debounced save persists content.
- Max length validation is displayed.
- Ended sessions prevent inappropriate edits.

### 11.8 Ruleset import/admin flow

Flow:

- Login as admin.
- Import a new valid ruleset.
- Create game with imported ruleset.
- Import updated definition with same code.
- Verify update appears.
- Login as non-admin DM and attempt import.

Assertions:

- Admin import create/update status is correct.
- Non-admin import is denied.
- Existing games remain linked after ruleset update.
- Invalid import displays validation errors.

### 11.9 Multi-user and authorization boundaries

Flow:

- DM A creates game/session.
- DM B logs in separately.
- DM B attempts to access DM A game/session routes through UI or direct navigation.
- Player A joins game/session.
- Player B joins same session.
- Player B attempts to use Player A token/action/prompt where possible.

Assertions:

- DM B cannot view, mutate, resolve, or delete DM A data.
- Player tokens cannot access other games/sessions.
- Player cannot submit another player's roll prompt.
- Player cannot withdraw another player's action.
- Hidden NPCs are not visible to players but are visible to DM.

### 11.10 Error and recovery E2E

Cases:

- API temporarily unavailable produces user-visible error.
- Polling failure shows reconnecting/offline state.
- Polling resumes after API recovery.
- 401 during DM session redirects to login.
- 404 session redirects to ended/not-found state.
- Browser refresh during live session restores state.
- Back/forward navigation does not duplicate submissions.
- Double-click submit buttons do not create duplicate actions/games/NPCs.

## 12. Edge case checklist by domain

### 12.1 Input validation

- Empty strings.
- Whitespace-only strings.
- Leading/trailing whitespace.
- Strings at max length.
- Strings over max length.
- Unicode names if supported.
- HTML/script content in user-entered text.
- Invalid GUIDs in route params.
- Unknown IDs with valid GUID shape.
- Missing JSON body.
- Malformed JSON body.
- Extra unknown JSON properties.
- Null values for optional fields.
- Null values for required fields.
- Negative numeric values.
- Zero values.
- Very large numeric values.

### 12.2 Auth and ownership

- Missing DM token.
- Invalid DM token.
- Expired DM token.
- Valid token for wrong DM.
- Missing player token.
- Invalid player token.
- Token for wrong game.
- Token for right game but wrong session.
- Admin-only route with non-admin token.
- Player attempting DM-only operations.
- DM attempting operations on player-token-only endpoints if behavior differs.

### 12.3 Concurrency

- Two players join with same character name at same time.
- Two actions submitted at same time.
- DM resolves while player withdraws.
- DM sends roll prompt while action is resolved.
- Player submits roll prompt while DM cancels it.
- DM advances combat while reordering initiative.
- Notes autosave overlaps with manual save or navigation.
- Session stop occurs while player submits action.

### 12.4 Data consistency

- Sequence numbers remain unique and ordered.
- Session version increments on every visible state change.
- Cascades remove dependent data.
- Historical action log remains readable after character/NPC changes.
- Ended sessions are immutable except intended past-note editing.
- Ruleset import update does not corrupt existing games.
- Inventory and stat JSON remain valid after every update.

### 12.5 Browser/client behavior

- Reload page while authenticated.
- Reload page as player.
- Open same session in two tabs.
- LocalStorage unavailable or cleared.
- Clipboard write failure.
- Hidden tab polling pause/resume.
- Slow network response.
- Duplicate form submissions.
- Mobile viewport for player session.
- Keyboard-only navigation.

## 13. Accessibility test plan

Automated checks:

- Run axe on key pages:
  - login
  - games
  - rulesets
  - DM session
  - player session
  - join pages
  - summary

Manual/automated interaction checks:

- All form controls have labels.
- Error messages are associated with fields or announced through alert/status regions.
- Buttons have accessible names.
- Icon-only buttons include `aria-label`.
- Modal dialogs expose role/aria-modal and manage focus.
- Keyboard can:
  - login/register
  - create game
  - join session
  - submit action
  - resolve action
  - reorder initiative
  - dismiss modal/toast
- Focus is not lost after route changes or failed form submissions.
- Color contrast meets WCAG AA for badges, alerts, buttons, and text.
- Player session is usable on mobile viewport.

## 14. Security test plan

Automated and manual checks:

- JWT secret production validation rejects checked-in placeholders.
- Seed admin password production validation rejects weak/default value.
- API proxy forwards only allowlisted headers.
- API responses do not leak stack traces outside development.
- Auth failures use generic messages for login.
- Player tokens are unguessable and scoped.
- CORS allows only configured origins.
- Stored user content is escaped in UI:
  - game names
  - character names
  - NPC names
  - action descriptions
  - resolution text
  - notes
  - ruleset display content
- Large payloads are rejected or handled safely.
- Unauthorized route probing returns 401/403/404 without exposing private data.
- Admin report is admin-only.
- Docker/Kubernetes placeholder secrets are not accepted in production.

## 15. Performance and reliability test plan

API:

- Ruleset list/detail response time with seeded rulesets.
- Session state response time with:
  - many characters
  - many NPCs
  - many actions
  - many roll prompts
  - several combat encounters
- Action submission under concurrent players.
- Combat advance under repeated calls.
- Notes save with large note near max length.
- Database file growth and query behavior for long campaigns.

UI:

- Nuxt production build completes.
- Initial page load for login/games/session pages.
- Player session remains responsive with large action feed.
- Action grouping handles large histories without excessive recomputation.
- Polling backoff reduces request rate during failures.
- Hidden tab pauses polling.
- Dice rollers do not block UI.

Reliability:

- API restart during active session.
- UI refresh during active session.
- Database locked/transient SQLite failure behavior.
- Session timeout service closes stale sessions.

## 16. Regression test priorities

When expanding the suite, prioritize tests in this order:

1. Auth and ownership boundaries for DM/player/admin routes.
2. Full player action lifecycle from submit to resolve/reject/withdraw.
3. Session polling fatal/non-fatal behavior.
4. Combat initiative and encounter grouping.
5. Character creation from rulesets.
6. Roll prompts.
7. Inventory/stat changes.
8. Session notes.
9. Nuxt API proxy header/status behavior.
10. Accessibility smoke checks for core pages.

## 17. Suggested test suite organization

### 17.1 API

Suggested layout:

```text
api/tests/NotesApi.Tests/
  Unit/
    RulesetDefinitionValidatorTests.cs
    CharacterCreationTests.cs
    CharacterInventoryTests.cs
    ActionOutcomeResolverTests.cs
    ResponseMappingTests.cs
  Integration/
    AuthApiTests.cs
    RulesetsApiTests.cs
    GamesApiTests.cs
    ParticipantsApiTests.cs
    SessionsApiTests.cs
    ActionsApiTests.cs
    CombatApiTests.cs
    RollPromptsApiTests.cs
    SessionNotesApiTests.cs
    AdminApiTests.cs
  Support/
    TestApplicationFactory.cs
    TestAuth.cs
    TestData.cs
```

### 17.2 UI

Suggested layout:

```text
ui/src/tests/
  unit/
    use-api.test.ts
    use-player-tokens.test.ts
    use-session-polling.test.ts
    rulesets.test.ts
    dice.test.ts
    inventory.test.ts
    action-log.test.ts
    action-outcome.test.ts
  components/
    LoginPage.test.ts
    GamesPage.test.ts
    PlayerSessionPage.test.ts
    DmSessionPage.test.ts
    SessionNotesPanel.test.ts
    InventoryEditor.test.ts
    DiceRollers.test.ts
  e2e/
    auth-and-game.spec.ts
    player-action-lifecycle.spec.ts
    combat.spec.ts
    roll-prompts.spec.ts
    notes.spec.ts
    authorization.spec.ts
```

## 18. Coverage expectations

Use coverage as a signal, not the only quality gate.

Recommended targets:

- API domain/unit logic: high line and branch coverage for rulesets, character creation, inventory, action outcome, auth helpers, and mappings.
- API integration: route-level coverage for every public endpoint, including success and authorization failure paths.
- UI unit/composable: high coverage for composables and pure helpers.
- UI components/pages: cover critical user-visible state transitions and form submissions.
- E2E: cover representative happy paths and critical failure/authorization paths rather than every edge case.

Coverage reports should exclude:

- generated Nuxt files.
- build outputs.
- static JSON fixture files unless specifically validating schemas.
- trivial DTO/property-only classes where behavior is covered by integration tests.

## 19. Definition of done for new features

Every feature should include:

- Unit tests for new pure logic.
- API integration tests for new or changed endpoints.
- UI component/composable tests for new client state transitions.
- E2E coverage for new critical user journeys or auth-sensitive behavior.
- Negative tests for validation and authorization failures.
- Accessibility checks for new interactive UI.
- Documentation update if commands, environment variables, or workflows changed.

## 20. CI command checklist

Minimum:

```bash
dotnet test api/tests/NotesApi.Tests/NotesApi.Tests.csproj
npm --prefix ui/src test
npm --prefix ui/src run build
```

Recommended after E2E tooling is added:

```bash
npm --prefix ui/src run test:e2e
```

Recommended Docker validation:

```bash
docker build -t ttrpg-api:test .
docker build -t ttrpg-ui:test ui/src
```

## 21. Open implementation recommendations

- Add Vue SFC test support to Vitest before expanding frontend component tests.
- Add API integration fixtures based on `WebApplicationFactory<Program>`.
- Add Playwright with a small smoke suite first, then expand into full workflows.
- Add stable accessible names or `data-testid` attributes where E2E selectors would otherwise be brittle.
- Consider extracting clock/random abstractions for easier deterministic tests around session timeout, polling, invite code generation, join code generation, JWT expiry, and dice rolls.
- Consider a `Test` ASP.NET Core environment that uses safe deterministic config and an isolated database.
