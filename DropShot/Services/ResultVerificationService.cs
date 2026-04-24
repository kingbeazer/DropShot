using DropShot.Data;
using DropShot.Models;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

public class ResultVerificationService(EmailService emailService, EmailTemplateService emailTemplateService, IConfiguration config, ILogger<ResultVerificationService> logger)
{
    private string BaseUrl => config["App:BaseUrl"]?.TrimEnd('/') ?? "";

    public async Task SendResultNotificationAsync(CompetitionFixture fixture)
    {
        var title = FixtureTitle(fixture);
        var resultSummary = fixture.ResultSummary ?? "";
        var (side1, side2) = SideNames(fixture);
        var winnerName = WinnerName(fixture);
        var subject = $"Match result: {side1} vs {side2}";
        var html = emailTemplateService.MatchResultEmail(title, resultSummary, side1, side2, winnerName);

        var emails = new[] { fixture.Player1, fixture.Player2, fixture.Player3, fixture.Player4 }
            .Where(p => p?.Email != null)
            .Select(p => p!.Email!)
            .Distinct()
            .Select(email => SendSafe(email, subject, html, "result notification", isHtml: true));

        await Task.WhenAll(emails);
    }

    public async Task SendAdminVerificationEmailsAsync(CompetitionFixture fixture, IEnumerable<string> adminEmails)
    {
        if (fixture.VerificationToken == null) return;

        var title = FixtureTitle(fixture);
        var (side1, side2) = SideNames(fixture);
        var winnerName = WinnerName(fixture);
        var verifyUrl = $"{BaseUrl}/verify-result/{fixture.VerificationToken}";
        var subject = $"Result verification required: {side1} vs {side2}";
        var html = emailTemplateService.AdminVerificationEmail(title, fixture.ResultSummary ?? "", verifyUrl, side1, side2, winnerName);

        await Task.WhenAll(adminEmails.Select(email => SendSafe(email, subject, html, "admin verification", isHtml: true)));
    }

    // ── Team match (rubber-based) notifications ──────────────────────────────

    /// <summary>
    /// Sends a result notification to every player who participated in the team
    /// match (home and away players across all rubbers).
    /// </summary>
    public async Task SendResultNotificationForTeamMatchAsync(CompetitionFixture fixture, IEnumerable<Rubber> rubbers)
    {
        var title = FixtureTitle(fixture);
        var (home, away) = SideNames(fixture);
        var winnerName = WinnerName(fixture);
        var subject = $"Match result: {home} vs {away}";
        var rubberData = BuildRubberEmailData(rubbers, fixture);
        var html = emailTemplateService.MatchResultEmailForTeamMatch(title, home, away, winnerName, rubberData);

        var playerEmails = rubbers
            .SelectMany(r => new[] { r.HomePlayer1, r.HomePlayer2, r.AwayPlayer1, r.AwayPlayer2 })
            .Where(p => p?.Email != null)
            .Select(p => p!.Email!)
            .Distinct()
            .Select(email => SendSafe(email, subject, html, "team match result notification", isHtml: true));

        await Task.WhenAll(playerEmails);
    }

    /// <summary>
    /// Sends an admin verification email for a team match. The fixture must already
    /// have a VerificationToken set.
    /// </summary>
    public async Task SendAdminVerificationEmailsForTeamMatchAsync(
        CompetitionFixture fixture, IEnumerable<Rubber> rubbers, IEnumerable<string> adminEmails)
    {
        if (fixture.VerificationToken == null) return;

        var title = FixtureTitle(fixture);
        var (home, away) = SideNames(fixture);
        var winnerName = WinnerName(fixture);
        var verifyUrl = $"{BaseUrl}/verify-result/{fixture.VerificationToken}";
        var subject = $"Result verification required: {home} vs {away}";
        var rubberData = BuildRubberEmailData(rubbers, fixture);
        var html = emailTemplateService.AdminVerificationEmailForTeamMatch(title, home, away, winnerName, verifyUrl, rubberData);

        await Task.WhenAll(adminEmails.Select(email =>
            SendSafe(email, subject, html, "team match admin verification", isHtml: true)));
    }

