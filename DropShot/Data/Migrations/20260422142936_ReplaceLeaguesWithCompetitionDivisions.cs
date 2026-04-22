using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceLeaguesWithCompetitionDivisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tear down the league structures from AddLeagueStructures. Use
            // defensive IF EXISTS guards so this works against databases where
            // the prior migration was partially applied or never ran (e.g.
            // staging DBs whose history table was reconciled by hand).
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Competition_LeagueDivisions_LeagueDivisionId')
                    ALTER TABLE [Competition] DROP CONSTRAINT [FK_Competition_LeagueDivisions_LeagueDivisionId];
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Competition_LeagueDivisionId' AND object_id = OBJECT_ID(N'[Competition]'))
                    DROP INDEX [IX_Competition_LeagueDivisionId] ON [Competition];
                IF COL_LENGTH(N'[Competition]', N'LeagueDivisionId') IS NOT NULL
                    ALTER TABLE [Competition] DROP COLUMN [LeagueDivisionId];
                IF OBJECT_ID(N'[LeagueDivisions]', N'U') IS NOT NULL DROP TABLE [LeagueDivisions];
                IF OBJECT_ID(N'[LeagueMemberships]', N'U') IS NOT NULL DROP TABLE [LeagueMemberships];
                IF OBJECT_ID(N'[LeagueSeasons]', N'U') IS NOT NULL DROP TABLE [LeagueSeasons];
                IF OBJECT_ID(N'[Leagues]', N'U') IS NOT NULL DROP TABLE [Leagues];
            ");

            // ── New: divisions live inside a Competition ──
            // Each step is guarded so a retry after a partial run converges.
            // Self-referencing FK uses NO ACTION (SQL Server forbids SET NULL
            // / CASCADE on self-references because of cycle / multi-path rules).
            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'[Competition]', N'HasDivisions') IS NULL
                    ALTER TABLE [Competition] ADD [HasDivisions] bit NOT NULL CONSTRAINT DF_Competition_HasDivisions DEFAULT CAST(0 AS bit);
                IF COL_LENGTH(N'[Competition]', N'SeededFromCompetitionId') IS NULL
                    ALTER TABLE [Competition] ADD [SeededFromCompetitionId] int NULL;
                IF COL_LENGTH(N'[CompetitionParticipants]', N'CompetitionDivisionId') IS NULL
                    ALTER TABLE [CompetitionParticipants] ADD [CompetitionDivisionId] int NULL;
                IF COL_LENGTH(N'[CompetitionTeams]', N'CompetitionDivisionId') IS NULL
                    ALTER TABLE [CompetitionTeams] ADD [CompetitionDivisionId] int NULL;

                IF OBJECT_ID(N'[CompetitionDivisions]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [CompetitionDivisions] (
                        [CompetitionDivisionId] int NOT NULL IDENTITY,
                        [CompetitionId] int NOT NULL,
                        [Rank] tinyint NOT NULL,
                        [Name] nvarchar(80) NOT NULL,
                        CONSTRAINT [PK_CompetitionDivisions] PRIMARY KEY ([CompetitionDivisionId]),
                        CONSTRAINT [FK_CompetitionDivisions_Competition_CompetitionId] FOREIGN KEY ([CompetitionId]) REFERENCES [Competition] ([CompetitionID]) ON DELETE CASCADE
                    );
                END;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Competition_SeededFromCompetitionId' AND object_id = OBJECT_ID(N'[Competition]'))
                    CREATE INDEX [IX_Competition_SeededFromCompetitionId] ON [Competition] ([SeededFromCompetitionId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CompetitionDivisions_CompetitionId_Rank' AND object_id = OBJECT_ID(N'[CompetitionDivisions]'))
                    CREATE UNIQUE INDEX [IX_CompetitionDivisions_CompetitionId_Rank] ON [CompetitionDivisions] ([CompetitionId], [Rank]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CompetitionParticipants_CompetitionDivisionId' AND object_id = OBJECT_ID(N'[CompetitionParticipants]'))
                    CREATE INDEX [IX_CompetitionParticipants_CompetitionDivisionId] ON [CompetitionParticipants] ([CompetitionDivisionId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CompetitionTeams_CompetitionDivisionId' AND object_id = OBJECT_ID(N'[CompetitionTeams]'))
                    CREATE INDEX [IX_CompetitionTeams_CompetitionDivisionId] ON [CompetitionTeams] ([CompetitionDivisionId]);

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Competition_Competition_SeededFromCompetitionId')
                    ALTER TABLE [Competition] ADD CONSTRAINT [FK_Competition_Competition_SeededFromCompetitionId] FOREIGN KEY ([SeededFromCompetitionId]) REFERENCES [Competition] ([CompetitionID]) ON DELETE NO ACTION;
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CompetitionParticipants_CompetitionDivisions_CompetitionDivisionId')
                    ALTER TABLE [CompetitionParticipants] ADD CONSTRAINT [FK_CompetitionParticipants_CompetitionDivisions_CompetitionDivisionId] FOREIGN KEY ([CompetitionDivisionId]) REFERENCES [CompetitionDivisions] ([CompetitionDivisionId]) ON DELETE NO ACTION;
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CompetitionTeams_CompetitionDivisions_CompetitionDivisionId')
                    ALTER TABLE [CompetitionTeams] ADD CONSTRAINT [FK_CompetitionTeams_CompetitionDivisions_CompetitionDivisionId] FOREIGN KEY ([CompetitionDivisionId]) REFERENCES [CompetitionDivisions] ([CompetitionDivisionId]) ON DELETE NO ACTION;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Competition_Competition_SeededFromCompetitionId",
                table: "Competition");

            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionParticipants_CompetitionDivisions_CompetitionDivisionId",
                table: "CompetitionParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionTeams_CompetitionDivisions_CompetitionDivisionId",
                table: "CompetitionTeams");

            migrationBuilder.DropTable(name: "CompetitionDivisions");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionTeams_CompetitionDivisionId",
                table: "CompetitionTeams");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionParticipants_CompetitionDivisionId",
                table: "CompetitionParticipants");

            migrationBuilder.DropIndex(
                name: "IX_Competition_SeededFromCompetitionId",
                table: "Competition");

            migrationBuilder.DropColumn(name: "CompetitionDivisionId", table: "CompetitionTeams");
            migrationBuilder.DropColumn(name: "CompetitionDivisionId", table: "CompetitionParticipants");
            migrationBuilder.DropColumn(name: "SeededFromCompetitionId", table: "Competition");
            migrationBuilder.DropColumn(name: "HasDivisions", table: "Competition");

            // Recreate the league tables (matches the original AddLeagueStructures).
            migrationBuilder.CreateTable(
                name: "Leagues",
                columns: table => new
                {
                    LeagueId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HostClubId = table.Column<int>(type: "int", nullable: false),
                    CompetitionFormat = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GamesPerSet = table.Column<int>(type: "int", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    LeagueScoring = table.Column<byte>(type: "tinyint", nullable: false),
                    MatchFormat = table.Column<byte>(type: "tinyint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NumberOfSets = table.Column<int>(type: "int", nullable: false),
                    RubberTemplateKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SetWinMode = table.Column<byte>(type: "tinyint", nullable: false),
                    TeamSize = table.Column<int>(type: "int", nullable: false),
                    TeamsPerDivisionMin = table.Column<int>(type: "int", nullable: false),
                    TeamsPerDivisionTarget = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leagues", x => x.LeagueId);
                    table.ForeignKey(
                        name: "FK_Leagues_Clubs_HostClubId",
                        column: x => x.HostClubId,
                        principalTable: "Clubs",
                        principalColumn: "ClubId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeagueMemberships",
                columns: table => new
                {
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    CurrentDivisionRank = table.Column<byte>(type: "tinyint", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueMemberships", x => new { x.LeagueId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_LeagueMemberships_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "LeagueId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeagueMemberships_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueSeasons",
                columns: table => new
                {
                    LeagueSeasonId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueSeasons", x => x.LeagueSeasonId);
                    table.ForeignKey(
                        name: "FK_LeagueSeasons_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "LeagueId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<int>(
                name: "LeagueDivisionId",
                table: "Competition",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeagueDivisions",
                columns: table => new
                {
                    LeagueDivisionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    LeagueSeasonId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Rank = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueDivisions", x => x.LeagueDivisionId);
                    table.ForeignKey(
                        name: "FK_LeagueDivisions_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "CompetitionID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeagueDivisions_LeagueSeasons_LeagueSeasonId",
                        column: x => x.LeagueSeasonId,
                        principalTable: "LeagueSeasons",
                        principalColumn: "LeagueSeasonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Competition_LeagueDivisionId",
                table: "Competition",
                column: "LeagueDivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueDivisions_CompetitionId",
                table: "LeagueDivisions",
                column: "CompetitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueDivisions_LeagueSeasonId_Rank",
                table: "LeagueDivisions",
                columns: new[] { "LeagueSeasonId", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueMemberships_PlayerId",
                table: "LeagueMemberships",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_HostClubId",
                table: "Leagues",
                column: "HostClubId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueSeasons_LeagueId_Name",
                table: "LeagueSeasons",
                columns: new[] { "LeagueId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Competition_LeagueDivisions_LeagueDivisionId",
                table: "Competition",
                column: "LeagueDivisionId",
                principalTable: "LeagueDivisions",
                principalColumn: "LeagueDivisionId",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
