using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchFormatAndLeagueScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "LeagueScoring",
                table: "Competition",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "MatchFormat",
                table: "Competition",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfSets",
                table: "Competition",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeagueScoring",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "MatchFormat",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "NumberOfSets",
                table: "Competition");
        }
    }
}
