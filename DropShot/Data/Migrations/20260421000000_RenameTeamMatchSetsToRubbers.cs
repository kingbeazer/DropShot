using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameTeamMatchSetsToRubbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── CompetitionParticipants: Grade enum → Role string ──────────────
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "CompetitionParticipants",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE cp
SET cp.Role = CASE
    WHEN p.Sex = 1 AND cp.Grade = 1 THEN 'MA'
    WHEN p.Sex = 1 AND cp.Grade = 2 THEN 'MB'
    WHEN p.Sex = 2 AND cp.Grade = 1 THEN 'FA'
    WHEN p.Sex = 2 AND cp.Grade = 2 THEN 'FB'
    ELSE NULL
END
FROM CompetitionParticipants cp
INNER JOIN Players p ON p.PlayerId = cp.PlayerId
WHERE cp.Grade IS NOT NULL;
");

            migrationBuilder.DropColumn(
                name: "Grade",
                table: "CompetitionParticipants");

            // ── TeamMatchSets → Rubbers (destructive: drop old table, recreate) ─
            migrationBuilder.DropTable(name: "TeamMatchSets");

            migrationBuilder.CreateTable(
                name: "Rubbers",
                columns: table => new
                {
                    RubberId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionFixtureId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: ""),
                    CourtNumber = table.Column<int>(type: "int", nullable: false),
                    HomeRolesJson = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, defaultValue: "[]"),
                    AwayRolesJson = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, defaultValue: "[]"),
                    HomePlayer1Id = table.Column<int>(type: "int", nullable: true),
                    HomePlayer2Id = table.Column<int>(type: "int", nullable: true),
                    AwayPlayer1Id = table.Column<int>(type: "int", nullable: true),
                    AwayPlayer2Id = table.Column<int>(type: "int", nullable: true),
                    HomeGames = table.Column<int>(type: "int", nullable: true),
                    AwayGames = table.Column<int>(type: "int", nullable: true),
                    WinnerTeamId = table.Column<int>(type: "int", nullable: true),
                    IsComplete = table.Column<bool>(type: "bit", nullable: false),
                    SavedMatchId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rubbers", x => x.RubberId);
                    table.ForeignKey(
                        name: "FK_Rubbers_CompetitionFixtures_CompetitionFixtureId",
                        column: x => x.CompetitionFixtureId,
                        principalTable: "CompetitionFixtures",
                        principalColumn: "CompetitionFixtureId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rubbers_CompetitionTeams_WinnerTeamId",
                        column: x => x.WinnerTeamId,
                        principalTable: "CompetitionTeams",
                        principalColumn: "CompetitionTeamId");
                    table.ForeignKey(
                        name: "FK_Rubbers_Players_AwayPlayer1Id",
                        column: x => x.AwayPlayer1Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rubbers_Players_AwayPlayer2Id",
                        column: x => x.AwayPlayer2Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rubbers_Players_HomePlayer1Id",
                        column: x => x.HomePlayer1Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rubbers_Players_HomePlayer2Id",
                        column: x => x.HomePlayer2Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rubbers_SavedMatch_SavedMatchId",
                        column: x => x.SavedMatchId,
                        principalTable: "SavedMatch",
                        principalColumn: "SavedMatchId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(name: "IX_Rubbers_AwayPlayer1Id",    table: "Rubbers", column: "AwayPlayer1Id");
            migrationBuilder.CreateIndex(name: "IX_Rubbers_AwayPlayer2Id",    table: "Rubbers", column: "AwayPlayer2Id");
            migrationBuilder.CreateIndex(name: "IX_Rubbers_CompetitionFixtureId", table: "Rubbers", column: "CompetitionFixtureId");
            migrationBuilder.CreateIndex(name: "IX_Rubbers_HomePlayer1Id",    table: "Rubbers", column: "HomePlayer1Id");
            migrationBuilder.CreateIndex(name: "IX_Rubbers_HomePlayer2Id",    table: "Rubbers", column: "HomePlayer2Id");
            migrationBuilder.CreateIndex(name: "IX_Rubbers_SavedMatchId",     table: "Rubbers", column: "SavedMatchId");
            migrationBuilder.CreateIndex(name: "IX_Rubbers_WinnerTeamId",     table: "Rubbers", column: "WinnerTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Rubbers");

            migrationBuilder.CreateTable(
                name: "TeamMatchSets",
                columns: table => new
                {
                    TeamMatchSetId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionFixtureId = table.Column<int>(type: "int", nullable: false),
                    SetNumber = table.Column<int>(type: "int", nullable: false),
                    Phase = table.Column<byte>(type: "tinyint", nullable: false),
                    SetType = table.Column<byte>(type: "tinyint", nullable: false),
                    CourtNumber = table.Column<int>(type: "int", nullable: false),
                    HomePlayer1Id = table.Column<int>(type: "int", nullable: true),
                    HomePlayer2Id = table.Column<int>(type: "int", nullable: true),
                    AwayPlayer1Id = table.Column<int>(type: "int", nullable: true),
                    AwayPlayer2Id = table.Column<int>(type: "int", nullable: true),
                    HomeGames = table.Column<int>(type: "int", nullable: true),
                    AwayGames = table.Column<int>(type: "int", nullable: true),
                    WinnerTeamId = table.Column<int>(type: "int", nullable: true),
                    IsComplete = table.Column<bool>(type: "bit", nullable: false),
                    SavedMatchId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMatchSets", x => x.TeamMatchSetId);
                    table.ForeignKey(
                        name: "FK_TeamMatchSets_CompetitionFixtures_CompetitionFixtureId",
                        column: x => x.CompetitionFixtureId,
                        principalTable: "CompetitionFixtures",
                        principalColumn: "CompetitionFixtureId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamMatchSets_CompetitionTeams_WinnerTeamId",
                        column: x => x.WinnerTeamId,
                        principalTable: "CompetitionTeams",
                        principalColumn: "CompetitionTeamId");
                    table.ForeignKey(
                        name: "FK_TeamMatchSets_Players_AwayPlayer1Id",
                        column: x => x.AwayPlayer1Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMatchSets_Players_AwayPlayer2Id",
                        column: x => x.AwayPlayer2Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMatchSets_Players_HomePlayer1Id",
                        column: x => x.HomePlayer1Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMatchSets_Players_HomePlayer2Id",
                        column: x => x.HomePlayer2Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMatchSets_SavedMatch_SavedMatchId",
                        column: x => x.SavedMatchId,
                        principalTable: "SavedMatch",
                        principalColumn: "SavedMatchId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(name: "IX_TeamMatchSets_AwayPlayer1Id",    table: "TeamMatchSets", column: "AwayPlayer1Id");
            migrationBuilder.CreateIndex(name: "IX_TeamMatchSets_AwayPlayer2Id",    table: "TeamMatchSets", column: "AwayPlayer2Id");
            migrationBuilder.CreateIndex(name: "IX_TeamMatchSets_CompetitionFixtureId", table: "TeamMatchSets", column: "CompetitionFixtureId");
            migrationBuilder.CreateIndex(name: "IX_TeamMatchSets_HomePlayer1Id",    table: "TeamMatchSets", column: "HomePlayer1Id");
            migrationBuilder.CreateIndex(name: "IX_TeamMatchSets_HomePlayer2Id",    table: "TeamMatchSets", column: "HomePlayer2Id");
            migrationBuilder.CreateIndex(name: "IX_TeamMatchSets_SavedMatchId",     table: "TeamMatchSets", column: "SavedMatchId");
            migrationBuilder.CreateIndex(name: "IX_TeamMatchSets_WinnerTeamId",     table: "TeamMatchSets", column: "WinnerTeamId");

            migrationBuilder.AddColumn<byte>(
                name: "Grade",
                table: "CompetitionParticipants",
                type: "tinyint",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "Role",
                table: "CompetitionParticipants");
        }
    }
}
