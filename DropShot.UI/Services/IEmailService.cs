using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Host-agnostic surface for sending transactional emails from shared Razor pages.
/// Web implementation calls EmailService directly; MAUI implementation posts to /api/contact.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send a contact-form message. Returns false if the caller has been rate-limited;
    /// throws on actual send failures.
    /// </summary>
    Task<bool> SendContactMessageAsync(ContactMessageDto message, CancellationToken ct = default);
}
