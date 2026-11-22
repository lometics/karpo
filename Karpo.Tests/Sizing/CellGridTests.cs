// The widget grid: the cell's arithmetic (Layout.CellsFor, CellSpan, CellPx,
// CellTextDips) and what it does to a laid-out bar — every widget a whole
// number of cells, its cells filling it end to end, no overlap between parts
// and the same gap between every pair of neighbours at every height, scale and
// edge. Measured without a Gfx (Layout.ForTests).

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Sizing;

public sealed class CellGridTests
{
    private static readonly uint[] Dpis = [96, 120, 144, 192];

    private static readonly int[] Heights = [28, 32, 40, 48, 56, 72, 96, 115, 160];

    private static RECT Area(int width, int height) => new() { left = 0, top = 0, right = width, bottom = height };

    private static PartSpec Spec(string id, BarKind kind) => new(Config.IslandKey(null, id), kind);

    // --- The rule itself -------------------------------------------------------

    [Theory]
    [InlineData(0f, 1)]
    [InlineData(1f, 1)]
    [InlineData(44f, 1)]
    [InlineData(44.04f, 1)]     // within the slack: a measurement, not another cell
    [InlineData(45f, 2)]
    [InlineData(88f, 2)]
    [InlineData(88.5f, 3)]
    [InlineData(132f, 3)]
    public void ContentRoundsUpToWholeCells(float content, int cells) =>
        Assert.Equal(cells, Layout.CellsFor(content, 44f));

    [Fact]
    public void ACellIsNeverLessThanOneAndNeverUndefined()
    {
        Assert.Equal(1, Layout.CellsFor(-20f, 44f));
        Assert.Equal(1, Layout.CellsFor(float.NaN, 44f));
        Assert.Equal(1, Layout.CellsFor(float.PositiveInfinity, 0f));
        Assert.Equal(1, Layout.CellsFor(500f, 0f));
        Assert.Equal(1, Layout.CellsFor(500f, float.NaN));
    }

    [Fact]
    public void ASpanIsItsCellsTimesTheCell()
    {
        Assert.Equal(44f, Layout.CellSpan(30f, 44f));
        Assert.Equal(88f, Layout.CellSpan(45f, 44f));
        Assert.Equal(132f, Layout.CellSpan(89f, 44f));
        // No cell to stand on: whole pixels, so nothing falls between two.
        Assert.Equal(46f, Layout.CellSpan(45.2f, 0f));
    }

    [Fact]
    public void TheCellIsTheBarsUsableThickness()
    {
        foreach (var dpi in Dpis)
        {
            foreach (var height in Heights)
            {
                var config = new Config { Height = height };
                var cell = Layout.CellPx(dpi, config);
                Assert.Equal(Layout.IslandThicknessPx(dpi, config), cell);
                Assert.True(cell > 0, $"a cell at {height} DIP, {dpi} DPI");
                // The bar less the breathing room it keeps at each edge, and so
                // never thicker than the bar itself.
                Assert.True(cell <= Layout.ThicknessPx(dpi, config), $"{cell} within the bar at {height} DIP");
            }
        }
    }

    [Fact]
    public void WidgetTextGrowsWithTheCellBetweenItsBounds()
    {
        // The 48-DIP bar's card (38.4 DIPs): 12.5 DIPs of text, a third of it, and in proportion on any other.
        Assert.Equal(12.5f, Layout.CellTextDips(38.4f), 3);
        Assert.Equal(11f, Layout.CellTextDips(20f), 3);
        Assert.Equal(11f, Layout.CellTextDips(0f), 3);
        Assert.Equal(26f, Layout.CellTextDips(4000f), 3);
        // A cell that is not a number falls back to that bar's.
        Assert.Equal(12.5f, Layout.CellTextDips(float.NaN), 3);
        var last = 0f;
        for (var cell = 1f; cell <= 400f; cell += 1f)
        {
            var size = Layout.CellTextDips(cell);
            Assert.True(size >= last, $"text shrank at cell {cell}");
            last = size;
        }
    }

    /// <summary>
    /// An app button's label grows with the cell, as every widget's text does.
    /// Karpo.Tests\Sizing\OneRowTests holds that, since it needs the text measured.
    /// </summary>
    [Fact]
    public void ALabelGrowsWithTheCell()
    {
        var taskbar = Layout.LabelPxFor(96, new Config { Height = 48 }, 1f);
        Assert.Equal(12.5f, taskbar, 3);
        Assert.True(Layout.LabelPxFor(96, new Config { Height = 96 }, 1f) > taskbar, "a thicker bar, a bigger label");
    }

    [Fact]
    public void TheIconSizeAlwaysFollowsTheBar()
    {
        // Half the bar's height on a 2-DIP grid, 16 at least, at every height.
        foreach (var height in Heights)
        {
            var icon = new Config { Height = height }.IconDips();
            Assert.Equal(MathF.Max(16f, MathF.Round(height / 4f, MidpointRounding.AwayFromZero) * 2f), icon, 3);
        }
    }

