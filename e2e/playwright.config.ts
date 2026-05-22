import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:3000';
const mobileBaseURL = process.env.E2E_MOBILE_BASE_URL ?? 'http://localhost:3001';
const apiURL = process.env.E2E_API_URL ?? 'http://localhost:5294';

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 2 : 2,
  timeout: 120_000,
  expect: { timeout: 20_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  globalSetup: './global-setup.ts',
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 15_000,
  },
  projects: [
    {
      name: 'chromium',
      testIgnore: /mobile\//,
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'mobile-chromium',
      testMatch: /mobile\/.*\.spec\.ts/,
      use: {
        ...devices['Pixel 5'],
        baseURL: mobileBaseURL,
      },
    },
  ],
  metadata: {
    apiURL,
    mobileBaseURL,
  },
});
