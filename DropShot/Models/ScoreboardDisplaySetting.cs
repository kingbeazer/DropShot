namespace DropShot.Models;

public class ScoreboardDisplaySetting
{
    public int Id { get; set; }
    public int CourtId { get; set; }
    public Court Court { get; set; } = null!;
    public string? LiveStreamUrl { get; set; }
    public bool ShowLiveStream { get; set; }
    public string Layout { get; set; } = "default";
    public bool Fullscreen { get; set; }
}