    [Fact]
    public void ATallBarsAppIconsKeepGrowing()
    {
        // They stopped at 24 DIPs: a third of a 72-DIP bar, beside widgets that had grown.
        Assert.Equal(36f, new Config { Height = 72 }.IconDips(), 3);
        Assert.Equal(80f, new Config { Height = Config.MaxHeight }.IconDips(), 3);
    }

    // --- A width someone chose -------------------------------------------------

    [Fact]
    public void WithoutAChosenWidthEveryCellTakesWhatItsContentNeeds()
    {
        var lengths = Layout.AllotCells([10f, 50f, 100f], WidgetItem.AutomaticWidth, 44f);

        // Exactly what each needs, one cell at least: not whole cells (docs 56).
        Assert.Equal([44f, 50f, 100f], lengths);
    }

    [Fact]
    public void AChosenWidthIsSharedOutLargestFirst()
    {
        // Six cells between a wide first cell and three buttons: everyone keeps
        // one and the one that wanted more takes the rest.
        Assert.Equal([3 * 44f, 44f, 44f, 44f], Layout.AllotCells([130f, 20f, 20f, 20f], 6, 44f));
        // Nobody wants the spare room and neither is more elastic than the
        // other, so it is spread rather than dumped on one of them.
        Assert.Equal([2 * 44f, 2 * 44f], Layout.AllotCells([20f, 20f], 4, 44f));
        // One cell of content asked to be two: it gets two and fills them.
        Assert.Equal([88f], Layout.AllotCells([30f], 2, 44f));
    }

    [Fact]
    public void RoomNobodyAskedForGoesToTheCellThatCanUseIt()
    {
        // The media widget's shape: a wide info cell and three buttons. At any
        // chosen width the buttons stay one cell each — a glyph in a cell twice
        // its size leaves the row unevenly spaced and the card dead after it —
        // and the info cell, the elastic one, takes everything over.
        foreach (var cells in new[] { 4, 5, 6, 7, 8 })
        {
            var lengths = Layout.AllotCells([130f, 20f, 20f, 20f], cells, 44f);

            Assert.Equal(44f, lengths[1]);
            Assert.Equal(44f, lengths[2]);
            Assert.Equal(44f, lengths[3]);
            Assert.Equal((cells - 3) * 44f, lengths[0]);
            Assert.Equal(cells * 44f, lengths.Sum());
        }
    }

    [Fact]
    public void WhereEveryCellWantsTheSameTheRoomIsSpreadRatherThanDumped()
    {
        // A row of drives, or of buttons: nobody is more elastic than anybody
        // else, so the spare is handed round one cell at a time instead of
        // landing on one of them.
        Assert.Equal([2 * 44f, 2 * 44f, 44f], Layout.AllotCells([20f, 20f, 20f], 5, 44f));
        Assert.Equal([2 * 44f, 44f, 44f], Layout.AllotCells([20f, 20f, 20f], 4, 44f));
        Assert.Equal([2 * 44f, 2 * 44f, 2 * 44f], Layout.AllotCells([20f, 20f, 20f], 6, 44f));
        // And never more than one cell apart, whatever the width.
        foreach (var cells in Enumerable.Range(3, 6))
        {
            var lengths = Layout.AllotCells([20f, 20f, 20f], cells, 44f);
            Assert.True((lengths.Max() - lengths.Min()) <= 44f, $"{cells} cells came out {lengths.Max() - lengths.Min()} px apart");
        }
    }

    [Fact]
    public void AChosenWidthTooSmallForEveryCellShowsWhatFits()
    {
        // The floor is one grid cell per cell shown; the rest are not drawn.
        var lengths = Layout.AllotCells([40f, 40f, 40f, 40f], 2, 44f);

        Assert.Equal([44f, 44f, 0f, 0f], lengths);
        Assert.Equal(88f, lengths.Sum());
    }

    [Fact]
    public void AChosenWidthIsExactlyThatManyCells()
    {
        foreach (var cells in Enumerable.Range(WidgetItem.MinWidth, WidgetItem.MaxWidth))
        {
            foreach (var content in new[] { new[] { 5f }, [300f], [20f, 20f, 500f] })
            {
                var lengths = Layout.AllotCells(content, cells, 44f);

                Assert.Equal(cells * 44f, lengths.Sum(), 3);
                Assert.All(lengths, length => Assert.Equal(MathF.Round(length / 44f), length / 44f, 3));
            }
        }
    }

    [Fact]
    public void AWidgetOnTheBarGetsTheWidthItWasGiven()
    {
        var specs = new List<PartSpec>
        {
            new(Config.IslandKey(null, "w"), BarKind.WidgetsBoard, 4),
            new(Config.IslandKey(null, "d"), BarKind.WidgetsBoard, 2),
            new(Config.IslandKey(null, "s"), BarKind.NightLight),
        };
        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Bottom, new Config { Height = 48 }, specs, cellLength: 30f);
        var cell = layout.CellSide;

