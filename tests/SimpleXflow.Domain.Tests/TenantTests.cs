using SimpleXflow.Domain.Tenants;

namespace SimpleXflow.Domain.Tests;

public sealed class TenantTests
{
    [Fact]
    public void Create_NormalizesNameAndSlug()
    {
        var tenant = Tenant.Create("  OST Simulation Lab  ");

        Assert.Equal("OST Simulation Lab", tenant.Name);
        Assert.Equal("ost-simulation-lab", tenant.Slug);
        Assert.NotEqual(Guid.Empty, tenant.Id);
    }

    [Fact]
    public void Create_RejectsEmptyName()
    {
        Assert.Throws<ArgumentException>(() => Tenant.Create(" "));
    }
}
