# GitHub Deployment

Recommended setup:

- GitHub repository for source code, issues, pull requests, and Actions.
- Azure App Service for hosting the Blazor Web App.
- Azure SQL Database Free for production data.

## 1. Create the Azure App Service

In the Azure Portal:

1. Create a Resource Group, for example `rg-simplexflow`.
2. Create an App Service Plan.
3. Create an App Service Web App.
4. Choose Linux and the .NET runtime matching the project target framework.
5. Give the app a globally unique name, for example `simplexflow`.

## 2. Create the Azure SQL Database Free database

In the Azure Portal:

1. Create an Azure SQL Database.
2. On the Compute + storage step, apply the Free database offer if it is available for your subscription.
3. Choose the free limit behavior:
   - auto-pause until next month when the free limit is reached, or
   - continue with charges when the free limit is exceeded.
4. Create or select a SQL Server for the database.
5. Set the firewall so the App Service can connect.
6. Copy the ADO.NET connection string and insert the SQL admin password.

## 3. Configure production settings

In the App Service, open Settings > Environment variables and add:

```text
Database__Provider=SqlServer
ConnectionStrings__DefaultConnection=<Azure SQL ADO.NET connection string>
ASPNETCORE_ENVIRONMENT=Production
```

App Service environment variables override `appsettings.json`, so local development can keep using SQLite.

## 4. Add the GitHub secret

In Azure App Service:

1. Open Overview.
2. Download the publish profile.
3. Copy the full file contents.

In GitHub:

1. Open the repository.
2. Go to Settings > Secrets and variables > Actions.
3. Create a repository secret named `AZURE_WEBAPP_PUBLISH_PROFILE`.
4. Paste the publish profile contents as the value.

## 5. Enable the workflow

Open `.github/workflows/deploy-azure-app-service.yml` and replace:

```text
REPLACE_WITH_APP_SERVICE_NAME
```

with the exact Azure App Service name.

Push to `main` or `master`, or run the workflow manually from the GitHub Actions tab.

## Notes

Publish profiles are the quickest setup. For stricter production security, replace the publish profile with Azure OpenID Connect later.

The app creates the database schema on startup with EF Core `EnsureCreatedAsync()`. That keeps the first prototype easy to deploy. Before heavier production use, switch to EF Core migrations so database changes are versioned.
