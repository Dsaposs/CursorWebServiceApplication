import { ApiClient } from './helpers/api-client';
import { writeAdminAuthCache } from './helpers/auth-cache';
import { E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD } from './helpers/test-data';
import { waitForStack } from './helpers/wait-for-stack';

export default async function globalSetup() {
  const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5294';

  await waitForStack({
    apiUrl,
    uiUrl: process.env.E2E_BASE_URL ?? 'http://localhost:3000',
    mobileUrl: process.env.E2E_MOBILE_BASE_URL ?? 'http://localhost:3001',
    timeoutMs: Number(process.env.E2E_WAIT_TIMEOUT_MS ?? 180_000),
  });

  const auth = await ApiClient.login(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
  writeAdminAuthCache({ token: auth.token, email: E2E_ADMIN_EMAIL });
}
