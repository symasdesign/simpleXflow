using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleXflow.Infrastructure.Persistence;

public sealed class PostgresApplicationDbContextFactory
    : IDesignTimeDbContextFactory<PostgresApplicationDbContext>
{
    public PostgresApplicationDbContext CreateDbContext(string[] args)
    {
        var applicationServices = CreateApplicationServices();
        var optionsBuilder = new DbContextOptionsBuilder<PostgresApplicationDbContext>();
        optionsBuilder.UseNpgsql(
                "Host=localhost;Database=simplexflow_design_time;Username=postgres;Password=postgres")
            .UseApplicationServiceProvider(applicationServices);

        return new PostgresApplicationDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }

    private static IServiceProvider CreateApplicationServices()
    {
        var services = new ServiceCollection();
        services.Configure<IdentityOptions>(options =>
        {
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        });

        return services.BuildServiceProvider();
    }
}
