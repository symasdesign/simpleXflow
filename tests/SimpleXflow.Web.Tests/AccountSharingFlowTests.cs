using System.Net;
using Microsoft.EntityFrameworkCore;
using SimpleXflow.Web.Tests.Support;

namespace SimpleXflow.Web.Tests;

public sealed class AccountSharingFlowTests : WebTest
{
    [Fact]
    public async Task RegistrationConfirmation_DoesNotExposeConfirmationTokenOrConfirmAnotherAccount()
    {
        using var registration = await RegisterAsync();
        using var anonymous = Factory.CreateBrowser();
        using var response = await anonymous.GetAsync("/Account/RegisterConfirmation?Email=owner%40example.test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/Account/ConfirmEmail?", html);
        Assert.DoesNotContain("Click here to confirm your account", html);
        Assert.Contains("Email delivery is not configured", html);
        Assert.False(await Factory.WithDatabaseAsync(db => db.Users.Select(user => user.EmailConfirmed).SingleAsync()));
    }

    [Fact]
    public async Task Invitation_PreservesItsTokenThroughLoginAndRegistrationLinks()
    {
        using var response = await Browser.GetAsync("/invitations/accept?token=sample-token_123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("/Account/Login?ReturnUrl=%2Finvitations%2Faccept%3Ftoken%3Dsample-token_123", html);
        Assert.Contains("/Account/Register?ReturnUrl=%2Finvitations%2Faccept%3Ftoken%3Dsample-token_123", html);
        Assert.Contains("confirmed email address", html);
    }

    [Fact]
    public async Task Invitation_UnverifiedAccountMustConfirmEmailBeforeJoining()
    {
        using var registration = await RegisterAsync();
        using var response = await Browser.GetAsync("/invitations/accept?token=sample-token_123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Confirm your email address first", html);
        Assert.Contains("/Account/ResendEmailConfirmation?ReturnUrl=", html);
        Assert.DoesNotContain(">Accept invitation</button>", html);
    }

    [Fact]
    public async Task ResendConfirmation_WithoutEmailDelivery_ExplainsUnavailableInsteadOfClaimingEmailWasSent()
    {
        using var response = await SubmitAsync(Browser, "/Account/ResendEmailConfirmation", "resend-email-confirmation", new()
        {
            ["Input.Email"] = "owner@example.test"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Email delivery is not configured", html);
        Assert.DoesNotContain("Verification email sent", html);
    }

    [Fact]
    public async Task ConfirmEmail_MalformedCode_DoesNotFailTheRequestOrConfirmTheUser()
    {
        using var registration = await RegisterAsync();
        var userId = await Factory.WithDatabaseAsync(db => db.Users.Select(user => user.Id).SingleAsync());
        using var response = await Browser.GetAsync($"/Account/ConfirmEmail?UserId={userId}&Code=!");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This confirmation link is invalid", await response.Content.ReadAsStringAsync());
        Assert.False(await Factory.WithDatabaseAsync(db => db.Users.Select(user => user.EmailConfirmed).SingleAsync()));
    }
}
