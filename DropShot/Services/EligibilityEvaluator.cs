using DropShot.Models;

namespace DropShot.Services;

/// <summary>
/// A single rule that the caller violated when attempting to enter or be added
/// to a competition. Codes are stable identifiers for admin-UI tooling; messages
/// are human-readable and safe to display directly.
/// </summary>
public sealed record EligibilityViolation(string Code, string Message);

/// <summary>
/// Deterministic, DB-free check of whether a <see cref="Player"/> satisfies a
/// <see cref="Competition"/>'s sex / age / allow-list rules. The user-facing
/// self-registration flow blocks on any violation; admin flows surface them as
/// a confirmation prompt that can be overridden.
/// </summary>
public static class EligibilityEvaluator
{
    public static List<EligibilityViolation> Evaluate(
        Competition comp, Player player, IReadOnlySet<int>? allowedPlayerIds = null)
    {
        var violations = new List<EligibilityViolation>();

        if (comp.EligibleSex.HasValue && player.Sex != comp.EligibleSex.Value)
        {
            violations.Add(new(
                "sex",
                $"Player sex is {FormatSex(player.Sex)}; competition is restricted to {FormatSex(comp.EligibleSex)}."));
        }

        if (comp.MinAge.HasValue || comp.MaxAge.HasValue)
        {
            if (!player.DateOfBirth.HasValue)
            {
                violations.Add(new(
                    "age-unknown",
                    "Player has no date of birth on file; age cannot be verified."));
            }
            else
            {
                var refDate = DateOnly.FromDateTime(comp.StartDate ?? DateTime.UtcNow);
                var age = AgeOn(refDate, player.DateOfBirth.Value);
                if (comp.MinAge.HasValue && age < comp.MinAge.Value)
                    violations.Add(new(
                        "min-age",
                        $"Player is {age}; minimum age for this competition is {comp.MinAge.Value}."));
                if (comp.MaxAge.HasValue && age > comp.MaxAge.Value)
                    violations.Add(new(
                        "max-age",
                        $"Player is {age}; maximum age for this competition is {comp.MaxAge.Value}."));
            }
        }

        if (comp.IsRestricted)
        {
            var set = allowedPlayerIds
                ?? (IReadOnlySet<int>)comp.AllowedPlayers.Select(ap => ap.PlayerId).ToHashSet();
            if (!set.Contains(player.PlayerId))
                violations.Add(new(
                    "allowlist",
                    "Player is not on this competition's allow-list."));
        }

        return violations;
    }

    private static int AgeOn(DateOnly on, DateOnly dob)
    {
        int age = on.Year - dob.Year;
        if (dob > on.AddYears(-age)) age--;
        return age;
    }

    private static string FormatSex(PlayerSex? sex) => sex switch
    {
        PlayerSex.Male => "male",
        PlayerSex.Female => "female",
        PlayerSex.Other => "other",
        _ => "not set",
    };
}
