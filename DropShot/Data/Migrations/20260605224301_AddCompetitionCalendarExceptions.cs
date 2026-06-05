using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionCalendarExceptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompetitionCalendarExceptions",
                columns: table => new
                {
                    CompetitionCalendarExceptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    CompetitionDivisionId = table.Column<int>(type: "int", nullable: true),
                    ExceptionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionCalendarExceptions", x => x.CompetitionCalendarExceptionId);
                    table.ForeignKey(
                        name: "FK_CompetitionCalendarExceptions_CompetitionDivisions_CompetitionDivisionId",
                        column: x => x.CompetitionDivisionId,
                        principalTable: "CompetitionDivisions",
                        principalColumn: "CompetitionDivisionId");
                    table.ForeignKey(
                        name: "FK_CompetitionCalendarExceptions_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "CompetitionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCalendarExceptions_CompetitionDivisionId",
                table: "CompetitionCalendarExceptions",
                column: "CompetitionDivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCalendarExceptions_CompetitionId_CompetitionDivisionId_ExceptionDate",
                table: "CompetitionCalendarExceptions",
                columns: new[] { "CompetitionId", "CompetitionDivisionId", "ExceptionDate" },
                unique: true,
                filter: "[CompetitionDivisionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionCalendarExceptions");
        }
    }
}
