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

  function getCallbackList(name) {
    if (!callbacks.has(name)) {
      callbacks.set(name, []);
    }

    return callbacks.get(name);
  }

  function emit(name, ...args) {
    for (const callback of getCallbackList(name)) {
      callback({}, ...args);
    }
  }

  function emitRaw(name, ...args) {
    for (const callback of getCallbackList(name)) {
      callback(...args);
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
        emit("saveLogic", xml);
        scheduleEditorChange(activeHostId, null, currentLogicXml);
      },
      openLogicRelay: async (xml) => {
        currentLogicXml = xml ?? "";
        emit("openLogic", xml);
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

    return bundlePromise;
  }

  function dispatchEditorResize() {
    window.requestAnimationFrame(() => window.dispatchEvent(new Event("resize")));
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
      && (!state.suppressChangesUntil || window.performance.now() > state.suppressChangesUntil);
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
    state.changeTimer = window.setTimeout(() => {
      if (!shouldNotifyChanges(state)) {
        return;
      }

      const pendingXml = state.pendingXml ?? null;
      const pendingLogicXml = state.pendingLogicXml ?? null;
      state.pendingXml = null;
      state.pendingLogicXml = null;

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
      const state = editorStates.get(activeHostId) ?? {};
      editorStates.set(activeHostId, { ...state, xml });
    }
  }

  function getDownloadLinkXml() {
    const href = document.getElementById("js-download-diagram")?.getAttribute("href") ?? "";
    const prefix = "data:application/bpmn20-xml;charset=UTF-8,";
    if (!href.startsWith(prefix)) {
      return "";
    }

    try {
      return decodeURIComponent(href.slice(prefix.length)).trim();
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

    if (!currentLogicXml.trim()) {
      return;
    }

    window.requestAnimationFrame(() => emit("openLogic", currentLogicXml));
  }

  function replayOpenXml(editorXml, logicXml) {
    const attemptId = ++openAttemptId;
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
      return `${withoutExistingLogic} content="${escapedLogic}"${end}`;
    });
  }

  async function initialize(hostId, xml, logicXml, logicTargetElementId) {
    await ensureBundle();
    const host = document.getElementById(hostId);
    if (!host) {
      throw new Error("The simpleXflow editor host is missing.");
    }

    const editorXml = normalizeArchitectureXml(attachLogicToElement(xml, logicXml, logicTargetElementId));
    activeHostId = hostId;
    suppressChangeNotifications(hostId);
    editorStates.set(hostId, { ...getState(hostId), xml: editorXml, logicXml, logicTargetElementId });
    rememberArchitectureXml(editorXml);
    await waitForCallback("openXmlFile", 2500);
    replayOpenXml(editorXml, logicXml);
    host.classList.add("is-ready");
    window.markAsClean?.();
  }

  async function openXml(hostId, xml, logicXml, logicTargetElementId) {
    const editorXml = normalizeArchitectureXml(attachLogicToElement(xml, logicXml, logicTargetElementId));
    activeHostId = hostId;
    suppressChangeNotifications(hostId);
    editorStates.set(hostId, { ...getState(hostId), xml: editorXml, logicXml, logicTargetElementId });
    rememberArchitectureXml(editorXml);
    await ensureBundle();
    await waitForCallback("openXmlFile", 2500);
    replayOpenXml(editorXml, logicXml);
    window.markAsClean?.();
  }

  async function getXml() {
    await ensureBundle();
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
    suppressChangeNotifications(hostId, 1200);
    window.requestAnimationFrame(() => installChangeObserver(hostId));
  }

  function unwatchChanges(hostId) {
    if (!hostId || !editorStates.has(hostId)) {
      return;
    }

    const state = editorStates.get(hostId);
    window.clearTimeout(state.changeTimer);
    state.downloadLinkObserver?.disconnect();
    state.changeEventTargets?.forEach(({ target, listener }) => {
      for (const eventName of listener.eventNames) {
        target.removeEventListener(eventName, listener.handle, true);
      }
    });
    delete state.downloadLinkObserver;
    delete state.changeEventTargets;
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
    resize,
    watchChanges,
    unwatchChanges
  };
})();
