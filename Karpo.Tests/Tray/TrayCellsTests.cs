// The shape of the notification area on the bar: every icon in a cell of its own, the icon and a
// face's padding along the bar and the bar's thickness across it (the person, 2026-10-22: "they dont
// have to have the 1by1 ratio anymore", §290), centred across the bar, the cells side by side with no gap
// between them as Windows 11's tray icons stand (the person, 2026-10-08: "tray icons sit connected,
// with no gap between them"), with the overflow chevron as one more such cell; and its picture the size of every other icon on the bar
// (the person, 2026-10-08: "tray icons show in the same size as all the other icons on the bar
// again"). Measured on a real layout without a Gfx (Layout.ForTests), with a widget either side of
// the tray, which keeps the widgets' gap to them.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.TrayIcons;

public sealed class TrayCellsTests
{
    private static readonly uint[] Dpis = [96, 120, 144, 192];

    private static readonly int[] Heights = [28, 32, 40, 48, 56, 72, 96, 115, 160];

    private static readonly Edge[] Edges = [Edge.Bottom, Edge.Top, Edge.Left, Edge.Right];

    private static readonly string BatteryKey = Config.IslandKey(null, "battery");

    private static readonly string TrayKey = Config.IslandKey(null, "tray");

    private static RECT Area(int width, int height) => new() { left = 0, top = 0, right = width, bottom = height };

    /// <summary>A bar with a tray, and a widget either side of it to compare the rhythm against.</summary>
    private static Layout Bar(uint dpi, int height, Edge edge, int icons, bool overflow = false, BarStyle style = BarStyle.Islands)
    {
        // The islands look by default: the rhythm is the widgets' gap, which a connected bar has none of.
        var config = new Config { Height = height, Style = style };
        List<PartSpec> specs =
        [
            new(BatteryKey, BarKind.EnergySaver),
            new(TrayKey, BarKind.Tray),
            new(Config.IslandKey(null, "wifi"), BarKind.TouchKeyboard),
        ];
        var area = edge.IsVertical() ? Area(1200, 2600) : Area(2600, 1200);
        return Layout.ForTests(
            dpi,
            area,
            edge,
            config,
            specs,
            Layout.CellPx(dpi, config),
            tray: icons,
            trayOverflow: overflow);
    }

    private static float Along(Layout layout, RectF rect) => layout.Vertical ? rect.Y : rect.X;

    private static float Length(Layout layout, RectF rect) => layout.Vertical ? rect.Height : rect.Width;

    private static float Across(Layout layout, RectF rect) => layout.Vertical ? rect.Width : rect.Height;

    /// <summary>The room between the tray and the widget before it, read off this very bar.</summary>
    private static float WidgetGap(Layout layout)
    {
        var battery = layout.BlockRect(layout.PartIndex(BatteryKey));
        var tray = layout.BlockRect(layout.PartIndex(TrayKey));
        return Along(layout, tray) - (Along(layout, battery) + Length(layout, battery));
    }

    /// <summary>The middle of <paramref name="rect"/>, for the hit test.</summary>
    private static (float X, float Y) Middle(RectF rect) => (rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);

    // --- The cell -------------------------------------------------------------------

