// sports §369: following a league and browsing it — ESPN's answers of a league's teams, its table (a
// league's one, a conference's, a cup's groups) and its calendar read; a league found by its name, its
// longer name or its slug; a league followed through the file and through the menu's search, whose rows
// carry the logos; a league's games of its day, the face's most pressing of several, the table around
// a team; and the words of a league's day.

using System.Text.Json;
using Karpo.Dock;
using Karpo.Dock.Sports;
using Karpo.Tests.Core;

namespace Karpo.Tests.Sports;

public sealed class SportsLeagueTests
{
    private static readonly DateTime Saved = SavedSports.LeagueSavedUtc;

    [Fact]
    public void ALeaguesTeamsAreReadByNameEachWithItsLogo()
    {
        var bundesliga = SportsLeagues.Find("soccer", "ger.1")!;
        var teams = SportsAnswers.LeagueTeams(SavedSports.Answer("league-teams"), bundesliga);

        Assert.Equal(18, teams.Count);
        Assert.All(teams, team => Assert.Equal(("soccer", "ger.1"), (team.Sport, team.League)));
        Assert.All(teams, team => Assert.True(SportsAnswers.IsLogo(team.Logo), team.Name));
        Assert.Equal(teams.Select(team => team.Name).Order(StringComparer.CurrentCultureIgnoreCase), teams.Select(team => team.Name));
        var union = Assert.Single(teams, team => team.Id == "598");
        Assert.Equal(("1. FC Union Berlin", "FCU"), (union.Name, union.Abbreviation));
        Assert.Empty(SportsAnswers.LeagueTeams("{\"code\":400}", bundesliga));
    }

    [Fact]
    public void AFootballTableIsItsRanksAndPoints()
    {
        var table = SportsAnswers.Table(SavedSports.Answer("standings-soccer"), "soccer");

        var group = Assert.Single(table);
        Assert.Equal("", group.Name);
        Assert.Equal(18, group.Rows.Count);
        Assert.Equal(Enumerable.Range(1, 18), group.Rows.Select(place => place.Rank));
        Assert.Equal(("132", "Bayern", "MUN", "17"), (group.Rows[0].Id, group.Rows[0].Name, group.Rows[0].Abbreviation, group.Rows[0].Value));
        Assert.Equal(("124", "17"), (group.Rows[1].Id, group.Rows[1].Value));
        Assert.True(SportsAnswers.IsLogo(group.Rows[0].Logo));
    }

    [Fact]
    public void AnAmericanTableIsItsConferencesBySeedWithTheirRecords()
    {
        var table = SportsAnswers.Table(SavedSports.Answer("standings-nba"), "basketball");

        Assert.Equal(["Eastern Conference", "Western Conference"], table.Select(group => group.Name));
        Assert.All(table, group => Assert.Equal(15, group.Rows.Count));
        // ESPN lists them by name; the table is by seed, and the record its value.
        Assert.All(table, group => Assert.Equal(Enumerable.Range(1, 15), group.Rows.Select(place => place.Rank)));
        Assert.All(table.SelectMany(group => group.Rows), place => Assert.Matches(@"^\d+-\d+$", place.Value));
    }

    [Fact]
    public void ACupsTableIsItsGroups()
    {
        var table = SportsAnswers.Table(SavedSports.Answer("standings-groups"), "soccer");

        Assert.Equal(["Group A", "Group B", "Group C"], table.Select(group => group.Name));
        Assert.All(table, group => Assert.Equal([1, 2, 3, 4], group.Rows.Select(place => place.Rank)));
        Assert.True(SportsAnswers.Readable(SavedSports.Answer("standings-groups"), "children"));
        Assert.False(SportsAnswers.Readable(SavedSports.Answer("standings-groups")));
        Assert.Empty(SportsAnswers.Table("<html>", "soccer"));
    }

