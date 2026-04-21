using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionRubberTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RubberTemplateKey",
                table: "Competition",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompetitionRubberTemplates",
                columns: table => new
                {
                    CompetitionRubberTemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionRubberTemplates", x => x.CompetitionRubberTemplateId);
                    table.ForeignKey(
                        name: "FK_CompetitionRubberTemplates_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "CompetitionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RubberTemplateRubbers",
                columns: table => new
                {
                    RubberTemplateRubberId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionRubberTemplateId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CourtNumber = table.Column<int>(type: "int", nullable: false),
                    HomeRolesJson = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AwayRolesJson = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RubberTemplateRubbers", x => x.RubberTemplateRubberId);
                    table.ForeignKey(
                        name: "FK_RubberTemplateRubbers_CompetitionRubberTemplates_CompetitionRubberTemplateId",
                        column: x => x.CompetitionRubberTemplateId,
                        principalTable: "CompetitionRubberTemplates",
                        principalColumn: "CompetitionRubberTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionRubberTemplates_CompetitionId",
                table: "CompetitionRubberTemplates",
                column: "CompetitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RubberTemplateRubbers_CompetitionRubberTemplateId",
                table: "RubberTemplateRubbers",
                column: "CompetitionRubberTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RubberTemplateRubbers");
            migrationBuilder.DropTable(name: "CompetitionRubberTemplates");
            migrationBuilder.DropColumn(name: "RubberTemplateKey", table: "Competition");
        }
    }
}
