using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SimpleXflow.Web.Tests.Support;

namespace SimpleXflow.Web.Tests;

public sealed class HttpSurfaceTests : WebTest
{
    [Theory]
    [InlineData("/", "Semi-formal simulation specification")]
    [InlineData("/help", "Get started with tenant-aware")]
    [InlineData("/Account/Login", "Welcome back")]
    [InlineData("/Account/Register", "Create your account")]
    [InlineData("/Account/ForgotPassword", "Forgot your password?")]
    [InlineData("/Account/AccessDenied", "Access denied")]
    public async Task PublicPages_RenderWithoutAuthentication(string path, string expectedContent)
    {
        using var response = await Browser.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(expectedContent, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/projects")]
    [InlineData("/projects/3a2b0c70-164e-4ce2-91e7-1bcf50f623aa")]
    [InlineData("/workspaces")]
    [InlineData("/workspaces/3a2b0c70-164e-4ce2-91e7-1bcf50f623aa")]
    [InlineData("/workspaces/3a2b0c70-164e-4ce2-91e7-1bcf50f623aa/projects/41b70ee3-16f9-42eb-83e6-26889901cc80")]
    [InlineData("/Account/Manage")]
    [InlineData("/Account/Manage/ChangePassword")]
    [InlineData("/Account/Manage/PersonalData")]
    public async Task ProtectedPages_ChallengeAnonymousUsersAndPreserveReturnUrl(string path)
    {
        using var response = await Browser.GetAsync(path);

        AssertRedirect(response, "/Account/Login");
        Assert.Equal("?ReturnUrl=" + Uri.EscapeDataString(path), response.Headers.Location!.Query);
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFoundPage()
    {
        using var response = await Browser.GetAsync("/route-that-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Not Found", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthEndpoints_ReportProcessAndSqliteConnectivity()
    {
        var process = await Browser.GetFromJsonAsync<JsonElement>("/healthz");
        var database = await Browser.GetFromJsonAsync<JsonElement>("/healthz/db");

        Assert.Equal("ok", process.GetProperty("status").GetString());
        Assert.Equal("ok", database.GetProperty("status").GetString());
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", database.GetProperty("provider").GetString());
    }

    [Fact]
    public async Task DatabaseHealth_WhenConnectionFails_Returns503WithoutLeakingMessageOrConnectionString()
    {
        Factory.FailDatabaseConnections = true;
        using var response = await Browser.GetAsync("/healthz/db");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal("unavailable", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("SqliteException", json.RootElement.GetProperty("error").GetProperty("type").GetString());
        Assert.DoesNotContain("Simulated database unavailable", body);
        Assert.DoesNotContain(Factory.DatabasePath, body);
        using var liveness = await Browser.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
    }

    [Fact]
    public async Task Startup_InitializesLocalSchemaAndPersistsDataProtectionKeysInItsDatabase()
    {
        _ = await ReadFormAsync(Browser, "/Account/Register", "register");

        Assert.True(File.Exists(Factory.DatabasePath));
        var state = await Factory.WithDatabaseAsync(async db => new
        {
            Users = await db.Users.CountAsync(),
            Tenants = await db.Tenants.CountAsync(),
            Projects = await db.Projects.IgnoreQueryFilters().CountAsync(),
            Attachments = await db.ProjectAttachments.IgnoreQueryFilters().CountAsync(),
            Keys = await db.DataProtectionKeys.CountAsync()
        });
        Assert.Equal(0, state.Users);
        Assert.Equal(0, state.Tenants);
        Assert.Equal(0, state.Projects);
        Assert.Equal(0, state.Attachments);
        Assert.True(state.Keys > 0);
    }

    [Theory]
    [InlineData("/Account/Register", "register")]
    [InlineData("/Account/Login", "login")]
    [InlineData("/Account/Logout", null)]
    public async Task AccountPosts_RejectMissingAntiforgeryToken(string path, string? handler)
    {
        using var response = await Browser.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = handler ?? "",
            ["returnUrl"] = ""
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await Factory.WithDatabaseAsync(db => db.Users.CountAsync()));
    }

    [Fact]
    public async Task AntiforgeryToken_FromAnotherBrowser_IsRejected()
    {
        var form = await ReadFormAsync(Browser, "/Account/Register", "register");
        using var otherBrowser = Factory.CreateBrowser();
        using var response = await otherBrowser.PostAsync("/Account/Register", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
