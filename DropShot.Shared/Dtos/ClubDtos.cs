namespace DropShot.Shared.Dtos;

public record ClubDto(
    int ClubId,
    string Name,
    string? AddressLine1,
    string? AddressLine2,
    string? Town,
    string? Postcode,
    string? Phone,
    string? Email,
    string? Website,
    int CourtCount);

public record ClubDetailDto(
    int ClubId,
    string Name,
    string? AddressLine1,
    string? AddressLine2,
    string? Town,
    string? Postcode,
    string? Phone,
    string? Email,
    string? Website,
    List<CourtDto> Courts);

public record CourtDto(
    int CourtId,
    int ClubId,
    string Name,
    CourtSurface Surface,
    bool IsIndoor);

public record SaveClubRequest(
    string Name,
    string? AddressLine1,
    string? AddressLine2,
    string? Town,
    string? Postcode,
    string? Phone,
    string? Email,
    string? Website);

public record AddCourtRequest(string Name, CourtSurface Surface, bool IsIndoor);
public record UpdateCourtRequest(string Name, CourtSurface Surface, bool IsIndoor);
