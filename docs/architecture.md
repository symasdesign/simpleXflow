# Architecture

simpleXflow follows a pragmatic clean architecture layout.

## Domain

The domain layer contains entities without framework dependencies:

- `Tenant`
- `FlowProject`
- `ProjectAttachment`

Tenant-owned data implements `ITenantEntity`.

## Application

The application layer exposes use-case contracts. The web UI depends on these contracts instead of directly talking to EF Core.

Current contract:

- `IFlowProjectService`

## Infrastructure

Infrastructure owns persistence, Identity integration, and tenant resolution.

- `ApplicationUser` extends ASP.NET Core Identity with `TenantId`
- `TenantClaimsPrincipalFactory` adds `tenant_id` to the signed-in user
- `CurrentTenantContext` reads the tenant claim from the current request
- `ApplicationDbContext` uses EF Core global query filters for tenant-owned entities

## Web

The Blazor app owns presentation and account pages. Registration creates a tenant first, assigns it to the new user, and signs the user into an isolated workspace.

## Persistence

SQLite is the local default. PostgreSQL is the preferred shared deployment database because it can run on free/low-cost serverless providers such as Neon. Azure SQL remains available as a fallback.

- `Sqlite`: uses `UseSqlite` with the local `DefaultConnection`
- `Postgres` or `PostgreSql`: uses `UseNpgsql` with the configured `DefaultConnection`
- `SqlServer` or `AzureSql`: uses `UseSqlServer` with the configured `DefaultConnection`

Production-grade providers use EF Core migrations on startup. The migrations are split per provider so SQL Server and PostgreSQL can keep provider-specific column types and model snapshots:

- `Persistence/Migrations/Postgres`
- `Persistence/Migrations/SqlServer`

This keeps local development simple while allowing Azure Container Apps deployments to switch database providers through application settings.
