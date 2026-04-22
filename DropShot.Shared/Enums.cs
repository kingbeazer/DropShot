namespace DropShot.Shared;

public enum CompetitionFormat : byte
{
    Singles = 1,
    Doubles = 2,
    Team = 3,
    MixedDoubles = 4,
    TeamMatch = 5
}

public enum PlayerSex : byte
{
    Male = 1,
    Female = 2,
    Other = 3
}

public enum CourtSurface : byte
{
    Hard = 1,
    Clay = 2,
    Grass = 3,
    Carpet = 4
}

public enum StageType : byte
{
    RoundRobin   = 1,
    Knockout     = 2,   // full auto-bracket (generates all rounds automatically)
    Final        = 3,
    QuarterFinal = 4,   // top 8 players — 4 matches
    SemiFinal    = 5,   // top 4 players (or QF winners) — 2 matches
}

public enum ParticipantStatus : byte
{
    Registered = 1,
    Confirmed = 2,
    Withdrawn = 3,
    Eliminated = 4
}

public enum FriendStatus : byte
{
    Pending = 1,
    Accepted = 2,
    Blocked = 3
}

public enum FixtureStatus : byte
{
    Scheduled = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    Walkover = 5
}

public enum MatchFormatType : byte
{
    BestOf = 1,
    FixedSets = 2
}

public enum LeagueScoringMode : byte
{
    WinPoints = 1,
    SetsWon = 2,
    GamesWon = 3
}

public enum SetWinMode : byte
{
    WinBy2 = 0,
    FirstTo = 1
}
