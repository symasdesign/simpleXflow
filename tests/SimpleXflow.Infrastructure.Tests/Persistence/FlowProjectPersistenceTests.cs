using Microsoft.EntityFrameworkCore;
using SimpleXflow.Application.Projects;
using SimpleXflow.Domain.Projects;
using SimpleXflow.Infrastructure.Persistence;
using SimpleXflow.Infrastructure.Projects;
using SimpleXflow.Infrastructure.Tests.Fakes;
using SimpleXflow.Infrastructure.Tests.Fixtures;

namespace SimpleXflow.Infrastructure.Tests.Persistence;

public sealed class FlowProjectPersistenceTests : IDisposable
{
    private readonly SqliteDatabase database = new();

    [Fact]
    public async Task CreateAndUpdate_PersistCompleteProjectAcrossIndependentContexts()
    {
        Guid id;
        await using (var context = database.CreateContext(database.TenantA))
        {
            id = await Service(context, database.TenantA).CreateProjectAsync(
                new(" Imported flow ", " <architecture /> ", "<logic />"));
        }
        DateTimeOffset created;
        await using (var context = database.CreateContext(database.TenantA))
        {
            var detail = await Service(context, database.TenantA).GetProjectAsync(id);
            Assert.NotNull(detail);
            Assert.Equal(id, detail.Id);
            Assert.Equal("Imported flow", detail.Name);
            Assert.Equal("<architecture />", detail.BpmnXml);
            Assert.Equal("<logic />", detail.LogicXml);
            Assert.False(detail.CanUndo);
            created = detail.CreatedUtc;
            Assert.True(created <= detail.UpdatedUtc);
            Assert.Equal(database.TenantA, (await context.Projects.SingleAsync()).TenantId);
            await Service(context, database.TenantA).UpdateProjectAsync(id,
                new(" Changed ", " <changed /> ", " <changed-logic /> "));
        }
        await using (var context = database.CreateContext(database.TenantA))
        {
            var saved = await Service(context, database.TenantA).GetProjectAsync(id);
            Assert.NotNull(saved);
            Assert.Equal("Changed", saved.Name);
            Assert.Equal("<changed />", saved.BpmnXml);
            Assert.Equal("<changed-logic />", saved.LogicXml);
            await Service(context, database.TenantA).UpdateProjectAsync(id, new("Latest", "<latest />", "  "));
        }
        await using var readContext = database.CreateContext(database.TenantA);
        var latest = await Service(readContext, database.TenantA).GetProjectAsync(id);
        Assert.NotNull(latest);
        Assert.Equal("Latest", latest.Name);
        Assert.Equal("<latest />", latest.BpmnXml);
        Assert.Null(latest.LogicXml);
        Assert.Equal(created, latest.CreatedUtc);
        Assert.True(latest.UpdatedUtc >= created);
        Assert.False(latest.CanUndo);
        Assert.Empty(await readContext.ProjectVersions.ToListAsync());
    }

    [Fact]
    public async Task CreateProjectAsync_SkipsOccupiedSuffixesWithinCurrentTenant()
    {
        await database.SeedProjectAsync(database.TenantA, "Imported flow");
        await database.SeedProjectAsync(database.TenantA, "Imported flow (1)");
        await database.SeedProjectAsync(database.TenantB, "Imported flow (2)");
        Guid id;
        await using (var context = database.CreateContext(database.TenantA))
        {
            id = await Service(context, database.TenantA).CreateProjectAsync(new(" Imported flow ", "<xml />"));
        }
        await using var readContext = database.CreateContext(database.TenantA);
        Assert.Equal("Imported flow (2)", (await readContext.Projects.SingleAsync(project => project.Id == id)).Name);
    }