    [Fact]
    public void ALeaguesNextDayIsTheFirstOfItsCalendarAfterTheOneShown()
    {
        var scoreboard = SavedSports.Answer("league-scoreboard");

        Assert.Equal(new DateOnly(2026, 10, 30), SportsAnswers.NextDay(scoreboard, new DateOnly(2026, 10, 26)));
        Assert.Equal(new DateOnly(2026, 10, 31), SportsAnswers.NextDay(scoreboard, new DateOnly(2026, 10, 30)));
        Assert.Equal(new DateOnly(2026, 11, 1), SportsAnswers.NextDay(scoreboard, new DateOnly(2026, 10, 31)));
        // A calendar of the days without games (baseball's) says nothing of the next.
        Assert.Null(SportsAnswers.NextDay("{\"leagues\":[{\"calendarIsWhitelist\":false,\"calendar\":[\"2026-10-31T07:00Z\"]}],\"events\":[]}", new DateOnly(2026, 10, 26)));
        Assert.Null(SportsAnswers.NextDay("{}", new DateOnly(2026, 10, 26)));
    }

    [Fact]
    public void ALeaguesRequestsAreItsSlugsAndNothingElse()
    {
        Assert.Equal("https://site.api.espn.com/apis/site/v2/sports/soccer/ger.1/scoreboard", SportsAnswers.LeagueUrl("soccer", "ger.1"));
        Assert.Equal("https://site.api.espn.com/apis/site/v2/sports/soccer/ger.1/scoreboard?dates=20261031", SportsAnswers.LeagueUrl("soccer", "ger.1", new DateOnly(2026, 10, 31)));
        Assert.Equal("https://site.api.espn.com/apis/site/v2/sports/basketball/nba/teams", SportsAnswers.TeamsUrl("basketball", "nba"));
        Assert.Equal("https://site.api.espn.com/apis/v2/sports/soccer/fifa.world/standings", SportsAnswers.TableUrl("soccer", "fifa.world"));
        // Every league of the table has a logo on ESPN's picture server, and words to be found by.
        Assert.All(SportsLeagues.All, league => Assert.True(SportsAnswers.IsLogo(league.Logo), league.League));
        Assert.All(SportsLeagues.All, league => Assert.NotEqual("", league.Words));
        Assert.All(SportsLeagues.All, league => Assert.NotEqual("", SportsLeagues.SportName(league.Sport)));
    }

    [Theory]
    [InlineData("bundes", "ger.1,aut.1")]
    [InlineData("German", "ger.1")]
    [InlineData("world cup", "fifa.world")]
    [InlineData("NBA", "nba,wnba")]
    [InlineData("premier", "eng.1,sco.1")]
    [InlineData("super lig", "tur.1")]
    [InlineData("champions", "uefa.champions,eng.2,uefa.euro")]
    [InlineData("x", "")]
    [InlineData("zzz", "")]
    public void ALeagueIsFoundByItsNameItsLongerNameOrItsSlug(string typed, string found) =>
        Assert.Equal(found, string.Join(',', SportsSearch.Leagues(typed).Select(league => league.League)));

    [Fact]
    public void ALeagueFollowedIsWrittenAndReadBack()
    {
        var line = ListKind.Sports.New();
        line.Id = "sports";
        line.Sports = SavedSports.Bundesliga();
        line.Normalize();
        Assert.True(line.Sports!.IsSet);
        Assert.False(line.Sports.IsTeam);
        Assert.Equal("soccer/ger.1", line.Sports.Key);
        Assert.Null(WidgetSetup.Needs(line));

        var config = Configs.With(Configs.Holding(line));
        using (var written = JsonDocument.Parse(config.ToJson()))
        {
            var saved = Configs.Find(written.RootElement, "sports")!.Value;
            Assert.Equal(("soccer", "ger.1", "", "Bundesliga"), (saved.GetProperty("sport").GetString(), saved.GetProperty("league").GetString(), saved.GetProperty("team").GetString(), saved.GetProperty("name").GetString()));
        }
        var back = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal("soccer/ger.1", Configs.Islands(back)[0].Folder!.Cells.Single().Island.Sports!.Key);
    }

