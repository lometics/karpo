// sports §479: the competitions the person named, drawn from ESPN's saved answers (SavedSports) — the World Cup
// (its final, its round of 32's shoot-outs, a group day), the Champions League (its matchday now, its round of 16's
// second legs), the Premier League, the Swiss Super League as ESPN has it, Sporting after its second leg, the NBA —
// each in every list it has (§603: its fixtures and results, its table, its knockout, a widget each), at one, four and
// eight rows and whole, dark and light, at 100 % and 150 % (sports-<case>-<list>-<dark|light>-<100|150>.png in
// KARPO_PNG_DIR). Runs with KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.Sports;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Sports;

public sealed unsafe class SportsCompetitionPictures
{
    internal sealed record Case(string Name, DateTime At, SportsOptions Team, Func<SportsAnswer> Answer);

    internal static Case[] Cases =>
    [
        new("worldcup-final", SavedSports.CupsSavedUtc, SavedSports.WorldCup(), SavedSports.WorldCupAnswer),
        new("worldcup-shootouts", SavedSports.ShootoutsUtc, SavedSports.WorldCup(), SavedSports.WorldCupShootouts),
        new("worldcup-groups", SavedSports.GroupDayUtc, SavedSports.WorldCup(), SavedSports.WorldCupGroupDay),
        new("ucl-matchday", SavedSports.CupsSavedUtc, SavedSports.ChampionsLeague(), SavedSports.ChampionsLeagueAnswer),
        new("ucl-second-legs", SavedSports.SecondLegsUtc, SavedSports.ChampionsLeague(), SavedSports.SecondLegsAnswer),
        new("premier-league", SavedSports.CupsSavedUtc, SavedSports.PremierLeague(), SavedSports.PremierLeagueAnswer),
        new("super-league", SavedSports.CupsSavedUtc, SavedSports.SuperLeague(), SavedSports.SuperLeagueAnswer),
        new("sporting", SavedSports.SecondLegsUtc.AddHours(1), SavedSports.Sporting(), SavedSports.SportingAnswer),
        new("nba", SavedSports.LeagueSavedUtc, SavedSports.Nba(), () => { var answer = SavedSports.NbaAnswer(); answer.Table = SportsAnswers.Table(SavedSports.Answer("standings-nba-full"), "basketball"); return answer; }),
        // §485: games on now, played and to come, at a moment of the saved answers' own (SavedSports.AsOf).
        new("ucl-live", SavedSports.MatchdayLiveUtc, SavedSports.ChampionsLeague(), SavedSports.MatchdayLive),
        new("worldcup-live", SavedSports.KnockoutLiveUtc, SavedSports.WorldCup(), SavedSports.KnockoutLive),
        new("team-live", SavedSports.KnockoutLiveUtc, SavedSports.KnockoutTeamLive().Team, () => SavedSports.KnockoutTeamLive().Answer),
    ];

    /// <summary>§603: the sports widget showing <paramref name="view"/>: its fixtures and results, its table, its knockout.</summary>
    internal static ListKind KindOf(SportsPage view) => view switch
    {
        SportsPage.Table => ListKind.SportsTable,
        SportsPage.Knockout => ListKind.SportsKnockout,
        _ => ListKind.SportsFixtures,
    };

    /// <summary>A sports line of <paramref name="kind"/> (the score where none is said) following <paramref name="shown"/>'s team, held <paramref name="answer"/>.</summary>
    internal static SportsTile Line(Gfx gfx, Case shown, SportsAnswer answer, ListKind kind = ListKind.Sports, LineSize? size = null)
    {
        var item = kind.New();
        item.Id = "sports";
        item.Sports = shown.Team;
        item.Size = size ?? item.Size;
        var tile = (SportsTile)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", "sports"), item, kind, gfx, new Config())!;
        var held = new SportsHeld(answer, shown.At, null, false);
        tile.Source = new SportsSource { Read = () => held, Logo = SportsPictures.Logo };
        return tile;
    }

    [UiFact]
    public void EveryCompetitionInEveryView()
    {
        OnStaThread(gfx =>
        {
            foreach (var shown in Cases)
            {
                SportsPictures.At(shown.At, () =>
                {
                    using var score = Line(gfx, shown, shown.Answer());
                    foreach (var view in Views(score))
                    {
                        using var line = Line(gfx, shown, shown.Answer(), KindOf(view));
                        foreach (var (dark, scale) in new[] { (true, 1f), (false, 1f), (true, 1.5f), (false, 1.5f) })
                        {
                            Sheet(gfx, line, scale, ListFormPictures.LookFor(dark),
                                Path.Combine(Pictures, $"sports-{shown.Name}-{view.ToString().ToLowerInvariant()}-{(dark ? "dark" : "light")}-{scale * 100:0}.png"));
                        }
                    }
                });
            }
        });
    }

