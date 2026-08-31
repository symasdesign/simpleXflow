# PostgreSQL / Neon Database

This is the preferred database path for the public simpleXflow deployment.

## Why

Azure SQL Database Free is usable, but the monthly free quota can be exhausted. When that happens, the database can pause until the next month and registration or login flows fail. PostgreSQL on Neon is simpler for this prototype because the Free plan can scale to zero when idle.

## Create the Neon database

1. Create a Neon Free project named `simplexflow`.
2. Create a database named `simplexflow`.
3. Copy the .NET/Npgsql connection string.
4. Ensure the connection string uses SSL, for example:

```text
Host=<host>;Database=simplexflow;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true
```

## Store the GitHub secret

In GitHub:

```text
Repository > Settings > Secrets and variables > Actions > New repository secret
```

Create this secret:

```text
POSTGRES_CONNECTION_STRING=<Neon .NET/Npgsql connection string>
```

## Deploy the code image

Pushes to `main` only build, test, and publish the image. To deploy a specific build:

1. Open GitHub > Actions.
2. Run `Deploy container to Azure`.
3. Use `latest` or the full commit SHA.

## Switch Azure Container Apps to PostgreSQL

After `POSTGRES_CONNECTION_STRING` exists and the current image is deployed:

1. Open GitHub > Actions.
2. Run `Configure Azure database`.
3. Keep provider `Postgres`.
4. Wait until the workflow is green.

The workflow stores the connection string as an Azure Container Apps secret and configures:

```text
Database__Provider=Postgres
ConnectionStrings__DefaultConnection=secretref:postgres-connection-string
```

The app applies PostgreSQL EF Core migrations on startup. A fresh Neon database starts empty, so existing Azure SQL users and projects are not copied automatically. Export/import from Azure SQL can be added as a separate one-time migration if the old data is needed.

## Verify

Open:

```text
https://simplexflow.ch/healthz/db
```

Expected result:

```json
{
  "status": "ok",
  "provider": "Npgsql.EntityFrameworkCore.PostgreSQL"
}
```
