import { test, expect } from '@playwright/test';
import { loginAdmin } from '../helpers/api-client';
import { E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD, uniqueLabel } from '../helpers/test-data';
import { createGameAndStartSession, loginThroughUi } from '../helpers/ui-flows';

const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5294';

test.describe('Authentication and games hub', () => {
  test('seeded admin can sign in and reach the games hub', async ({ page }) => {
    await loginThroughUi(page);
    await expect(page.getByRole('heading', { name: 'My Games' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Rulesets' })).toBeVisible();
  });

  test('DM can create a game and open the live session screen', async ({ page }) => {
    await loginThroughUi(page);
    const gameName = uniqueLabel('E2E Game');
    await createGameAndStartSession(page, gameName);
    await expect(page.locator('strong').filter({ hasText: 'DM Screen' })).toBeVisible();
    await expect(page.getByText('Exploration')).toBeVisible();
    await expect(page.getByRole('heading', { name: /Pending Actions/i })).toBeVisible();
  });

  test('API login returns a bearer token for the seeded admin', async () => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    expect(token.length).toBeGreaterThan(20);
  });
});
