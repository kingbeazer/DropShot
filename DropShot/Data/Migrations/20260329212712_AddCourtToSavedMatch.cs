using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Migrations
{
    /// <inheritdoc />
    public partial class AddCourtToSavedMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CourtId",
                table: "SavedMatch",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedMatch_CourtId",
                table: "SavedMatch",
                column: "CourtId");

            migrationBuilder.AddForeignKey(
                name: "FK_SavedMatch_Courts_CourtId",
                table: "SavedMatch",
                column: "CourtId",
                principalTable: "Courts",
                principalColumn: "CourtId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SavedMatch_Courts_CourtId",
                table: "SavedMatch");

            migrationBuilder.DropIndex(
                name: "IX_SavedMatch_CourtId",
                table: "SavedMatch");

            migrationBuilder.DropColumn(
                name: "CourtId",
                table: "SavedMatch");
        }
    }
}
