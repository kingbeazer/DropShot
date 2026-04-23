-- ============================================
-- Seed UK Tennis Clubs with Courts
-- ============================================
-- Sources: LTA directory, tenniscourtsmap.com, Wikipedia, club websites

-- Temp table to capture inserted ClubIds
DECLARE @InsertedClubs TABLE (ClubId INT, Name NVARCHAR(200));

-- Insert clubs
INSERT INTO Clubs (Name, AddressLine1, AddressLine2, Town, Postcode, Phone, Email, Website)
OUTPUT INSERTED.ClubId, INSERTED.Name INTO @InsertedClubs
VALUES
    -- London
    ('All England Lawn Tennis and Croquet Club', 'Church Road', 'Wimbledon', 'London', 'SW19 5AE', '020 8944 1066', 'info@aeltc.com', 'https://www.wimbledon.com'),
    ('Queen''s Club', 'Palliser Road', 'Barons Court', 'London', 'W14 9EQ', '020 7386 3400', 'info@queensclub.co.uk', 'https://www.queensclub.co.uk'),
    ('Roehampton Club', '43 Roehampton Lane', NULL, 'London', 'SW15 5LT', '020 8480 4200', 'info@roehamptonclub.co.uk', 'https://www.roehamptonclub.co.uk'),
    ('Cumberland Lawn Tennis Club', '25 Alvanley Gardens', 'West Hampstead', 'London', 'NW6 1JD', '020 7435 4805', NULL, 'https://cltc-hcc.com'),
    ('Holland Park Lawn Tennis Club', '1 Addison Road', 'Holland Park', 'London', 'W14 8DU', '020 7602 2226', NULL, 'https://www.hpltc.co.uk'),
    ('Sheen Parks Tennis', 'Richmond Park Academy', 'Park Avenue', 'London', 'SW14 8RG', '07917 844294', NULL, NULL),
    ('Bank of England LTC', 'Bank of England Sports Ground', 'Priory Lane', 'London', 'SW15 5JQ', '020 8392 4360', NULL, 'https://www.ourtennis.co.uk'),
    ('Campden Hill Lawn Tennis Club', '9 Aubrey Walk', 'Kensington', 'London', 'W8 7JH', '020 7727 3994', NULL, 'https://www.chltc.co.uk'),
    ('West Middlesex Lawn Tennis Club', '6 Berners Drive', 'Drayton Bridge Road', 'London', 'W13 0JS', '020 8998 3297', NULL, NULL),

    -- South East
    ('St George''s Hill Lawn Tennis Club', 'Warreners Lane', 'St George''s Hill', 'Weybridge', 'KT13 0LL', '01932 843658', NULL, 'https://stghltc.co.uk'),
    ('Surbiton Racket and Fitness Club', 'Berrylands', NULL, 'Surbiton', 'KT5 8JT', '020 8399 0456', NULL, 'https://www.surbiton.org'),
    ('Devonshire Park Lawn Tennis Club', 'College Road', 'Devonshire Park', 'Eastbourne', 'BN21 4JJ', '01323 410 090', NULL, 'https://www.devonshireparkltc.co.uk'),
    ('Thames Ditton Lawn Tennis Club', 'Summer Road', NULL, 'Thames Ditton', 'KT7 0QR', '020 8398 1336', NULL, NULL),
    ('Canterbury Tennis Club', 'Polo Farm Sports Club', 'New Dover Road', 'Canterbury', 'CT1 3NQ', '01227 830060', NULL, NULL),

    -- South West & West
    ('Bath Tennis Club', 'Park Lane', 'Weston Park', 'Bath', 'BA1 2XQ', '01225 315192', NULL, NULL),
    ('Bristol Central Tennis Club', 'Happy Lane', 'Derby Road, St Andrews', 'Bristol', 'BS7 9AQ', '0117 924 4975', NULL, 'https://bctc.org.uk'),
    ('Portishead Lawn Tennis Club', 'Esplanade Road', NULL, 'Portishead', 'BS20 7HD', NULL, NULL, NULL),
    ('Lansdown Tennis Squash and Croquet Club', 'Lansdown Road', NULL, 'Bath', 'BA1 5ET', '01225 339696', NULL, 'https://www.lansdownclub.co.uk'),
    ('Exeter Golf and Country Club', 'Topsham Road', 'Countess Wear', 'Exeter', 'EX2 7AE', '01392 874139', NULL, NULL),
    ('Tarka Tennis Centre', '7 Seven Brethren Bank', NULL, 'Barnstaple', 'EX31 2AS', NULL, NULL, NULL),

    -- Midlands
    ('Edgbaston Priory Club', 'Sir Harry''s Road', 'Edgbaston', 'Birmingham', 'B15 2UZ', '0121 440 2492', NULL, 'https://www.edgbastonpriory.com'),
    ('Edgbaston Archery and Lawn Tennis Society', '14a Westbourne Road', 'Edgbaston', 'Birmingham', 'B15 3TR', '0121 454 2992', NULL, NULL),
    ('Nottingham Tennis Centre', '761 University Boulevard', NULL, 'Nottingham', 'NG7 2RU', '0115 876 1600', NULL, NULL),
    ('Warwick Boat Club', '107 Old Warwick Road', NULL, 'Warwick', 'CV8 1HP', '01926 492783', NULL, NULL),
    ('The Shrewsbury Club', '179B Sundorne Road', NULL, 'Shrewsbury', 'SY1 4RJ', '01743 365612', NULL, 'https://www.theshrewsburyclub.co.uk'),

    -- North West
    ('Northern Lawn Tennis Club', 'Palatine Road', 'West Didsbury', 'Manchester', 'M20 3YA', '0161 445 3063', NULL, 'https://thenorthern.co.uk'),
    ('Manchester Tennis and Racquet Club', '33 Blackfriars Road', NULL, 'Manchester', 'M3 7AQ', '0161 834 5843', NULL, NULL),
    ('Liverpool Cricket Club', 'Aigburth Road', NULL, 'Liverpool', 'L19 3QF', '0151 427 2930', NULL, NULL),
    ('Chorley Tennis Club', 'Balmoral Road', NULL, 'Chorley', 'PR7 1LN', NULL, NULL, NULL),
    ('Windermere and District Tennis Club', 'Birthwaite Road', NULL, 'Windermere', 'LA23 1BU', NULL, NULL, NULL),

    -- Yorkshire & North East
    ('Hallamshire Tennis and Squash Club', '716 Ecclesall Road', NULL, 'Sheffield', 'S11 8TA', '0114 266 5950', NULL, 'https://www.hallamshire.net'),
    ('Ilkley Lawn Tennis and Squash Club', 'Stourton Road', NULL, 'Ilkley', 'LS29 9BG', '01943 607287', NULL, 'https://www.iltsc.co.uk'),
    ('Scarborough Tennis Centre', 'Filey Road', NULL, 'Scarborough', 'YO11 3AH', '01723 382651', NULL, NULL),
    ('Headingley Lawn Tennis Club', 'Headingley Lane', NULL, 'Leeds', 'LS6 3BR', '0113 275 8347', NULL, NULL),
    ('York Tennis Club', 'Shipton Road', NULL, 'York', 'YO30 5RE', '01904 651643', NULL, NULL),
    ('Jesmond Tennis Club', 'Osborne Road', 'Jesmond', 'Newcastle upon Tyne', 'NE2 2AJ', '0191 281 4454', NULL, NULL),

    -- Wales
    ('Cardiff Lawn Tennis Club', 'The Castle Grounds', 'North Road', 'Cardiff', 'CF10 3EW', '029 2038 2083', NULL, 'https://www.cardifflawntennisclub.co.uk'),
    ('Swansea Tennis and Squash Club', 'Cwm Farm Lane', 'Sketty', 'Swansea', 'SA2 9AU', '01792 206898', NULL, NULL),
    ('Wrexham Tennis Centre', 'Plas Coch Road', NULL, 'Wrexham', 'LL11 2BW', '01978 297460', NULL, NULL),

    -- Scotland
    ('The Grange Club', 'Portgower Place', 'Stockbridge', 'Edinburgh', 'EH4 1HQ', '0131 332 2148', NULL, 'https://www.thegrangeclub.com'),
    ('Newlands Lawn Tennis Club', '18 Mochrum Road', NULL, 'Glasgow', 'G43 2QE', '0141 637 2782', NULL, NULL),
    ('Whitecraigs Lawn Tennis and Sports Club', 'Ayr Road', 'Whitecraigs', 'Glasgow', 'G46 6SJ', '0141 639 4530', NULL, NULL),
    ('Aberdeen Tennis Centre', 'Westburn Road', NULL, 'Aberdeen', 'AB25 2DA', '01224 632 832', NULL, NULL),
    ('Craiglockhart Tennis Centre', '177 Colinton Road', NULL, 'Edinburgh', 'EH14 1BZ', '0131 444 1969', NULL, NULL),
    ('Bridge of Allan Tennis Club', 'Fountain Road', NULL, 'Bridge of Allan', 'FK9 4AU', NULL, NULL, NULL),

    -- East / East Anglia
    ('Cambridge University LTC', 'Wilberforce Road', NULL, 'Cambridge', 'CB3 0EQ', '01223 351346', NULL, NULL),
    ('Norwich Tennis Centre', 'Lime Tree Road', NULL, 'Norwich', 'NR2 2NA', '01603 416121', NULL, NULL),
    ('Old College Lawn Tennis and Croquet Club', 'Lansdown', NULL, 'Cheltenham', 'GL51 6QS', '01242 233688', NULL, NULL),
    ('Farnsfield Tennis Club', '2 Station Lane', NULL, 'Farnsfield', 'NG22 8LA', NULL, NULL, NULL),

    -- South Central
    ('Winchester Racquets and Fitness', 'Bereweeke Road', NULL, 'Winchester', 'SO22 6AN', '01962 852419', NULL, NULL),
    ('Oxford University LTC', 'Iffley Road', NULL, 'Oxford', 'OX4 1EQ', '01865 243098', NULL, NULL);

