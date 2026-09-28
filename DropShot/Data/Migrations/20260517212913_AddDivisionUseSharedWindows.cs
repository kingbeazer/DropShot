using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDivisionUseSharedWindows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing divisions get UseSharedMatchWindows = true so we don't
            // suddenly start treating them as misconfigured: that matches
            // pre-migration behaviour (a division with no per-division windows
            // implicitly falls back to the competition's shared windows).
            migrationBuilder.AddColumn<bool>(
                name: "UseSharedMatchWindows",
                table: "CompetitionDivisions",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UseSharedMatchWindows",
                table: "CompetitionDivisions");
        }
    }
}
