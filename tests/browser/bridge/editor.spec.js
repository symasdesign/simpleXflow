const { test, expect } = require('@playwright/test');
const path = require('node:path');

const bridgePath = path.resolve('src/SimpleXflow.Web/wwwroot/js/simplexflow-editor.js');
const architecture = '<definitions><process id="Process_1"><task id="Room1"></task></process></definitions>';

test.beforeEach(async ({ page }) => {
  // Real browser DOM and timers; only the separately compiled legacy bundle is substituted.
  await page.route('**/vendor/simbpmn/main_window/index.js', route => route.fulfill({
    contentType: 'application/javascript',
    body: `window.openedModels = []; window.openedLogic = [];
      electronAPI.onOpenXmlFile((_event, xml) => window.openedModels.push(xml));
      electronAPI.openLogic((_event, xml) => window.openedLogic.push(xml));`
  }));
  await page.route('http://bridge.test/', route => route.fulfill({
    contentType: 'text/html',
    body: '<div id="host"><div id="js-canvas"><span class="djs-element"></span></div><div id="js-properties-panel"><input></div><a id="js-download-diagram"></a></div><input id="outside">'
  }));
  await page.goto('http://bridge.test/');
  await page.clock.install();
  await page.addScriptTag({ path: bridgePath });
});

async function initialize(page, xml = architecture, logic = null, target = null) {
  await page.evaluate(args => simpleXflowEditor.initialize('host', ...args), [xml, logic, target]);
}

async function watch(page) {
  await page.evaluate(() => {
    window.notifications = [];
    simpleXflowEditor.watchChanges('host', {
      invokeMethodAsync: (...args) => { notifications.push(args); return Promise.resolve(); }
    });
  });
  await page.clock.runFor(3300);
}

test('loads the bundle once and opens the model in the ready host', async ({ page }) => {
  await initialize(page);
  await initialize(page);
  await expect(page.locator('#host')).toHaveClass('is-ready');
  expect(await page.evaluate(() => openedModels.at(-1))).toBe(architecture);
  await expect(page.locator('script[src*="vendor/simbpmn"]')).toHaveCount(1);
});

for (const xml of [null, '', '   ']) {
  test(`opens a valid default start-event diagram for ${JSON.stringify(xml)}`, async ({ page }) => {
    await initialize(page, xml);
    const result = await page.evaluate(async () => {
      const document = new DOMParser().parseFromString(await simpleXflowEditor.getXml(), 'application/xml');
      return { errors: document.querySelectorAll('parsererror').length, starts: document.getElementsByTagNameNS('*', 'startEvent').length };
    });
    expect(result).toEqual({ errors: 0, starts: 1 });
  });
}

test('reports a missing host', async ({ page }) => {
  await expect(page.evaluate(() => simpleXflowEditor.initialize('missing', '<definitions/>'))).rejects.toThrow('host is missing');
});

test('reports a bundle download failure', async ({ page }) => {
  await page.route('**/vendor/simbpmn/main_window/index.js', route => route.abort());
  await expect(initialize(page)).rejects.toThrow('Could not load');
});

test('can retry initialization after a transient bundle download failure', async ({ page }) => {
  const failDownload = route => route.abort();
  await page.route('**/vendor/simbpmn/main_window/index.js', failDownload);
  await expect(initialize(page)).rejects.toThrow('Could not load');
  await page.unroute('**/vendor/simbpmn/main_window/index.js', failDownload);
  await initialize(page);
  await expect(page.locator('#host')).toHaveClass('is-ready');
  expect(await page.evaluate(() => openedModels.at(-1))).toBe(architecture);
});

test('prefers the latest downloadable XML when saving', async ({ page }) => {
  await initialize(page);
  await page.evaluate(() => document.querySelector('#js-download-diagram').setAttribute('href',
    'data:application/bpmn20-xml;charset=UTF-8,' + encodeURIComponent('  <definitions id="changed"/>  ')));
  expect(await page.evaluate(() => simpleXflowEditor.getXml())).toBe('<definitions id="changed"/>');
});

for (const href of ['https://example.invalid/model.bpmn', 'data:application/bpmn20-xml;charset=UTF-8,%E0%A4%A']) {
  test(`falls back to the opened model for unusable download URI ${href}`, async ({ page }) => {
    await initialize(page);
    await page.locator('#js-download-diagram').evaluate((element, value) => element.setAttribute('href', value), href);
    expect(await page.evaluate(() => simpleXflowEditor.getXml())).toBe(architecture);
  });
}

test('escapes embedded logic and replaces an existing content attribute', async ({ page }) => {
  const logic = '<logic name="A & B">\n\tvalue\r</logic>';
  await initialize(page, architecture.replace('id="Room1"', 'id="Room1" content="old"'), logic, 'Room1');
  const result = await page.evaluate(async () => {
    const doc = new DOMParser().parseFromString(await simpleXflowEditor.getXml(), 'application/xml');
    return { errors: doc.querySelectorAll('parsererror').length, logic: doc.querySelector('task').getAttribute('content') };
  });
  expect(result).toEqual({ errors: 0, logic });
  expect(await page.evaluate(() => simpleXflowEditor.getLogic())).toBe(logic);
});

