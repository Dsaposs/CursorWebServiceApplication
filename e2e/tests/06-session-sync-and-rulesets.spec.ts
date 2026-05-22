import { test, expect } from '@playwright/test';
import { ApiClient, createSessionFixture, loginAdmin } from '../helpers/api-client';
import { E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD } from '../helpers/test-data';
import { seedAdminAuth } from '../helpers/ui-flows';

const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5294';

test.describe('Session sync API', () => {
  test('version and live endpoints return incremental session data', async () => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const client = new ApiClient({ apiUrl, token });

    const version = await client.getSessionVersion(fixture.sessionId);
    expect(version.version).toBeGreaterThan(0);

    const joined = await client.joinSession(fixture.joinCode, `Sync-${Date.now()}`, 'Sync Player');
    const player = new ApiClient({ apiUrl, playerToken: joined.participantToken });
    await player.submitPlayerAction(fixture.joinCode, 'Listen at the door');

    const live = await client.getSessionLive(fixture.sessionId, version.version > 0 ? 0 : 0) as {
      version: number;
      actions: Array<{ status: string }>;
    };

    expect(live.version).toBeGreaterThanOrEqual(version.version);
    expect(live.actions.some(action => action.status === 'Pending')).toBeTruthy();
  });

  test('player version endpoint requires participant token', async ({ request }) => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);

    const unauthorized = await request.get(`${apiUrl}/api/session-join/${fixture.joinCode}/version`);
    expect(unauthorized.status()).toBe(401);

    const joined = await new ApiClient({ apiUrl, token }).joinSession(
      fixture.joinCode,
      `Version-${Date.now()}`,
      'Version Player',
    );

    const authorized = await request.get(`${apiUrl}/api/session-join/${fixture.joinCode}/version`, {
      headers: { 'X-Player-Token': joined.participantToken },
    });
    expect(authorized.ok()).toBeTruthy();
  });

  test('rulesets list is available through the UI proxy', async ({ page }) => {
    await seedAdminAuth(page);
    await page.goto('/rulesets');
    await expect(page).toHaveURL(/\/rulesets$/);
    await expect(page.getByText(/Alien|D&D|ruleset/i).first()).toBeVisible();
  });
});
