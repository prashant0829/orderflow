import { defineConfig, devices } from '@playwright/test'

const gateway = process.env.VITE_API_URL ?? 'http://localhost:5100'

// These tests drive the real UI against the real backend, so the whole system must be running first:
//   FakePaymentGateway__DeclineRate=0 FakePaymentGateway__TransientFailureRate=0 RateLimit__Auth__Limit=1000 scripts/dev-up.sh
// (no random payment failures, and a login limit high enough for a suite that signs in many times; the limit itself is tested by scripts/e2e-security.sh)
export default defineConfig({
  testDir: './e2e',
  timeout: 90_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : [['list']],
  globalSetup: './e2e/global-setup.ts',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    extraHTTPHeaders: {},
  },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'] } },
    { name: 'phone', use: { ...devices['Pixel 7'] }, testMatch: /layout\.spec\.ts/ },
  ],
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: true,
    env: { VITE_API_URL: gateway },
  },
})
