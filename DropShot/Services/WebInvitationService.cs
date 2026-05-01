using DropShot.Data;
using DropShot.Models;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using DropShot.UI.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IInvitationService"/>. Builds the
/// fully-qualified invite URL from <c>App:BaseUrl</c> so MAUI receives a URL
/// pointing to the production web host (not the MAUI app's null base).
/// </summary>
public sealed class WebInvitationService(
    IDbContextFactory<MyDbContext> dbFactory,
    ICurrentUser currentUser,
    EmailService emailService,
    EmailTemplateService emailTemplates,
    IConfiguration config) : IInvitationService
{
    public async Task<LightPlayerInvitationDto> CreateOrReuseLightPlayerInvitationAsync(
        int lightPlayerId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Not authenticated.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var existing = await db.PlayerInvitations
            .Where(i => i.LightPlayerId == lightPlayerId
                && i.CreatedByUserId == userId
                && i.AcceptedAt == null)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(ct);

        Guid token;
        if (existing is not null)
        {
            token = existing.Token;
        }
        else
        {
            var owns = await db.Players.AnyAsync(p =>
                p.PlayerId == lightPlayerId && p.IsLight && p.CreatedByUserId == userId, ct);
            if (!owns)
                throw new InvalidOperationException("Only your own light players can be invited.");

            var invite = new PlayerInvitation
            {
                Token = Guid.NewGuid(),
                LightPlayerId = lightPlayerId,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            db.PlayerInvitations.Add(invite);
            await db.SaveChangesAsync(ct);
            token = invite.Token;
        }

        var baseUrl = config["App:BaseUrl"]?.TrimEnd('/') ?? "";
        var url = $"{baseUrl}/invite/{token}";
        return new LightPlayerInvitationDto(token, url);
    }

    public async Task SendInvitationEmailAsync(Guid token, string email, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Not authenticated.");

        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            throw new InvalidOperationException("Invalid email address.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var invite = await db.PlayerInvitations
            .Include(i => i.LightPlayer)
            .FirstOrDefaultAsync(i => i.Token == token, ct)
            ?? throw new KeyNotFoundException("Invitation not found.");

        if (invite.CreatedByUserId != userId)
            throw new InvalidOperationException("You can only send your own invitations.");

        invite.SentToEmail = email.Trim();
        await db.SaveChangesAsync(ct);

        var baseUrl = config["App:BaseUrl"]?.TrimEnd('/') ?? "";
        var inviteLink = $"{baseUrl}/invite/{token}";
        var displayName = invite.LightPlayer?.DisplayName ?? "this player";
        var subject = $"You've been invited to join DropShot as \"{displayName}\"";
        var html = emailTemplates.PlayerInvitationEmail(displayName, inviteLink);

        await emailService.SendEmailAsync(email.Trim(), subject, html, isHtml: true);
    }
}