-- Insert courts for each club (2-6 courts each, varied surfaces)
-- Using a cursor to give each club a realistic set of courts
DECLARE @CId INT, @CName NVARCHAR(200);
DECLARE club_cursor CURSOR FOR SELECT ClubId, Name FROM @InsertedClubs;
OPEN club_cursor;
FETCH NEXT FROM club_cursor INTO @CId, @CName;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Every club gets at least 2 hard courts
    INSERT INTO Courts (ClubId, Name, Surface, IsIndoor) VALUES (@CId, 'Court 1', 1, 0);
    INSERT INTO Courts (ClubId, Name, Surface, IsIndoor) VALUES (@CId, 'Court 2', 1, 0);

    -- Larger / well-known clubs get more courts
    IF @CName IN (
        'All England Lawn Tennis and Croquet Club', 'Queen''s Club', 'Roehampton Club',
        'Edgbaston Priory Club', 'Northern Lawn Tennis Club', 'Nottingham Tennis Centre',
        'Hallamshire Tennis and Squash Club', 'St George''s Hill Lawn Tennis Club',
        'The Grange Club', 'Ilkley Lawn Tennis and Squash Club',
        'Surbiton Racket and Fitness Club', 'Cardiff Lawn Tennis Club'
    )
    BEGIN
        INSERT INTO Courts (ClubId, Name, Surface, IsIndoor) VALUES (@CId, 'Court 3', 3, 0); -- Grass
        INSERT INTO Courts (ClubId, Name, Surface, IsIndoor) VALUES (@CId, 'Court 4', 3, 0); -- Grass
        INSERT INTO Courts (ClubId, Name, Surface, IsIndoor) VALUES (@CId, 'Court 5', 1, 1); -- Indoor hard
        INSERT INTO Courts (ClubId, Name, Surface, IsIndoor) VALUES (@CId, 'Court 6', 2, 0); -- Clay
    END
    ELSE IF @CName LIKE '%Centre%' OR @CName LIKE '%Priory%'
    BEGIN
        INSERT INTO Courts (ClubId, Name, Surface, IsIndoor) VALUES (@CId, 'Court 3', 1, 1); -- Indoor hard
        INSERT INTO Courts (ClubId, Name, Surface, IsIndoor) VALUES (@CId, 'Court 4', 1, 0);
    END
    ELSE
    BEGIN
        INSERT INTO Courts (ClubId, Name, Surface, IsIndoor) VALUES (@CId, 'Court 3', 1, 0);
    END

    FETCH NEXT FROM club_cursor INTO @CId, @CName;
END

CLOSE club_cursor;
DEALLOCATE club_cursor;

-- Summary
SELECT
    (SELECT COUNT(*) FROM @InsertedClubs) AS ClubsCreated,
    (SELECT COUNT(*) FROM Courts c INNER JOIN @InsertedClubs ic ON c.ClubId = ic.ClubId) AS CourtsCreated;

-- List all seeded clubs
SELECT ClubId, Name FROM @InsertedClubs ORDER BY Name;
