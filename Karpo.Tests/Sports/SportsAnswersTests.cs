// sports: ESPN's answers read (docs\widgets.md §239), from its own answers saved on 2026-10-19
// (SavedSports): a schedule's matches with their scores as objects, a scoreboard's with theirs as
// words, a match on now in each half, a final after overtime, a kick-off whose time is not known, a
// match put off, a search kept to the table's leagues, and what is no answer at all.

namespace Karpo.Tests.Sports;

public sealed class SportsAnswersTests
{
    [Fact]
    public void ASchedulesResultsAreReadWithTheirScoresAndTheirCompetitions()
    {
        var matches = SportsAnswers.Matches(SavedSports.Answer("soccer-results"), "esp.1");

        Assert.Equal(4, matches.Count);
        // By kick-off, the friendly first.
        Assert.Equal(matches.OrderBy(match => match.StartUtc), matches);
        var betis = matches.Single(match => match.Id == "401882846");
        Assert.Equal(new DateTime(2026, 10, 17, 16, 30, 0, DateTimeKind.Utc), betis.StartUtc);
        Assert.Equal(MatchState.Final, betis.State);
        Assert.Equal(("BET", "2", "BAR", "5"), (betis.Home.Abbreviation, betis.Home.Score, betis.Away.Abbreviation, betis.Away.Score));
        Assert.True(betis.Away.Winner);
        Assert.False(betis.Home.Winner);
        Assert.Equal("esp.1", betis.League);
        Assert.Equal("LALIGA", betis.LeagueName);
        Assert.StartsWith("https://www.espn.com/soccer/match/_/gameId/401882846", betis.Link);
        Assert.Equal("https://a.espncdn.com/i/teamlogos/soccer/500/83.png", betis.Away.Logo);
        // The Champions League's match is filed under its own competition.
        Assert.Equal("uefa.champions", matches.Single(match => match.Id == "401915435").League);
        // A friendly settled on penalties is a final as well.
        Assert.Equal(MatchState.Final, matches.Single(match => match.Status == "STATUS_FINAL_PEN").State);

        var (standing, record, logo) = SportsAnswers.TeamOf(SavedSports.Answer("soccer-results"));
        Assert.Equal("1st in Spanish LALIGA", standing);
        Assert.Equal("9-0-0", record);
        Assert.Equal("https://a.espncdn.com/i/teamlogos/soccer/500/83.png", logo);
    }

    [Fact]
    public void AFixtureIsScheduledAndOneWithoutItsTimeSaysSo()
    {
        var matches = SportsAnswers.Matches(SavedSports.Answer("soccer-fixtures"), "esp.1");

        Assert.All(matches, match => Assert.Equal(MatchState.Scheduled, match.State));
        Assert.All(matches, match => Assert.Equal("", match.Home.Score + match.Away.Score));
        var paris = matches[0];
        Assert.Equal(("PSG", "BAR"), (paris.Home.Abbreviation, paris.Away.Abbreviation));
        Assert.Equal("uefa.champions", paris.League);
        Assert.True(paris.TimeKnown);
        // "TBD": the day is known, the time is not.
        Assert.False(matches.Single(match => match.Away.Abbreviation == "VIL").TimeKnown);
    }

    [Fact]
    public void AScoreboardsMatchOnNowHasItsMinuteAndItsHalf()
    {
        var serieA = SportsAnswers.Matches(SavedSports.Answer("soccer-scoreboard"));
        var monza = serieA.Single(match => match.Home.Abbreviation == "MON");
        Assert.Equal(MatchState.Live, monza.State);
        Assert.Equal("STATUS_SECOND_HALF", monza.Status);
        Assert.Equal(("53'", 2), (monza.Clock, monza.Period));
        Assert.Equal(("2", "0"), (monza.Home.Score, monza.Away.Score));
        // The scoreboard's league is the answer's, its name the short one.
        Assert.Equal(("ita.1", "Serie A"), (monza.League, monza.LeagueName));
        // Before kick-off a scoreboard writes "0" for each side: not a score yet.
        var parma = serieA.Single(match => match.Home.Abbreviation == "PAR");
        Assert.Equal(MatchState.Scheduled, parma.State);

        var trabzon = Assert.Single(SportsAnswers.Matches(SavedSports.Answer("soccer-scoreboard-first-half")));
        Assert.Equal((MatchState.Live, "41'", 1), (trabzon.State, trabzon.Clock, trabzon.Period));
        Assert.Equal("TRAB", trabzon.Home.Abbreviation);
    }

