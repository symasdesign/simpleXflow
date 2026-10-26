using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using SimpleXflow.Infrastructure.Identity;

namespace SimpleXflow.Web.Components.Account;

public sealed class IdentityEmailSender(IConfiguration configuration, ILogger<IdentityEmailSender> logger) : IEmailSender<ApplicationUser>
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["Email:Smtp:Host"])
        && !string.IsNullOrWhiteSpace(configuration["Email:Smtp:From"]);

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your simpleXflow email", $"Confirm your email address to join shared workspaces: <a href='{confirmationLink}'>Confirm email</a>.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your simpleXflow password", $"<a href='{resetLink}'>Reset your password</a>. If you did not request this, ignore this email.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "Reset your simpleXflow password", $"Your password reset code is {HtmlEncoder.Default.Encode(resetCode)}.");

    private async Task SendAsync(string email, string subject, string html)
    {
        if (!IsConfigured)
        {
            // Local accounts remain usable; sharing requires real email verification.
            // Never expose confirmation/reset links in the UI or application logs.
            logger.LogWarning("Email delivery is unavailable because SMTP is not configured.");
            return;
        }

        using var message = new MailMessage(configuration["Email:Smtp:From"]!, email, subject, html) { IsBodyHtml = true };
        using var client = new SmtpClient(configuration["Email:Smtp:Host"], configuration.GetValue("Email:Smtp:Port", 587))
        {
            EnableSsl = configuration.GetValue("Email:Smtp:EnableSsl", true),
            UseDefaultCredentials = false
        };
        var userName = configuration["Email:Smtp:UserName"];
        if (!string.IsNullOrWhiteSpace(userName)) client.Credentials = new NetworkCredential(userName, configuration["Email:Smtp:Password"]);
        await client.SendMailAsync(message);
    }
}
