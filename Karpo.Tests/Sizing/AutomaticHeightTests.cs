// Automatic height for the earthquakes, the stopwatch and the week's weather (docs\widgets.md §388; the
// person, 2026-10-29: "auto height for them too", after the sports line had it, §371); the toggles, which had
// it too, are one form since §432, the whole rows of their buttons. Each is
// offered Automatic where a new line starts, and each line is as tall as what it draws at the most its line
// may be: drawn at that height it draws the same as at its most (nothing lost), ends under its last item
// (no room no item could take), and a taller most never makes it shorter. With nothing to list — a quiet
// week, a stopwatch with no laps, no toggles chosen — it is one row. At 100 % and 150 % and at the largest
// text. Runs with KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.Weather;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Sizing;

public sealed class AutomaticHeightTests
{
    // The week's weather left them in §590: its days are the daily forecast's seven, at a size.
    private static readonly ListKind[] Kinds = [ListKind.Quakes, ListKind.Stopwatch];

    private static readonly (float Scale, float Text)[] Looks = [(1f, 1f), (1.5f, 1f), (1f, 2.25f)];

    [Fact]
    public void AutomaticIsOfferedWhereANewLineStarts()
    {
        foreach (var kind in Kinds)
        {
            Assert.True(ListWidgets.Of(kind).Grows, $"{kind} is fixed only");
            var start = LineHeight.Automatic(kind.ListRange().Most);
            Assert.Equal(start, LineHeight.StartFor(kind));
            Assert.Equal(start, LineHeight.For(kind.New()));
        }
    }

