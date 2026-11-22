// "Combine taskbar buttons and hide labels: Never" on a bar with room (the person, 2026-10-05: "when
// i have combine taskbar button... set to never it shows me the three ... even though there is
// plenty of space. fix that.").
//
// Why it showed "…": a pinned app stands as a button per window where its windows are not combined,
// each a labelled button of two to four cells, and the pin's own range — one cell, its icon's — held
// them all inside that one cell; none fit, so they went behind "…" whatever the bar had to spare.
// The open apps were held the same way to their most, 32 cells, which a wide bar of labelled
// buttons passes long before it is full. Now neither holds them (Layout.ButtonRoom): the bar's free
// room is theirs, as it is on Windows 11's taskbar.
//
// And how they give way, as Windows 11's do: each labelled button is as long as its title asks, up
// to four cells (176 DIP); where the bar runs short they narrow alike — the longest cut to one
// length (Layout.EvenCut) — down to two cells (88 DIP), and only once every one of them is that
// short does the last go behind "…". "When taskbar is full" combines exactly then; "Always" is
// unchanged.
//
// Measured on a real layout without a Gfx (Layout.ForTests), whose labels are fake text of a known
// width, with the widgets a new bar has around the buttons, in their sections and at their own
// ranges (WidgetBounds.DefaultFor), as the dock hands them over.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Sizing;

public sealed class NeverCombineTests
{
    /// <summary>Twenty windows' titles, short and long, as a busy taskbar has them.</summary>
    private static readonly string[] Titles =
    [
        "Inbox - Mail",
        "Quarterly report - final version (2) - Word",
        "Notes",
        "Karpo - Visual Studio Code",
        "Calculator",
        "Downloads - File Explorer",
        "Budget 2026.xlsx - Excel",
        "Settings",
        "Weekly sync - Microsoft Teams",
        "Terminal",
        "Photos",
        "README.md - Notepad",
        "Spotify Premium",
        "Calendar - Outlook",
        "Task Manager",
        "Pull request #412 - Microsoft Edge",
        "Paint",
        "Snipping Tool",
        "Letter to the landlord about the heating - Word",
        "Clock",
    ];

    private static PartSpec Real(string id, BarKind kind, BarSection section) =>
        new(Config.IslandKey(null, id), kind, WidgetBounds.DefaultFor(kind)) { Section = section };

    /// <summary>
    /// A bar as a new one is made, at the ranges its widgets start with: the weather at the start;
    /// Start, search, Task view, two pins and the open apps in the middle; the battery and the clock
    /// at the end.
    /// </summary>
    private static readonly PartSpec[] Specs =
    [
        Real("weather", BarKind.WidgetsBoard, BarSection.Start),
        Real("start", BarKind.Start, BarSection.Middle),
        Real("search", BarKind.Search, BarSection.Middle),
        Real("taskview", BarKind.TaskView, BarSection.Middle),
        Real("pin-mail", BarKind.Pin, BarSection.Middle),
        Real("pin-paint", BarKind.Pin, BarSection.Middle),
        Real("apps", BarKind.Apps, BarSection.Middle),
        Real("battery", BarKind.EnergySaver, BarSection.End),
        Real("clock", BarKind.NightLight, BarSection.End),
    ];

    /// <summary>The weather three cells, the battery one.</summary>
    private static readonly int[] Cells = [3, 0, 0, 0, 0, 0, 0, 1, 0];

    /// <summary>
    /// <paramref name="windows"/> windows a window each: the first the mail pin's, a labelled button
    /// in the pin's place; the paint pin not running, its icon alone; the rest the open apps'.
    /// </summary>
    private static (string?[] Labels, int[] Counts) EachWindow(int windows) =>
        ([Titles[0], null, .. Titles.Skip(1).Take(windows - 1)], [0, 0, 0, 0, 1, 1, windows - 1, 0, 0]);

