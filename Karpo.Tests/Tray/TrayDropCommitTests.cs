// A dragged tray icon lands exactly where the gap was shown when it is let go.
//
// The person: "when reordering the tray icons it sometimes happens that when releasing the icons it
// moves to another spot than the one i put it to". The drop was read again from the release point
// after the drag had been forgotten (Dock.EndTrayDrag cleared it first): without its own place an
// icon moved along the bar was read by the overflow's rule (TrayDrags.IndexAt, the dragged one
// counted among the others), a place too far whenever the pointer was past a cell's middle, and one
// moved within the overflow a row too far down; without the place the last move gave, the hysteresis
// that held the gap was gone too. The drop now commits what the last move showed.
//
// The drag is played here as the dock plays it — each move read with the place the last one gave
// (TrayDrags.OnLayout, Flyout.DropAt), the gap drawn as the bar and the overflow draw it (Scene.TrayDrag,
// Flyout.GapPlaces), the last reading written as the dock writes it (TrayLayout.Apply) — over many
// orders, states, paths and boundaries: the order written is the order shown.

using Karpo.Dock;
using TerraFX.Interop.Windows;

// Not Karpo.Tests.Tray: a local named "tray" elsewhere in the tests would shadow a namespace of that name.
namespace Karpo.Tests.TrayIcons;

public sealed class TrayDropCommitTests
{
    private static Layout Bar(Edge edge, int icons, bool chevron)
    {
        var config = new Config { Height = 48, Style = BarStyle.Islands };
        List<PartSpec> specs =
        [
            new(Config.IslandKey(null, "battery"), BarKind.EnergySaver),
            new(Config.IslandKey(null, "tray"), BarKind.Tray),
        ];
        var area = new RECT { left = 0, top = 0, right = 1920, bottom = 1080 };
        return Layout.ForTests(96, area, edge, config, specs, Layout.CellPx(96, config), tray: icons, trayOverflow: chevron);
    }

    /// <summary>The screen point <paramref name="steps"/> icon steps past the first icon's start, across the tray's middle.</summary>
    private static POINT Along(Layout layout, float steps)
    {
        var block = layout.PartAt(layout.PartOf(Hit.TrayOverflow));
        var along = block.Left + (layout.TrayOverflow ? layout.TrayStep : 0f) + steps * layout.TrayStep;
        var cell = layout.ItemRect(Hit.TrayOverflow);
        return layout.Vertical
            ? new POINT { x = layout.X + (int)(cell.X + cell.Width / 2f), y = layout.Y + (int)MathF.Round(along) }
            : new POINT { x = layout.X + (int)MathF.Round(along), y = layout.Y + (int)(cell.Y + cell.Height / 2f) };
    }

    /// <summary>The tray as the dock holds it: a config with an order and states, and the icons Windows reports.</summary>
    private static (TrayIconOptions Config, List<TrayEntry> Icons) Tray(Random random, int count)
    {
        var icons = Enumerable.Range(0, count).Select(index => new TrayEntry($"app{index}|1", random.Next(3) == 0)).ToList();
        var config = new TrayIconOptions();
        foreach (var icon in icons)
        {
            // Some chosen (hidden completely among them), the rest Windows' own choice.
            switch (random.Next(5))
            {
                case 0: config.States[icon.Identity] = TrayState.Taskbar; break;
                case 1: config.States[icon.Identity] = TrayState.Overflow; break;
                case 2: config.States[icon.Identity] = TrayState.Hidden; break;
            }
        }
        // An order that names some of them, and one whose app is not running.
        config.Order = [.. icons.Select(icon => icon.Identity).Where(_ => random.Next(3) > 0).OrderBy(_ => random.Next()), "gone|1"];
        return (config, icons);
    }

