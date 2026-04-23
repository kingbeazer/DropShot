-- ============================================
-- Seed Teams + Participants for a 4-Division TeamMatch Competition
-- ============================================
-- For the given @CompetitionId (which must have divisions already
-- created), this script:
--   1. Creates @TeamsPerDivision teams per division.
--   2. Picks 2 male + 2 female players from ClubId = @ClubId who are
--      not already participants in this competition.
--   3. Inserts CompetitionParticipant rows with the team + division
--      links and the Mixed Team Tennis roles MA / MB / FA / FB.
--
-- Defaults match the Mixed Team Tennis preset (4 players per team,
-- 2 male + 2 female). Adjust the variables at the top to taste.
-- ============================================

SET NOCOUNT ON;

DECLARE @CompetitionId     INT = 1;   -- ← your 4-division TeamMatch competition
DECLARE @ClubId            INT = 1;
DECLARE @TeamsPerDivision  INT = 8;   -- 8 × 4 divisions = 32 teams
DECLARE @MalesPerTeam      INT = 2;
DECLARE @FemalesPerTeam    INT = 2;

-- ── Sanity checks ────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Competition WHERE CompetitionID = @CompetitionId)
BEGIN
    RAISERROR('Competition %d does not exist.', 16, 1, @CompetitionId);
    RETURN;
END

DECLARE @DivisionCount INT = (SELECT COUNT(*) FROM CompetitionDivisions WHERE CompetitionId = @CompetitionId);
IF @DivisionCount = 0
BEGIN
    RAISERROR('Competition %d has no divisions — create them first.', 16, 1, @CompetitionId);
    RETURN;
END

DECLARE @TeamsTotal    INT = @DivisionCount * @TeamsPerDivision;
DECLARE @MalesNeeded   INT = @TeamsTotal * @MalesPerTeam;
DECLARE @FemalesNeeded INT = @TeamsTotal * @FemalesPerTeam;

-- ── Available player pools (ClubMembers of @ClubId, not yet in this comp) ──
DECLARE @AvailableMales TABLE (
    Seq INT IDENTITY(1,1) PRIMARY KEY,
    PlayerId INT NOT NULL
);
DECLARE @AvailableFemales TABLE (
    Seq INT IDENTITY(1,1) PRIMARY KEY,
    PlayerId INT NOT NULL
);

INSERT INTO @AvailableMales (PlayerId)
SELECT TOP (@MalesNeeded) p.PlayerId
FROM Players p
INNER JOIN ClubMembers cm ON cm.PlayerId = p.PlayerId AND cm.ClubId = @ClubId AND cm.IsActive = 1
LEFT JOIN CompetitionParticipants cp
    ON cp.PlayerId = p.PlayerId AND cp.CompetitionId = @CompetitionId
WHERE p.Sex = 1
  AND cp.PlayerId IS NULL
ORDER BY NEWID();

INSERT INTO @AvailableFemales (PlayerId)
SELECT TOP (@FemalesNeeded) p.PlayerId
FROM Players p
INNER JOIN ClubMembers cm ON cm.PlayerId = p.PlayerId AND cm.ClubId = @ClubId AND cm.IsActive = 1
LEFT JOIN CompetitionParticipants cp
    ON cp.PlayerId = p.PlayerId AND cp.CompetitionId = @CompetitionId
WHERE p.Sex = 2
  AND cp.PlayerId IS NULL
ORDER BY NEWID();

DECLARE @MalesFound   INT = (SELECT COUNT(*) FROM @AvailableMales);
DECLARE @FemalesFound INT = (SELECT COUNT(*) FROM @AvailableFemales);

IF @MalesFound < @MalesNeeded OR @FemalesFound < @FemalesNeeded
BEGIN
    DECLARE @msg NVARCHAR(400) = FORMATMESSAGE(
        N'Not enough club members to fill %d teams. Need %d males (have %d) and %d females (have %d). Top up the club first.',
        @TeamsTotal, @MalesNeeded, @MalesFound, @FemalesNeeded, @FemalesFound);
    RAISERROR(@msg, 16, 1);
    RETURN;
END

-- ── Create teams, one per (division × slot) ──────────────────
-- We track the generated CompetitionTeamId alongside the slot index so
-- we can slice the player pools deterministically afterwards.
DECLARE @NewTeams TABLE (
    TeamSlot INT IDENTITY(1,1) PRIMARY KEY,   -- 1 .. @TeamsTotal, global order
    DivisionRank TINYINT NOT NULL,
    DivisionId INT NOT NULL,
    DivisionSlot INT NOT NULL,                 -- 1 .. @TeamsPerDivision within the division
    CompetitionTeamId INT NULL
);

