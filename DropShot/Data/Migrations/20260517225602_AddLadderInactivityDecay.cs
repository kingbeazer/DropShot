using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLadderInactivityDecay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastDecayAppliedAt",
                table: "CompetitionParticipants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastInactivityWarningAt",
                table: "CompetitionParticipants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LadderInactivityDecays",
                columns: table => new
                {
                    LadderInactivityDecayId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RatingBefore = table.Column<double>(type: "float", nullable: false),
                    RatingAfter = table.Column<double>(type: "float", nullable: false),
                    DaysInactive = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LadderInactivityDecays", x => x.LadderInactivityDecayId);
                    table.ForeignKey(
                        name: "FK_LadderInactivityDecays_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "CompetitionID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LadderInactivityDecays_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LadderInactivityDecays_CompetitionId_AppliedAt",
                table: "LadderInactivityDecays",
                columns: new[] { "CompetitionId", "AppliedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LadderInactivityDecays_PlayerId",
                table: "LadderInactivityDecays",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LadderInactivityDecays");

            migrationBuilder.DropColumn(
                name: "LastDecayAppliedAt",
                table: "CompetitionParticipants");

            migrationBuilder.DropColumn(
                name: "LastInactivityWarningAt",
                table: "CompetitionParticipants");
        }
    }
}
