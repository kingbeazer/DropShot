using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClubLinkRequests_And_CompetitionCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatorUserId",
                table: "Competition",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRestricted",
                table: "Competition",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ClubLinkRequests",
                columns: table => new
                {
                    ClubLinkRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClubId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubLinkRequests", x => x.ClubLinkRequestId);
                    table.ForeignKey(
                        name: "FK_ClubLinkRequests_AspNetUsers_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClubLinkRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClubLinkRequests_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "ClubId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionAllowedPlayers",
                columns: table => new
                {
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionAllowedPlayers", x => new { x.CompetitionId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_CompetitionAllowedPlayers_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "CompetitionID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionAllowedPlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Competition_CreatorUserId",
                table: "Competition",
                column: "CreatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubLinkRequests_ClubId_Status",
                table: "ClubLinkRequests",
                columns: new[] { "ClubId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubLinkRequests_ResolvedByUserId",
                table: "ClubLinkRequests",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubLinkRequests_UserId_ClubId",
                table: "ClubLinkRequests",
                columns: new[] { "UserId", "ClubId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionAllowedPlayers_PlayerId",
                table: "CompetitionAllowedPlayers",
                column: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Competition_AspNetUsers_CreatorUserId",
                table: "Competition",
                column: "CreatorUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Competition_AspNetUsers_CreatorUserId",
                table: "Competition");

            migrationBuilder.DropTable(
                name: "ClubLinkRequests");

            migrationBuilder.DropTable(
                name: "CompetitionAllowedPlayers");

            migrationBuilder.DropIndex(
                name: "IX_Competition_CreatorUserId",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "CreatorUserId",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "IsRestricted",
                table: "Competition");
        }
    }
}
