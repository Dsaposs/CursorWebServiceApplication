import { test, expect } from '@playwright/test';
import { ApiClient, createSessionFixture, loginAdmin } from '../helpers/api-client';
import { E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD, uniqueLabel } from '../helpers/test-data';
import { seedAdminAuth, seedPlayerToken } from '../helpers/ui-flows';

const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5294';

test.describe('Session lifecycle', () => {
  test('DM can stop an active session from the DM screen', async ({ page }) => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);

    await seedAdminAuth(page);
    await page.goto(`/sessions/${fixture.sessionId}/dm`);

    await page.getByRole('button', { name: 'Stop Session' }).first().click();
    await page.getByRole('dialog').getByRole('button', { name: 'Stop Session' }).click();
    await expect(page).toHaveURL(new RegExp(`/sessions/${fixture.sessionId}/summary$`), { timeout: 45_000 });
  });

  test('API stop marks the session inactive', async () => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const client = new ApiClient({ apiUrl, token });

    const stopped = await client.stopSession(fixture.sessionId);
    expect(stopped.isActive).toBe(false);
  });

  test('player is redirected when the session ends', async ({ page }) => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const client = new ApiClient({ apiUrl, token });

    const joined = await client.joinSession(fixture.joinCode, uniqueLabel('Survivor'), uniqueLabel('Player'));
    await client.stopSession(fixture.sessionId);

    await seedPlayerToken(page, fixture.joinCode, joined.participantToken, fixture.gameId);
    await page.goto(`/sessions/${fixture.joinCode}/player`);
    await expect(page).toHaveURL(/\/sessions\/(ended|[0-9a-f-]+\/summary)/, { timeout: 60_000 });
  });
});
