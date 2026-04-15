using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClubLitePlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByClubId",
                table: "Players",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_CreatedByClubId",
                table: "Players",
                column: "CreatedByClubId");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Clubs_CreatedByClubId",
                table: "Players",
                column: "CreatedByClubId",
                principalTable: "Clubs",
                principalColumn: "ClubId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Clubs_CreatedByClubId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_CreatedByClubId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "CreatedByClubId",
                table: "Players");
        }
    }
}
