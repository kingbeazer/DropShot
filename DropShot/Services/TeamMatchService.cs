using DropShot.Models;

namespace DropShot.Services;

public static class TeamMatchService
{
    /// <summary>
    /// Creates the 8 TeamMatchSet rows for a team fixture based on team compositions.
    /// </summary>
    public static List<TeamMatchSet> CreateSetsForFixture(
        int fixtureId,
        List<CompetitionParticipant> homeMembers,
        List<CompetitionParticipant> awayMembers)
    {
        var homeMaleA = homeMembers.First(m => m.Player!.Sex == PlayerSex.Male && m.Grade == PlayerGrade.A);
        var homeFemaleA = homeMembers.First(m => m.Player!.Sex == PlayerSex.Female && m.Grade == PlayerGrade.A);
        var homeMaleB = homeMembers.First(m => m.Player!.Sex == PlayerSex.Male && m.Grade == PlayerGrade.B);
        var homeFemaleB = homeMembers.First(m => m.Player!.Sex == PlayerSex.Female && m.Grade == PlayerGrade.B);

        var awayMaleA = awayMembers.First(m => m.Player!.Sex == PlayerSex.Male && m.Grade == PlayerGrade.A);
        var awayFemaleA = awayMembers.First(m => m.Player!.Sex == PlayerSex.Female && m.Grade == PlayerGrade.A);
        var awayMaleB = awayMembers.First(m => m.Player!.Sex == PlayerSex.Male && m.Grade == PlayerGrade.B);
        var awayFemaleB = awayMembers.First(m => m.Player!.Sex == PlayerSex.Female && m.Grade == PlayerGrade.B);

        return
        [
            // Phase 1 — Gender Doubles
            // Sets 1-2: Men's Doubles on Court 1
            MakeSet(fixtureId, 1, TeamMatchPhase.GenderDoubles, TeamMatchSetType.MensDoubles, 1,
                homeMaleA.PlayerId, homeMaleB.PlayerId, awayMaleA.PlayerId, awayMaleB.PlayerId),
            MakeSet(fixtureId, 2, TeamMatchPhase.GenderDoubles, TeamMatchSetType.MensDoubles, 1,
                homeMaleA.PlayerId, homeMaleB.PlayerId, awayMaleA.PlayerId, awayMaleB.PlayerId),

            // Sets 3-4: Women's Doubles on Court 2
            MakeSet(fixtureId, 3, TeamMatchPhase.GenderDoubles, TeamMatchSetType.WomensDoubles, 2,
                homeFemaleA.PlayerId, homeFemaleB.PlayerId, awayFemaleA.PlayerId, awayFemaleB.PlayerId),
            MakeSet(fixtureId, 4, TeamMatchPhase.GenderDoubles, TeamMatchSetType.WomensDoubles, 2,
                homeFemaleA.PlayerId, homeFemaleB.PlayerId, awayFemaleA.PlayerId, awayFemaleB.PlayerId),

            // Phase 2 — Mixed Doubles
            // Sets 5-6: A-Grade Mixed on Court 1
            MakeSet(fixtureId, 5, TeamMatchPhase.MixedDoubles, TeamMatchSetType.MixedDoublesA, 1,
                homeMaleA.PlayerId, homeFemaleA.PlayerId, awayMaleA.PlayerId, awayFemaleA.PlayerId),
            MakeSet(fixtureId, 6, TeamMatchPhase.MixedDoubles, TeamMatchSetType.MixedDoublesA, 1,
                homeMaleA.PlayerId, homeFemaleA.PlayerId, awayMaleA.PlayerId, awayFemaleA.PlayerId),

            // Sets 7-8: B-Grade Mixed on Court 2
            MakeSet(fixtureId, 7, TeamMatchPhase.MixedDoubles, TeamMatchSetType.MixedDoublesB, 2,
                homeMaleB.PlayerId, homeFemaleB.PlayerId, awayMaleB.PlayerId, awayFemaleB.PlayerId),
            MakeSet(fixtureId, 8, TeamMatchPhase.MixedDoubles, TeamMatchSetType.MixedDoublesB, 2,
                homeMaleB.PlayerId, homeFemaleB.PlayerId, awayMaleB.PlayerId, awayFemaleB.PlayerId),
        ];
    }

    private static TeamMatchSet MakeSet(
        int fixtureId, int setNumber, TeamMatchPhase phase, TeamMatchSetType setType,
        int courtNumber, int hp1, int hp2, int ap1, int ap2) => new()
    {
        CompetitionFixtureId = fixtureId,
        SetNumber = setNumber,
        Phase = phase,
        SetType = setType,
        CourtNumber = courtNumber,
        HomePlayer1Id = hp1,
        HomePlayer2Id = hp2,
        AwayPlayer1Id = ap1,
        AwayPlayer2Id = ap2
    };

    public static (int homeScore, int awayScore) ComputeScore(
        IEnumerable<TeamMatchSet> sets, int homeTeamId, int awayTeamId)
    {
        int home = 0, away = 0;
        foreach (var s in sets.Where(s => s.IsComplete))
        {
            if (s.WinnerTeamId == homeTeamId) home++;
            else if (s.WinnerTeamId == awayTeamId) away++;
        }
        return (home, away);
    }

    public static bool AllSetsComplete(IEnumerable<TeamMatchSet> sets)
        => sets.All(s => s.IsComplete);
}
