const { test: base, expect } = require('@playwright/test');
const { randomUUID } = require('node:crypto');
const fs = require('node:fs/promises');
const path = require('node:path');
const password = 'Test-Only-Password42!';
const modelName = page => page.getByLabel('Model name', { exact: true });

const test = base.extend({
  page: async ({ page }, use) => {
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await use(page);
    expect(errors, 'No uncaught browser errors').toEqual([]);
  }
});

async function register(page) {
  const id = randomUUID();
  const email = `browser-${id}@example.test`;
  await page.goto('/Account/Register');
  await page.getByLabel('Organization name', { exact: true }).fill(`Browser ${id}`);
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByLabel('Confirm Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await expect(page).toHaveURL(/\/workspaces$/);
  await expect(page.getByRole('heading', { name: 'Your first workspace' })).toBeVisible();
  await expect(page.locator('.hub-shell')).toHaveAttribute('data-interactive', 'true');
  return email;
}
async function create(page, kind, name) {
  await page.getByRole('button', { name: `New ${kind}`, exact: true }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('textbox').fill(name);
  await dialog.getByRole('button', { name: 'Create', exact: true }).click();
  await expect(dialog).toBeHidden();
}
async function setupProject(page) {
  await register(page);
  await create(page, 'workspace', 'Team workspace');
  await create(page, 'project', 'Simulation project');
  return page.url();
}
async function editorReady(page) {
  await expect(page.locator('#simplexflow-editor')).toHaveClass(/is-ready/);
  await expect(page.locator('#js-canvas .djs-shape').first()).toBeVisible();
  await expect(page.locator('.modeler-warning')).toHaveCount(0);
  // Geometry must be in the visible viewport, without toggling full screen.
  await expect.poll(async () => {
    const canvas = await page.locator('#js-canvas').boundingBox();
    const shape = await page.locator('#js-canvas .djs-shape').first().boundingBox();
    return !!(canvas && shape && shape.width > 0 && shape.x < canvas.x + canvas.width && shape.x + shape.width > canvas.x && shape.y < canvas.y + canvas.height && shape.y + shape.height > canvas.y);
  }).toBeTruthy();
}
async function newModel(page, name = 'Test model') {
  await create(page, 'model', name);
  await expect(page).toHaveURL(/\/projects\/[a-f0-9-]{36}$/);
  await editorReady(page);
  return page.url();
}
async function more(page) {
  const menu = page.locator('details.model-actions');
  if (!await menu.getAttribute('open').then(value => value !== null)) await menu.locator('summary').click();
}
async function saveRename(page, name) {
  await modelName(page).fill(name);
  await expect(page.locator('.autosave-indicator')).toContainText('Saved');
  await expect.poll(() => page.locator('.autosave-indicator').textContent()).not.toContain('failed');
  // Switching through history flushes the pending debounced save before returning.
  await page.getByRole('button', { name: 'History & work' }).click();
  await expect(page.locator('.collaboration-drawer')).toBeVisible();
  await page.getByRole('button', { name: 'Close collaboration' }).click();
}
async function expectDiagram(page, elementId, label) {
  await editorReady(page);
  const shape = page.locator(`#js-canvas .djs-shape[data-element-id="${elementId}"]`);
  await expect(shape).toBeVisible();
  await expect(page.locator('#js-canvas .djs-label').filter({ hasText: label }).first()).toBeVisible();
  await expect.poll(async () => {
    const canvas = await page.locator('#js-canvas').boundingBox();
    const bounds = await shape.boundingBox();
    return !!(canvas && bounds && bounds.width > 0 && bounds.x >= canvas.x && bounds.y >= canvas.y && bounds.x + bounds.width <= canvas.x + canvas.width && bounds.y + bounds.height <= canvas.y + canvas.height);
  }, { message: 'The selected model is drawn inside its visible canvas' }).toBeTruthy();
}
async function editDiagramLabel(page, elementId, name) {
  // Use the editor's hit target: group bounds can include the label, while .djs-visual is covered by it.
  await page.locator(`#js-canvas .djs-element[data-element-id="${elementId}"] .djs-hit`).first().dblclick();
  const label = page.locator('.djs-direct-editing-content');
  await label.fill(name);
  await label.press('Control+Enter');
  await expect(label).toBeHidden();
}
async function projectBreadcrumb(page, name = 'Simulation project') {
  await page.locator('.app-header').getByRole('navigation', { name: 'Breadcrumb' }).getByRole('link', { name, exact: true }).click();
  await expect(page.locator('.hub-shell')).toHaveAttribute('data-interactive', 'true');
}

test('anonymous workspaces require login and accounts start with zero workspaces', async ({ page }) => {
  await page.goto('/workspaces');
  await expect(page).toHaveURL(/\/Account\/Login\?ReturnUrl=/);
  await expect(page.getByRole('heading', { name: 'Welcome back' })).toBeVisible();
  const email = await register(page);
  await page.locator('.account-menu summary').click();
  await page.getByRole('button', { name: 'Logout', exact: true }).click();
  await expect(page).toHaveURL(/\/$/);
  await page.goto('/workspaces');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Password', { exact: true }).fill('Incorrect-Password42!');
  await page.getByRole('button', { name: 'Log in', exact: true }).click();
  await expect(page.getByText('Error: Invalid login attempt.', { exact: true })).toBeVisible();
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Log in', exact: true }).click();
  await expect(page).toHaveURL(/\/workspaces$/);
});

test('workspace, project, nested folders and models remain visible after reload and selection', async ({ page }) => {
  const project = await setupProject(page);
  await create(page, 'folder', 'Processes/Sales');
  await page.getByRole('row').filter({ hasText: 'Processes' }).click();
  await page.getByRole('row').filter({ hasText: 'Sales' }).click();
  const folder = page.url();
  const first = await newModel(page, 'Test');
  await page.screenshot({ path: 'artifacts/browser/model-preview.png' });
  await saveRename(page, 'Persisted Test');
  await page.reload();
  await editorReady(page);
  await expect(modelName(page)).toHaveValue('Persisted Test');
  await page.goto(project);
  await newModel(page, 'Second model');
  await page.getByRole('button', { name: 'Toggle model list' }).click();
  await page.getByRole('navigation', { name: 'simpleXflow models' }).getByRole('button').filter({ hasText: 'Persisted Test' }).click();
  await expect(page).toHaveURL(first);
  await editorReady(page);
  await page.getByRole('button', { name: 'Toggle model list' }).click();
  await page.getByTitle('New model', { exact: true }).click();
  await expect(page).not.toHaveURL(first);
  await editorReady(page);
  await page.goto(folder);
  await expect(page.getByRole('row').filter({ hasText: 'Persisted Test' })).toBeVisible();
  await page.screenshot({ path: 'artifacts/browser/workspace-preview.png' });
});

test('another organization cannot open private workspace or model', async ({ page, browser, baseURL }) => {
  const project = await setupProject(page);
  const privateModel = await newModel(page, 'Private model');
  const context = await browser.newContext({ baseURL });
  try {
    const other = await context.newPage();
    await register(other);
    await other.goto(project);
    await expect(other.getByRole('heading', { name: 'Workspace unavailable' })).toBeVisible();
    await expect(other.getByText('Private model')).toHaveCount(0);
    await other.goto(privateModel);
    await expect(other).toHaveURL(/\/workspaces$/);
  } finally { await context.close(); }
});

test('diagram edits autosave without a fullscreen workaround', async ({ page }) => {
  await setupProject(page);
  await newModel(page);
  await page.locator('#js-canvas .djs-element[data-element-id="StartEvent_1"]').first().dblclick();
  const label = page.locator('.djs-direct-editing-content');
  await label.fill('Browser edited start');
  await label.press('Control+Enter');
  await expect(label).toBeHidden();
  await expect(page.locator('.autosave-indicator')).toContainText('Saved');
  await page.getByRole('button', { name: 'History & work' }).click();
  await expect(page.locator('.collaboration-drawer')).toBeVisible();
  await page.reload();
  await editorReady(page);
  await expect(page.locator('#js-canvas .djs-label').filter({ hasText: 'Browser edited start' }).first()).toBeVisible();
});

for (const sample of [
  { id: 'paper-coffee-break', name: 'Coffee', element: 'StartEvent_VisitorsArrive' },
  { id: 'paper-mm1-queue', name: 'M/M/1', element: 'Task_FIFOQueue' },
  { id: 'poster-hospital-er', name: 'Hospital', element: 'Task_Room1' }
]) {
  test(`model templates are separate from saved models: ${sample.id}`, async ({ page }) => {
    await setupProject(page);
    await newModel(page);
    await more(page);
    await page.getByRole('combobox', { name: 'Model template' }).selectOption(sample.id);
    await page.getByRole('button', { name: 'Load template', exact: true }).click();
    await expect(page.getByRole('alertdialog')).toBeVisible();
    await page.getByRole('button', { name: 'Cancel', exact: true }).click();
    await expect(modelName(page)).toHaveValue('Test model');
    await page.getByRole('button', { name: 'Load template', exact: true }).click();
    await page.getByRole('button', { name: 'Replace model', exact: true }).click();
    await expect(page.locator('.status-line')).toContainText('loaded and saved');
    await page.reload();
    await editorReady(page);
    await expect(modelName(page)).toHaveValue(new RegExp(sample.name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')));
    await expect(page.locator(`#js-canvas [data-element-id="${sample.element}"]`).first()).toBeVisible();
    await more(page);
    await page.getByRole('button', { name: 'Full screen', exact: true }).click();
    await expect(page.locator('.editor-panel')).toHaveClass(/is-fullscreen/);
    await more(page);
    await page.getByRole('button', { name: 'Exit full screen', exact: true }).click();
    await expect(page.locator('.editor-panel')).not.toHaveClass(/is-fullscreen/);
  });
}

test('local backup preserves the model and can be imported into its folder', async ({ page }) => {
  const project = await setupProject(page);
  await newModel(page, 'Backup model');
  // Exercise the standard browser fallback without a native OS picker in automation.
  await page.evaluate(() => { window.showSaveFilePicker = undefined; });
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Save locally…' }).click();
  const download = await downloadPromise;
  const backupPath = await download.path();
  const backup = JSON.parse(await fs.readFile(backupPath, 'utf8'));
  expect(backup.format).toBe('simpleXflow');
  expect(backup.name).toBe('Backup model');
  expect(backup.bpmnXml).toContain('StartEvent_1');
  await page.goto(project);
  await expect(page.locator('.hub-shell')).toHaveAttribute('data-interactive', 'true');
  await page.locator('#hub-import-file').setInputFiles({ name: 'restored.sxf', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(backup)) });
  await editorReady(page);
  await expect(modelName(page)).toHaveValue(/Backup model/);
});

test('imports BPMN and rejects invalid files without changing the current model', async ({ page }) => {
  await setupProject(page);
  await page.locator('#hub-import-file').setInputFiles(path.resolve('tests/browser/fixtures/imported-model.bpmn'));
  await editorReady(page);
  const url = page.url();
  await expect(modelName(page)).toHaveValue('imported-model');
  await more(page);
  await page.locator('#project-import-file').setInputFiles({ name: 'invalid.xml', mimeType: 'text/xml', buffer: Buffer.from('not BPMN') });
  await expect(page.locator('.status-line')).toContainText('not a valid simpleXflow project');
  await expect(page).toHaveURL(url);
  await page.reload();
  await expect(page.locator('#js-canvas [data-element-id="Imported_Start"]').first()).toBeVisible();
});

test('version releases, assigned work and model deletion are persisted', async ({ page }) => {
  const project = await setupProject(page);
  const modelUrl = await newModel(page, 'Versioned model');
  await editDiagramLabel(page, 'StartEvent_1', 'Versioned start');
  await page.getByRole('button', { name: 'History & work' }).click();
  const panel = page.locator('.collaboration-drawer');
  await panel.getByLabel('Version label').fill('Review 1.0');
  await panel.getByLabel('Planned release').fill('2027-01-15');
  await panel.getByRole('button', { name: 'Save version', exact: true }).click();
  const review = panel.locator('.version-row').filter({ hasText: 'Review 1.0' });
  await expect(review).toBeVisible();
  await review.getByRole('button', { name: 'Release version', exact: true }).click();
  await expect(panel.getByText('Released', { exact: true })).toBeVisible();
  await panel.getByLabel('Title', { exact: true }).fill('Validate resources');
  const assignee = await panel.getByLabel('Assigned to').locator('option').nth(1).getAttribute('value');
  await panel.getByLabel('Assigned to').selectOption(assignee);
  await panel.getByLabel('Delivery date').fill('2027-01-20');
  await panel.getByRole('button', { name: 'Create work package', exact: true }).click();
  await expect(panel.locator('.task-row')).toContainText('Validate resources');
  await page.reload();
  await page.getByRole('button', { name: 'History & work' }).click();
  await expect(page.locator('.task-row')).toContainText('Validate resources');
  await page.getByRole('button', { name: 'Close collaboration' }).click();
  await projectBreadcrumb(page);
  await page.getByRole('button', { name: /^Work packages/ }).click();
  await page.locator('.task-row').filter({ hasText: 'Validate resources' }).getByRole('link', { name: 'Open model', exact: true }).click();
  await expect(page).toHaveURL(modelUrl);
  await expectDiagram(page, 'StartEvent_1', 'Versioned start');
  await more(page);
  await page.getByRole('button', { name: 'Delete model…', exact: true }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Delete model', exact: true }).click();
  await expect(page).toHaveURL(project);
  await expect(page.getByRole('row').filter({ hasText: 'Versioned model' })).toHaveCount(0);
});

test('stale parallel edits report a conflict and preserve the saved version', async ({ page, context }) => {
  await setupProject(page);
  const url = await newModel(page, 'Shared draft');
  const other = await context.newPage();
  try {
    await other.goto(url);
    await editorReady(other);
    await saveRename(page, 'First writer');
    await modelName(other).fill('Stale writer');
    await expect(other.locator('.conflict-banner')).toBeVisible();
    await page.reload();
    await expect(modelName(page)).toHaveValue('First writer');
    await expect(modelName(other)).toHaveValue('Stale writer');
  } finally { await other.close(); }
});

test('member invitation link remains visible after creation and is revocable', async ({ page }) => {
  await setupProject(page);
  await page.getByRole('button', { name: /^Members/ }).click();
  const members = page.getByRole('region', { name: 'Workspace members' });
  await members.getByLabel('Email', { exact: true }).fill('guest@example.test');
  await members.getByRole('button', { name: 'Create invitation link' }).click();
  await expect(members.getByLabel('Invitation link')).toHaveValue(/\/invitations\/accept\?token=/);
  await expect(members.getByText('No email has been sent.', { exact: false })).toBeVisible();
  await members.getByRole('button', { name: 'Revoke', exact: true }).click();
  await expect(members.getByLabel('Invitation link')).toHaveCount(0);
});

test('legacy help sample links create a model inside the organization hierarchy', async ({ page }) => {
  await register(page);
  await page.goto('/projects?sample=paper-coffee-break');
  await expect(page).toHaveURL(/\/projects\/[a-f0-9-]{36}$/);
  await editorReady(page);
  await expect(modelName(page)).toHaveValue('Paper sample - Coffee break');
  await expect(page.getByRole('navigation', { name: 'Breadcrumb' }).getByRole('link', { name: 'My workspace' })).toBeVisible();
});

test('Imported models opens the selected diagram after repeated hub navigation, keyboard opening and reload', async ({ page }) => {
  await register(page);
  await create(page, 'workspace', 'Migrated workspace');
  await create(page, 'project', 'Imported models');
  const firstXml = await fs.readFile(path.resolve('tests/browser/fixtures/imported-model.bpmn'), 'utf8');
  await page.locator('#hub-import-file').setInputFiles({ name: 'first-model.bpmn', mimeType: 'text/xml', buffer: Buffer.from(firstXml) });
  await expectDiagram(page, 'Imported_Start', 'Imported start');
  const firstUrl = page.url();
  await projectBreadcrumb(page, 'Imported models');
  await page.locator('#hub-import-file').setInputFiles({
    name: 'second-model.bpmn', mimeType: 'text/xml',
    buffer: Buffer.from(firstXml.replaceAll('Imported_', 'Second_').replace('Imported start', 'Second start'))
  });
  await expectDiagram(page, 'Second_Start', 'Second start');
  const secondUrl = page.url();
  await projectBreadcrumb(page, 'Imported models');

  // A real row activation exercises client navigation; page.goto alone misses stale editor DOM.
  const firstRow = page.getByRole('row').filter({ hasText: 'first-model' });
  await firstRow.focus();
  await firstRow.press('Enter');
  await expect(page).toHaveURL(firstUrl);
  await expect(modelName(page)).toHaveValue('first-model');
  await expectDiagram(page, 'Imported_Start', 'Imported start');
  await expect(page.locator('#js-canvas [data-element-id="Second_Start"]')).toHaveCount(0);
  await projectBreadcrumb(page, 'Imported models');
  await page.getByRole('row').filter({ hasText: 'second-model' }).click();
  await expect(page).toHaveURL(secondUrl);
  await expectDiagram(page, 'Second_Start', 'Second start');
  await expect(page.locator('#js-canvas [data-element-id="Imported_Start"]')).toHaveCount(0);
  await page.reload();
  await expectDiagram(page, 'Second_Start', 'Second start');
  await page.getByRole('button', { name: 'Toggle model list' }).click();
  await page.getByRole('navigation', { name: 'simpleXflow models' }).getByRole('button').filter({ hasText: 'first-model' }).click();
  await expect(page).toHaveURL(firstUrl);
  await expectDiagram(page, 'Imported_Start', 'Imported start');
});

test('switching models flushes pending diagram edits to the correct model', async ({ page }) => {
  await setupProject(page);
  const firstUrl = await newModel(page, 'First document');
  await projectBreadcrumb(page);
  const secondUrl = await newModel(page, 'Second document');
  await page.getByRole('button', { name: 'Toggle model list' }).click();
  const modelList = page.getByRole('navigation', { name: 'simpleXflow models' });
  await modelList.getByRole('button').filter({ hasText: 'First document' }).click();
  await expect(page).toHaveURL(firstUrl);
  await editorReady(page);
  await page.getByRole('button', { name: 'Toggle model list' }).click();
  await editDiagramLabel(page, 'StartEvent_1', 'Only in the first document');
  // Deliberately navigate before waiting for the debounced autosave indicator.
  await modelList.getByRole('button').filter({ hasText: 'Second document' }).click();
  await expect(page).toHaveURL(secondUrl);
  await editorReady(page);
  await expect(page.locator('#js-canvas .djs-label').filter({ hasText: 'Only in the first document' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Toggle model list' }).click();
  await modelList.getByRole('button').filter({ hasText: 'First document' }).click();
  await expectDiagram(page, 'StartEvent_1', 'Only in the first document');
  await page.reload();
  await expectDiagram(page, 'StartEvent_1', 'Only in the first document');
});

test('delayed editor startup loads the selected saved XML instead of a blank default', async ({ page }) => {
  await setupProject(page);
  await page.locator('#hub-import-file').setInputFiles(path.resolve('tests/browser/fixtures/imported-model.bpmn'));
  await expectDiagram(page, 'Imported_Start', 'Imported start');
  let releaseBundle;
  let requested = false;
  const bundleGate = new Promise(resolve => { releaseBundle = resolve; });
  await page.route('**/vendor/simbpmn/main_window/index.js', async route => {
    requested = true;
    await bundleGate;
    await route.continue();
  });
  try {
    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(modelName(page)).toHaveValue('imported-model');
    await expect.poll(() => requested).toBeTruthy();
    await expect(page.locator('#js-canvas .djs-shape')).toHaveCount(0);
  } finally { releaseBundle(); }
  await expectDiagram(page, 'Imported_Start', 'Imported start');
  await expect(page.locator('#js-canvas [data-element-id="StartEvent_1"]')).toHaveCount(0);
});

test('failed editor bundle reports an error and Retry restores the saved model', async ({ page }) => {
  await setupProject(page);
  await page.locator('#hub-import-file').setInputFiles(path.resolve('tests/browser/fixtures/imported-model.bpmn'));
  await expectDiagram(page, 'Imported_Start', 'Imported start');
  let requests = 0;
  await page.route('**/vendor/simbpmn/main_window/index.js', route => {
    requests += 1;
    return requests === 1 ? route.abort('failed') : route.continue();
  });
  await page.reload();
  const warning = page.locator('.modeler-warning');
  await expect(warning).toContainText('Could not load the simpleXflow editor bundle');
  await expect(modelName(page)).toHaveValue('imported-model');
  await warning.getByRole('button', { name: 'Retry', exact: true }).click();
  await expectDiagram(page, 'Imported_Start', 'Imported start');
  expect(requests).toBe(2);
  await page.reload();
  await expectDiagram(page, 'Imported_Start', 'Imported start');
});

test('restoring a version restores its diagram and keeps later edits in history', async ({ page }) => {
  await setupProject(page);
  await newModel(page, 'Restorable model');
  await editDiagramLabel(page, 'StartEvent_1', 'Approved start');
  await page.getByRole('button', { name: 'History & work' }).click();
  const panel = page.locator('.collaboration-drawer');
  await panel.getByLabel('Version label').fill('Approved baseline');
  await panel.getByRole('button', { name: 'Save version', exact: true }).click();
  await expect(panel.locator('.version-row').filter({ hasText: 'Approved baseline' })).toBeVisible();
  await page.getByRole('button', { name: 'Close collaboration' }).click();
  await expect(panel).toBeHidden();
  await expectDiagram(page, 'StartEvent_1', 'Approved start');
  await editDiagramLabel(page, 'StartEvent_1', 'Unapproved change');
  await page.getByRole('button', { name: 'History & work' }).click();
  await panel.getByLabel('Version label').fill('Later draft');
  await panel.getByRole('button', { name: 'Save version', exact: true }).click();
  const baseline = panel.locator('.version-row').filter({ hasText: 'Approved baseline' });
  await baseline.getByRole('button', { name: 'Restore', exact: true }).click();
  await baseline.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expectDiagram(page, 'StartEvent_1', 'Unapproved change');
  await baseline.getByRole('button', { name: 'Restore', exact: true }).click();
  await baseline.getByRole('button', { name: 'Confirm restore', exact: true }).click();
  await expect(panel.getByRole('status')).toContainText('Version restored');
  await expectDiagram(page, 'StartEvent_1', 'Approved start');
  await expect(panel.locator('.version-row').filter({ hasText: 'Later draft' })).toBeVisible();
  await page.reload();
  await expectDiagram(page, 'StartEvent_1', 'Approved start');
});

test('moving models into nested folders and cancelling deletion preserve saved content', async ({ page }) => {
  const project = await setupProject(page);
  const url = await newModel(page, 'Moved model');
  await editDiagramLabel(page, 'StartEvent_1', 'Keep this model');
  await more(page);
  await page.getByRole('button', { name: 'Move to folder…', exact: true }).click();
  const move = page.getByRole('dialog', { name: 'Move model' });
  await move.getByLabel('Folder path').fill('Processes/Sales');
  await move.getByRole('button', { name: 'Move', exact: true }).click();
  await expect(move).toBeHidden();
  await expect(page.locator('.app-header').getByRole('navigation', { name: 'Breadcrumb' })).toContainText('Processes/Sales');
  await more(page);
  await page.getByRole('button', { name: 'Delete model…', exact: true }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Cancel', exact: true }).click();
  await projectBreadcrumb(page);
  await expect(page).toHaveURL(project);
  await expect(page.getByRole('row').filter({ hasText: 'Moved model' })).toHaveCount(0);
  await page.getByRole('row').filter({ hasText: 'Processes' }).click();
  await page.getByRole('row').filter({ hasText: 'Sales' }).click();
  await page.getByRole('row').filter({ hasText: 'Moved model' }).click();
  await expect(page).toHaveURL(url);
  await expectDiagram(page, 'StartEvent_1', 'Keep this model');
  await page.reload();
  await expectDiagram(page, 'StartEvent_1', 'Keep this model');
});

test('invalid hub imports keep the folder unchanged and model names remain literal text', async ({ page }) => {
  await setupProject(page);
  const name = '<img src=x onerror=alert(1)> & important model';
  await newModel(page, name);
  await projectBreadcrumb(page);
  const rows = page.getByRole('table', { name: 'Models and folders' }).getByRole('row');
  await expect(rows).toHaveCount(2);
  await expect(page.getByRole('row').filter({ hasText: name })).toBeVisible();
  await expect(page.locator('.file-table-row img')).toHaveCount(0);
  const url = page.url();
  await page.locator('#hub-import-file').setInputFiles({ name: 'broken.sxf', mimeType: 'application/json', buffer: Buffer.from('{broken') });
  await expect(page.getByRole('status')).toContainText('Import failed:');
  await expect(page).toHaveURL(url);
  await expect(rows).toHaveCount(2);
  await page.getByRole('row').filter({ hasText: name }).click();
  await editorReady(page);
  await expect(modelName(page)).toHaveValue(name);
  await page.reload();
  await expect(modelName(page)).toHaveValue(name);
  await editorReady(page);
});

test('workspace and file navigation remains keyboard operable on a narrow screen', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await setupProject(page);
  await newModel(page, 'Mobile model');
  await projectBreadcrumb(page);
  await expect(page.getByRole('heading', { name: 'Simulation project', exact: true })).toBeVisible();
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
  const row = page.getByRole('row').filter({ hasText: 'Mobile model' });
  await row.focus();
  await expect(row).toBeFocused();
  await row.press('Enter');
  await editorReady(page);
  await expect(modelName(page)).toHaveValue('Mobile model');
  await projectBreadcrumb(page);
  const search = page.getByRole('searchbox', { name: 'Search files' });
  await search.fill('absent');
  await expect(page.getByRole('heading', { name: 'No matching models or folders' })).toBeVisible();
  await search.fill('Mobile');
  await expect(row).toBeVisible();
});
