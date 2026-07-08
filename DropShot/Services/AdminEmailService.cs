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
    /// Warns a SinglesLadder participant that their rating will start decaying
    /// in <paramref name="daysUntilDecay"/> days unless they play a match.
    /// Sent by <see cref="LadderInactivityService"/> from the daily sweep.
    /// </summary>
    public async Task SendLadderInactivityWarningAsync(
        Player player, Competition competition, int daysUntilDecay, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(player.Email)) return;

        var link = $"{BaseUrl}/competition/{competition.CompetitionID}/view";
        var subject = $"Play a match in {competition.CompetitionName} to keep your rating";
        var bodyText =
            $"Hi {player.DisplayName},\n\n" +
            $"Your rating in **{competition.CompetitionName}** will start decaying in {daysUntilDecay} day{(daysUntilDecay == 1 ? "" : "s")} unless you record a match.\n\n" +
            $"After the {LadderInactivityService.GraceDays}-day grace period, the ladder loses {LadderInactivityService.DecayPointsPerWeek:0} points per week of inactivity — recoverable as soon as you play again.\n\n" +
            $"[Record a match]({link})";

        var html = emailTemplateService.AdminCustomEmail(bodyText);
        await SendSafe(player.Email!, subject, html, "ladder inactivity warning", isHtml: true);
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

    /// <summary>
    /// Sends a fixture reminder email to every player in the fixture.
    /// Supports variables: {PlayerName}, {OpponentName}, {CompetitionName},
    /// {MatchDate}, {MatchLink}, {ResultLink}.
    /// </summary>
    public async Task SendFixtureReminderAsync(
        CompetitionFixture fixture,
        CompetitionFixtureReminder reminder,
        CancellationToken ct = default)
    {
        var competitionName = fixture.Competition?.CompetitionName ?? "";
        var matchDate = fixture.ScheduledAt?.ToString("dddd dd MMMM yyyy 'at' HH:mm") ?? "";
        var matchLink = $"{BaseUrl}/competition/{fixture.CompetitionId}/view";

        // Score submission URL used for singles/doubles and for captains in team fixtures.
        var scoreLink = fixture.ResultSubmissionToken.HasValue
            ? $"{BaseUrl}/match/submit/{fixture.ResultSubmissionToken.Value}"
            : matchLink;

        // Collect all players with their "opponent" label for substitution.
        var sides = new[]
        {
            (Player: fixture.Player1, OpponentName: SideName(fixture.Player2, fixture.Player4)),
            (Player: fixture.Player2, OpponentName: SideName(fixture.Player1, fixture.Player3)),
            (Player: fixture.Player3, OpponentName: SideName(fixture.Player2, fixture.Player4)),
            (Player: fixture.Player4, OpponentName: SideName(fixture.Player1, fixture.Player3)),
        };

        // For team fixtures: always email all participants. Captains get the score submission link;
        // other participants get the match overview link as {ResultLink}.
        if (fixture.HomeTeam is not null || fixture.AwayTeam is not null)
        {
            var captainIds = new HashSet<int?> {
                fixture.HomeTeam?.CaptainPlayerId,
                fixture.AwayTeam?.CaptainPlayerId
            };

            var recipients = new List<(Player player, string opponentLabel, string resultLink)>();
            var homeOpponent = fixture.AwayTeam?.Name ?? "Away team";
            var awayOpponent = fixture.HomeTeam?.Name ?? "Home team";

            if (fixture.HomeTeam?.Participants != null)
                foreach (var p in fixture.HomeTeam.Participants.Where(p => p.Player?.Email != null))
                    recipients.Add((p.Player!, homeOpponent,
                        captainIds.Contains(p.PlayerId) ? scoreLink : matchLink));

            if (fixture.AwayTeam?.Participants != null)
                foreach (var p in fixture.AwayTeam.Participants.Where(p => p.Player?.Email != null))
                    recipients.Add((p.Player!, awayOpponent,
                        captainIds.Contains(p.PlayerId) ? scoreLink : matchLink));

            await Task.WhenAll(recipients.Select(c =>
            {
                var subject = SubstituteReminderVars(reminder.Subject, c.player.DisplayName, c.opponentLabel, competitionName, matchDate, matchLink, c.resultLink);
                var body = SubstituteReminderVars(reminder.Body, c.player.DisplayName, c.opponentLabel, competitionName, matchDate, matchLink, c.resultLink);
                var html = emailTemplateService.AdminCustomEmail(body);
                return SendSafe(c.player.Email!, subject, html, "fixture reminder", isHtml: true);
            }));
            return;
        }

        // Singles/doubles: all players get the score submission link.
        await Task.WhenAll(sides
            .Where(s => s.Player?.Email != null)
            .Select(s =>
            {
                var subject = SubstituteReminderVars(reminder.Subject, s.Player!.DisplayName, s.OpponentName, competitionName, matchDate, matchLink, scoreLink);
                var body = SubstituteReminderVars(reminder.Body, s.Player.DisplayName, s.OpponentName, competitionName, matchDate, matchLink, scoreLink);
                var html = emailTemplateService.AdminCustomEmail(body);
                return SendSafe(s.Player.Email!, subject, html, "fixture reminder", isHtml: true);
            }));
    }

    private static string SideName(Player? a, Player? b)
    {
        var parts = new[] { a?.DisplayName, b?.DisplayName }.Where(n => n != null).ToList();
        return parts.Count > 0 ? string.Join(" & ", parts) : "";
    }

    private static string SubstituteReminderVars(
        string template, string playerName, string opponentName,
        string competitionName, string matchDate, string matchLink, string resultLink)
        => template
            .Replace("{PlayerName}", System.Net.WebUtility.HtmlEncode(playerName))
            .Replace("{OpponentName}", System.Net.WebUtility.HtmlEncode(opponentName))
            .Replace("{CompetitionName}", System.Net.WebUtility.HtmlEncode(competitionName))
            .Replace("{MatchDate}", System.Net.WebUtility.HtmlEncode(matchDate))
            .Replace("{MatchLink}", System.Net.WebUtility.HtmlEncode(matchLink))
            .Replace("{ResultLink}", System.Net.WebUtility.HtmlEncode(resultLink));

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

    // ── Club admin role requests ──────────────────────────────────────────────

    /// <summary>
    /// Notifies the site admin address that a user has requested club admin access.
    /// </summary>
    public async Task SendClubAdminRequestReceivedAsync(Club club, ApplicationUser requester)
    {
        var adminAddress = config["App:AdminNotificationEmail"];
        if (string.IsNullOrWhiteSpace(adminAddress)) return;

        var manageLink = $"{BaseUrl}/admin/club-admin-requests";
        var requesterName = requester.DisplayName is { Length: > 0 } ? requester.DisplayName : requester.UserName ?? "A user";
        var subject = $"New club admin request for {club.Name}";
        var html = emailTemplateService.ClubAdminRequestReceivedEmail(requesterName, club.Name, manageLink);
        await SendSafe(adminAddress, subject, html, "club admin request received", isHtml: true);
    }

    /// <summary>
    /// Notifies the requesting user that their club admin request was approved.
    /// </summary>
    public async Task SendClubAdminRequestApprovedAsync(Club club, ApplicationUser user)
    {
        if (string.IsNullOrEmpty(user.Email)) return;

        var clubLink = $"{BaseUrl}/clubs/{club.ClubId}";
        var userName = user.DisplayName ?? user.UserName ?? "";
        var subject = $"You're now an admin of {club.Name}";
        var html = emailTemplateService.ClubAdminRequestApprovedEmail(userName, club.Name, clubLink);
        await SendSafe(user.Email, subject, html, "club admin request approved", isHtml: true);
    }

    /// <summary>
    /// Notifies the requesting user that their club admin request was rejected.
    /// </summary>
    public async Task SendClubAdminRequestRejectedAsync(Club club, ApplicationUser user)
    {
        if (string.IsNullOrEmpty(user.Email)) return;

        var userName = user.DisplayName ?? user.UserName ?? "";
        var subject = "Club admin request declined";
        var html = emailTemplateService.ClubAdminRequestRejectedEmail(userName, club.Name);
        await SendSafe(user.Email, subject, html, "club admin request rejected", isHtml: true);
    }
}
