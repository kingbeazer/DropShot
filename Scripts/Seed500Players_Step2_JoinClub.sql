-- ============================================
-- Step 2 of 2: Add Club-Created Players to ClubMembers
-- ============================================
-- Finds every Player with CreatedByClubId = @ClubId who is not yet an
-- active member of that club, and inserts a ClubMembers row.
--
-- Idempotent — safe to run multiple times. If Step 1 is re-run, just
-- re-run this too and it will only add the players that aren't yet
-- joined.
-- ============================================

SET NOCOUNT ON;

DECLARE @ClubId INT = 1;

IF NOT EXISTS (SELECT 1 FROM Clubs WHERE ClubId = @ClubId)
BEGIN
    RAISERROR('Club with ClubId %d does not exist.', 16, 1, @ClubId);
    RETURN;
END

INSERT INTO ClubMembers (ClubId, PlayerId, JoinedAt, IsActive)
SELECT @ClubId, p.PlayerId, GETUTCDATE(), 1
FROM Players p
LEFT JOIN ClubMembers cm
    ON cm.PlayerId = p.PlayerId AND cm.ClubId = @ClubId
WHERE p.CreatedByClubId = @ClubId
  AND cm.PlayerId IS NULL;

SELECT
    @@ROWCOUNT                                                AS PlayersJoined,
    @ClubId                                                   AS ClubId,
    (SELECT Name FROM Clubs WHERE ClubId = @ClubId)           AS ClubName,
    (SELECT COUNT(*) FROM ClubMembers WHERE ClubId = @ClubId) AS TotalClubMembers;