        Assert.Equal(4f, layout.PartAt(0).Width / cell, 3);
        Assert.Equal(2f, layout.PartAt(1).Width / cell, 3);
        // Nobody chose one for the third: its content decides, and 30 px is one cell.
        Assert.Equal(1f, layout.PartAt(2).Width / cell, 3);
    }

    // --- What it does to a bar -------------------------------------------------

    /// <summary>A bar of widgets in all three groups, with the taskbar's own parts among them.</summary>
    private static List<PartSpec> Bar() =>
    [
        Spec("start", BarKind.Start),
        Spec("weather", BarKind.WidgetsBoard),
        Spec("drives", BarKind.WidgetsBoard),
        Spec("apps", BarKind.Apps),
        Spec("timer", BarKind.NightLight),
        Spec("media", BarKind.WidgetsBoard),
        Spec("battery", BarKind.EnergySaver),
        Spec("tray", BarKind.Tray),
        Spec("calendar", BarKind.NightLight),
    ];

    [Fact]
    public void EveryWidgetIsOneCellAtLeastAndWholePixelsAtEveryHeightScaleAndEdge()
    {
        foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
        {
            foreach (var dpi in Dpis)
            {
                foreach (var height in Heights)
                {
                    var config = new Config { Height = height };
                    var vertical = edge.IsVertical();
                    var layout = Layout.ForTests(
                        dpi,
                        vertical ? Area(1200, 2400) : Area(2400, 1200),
                        edge,
                        config,
                        Specs(),
                        // An awkward content length, so the rounding has work to do.
                        cellLength: 37f * (dpi / 96f) + 5f);
                    var cell = layout.CellSide;
                    Assert.Equal(Layout.CellPx(dpi, config), cell);
                    foreach (var part in layout.Parts)
                    {
                        if (part.Width <= 0f || !IsWidget(part.Kind))
                        {
                            continue;
                        }
                        var cells = part.Width / cell;
                        // As long as its content needs, not whole cells: whole pixels, one cell at least.
                        Assert.Equal(MathF.Round(part.Width), part.Width, 3);
                        Assert.True(cells >= 1f, $"{part.Kind} at {height} DIP, {dpi} DPI is {cells} cells");
                    }
                }
            }
        }

        static List<PartSpec> Specs() => Bar();
        static bool IsWidget(BarKind kind) => kind.IsIsland();
    }

    [Fact]
    public void AWidgetsCellsFillItEndToEndAndAreCellsThemselves()
    {
        var layout = Layout.ForTests(144, Area(3000, 1500), Edge.Bottom, new Config { Height = 72 }, Bar(), cellLength: 61f);
        var cell = layout.CellSide;
        for (var part = 0; part < layout.Specs.Count; part++)
        {
            if (!layout.Specs[part].Kind.IsIsland() || !layout.Placed(part))
            {
                continue;
            }
            var block = layout.BlockRect(part);
            var card = layout.CardRect(part);
            Assert.Equal(block.Width, card.Width, 3);
            Assert.Equal(block.X, card.X, 3);
            var at = block.X;
            for (var index = 0; index < layout.PartAt(part).Count; index++)
            {
                var rect = layout.ItemRect(Hit.Cell(part, index));
                Assert.False(rect.IsEmpty, $"cell {index} of part {part}");
                // Contiguous: no gap and no overlap inside a widget.
                Assert.Equal(at, rect.X, 3);
                Assert.True(rect.Width >= cell - 0.01f, $"cell {index} of part {part} is shorter than a cell");
                Assert.Equal(MathF.Round(rect.Width), rect.Width, 3);
                // One cell across the bar: the cell is a square.
                Assert.Equal(cell, rect.Height, 3);
                at = rect.Right;
            }
            Assert.Equal(block.Right, at, 3);
        }
    }

    [Fact]
    public void NeighbouringWidgetsKeepOneGapAndNeverOverlap()
    {
        foreach (var height in Heights)
        {
            // The islands look, so there is a gap to keep the same.
            var layout = Layout.ForTests(120, Area(3000, 1500), Edge.Bottom, new Config { Height = height, Style = BarStyle.Islands }, Bar(), cellLength: 50f);
            var placed = Enumerable.Range(0, layout.Specs.Count)
                .Where(layout.Placed)
                .Select(index => (Index: index, Part: layout.PartAt(index)))
                .OrderBy(entry => entry.Part.Left)
                .ToList();
            var gaps = new List<float>();
            for (var at = 1; at < placed.Count; at++)
            {
                var before = placed[at - 1].Part;
                var after = placed[at].Part;
                Assert.True(before.Right <= after.Left + 0.01f, $"{before.Kind} runs into {after.Kind} at {height} DIP");
                if (before.Kind.IsIsland() && after.Kind.IsIsland())
                {
                    gaps.Add(after.Left - before.Right);
                }
            }
            // The room between two widgets is the same everywhere on the bar:
            // whole cells never push one pair further apart than another.
            Assert.All(gaps, gap => Assert.Equal(gaps[0], gap, 3));
        }
    }
}
