namespace DropShot.Shared.Dtos;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    string UserName,
    string Email,
    List<string> Roles,
    List<int> AdminClubIds);

public record UserInfoDto(
    string UserId,
    string UserName,
    string Email,
    List<string> Roles,
    List<int> AdminClubIds);