    [Fact]
    public void TheSearchRowFollowsALeagueAndOffersTheLeaguesWithTheirLogos()
    {
        var line = ListKind.Sports.New();
        var row = Assert.Single(OptionRows.Of(line), row => row.Key == OptionRows.SportsTeam);
        // Nothing typed: every league of the table to browse, each with its logo and its sport.
        var offered = row.Finder!.Suggested;
        Assert.Equal(SportsLeagues.All.Count, offered.Count);
        Assert.All(offered, found => Assert.True(SportsAnswers.IsLogo(found.Picture)));
        Assert.Equal(("Bundesliga", "Football"), (offered[2].Title, offered[2].Detail));
        Assert.DoesNotContain(offered, found => found.Chosen);

        Assert.True(row.Pick!(offered[2]));
        Assert.Equal("soccer/ger.1", line.Sports!.Key);
        Assert.Equal(("Bundesliga", ""), (line.Sports.Name, line.Sports.Abbreviation));
        Assert.Equal("Bundesliga · Football", SportsSearch.Status(line.Sports));
        var again = Assert.Single(OptionRows.Of(line), row => row.Key == OptionRows.SportsTeam);
        Assert.Equal("Bundesliga", again.Text);
        Assert.False(again.Pick!(again.Finder!.Suggested[2]));
        Assert.True(Assert.Single(again.Finder.Suggested, found => found.Chosen).Value is SportsLeague { League: "ger.1" });

        // A team picked after it: the team followed in its league, the league no more.
        var lakers = SportsAnswers.Teams(SavedSports.Answer("search-lakers"))[0];
        Assert.True(again.Pick!(new OptionFound(lakers.Name, "", lakers)));
        Assert.Equal("basketball/nba/13", line.Sports.Key);
    }

    [Fact]
    public async Task ALeaguesNameTypedFindsTheLeagueThenEveryTeamOfItThenTheSearchsEachOnce()
    {
        var bundesliga = SportsAnswers.LeagueTeams(SavedSports.Answer("league-teams"), SportsLeagues.Find("soccer", "ger.1")!);
        var asked = new List<string>();
        var finder = new OptionFields.FollowFinder(
            "soccer/ger.1/132",
            (league, _) =>
            {
                asked.Add(league.League);
                return Task.FromResult(bundesliga);
            },
            (_, _) => Task.FromResult(new List<SportsTeam> { bundesliga[0], new("soccer", "aut.1", "5000", "SK Rapid", "RAP", "") }));

        var found = await finder.FindAsync("bundesliga", CancellationToken.None);

        Assert.Equal(["ger.1"], asked);
        Assert.Equal(["Bundesliga", "Austrian Bundesliga"], found.Take(2).Select(row => row.Title));
        // The league's eighteen, then the search's one it did not have.
        Assert.Equal(2 + 18 + 1, found.Count);
        Assert.Equal("SK Rapid", found[^1].Title);
        Assert.True(Assert.Single(found, row => row.Chosen).Value is SportsTeam { Id: "132" });
        Assert.All(found.Skip(2).Take(18), row => Assert.True(SportsAnswers.IsLogo(row.Picture)));

        // The search failing leaves what was found; nothing found at all says so.
        var failing = new OptionFields.FollowFinder("", (_, _) => Task.FromResult(bundesliga), (_, _) => Task.FromException<List<SportsTeam>>(new HttpRequestException("down")));
        Assert.Equal(2 + 18, (await failing.FindAsync("bundesliga", CancellationToken.None)).Count);
        await Assert.ThrowsAsync<HttpRequestException>(() => failing.FindAsync("lakers", CancellationToken.None));
    }