    /// <summary>The same windows an app each: the two pins' icons, and an icon for every two windows of the rest.</summary>
    private static (string?[] Labels, int[] Counts) EachApp(int windows)
    {
        var apps = Math.Max(1, windows / 2);
        return ([.. Enumerable.Repeat<string?>(null, 2 + apps)], [0, 0, 0, 0, 1, 1, apps, 0, 0]);
    }

    private static Layout Lay(int length, (string?[] Labels, int[] Counts) tiles, uint dpi = 96, BarStyle style = BarStyle.Islands)
    {
        var config = new Config { Height = 48, Style = style };
        var area = new RECT { left = 0, top = 0, right = length, bottom = 1600 };
        return Layout.ForTests(dpi, area, Edge.Bottom, config, Specs, Layout.CellPx(dpi, config), cells: Cells, labels: tiles.Labels, tileCounts: tiles.Counts);
    }

    private static float Length(RectF rect) => rect.Width;

    /// <summary>
    /// What the bar's parts take along it, section by section: from a section's first part to its
    /// last, and the gap between two sections that both hold something.
    /// </summary>
    private static float Taken(Layout layout)
    {
        var total = 0f;
        var sections = 0;
        foreach (var section in new[] { BarSection.Start, BarSection.Middle, BarSection.End })
        {
            var parts = Enumerable.Range(0, Specs.Length).Where(index => Specs[index].Section == section && layout.Parts[index].Width > 0f).Select(index => layout.Parts[index]).ToList();
            if (parts.Count == 0)
            {
                continue;
            }
            total += (sections++ > 0 ? layout.SectionGap : 0f) + parts.Max(part => part.Right) - parts.Min(part => part.Left);
        }
        return total;
    }

    /// <summary>
    /// The shortest bar every one of <paramref name="windows"/> windows' buttons fits on at its least,
    /// worked out from a bar with room to spare: what it takes, less what each labelled button has
    /// above two cells.
    /// </summary>
    private static float Threshold(int windows, uint dpi, BarStyle style)
    {
        var tiles = EachWindow(windows);
        var wide = Lay(20000, tiles, dpi, style);
        Assert.Empty(wide.Overflowed);
        var least = 2f * wide.Slot;
        var above = Enumerable.Range(0, tiles.Labels.Length).Where(wide.Labelled).Sum(tile => Length(wide.TileCell(tile)) - least);
        return Taken(wide) - above + (2f * wide.EndRoom);
    }

    // --- The regression -------------------------------------------------------------------------

    /// <summary>
    /// The person's bar: a pinned app with a window open and a few more open apps, a window each, on
    /// a 1920 px bar with room to spare. Every one stands on the bar, saying its whole title as far
    /// as four cells allow; there is no "…". The pin's window held to the pin's one cell was what
    /// went behind it.
    /// </summary>
    [Theory]
    [InlineData(BarStyle.Islands)]
    [InlineData(BarStyle.Floating)]
    public void APinsWindowIsNotHeldToThePinsOneCell(BarStyle style)
    {
        var tiles = EachWindow(4);
        var layout = Lay(1920, tiles, style: style);

        Assert.Empty(layout.Overflowed);
        Assert.True(layout.ItemRect(Hit.AppOverflow).IsEmpty);
        Assert.Empty(layout.NotShown);
        Assert.True(layout.Labelled(0));
        Assert.False(layout.TileCell(0).IsEmpty);
        Assert.Equal(4f * layout.Slot, Length(layout.TileCell(0)), 0.5f);
        // The pin's window stands in the pin's place, before the pin that isn't running.
        Assert.True(layout.TileCell(0).Right <= layout.TileCell(1).X + 0.5f);
        for (var tile = 0; tile < tiles.Labels.Length; tile++)
        {
            Assert.Equal(tiles.Labels[tile] is not null, layout.Labelled(tile));
        }
    }

