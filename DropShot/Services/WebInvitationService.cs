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

    public async Task<InvitationViewDto> GetInvitationViewAsync(Guid token, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var invite = await db.PlayerInvitations
            .Include(i => i.LightPlayer)
            .FirstOrDefaultAsync(i => i.Token == token, ct);

        if (invite is null)
            return new InvitationViewDto(InvitationViewStatus.Invalid,
                "This invitation link isn't valid.", null, 0, null);

        if (invite.AcceptedAt is not null)
            return new InvitationViewDto(InvitationViewStatus.AlreadyAccepted,
                "This invitation has already been accepted.", null, 0, null);

        if (invite.LightPlayer is null || !invite.LightPlayer.IsLight)
            return new InvitationViewDto(InvitationViewStatus.Invalid,
                "This invitation is no longer valid — the player record has changed.",
                null, 0, null);

        var lightName = invite.LightPlayer.DisplayName;
        var lightId = invite.LightPlayerId;

        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return new InvitationViewDto(InvitationViewStatus.NeedsAuth,
                null, lightName, lightId, null);

        if (invite.CreatedByUserId == userId)
            return new InvitationViewDto(InvitationViewStatus.SelfInvited,
                "You can't accept an invitation that you sent yourself.",
                lightName, lightId, null);

        var currentPlayer = await db.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsLight, ct);

        if (currentPlayer is null)
            return new InvitationViewDto(InvitationViewStatus.Invalid,
                "Your account isn't linked to a player record. Please contact support.",
                lightName, lightId, null);

        if (currentPlayer.PlayerId == lightId)
            return new InvitationViewDto(InvitationViewStatus.Invalid,
                "You can't accept an invitation for your own player record.",
                lightName, lightId, null);

        return new InvitationViewDto(InvitationViewStatus.Confirm,
            null, lightName, lightId, currentPlayer.DisplayName);
    }

    public async Task<AcceptInvitationResultDto> AcceptInvitationAsync(Guid token, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return new AcceptInvitationResultDto(false, "You must be signed in to accept an invitation.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var invite = await db.PlayerInvitations
            .FirstOrDefaultAsync(i => i.Token == token && i.AcceptedAt == null, ct);
        if (invite is null)
            return new AcceptInvitationResultDto(false, "Invitation is no longer valid.");

        if (invite.CreatedByUserId == userId)
            return new AcceptInvitationResultDto(false, "You can't accept an invitation that you sent yourself.");

        var lightPlayer = await db.Players.FindAsync([invite.LightPlayerId], ct);
        if (lightPlayer is not { IsLight: true })
            return new AcceptInvitationResultDto(false, "The invited player record is no longer available.");

        var currentPlayer = await db.Players
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsLight, ct);
        if (currentPlayer is null)
            return new AcceptInvitationResultDto(false, "Your account isn't linked to a player record.");

        var lightId = lightPlayer.PlayerId;
        var verifiedId = currentPlayer.PlayerId;

        var affected = await db.SavedMatch
            .Where(m => m.Player1Id == lightId || m.Player2Id == lightId
                     || m.Player3Id == lightId || m.Player4Id == lightId
                     || m.WinnerPlayerId == lightId)
            .ToListAsync(ct);
        foreach (var m in affected)
        {
            if (m.Player1Id == lightId) m.Player1Id = verifiedId;
            if (m.Player2Id == lightId) m.Player2Id = verifiedId;
            if (m.Player3Id == lightId) m.Player3Id = verifiedId;
            if (m.Player4Id == lightId) m.Player4Id = verifiedId;
            if (m.WinnerPlayerId == lightId) m.WinnerPlayerId = verifiedId;
        }

        invite.AcceptedAt = DateTime.UtcNow;
        invite.AcceptedByUserId = userId;

        var inviterAlreadyBookmarked = await db.UserPlayers.AnyAsync(up =>
            up.UserId == invite.CreatedByUserId && up.PlayerId == verifiedId, ct);
        if (!inviterAlreadyBookmarked)
        {
            db.UserPlayers.Add(new UserPlayer
            {
                UserId = invite.CreatedByUserId,
                PlayerId = verifiedId
            });
        }

        db.Players.Remove(lightPlayer);
        await db.SaveChangesAsync(ct);

        return new AcceptInvitationResultDto(true, null);
    }
}