    [Theory]
    [InlineData("", "<xml />")]
    [InlineData("  ", "<xml />")]
    [InlineData("Project", "")]
    [InlineData("Project", "  ")]
    public async Task CreateProjectAsync_InvalidInputLeavesDatabaseUnchanged(string name, string xml)
    {
        await using var context = database.CreateContext(database.TenantA);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(context, database.TenantA).CreateProjectAsync(new(name, xml)));
        await using var readContext = database.CreateContext();
        Assert.Empty(await readContext.Projects.ToListAsync());
    }

    [Fact]
    public async Task UpdateProjectAsync_UnchangedNormalizedInputPreservesTimestamp()
    {
        var id = await database.SeedProjectAsync(database.TenantA);
        var fixedTimestamp = DateTimeOffset.UnixEpoch;
        await using (var setup = database.CreateContext())
        {
            setup.Entry(await setup.Projects.SingleAsync()).Property(p => p.UpdatedUtc).CurrentValue = fixedTimestamp;
            await setup.SaveChangesAsync();
        }
        await using (var context = database.CreateContext(database.TenantA))
        {
            await Service(context, database.TenantA).UpdateProjectAsync(id, new(" Original ", " <original /> ", "  "));
        }
        await using var readContext = database.CreateContext(database.TenantA);
        Assert.Equal(fixedTimestamp, (await readContext.Projects.SingleAsync()).UpdatedUtc);
        Assert.Empty(await readContext.ProjectVersions.ToListAsync());
    }

    [Fact]
    public async Task UndoProjectAsync_RestoresAndDeletesLatestPersistedVersionAcrossContexts()
    {
        var id = await database.SeedProjectAsync(database.TenantA, "Current", "<current />");
        await using (var setup = database.CreateContext())
        {
            setup.ProjectVersions.AddRange(
                new ProjectVersion(database.TenantA, id, 1, "First", "<first />", null),
                new ProjectVersion(database.TenantA, id, 3, "Third", "<third />", "<logic />"),
                new ProjectVersion(database.TenantA, id, 2, "Second", "<second />", null));
            await setup.SaveChangesAsync();
        }
        foreach (var expected in new[] { ("Third", "<third />", "<logic />", 2), ("Second", "<second />", (string?)null, 1), ("First", "<first />", (string?)null, 0) })
        {
            await using (var context = database.CreateContext(database.TenantA))
            {
                Assert.True((await Service(context, database.TenantA).GetProjectAsync(id))!.CanUndo);
                await Service(context, database.TenantA).UndoProjectAsync(id);
            }
            await using var readContext = database.CreateContext(database.TenantA);
            var detail = await Service(readContext, database.TenantA).GetProjectAsync(id);
            Assert.NotNull(detail);
            Assert.Equal(expected.Item1, detail.Name);
            Assert.Equal(expected.Item2, detail.BpmnXml);
            Assert.Equal(expected.Item3, detail.LogicXml);
            Assert.Equal(expected.Item4 > 0, detail.CanUndo);
            Assert.Equal(expected.Item4, await readContext.ProjectVersions.CountAsync());
        }
    }

    [Fact]
    public async Task UndoProjectAsync_RestoresAndClearsLegacySnapshot()
    {
        var id = await database.SeedProjectAsync(database.TenantA, "Current");
        await using (var setup = database.CreateContext())
        {
            var entry = setup.Entry(await setup.Projects.SingleAsync());
            entry.Property(p => p.PreviousName).CurrentValue = "Legacy";
            entry.Property(p => p.PreviousBpmnXml).CurrentValue = "<legacy />";
            entry.Property(p => p.PreviousLogicXml).CurrentValue = "<legacy-logic />";
            entry.Property(p => p.PreviousUpdatedUtc).CurrentValue = DateTimeOffset.UnixEpoch;
            await setup.SaveChangesAsync();
        }
        await using (var context = database.CreateContext(database.TenantA))
        {
            Assert.True((await Service(context, database.TenantA).GetProjectAsync(id))!.CanUndo);
            await Service(context, database.TenantA).UndoProjectAsync(id);
        }
        await using var readContext = database.CreateContext(database.TenantA);
        var restored = await readContext.Projects.SingleAsync();
        Assert.Equal("Legacy", restored.Name);
        Assert.Equal("<legacy />", restored.BpmnXml);
        Assert.Equal("<legacy-logic />", restored.LogicXml);
        Assert.Null(restored.PreviousName);
        Assert.Null(restored.PreviousBpmnXml);
        Assert.Null(restored.PreviousLogicXml);
        Assert.Null(restored.PreviousUpdatedUtc);
        Assert.False(restored.CanUndo);
    }

    [Fact]
    public async Task UpdateProjectAsync_PreservesExistingHistoricalVersion()
    {
        var id = await database.SeedProjectAsync(database.TenantA);
        await using (var setup = database.CreateContext())
        {
            setup.ProjectVersions.Add(new ProjectVersion(database.TenantA, id, 1, "Historical", "<history />", null));
            await setup.SaveChangesAsync();
        }
        await using (var context = database.CreateContext(database.TenantA))
        {
            await Service(context, database.TenantA).UpdateProjectAsync(id, new("Autosaved", "<autosaved />", null));
        }
        await using var readContext = database.CreateContext(database.TenantA);
        Assert.Equal("Historical", (await readContext.ProjectVersions.SingleAsync()).Name);
        Assert.True((await Service(readContext, database.TenantA).GetProjectAsync(id))!.CanUndo);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectOperations_CannotReadOrChangeMissingOrForeignProject(bool foreign)
    {
        var id = foreign ? await database.SeedProjectAsync(database.TenantB) : Guid.NewGuid();
        await using var context = database.CreateContext(database.TenantA);
        var service = Service(context, database.TenantA);
        Assert.Null(await service.GetProjectAsync(id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateProjectAsync(id, new("Changed", "<changed />", null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UndoProjectAsync(id));
        await using var readContext = database.CreateContext();
        if (foreign)
        {
            Assert.Equal("Original", (await readContext.Projects.SingleAsync()).Name);
        }
    }

    [Theory]
    [InlineData("list")]
    [InlineData("get")]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("undo")]
    public async Task ProjectOperations_RequireTenant(string operation)
    {
        await using var context = database.CreateContext();
        var service = Service(context, null);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => operation switch
        {
            "list" => service.GetProjectsAsync(),
            "get" => service.GetProjectAsync(Guid.NewGuid()),
            "create" => service.CreateProjectAsync(new("Name", "<xml />")),
            "update" => service.UpdateProjectAsync(Guid.NewGuid(), new("Name", "<xml />", null)),
            "undo" => service.UndoProjectAsync(Guid.NewGuid()),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        });
        Assert.Contains("A tenant is required", error.Message);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("get")]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("undo")]
    public async Task ProjectOperations_ObserveCancellationBeforeChangingData(string operation)
    {
        var id = await database.SeedProjectAsync(database.TenantA);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var context = database.CreateContext(database.TenantA);
        var service = Service(context, database.TenantA);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation switch
        {
            "list" => service.GetProjectsAsync(cancellation.Token),
            "get" => service.GetProjectAsync(id, cancellation.Token),
            "create" => service.CreateProjectAsync(new("Name", "<xml />"), cancellation.Token),
            "update" => service.UpdateProjectAsync(id, new("Name", "<xml />", null), cancellation.Token),
            "undo" => service.UndoProjectAsync(id, cancellation.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        });
        await using var readContext = database.CreateContext();
        Assert.Equal("Original", (await readContext.Projects.SingleAsync()).Name);
    }

    [Fact]
    public async Task UndoProjectAsync_RollsBackVersionRemovalIfRestoredNameConflicts()
    {
        var id = await database.SeedProjectAsync(database.TenantA, "Current");
        await database.SeedProjectAsync(database.TenantA, "Occupied");
        await using (var setup = database.CreateContext())
        {
            setup.ProjectVersions.Add(new ProjectVersion(database.TenantA, id, 1, "Occupied", "<history />", null));
            await setup.SaveChangesAsync();
        }
        await using (var context = database.CreateContext(database.TenantA))
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => Service(context, database.TenantA).UndoProjectAsync(id));
        }
        await using var verification = database.CreateContext(database.TenantA);
        Assert.Equal("Current", (await verification.Projects.SingleAsync(project => project.Id == id)).Name);
        Assert.Equal("Occupied", (await verification.ProjectVersions.SingleAsync()).Name);
    }
    private static FlowProjectService Service(ApplicationDbContext context, Guid? tenantId) => new(context, new TestTenantContext(tenantId));
    public void Dispose() => database.Dispose();
}
