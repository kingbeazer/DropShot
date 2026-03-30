using DropShot.Shared;
using DropShot.Shared.Dtos;

namespace DropShot.Maui.Components.Pages;

public class PlayerFormModel
{
    public string DisplayName { get; set; } = "";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public PlayerSex? Sex { get; set; }
    public string? ContactPreferences { get; set; }
    public string? MobileNumber { get; set; }

    public static PlayerFormModel From(PlayerDto dto) => new()
    {
        DisplayName = dto.DisplayName,
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        Email = dto.Email,
        DateOfBirth = dto.DateOfBirth.HasValue
            ? dto.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)
            : null,
        Sex = dto.Sex,
        ContactPreferences = dto.ContactPreferences,
        MobileNumber = dto.MobileNumber
    };
}
