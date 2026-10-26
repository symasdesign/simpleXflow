using Microsoft.EntityFrameworkCore;
using SimpleXflow.Application.Projects;
using SimpleXflow.Infrastructure.Projects;
using SimpleXflow.Infrastructure.Tests.Fakes;
using SimpleXflow.Infrastructure.Tests.Fixtures;

namespace SimpleXflow.Infrastructure.Tests.Persistence;

public sealed class FlowProjectServiceTests : IDisposable
{
    private readonly SqliteDatabase database = new();

    [Fact]
    public async Task GetProjectsAsync_ReturnsOnlyProjectsForCurrentTenant()
    {
        await database.SeedProjectAsync(database.TenantA, "Visible");
        await database.SeedProjectAsync(database.TenantB, "Hidden");
        await using var context = database.CreateContext(database.TenantA);
        var service = new FlowProjectService(context, new TestTenantContext(database.TenantA));
        Assert.Equal("Visible", Assert.Single(await service.GetProjectsAsync()).Name);
    }

    [Fact]
    public async Task GetProjectsAsync_SortsByPersistedUpdatedTimeForSqliteCompatibility()
    {
        var olderId = await database.SeedProjectAsync(database.TenantA, "Older");
        var newerId = await database.SeedProjectAsync(database.TenantA, "Newer");
        await using (var setup = database.CreateContext(database.TenantA))
        {
            var projects = await setup.Projects.ToDictionaryAsync(project => project.Id);
            setup.Entry(projects[olderId]).Property(project => project.UpdatedUtc).CurrentValue = DateTimeOffset.UnixEpoch;
            setup.Entry(projects[newerId]).Property(project => project.UpdatedUtc).CurrentValue = DateTimeOffset.UnixEpoch.AddDays(1);
            await setup.SaveChangesAsync();
        }
        await using var context = database.CreateContext(database.TenantA);
        var service = new FlowProjectService(context, new TestTenantContext(database.TenantA));
        Assert.Equal(["Newer", "Older"], (await service.GetProjectsAsync()).Select(project => project.Name));
    }

    [Fact]
    public async Task UndoProjectAsync_RejectsProjectWithoutUndoSnapshot()
    {
        var id = await database.SeedProjectAsync(database.TenantA);
        await using var context = database.CreateContext(database.TenantA);
        var service = new FlowProjectService(context, new TestTenantContext(database.TenantA));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UndoProjectAsync(id));
        Assert.Contains("no saved change", error.Message);
    }

    public void Dispose() => database.Dispose();
}
