using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleXflow.Infrastructure.Persistence;

public sealed class SqlServerApplicationDbContextFactory
    : IDesignTimeDbContextFactory<SqlServerApplicationDbContext>
{
    public SqlServerApplicationDbContext CreateDbContext(string[] args)
    {
        var applicationServices = CreateApplicationServices();
        var optionsBuilder = new DbContextOptionsBuilder<SqlServerApplicationDbContext>();
        optionsBuilder.UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=SimpleXflowDesignTime;Trusted_Connection=True;MultipleActiveResultSets=true")
            .UseApplicationServiceProvider(applicationServices);

        return new SqlServerApplicationDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
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
