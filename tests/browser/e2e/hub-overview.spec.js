const { test, expect } = require('@playwright/test');
const { randomUUID } = require('node:crypto');

test('workspace overview searches, filters, changes view and opens management in context', async ({ page }) => {
  const id = randomUUID();
  await page.goto('/Account/Register');
  await page.getByLabel('Organization name', { exact: true }).fill(`Overview ${id}`);
  await page.getByLabel('Email', { exact: true }).fill(`overview-${id}@example.test`);
  await page.getByLabel('Password', { exact: true }).fill('Test-Only-Password42!');
  await page.getByLabel('Confirm Password', { exact: true }).fill('Test-Only-Password42!');
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await expect(page).toHaveURL(/\/workspaces$/);
  await expect(page.locator('.hub-shell')).toHaveAttribute('data-interactive', 'true');

  async function create(kind, name) {
    await page.getByRole('button', { name: `New ${kind}`, exact: true }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('textbox').fill(name);
    await dialog.getByRole('button', { name: 'Create', exact: true }).click();
    await expect(dialog).toBeHidden();
  }
  async function overview() {
    await page.goto('/workspaces');
    await expect(page.locator('.hub-shell')).toHaveAttribute('data-interactive', 'true');
  }

  await create('workspace', 'Zulu team');
  const workspaceUrl = page.url();
  await create('project', 'Zulu project');
  await page.goto(workspaceUrl);
  await expect(page.locator('.hub-shell')).toHaveAttribute('data-interactive', 'true');
  await create('project', 'Alpha project');
  await overview();
  await create('workspace', 'Alpha team');
  await overview();

  await page.getByRole('button', { name: 'Open navigation', exact: true }).click();
  const navigation = page.locator('details.global-navigation');
  await expect(navigation).toHaveAttribute('open', '');
  await navigation.getByRole('link', { name: 'Workspaces', exact: true }).click();
  await expect(navigation).not.toHaveAttribute('open', '');
  await page.getByRole('button', { name: 'Open navigation', exact: true }).click();
  await page.locator('.app-header .help-link').click();
  await expect(page).toHaveURL(/\/help$/);
  await expect(navigation).not.toHaveAttribute('open', '');
  await page.goBack();
  await expect(page.locator('.hub-shell')).toHaveAttribute('data-interactive', 'true');

  const cards = page.locator('.workspace-grid').getByRole('article');
  await expect(cards.getByRole('heading')).toHaveText(['Alpha team', 'Zulu team']);
  await page.getByLabel('Sort workspaces').selectOption('projects');
  await expect(cards.getByRole('heading')).toHaveText(['Zulu team', 'Alpha team']);
  await expect(cards.first()).toContainText('2 projects');
  await page.getByRole('button', { name: 'List', exact: true }).click();
  await expect(page.getByRole('button', { name: 'List', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('.workspace-list')).toBeVisible();
  await page.getByLabel('Search workspaces').fill('alpha');
  await expect(cards).toHaveCount(1);
  await expect(cards.getByRole('heading')).toHaveText('Alpha team');
  await page.getByLabel('Search workspaces').fill('');
  await page.getByLabel('Filter workspaces').selectOption('shared');
  await expect(page.getByRole('heading', { name: 'No matching workspaces' })).toBeVisible();
  await page.getByLabel('Filter workspaces').selectOption('own');
  await expect(cards).toHaveCount(2);
  await page.getByRole('button', { name: 'Grid', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Grid', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('.workspace-list')).toHaveCount(0);
  await page.screenshot({ path: 'artifacts/browser/hub-overview-preview.png' });
  await cards.filter({ has: page.getByRole('heading', { name: 'Zulu team', exact: true }) }).getByRole('link', { name: 'Manage', exact: true }).click();
  await expect(page).toHaveURL(/\?section=members$/);
  await expect(page.getByRole('button', { name: /^Members/ })).toHaveClass('active');
  await expect(page.locator('.app-header').getByRole('navigation', { name: 'Breadcrumb' })).toContainText('Zulu team');
  await page.getByRole('button', { name: 'Projects', exact: true }).click();
  await expect(page).toHaveURL(workspaceUrl);
  await page.reload();
  await expect(page.locator('.hub-shell')).toHaveAttribute('data-interactive', 'true');
  await expect(page.getByRole('button', { name: 'Projects', exact: true })).toHaveClass('active');
  const projects = page.getByRole('table', { name: 'Projects', exact: true });
  await expect(projects.getByRole('row').nth(1)).toContainText('Alpha project');
  await expect(projects.getByRole('row').nth(2)).toContainText('Zulu project');
  await page.getByLabel('Search files').fill('missing');
  await expect(page.getByRole('heading', { name: 'No matching projects' })).toBeVisible();
});
