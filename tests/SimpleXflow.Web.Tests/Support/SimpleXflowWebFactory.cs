using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SimpleXflow.Infrastructure.Persistence;

namespace SimpleXflow.Web.Tests.Support;

/// <summary>
/// Boots the real middleware, Razor components, Identity and initializer. Every factory owns
/// a temporary SQLite database; no application database or external service is used.
/// </summary>
public sealed class SimpleXflowWebFactory : WebApplicationFactory<Program>
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "simplexflow-web-tests", Guid.NewGuid().ToString("N"));
    private readonly ConnectionFailureInterceptor connectionFailure = new();

    public string DatabasePath => Path.Combine(directory, "test.db");
    public bool FailDatabaseConnections
    {
        get => connectionFailure.Enabled;
        set => connectionFailure.Enabled = value;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(directory);
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={DatabasePath};Pooling=False");
        // Account tests must never use a developer's real SMTP configuration.
        builder.UseSetting("Email:Smtp:Host", "");
        builder.UseSetting("Email:Smtp:From", "");
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Critical));
        builder.ConfigureTestServices(services =>
        {
            // Replace both the context and deferred options callbacks, including any provider
            // configured by the developer's environment, before the initializer can resolve it.
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<SqlServerApplicationDbContext>();
            services.RemoveAll<PostgresApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options
                .UseSqlite($"Data Source={DatabasePath};Pooling=False")
                .AddInterceptors(connectionFailure));
        });
    }

    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    public async Task<T> WithDatabaseAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class ConnectionFailureInterceptor : DbConnectionInterceptor
    {
        public bool Enabled { get; set; }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (Enabled)
            {
                throw new SqliteException("Simulated database unavailable.", 14);
            }

            return ValueTask.FromResult(result);
        }
    }
}