    /// <summary>A pointer's way along the tray: wandering, and resting near the lines between places.</summary>
    private static List<float> Path(Random random, int cells)
    {
        var path = new List<float>();
        var at = (float)(random.NextDouble() * cells);
        for (var move = random.Next(1, 12); move > 0; move--)
        {
            at = random.Next(3) == 0
                // Close to a line, either side of the hysteresis.
                ? random.Next(-1, cells + 1) + (random.Next(2) == 0 ? 0.5f : 0f) + (float)(random.NextDouble() * 0.5 - 0.25)
                : Math.Clamp(at + (float)(random.NextDouble() * 2 - 1), -0.4f, cells + 0.4f);
            path.Add(at);
        }
        return path;
    }

    /// <summary>The order the bar shows: the others closed up, the dragged one in the gap (Scene.TrayDrag).</summary>
    private static List<string> ShownOnBar(List<string> drawn, string dragged, int gap)
    {
        var order = drawn.Where(identity => identity != dragged).ToList();
        order.Insert(Math.Clamp(gap, 0, order.Count), dragged);
        return order;
    }

    /// <summary>
    /// Along the bar, and out of the overflow onto it, on a bottom and a side bar, with and without the
    /// chevron: the icons on the bar after the drop are in the order the bar showed as it was let go.
    /// </summary>
    [Fact]
    public void ADropOnTheBarLandsWhereTheGapWasShown()
    {
        var random = new Random(20261009);
        var checkedDrops = 0;
        for (var trial = 0; trial < 3000; trial++)
        {
            var (config, icons) = Tray(random, random.Next(2, 10));
            var (shownEntries, hiddenEntries) = TrayLayout.Split(config, icons);
            var shown = shownEntries.Select(icon => icon.Identity).ToList();
            var hidden = hiddenEntries.Select(icon => icon.Identity).ToList();
            var fromBar = random.Next(3) > 0;
            if (fromBar ? shown.Count == 0 : hidden.Count == 0 || shown.Count == 0)
            {
                continue;
            }
            var edge = random.Next(2) == 0 ? Edge.Bottom : Edge.Left;
            var layout = Bar(edge, shown.Count, chevron: random.Next(2) == 0 || hidden.Count > 0);
            var heldAt = fromBar ? random.Next(shown.Count) : -1;
            var dragged = fromBar ? shown[heldAt] : hidden[random.Next(hidden.Count)];

            // Every move read with the place the last one gave (Dock.TrayTargetAt).
            TrayDrop? drop = null;
            foreach (var steps in Path(random, shown.Count))
            {
                var current = drop is { Area: TrayArea.Shown } last ? last.Index : -1;
                drop = TrayDrags.OnLayout(layout, Along(layout, steps), shown.Count, hidden.Count, heldAt, current);
            }
            if (drop is not { Area: TrayArea.Shown } landing)
            {
                continue;
            }
            var expected = ShownOnBar(shown, dragged, landing.Index);
            var neighbours = shown.Where(identity => identity != dragged).ToList();

            var next = config.Copy();
            _ = TrayLayout.Apply(next, icons, dragged, landing, neighbours);

            Assert.Equal(expected, TrayLayout.Split(next, icons).Shown.Select(icon => icon.Identity));
            checkedDrops++;
        }
        Assert.True(checkedDrops > 1000, $"{checkedDrops} drops checked");
    }

    /// <summary>An overflow of <paramref name="count"/> icons in a grid of five, 32 px a cell, its fills 28 px.</summary>
    private static List<(float X, float Y, float W, float H)> Grid(int count) =>
        [.. Enumerable.Range(0, count).Select(index => (index % 5 * 32f, index / 5 * 32f, 28f, 28f))];

