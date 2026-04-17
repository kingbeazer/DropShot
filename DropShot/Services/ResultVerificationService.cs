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
        var subject = $"Match result: {title}";
        var html = emailTemplateService.MatchResultEmail(title, resultSummary);

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
        var verifyUrl = $"{BaseUrl}/verify-result/{fixture.VerificationToken}";
        var subject = $"Result verification required: {title}";
        var html = emailTemplateService.AdminVerificationEmail(title, fixture.ResultSummary ?? "", verifyUrl);

        await Task.WhenAll(adminEmails.Select(email => SendSafe(email, subject, html, "admin verification", isHtml: true)));
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
