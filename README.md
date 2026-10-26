# simpleXflow

simpleXflow is the Blazor WebApp successor for the former Electron-based modeling tool.

## Structure

- `src/SimpleXflow.Domain`: tenant and simpleXflow project domain model
- `src/SimpleXflow.Application`: use-case contracts and DTOs
- `src/SimpleXflow.Infrastructure`: EF Core, Identity user, tenant context, project persistence
- `src/SimpleXflow.Web`: Blazor UI, authentication screens, tenant registration flow

## Current foundation

- Blazor Server interactivity
- ASP.NET Core Identity registration and login
- One organization per registered user at account creation, with shared workspaces, projects and model folders
- Tenant ID is emitted as an auth claim
- EF Core query filters and service permissions enforce explicit workspace membership, including invited members from other organizations
- Workspace Owner/Admin/Editor/Viewer roles, expiring invitations and verified-email acceptance
- Model history, planned/manual releases, optimistic save conflicts, editor presence and delegated work packages
- SQLite locally, PostgreSQL/Neon for low-cost shared deployments via configuration
- Azure SQL remains available as a fallback provider
- Compact workspace hub and visual simpleXflow editor using the migrated modeler bundle
- Architecture, split, and logic views backed by the previous Electron visualizer behavior
- Built-in paper and poster samples for presentation demos
- In-app Help/Get Started page
- Domain, application, persistence, HTTP, import, editor bridge and browser regression tests
- GitHub Actions pipeline for build, test, and container image publishing
- Automatic Azure Container Apps deployment after successful builds on main, with manual rollback

## Run

```powershell
dotnet run --project src/SimpleXflow.Web/SimpleXflow.Web.csproj
```

The app creates the local SQLite database automatically on startup.

## Deploy

See `docs/deployment-container-apps.md` for the current GitHub Actions, Azure Container Apps, and PostgreSQL/Neon setup.
See `docs/database-postgres-neon.md` for the database switch.
See `docs/deployment-github.md` only for legacy App Service notes.

## Paper Demo

See `docs/paper-demo.md` for the EUROSIM 2026 presentation setup and built-in paper samples.

## Next migration steps

1. Replace the compatibility bundle with first-class source-based frontend bundling.
2. Add import/export endpoints for BPMN ZIP packages and tenant-scoped attachments.
3. Add static model validation for simpleXflow semantics.
4. Add a one-time data migration path from Azure SQL to PostgreSQL if existing demo data must be preserved.

## Tests

Run the complete suite from the repository root with PowerShell 7, .NET 10 and Node.js 22:

```powershell
./scripts/test.ps1 -InstallBrowsers  # First run: restore dependencies and install Chromium
./scripts/test.ps1                  # Subsequent complete runs
./scripts/test.ps1 -SkipBrowser     # .NET unit and integration tests with coverage
```

The tests use isolated SQLite databases and local browser sessions. No cloud credentials are needed.
See [Testing](docs/testing.md) for individual suites, reports, test design and coverage boundaries.

See [Workspaces and collaboration](docs/workspaces.md) for hierarchy, permissions, invitations, local files and migration behavior.
