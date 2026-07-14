using SendGrid;
using SendGrid.Helpers.Mail;

namespace Launchly.API.Infrastructure.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string firstName, string token);
    Task SendPasswordResetEmailAsync(string toEmail, string firstName, string token);
}

public class EmailService : IEmailService
{
    private readonly ISendGridClient _client;
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        ISendGridClient client,
        IConfiguration config,
        ILogger<EmailService> logger)
    {
        _client = client;
        _config = config;
        _logger = logger;
    }

    // ─── Verification Email ───────────────────────────────────────────────────

    public async Task SendVerificationEmailAsync(string toEmail, string firstName, string token)
    {
        var platformDomain = _config["PLATFORM_DOMAIN"] ?? "localhost:4200";
        var scheme = platformDomain.Contains("localhost") ? "http" : "https";
        var link = $"{scheme}://{platformDomain}/verify-email?token={token}";

        var subject = "Verify your Launchly account";
        var plainText = $"Hi {firstName},\n\nPlease verify your email by visiting:\n{link}\n\nThis link expires in 24 hours.";
        var html = $@"
            <p>Hi {firstName},</p>
            <p>Welcome to Launchly! Please verify your email address to activate your account.</p>
            <p><a href=""{link}"">Verify my email</a></p>
            <p>This link expires in 24 hours. If you didn't create this account, you can ignore this email.</p>";

        await SendAsync(toEmail, subject, plainText, html);
    }

    // ─── Password Reset Email ─────────────────────────────────────────────────

    public async Task SendPasswordResetEmailAsync(string toEmail, string firstName, string token)
    {
        var platformDomain = _config["PLATFORM_DOMAIN"] ?? "localhost:4200";
        var scheme = platformDomain.Contains("localhost") ? "http" : "https";
        var link = $"{scheme}://{platformDomain}/reset-password?token={token}";

        var subject = "Reset your Launchly password";
        var plainText = $"Hi {firstName},\n\nReset your password by visiting:\n{link}\n\nThis link expires in 30 minutes. If you didn't request this, ignore this email.";
        var html = $@"
            <p>Hi {firstName},</p>
            <p>We received a request to reset your password.</p>
            <p><a href=""{link}"">Reset my password</a></p>
            <p>This link expires in 30 minutes. If you didn't request this, you can safely ignore this email — your password will not be changed.</p>";

        await SendAsync(toEmail, subject, plainText, html);
    }

    // ─── Private Send Helper ──────────────────────────────────────────────────

    private async Task SendAsync(string toEmail, string subject, string plainText, string html)
    {
        var fromEmail = _config["SENDGRID_FROM_EMAIL"] ?? "noreply@launchly.app";
        var fromName = _config["SENDGRID_FROM_NAME"] ?? "Launchly";

        var from = new EmailAddress(fromEmail, fromName);
        var to = new EmailAddress(toEmail);

        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainText, html);

        try
        {
            var response = await _client.SendEmailAsync(msg);

            if ((int)response.StatusCode >= 400)
            {
                var body = await response.Body.ReadAsStringAsync();
                _logger.LogWarning(
                    "SendGrid returned {StatusCode} sending to {Email}: {Body}",
                    response.StatusCode, toEmail, body);
            }
        }
        catch (Exception ex)
        {
            // Email failure should never break the calling operation
            // (registration / password reset must still succeed).
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
        }
    }
}