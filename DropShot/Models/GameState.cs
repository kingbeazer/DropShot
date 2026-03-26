namespace DropShot.Models;

public record GameState(
    int UserPts,
    int OppPts,
    int UserG,
    int OppG,
    int UserS,
    int OppS,
    bool TieBreak,
    int DeuceCount,
    bool IsUserServing,
    int UserGamesSnapshot,
    int OpponentGamesSnapshot,
    List<SetScore>? SetScores,
    DateTime Timestamp,
    bool isComplete
);
