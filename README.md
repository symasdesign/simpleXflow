# simpleXflow

simpleXflow is the Blazor WebApp successor for the former Electron-based simBPMN visualizer.

## Structure

- `src/SimpleXflow.Domain`: tenant and flow project domain model
- `src/SimpleXflow.Application`: use-case contracts and DTOs
- `src/SimpleXflow.Infrastructure`: EF Core, Identity user, tenant context, project persistence
- `src/SimpleXflow.Web`: Blazor UI, authentication screens, tenant registration flow

## Current foundation

- Blazor Server interactivity
- ASP.NET Core Identity registration and login
- One tenant per registered user at account creation
- Tenant ID is emitted as an auth claim
- EF Core global query filters isolate tenant-owned project data
- SQLite locally, Azure SQL Database for shared deployments via configuration
- Visual simpleXflow workspace using the migrated simBPMN/bpmn-js modeler bundle
- Architecture, split, and logic views backed by the previous Electron visualizer behavior
- Domain and infrastructure unit tests
- GitHub Actions pipeline for build, test, package, and Azure App Service deployment
- Container image pipeline for Azure Container Apps migration

## Run

```powershell
dotnet run --project src/SimpleXflow.Web/SimpleXflow.Web.csproj
```

The app creates the local SQLite database automatically on startup.

## Deploy

See `docs/deployment-github.md` for the current GitHub Actions, Azure App Service, and Azure SQL Database setup.
See `docs/deployment-container-apps.md` for the container-based Azure Container Apps path.

## Next migration steps

1. Replace the compatibility bundle with first-class source-based frontend bundling.
2. Add import/export endpoints for BPMN ZIP packages and tenant-scoped attachments.
3. Add static model validation for simpleXflow semantics.
4. Replace startup schema creation with EF Core migrations for production-grade releases.

## Tests

```powershell
dotnet test tests/SimpleXflow.Domain.Tests/SimpleXflow.Domain.Tests.csproj
dotnet test tests/SimpleXflow.Infrastructure.Tests/SimpleXflow.Infrastructure.Tests.csproj
```
