const { test, expect } = require('@playwright/test');
const { readFileSync } = require('node:fs');
const path = require('node:path');

const webRoot = path.resolve('src/SimpleXflow.Web/wwwroot');
const legacyPage = readFileSync(path.join(webRoot, 'vendor/simbpmn/main_window/index.html'), 'utf8');
const body = legacyPage.match(/<body>([\s\S]*)<\/body>/)[1];

test.beforeEach(async ({ page }) => {
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.route('http://modeler.test/**', route => {
    const pathname = new URL(route.request().url()).pathname;
    if (pathname === '/') {
      return route.fulfill({ contentType: 'text/html', body: `<link rel="stylesheet" href="/app.css">
        <style>#host{height:500px;width:900px}#content{height:100%}#sidebar,#horizontalResizer{display:none!important}
        #js-drop-zone{height:100%;width:100%}#middle{height:100%;width:650px}#bpmnContent{height:450px}
        #js-properties-panel{width:250px}#bottomMenu{height:50px}</style>
        <div id="host" class="simplexflow-editor-host">${body}<a id="js-download-diagram" hidden></a><a id="js-download-svg" hidden></a></div>` });
    }
    return route.fulfill({ path: path.join(webRoot, pathname.slice(1)) });
  });
  await page.goto('http://modeler.test/');
  await page.addScriptTag({ path: path.join(webRoot, 'js/simplexflow-editor.js') });
  await page.evaluate(async () => {
    await simpleXflowEditor.initialize('host', null);
    simpleXflowEditor.watchChanges('host', { invokeMethodAsync: () => Promise.resolve() });
  });
  expect(errors).toEqual([]);
});

test('the loaded architecture is inside its viewport before toggling fullscreen', async ({ page }) => {
  const shape = page.locator('#js-canvas [data-element-id="StartEvent_1"]').first();
  await expect(shape).toBeVisible();
  await expect.poll(async () => {
    const diagram = await page.locator('#js-canvas').boundingBox();
    const bounds = await shape.boundingBox();
    return bounds && bounds.x >= diagram.x && bounds.y >= diagram.y
      && bounds.x + bounds.width <= diagram.x + diagram.width
      && bounds.y + bounds.height <= diagram.y + diagram.height;
  }).toBe(true);
});

test('architecture resources survive logic import and keep their registry IDs', async ({ page }) => {
  await page.evaluate(() => {
    const modeler = simpleXflowModelers.architecture;
    const modeling = modeler.get('modeling');
    const root = modeler.get('canvas').getRootElement();
    const task = modeling.createShape({ type: 'bpmn:Task' }, { x: 400, y: 300 }, root);
    const resource = modeling.createShape({ type: 'regularBPMN:Resource' }, { x: 300, y: 150 }, root);
    modeling.updateProperties(resource, { name: 'Additional staff' });
    modeling.connect(resource, task, { type: 'bpmn:Association' });
    window.resourceId = resource.id;
    window.taskId = task.id;
    modeler.get('selection').select(task);
  });
  await expect.poll(() => page.evaluate(() => simpleXflowModelers.logic.get('elementRegistry').get(resourceId)?.businessObject.name))
    .toBe('Additional staff');
  await page.locator('#logicViewMenuItem').click();
  const resourceId = await page.evaluate(() => window.resourceId);
  await expect(page.locator(`#js-simbpmncanvas [data-element-id="${resourceId}"]`).first()).toBeVisible();
  // Selecting the task again must update one resource, not create a duplicate.
  await page.evaluate(() => {
    const modeler = simpleXflowModelers.architecture;
    modeler.get('selection').select([]);
    modeler.get('selection').select(modeler.get('elementRegistry').get(taskId));
  });
  await expect.poll(() => page.evaluate(() => simpleXflowModelers.logic.get('elementRegistry').getAll()
    .filter(element => element.businessObject.name === 'Additional staff' && !element.labelTarget).length)).toBe(1);
  const exported = await page.evaluate(async () => {
    const architecture = simpleXflowModelers.architecture;
    const logic = simpleXflowModelers.logic;
    architecture.get('modeling').updateProperties(architecture.get('elementRegistry').get(taskId), { name: 'Unsaved task' });
    logic.get('modeling').updateProperties(logic.get('elementRegistry').get(resourceId), { name: 'Unsaved staff' });
    const xml = await simpleXflowEditor.getXml();
    return { xml, logicXml: await simpleXflowEditor.getLogic() };
  });
  expect(exported.xml).toContain('Unsaved task');
  expect(exported.xml).toContain('Unsaved staff');
  expect(exported.logicXml).toContain('Unsaved staff');
});

