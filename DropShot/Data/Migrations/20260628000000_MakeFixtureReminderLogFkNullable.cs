using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MyDbContext))]
    [Migration("20260628000000_MakeFixtureReminderLogFkNullable")]
    public partial class MakeFixtureReminderLogFkNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Allow null so log rows can record emails sent via the default template
            // (when no custom CompetitionFixtureReminder is configured).
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = N'FK_CompetitionFixtureReminderLogs_CompetitionFixtureReminders_CompetitionFixtureReminderId')
                BEGIN
                    ALTER TABLE [CompetitionFixtureReminderLogs]
                        DROP CONSTRAINT [FK_CompetitionFixtureReminderLogs_CompetitionFixtureReminders_CompetitionFixtureReminderId];
                END");

            migrationBuilder.Sql(@"
                ALTER TABLE [CompetitionFixtureReminderLogs]
                    ALTER COLUMN [CompetitionFixtureReminderId] int NULL;");

            migrationBuilder.Sql(@"
                ALTER TABLE [CompetitionFixtureReminderLogs]
                    ADD CONSTRAINT [FK_CompetitionFixtureReminderLogs_CompetitionFixtureReminders_CompetitionFixtureReminderId]
                    FOREIGN KEY ([CompetitionFixtureReminderId])
                    REFERENCES [CompetitionFixtureReminders] ([CompetitionFixtureReminderId]) ON DELETE SET NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove rows that used the default (null FK) before reverting
            migrationBuilder.Sql(@"
                DELETE FROM [CompetitionFixtureReminderLogs]
                WHERE [CompetitionFixtureReminderId] IS NULL;");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = N'FK_CompetitionFixtureReminderLogs_CompetitionFixtureReminders_CompetitionFixtureReminderId')
                BEGIN
                    ALTER TABLE [CompetitionFixtureReminderLogs]
                        DROP CONSTRAINT [FK_CompetitionFixtureReminderLogs_CompetitionFixtureReminders_CompetitionFixtureReminderId];
                END");

            migrationBuilder.Sql(@"
                ALTER TABLE [CompetitionFixtureReminderLogs]
                    ALTER COLUMN [CompetitionFixtureReminderId] int NOT NULL;");

            migrationBuilder.Sql(@"
                ALTER TABLE [CompetitionFixtureReminderLogs]
                    ADD CONSTRAINT [FK_CompetitionFixtureReminderLogs_CompetitionFixtureReminders_CompetitionFixtureReminderId]
                    FOREIGN KEY ([CompetitionFixtureReminderId])
                    REFERENCES [CompetitionFixtureReminders] ([CompetitionFixtureReminderId]) ON DELETE CASCADE;");
        }
    }
}
