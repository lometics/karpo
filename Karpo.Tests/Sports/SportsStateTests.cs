// sports §485: where every match stands, at a glance — on now, played or to come — in a league's matchday, a
// knockout, a followed team's lists, the tooltips and the face. From ESPN's answers saved on 2026-11-02, rewound to a
// moment of their own (SavedSports.AsOf: the Champions League's second legs on 17 March at 21:10 UTC, the World Cup's
// second quarterfinal on 10 July at 19:40 UTC) with the clock held there. Nothing reaches the network.
//
// The pictures (sports-state-<case>-<view>-<dark|light>-<100|150|225text>.png and sports-state-faces-<dark|light>.png in
// KARPO_PNG_DIR) run with KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.Panels;
using Karpo.Dock.Sports;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Sports;

public sealed class SportsStateTests
{
    private static SportsMatch Between(IEnumerable<SportsMatch> matches, string home, string away) =>
        matches.Single(match => match.Home.Abbreviation == home && match.Away.Abbreviation == away);

    private static List<SportsMatch> KnockoutNow() => SavedSports.AsOf(SavedSports.WorldCupKnockout(), SavedSports.KnockoutLiveUtc);

    [Fact]
    public void ARowSaysWhereItsMatchStandsAndAResultUnderNoDayItsDay()
    {
        var knockout = KnockoutNow();
        var localNow = SavedSports.KnockoutLiveUtc.ToLocalTime();
        var live = Assert.Single(knockout, match => match.State == MatchState.Live);
        Assert.Equal("40'", SportsText.Row(live, SportsClock.Minutes, localNow, null));
        // Nothing to come has a score yet; everything before it is as it ended.
        Assert.All(knockout.Where(match => match.StartUtc > SavedSports.KnockoutLiveUtc), match => Assert.Equal((MatchState.Scheduled, ""), (match.State, match.Home.Score)));
        Assert.All(knockout.Where(match => match.StartUtc < live.StartUtc), match => Assert.Equal(MatchState.Final, match.State));

        // A result: how it ended under its day's heading; its day too under none (a round, a team's results).
        var belgium = Between(knockout, "BEL", "SEN");
        var day = DateOnly.FromDateTime(belgium.StartUtc.ToLocalTime());
        Assert.Equal(S.DockSportsAfterExtraTime, SportsText.Row(belgium, SportsClock.Minutes, localNow, day));
        Assert.Equal($"{S.DockSportsAfterExtraTime} · {Nls.MonthDay(day)}", SportsText.Row(belgium, SportsClock.Minutes, localNow, null));
        // Where the names would have no room: its day alone; one on now keeps its clock.
        Assert.Equal(Nls.MonthDay(day), SportsText.Short(belgium, localNow));
        Assert.Null(SportsText.Short(live, localNow));
        var brazil = Between(knockout, "BRA", "JPN");
        Assert.Equal($"{S.DockSportsFinal} · {Nls.MonthDay(DateOnly.FromDateTime(brazil.StartUtc.ToLocalTime()))}", SportsText.Row(brazil, SportsClock.Minutes, localNow, null));

        // To come: its time under its day, its day and time under none.
        var next = knockout.First(match => match.State == MatchState.Scheduled);
        Assert.Equal(SportsText.When(next, localNow), SportsText.Row(next, SportsClock.Minutes, localNow, null));
        Assert.Equal(Nls.Time(next.StartUtc.ToLocalTime()), SportsText.Row(next, SportsClock.Minutes, localNow, DateOnly.FromDateTime(next.StartUtc.ToLocalTime())));
        Assert.Equal(Nls.ShortDayName(next.StartUtc.ToLocalTime().DayOfWeek), SportsText.Short(next, localNow));
    }

