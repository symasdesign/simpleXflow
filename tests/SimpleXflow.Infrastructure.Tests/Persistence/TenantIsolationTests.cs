using Microsoft.EntityFrameworkCore;
using SimpleXflow.Domain.Common;
using SimpleXflow.Domain.Projects;
using SimpleXflow.Infrastructure.Persistence;
using SimpleXflow.Infrastructure.Tests.Fixtures;

namespace SimpleXflow.Infrastructure.Tests.Persistence;

public sealed class TenantIsolationTests : IDisposable
{
    private readonly SqliteDatabase database = new();

    public enum EntityKind { Project, Attachment, Version }
    public enum SaveOverload { Sync, SyncBoolean, Async, AsyncBoolean }
    public enum Mutation { Add, Modify, Delete }

    public static IEnumerable<object[]> WriteCases() =>
        from kind in Enum.GetValues<EntityKind>()
        from save in Enum.GetValues<SaveOverload>()
        from mutation in Enum.GetValues<Mutation>()
        select new object[] { kind, save, mutation };

    public static IEnumerable<object[]> EntitySaveCases() =>
        from kind in Enum.GetValues<EntityKind>()
        from save in Enum.GetValues<SaveOverload>()
        select new object[] { kind, save };

    [Fact]
    public async Task QueryFilters_IsolateProjectsAttachmentsAndVersionsPerContext()
    {
        await SeedGraphAsync(database.TenantA);
        await SeedGraphAsync(database.TenantB);
        foreach (var tenant in new[] { database.TenantA, database.TenantB })
        {
            await using var context = database.CreateContext(tenant);
            Assert.Equal(tenant, (await context.Projects.SingleAsync()).TenantId);
            Assert.Equal(tenant, (await context.ProjectAttachments.SingleAsync()).TenantId);
            Assert.Equal(tenant, (await context.ProjectVersions.SingleAsync()).TenantId);
            var graph = await context.Projects.Include(p => p.Attachments).Include(p => p.Versions).SingleAsync();
            Assert.Equal(tenant, Assert.Single(graph.Attachments).TenantId);
            Assert.Equal(tenant, Assert.Single(graph.Versions).TenantId);
        }
        await using var administrativeContext = database.CreateContext();
        Assert.Equal(2, await administrativeContext.Projects.CountAsync());
        Assert.Equal(2, await administrativeContext.ProjectAttachments.CountAsync());
        Assert.Equal(2, await administrativeContext.ProjectVersions.CountAsync());
    }

