using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Migrations
{
    /// <inheritdoc />
    public partial class AddResultVerificationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalResultSummary",
                table: "CompetitionFixtures",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginalWinnerPlayerId",
                table: "CompetitionFixtures",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ResultModifiedByAdmin",
                table: "CompetitionFixtures",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalResultSummary",
                table: "CompetitionFixtures");

            migrationBuilder.DropColumn(
                name: "OriginalWinnerPlayerId",
                table: "CompetitionFixtures");

            migrationBuilder.DropColumn(
                name: "ResultModifiedByAdmin",
                table: "CompetitionFixtures");
        }
    }
}
