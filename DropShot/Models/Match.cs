namespace DropShot.Models;

public class Match
{
    public Stack<GameState> History { get; set; } = new Stack<GameState>();
    public List<GameState> HistoryList { get; set; } = new List<GameState>();
    public string Player1 { get; set; }
    public string Player2 { get; set; }
    public string Player3 { get; set; }
    public string Player4 { get; set; }
    public bool Complete { get; set; }
}
