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
            .Replace("{PlayerName}", playerName)
            .Replace("{CompetitionName}", competitionName)
            .Replace("{MatchLink}", matchLink);

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
}
