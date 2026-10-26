// Keep the picker in the original click gesture. The browser chooses the local path.
window.simpleXflowFiles = {
    reload() { window.location.reload(); },
    async saveModel(button) {
        const name = button.dataset.modelName || 'Model';
        const suggestedName = name.replace(/[<>:"/\\|?*\x00-\x1f]/g, '_') + '.sxf';
        button.disabled = true;
        let handle;
        try {
            if (window.showSaveFilePicker) {
                handle = await window.showSaveFilePicker({
                    suggestedName,
                    types: [{ description: 'simpleXflow model (architecture and logic)', accept: { 'application/json': ['.sxf'] } }]
                });
            }
            const bpmnXml = await window.simpleXflowEditor.getXml();
            const logicXml = await window.simpleXflowEditor.getLogic();
            if (!bpmnXml) throw new Error('The editor is still loading. Try again in a moment.');
            const file = new Blob([JSON.stringify({ format: 'simpleXflow', version: 1, name, bpmnXml, logicXml }, null, 2)], { type: 'application/json' });
            if (handle) {
                const stream = await handle.createWritable();
                await stream.write(file);
                await stream.close();
                announce('Model saved locally.');
            } else {
                const url = URL.createObjectURL(file);
                const link = document.createElement('a');
                link.href = url;
                link.download = suggestedName;
                document.body.appendChild(link);
                link.click();
                link.remove();
                setTimeout(() => URL.revokeObjectURL(url), 60000);
                announce('Download started. Your browser controls the destination folder; enable “Ask where to save” to choose a path.');
            }
        } catch (error) {
            if (error.name !== 'AbortError') announce(`Local save failed: ${error.message}`);
        } finally { button.disabled = false; }
    }
};

function announce(message) {
    document.getElementById('local-save-status')?.remove();
    const status = document.createElement('div');
    status.id = 'local-save-status';
    status.className = 'local-save-status';
    status.setAttribute('role', 'status');
    status.textContent = message;
    document.body.appendChild(status);
}
