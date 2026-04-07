using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Migrations
{
    /// <inheritdoc />
    public partial class AddClubIdToRulesSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the column as nullable so existing rows can be backfilled.
            migrationBuilder.AddColumn<int>(
                name: "ClubId",
                table: "RulesSets",
                type: "int",
                nullable: true);

            // 2. Backfill existing RulesSets:
            //    - Prefer the HostClubId most commonly referenced by linked Competitions.
            //    - Fall back to the first available Club row.
            migrationBuilder.Sql(@"
                UPDATE rs
                SET rs.ClubId = x.ClubId
                FROM RulesSets rs
                CROSS APPLY (
                    SELECT TOP 1 c.HostClubId AS ClubId
                    FROM Competitions c
                    WHERE c.RulesSetId = rs.RulesSetId
                      AND c.HostClubId IS NOT NULL
                    GROUP BY c.HostClubId
                    ORDER BY COUNT(*) DESC
                ) x
                WHERE rs.ClubId IS NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE RulesSets
                SET ClubId = (SELECT TOP 1 ClubId FROM Clubs ORDER BY ClubId)
                WHERE ClubId IS NULL
                  AND EXISTS (SELECT 1 FROM Clubs);
            ");

            // 3. Drop any RulesSets that still have no club (only possible if no clubs exist at all).
            migrationBuilder.Sql("DELETE FROM RulesSets WHERE ClubId IS NULL;");

            // 4. Make the column NOT NULL.
            migrationBuilder.AlterColumn<int>(
                name: "ClubId",
                table: "RulesSets",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // 5. Index + FK.
            migrationBuilder.CreateIndex(
                name: "IX_RulesSets_ClubId",
                table: "RulesSets",
                column: "ClubId");

            migrationBuilder.AddForeignKey(
                name: "FK_RulesSets_Clubs_ClubId",
                table: "RulesSets",
                column: "ClubId",
                principalTable: "Clubs",
                principalColumn: "ClubId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RulesSets_Clubs_ClubId",
                table: "RulesSets");

            migrationBuilder.DropIndex(
                name: "IX_RulesSets_ClubId",
                table: "RulesSets");

            migrationBuilder.DropColumn(
                name: "ClubId",
                table: "RulesSets");
        }
    }
}
