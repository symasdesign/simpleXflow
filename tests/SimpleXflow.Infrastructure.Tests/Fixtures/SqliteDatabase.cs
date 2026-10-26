using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SimpleXflow.Domain.Projects;
using SimpleXflow.Domain.Tenants;
using SimpleXflow.Infrastructure.Persistence;
using SimpleXflow.Infrastructure.Tests.Fakes;

namespace SimpleXflow.Infrastructure.Tests.Fixtures;

/// <summary>One relational database per test, shared by independent DbContexts.</summary>
internal sealed class SqliteDatabase : IDisposable
{
    public SqliteConnection Connection { get; } = new("Data Source=:memory:;Foreign Keys=True");
    public Guid TenantA { get; }
    public Guid TenantB { get; }

    public SqliteDatabase()
    {
        Connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
        var tenantA = Tenant.Create("Fixture tenant A");
        var tenantB = Tenant.Create("Fixture tenant B");
        TenantA = tenantA.Id;
        TenantB = tenantB.Id;
        context.Tenants.AddRange(tenantA, tenantB);
        context.SaveChanges();
    }

    // No tenant is used only for arrangement/verification, like administrative access.
    public ApplicationDbContext CreateContext(Guid? tenantId = null, string? userId = null) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(Connection).Options,
        new TestTenantContext(tenantId, userId));

    public async Task<Guid> SeedProjectAsync(Guid tenantId, string name = "Original", string xml = "<original />")
    {
        await using var context = CreateContext();
        var project = new FlowProject(tenantId, name, xml);
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project.Id;
    }

    public void Dispose() => Connection.Dispose();
}
