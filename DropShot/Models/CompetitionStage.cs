namespace DropShot.Models;

public enum StageType : byte
{
    RoundRobin   = 1,
    Knockout     = 2,   // full auto-bracket (generates all rounds automatically)
    Final        = 3,
    QuarterFinal = 4,   // top 8 players — 4 matches
    SemiFinal    = 5,   // top 4 players (or QF winners) — 2 matches
}

public class CompetitionStage
{
    public int CompetitionStageId { get; set; }
    public int CompetitionId { get; set; }
    public string Name { get; set; } = "";
    public int StageOrder { get; set; }
    public StageType StageType { get; set; }

    public Competition Competition { get; set; } = null!;
    public ICollection<CompetitionFixture> Fixtures { get; set; } = [];
}
