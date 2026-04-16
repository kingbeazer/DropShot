using DropShot.Data;
using DropShot.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DropShot.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class UsersController(UserManager<ApplicationUser> userManager) : ControllerBase
{
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
