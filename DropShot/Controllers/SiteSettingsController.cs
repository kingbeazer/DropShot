using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropShot.Controllers;

/// <summary>
/// Site-wide settings tunable by Admin / SuperAdmin. Backs Admin/SiteSettings
/// (phase 5). Reads are <see cref="AllowAnonymous"/> because the only setting
/// today (content max width) drives the host layout for every visitor.
/// </summary>
[ApiController]
[Route("api/site-settings")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class SiteSettingsController(ISiteSettingsService settings) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<SiteSettingsDto>> Get(CancellationToken ct)
    {
        return await settings.GetSettingsAsync(ct);
    }

    [HttpPut]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Update(
        [FromBody] UpdateContentMaxWidthRequest req, CancellationToken ct)
    {
        try
        {
            await settings.SetContentMaxWidthPxAsync(req.ContentMaxWidthPx, ct);
            return NoContent();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
