using DropShot.Shared;
using DropShot.Shared.Dtos;

namespace DropShot.Maui.Components.Pages;

public class CompetitionFormModel
{
    public string CompetitionName { get; set; } = "";
    public CompetitionFormat CompetitionFormat { get; set; } = CompetitionFormat.Singles;
    public int? MaxParticipants { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MaxAge { get; set; }
    public PlayerSex? EligibleSex { get; set; }
    public int? HostClubId { get; set; }
    public int? RulesSetId { get; set; }

    public static CompetitionFormModel From(CompetitionDto dto) => new()
    {
        CompetitionName = dto.CompetitionName,
        CompetitionFormat = dto.CompetitionFormat,
        MaxParticipants = dto.MaxParticipants,
        StartDate = dto.StartDate,
        EndDate = dto.EndDate,
        MaxAge = dto.MaxAge,
        EligibleSex = dto.EligibleSex,
        HostClubId = dto.HostClubId,
        RulesSetId = dto.RulesSetId
    };
}
