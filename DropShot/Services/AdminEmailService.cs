using DropShot.Data;
using DropShot.Models;

namespace DropShot.Services;

public class AdminEmailService(
    EmailService emailService,
    EmailTemplateService emailTemplateService,
    IConfiguration config,
    ILogger<AdminEmailService> logger)
{
    private string BaseUrl => config["App:BaseUrl"]?.TrimEnd('/') ?? "";

    /// <summary>
    /// Sends an email to all players in a fixture. The MatchLink variable resolves to the
    /// live match URL if a SavedMatch exists, otherwise the competition view page.
    /// </summary>
    public async Task SendMatchEmailAsync(CompetitionFixture fixture, string subject, string body)
    {
        var matchLink = fixture.SavedMatchId.HasValue
            ? $"{BaseUrl}/match/{fixture.SavedMatchId}"
            : $"{BaseUrl}/competition/{fixture.CompetitionId}/view";

        var competitionName = fixture.Competition?.CompetitionName ?? "";

        var players = new[] { fixture.Player1, fixture.Player2, fixture.Player3, fixture.Player4 }
            .Where(p => p?.Email != null)
            .Distinct()
            .ToList();

        await Task.WhenAll(players.Select(player =>
        {
            var resolvedSubject = SubstituteVariables(subject, player!.DisplayName, competitionName, matchLink);
            var resolvedBody = SubstituteVariables(body, player.DisplayName, competitionName, matchLink);
            var html = emailTemplateService.AdminCustomEmail(resolvedBody);
            return SendSafe(player.Email!, resolvedSubject, html, "match-specific admin email", isHtml: true);
        }));
    }

    /// <summary>
    /// Sends an email to all competition participants who have an email address.
    /// </summary>
    public async Task SendCompetitionEmailAsync(
        IEnumerable<Player> participants,
        string competitionName,
        string subject,
        string body,
        int competitionId)
    {
        var link = $"{BaseUrl}/competition/{competitionId}/view";

        await Task.WhenAll(participants
            .Where(p => p.Email != null)
            .Select(player =>
            {
                var resolvedSubject = SubstituteVariables(subject, player.DisplayName, competitionName, link);
                var resolvedBody = SubstituteVariables(body, player.DisplayName, competitionName, link);
                var html = emailTemplateService.AdminCustomEmail(resolvedBody);
                return SendSafe(player.Email!, resolvedSubject, html, "competition-wide admin email", isHtml: true);
            }));
    }

    private static string SubstituteVariables(string template, string playerName, string competitionName, string matchLink)
        => template
            .Replace("{PlayerName}", System.Net.WebUtility.HtmlEncode(playerName))
            .Replace("{CompetitionName}", System.Net.WebUtility.HtmlEncode(competitionName))
            .Replace("{MatchLink}", System.Net.WebUtility.HtmlEncode(matchLink));

    private async Task SendSafe(string email, string subject, string body, string context, bool isHtml = false)
    {
        try
        {
            await emailService.SendEmailAsync(email, subject, body, isHtml);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send {Context} to {Email}", context, email);
        }
    }

    // ── Club link requests ────────────────────────────────────────────────────

    /// <summary>
    /// Notifies the site-wide admin address that a new user has just registered.
    /// Destination address is read from <c>App:AdminNotificationEmail</c>
    /// (defaults to <c>admin@ds.tennis</c>). Fire-and-forget — failures are
    /// logged but don't block the registration flow.
    /// </summary>
    public async Task SendNewUserNotificationAsync(
        string userEmail, string? displayName, DateTime registeredAtUtc)
    {
        var adminAddress = config["App:AdminNotificationEmail"];
        if (string.IsNullOrWhiteSpace(adminAddress)) return;

        var body = $"""
            <p>A new user has just registered on DropShot.</p>
            <ul>
                <li><strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(userEmail)}</li>
                <li><strong>Display name:</strong> {System.Net.WebUtility.HtmlEncode(displayName ?? "(not set)")}</li>
                <li><strong>Registered at (UTC):</strong> {registeredAtUtc:yyyy-MM-dd HH:mm:ss}</li>
            </ul>
            """;

        await SendSafe(
            adminAddress,
            "New DropShot user registered",
            emailTemplateService.AdminCustomEmail(body),
            "new user notification",
            isHtml: true);
    }

    /// <summary>
    /// Notifies club admins that a new link request has been submitted.
    /// </summary>
    public async Task SendClubLinkRequestReceivedAsync(
        Club club, ApplicationUser requester, IEnumerable<ApplicationUser> clubAdmins)
    {
        var manageLink = $"{BaseUrl}/clubadmin/link-requests";
        var requesterName = requester.DisplayName is { Length: > 0 } ? requester.DisplayName : requester.UserName ?? "A user";

        foreach (var admin in clubAdmins)
        {
            if (string.IsNullOrEmpty(admin.Email)) continue;

            var adminName = admin.DisplayName ?? admin.UserName ?? "";
            var subject = $"New club link request for {club.Name}";
            var html = emailTemplateService.ClubLinkRequestReceivedEmail(adminName, requesterName, club.Name, manageLink);
            await SendSafe(admin.Email, subject, html, "club link request received", isHtml: true);
        }
    }

    /// <summary>
    /// Notifies the requesting user that their club link request was approved.
    /// </summary>
    public async Task SendClubLinkRequestApprovedAsync(Club club, ApplicationUser user)
    {
        if (string.IsNullOrEmpty(user.Email)) return;

        var clubLink = $"{BaseUrl}/clubs";
        var userName = user.DisplayName ?? user.UserName ?? "";
        var subject = $"You're now linked to {club.Name}";
        var html = emailTemplateService.ClubLinkRequestApprovedEmail(userName, club.Name, clubLink);
        await SendSafe(user.Email, subject, html, "club link request approved", isHtml: true);
    }

    /// <summary>
    /// Notifies the requesting user that their club link request was rejected.
    /// </summary>
    public async Task SendClubLinkRequestRejectedAsync(Club club, ApplicationUser user)
    {
        if (string.IsNullOrEmpty(user.Email)) return;

        var userName = user.DisplayName ?? user.UserName ?? "";
        var subject = "Club link request declined";
        var html = emailTemplateService.ClubLinkRequestRejectedEmail(userName, club.Name);
        await SendSafe(user.Email, subject, html, "club link request rejected", isHtml: true);
    }
}
