using DropShot.Models;

namespace DropShot.Services;

public record RubberDef(
    int Order,
    string Name,
    int CourtNumber,
    IReadOnlyList<string> HomeRoles,
    IReadOnlyList<string> AwayRoles);

/// <summary>
/// Code-default rubber templates. A competition can override these by attaching a
/// <see cref="CompetitionRubberTemplate"/> row or by selecting a non-default preset
/// via <c>Competition.RubberTemplateKey</c>.
/// </summary>
public static class RubberTemplateRegistry
{
    public static class Roles
    {
        public const string MA = "MA";
        public const string MB = "MB";
        public const string FA = "FA";
        public const string FB = "FB";
    }

    public const string MttKey = "mtt";
    public const string CountyDoublesKey = "county-doubles";

    private static readonly IReadOnlyList<RubberDef> MttTemplate =
    [
        new(1, "Men's Doubles",   1, [Roles.MA, Roles.MB], [Roles.MA, Roles.MB]),
        new(2, "Men's Doubles",   1, [Roles.MA, Roles.MB], [Roles.MA, Roles.MB]),
        new(3, "Women's Doubles", 2, [Roles.FA, Roles.FB], [Roles.FA, Roles.FB]),
        new(4, "Women's Doubles", 2, [Roles.FA, Roles.FB], [Roles.FA, Roles.FB]),
        new(5, "Mixed A",         1, [Roles.MA, Roles.FA], [Roles.MA, Roles.FA]),
        new(6, "Mixed A",         1, [Roles.MA, Roles.FA], [Roles.MA, Roles.FA]),
        new(7, "Mixed B",         2, [Roles.MB, Roles.FB], [Roles.MB, Roles.FB]),
        new(8, "Mixed B",         2, [Roles.MB, Roles.FB], [Roles.MB, Roles.FB]),
    ];

    // County Doubles: 4 players per team split into two pairs (D1A+D1B, D2A+D2B).
    // Each home pair plays each away pair once → 4 rubbers on a single court.
    private static readonly IReadOnlyList<RubberDef> CountyDoublesTemplate =
    [
        new(1, "Pair 1 vs Pair 1", 1, ["D1A", "D1B"], ["D1A", "D1B"]),
        new(2, "Pair 1 vs Pair 2", 1, ["D1A", "D1B"], ["D2A", "D2B"]),
        new(3, "Pair 2 vs Pair 1", 1, ["D2A", "D2B"], ["D1A", "D1B"]),
        new(4, "Pair 2 vs Pair 2", 1, ["D2A", "D2B"], ["D2A", "D2B"]),
    ];

    private static readonly Dictionary<string, (string Label, IReadOnlyList<RubberDef> Template)> Presets = new()
    {
        [MttKey]           = ("Mixed Team Tennis (8 rubbers)", MttTemplate),
        [CountyDoublesKey] = ("County Doubles (4 rubbers)",    CountyDoublesTemplate),
    };

    private static readonly Dictionary<CompetitionFormat, string> FormatDefaults = new()
    {
        [CompetitionFormat.TeamMatch] = MttKey,
    };

    /// <summary>
    /// Resolve a preset by explicit key, falling back to the format's default.
    /// </summary>
    public static IReadOnlyList<RubberDef>? Resolve(CompetitionFormat? format, string? key)
    {
        if (!string.IsNullOrWhiteSpace(key) && Presets.TryGetValue(key!, out var byKey))
            return byKey.Template;

        if (format.HasValue && FormatDefaults.TryGetValue(format.Value, out var defaultKey)
            && Presets.TryGetValue(defaultKey, out var byDefault))
            return byDefault.Template;

        return null;
    }

    public static IReadOnlyList<string> GetRoleSet(IReadOnlyList<RubberDef>? template)
    {
        if (template is null) return [];
        return template
            .SelectMany(d => d.HomeRoles.Concat(d.AwayRoles))
            .Distinct()
            .OrderBy(r => r)
            .ToList();
    }

    public static IEnumerable<(string Key, string Label)> AvailablePresets() =>
        Presets.Select(kv => (kv.Key, kv.Value.Label));
}
