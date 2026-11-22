// sports: ESPN's own answers (docs\widgets.md §239), saved on 2026-10-19 at 17:42 UTC and cut to the
// fields Karpo reads, every value as ESPN gave it (Saved\*.json): Barcelona's results and fixtures
// across its competitions, Serie A's and the Süper Lig's scoreboards with a match in each half, the
// Chiefs' and the Lakers' and the Dodgers' schedules, the NFL's scoreboard with a final after
// overtime, and three searches. The answers and the teams the tests build from them.

using System.Reflection;

namespace Karpo.Tests.Sports;

internal static class SavedSports
{
    /// <summary>When the answers were saved: Monza 2–0 Cagliari in the 53rd minute, Trabzonspor 1–0 Beşiktaş in the 41st.</summary>
    public static readonly DateTime SavedUtc = new(2026, 10, 19, 17, 42, 0, DateTimeKind.Utc);

    /// <summary>The saved answer <paramref name="name"/> ("soccer-results"), as ESPN gave it.</summary>
    public static string Answer(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Karpo.Tests.Sports.{name}.json")
            ?? throw new InvalidOperationException($"No saved answer {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Barcelona, followed in LALIGA, as a search finds it.</summary>
    public static SportsOptions Barcelona() => new() { Sport = "soccer", League = "esp.1", Team = "83", Name = "Barcelona", Abbreviation = "BAR" };

    /// <summary>Monza, whose match was on when the answers were saved.</summary>
    public static SportsOptions Monza() => new() { Sport = "soccer", League = "ita.1", Team = "4007", Name = "Monza", Abbreviation = "MON" };

    /// <summary>The Kansas City Chiefs.</summary>
    public static SportsOptions Chiefs() => new() { Sport = "football", League = "nfl", Team = "12", Name = "Kansas City Chiefs", Abbreviation = "KC" };

    /// <summary>Barcelona's answer as the store makes it from its results and fixtures.</summary>
    public static SportsAnswer BarcelonaAnswer()
    {
        var answer = new SportsAnswer { ScheduledUtc = SavedUtc };
        answer.Matches.AddRange(SportsAnswers.Matches(Answer("soccer-results"), "esp.1"));
        answer.Matches.AddRange(SportsAnswers.Matches(Answer("soccer-fixtures"), "esp.1"));
        answer.Matches.Sort((one, other) => one.StartUtc.CompareTo(other.StartUtc));
        (answer.Standing, answer.Record, answer.Logo) = SportsAnswers.TeamOf(Answer("soccer-results"));
        return answer;
    }

    /// <summary>Monza's answer: its match on now, from Serie A's scoreboard, and the league's other match of the day.</summary>
    public static SportsAnswer MonzaLive()
    {
        var answer = new SportsAnswer { ScheduledUtc = SavedUtc };
        SportsAnswers.Merge(answer, SportsAnswers.Matches(Answer("soccer-scoreboard"), "ita.1"), "4007");
        return answer;
    }

    // §369: a league's answers, saved on 2026-10-26 at 15:30 UTC — the Bundesliga's scoreboard (Sunday's
    // two finals and its calendar), its next round's three days (Friday's game, Saturday's six, Sunday's
    // two), its teams and its table; the NBA's scoreboard of the evening and its table; the World Cup's
    // first three groups.

    /// <summary>When the league's answers were saved: Monday afternoon, the Bundesliga's next games on Saturday.</summary>
    public static readonly DateTime LeagueSavedUtc = new(2026, 10, 26, 15, 30, 0, DateTimeKind.Utc);

    /// <summary>The Bundesliga, followed itself.</summary>
    public static SportsOptions Bundesliga() => new() { Sport = "soccer", League = "ger.1", Name = "Bundesliga" };

    /// <summary>The NBA, followed itself.</summary>
    public static SportsOptions Nba() => new() { Sport = "basketball", League = "nba", Name = "NBA" };

    /// <summary>Bayern Munich, followed in the Bundesliga.</summary>
    public static SportsOptions Bayern() => new() { Sport = "soccer", League = "ger.1", Team = "132", Name = "Bayern Munich", Abbreviation = "MUN" };

    /// <summary>The Bundesliga's answer as the store makes it: Sunday's results, the next round's games and the table.</summary>
    public static SportsAnswer BundesligaAnswer()
    {
        var answer = new SportsAnswer { ScheduledUtc = LeagueSavedUtc, Logo = SportsLeagues.Find("soccer", "ger.1")!.Logo };
        answer.Matches.AddRange(SportsAnswers.Matches(Answer("league-scoreboard"), "ger.1"));
        foreach (var day in new[] { "league-day-1030", "league-day-1031", "league-day-1101" })
        {
            answer.Matches.AddRange(SportsAnswers.Matches(Answer(day), "ger.1"));
        }
        answer.Matches.Sort((one, other) => one.StartUtc.CompareTo(other.StartUtc));
        answer.Table = SportsAnswers.Table(Answer("standings-soccer"), "soccer");
        return answer;
    }

    /// <summary>Bayern's answer from the league's: its two games, and the table it leads.</summary>
    public static SportsAnswer BayernAnswer()
    {
        var league = BundesligaAnswer();
        return new SportsAnswer
        {
            ScheduledUtc = LeagueSavedUtc,
            Matches = [.. league.Matches.Where(match => match.Plays("132"))],
            Table = league.Table,
            Standing = "1st in German Bundesliga",
            Record = "5-2-0",
        };
    }

    /// <summary>The NBA's answer: the evening's nine games, and its two conferences' tables.</summary>
    public static SportsAnswer NbaAnswer() => new()
    {
        ScheduledUtc = LeagueSavedUtc,
        Matches = SportsAnswers.Matches(Answer("league-scoreboard-nba"), "nba"),
        Table = SportsAnswers.Table(Answer("standings-nba"), "basketball"),
        Logo = SportsLeagues.Find("basketball", "nba")!.Logo,
    };

    // §479: the competitions the person named, saved on 2026-11-02 and cut the same way: the World Cup's final day
    // (Spain 1–0 Argentina after extra time) with its calendar's stages, its knockout in June and July (two
    // shoot-outs on 29 June, extra time on 1 and 11 July), a group day (20 June) and its twelve groups; the
    // Champions League's March (the round of 16's two legs, Sporting through 5–3 on aggregate after extra time),
    // its final (Paris 1–1 Arsenal, 4–3 on penalties), its next matchday and its league phase's 36; the Premier
    // League's Sunday and its table with its zones; the Swiss Super League as ESPN has it now (nothing since
    // September 2025, no table); Sporting's schedule from February 2026; the NHL's and the NBA's whole tables.

    /// <summary>The World Cup, followed itself.</summary>
    public static SportsOptions WorldCup() => new() { Sport = "soccer", League = "fifa.world", Name = "FIFA World Cup" };

    /// <summary>The Champions League, followed itself.</summary>
    public static SportsOptions ChampionsLeague() => new() { Sport = "soccer", League = "uefa.champions", Name = "UEFA Champions League" };

    /// <summary>The Premier League, followed itself.</summary>
    public static SportsOptions PremierLeague() => new() { Sport = "soccer", League = "eng.1", Name = "Premier League" };

    /// <summary>The Swiss Super League, followed itself.</summary>
    public static SportsOptions SuperLeague() => new() { Sport = "soccer", League = "sui.1", Name = "Swiss Super League" };

    /// <summary>Sporting CP, followed in Portugal's league.</summary>
    public static SportsOptions Sporting() => new() { Sport = "soccer", League = "por.1", Team = "2250", Name = "Sporting CP", Abbreviation = "SCP" };

    /// <summary>When the competitions' answers were saved: Monday noon, the Champions League's next matchday on Tuesday.</summary>
    public static readonly DateTime CupsSavedUtc = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Late on the World Cup's round of 32's second day: two shoot-outs that evening.</summary>
    public static readonly DateTime ShootoutsUtc = new(2026, 6, 30, 1, 0, 0, DateTimeKind.Utc);

    /// <summary>Evening of the World Cup's group day, 20 June.</summary>
    public static readonly DateTime GroupDayUtc = new(2026, 6, 20, 22, 30, 0, DateTimeKind.Utc);

    /// <summary>After the Champions League's round of 16's second legs on 17 March.</summary>
    public static readonly DateTime SecondLegsUtc = new(2026, 3, 17, 23, 0, 0, DateTimeKind.Utc);

    private static List<SportsMatch> All(params string[] names) =>
        [.. names.SelectMany(name => SportsAnswers.Matches(Answer(name))).DistinctBy(match => match.Id).OrderBy(match => match.StartUtc)];

    /// <summary>The World Cup's knockout, every round's from the round of 32 to the final.</summary>
    public static List<SportsMatch> WorldCupKnockout() => All("wc-june-knockout", "wc-july");

    /// <summary>The World Cup as the store makes it now: its final day, its groups and its knockout.</summary>
    public static SportsAnswer WorldCupAnswer() => new()
    {
        ScheduledUtc = CupsSavedUtc,
        Matches = All("wc-now"),
        Table = SportsAnswers.Table(Answer("standings-wc"), "soccer"),
        Knockout = WorldCupKnockout(),
        Logo = SportsLeagues.Find("soccer", "fifa.world")!.Logo,
    };

    /// <summary>The World Cup on its round of 32's second day: the day's games and the knockout so far.</summary>
    public static SportsAnswer WorldCupShootouts() => new()
    {
        ScheduledUtc = ShootoutsUtc,
        Matches = [.. WorldCupKnockout().Where(match => SportsAnswers.EspnDay(match.StartUtc) == new DateOnly(2026, 6, 29))],
        Table = SportsAnswers.Table(Answer("standings-wc"), "soccer"),
        Knockout = [.. WorldCupKnockout().Where(match => match.StartUtc < ShootoutsUtc.AddDays(4))],
        Logo = SportsLeagues.Find("soccer", "fifa.world")!.Logo,
    };

    /// <summary>The World Cup on a group day: its three games, and the groups.</summary>
    public static SportsAnswer WorldCupGroupDay() => new()
    {
        ScheduledUtc = GroupDayUtc,
        Matches = All("wc-group-day"),
        Table = SportsAnswers.Table(Answer("standings-wc"), "soccer"),
        Logo = SportsLeagues.Find("soccer", "fifa.world")!.Logo,
    };

    /// <summary>The Champions League now: Tuesday's matchday and the league phase's table.</summary>
    public static SportsAnswer ChampionsLeagueAnswer() => new()
    {
        ScheduledUtc = CupsSavedUtc,
        Matches = All("ucl-now"),
        Table = SportsAnswers.Table(Answer("standings-ucl"), "soccer"),
        Logo = SportsLeagues.Find("soccer", "uefa.champions")!.Logo,
    };

    /// <summary>The Champions League after its round of 16's second legs: that evening's games and the knockout to the final.</summary>
    public static SportsAnswer SecondLegsAnswer() => new()
    {
        ScheduledUtc = SecondLegsUtc,
        Matches = [.. All("ucl-march").Where(match => SportsAnswers.EspnDay(match.StartUtc) == new DateOnly(2026, 3, 17))],
        Knockout = All("ucl-march", "ucl-final"),
        Logo = SportsLeagues.Find("soccer", "uefa.champions")!.Logo,
    };

    /// <summary>The Premier League on Sunday 1 November: Liverpool 0–1 Arsenal, and the table with its zones.</summary>
    public static SportsAnswer PremierLeagueAnswer() => new()
    {
        ScheduledUtc = CupsSavedUtc,
        Matches = All("eng-day"),
        Table = SportsAnswers.Table(Answer("standings-eng"), "soccer"),
        Logo = SportsLeagues.Find("soccer", "eng.1")!.Logo,
    };

    /// <summary>The Swiss Super League as ESPN answers now: September 2025's games, no table.</summary>
    public static SportsAnswer SuperLeagueAnswer() => new()
    {
        ScheduledUtc = CupsSavedUtc,
        Matches = All("sui-now"),
        Table = SportsAnswers.Table(Answer("standings-sui"), "soccer"),
        Logo = SportsLeagues.Find("soccer", "sui.1")!.Logo,
    };

    /// <summary>Sporting's answer after its second leg against Bodø/Glimt: its results from February and its league's table.</summary>
    public static SportsAnswer SportingAnswer()
    {
        var answer = new SportsAnswer { ScheduledUtc = SecondLegsUtc, Matches = All("sporting-2025") };
        (answer.Standing, answer.Record, answer.Logo) = SportsAnswers.TeamOf(Answer("sporting-2025"));
        return answer;
    }

    // §485: the saved answers as they stood at a moment of their own: what had not begun then to come, what began
    // within two hours on with its minute (its score as it ended — a stand-in), the rest as ESPN saved it.

    /// <summary>The Champions League's round of 16's second legs on Tuesday 17 March at 21:10 UTC: one game played, three on in their 55th minute, Wednesday's four to come.</summary>
    public static readonly DateTime MatchdayLiveUtc = new(2026, 3, 17, 21, 10, 0, DateTimeKind.Utc);

    /// <summary>The World Cup's second quarterfinal on 10 July at 19:40 UTC: the rounds of 32 and 16 and the first quarterfinal played, the second in its 40th minute, the rest to come.</summary>
    public static readonly DateTime KnockoutLiveUtc = new(2026, 7, 10, 19, 40, 0, DateTimeKind.Utc);

    /// <summary><paramref name="matches"/> as they stood at <paramref name="utcNow"/> (see above).</summary>
    public static List<SportsMatch> AsOf(IEnumerable<SportsMatch> matches, DateTime utcNow)
    {
        var all = matches.ToList();
        foreach (var match in all)
        {
            var played = utcNow - match.StartUtc;
            if (played < TimeSpan.Zero)
            {
                match.State = MatchState.Scheduled;
                match.Status = "STATUS_SCHEDULED";
                match.Clock = "";
                match.Period = 0;
                foreach (var side in new[] { match.Home, match.Away })
                {
                    (side.Score, side.Shootout, side.Aggregate, side.Winner, side.Advances) = ("", "", "", false, false);
                }
            }
            else if (played < TimeSpan.FromMinutes(110))
            {
                var minutes = (int)played.TotalMinutes;
                var (minute, period, status) = minutes <= 45 ? (minutes, 1, "STATUS_FIRST_HALF") : minutes <= 60 ? (45, 1, "STATUS_HALFTIME") : (Math.Min(90, minutes - 15), 2, "STATUS_SECOND_HALF");
                match.State = MatchState.Live;
                match.Status = status;
                match.Clock = $"{minute}'";
                match.Period = period;
                foreach (var side in new[] { match.Home, match.Away })
                {
                    (side.Shootout, side.Winner, side.Advances) = ("", false, false);
                }
            }
        }
        return all;
    }

    /// <summary>The Champions League's matchday at <see cref="MatchdayLiveUtc"/>: March's games, the knockout to the final.</summary>
    public static SportsAnswer MatchdayLive() => new()
    {
        ScheduledUtc = MatchdayLiveUtc,
        Matches = AsOf(All("ucl-march"), MatchdayLiveUtc),
        Knockout = AsOf(All("ucl-march", "ucl-final"), MatchdayLiveUtc),
        Logo = SportsLeagues.Find("soccer", "uefa.champions")!.Logo,
    };

    /// <summary>The World Cup at <see cref="KnockoutLiveUtc"/>: the day's games, the groups and the knockout.</summary>
    public static SportsAnswer KnockoutLive() => new()
    {
        ScheduledUtc = KnockoutLiveUtc,
        Matches = [.. AsOf(WorldCupKnockout(), KnockoutLiveUtc).Where(match => (match.StartUtc - KnockoutLiveUtc).Duration() < TimeSpan.FromDays(2))],
        Table = SportsAnswers.Table(Answer("standings-wc"), "soccer"),
        Knockout = AsOf(WorldCupKnockout(), KnockoutLiveUtc),
        Logo = SportsLeagues.Find("soccer", "fifa.world")!.Logo,
    };

    /// <summary>A side of the World Cup's game on at <see cref="KnockoutLiveUtc"/>, followed as a team: its knockout games as they stood.</summary>
    public static (SportsOptions Team, SportsAnswer Answer) KnockoutTeamLive()
    {
        var knockout = AsOf(WorldCupKnockout(), KnockoutLiveUtc);
        var side = knockout.First(match => match.State == MatchState.Live).Home;
        var team = new SportsOptions { Sport = "soccer", League = "fifa.world", Team = side.Id, Name = side.Name, Abbreviation = side.Abbreviation };
        return (team, new SportsAnswer { ScheduledUtc = KnockoutLiveUtc, Matches = [.. knockout.Where(match => match.Plays(side.Id))], Logo = side.Logo });
    }

    /// <summary>A match of the team's, made for a test: <paramref name="state"/> at <paramref name="startUtc"/>, home or away.</summary>
    public static SportsMatch Match(string id, DateTime startUtc, MatchState state, bool home = true, string team = "83", string other = "86", string score = "", string otherScore = "") => new()
    {
        Id = id,
        StartUtc = startUtc,
        State = state,
        League = "esp.1",
        LeagueName = "LALIGA",
        Home = home ? new SportsSide { Id = team, Abbreviation = "BAR", Name = "Barcelona", Score = score } : new SportsSide { Id = other, Abbreviation = "RMA", Name = "Real Madrid", Score = otherScore },
        Away = home ? new SportsSide { Id = other, Abbreviation = "RMA", Name = "Real Madrid", Score = otherScore } : new SportsSide { Id = team, Abbreviation = "BAR", Name = "Barcelona", Score = score },
    };
}
