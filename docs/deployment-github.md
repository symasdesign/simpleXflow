# Legacy Azure App Service Deployment

The App Service deployment path was removed after switching simpleXflow to Azure Container Apps.

Current setup:

- GitHub repository for source code, issues, pull requests, and Actions.
- GitHub Actions build and publish a container image to GitHub Container Registry.
- Azure Container Apps hosts the Blazor Web App.
- Azure SQL Database Free remains the production database.

Use `docs/deployment-container-apps.md` for the active deployment path.

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

## Production settings

In Azure Container Apps, open Settings > Environment variables and add:

```text
Database__Provider=SqlServer
ConnectionStrings__DefaultConnection=<Azure SQL ADO.NET connection string>
ASPNETCORE_ENVIRONMENT=Production
DataProtection__KeyPath=/tmp/simplexflow/DataProtectionKeys
```

Azure environment variables override `appsettings.json`, so local development can keep using SQLite.

## Notes

The app creates the database schema on startup with EF Core `EnsureCreatedAsync()`. That keeps the first prototype easy to deploy. Before heavier production use, switch to EF Core migrations so database changes are versioned.
