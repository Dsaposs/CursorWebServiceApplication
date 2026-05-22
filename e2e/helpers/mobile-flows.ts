import { expect, type Page } from '@playwright/test';
import { readAdminAuthCache } from './auth-cache';
import { E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD } from './test-data';

export async function seedMobileAdminAuth(page: Page, email = E2E_ADMIN_EMAIL, token?: string) {
  const cached = readAdminAuthCache();
  const authToken = token ?? cached?.token;
  if (!authToken) {
    throw new Error('Admin auth token missing. Global setup should seed e2e/.auth/admin-token.json.');
  }

  await page.addInitScript(({ storedToken }) => {
    localStorage.setItem('ttrpg_token', storedToken);
  }, { storedToken: authToken });
}

async function fillIonInput(page: Page, label: string, value: string) {
  const item = page.locator('ion-item').filter({ has: page.locator('ion-label', { hasText: label }) });
  const input = item.locator('input, textarea').first();
  await input.fill(value, { force: true });
  // IonInput v-model does not always update from fill(); emit ionInput so Vue refs sync.
  await input.evaluate((el, text) => {
    const node = el as HTMLInputElement;
    node.value = text;
    node.dispatchEvent(new CustomEvent('ionInput', { bubbles: true, composed: true, detail: { value: text } }));
    node.dispatchEvent(new Event('input', { bubbles: true }));
  }, value);
}

export async function loginMobileThroughUi(
  page: Page,
  email = E2E_ADMIN_EMAIL,
  password = E2E_ADMIN_PASSWORD,
) {
  await page.goto('/login');
  await fillIonInput(page, 'Email', email);
  await fillIonInput(page, 'Password', password);
  await page.locator('ion-button').filter({ hasText: 'Sign In' }).click({ force: true });
  await expect(page).toHaveURL(/\/home$/);
}

export async function joinMobileSession(page: Page, joinCode: string, displayName: string) {
  await page.goto('/join');
  await fillIonInput(page, 'Session Code', joinCode);
  await fillIonInput(page, 'Your Name', displayName);
  await page.locator('ion-button').filter({ hasText: 'Join Session' }).click({ force: true });
  await expect(page).toHaveURL(new RegExp(`/session/${joinCode}$`, 'i'));
}

export async function submitMobileAction(page: Page, actionText: string, flavourText?: string) {
  await page.locator('ion-button').filter({ hasText: '+ Act' }).click({ force: true });
  await page.locator('ion-textarea').first().locator('textarea').fill(actionText, { force: true });
  if (flavourText) {
    await page.locator('ion-textarea').nth(1).locator('textarea').fill(flavourText, { force: true });
  }
  await page.locator('ion-button').filter({ hasText: 'Submit' }).click({ force: true });
  await expect(page.getByText(actionText)).toBeVisible({ timeout: 45_000 });
}
