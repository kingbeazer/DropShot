using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerRatingSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerRatingSnapshots",
                columns: table => new
                {
                    PlayerRatingSnapshotId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    Rating = table.Column<double>(type: "float", nullable: false),
                    RubbersPlayed = table.Column<int>(type: "int", nullable: false),
                    IsProvisional = table.Column<bool>(type: "bit", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRatingSnapshots", x => x.PlayerRatingSnapshotId);
                    table.ForeignKey(
                        name: "FK_PlayerRatingSnapshots_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "CompetitionID");
                    table.ForeignKey(
                        name: "FK_PlayerRatingSnapshots_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRatingSnapshots_CompetitionId",
                table: "PlayerRatingSnapshots",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRatingSnapshots_PlayerId_CompetitionId_Kind",
                table: "PlayerRatingSnapshots",
                columns: new[] { "PlayerId", "CompetitionId", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerRatingSnapshots");
        }
    }
}
