// §603: the sports line split into widgets of fixed content (docs\widgets.md §603). The person, 2026-11-09: "split all of
// them" — the score, its fixtures and results, its table and a cup's knockout, a widget each.
//
// So the score says the same at Compact and at Full — the competition, the two sides, the score or the kick-off, the
// clock, the round —, drawn twice as tall says nothing more, and its height is its size's whatever it shows; each list
// shows its own page's rows and nothing of the others'; and no line has a View to choose any more. Drawn on ESPN's saved
// answers (SavedSports) with the clock held still.
//
// Pictures (KARPO_PNG_DIR): sports-split-<case>-<dark|light>.png — the score at Compact and at Full, its fixtures and
// results, its table and its knockout side by side, at 150 %. Needs Direct2D: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.Sports;
using Karpo.Tests.Core;
using Karpo.Tests.Islands;
using Karpo.Tests.Sports;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed unsafe class SportsSplitTests
{
    private static readonly LineSize[] Sizes = [LineSize.Compact, LineSize.Full];

    private static readonly ListKind[] Lists = [ListKind.SportsFixtures, ListKind.SportsTable, ListKind.SportsKnockout];

    private sealed record Shown(string Name, DateTime At, SportsOptions? Team, SportsHeld Held);

    private static readonly DateTime Saved = SavedSports.SavedUtc;
    private static readonly DateTime AfterBetis = new(2026, 10, 17, 18, 40, 0, DateTimeKind.Utc);

    /// <summary>A team on now, its next match, its final, a league's day, a league with a game on, a cup's knockout on now.</summary>
    private static Shown[] Cases =>
    [
        new("live", Saved, SavedSports.Monza(), new SportsHeld(SportsPictures.MonzaWithFixtures(), Saved, null, false)),
        new("next", Saved, SavedSports.Barcelona(), new SportsHeld(SavedSports.BarcelonaAnswer(), Saved, null, false)),
        new("final", AfterBetis, SavedSports.Barcelona(), new SportsHeld(SavedSports.BarcelonaAnswer(), AfterBetis, null, false)),
        new("league", SavedSports.LeagueSavedUtc, SavedSports.Bundesliga(), new SportsHeld(SavedSports.BundesligaAnswer(), SavedSports.LeagueSavedUtc, null, false)),
        new("league-live", Saved, SportsPictures.SerieA(), new SportsHeld(SportsPictures.SerieAAnswer(), Saved, null, false)),
        new("worldcup-live", SavedSports.KnockoutLiveUtc, SavedSports.WorldCup(), new SportsHeld(SavedSports.KnockoutLive(), SavedSports.KnockoutLiveUtc, null, false)),
    ];

    /// <summary>Nothing to show: no team, nothing come yet, ESPN out of reach.</summary>
    private static Shown[] Nothing =>
    [
        new("unset", Saved, null, default),
        new("waiting", Saved, SavedSports.Barcelona(), default),
        new("unreachable", Saved, SavedSports.Barcelona(), new SportsHeld(null, null, S.DockSportsUnreachable, false)),
    ];

    /// <summary>A line of <paramref name="kind"/> following <paramref name="shown"/>'s team, at <paramref name="size"/> where it has one.</summary>
    private static SportsTile Line(Gfx gfx, Shown shown, ListKind kind, LineSize? size = null)
    {
        var item = kind.New();
        item.Id = "sports";
        item.Sports = shown.Team ?? new SportsOptions();
        item.Size = size ?? item.Size;
        item.Normalize();
        var tile = (SportsTile)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", "sports"), item, kind, gfx, new Config())!;
        var held = shown.Held;
        tile.Source = new SportsSource { Read = () => held, Logo = SportsPictures.Logo };
        return tile;
    }

    private static (TileStyle Style, float Width) Styled(float scale = 1.5f, float text = 1f)
    {
        var look = ListFormPictures.LookFor(true, text);
        return (FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look)), FolderFlyout.ListWidth(scale));
    }

    /// <summary>What a line says drawn into <paramref name="width"/> × <paramref name="height"/>: every word and figure, one by one.</summary>
    private static HashSet<string> Said(CellIsland line, float width, float height, in TileStyle style)
    {
        var said = new List<string>();
        var box = PopupPaint.Rect(0, 0, width, height);
        var drawn = style;
        PaintWatch.Said = (text, _) => said.Add(text);
        try
        {
            Bitmap(width, height, dc => line.PaintOn(dc, box, drawn, WidgetSurface.List), _ => { });
        }
        finally
        {
            PaintWatch.Said = null;
        }
        return [.. said.SelectMany(text => text.Split([" · ", " ", " ", " "], StringSplitOptions.RemoveEmptyEntries))];
    }

    /// <summary>
    /// The score at Compact and at Full says the competition, the round, the score's figures and each side (by one of its
    /// names), drawn twice as tall says nothing more; and with nothing to show both say why.
    /// </summary>
    [UiFact]
    public void TheScoreSaysTheSameAtEachSizeAndATallerLineNoMore()
    {
        var failures = new List<string>();
        OnStaThread(gfx =>
        {
            var (style, width) = Styled();
            foreach (var shown in Cases)
            {
                SportsPictures.At(shown.At, () =>
                {
                    foreach (var size in Sizes)
                    {
                        using var line = Line(gfx, shown, ListKind.Sports, size);
                        var reading = line.Read();
                        var match = reading.View.Match!;
                        var height = line.ListHeight(width, style);
                        var at = Said(line, width, height, style);
                        var taller = Said(line, width, height * 2, style);
                        var where = $"{shown.Name} {size}";
                        if (!at.SetEquals(taller))
                        {
                            failures.Add($"{where}: twice as tall it says {string.Join(", ", taller.Except(at))} more, {string.Join(", ", at.Except(taller))} less");
                        }
                        var detail = SportsText.Detail(match, reading.HomeFirst, round: true);
                        string[] pieces = [.. match.LeagueName.Split(' ', StringSplitOptions.RemoveEmptyEntries), .. detail.Split([" · ", " "], StringSplitOptions.RemoveEmptyEntries)];
                        if (match.State is MatchState.Live or MatchState.Final)
                        {
                            pieces = [.. pieces, match.Home.Score, match.Away.Score];
                        }
                        var said = string.Join(" ", at);
                        foreach (var piece in pieces.Where(piece => piece.Length > 0 && !said.Contains(piece, StringComparison.Ordinal)))
                        {
                            failures.Add($"{where}: says no {piece} ({said})");
                        }
                        // Each side by one of its names, the followed team's opponent at least.
                        var (_, other, _) = match.Sides(reading.Team.Team);
                        if (!new[] { other.FullName, other.Name, other.Abbreviation }.Any(name => name.Length > 0 && said.Contains(name.Split(' ')[0], StringComparison.Ordinal)))
                        {
                            failures.Add($"{where}: names no {other.Name} ({said})");
                        }
                    }
                });
            }
            foreach (var shown in Nothing)
            {
                SportsPictures.At(shown.At, () =>
                {
                    foreach (var size in Sizes)
                    {
                        using var line = Line(gfx, shown, ListKind.Sports, size);
                        // No team: the setup's own words (Set up in Settings); else why there is no match.
                        var why = shown.Team is null ? S.DockSportsNoTeam : line.Read().Said(Clock.Now());
                        var said = string.Join(" ", Said(line, width, line.ListHeight(width, style), style));
                        if (!why.Split(' ').All(word => said.Contains(word, StringComparison.Ordinal)))
                        {
                            failures.Add($"{shown.Name} {size}: says {said}, not {why}");
                        }
                    }
                });
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// The score's height is its size's whatever it shows — a match on now, one to come, a league's, nothing yet, no team —,
    /// at 100 % and 150 % and 225 % text: Compact the kit's row with its caption, Full the scoreboard, Compact the smaller;
    /// and it never scrolls.
    /// </summary>
    [UiFact]
    public void TheScoresHeightIsItsSizesWhateverItShows()
    {
        var failures = new List<string>();
        OnStaThread(gfx =>
        {
            foreach (var (scale, text) in new[] { (1f, 1f), (1.5f, 1f), (1f, 2.25f) })
            {
                var (style, width) = Styled(scale, text);
                var heights = new Dictionary<LineSize, HashSet<float>>();
                foreach (var shown in Cases.Concat(Nothing))
                {
                    SportsPictures.At(shown.At, () =>
                    {
                        foreach (var size in Sizes)
                        {
                            using var line = Line(gfx, shown, ListKind.Sports, size);
                            (heights.TryGetValue(size, out var seen) ? seen : heights[size] = []).Add(line.ListHeight(width, style));
                            if (line.ScrollHeight(width, LineRows.Px(1, style), style) != 0)
                            {
                                failures.Add($"{shown.Name} {size} scrolls");
                            }
                            var layout = line.Layout(width, style, line.Read());
                            // Its scoreboard at Full where it has a match; one row at Compact, and with nothing to show.
                            if ((size == LineSize.Compact || shown.Held.Shown is null) != layout.One)
                            {
                                failures.Add($"{shown.Name} {size}: {(layout.One ? "one row" : string.Join(", ", layout.Rows.Select(row => row.Kind)))}");
                            }
                            if (!layout.One && layout.Rows.Any(row => row.Kind != SportsRowKind.Board))
                            {
                                failures.Add($"{shown.Name} {size}: more than its scoreboard");
                            }
                        }
                    });
                }
                var where = $"{scale * 100:0} %, text {text * 100:0} %";
                if (heights[LineSize.Compact].Count != 1 || heights[LineSize.Full].Count != 1)
                {
                    failures.Add($"{where}: Compact {string.Join("/", heights[LineSize.Compact])}, Full {string.Join("/", heights[LineSize.Full])}");
                    continue;
                }
                if (heights[LineSize.Compact].Single() != LineRow.Height(style, caption: true))
                {
                    failures.Add($"{where}: Compact is {heights[LineSize.Compact].Single()}, not a row with its caption");
                }
                if (heights[LineSize.Compact].Single() >= heights[LineSize.Full].Single())
                {
                    failures.Add($"{where}: Compact is not the smaller");
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Each list shows its own page's rows and nothing of the others': the fixtures and results their headings and matches
    /// (a team's standing under them where there is no table), never a scoreboard, a table's place or a knockout's round; the
    /// table its groups' columns and places; the knockout its rounds' headings and their matches, every one of them its own.
    /// </summary>
    [UiFact]
    public void EachListShowsItsPageAndNothingOfTheOthers()
    {
        var failures = new List<string>();
        OnStaThread(gfx =>
        {
            var (style, width) = Styled();
            foreach (var shown in Cases)
            {
                SportsPictures.At(shown.At, () =>
                {
                    foreach (var kind in Lists)
                    {
                        using var line = Line(gfx, shown, kind);
                        var reading = line.Read();
                        var layout = line.Layout(width, style, reading);
                        var where = $"{shown.Name} {kind}";
                        if (layout.One)
                        {
                            // Only where its competition has none of it, which it then says.
                            var page = kind == ListKind.SportsTable ? SportsPage.Table : SportsPage.Knockout;
                            if (kind == ListKind.SportsFixtures || reading.Pages.Contains(page)
                                || layout.Said != (page == SportsPage.Table ? S.DockSportsNoTable : S.DockSportsNoKnockout))
                            {
                                failures.Add($"{where}: one row \"{layout.Said}\"");
                            }
                            continue;
                        }
                        SportsRowKind[] allowed = kind switch
                        {
                            ListKind.SportsTable => [SportsRowKind.Columns, SportsRowKind.Place],
                            ListKind.SportsKnockout => [SportsRowKind.Heading, SportsRowKind.Match],
                            _ => [SportsRowKind.Heading, SportsRowKind.Match, SportsRowKind.Note],
                        };
                        foreach (var row in layout.Rows.Where(row => !allowed.Contains(row.Kind)))
                        {
                            failures.Add($"{where}: a {row.Kind} row");
                        }
                        if (kind == ListKind.SportsKnockout && layout.Rows.Where(row => row.Match is not null).Any(row => !reading.Knockout.Contains(row.Match!)))
                        {
                            failures.Add($"{where}: a match not of its knockout");
                        }
                        if (kind == ListKind.SportsFixtures && reading.View.Match is { } shownMatch && !layout.Rows.Any(row => row.Match?.Id == shownMatch.Id))
                        {
                            failures.Add($"{where}: the score's match is not among its fixtures and results");
                        }
                    }
                });
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>No sports line has a View to choose any more: its team is its one row of what it shows, whatever its kind.</summary>
    [Fact]
    public void NoLineHasAView()
    {
        foreach (var kind in (ListKind[])[ListKind.Sports, .. Lists])
        {
            var line = kind.New();
            line.Sports = SavedSports.Barcelona();
            var rows = OptionRows.Of(line);
            Assert.Contains(rows, row => row.Key == OptionRows.SportsTeam);
            Assert.DoesNotContain(rows, row => row.Key == "SportsViewIndex" || row.Title == "View");
        }
    }

    /// <summary>The score at Compact and at Full, its fixtures and results, its table and its knockout, side by side, at 150 %.</summary>
    [UiFact]
    public void SplitPictures()
    {
        OnStaThread(gfx =>
        {
            foreach (var shown in new[] { Cases[0], Cases[3], Cases[5] })
            {
                SportsPictures.At(shown.At, () =>
                {
                    foreach (var dark in new[] { true, false })
                    {
                        Sheet(gfx, shown, dark, Path.Combine(Offscreen.Pictures, $"sports-split-{shown.Name}-{(dark ? "dark" : "light")}.png"));
                    }
                });
            }
        });
    }

    private static void Sheet(Gfx gfx, Shown shown, bool dark, string path)
    {
        const float scale = 1.5f;
        var look = ListFormPictures.LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var width = FolderFlyout.ListWidth(scale);
        (string Caption, ListKind Kind, LineSize? Size)[] panels =
        [
            ("Score · Compact", ListKind.Sports, LineSize.Compact), ("Score · Full", ListKind.Sports, LineSize.Full),
            ("Fixtures and results", ListKind.SportsFixtures, null), ("Table", ListKind.SportsTable, null), ("Knockout", ListKind.SportsKnockout, null),
        ];
        var lines = panels.Select(panel => Line(gfx, shown, panel.Kind, panel.Size)).ToList();
        try
        {
            var most = LineRows.Px(12, style);
            var heights = lines.Select(line => MathF.Min(most, line.ListHeight(width, style))).ToList();
            var pad = PopupPaint.Round(16 * scale);
            var label = PopupPaint.Round(11 * scale);
            var labelLine = TilePaint.LineHeight(gfx, label);
            var sheetWidth = pad + (panels.Length * (width + pad));
            var sheetHeight = pad + labelLine + heights.Max() + pad;
            Bitmap(sheetWidth, sheetHeight, dc =>
            {
                PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0,
                    PopupPaint.Ink(look.Dark ? Windows.UI.Color.FromArgb(255, 0x1C, 0x1C, 0x1C) : Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8)));
                for (var at = 0; at < panels.Length; at++)
                {
                    var x = pad + (at * (width + pad));
                    PopupPaint.Text(dc, gfx, panels[at].Caption, label, Weight.Regular, theme.TextSecondary, x, pad);
                    var plate = PopupPaint.Rect(x, pad + labelLine, width, heights[at]);
                    PopupPaint.Fill(dc, plate, PopupPaint.Round(8 * scale), PopupPaint.Ink(theme.Base));
                    PopupPaint.Stroke(dc, plate, PopupPaint.Round(8 * scale), MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
                    var context = dc;
                    var line = lines[at];
                    var height = heights[at];
                    PopupPaint.Clipped(context, plate, PopupPaint.Round(8 * scale), () => Shifted(context, plate.left, plate.top, () =>
                        FolderFlyout.PaintContent(context, line, PopupPaint.Rect(0, 0, width, height), style)));
                }
            }, bitmap => Save(bitmap, path));
        }
        finally
        {
            foreach (var line in lines)
            {
                line.Dispose();
            }
        }
    }
}
