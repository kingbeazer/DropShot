namespace DropShot.Shared.Dtos;

/// <summary>
/// Indicates how the current user relates to a club in the public clubs directory.
/// </summary>
public enum ClubLinkStatus
{
    /// <summary>Caller has no link to the club and no pending request.</summary>
    None = 0,
    /// <summary>Caller has submitted a link request that has not yet been approved or rejected.</summary>
    Pending = 1,
    /// <summary>Caller's player is in the club's membership list.</summary>
    Linked = 2,
    /// <summary>Caller is an administrator of the club.</summary>
    Administered = 3
}

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

public record ClubWithLinkStatusDto(
    int ClubId,
    string Name,
    string? AddressLine1,
    string? AddressLine2,
    string? Town,
    string? Postcode,
    string? Phone,
    string? Email,
    string? Website,
    int CourtCount,
    ClubLinkStatus LinkStatus);

public record ClubLinkRequestDto(
    int ClubLinkRequestId,
    int ClubId,
    string ClubName,
    string UserId,
    string UserName,
    string? UserEmail,
    string Status,
    DateTime RequestedAt,
    DateTime? ResolvedAt);

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
