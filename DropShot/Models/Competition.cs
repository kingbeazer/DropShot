namespace DropShot.Models
{
    public enum CompetitionFormat
    {
        Singles = 1,
        Doubles = 2,
        Team = 3,
        MixedDoubles = 4,
        MixedTeam = 5
    }

    public class Competition
    {
        public int CompetitionID { get; set; }
        public string CompetitionName { get; set; } = "";
        public CompetitionFormat CompetitionFormat { get; set; }

        public int? MaxParticipants { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? MaxAge { get; set; }
        public int? MinAge { get; set; }
        public PlayerSex? EligibleSex { get; set; }
        public int? RulesSetId { get; set; }
        public int? HostClubId { get; set; }
        public int? EventId { get; set; }
        public int BestOf { get; set; } = 3;
        public bool RequireVerification { get; set; } = false;
        public bool IsArchived { get; set; } = false;

        public RulesSet? Rules { get; set; }
        public Club? HostClub { get; set; }
        public Event? Event { get; set; }
        public ICollection<CompetitionParticipant> Participants { get; set; } = [];
        public ICollection<CompetitionFixture> Fixtures { get; set; } = [];
        public ICollection<CompetitionStage> Stages { get; set; } = [];
        public ICollection<CompetitionTeam> Teams { get; set; } = [];
        public ICollection<CompetitionMatchWindow> MatchWindows { get; set; } = [];
        public ICollection<CompetitionAdmin> Admins { get; set; } = [];
        public ICollection<CourtPair> CourtPairs { get; set; } = [];
    }
}
