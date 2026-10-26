# Organizations, workspaces and models

Each account belongs to one organization. Registration creates that organization; the existing `Tenant` and `TenantId` database names are retained to preserve account data. An organization can start with no workspaces.

The navigation follows this hierarchy:

The compact navigation was compared with the signed-in [Camunda Hub](https://hub.camunda.io/workspaces)
on 2026-10-26. The global menu opens on demand, while the organization/workspace/project/model
breadcrumb lives in the header. Workspace cards offer Open and Manage actions, search, organization
filters, sorting and a list view. The model list and history panel open only when needed.
The organization/workspace rules below follow the requested simpleXflow behavior.

```text
Organization
  Workspace
    Project
      Folder / nested folder
        Model (architecture + logic)
```

Workspaces belong to an organization and have explicit members. An invited member keeps their own organization and sees the external workspace under shared workspaces. Organization membership alone does not grant access to every workspace in that organization.

A project is a container for models. The existing `FlowProject` entity and `/projects/{id}` route continue to identify an individual model for compatibility with saved links and the editor. New `WorkspaceProject` records represent project containers. A model stores its folder path relative to its project, separately from its display name.

Model names are unique within their project and folder. The same name can be reused in another project or folder; creating another model in the same location adds a numeric suffix.

## Workspace roles

| Action | Owner | Admin | Editor | Viewer |
| --- | --- | --- | --- | --- |
| Read models, history and work packages | Yes | Yes | Yes | Yes |
| Create projects, folders and models | Yes | Yes | Yes | No |
| Edit models and create checkpoints | Yes | Yes | Yes | No |
| Delete models, restore history, plan and release versions | Yes | Yes | No | No |
| Invite, change or remove Editors and Viewers | Yes | Yes | No | No |
| Appoint or remove Admins | Yes | No | No | No |
| Create and delegate work packages | Yes | Yes | No | No |
| Update status of an assigned work package | Yes | Yes | Yes | Yes |

Every workspace has its creator as Owner. That membership cannot be removed or demoted. Ownership transfer is not currently exposed. Permissions are checked in the service layer, including after membership is changed while another member has the editor open.

## Invitations

An administrator creates an invitation for a specific email address and role. The returned link is shared with that person. Links expire after seven days and can be revoked or replaced. The database stores only a hash of the random token. Accepting a link requires signing in with the matching, confirmed email address; accepting it does not change the account's organization.

Account email confirmation requires the configured email sender. Set `Email:Smtp:Host` and `Email:Smtp:From`, plus `Email:Smtp:UserName` and `Email:Smtp:Password` if the server requires authentication. Port defaults to 587 and TLS (`Email:Smtp:EnableSsl`) defaults to enabled. Environment variables use double underscores, for example `Email__Smtp__Host`; keep the password in deployment secrets. Without delivery configuration, users can still use their own workspace, but external membership cannot bypass email verification.

## Saving and collaboration

Model changes are autosaved. Every changed workspace-model save creates a snapshot containing both architecture and logic, its author and creation time. Explicit checkpoints can be named and optionally assigned a planned release date. Owner/Admin can mark a version as released. Restoring a version appends a new history entry; it does not remove the previous history. A planned date is metadata, not an automatic scheduled release.

The editor sends the revision it loaded with every save. A save based on an older revision is rejected with a reload message, preserving the server's newer model. The server returns the exact revision it saved. Collaboration uses this whole-model conflict check; it does not merge simultaneous diagram edits automatically. Local editor undo remains separate from saved version history.

The editor sends presence heartbeats approximately every 25 seconds. Other active editors are shown by name/email. Entries expire after 75 seconds without a heartbeat, including after a lost connection. Presence indicates recent activity and does not lock the file or authorize writes. Concurrent calls within a Blazor circuit are serialized before using its scoped database context.

Work packages have a title, description, optional model, assigned workspace member, due date and status (`Open`, `In progress`, `Done`). Owner/Admin controls delegation and deadlines. An assignee can update status without changing the assignment or scope.

## Local files and folders

Exporting a `.sxf` file preserves the model name, architecture XML and logic XML together. Browsers that support the save-file picker allow choosing the local path and filename. Other browsers use their download flow and configured download location. A web application cannot silently write an arbitrary local filesystem path.

Workspace folders organize the persisted models independently of local export paths. Relative folder paths support nested folders; traversal segments and drive paths are rejected. Deleting a model also removes its saved versions and presence entries; any linked work package keeps its other fields and loses the model reference.

## Existing data and database upgrades

PostgreSQL and SQL Server have `AddWorkspaceCollaboration` migrations. Local SQLite databases created by earlier versions with `EnsureCreated` receive an additive schema upgrade. Existing models, architecture/logic XML and saved versions are preserved; startup does not recreate the database.

On the first workspace listing for an account with older, uncontained models, those models are placed in `My workspace` / `Imported models`. Existing users of that organization become explicit members to retain their previous access; the user performing that migration becomes Owner and the others become Editors. New organizations with no models remain empty. A concurrency check prevents two simultaneous visits from moving the same models twice.

Backwards-compatible sample and model creation without an explicit project target creates or reuses the authenticated user's own workspace/project. External shared workspaces are never chosen implicitly for this fallback.

## Validation

Infrastructure tests cover cross-organization access, hidden workspaces, invitation expiry/verification/revocation and role checks, concurrent stale saves, revoked membership in an existing circuit, audit restoration and release, deletion, presence expiry, work-package delegation, path normalization, legacy model migration and repeatable SQLite upgrades. Migration snapshot checks cover both PostgreSQL and SQL Server.
