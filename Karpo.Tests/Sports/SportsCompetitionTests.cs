// sports §479: the competitions the person named, read and said from ESPN's own answers saved on 2026-11-02
// (SavedSports): the World Cup's shoot-outs, extra time, rounds, groups and knockout months; the Champions League's
// two legs and aggregates, its final on penalties and its league phase; the Premier League's table with its
// columns and zones; the Swiss Super League as ESPN has it now; a team's schedule with its legs; the NHL's and
// the NBA's tables. Nothing reaches the network.

using Karpo.Dock.Panels;
using Karpo.Dock.Sports;

namespace Karpo.Tests.Sports;

public sealed class SportsCompetitionTests
{
    private static SportsMatch Between(IEnumerable<SportsMatch> matches, string home, string away) =>
        matches.Single(match => match.Home.Abbreviation == home && match.Away.Abbreviation == away);

    [Fact]
    public void AShootOutSaysWhoWonAndByHowMuch()
    {
        var knockout = SavedSports.WorldCupKnockout();
        var germany = Between(knockout, "GER", "PAR");
        Assert.Equal(MatchState.Final, germany.State);
        Assert.True(germany.Penalties);
        Assert.Equal(("3", "4"), (germany.Home.Shootout, germany.Away.Shootout));
        Assert.Equal("Round of 32", germany.Round);
        Assert.Equal(S.DockSportsPens, SportsText.Ended(germany));
        Assert.Equal(S.DockSportsPens, SportsText.Trail(germany, SportsShows.Final, SportsClock.Minutes, DateTime.Now));
        Assert.Equal($"Round of 32 · {S.DockSportsWonOnPenalties("Paraguay", "4–3")}", SportsText.Detail(germany, homeFirst: true, round: true));
        Assert.Equal(S.DockSportsWonOnPenalties("Paraguay", "4–3"), SportsText.Detail(germany, homeFirst: true, round: false));

        // The Champions League's final, Paris 1–1 Arsenal, 4–3 on penalties.
        var final = Assert.Single(SportsAnswers.Matches(SavedSports.Answer("ucl-final")));
        Assert.Equal("Final", final.Round);
        Assert.Equal(S.DockSportsWonOnPenalties("PSG", "4–3"), SportsText.Detail(final, homeFirst: true, round: false));
        Assert.Equal("Paris Saint-Germain", final.Home.FullName);

        // A shoot-out on now: its score so far, and "Pens" for its clock.
        var on = SportsAnswers.Matches(SavedSports.Answer("ucl-final")).Single();
        on.State = MatchState.Live;
        on.Status = "STATUS_SHOOTOUT";
        on.Home.Winner = false;
        Assert.Equal(S.DockSportsPens, SportsText.Clock(on, SportsClock.Minutes));
        Assert.Equal(S.DockSportsPenalties("4–3"), SportsText.Detail(on, homeFirst: true, round: false));
    }

    [Fact]
    public void ExtraTimeIsSaidAndAPlainFinalIsNot()
    {
        var knockout = SavedSports.WorldCupKnockout();
        var belgium = Between(knockout, "BEL", "SEN");
        Assert.True(belgium.ExtraTime);
        Assert.False(belgium.Penalties);
        Assert.Equal(S.DockSportsAfterExtraTime, SportsText.Ended(belgium));
        Assert.Equal("", SportsText.Detail(belgium, homeFirst: true, round: false));
        var final = Assert.Single(SavedSports.WorldCupAnswer().Matches);
        Assert.Equal(("ESP", "ARG", "Final"), (final.Home.Abbreviation, final.Away.Abbreviation, final.Round));
        Assert.Equal(S.DockSportsAfterExtraTime, SportsText.Ended(final));
        var plain = Between(knockout, "BRA", "JPN");
        Assert.False(plain.ExtraTime);
        Assert.Equal(S.DockSportsFinal, SportsText.Ended(plain));
        // Every round of the knockout, once each: 16, 8, 4, 2, the third place and the final.
        Assert.Equal(32, knockout.Count);
        Assert.Equal(["Round of 32", "Round of 16", "Quarterfinals", "Semifinals", "3rd-Place Match", "Final"], knockout.Select(match => match.Round).Distinct());
    }

