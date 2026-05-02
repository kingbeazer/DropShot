namespace DropShot.Shared;

public enum CourtClaimOutcome
{
    Free,
    StaleAutoClosed,
    InGrace,
    NeedsChallenge
}

public sealed record CourtClaimResult(
    CourtClaimOutcome Outcome,
    int? SavedMatchId = null,
    DateTime? GraceUntilUtc = null,
    string? OccupantLabel = null)
{
    public static CourtClaimResult Free() => new(CourtClaimOutcome.Free);
    public static CourtClaimResult StaleAutoClosed(int savedMatchId) =>
        new(CourtClaimOutcome.StaleAutoClosed, savedMatchId);
    public static CourtClaimResult InGrace(int savedMatchId, DateTime until, string label) =>
        new(CourtClaimOutcome.InGrace, savedMatchId, until, label);
    public static CourtClaimResult NeedsChallenge(int savedMatchId, string label) =>
        new(CourtClaimOutcome.NeedsChallenge, savedMatchId, null, label);
}