    [Fact]
    public void ALeagueShowsTheDayOfItsNextGamesOnceItsResultsAreOld()
    {
        var answer = SavedSports.BundesligaAnswer();

        var view = SportsPick.Of(answer, "", Saved);
        Assert.Equal((SportsShows.Next, "401884746"), (view.Shows, view.Match?.Id));
        // A football round: Friday's game, Saturday's six and Sunday's two; a day alone where a league plays every day.
        var day = SportsPick.Day(view, answer, SportsLeagues.RoundDays("soccer"));
        Assert.Equal(9, day.Count);
        Assert.Equal("401884752", day[^1].Id);
        Assert.Single(SportsPick.Day(view, answer, SportsLeagues.RoundDays("basketball")));

        // Sunday evening, a result in: Sunday's two games.
        var sunday = SportsPick.Of(answer, "", new DateTime(2026, 10, 25, 19, 0, 0, DateTimeKind.Utc));
        Assert.Equal(SportsShows.Final, sunday.Shows);
        Assert.Equal(["401884760", "401884759"], SportsPick.Day(sunday, answer).Select(match => match.Id));
        Assert.Empty(SportsPick.Day(SportsView.None, answer));
    }

    [Fact]
    public void TheFaceLeadsWithAGameOnThenAResultThenTheSoonest()
    {
        SportsView View(SportsShows shows, int hours) => new(shows, SavedSports.Match(shows.ToString(), Saved.AddHours(hours), MatchState.Scheduled), [], [], []);

        Assert.Equal(1, SportsPick.Lead([View(SportsShows.Next, 2), View(SportsShows.Live, 0), View(SportsShows.Final, -1)]));
        Assert.Equal(1, SportsPick.Lead([View(SportsShows.Next, 2), View(SportsShows.Final, -1)]));
        Assert.Equal(1, SportsPick.Lead([View(SportsShows.Next, 20), View(SportsShows.Next, 2), View(SportsShows.Last, -90)]));
        Assert.Equal(0, SportsPick.Lead([View(SportsShows.Next, 2), View(SportsShows.Next, 2)]));
        Assert.Equal(1, SportsPick.Lead([SportsView.None, View(SportsShows.Last, -90)]));
        Assert.Equal(0, SportsPick.Lead([SportsView.None]));
        Assert.Equal(-1, SportsPick.Lead([]));
    }

    [Fact]
    public void ATeamsTableIsTheRowsAroundItALeaguesFromTheTop()
    {
        var bayern = SportsReading.From(SavedSports.Bayern(), new SportsHeld(SavedSports.BayernAnswer(), Saved, null, false), Saved, TimeSpan.FromMinutes(15));
        Assert.Equal((0, 3), Window(bayern.TableRows(3)));

        var table = SavedSports.BayernAnswer().Table;
        var seventh = table[0].Rows[6];
        var mid = SavedSports.Bayern();
        mid.Team = seventh.Id;
        var middle = SportsReading.From(mid, new SportsHeld(SavedSports.BayernAnswer(), Saved, null, false), Saved, TimeSpan.FromMinutes(15));
        Assert.Equal((5, 3), Window(middle.TableRows(3)));
        Assert.Equal((4, 5), Window(middle.TableRows(5)));
        var last = SavedSports.Bayern();
        last.Team = table[0].Rows[^1].Id;
        Assert.Equal((14, 4), Window(SportsReading.From(last, new SportsHeld(SavedSports.BayernAnswer(), Saved, null, false), Saved, TimeSpan.FromMinutes(15)).TableRows(4)));

        var league = SportsReading.From(SavedSports.Bundesliga(), new SportsHeld(SavedSports.BundesligaAnswer(), Saved, null, false), Saved, TimeSpan.FromMinutes(15));
        Assert.Equal((0, 8), Window(league.TableRows(8)));
        Assert.Equal(9, league.Day.Count);
        Assert.Null(league.TableRows(0));
        // A team in none of the table: none.
        var stranger = SavedSports.Bayern();
        stranger.Team = "999999";
        Assert.Null(SportsReading.From(stranger, new SportsHeld(SavedSports.BayernAnswer(), Saved, null, false), Saved, TimeSpan.FromMinutes(15)).TableRows(5));

        static (int, int) Window((SportsGroup Group, int Skip, int Take)? rows) => rows is { } shown ? (shown.Skip, shown.Take) : (-1, -1);
    }

