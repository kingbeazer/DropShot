using DropShot.Data;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DropShot.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class UsersController(
    UserManager<ApplicationUser> userManager,
    IPlayerService playerService,
    IUserService userService) : ControllerBase
{
    /// <summary>
    /// Minimal user list for the SuperAdmin Players "Link account" dropdown.
    /// SuperAdmin only.
    /// </summary>
    [HttpGet("for-linking")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<List<ApplicationUserDto>>> GetForLinking(CancellationToken ct)
    {
        return await playerService.GetUsersForLinkingAsync(ct);
    }

    /// <summary>
    /// Full user roster with role flags and club-admin assignments. Backs
    /// Admin/UserManagement (phase 5).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<List<UserManagementRowDto>>> GetAll(CancellationToken ct)
    {
        return await userService.GetAllAsync(ct);
    }

    /// <summary>Toggle a role on a user. SuperAdmin only.</summary>
    [HttpPut("{id}/role")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> SetRole(string id, [FromBody] SetUserRoleRequest req, CancellationToken ct)
    {
        try { await userService.SetRoleAsync(id, req.Role, req.Granted, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Toggle premium subscription. SuperAdmin only.</summary>
    [HttpPut("{id}/premium")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> SetPremium(string id, [FromBody] SetUserPremiumRequest req, CancellationToken ct)
    {
        try { await userService.SetPremiumAsync(id, req.IsSubscribed, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Edit username + email. Admin/SuperAdmin can edit anyone, but only a
    /// SuperAdmin can edit another Admin or SuperAdmin.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        try { await userService.UpdateAsync(id, req, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Delete a user. Same admin-tier rule as Update; in addition the caller
    /// can never delete themselves.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var currentUserId = userManager.GetUserId(User);
        if (currentUserId == id) return BadRequest(new { message = "You cannot delete yourself." });
        if (!await CanModifyAsync(id)) return Forbid();
        try { await userService.DeleteAsync(id, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id}/club-admins")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> AddClubAdmin(string id, [FromBody] AddClubAdminRequest req, CancellationToken ct)
    {
        try { await userService.AddClubAdminAsync(id, req.ClubId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id}/club-admins/{clubId:int}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> RemoveClubAdmin(string id, int clubId, CancellationToken ct)
    {
        try { await userService.RemoveClubAdminAsync(id, clubId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Existing premium-toggle convenience endpoint — kept for backwards compat.</summary>
    [HttpPost("{id}/upgrade-premium")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> UpgradePremium(string id, [FromBody] UpgradePremiumRequest req)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        user.IsSubscribed = req.IsSubscribed;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return NoContent();
    }

    /// <summary>
    /// Admin and SuperAdmin can edit/delete; only SuperAdmin can edit/delete
    /// another Admin or SuperAdmin. Returns false if the target is admin-tier
    /// and the caller is not SuperAdmin.
    /// </summary>
    private async Task<bool> CanModifyAsync(string targetUserId)
    {
        var caller = await userManager.GetUserAsync(User);
        var isCallerSuperAdmin = caller is not null && await userManager.IsInRoleAsync(caller, "SuperAdmin");
        if (isCallerSuperAdmin) return true;

        var target = await userManager.FindByIdAsync(targetUserId);
        if (target is null) return true; // 404 will be returned by the action.
        var targetIsAdmin = await userManager.IsInRoleAsync(target, "Admin")
            || await userManager.IsInRoleAsync(target, "SuperAdmin");
        return !targetIsAdmin;
    }
}
