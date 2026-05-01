using DropShot.Shared.Dtos;
using DropShot.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DropShot.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class ContactController(IEmailService emailService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ContactMessageDto message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message.Name) ||
            string.IsNullOrWhiteSpace(message.Email) ||
            string.IsNullOrWhiteSpace(message.Subject) ||
            string.IsNullOrWhiteSpace(message.Message))
            return BadRequest();

        var ok = await emailService.SendContactMessageAsync(message, ct);
        return ok ? Ok() : StatusCode(StatusCodes.Status429TooManyRequests);
    }
}
