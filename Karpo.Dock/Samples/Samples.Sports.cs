// §635: the sample of a sports widget: a team followed in a league with a match on now — Barcelona 2–1 Real Madrid in the
// 67th minute in LALIGA —, its last results and its next fixtures round it, where it stands, and the league's table. Made
// up against the clock now, so the match is always on; no logo is asked for (the sides' abbreviations stand for them).

using Karpo.Core;
using Karpo.Dock.Sports;

namespace Karpo.Dock;

internal static partial class Samples
{
    private const string League = "esp.1";
    private const string LeagueName = "LALIGA";

    /// <summary>What a sports widget follows in the samples where it follows nothing: Barcelona, in LALIGA.</summary>
    public static SportsOptions SportsTeam() => new() { Sport = SportsLeagues.Soccer, League = League, Team = "83", Name = "Barcelona", Abbreviation = "BAR" };

    /// <summary>Barcelona's answer as the store would hold it, fetched a minute ago: a match on now, its results and fixtures, its standing and the table.</summary>
    public static SportsHeld Sports()
    {
        var now = Clock.Now().ToUniversalTime();
        var kickOff = now.AddMinutes(-72);
        // The others on the hour, as fixtures are: 19:00 UTC a week apart.
        var evening = new DateTime(now.Date.Ticks, DateTimeKind.Utc).AddHours(19);
        var answer = new SportsAnswer
        {
            ScheduledUtc = now.AddMinutes(-1),
            Standing = "1st in Spanish LALIGA",
            Record = "9-1-1",
            Matches =
            [
                Match("s1", evening.AddDays(-21), MatchState.Final, Side("83", "BAR", "Barcelona", "3"), Side("95", "VAL", "Valencia", "0")),
                Match("s2", evening.AddDays(-14), MatchState.Final, Side("89", "BET", "Real Betis", "1"), Side("83", "BAR", "Barcelona", "1")),
                Match("s3", evening.AddDays(-7), MatchState.Final, Side("83", "BAR", "Barcelona", "2"), Side("244", "SEV", "Sevilla", "0")),
                Match("s4", kickOff, MatchState.Live, Side("83", "BAR", "Barcelona", "2"), Side("86", "RMA", "Real Madrid", "1"), clock: "67'"),
                Match("s5", evening.AddDays(7), MatchState.Scheduled, Side("1068", "ATM", "Atlético Madrid"), Side("83", "BAR", "Barcelona")),
                Match("s6", evening.AddDays(14), MatchState.Scheduled, Side("83", "BAR", "Barcelona"), Side("93", "ATH", "Athletic Club")),
                Match("s7", evening.AddDays(21), MatchState.Scheduled, Side("92", "VIL", "Villarreal"), Side("83", "BAR", "Barcelona")),
            ],
            Table =
            [
                new SportsGroup
                {
                    Rows =
                    [
                        Place(1, "83", "BAR", "Barcelona", 11, 9, 1, 1, "+21", 28),
                        Place(2, "86", "RMA", "Real Madrid", 11, 8, 2, 1, "+15", 26),
                        Place(3, "1068", "ATM", "Atlético Madrid", 11, 7, 2, 2, "+11", 23),
                        Place(4, "92", "VIL", "Villarreal", 11, 6, 3, 2, "+7", 21),
                        Place(5, "93", "ATH", "Athletic Club", 11, 5, 4, 2, "+5", 19),
                        Place(6, "89", "BET", "Real Betis", 11, 5, 3, 3, "+3", 18),
                    ],
                },
            ],
        };
        return new SportsHeld(answer, now.AddMinutes(-1), null, false);
    }

    /// <summary>A sports line's source over the sample: nothing fetched, no logo.</summary>
    public static SportsSource SportsSource()
    {
        var held = Sports();
        return new SportsSource { Read = () => held };
    }

    private static SportsSide Side(string id, string abbreviation, string name, string score = "") =>
        new() { Id = id, Abbreviation = abbreviation, Name = name, Score = score };

    private static SportsMatch Match(string id, DateTime start, MatchState state, SportsSide home, SportsSide away, string clock = "")
    {
        if (state == MatchState.Final)
        {
            var (one, other) = (int.Parse(home.Score, System.Globalization.CultureInfo.InvariantCulture), int.Parse(away.Score, System.Globalization.CultureInfo.InvariantCulture));
            home.Winner = one > other;
            away.Winner = other > one;
        }
        return new SportsMatch
        {
            Id = id,
            StartUtc = start,
            State = state,
            Status = state switch { MatchState.Live => "STATUS_SECOND_HALF", MatchState.Final => "STATUS_FULL_TIME", _ => "STATUS_SCHEDULED" },
            Clock = clock,
            Period = state == MatchState.Live ? 2 : 0,
            League = League,
            LeagueName = LeagueName,
            Home = home,
            Away = away,
        };
    }

    private static SportsPlace Place(int rank, string id, string abbreviation, string name, int played, int won, int drawn, int lost, string difference, int points) => new()
    {
        Rank = rank,
        Id = id,
        Abbreviation = abbreviation,
        Name = name,
        Value = points.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Played = played.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Won = won.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Drawn = drawn.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Lost = lost.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Difference = difference,
        Points = points.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
