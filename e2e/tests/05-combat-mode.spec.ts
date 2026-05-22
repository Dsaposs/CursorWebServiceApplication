import { test, expect } from '@playwright/test';
import { ApiClient, createSessionFixture, loginAdmin } from '../helpers/api-client';
import { E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD, uniqueLabel } from '../helpers/test-data';
import { seedAdminAuth, seedPlayerToken } from '../helpers/ui-flows';

const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5294';

test.describe('Combat mode', () => {
  test('DM can start combat and see initiative order', async ({ page }) => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const client = new ApiClient({ apiUrl, token });
    await client.joinSession(fixture.joinCode, uniqueLabel('Marine'), uniqueLabel('Player'));

    await seedAdminAuth(page);
    await page.goto(`/sessions/${fixture.sessionId}/dm`);

    await page.getByRole('button', { name: 'Start combat' }).click();
    await expect(page.getByText(/Round \d+/)).toBeVisible({ timeout: 45_000 });
    await expect(page.locator('.initiative-list .initiative-item').first()).toBeVisible();
  });

  test('combat start API rolls initiative for active session', async () => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const client = new ApiClient({ apiUrl, token });

    await client.joinSession(fixture.joinCode, uniqueLabel('Pilot'), uniqueLabel('Player'));
    await client.setSessionMode(fixture.sessionId, 'Combat');
    await client.startCombat(fixture.sessionId);

    const state = await client.getDmSession(fixture.sessionId) as {
      state: string;
      initiative: Array<{ combatantName: string }>;
    };

    expect(state.state).toBe('Combat');
    expect(state.initiative.length).toBeGreaterThan(0);
  });

  test('player sees combat banner when session enters combat', async ({ page }) => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const client = new ApiClient({ apiUrl, token });

    const joined = await client.joinSession(fixture.joinCode, uniqueLabel('Roughneck'), uniqueLabel('Player'));
    await client.setSessionMode(fixture.sessionId, 'Combat');
    await client.startCombat(fixture.sessionId);

    await seedPlayerToken(page, fixture.joinCode, joined.participantToken, fixture.gameId);
    await page.goto(`/sessions/${fixture.joinCode}/player`);
    await expect(page.getByText('Combat is active')).toBeVisible({ timeout: 45_000 });
  });
});
