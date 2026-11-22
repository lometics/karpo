// The app buttons as Windows 11's taskbar lays them out (Karpo.Dock\AppButtons.cs), measured on a real
// layout without a Gfx (Layout.ForTests), whose labels are fake text of a known width:
//
// - "Combine taskbar buttons and hide labels": an app each, its icon alone, or a window each with
//   its title — whole cells, two to four —, and "when taskbar is full" the first until a window
//   each no longer fits (AppButtons.Lay).
// - What the bar has no room for goes behind "…", from the end of the list, and the app buttons
//   make that room before any widget gives way; labels give up cells before any button goes.
// - A pin whose windows are not combined stands as a button per window, in its own place.
//
// Nothing is drawn and nothing is launched: these are numbers.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Sizing;

public sealed class AppOverflowTests
{
    private const uint Dpi = 96;

    /// <summary>A title that needs more than four cells, which is the most a label is given.</summary>
    private const string Long = "Quarterly report - final version (2) - Word";

    private static Config Bar() => new() { Height = 48 };

    private static PartSpec Spec(string id, BarKind kind) => new(Config.IslandKey(null, id), kind);

    private static Layout Lay(
        int length,
        IReadOnlyList<PartSpec> specs,
        int tiles = 0,
        IReadOnlyList<string?>? labels = null,
        IReadOnlyList<int>? counts = null,
        Edge edge = Edge.Bottom,
        IReadOnlyList<int>? cells = null)
    {
        var config = Bar();
        var area = edge.IsVertical()
            ? new RECT { left = 0, top = 0, right = 1600, bottom = length }
            : new RECT { left = 0, top = 0, right = length, bottom = 1600 };
        return Layout.ForTests(Dpi, area, edge, config, specs, Layout.CellPx(Dpi, config), tiles: tiles, cells: cells, labels: labels, tileCounts: counts);
    }

    /// <summary>The Apps widget alone, holding <paramref name="labels"/> — a window each where they say something.</summary>
    private static Layout Apps(int length, IReadOnlyList<string?> labels) =>
        Lay(length, [Spec("apps", BarKind.Apps)], labels: labels, counts: [labels.Count]);

    private static float Along(Layout layout, RectF rect) => layout.Vertical ? rect.Y : rect.X;

    private static float End(Layout layout, RectF rect) => layout.Vertical ? rect.Bottom : rect.Right;

    private static float Length(Layout layout, RectF rect) => layout.Vertical ? rect.Height : rect.Width;

    /// <summary>The tiles the bar shows, by their place among its tiles.</summary>
    private static List<int> Shown(Layout layout, int tiles) =>
        [.. Enumerable.Range(0, tiles).Where(tile => !layout.ItemRect(Hit.Tile(tile)).IsEmpty)];

    /// <summary>How many cells tile <paramref name="tile"/> is long, by its cell.</summary>
    private static int CellsOf(Layout layout, int tile) =>
        (int)MathF.Round(Length(layout, layout.TileCell(tile)) / layout.Slot);

    /// <summary>What the bar's buttons take along it, from the first one's cell to the last's, "…" included.</summary>
    private static float Run(Layout layout, int tiles)
    {
        var cells = Enumerable.Range(0, tiles).Select(layout.TileCell).Append(layout.OverflowCell()).Where(cell => !cell.IsEmpty).ToList();
        return cells.Count == 0 ? 0f : cells.Max(cell => End(layout, cell)) - cells.Min(cell => Along(layout, cell));
    }

