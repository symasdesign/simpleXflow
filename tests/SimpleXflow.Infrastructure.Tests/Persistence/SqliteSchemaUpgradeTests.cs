using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SimpleXflow.Infrastructure.Persistence;
using SimpleXflow.Infrastructure.Tests.Fakes;
using SimpleXflow.Infrastructure.Tests.Fixtures;

namespace SimpleXflow.Infrastructure.Tests.Persistence;

public sealed class SqliteSchemaUpgradeTests
{
    [Fact]
    public async Task UpgradeExistingLocalDatabase_PreservesModelsAndHistoryAndIsRepeatable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new ApplicationDbContext(SqliteDatabase.CreateOptionsBuilder(connection).Options, new TestTenantContext(null));
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "Projects" (
                "Id" TEXT NOT NULL PRIMARY KEY, "TenantId" TEXT NOT NULL, "Name" TEXT NOT NULL,
                "BpmnXml" TEXT NOT NULL, "LogicXml" TEXT NULL, "PreviousName" TEXT NULL,
                "PreviousBpmnXml" TEXT NULL, "PreviousLogicXml" TEXT NULL, "PreviousUpdatedUtc" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL, "UpdatedUtc" TEXT NOT NULL);
            CREATE UNIQUE INDEX "IX_Projects_TenantId_Name" ON "Projects" ("TenantId", "Name");
            CREATE TABLE "ProjectVersions" (
                "Id" TEXT NOT NULL PRIMARY KEY, "TenantId" TEXT NOT NULL, "FlowProjectId" TEXT NOT NULL,
                "VersionNumber" INTEGER NOT NULL, "Name" TEXT NOT NULL, "BpmnXml" TEXT NOT NULL,
                "LogicXml" TEXT NULL, "CreatedUtc" TEXT NOT NULL);
            INSERT INTO "Projects" ("Id", "TenantId", "Name", "BpmnXml", "LogicXml", "CreatedUtc", "UpdatedUtc")
            VALUES ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002', 'Existing model', '<architecture />', '<logic />', '2026-01-01 00:00:00+00:00', '2026-01-01 00:00:00+00:00');
            INSERT INTO "ProjectVersions" ("Id", "TenantId", "FlowProjectId", "VersionNumber", "Name", "BpmnXml", "CreatedUtc")
            VALUES ('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 1, 'Earlier model', '<earlier />', '2026-01-01 00:00:00+00:00');
            """);
        await SqliteSchemaUpgrade.ApplyAsync(context);
        await SqliteSchemaUpgrade.ApplyAsync(context);
        var model = await context.Projects.SingleAsync();
        Assert.Equal("Existing model", model.Name);
        Assert.Equal("<architecture />", model.BpmnXml);
        Assert.Equal("<logic />", model.LogicXml);
        Assert.Null(model.WorkspaceProjectId);
        Assert.Equal(0, model.Revision);
        Assert.Equal("", model.FolderPath);
        Assert.Equal("<earlier />", (await context.ProjectVersions.SingleAsync()).BpmnXml);
        Assert.Empty(await context.Workspaces.ToListAsync());
        Assert.Empty(await context.WorkspaceProjects.ToListAsync());
        Assert.Empty(await context.WorkspaceMembers.ToListAsync());
        Assert.Empty(await context.ModelFolders.ToListAsync());
        var indexSql = await context.Database.SqlQueryRaw<string>("SELECT sql AS Value FROM sqlite_master WHERE name = 'IX_Projects_TenantId_Name'").SingleAsync();
        Assert.Contains("WHERE \"WorkspaceProjectId\" IS NULL", indexSql);
    }
}