INSERT INTO @NewTeams (DivisionRank, DivisionId, DivisionSlot)
SELECT d.Rank, d.CompetitionDivisionId, n.Num
FROM CompetitionDivisions d
CROSS JOIN (
    SELECT TOP (@TeamsPerDivision)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Num
    FROM sys.all_objects
) n
WHERE d.CompetitionId = @CompetitionId
ORDER BY d.Rank, n.Num;

-- Insert one team row per planned slot; capture the generated ids.
DECLARE @TeamSlot INT = 1;
WHILE @TeamSlot <= @TeamsTotal
BEGIN
    DECLARE @Rank TINYINT, @DivisionId INT, @DivisionSlot INT;
    SELECT @Rank = DivisionRank, @DivisionId = DivisionId, @DivisionSlot = DivisionSlot
    FROM @NewTeams WHERE TeamSlot = @TeamSlot;

    DECLARE @TeamName NVARCHAR(100) =
        N'Div ' + CAST(@Rank AS NVARCHAR(2)) + N' — Team ' + CAST(@DivisionSlot AS NVARCHAR(3));

    DECLARE @NewId INT;
    INSERT INTO CompetitionTeams (CompetitionId, Name, CompetitionDivisionId)
    VALUES (@CompetitionId, @TeamName, @DivisionId);
    SET @NewId = SCOPE_IDENTITY();

    UPDATE @NewTeams SET CompetitionTeamId = @NewId WHERE TeamSlot = @TeamSlot;
    SET @TeamSlot += 1;
END

-- ── Assign participants, 2 male + 2 female per team ──────────
-- Each team consumes a contiguous slice of each pool, with fixed roles.
INSERT INTO CompetitionParticipants
    (CompetitionId, PlayerId, RegisteredAt, Status, TeamId, CompetitionDivisionId, Role)
SELECT
    @CompetitionId,
    m.PlayerId,
    GETUTCDATE(),
    2,                                             -- Confirmed
    t.CompetitionTeamId,
    t.DivisionId,
    CASE ((m.Seq - 1) % @MalesPerTeam) WHEN 0 THEN N'MA' ELSE N'MB' END
FROM @AvailableMales m
INNER JOIN @NewTeams t
    ON t.TeamSlot = ((m.Seq - 1) / @MalesPerTeam) + 1;

INSERT INTO CompetitionParticipants
    (CompetitionId, PlayerId, RegisteredAt, Status, TeamId, CompetitionDivisionId, Role)
SELECT
    @CompetitionId,
    f.PlayerId,
    GETUTCDATE(),
    2,                                             -- Confirmed
    t.CompetitionTeamId,
    t.DivisionId,
    CASE ((f.Seq - 1) % @FemalesPerTeam) WHEN 0 THEN N'FA' ELSE N'FB' END
FROM @AvailableFemales f
INNER JOIN @NewTeams t
    ON t.TeamSlot = ((f.Seq - 1) / @FemalesPerTeam) + 1;

-- ── Optional: make the first participant of each team its captain ──
UPDATE ct
SET ct.CaptainPlayerId = first_member.PlayerId
FROM CompetitionTeams ct
INNER JOIN @NewTeams nt ON nt.CompetitionTeamId = ct.CompetitionTeamId
CROSS APPLY (
    SELECT TOP 1 cp.PlayerId
    FROM CompetitionParticipants cp
    WHERE cp.CompetitionId = @CompetitionId
      AND cp.TeamId = ct.CompetitionTeamId
    ORDER BY cp.Role             -- FA, FB, MA, MB → alphabetically FA is first
) first_member
WHERE ct.CaptainPlayerId IS NULL;

-- ── Summary ──────────────────────────────────────────────────
SELECT
    @CompetitionId                              AS CompetitionId,
    @DivisionCount                              AS DivisionsUsed,
    @TeamsTotal                                 AS TeamsCreated,
    @MalesNeeded + @FemalesNeeded               AS ParticipantsAssigned;

SELECT d.Rank,
       d.Name                                   AS DivisionName,
       COUNT(DISTINCT ct.CompetitionTeamId)     AS Teams,
       COUNT(cp.PlayerId)                       AS Participants
FROM CompetitionDivisions d
LEFT JOIN CompetitionTeams ct
    ON ct.CompetitionDivisionId = d.CompetitionDivisionId
LEFT JOIN CompetitionParticipants cp
    ON cp.CompetitionDivisionId = d.CompetitionDivisionId
WHERE d.CompetitionId = @CompetitionId
GROUP BY d.Rank, d.Name
ORDER BY d.Rank;
