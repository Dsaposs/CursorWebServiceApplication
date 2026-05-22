import { test, expect } from '@playwright/test';
import { ApiClient, createSessionFixture, loginAdmin } from '../helpers/api-client';
import { E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD, uniqueLabel } from '../helpers/test-data';
import { joinSessionAsNewCharacter, seedAdminAuth } from '../helpers/ui-flows';

const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5294';

test.describe('Player join flow', () => {
  test('new player can create a character and enter the session', async ({ page }) => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);

    const characterName = uniqueLabel('Scientist');
    const playerName = uniqueLabel('Player');

    await joinSessionAsNewCharacter(page, fixture.joinCode, characterName, playerName);

    await expect(page.getByRole('heading', { name: 'Actions' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Take Action' })).toBeVisible();
    await expect(page.locator('strong').filter({ hasText: characterName })).toBeVisible();
  });

  test('API join creates a participant token', async () => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const client = new ApiClient({ apiUrl, token });

    const joined = await client.joinSession(fixture.joinCode, uniqueLabel('Marine'), uniqueLabel('Player'));
    expect(joined.participantToken.length).toBeGreaterThan(10);
    expect(joined.character.classKey).toBe('scientist');
  });

  test('DM games hub shows join code after session start', async ({ page }) => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);

    await seedAdminAuth(page);
    await page.goto('/games');
    await page.getByRole('button', { name: fixture.gameName }).click();
    await expect(page.getByText(fixture.joinCode)).toBeVisible();
  });
});
