using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <summary>
    /// Normalise <c>CompetitionFixtures.ResultSummary</c> /
    /// <c>OriginalResultSummary</c> onto the canonical set-list format
    /// (space-separated, en-dash between scores: <c>"6–3 3–6 6–2"</c>).
    ///
    /// Pre-2026-05-06 the live-scoring path wrote comma-separated hyphenated
    /// summaries (<c>"6-3, 3-6, 6-2"</c>); the Submit Score dialog and the
    /// fixture simulator wrote the canonical form. The home-page recent-
    /// results card only knew how to parse the canonical form, so legacy
    /// rows rendered as one giant overflowing cell. From the same release
    /// all three writers produce the canonical form.
    ///
    /// The <c>LIKE '%, %'</c> filter is the marker for legacy rows:
    /// canonical rows have no comma+space, and team-aggregate summaries
    /// (single rubber-count pair like <c>"3-2"</c>) also have no
    /// comma+space and so are correctly skipped.
    ///
    /// Idempotent: re-running is a no-op once the LIKE filter no longer
    /// matches. <c>Down()</c> is intentionally a no-op — once normalised
    /// we can't reliably tell which rows were originally legacy versus
    /// natively canonical, so reversion would corrupt newly-written rows.
    /// </summary>
    public partial class NormaliseResultSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE CompetitionFixtures
                SET    ResultSummary = REPLACE(REPLACE(ResultSummary, '-', N'–'), ', ', ' ')
                WHERE  ResultSummary LIKE '%, %';");

            migrationBuilder.Sql(@"
                UPDATE CompetitionFixtures
                SET    OriginalResultSummary = REPLACE(REPLACE(OriginalResultSummary, '-', N'–'), ', ', ' ')
                WHERE  OriginalResultSummary LIKE '%, %';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: see class summary. Canonical and natively-canonical
            // rows are indistinguishable post-Up, so a blanket reverse
            // would mangle data written after this migration ran.
        }
    }
}
