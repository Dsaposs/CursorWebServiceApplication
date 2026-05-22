import { test, expect } from '@playwright/test';
import { ApiClient } from '../helpers/api-client';
import { E2E_VALID_PASSWORD, uniqueEmail } from '../helpers/test-data';
import { registerThroughUi, seedAdminAuth } from '../helpers/ui-flows';

const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5294';

test.describe('User registration', () => {
  test.describe.configure({ mode: 'serial' });

  test('new DM can register through the UI and reach the games hub', async ({ page }) => {
    const email = uniqueEmail('register-ui');
    await registerThroughUi(page, email, E2E_VALID_PASSWORD);
    await expect(page.getByRole('heading', { name: 'My Games' })).toBeVisible();
  });

  test('register API rejects duplicate email', async () => {
    const email = uniqueEmail('register-dup');
    await ApiClient.register(apiUrl, email, E2E_VALID_PASSWORD);
    await expect(ApiClient.register(apiUrl, email, E2E_VALID_PASSWORD)).rejects.toThrow(/400|already exists/i);
  });

  test('password mismatch shows client error when passwords do not match', async ({ page }) => {
    await page.goto('/login');
    await page.getByRole('button', { name: 'Register' }).click();
    await page.getByLabel('Email').fill(uniqueEmail('mismatch'));
    await page.getByLabel('Password', { exact: true }).fill(E2E_VALID_PASSWORD);
    await page.getByLabel('Confirm password').fill('Different1');
    await page.getByRole('button', { name: 'Create Account & Sign In' }).click();
    await expect(page.getByText('Passwords do not match.')).toBeVisible();
  });

  test('authenticated user visiting login is redirected to games hub', async ({ page }) => {
    await seedAdminAuth(page);
    await page.goto('/login');
    await expect(page).toHaveURL(/\/games$/);
  });
});