    /// <summary>
    /// Within the overflow, its first rows icons the bar has no room for: the hidden icons after the
    /// drop are in the order the overflow showed as it was let go.
    /// </summary>
    [Fact]
    public void ADropInTheOverflowLandsWhereTheGapWasShown()
    {
        var random = new Random(91);
        var checkedDrops = 0;
        for (var trial = 0; trial < 3000; trial++)
        {
            var (config, icons) = Tray(random, random.Next(2, 14));
            var (shownEntries, hiddenEntries) = TrayLayout.Split(config, icons);
            var shown = shownEntries.Select(icon => icon.Identity).ToList();
            var hidden = hiddenEntries.Select(icon => icon.Identity).ToList();
            var drawn = random.Next(shown.Count + 1);
            // What the overflow holds: the icons the bar has no room for, then the hidden ones.
            var rows = TrayDrags.Overflow(shown, drawn, hidden);
            var spilled = shown.Count - drawn;
            if (rows.Count == 0)
            {
                continue;
            }
            var fills = Grid(rows.Count);
            var held = random.Next(rows.Count);
            var dragged = rows[held];

            // Every move read with the row the last one gave (Dock.ShowTrayDrag, Flyout.DropIndex).
            var gap = -1;
            for (var move = random.Next(1, 10); move > 0; move--)
            {
                var (fx, fy, _, _) = fills[random.Next(rows.Count)];
                var x = fx + (float)(random.NextDouble() * 40 - 6);
                var y = fy + (float)(random.NextDouble() * 40 - 6);
                gap = Flyout.DropAt(fills, x, y, grid: true, held, gap);
            }
            var landing = TrayDrags.InOverflow(gap, rows.Count, held, spilled);

            // The order the overflow shows: the others closed up, the dragged one in the gap (Flyout.GapPlaces).
            var places = Flyout.GapPlaces(fills, gap, held, grid: true);
            var cells = Enumerable.Range(0, rows.Count).Where(row => row != held).Select(row => (Row: row, places[row].X, places[row].Y)).ToList();
            var into = Math.Clamp(gap > held ? gap - 1 : gap, 0, rows.Count - 1);
            var seen = rows.Where((_, row) => row != held).ToList();
            seen.Insert(into, dragged);
            // Of those, the hidden ones, the dragged one with them now.
            var expected = seen.Where(identity => identity == dragged || hidden.Contains(identity)).ToList();
            var neighbours = rows.Skip(spilled).Where(identity => identity != dragged).ToList();

            var next = config.Copy();
            _ = TrayLayout.Apply(next, icons, dragged, landing, neighbours);

            Assert.Equal(expected, TrayLayout.Split(next, icons).Overflow.Select(icon => icon.Identity));
            // And that is the order drawn: the others in their cells, the dragged one in the cell they leave free.
            var last = fills[^1];
            List<(float X, float Y)> all = [.. fills.Select(fill => (fill.X, fill.Y)), rows.Count % 5 != 0 ? (last.X + 32f, last.Y) : (0f, last.Y + 32f)];
            var free = all.First(cell => !cells.Any(other => other.X == cell.X && other.Y == cell.Y));
            var drawnOrder = cells.Select(cell => (rows[cell.Row], cell.X, cell.Y)).Append((dragged, free.X, free.Y))
                .OrderBy(cell => cell.Y).ThenBy(cell => cell.X).Select(cell => cell.Item1);
            Assert.Equal(seen, drawnOrder);
            checkedDrops++;
        }
        Assert.True(checkedDrops > 1000, $"{checkedDrops} drops checked");
    }

    /// <summary>
    /// The defect: four icons on the bar, the first dragged right and let go on the right half of the
    /// third cell. The bar showed it third; read again at the drop without the drag — its own place
    /// and the place shown forgotten — it went last.
    /// </summary>
    [Fact]
    public void AnIconLetGoPastACellsMiddleLandsInThatCell()
    {
        var layout = Bar(Edge.Bottom, 4, chevron: true);
        List<TrayEntry> icons = [new("a|1", false), new("b|1", false), new("c|1", false), new("d|1", false)];
        var config = new TrayIconOptions();
        var at = Along(layout, 2.7f);

        var shown = TrayDrags.OnLayout(layout, at, 4, 0, heldAt: 0, current: 2);
        var forgotten = TrayDrags.OnLayout(layout, at, 4, 0);

        Assert.Equal(new TrayDrop(TrayArea.Shown, 2), shown);
        Assert.Equal(new TrayDrop(TrayArea.Shown, 3), forgotten);
        var next = config.Copy();
        Assert.True(TrayLayout.Apply(next, icons, "a|1", shown!.Value, ["b|1", "c|1", "d|1"]));
        Assert.Equal(["b|1", "c|1", "a|1", "d|1"], TrayLayout.Split(next, icons).Shown.Select(icon => icon.Identity));
    }

