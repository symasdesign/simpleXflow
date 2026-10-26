using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SimpleXflow.Infrastructure.Identity;
using SimpleXflow.Infrastructure.Services;

namespace SimpleXflow.Infrastructure.Tests.Identity;

public sealed class CurrentTenantContextTests
{
    [Fact]
    public void WithoutHttpContext_TenantIsUnavailableAndAccessThrows()
    {
        var context = new CurrentTenantContext(new TestHttpContextAccessor());
        Assert.False(context.IsAvailable);
        Assert.Throws<InvalidOperationException>(() => context.TenantId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-guid")]
    public void MissingOrMalformedTenantClaim_IsUnavailable(string? claim)
    {
        var identity = new ClaimsIdentity("Test");
        if (claim is not null)
        {
            identity.AddClaim(new Claim(TenantClaimTypes.TenantId, claim));
        }
        var accessor = new TestHttpContextAccessor { HttpContext = new DefaultHttpContext { User = new(identity) } };
        var context = new CurrentTenantContext(accessor);
        Assert.False(context.IsAvailable);
        Assert.Throws<InvalidOperationException>(() => context.TenantId);
    }

    [Fact]
    public void ValidClaim_ResolvesCurrentTenantAndFollowsRequestChanges()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var accessor = new TestHttpContextAccessor { HttpContext = Request(tenantA) };
        var context = new CurrentTenantContext(accessor);
        Assert.True(context.IsAvailable);
        Assert.Equal(tenantA, context.TenantId);
        accessor.HttpContext = Request(tenantB);
        Assert.Equal(tenantB, context.TenantId);
        accessor.HttpContext = null;
        Assert.False(context.IsAvailable);
    }

    [Fact]
    public void UnrelatedGuidClaim_DoesNotIdentifyTenant()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Test");
        var context = new CurrentTenantContext(new TestHttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new(identity) },
        });
        Assert.False(context.IsAvailable);
    }

    private static DefaultHttpContext Request(Guid id) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(TenantClaimTypes.TenantId, id.ToString())], "Test")),
    };

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
