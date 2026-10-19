import { defineConfig, devices } from '@playwright/test';

/** The ingress of the k3d cluster; override to point at another deployment. */
const baseURL = process.env['BASE_URL'] ?? 'http://invoiceflow.localhost:8080';

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL,
    // The app picks its language from the browser; the tests read English texts.
    locale: 'en-GB',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
