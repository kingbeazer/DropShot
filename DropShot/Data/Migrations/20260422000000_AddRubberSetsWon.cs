using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRubberSetsWon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HomeSetsWon",
                table: "Rubbers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwaySetsWon",
                table: "Rubbers",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "HomeSetsWon", table: "Rubbers");
            migrationBuilder.DropColumn(name: "AwaySetsWon", table: "Rubbers");
        }
    }
}