    /// <summary>
    /// The open apps' most as a new widget has it — the most there is, 32 cells — holds nothing: on
    /// a 3840 px bar twenty windows' buttons, more than 32 cells together, all stand at their own
    /// length. The most was set so as not to cut them off, and it did.
    /// </summary>
    [Fact]
    public void TheOpenAppsTakeTheBarsRoomPastTheirMost()
    {
        var tiles = EachWindow(20);
        var layout = Lay(3840, tiles);
        var apps = Array.IndexOf(Specs, Specs.First(spec => spec.Kind == BarKind.Apps));

        Assert.Equal(WidgetItem.MostCells, Specs[apps].Room.Most);
        Assert.True(layout.Parts[apps].Width > WidgetItem.MostCells * layout.CellSide, $"the open apps take {layout.Parts[apps].Width} px");
        Assert.Empty(layout.Overflowed);
        var wide = Lay(20000, tiles);
        for (var tile = 0; tile < tiles.Labels.Length; tile++)
        {
            Assert.Equal(Length(wide.TileCell(tile)), Length(layout.TileCell(tile)), 0.5f);
        }
    }

    /// <summary>A most the person narrowed is still theirs: the open apps' buttons stay inside it, and what does not fit goes behind "…".</summary>
    [Fact]
    public void AMostThePersonNarrowedStillHoldsThem()
    {
        var narrowed = Specs.Select(spec => spec.Kind == BarKind.Apps ? spec with { Room = new WidgetBounds(1, 8) } : spec).ToList();
        var config = new Config { Height = 48, Style = BarStyle.Islands };
        var tiles = EachWindow(12);
        var layout = Layout.ForTests(96, new RECT { left = 0, top = 0, right = 3840, bottom = 1600 }, Edge.Bottom, config, narrowed, Layout.CellPx(96, config), cells: Cells, labels: tiles.Labels, tileCounts: tiles.Counts);
        var apps = narrowed.FindIndex(spec => spec.Kind == BarKind.Apps);

        Assert.NotEmpty(layout.Overflowed);
        Assert.True(layout.Parts[apps].Width <= (8 * layout.CellSide) + 0.5f);
    }

    /// <summary>What the room a part of app buttons is held to keeps of its range (Layout.ButtonRoom).</summary>
    [Fact]
    public void WhatHoldsAppButtons()
    {
        var pin = WidgetBounds.DefaultFor(BarKind.Pin);
        var apps = WidgetBounds.DefaultFor(BarKind.Apps);

        // A pin's one cell is its icon's, and its fewest is still kept whole.
        Assert.Equal(1, Layout.ButtonRoom(BarKind.Pin, pin).Least);
        Assert.Equal(int.MaxValue, Layout.ButtonRoom(BarKind.Pin, pin).Most);
        Assert.Equal(3, Layout.ButtonRoom(BarKind.Pin, new WidgetBounds(3, 3)).Least);
        // The open apps' most as it starts is the most there is, which holds nothing; one narrowed holds.
        Assert.True(Layout.ButtonRoom(BarKind.Apps, apps).Open);
        Assert.Equal(400, Layout.ButtonRoom(BarKind.Apps, apps).Clamp(400));
        Assert.Equal(new WidgetBounds(2, 8), Layout.ButtonRoom(BarKind.Apps, new WidgetBounds(2, 8)));
        // Anything else keeps its range.
        Assert.Equal(new WidgetBounds(1, 4), Layout.ButtonRoom(BarKind.NightLight, new WidgetBounds(1, 4)));
    }

    // --- Narrowing alike, then "…" --------------------------------------------------------------

    public static TheoryData<int, uint, BarStyle> Bars()
    {
        var bars = new TheoryData<int, uint, BarStyle>();
        foreach (var windows in new[] { 3, 8, 20 })
        {
            foreach (var dpi in new uint[] { 96, 144 })
            {
                foreach (var style in new[] { BarStyle.Islands })
                {
                    bars.Add(windows, dpi, style);
                }
            }
        }
        return bars;
    }

