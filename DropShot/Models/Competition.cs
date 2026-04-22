namespace DropShot.Models
{
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

    public enum CompetitionFormat
    {
        Singles = 1,
        Doubles = 2,
        Team = 3,
        MixedDoubles = 4,
        TeamMatch = 5
    }

    public class Competition
    {
        public int CompetitionID { get; set; }
        public string CompetitionName { get; set; } = "";
        public CompetitionFormat CompetitionFormat { get; set; }

        public int? MaxParticipants { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? RegisterByDate { get; set; }
        public int? MaxAge { get; set; }
        public int? MinAge { get; set; }
        public PlayerSex? EligibleSex { get; set; }
        public int? RulesSetId { get; set; }
        public int? HostClubId { get; set; }
        public int? EventId { get; set; }
        public int BestOf { get; set; } = 3;
        public bool RequireVerification { get; set; } = false;
        public bool IsArchived { get; set; } = false;
        public bool IsStarted { get; set; } = false;

        /// <summary>
        /// For "user competitions" (no host club), identifies the subscribed user who created
        /// the competition. Mutually exclusive with <see cref="HostClubId"/>.
        /// </summary>
        public string? CreatorUserId { get; set; }
        public DropShot.Data.ApplicationUser? CreatorUser { get; set; }

        /// <summary>
        /// When true, the competition is only visible/enterable to the explicit
        /// <see cref="AllowedPlayers"/> list (in addition to the implicit access rule).
        /// </summary>
        public bool IsRestricted { get; set; } = false;

        /// <summary>
        /// Number of players per team (or pair). Defaults: 2 for Doubles/MixedDoubles, driven by template for TeamMatch.
        /// </summary>
        public int? TeamSize { get; set; }

        /// <summary>
        /// Optional preset key (see <c>RubberTemplateRegistry</c>) overriding the default template for this format.
        /// When null, the format's default preset is used. Overridden by any <c>RubberTemplate</c> attached to this competition.
        /// </summary>
        public string? RubberTemplateKey { get; set; }

        public MatchFormatType MatchFormat { get; set; } = MatchFormatType.BestOf;
        public int NumberOfSets { get; set; } = 3;
        public int GamesPerSet { get; set; } = 6;
        public SetWinMode SetWinMode { get; set; } = SetWinMode.WinBy2;
        public LeagueScoringMode LeagueScoring { get; set; } = LeagueScoringMode.WinPoints;

        /// <summary>
        /// When this competition is one division of a league season, points at the
        /// owning <see cref="LeagueDivision"/> row. Null for standalone competitions.
        /// </summary>
        public int? LeagueDivisionId { get; set; }

        public RulesSet? Rules { get; set; }
        public Club? HostClub { get; set; }
        public Event? Event { get; set; }
        public LeagueDivision? LeagueDivision { get; set; }
        public ICollection<CompetitionParticipant> Participants { get; set; } = [];
        public ICollection<CompetitionFixture> Fixtures { get; set; } = [];
        public ICollection<CompetitionStage> Stages { get; set; } = [];
        public ICollection<CompetitionTeam> Teams { get; set; } = [];
        public ICollection<CompetitionMatchWindow> MatchWindows { get; set; } = [];
        public ICollection<CompetitionAdmin> Admins { get; set; } = [];
        public ICollection<CourtPair> CourtPairs { get; set; } = [];
        public ICollection<CompetitionAllowedPlayer> AllowedPlayers { get; set; } = [];
        public CompetitionRubberTemplate? RubberTemplate { get; set; }
    }
}
