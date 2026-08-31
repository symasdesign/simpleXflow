using Microsoft.EntityFrameworkCore;
using SimpleXflow.Application.Abstractions;

namespace SimpleXflow.Infrastructure.Persistence;

public sealed class SqlServerApplicationDbContext(
    DbContextOptions<SqlServerApplicationDbContext> options,
    ITenantContext tenantContext)
    : ApplicationDbContext(options, tenantContext);
