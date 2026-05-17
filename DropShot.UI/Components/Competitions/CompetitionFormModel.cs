using System.ComponentModel.DataAnnotations;
using DropShot.Shared;

namespace DropShot.UI.Components.Competitions;

public sealed class CompetitionFormModel
{
    [Required(ErrorMessage = "Competition name is required!")]
    [StringLength(200, ErrorMessage = "Name must be 200 characters or fewer.")]
    public string CompetitionName { get; set; } = "";
    public CompetitionCategoryUi? Category { get; set; }
    public CompetitionFormatUi? Format { get; set; }
    public int? MaxParticipants { get; set; }
    public DateTime? StartDateDt { get; set; }
    public DateTime? EndDateDt { get; set; }
    public DateTime? RegisterByDateDt { get; set; }
    public int? MaxAge { get; set; }
    public int? MinAge { get; set; }
    public int? HostClubId { get; set; }
    public int? RulesSetId { get; set; }
    public int? EventId { get; set; }
    public int BestOf { get; set; } = 3;
    public bool RequireVerification { get; set; } = false;
    public int? TeamSize { get; set; }
    public MatchFormatType MatchFormat { get; set; } = MatchFormatType.BestOf;
    public int NumberOfSets { get; set; } = 3;
    public int GamesPerSet { get; set; } = 6;
    public SetWinMode SetWinMode { get; set; } = SetWinMode.WinBy2;
    public LeagueScoringMode LeagueScoring { get; set; } = LeagueScoringMode.WinPoints;
    public RubberTieBreakMode RubberTieBreak { get; set; } = RubberTieBreakMode.AdminDecides;
    public int? MinDaysBetweenPlayerMatches { get; set; }
    public bool HasDivisions { get; set; }
    public int? SeededFromCompetitionId { get; set; }
    public string? Description { get; set; }
}

// UI-only enums that separate category (Male/Female/Mixed) from the format shown in the
// dropdown (Singles/Doubles/Team). The persisted CompetitionFormat + EligibleSex pair is
// derived from these two on save.
public enum CompetitionCategoryUi { Male, Female, Mixed }
public enum CompetitionFormatUi { Singles, Doubles, Team }

public static class CompetitionFormHelpers
{
    public static IEnumerable<CompetitionFormatUi> AllowedFormats(CompetitionCategoryUi? category) =>
        new[] { CompetitionFormatUi.Singles, CompetitionFormatUi.Doubles, CompetitionFormatUi.Team };

    public static CompetitionFormat? EffectivePersistedFormat(CompetitionCategoryUi? category, CompetitionFormatUi? format) =>
        (category, format) switch
        {
            (CompetitionCategoryUi.Mixed, CompetitionFormatUi.Doubles) => CompetitionFormat.MixedDoubles,
            (CompetitionCategoryUi.Mixed, CompetitionFormatUi.Team)    => CompetitionFormat.TeamMatch,
            (_, CompetitionFormatUi.Singles) => CompetitionFormat.Singles,
            (_, CompetitionFormatUi.Doubles) => CompetitionFormat.Doubles,
            (_, CompetitionFormatUi.Team)    => CompetitionFormat.Team,
            _ => null
        };

    public static PlayerSex? EffectiveEligibleSex(CompetitionCategoryUi? category) => category switch
    {
        CompetitionCategoryUi.Male   => PlayerSex.Male,
        CompetitionCategoryUi.Female => PlayerSex.Female,
        _ => null
    };
}
