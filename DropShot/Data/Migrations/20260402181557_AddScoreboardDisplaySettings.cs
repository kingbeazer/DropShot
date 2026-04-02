using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Migrations
{
    /// <inheritdoc />
    public partial class AddScoreboardDisplaySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScoreboardDisplaySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourtId = table.Column<int>(type: "int", nullable: false),
                    LiveStreamUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ShowLiveStream = table.Column<bool>(type: "bit", nullable: false),
                    Layout = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "default"),
                    Fullscreen = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreboardDisplaySettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoreboardDisplaySettings_Courts_CourtId",
                        column: x => x.CourtId,
                        principalTable: "Courts",
                        principalColumn: "CourtId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreboardDisplaySettings_CourtId",
                table: "ScoreboardDisplaySettings",
                column: "CourtId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScoreboardDisplaySettings");
        }
    }
}
