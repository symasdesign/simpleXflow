window.simpleXflowEditor = (() => {
  const scriptUrl = "/vendor/simbpmn/main_window/index.js";
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

  function hasRenderedArchitecture() {
    const canvas = document.getElementById("js-canvas");
    return !!canvas?.querySelector(".djs-element, .djs-shape, .djs-connection");
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

  function getFallbackArchitectureXml() {
    return currentArchitectureXml || getActiveEditorState()?.xml || "";
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

    const editorXml = attachLogicToElement(xml, logicXml, logicTargetElementId);
    activeHostId = hostId;
    editorStates.set(hostId, { xml: editorXml, logicXml, logicTargetElementId });
    rememberArchitectureXml(editorXml);
    await waitForCallback("openXmlFile", 2500);
    replayOpenXml(editorXml, logicXml);
    host.classList.add("is-ready");
    window.markAsClean?.();
  }

  async function openXml(hostId, xml, logicXml, logicTargetElementId) {
    const editorXml = attachLogicToElement(xml, logicXml, logicTargetElementId);
    activeHostId = hostId;
    editorStates.set(hostId, { xml: editorXml, logicXml, logicTargetElementId });
    rememberArchitectureXml(editorXml);
    await ensureBundle();
    await waitForCallback("openXmlFile", 2500);
    replayOpenXml(editorXml, logicXml);
    window.markAsClean?.();
  }

  async function getXml() {
    await ensureBundle();
    await waitForCallback("createXmlFile", 1000);

    return new Promise((resolve) => {
      let settled = false;

      const finish = (xml) => {
        if (settled) {
          return;
        }

        settled = true;
        window.clearTimeout(timeout);

        const editorXml = xml?.trim() ? xml : getFallbackArchitectureXml();
        rememberArchitectureXml(editorXml);
        resolve(editorXml);
      };

      const timeout = window.setTimeout(() => finish(getFallbackArchitectureXml()), 3000);

      const event = {
        sender: {
          send: (channel, xml) => {
            if (channel === "xml-value") {
              finish(xml);
            }
          }
        }
      };

      try {
        emitRaw("createXmlFile", event);
      } catch (error) {
        console.warn("Could not export the current simpleXflow model. Saving the last known model instead.", error);
        finish(getFallbackArchitectureXml());
      }
    });
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
    resize
  };
})();
