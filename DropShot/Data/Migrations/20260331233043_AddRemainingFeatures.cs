using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Migrations
{
    /// <inheritdoc />
    public partial class AddRemainingFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClubEmailTemplates",
                columns: table => new
                {
                    ClubEmailTemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClubId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubEmailTemplates", x => x.ClubEmailTemplateId);
                    table.ForeignKey(
                        name: "FK_ClubEmailTemplates_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "ClubId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionAdmins",
                columns: table => new
                {
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionAdmins", x => new { x.CompetitionId, x.UserId });
                    table.ForeignKey(
                        name: "FK_CompetitionAdmins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionAdmins_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "CompetitionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionTemplates",
                columns: table => new
                {
                    CompetitionTemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClubId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Format = table.Column<byte>(type: "tinyint", nullable: true),
                    RulesSetId = table.Column<int>(type: "int", nullable: true),
                    BestOf = table.Column<int>(type: "int", nullable: true),
                    MaxAge = table.Column<int>(type: "int", nullable: true),
                    EligibleSex = table.Column<byte>(type: "tinyint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionTemplates", x => x.CompetitionTemplateId);
                    table.ForeignKey(
                        name: "FK_CompetitionTemplates_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "ClubId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionTemplateWindows",
                columns: table => new
                {
                    CompetitionTemplateWindowId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionTemplateId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionTemplateWindows", x => x.CompetitionTemplateWindowId);
                    table.ForeignKey(
                        name: "FK_CompetitionTemplateWindows_CompetitionTemplates_CompetitionTemplateId",
                        column: x => x.CompetitionTemplateId,
                        principalTable: "CompetitionTemplates",
                        principalColumn: "CompetitionTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubEmailTemplates_ClubId",
                table: "ClubEmailTemplates",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionAdmins_UserId",
                table: "CompetitionAdmins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionTemplates_ClubId",
                table: "CompetitionTemplates",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionTemplateWindows_CompetitionTemplateId",
                table: "CompetitionTemplateWindows",
                column: "CompetitionTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubEmailTemplates");

            migrationBuilder.DropTable(
                name: "CompetitionAdmins");

            migrationBuilder.DropTable(
                name: "CompetitionTemplateWindows");

            migrationBuilder.DropTable(
                name: "CompetitionTemplates");
        }
    }
}