    [Fact]
    public void AResultsLoserIsTheQuietSide()
    {
        var knockout = SavedSports.WorldCupKnockout();
        var brazil = Between(knockout, "BRA", "JPN");
        Assert.Equal(("2", "1"), (brazil.Home.Score, brazil.Away.Score));
        Assert.Equal((false, true), (SportsText.Lost(brazil, brazil.Home), SportsText.Lost(brazil, brazil.Away)));
        // A shoot-out's loser, its score level.
        var germany = Between(knockout, "GER", "PAR");
        Assert.Equal((true, false), (SportsText.Lost(germany, germany.Home), SportsText.Lost(germany, germany.Away)));
        // A draw has none, and neither has a game on now or to come.
        var draw = SavedSports.SportingAnswer().Matches.First(match => match.State == MatchState.Final && match.Home.Score == match.Away.Score);
        Assert.Equal((false, false), (SportsText.Lost(draw, draw.Home), SportsText.Lost(draw, draw.Away)));
        Assert.All(KnockoutNow().Where(match => match.State != MatchState.Final), match => Assert.False(SportsText.Lost(match, match.Home) || SportsText.Lost(match, match.Away)));

        // The row's names in their three parts, at every form they take.
        foreach (var name in new[] { SportsText.Named(brazil, true, full: true), SportsText.Named(brazil, true), SportsText.Score(brazil, true) })
        {
            var parts = SportsTile.Parts(brazil, true, name);
            Assert.NotNull(parts);
            Assert.Equal(name, parts.Value.First + parts.Value.Score + parts.Value.Second);
            Assert.Equal(" 2–1", parts.Value.Score);
        }
        Assert.Null(SportsTile.Parts(brazil, true, "Brazil 2–1 Jap…"));
    }

    [Fact]
    public void ATooltipSaysWhereItStandsInWords()
    {
        var knockout = KnockoutNow();
        var localNow = SavedSports.KnockoutLiveUtc.ToLocalTime();
        var live = knockout.Single(match => match.State == MatchState.Live);
        Assert.Equal(S.DockSportsTipLiveMinutes(40), SportsText.LiveSaid(live, SportsClock.Minutes));
        Assert.Equal(S.DockSportsTipLiveMinutes(40), SportsTips.Match(live, homeFirst: true, SportsClock.Minutes, localNow).Split('\n')[2]);
        var basketball = new SportsMatch { State = MatchState.Live, Status = "STATUS_IN_PROGRESS", Clock = "5:32", Period = 3 };
        Assert.Equal(S.DockSportsTipLive("Q3 5:32"), SportsText.LiveSaid(basketball, SportsClock.Quarters));
        live.Status = "STATUS_HALFTIME";
        Assert.Equal(S.DockSportsTipHalftime, SportsText.LiveSaid(live, SportsClock.Minutes));

        // Played: how long ago, by its day here.
        var brazil = Between(knockout, "BRA", "JPN");
        var played = brazil.StartUtc.ToLocalTime().Date;
        Assert.Equal(S.DockSportsTipFinishedToday, SportsText.Finished(brazil, played.AddHours(23).AddMinutes(59)));
        Assert.Equal(S.DockSportsTipFinishedYesterday, SportsText.Finished(brazil, played.AddDays(1).AddHours(12)));
        Assert.Equal(S.DockSportsTipFinishedDaysAgo(3), SportsText.Finished(brazil, played.AddDays(3).AddHours(12)));
        Assert.Equal(S.DockSportsTipFinished, SportsText.Finished(brazil, played.AddDays(40)));
        Assert.StartsWith(S.DockSportsTipFinishedDaysAgo(3) + " · ", SportsTips.Match(brazil, true, SportsClock.Minutes, played.AddDays(3).AddHours(12)).Split('\n')[2], StringComparison.Ordinal);

        // To come: when it starts.
        var next = knockout.First(match => match.State == MatchState.Scheduled);
        var start = next.StartUtc.ToLocalTime();
        var time = Nls.Time(start);
        Assert.Equal(S.DockSportsTipStartsToday(time), SportsText.Starts(next, start.Date.AddMinutes(1)));
        Assert.Equal(S.DockSportsTipStartsTomorrow(time), SportsText.Starts(next, start.Date.AddDays(-1).AddHours(12)));
        Assert.Equal(S.DockSportsTipStartsOn(Nls.DayAndShortDate(DateOnly.FromDateTime(start), year: false), time), SportsText.Starts(next, start.Date.AddDays(-3)));
        Assert.Equal(SportsText.Starts(next, localNow), SportsTips.Match(next, true, SportsClock.Minutes, localNow).Split('\n')[2]);
        next.TimeKnown = false;
        Assert.Equal(S.DockSportsTipStartsDay(Nls.DayAndShortDate(DateOnly.FromDateTime(start), year: false)), SportsText.Starts(next, start.Date.AddDays(-3)));
    }

