const { defineConfig, devices } = require('@playwright/test');
const { mkdirSync } = require('node:fs');
const path = require('node:path');
const { randomUUID } = require('node:crypto');

// The suite always owns its local server and a new database. Never reuse a developer session.
const dataDirectory = path.resolve('artifacts/browser/databases');
mkdirSync(dataDirectory, { recursive: true });
const port = Number(process.env.SIMPLEXFLOW_TEST_PORT || 5187);
const baseURL = `http://127.0.0.1:${port}`;
const configuration = process.env.SIMPLEXFLOW_TEST_CONFIGURATION || 'Release';

module.exports = defineConfig({
  testDir: './tests/browser/e2e',
  outputDir: 'artifacts/browser/e2e-results',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,
  timeout: 60000,
  expect: { timeout: 15000 },
  reporter: [['list'], ['html', { outputFolder: 'artifacts/browser/e2e-report', open: 'never' }]],
  use: {
    ...devices['Desktop Chrome'], baseURL,
    trace: 'retain-on-failure', screenshot: 'only-on-failure'
  },
  webServer: {
    command: `dotnet run --project src/SimpleXflow.Web --configuration ${configuration} --no-build --no-launch-profile`,
    url: `${baseURL}/healthz/db`,
    reuseExistingServer: false,
    timeout: 120000,
    env: {
      ASPNETCORE_ENVIRONMENT: 'Development',
      ASPNETCORE_URLS: baseURL,
      Database__Provider: 'Sqlite',
      Email__Smtp__Host: '',
      Email__Smtp__From: '',
      ConnectionStrings__DefaultConnection: `Data Source=${path.join(dataDirectory, `${randomUUID()}.db`)}`,
      Logging__LogLevel__Default: 'Warning'
    }
  }
});
