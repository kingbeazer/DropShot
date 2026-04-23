-- ============================================
-- Step 1 of 2: Insert 500 Random Players
-- ============================================
-- Creates 500 light players with random first/last names, sex, and
-- date of birth (18-75), stamped with CreatedByClubId = @ClubId so
-- Step 2 can find them for the ClubMembers join.
--
-- Uses a temp table (not table variables) for the generated seed rows
-- so the query optimizer has real cardinality info — table variables
-- with 4-way joins + NEWID() produce catastrophic plans on Azure SQL.
-- ============================================

SET NOCOUNT ON;

DECLARE @ClubId INT = 1;
DECLARE @HowMany INT = 500;

IF NOT EXISTS (SELECT 1 FROM Clubs WHERE ClubId = @ClubId)
BEGIN
    RAISERROR('Club with ClubId %d does not exist.', 16, 1, @ClubId);
    RETURN;
END

DECLARE @RunTag NVARCHAR(20) = FORMAT(SYSUTCDATETIME(), 'yyyyMMddHHmmss');

-- ── Age window: 18 to 75 years old ───────────────────────────
DECLARE @MinDob DATE = DATEADD(YEAR, -75, CAST(GETUTCDATE() AS DATE));
DECLARE @DobSpanDays INT = DATEDIFF(DAY, @MinDob, DATEADD(YEAR, -18, CAST(GETUTCDATE() AS DATE))) + 1;

-- ── Name pools (temp tables — real stats) ────────────────────
IF OBJECT_ID('tempdb..#FirstMale')   IS NOT NULL DROP TABLE #FirstMale;
IF OBJECT_ID('tempdb..#FirstFemale') IS NOT NULL DROP TABLE #FirstFemale;
IF OBJECT_ID('tempdb..#Last')        IS NOT NULL DROP TABLE #Last;
IF OBJECT_ID('tempdb..#Seeds')       IS NOT NULL DROP TABLE #Seeds;

CREATE TABLE #FirstMale   (N INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(40));
CREATE TABLE #FirstFemale (N INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(40));
CREATE TABLE #Last        (N INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(40));

INSERT INTO #FirstMale (Name) VALUES
 ('Adam'),('Alex'),('Alfie'),('Arthur'),('Ben'),('Blake'),('Callum'),('Charlie'),
 ('Daniel'),('David'),('Dylan'),('Edward'),('Elliot'),('Ethan'),('Finn'),('Freddie'),
 ('George'),('Harry'),('Henry'),('Hugo'),('Isaac'),('Jack'),('Jacob'),('James'),
 ('Jamie'),('Jasper'),('Joshua'),('Kai'),('Leo'),('Lewis'),('Liam'),('Logan'),
 ('Louis'),('Lucas'),('Luke'),('Marcus'),('Mason'),('Matthew'),('Max'),('Michael'),
 ('Nathan'),('Noah'),('Oliver'),('Oscar'),('Owen'),('Patrick'),('Peter'),('Reuben'),
 ('Richard'),('Robert'),('Ryan'),('Samuel'),('Sebastian'),('Simon'),('Stephen'),('Theo'),
 ('Thomas'),('Toby'),('Tom'),('Victor'),('William'),('Xavier'),('Zach'),('Zain');

INSERT INTO #FirstFemale (Name) VALUES
 ('Alice'),('Amber'),('Amelia'),('Anna'),('Ava'),('Beatrice'),('Beth'),('Charlotte'),
 ('Chloe'),('Claire'),('Daisy'),('Ella'),('Ellie'),('Emily'),('Emma'),('Evelyn'),
 ('Florence'),('Freya'),('Georgia'),('Grace'),('Hannah'),('Harper'),('Holly'),('Iris'),
 ('Isabella'),('Ivy'),('Jasmine'),('Jessica'),('Jodie'),('Katie'),('Keira'),('Layla'),
 ('Lily'),('Lola'),('Lucy'),('Maisie'),('Martha'),('Matilda'),('Maya'),('Megan'),
 ('Mia'),('Millie'),('Molly'),('Nancy'),('Nina'),('Olivia'),('Phoebe'),('Poppy'),
 ('Rachel'),('Rosie'),('Ruby'),('Sarah'),('Sienna'),('Sophia'),('Sophie'),('Summer'),
 ('Tilly'),('Uma'),('Violet'),('Willow'),('Wendy'),('Yasmin'),('Zara'),('Zoe');

