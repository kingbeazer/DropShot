using DropShot.Data;
using DropShot.Models;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

public class ResultVerificationService(EmailService emailService, IConfiguration config, ILogger<ResultVerificationService> logger)
{
    private string BaseUrl => config["App:BaseUrl"]?.TrimEnd('/') ?? "";

    public async Task SendResultNotificationAsync(CompetitionFixture fixture)
    {
        var (subject, body) = ResultEmailContent(fixture);

        var emails = new[] { fixture.Player1, fixture.Player2, fixture.Player3, fixture.Player4 }
            .Where(p => p?.Email != null)
            .Select(p => p!.Email!)
            .Distinct()
            .Select(email => SendSafe(email, subject, body, "result notification"));

        await Task.WhenAll(emails);
    }

    public async Task SendAdminVerificationEmailsAsync(CompetitionFixture fixture, IEnumerable<string> adminEmails)
    {
        if (fixture.VerificationToken == null) return;

        var title = FixtureTitle(fixture);
        var verifyUrl = $"{BaseUrl}/verify-result/{fixture.VerificationToken}";
        var subject = $"Result verification required: {title}";
        var body = $"A result has been submitted for {title}.\n\nResult: {fixture.ResultSummary}\n\nClick the link below to verify this result:\n{verifyUrl}";

        await Task.WhenAll(adminEmails.Select(email => SendSafe(email, subject, body, "admin verification")));
    }

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

    private static (string subject, string body) ResultEmailContent(CompetitionFixture fixture)
    {
        var title = FixtureTitle(fixture);
        return (
            $"Match result: {title}",
            $"The result for your match in {title} has been recorded: {fixture.ResultSummary}."
        );
    }

    private async Task SendSafe(string email, string subject, string body, string context)
    {
        try
        {
            await emailService.SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send {Context} email to {Email}", context, email);
        }
    }
}
