using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFixtureReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResultSubmissionToken",
                table: "CompetitionFixtures",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompetitionFixtureReminders",
                columns: table => new
                {
                    CompetitionFixtureReminderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    HoursBefore = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IncludeResultLink = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionFixtureReminders", x => x.CompetitionFixtureReminderId);
                    table.ForeignKey(
                        name: "FK_CompetitionFixtureReminders_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "CompetitionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionFixtureReminderLogs",
                columns: table => new
                {
                    CompetitionFixtureReminderLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionFixtureReminderId = table.Column<int>(type: "int", nullable: false),
                    CompetitionFixtureId = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionFixtureReminderLogs", x => x.CompetitionFixtureReminderLogId);
                    table.ForeignKey(
                        name: "FK_CompetitionFixtureReminderLogs_CompetitionFixtureReminders_CompetitionFixtureReminderId",
                        column: x => x.CompetitionFixtureReminderId,
                        principalTable: "CompetitionFixtureReminders",
                        principalColumn: "CompetitionFixtureReminderId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionFixtureReminderLogs_CompetitionFixtures_CompetitionFixtureId",
                        column: x => x.CompetitionFixtureId,
                        principalTable: "CompetitionFixtures",
                        principalColumn: "CompetitionFixtureId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionFixtureReminderLogs_CompetitionFixtureId",
                table: "CompetitionFixtureReminderLogs",
                column: "CompetitionFixtureId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionFixtureReminderLogs_CompetitionFixtureReminderId",
                table: "CompetitionFixtureReminderLogs",
                column: "CompetitionFixtureReminderId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionFixtureReminders_CompetitionId",
                table: "CompetitionFixtureReminders",
                column: "CompetitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionFixtureReminderLogs");

            migrationBuilder.DropTable(
                name: "CompetitionFixtureReminders");

            migrationBuilder.DropColumn(
                name: "ResultSubmissionToken",
                table: "CompetitionFixtures");
        }
    }
}