    [Fact]
    public void ATieOfTwoLegsSaysItsLegAndItsAggregate()
    {
        var march = SportsAnswers.Matches(SavedSports.Answer("ucl-march"));
        var first = Between(march, "BODO", "SCP");
        Assert.Equal((1, "", false), (first.Leg, first.Home.Aggregate, first.Home.Advances));
        Assert.Equal(S.DockSportsFirstLeg, SportsText.Detail(first, homeFirst: true, round: false));
        var second = Between(march, "SCP", "BODO");
        Assert.Equal((2, "5", "3", true, false), (second.Leg, second.Home.Aggregate, second.Away.Aggregate, second.Home.Advances, second.Away.Advances));
        Assert.Equal(S.DockSportsAfterExtraTime, SportsText.Ended(second));
        Assert.Equal($"Round of 16 · {S.DockSportsSecondLeg} · {S.DockSportsAdvanceOnAggregate("Sporting", "5–3")}", SportsText.Detail(second, homeFirst: true, round: true));
        // The away side through: its aggregate first in the sentence.
        var chelsea = Between(march, "CHE", "PSG");
        Assert.Equal($"{S.DockSportsSecondLeg} · {S.DockSportsAdvanceOnAggregate("PSG", "8–2")}", SportsText.Detail(chelsea, homeFirst: true, round: false));
        // Not decided yet, on now: the aggregate so far, home side first.
        second.State = MatchState.Live;
        Assert.EndsWith(S.DockSportsAggregate("5–3"), SportsText.Detail(second, homeFirst: true, round: false), StringComparison.Ordinal);

        // A team's schedule says the same in its scores: Sporting's second leg, its stage its round.
        var schedule = SavedSports.SportingAnswer().Matches;
        var leg = schedule.Single(match => match.Leg == 2 && match.Plays("2980"));
        Assert.Equal(("Round of 16", "5", "3", true), (leg.Round, leg.Home.Aggregate, leg.Away.Aggregate, leg.Home.Advances));
        // A league's matchday has no round: its stage is the season's own name.
        Assert.All(schedule.Where(match => match.LeagueName == "Portuguese Liga"), match => Assert.Equal("", match.Round));
    }

    [Fact]
    public void ATableReadsEveryColumnAndItsZones()
    {
        var table = Assert.Single(SavedSports.PremierLeagueAnswer().Table);
        Assert.Equal(20, table.Rows.Count);
        var top = table.Rows[0];
        Assert.Equal((1, "Arsenal", "9", "8", "0", "1", "+9", "24"), (top.Rank, top.FullName, top.Played, top.Won, top.Drawn, top.Lost, top.Difference, top.Points));
        Assert.Equal(("#81D6AC", "Champions League"), (top.Zone, top.ZoneText));
        Assert.Contains(table.Rows, place => place.ZoneText.Contains("Relegation", StringComparison.OrdinalIgnoreCase));
        // Football: P, GD, Pts; wide W, D and L too. Hockey without draws; basketball's W, L and GB.
        Assert.Equal([S.DockSportsColumnPlayed, S.DockSportsColumnDifference, S.DockSportsColumnPoints], SportsColumns.Of("soccer", wide: false).Select(column => column.Head));
        Assert.Equal(6, SportsColumns.Of("soccer", wide: true).Count);
        Assert.Equal(["9", "8", "0", "1", "+9", "24"], SportsColumns.Of("soccer", wide: true).Select(column => column.Value(top)));
        var nhl = SportsAnswers.Table(SavedSports.Answer("standings-nhl"), "hockey")[0].Rows[0];
        Assert.Equal(["16", "12", "2", "+27", "26"], SportsColumns.Of("hockey", wide: true).Select(column => column.Value(nhl)));
        var nba = SportsAnswers.Table(SavedSports.Answer("standings-nba-full"), "basketball");
        Assert.Equal(2, nba.Count);
        Assert.All(nba.SelectMany(group => group.Rows), place => Assert.True(place.Won.Length > 0 && place.Lost.Length > 0 && place.Behind.Length > 0, place.Name));

        // Every group of a cup, each named; the league phase's 36 in one.
        var groups = SavedSports.WorldCupAnswer().Table;
        Assert.Equal(12, groups.Count);
        Assert.Equal("Group L", groups[^1].Name);
        Assert.Equal(36, Assert.Single(SavedSports.ChampionsLeagueAnswer().Table).Rows.Count);

        // A place's tooltip says it whole.
        var tip = SportsTips.Place(top, "soccer");
        Assert.StartsWith("1. Arsenal\nChampions League\n", tip, StringComparison.Ordinal);
        Assert.Contains($"{S.DockSportsColumnPointsName} 24", tip, StringComparison.Ordinal);
    }