    /// <summary>
    /// The same in the overflow: the first of four hidden icons let go on the third. The overflow
    /// showed it third; read without its own row it went a row on.
    /// </summary>
    [Fact]
    public void AnIconMovedDownTheOverflowLandsOnTheRowItWasLetGoOn()
    {
        var fills = Grid(4);
        var (x, y) = (fills[2].X + 20, fills[2].Y + 14);

        var shown = TrayDrags.InOverflow(Flyout.DropAt(fills, x, y, grid: true, held: 0), 4, from: 0);
        var forgotten = TrayDrags.InOverflow(Flyout.DropAt(fills, x, y, grid: true), 4);

        Assert.Equal(new TrayDrop(TrayArea.Overflow, 2), shown);
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 3), forgotten);
    }

    /// <summary>
    /// The hysteresis held the gap past a line as the pointer rested there; the drop read without the
    /// place shown took the other side of it.
    /// </summary>
    [Fact]
    public void ADropJustPastALineKeepsThePlaceTheGapHeld()
    {
        var layout = Bar(Edge.Bottom, 5, chevron: true);
        // Over the third cell, then a tenth of a step into the fourth: the gap stays at the third.
        var shown = TrayDrags.OnLayout(layout, Along(layout, 2.5f), 5, 0, heldAt: 0);
        Assert.Equal(new TrayDrop(TrayArea.Shown, 2), shown);
        var resting = Along(layout, 3.1f);
        Assert.Equal(shown, TrayDrags.OnLayout(layout, resting, 5, 0, heldAt: 0, current: 2));
        Assert.NotEqual(shown, TrayDrags.OnLayout(layout, resting, 5, 0, heldAt: 0));
    }

    /// <summary>
    /// The tray changed under the drag — an icon gone, one come — after the place was shown: the icon
    /// still lands between the neighbours it was shown between, not at the count it had.
    /// </summary>
    [Fact]
    public void AnIconLandsBetweenTheNeighboursItWasShownBetween()
    {
        var config = new TrayIconOptions();
        // Shown as a, b, [e], c, d: e from the overflow before c.
        var seen = new List<string> { "a|1", "b|1", "c|1", "d|1" };
        var drop = new TrayDrop(TrayArea.Shown, 2);
        // Then a's app quit, and a new one came first in the order.
        config.Order = ["new|1", "b|1", "c|1", "d|1"];
        List<TrayEntry> now = [new("new|1", false), new("b|1", false), new("c|1", false), new("d|1", false), new("e|1", true)];

        Assert.True(TrayLayout.Apply(config, now, "e|1", drop, seen));

        Assert.Equal(["new|1", "b|1", "e|1", "c|1", "d|1"], TrayLayout.Split(config, now).Shown.Select(icon => icon.Identity));
    }

    [Fact]
    public void AtTheEndItGoesAfterTheLastItWasShownAfter()
    {
        var config = new TrayIconOptions { Order = ["a|1", "b|1", "c|1"] };
        List<TrayEntry> now = [new("a|1", false), new("b|1", false), new("c|1", false), new("late|1", false), new("e|1", true)];

        // Shown after c, the last there was; an icon come since stands after it.
        Assert.True(TrayLayout.Apply(config, now, "e|1", new TrayDrop(TrayArea.Shown, 3), ["a|1", "b|1", "c|1"]));

        Assert.Equal(["a|1", "b|1", "c|1", "e|1", "late|1"], TrayLayout.Split(config, now).Shown.Select(icon => icon.Identity));
    }
}
