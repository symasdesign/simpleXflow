window.simpleXflowEditor = (() => {
  const scriptUrl = "/vendor/simbpmn/main_window/index.js";
  const callbacks = new Map();
  const editorStates = new Map();
  let bundlePromise;

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
      saveLogicRelay: async (xml) => emit("saveLogic", xml),
      openLogicRelay: async (xml) => emit("openLogic", xml),
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
        script.onerror = () => reject(new Error("Could not load simBPMN editor bundle."));
        document.body.appendChild(script);
      });
    }

    return bundlePromise;
  }

  async function initialize(hostId, xml) {
    await ensureBundle();
    const host = document.getElementById(hostId);
    if (!host) {
      return;
    }

    editorStates.set(hostId, { xml });
    emit("openXmlFile", xml);
    window.markAsClean?.();
  }

  async function openXml(hostId, xml) {
    editorStates.set(hostId, { xml });
    await ensureBundle();
    emit("openXmlFile", xml);
    window.markAsClean?.();
  }

  async function getXml() {
    await ensureBundle();

    return new Promise((resolve, reject) => {
      const timeout = window.setTimeout(() => reject(new Error("Timed out while saving the diagram.")), 8000);

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
    getXml
  };
})();
