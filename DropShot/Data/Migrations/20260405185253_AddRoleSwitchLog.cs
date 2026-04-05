using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleSwitchLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoleSwitchLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FromRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleSwitchLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoleSwitchLogs_Timestamp",
                table: "RoleSwitchLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_RoleSwitchLogs_UserId",
                table: "RoleSwitchLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleSwitchLogs");
        }
    }
}
