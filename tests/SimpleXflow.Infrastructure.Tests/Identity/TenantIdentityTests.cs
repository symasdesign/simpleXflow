using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpleXflow.Application.Abstractions;
using SimpleXflow.Infrastructure.Identity;
using SimpleXflow.Infrastructure.Persistence;
using SimpleXflow.Infrastructure.Services;
using SimpleXflow.Infrastructure.Tests.Fakes;
using SimpleXflow.Infrastructure.Tests.Fixtures;

namespace SimpleXflow.Infrastructure.Tests.Identity;

public sealed class TenantIdentityTests : IDisposable
{
    private readonly SqliteDatabase database = new();

    [Fact]
    public async Task ClaimsFactory_PreservesIdentityClaimsAndAddsPersistedTenant()
    {
        using var provider = CreateServices();
        string userId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = "alice@example.test",
                Email = "alice@example.test",
                TenantId = database.TenantA,
            };
            var result = await manager.CreateAsync(user, "Valid-password-123!");
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
            userId = user.Id;
            Assert.True((await manager.AddClaimAsync(user, new Claim("permission", "edit-projects"))).Succeeded);
        }

        await using var verification = provider.CreateAsyncScope();
        var userManager = verification.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var persisted = await userManager.FindByIdAsync(userId);
        Assert.NotNull(persisted);
        Assert.True(await userManager.CheckPasswordAsync(persisted, "Valid-password-123!"));
        Assert.False(await userManager.CheckPasswordAsync(persisted, "wrong-password"));
        var factory = verification.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();
        Assert.IsType<TenantClaimsPrincipalFactory>(factory);
        var principal = await factory.CreateAsync(persisted);
        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Equal(userId, principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("alice@example.test", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("edit-projects", principal.FindFirstValue("permission"));
        Assert.Equal(database.TenantA.ToString(), Assert.Single(principal.FindAll(TenantClaimTypes.TenantId)).Value);
        var tenant = new CurrentTenantContext(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal },
        });
        Assert.True(tenant.IsAvailable);
        Assert.Equal(database.TenantA, tenant.TenantId);
    }

    [Fact]
    public async Task UserStore_EnforcesTenantForeignKey()
    {
        await using var context = database.CreateContext();
        context.Users.Add(new ApplicationUser { UserName = "orphan", TenantId = Guid.NewGuid() });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task TenantDeletion_IsRestrictedWhenUserExists()
    {
        await using (var context = database.CreateContext())
        {
            context.Users.Add(new ApplicationUser { UserName = "owner", TenantId = database.TenantA });
            await context.SaveChangesAsync();
        }
        await using var deleteContext = database.CreateContext();
        deleteContext.Tenants.Remove(await deleteContext.Tenants.SingleAsync(tenant => tenant.Id == database.TenantA));
        await Assert.ThrowsAsync<DbUpdateException>(() => deleteContext.SaveChangesAsync());
        await using var verification = database.CreateContext();
        Assert.Equal(database.TenantA, (await verification.Users.SingleAsync()).TenantId);
    }

    private ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITenantContext>(new TestTenantContext(database.TenantA));
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(database.Connection));
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, TenantClaimsPrincipalFactory>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    public void Dispose() => database.Dispose();
}
