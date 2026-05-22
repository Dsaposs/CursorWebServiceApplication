import { test, expect } from '@playwright/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5294';

test.describe('Smoke', () => {
  test('API health endpoint responds', async ({ request }) => {
    const response = await request.get(`${apiUrl}/health`);
    expect(response.ok()).toBeTruthy();
    await expect(response.text()).resolves.toMatch(/healthy/i);
  });

  test('UI login page loads', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByRole('heading', { name: 'TTRPG Table' })).toBeVisible();
    await expect(page.getByLabel('Email')).toBeVisible();
    await expect(page.locator('form').getByRole('button', { name: 'Sign In' })).toBeVisible();
  });

  test('Swagger UI is reachable on the API', async ({ request }) => {
    const response = await request.get(`${apiUrl}/swagger/index.html`);
    expect(response.ok()).toBeTruthy();
  });
});
