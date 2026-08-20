# Azure Container Apps Deployment

This is the preferred path once simpleXflow should use custom domains without paying for a permanently allocated Azure App Service Basic instance.

## Architecture

- GitHub Actions builds and tests the solution.
- GitHub Actions publishes a Linux container image to GitHub Container Registry.
- Azure Container Apps runs the image on the Consumption plan.
- Azure SQL Database Free remains the production database.
- Hostpoint domains point to Azure Container Apps.

## Container image

The image is published by `.github/workflows/container-image.yml`:

```text
ghcr.io/symasdesign/simplexflow:latest
ghcr.io/symasdesign/simplexflow:<commit-sha>
```

The container listens on port `8080`.

For the lowest-friction Azure Container Apps setup, make the GitHub Container Registry package public after the first successful workflow run:

```text
GitHub repository > Packages > simplexflow > Package settings > Change visibility > Public
```

If the package must stay private, create a GitHub token with `read:packages` permission and configure it as registry credentials in Azure Container Apps.

## Required Container App settings

Configure these environment variables on the Container App:

```text
Database__Provider=SqlServer
ConnectionStrings__DefaultConnection=<Azure SQL ADO.NET connection string>
ASPNETCORE_ENVIRONMENT=Production
DataProtection__KeyPath=/tmp/simplexflow/DataProtectionKeys
```

Set ingress to external HTTP ingress and target port `8080`.

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
