namespace DropShot.Models;

public class TennisMatchState
{
    // Scores
    public int UserPoints { get; set; }
    public int OppPoints { get; set; }
    public int UserGames { get; set; }
    public int OppGames { get; set; }
    public int UserSets { get; set; }
    public int OppSets { get; set; }
    public int DeuceCount { get; set; }
    public bool IsTieBreak { get; set; }
    public bool IsUserServing { get; set; } = true;
    public bool IsMatchEnded { get; set; }
    public List<SetScore> SetScores { get; set; } = [];

    // Per-match config (set before Start Match)
    public int BestOf { get; set; } = 3;
    public int GamesFirstTo { get; set; } = 6;
    public bool UnlimitedDeuce { get; set; } = true;
    public bool GameScoring { get; set; } = true;
}
