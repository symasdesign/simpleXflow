using Microsoft.EntityFrameworkCore;
using SimpleXflow.Application.Abstractions;

namespace SimpleXflow.Infrastructure.Persistence;

public sealed class PostgresApplicationDbContext(
    DbContextOptions<PostgresApplicationDbContext> options,
    ITenantContext tenantContext)
    : ApplicationDbContext(options, tenantContext);