    [Fact]
    public void AmericasSchedulesAndScoreboardsAreReadAlike()
    {
        var chiefs = SportsAnswers.Matches(SavedSports.Answer("nfl-schedule"), "nfl");
        Assert.Equal(4, chiefs.Count);
        // No league in a league's own schedule: the one it was asked for.
        Assert.All(chiefs, match => Assert.Equal("nfl", match.League));
        var raiders = chiefs[0];
        Assert.Equal((MatchState.Final, "LV", "27", "KC", "30"), (raiders.State, raiders.Home.Abbreviation, raiders.Home.Score, raiders.Away.Abbreviation, raiders.Away.Score));
        Assert.Equal(MatchState.Scheduled, chiefs[2].State);

        var nfl = SportsAnswers.Matches(SavedSports.Answer("nfl-scoreboard"));
        var overtime = nfl.Single(match => match.Home.Abbreviation == "DEN");
        Assert.Equal((MatchState.Final, 5), (overtime.State, overtime.Period));
        Assert.Equal(("17", "14"), (overtime.Home.Score, overtime.Away.Score));
        Assert.Equal("nfl", overtime.League);

        Assert.Equal(2, SportsAnswers.Matches(SavedSports.Answer("nba-schedule"), "nba").Count);
        Assert.All(SportsAnswers.Matches(SavedSports.Answer("mlb-schedule"), "mlb"), match => Assert.Equal(MatchState.Final, match.State));
    }

    [Theory]
    [InlineData("STATUS_POSTPONED", "post", false, MatchState.Postponed)]
    [InlineData("STATUS_SUSPENDED", "in", false, MatchState.Postponed)]
    [InlineData("STATUS_CANCELED", "post", false, MatchState.Canceled)]
    [InlineData("STATUS_ABANDONED", "post", false, MatchState.Canceled)]
    [InlineData("STATUS_HALFTIME", "in", false, MatchState.Live)]
    [InlineData("STATUS_DELAYED", "in", false, MatchState.Live)]
    [InlineData("STATUS_FINAL_AET", "post", true, MatchState.Final)]
    [InlineData("STATUS_SCHEDULED", "pre", false, MatchState.Scheduled)]
    public void EveryStatusIsAState(string name, string state, bool completed, MatchState expected)
    {
        // A saved fixture with another status, as ESPN writes one (its name, its state, done or not).
        var json = SavedSports.Answer("soccer-fixtures")
            .Replace("\"STATUS_SCHEDULED\"", $"\"{name}\"", StringComparison.Ordinal)
            .Replace("\"state\": \"pre\"", $"\"state\": \"{state}\"", StringComparison.Ordinal)
            .Replace("\"completed\": false", $"\"completed\": {(completed ? "true" : "false")}", StringComparison.Ordinal);

        Assert.All(SportsAnswers.Matches(json), match => Assert.Equal(expected, match.State));
    }

    [Fact]
    public void WhatIsNoAnswerReadsAsNoneAndIsTold()
    {
        foreach (var json in new[] { "", "<html>Service unavailable</html>", "[]", "{}", "{\"events\": 3}", "null" })
        {
            Assert.Empty(SportsAnswers.Matches(json));
            Assert.Empty(SportsAnswers.Teams(json));
            Assert.Equal(("", "", ""), SportsAnswers.TeamOf(json));
            Assert.False(SportsAnswers.Readable(json));
        }
        Assert.True(SportsAnswers.Readable("{\"events\": []}"));
        Assert.True(SportsAnswers.Readable(SavedSports.Answer("nba-schedule")));
        // A match without its sides, its id or its kick-off is no match.
        Assert.Empty(SportsAnswers.Matches("""{"events":[{"id":"1","date":"2026-10-20T19:00Z","competitions":[{"competitors":[]}]},{"date":"x","competitions":[]}]}"""));
    }

