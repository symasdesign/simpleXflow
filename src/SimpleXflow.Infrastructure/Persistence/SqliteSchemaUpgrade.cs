using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace SimpleXflow.Infrastructure.Persistence;

/// <summary>Additive upgrade for local databases originally created with EnsureCreated.</summary>
public static class SqliteSchemaUpgrade
{
    public static async Task ApplyAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsSqlite()) return;
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await AddMissingColumnsAsync(db, "Projects", new Dictionary<string, string>
            {
                ["WorkspaceProjectId"] = "TEXT NULL REFERENCES WorkspaceProjects(Id)",
                ["FolderPath"] = "TEXT NOT NULL DEFAULT ''",
                ["Revision"] = "INTEGER NOT NULL DEFAULT 0",
                ["LastEditedBy"] = "TEXT NULL"
            }, cancellationToken);
            await AddMissingColumnsAsync(db, "ProjectVersions", new Dictionary<string, string>
            {
                ["Label"] = "TEXT NOT NULL DEFAULT ''",
                ["CreatedBy"] = "TEXT NULL",
                ["PlannedReleaseUtc"] = "TEXT NULL",
                ["ReleasedUtc"] = "TEXT NULL",
                ["ReleasedBy"] = "TEXT NULL"
            }, cancellationToken);
            // EF's provider-specific script also creates any missing Identity/history tables.
            // Existing tables and indexes are preserved, including all stored models.
            var script = db.Database.GenerateCreateScript()
                .Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ", StringComparison.Ordinal)
                .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ", StringComparison.Ordinal)
                .Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ", StringComparison.Ordinal);
            // Earlier releases enforced model names across an entire organization.
            // Recreate that index as a legacy-only constraint before installing folder-scoped uniqueness.
            await db.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS \"IX_Projects_TenantId_Name\";", cancellationToken);
            await db.Database.ExecuteSqlRawAsync(script, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    private static async Task AddMissingColumnsAsync(ApplicationDbContext db, string table, IReadOnlyDictionary<string, string> columns, CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken)) existing.Add(reader.GetString(1));
        }
        if (existing.Count == 0) return;
        foreach (var (name, type) in columns)
        {
            if (existing.Contains(name)) continue;
            command.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{name}\" {type};";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
