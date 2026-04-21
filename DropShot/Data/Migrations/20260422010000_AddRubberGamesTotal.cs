using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRubberGamesTotal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HomeGamesTotal",
                table: "Rubbers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayGamesTotal",
                table: "Rubbers",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "HomeGamesTotal", table: "Rubbers");
            migrationBuilder.DropColumn(name: "AwayGamesTotal", table: "Rubbers");
        }
    }
}
