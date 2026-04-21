using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DropShot.Models;

public class RubberTemplateRubber
{
    public int RubberTemplateRubberId { get; set; }
    public int CompetitionRubberTemplateId { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = "";
    public int CourtNumber { get; set; } = 1;
    public string HomeRolesJson { get; set; } = "[]";
    public string AwayRolesJson { get; set; } = "[]";

    public CompetitionRubberTemplate Template { get; set; } = null!;

    [NotMapped]
    public IReadOnlyList<string> HomeRoles
    {
        get => JsonSerializer.Deserialize<List<string>>(HomeRolesJson) ?? [];
        set => HomeRolesJson = JsonSerializer.Serialize(value);
    }

    [NotMapped]
    public IReadOnlyList<string> AwayRoles
    {
        get => JsonSerializer.Deserialize<List<string>>(AwayRolesJson) ?? [];
        set => AwayRolesJson = JsonSerializer.Serialize(value);
    }
}
