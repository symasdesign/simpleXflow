using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleXflow.Infrastructure;
using SimpleXflow.Infrastructure.Persistence;

namespace SimpleXflow.Infrastructure.Tests.Persistence;

public sealed class DatabaseProviderConfigurationTests
{
    [Fact]
    public void PostgresDesignTimeContext_UsesNpgsqlMigrationsAndIdentitySchemaVersion3()
    {
        using var dbContext = new PostgresApplicationDbContextFactory().CreateDbContext([]);

        Assert.Contains("Npgsql", dbContext.Database.ProviderName);
        Assert.Contains("20260831132037_InitialCreate", dbContext.Database.GetMigrations());
        AssertIdentityPasskeysAreMapped(dbContext);
    }

    [Fact]
    public void SqlServerDesignTimeContext_UsesSqlServerMigrationsAndIdentitySchemaVersion3()
    {
        using var dbContext = new SqlServerApplicationDbContextFactory().CreateDbContext([]);

        Assert.Contains("SqlServer", dbContext.Database.ProviderName);
        Assert.Contains("20260828142810_InitialCreate", dbContext.Database.GetMigrations());
        AssertIdentityPasskeysAreMapped(dbContext);
    }

    [Fact]
    public void AddInfrastructure_RegistersPostgresContextForPostgresProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Postgres",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=simplexflow;Username=postgres;Password=postgres",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<IdentityOptions>(options =>
        {
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        });

        services.AddInfrastructure(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.IsType<PostgresApplicationDbContext>(dbContext);
        Assert.Contains("Npgsql", dbContext.Database.ProviderName);
        AssertIdentityPasskeysAreMapped(dbContext);
    }

    private static void AssertIdentityPasskeysAreMapped(ApplicationDbContext dbContext)
    {
        var passkeyEntity = dbContext.Model.FindEntityType(typeof(IdentityUserPasskey<string>));

        Assert.NotNull(passkeyEntity);
        Assert.Equal("AspNetUserPasskeys", passkeyEntity.GetTableName());
    }
}
