namespace DropShot.Models;

public class CourtPair
{
    public int CourtPairId { get; set; }
    public int CompetitionId { get; set; }
    public int Court1Id { get; set; }
    public int Court2Id { get; set; }
    public string Name { get; set; } = "";

    public Competition Competition { get; set; } = null!;
    public Court Court1 { get; set; } = null!;
    public Court Court2 { get; set; } = null!;
}
