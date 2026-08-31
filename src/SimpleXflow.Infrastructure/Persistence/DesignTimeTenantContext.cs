using SimpleXflow.Application.Abstractions;

namespace SimpleXflow.Infrastructure.Persistence;

internal sealed class DesignTimeTenantContext : ITenantContext
{
    public bool IsAvailable => false;

    public Guid TenantId => Guid.Empty;
}
