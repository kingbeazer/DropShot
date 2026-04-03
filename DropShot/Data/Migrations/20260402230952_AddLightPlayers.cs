using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Migrations
{
    /// <inheritdoc />
    public partial class AddLightPlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Players",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLight",
                table: "Players",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Players_CreatedByUserId",
                table: "Players",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_AspNetUsers_CreatedByUserId",
                table: "Players",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_AspNetUsers_CreatedByUserId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_CreatedByUserId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "IsLight",
                table: "Players");
        }
    }
}
