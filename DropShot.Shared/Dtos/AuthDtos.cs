namespace DropShot.Shared.Dtos;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    string UserName,
    string Email,
    List<string> Roles,
    List<int> AdminClubIds,
    string ActiveRole,
    List<string> GrantedRoles);

public record UserInfoDto(
    string UserId,
    string UserName,
    string Email,
    List<string> Roles,
    List<int> AdminClubIds,
    string ActiveRole,
    List<string> GrantedRoles);

public record SwitchRoleRequest(string Role);

public record SwitchRoleResponse(
    string AccessToken,
    string ActiveRole,
    List<string> GrantedRoles);
