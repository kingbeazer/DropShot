using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSinglesLadderSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "EloRating",
                table: "CompetitionParticipants",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "IsProvisional",
                table: "CompetitionParticipants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMatchAt",
                table: "CompetitionParticipants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchesPlayed",
                table: "CompetitionParticipants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Player1RatingAfter",
                table: "CompetitionFixtures",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Player1RatingBefore",
                table: "CompetitionFixtures",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Player2RatingAfter",
                table: "CompetitionFixtures",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Player2RatingBefore",
                table: "CompetitionFixtures",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LadderKFactor",
                table: "Competition",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "LadderProvisionalMatches",
                table: "Competition",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "LadderStartingRating",
                table: "Competition",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "LadderUseMarginOfVictory",
                table: "Competition",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EloRating",
                table: "CompetitionParticipants");

            migrationBuilder.DropColumn(
                name: "IsProvisional",
                table: "CompetitionParticipants");

            migrationBuilder.DropColumn(
                name: "LastMatchAt",
                table: "CompetitionParticipants");

            migrationBuilder.DropColumn(
                name: "MatchesPlayed",
                table: "CompetitionParticipants");

            migrationBuilder.DropColumn(
                name: "Player1RatingAfter",
                table: "CompetitionFixtures");

            migrationBuilder.DropColumn(
                name: "Player1RatingBefore",
                table: "CompetitionFixtures");

            migrationBuilder.DropColumn(
                name: "Player2RatingAfter",
                table: "CompetitionFixtures");

            migrationBuilder.DropColumn(
                name: "Player2RatingBefore",
                table: "CompetitionFixtures");

            migrationBuilder.DropColumn(
                name: "LadderKFactor",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "LadderProvisionalMatches",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "LadderStartingRating",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "LadderUseMarginOfVictory",
                table: "Competition");
        }
    }
}
