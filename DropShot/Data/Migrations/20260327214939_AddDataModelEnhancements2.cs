using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DropShot.Migrations
{
    /// <inheritdoc />
    public partial class AddDataModelEnhancements2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_AspNetUsers_UserId",
                table: "Players");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Players",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Players",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "ContactPreferences",
                table: "Players",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Players",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Players",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Players",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileImagePath",
                table: "Players",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Sex",
                table: "Players",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CompetitionName",
                table: "Competition",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<byte>(
                name: "EligibleSex",
                table: "Competition",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Competition",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HostClubId",
                table: "Competition",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAge",
                table: "Competition",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxParticipants",
                table: "Competition",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RulesSetId",
                table: "Competition",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Competition",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Clubs",
                columns: table => new
                {
                    ClubId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Town = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clubs", x => x.ClubId);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionParticipants",
                columns: table => new
                {
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionParticipants", x => new { x.CompetitionId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_CompetitionParticipants_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "CompetitionID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionParticipants_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionStages",
                columns: table => new
                {
                    CompetitionStageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StageOrder = table.Column<int>(type: "int", nullable: false),
                    StageType = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionStages", x => x.CompetitionStageId);
                    table.ForeignKey(
                        name: "FK_CompetitionStages_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "CompetitionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerFriends",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    FriendPlayerId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerFriends", x => new { x.PlayerId, x.FriendPlayerId });
                    table.ForeignKey(
                        name: "FK_PlayerFriends_Players_FriendPlayerId",
                        column: x => x.FriendPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerFriends_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RulesSets",
                columns: table => new
                {
                    RulesSetId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RulesSets", x => x.RulesSetId);
                });

            migrationBuilder.CreateTable(
                name: "ClubMembers",
                columns: table => new
                {
                    ClubId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubMembers", x => new { x.ClubId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_ClubMembers_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "ClubId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClubMembers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Courts",
                columns: table => new
                {
                    CourtId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClubId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Surface = table.Column<byte>(type: "tinyint", nullable: false),
                    IsIndoor = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courts", x => x.CourtId);
                    table.ForeignKey(
                        name: "FK_Courts_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "ClubId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClubLadders",
                columns: table => new
                {
                    ClubLadderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClubId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RulesSetId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    KFactor = table.Column<double>(type: "float", nullable: false),
                    InactivityDeductionDays = table.Column<int>(type: "int", nullable: false),
                    InactivityDeductionPoints = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubLadders", x => x.ClubLadderId);
                    table.ForeignKey(
                        name: "FK_ClubLadders_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "ClubId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClubLadders_RulesSets_RulesSetId",
                        column: x => x.RulesSetId,
                        principalTable: "RulesSets",
                        principalColumn: "RulesSetId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RulesSetItems",
                columns: table => new
                {
                    RulesSetItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RulesSetId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    RuleText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RulesSetItems", x => x.RulesSetItemId);
                    table.ForeignKey(
                        name: "FK_RulesSetItems_RulesSets_RulesSetId",
                        column: x => x.RulesSetId,
                        principalTable: "RulesSets",
                        principalColumn: "RulesSetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionFixtures",
                columns: table => new
                {
                    CompetitionFixtureId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    CompetitionStageId = table.Column<int>(type: "int", nullable: true),
                    CourtId = table.Column<int>(type: "int", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Player1Id = table.Column<int>(type: "int", nullable: true),
                    Player2Id = table.Column<int>(type: "int", nullable: true),
                    Player3Id = table.Column<int>(type: "int", nullable: true),
                    Player4Id = table.Column<int>(type: "int", nullable: true),
                    SavedMatchId = table.Column<int>(type: "int", nullable: true),
                    ResultSummary = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WinnerPlayerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionFixtures", x => x.CompetitionFixtureId);
                    table.ForeignKey(
                        name: "FK_CompetitionFixtures_CompetitionStages_CompetitionStageId",
                        column: x => x.CompetitionStageId,
                        principalTable: "CompetitionStages",
                        principalColumn: "CompetitionStageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionFixtures_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "CompetitionID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionFixtures_Courts_CourtId",
                        column: x => x.CourtId,
                        principalTable: "Courts",
                        principalColumn: "CourtId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CompetitionFixtures_Players_Player1Id",
                        column: x => x.Player1Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionFixtures_Players_Player2Id",
                        column: x => x.Player2Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionFixtures_Players_Player3Id",
                        column: x => x.Player3Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionFixtures_Players_Player4Id",
                        column: x => x.Player4Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionFixtures_SavedMatch_SavedMatchId",
                        column: x => x.SavedMatchId,
                        principalTable: "SavedMatch",
                        principalColumn: "SavedMatchId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LadderEntries",
                columns: table => new
                {
                    LadderEntryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClubLadderId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    EloRating = table.Column<double>(type: "float", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    LastMatchAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LadderEntries", x => x.LadderEntryId);
                    table.ForeignKey(
                        name: "FK_LadderEntries_ClubLadders_ClubLadderId",
                        column: x => x.ClubLadderId,
                        principalTable: "ClubLadders",
                        principalColumn: "ClubLadderId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LadderEntries_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Competition_HostClubId",
                table: "Competition",
                column: "HostClubId");

            migrationBuilder.CreateIndex(
                name: "IX_Competition_RulesSetId",
                table: "Competition",
                column: "RulesSetId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubLadders_ClubId",
                table: "ClubLadders",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubLadders_RulesSetId",
                table: "ClubLadders",
                column: "RulesSetId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubMembers_PlayerId",
                table: "ClubMembers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionFixtures_CompetitionId",
                table: "CompetitionFixtures",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionFixtures_CompetitionStageId",
                table: "CompetitionFixtures",
                column: "CompetitionStageId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionFixtures_CourtId",
                table: "CompetitionFixtures",
                column: "CourtId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionFixtures_Player1Id",
                table: "CompetitionFixtures",
                column: "Player1Id");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionFixtures_Player2Id",
                table: "CompetitionFixtures",
                column: "Player2Id");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionFixtures_Player3Id",
                table: "CompetitionFixtures",
                column: "Player3Id");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionFixtures_Player4Id",
                table: "CompetitionFixtures",
                column: "Player4Id");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionFixtures_SavedMatchId",
                table: "CompetitionFixtures",
                column: "SavedMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionParticipants_PlayerId",
                table: "CompetitionParticipants",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionStages_CompetitionId",
                table: "CompetitionStages",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Courts_ClubId",
                table: "Courts",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_LadderEntries_ClubLadderId_PlayerId",
                table: "LadderEntries",
                columns: new[] { "ClubLadderId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LadderEntries_PlayerId",
                table: "LadderEntries",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerFriends_FriendPlayerId",
                table: "PlayerFriends",
                column: "FriendPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_RulesSetItems_RulesSetId",
                table: "RulesSetItems",
                column: "RulesSetId");

            migrationBuilder.AddForeignKey(
                name: "FK_Competition_Clubs_HostClubId",
                table: "Competition",
                column: "HostClubId",
                principalTable: "Clubs",
                principalColumn: "ClubId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Competition_RulesSets_RulesSetId",
                table: "Competition",
                column: "RulesSetId",
                principalTable: "RulesSets",
                principalColumn: "RulesSetId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_AspNetUsers_UserId",
                table: "Players",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Competition_Clubs_HostClubId",
                table: "Competition");

            migrationBuilder.DropForeignKey(
                name: "FK_Competition_RulesSets_RulesSetId",
                table: "Competition");

            migrationBuilder.DropForeignKey(
                name: "FK_Players_AspNetUsers_UserId",
                table: "Players");

            migrationBuilder.DropTable(
                name: "ClubMembers");

            migrationBuilder.DropTable(
                name: "CompetitionFixtures");

            migrationBuilder.DropTable(
                name: "CompetitionParticipants");

            migrationBuilder.DropTable(
                name: "LadderEntries");

            migrationBuilder.DropTable(
                name: "PlayerFriends");

            migrationBuilder.DropTable(
                name: "RulesSetItems");

            migrationBuilder.DropTable(
                name: "CompetitionStages");

            migrationBuilder.DropTable(
                name: "Courts");

            migrationBuilder.DropTable(
                name: "ClubLadders");

            migrationBuilder.DropTable(
                name: "Clubs");

            migrationBuilder.DropTable(
                name: "RulesSets");

            migrationBuilder.DropIndex(
                name: "IX_Competition_HostClubId",
                table: "Competition");

            migrationBuilder.DropIndex(
                name: "IX_Competition_RulesSetId",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "ContactPreferences",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "ProfileImagePath",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Sex",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "EligibleSex",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "HostClubId",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "MaxAge",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "MaxParticipants",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "RulesSetId",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Competition");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Players",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Players",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "CompetitionName",
                table: "Competition",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_AspNetUsers_UserId",
                table: "Players",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