    /// <summary>
    /// A tray cell is the icon and the room between two faces side by side along the bar (a face's
    /// padding, <see cref="FolderIsland.Between"/>, half either side), and the thickness every small
    /// widget and app button has across it (<see cref="Layout.CellSide"/>): narrower than it is thick,
    /// the whole thickness the hover's and the press's. The chevron's is the same.
    /// </summary>
    [Fact]
    public void EveryTrayCellIsTheIconAndItsPadding()
    {
        foreach (var dpi in Dpis)
        {
            foreach (var height in Heights)
            {
                foreach (var edge in Edges)
                {
                    var layout = Bar(dpi, height, edge, icons: 4, overflow: true);
                    var what = $"{dpi} dpi, {height} DIP, {edge}: step {layout.TrayStep}, cell {layout.TrayCell}, icon {layout.TrayIconSize}";
                    var fit = new Fit(layout.StyleFor(-1, default));
                    Assert.Equal(layout.CellSide, layout.TrayCell);
                    Assert.Equal(layout.Slot, layout.TrayCell);
                    Assert.Equal(MathF.Min(layout.TrayCell, layout.TrayIconSize + fit.Px(8f)), layout.TrayStep);
                    // Never longer than it is thick: on a 28-DIP bar keeping its cards' room (§533) the step is the cell's whole side.
                    Assert.True(layout.TrayStep <= layout.TrayCell, what);
                    Assert.True(layout.TrayStep > layout.TrayIconSize, what);
                    for (var index = 0; index < 4; index++)
                    {
                        var rect = layout.ItemRect(Hit.Tray(index));
                        Assert.False(rect.IsEmpty, what);
                        Assert.Equal(layout.TrayStep, Length(layout, rect));
                        Assert.Equal(layout.TrayCell, Across(layout, rect));
                    }
                    var chevron = layout.ItemRect(Hit.TrayOverflow);
                    Assert.Equal(layout.TrayStep, Length(layout, chevron));
                    Assert.Equal(layout.TrayCell, Across(layout, chevron));
                }
            }
        }
    }

    /// <summary>
    /// On the taskbar's 48-DIP bar a tray cell is its 16-DIP icon and the 5 DIP between two faces (§506), 21 DIP
    /// along the bar (a pixel either way for the roundings), where it was the bar's square and then Windows 11's 24.
    /// </summary>
    [Fact]
    public void OnTheTaskbarsBarACellIsWindowsOwnSlot()
    {
        foreach (var dpi in Dpis)
        {
            var layout = Bar(dpi, 48, Edge.Bottom, icons: 2);
            Assert.InRange(layout.TrayStep, MathF.Round(24 * dpi / 96f) - 1, MathF.Round(24 * dpi / 96f) + 1);
        }
    }

