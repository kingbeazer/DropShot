using DropShot.Shared;

namespace DropShot.Models;

public class CompetitionParticipant
{
    public int CompetitionId { get; set; }
    public int PlayerId { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public ParticipantStatus Status { get; set; } = ParticipantStatus.Registered;
    public int? TeamId { get; set; }
    public int? CompetitionDivisionId { get; set; }
    public string? Role { get; set; }

    // ── Singles Elo Ladder per-player state ─────────────────────────────────
    // Live rating maintained by LadderRatingService on fixture finalisation.
    // Only meaningful when Competition.CompetitionFormat == SinglesLadder;
    // defaults are inert for other formats.
    public double EloRating { get; set; } = 1000.0;
    public int MatchesPlayed { get; set; } = 0;
    public bool IsProvisional { get; set; } = true;
    public DateTime? LastMatchAt { get; set; }

    public Competition Competition { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public CompetitionTeam? Team { get; set; }
    public CompetitionDivision? Division { get; set; }
}
