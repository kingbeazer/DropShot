using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionFixtureAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwayGamesTotal",
                table: "CompetitionFixtures",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwaySetsWon",
                table: "CompetitionFixtures",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeGamesTotal",
                table: "CompetitionFixtures",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeSetsWon",
                table: "CompetitionFixtures",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwayGamesTotal",
                table: "CompetitionFixtures");

            migrationBuilder.DropColumn(
                name: "AwaySetsWon",
                table: "CompetitionFixtures");

            migrationBuilder.DropColumn(
                name: "HomeGamesTotal",
                table: "CompetitionFixtures");

            migrationBuilder.DropColumn(
                name: "HomeSetsWon",
                table: "CompetitionFixtures");
        }
    }
}
