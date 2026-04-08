namespace DropShot.Models;

public class RulesSet
{
    public int RulesSetId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;

    public ICollection<RulesSetItem> Items { get; set; } = [];
    public ICollection<Competition> Competitions { get; set; } = [];
    public ICollection<ClubLadder> Ladders { get; set; } = [];
}

public class RulesSetItem
{
    public int RulesSetItemId { get; set; }
    public int RulesSetId { get; set; }
    public int SortOrder { get; set; }
    public string RuleText { get; set; } = "";

    public RulesSet RulesSet { get; set; } = null!;
}
