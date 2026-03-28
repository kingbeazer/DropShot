namespace DropShot.Models;

public class ClubLadder
{
    public int ClubLadderId { get; set; }
    public int ClubId { get; set; }
    public string Name { get; set; } = "";
    public int? RulesSetId { get; set; }
    public bool IsActive { get; set; } = true;
    public double KFactor { get; set; } = 32.0;
    public int InactivityDeductionDays { get; set; } = 30;
    public int InactivityDeductionPoints { get; set; } = 10;

    public Club Club { get; set; } = null!;
    public RulesSet? Rules { get; set; }
    public ICollection<LadderEntry> Entries { get; set; } = [];
}

public class LadderEntry
{
    public int LadderEntryId { get; set; }
    public int ClubLadderId { get; set; }
    public int PlayerId { get; set; }
    public double EloRating { get; set; } = 1000.0;
    public int Rank { get; set; }
    public DateTime LastMatchAt { get; set; } = DateTime.UtcNow;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public ClubLadder Ladder { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
