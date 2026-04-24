using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DropShot.Models;

public enum RubberStatus : byte
{
    NotStarted = 1,
    InProgress = 2,
    Complete = 3,
    Walkover = 4
}

public class Rubber
{
    public int RubberId { get; set; }
    public int CompetitionFixtureId { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = "";
    public int CourtNumber { get; set; }

    public string HomeRolesJson { get; set; } = "[]";
    public string AwayRolesJson { get; set; } = "[]";

    /// <summary>
    /// JSON array of per-set scores, e.g. [[6,4],[4,6],[7,5]]. Populated at
    /// rubber-submit time so the UI can show individual set scores alongside
    /// the set-count summary.
    /// </summary>
    public string? SetScoresJson { get; set; }

    public int? HomePlayer1Id { get; set; }
    public int? HomePlayer2Id { get; set; }
    public int? AwayPlayer1Id { get; set; }
    public int? AwayPlayer2Id { get; set; }

    public int? HomeGames { get; set; }
    public int? AwayGames { get; set; }
    public int? HomeSetsWon { get; set; }
    public int? AwaySetsWon { get; set; }
    public int? HomeGamesTotal { get; set; }
    public int? AwayGamesTotal { get; set; }
    public int? WinnerTeamId { get; set; }
    public bool IsComplete { get; set; }
    public int? SavedMatchId { get; set; }

    public CompetitionFixture Fixture { get; set; } = null!;
    public Player? HomePlayer1 { get; set; }
    public Player? HomePlayer2 { get; set; }
    public Player? AwayPlayer1 { get; set; }
    public Player? AwayPlayer2 { get; set; }
    public CompetitionTeam? WinnerTeam { get; set; }
    public SavedMatch? SavedMatch { get; set; }

    [NotMapped]
    public IReadOnlyList<string> HomeRoles
    {
        get => JsonSerializer.Deserialize<List<string>>(HomeRolesJson) ?? [];
        set => HomeRolesJson = JsonSerializer.Serialize(value);
    }

    [NotMapped]
    public IReadOnlyList<string> AwayRoles
    {
        get => JsonSerializer.Deserialize<List<string>>(AwayRolesJson) ?? [];
        set => AwayRolesJson = JsonSerializer.Serialize(value);
    }

    [NotMapped]
    public IReadOnlyList<(int Home, int Away)> SetScores
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SetScoresJson)) return [];
            try
            {
                var raw = JsonSerializer.Deserialize<List<int[]>>(SetScoresJson);
                return raw?.Where(p => p.Length == 2).Select(p => (p[0], p[1])).ToList() ?? [];
            }
            catch
            {
                return [];
            }
        }
    }
}
