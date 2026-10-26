using System.Net;
using System.Text.RegularExpressions;

namespace SimpleXflow.Web.Tests.Support;

public abstract class WebTest : IAsyncLifetime
{
    protected const string ValidPassword = "Test-password-42!";
    protected SimpleXflowWebFactory Factory { get; } = new();
    protected HttpClient Browser { get; private set; } = null!;

    public Task InitializeAsync()
    {
        Browser = Factory.CreateBrowser();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Browser.Dispose();
        await Factory.DisposeAsync();
    }

    protected static async Task<Dictionary<string, string>> ReadFormAsync(
        HttpClient browser, string url, string? handler = null)
    {
        using var response = await browser.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        foreach (Match form in Regex.Matches(html, @"<form\b[^>]*>(.*?)</form>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
            var values = new Dictionary<string, string>();
            foreach (Match input in Regex.Matches(form.Value, @"<input\b[^>]*>", RegexOptions.IgnoreCase))
            {
                var attributes = Regex.Matches(input.Value, "([\\w-]+)=\"([^\"]*)\"")
                    .ToDictionary(match => match.Groups[1].Value, match => WebUtility.HtmlDecode(match.Groups[2].Value));
                if (attributes.GetValueOrDefault("type") == "hidden" && attributes.TryGetValue("name", out var name))
                {
                    values[name] = attributes.GetValueOrDefault("value", "");
                }
            }

            if (values.ContainsKey("__RequestVerificationToken") &&
                (handler is null || values.GetValueOrDefault("_handler") == handler))
            {
                return values;
            }
        }

        Assert.Fail($"No antiforgery form with handler '{handler}' was rendered at {url}.");
        return null!;
    }

    protected static async Task<HttpResponseMessage> SubmitAsync(
        HttpClient browser, string url, string handler, Dictionary<string, string> fields)
    {
        var form = await ReadFormAsync(browser, url, handler);
        foreach (var (key, value) in fields)
        {
            form[key] = value;
        }

        return await browser.PostAsync(url, new FormUrlEncodedContent(form));
    }

    protected Task<HttpResponseMessage> RegisterAsync(
        string email = "owner@example.test", string tenant = "Test workspace", HttpClient? browser = null) =>
        SubmitAsync(browser ?? Browser, "/Account/Register?ReturnUrl=%2Fprojects", "register", new()
        {
            ["Input.TenantName"] = tenant,
            ["Input.Email"] = email,
            ["Input.Password"] = ValidPassword,
            ["Input.ConfirmPassword"] = ValidPassword
        });

    protected static void AssertRedirect(HttpResponseMessage response, string path)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var location = new Uri(new Uri("https://localhost"), response.Headers.Location);
        Assert.Equal("localhost", location.Host);
        Assert.Equal(path, location.AbsolutePath);
    }
}
