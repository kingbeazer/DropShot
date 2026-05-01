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
    IPlayerService playerService) : ControllerBase
{
    /// <summary>
    /// Minimal user list for the SuperAdmin Players "Link account" dropdown.
    /// SuperAdmin only — exposing the full user roster anywhere broader needs
    /// a separate decision (and likely the full Admin/UserManagement migration).
    /// </summary>
    [HttpGet("for-linking")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<List<ApplicationUserDto>>> GetForLinking(CancellationToken ct)
    {
        return await playerService.GetUsersForLinkingAsync(ct);
    }

    /// <summary>
    /// Toggles a user's premium (subscribed) status. Super-admin only — mirrors
    /// the SuperAdmin-only role switches in User Management.
    /// </summary>
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
}
