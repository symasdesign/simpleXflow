using Microsoft.AspNetCore.Identity;
using SimpleXflow.Domain.Tenants;

namespace SimpleXflow.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = default!;
}