    [Fact]
    public void ACupsKnockoutIsReadAMonthAtATimeAndAMonthOverOnce()
    {
        var stages = SportsAnswers.Stages(SavedSports.Answer("wc-now"));
        Assert.Equal(["Group", "Round of 32", "Rd of 16", "Quarterfinals", "Semifinals", "3rd-Place Match", "Final"], stages.Select(stage => stage.Label));
        Assert.Equal(6, SportsAnswers.KnockoutStages(stages).Count);
        Assert.Equal(["202606", "202607"], SportsAnswers.KnockoutMonths(stages, SavedSports.CupsSavedUtc));
        // During the groups, the knockout's months as far as five weeks on; before it is near, none.
        Assert.Equal(["202606"], SportsAnswers.KnockoutMonths(stages, new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(["202606", "202607"], SportsAnswers.KnockoutMonths(stages, new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Empty(SportsAnswers.KnockoutMonths(stages, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)));
        // The Champions League's league phase now: its knockout in February, further off than that.
        var ucl = SportsAnswers.Stages(SavedSports.Answer("ucl-now"));
        Assert.Equal("League Phase", ucl[0].Label);
        Assert.Empty(SportsAnswers.KnockoutMonths(ucl, SavedSports.CupsSavedUtc));
        Assert.Equal(["202702", "202703"], SportsAnswers.KnockoutMonths(ucl, new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc)));
        // A league's calendar is its days: no stages.
        Assert.Empty(SportsAnswers.Stages(SavedSports.Answer("league-day-1031")));
        Assert.Equal("https://site.api.espn.com/apis/site/v2/sports/soccer/fifa.world/scoreboard?dates=202607&limit=200", SportsAnswers.MonthUrl("soccer", "fifa.world", "202607"));

        var asked = new List<string>();
        Task<string> Get(string url, CancellationToken cancel, string list)
        {
            asked.Add(url);
            return Task.FromResult(SavedSports.Answer(url.Contains("202606", StringComparison.Ordinal) ? "wc-june-knockout" : "wc-july"));
        }
        var (matches, months) = SportsStore.Knockout(SavedSports.WorldCup(), stages, null, SavedSports.CupsSavedUtc, Get, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(32, matches.Count);
        Assert.Equal(["202606", "202607"], months);
        Assert.Equal(2, asked.Count);
        // Read again: both months are over and kept, so nothing is asked.
        var last = new SportsAnswer { Knockout = matches, KnockoutMonths = months };
        var (again, _) = SportsStore.Knockout(SavedSports.WorldCup(), stages, last, SavedSports.CupsSavedUtc.AddHours(1), Get, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(2, asked.Count);
        Assert.Equal(matches.Select(match => match.Id), again.Select(match => match.Id));
        // A month not over yet is asked every time, and one that fails keeps what the last answer held of it.
        Task<string> Fails(string url, CancellationToken cancel, string list) => throw new HttpRequestException("offline");
        var (kept, keptMonths) = SportsStore.Knockout(SavedSports.WorldCup(), stages, last, new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc), Fails, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(32, kept.Count);
        Assert.Equal(["202606"], keptMonths);
    }

    [Fact]
    public void EachCompetitionOffersTheViewsItHas()
    {
        SportsReading Reading(SportsOptions team, SportsAnswer answer, DateTime at) =>
            SportsReading.From(team, new SportsHeld(answer, at, null, false), at, TimeSpan.FromMinutes(15));
        Assert.Equal([SportsPage.Matches, SportsPage.Table, SportsPage.Knockout], Reading(SavedSports.WorldCup(), SavedSports.WorldCupAnswer(), SavedSports.CupsSavedUtc).Pages);
        Assert.Equal([SportsPage.Matches, SportsPage.Table], Reading(SavedSports.ChampionsLeague(), SavedSports.ChampionsLeagueAnswer(), SavedSports.CupsSavedUtc).Pages);
        Assert.Equal([SportsPage.Matches, SportsPage.Knockout], Reading(SavedSports.ChampionsLeague(), SavedSports.SecondLegsAnswer(), SavedSports.SecondLegsUtc).Pages);
        Assert.Equal([SportsPage.Matches, SportsPage.Table], Reading(SavedSports.PremierLeague(), SavedSports.PremierLeagueAnswer(), SavedSports.CupsSavedUtc).Pages);
        // ESPN keeps no table of the Swiss Super League's now, nor any match since September 2025.
        var swiss = Reading(SavedSports.SuperLeague(), SavedSports.SuperLeagueAnswer(), SavedSports.CupsSavedUtc);
        Assert.Equal([SportsPage.Matches], swiss.Pages);
        Assert.Equal(SportsShows.Last, swiss.View.Shows);
        // A team: no knockout of its own; its legs and aggregates are on its matches.
        Assert.Equal([SportsPage.Matches], Reading(SavedSports.Sporting(), SavedSports.SportingAnswer(), SavedSports.SecondLegsUtc).Pages);
    }

    [Fact]
    public void AnOldDateSaysItsYear()
    {
        var swiss = SavedSports.SuperLeagueAnswer().Matches[0];
        var now = SavedSports.CupsSavedUtc.ToLocalTime();
        Assert.Equal(Nls.ShortDate(new DateOnly(2025, 9, 28)), SportsText.Trail(swiss, SportsShows.Last, SportsClock.Minutes, now));
        Assert.Equal(Nls.DayAndShortDate(new DateOnly(2025, 9, 28), year: true), SportsText.DayOf(swiss.StartUtc, now));
        var recent = SavedSports.PremierLeagueAnswer().Matches[0];
        Assert.Equal(Nls.MonthDay(DateOnly.FromDateTime(recent.StartUtc.ToLocalTime())), SportsText.Trail(recent, SportsShows.Last, SportsClock.Minutes, now));
    }

    [Fact]
    public void AMatchsTooltipSaysItWhole()
    {
        var second = Between(SportsAnswers.Matches(SavedSports.Answer("ucl-march")), "SCP", "BODO");
        var tip = SportsTips.Match(second, homeFirst: true, SportsClock.Minutes, second.StartUtc.ToLocalTime().AddDays(2)).Split('\n');
        Assert.Equal($"{second.Home.FullName} 5–0 {second.Away.FullName}", tip[0]);
        Assert.Equal("UEFA Champions League · Round of 16", tip[1]);
        // §485: how long ago it finished, how, and when.
        Assert.StartsWith($"{S.DockSportsTipFinishedDaysAgo(2)} · {S.DockSportsAfterExtraTime} · ", tip[2], StringComparison.Ordinal);
        Assert.Equal($"{S.DockSportsSecondLeg} · {S.DockSportsAdvanceOnAggregate("Sporting", "5–3")}", tip[3]);
    }
}
