using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SimpleXflow.Application.Abstractions;
using SimpleXflow.Infrastructure.Identity;

namespace SimpleXflow.Infrastructure.Services;

public sealed class CurrentTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public bool IsAvailable => TryGetTenantId(out _);

    public Guid TenantId => TryGetTenantId(out var tenantId)
        ? tenantId
        : throw new InvalidOperationException("No tenant is available for the current request.");

    private bool TryGetTenantId(out Guid tenantId)
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirstValue(TenantClaimTypes.TenantId);
        return Guid.TryParse(value, out tenantId);
    }
}
