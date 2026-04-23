-- ============================================
-- Seed 500 Random Players into a Club
-- ============================================
-- Creates 500 light players with random first/last names, random sex
-- (roughly 50/50), and random dates of birth (ages 18-75), then joins
-- them all to @ClubId as active ClubMembers.
--
-- Safe to re-run: each batch uses a unique email suffix derived from
-- the current UTC timestamp so seeds never collide with prior runs.
-- ============================================

SET NOCOUNT ON;

DECLARE @ClubId INT = 1;
DECLARE @HowMany INT = 500;

IF NOT EXISTS (SELECT 1 FROM Clubs WHERE ClubId = @ClubId)
BEGIN
    RAISERROR('Club with ClubId %d does not exist.', 16, 1, @ClubId);
    RETURN;
END

-- Unique-per-run suffix so re-runs don't conflict on email uniqueness
DECLARE @RunTag NVARCHAR(20) = FORMAT(SYSUTCDATETIME(), 'yyyyMMddHHmmss');

DECLARE @InsertedPlayers TABLE (PlayerId INT);

-- ── Name pools ────────────────────────────────────────────────
DECLARE @FirstMale TABLE (N INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(40));
INSERT INTO @FirstMale (Name) VALUES
 ('Adam'),('Alex'),('Alfie'),('Arthur'),('Ben'),('Blake'),('Callum'),('Charlie'),
 ('Daniel'),('David'),('Dylan'),('Edward'),('Elliot'),('Ethan'),('Finn'),('Freddie'),
 ('George'),('Harry'),('Henry'),('Hugo'),('Isaac'),('Jack'),('Jacob'),('James'),
 ('Jamie'),('Jasper'),('Joshua'),('Kai'),('Leo'),('Lewis'),('Liam'),('Logan'),
 ('Louis'),('Lucas'),('Luke'),('Marcus'),('Mason'),('Matthew'),('Max'),('Michael'),
 ('Nathan'),('Noah'),('Oliver'),('Oscar'),('Owen'),('Patrick'),('Peter'),('Reuben'),
 ('Richard'),('Robert'),('Ryan'),('Samuel'),('Sebastian'),('Simon'),('Stephen'),('Theo'),
 ('Thomas'),('Toby'),('Tom'),('Victor'),('William'),('Xavier'),('Zach'),('Zain');

DECLARE @FirstFemale TABLE (N INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(40));
INSERT INTO @FirstFemale (Name) VALUES
 ('Alice'),('Amber'),('Amelia'),('Anna'),('Ava'),('Beatrice'),('Beth'),('Charlotte'),
 ('Chloe'),('Claire'),('Daisy'),('Ella'),('Ellie'),('Emily'),('Emma'),('Evelyn'),
 ('Florence'),('Freya'),('Georgia'),('Grace'),('Hannah'),('Harper'),('Holly'),('Iris'),
 ('Isabella'),('Ivy'),('Jasmine'),('Jessica'),('Jodie'),('Katie'),('Keira'),('Layla'),
 ('Lily'),('Lola'),('Lucy'),('Maisie'),('Martha'),('Matilda'),('Maya'),('Megan'),
 ('Mia'),('Millie'),('Molly'),('Nancy'),('Nina'),('Olivia'),('Phoebe'),('Poppy'),
 ('Rachel'),('Rosie'),('Ruby'),('Sarah'),('Sienna'),('Sophia'),('Sophie'),('Summer'),
 ('Tilly'),('Uma'),('Violet'),('Willow'),('Wendy'),('Yasmin'),('Zara'),('Zoe');

