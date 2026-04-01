using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchBehaviourFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceToken",
                table: "SavedMatch",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "SavedMatch",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultGameScoring",
                table: "Players",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceToken",
                table: "SavedMatch");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "SavedMatch");

            migrationBuilder.DropColumn(
                name: "DefaultGameScoring",
                table: "Players");
        }
    }
}
