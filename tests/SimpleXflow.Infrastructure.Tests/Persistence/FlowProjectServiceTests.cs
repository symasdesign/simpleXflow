using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SimpleXflow.Application.Projects;
using SimpleXflow.Domain.Projects;
using SimpleXflow.Domain.Tenants;
using SimpleXflow.Infrastructure.Persistence;
using SimpleXflow.Infrastructure.Projects;
using SimpleXflow.Infrastructure.Tests.Fakes;

namespace SimpleXflow.Infrastructure.Tests.Persistence;

public sealed class FlowProjectServiceTests : IDisposable
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");

    public FlowProjectServiceTests()
    {
        connection.Open();
    }

    [Fact]
    public async Task GetProjectsAsync_ReturnsOnlyProjectsForCurrentTenant()
    {
        var tenantA = Tenant.Create("Tenant A");
        var tenantB = Tenant.Create("Tenant B");
        await using var setupContext = CreateContext(null);
        await setupContext.Database.EnsureCreatedAsync();
        setupContext.Tenants.AddRange(tenantA, tenantB);
        setupContext.Projects.Add(new FlowProject(tenantA.Id, "Visible", "<a />"));
        setupContext.Projects.Add(new FlowProject(tenantB.Id, "Hidden", "<b />"));
        await setupContext.SaveChangesAsync();

        await using var tenantContext = CreateContext(tenantA.Id);
        var service = new FlowProjectService(tenantContext, new TestTenantContext(tenantA.Id));

        var projects = await service.GetProjectsAsync();

        var project = Assert.Single(projects);
        Assert.Equal("Visible", project.Name);
    }

    [Fact]
    public async Task GetProjectsAsync_SortsNewestProjectFirstInDotNetForSqliteCompatibility()
    {
        var tenant = Tenant.Create("Tenant A");
        await using var setupContext = CreateContext(null);
        await setupContext.Database.EnsureCreatedAsync();
        setupContext.Tenants.Add(tenant);

        setupContext.Projects.Add(new FlowProject(tenant.Id, "Older", "<older />"));
        await setupContext.SaveChangesAsync();
        await Task.Delay(20);
        setupContext.Projects.Add(new FlowProject(tenant.Id, "Newer", "<newer />"));
        await setupContext.SaveChangesAsync();

        await using var tenantContext = CreateContext(tenant.Id);
        var service = new FlowProjectService(tenantContext, new TestTenantContext(tenant.Id));

        var projects = await service.GetProjectsAsync();

        Assert.Equal(["Newer", "Older"], projects.Select(project => project.Name));
    }

    [Fact]
    public async Task CreateProjectAsync_AppendsSuffixWhenProjectNameAlreadyExistsInTenant()
    {
        var tenant = Tenant.Create("Tenant A");
        await using var setupContext = CreateContext(null);
        await setupContext.Database.EnsureCreatedAsync();
        setupContext.Tenants.Add(tenant);
        await setupContext.SaveChangesAsync();

        await using var tenantContext = CreateContext(tenant.Id);
        var service = new FlowProjectService(tenantContext, new TestTenantContext(tenant.Id));

        await service.CreateProjectAsync(new CreateProjectRequest("Imported flow", "<bpmn />"));
        await service.CreateProjectAsync(new CreateProjectRequest("Imported flow", "<bpmn />"));

        var projects = await service.GetProjectsAsync();

        Assert.Equal(["Imported flow", "Imported flow (1)"], projects.Select(project => project.Name).Order());
    }

    [Fact]
    public async Task CreateProjectAsync_StoresLogicXml()
    {
        var tenant = Tenant.Create("Tenant A");
        await using var setupContext = CreateContext(null);
        await setupContext.Database.EnsureCreatedAsync();
        setupContext.Tenants.Add(tenant);
        await setupContext.SaveChangesAsync();

        await using var tenantContext = CreateContext(tenant.Id);
        var service = new FlowProjectService(tenantContext, new TestTenantContext(tenant.Id));

        var projectId = await service.CreateProjectAsync(new CreateProjectRequest("Room logic", "<architecture />", "<logic />"));

        var project = await service.GetProjectAsync(projectId);
        Assert.NotNull(project);
        Assert.Equal("<logic />", project.LogicXml);
    }

    [Fact]
    public async Task SaveChangesAsync_RejectsChangingDataForAnotherTenant()
    {
        var tenantA = Tenant.Create("Tenant A");
        var tenantB = Tenant.Create("Tenant B");
        await using var setupContext = CreateContext(null);
        await setupContext.Database.EnsureCreatedAsync();
        setupContext.Tenants.AddRange(tenantA, tenantB);
        await setupContext.SaveChangesAsync();

        await using var context = CreateContext(tenantA.Id);
        context.Projects.Add(new FlowProject(tenantB.Id, "Wrong tenant", "<xml />"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        Assert.Contains("Tenant scoped data", exception.Message);
    }

    private ApplicationDbContext CreateContext(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        return new ApplicationDbContext(options, new TestTenantContext(tenantId));
    }

    public void Dispose()
    {
        connection.Dispose();
    }
}
