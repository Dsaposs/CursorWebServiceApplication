import { test, expect } from '@playwright/test';
import { createSessionFixture, loginAdmin } from '../../helpers/api-client';
import { E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD, uniqueLabel } from '../../helpers/test-data';
import { joinMobileSession, seedMobileAdminAuth, submitMobileAction } from '../../helpers/mobile-flows';

const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5294';

test.describe('Mobile app', () => {
  test('login page renders', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Sign In' })).toBeVisible();
  });

  test('seeded admin token loads games on home', async ({ page }) => {
    await seedMobileAdminAuth(page);
    await page.goto('/home');
    await expect(page.getByText('My Games')).toBeVisible();
  });

  test('authenticated home lists DM games', async ({ page }) => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);

    await seedMobileAdminAuth(page);
    await page.goto('/home');
    await expect(page.getByText(fixture.gameName)).toBeVisible({ timeout: 45_000 });
  });

  test('sign out returns to login', async ({ page }) => {
    await seedMobileAdminAuth(page);
    await page.goto('/home');
    await page.locator('ion-button').filter({ hasText: 'Sign Out' }).click({ force: true });
    await expect(page).toHaveURL(/\/login$/);
  });

  test('player can join a session and submit an action', async ({ page }) => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const playerName = uniqueLabel('MobilePlayer');
    const actionText = uniqueLabel('Scan the corridor');

    await joinMobileSession(page, fixture.joinCode, playerName);
    await expect(page.getByText(/Live|Offline/)).toBeVisible();
    await submitMobileAction(page, actionText);
  });
});
