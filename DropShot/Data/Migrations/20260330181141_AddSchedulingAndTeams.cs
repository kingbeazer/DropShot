using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingAndTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MobileNumber",
                table: "Players",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "CompetitionParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FixtureLabel",
                table: "CompetitionFixtures",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoundNumber",
                table: "CompetitionFixtures",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompetitionTeams",
                columns: table => new
                {
                    CompetitionTeamId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionTeams", x => x.CompetitionTeamId);
                    table.ForeignKey(
                        name: "FK_CompetitionTeams_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "CompetitionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionParticipants_TeamId",
                table: "CompetitionParticipants",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionTeams_CompetitionId",
                table: "CompetitionTeams",
                column: "CompetitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionParticipants_CompetitionTeams_TeamId",
                table: "CompetitionParticipants",
                column: "TeamId",
                principalTable: "CompetitionTeams",
                principalColumn: "CompetitionTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionParticipants_CompetitionTeams_TeamId",
                table: "CompetitionParticipants");

            migrationBuilder.DropTable(
                name: "CompetitionTeams");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionParticipants_TeamId",
                table: "CompetitionParticipants");

            migrationBuilder.DropColumn(
                name: "MobileNumber",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "CompetitionParticipants");

            migrationBuilder.DropColumn(
                name: "FixtureLabel",
                table: "CompetitionFixtures");

            migrationBuilder.DropColumn(
                name: "RoundNumber",
                table: "CompetitionFixtures");
        }
    }
}
