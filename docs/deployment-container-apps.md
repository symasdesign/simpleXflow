# Azure Container Apps Deployment

This is the preferred path once simpleXflow should use custom domains without paying for a permanently allocated Azure App Service Basic instance.

## Architecture

- GitHub Actions builds and tests the solution.
- GitHub Actions publishes a Linux container image to GitHub Container Registry.
- Azure Container Apps runs the image on the Consumption plan.
- PostgreSQL/Neon is the preferred production database for the public prototype.
- Azure SQL Database remains supported as a fallback provider.
- Hostpoint domains point to Azure Container Apps.

## Current Azure resources

```text
Resource group: rg-simplexflow
Container Apps environment: cae-simplexflow
Container App: ca-simplexflow
Container App URL: https://ca-simplexflow.victoriouswave-95802155.switzerlandnorth.azurecontainerapps.io
Image: ghcr.io/symasdesign/simplexflow:latest
```

## Container image

The image is published by `.github/workflows/container-image.yml`:

```text
ghcr.io/symasdesign/simplexflow:latest
ghcr.io/symasdesign/simplexflow:<commit-sha>
```

Pushes to `main` or `master` only build, test and publish the image. They do not deploy to Azure automatically.

## Manual deployment

After the image workflow is green, deploy manually from GitHub:

1. Open GitHub > Actions.
2. Select `Deploy container to Azure`.
3. Click `Run workflow`.
4. Use `latest` for the newest successful build, or paste a full commit SHA for a deterministic release.
5. Open the Container App URL or custom domain after the workflow finishes.

The manual deployment workflow updates this Azure Container App:

```text
Resource group: rg-simplexflow
Container App: ca-simplexflow
Image: ghcr.io/symasdesign/simplexflow:<selected-tag>
```

The workflow uses Azure OIDC login. The Azure app registration `github-simplexflow-deploy` trusts only this repository subject:

```text
repo:symasdesign/simpleXflow:ref:refs/heads/main
repo:symasdesign@15147370/simpleXflow@1340624403:ref:refs/heads/main
```

The app registration is assigned the custom role `simpleXflow Container App Image Deployer` on `ca-simplexflow`. No publish profile or client secret is required.

Manual deployment from a local Azure CLI session is also possible:

```powershell
az containerapp update `
  --resource-group rg-simplexflow `
  --name ca-simplexflow `
  --image ghcr.io/symasdesign/simplexflow:latest
```

The container listens on port `8080`.

For the lowest-friction Azure Container Apps setup, make the GitHub Container Registry package public after the first successful workflow run:

```text
GitHub repository > Packages > simplexflow > Package settings > Change visibility > Public
```

If the package must stay private, create a GitHub token with `read:packages` permission and configure it as registry credentials in Azure Container Apps.

The current Azure Container App is configured with GHCR registry credentials stored as an Azure Container Apps secret.

## Required Container App settings for PostgreSQL

Configure these environment variables on the Container App:

```text
Database__Provider=Postgres
ConnectionStrings__DefaultConnection=<PostgreSQL or Neon .NET/Npgsql connection string>
ASPNETCORE_ENVIRONMENT=Production
DataProtection__KeyPath=/tmp/simplexflow/DataProtectionKeys
```

Set ingress to external HTTP ingress and target port `8080`.

For PostgreSQL and SQL Server/Azure SQL the app applies EF Core migrations on startup. Local SQLite development keeps using `EnsureCreatedAsync()` for low-friction setup.

The preferred way to switch the live Container App to PostgreSQL is the manual GitHub workflow `Configure Azure database`. See `docs/database-postgres-neon.md`.

## Azure SQL fallback settings

Azure SQL remains supported for comparison or migration work:

```text
Database__Provider=SqlServer
ConnectionStrings__DefaultConnection=<Azure SQL ADO.NET connection string>
```

The previous Azure SQL Free database can pause when the monthly free quota is exhausted. In that case the app stays online, but registration and login flows that need the database fail until the quota resets or the provider is switched.

## Local container build

```powershell
docker build -t simplexflow:local .
docker run --rm -p 8080:8080 `
  -e Database__Provider=Sqlite `
  -e ConnectionStrings__DefaultConnection="DataSource=/tmp/simplexflow.db;Cache=Shared" `
  simplexflow:local
```

Then open:

```text
http://localhost:8080
```

## Custom domains

Use `simplexflow.ch` as the canonical domain and redirect the other Hostpoint domains to it.

Recommended direct bindings:

```text
simplexflow.ch
www.simplexflow.ch
```

Keep the remaining registered domains as redirects at Hostpoint.

Azure Container Apps supports custom domains and managed certificates. For an apex domain, create the A/TXT records shown in Azure. For `www`, create the CNAME/TXT records shown in Azure.

## Notes

The container stores data-protection keys in the container file system. That is acceptable for this prototype but invalidates existing auth cookies after a redeploy or scale-to-zero restart. For heavier production use, persist ASP.NET Core Data Protection keys in Azure Blob Storage or Key Vault.
