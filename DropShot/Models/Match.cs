namespace DropShot.Models;

public class Match
{
    public Stack<GameState> History { get; set; } = new Stack<GameState>();
    public List<GameState> HistoryList { get; set; } = new List<GameState>();
    public string Player1 { get; set; } = string.Empty;
    public string Player2 { get; set; } = string.Empty;
    public string Player3 { get; set; } = string.Empty;
    public string Player4 { get; set; } = string.Empty;
    public bool Complete { get; set; }
    public int? CourtId { get; set; }
}
