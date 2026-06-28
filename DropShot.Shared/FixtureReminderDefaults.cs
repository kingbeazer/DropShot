namespace DropShot.Shared;

/// <summary>
/// Default fixture reminder template used when a competition has no custom
/// reminder configured. Competitions can override these per-competition.
/// </summary>
public static class FixtureReminderDefaults
{
    public static readonly bool IncludeResultLink = true;

    public const string Subject = "Reminder: your {CompetitionName} fixture";

    public const string Body =
        "Hi {PlayerName},\n\n" +
        "This is a reminder that you have a fixture coming up.\n\n" +
        "**Opponent:** {OpponentName}\n" +
        "**Date:** {MatchDate}\n\n" +
        "View your match: {MatchLink}";
}
