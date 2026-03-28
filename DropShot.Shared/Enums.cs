namespace DropShot.Shared;

public enum CompetitionFormat : byte
{
    Singles = 1,
    Doubles = 2,
    Team = 3,
    MixedDoubles = 4
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
    RoundRobin = 1,
    Knockout = 2,
    Final = 3
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
    Cancelled = 4
}
