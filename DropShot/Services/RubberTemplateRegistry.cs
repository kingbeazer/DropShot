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

    // One rubber per matchup, played as the competition's configured
    // best-of-sets × games-per-set format. Two rubbers run simultaneously
    // (court 1: Men's Doubles then Mixed A; court 2: Women's Doubles then Mixed B).
    private static readonly IReadOnlyList<RubberDef> MttTemplate =
    [
        new(1, "Men's Doubles",   1, [Roles.MA, Roles.MB], [Roles.MA, Roles.MB]),
        new(2, "Women's Doubles", 2, [Roles.FA, Roles.FB], [Roles.FA, Roles.FB]),
        new(3, "Mixed A",         1, [Roles.MA, Roles.FA], [Roles.MA, Roles.FA]),
        new(4, "Mixed B",         2, [Roles.MB, Roles.FB], [Roles.MB, Roles.FB]),
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
        [MttKey]           = ("Mixed Team Tennis (4 rubbers)", MttTemplate),
        [CountyDoublesKey] = ("County Doubles (4 rubbers)",    CountyDoublesTemplate),
    };

    private static readonly Dictionary<CompetitionFormat, string> FormatDefaults = new()
    {
        [CompetitionFormat.TeamMatch] = MttKey,
    };

    /// <summary>
    /// Player attributes the assigner needs. Kept as a plain record so the registry
    /// stays decoupled from EF models.
    /// </summary>
    public record AssignmentCandidate(int PlayerId, string DisplayName, PlayerSex? Sex);

    /// <summary>
    /// Maps players to the roles of a team. Returns a dictionary keyed by PlayerId.
    /// Players omitted from the map are left without a role (captain assigns manually).
    /// </summary>
    public delegate IReadOnlyDictionary<int, string> RoleAssigner(
        IReadOnlyList<AssignmentCandidate> players);

    private static readonly Dictionary<string, RoleAssigner> Assigners = new()
    {
        [MttKey]           = AssignMtt,
        [CountyDoublesKey] = AssignSequential,
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

    public static string? GetFormatDefaultKey(CompetitionFormat? format) =>
        format.HasValue && FormatDefaults.TryGetValue(format.Value, out var key) ? key : null;

    /// <summary>
    /// Returns a role-assignment strategy for a given preset key, or null if the
    /// preset is unknown (e.g. fully custom template, where the captain assigns roles).
    /// </summary>
    public static RoleAssigner? GetRoleAssigner(string? key) =>
        key != null && Assigners.TryGetValue(key, out var a) ? a : null;

    // ── Built-in assigners ──────────────────────────────────────────────────

    private static IReadOnlyDictionary<int, string> AssignMtt(
        IReadOnlyList<AssignmentCandidate> players)
    {
        var result = new Dictionary<int, string>();
        var males = players.Where(p => p.Sex == PlayerSex.Male).OrderBy(p => p.DisplayName).ToList();
        var females = players.Where(p => p.Sex == PlayerSex.Female).OrderBy(p => p.DisplayName).ToList();
        if (males.Count >= 1)   result[males[0].PlayerId]   = Roles.MA;
        if (males.Count >= 2)   result[males[1].PlayerId]   = Roles.MB;
        if (females.Count >= 1) result[females[0].PlayerId] = Roles.FA;
        if (females.Count >= 2) result[females[1].PlayerId] = Roles.FB;
        return result;
    }

    private static IReadOnlyDictionary<int, string> AssignSequential(
        IReadOnlyList<AssignmentCandidate> players)
    {
        // Deterministic fallback: assign the preset's roles (alphabetical order) to
        // players (also alphabetical order) in turn.
        var roles = players.Count == 0 ? [] : Presets[CountyDoublesKey].Template
            .SelectMany(d => d.HomeRoles.Concat(d.AwayRoles))
            .Distinct()
            .OrderBy(r => r)
            .ToList();

        var ordered = players.OrderBy(p => p.DisplayName).ToList();
        var result = new Dictionary<int, string>();
        for (int i = 0; i < Math.Min(ordered.Count, roles.Count); i++)
            result[ordered[i].PlayerId] = roles[i];
        return result;
    }
}
