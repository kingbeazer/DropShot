using DropShot.Data;
using DropShot.Models;

namespace DropShot.Services;

public class AdminEmailService(
    EmailService emailService,
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
            return SendSafe(player.Email!, resolvedSubject, resolvedBody, "match-specific admin email");
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
                return SendSafe(player.Email!, resolvedSubject, resolvedBody, "competition-wide admin email");
            }));
    }

    private static string SubstituteVariables(string template, string playerName, string competitionName, string matchLink)
        => template
            .Replace("{PlayerName}", System.Net.WebUtility.HtmlEncode(playerName))
            .Replace("{CompetitionName}", System.Net.WebUtility.HtmlEncode(competitionName))
            .Replace("{MatchLink}", System.Net.WebUtility.HtmlEncode(matchLink));

    private async Task SendSafe(string email, string subject, string body, string context)
    {
        try
        {
            await emailService.SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send {Context} to {Email}", context, email);
        }
    }

    // ── Club link requests ────────────────────────────────────────────────────

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

            var subject = $"New club link request for {club.Name}";
            var body =
                $"Hi {System.Net.WebUtility.HtmlEncode(admin.DisplayName ?? admin.UserName ?? "")}," +
                $"\n\n{System.Net.WebUtility.HtmlEncode(requesterName)} has asked to be linked to " +
                $"{System.Net.WebUtility.HtmlEncode(club.Name)}." +
                $"\n\nReview the request: {manageLink}";

            await SendSafe(admin.Email, subject, body, "club link request received");
        }
    }

    /// <summary>
    /// Notifies the requesting user that their club link request was approved.
    /// </summary>
    public async Task SendClubLinkRequestApprovedAsync(Club club, ApplicationUser user)
    {
        if (string.IsNullOrEmpty(user.Email)) return;

        var clubLink = $"{BaseUrl}/clubs";
        var subject = $"You're now linked to {club.Name}";
        var body =
            $"Hi {System.Net.WebUtility.HtmlEncode(user.DisplayName ?? user.UserName ?? "")}," +
            $"\n\nYour request to join {System.Net.WebUtility.HtmlEncode(club.Name)} was approved." +
            $"\n\nView the club: {clubLink}";

        await SendSafe(user.Email, subject, body, "club link request approved");
    }

    /// <summary>
    /// Notifies the requesting user that their club link request was rejected.
    /// </summary>
    public async Task SendClubLinkRequestRejectedAsync(Club club, ApplicationUser user)
    {
        if (string.IsNullOrEmpty(user.Email)) return;

        var subject = $"Club link request declined";
        var body =
            $"Hi {System.Net.WebUtility.HtmlEncode(user.DisplayName ?? user.UserName ?? "")}," +
            $"\n\nYour request to join {System.Net.WebUtility.HtmlEncode(club.Name)} was not approved. " +
            "If you think this is a mistake, please reach out to the club directly.";

        await SendSafe(user.Email, subject, body, "club link request rejected");
    }
}
