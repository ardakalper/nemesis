import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  timeout: 120_000,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:5180/',
    ...devices['Desktop Chrome'],
    viewport: { width: 1280, height: 900 },
  },
  webServer: {
    command: 'node serve.mjs ../publish/wwwroot 5180',
    url: 'http://localhost:5180/',
    reuseExistingServer: !process.env.CI,
  },
});