INSERT INTO #Last (Name) VALUES
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

DECLARE @MaleCount   INT = (SELECT COUNT(*) FROM #FirstMale);
DECLARE @FemaleCount INT = (SELECT COUNT(*) FROM #FirstFemale);
DECLARE @LastCount   INT = (SELECT COUNT(*) FROM #Last);

-- ── Materialize seed rows into a temp table ──────────────────
-- Scalar subqueries (not joins) keep the plan simple: one lookup per row.
CREATE TABLE #Seeds (
    RowNum      INT           NOT NULL PRIMARY KEY,
    Sex         TINYINT       NOT NULL,
    DateOfBirth DATE          NOT NULL,
    FirstName   NVARCHAR(40)  NOT NULL,
    LastName    NVARCHAR(40)  NOT NULL
);

;WITH
N1 AS (SELECT n FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) v(n)),
N3 AS (SELECT 1 AS x FROM N1 a CROSS JOIN N1 b CROSS JOIN N1 c),
Tally AS (
    SELECT TOP (@HowMany)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS RowNum
    FROM N3
)
INSERT INTO #Seeds (RowNum, Sex, DateOfBirth, FirstName, LastName)
SELECT
    t.RowNum,
    pick.Sex,
    DATEADD(DAY, pick.DobOffset, @MinDob),
    CASE pick.Sex
        WHEN 1 THEN (SELECT Name FROM #FirstMale   WHERE N = pick.MaleIdx)
        ELSE        (SELECT Name FROM #FirstFemale WHERE N = pick.FemaleIdx)
    END,
    (SELECT Name FROM #Last WHERE N = pick.LastIdx)
FROM Tally t
CROSS APPLY (
    SELECT
        CAST(ABS(CHECKSUM(NEWID())) % 2 + 1 AS TINYINT)         AS Sex,
        ABS(CHECKSUM(NEWID())) % @DobSpanDays                    AS DobOffset,
        ABS(CHECKSUM(NEWID())) % @MaleCount   + 1                AS MaleIdx,
        ABS(CHECKSUM(NEWID())) % @FemaleCount + 1                AS FemaleIdx,
        ABS(CHECKSUM(NEWID())) % @LastCount   + 1                AS LastIdx
) pick
OPTION (RECOMPILE);

-- ── Insert into Players ───────────────────────────────────────
INSERT INTO Players
    (DisplayName, FirstName, LastName, Email, Sex, DateOfBirth, IsLight, CreatedAt, CreatedByClubId)
SELECT
    s.FirstName + ' ' + s.LastName,
    s.FirstName,
    s.LastName,
    LOWER(s.FirstName + '.' + s.LastName) + '.' + @RunTag + '.' + CAST(s.RowNum AS VARCHAR(10)) + '@example.com',
    s.Sex,
    s.DateOfBirth,
    1,
    GETUTCDATE(),
    @ClubId
FROM #Seeds s;

DECLARE @Inserted INT = @@ROWCOUNT;

DROP TABLE #FirstMale;
DROP TABLE #FirstFemale;
DROP TABLE #Last;
DROP TABLE #Seeds;

SELECT
    @Inserted                                         AS PlayersInserted,
    @ClubId                                           AS ClubId,
    (SELECT Name FROM Clubs WHERE ClubId = @ClubId)   AS ClubName,
    @RunTag                                           AS RunTag;
