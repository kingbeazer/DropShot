using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionEntryConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompetitionEntryConsents",
                columns: table => new
                {
                    CompetitionEntryConsentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    ConsentGivenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsentWordingShown = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConsentVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    WithdrawnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionEntryConsents", x => x.CompetitionEntryConsentId);
                    table.ForeignKey(
                        name: "FK_CompetitionEntryConsents_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "CompetitionID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionEntryConsents_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionEntryConsents_CompetitionId_PlayerId_WithdrawnUtc",
                table: "CompetitionEntryConsents",
                columns: new[] { "CompetitionId", "PlayerId", "WithdrawnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionEntryConsents_PlayerId",
                table: "CompetitionEntryConsents",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionEntryConsents");
        }
    }
}
