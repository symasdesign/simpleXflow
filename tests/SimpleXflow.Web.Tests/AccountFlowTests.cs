using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SimpleXflow.Domain.Projects;
using SimpleXflow.Infrastructure.Identity;
using SimpleXflow.Web.Tests.Support;

namespace SimpleXflow.Web.Tests;

public sealed class AccountFlowTests : WebTest
{
    [Fact]
    public async Task Registration_CreatesWorkspaceAndUserAndIssuesTenantIdentityCookie()
    {
        using var response = await RegisterAsync();

        AssertRedirect(response, "/projects");
        var user = await Factory.WithDatabaseAsync(db => db.Users.Include(candidate => candidate.Tenant).SingleAsync());
        Assert.Equal("owner@example.test", user.Email);
        Assert.Equal("Test workspace", user.Tenant.Name);
        Assert.Equal("test-workspace", user.Tenant.Slug);
        Assert.Equal(user.Tenant.Id, user.TenantId);
        Assert.NotNull(user.PasswordHash);
        Assert.NotEqual(ValidPassword, user.PasswordHash);

        var ticket = ReadIdentityTicket(response);
        Assert.Equal(user.Id, ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(user.TenantId.ToString(), ticket.Principal.FindFirstValue(TenantClaimTypes.TenantId));
        Assert.False(ticket.Properties.IsPersistent);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        using var projects = await Browser.GetAsync("/projects");
        AssertRedirect(projects, "/workspaces");
        using var workspaces = await Browser.GetAsync("/workspaces");
        Assert.Equal(HttpStatusCode.OK, workspaces.StatusCode);
        var workspaceHtml = WebUtility.HtmlDecode(await workspaces.Content.ReadAsStringAsync());
        Assert.Contains(user.Tenant.Name, workspaceHtml);
        Assert.Contains("Your first workspace", workspaceHtml);
        using var profile = await Browser.GetAsync("/Account/Manage");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Contains("owner@example.test", await profile.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Input.TenantName", "", "The Organization field is required.")]
    [InlineData("Input.TenantName", "X", "minimum length of 2")]
    [InlineData("Input.Email", "invalid-email", "not a valid e-mail address")]
    [InlineData("Input.Password", "123", "at least 6")]
    [InlineData("Input.ConfirmPassword", "Different-password-42!", "do not match")]
    public async Task Registration_InvalidForm_DoesNotCreateUserOrOrphanWorkspace(string field, string value, string error)
    {
        var fields = RegistrationFields();
        fields[field] = value;
        using var response = await SubmitAsync(Browser, "/Account/Register", "register", fields);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(error, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(0, await Factory.WithDatabaseAsync(db => db.Users.CountAsync()));
        Assert.Equal(0, await Factory.WithDatabaseAsync(db => db.Tenants.CountAsync()));
    }

    [Fact]
    public async Task Registration_WeakPasswordRejectedByIdentity_DoesNotPersistWorkspace()
    {
        var fields = RegistrationFields();
        fields["Input.Password"] = fields["Input.ConfirmPassword"] = "lowercaseonly";
        using var response = await SubmitAsync(Browser, "/Account/Register", "register", fields);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Passwords must have", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, await Factory.WithDatabaseAsync(db => db.Users.CountAsync()));
        Assert.Equal(0, await Factory.WithDatabaseAsync(db => db.Tenants.CountAsync()));
    }

    [Fact]
    public async Task Registration_DuplicateEmail_DoesNotCreateOrphanWorkspace()
    {
        using var first = await RegisterAsync();
        AssertRedirect(first, "/projects");
        using var otherBrowser = Factory.CreateBrowser();
        using var duplicate = await RegisterAsync(tenant: "Unused workspace", browser: otherBrowser);

        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("already taken", await duplicate.Content.ReadAsStringAsync());
        Assert.Equal(1, await Factory.WithDatabaseAsync(db => db.Users.CountAsync()));
        Assert.Equal(1, await Factory.WithDatabaseAsync(db => db.Tenants.CountAsync()));
        using var projects = await otherBrowser.GetAsync("/projects");
        AssertRedirect(projects, "/Account/Login");
    }

    [Theory]
    [InlineData("Test workspace")]
    [InlineData("TEST WORKSPACE")]
    [InlineData("Test---workspace")]
    public async Task Registration_ExistingWorkspaceSlug_DoesNotGrantUninvitedTenantAccess(string tenant)
    {
        using var first = await RegisterAsync();
        AssertRedirect(first, "/projects");
        using var otherBrowser = Factory.CreateBrowser();
        using var attempt = await RegisterAsync("uninvited@example.test", tenant, otherBrowser);

        Assert.Equal(HttpStatusCode.OK, attempt.StatusCode);
        Assert.Contains("Please choose a different organization name.", await attempt.Content.ReadAsStringAsync());
        Assert.Equal(1, await Factory.WithDatabaseAsync(db => db.Users.CountAsync()));
        Assert.Equal(1, await Factory.WithDatabaseAsync(db => db.Tenants.CountAsync()));
        using var projects = await otherBrowser.GetAsync("/projects");
        AssertRedirect(projects, "/Account/Login");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Login_WithValidCredentials_PreservesTenantAndRememberMe(bool rememberMe)
    {
        using var registration = await RegisterAsync();
        AssertRedirect(registration, "/projects");
        using var returningBrowser = Factory.CreateBrowser();
        using var response = await SubmitAsync(returningBrowser, "/Account/Login?ReturnUrl=%2Fprojects", "login", new()
        {
            ["Input.Email"] = "owner@example.test",
            ["Input.Password"] = ValidPassword,
            ["Input.RememberMe"] = rememberMe.ToString()
        });

        AssertRedirect(response, "/projects");
        var ticket = ReadIdentityTicket(response);
        Assert.Equal(rememberMe, ticket.Properties.IsPersistent);
        Assert.Equal(ReadIdentityTicket(registration).Principal.FindFirstValue(TenantClaimTypes.TenantId),
            ticket.Principal.FindFirstValue(TenantClaimTypes.TenantId));
        using var projects = await returningBrowser.GetAsync("/workspaces");
        Assert.Equal(HttpStatusCode.OK, projects.StatusCode);
    }

    [Theory]
    [InlineData("unknown@example.test", "Wrong-password-42!")]
    [InlineData("owner@example.test", "Wrong-password-42!")]
    public async Task Login_InvalidCredentials_ReturnsGenericErrorAndLeavesBrowserAnonymous(string email, string password)
    {
        using var registration = await RegisterAsync();
        using var anonymousBrowser = Factory.CreateBrowser();
        using var response = await SubmitAsync(anonymousBrowser, "/Account/Login", "login", new()
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid login attempt.", await response.Content.ReadAsStringAsync());
        using var projects = await anonymousBrowser.GetAsync("/projects");
        AssertRedirect(projects, "/Account/Login");
    }

    [Fact]
    public async Task Logout_ClearsIdentityCookieAndRevokesAccessToProtectedPages()
    {
        using var registration = await RegisterAsync();
        var form = await ReadFormAsync(Browser, "/Account/Manage");
        form["ReturnUrl"] = "";
        using var response = await Browser.PostAsync("/Account/Logout", new FormUrlEncodedContent(form));

        AssertRedirect(response, "/");
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie =>
            cookie.StartsWith(".AspNetCore.Identity.Application=;", StringComparison.Ordinal));
        using var projects = await Browser.GetAsync("/projects");
        using var profile = await Browser.GetAsync("/Account/Manage");
        AssertRedirect(projects, "/Account/Login");
        AssertRedirect(profile, "/Account/Login");
    }

    [Fact]
    public async Task Logout_WithoutAntiforgeryToken_DoesNotSignOutUser()
    {
        using var registration = await RegisterAsync();
        using var response = await Browser.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["returnUrl"] = ""
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var projects = await Browser.GetAsync("/workspaces");
        Assert.Equal(HttpStatusCode.OK, projects.StatusCode);
    }

    [Fact]
    public async Task IdentityCookie_CannotBeReplacedWithForgedTenantClaims()
    {
        using var anonymousBrowser = Factory.CreateBrowser();
        anonymousBrowser.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Identity.Application=tenant_id%3D00000000-0000-0000-0000-000000000001");
        using var response = await anonymousBrowser.GetAsync("/projects");

        AssertRedirect(response, "/Account/Login");
    }

    [Fact]
    public async Task AuthenticatedProjectPages_FilterProjectsByTheTenantFromTheIdentityCookie()
    {
        using var firstRegistration = await RegisterAsync();
        using var secondBrowser = Factory.CreateBrowser();
        using var secondRegistration = await RegisterAsync("other@example.test", "Other workspace", secondBrowser);
        AssertRedirect(firstRegistration, "/projects");
        AssertRedirect(secondRegistration, "/projects");
        var projects = await Factory.WithDatabaseAsync(async db =>
        {
            var firstTenant = await db.Users.Where(user => user.Email == "owner@example.test").Select(user => user.TenantId).SingleAsync();
            var secondTenant = await db.Users.Where(user => user.Email == "other@example.test").Select(user => user.TenantId).SingleAsync();
            var first = new FlowProject(firstTenant, "Owner private model", "<definitions />");
            var second = new FlowProject(secondTenant, "Other private model", "<definitions />");
            db.Projects.AddRange(first, second);
            await db.SaveChangesAsync();
            return (first.Id, SecondId: second.Id);
        });

        using var firstPage = await Browser.GetAsync($"/projects/{projects.Id}");
        using var secondPage = await secondBrowser.GetAsync($"/projects/{projects.SecondId}");
        Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);
        var firstHtml = await firstPage.Content.ReadAsStringAsync();
        var secondHtml = await secondPage.Content.ReadAsStringAsync();
        Assert.Contains("Owner private model", firstHtml);
        Assert.DoesNotContain("Other private model", firstHtml);
        Assert.Contains("Other private model", secondHtml);
        Assert.DoesNotContain("Owner private model", secondHtml);

        using var forbidden = await Browser.GetAsync($"/projects/{projects.SecondId}");
        AssertRedirect(forbidden, "/workspaces");
        Assert.DoesNotContain("Other private model", await forbidden.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ForgotPassword_UnknownAccount_DoesNotRevealWhetherTheUserExists()
    {
        using var response = await SubmitAsync(Browser, "/Account/ForgotPassword", "forgot-password", new()
        {
            ["Input.Email"] = "unknown@example.test"
        });

        AssertRedirect(response, "/Account/ForgotPasswordConfirmation");
        Assert.Equal(0, await Factory.WithDatabaseAsync(db => db.Users.CountAsync()));
    }

    private AuthenticationTicket ReadIdentityTicket(HttpResponseMessage response)
    {
        const string cookiePrefix = ".AspNetCore.Identity.Application=";
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith(cookiePrefix, StringComparison.Ordinal));
        var encodedTicket = cookie[cookiePrefix.Length..].Split(';')[0];
        var options = Factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        var ticket = options.TicketDataFormat.Unprotect(encodedTicket);
        Assert.NotNull(ticket);
        return ticket;
    }

    private static Dictionary<string, string> RegistrationFields() => new()
    {
        ["Input.TenantName"] = "Test workspace",
        ["Input.Email"] = "owner@example.test",
        ["Input.Password"] = ValidPassword,
        ["Input.ConfirmPassword"] = ValidPassword
    };
}
