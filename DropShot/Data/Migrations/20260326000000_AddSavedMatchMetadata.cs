using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedMatchMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MatchJson",
                table: "SavedMatch",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Player1",
                table: "SavedMatch",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Player2",
                table: "SavedMatch",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Player3",
                table: "SavedMatch",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Player4",
                table: "SavedMatch",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WinnerName",
                table: "SavedMatch",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "SavedMatch",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Player1", table: "SavedMatch");
            migrationBuilder.DropColumn(name: "Player2", table: "SavedMatch");
            migrationBuilder.DropColumn(name: "Player3", table: "SavedMatch");
            migrationBuilder.DropColumn(name: "Player4", table: "SavedMatch");
            migrationBuilder.DropColumn(name: "WinnerName", table: "SavedMatch");
            migrationBuilder.DropColumn(name: "CreatedAt", table: "SavedMatch");

            migrationBuilder.AlterColumn<string>(
                name: "MatchJson",
                table: "SavedMatch",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