    [Theory]
    [MemberData(nameof(WriteCases))]
    public async Task SaveChanges_AllOverloadsRejectForeignTenantWrites(EntityKind kind, SaveOverload save, Mutation mutation)
    {
        var projectId = await SeedGraphAsync(database.TenantB);
        await using var context = database.CreateContext(database.TenantA);
        if (mutation == Mutation.Add)
        {
            context.Add(NewEntity(kind, database.TenantB, projectId));
        }
        else
        {
            var entity = await LoadEntityAsync(context, kind);
            context.Entry(entity).State = mutation == Mutation.Delete ? EntityState.Deleted : EntityState.Modified;
        }
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(context, save));
        Assert.Contains("Tenant scoped data", error.Message);
        await using var verification = database.CreateContext();
        Assert.Single(await verification.Projects.ToListAsync());
        Assert.Single(await verification.ProjectAttachments.ToListAsync());
        Assert.Single(await verification.ProjectVersions.ToListAsync());
    }

    [Theory]
    [MemberData(nameof(WriteCases))]
    public async Task SaveChanges_AllOverloadsAllowOwnTenantWrites(EntityKind kind, SaveOverload save, Mutation mutation)
    {
        var projectId = await SeedGraphAsync(database.TenantA);
        await using var context = database.CreateContext(database.TenantA);
        if (mutation == Mutation.Add)
        {
            context.Add(NewEntity(kind, database.TenantA, projectId));
        }
        else
        {
            var entity = await LoadEntityAsync(context, kind);
            if (mutation == Mutation.Modify)
            {
                var property = kind == EntityKind.Attachment ? "FileName" : "Name";
                context.Entry(entity).Property(property).CurrentValue = "Changed";
            }
            else
            {
                context.Remove(entity);
            }
        }
        Assert.True(await SaveAsync(context, save) > 0);
        await using var verification = database.CreateContext(database.TenantA);
        var entities = await LoadAllAsync(verification, kind);
        Assert.Equal(mutation == Mutation.Add ? 2 : mutation == Mutation.Delete ? 0 : 1, entities.Count);
        if (mutation == Mutation.Modify)
        {
            var property = kind == EntityKind.Attachment ? "FileName" : "Name";
            Assert.Equal("Changed", verification.Entry(entities[0]).Property(property).CurrentValue);
        }
    }

    [Theory]
    [MemberData(nameof(EntitySaveCases))]
    public async Task SaveChanges_AssignsCurrentTenantToNewEntitiesWithEmptyTenant(EntityKind kind, SaveOverload save)
    {
        var projectId = await SeedGraphAsync(database.TenantA);
        await using var context = database.CreateContext(database.TenantA);
        var entity = NewEntity(kind, Guid.Empty, projectId);
        context.Add(entity);
        await SaveAsync(context, save);
        Assert.Equal(database.TenantA, entity.TenantId);
        await using var verification = database.CreateContext(database.TenantA);
        Assert.Equal(2, (await LoadAllAsync(verification, kind)).Count);
    }

    [Theory]
    [MemberData(nameof(EntitySaveCases))]
    public async Task SaveChanges_RejectsReassigningTrackedForeignEntityToCurrentTenant(EntityKind kind, SaveOverload save)
    {
        await SeedGraphAsync(database.TenantB);
        await using var context = database.CreateContext(database.TenantA);
        var entity = await LoadEntityAsync(context, kind);
        entity.TenantId = database.TenantA;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(context, save));
        Assert.Contains("Tenant scoped data", error.Message);
        await using var verification = database.CreateContext(database.TenantB);
        Assert.Equal(database.TenantB, (await LoadEntityAsync(verification, kind)).TenantId);
    }

    [Fact]
    public async Task ForeignEntitiesTrackedWithoutChanges_DoNotBlockOwnWrites()
    {
        await SeedGraphAsync(database.TenantB);
        await using var context = database.CreateContext(database.TenantA);
        await context.Projects.IgnoreQueryFilters().LoadAsync();
        context.Projects.Add(new FlowProject(database.TenantA, "Own project", "<own />"));
        await context.SaveChangesAsync();
        await using var verification = database.CreateContext(database.TenantA);
        Assert.Equal("Own project", (await verification.Projects.SingleAsync()).Name);
    }

    [Fact]
    public async Task DeleteProject_CascadesToAttachmentsAndVersions()
    {
        await SeedGraphAsync(database.TenantA);
        await using (var context = database.CreateContext(database.TenantA))
        {
            context.Projects.Remove(await context.Projects.SingleAsync());
            await context.SaveChangesAsync();
        }
        await using var verification = database.CreateContext();
        Assert.Empty(await verification.ProjectAttachments.ToListAsync());
        Assert.Empty(await verification.ProjectVersions.ToListAsync());
    }

    [Theory]
    [InlineData(EntityKind.Attachment)]
    [InlineData(EntityKind.Version)]
    public async Task Database_RejectsChildrenWithoutProject(EntityKind kind)
    {
        await using var context = database.CreateContext(database.TenantA);
        context.Add(NewEntity(kind, database.TenantA, Guid.NewGuid()));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_EnforcesUniqueProjectNamesWithinTenant()
    {
        await database.SeedProjectAsync(database.TenantA, "Duplicate");
        await database.SeedProjectAsync(database.TenantB, "Duplicate");
        await using var context = database.CreateContext(database.TenantA);
        context.Projects.Add(new FlowProject(database.TenantA, "Duplicate", "<xml />"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_EnforcesUniqueVersionNumbersWithinProject()
    {
        var id = await SeedGraphAsync(database.TenantA);
        await using var context = database.CreateContext(database.TenantA);
        context.ProjectVersions.Add(new ProjectVersion(database.TenantA, id, 1, "Duplicate", "<xml />", null));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private async Task<Guid> SeedGraphAsync(Guid tenantId)
    {
        var id = await database.SeedProjectAsync(tenantId);
        await using var context = database.CreateContext();
        context.ProjectAttachments.Add(new(tenantId, id, "diagram.xml", "application/xml", [1, 2, 3]));
        context.ProjectVersions.Add(new(tenantId, id, 1, "Snapshot", "<snapshot />", "<logic />"));
        await context.SaveChangesAsync();
        return id;
    }

    private static ITenantEntity NewEntity(EntityKind kind, Guid tenantId, Guid projectId) => kind switch
    {
        EntityKind.Project => new FlowProject(tenantId, "New project", "<new />"),
        EntityKind.Attachment => new ProjectAttachment(tenantId, projectId, "new.xml", "application/xml", [4, 5, 6]),
        EntityKind.Version => new ProjectVersion(tenantId, projectId, 2, "New snapshot", "<new />", null),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static async Task<ITenantEntity> LoadEntityAsync(ApplicationDbContext context, EntityKind kind) => kind switch
    {
        EntityKind.Project => await context.Projects.IgnoreQueryFilters().SingleAsync(),
        EntityKind.Attachment => await context.ProjectAttachments.IgnoreQueryFilters().SingleAsync(),
        EntityKind.Version => await context.ProjectVersions.IgnoreQueryFilters().SingleAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static async Task<List<ITenantEntity>> LoadAllAsync(ApplicationDbContext context, EntityKind kind) => kind switch
    {
        EntityKind.Project => (await context.Projects.ToListAsync()).Cast<ITenantEntity>().ToList(),
        EntityKind.Attachment => (await context.ProjectAttachments.ToListAsync()).Cast<ITenantEntity>().ToList(),
        EntityKind.Version => (await context.ProjectVersions.ToListAsync()).Cast<ITenantEntity>().ToList(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static async Task<int> SaveAsync(ApplicationDbContext context, SaveOverload save) => save switch
    {
        SaveOverload.Sync => context.SaveChanges(),
        SaveOverload.SyncBoolean => context.SaveChanges(acceptAllChangesOnSuccess: false),
        SaveOverload.Async => await context.SaveChangesAsync(),
        SaveOverload.AsyncBoolean => await context.SaveChangesAsync(acceptAllChangesOnSuccess: false),
        _ => throw new ArgumentOutOfRangeException(nameof(save)),
    };

    public void Dispose() => database.Dispose();
}
