namespace DropShot.Models;

public enum QrSessionStatus
{
    Pending,
    Authenticated,
    Expired
}

public class QrLoginSession
{
    public required string Token { get; set; }
    public QrSessionStatus Status { get; set; } = QrSessionStatus.Pending;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public int? CourtId { get; set; }
    public List<string> Roles { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsExpired => DateTime.UtcNow - CreatedAt > TimeSpan.FromMinutes(5);
}