    [Fact]
    public void ASearchOffersTheTeamsOfTheTablesLeaguesOnly()
    {
        // The Chiefs, not the rugby sides, the cricket teams or South Africa's Kaizer Chiefs.
        var chiefs = SportsAnswers.Teams(SavedSports.Answer("search-chiefs"));
        var kansas = Assert.Single(chiefs);
        Assert.Equal(new SportsTeam("football", "nfl", "12", "Kansas City Chiefs", "KC", kansas.Logo), kansas);
        Assert.Equal("NFL · KC", SportsSearch.Detail(kansas));

        // Real Madrid in LALIGA; not its women's side, its under-19s in a cup nor the B side in the second division.
        var madrid = SportsAnswers.Teams(SavedSports.Answer("search-real-madrid"));
        Assert.Equal([("esp.1", "86")], madrid.Select(team => (team.League, team.Id)));

        // The Lakers and the college teams of the same name, each in its own league.
        var lakers = SportsAnswers.Teams(SavedSports.Answer("search-lakers"));
        Assert.Equal("13", lakers[0].Id);
        Assert.Equal(lakers.Count, lakers.Select(team => (team.League, team.Id)).Distinct().Count());
        Assert.All(lakers, team => Assert.NotNull(team.Of));
    }

    [Fact]
    public void TheRequestsCarryTheTeamAndTheLeagueAndNothingElse()
    {
        Assert.Equal(
            ["https://site.api.espn.com/apis/site/v2/sports/soccer/all/teams/83/schedule", "https://site.api.espn.com/apis/site/v2/sports/soccer/all/teams/83/schedule?fixture=true"],
            SportsAnswers.ScheduleUrls(SavedSports.Barcelona()));
        Assert.Equal(["https://site.api.espn.com/apis/site/v2/sports/football/nfl/teams/12/schedule"], SportsAnswers.ScheduleUrls(SavedSports.Chiefs()));
        Assert.Equal("https://site.api.espn.com/apis/site/v2/sports/soccer/ita.1/scoreboard?dates=20261019", SportsAnswers.ScoreboardUrl("soccer", "ita.1", new DateOnly(2026, 10, 19)));
        Assert.Equal("https://site.web.api.espn.com/apis/common/v3/search?query=real%20madrid&type=team&limit=20", SportsAnswers.SearchUrl("real madrid"));
        // ESPN files a Monday-night game, past midnight in UTC, under Monday in New York.
        Assert.Equal(new DateOnly(2026, 10, 19), SportsAnswers.EspnDay(new DateTime(2026, 10, 20, 0, 15, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void OnlyEspnsPagesAreOpenedAndOnlyItsPictureServerAskedForLogos()
    {
        Assert.True(SportsAnswers.Opens("https://www.espn.com/soccer/match/_/gameId/401882846"));
        Assert.False(SportsAnswers.Opens("http://www.espn.com/soccer"));
        Assert.False(SportsAnswers.Opens("https://espn.com.example.org/"));
        Assert.False(SportsAnswers.Opens("sportscenter://x-callback-url/showGame"));

        Assert.True(SportsAnswers.IsLogo("https://a.espncdn.com/i/teamlogos/soccer/500/83.png"));
        Assert.False(SportsAnswers.IsLogo("https://example.com/i/teamlogos/soccer/500/83.png"));
        Assert.Equal("https://a.espncdn.com/combiner/i?img=/i/teamlogos/soccer/500/83.png&w=64&h=64", SportsAnswers.LogoUrl("https://a.espncdn.com/i/teamlogos/soccer/500/83.png", 64));
        Assert.Equal("https://a.espncdn.com/combiner/i?img=/i/teamlogos/nfl/500-dark/scoreboard/kc.png&w=64&h=64", SportsAnswers.LogoUrl("https://a.espncdn.com/i/teamlogos/nfl/500/scoreboard/kc.png", 64, dark: true));
    }

    [Fact]
    public void AScoreboardMergedIntoASchedulesTheFresherAndTheLeaguesOthersBesideIt()
    {
        var answer = new SportsAnswer();
        answer.Matches.Add(SavedSports.Match("401874762", new DateTime(2026, 10, 19, 16, 30, 0, DateTimeKind.Utc), MatchState.Scheduled, team: "4007", other: "2925"));
        answer.Matches[0].LeagueName = "Italian Serie A";

        SportsAnswers.Merge(answer, SportsAnswers.Matches(SavedSports.Answer("soccer-scoreboard")), "4007");

        var monza = Assert.Single(answer.Matches);
        Assert.Equal((MatchState.Live, "53'"), (monza.State, monza.Clock));
        Assert.Equal("Serie A", monza.LeagueName);
        var other = Assert.Single(answer.Others);
        Assert.Equal("PAR", other.Home.Abbreviation);
    }
}