    /// <summary>The style and the width of a list at <paramref name="scale"/>, dark.</summary>
    private static (TileStyle Style, float Width) Styled(float scale = 1.5f)
    {
        var look = ListFormPictures.LookFor(true);
        return (FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look)), FolderFlyout.ListWidth(scale));
    }

    /// <summary>The layout's headings, each with the matches under it.</summary>
    private static List<(SportsRow Heading, List<SportsMatch> Matches)> Groups(SportsLayout layout)
    {
        var groups = new List<(SportsRow, List<SportsMatch>)>();
        foreach (var row in layout.Rows)
        {
            if (row.Kind == SportsRowKind.Heading)
            {
                groups.Add((row, []));
            }
            else if (row.Match is { } match && groups.Count > 0 && row.Kind == SportsRowKind.Match)
            {
                groups[^1].Item2.Add(match);
            }
        }
        return groups;
    }

    private static SportsCompetitionPictures.Case Case(string name) => SportsCompetitionPictures.Cases.Single(shown => shown.Name == name);

    [UiFact]
    public void AMatchdayListsTheGamesOnNowFirstThenEachDay()
    {
        OnStaThread(gfx =>
        {
            var shown = Case("ucl-live");
            SportsPictures.At(shown.At, () =>
            {
                using var line = SportsCompetitionPictures.Line(gfx, shown, shown.Answer(), ListKind.SportsFixtures);
                var (style, width) = Styled();
                var groups = Groups(line.Layout(width, style, line.Read()));
                Assert.Equal(3, groups.Count);
                // "Live now" first, red, the competition before it: the three second legs on in their 55th minute.
                var (live, on) = groups[0];
                Assert.Equal((SportsTone.Live, true, "UEFA Champions League"), (live.Tone, live.Live, live.Lead));
                Assert.StartsWith(S.DockSportsLiveNow, live.Text, StringComparison.Ordinal);
                Assert.Equal(3, on.Count);
                Assert.All(on, match => Assert.Equal((MatchState.Live, "55'"), (match.State, match.Clock)));
                // Then today's result, and tomorrow's four to come, both strong; nothing on outside "Live now".
                var (today, played) = groups[1];
                Assert.StartsWith(S.DockCalendarToday, today.Words, StringComparison.Ordinal);
                Assert.Equal(SportsTone.Strong, today.Tone);
                Assert.Equal(MatchState.Final, Assert.Single(played).State);
                var (tomorrow, coming) = groups[2];
                Assert.StartsWith(S.DockCalendarTomorrow, tomorrow.Words, StringComparison.Ordinal);
                Assert.Equal(SportsTone.Strong, tomorrow.Tone);
                Assert.Equal(4, coming.Count);
                Assert.All(coming, match => Assert.Equal(MatchState.Scheduled, match.State));
                Assert.False(today.Live || tomorrow.Live);
            });

            // A day gone by is quiet: the Premier League's Sunday, seen on Monday.
            var premier = Case("premier-league");
            SportsPictures.At(premier.At, () =>
            {
                using var line = SportsCompetitionPictures.Line(gfx, premier, premier.Answer(), ListKind.SportsFixtures);
                var (style, width) = Styled();
                var (yesterday, _) = Groups(line.Layout(width, style, line.Read()))[0];
                Assert.Equal(SportsTone.Quiet, yesterday.Tone);
                Assert.StartsWith($"{premier.Team.Name} · {S.DockCalendarYesterday}", yesterday.Words, StringComparison.Ordinal);
            });
        });
    }

    [UiFact]
    public void AKnockoutsPlayedRoundsAreQuietAndItsLiveRoundIsBroughtIntoSight()
    {
        OnStaThread(gfx =>
        {
            var shown = Case("worldcup-live");
            SportsPictures.At(shown.At, () =>
            {
                using var line = SportsCompetitionPictures.Line(gfx, shown, shown.Answer(), ListKind.SportsKnockout);
                var (style, width) = Styled();
                var layout = line.Layout(width, style, line.Read());
                Assert.False(layout.One);
                var groups = Groups(layout);
                Assert.Equal(
                    [("Round of 32", SportsTone.Quiet, false), ("Round of 16", SportsTone.Quiet, false), ("Quarterfinals", SportsTone.Strong, true),
                        ("Semifinals", SportsTone.Strong, false), ("Third-place match", SportsTone.Strong, false), ("Final", SportsTone.Strong, false)],
                    groups.Select(group => (group.Heading.Text, group.Heading.Tone, group.Heading.Live)));
                // Shown, the round on now is scrolled to: its heading at the top, as much of it as fits.
                var quarterfinals = layout.Rows.First(row => row.Kind == SportsRowKind.Heading && row.Live);
                var band = line.ScrollReveal(width, style);
                Assert.NotNull(band);
                Assert.Equal(quarterfinals.Top, band.Value.Top);
                Assert.Equal(layout.Rows.Last(row => row.Match?.Round == "Quarterfinals").Bottom, band.Value.Bottom);
                Assert.Null(line.ScrollReveal(width, style));
            });

            // Every round played: nothing to bring into sight, the list from its top.
            var final = Case("worldcup-final");
            SportsPictures.At(final.At, () =>
            {
                using var line = SportsCompetitionPictures.Line(gfx, final, final.Answer(), ListKind.SportsKnockout);
                var (style, width) = Styled();
                var layout = line.Layout(width, style, line.Read());
                Assert.All(Groups(layout), group => Assert.Equal(SportsTone.Quiet, group.Heading.Tone));
                Assert.Null(SportsTile.Current(layout));
            });
        });
    }

    [UiFact]
    public void AFollowedTeamsBoardSaysLiveAndItsResultsAreQuiet()
    {
        OnStaThread(gfx =>
        {
            var shown = Case("team-live");
            SportsPictures.At(shown.At, () =>
            {
                using var score = SportsCompetitionPictures.Line(gfx, shown, shown.Answer());
                var (style, width) = Styled();
                var reading = score.Read();
                Assert.Equal(SportsShows.Live, reading.View.Shows);
                var board = Assert.Single(score.Layout(width, style, reading).Rows);
                Assert.Equal((SportsRowKind.Board, true), (board.Kind, board.Live));
                Assert.Equal($"{S.DockSportsLive} · 40'", SportsTile.Status(board.Match!, SportsShows.Live, SportsClock.Minutes, SavedSports.KnockoutLiveUtc.ToLocalTime()));
                // §603: its fixtures and results, the match on now first under "Live now", in the live red.
                using var line = SportsCompetitionPictures.Line(gfx, shown, shown.Answer(), ListKind.SportsFixtures);
                var layout = line.Layout(width, style, reading);
                Assert.Equal((SportsRowKind.Heading, SportsTone.Live, S.DockSportsLiveNow), (layout.Rows[0].Kind, layout.Rows[0].Tone, layout.Rows[0].Text));
                Assert.Equal(board.Match!.Id, layout.Rows[1].Match!.Id);
                var headings = layout.Rows.Where(row => row.Kind == SportsRowKind.Heading).ToDictionary(row => row.Text, row => row.Tone);
                Assert.Equal(SportsTone.Quiet, headings[S.DockSportsPlayed]);
                if (headings.TryGetValue(S.DockSportsComing, out var coming))
                {
                    Assert.Equal(SportsTone.Strong, coming);
                }
                // Each result says how it ended and its day at its end.
                Assert.All(layout.Rows.Where(row => row.Kind == SportsRowKind.Match && row.Match!.State == MatchState.Final), row => Assert.True(row.Dated));
            });

            // The last played, its season over: how it ended and its day at the scoreboard's head.
            var sporting = SavedSports.SportingAnswer().Matches.Last(match => match.State == MatchState.Final);
            var day = DateOnly.FromDateTime(sporting.StartUtc.ToLocalTime());
            Assert.Equal($"{SportsText.Ended(sporting)} · {Nls.MonthDay(day)}", SportsTile.Status(sporting, SportsShows.Last, SportsClock.Minutes, sporting.StartUtc.ToLocalTime().AddDays(3)));
        });
    }

    [Fact]
    public void TheFaceSaysAGameOnNowApartFromItsResult()
    {
        var (team, answer) = SavedSports.KnockoutTeamLive();
        var now = SavedSports.KnockoutLiveUtc;
        var live = SportsReading.From(team, new SportsHeld(answer, now, null, false), now, TimeSpan.FromMinutes(15));
        Assert.Equal(SportsShows.Live, live.View.Shows);
        Assert.EndsWith(" 40'", live.Said(now.ToLocalTime()), StringComparison.Ordinal);
        Assert.Contains(S.DockSportsTipLiveMinutes(40), live.Tooltip(now.ToLocalTime()), StringComparison.Ordinal);

        // The same game played, as ESPN saved it: its result, how it ended, and how long ago in the tooltip.
        var match = live.View.Match!;
        var played = new SportsAnswer { ScheduledUtc = now, Matches = [.. SavedSports.WorldCupKnockout().Where(each => each.Plays(team.Team))] };
        var later = match.StartUtc.AddHours(3);
        var result = SportsReading.From(team, new SportsHeld(played, later, null, false), later, TimeSpan.FromMinutes(15));
        Assert.Equal((SportsShows.Final, match.Id), (result.View.Shows, result.View.Match?.Id));
        Assert.EndsWith(SportsText.Ended(result.View.Match!), result.Said(later.ToLocalTime()), StringComparison.Ordinal);
        Assert.Contains(SportsText.Finished(match, later.ToLocalTime()), result.Tooltip(later.ToLocalTime()), StringComparison.Ordinal);
        Assert.DoesNotContain(S.DockSportsTipLiveMinutes(40), result.Tooltip(later.ToLocalTime()), StringComparison.Ordinal);
        Assert.NotEqual(live.Fingerprint(now.ToLocalTime()), result.Fingerprint(later.ToLocalTime()));
    }

    /// <summary>
    /// §485: the matchday, the knockout and the team as they stood, in each of their views, dark and light, at 100 % and
    /// 150 % and at 225 % text; and the face live beside its result.
    /// </summary>
    [UiFact]
    public void EveryStateDrawn()
    {
        var failures = new List<string>();
        OnStaThread(gfx =>
        {
            foreach (var name in new[] { "ucl-live", "worldcup-live", "team-live", "premier-league", "sporting" })
            {
                var shown = Case(name);
                SportsPictures.At(shown.At, () =>
                {
                    using var score = SportsCompetitionPictures.Line(gfx, shown, shown.Answer());
                    foreach (var view in SportsCompetitionPictures.Views(score).Where(view => view != SportsPage.Table))
                    {
                        using var line = SportsCompetitionPictures.Line(gfx, shown, shown.Answer(), SportsCompetitionPictures.KindOf(view));
                        foreach (var (dark, scale, text) in new[] { (true, 1f, 1f), (false, 1f, 1f), (true, 1.5f, 1f), (false, 1.5f, 1f), (true, 1f, 2.25f), (false, 1f, 2.25f) })
                        {
                            var size = text > 1 ? "225text" : $"{scale * 100:0}";
                            SportsCompetitionPictures.Sheet(gfx, line, scale, ListFormPictures.LookFor(dark, text),
                                Path.Combine(Pictures, $"sports-state-{name}-{view.ToString().ToLowerInvariant()}-{(dark ? "dark" : "light")}-{size}.png"));
                        }
                    }
                });
            }

            var (team, answer) = SavedSports.KnockoutTeamLive();
            var now = SavedSports.KnockoutLiveUtc;
            var match = answer.Matches.Single(each => each.State == MatchState.Live);
            var later = match.StartUtc.AddHours(3);
            var played = new SportsAnswer { ScheduledUtc = later, Matches = [.. SavedSports.WorldCupKnockout().Where(each => each.Plays(team.Team))] };
            var lastSeason = SavedSports.SportingAnswer();
            SportsPictures.Shot[] shots =
            [
                new($"{team.Name} on now", now, team, new SportsHeld(answer, now, null, false)),
                new($"{team.Name}, its result", later, team, new SportsHeld(played, later, null, false)),
                new("Sporting, its last result", SavedSports.SecondLegsUtc.AddDays(3), SavedSports.Sporting(), new SportsHeld(lastSeason, SavedSports.SecondLegsUtc.AddDays(3), null, false)),
                new($"{team.Name} on now, 150 %", now, team, new SportsHeld(answer, now, null, false), 1.5f),
                new($"{team.Name}, its result, 150 %", later, team, new SportsHeld(played, later, null, false), 1.5f),
                new("On its side, on now", now, team, new SportsHeld(answer, now, null, false), 1.5f, Vertical: true),
            ];
            foreach (var dark in new[] { true, false })
            {
                SportsPictures.Faces(gfx, shots, ListFormPictures.LookFor(dark), Path.Combine(Pictures, $"sports-state-faces-{(dark ? "dark" : "light")}.png"), failures);
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
