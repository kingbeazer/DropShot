using DropShot.Data;

namespace DropShot.Models;

public enum PlayerSex : byte
{
    Male = 1,
    Female = 2,
    Other = 3
}

public class Player
{
    public int PlayerId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsLight { get; set; }
    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public PlayerSex? Sex { get; set; }
    public string? ProfileImagePath { get; set; }
    public string? ContactPreferences { get; set; }
    public string? MobileNumber { get; set; }
    public bool? DefaultGameScoring { get; set; }

    public ICollection<CompetitionParticipant> CompetitionParticipants { get; set; } = [];
    public ICollection<PlayerFriend> Friends { get; set; } = [];
    public ICollection<PlayerFriend> FriendOf { get; set; } = [];
    public ICollection<ClubMember> ClubMemberships { get; set; } = [];
}