    /// <summary>Where the pointer lands on <paramref name="rect"/>'s middle.</summary>
    private static Hit? Pressed(Layout layout, RectF rect) => layout.HitTest(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));

    // --- "…" ------------------------------------------------------------------------------------

    /// <summary>With room for every button, nothing stands behind "…", and there is no "…" at all.</summary>
    [Fact]
    public void WithRoomForEveryButtonThereIsNoMore()
    {
        var layout = Lay(1920, [Spec("start", BarKind.Start), Spec("apps", BarKind.Apps), Spec("clock", BarKind.NightLight)], tiles: 6);

        Assert.Empty(layout.Overflowed);
        Assert.True(layout.ItemRect(Hit.AppOverflow).IsEmpty);
        Assert.True(layout.OverflowCell().IsEmpty);
        Assert.Equal(6, Shown(layout, 6).Count);
    }

    /// <summary>
    /// What the bar has no room for goes behind "…" from the end of the list — the buttons it shows
    /// are the first ones —, and "…" stands right after the last of them, in the Apps widget, where
    /// a press on it is a press on "…". Nothing else on the bar moves for it.
    /// </summary>
    [Fact]
    public void WhatHasNoRoomGoesBehindMoreFromTheEnd()
    {
        List<PartSpec> specs = [Spec("start", BarKind.Start), Spec("apps", BarKind.Apps), Spec("clock", BarKind.NightLight)];
        var layout = Lay(600, specs, tiles: 30);
        var shown = Shown(layout, 30);

        Assert.InRange(shown.Count, 1, 29);
        Assert.Equal(Enumerable.Range(0, shown.Count), shown);
        Assert.Equal(Enumerable.Range(shown.Count, 30 - shown.Count), layout.Overflowed);
        var more = layout.ItemRect(Hit.AppOverflow);
        Assert.False(more.IsEmpty);
        Assert.Equal(1, layout.PartOf(Hit.AppOverflow));
        // A widget's gap after the last button, as the buttons are from each other (Layout.TileGap, §501).
        Assert.True(Along(layout, more) >= End(layout, layout.ItemRect(Hit.Tile(shown[^1]))));
        Assert.Equal(HitKind.AppOverflow, Pressed(layout, more)?.Kind);
        // The clock keeps its place after them, whole.
        Assert.Empty(layout.NotShown);
        Assert.True(layout.Parts[2].Left >= End(layout, layout.OverflowCell()));
        Assert.True(layout.Parts[2].Right <= layout.Length + 0.5f);
    }

    /// <summary>
    /// The app buttons make room before any widget gives way, as Windows 11's taskbar makes room in
    /// its apps and never in the rest of it: a bar too short for its buttons and its widgets puts
    /// buttons behind "…" and keeps every widget.
    /// </summary>
    [Fact]
    public void AppButtonsMakeRoomBeforeAnyWidgetGivesWay()
    {
        List<PartSpec> specs = [Spec("apps", BarKind.Apps), Spec("weather", BarKind.WidgetsBoard), Spec("battery", BarKind.EnergySaver), Spec("clock", BarKind.NightLight)];
        var roomy = Lay(4000, specs, tiles: 24, cells: [0, 3, 2, 0]);
        var cramped = Lay(900, specs, tiles: 24, cells: [0, 3, 2, 0]);

        Assert.Empty(roomy.Overflowed);
        Assert.NotEmpty(cramped.Overflowed);
        Assert.Empty(cramped.NotShown);
        foreach (var part in new[] { 1, 2, 3 })
        {
            Assert.Equal(roomy.Parts[part].Width, cramped.Parts[part].Width);
        }
    }

    /// <summary>
    /// The pins keep their buttons while the open apps after them go behind "…": it takes from the
    /// end of the list, which is where the open apps stand, and it stands at their end.
    /// </summary>
    [Fact]
    public void PinsKeepTheirButtonsWhileTheOpenAppsGoBehindMore()
    {
        List<PartSpec> specs = [Spec("p0", BarKind.Pin), Spec("p1", BarKind.Pin), Spec("apps", BarKind.Apps), Spec("clock", BarKind.NightLight)];
        var layout = Lay(500, specs, tiles: 2 + 20);

        Assert.NotEmpty(layout.Overflowed);
        Assert.False(layout.ItemRect(Hit.Tile(0)).IsEmpty);
        Assert.False(layout.ItemRect(Hit.Tile(1)).IsEmpty);
        Assert.Equal(2, layout.PartOf(Hit.AppOverflow));
        Assert.All(layout.Overflowed, tile => Assert.True(tile >= 2, $"pin {tile} went behind \"…\" before the open apps did"));
    }

    /// <summary>
    /// With nothing open, the pins themselves go behind "…" from the last, and "…" stands where the
    /// last pin would have: after the last one shown, with no room left over for the ones it hides.
    /// </summary>
    [Fact]
    public void WithNothingOpenTheLastPinsGoBehindMoreInTheirPlace()
    {
        var pins = Enumerable.Range(0, 12).Select(index => Spec($"p{index}", BarKind.Pin)).ToList();
        List<PartSpec> specs = [.. pins, Spec("apps", BarKind.Apps), Spec("clock", BarKind.NightLight)];
        var layout = Lay(400, specs, tiles: 12);
        var shown = Shown(layout, 12);

        Assert.InRange(shown.Count, 1, 11);
        Assert.Equal(Enumerable.Range(shown.Count, 12 - shown.Count), layout.Overflowed);
        Assert.Equal(11, layout.PartOf(Hit.AppOverflow));
        var more = layout.ItemRect(Hit.AppOverflow);
        var last = layout.ItemRect(Hit.Tile(shown[^1]));
        Assert.True(Along(layout, more) >= End(layout, last));
        // A widget's gap after it: the hidden pins take no room.
        Assert.Equal(End(layout, layout.TileCell(shown[^1])) + layout.WidgetGap, Along(layout, layout.OverflowCell()), 0.01);
        foreach (var hidden in layout.Overflowed.Where(tile => tile < 11))
        {
            Assert.Equal(0f, layout.Parts[hidden].Width);
        }
        Assert.Empty(layout.NotShown);
    }

    /// <summary>
    /// A pin standing as more than one button — a window each — stands in the pin's place, its
    /// buttons a widget's gap apart on its card as the Apps widget's are.
    /// </summary>
    [Fact]
    public void APinsWindowsEachStandInThePinsPlace()
    {
        List<PartSpec> specs = [Spec("p0", BarKind.Pin), Spec("p1", BarKind.Pin), Spec("apps", BarKind.Apps)];
        var layout = Lay(1920, specs, labels: [null, "Budget.xlsx", "Plan.xlsx", "Inbox - Mail"], counts: [1, 2, 1]);

        Assert.Equal(2, layout.Parts[1].Count);
        Assert.Equal(1, layout.PartOf(Hit.Tile(1)));
        Assert.Equal(1, layout.PartOf(Hit.Tile(2)));
        Assert.Equal(2, layout.PartOf(Hit.Tile(3)));
        // In the pin's place, one after the other, each a labelled button of its own.
        var block = layout.BlockRect(1);
        foreach (var tile in new[] { 1, 2 })
        {
            var cell = layout.TileCell(tile);
            Assert.True(Along(layout, cell) >= Along(layout, block) - 0.5f && End(layout, cell) <= End(layout, block) + 0.5f);
            Assert.True(layout.Labelled(tile));
            var pressed = Pressed(layout, layout.ItemRect(Hit.Tile(tile)));
            Assert.Equal((HitKind.Tile, tile), (pressed?.Kind, pressed?.Index));
        }
        Assert.Equal(End(layout, layout.TileCell(1)) + layout.WidgetGap, Along(layout, layout.TileCell(2)), 0.01);
        Assert.False(layout.Labelled(0));
    }

    // --- Labels ---------------------------------------------------------------------------------

    /// <summary>
    /// A window's own button is its icon and its title, in whole cells: two for a short title, four
    /// for a long one, which is cut rather than given more. An icon alone keeps its one cell, and the
    /// icon of a labelled one stands where it always does, at the button's start, the label after it.
    /// </summary>
    [Fact]
    public void ALabelledButtonIsWholeCellsFromTwoToFour()
    {
        var layout = Apps(1920, ["", "ab", Long]);

        Assert.False(layout.Labelled(0));
        Assert.Equal(1, CellsOf(layout, 0));
        Assert.True(layout.TileLabel(0).IsEmpty);
        Assert.Equal(2, CellsOf(layout, 1));
        Assert.Equal(4, CellsOf(layout, 2));
        foreach (var tile in new[] { 1, 2 })
        {
            var button = layout.ItemRect(Hit.Tile(tile));
            var label = layout.TileLabel(tile);
            var icon = layout.IconBox(Hit.Tile(tile), layout.Icon);
            Assert.True(layout.Labelled(tile));
            Assert.False(label.IsEmpty);
            Assert.True(label.X >= icon.Right && label.Right <= button.Right, $"tile {tile}'s label {label} is not after its icon {icon} inside {button}");
            Assert.Equal(button.Y, label.Y);
            Assert.True(icon.X - button.X < layout.Well, $"tile {tile}'s icon stands {icon.X - button.X} px into its button");
        }
    }

    /// <summary>
    /// A bar that runs short makes its labels give up cells — the widest first, down to two — before
    /// any button goes behind "…"; the buttons shown only ever get fewer as the bar gets shorter,
    /// and what they take always fits.
    /// </summary>
    [Fact]
    public void LabelsGiveUpCellsBeforeAnyButtonGoesBehindMore()
    {
        string?[] labels = [Long, Long, "Notes", Long, "Mail", Long];
        var before = labels.Length;
        var cellsBefore = Enumerable.Repeat(int.MaxValue, labels.Length).ToArray();
        var squeezed = false;
        var overflowed = false;
        for (var length = 1400; length >= 120; length -= 9)
        {
            var layout = Apps(length, labels);
            var shown = Shown(layout, labels.Length);
            var cells = Enumerable.Range(0, labels.Length).Select(tile => shown.Contains(tile) ? CellsOf(layout, tile) : 0).ToArray();

            Assert.True(Run(layout, labels.Length) <= layout.Length + 0.5f, $"{length} px: the buttons take {Run(layout, labels.Length)}");
            Assert.InRange(shown.Count, 0, before);
            if (layout.Overflowed.Count > 0)
            {
                overflowed = true;
                Assert.All(shown, tile => Assert.Equal(2, cells[tile]));
            }
            else
            {
                Assert.All(shown, tile => Assert.InRange(cells[tile], 2, 4));
            }
            foreach (var tile in shown)
            {
                Assert.True(cells[tile] <= cellsBefore[tile], $"{length} px: tile {tile} got longer as the bar got shorter");
                cellsBefore[tile] = cells[tile];
            }
            squeezed |= layout.Overflowed.Count == 0 && cells.Any(count => count is > 2 and < 4);
            before = shown.Count;
        }
        Assert.True(squeezed, "no bar had its labels part way between four cells and two");
        Assert.True(overflowed, "no bar was short enough to put a button behind \"…\"");
    }

    /// <summary>Down the side of the screen there is no room along the bar for a word beside an icon: no labels.</summary>
    [Fact]
    public void AVerticalBarLabelsNothing()
    {
        var layout = Lay(1080, [Spec("apps", BarKind.Apps)], labels: ["Budget.xlsx", Long], counts: [2], edge: Edge.Left);

        foreach (var tile in new[] { 0, 1 })
        {
            Assert.False(layout.Labelled(tile));
            Assert.True(layout.TileLabel(tile).IsEmpty);
            Assert.Equal(1, CellsOf(layout, tile));
        }
    }

    /// <summary>
    /// A button dragged among labelled ones lands before the first one whose middle is past the
    /// pointer, by the buttons' own lengths rather than by counting cells.
    /// </summary>
    [Fact]
    public void ADropAmongLabelledButtonsGoesByTheirOwnLengths()
    {
        var layout = Apps(1920, ["ab", Long, "", Long]);

        for (var tile = 0; tile < 4; tile++)
        {
            var cell = layout.TileCell(tile);
            var middle = Along(layout, cell) + (Length(layout, cell) / 2f);
            Assert.Equal(tile, layout.Insertion(middle - 1f));
            Assert.Equal(tile + 1, layout.Insertion(middle + 1f));
        }
    }

    // --- Combining ------------------------------------------------------------------------------

    /// <summary>Six windows of three apps, a window each with its title, and the same three apps an app each.</summary>
    private static readonly string?[] EachWindow = [Long, "Notes", Long, "Inbox - Mail", Long, "Calendar - Mail"];

    private static readonly string?[] EachApp = [null, null, null];

    /// <summary>
    /// How the three settings lay a bar out: "always" an app each whatever the room, "never" a
    /// window each whatever the room — what does not fit going behind "…" —, and "when taskbar is
    /// full" a window each while they fit, labels squeezed first, and an app each once they don't.
    /// </summary>
    [Fact]
    public void EachSettingLaysTheBarOutItsOwnWay()
    {
        var probe = Apps(8000, EachWindow);
        var slot = probe.Slot;
        var gap = probe.WidgetGap;
        // A window each at their shortest, and an app each.
        var eachShortest = (EachWindow.Length * 2 * slot) + ((EachWindow.Length - 1) * gap);
        var eachApp = (EachApp.Length * slot) + ((EachApp.Length - 1) * gap);
        var wide = 8000;
        var squeezed = (int)MathF.Ceiling(eachShortest + (slot / 2f));
        var full = (int)MathF.Floor((eachShortest + eachApp) / 2f);

        foreach (var (mode, length, expectFull, expectOverflow) in new[]
        {
            (ButtonCombine.Always, wide, true, false),
            (ButtonCombine.Always, full, true, false),
            (ButtonCombine.Never, wide, false, false),
            (ButtonCombine.Never, full, false, true),
            (ButtonCombine.WhenFull, wide, false, false),
            (ButtonCombine.WhenFull, squeezed, false, false),
            (ButtonCombine.WhenFull, full, true, false),
        })
        {
            var each = mode == ButtonCombine.Always ? null : EachWindow;
            var (shown, layout, isFull) = AppButtons.Lay(mode, each, EachApp, labels => Apps(length, labels));
            var what = $"{mode} on {length} px";

            // "Always" has no list a window each, so it is never "full": it simply is an app each.
            Assert.Equal(expectFull && mode == ButtonCombine.WhenFull, isFull);
            Assert.Same(expectFull ? EachApp : EachWindow, shown);
            Assert.Equal(expectOverflow, layout.Overflowed.Count > 0);
            Assert.Equal(!expectFull, layout.Labelled(0));
            Assert.True(Run(layout, shown.Length) <= layout.Length + 0.5f, what);
        }
    }

    /// <summary>
    /// "When taskbar is full" comes back to a window each as soon as they fit again: the test is the
    /// bar's own, asked afresh of every layout, and not something the bar remembers.
    /// </summary>
    [Fact]
    public void WhenFullGoesBackToAWindowEachOnceTheyFitAgain()
    {
        var eachLongest = Run(Apps(8000, EachWindow), EachWindow.Length);
        var lengths = new[] { 8000, 900, 400, 250, 400, 900, 8000 };
        var seen = new List<bool>();
        foreach (var length in lengths)
        {
            var (_, layout, isFull) = AppButtons.Lay(ButtonCombine.WhenFull, EachWindow, EachApp, labels => Apps(length, labels));
            seen.Add(isFull);
            Assert.Equal(isFull, !layout.Labelled(0));
            if (length >= eachLongest)
            {
                Assert.False(isFull, $"{length} px holds every window's button whole, and the bar went to an app each");
            }
        }
        Assert.Contains(true, seen);
        Assert.Equal(seen, seen.AsEnumerable().Reverse());
    }

    /// <summary>What the settings mean for a bar's buttons, as the dock asks it once it has laid a window each out.</summary>
    [Theory]
    [InlineData(ButtonCombine.Always, false, true)]
    [InlineData(ButtonCombine.Always, true, true)]
    [InlineData(ButtonCombine.WhenFull, false, false)]
    [InlineData(ButtonCombine.WhenFull, true, true)]
    [InlineData(ButtonCombine.Never, false, false)]
    [InlineData(ButtonCombine.Never, true, false)]
    public void WhetherABarCombines(ButtonCombine mode, bool overflowed, bool combines) =>
        Assert.Equal(combines, AppButtons.Combines(mode, overflowed));

    /// <summary>A window's own button says its title, trimmed, or its app's name where it has none.</summary>
    [Theory]
    [InlineData("Inbox - Mail", "Mail", "Inbox - Mail")]
    [InlineData("  Notes  ", "Notepad", "Notes")]
    [InlineData("", "Notepad", "Notepad")]
    [InlineData("   ", "Notepad", "Notepad")]
    [InlineData(null, "Notepad", "Notepad")]
    public void AWindowsButtonSaysItsTitle(string? title, string app, string said) =>
        Assert.Equal(said, AppButtons.Label(title, app));
}