test('read-only mode allows selection but blocks model changes and label editing', async ({ page }) => {
  const result = await page.evaluate(() => {
    simpleXflowEditor.setReadOnly('host', true);
    const modeler = simpleXflowModelers.architecture;
    const element = modeler.get('elementRegistry').get('StartEvent_1');
    modeler.get('selection').select(element);
    modeler.get('modeling').updateProperties(element, { name: 'Forbidden edit' });
    return {
      selected: modeler.get('selection').get()[0].id,
      name: element.businessObject.name ?? null,
      edit: modeler.get('directEditing').activate(element),
      move: modeler.get('rules').allowed('elements.move', { shapes: [element] })
    };
  });
  expect(result).toEqual({ selected: 'StartEvent_1', name: null, edit: false, move: false });
  await page.evaluate(() => {
    simpleXflowEditor.setReadOnly('host', false);
    const modeler = simpleXflowModelers.architecture;
    modeler.get('modeling').updateProperties(modeler.get('elementRegistry').get('StartEvent_1'), { name: 'Allowed edit' });
  });
  await expect(page.locator('#js-canvas .djs-label').filter({ hasText: 'Allowed edit' }).first()).toBeVisible();
});

test('first edits notify immediately and remain dirty until the matching save is acknowledged', async ({ page }) => {
  const first = await page.evaluate(async () => {
    window.notifications = [];
    simpleXflowEditor.watchChanges('host', { invokeMethodAsync: (...args) => { notifications.push(args); return Promise.resolve(); } });
    const modeler = simpleXflowModelers.architecture;
    modeler.get('modeling').updateProperties(modeler.get('elementRegistry').get('StartEvent_1'), { name: 'Immediate edit' });
    const version = simpleXflowEditor.getChangeVersion('host');
    const notification = notifications[0];
    const xml = await simpleXflowEditor.getXml();
    const event = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(event);
    return { version, notification, xml, blocked: event.defaultPrevented };
  });
  expect(first.notification).toEqual(['NotifyEditorDirtyAsync', first.version]);
  expect(first.xml).toContain('Immediate edit');
  expect(first.blocked).toBe(true);
  await expect.poll(() => page.evaluate(() => notifications.some(args => args[0] === 'NotifyModelChangedAsync' && args[1].includes('Immediate edit')))).toBe(true);
  const final = await page.evaluate(firstVersion => {
    const modeler = simpleXflowModelers.architecture;
    modeler.get('modeling').updateProperties(modeler.get('elementRegistry').get('StartEvent_1'), { name: 'Edit during save' });
    const staleSave = simpleXflowEditor.clearDirty('host', firstVersion);
    const newVersion = simpleXflowEditor.getChangeVersion('host');
    const currentSave = simpleXflowEditor.clearDirty('host', newVersion);
    const event = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(event);
    return { staleSave, currentSave, blocked: event.defaultPrevented, newVersion };
  }, first.version);
  expect(final.staleSave).toBe(false);
  expect(final.currentSave).toBe(true);
  expect(final.blocked).toBe(false);
  expect(final.newVersion).toBeGreaterThan(first.version);
});
