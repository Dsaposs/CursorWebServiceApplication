import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.PERF_UI_URL ?? process.env.E2E_BASE_URL ?? 'http://localhost:3000';
const apiURL = process.env.PERF_API_URL ?? process.env.E2E_API_URL ?? 'http://localhost:5294';

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 180_000,
  reporter: [['list']],
  globalSetup: './global-setup.ts',
  use: {
    baseURL,
    trace: 'off',
    screenshot: 'off',
    video: 'off',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  metadata: { apiURL },
});
