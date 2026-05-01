using System.Net;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Services;

/// <summary>
/// Web implementation of IEmailService. Applies the existing ContactRateLimiter, formats the
/// admin notification body, and delegates to EmailService/EmailTemplateService.
/// </summary>
public sealed class WebEmailService : IEmailService
{
    private readonly EmailService _emailService;
    private readonly EmailTemplateService _templateService;
    private readonly ContactRateLimiter _rateLimiter;
    private readonly IConfiguration _config;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public WebEmailService(
        EmailService emailService,
        EmailTemplateService templateService,
        ContactRateLimiter rateLimiter,
        IConfiguration config,
        IHttpContextAccessor httpContextAccessor)
    {
        _emailService = emailService;
        _templateService = templateService;
        _rateLimiter = rateLimiter;
        _config = config;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<bool> SendContactMessageAsync(ContactMessageDto message, CancellationToken ct = default)
    {
        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (!_rateLimiter.TryRecord($"ip:{ip}") || !_rateLimiter.TryRecord($"email:{message.Email}"))
            return false;

        var adminAddress = _config["App:AdminNotificationEmail"] ?? "admin@ds.tennis";
        var body = $"""
            <p><strong>From:</strong> {WebUtility.HtmlEncode(message.Name)} &lt;{WebUtility.HtmlEncode(message.Email)}&gt;</p>
            <p><strong>Subject:</strong> {WebUtility.HtmlEncode(message.Subject)}</p>
            <hr />
            <p>{WebUtility.HtmlEncode(message.Message).Replace("\n", "<br />")}</p>
            """;

        await _emailService.SendEmailAsync(
            adminAddress,
            $"[DropShot contact] {message.Subject}",
            _templateService.AdminCustomEmail(body),
            isHtml: true);

        return true;
    }
}