DECLARE @Last TABLE (N INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(40));
INSERT INTO @Last (Name) VALUES
 ('Adams'),('Allen'),('Bailey'),('Baker'),('Barnes'),('Bennett'),('Brooks'),('Brown'),
 ('Campbell'),('Carter'),('Chambers'),('Chapman'),('Clark'),('Cole'),('Collins'),('Cooper'),
 ('Davies'),('Davis'),('Dixon'),('Doyle'),('Edwards'),('Ellis'),('Evans'),('Fisher'),
 ('Fleming'),('Foster'),('Gibson'),('Grant'),('Gray'),('Green'),('Griffin'),('Hall'),
 ('Harper'),('Harris'),('Harrison'),('Hart'),('Hill'),('Holmes'),('Howard'),('Hughes'),
 ('Hunter'),('James'),('Jenkins'),('Johnson'),('Jones'),('Kelly'),('Kennedy'),('King'),
 ('Knight'),('Lawrence'),('Lee'),('Lewis'),('Long'),('Marshall'),('Mason'),('Mitchell'),
 ('Morgan'),('Morris'),('Murphy'),('Murray'),('Nelson'),('Owen'),('Parker'),('Patel'),
 ('Patterson'),('Pearce'),('Perry'),('Phillips'),('Powell'),('Price'),('Reed'),('Richardson'),
 ('Roberts'),('Robinson'),('Russell'),('Sanders'),('Scott'),('Shaw'),('Smith'),('Stone'),
 ('Sullivan'),('Taylor'),('Thompson'),('Turner'),('Walker'),('Ward'),('Watson'),('Watts'),
 ('West'),('White'),('Williams'),('Wilson'),('Wood'),('Wright'),('Young');

DECLARE @MaleCount   INT = (SELECT COUNT(*) FROM @FirstMale);
DECLARE @FemaleCount INT = (SELECT COUNT(*) FROM @FirstFemale);
DECLARE @LastCount   INT = (SELECT COUNT(*) FROM @Last);

-- ── Age window: 18 to 75 years old, inclusive ────────────────
DECLARE @MinDob DATE = DATEADD(YEAR, -75, CAST(GETUTCDATE() AS DATE));
DECLARE @MaxDob DATE = DATEADD(YEAR, -18, CAST(GETUTCDATE() AS DATE));
DECLARE @DobSpanDays INT = DATEDIFF(DAY, @MinDob, @MaxDob) + 1;

-- ── Generate and insert ───────────────────────────────────────
;WITH Tally AS (
    SELECT TOP (@HowMany)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS RowNum
    FROM sys.all_objects
),
Picked AS (
    SELECT
        t.RowNum,
        CAST(ABS(CHECKSUM(NEWID())) % 2 + 1 AS TINYINT)   AS Sex,
        DATEADD(DAY, ABS(CHECKSUM(NEWID())) % @DobSpanDays, @MinDob) AS DateOfBirth,
        ABS(CHECKSUM(NEWID())) % @MaleCount   + 1 AS MaleIdx,
        ABS(CHECKSUM(NEWID())) % @FemaleCount + 1 AS FemaleIdx,
        ABS(CHECKSUM(NEWID())) % @LastCount   + 1 AS LastIdx
    FROM Tally t
),
Named AS (
    SELECT
        p.RowNum,
        p.Sex,
        p.DateOfBirth,
        CASE p.Sex WHEN 1 THEN m.Name ELSE f.Name END AS FirstName,
        l.Name AS LastName
    FROM Picked p
    INNER JOIN @FirstMale   m ON m.N = p.MaleIdx
    INNER JOIN @FirstFemale f ON f.N = p.FemaleIdx
    INNER JOIN @Last        l ON l.N = p.LastIdx
)
INSERT INTO Players
    (DisplayName, FirstName, LastName, Email, Sex, DateOfBirth, IsLight, CreatedAt, CreatedByClubId)
OUTPUT INSERTED.PlayerId INTO @InsertedPlayers (PlayerId)
SELECT
    n.FirstName + ' ' + n.LastName,
    n.FirstName,
    n.LastName,
    LOWER(n.FirstName + '.' + n.LastName) + '.' + @RunTag + '.' + CAST(n.RowNum AS VARCHAR(10)) + '@example.com',
    n.Sex,
    n.DateOfBirth,
    1,
    GETUTCDATE(),
    @ClubId
FROM Named n;

-- ── Join all new players to the club ──────────────────────────
INSERT INTO ClubMembers (ClubId, PlayerId, JoinedAt, IsActive)
SELECT @ClubId, PlayerId, GETUTCDATE(), 1
FROM @InsertedPlayers;

-- ── Summary ──────────────────────────────────────────────────
SELECT
    (SELECT COUNT(*) FROM @InsertedPlayers)                         AS PlayersCreated,
    @ClubId                                                          AS ClubId,
    (SELECT Name FROM Clubs WHERE ClubId = @ClubId)                  AS ClubName,
    (SELECT COUNT(*) FROM ClubMembers WHERE ClubId = @ClubId)        AS TotalClubMembers;
