using DropShot.Shared;

namespace DropShot.Models;

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