    /// <summary>
    /// The person: *"tray icons sit connected, with no gap between them"*, as Windows 11's do. Each
    /// cell starts where the one before it ends, the chevron's too, in every look, at every height,
    /// DPI and edge; the widgets either side still keep their gap to the tray.
    /// </summary>
    [Fact]
    public void TheCellsTouch()
    {
        foreach (var style in Enum.GetValues<BarStyle>())
        {
            foreach (var dpi in Dpis)
            {
                foreach (var height in Heights)
                {
                    foreach (var edge in Edges)
                    {
                        var layout = Bar(dpi, height, edge, icons: 5, overflow: true, style: style);
                        var what = $"{style}, {dpi} dpi, {height} DIP, {edge}";
                        var places = new List<RectF> { layout.ItemRect(Hit.TrayOverflow) };
                        for (var index = 0; index < 5; index++)
                        {
                            places.Add(layout.ItemRect(Hit.Tray(index)));
                        }
                        for (var at = 1; at < places.Count; at++)
                        {
                            Assert.False(places[at].IsEmpty, what);
                            var gap = Along(layout, places[at]) - (Along(layout, places[at - 1]) + Length(layout, places[at - 1]));
                            Assert.True(gap == 0f, $"{what}: {gap} px between cell {at - 1} and cell {at}");
                        }
                        // Its neighbours are widgets too: a widget's gap (§501, §515).
                        if (style == BarStyle.Islands)
                        {
                            Assert.True(WidgetGap(layout) == layout.WidgetGap, what);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TheBlockIsItsCellsSideBySide()
    {
        foreach (var height in Heights)
        {
            foreach (var overflow in new[] { false, true })
            {
                var layout = Bar(96, height, Edge.Bottom, icons: 3, overflow: overflow);
                var cells = 3 + (overflow ? 1 : 0);

                Assert.Equal(cells * layout.TrayStep, Length(layout, layout.BlockRect(layout.PartIndex(TrayKey))), 0.01);
            }
        }
    }

    /// <summary>
    /// A tray of a set size holds as many whole tray cells as its room, side by side: three of the
    /// bar's cells hold as many tray cells as fit in them, the chevron's among them where it stands.
    /// </summary>
    [Fact]
    public void ATrayOfASetSizeHoldsWholeCellsSideBySide()
    {
        var config = new Config { Height = 48, Style = BarStyle.Islands };
        foreach (var dpi in Dpis)
        {
            var cells = (int)MathF.Floor(3 * Layout.CellPx(dpi, config) / Bar(dpi, 48, Edge.Bottom, icons: 1).TrayStep);
            var layout = Layout.ForTests(
                dpi,
                Area(2600, 1200),
                Edge.Bottom,
                config,
                [new(BatteryKey, BarKind.EnergySaver), new(TrayKey, BarKind.Tray, 3), new(Config.IslandKey(null, "wifi"), BarKind.TouchKeyboard)],
                Layout.CellPx(dpi, config),
                tray: cells + 2,
                trayOverflow: false);

            Assert.True(cells > 3, $"{dpi} dpi: {cells} tray cells in three of the bar's");
            Assert.True(layout.TrayOverflow);
            Assert.Equal(cells - 1, layout.PartAt(layout.PartIndex(TrayKey)).Count);
            Assert.False(Layout.TraySpills(new PartSpec(TrayKey, BarKind.Tray, 3), cells, layout.CellSide, layout.TrayStep));
            Assert.True(Layout.TraySpills(new PartSpec(TrayKey, BarKind.Tray, 3), cells + 1, layout.CellSide, layout.TrayStep));
        }
    }

    [Fact]
    public void ACellIsTheSameSizeHoweverManyIconsThereAre()
    {
        // The number of icons is content — the tray grows when an app starts, as a drive appearing
        // grows the drives widget. What must not move is one icon's own cell.
        var two = Bar(96, 48, Edge.Bottom, icons: 2);
        var nine = Bar(96, 48, Edge.Bottom, icons: 9);

        Assert.Equal(two.TrayCell, nine.TrayCell);
        Assert.Equal(two.TrayStep, nine.TrayStep);
        Assert.Equal(two.TrayIconSize, nine.TrayIconSize);
        Assert.Equal(
            Length(two, two.ItemRect(Hit.Tray(0))),
            Length(nine, nine.ItemRect(Hit.Tray(0))));
    }

    // --- Closer together (the person, 2026-10-22) -------------------------------------

    /// <summary>
    /// The person: *"make the tray icons a little closer together, they dont have to have the 1by1
    /// ratio anymore"* (they had asked for squares on 2026-10-05). Every icon's cell and the
    /// chevron's are narrower along the bar than they are thick across it, in every look, at every
    /// height from 32 to 64 DIP, at 100, 125, 150 and 200 %, on a horizontal bar and a vertical one,
    /// and centred across the bar.
    /// </summary>
    [Fact]
    public void EveryTrayCellIsNarrowerThanItIsThick()
    {
        foreach (var style in Enum.GetValues<BarStyle>())
        {
            foreach (var dpi in Dpis)
            {
                // §687: a dock's sizes up to the one whose panel is 64 DIP thick too.
                for (var height = 32; height <= 64 + (style == BarStyle.Floating ? 8 : 0); height++)
                {
                    foreach (var edge in Edges)
                    {
                        var layout = Bar(dpi, height, edge, icons: 3, overflow: true, style: style);
                        var what = $"{style}, {dpi} dpi, {height} DIP, {edge}";
                        var block = layout.BlockRect(layout.PartIndex(TrayKey));
                        var cells = new List<RectF> { layout.ItemRect(Hit.TrayOverflow) };
                        for (var index = 0; index < 3; index++)
                        {
                            cells.Add(layout.ItemRect(Hit.Tray(index)));
                        }
                        // §687: a dock's panel is its size less the 8 DIP it floats over, so a 32 DIP dock's is 24 thick: there a
                        // cell is the icon and its gap, as long as the panel is thick and no longer (Layout: "never longer than
                        // that thickness"); narrower wherever the panel is 32 DIP thick or more, as every bar's is.
                        var thick = height - (style == BarStyle.Floating ? 8 : 0);
                        foreach (var cell in cells)
                        {
                            Assert.False(cell.IsEmpty, what);
                            Assert.True(thick >= 32 ? Length(layout, cell) < Across(layout, cell) : Length(layout, cell) <= Across(layout, cell), $"{what}: a cell {cell.Width} by {cell.Height}");
                            Assert.Equal(layout.TrayStep, Length(layout, cell));
                            Assert.Equal(layout.TrayCell, Across(layout, cell));
                            // Centred across the bar, as the block it stands in is (a pixel either way
                            // for the two roundings), and never thicker than it.
                            Assert.True(
                                MathF.Abs(AcrossMiddle(layout, cell) - AcrossMiddle(layout, block)) <= 1f,
                                $"{what}: the cell's middle {AcrossMiddle(layout, cell)}, the block's {AcrossMiddle(layout, block)}");
                            Assert.True(Across(layout, cell) <= Across(layout, block), what);
                        }
                    }
                }
            }
        }
    }

    /// <summary>The picture stands in the middle of its cell, both ways, and inside it.</summary>
    [Fact]
    public void ThePictureIsCentredInItsCell()
    {
        foreach (var style in Enum.GetValues<BarStyle>())
        {
            foreach (var dpi in Dpis)
            {
                for (var height = 32; height <= 64; height += 4)
                {
                    foreach (var edge in Edges)
                    {
                        var layout = Bar(dpi, height, edge, icons: 3, overflow: true, style: style);
                        var what = $"{style}, {dpi} dpi, {height} DIP, {edge}";
                        for (var index = 0; index < 3; index++)
                        {
                            var cell = layout.ItemRect(Hit.Tray(index));
                            var box = layout.TrayIconRect(index);
                            Assert.Equal(layout.TrayIconSize, box.Width);
                            Assert.Equal(layout.TrayIconSize, box.Height);
                            Assert.True(box.X >= cell.X && box.Right <= cell.Right + 0.01f, what);
                            Assert.True(box.Y >= cell.Y && box.Bottom <= cell.Bottom + 0.01f, what);
                            // Room either side alike, give or take the pixel a floor takes.
                            Assert.True(MathF.Abs((box.X - cell.X) - (cell.Right - box.Right)) <= 1f, what);
                            Assert.True(MathF.Abs((box.Y - cell.Y) - (cell.Bottom - box.Bottom)) <= 1f, what);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// A drag reads the bar along it only (Dock.TrayTargetAt, TrayDrags.OnBar), from the block's start,
    /// the chevron's step and one cell's step to the next: the middle of every cell is that icon's
    /// own place among the shown ones, and the chevron's middle is the overflow, at every height, in
    /// every look and on every edge.
    /// </summary>
    [Fact]
    public void TheDropCellsAreTheCells()
    {
        const int icons = 4;
        foreach (var style in Enum.GetValues<BarStyle>())
        {
            foreach (var dpi in Dpis)
            {
                for (var height = 32; height <= 64; height += 4)
                {
                    foreach (var edge in Edges)
                    {
                        var layout = Bar(dpi, height, edge, icons: icons, overflow: true, style: style);
                        var what = $"{style}, {dpi} dpi, {height} DIP, {edge}";
                        var block = layout.PartAt(layout.PartIndex(TrayKey));
                        var chevronSlot = layout.TrayOverflow ? layout.TrayStep : 0f;

                        TrayDrop? DropAt(RectF cell) => TrayDrags.OnBar(
                            Along(layout, cell) + Length(layout, cell) / 2f,
                            block.Left,
                            block.Width,
                            chevronSlot,
                            layout.TrayStep,
                            icons,
                            hidden: 2,
                            held: true);

                        Assert.Equal(new TrayDrop(TrayArea.Overflow, 2), DropAt(layout.ItemRect(Hit.TrayOverflow)));
                        for (var index = 0; index < icons; index++)
                        {
                            var cell = layout.ItemRect(Hit.Tray(index));
                            // The cell starts where the drag counts it from.
                            Assert.True(
                                MathF.Abs(block.Left + chevronSlot + index * layout.TrayStep - Along(layout, cell)) <= 1f,
                                $"{what}: icon {index} at {Along(layout, cell)}");
                            Assert.Equal(new TrayDrop(TrayArea.Shown, index), DropAt(cell));
                            // And the gap a drag opens moves the pictures a whole step (Scene.TrayDrag).
                            if (index > 0)
                            {
                                Assert.Equal(
                                    layout.TrayStep,
                                    Along(layout, layout.TrayIconRect(index)) - Along(layout, layout.TrayIconRect(index - 1)),
                                    0.01f);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>The middle of <paramref name="rect"/> across the bar.</summary>
    private static float AcrossMiddle(Layout layout, RectF rect) =>
        layout.Vertical ? rect.X + rect.Width / 2f : rect.Y + rect.Height / 2f;

    // --- The picture ----------------------------------------------------------------

    [Fact]
    public void ThePictureFitsItsCellAndIsNeverSmallerThanWindowsDraws()
    {
        foreach (var dpi in Dpis)
        {
            foreach (var height in Heights)
            {
                var layout = Bar(dpi, height, Edge.Bottom, icons: 2);
                var what = $"{dpi} dpi, {height} DIP";

                Assert.True(layout.TrayIconSize < layout.TrayStep, what);
                Assert.True(layout.TrayIconSize >= MathF.Min(layout.SmallIcon, layout.TrayCell), what);
                // Inside its cell.
                var box = layout.TrayIconRect(0);
                var cell = layout.ItemRect(Hit.Tray(0));
                Assert.True(Along(layout, box) >= Along(layout, cell), what);
                Assert.True(
                    Along(layout, box) + Length(layout, box) <= Along(layout, cell) + Length(layout, cell) + 0.01f,
                    what);
            }
        }
    }

    /// <summary>
    /// The person, 2026-10-09: *"the icons that are not pins, like network, tray icons,
    /// notifications, etc in the taskbar are too big."* On the 48-DIP bar a tray icon is Windows'
    /// 16 DIP at every DPI, beside a pin's 24 (docs\widgets.md §57).
    /// </summary>
    [Fact]
    public void OnTheTaskbarsBarThePictureIsWindowsSixteenDips()
    {
        foreach (var dpi in Dpis)
        {
            var layout = Bar(dpi, 48, Edge.Bottom, icons: 2);
            var scale = dpi / 96f;

            Assert.Equal(MathF.Round(16 * scale, MidpointRounding.AwayFromZero), layout.TrayIconSize);
            Assert.Equal(MathF.Round(24 * scale, MidpointRounding.AwayFromZero), layout.Icon);
        }
    }

    /// <summary>
    /// At every height, DPI and edge, in every look, a tray icon is the size the network's, the
    /// sound's and the bell beside it are drawn at (<see cref="Fit.SystemIconFor"/>), never larger
    /// than an app icon, and in the middle of a cell with room round it.
    /// </summary>
    [Fact]
    public void ThePictureIsTheSizeOfTheStatusIconsBesideIt()
    {
        foreach (var style in Enum.GetValues<BarStyle>())
        {
            foreach (var dpi in Dpis)
            {
                foreach (var height in Heights)
                {
                    foreach (var edge in Edges)
                    {
                        var layout = Bar(dpi, height, edge, icons: 2, overflow: true, style: style);
                        var what = $"{style}, {dpi} dpi, {height} DIP, {edge}: tray {layout.TrayIconSize}, pin {layout.Icon}, cell {layout.TrayCell}";

                        Assert.Equal(MathF.Min(layout.Icon, Fit.SystemIconFor(layout.CellSide, layout.Scale)), layout.TrayIconSize);
                        Assert.Equal(layout.TrayIconSize, Layout.TrayIconPx(dpi, new Config { Height = height, Style = style }));
                        Assert.Equal(layout.TrayIconSize, layout.TrayIconRect(0).Width);
                        Assert.True(layout.TrayIconSize <= layout.Icon, what);
                        Assert.True(layout.TrayIconSize < layout.TrayStep, what);
                    }
                }
            }
        }
    }

    // --- The overflow ---------------------------------------------------------------

    /// <summary>
    /// The overflow's squares are the bar's thickness, five to a row, and its icons the bar's
    /// tray icons' size, which is what they were captured at.
    /// </summary>
    [Fact]
    public void TheOverflowsSquaresAreTheBars()
    {
        Assert.Equal(5, Layout.TrayOverflowColumns);
        foreach (var dpi in Dpis)
        {
            foreach (var height in Heights)
            {
                var layout = Bar(dpi, height, Edge.Bottom, icons: 2, overflow: true);
                Assert.Equal(layout.TrayCell, layout.TrayOverflowCell);
                Assert.True(layout.TrayOverflowCell >= layout.TrayIconSize);
            }
        }
    }

    /// <summary>
    /// The size the bar draws at is the size the dock asks apps for. An app hands over one bitmap
    /// rather than a group the shell can pick a size from, so a picture captured at one size and
    /// drawn at another is a stretched one; the two numbers are the same number.
    /// </summary>
    [Fact]
    public void TheCaptureSizeIsTheDrawnSize()
    {
        foreach (var dpi in Dpis)
        {
            foreach (var height in Heights)
            {
                var config = new Config { Height = height };

                Assert.Equal(Bar(dpi, height, Edge.Bottom, icons: 2).TrayIconSize, Layout.TrayIconPx(dpi, config));
            }
        }
    }

    // --- Hit-testing ----------------------------------------------------------------

    [Fact]
    public void EachCellIsHitOnItsOwn()
    {
        foreach (var edge in Edges)
        {
            var layout = Bar(96, 48, edge, icons: 4, overflow: true);
            for (var index = 0; index < 4; index++)
            {
                var (x, y) = Middle(layout.ItemRect(Hit.Tray(index)));
                var hit = layout.HitTest(x, y);
                Assert.Equal(HitKind.Tray, hit?.Kind);
                Assert.Equal(index, hit?.Index);
            }
            var chevron = Middle(layout.ItemRect(Hit.TrayOverflow));
            Assert.Equal(HitKind.TrayOverflow, layout.HitTest(chevron.X, chevron.Y)?.Kind);
        }
    }

    [Fact]
    public void NoPointOfTheBlockFallsBetweenTwoCells()
    {
        // The cells touch, so every point of the block is one of them, and a press anywhere on the
        // tray reaches an icon rather than the bar behind it.
        var layout = Bar(96, 48, Edge.Bottom, icons: 4, overflow: true);
        var block = layout.BlockRect(layout.PartIndex(TrayKey));
        var across = layout.Vertical ? block.X + block.Width / 2f : block.Y + block.Height / 2f;

        for (var at = Along(layout, block); at < Along(layout, block) + Length(layout, block); at += 1f)
        {
            var hit = layout.Vertical ? layout.HitTest(across, at) : layout.HitTest(at, across);
            Assert.True(hit is { Kind: HitKind.Tray or HitKind.TrayOverflow }, $"at {at}: {hit?.Kind}");
        }
    }
}
