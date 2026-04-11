using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedMatchPlayerIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Player1Id",
                table: "SavedMatch",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Player2Id",
                table: "SavedMatch",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Player3Id",
                table: "SavedMatch",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Player4Id",
                table: "SavedMatch",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WinnerPlayerId",
                table: "SavedMatch",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedMatch_Player1Id",
                table: "SavedMatch",
                column: "Player1Id");

            migrationBuilder.CreateIndex(
                name: "IX_SavedMatch_Player2Id",
                table: "SavedMatch",
                column: "Player2Id");

            migrationBuilder.CreateIndex(
                name: "IX_SavedMatch_Player3Id",
                table: "SavedMatch",
                column: "Player3Id");

            migrationBuilder.CreateIndex(
                name: "IX_SavedMatch_Player4Id",
                table: "SavedMatch",
                column: "Player4Id");

            migrationBuilder.CreateIndex(
                name: "IX_SavedMatch_WinnerPlayerId",
                table: "SavedMatch",
                column: "WinnerPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_SavedMatch_Players_Player1Id",
                table: "SavedMatch",
                column: "Player1Id",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SavedMatch_Players_Player2Id",
                table: "SavedMatch",
                column: "Player2Id",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SavedMatch_Players_Player3Id",
                table: "SavedMatch",
                column: "Player3Id",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SavedMatch_Players_Player4Id",
                table: "SavedMatch",
                column: "Player4Id",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SavedMatch_Players_WinnerPlayerId",
                table: "SavedMatch",
                column: "WinnerPlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SavedMatch_Players_Player1Id",
                table: "SavedMatch");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedMatch_Players_Player2Id",
                table: "SavedMatch");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedMatch_Players_Player3Id",
                table: "SavedMatch");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedMatch_Players_Player4Id",
                table: "SavedMatch");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedMatch_Players_WinnerPlayerId",
                table: "SavedMatch");

            migrationBuilder.DropIndex(
                name: "IX_SavedMatch_Player1Id",
                table: "SavedMatch");

            migrationBuilder.DropIndex(
                name: "IX_SavedMatch_Player2Id",
                table: "SavedMatch");

            migrationBuilder.DropIndex(
                name: "IX_SavedMatch_Player3Id",
                table: "SavedMatch");

            migrationBuilder.DropIndex(
                name: "IX_SavedMatch_Player4Id",
                table: "SavedMatch");

            migrationBuilder.DropIndex(
                name: "IX_SavedMatch_WinnerPlayerId",
                table: "SavedMatch");

            migrationBuilder.DropColumn(
                name: "Player1Id",
                table: "SavedMatch");

            migrationBuilder.DropColumn(
                name: "Player2Id",
                table: "SavedMatch");

            migrationBuilder.DropColumn(
                name: "Player3Id",
                table: "SavedMatch");

            migrationBuilder.DropColumn(
                name: "Player4Id",
                table: "SavedMatch");

            migrationBuilder.DropColumn(
                name: "WinnerPlayerId",
                table: "SavedMatch");
        }
    }
}
