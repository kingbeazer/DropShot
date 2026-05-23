using DropShot.Shared;

namespace DropShot.Models
{
    public class Competition
    {
        public int CompetitionID { get; set; }
        public string CompetitionName { get; set; } = "";
        public CompetitionFormat CompetitionFormat { get; set; }

        /// <summary>
        /// Free-form general information about the competition, written in
        /// Markdown. Rendered to HTML for display by both the edit-mode
        /// preview and the player-facing view page. Null/empty hides the
        /// "About" panel entirely.
        /// </summary>
        public string? Description { get; set; }

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

        /// <summary>
        /// Scoring rules for the optional final-set tie break. A tie break is
        /// not mandatory — players may decide on the day depending on time
        /// available. When the final set IS played as a tie break, the score
        /// is validated against these settings instead of the regular
        /// <see cref="GamesPerSet"/> / <see cref="SetWinMode"/>.
        /// </summary>
        public int FinalSetTieBreakGames { get; set; } = 10;
        public SetWinMode FinalSetTieBreakWinMode { get; set; } = SetWinMode.WinBy2;

        public LeagueScoringMode LeagueScoring { get; set; } = LeagueScoringMode.WinPoints;

        /// <summary>
        /// How to resolve a knockout team-match when the rubbers are tied.
        /// Only applies to TeamMatch competitions. See <see cref="DropShot.Shared.RubberTieBreakMode"/>.
        /// </summary>
        public DropShot.Shared.RubberTieBreakMode RubberTieBreak { get; set; } = DropShot.Shared.RubberTieBreakMode.AdminDecides;

        /// <summary>
        /// Minimum number of days that must elapse between any two matches for the
        /// same player during auto-scheduling. Null or 0 means no constraint.
        /// </summary>
        public int? MinDaysBetweenPlayerMatches { get; set; }

        /// <summary>
        /// When true, this competition is split into ranked divisions via
        /// <see cref="Divisions"/>. Teams only play other teams in the same
        /// division, and league tables render per-division.
        /// </summary>
        public bool HasDivisions { get; set; }

        /// <summary>
        /// Optional pointer at a previous season's competition. Used at competition
        /// creation time to seed division assignments from the previous season's
        /// final standings.
        /// </summary>
        public int? SeededFromCompetitionId { get; set; }

        // ── Singles Elo Ladder config ───────────────────────────────────────
        // Only meaningful when CompetitionFormat == SinglesLadder. Defaults
        // are inert for other formats. K is doubled while a participant's
        // MatchesPlayed < LadderProvisionalMatches (mirrors PlayerRatingService
        // 40/20 convention).
        public double LadderKFactor { get; set; } = 20.0;
        public double LadderStartingRating { get; set; } = 1000.0;
        public int LadderProvisionalMatches { get; set; } = 10;
        public bool LadderUseMarginOfVictory { get; set; } = true;

        public RulesSet? Rules { get; set; }
        public Club? HostClub { get; set; }
        public Event? Event { get; set; }
        public Competition? SeededFromCompetition { get; set; }
        public ICollection<CompetitionDivision> Divisions { get; set; } = [];
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
