using DropShot.Models;

namespace DropShot.Services;

public class TennisScoreService
{
    public void CheckGame(TennisMatchState s)
    {
        if (s.IsTieBreak)
        {
            if ((s.UserPoints >= 7 || s.OppPoints >= 7) && Math.Abs(s.UserPoints - s.OppPoints) >= 2)
            {
                if (s.UserPoints > s.OppPoints)
                {
                    s.UserGames++;
                    s.UserSets++;
                }
                else
                {
                    s.OppSets++;
                    s.OppGames++;
                }
                EndSet(s);
            }

            var dsd = (s.UserPoints + s.OppPoints) % 2;
            if (dsd == 1)
                s.IsUserServing = !s.IsUserServing;
            return;
        }

        if (s.GameScoring)
        {
            if (s.UserPoints > 0)
                s.UserGames++;
            else
                s.OppGames++;
            ResetGame(s);
            CheckSet(s);
            return;
        }

        if (s.UserPoints >= 4 || s.OppPoints >= 4)
        {
            int diff = s.UserPoints - s.OppPoints;

            if (s.UnlimitedDeuce)
            {
                if (diff >= 2)
                {
                    s.UserGames++;
                    ResetGame(s);
                    CheckSet(s);
                }
                else if (diff <= -2)
                {
                    s.OppGames++;
                    ResetGame(s);
                    CheckSet(s);
                }
            }
            else
            {
                if (diff == 2 || diff == -2 || (diff == 1 && s.OppPoints > 3) || (diff == -1 && s.UserPoints > 3))
                {
                    if (diff > 0)
                        s.UserGames++;
                    else
                        s.OppGames++;
                    ResetGame(s);
                    CheckSet(s);
                }
            }
        }

        if (s.UserPoints >= 3 && s.OppPoints >= 3 && s.UserPoints == s.OppPoints)
            s.DeuceCount++;
    }

    public void CheckSet(TennisMatchState s)
    {
        if (s.UserGames == s.GamesFirstTo && s.OppGames == s.GamesFirstTo)
        {
            s.IsTieBreak = true;
            s.UserPoints = s.OppPoints = 0;
            return;
        }

        if ((s.UserGames >= s.GamesFirstTo || s.OppGames >= s.GamesFirstTo) && Math.Abs(s.UserGames - s.OppGames) >= 2)
        {
            if (s.UserGames > s.OppGames)
                s.UserSets++;
            else
                s.OppSets++;

            EndSet(s);
        }
    }

    public void EndSet(TennisMatchState s)
    {
        s.SetScores.Add(new SetScore
        {
            SetNumber = s.SetScores.Count + 1,
            UserGames = s.UserGames,
            OpponentGames = s.OppGames
        });
        s.UserGames = s.OppGames = 0;
        s.UserPoints = s.OppPoints = 0;
        s.IsTieBreak = false;

        if (s.UserSets == GetMaxSetsToWin(s.BestOf) || s.OppSets == GetMaxSetsToWin(s.BestOf))
            s.IsMatchEnded = true;
    }

    public string? EndMatch(TennisMatchState s, Match match, bool isDoubles)
    {
        match.Complete = true;

        string winner;
        if (s.UserSets > s.OppSets)
        {
            winner = match.Player1 ?? "";
            if (isDoubles)
                winner += " & " + match.Player2;
        }
        else
        {
            winner = isDoubles
                ? (match.Player3 + " & " + match.Player4)
                : (match.Player2 ?? "");
        }

        return $"Congratulations {winner}";
    }

    public void ResetGame(TennisMatchState s)
    {
        s.UserPoints = 0;
        s.OppPoints = 0;
        s.IsUserServing = !s.IsUserServing;
        s.DeuceCount = 0;
    }

    public void ResetMatch(TennisMatchState s)
    {
        s.UserPoints = s.OppPoints = 0;
        s.UserGames = s.OppGames = 0;
        s.UserSets = s.OppSets = 0;
        s.IsTieBreak = false;
        s.IsMatchEnded = false;
        s.DeuceCount = 0;
        s.SetScores.Clear();
    }

    public void RestoreFromGameState(TennisMatchState s, GameState gs)
    {
        s.UserPoints = gs.UserPts;
        s.OppPoints = gs.OppPts;
        s.UserGames = gs.UserG;
        s.OppGames = gs.OppG;
        s.UserSets = gs.UserS;
        s.OppSets = gs.OppS;
        s.IsTieBreak = gs.TieBreak;
        s.DeuceCount = gs.DeuceCount;
        s.IsUserServing = gs.IsUserServing;
        s.IsMatchEnded = gs.isComplete;
        s.SetScores = gs.SetScores != null
            ? gs.SetScores.Select(sc => sc.Clone()).ToList()
            : [];
    }

    public string GetScoreDisplayUser(TennisMatchState s)
    {
        if (s.IsTieBreak)
            return $"{s.UserPoints}";

        if (s.UserPoints >= 3 && s.OppPoints >= 3)
        {
            if (s.UserPoints == s.OppPoints)
                return "40";
            if (s.UserPoints == s.OppPoints + 1) return $"A({s.DeuceCount})";
            if (s.OppPoints == s.UserPoints + 1) return "";
        }
        return ConvertToTennisScore(s.UserPoints);
    }

    public string GetScoreDisplayOpp(TennisMatchState s)
    {
        if (s.IsTieBreak)
            return $"{s.OppPoints}";

        if (s.UserPoints >= 3 && s.OppPoints >= 3)
        {
            if (s.UserPoints == s.OppPoints)
                return "40";
            if (s.OppPoints == s.UserPoints + 1) return $"A({s.DeuceCount})";
            if (s.UserPoints == s.OppPoints + 1) return "";
        }
        return ConvertToTennisScore(s.OppPoints);
    }

    public static string ConvertToTennisScore(int points) => points switch
    {
        0 => "0",
        1 => "15",
        2 => "30",
        3 => "40",
        _ => "40"
    };

    public static int GetMaxSetsToWin(int bestOf) => bestOf switch
    {
        3 => 2,
        5 => 3,
        7 => 4,
        _ => 0
    };
}
