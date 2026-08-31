# Legacy Azure App Service Deployment

The App Service deployment path was removed after switching simpleXflow to Azure Container Apps.

Current setup:

- GitHub repository for source code, issues, pull requests, and Actions.
- GitHub Actions build and publish a container image to GitHub Container Registry.
- Azure Container Apps hosts the Blazor Web App.
- PostgreSQL/Neon is the preferred production database for the public prototype.
- Azure SQL Database Free remains available as a fallback provider.

Use `docs/deployment-container-apps.md` for the active deployment path.
Use `docs/database-postgres-neon.md` for the active database setup.

## Azure SQL Database Free

In the Azure Portal:

1. Create an Azure SQL Database.
2. On the Compute + storage step, apply the Free database offer if it is available for your subscription.
3. Choose the free limit behavior:
   - auto-pause until next month when the free limit is reached, or
   - continue with charges when the free limit is exceeded.
4. Create or select a SQL Server for the database.
5. Set the firewall so the Container App can connect.
6. Copy the ADO.NET connection string and insert the SQL admin password.

For Container Apps, allow the Container App egress path to connect to the SQL Server or configure the SQL firewall accordingly.

## PostgreSQL production settings

In Azure Container Apps, open Settings > Environment variables and add:

```text
Database__Provider=Postgres
ConnectionStrings__DefaultConnection=<PostgreSQL or Neon .NET/Npgsql connection string>
ASPNETCORE_ENVIRONMENT=Production
DataProtection__KeyPath=/tmp/simplexflow/DataProtectionKeys
```

The manual GitHub workflow `Configure Azure database` stores the connection string as an Azure Container Apps secret and sets these values.

## Azure SQL fallback settings

In Azure Container Apps, open Settings > Environment variables and add:

```text
Database__Provider=SqlServer
ConnectionStrings__DefaultConnection=<Azure SQL ADO.NET connection string>
ASPNETCORE_ENVIRONMENT=Production
DataProtection__KeyPath=/tmp/simplexflow/DataProtectionKeys
```

Azure environment variables override `appsettings.json`, so local development can keep using SQLite.

## Notes

The app applies EF Core migrations on startup for PostgreSQL and SQL Server/Azure SQL. Local SQLite development keeps using `EnsureCreatedAsync()` for low-friction setup.
