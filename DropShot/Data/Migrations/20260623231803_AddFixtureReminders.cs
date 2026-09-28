using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFixtureReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guarded SQL handles the case where a previous partial run already
            // created the column or tables before the migration was recorded.

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'[CompetitionFixtures]')
                      AND name = N'ResultSubmissionToken')
                BEGIN
                    ALTER TABLE [CompetitionFixtures] ADD [ResultSubmissionToken] uniqueidentifier NULL;
                END");

            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[CompetitionFixtureReminders]') IS NULL
                BEGIN
                    CREATE TABLE [CompetitionFixtureReminders] (
                        [CompetitionFixtureReminderId] int NOT NULL IDENTITY,
                        [CompetitionId] int NOT NULL,
                        [HoursBefore] int NOT NULL,
                        [Subject] nvarchar(max) NOT NULL,
                        [Body] nvarchar(max) NOT NULL,
                        [IncludeResultLink] bit NOT NULL,
                        CONSTRAINT [PK_CompetitionFixtureReminders] PRIMARY KEY ([CompetitionFixtureReminderId]),
                        CONSTRAINT [FK_CompetitionFixtureReminders_Competition_CompetitionId]
                            FOREIGN KEY ([CompetitionId]) REFERENCES [Competition] ([CompetitionID]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_CompetitionFixtureReminders_CompetitionId]
                        ON [CompetitionFixtureReminders] ([CompetitionId]);
                END");

            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[CompetitionFixtureReminderLogs]') IS NULL
                BEGIN
                    CREATE TABLE [CompetitionFixtureReminderLogs] (
                        [CompetitionFixtureReminderLogId] int NOT NULL IDENTITY,
                        [CompetitionFixtureReminderId] int NOT NULL,
                        [CompetitionFixtureId] int NOT NULL,
                        [SentAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_CompetitionFixtureReminderLogs] PRIMARY KEY ([CompetitionFixtureReminderLogId]),
                        CONSTRAINT [FK_CompetitionFixtureReminderLogs_CompetitionFixtureReminders_CompetitionFixtureReminderId]
                            FOREIGN KEY ([CompetitionFixtureReminderId])
                            REFERENCES [CompetitionFixtureReminders] ([CompetitionFixtureReminderId]) ON DELETE CASCADE,
                        CONSTRAINT [FK_CompetitionFixtureReminderLogs_CompetitionFixtures_CompetitionFixtureId]
                            FOREIGN KEY ([CompetitionFixtureId])
                            REFERENCES [CompetitionFixtures] ([CompetitionFixtureId]) ON DELETE NO ACTION
                    );
                    CREATE INDEX [IX_CompetitionFixtureReminderLogs_CompetitionFixtureId]
                        ON [CompetitionFixtureReminderLogs] ([CompetitionFixtureId]);
                    CREATE INDEX [IX_CompetitionFixtureReminderLogs_CompetitionFixtureReminderId]
                        ON [CompetitionFixtureReminderLogs] ([CompetitionFixtureReminderId]);
                END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionFixtureReminderLogs");

            migrationBuilder.DropTable(
                name: "CompetitionFixtureReminders");

            migrationBuilder.DropColumn(
                name: "ResultSubmissionToken",
                table: "CompetitionFixtures");
        }
    }
}
