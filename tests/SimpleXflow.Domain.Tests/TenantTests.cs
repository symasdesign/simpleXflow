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
    public void CreateSlug_NormalizesLikeTenantCreation()
    {
        var slug = Tenant.CreateSlug("  OST Simulation Lab  ");

        Assert.Equal("ost-simulation-lab", slug);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void Create_RejectsEmptyName(string? name)
    {
        Assert.Throws<ArgumentException>("name", () => Tenant.Create(name!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void CreateSlug_RejectsEmptyName(string? name)
    {
        Assert.Throws<ArgumentException>("name", () => Tenant.CreateSlug(name!));
    }

    [Theory]
    [InlineData("--OST___Simulation...Lab--", "ost-simulation-lab")]
    [InlineData("\tA\r\nB   C\t", "a-b-c")]
    [InlineData("Department 42", "department-42")]
    [InlineData("Zürich & St. Gallen", "zürich-st-gallen")]
    [InlineData("日本語 研究室", "日本語-研究室")]
    [InlineData("Istanbul", "istanbul")]
    public void CreateSlug_CollapsesSeparatorsAndPreservesLettersAndDigits(string name, string expected)
    {
        var tenant = Tenant.Create(name);

        Assert.Equal(expected, tenant.Slug);
        Assert.Equal(expected, Tenant.CreateSlug(name));
        Assert.Equal(expected, Tenant.CreateSlug(expected));
    }

    [Fact]
    public void Create_InitializesIndependentTenantsWithoutProjects()
    {
        var before = DateTimeOffset.UtcNow;
        var first = Tenant.Create("Shared name");
        var second = Tenant.Create("Shared name");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.Slug, second.Slug);
        Assert.Empty(first.Projects);
        Assert.Empty(second.Projects);
        Assert.InRange(first.CreatedUtc, before, DateTimeOffset.UtcNow);
        Assert.InRange(second.CreatedUtc, before, DateTimeOffset.UtcNow);
    }
}