    [UiFact]
    public void TheEarthquakesAreAsTallAsTheirRows()
    {
        var failures = new List<string>();
        SavedWeather.AtAFixedTime(() => OnStaThread(gfx =>
        {
            QuakesLine Line(QuakeHeld held)
            {
                var tile = (QuakesLine)Karpo.Dock.Islands.CreateLine(Config.IslandKey(ListFormPictures.Owner, "quakes"), Quakes(LineHeight.Automatic(2)), ListKind.Quakes, gfx, new Config())!;
                tile.Source = () => held;
                return tile;
            }
            using var busy = Line(QuakePictures.Week());
            Follows("a busy week", busy, Quakes, 8, (box, style) =>
            {
                var plan = busy.Plan(box, style, busy.Read());
                return ($"{(plan.One ? "one" : plan.Headed ? "headed" : "rows")} {plan.Rows.Count}",
                    !plan.One && plan.Headed && plan.Rows.Count > 0 ? plan.Rows[^1].bottom + style.Px(4) : null);
            }, failures);
            // §401: with nothing to list, its header over the one Status row, as the places' mockup draws it; one row
            // where its most is one.
            using var quiet = Line(new QuakeHeld { Quakes = [], FetchedUtc = Clock.Now().ToUniversalTime().AddMinutes(-1) });
            HeaderOverStatus("a quiet week", quiet, Quakes, failures);
            using var waiting = Line(QuakeHeld.None);
            HeaderOverStatus("nothing come", waiting, Quakes, failures);
        }));
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [UiFact]
    public void TheStopwatchIsAsTallAsItsLaps()
    {
        var failures = new List<string>();
        OnStaThread(gfx =>
        {
            var now = TimeSpan.FromSeconds(100);
            StopwatchTile Line(StopwatchState state)
            {
                var countdowns = new Countdowns();
                countdowns.Stopwatches.Set("stopwatch", state);
                return new StopwatchTile(Config.IslandKey(null, "stopwatch"), Item(ListKind.Stopwatch, LineHeight.Automatic(2)), gfx, new Config(), countdowns, () => now, () => ".");
            }
            var running = StopwatchState.Start(now - TimeSpan.FromSeconds(83.4));
            using var laps = Line(running.Lap(now - TimeSpan.FromSeconds(70)).Lap(now - TimeSpan.FromSeconds(41.2)).Lap(now - TimeSpan.FromSeconds(3.9)));
            Follows("three laps", laps, height => Item(ListKind.Stopwatch, height), 6, (box, style) =>
            {
                var plan = laps.Plan(box, style);
                var shown = plan.Laps.Count;
                return ($"laps {shown}", shown > 0 ? plan.Foot : null);
            }, failures);
            using var none = Line(running);
            OneRow("no laps", none, height => Item(ListKind.Stopwatch, height), failures);
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [UiFact]
    public void TheTogglesAreAsTallAsTheirRows()
    {
        var failures = new List<string>();
        OnStaThread(gfx =>
        {
            Toggle[] seven = [Toggle.AirplaneMode, Toggle.Vpn, Toggle.RotationLock, Toggle.EnergySaver, Toggle.NightLight, Toggle.MobileHotspot, Toggle.NearbyShare];
            CellItem Toggles(LineHeight height, Toggle[] chosen)
            {
                var item = Item(ListKind.Toggles, height);
                item.Toggles = new TogglesOptions { Shown = [.. chosen] };
                return item;
            }
            // §432: one form, every row of its buttons in the whole rows that hold them, all seven drawn.
            using var some = (TogglesTile)Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, "toggles"), Toggles(LineHeight.Automatic(2), seven), ListKind.Toggles, gfx, new Config())!;
            foreach (var (scale, text) in Looks)
            {
                var style = StyleFor(scale, text);
                var width = FolderFlyout.ListWidth(scale);
                var content = ((IListContent)some).ListHeight(width, style);
                var rows = TogglesTile.Lay(seven.Length, width, style);
                var plan = some.Plan(PopupPaint.Rect(0, 0, width, content), style);
                var at = $"seven toggles at {scale * 100:0} %, text {text * 100:0} %: {content:0.#} px";
                if (MathF.Abs(content - LineRows.Whole(rows.Height, style)) > 0.5f)
                {
                    failures.Add($"{at}, not the whole rows of its {rows.Height:0.#} px of buttons ({LineRows.Whole(rows.Height, style):0.#})");
                }
                if (plan.Buttons.Count != seven.Length || plan.Buttons.Any(button => button.bottom > content + 0.5f))
                {
                    failures.Add($"{at}: {plan.Buttons.Count} buttons drawn inside it");
                }
            }
            using var none = (TogglesTile)Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, "none"), Toggles(LineHeight.Automatic(2), []), ListKind.Toggles, gfx, new Config())!;
            OneRow("none chosen", none, height => Toggles(height, []), failures);
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private static CellItem Item(ListKind kind, LineHeight height)
    {
        var item = kind.New();
        item.Id = kind.Id();
        item.Height = height;
        item.Normalize();
        return item;
    }

    private static CellItem Quakes(LineHeight height)
    {
        var item = Item(ListKind.Quakes, height);
        item.Weather = SavedQuakes.Zurich();
        item.Weather.Quakes = new QuakeOptions { Radius = 2000, Magnitude = 2.5 };
        return item;
    }

    private static TileStyle StyleFor(float scale, float text)
    {
        var look = ListFormPictures.LookFor(true, text);
        return FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
    }

    /// <summary>
    /// <paramref name="tile"/> automatic up to one to <paramref name="most"/> rows: drawn at the height it
    /// measures, what <paramref name="drawn"/> says it draws is what it draws at its most, its last item's
    /// foot (where it has one) under a px from the line's, and never shorter for a taller most.
    /// </summary>
    private static void Follows(string what, CellIsland tile, Func<LineHeight, CellItem> item, int most,
        Func<D2D_RECT_F, TileStyle, (string Said, float? Foot)> drawn, List<string> failures)
    {
        foreach (var (scale, text) in Looks)
        {
            var style = StyleFor(scale, text);
            var width = FolderFlyout.ListWidth(scale);
            var before = 0f;
            for (var cells = LineHeight.Fewest; cells <= most * LineHeight.Row; cells++)
            {
                var height = LineHeight.Automatic(cells);
                tile.Configure(item(height), new Config());
                var content = ((IListContent)tile).ListHeight(width, style);
                var given = height.Given(content, LineRows.Cell(style));
                var where = $"{what} at {scale * 100:0} %, text {text * 100:0} %, automatic up to {cells} cells: {given:0.#} px";
                var mostPx = LineRows.Cells(cells, style);
                var atMost = drawn(PopupPaint.Rect(0, 0, width, mostPx), style).Said;
                var (said, drawnFoot) = drawn(PopupPaint.Rect(0, 0, width, given), style);
                // The air under the last item is the line's where its most has the room for it.
                var foot = drawnFoot is { } at ? MathF.Min(at, mostPx) : (float?)null;
                if (said != atMost)
                {
                    failures.Add($"{where} draws {said}, at its most {atMost}");
                }
                // A line is a row of the list at the least, whatever it holds, and whole cells (§442): less than a cell under its last item.
                if (foot is { } end && given - end >= LineRows.Cell(style) && given > LineRows.Px(1, style) + 0.5f)
                {
                    failures.Add($"{where}, {given - end:0.#} px under its last item");
                }
                if (foot is { } past && past > given + 0.5f)
                {
                    failures.Add($"{where}, its last item ends at {past:0.#} px");
                }
                if (given + 0.5f < before)
                {
                    failures.Add($"{where}, shorter than at {cells - 1} cells ({before:0.#} px)");
                }
                before = given;
            }
        }
    }

    /// <summary>With nothing to list, a header over the Status's row at every most of two rows and more; one row at a most of one.</summary>
    private static void HeaderOverStatus(string what, CellIsland tile, Func<LineHeight, CellItem> item, List<string> failures)
    {
        foreach (var (scale, text) in Looks)
        {
            var style = StyleFor(scale, text);
            foreach (var rows in new[] { 1, 2, 4, 8 })
            {
                tile.Configure(item(LineHeight.Automatic(rows * LineHeight.Row)), new Config());
                var content = ((IListContent)tile).ListHeight(FolderFlyout.ListWidth(scale), style);
                var given = LineHeight.Automatic(rows * LineHeight.Row).Given(content, LineRows.Cell(style));
                var two = 2 * LineRow.Height(style, caption: false);
                var expected = LineRows.Px(rows, style) + 0.5f >= two ? two : LineRows.Px(1, style);
                if (MathF.Abs(given - expected) > 0.5f)
                {
                    failures.Add($"{what} at {scale * 100:0} %, text {text * 100:0} %, automatic up to {rows}: {given:0.#} px, not {expected:0.#}");
                }
            }
        }
    }

    /// <summary>With nothing to list, one row at every most.</summary>
    private static void OneRow(string what, CellIsland tile, Func<LineHeight, CellItem> item, List<string> failures)
    {
        foreach (var (scale, text) in Looks)
        {
            var style = StyleFor(scale, text);
            foreach (var rows in new[] { 1, 4, 8 })
            {
                tile.Configure(item(LineHeight.Automatic(rows * LineHeight.Row)), new Config());
                var content = ((IListContent)tile).ListHeight(FolderFlyout.ListWidth(scale), style);
                if (MathF.Abs(content - LineRows.Px(1, style)) > 0.5f)
                {
                    failures.Add($"{what} at {scale * 100:0} %, text {text * 100:0} %, automatic up to {rows}: {content:0.#} px, not one row ({LineRows.Px(1, style):0.#})");
                }
            }
        }
    }
}
