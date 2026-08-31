using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleXflow.Application.Abstractions;
using SimpleXflow.Application.Projects;
using SimpleXflow.Infrastructure.Identity;
using SimpleXflow.Infrastructure.Persistence;
using SimpleXflow.Infrastructure.Projects;
using SimpleXflow.Infrastructure.Services;

namespace SimpleXflow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseProvider = configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, CurrentTenantContext>();
        if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
            || databaseProvider.Equals("AzureSql", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<ApplicationDbContext, SqlServerApplicationDbContext>(options =>
                options.UseSqlServer(
                    connectionString,
                    sqlOptions => sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null)));
        }
        else if (databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase)
            || databaseProvider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
            || databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<ApplicationDbContext, PostgresApplicationDbContext>(options =>
                options.UseNpgsql(
                    connectionString,
                    postgresOptions => postgresOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null)));
        }
        else if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(connectionString));
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported database provider '{databaseProvider}'. Use 'Sqlite', 'SqlServer', or 'Postgres'.");
        }

        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, TenantClaimsPrincipalFactory>();
        services.AddScoped<IFlowProjectService, FlowProjectService>();

        return services;
    }
}
