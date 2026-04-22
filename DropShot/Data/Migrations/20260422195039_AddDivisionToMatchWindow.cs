using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDivisionToMatchWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent — same pattern as ReplaceLeaguesWithCompetitionDivisions.
            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'[CompetitionMatchWindows]', N'CompetitionDivisionId') IS NULL
                    ALTER TABLE [CompetitionMatchWindows] ADD [CompetitionDivisionId] int NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CompetitionMatchWindows_CompetitionDivisionId' AND object_id = OBJECT_ID(N'[CompetitionMatchWindows]'))
                    CREATE INDEX [IX_CompetitionMatchWindows_CompetitionDivisionId] ON [CompetitionMatchWindows] ([CompetitionDivisionId]);

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CompetitionMatchWindows_CompetitionDivisions_CompetitionDivisionId')
                    ALTER TABLE [CompetitionMatchWindows]
                        ADD CONSTRAINT [FK_CompetitionMatchWindows_CompetitionDivisions_CompetitionDivisionId]
                        FOREIGN KEY ([CompetitionDivisionId])
                        REFERENCES [CompetitionDivisions] ([CompetitionDivisionId])
                        ON DELETE NO ACTION;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionMatchWindows_CompetitionDivisions_CompetitionDivisionId",
                table: "CompetitionMatchWindows");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionMatchWindows_CompetitionDivisionId",
                table: "CompetitionMatchWindows");

            migrationBuilder.DropColumn(
                name: "CompetitionDivisionId",
                table: "CompetitionMatchWindows");
        }
    }
}