    /// <summary>
    /// At every list width, at 100 % and 150 % and the largest text: every match, every place and the scoreboard says
    /// itself whole in its tooltip (both sides' full names, a team's full name); every place's name has room for at
    /// least its abbreviation beside the columns; and each view lays out whole, scrolled where it is taller than the line.
    /// </summary>
    [UiFact]
    public void EveryRowSaysItselfWholeAndTheColumnsFit()
    {
        var failures = new List<string>();
        OnStaThread(gfx =>
        {
            foreach (var shown in Cases)
            {
                SportsPictures.At(shown.At, () =>
                {
                    using var score = Line(gfx, shown, shown.Answer());
                    var reading = score.Read();
                    foreach (var view in Views(score))
                    {
                        using var line = Line(gfx, shown, shown.Answer(), KindOf(view));
                        foreach (var listWidth in Enum.GetValues<FolderListWidth>())
                        {
                            foreach (var (scale, text) in new[] { (1f, 1f), (1.5f, 1f), (1f, 2.25f) })
                            {
                                var look = ListFormPictures.LookFor(true, text);
                                var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                                var width = FolderFlyout.ListWidth(scale, listWidth);
                                var where = $"{shown.Name}, {view}, {listWidth} at {scale * 100:0} %, text {text * 100:0} %";
                                var layout = line.Layout(width, style, reading);
                                var box = PopupPaint.Rect(0, 0, width, layout.Height);
                                if (layout.One)
                                {
                                    failures.Add($"{where}: lays out one row");
                                    continue;
                                }
                                if (line.ScrollHeight(width, LineRows.Px(4, style), style) < layout.Height - 1)
                                {
                                    failures.Add($"{where}: {layout.Height:0} px of it, and four rows of it do not scroll");
                                }
                                foreach (var row in layout.Rows)
                                {
                                    var tip = line.CardTip(new System.Numerics.Vector2(width / 2, row.Top + (row.Height / 2)), box, style, WidgetSurface.List)?.Text ?? "";
                                    if (row.Match is { } match && !(tip.Contains(Full(match.Home), StringComparison.Ordinal) && tip.Contains(Full(match.Away), StringComparison.Ordinal)))
                                    {
                                        failures.Add($"{where}: {match.Home.Abbreviation}–{match.Away.Abbreviation}'s tooltip \"{tip}\"");
                                    }
                                    if (row.Place is { } place)
                                    {
                                        if (!tip.Contains(place.FullName.Length > 0 ? place.FullName : place.Name, StringComparison.Ordinal))
                                        {
                                            failures.Add($"{where}: {place.Name}'s tooltip \"{tip}\"");
                                        }
                                        var (room, _) = line.NameRoom(reading, row.Group!, width, style);
                                        if (ListTiers.Name(gfx, [place.Abbreviation], style.Text(14), row.Own ? Weight.Semibold : Weight.Regular, room) is null)
                                        {
                                            failures.Add($"{where}: {place.Name} has {room:0} px beside its columns");
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(60)));

        static string Full(SportsSide side) => side.FullName.Length > 0 ? side.FullName : side.Name;
    }

    /// <summary>
    /// §484: no views at the line's head: the keys (§470) reach every row that opens something and nothing else, the list
    /// starts at the line's top and nothing of it stays while the rest scrolls; the view its settings pick is then that
    /// view's stops.
    /// </summary>
    [UiFact]
    public void TheLineDrawsNoSelectorAndTheKeysReachEveryRow()
    {
        OnStaThread(gfx => SportsPictures.At(SavedSports.CupsSavedUtc, () =>
        {
            var shown = Cases[0];
            using var score = Line(gfx, shown, shown.Answer());
            var look = ListFormPictures.LookFor(true);
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
            var width = FolderFlyout.ListWidth(1.5f);
            var box = PopupPaint.Rect(0, 0, width, LineRows.Px(8, style));
            Assert.Equal([SportsPage.Matches, SportsPage.Table, SportsPage.Knockout], Views(score));
            foreach (var (view, links) in new[] { (SportsPage.Matches, -1), (SportsPage.Knockout, 32), (SportsPage.Table, 48) })
            {
                using var line = Line(gfx, shown, shown.Answer(), KindOf(view));
                var layout = line.Layout(width, style, line.Read());
                Assert.False(layout.One, $"{view}");
                Assert.Equal(0f, layout.Rows[0].Top);
                Assert.Equal(0f, ((IListScroll)line).ScrollHead(width, box.bottom, style));
                var stops = LineFocus.Parts(line, box, style, WidgetSurface.List).Select(part => part.Target).ToList();
                Assert.Equal([.. Enumerable.Range(1, layout.Rows.Count(row => row.Link is { Length: > 0 }))], stops);
                if (links >= 0)
                {
                    Assert.Equal(links, stops.Count);
                }
                // Nothing at the head names a view; its tooltip is its first row's.
                var tip = line.CardTip(new System.Numerics.Vector2(width / 2, layout.Rows[0].Height / 2), box, style, WidgetSurface.List)?.Text ?? "";
                Assert.DoesNotContain(tip, new[] { S.DockSportsTable, S.DockSportsKnockout });
            }
        }));
    }

    /// <summary>
    /// §603: a table or a knockout its competition has none of says so on one row — the Premier League has no knockout, the
    /// NBA's team no knockout either —, and one it has is its rows from the line's top.
    /// </summary>
    [UiFact]
    public void AListItsCompetitionHasNoneOfSaysSo()
    {
        OnStaThread(gfx =>
        {
            var look = ListFormPictures.LookFor(true);
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
            var width = FolderFlyout.ListWidth(1.5f);
            var league = Cases[5];
            SportsPictures.At(league.At, () =>
            {
                using var knockout = Line(gfx, league, league.Answer(), ListKind.SportsKnockout);
                var none = knockout.Layout(width, style, knockout.Read());
                Assert.True(none.One);
                Assert.Equal(S.DockSportsNoKnockout, none.Said);
                Assert.Equal(LineRows.Px(1, style), knockout.ListHeight(width, style));
                using var table = Line(gfx, league, league.Answer(), ListKind.SportsTable);
                var rows = table.Layout(width, style, table.Read());
                Assert.False(rows.One);
                Assert.All(rows.Rows, row => Assert.True(row.Kind is SportsRowKind.Columns or SportsRowKind.Place, $"{row.Kind}"));
            });
            // Nothing come yet: why, not that it has none.
            var cup = Cases[0];
            SportsPictures.At(cup.At, () =>
            {
                var item = ListKind.SportsTable.New();
                item.Sports = cup.Team;
                using var line = (SportsTile)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", "table"), item, ListKind.SportsTable, gfx, new Config())!;
                line.Source = new SportsSource { Read = () => default, Logo = SportsPictures.Logo };
                var waiting = line.Layout(width, style, line.Read());
                Assert.True(waiting.One);
                Assert.Equal(S.DockSportsLoading, waiting.Said);
            });
        });
    }

    /// <summary>The views <paramref name="line"/> offers now.</summary>
    internal static IReadOnlyList<SportsPage> Views(SportsTile line) => line.Read().Pages;

    /// <summary><paramref name="line"/> at one, four and eight rows and at its whole content's height, side by side.</summary>
    internal static void Sheet(Gfx gfx, SportsTile line, float scale, Look look, string path)
    {
        var theme = FlyoutTheme.For(look);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var width = FolderFlyout.ListWidth(scale);
        line.ListScroll = 0;
        var whole = MathF.Max(LineRows.Px(2, style), line.ScrollHeight(width, LineRows.Px(24, style), style));
        (string Caption, float Height)[] panels = [("1 row", LineRows.Px(1, style)), ("4 rows", LineRows.Px(4, style)), ("8 rows", LineRows.Px(8, style)), ("All of it, scrolled through", whole)];
        var pad = PopupPaint.Round(16 * scale);
        var label = PopupPaint.Round(11 * scale);
        var labelLine = TilePaint.LineHeight(gfx, label);
        var sheetWidth = pad + (panels.Length * (width + pad));
        var sheetHeight = pad + labelLine + panels.Max(panel => panel.Height) + pad;
        Bitmap(sheetWidth, sheetHeight, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0,
                PopupPaint.Ink(look.Dark ? Windows.UI.Color.FromArgb(255, 0x1C, 0x1C, 0x1C) : Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8)));
            for (var at = 0; at < panels.Length; at++)
            {
                var (caption, height) = panels[at];
                var x = pad + (at * (width + pad));
                PopupPaint.Text(dc, gfx, caption, label, Weight.Regular, theme.TextSecondary, x, pad);
                var plate = PopupPaint.Rect(x, pad + labelLine, width, height);
                PopupPaint.Fill(dc, plate, PopupPaint.Round(8 * scale), PopupPaint.Ink(theme.Base));
                PopupPaint.Stroke(dc, plate, PopupPaint.Round(8 * scale), MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
                var context = dc;
                PopupPaint.Clipped(context, plate, PopupPaint.Round(8 * scale), () => Shifted(context, plate.left, plate.top, () =>
                    FolderFlyout.PaintContent(context, line, PopupPaint.Rect(0, 0, width, height), style)));
            }
        }, bitmap => Save(bitmap, path));
    }
}
