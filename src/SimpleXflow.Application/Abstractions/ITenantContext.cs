namespace SimpleXflow.Application.Abstractions;

public interface ITenantContext
{
    bool IsAvailable { get; }

    Guid TenantId { get; }
}
