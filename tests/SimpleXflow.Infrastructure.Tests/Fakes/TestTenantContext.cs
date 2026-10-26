using SimpleXflow.Application.Abstractions;

namespace SimpleXflow.Infrastructure.Tests.Fakes;

internal sealed class TestTenantContext(Guid? tenantId, string? userId = null) : ITenantContext
{
    public string? UserId => userId;
    public bool IsAvailable => tenantId.HasValue;

    public Guid TenantId => tenantId ?? throw new InvalidOperationException("No test tenant is available.");
}
