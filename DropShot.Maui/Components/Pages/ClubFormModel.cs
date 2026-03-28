using DropShot.Shared.Dtos;

namespace DropShot.Maui.Components.Pages;

public class ClubFormModel
{
    public string Name { get; set; } = "";
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Town { get; set; }
    public string? Postcode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    public static ClubFormModel From(ClubDto dto) => new()
    {
        Name = dto.Name,
        AddressLine1 = dto.AddressLine1,
        AddressLine2 = dto.AddressLine2,
        Town = dto.Town,
        Postcode = dto.Postcode,
        Phone = dto.Phone,
        Email = dto.Email,
        Website = dto.Website
    };
}
