namespace DropShot.Shared;

/// <summary>
/// Mutable scoring state for the live-scoring page. Score-derivation logic
/// lives in <c>TennisScoreService</c>; this is the data backing it. Lifted
/// here from <c>DropShot.Models</c> alongside <see cref="Match"/> so the
/// shared scoring pipeline can target the RCL.
/// </summary>
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
    public int DeuceLimit { get; set; } = 1;
    public SetWinMode SetWinMode { get; set; } = SetWinMode.WinBy2;
    public bool GameScoring { get; set; } = true;
    public bool IsFixedSets { get; set; }
    public int FixedSetCount { get; set; } = 3;
}