    /// <summary>
    /// Never, with <paramref name="windows"/> windows, on every length of bar from too short to
    /// roomy: no "…" while their buttons fit at their least, two cells each, and "…" as soon as they
    /// don't; the labelled buttons narrowed alike — the ones cut all one length, none longer than
    /// it, none below two cells —, all at two cells once "…" is there; nothing longer as the bar
    /// gets shorter; and every widget kept while a button can still give way.
    /// </summary>
    [Theory]
    [MemberData(nameof(Bars))]
    public void NoMoreWhileTheyFitAtTheirLeastAndTheyNarrowAlike(int windows, uint dpi, BarStyle style)
    {
        var tiles = EachWindow(windows);
        var count = tiles.Labels.Length;
        var threshold = Threshold(windows, dpi, style);
        var natural = Lay(20000, tiles, dpi, style);
        var step = 3;
        var before = Enumerable.Repeat(float.MaxValue, count).ToArray();
        var hiddenBefore = int.MaxValue;
        var (narrowed, overflowed, whole) = (false, false, false);
        for (var length = (int)(threshold * 1.6f) + 400; length >= (int)(threshold * 0.6f); length -= step)
        {
            var layout = Lay(length, tiles, dpi, style);
            var least = 2f * layout.Slot;
            var what = $"{windows} windows, {dpi} dpi, {style}, {length} px (least fit at {threshold})";
            var shown = Enumerable.Range(0, count).Where(tile => !layout.TileCell(tile).IsEmpty).ToList();
            var labelled = shown.Where(layout.Labelled).ToList();
            var lengths = labelled.ToDictionary(tile => tile, tile => Length(layout.TileCell(tile)));

            if (length >= threshold + 1f)
            {
                Assert.True(layout.Overflowed.Count == 0, $"{what}: \"…\" with every button fitting at its least");
                Assert.Empty(layout.NotShown);
            }
            if (length <= threshold - 1f)
            {
                Assert.True(layout.Overflowed.Count > 0, $"{what}: no \"…\" though the buttons do not fit at their least");
            }
            // Never below the least, never above what the title asks.
            foreach (var (tile, room) in lengths)
            {
                Assert.True(room >= least - 0.5f, $"{what}: tile {tile} is {room} px, below its least {least}");
                Assert.True(room <= Length(natural.TileCell(tile)) + 0.5f, $"{what}: tile {tile} is longer than its title asks");
            }
            var cut = lengths.Where(pair => pair.Value < Length(natural.TileCell(pair.Key)) - 0.5f).Select(pair => pair.Value).ToList();
            if (cut.Count > 0)
            {
                // The ones cut are one length, and none left whole is longer than it: the longest went first.
                Assert.True(cut.Max() - cut.Min() <= 0.5f, $"{what}: the buttons cut are {cut.Min()} to {cut.Max()} px");
                Assert.All(lengths.Values, room => Assert.True(room <= cut.Max() + 0.5f, $"{what}: a button of {room} px left whole beside ones cut to {cut.Max()}"));
            }
            if (layout.Overflowed.Count > 0)
            {
                Assert.All(lengths.Values, room => Assert.Equal(least, room, 0.5f));
                overflowed = true;
            }
            else
            {
                narrowed |= cut.Count > 0 && cut.Max() > least + 1f;
                whole |= cut.Count == 0;
            }
            // Shorter never brings a button back or makes one longer.
            Assert.True(hiddenBefore == int.MaxValue || layout.Overflowed.Count >= hiddenBefore, $"{what}: a button came back from behind \"…\" as the bar got shorter");
            hiddenBefore = layout.Overflowed.Count;
            foreach (var tile in labelled)
            {
                Assert.True(lengths[tile] <= before[tile] + 0.5f, $"{what}: tile {tile} got longer as the bar got shorter");
                before[tile] = lengths[tile];
            }
            // What the buttons take fits the bar, "…" included.
            var cells = shown.Select(layout.TileCell).Append(layout.OverflowCell()).Where(cell => !cell.IsEmpty).ToList();
            Assert.True(cells.Max(cell => cell.Right) <= layout.Length + 0.5f, $"{what}: the buttons run to {cells.Max(cell => cell.Right)}");
        }
        Assert.True(whole, "no bar had room for every button whole");
        Assert.True(narrowed, "no bar had its buttons narrowed part way");
        Assert.True(overflowed, "no bar was short enough for \"…\"");
    }

