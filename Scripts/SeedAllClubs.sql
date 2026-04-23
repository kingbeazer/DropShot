-- ============================================
-- Master Seed Script: UK Tennis Clubs
-- ============================================
-- Total: ~1,268 clubs across all regions of the UK
--
-- Sources: LTA ClubSpark directories, county tennis association websites,
--          tenniscourtsmap.com, Wikipedia, individual club websites
--
-- Data collected: April 2026
--
-- Coverage:
--   Part 1 (East):      Norfolk, Bedfordshire, Suffolk, Cambridgeshire = 158 clubs
--   Part 2 (Midlands):  Derbyshire, Lincolnshire, Shropshire, Leicestershire,
--                        Northamptonshire, Nottinghamshire, Warwickshire,
--                        Staffordshire, Herefordshire & Worcestershire = 243 clubs
--   Part 3 (North):     Cumbria, Durham & Cleveland, Northumberland,
--                        Isle of Man, Yorkshire = 242 clubs
--   Part 4 (South/SW):  Channel Islands, Dorset, Wiltshire, Somerset, Avon,
--                        Gloucestershire, Devon, Cornwall, Hampshire & IoW = 319 clubs
--   Part 5 (Remaining): Hertfordshire, Essex, Berkshire, Buckinghamshire,
--                        Oxfordshire, Scotland, Wales = 306 clubs
--
-- NOTE: Some clubs have full addresses, others only have Name and Town.
--       Addresses can be enriched later via the LTA venue finder.
--       London & South East (Surrey, Sussex, Kent, Middlesex) not yet included
--       - these can be added as Part 6.
-- ============================================

PRINT 'Starting UK Tennis Clubs seed...';
PRINT '';

-- Part 1: East England
PRINT 'Seeding Part 1: East England (158 clubs)...';
:r SeedClubs_Part1_East.sql
PRINT 'Part 1 complete.';
PRINT '';

-- Part 2: Midlands
PRINT 'Seeding Part 2: Midlands (243 clubs)...';
:r SeedClubs_Part2_Midlands.sql
PRINT 'Part 2 complete.';
PRINT '';

-- Part 3: North England
PRINT 'Seeding Part 3: North England (242 clubs)...';
:r SeedClubs_Part3_North.sql
PRINT 'Part 3 complete.';
PRINT '';

-- Part 4: South & South West
PRINT 'Seeding Part 4: South & South West (319 clubs)...';
:r SeedClubs_Part4_SouthWest.sql
PRINT 'Part 4 complete.';
PRINT '';

-- Part 5: Remaining (Herts, Essex, Berks, Bucks, Oxon, Scotland, Wales)
PRINT 'Seeding Part 5: Remaining counties + Scotland + Wales (306 clubs)...';
:r SeedClubs_Part5_Remaining.sql
PRINT 'Part 5 complete.';
PRINT '';

-- Summary
PRINT '============================================';
PRINT 'Seed complete!';
PRINT '';
SELECT COUNT(*) AS TotalClubsSeeded FROM Clubs;
SELECT Town, COUNT(*) AS ClubCount FROM Clubs GROUP BY Town ORDER BY ClubCount DESC;
