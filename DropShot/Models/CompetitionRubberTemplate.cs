namespace DropShot.Models;

public class CompetitionRubberTemplate
{
    public int CompetitionRubberTemplateId { get; set; }
    public int CompetitionId { get; set; }

    public Competition Competition { get; set; } = null!;
    public ICollection<RubberTemplateRubber> Rubbers { get; set; } = [];
}
