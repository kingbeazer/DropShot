using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Migrations
{
    /// <inheritdoc />
    public partial class AddCourtToMatchWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CourtId",
                table: "CompetitionMatchWindows",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionMatchWindows_CourtId",
                table: "CompetitionMatchWindows",
                column: "CourtId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionMatchWindows_Courts_CourtId",
                table: "CompetitionMatchWindows",
                column: "CourtId",
                principalTable: "Courts",
                principalColumn: "CourtId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionMatchWindows_Courts_CourtId",
                table: "CompetitionMatchWindows");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionMatchWindows_CourtId",
                table: "CompetitionMatchWindows");

            migrationBuilder.DropColumn(
                name: "CourtId",
                table: "CompetitionMatchWindows");
        }
    }
}
