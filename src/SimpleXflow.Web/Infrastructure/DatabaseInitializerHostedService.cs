using System.Data;
using Microsoft.EntityFrameworkCore;
using SimpleXflow.Infrastructure.Persistence;

namespace SimpleXflow.Web.Infrastructure;

internal sealed class DatabaseInitializerHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseInitializerHostedService> logger) : BackgroundService
{
    private const string InitialMigrationId = "20260828142810_InitialCreate";
    private const string InitialMigrationProductVersion = "10.0.7";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await ApplyDatabaseSchemaAsync(dbContext, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Database initialization failed after all retry attempts.");
        }
    }

    private async Task ApplyDatabaseSchemaAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 12;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (IsSqlServer(dbContext))
                {
                    await PrepareExistingSqlServerSchemaForMigrationsAsync(dbContext, cancellationToken);
                    await dbContext.Database.MigrateAsync(cancellationToken);
                    logger.LogInformation("SQL Server database schema is up to date.");
                    return;
                }

                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                logger.LogInformation("Local database schema is available.");
                return;
            }
            catch (Exception exception) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(attempt * 5);
                logger.LogWarning(
                    exception,
                    "Database initialization failed on attempt {Attempt}/{MaxAttempts}. Retrying in {DelaySeconds} seconds.",
                    attempt,
                    maxAttempts,
                    delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static bool IsSqlServer(ApplicationDbContext dbContext)
    {
        return dbContext.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static async Task PrepareExistingSqlServerSchemaForMigrationsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var hasMigrationHistory = await SqlServerTableExistsAsync(dbContext, "__EFMigrationsHistory", cancellationToken);
        if (hasMigrationHistory
            && await SqlServerInitialMigrationExistsAsync(dbContext, cancellationToken))
        {
            return;
        }

        var hasPrototypeTables =
            await SqlServerTableExistsAsync(dbContext, "AspNetUsers", cancellationToken)
            || await SqlServerTableExistsAsync(dbContext, "Tenants", cancellationToken)
            || await SqlServerTableExistsAsync(dbContext, "Projects", cancellationToken);

        if (!hasPrototypeTables)
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            SET XACT_ABORT ON;

            IF OBJECT_ID(N'[dbo].[Tenants]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Tenants] (
                    [Id] uniqueidentifier NOT NULL,
                    [Name] nvarchar(200) NOT NULL,
                    [Slug] nvarchar(220) NOT NULL,
                    [CreatedUtc] datetimeoffset NOT NULL,
                    CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
                );
            END;

            DECLARE @DefaultTenantId uniqueidentifier;
            SELECT TOP(1) @DefaultTenantId = [Id] FROM [dbo].[Tenants] ORDER BY [CreatedUtc], [Name];

            IF @DefaultTenantId IS NULL
            BEGIN
                SET @DefaultTenantId = NEWID();
                INSERT INTO [dbo].[Tenants] ([Id], [Name], [Slug], [CreatedUtc])
                VALUES (@DefaultTenantId, N'Default workspace', N'default-workspace', SYSDATETIMEOFFSET());
            END;

            IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[AspNetUsers]', N'TenantId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[AspNetUsers] ADD [TenantId] uniqueidentifier NULL;
            END;

            IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[AspNetUsers]', N'TenantId') IS NOT NULL
            BEGIN
                UPDATE [u]
                SET [TenantId] = @DefaultTenantId
                FROM [dbo].[AspNetUsers] AS [u]
                WHERE [u].[TenantId] IS NULL
                   OR NOT EXISTS (
                       SELECT 1
                       FROM [dbo].[Tenants] AS [t]
                       WHERE [t].[Id] = [u].[TenantId]
                   );

                IF EXISTS (
                    SELECT 1
                    FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]')
                      AND name = N'TenantId'
                      AND is_nullable = 1
                )
                BEGIN
                    ALTER TABLE [dbo].[AspNetUsers] ALTER COLUMN [TenantId] uniqueidentifier NOT NULL;
                END;
            END;

            IF OBJECT_ID(N'[dbo].[Projects]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Projects] (
                    [Id] uniqueidentifier NOT NULL,
                    [TenantId] uniqueidentifier NOT NULL,
                    [Name] nvarchar(240) NOT NULL,
                    [BpmnXml] nvarchar(max) NOT NULL,
                    [LogicXml] nvarchar(max) NULL,
                    [CreatedUtc] datetimeoffset NOT NULL,
                    [UpdatedUtc] datetimeoffset NOT NULL,
                    CONSTRAINT [PK_Projects] PRIMARY KEY ([Id])
                );
            END;

            IF OBJECT_ID(N'[dbo].[Projects]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Projects]', N'LogicXml') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Projects] ADD [LogicXml] nvarchar(max) NULL;
            END;

            IF OBJECT_ID(N'[dbo].[Projects]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Projects]', N'TenantId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Projects] ADD [TenantId] uniqueidentifier NULL;
            END;

            IF OBJECT_ID(N'[dbo].[Projects]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Projects]', N'TenantId') IS NOT NULL
            BEGIN
                UPDATE [p]
                SET [TenantId] = @DefaultTenantId
                FROM [dbo].[Projects] AS [p]
                WHERE [p].[TenantId] IS NULL
                   OR NOT EXISTS (
                       SELECT 1
                       FROM [dbo].[Tenants] AS [t]
                       WHERE [t].[Id] = [p].[TenantId]
                   );

                IF EXISTS (
                    SELECT 1
                    FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'[dbo].[Projects]')
                      AND name = N'TenantId'
                      AND is_nullable = 1
                )
                BEGIN
                    ALTER TABLE [dbo].[Projects] ALTER COLUMN [TenantId] uniqueidentifier NOT NULL;
                END;
            END;

            IF OBJECT_ID(N'[dbo].[Projects]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Projects]', N'CreatedUtc') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Projects]
                ADD [CreatedUtc] datetimeoffset NOT NULL
                    CONSTRAINT [DF_Projects_CreatedUtc] DEFAULT SYSDATETIMEOFFSET();
            END;

            IF OBJECT_ID(N'[dbo].[Projects]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Projects]', N'UpdatedUtc') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Projects]
                ADD [UpdatedUtc] datetimeoffset NOT NULL
                    CONSTRAINT [DF_Projects_UpdatedUtc] DEFAULT SYSDATETIMEOFFSET();
            END;

            IF OBJECT_ID(N'[dbo].[ProjectAttachments]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ProjectAttachments] (
                    [Id] uniqueidentifier NOT NULL,
                    [TenantId] uniqueidentifier NOT NULL,
                    [ProjectId] uniqueidentifier NOT NULL,
                    [FileName] nvarchar(260) NOT NULL,
                    [ContentType] nvarchar(120) NOT NULL,
                    [Content] varbinary(max) NOT NULL,
                    [CreatedUtc] datetimeoffset NOT NULL,
                    CONSTRAINT [PK_ProjectAttachments] PRIMARY KEY ([Id])
                );
            END;

            IF OBJECT_ID(N'[dbo].[ProjectAttachments]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[ProjectAttachments]', N'TenantId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ProjectAttachments] ADD [TenantId] uniqueidentifier NULL;
            END;

            IF OBJECT_ID(N'[dbo].[ProjectAttachments]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[ProjectAttachments]', N'TenantId') IS NOT NULL
            BEGIN
                UPDATE [a]
                SET [TenantId] = COALESCE([p].[TenantId], @DefaultTenantId)
                FROM [dbo].[ProjectAttachments] AS [a]
                LEFT JOIN [dbo].[Projects] AS [p] ON [p].[Id] = [a].[ProjectId]
                WHERE [a].[TenantId] IS NULL
                   OR NOT EXISTS (
                       SELECT 1
                       FROM [dbo].[Tenants] AS [t]
                       WHERE [t].[Id] = [a].[TenantId]
                   );

                IF EXISTS (
                    SELECT 1
                    FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'[dbo].[ProjectAttachments]')
                      AND name = N'TenantId'
                      AND is_nullable = 1
                )
                BEGIN
                    ALTER TABLE [dbo].[ProjectAttachments] ALTER COLUMN [TenantId] uniqueidentifier NOT NULL;
                END;
            END;

            IF OBJECT_ID(N'[dbo].[ProjectAttachments]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[ProjectAttachments]', N'CreatedUtc') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ProjectAttachments]
                ADD [CreatedUtc] datetimeoffset NOT NULL
                    CONSTRAINT [DF_ProjectAttachments_CreatedUtc] DEFAULT SYSDATETIMEOFFSET();
            END;

            IF OBJECT_ID(N'[dbo].[AspNetUserPasskeys]', N'U') IS NULL
               AND OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
            BEGIN
                CREATE TABLE [dbo].[AspNetUserPasskeys] (
                    [CredentialId] varbinary(1024) NOT NULL,
                    [UserId] nvarchar(450) NOT NULL,
                    [Data] nvarchar(max) NOT NULL,
                    CONSTRAINT [PK_AspNetUserPasskeys] PRIMARY KEY ([CredentialId])
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_Slug' AND object_id = OBJECT_ID(N'[dbo].[Tenants]'))
               AND NOT EXISTS (
                   SELECT [Slug]
                   FROM [dbo].[Tenants]
                   GROUP BY [Slug]
                   HAVING COUNT(*) > 1
               )
            BEGIN
                CREATE UNIQUE INDEX [IX_Tenants_Slug] ON [dbo].[Tenants] ([Slug]);
            END;

            IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[AspNetUsers]', N'TenantId') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AspNetUsers_TenantId' AND object_id = OBJECT_ID(N'[dbo].[AspNetUsers]'))
            BEGIN
                CREATE INDEX [IX_AspNetUsers_TenantId] ON [dbo].[AspNetUsers] ([TenantId]);
            END;

            IF OBJECT_ID(N'[dbo].[Projects]', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Projects_TenantId_Name' AND object_id = OBJECT_ID(N'[dbo].[Projects]'))
               AND NOT EXISTS (
                   SELECT [TenantId], [Name]
                   FROM [dbo].[Projects]
                   GROUP BY [TenantId], [Name]
                   HAVING COUNT(*) > 1
               )
            BEGIN
                CREATE UNIQUE INDEX [IX_Projects_TenantId_Name] ON [dbo].[Projects] ([TenantId], [Name]);
            END;

            IF OBJECT_ID(N'[dbo].[ProjectAttachments]', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectAttachments_ProjectId' AND object_id = OBJECT_ID(N'[dbo].[ProjectAttachments]'))
            BEGIN
                CREATE INDEX [IX_ProjectAttachments_ProjectId] ON [dbo].[ProjectAttachments] ([ProjectId]);
            END;

            IF OBJECT_ID(N'[dbo].[AspNetUserPasskeys]', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AspNetUserPasskeys_UserId' AND object_id = OBJECT_ID(N'[dbo].[AspNetUserPasskeys]'))
            BEGIN
                CREATE INDEX [IX_AspNetUserPasskeys_UserId] ON [dbo].[AspNetUserPasskeys] ([UserId]);
            END;

            IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[AspNetUsers]', N'TenantId') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AspNetUsers_Tenants_TenantId')
            BEGIN
                ALTER TABLE [dbo].[AspNetUsers]
                ADD CONSTRAINT [FK_AspNetUsers_Tenants_TenantId]
                FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION;
            END;

            IF OBJECT_ID(N'[dbo].[Projects]', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Projects_Tenants_TenantId')
            BEGIN
                ALTER TABLE [dbo].[Projects]
                ADD CONSTRAINT [FK_Projects_Tenants_TenantId]
                FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE CASCADE;
            END;

            IF OBJECT_ID(N'[dbo].[ProjectAttachments]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[Projects]', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProjectAttachments_Projects_ProjectId')
            BEGIN
                ALTER TABLE [dbo].[ProjectAttachments]
                ADD CONSTRAINT [FK_ProjectAttachments_Projects_ProjectId]
                FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects] ([Id]) ON DELETE CASCADE;
            END;

            IF OBJECT_ID(N'[dbo].[AspNetUserPasskeys]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AspNetUserPasskeys_AspNetUsers_UserId')
            BEGIN
                ALTER TABLE [dbo].[AspNetUserPasskeys]
                ADD CONSTRAINT [FK_AspNetUserPasskeys_AspNetUsers_UserId]
                FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE;
            END;

            IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[__EFMigrationsHistory] (
                    [MigrationId] nvarchar(150) NOT NULL,
                    [ProductVersion] nvarchar(32) NOT NULL,
                    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                );
            END;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            IF NOT EXISTS (
                SELECT 1
                FROM [dbo].[__EFMigrationsHistory]
                WHERE [MigrationId] = {InitialMigrationId}
            )
            BEGIN
                INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES ({InitialMigrationId}, {InitialMigrationProductVersion});
            END;
            """,
            cancellationToken);
    }

    private static async Task<bool> SqlServerTableExistsAsync(
        ApplicationDbContext dbContext,
        string tableName,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT CASE WHEN OBJECT_ID(N'[dbo].[{tableName}]', N'U') IS NULL THEN 0 ELSE 1 END";

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) == 1;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> SqlServerInitialMigrationExistsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM [dbo].[__EFMigrationsHistory]
                        WHERE [MigrationId] = N'{InitialMigrationId}'
                    )
                    THEN 1
                    ELSE 0
                END
                """;

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) == 1;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}