    private static IEnumerable<(string Name, string HomePlayers, string AwayPlayers, string Score, bool? HomeWon)>
        BuildRubberEmailData(IEnumerable<Rubber> rubbers, CompetitionFixture fixture)
    {
        foreach (var r in rubbers.OrderBy(x => x.Order))
        {
            var homeParts = new[] { r.HomePlayer1?.DisplayName, r.HomePlayer2?.DisplayName }
                .Where(n => !string.IsNullOrEmpty(n));
            var awayParts = new[] { r.AwayPlayer1?.DisplayName, r.AwayPlayer2?.DisplayName }
                .Where(n => !string.IsNullOrEmpty(n));

            string score;
            bool? homeWon = null;
            if (r.IsComplete && r.HomeSetsWon.HasValue && r.AwaySetsWon.HasValue)
            {
                score = $"{r.HomeSetsWon}–{r.AwaySetsWon} sets";
                homeWon = r.WinnerTeamId == fixture.HomeTeamId ? true
                        : r.WinnerTeamId == fixture.AwayTeamId ? false
                        : (bool?)null;
            }
            else if (r.IsComplete && r.HomeGames.HasValue && r.AwayGames.HasValue)
            {
                score = $"{r.HomeGames}–{r.AwayGames}";
                homeWon = r.WinnerTeamId == fixture.HomeTeamId ? true
                        : r.WinnerTeamId == fixture.AwayTeamId ? false
                        : (bool?)null;
            }
            else
            {
                score = "Not played";
            }

            yield return (
                r.Name,
                homeParts.Any() ? string.Join(" & ", homeParts) : "TBD",
                awayParts.Any() ? string.Join(" & ", awayParts) : "TBD",
                score,
                homeWon
            );
        }
    }

    // ── Shared ───────────────────────────────────────────────────────────────

    public async Task<List<string>> GetAdminEmailsForCompetitionAsync(int competitionId, MyDbContext db)
    {
        return await db.ClubAdministrators
            .Where(ca => db.Competition.Any(c => c.CompetitionID == competitionId && c.HostClubId == ca.ClubId))
            .Join(db.Users, ca => ca.UserId, u => u.Id, (_, u) => u.Email)
            .Where(email => email != null)
            .Select(email => email!)
            .ToListAsync();
    }

    private static string FixtureTitle(CompetitionFixture fixture)
    {
        var name = fixture.Competition?.CompetitionName ?? "Competition";
        return fixture.FixtureLabel != null ? $"{name} — {fixture.FixtureLabel}" : name;
    }

    private static (string side1, string side2) SideNames(CompetitionFixture f)
    {
        if (f.HomeTeam != null || f.AwayTeam != null)
            return (f.HomeTeam?.Name ?? "TBD", f.AwayTeam?.Name ?? "TBD");

        var s1 = new[] { f.Player1?.DisplayName, f.Player3?.DisplayName }
            .Where(n => n != null);
        var s2 = new[] { f.Player2?.DisplayName, f.Player4?.DisplayName }
            .Where(n => n != null);
        return (
            s1.Any() ? string.Join(" & ", s1) : "TBD",
            s2.Any() ? string.Join(" & ", s2) : "TBD"
        );
    }

    private static string? WinnerName(CompetitionFixture f)
    {
        if (f.WinnerTeamId.HasValue)
        {
            if (f.HomeTeam != null && f.WinnerTeamId == f.HomeTeamId) return f.HomeTeam.Name;
            if (f.AwayTeam != null && f.WinnerTeamId == f.AwayTeamId) return f.AwayTeam.Name;
        }
        if (f.WinnerPlayerId.HasValue)
        {
            var (side1, side2) = SideNames(f);
            if (f.WinnerPlayerId == f.Player1Id) return side1;
            if (f.WinnerPlayerId == f.Player2Id) return side2;
        }
        return null;
    }

    private async Task SendSafe(string email, string subject, string body, string context, bool isHtml = false)
    {
        try
        {
            await emailService.SendEmailAsync(email, subject, body, isHtml);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send {Context} email to {Email}", context, email);
        }
    }
}
