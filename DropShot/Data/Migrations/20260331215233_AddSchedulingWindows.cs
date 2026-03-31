using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingWindows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClubSchedulingTemplates",
                columns: table => new
                {
                    ClubSchedulingTemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClubId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubSchedulingTemplates", x => x.ClubSchedulingTemplateId);
                    table.ForeignKey(
                        name: "FK_ClubSchedulingTemplates_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "ClubId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionMatchWindows",
                columns: table => new
                {
                    CompetitionMatchWindowId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionMatchWindows", x => x.CompetitionMatchWindowId);
                    table.ForeignKey(
                        name: "FK_CompetitionMatchWindows_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "CompetitionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClubSchedulingTemplateWindows",
                columns: table => new
                {
                    ClubSchedulingTemplateWindowId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClubSchedulingTemplateId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubSchedulingTemplateWindows", x => x.ClubSchedulingTemplateWindowId);
                    table.ForeignKey(
                        name: "FK_ClubSchedulingTemplateWindows_ClubSchedulingTemplates_ClubSchedulingTemplateId",
                        column: x => x.ClubSchedulingTemplateId,
                        principalTable: "ClubSchedulingTemplates",
                        principalColumn: "ClubSchedulingTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubSchedulingTemplates_ClubId",
                table: "ClubSchedulingTemplates",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubSchedulingTemplateWindows_ClubSchedulingTemplateId",
                table: "ClubSchedulingTemplateWindows",
                column: "ClubSchedulingTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionMatchWindows_CompetitionId",
                table: "CompetitionMatchWindows",
                column: "CompetitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubSchedulingTemplateWindows");

            migrationBuilder.DropTable(
                name: "CompetitionMatchWindows");

            migrationBuilder.DropTable(
                name: "ClubSchedulingTemplates");
        }
    }
}
