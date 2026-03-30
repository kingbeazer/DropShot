-- ============================================================
-- DropShot – Seed 20 random players
-- Run against your SQL Server database after applying all
-- EF Core migrations (dotnet ef database update).
--
-- Idempotent: uses MERGE to avoid duplicate inserts.
-- Players are inserted with UserId = NULL (unlinked accounts
-- that can be connected to an identity user later).
-- ============================================================

MERGE INTO Players AS target
USING (VALUES
  -- DisplayName,       FirstName,   LastName,     Email,                              MobileNumber,  Sex, DateOfBirth
  ('James Thornton',   'James',     'Thornton',   'james.thornton@example.com',       '07700900001', 1,   '1990-04-12'),
  ('Sarah Mitchell',   'Sarah',     'Mitchell',   'sarah.mitchell@example.com',       '07700900002', 2,   '1994-08-23'),
  ('Oliver Patel',     'Oliver',    'Patel',      'oliver.patel@example.com',         '07700900003', 1,   '1988-11-05'),
  ('Emma Clarke',      'Emma',      'Clarke',     'emma.clarke@example.com',          '07700900004', 2,   '1996-02-17'),
  ('William Hassan',   'William',   'Hassan',     'william.hassan@example.com',       '07700900005', 1,   '1985-07-30'),
  ('Chloe Robertson',  'Chloe',     'Robertson',  'chloe.robertson@example.com',      '07700900006', 2,   '1999-05-09'),
  ('Harry Singh',      'Harry',     'Singh',      'harry.singh@example.com',          '07700900007', 1,   '1993-12-22'),
  ('Lucy Williamson',  'Lucy',      'Williamson', 'lucy.williamson@example.com',      '07700900008', 2,   '1991-03-14'),
  ('George Campbell',  'George',    'Campbell',   'george.campbell@example.com',      '07700900009', 1,   '1987-09-01'),
  ('Poppy Evans',      'Poppy',     'Evans',      'poppy.evans@example.com',          '07700900010', 2,   '2000-06-28'),
  ('Ethan Murray',     'Ethan',     'Murray',     'ethan.murray@example.com',         '07700900011', 1,   '1995-01-19'),
  ('Isabella King',    'Isabella',  'King',       'isabella.king@example.com',        '07700900012', 2,   '1992-10-07'),
  ('Jack Fletcher',    'Jack',      'Fletcher',   'jack.fletcher@example.com',        '07700900013', 1,   '1989-04-25'),
  ('Mia Harrison',     'Mia',       'Harrison',   'mia.harrison@example.com',         '07700900014', 2,   '1997-08-11'),
  ('Noah Cooper',      'Noah',      'Cooper',     'noah.cooper@example.com',          '07700900015', 1,   '1984-02-03'),
  ('Amelia Scott',     'Amelia',    'Scott',      'amelia.scott@example.com',         '07700900016', 2,   '1998-12-16'),
  ('Liam Turner',      'Liam',      'Turner',     'liam.turner@example.com',          '07700900017', 1,   '2001-07-20'),
  ('Sophie Adams',     'Sophie',    'Adams',      'sophie.adams@example.com',         '07700900018', 2,   '1986-11-29'),
  ('Benjamin Ward',    'Benjamin',  'Ward',       'benjamin.ward@example.com',        '07700900019', 1,   '1983-05-04'),
  ('Charlotte Hughes', 'Charlotte', 'Hughes',     'charlotte.hughes@example.com',     '07700900020', 2,   '2002-09-13')
) AS source (DisplayName, FirstName, LastName, Email, MobileNumber, Sex, DateOfBirth)
ON target.Email = source.Email

WHEN NOT MATCHED BY TARGET THEN
    INSERT (DisplayName, FirstName, LastName, Email, MobileNumber, Sex, DateOfBirth, UserId, CreatedAt)
    VALUES (
        source.DisplayName,
        source.FirstName,
        source.LastName,
        source.Email,
        source.MobileNumber,
        source.Sex,
        CAST(source.DateOfBirth AS date),
        NULL,            -- no linked identity user account
        GETUTCDATE()
    )

WHEN MATCHED THEN
    -- Update mobile number and name in case they have changed
    UPDATE SET
        DisplayName  = source.DisplayName,
        FirstName    = source.FirstName,
        LastName     = source.LastName,
        MobileNumber = source.MobileNumber,
        Sex          = source.Sex,
        DateOfBirth  = CAST(source.DateOfBirth AS date);

-- Report how many rows were affected
SELECT
    COUNT(*) AS TotalPlayers,
    SUM(CASE WHEN MobileNumber IS NOT NULL THEN 1 ELSE 0 END) AS PlayersWithMobile
FROM Players;
