using System.Data.Common;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimpleXflow.Infrastructure;
using SimpleXflow.Infrastructure.Identity;
using SimpleXflow.Infrastructure.Persistence;
using SimpleXflow.Web.Components;
using SimpleXflow.Web.Components.Account;
using SimpleXflow.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("simpleXflow");
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddScoped<DatabaseInitializer>();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

await InitializeDatabaseAsync(app);

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    if (!app.Environment.IsDevelopment()
        && !context.Request.IsHttps
        && IsPublicHost(context.Request.Host))
    {
        context.Request.Scheme = Uri.UriSchemeHttps;
    }

    await next();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/healthz/db", async (ApplicationDbContext dbContext, CancellationToken cancellationToken) =>
{
    try
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        await dbContext.Database.CloseConnectionAsync();

        return Results.Ok(new
        {
            status = "ok",
            provider = dbContext.Database.ProviderName,
        });
    }
    catch (Exception exception)
    {
        return Results.Json(
            new
            {
                status = "unavailable",
                provider = dbContext.Database.ProviderName,
                error = new
                {
                    type = exception.GetType().Name,
                    code = exception is DbException dbException ? (int?)dbException.ErrorCode : null,
                    databaseErrors = GetDatabaseErrors(exception),
                },
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();

static bool IsPublicHost(HostString host)
{
    if (!host.HasValue)
    {
        return false;
    }

    return !host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        && !host.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        && !host.Host.Equals("[::1]", StringComparison.OrdinalIgnoreCase);
}

static IReadOnlyList<DatabaseErrorDescriptor> GetDatabaseErrors(Exception exception)
{
    var errors = new List<DatabaseErrorDescriptor>();
    var currentException = exception;

    while (currentException is not null)
    {
        var errorsValue = currentException.GetType().GetProperty("Errors")?.GetValue(currentException);
        if (errorsValue is System.Collections.IEnumerable databaseErrors)
        {
            foreach (var databaseError in databaseErrors)
            {
                errors.Add(new DatabaseErrorDescriptor(
                    GetIntProperty(databaseError, "Number"),
                    GetIntProperty(databaseError, "State"),
                    GetIntProperty(databaseError, "Class")));
            }
        }

        currentException = currentException.InnerException;
    }

    return errors;
}

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync(CancellationToken.None);
}

static int? GetIntProperty(object instance, string propertyName)
{
    var value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
    if (value is null)
    {
        return null;
    }

    return Convert.ToInt32(value);
}

internal sealed record DatabaseErrorDescriptor(int? Number, int? State, int? Class);