    // --- The other two settings -----------------------------------------------------------------

    /// <summary>
    /// "When taskbar is full" is a window each, narrowed alike as Never is, for as long as they fit at
    /// their least, and an app each — icons — from the first length at which they would not.
    /// </summary>
    [Theory]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(20)]
    public void WhenFullCombinesOnlyOnceTheyNoLongerFitAtTheirLeast(int windows)
    {
        var threshold = Threshold(windows, 96, BarStyle.Islands);
        var each = EachWindow(windows);
        var apps = EachApp(windows);
        foreach (var length in new[] { (int)MathF.Ceiling(threshold) + 1, (int)MathF.Ceiling(threshold) + 60, 4000, (int)MathF.Floor(threshold) - 1, (int)(threshold * 0.8f) })
        {
            var (shown, layout, full) = AppButtons.Lay(ButtonCombine.WhenFull, each.Labels, apps.Labels, labels => Lay(length, ReferenceEquals(labels, each.Labels) ? each : apps));
            var fits = length >= threshold;

            Assert.Equal(!fits, full);
            Assert.Same(fits ? each.Labels : apps.Labels, shown);
            Assert.Equal(fits, layout.Labelled(0));
            if (fits)
            {
                Assert.Empty(layout.Overflowed);
            }
        }
    }

    /// <summary>"Always" is an app each, icons alone, whatever the room.</summary>
    [Theory]
    [InlineData(400)]
    [InlineData(1920)]
    [InlineData(8000)]
    public void AlwaysIsAnAppEachWhateverTheRoom(int length)
    {
        var apps = EachApp(20);
        var (shown, layout, full) = AppButtons.Lay(ButtonCombine.Always, null, apps.Labels, labels => Lay(length, apps));

        Assert.False(full);
        Assert.Same(apps.Labels, shown);
        Assert.All(Enumerable.Range(0, apps.Labels.Length), tile => Assert.False(layout.Labelled(tile)));
    }

    // --- The cut itself -------------------------------------------------------------------------

    /// <summary>
    /// The one length the longest buttons are cut to: the longest first, each down to the next, so
    /// the ones cut come out alike; never below the least; the longest where nothing is short.
    /// </summary>
    [Theory]
    [InlineData(new[] { 176f, 176f, 132f }, 44f, 88f, 154f)]
    [InlineData(new[] { 176f, 132f }, 44f, 88f, 132f)]
    [InlineData(new[] { 176f, 132f }, 100f, 88f, 104f)]
    [InlineData(new[] { 176f, 132f, 88f }, 100f, 88f, 104f)]
    [InlineData(new[] { 176f, 176f, 176f }, 1000f, 88f, 88f)]
    [InlineData(new[] { 176f, 132f }, 0f, 88f, 176f)]
    [InlineData(new float[0], 50f, 88f, 88f)]
    public void TheCutIsOneLengthForTheLongest(float[] lengths, float over, float least, float cut) =>
        Assert.Equal(cut, Layout.EvenCut(lengths, over, least), 0.01f);

    /// <summary>What the cut gives up is exactly what was short, wherever the least allows it.</summary>
    [Fact]
    public void TheCutGivesUpWhatWasShort()
    {
        var random = new Random(68);
        for (var round = 0; round < 500; round++)
        {
            var lengths = Enumerable.Range(0, random.Next(1, 25)).Select(_ => 88f + (random.Next(0, 89))).ToList();
            var over = random.Next(1, 800);
            var cut = Layout.EvenCut(lengths, over, 88f);
            var given = lengths.Sum(length => MathF.Max(0f, length - cut));
            var most = lengths.Sum(length => length - 88f);

            Assert.InRange(cut, 88f, lengths.Max());
            Assert.Equal(MathF.Min(over, most), given, 0.01f);
        }
    }
}
