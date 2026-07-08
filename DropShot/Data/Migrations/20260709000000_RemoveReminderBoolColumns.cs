using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    [DbContext(typeof(MyDbContext))]
    [Migration("20260709000000_RemoveReminderBoolColumns")]
    public partial class RemoveReminderBoolColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('CompetitionFixtureReminders')
                      AND name = 'SendToCaptainsOnly'
                )
                BEGIN
                    ALTER TABLE CompetitionFixtureReminders DROP COLUMN SendToCaptainsOnly;
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('CompetitionFixtureReminders')
                      AND name = 'IncludeResultLink'
                )
                BEGIN
                    ALTER TABLE CompetitionFixtureReminders DROP COLUMN IncludeResultLink;
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('CompetitionFixtureReminders')
                      AND name = 'SendToCaptainsOnly'
                )
                BEGIN
                    ALTER TABLE CompetitionFixtureReminders
                    ADD SendToCaptainsOnly BIT NOT NULL DEFAULT 1;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('CompetitionFixtureReminders')
                      AND name = 'IncludeResultLink'
                )
                BEGIN
                    ALTER TABLE CompetitionFixtureReminders
                    ADD IncludeResultLink BIT NOT NULL DEFAULT 1;
                END
            ");
        }
    }
}
