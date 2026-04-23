-- ============================================
-- Seed 50 Players and Add Them to a Club
-- ============================================
-- SET THIS to the ClubId you want to seed players into
DECLARE @ClubId INT = 1;

-- Verify the club exists
IF NOT EXISTS (SELECT 1 FROM Clubs WHERE ClubId = @ClubId)
BEGIN
    RAISERROR('Club with ClubId %d does not exist.', 16, 1, @ClubId);
    RETURN;
END

-- Temp table to hold new PlayerIds
DECLARE @InsertedPlayers TABLE (PlayerId INT);

-- Insert 50 light players (no linked user account)
INSERT INTO Players (DisplayName, FirstName, LastName, Email, Sex, DateOfBirth, IsLight, CreatedAt, CreatedByClubId)
OUTPUT INSERTED.PlayerId INTO @InsertedPlayers
VALUES
    ('Alice Thompson',   'Alice',    'Thompson',   'alice.thompson@example.com',   1, '1990-03-15', 1, GETUTCDATE(), @ClubId),
    ('Ben Carter',       'Ben',      'Carter',     'ben.carter@example.com',       1, '1988-07-22', 1, GETUTCDATE(), @ClubId),
    ('Charlotte Evans',  'Charlotte','Evans',      'charlotte.evans@example.com',  2, '1995-01-10', 1, GETUTCDATE(), @ClubId),
    ('Daniel Hughes',    'Daniel',   'Hughes',     'daniel.hughes@example.com',    1, '1992-11-05', 1, GETUTCDATE(), @ClubId),
    ('Emily Watson',     'Emily',    'Watson',     'emily.watson@example.com',     2, '1991-06-18', 1, GETUTCDATE(), @ClubId),
    ('Freddie Clark',    'Freddie',  'Clark',      'freddie.clark@example.com',    1, '1989-09-30', 1, GETUTCDATE(), @ClubId),
    ('Grace Mitchell',   'Grace',    'Mitchell',   'grace.mitchell@example.com',   2, '1994-04-25', 1, GETUTCDATE(), @ClubId),
    ('Harry Robinson',   'Harry',    'Robinson',   'harry.robinson@example.com',   1, '1993-12-08', 1, GETUTCDATE(), @ClubId),
    ('Isabella Scott',   'Isabella', 'Scott',      'isabella.scott@example.com',   2, '1996-02-14', 1, GETUTCDATE(), @ClubId),
    ('Jack Turner',      'Jack',     'Turner',     'jack.turner@example.com',      1, '1987-08-01', 1, GETUTCDATE(), @ClubId),
    ('Katie Ward',       'Katie',    'Ward',       'katie.ward@example.com',       2, '1990-10-20', 1, GETUTCDATE(), @ClubId),
    ('Liam Bennett',     'Liam',     'Bennett',    'liam.bennett@example.com',     1, '1991-05-12', 1, GETUTCDATE(), @ClubId),
    ('Megan Foster',     'Megan',    'Foster',     'megan.foster@example.com',     2, '1993-07-03', 1, GETUTCDATE(), @ClubId),
    ('Nathan Reed',      'Nathan',   'Reed',       'nathan.reed@example.com',      1, '1988-01-27', 1, GETUTCDATE(), @ClubId),
    ('Olivia Barnes',    'Olivia',   'Barnes',     'olivia.barnes@example.com',    2, '1995-09-16', 1, GETUTCDATE(), @ClubId),
    ('Patrick Cooper',   'Patrick',  'Cooper',     'patrick.cooper@example.com',   1, '1992-03-09', 1, GETUTCDATE(), @ClubId),
    ('Quinn Morgan',     'Quinn',    'Morgan',     'quinn.morgan@example.com',     2, '1994-11-22', 1, GETUTCDATE(), @ClubId),
    ('Ryan Kelly',       'Ryan',     'Kelly',      'ryan.kelly@example.com',       1, '1990-06-05', 1, GETUTCDATE(), @ClubId),
    ('Sophie Price',     'Sophie',   'Price',      'sophie.price@example.com',     2, '1989-12-31', 1, GETUTCDATE(), @ClubId),
    ('Thomas Bailey',    'Thomas',   'Bailey',     'thomas.bailey@example.com',    1, '1991-08-14', 1, GETUTCDATE(), @ClubId),
    ('Uma Patterson',    'Uma',      'Patterson',  'uma.patterson@example.com',    2, '1993-04-07', 1, GETUTCDATE(), @ClubId),
    ('Victor Gray',      'Victor',   'Gray',       'victor.gray@example.com',      1, '1987-10-19', 1, GETUTCDATE(), @ClubId),
    ('Wendy James',      'Wendy',    'James',      'wendy.james@example.com',      2, '1996-01-23', 1, GETUTCDATE(), @ClubId),
    ('Xavier Holmes',    'Xavier',   'Holmes',     'xavier.holmes@example.com',    1, '1992-07-11', 1, GETUTCDATE(), @ClubId),
    ('Yasmin Cole',      'Yasmin',   'Cole',       'yasmin.cole@example.com',      2, '1994-05-28', 1, GETUTCDATE(), @ClubId),
    ('Zach Murray',      'Zach',     'Murray',     'zach.murray@example.com',      1, '1990-02-17', 1, GETUTCDATE(), @ClubId),
    ('Amber Richardson', 'Amber',    'Richardson', 'amber.richardson@example.com', 2, '1991-09-04', 1, GETUTCDATE(), @ClubId),
    ('Blake Sullivan',   'Blake',    'Sullivan',   'blake.sullivan@example.com',   1, '1988-11-13', 1, GETUTCDATE(), @ClubId),
    ('Chloe Howard',     'Chloe',    'Howard',     'chloe.howard@example.com',     2, '1995-03-26', 1, GETUTCDATE(), @ClubId),
    ('Dylan West',       'Dylan',    'West',       'dylan.west@example.com',       1, '1993-06-09', 1, GETUTCDATE(), @ClubId),
    ('Ella Brooks',      'Ella',     'Brooks',     'ella.brooks@example.com',      2, '1989-08-21', 1, GETUTCDATE(), @ClubId),
    ('Finn Sanders',     'Finn',     'Sanders',    'finn.sanders@example.com',     1, '1992-12-02', 1, GETUTCDATE(), @ClubId),
    ('Georgia Long',     'Georgia',  'Long',       'georgia.long@example.com',     2, '1994-10-15', 1, GETUTCDATE(), @ClubId),
    ('Hugo Russell',     'Hugo',     'Russell',    'hugo.russell@example.com',     1, '1991-01-30', 1, GETUTCDATE(), @ClubId),
    ('Iris Griffin',     'Iris',     'Griffin',    'iris.griffin@example.com',     2, '1990-04-11', 1, GETUTCDATE(), @ClubId),
    ('Jake Dixon',       'Jake',     'Dixon',      'jake.dixon@example.com',       1, '1988-06-24', 1, GETUTCDATE(), @ClubId),
    ('Keira Chapman',    'Keira',    'Chapman',    'keira.chapman@example.com',    2, '1996-08-07', 1, GETUTCDATE(), @ClubId),
    ('Leo Grant',        'Leo',      'Grant',      'leo.grant@example.com',        1, '1993-02-19', 1, GETUTCDATE(), @ClubId),
    ('Millie Hart',      'Millie',   'Hart',       'millie.hart@example.com',      2, '1995-05-03', 1, GETUTCDATE(), @ClubId),
    ('Noah Perry',       'Noah',     'Perry',      'noah.perry@example.com',       1, '1989-07-16', 1, GETUTCDATE(), @ClubId),
    ('Phoebe Shaw',      'Phoebe',   'Shaw',       'phoebe.shaw@example.com',      2, '1992-09-28', 1, GETUTCDATE(), @ClubId),
    ('Ruben Harper',     'Ruben',    'Harper',     'ruben.harper@example.com',     1, '1990-11-10', 1, GETUTCDATE(), @ClubId),
    ('Sienna Ellis',     'Sienna',   'Ellis',      'sienna.ellis@example.com',     2, '1994-01-05', 1, GETUTCDATE(), @ClubId),
    ('Toby Marshall',    'Toby',     'Marshall',   'toby.marshall@example.com',    1, '1991-03-18', 1, GETUTCDATE(), @ClubId),
    ('Violet Stone',     'Violet',   'Stone',      'violet.stone@example.com',     2, '1993-08-31', 1, GETUTCDATE(), @ClubId),
    ('Will Pearce',      'Will',     'Pearce',     'will.pearce@example.com',      1, '1987-12-14', 1, GETUTCDATE(), @ClubId),
    ('Zara Fleming',     'Zara',     'Fleming',    'zara.fleming@example.com',     2, '1995-06-27', 1, GETUTCDATE(), @ClubId),
    ('Adam Doyle',       'Adam',     'Doyle',      'adam.doyle@example.com',       1, '1992-04-08', 1, GETUTCDATE(), @ClubId),
    ('Beth Chambers',    'Beth',     'Chambers',   'beth.chambers@example.com',    2, '1990-10-01', 1, GETUTCDATE(), @ClubId),
    ('Callum Watts',     'Callum',   'Watts',      'callum.watts@example.com',     1, '1988-02-23', 1, GETUTCDATE(), @ClubId);

-- Add all new players as active club members
INSERT INTO ClubMembers (ClubId, PlayerId, JoinedAt, IsActive)
SELECT @ClubId, PlayerId, GETUTCDATE(), 1
FROM @InsertedPlayers;

-- Summary
SELECT
    (SELECT COUNT(*) FROM @InsertedPlayers) AS PlayersCreated,
    @ClubId AS ClubId,
    (SELECT Name FROM Clubs WHERE ClubId = @ClubId) AS ClubName;
