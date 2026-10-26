window.simpleXflowEditor = (() => {
  const scriptUrl = "/vendor/simbpmn/main_window/index.js";
  const defaultArchitectureXml = `<?xml version="1.0" encoding="UTF-8"?>
<bpmn2:definitions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:bpmn2="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" xsi:schemaLocation="http://www.omg.org/spec/BPMN/20100524/MODEL BPMN20.xsd" id="sample-diagram" targetNamespace="http://bpmn.io/schema/bpmn">
  <bpmn2:process id="Process_1" isExecutable="false">
    <bpmn2:startEvent id="StartEvent_1" />
  </bpmn2:process>
  <bpmndi:BPMNDiagram id="BPMNDiagram_1">
    <bpmndi:BPMNPlane id="BPMNPlane_1" bpmnElement="Process_1">
      <bpmndi:BPMNShape id="_BPMNShape_StartEvent_2" bpmnElement="StartEvent_1">
        <dc:Bounds height="36.0" width="36.0" x="412.0" y="240.0" />
      </bpmndi:BPMNShape>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn2:definitions>`;
  const callbacks = new Map();
  const editorStates = new Map();
  let bundlePromise;
  let activeHostId;
  let currentArchitectureXml = "";
  let currentLogicXml = "";
  let openAttemptId = 0;
  const modelSyncDepth = { architecture: 0, logic: 0 };
  const decodeUriComponent = globalThis.decodeURIComponent?.bind(globalThis);

  window.addEventListener("beforeunload", event => {
    if ([...editorStates.values()].some(state => state.dirty && !state.readOnly)) {
      event.preventDefault();
      event.returnValue = "";
    }
  });

  function getCallbackList(name) {
    if (!callbacks.has(name)) {
      callbacks.set(name, []);
    }

    return callbacks.get(name);
  }

  async function emit(name, ...args) {
    const model = name === "openLogic" || name === "adjustResourcesInLogic" ? "logic" : "architecture";
    modelSyncDepth[model]++;
    try {
      return await Promise.all(getCallbackList(name).map(callback => callback({}, ...args)));
    } finally {
      modelSyncDepth[model]--;
    }
  }

  function ensureElectronShim() {
    if (window.electronAPI) {
      return;
    }

    window.electronAPI = {
      addAttachment: async () => [],
      openFileDialog: async () => undefined,
      openFile: async () => undefined,
      scanDirectory: async () => [],
      isDirectory: async () => false,
      getWorkspacePath: async () => "simpleXflow cloud workspace",
      createNewFile: async (_filename, xml) => emit("openXmlFile", xml),
      deleteFile: async () => undefined,
      saveLogicRelay: async (xml) => {
        currentLogicXml = xml ?? "";
        await emit("saveLogic", xml);
        scheduleEditorChange(activeHostId, null, currentLogicXml);
      },
      openLogicRelay: async (xml) => {
        currentLogicXml = xml ?? "";
        await emit("openLogic", xml);
        delete getState(activeHostId).logicSize;
        dispatchEditorResize();
      },
      adjustResourcesInLogicRelay: async (resources) => emit("adjustResourcesInLogic", resources),
      exportBPMN: async () => undefined,
      importBPMN: async () => undefined,
      changeWorkspaceLocation: async () => "simpleXflow cloud workspace",
      changeApplicationLanguage: async () => undefined,
      getApplicationLanguage: async () => "de",
      getTranslation: async (key) => translations[key] ?? key,

      loadFolder: (callback) => getCallbackList("loadFolder").push(callback),
      callExportBPMN: (callback) => getCallbackList("callExportBPMN").push(callback),
      callImportBPMN: (callback) => getCallbackList("callImportBPMN").push(callback),
      onCreateXmlFile: (callback) => getCallbackList("createXmlFile").push(callback),
      returnToMainPage: (callback) => getCallbackList("returnToMainPage").push(callback),
      onOpenXmlFile: (callback) => getCallbackList("openXmlFile").push(callback),
      saveLogic: (callback) => getCallbackList("saveLogic").push(callback),
      openLogic: (callback) => getCallbackList("openLogic").push(callback),
      adjustResourcesInLogic: (callback) => getCallbackList("adjustResourcesInLogic").push(callback),
      askForSavingChanges: () => 0,
      askForDeleting: () => 1,
      saveForQuit: async () => undefined,
      closeApp: () => undefined,
      isDev: () => true,
      projectExists: () => false,
      canRenameProject: () => true,
      hasProjectAttachments: () => false,
      renameProject: async () => undefined,
      showMessage: (_title, message) => {
        console.warn(message);
        return 0;
      }
    };
  }

  function ensureBundle() {
    ensureElectronShim();

    if (!bundlePromise) {
      bundlePromise = new Promise((resolve, reject) => {
        const existing = document.querySelector(`script[src="${scriptUrl}"]`);
        if (existing) {
          if (existing.dataset.simplexflowLoading === "true") {
            existing.addEventListener("load", () => resolve(), { once: true });
            existing.addEventListener("error", () => reject(new Error("Could not load the simpleXflow editor bundle.")), { once: true });
          } else {
            resolve();
          }
          return;
        }

        const script = document.createElement("script");
        script.src = scriptUrl;
        script.async = false;
        script.dataset.simplexflowLoading = "true";
        script.onload = () => {
          script.dataset.simplexflowLoading = "false";
          script.dataset.simplexflowLoaded = "true";
          resolve();
        };
        script.onerror = () => reject(new Error("Could not load the simpleXflow editor bundle."));
        document.body.appendChild(script);
      });
    }

    const pendingBundle = bundlePromise;
    return pendingBundle.catch((error) => {
      if (bundlePromise === pendingBundle) {
        document.querySelector(`script[src="${scriptUrl}"]`)?.remove();
        bundlePromise = undefined;
      }
      throw error;
    });
  }

  function dispatchEditorResize() {
    window.requestAnimationFrame(() => {
      for (const [name, modeler] of Object.entries(window.simpleXflowModelers ?? {})) {
        const canvas = modeler.get("canvas");
        const container = canvas.getContainer();
        if (!container.clientWidth || !container.clientHeight) {
          continue;
        }
        canvas.resized();
        const state = getState(activeHostId);
        const size = `${container.clientWidth}x${container.clientHeight}`;
        if (state[`${name}Size`] !== size) {
          canvas.zoom("fit-viewport");
          state[`${name}Size`] = size;
        }
      }
      window.dispatchEvent(new Event("resize"));
    });
  }

  function getState(hostId) {
    const state = editorStates.get(hostId) ?? {};
    editorStates.set(hostId, state);
    return state;
  }

  function suppressChangeNotifications(hostId, durationMs = 3200) {
    if (!hostId) {
      return;
    }

    const state = getState(hostId);
    state.suppressChangesUntil = window.performance.now() + durationMs;
  }

  function shouldNotifyChanges(state) {
    return !!state?.dotNetReference
      && !state.readOnly
      && !state.loading
      && (window.simpleXflowModelers?.architecture
        ? !!state.dirty
        : !state.suppressChangesUntil || window.performance.now() > state.suppressChangesUntil);
  }

  function getChangeVersion(hostId) {
    return getState(hostId).changeVersion ?? 0;
  }

  function clearDirty(hostId, savedVersion) {
    const state = getState(hostId);
    if (getChangeVersion(hostId) !== savedVersion) return false;
    state.dirty = false;
    window.markAsClean?.();
    return true;
  }

  function notifyEditorDirty(hostId) {
    const state = getState(hostId);
    state.dirty = true;
    state.changeVersion = getChangeVersion(hostId) + 1;
    state.dotNetReference?.invokeMethodAsync("NotifyEditorDirtyAsync", state.changeVersion)
      .catch(error => console.warn("Could not notify Blazor about unsaved editor changes.", error));
  }

  function scheduleEditorChange(hostId, xml, logicXml) {
    if (!hostId) {
      return;
    }

    const state = getState(hostId);
    if (!shouldNotifyChanges(state)) {
      return;
    }

    if (xml?.trim()) {
      state.pendingXml = normalizeArchitectureXml(xml);
    }

    if (logicXml !== undefined) {
      state.pendingLogicXml = logicXml ?? "";
    }

    window.clearTimeout(state.changeTimer);
    state.changeTimer = window.setTimeout(async () => {
      if (!shouldNotifyChanges(state)) {
        return;
      }

      if (window.simpleXflowModelers?.architecture) {
        try {
          const version = getChangeVersion(hostId);
          const xml = await getXml();
          const logicXml = await getLogic();
          if (version !== getChangeVersion(hostId)) {
            scheduleEditorChange(hostId);
            return;
          }
          if (shouldNotifyChanges(state)) {
            await state.dotNetReference.invokeMethodAsync("NotifyModelChangedAsync", xml, logicXml);
          }
        } catch (error) {
          console.warn("Could not notify Blazor about a simpleXflow editor change.", error);
        }
        return;
      }

      let pendingXml = state.pendingXml ?? null;
      const pendingLogicXml = state.pendingLogicXml ?? null;
      state.pendingXml = null;
      state.pendingLogicXml = null;

      if (!pendingXml) {
        pendingXml = getDownloadLinkXml();
      }

      if (!pendingXml && pendingLogicXml === null) {
        return;
      }

      state.dotNetReference
        .invokeMethodAsync("NotifyModelChangedAsync", pendingXml, pendingLogicXml)
        .catch((error) => console.warn("Could not notify Blazor about a simpleXflow editor change.", error));
    }, 250);
  }

  function installChangeObserver(hostId) {
    const state = getState(hostId);
    state.changeEventTargets?.forEach(({ target, listener }) => {
      for (const eventName of listener.eventNames) {
        target.removeEventListener(eventName, listener.handle, true);
      }
    });
    state.downloadLinkObserver?.disconnect();
    state.modelerListeners?.forEach(({ eventBus, listener }) => eventBus.off("commandStack.changed", listener));

    if (window.simpleXflowModelers?.architecture) {
      state.modelerListeners = Object.entries(window.simpleXflowModelers).map(([name, modeler]) => {
        const eventBus = modeler.get("eventBus");
        const listener = () => {
          if (state.loading || state.readOnly || modelSyncDepth[name] > 0) return;
          notifyEditorDirty(hostId);
          scheduleEditorChange(hostId);
        };
        eventBus.on("commandStack.changed", listener);
        return { eventBus, listener };
      });
      return;
    }

    const host = document.getElementById(hostId);
    const downloadLink = document.getElementById("js-download-diagram");
    const propertiesPanel = document.getElementById("js-properties-panel");
    const targets = [downloadLink, propertiesPanel].filter(Boolean);

    if (targets.length === 0) {
      window.setTimeout(() => installChangeObserver(hostId), 250);
      return;
    }

    const shouldTrackEvent = (event) => {
      if (event?.type === "keyup") {
        const trackedKeys = ["Delete", "Backspace", "z", "Z", "y", "Y"];
        if (!trackedKeys.includes(event.key)) {
          return false;
        }
      }

      return !event?.target
        || !(event.target instanceof Node)
        || !host
        || host.contains(event.target)
        || event.target === document.body;
    };

    const createListener = (eventNames) => {
      const listener = {
        eventNames,
        handle: (event) => {
          if (shouldTrackEvent(event)) {
            scheduleEditorChange(hostId);
          }
        }
      };

      return listener;
    };

    const addTrackedListener = (target, eventNames) => {
      const listener = createListener(eventNames);
      for (const eventName of eventNames) {
        target.addEventListener(eventName, listener.handle, true);
      }

      state.changeEventTargets.push({ target, listener });
    };

    state.changeEventTargets = [];
    if (propertiesPanel) {
      addTrackedListener(propertiesPanel, ["input", "change", "blur"]);
    }

    addTrackedListener(document, ["keyup"]);

    if (downloadLink) {
      state.downloadLinkObserver = new MutationObserver(() => {
        const xml = getDownloadLinkXml();
        if (xml) {
          rememberArchitectureXml(xml);
          scheduleEditorChange(hostId, xml, currentLogicXml);
        }
      });

      state.downloadLinkObserver.observe(downloadLink, {
        attributes: true,
        attributeFilter: ["href"]
      });
    }
  }

  function hasRenderedArchitecture() {
    const canvas = document.getElementById("js-canvas");
    return !!canvas?.querySelector(".djs-element, .djs-shape, .djs-connection");
  }

  function normalizeArchitectureXml(xml) {
    if (typeof xml !== "string") {
      return defaultArchitectureXml;
    }

    const normalizedXml = xml.trim();
    return normalizedXml || defaultArchitectureXml;
  }

  function getActiveEditorState() {
    if (activeHostId && editorStates.has(activeHostId)) {
      return editorStates.get(activeHostId);
    }

    let latestState;
    for (const state of editorStates.values()) {
      latestState = state;
    }

    return latestState;
  }

  function rememberArchitectureXml(xml) {
    if (!xml?.trim()) {
      return;
    }

    currentArchitectureXml = xml;

    if (activeHostId) {
      getState(activeHostId).xml = xml;
    }
  }

  function getDownloadLinkXml() {
    const href = document.getElementById("js-download-diagram")?.getAttribute("href") ?? "";
    const prefix = "data:application/bpmn20-xml;charset=UTF-8,";
    if (!href.startsWith(prefix)) {
      return "";
    }

    try {
      return decodeUriComponent(href.slice(prefix.length)).trim();
    } catch (error) {
      console.warn("Could not decode the current simpleXflow download XML.", error);
      return "";
    }
  }

  function getFallbackArchitectureXml() {
    return normalizeArchitectureXml(getDownloadLinkXml() || currentArchitectureXml || getActiveEditorState()?.xml || "");
  }

  async function waitForCallback(name, timeoutMs) {
    if (getCallbackList(name).length > 0) {
      return true;
    }

    const startedAt = window.performance.now();

    return new Promise((resolve) => {
      const interval = window.setInterval(() => {
        if (getCallbackList(name).length > 0) {
          window.clearInterval(interval);
          resolve(true);
          return;
        }

        if (window.performance.now() - startedAt >= timeoutMs) {
          window.clearInterval(interval);
          resolve(false);
        }
      }, 50);
    });
  }

  function openLogic(logicXml) {
    currentLogicXml = logicXml ?? "";
    // Reset the logic canvas when changing to a model without saved logic.
    return emit("openLogic", currentLogicXml.trim() || defaultArchitectureXml);
  }

  async function replayOpenXml(editorXml, logicXml, logicTargetElementId) {
    const attemptId = ++openAttemptId;
    document.getElementById("js-download-diagram")?.removeAttribute("href");
    if (window.simpleXflowModelers?.architecture) {
      await emit("openXmlFile", editorXml);
      if (attemptId !== openAttemptId) {
        return;
      }
      await openLogic(logicXml);
      const modeler = window.simpleXflowModelers.architecture;
      const target = logicTargetElementId && modeler.get("elementRegistry").get(logicTargetElementId);
      if (target) {
        modeler.get("selection").select(target);
      }
      const state = getState(activeHostId);
      delete state.architectureSize;
      delete state.logicSize;
      dispatchEditorResize();
      return;
    }
    const open = () => {
      if (attemptId !== openAttemptId) {
        return;
      }

      emit("openXmlFile", editorXml);
      openLogic(logicXml);
      dispatchEditorResize();
    };

    open();
    window.requestAnimationFrame(() => {
      if (attemptId === openAttemptId) {
        open();
      }
    });

    for (const delay of [120, 420, 900, 1500, 2500]) {
      window.setTimeout(() => {
        if (attemptId !== openAttemptId) {
          return;
        }

        if (!hasRenderedArchitecture()) {
          open();
        } else {
          dispatchEditorResize();
        }
      }, delay);
    }
  }

  function attachLogicToElement(xml, logicXml, targetElementId) {
    if (!xml || !logicXml?.trim() || !targetElementId?.trim()) {
      return xml;
    }

    const escapedElementId = targetElementId.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    const elementPattern = new RegExp(`(<[^!?/][^>]*\\bid=["']${escapedElementId}["'][^>]*)(>)`, "i");
    const escapedLogic = logicXml
      .replace(/&/g, "&amp;")
      .replace(/"/g, "&quot;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/\r/g, "&#13;")
      .replace(/\n/g, "&#10;")
      .replace(/\t/g, "&#9;");

    return xml.replace(elementPattern, (_match, start, end) => {
      const withoutExistingLogic = start.replace(/\scontent=(?:"[^"]*"|'[^']*')/i, "");
      const selfClosing = /\/\s*$/.test(withoutExistingLogic);
      const openingTag = withoutExistingLogic.replace(/\/\s*$/, "").trimEnd();
      return `${openingTag} content="${escapedLogic}"${selfClosing ? "/" : ""}${end}`;
    });
  }

  async function initialize(hostId, xml, logicXml, logicTargetElementId, dotNetReference) {
    await ensureBundle();
    const host = document.getElementById(hostId);
    if (!host) {
      throw new Error("The simpleXflow editor host is missing.");
    }

    const editorXml = normalizeArchitectureXml(attachLogicToElement(xml, logicXml, logicTargetElementId));
    activeHostId = hostId;
    suppressChangeNotifications(hostId);
    window.clearTimeout(getState(hostId).changeTimer);
    Object.assign(getState(hostId), { xml: editorXml, logicXml, logicTargetElementId, loading: true, dirty: false, changeVersion: getChangeVersion(hostId) + 1 });
    rememberArchitectureXml(editorXml);
    await waitForCallback("openXmlFile", 2500);
    await replayOpenXml(editorXml, logicXml, logicTargetElementId);
    getState(hostId).loading = false;
    if (dotNetReference) watchChanges(hostId, dotNetReference);
    host.classList.add("is-ready");
    window.markAsClean?.();
  }

  async function openXml(hostId, xml, logicXml, logicTargetElementId) {
    const editorXml = normalizeArchitectureXml(attachLogicToElement(xml, logicXml, logicTargetElementId));
    activeHostId = hostId;
    suppressChangeNotifications(hostId);
    window.clearTimeout(getState(hostId).changeTimer);
    Object.assign(getState(hostId), { xml: editorXml, logicXml, logicTargetElementId, loading: true, dirty: false, changeVersion: getChangeVersion(hostId) + 1 });
    rememberArchitectureXml(editorXml);
    await ensureBundle();
    await waitForCallback("openXmlFile", 2500);
    await replayOpenXml(editorXml, logicXml, logicTargetElementId);
    getState(hostId).loading = false;
    window.markAsClean?.();
  }

  async function getXml() {
    await ensureBundle();

    const modelers = window.simpleXflowModelers;
    if (modelers?.architecture) {
      // The download link is debounced. Explicit saves must include the live
      // logic and architecture, including edits made immediately before export.
      if (modelers.logic && currentLogicXml.trim()) {
        const logic = await modelers.logic.saveXML({ format: true });
        currentLogicXml = logic.xml;
        await emit("saveLogic", logic.xml);
      }
      const { xml } = await modelers.architecture.saveXML({ format: true });
      rememberArchitectureXml(xml);
      return xml;
    }

    suppressChangeNotifications(activeHostId, 1600);
    const editorXml = getFallbackArchitectureXml();
    rememberArchitectureXml(editorXml);
    suppressChangeNotifications(activeHostId, 900);
    return editorXml;
  }

  async function getLogic() {
    await ensureBundle();
    return currentLogicXml;
  }

  function resize(hostId) {
    window.requestAnimationFrame(() => {
      const host = document.getElementById(hostId);
      if (!host) {
        return;
      }

      dispatchEditorResize();
    });
  }

  function watchChanges(hostId, dotNetReference) {
    const state = getState(hostId);
    state.dotNetReference = dotNetReference;
    state.resizeObserver?.disconnect();
    state.resizeObserver = new ResizeObserver(dispatchEditorResize);
    for (const id of [hostId, "js-canvas", "js-simbpmncanvas"]) {
      const element = document.getElementById(id);
      if (element) state.resizeObserver.observe(element);
    }
    if (window.simpleXflowModelers?.architecture) {
      installChangeObserver(hostId);
    } else {
      suppressChangeNotifications(hostId, 1200);
      window.requestAnimationFrame(() => installChangeObserver(hostId));
    }
  }

  function setReadOnly(hostId, readOnly) {
    const state = getState(hostId);
    state.readOnly = !!readOnly;
    const host = document.getElementById(hostId);
    if (!host) return;
    host.classList.toggle("is-readonly", state.readOnly);
    for (const [name, modeler] of Object.entries(window.simpleXflowModelers ?? {})) {
      if (modeler.simpleXflowReadOnlyGuards) continue;
      modeler.simpleXflowReadOnlyGuards = true;
      const guards = [
        [modeler.get("rules"), "allowed", false],
        [modeler.get("directEditing"), "activate", false],
        [modeler.get("commandStack"), "undo"],
        [modeler.get("commandStack"), "redo"],
        [modeler.get("commandStack"), "execute"]
      ];
      for (const [service, method, deniedResult] of guards) {
        const original = service[method];
        service[method] = function (...args) {
          if (getState(hostId).readOnly && !(method === "execute" && modelSyncDepth[name] > 0)) {
            return deniedResult;
          }
          return original.apply(this, args);
        };
      }
    }
    const updateControls = () => {
      for (const element of host.querySelectorAll("#js-properties-panel input, #js-properties-panel textarea, #js-properties-panel select, #js-properties-panel [contenteditable]")) {
        if (state.readOnly) {
          if (!element.hasAttribute("data-simplexflow-disabled")) {
            element.dataset.simplexflowDisabled = String(!!element.disabled);
            element.dataset.simplexflowEditable = element.getAttribute("contenteditable") ?? "";
          }
          element.disabled = true;
          if (element.hasAttribute("contenteditable")) element.setAttribute("contenteditable", "false");
        } else if (element.hasAttribute("data-simplexflow-disabled")) {
          element.disabled = element.dataset.simplexflowDisabled === "true";
          if (element.dataset.simplexflowEditable) element.setAttribute("contenteditable", element.dataset.simplexflowEditable);
          delete element.dataset.simplexflowDisabled;
          delete element.dataset.simplexflowEditable;
        }
      }
    };
    state.readOnlyObserver?.disconnect();
    state.readOnlyObserver = new MutationObserver(updateControls);
    state.readOnlyObserver.observe(host, { childList: true, subtree: true });
    updateControls();
    if (!document.getElementById("simplexflow-readonly-style")) {
      const style = document.createElement("style");
      style.id = "simplexflow-readonly-style";
      style.textContent = ".simplexflow-editor-host.is-readonly .djs-palette,.simplexflow-editor-host.is-readonly .djs-context-pad{display:none!important}";
      document.head.appendChild(style);
    }
  }

  function unwatchChanges(hostId) {
    if (!hostId || !editorStates.has(hostId)) {
      return;
    }

    const state = editorStates.get(hostId);
    window.clearTimeout(state.changeTimer);
    state.resizeObserver?.disconnect();
    state.readOnlyObserver?.disconnect();
    state.downloadLinkObserver?.disconnect();
    state.modelerListeners?.forEach(({ eventBus, listener }) => eventBus.off("commandStack.changed", listener));
    state.changeEventTargets?.forEach(({ target, listener }) => {
      for (const eventName of listener.eventNames) {
        target.removeEventListener(eventName, listener.handle, true);
      }
    });
    delete state.downloadLinkObserver;
    delete state.changeEventTargets;
    delete state.modelerListeners;
    delete state.dotNetReference;
  }

  const translations = {
    Change: "Ändern",
    Specifications: "Spezifikationen",
    WorkspaceLocation: "Workspace",
    WorkspaceLocationDescription: "Cloudbasierter, tenant-isolierter simpleXflow Workspace.",
    WorkspaceLocationLabel: "Ablage",
    Language: "Sprache",
    LanguageDescription: "Sprache der Modellierungsoberfläche.",
    Create: "Erstellen",
    Search: "Suchen"
  };

  return {
    initialize,
    openXml,
    getXml,
    getLogic,
    getChangeVersion,
    isDirty: hostId => !!getState(hostId).dirty,
    clearDirty,
    resize,
    watchChanges,
    setReadOnly,
    unwatchChanges
  };
})();
