namespace DropShot.Models;

public class SavedMatch
{
    public int SavedMatchId { get; set; }
    public string MatchJson { get; set; }
    public bool Complete { get; set; }
}
