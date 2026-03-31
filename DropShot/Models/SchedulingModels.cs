namespace DropShot.Models;

public class CompetitionMatchWindow
{
    public int CompetitionMatchWindowId { get; set; }
    public int CompetitionId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public Competition Competition { get; set; } = null!;
}

public class ClubSchedulingTemplate
{
    public int ClubSchedulingTemplateId { get; set; }
    public int ClubId { get; set; }
    public string Name { get; set; } = "";

    public Club Club { get; set; } = null!;
    public ICollection<ClubSchedulingTemplateWindow> Windows { get; set; } = [];
}

public class ClubSchedulingTemplateWindow
{
    public int ClubSchedulingTemplateWindowId { get; set; }
    public int ClubSchedulingTemplateId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public ClubSchedulingTemplate Template { get; set; } = null!;
}
