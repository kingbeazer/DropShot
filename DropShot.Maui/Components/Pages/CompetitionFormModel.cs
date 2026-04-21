using DropShot.Shared;
using DropShot.Shared.Dtos;

namespace DropShot.Maui.Components.Pages;

public class CompetitionFormModel
{
    public string CompetitionName { get; set; } = "";
    public CompetitionCategoryUi? Category { get; set; }
    public CompetitionFormatUi? Format { get; set; }
    public int? MaxParticipants { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MaxAge { get; set; }
    public int? MinAge { get; set; }
    public int? HostClubId { get; set; }
    public int? RulesSetId { get; set; }
    public int? EventId { get; set; }

    /// <summary>Persisted format derived from (Category, Format). Null until both are picked.</summary>
    public CompetitionFormat? CompetitionFormat => (Category, Format) switch
    {
        (CompetitionCategoryUi.Mixed, CompetitionFormatUi.Doubles) => Shared.CompetitionFormat.MixedDoubles,
        (CompetitionCategoryUi.Mixed, CompetitionFormatUi.Team)    => Shared.CompetitionFormat.TeamMatch,
        (_, CompetitionFormatUi.Singles) => Shared.CompetitionFormat.Singles,
        (_, CompetitionFormatUi.Doubles) => Shared.CompetitionFormat.Doubles,
        (_, CompetitionFormatUi.Team)    => Shared.CompetitionFormat.Team,
        _ => (CompetitionFormat?)null
    };

    /// <summary>Persisted EligibleSex derived from Category. Mixed → null (open).</summary>
    public PlayerSex? EligibleSex => Category switch
    {
        CompetitionCategoryUi.Male   => PlayerSex.Male,
        CompetitionCategoryUi.Female => PlayerSex.Female,
        CompetitionCategoryUi.Mixed  => null,
        _ => null
    };

    public static CompetitionFormModel From(CompetitionDto dto)
    {
        var (category, format) = SplitPersistedFormat(dto.CompetitionFormat, dto.EligibleSex);
        return new CompetitionFormModel
        {
            CompetitionName = dto.CompetitionName,
            Category = category,
            Format = format,
            MaxParticipants = dto.MaxParticipants,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            MaxAge = dto.MaxAge,
            MinAge = dto.MinAge,
            HostClubId = dto.HostClubId,
            RulesSetId = dto.RulesSetId,
            EventId = dto.EventId
        };
    }

    public static (CompetitionCategoryUi Category, CompetitionFormatUi Format) SplitPersistedFormat(
        CompetitionFormat persisted, PlayerSex? eligibleSex) => persisted switch
    {
        Shared.CompetitionFormat.MixedDoubles => (CompetitionCategoryUi.Mixed, CompetitionFormatUi.Doubles),
        Shared.CompetitionFormat.TeamMatch    => (CompetitionCategoryUi.Mixed, CompetitionFormatUi.Team),
        _ => (
            eligibleSex switch
            {
                PlayerSex.Female => CompetitionCategoryUi.Female,
                _                => CompetitionCategoryUi.Male
            },
            persisted switch
            {
                Shared.CompetitionFormat.Doubles => CompetitionFormatUi.Doubles,
                Shared.CompetitionFormat.Team    => CompetitionFormatUi.Team,
                _                                => CompetitionFormatUi.Singles
            })
    };

    public static IEnumerable<CompetitionFormatUi> AllowedFormats(CompetitionCategoryUi? category) =>
        category == CompetitionCategoryUi.Mixed
            ? new[] { CompetitionFormatUi.Doubles, CompetitionFormatUi.Team }
            : new[] { CompetitionFormatUi.Singles, CompetitionFormatUi.Doubles, CompetitionFormatUi.Team };
}

public enum CompetitionCategoryUi { Male, Female, Mixed }
public enum CompetitionFormatUi { Singles, Doubles, Team }
