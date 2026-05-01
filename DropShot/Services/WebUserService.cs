using DropShot.Data;
using DropShot.Models;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Web implementation of <see cref="IUserService"/>. Wraps
/// <c>UserManager&lt;ApplicationUser&gt;</c> and the <c>ClubAdministrators</c>
/// table behind a shared interface so MAUI's Admin/UserManagement can talk to
/// the same surface via API. Role-toggle / premium-toggle authorisation is
/// enforced at the controller (SuperAdmin only); CRUD ops require Admin or
/// SuperAdmin and additionally protect against deleting Admins/SuperAdmins
/// without SuperAdmin rights — that check lives in the controller too.
/// </summary>
public sealed class WebUserService(
    UserManager<ApplicationUser> userManager,
    IDbContextFactory<MyDbContext> dbFactory) : IUserService
{
    public async Task<List<UserManagementRowDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var allAssignments = await db.ClubAdministrators
            .Include(ca => ca.Club)
            .ToListAsync(ct);
        var assignmentsByUser = allAssignments
            .GroupBy(ca => ca.UserId)
            .ToDictionary(g => g.Key, g => g
                .Select(ca => new ClubAdminAssignmentDto(ca.ClubId, ca.Club.Name))
                .ToList());

        var users = await userManager.Users.OrderBy(u => u.UserName).ToListAsync(ct);

        var rows = new List<UserManagementRowDto>(users.Count);
        foreach (var user in users)
        {
            var isSuperAdmin = await userManager.IsInRoleAsync(user, "SuperAdmin");
            var isAdmin = await userManager.IsInRoleAsync(user, "Admin");
            assignmentsByUser.TryGetValue(user.Id, out var clubs);
            rows.Add(new UserManagementRowDto(
                user.Id, user.UserName, user.Email, user.DisplayName,
                isSuperAdmin, isAdmin, user.IsSubscribed,
                clubs ?? new List<ClubAdminAssignmentDto>()));
        }
        return rows;
    }

    public async Task SetRoleAsync(string userId, string role, bool granted, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        IdentityResult result = granted
            ? await userManager.AddToRoleAsync(user, role)
            : await userManager.RemoveFromRoleAsync(user, role);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public async Task SetPremiumAsync(string userId, bool isSubscribed, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");
        user.IsSubscribed = isSubscribed;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public async Task UpdateAsync(string userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        var errors = new List<string>();
        if (user.UserName != request.UserName)
        {
            var r = await userManager.SetUserNameAsync(user, request.UserName);
            if (!r.Succeeded) errors.AddRange(r.Errors.Select(e => e.Description));
        }
        if (user.Email != request.Email)
        {
            var r = await userManager.SetEmailAsync(user, request.Email);
            if (!r.Succeeded) errors.AddRange(r.Errors.Select(e => e.Description));
        }
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));
    }

    public async Task DeleteAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var player = await db.Players.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
            if (player is not null)
            {
                player.UserId = null;
                await db.SaveChangesAsync(ct);
            }
        }

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public async Task AddClubAdminAsync(string userId, int clubId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var exists = await db.ClubAdministrators
            .AnyAsync(ca => ca.UserId == userId && ca.ClubId == clubId, ct);
        if (exists) return;

        db.ClubAdministrators.Add(new ClubAdministrator
        {
            UserId = userId,
            ClubId = clubId,
            AssignedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        if (!await userManager.IsInRoleAsync(user, "ClubAdmin"))
            await userManager.AddToRoleAsync(user, "ClubAdmin");
    }

    public async Task RemoveClubAdminAsync(string userId, int clubId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var record = await db.ClubAdministrators.FindAsync([userId, clubId], ct);
        if (record is null) return;
        db.ClubAdministrators.Remove(record);
        await db.SaveChangesAsync(ct);

        var remaining = await db.ClubAdministrators.AnyAsync(ca => ca.UserId == userId, ct);
        if (!remaining)
            await userManager.RemoveFromRoleAsync(user, "ClubAdmin");
    }
}
