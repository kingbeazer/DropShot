namespace DropShot.Models;

public class RoleSwitchLog
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string FromRole { get; set; } = null!;
    public string ToRole { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
}
