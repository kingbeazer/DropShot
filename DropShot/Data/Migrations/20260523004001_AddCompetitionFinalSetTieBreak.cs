using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionFinalSetTieBreak : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FinalSetTieBreakGames",
                table: "Competition",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte>(
                name: "FinalSetTieBreakWinMode",
                table: "Competition",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<bool>(
                name: "HasFinalSetTieBreak",
                table: "Competition",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalSetTieBreakGames",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "FinalSetTieBreakWinMode",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "HasFinalSetTieBreak",
                table: "Competition");
        }
    }
}
