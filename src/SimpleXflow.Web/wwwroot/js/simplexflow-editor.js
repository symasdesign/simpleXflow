window.simpleXflowEditor = (() => {
  const scriptUrl = "/vendor/simbpmn/main_window/index.js";
  const callbacks = new Map();
  const editorStates = new Map();
  let bundlePromise;
  let currentLogicXml = "";

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
          resolve();
          return;
        }

        const script = document.createElement("script");
        script.src = scriptUrl;
        script.async = false;
        script.onload = () => resolve();
        script.onerror = () => reject(new Error("Could not load the simpleXflow editor bundle."));
        document.body.appendChild(script);
      });
    }

    return bundlePromise;
  }

  function openLogic(logicXml) {
    currentLogicXml = logicXml ?? "";

    if (!currentLogicXml.trim()) {
      return;
    }

    window.requestAnimationFrame(() => emit("openLogic", currentLogicXml));
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
      return;
    }

    const editorXml = attachLogicToElement(xml, logicXml, logicTargetElementId);
    editorStates.set(hostId, { xml: editorXml, logicXml, logicTargetElementId });
    emit("openXmlFile", editorXml);
    openLogic(logicXml);
    host.classList.add("is-ready");
    window.markAsClean?.();
  }

  async function openXml(hostId, xml, logicXml, logicTargetElementId) {
    const editorXml = attachLogicToElement(xml, logicXml, logicTargetElementId);
    editorStates.set(hostId, { xml: editorXml, logicXml, logicTargetElementId });
    await ensureBundle();
    emit("openXmlFile", editorXml);
    openLogic(logicXml);
    window.markAsClean?.();
  }

  async function getXml() {
    await ensureBundle();

    return new Promise((resolve, reject) => {
      const timeout = window.setTimeout(
        () => reject(new Error("The visual simpleXflow editor did not respond while saving.")),
        3000);

      const event = {
        sender: {
          send: (channel, xml) => {
            if (channel === "xml-value") {
              window.clearTimeout(timeout);
              resolve(xml);
            }
          }
        }
      };

      emitRaw("createXmlFile", event);
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

      window.dispatchEvent(new Event("resize"));
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
