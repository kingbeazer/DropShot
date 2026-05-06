-- Normalise CompetitionFixtures.ResultSummary / OriginalResultSummary
-- onto the canonical set-list format used by all writers from
-- 2026-05-06 onward: space-separated, en-dash between the two scores
-- e.g. "6-3, 3-6, 6-2"  ->  "6–3 3–6 6–2"
--
-- The LIKE '%, %' filter touches only legacy comma-separated rows:
--   - already-migrated rows ("6–3 3–6 6–2") have no ", " and are skipped
--   - team aggregate summaries ("3-2", a single rubber-count pair) have
--     no ", " and are skipped (those legitimately use a hyphen)
--
-- Idempotent: re-running is a no-op once the LIKE filter no longer matches.
-- Safe to run inside or outside an EF migration; if you scaffold an empty
-- migration via `dotnet ef migrations add NormaliseResultSummary`,
-- paste the two UPDATE statements into the Up() body via
-- migrationBuilder.Sql(@"...");

UPDATE CompetitionFixtures
SET    ResultSummary = REPLACE(REPLACE(ResultSummary, '-', N'–'), ', ', ' ')
WHERE  ResultSummary LIKE '%, %';

UPDATE CompetitionFixtures
SET    OriginalResultSummary = REPLACE(REPLACE(OriginalResultSummary, '-', N'–'), ', ', ' ')
WHERE  OriginalResultSummary LIKE '%, %';
