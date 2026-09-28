using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    public partial class RemoveReminderBoolColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop default constraints before dropping columns (SQL Server requires this).
            migrationBuilder.Sql(@"
                DECLARE @sql NVARCHAR(MAX) = N'';
                SELECT @sql += N'ALTER TABLE CompetitionFixtureReminders DROP CONSTRAINT ' + QUOTENAME(dc.name) + ';'
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                WHERE c.object_id = OBJECT_ID('CompetitionFixtureReminders')
                  AND c.name IN ('SendToCaptainsOnly', 'IncludeResultLink');
                EXEC sp_executesql @sql;
            ");

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
