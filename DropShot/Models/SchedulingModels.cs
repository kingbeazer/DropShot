namespace DropShot.Models;

public class CompetitionMatchWindow
{
    public int CompetitionMatchWindowId { get; set; }
    public int CompetitionId { get; set; }
    public int? CourtId { get; set; }

    /// <summary>
    /// When set, this window only applies to the named division. When null,
    /// the window is "shared" and applies to every division (plus any
    /// non-divisioned fixtures).
    /// </summary>
    public int? CompetitionDivisionId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public Competition Competition { get; set; } = null!;
    public Court? Court { get; set; }
    public CompetitionDivision? Division { get; set; }
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

public class CompetitionTemplate
{
    public int CompetitionTemplateId { get; set; }
    public int ClubId { get; set; }
    public string Name { get; set; } = "";
    public CompetitionFormat? Format { get; set; }
    public int? RulesSetId { get; set; }
    public int? BestOf { get; set; }
    public int? MaxAge { get; set; }
    public PlayerSex? EligibleSex { get; set; }

    public Club Club { get; set; } = null!;
    public ICollection<CompetitionTemplateWindow> Windows { get; set; } = [];
}

public class CompetitionTemplateWindow
{
    public int CompetitionTemplateWindowId { get; set; }
    public int CompetitionTemplateId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public CompetitionTemplate Template { get; set; } = null!;
}
