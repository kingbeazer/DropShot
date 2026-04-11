using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Migrations
{
    /// <inheritdoc />
    public partial class AddMixedTeamTennis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CaptainPlayerId",
                table: "CompetitionTeams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Grade",
                table: "CompetitionParticipants",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayTeamId",
                table: "CompetitionFixtures",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CourtPairId",
                table: "CompetitionFixtures",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeTeamId",
                table: "CompetitionFixtures",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WinnerTeamId",
                table: "CompetitionFixtures",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CourtPairs",
                columns: table => new
                {
                    CourtPairId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    Court1Id = table.Column<int>(type: "int", nullable: false),
                    Court2Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtPairs", x => x.CourtPairId);
                    table.ForeignKey(
                        name: "FK_CourtPairs_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "CompetitionID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourtPairs_Courts_Court1Id",
                        column: x => x.Court1Id,
                        principalTable: "Courts",
                        principalColumn: "CourtId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourtPairs_Courts_Court2Id",
                        column: x => x.Court2Id,
                        principalTable: "Courts",
                        principalColumn: "CourtId",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionTeams_CaptainPlayerId",
                table: "CompetitionTeams",
                column: "CaptainPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionFixtures_AwayTeamId",
                table: "CompetitionFixtures",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionFixtures_CourtPairId",
                table: "CompetitionFixtures",
                column: "CourtPairId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionFixtures_HomeTeamId",
                table: "CompetitionFixtures",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionFixtures_WinnerTeamId",
                table: "CompetitionFixtures",
                column: "WinnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_CourtPairs_CompetitionId",
                table: "CourtPairs",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CourtPairs_Court1Id",
                table: "CourtPairs",
                column: "Court1Id");

            migrationBuilder.CreateIndex(
                name: "IX_CourtPairs_Court2Id",
                table: "CourtPairs",
                column: "Court2Id");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMatchSets_AwayPlayer1Id",
                table: "TeamMatchSets",
                column: "AwayPlayer1Id");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMatchSets_AwayPlayer2Id",
                table: "TeamMatchSets",
                column: "AwayPlayer2Id");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMatchSets_CompetitionFixtureId",
                table: "TeamMatchSets",
                column: "CompetitionFixtureId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMatchSets_HomePlayer1Id",
                table: "TeamMatchSets",
                column: "HomePlayer1Id");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMatchSets_HomePlayer2Id",
                table: "TeamMatchSets",
                column: "HomePlayer2Id");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMatchSets_SavedMatchId",
                table: "TeamMatchSets",
                column: "SavedMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMatchSets_WinnerTeamId",
                table: "TeamMatchSets",
                column: "WinnerTeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionFixtures_CompetitionTeams_AwayTeamId",
                table: "CompetitionFixtures",
                column: "AwayTeamId",
                principalTable: "CompetitionTeams",
                principalColumn: "CompetitionTeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionFixtures_CompetitionTeams_HomeTeamId",
                table: "CompetitionFixtures",
                column: "HomeTeamId",
                principalTable: "CompetitionTeams",
                principalColumn: "CompetitionTeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionFixtures_CompetitionTeams_WinnerTeamId",
                table: "CompetitionFixtures",
                column: "WinnerTeamId",
                principalTable: "CompetitionTeams",
                principalColumn: "CompetitionTeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionFixtures_CourtPairs_CourtPairId",
                table: "CompetitionFixtures",
                column: "CourtPairId",
                principalTable: "CourtPairs",
                principalColumn: "CourtPairId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionTeams_Players_CaptainPlayerId",
                table: "CompetitionTeams",
                column: "CaptainPlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionFixtures_CompetitionTeams_AwayTeamId",
                table: "CompetitionFixtures");

            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionFixtures_CompetitionTeams_HomeTeamId",
                table: "CompetitionFixtures");

            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionFixtures_CompetitionTeams_WinnerTeamId",
                table: "CompetitionFixtures");

            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionFixtures_CourtPairs_CourtPairId",
                table: "CompetitionFixtures");

            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionTeams_Players_CaptainPlayerId",
                table: "CompetitionTeams");

            migrationBuilder.DropTable(
                name: "CourtPairs");

            migrationBuilder.DropTable(
                name: "TeamMatchSets");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionTeams_CaptainPlayerId",
                table: "CompetitionTeams");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionFixtures_AwayTeamId",
                table: "CompetitionFixtures");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionFixtures_CourtPairId",
                table: "CompetitionFixtures");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionFixtures_HomeTeamId",
                table: "CompetitionFixtures");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionFixtures_WinnerTeamId",
                table: "CompetitionFixtures");

            migrationBuilder.DropColumn(
                name: "CaptainPlayerId",
                table: "CompetitionTeams");

            migrationBuilder.DropColumn(
                name: "Grade",
                table: "CompetitionParticipants");

            migrationBuilder.DropColumn(
                name: "AwayTeamId",
                table: "CompetitionFixtures");

            migrationBuilder.DropColumn(
                name: "CourtPairId",
                table: "CompetitionFixtures");

            migrationBuilder.DropColumn(
                name: "HomeTeamId",
                table: "CompetitionFixtures");

            migrationBuilder.DropColumn(
                name: "WinnerTeamId",
                table: "CompetitionFixtures");
        }
    }
}
