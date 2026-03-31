namespace DropShot.Models;

public enum CourtSurface : byte
{
    Hard = 1,
    Clay = 2,
    Grass = 3,
    Carpet = 4
}

public class Club
{
    public int ClubId { get; set; }
    public string Name { get; set; } = "";
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Town { get; set; }
    public string? Postcode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    public ICollection<Court> Courts { get; set; } = [];
    public ICollection<ClubMember> Members { get; set; } = [];
    public ICollection<ClubAdministrator> Administrators { get; set; } = [];
    public ICollection<Competition> Competitions { get; set; } = [];
    public ICollection<ClubLadder> Ladders { get; set; } = [];
    public ICollection<ClubSchedulingTemplate> SchedulingTemplates { get; set; } = [];
}

public class Court
{
    public int CourtId { get; set; }
    public int ClubId { get; set; }
    public string Name { get; set; } = "";
    public CourtSurface Surface { get; set; }
    public bool IsIndoor { get; set; }

    public Club Club { get; set; } = null!;
    public ICollection<CompetitionFixture> Fixtures { get; set; } = [];
}
