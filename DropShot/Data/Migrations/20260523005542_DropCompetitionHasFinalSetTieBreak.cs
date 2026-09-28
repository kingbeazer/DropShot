using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropCompetitionHasFinalSetTieBreak : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasFinalSetTieBreak",
                table: "Competition");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasFinalSetTieBreak",
                table: "Competition",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