    [Fact]
    public void ALeaguesWordsAreItsGamesSidesAboutTheScoreAndItsDay()
    {
        var answer = SavedSports.BundesligaAnswer();
        var reading = SportsReading.From(SavedSports.Bundesliga(), new SportsHeld(answer, Saved, null, false), Saved, TimeSpan.FromMinutes(15));
        var localNow = Saved.ToLocalTime();

        Assert.StartsWith("ELV – M05 ", reading.Said(localNow), StringComparison.Ordinal);
        var final = answer.Matches.Single(match => match.Id == "401884759");
        Assert.Matches(@"^Freiburg \d+–\d+ Bayern$", SportsText.Named(final, homeFirst: true));
        var friday = DateOnly.FromDateTime(answer.Matches.Single(match => match.Id == "401884746").StartUtc.ToLocalTime());
        Assert.Equal(S.DockSportsFinal, SportsText.Row(final, SportsClock.Minutes, localNow, friday));
        // On the list's day its time; on another its day and time.
        var friday1930 = answer.Matches.Single(match => match.Id == "401884746");
        Assert.Equal(Karpo.Dock.Panels.Nls.Time(friday1930.StartUtc.ToLocalTime()), SportsText.Row(friday1930, SportsClock.Minutes, localNow, friday));
        var classic = answer.Matches.Single(match => match.Id == "401884754");
        Assert.Equal(SportsText.When(classic, localNow), SportsText.Row(classic, SportsClock.Minutes, localNow, friday));
        Assert.Equal("Bayern – Dortmund", SportsText.Named(classic, homeFirst: true));

        var today = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Local);
        Assert.Equal(S.DockCalendarToday, SportsText.DayOf(today.ToUniversalTime(), today));
        Assert.Equal(S.DockCalendarTomorrow, SportsText.DayOf(today.AddDays(1).ToUniversalTime(), today));
        Assert.Equal(S.DockCalendarYesterday, SportsText.DayOf(today.AddDays(-1).ToUniversalTime(), today));
        Assert.Equal(Karpo.Dock.Panels.Nls.DayAndShortDate(new DateOnly(2026, 11, 4), year: false), SportsText.DayOf(today.AddDays(4).ToUniversalTime(), today));
    }

    [Fact]
    public void ASideWithoutItsLogoIsItsMonogramOnItsOwnColour()
    {
        Assert.Equal("MU", SportsText.Monogram(new SportsSide { Abbreviation = "MUN", Name = "Bayern" }, 2));
        Assert.Equal("M", SportsText.Monogram(new SportsSide { Abbreviation = "MUN" }, 1));
        Assert.Equal("BA", SportsText.Monogram(new SportsSide { Name = "ba rcelona" }, 2));
        Assert.Equal("?", SportsText.Monogram(new SportsSide(), 2));
        // The same side the same colour every time; not every side one colour.
        Assert.Equal(SportsText.Tint("132"), SportsText.Tint("132"));
        Assert.True(Enumerable.Range(100, 40).Select(id => SportsText.Tint(id.ToString(System.Globalization.CultureInfo.InvariantCulture))).Distinct().Count() > 6);
    }

    [Fact]
    public void ALeaguesPageIsItsOwnOnEspn()
    {
        Assert.Equal("https://www.espn.com/soccer/league/_/name/ger.1", SportsTile.PageOf(SavedSports.Bundesliga()));
        Assert.Equal("https://www.espn.com/nba/", SportsTile.PageOf(SavedSports.Nba()));
        Assert.Equal("https://www.espn.com/soccer/team/_/id/132", SportsTile.PageOf(SavedSports.Bayern()));
        Assert.Null(SportsTile.PageOf(new SportsOptions()));
        Assert.All(new[] { SavedSports.Bundesliga(), SavedSports.Nba(), SavedSports.Bayern() }, team => Assert.True(SportsAnswers.Opens(SportsTile.PageOf(team))));
    }
}
