using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleXflow.Application.Abstractions;
using SimpleXflow.Application.Projects;
using SimpleXflow.Infrastructure.Identity;
using SimpleXflow.Infrastructure.Persistence;
using SimpleXflow.Infrastructure.Projects;
using SimpleXflow.Infrastructure.Services;
using SimpleXflow.Infrastructure.Tests.Fakes;
using SimpleXflow.Infrastructure.Tests.Fixtures;

namespace SimpleXflow.Infrastructure.Tests.Persistence;

public sealed class DatabaseProviderConfigurationTests
{
    [Fact]
    public void DirectSqliteContextBeforeIdentityRegistration_PreservesPasskeySchema()
    {
        // A fresh EF service provider makes the first model build deterministic.
        using var efServices = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        var options = SqliteDatabase.CreateOptionsBuilder(connection)
            .UseInternalServiceProvider(efServices);
        using var directContext = new ApplicationDbContext(options.Options, new TestTenantContext(null));
        AssertIdentityAndDataProtectionMapping(directContext);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityCore<ApplicationUser>(identity => identity.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddInfrastructure(Configuration("Sqlite", "Data Source=:memory:"));
        services.AddDbContext<ApplicationDbContext>(builder => builder.UseInternalServiceProvider(efServices));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var configuredContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AssertIdentityAndDataProtectionMapping(configuredContext);
        Assert.Same(directContext.Model, configuredContext.Model);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DesignTimeContext_MapsIdentityVersion3AndCompleteProviderMigrations(bool postgres)
    {
        using var context = DesignTimeContext(postgres);
        Assert.Equal(postgres ? "Npgsql.EntityFrameworkCore.PostgreSQL" : "Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
        Assert.Equal(postgres
            ? new[] { "20260831132037_InitialCreate", "20260901211324_PersistDataProtectionKeys", "20260902061445_AddProjectUndoSnapshots", "20260902071342_AddProjectVersionHistory", "20261026160052_AddWorkspaceCollaboration" }
            : new[] { "20260828142810_InitialCreate", "20260901211342_PersistDataProtectionKeys", "20260902061458_AddProjectUndoSnapshots", "20260902071356_AddProjectVersionHistory", "20261026160144_AddWorkspaceCollaboration" },
            context.Database.GetMigrations());
        AssertIdentityAndDataProtectionMapping(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Migrations_GenerateUpgradeAndRollbackScriptsWithoutDatabaseConnection(bool postgres)
    {
        using var context = DesignTimeContext(postgres);
        var migrator = context.GetService<IMigrator>();
        var upgrade = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE", upgrade);
        Assert.Contains("Projects", upgrade);
        Assert.Contains("ProjectVersions", upgrade);
        Assert.Contains("DataProtectionKeys", upgrade);
        Assert.Contains("AspNetUserPasskeys", upgrade);
        Assert.Contains("__EFMigrationsHistory", upgrade);
        var rollback = migrator.GenerateScript(context.Database.GetMigrations().Last(), Migration.InitialDatabase);
        Assert.Contains("DROP TABLE", rollback);
        Assert.Contains("ProjectVersions", rollback);
        Assert.Contains("Projects", rollback);
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MigrationSnapshot_MatchesCurrentProviderModel(bool postgres)
    {
        using var context = DesignTimeContext(postgres);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(null, typeof(ApplicationDbContext), "Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData("Sqlite", typeof(ApplicationDbContext), "Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData("sQlItE", typeof(ApplicationDbContext), "Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData("SqlServer", typeof(SqlServerApplicationDbContext), "Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("AzureSql", typeof(SqlServerApplicationDbContext), "Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("sQlSeRvEr", typeof(SqlServerApplicationDbContext), "Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Postgres", typeof(PostgresApplicationDbContext), "Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("PostgreSql", typeof(PostgresApplicationDbContext), "Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("pOsTgReSqL", typeof(PostgresApplicationDbContext), "Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void AddInfrastructure_ResolvesConfiguredProviderAndScopedServices(string? providerName, Type contextType, string expectedProvider)
    {
        var connectionString = contextType == typeof(PostgresApplicationDbContext)
            ? "Host=localhost;Database=simplexflow;Username=postgres;Password=postgres"
            : contextType == typeof(SqlServerApplicationDbContext)
                ? "Server=localhost;Database=simplexflow;Integrated Security=true;TrustServerCertificate=true"
                : "Data Source=:memory:";
        var configuration = Configuration(providerName, connectionString);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityCore<ApplicationUser>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddEntityFrameworkStores<ApplicationDbContext>();
        Assert.Same(services, services.AddInfrastructure(configuration));
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(contextType, context.GetType());
        Assert.Equal(expectedProvider, context.Database.ProviderName);
        Assert.Same(context, scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        Assert.IsType<CurrentTenantContext>(scope.ServiceProvider.GetRequiredService<ITenantContext>());
        Assert.IsType<FlowProjectService>(scope.ServiceProvider.GetRequiredService<IFlowProjectService>());
        Assert.IsType<TenantClaimsPrincipalFactory>(scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>());
        AssertIdentityAndDataProtectionMapping(context);
        if (contextType == typeof(ApplicationDbContext))
        {
            Assert.Empty(context.Database.GetMigrations());
        }
        using var otherScope = provider.CreateScope();
        Assert.NotSame(context, otherScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Fact]
    public void AddInfrastructure_RejectsMissingConnectionString()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddInfrastructure(Configuration("Sqlite", null)));
        Assert.Contains("DefaultConnection", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("MySql")]
    [InlineData("unsupported")]
    public void AddInfrastructure_RejectsUnknownProvider(string provider)
    {
        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddInfrastructure(Configuration(provider, "Data Source=:memory:")));
        Assert.Contains("Unsupported database provider", error.Message);
    }

    private static IConfiguration Configuration(string? provider, string? connectionString) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = provider,
            ["ConnectionStrings:DefaultConnection"] = connectionString,
        }).Build();

    private static ApplicationDbContext DesignTimeContext(bool postgres) => postgres
        ? new PostgresApplicationDbContextFactory().CreateDbContext([])
        : new SqlServerApplicationDbContextFactory().CreateDbContext([]);

    private static void AssertIdentityAndDataProtectionMapping(ApplicationDbContext context)
    {
        var passkey = context.Model.FindEntityType(typeof(IdentityUserPasskey<string>));
        Assert.NotNull(passkey);
        Assert.Equal("AspNetUserPasskeys", passkey.GetTableName());
        var key = context.Model.FindEntityType(typeof(DataProtectionKey));
        Assert.NotNull(key);
        Assert.Equal("DataProtectionKeys", key.GetTableName());
    }
}
