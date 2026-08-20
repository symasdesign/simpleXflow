# Visualizer Migration

The Blazor workspace uses the existing Electron visualizer as a compatibility bundle.

## Scope

The migrated bundle includes the bpmn-js based modelers for:

- system architecture
- split architecture / logic view
- system logic
- regular BPMN notation and simpleXflow custom elements
- custom palette, context pad, renderers, rules, properties panel, minimap, color picker
- architecture-to-logic resource synchronization

Blazor owns authentication, tenant selection through claims, persistence, and project navigation. The visual editor is hosted by `SimpleXflowModeler.razor` and bridged through `wwwroot/js/simplexflow-editor.js`.

## Existing Project Import

The Projects page can import existing simpleXflow project files from the browser. Supported formats are:

- `.bpmn` and `.xml` files containing BPMN definitions
- `.zip` exports containing at least one `.bpmn` file

Imported files are stored as tenant-scoped `FlowProject` records. If a project with the same name already exists for the tenant, the service appends a suffix such as `(1)` instead of failing on the unique tenant/name index.

## Paper Samples

The Projects page also includes built-in paper samples. They are loaded as drafts and only become tenant data when the user saves them:

- `Paper sample - Coffee break`
- `Paper sample - M/M/1 queue`

The samples are intended for the EUROSIM 2026 presentation and are documented in `docs/paper-demo.md`.

## Conceptual Alignment

The WSC 2026 paper describes simpleXflow as a semiformal language based on BPMN notation with separate system architecture and system logic. It emphasizes entities, tokens, resources, queues, buffers, and time-dependent behavior. The migrated editor keeps that conceptual separation through architecture, split, and logic tabs.

## Follow-Up

The current integration is intentionally a compatibility layer. It gives the Blazor app feature parity quickly while preserving the old modeling behavior. The next architectural step should be to move the original JavaScript source into a dedicated frontend package and build it from source instead of serving the copied Webpack output.