test('embeds logic into a self-closing BPMN element without corrupting XML', async ({ page }) => {
  await initialize(page, '<definitions><task id="Room1" /></definitions>', '<logic/>', 'Room1');
  const result = await page.evaluate(async () => {
    const doc = new DOMParser().parseFromString(await simpleXflowEditor.getXml(), 'application/xml');
    return { errors: doc.querySelectorAll('parsererror').length, content: doc.querySelector('task')?.getAttribute('content') };
  });
  expect(result).toEqual({ errors: 0, content: '<logic/>' });
});

test('matches special characters in target IDs literally', async ({ page }) => {
  await initialize(page, '<definitions><task id="RoomX1"/><task id="Room.1"></task></definitions>', '<logic/>', 'Room.1');
  const result = await page.evaluate(async () => {
    const doc = new DOMParser().parseFromString(await simpleXflowEditor.getXml(), 'application/xml');
    return [...doc.querySelectorAll('task')].map(task => task.getAttribute('content'));
  });
  expect(result).toEqual([null, '<logic/>']);
});

test('a project switch cancels stale delayed model opens', async ({ page }) => {
  await initialize(page);
  await page.evaluate(() => {
    document.querySelector('#js-canvas').replaceChildren();
    return simpleXflowEditor.openXml('host', '<definitions id="second"/>', '');
  });
  const count = await page.evaluate(() => openedModels.length);
  await page.clock.runFor(3000);
  expect(await page.evaluate(count => openedModels.slice(count), count)).toEqual(
    expect.arrayContaining(['<definitions id="second"/>']));
  expect(await page.evaluate(count => openedModels.slice(count).every(xml => xml.includes('second')), count)).toBe(true);
  expect(await page.evaluate(() => simpleXflowEditor.getLogic())).toBe('');
});

test('suppresses startup notifications and debounces subsequent changes', async ({ page }) => {
  await initialize(page);
  await page.evaluate(() => {
    window.notifications = [];
    simpleXflowEditor.watchChanges('host', { invokeMethodAsync: (...args) => { notifications.push(args); return Promise.resolve(); } });
  });
  await page.clock.runFor(20);
  await page.evaluate(() => electronAPI.saveLogicRelay('<startup/>'));
  await page.clock.runFor(3300);
  expect(await page.evaluate(() => notifications)).toEqual([]);
  await page.evaluate(async () => {
    await electronAPI.saveLogicRelay('<first/>');
    await electronAPI.saveLogicRelay('<second/>');
  });
  await page.clock.runFor(300);
  expect(await page.evaluate(() => notifications)).toEqual([['NotifyModelChangedAsync', '', '<second/>']]);
});

test('observes architecture edits from the download link', async ({ page }) => {
  await initialize(page);
  await watch(page);
  await page.evaluate(() => document.querySelector('#js-download-diagram').setAttribute('href',
    'data:application/bpmn20-xml;charset=UTF-8,' + encodeURIComponent('<definitions id="edit"/>')));
  await page.clock.runFor(300);
  expect(await page.evaluate(() => notifications)).toEqual([['NotifyModelChangedAsync', '<definitions id="edit"/>', '']]);
});

test('ignores unrelated keyboard input outside the editor', async ({ page }) => {
  await initialize(page);
  await page.evaluate(() => document.querySelector('#js-download-diagram').setAttribute('href',
    'data:application/bpmn20-xml;charset=UTF-8,' + encodeURIComponent('<definitions id="keyboard"/>')));
  await watch(page);
  await page.evaluate(() => document.querySelector('#outside').dispatchEvent(new KeyboardEvent('keyup', { key: 'Delete', bubbles: true })));
  await page.clock.runFor(300);
  expect(await page.evaluate(() => notifications)).toEqual([]);
  // The same key inside the editor must notify, so this test detects a broken event filter.
  await page.evaluate(() => document.querySelector('#js-properties-panel input').dispatchEvent(new KeyboardEvent('keyup', { key: 'Delete', bubbles: true })));
  await page.clock.runFor(300);
  expect(await page.evaluate(() => notifications)).toEqual([['NotifyModelChangedAsync', '<definitions id="keyboard"/>', null]]);
});

test('unwatch cancels pending callbacks and removes observers', async ({ page }) => {
  await initialize(page);
  await watch(page);
  await page.evaluate(async () => {
    await electronAPI.saveLogicRelay('<pending/>');
    simpleXflowEditor.unwatchChanges('host');
    await electronAPI.saveLogicRelay('<disposed/>');
    document.querySelector('#js-download-diagram').setAttribute('href', 'data:application/bpmn20-xml;charset=UTF-8,%3Cdefinitions%2F%3E');
  });
  await page.clock.runFor(500);
  expect(await page.evaluate(() => notifications)).toEqual([]);
});
