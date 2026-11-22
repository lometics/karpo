// Every widget on the bar stands alone (§514; the person, 2026-11-05: "every widget is just its own group / group should not
// really exist anymore for the taskbar itself"): one gap between any two neighbours of a section, a widget's (6 DIP, §501,
// §506), whatever they are — the pins and the open apps, a bare corner, a search box — on every kind of bar; each widget on
// its own card (§512). The sections are laid out each on its own, at least 14 DIP apart on a docked bar.
// The app buttons of one part stand a widget's gap apart too; a pin and its running app take the same room.
//
// Held here without drawing: the measurement. The cards are held on the real tree in SceneTreeTests (GroupCards.cs,
// LookSurfacePictures.cs).

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Islands;

public sealed class WidgetGapTests
{
    private static RECT Area(Edge edge) =>
        edge.IsVertical() ? new RECT { right = 1600, bottom = 3000 } : new RECT { right = 3000, bottom = 1600 };

    private static PartSpec Widget(string id, BarKind kind = BarKind.DoNotDisturb, BarSection section = BarSection.Start) =>
        new PartSpec(Config.IslandKey(null, id), kind) { Section = section };

    private static Layout Bar(IReadOnlyList<PartSpec> specs, BarStyle style = BarStyle.Islands, Edge edge = Edge.Bottom, uint dpi = 96, bool editing = false, int tiles = 0)
    {
        var config = new Config { Height = 48, Style = style };
        return Layout.ForTests(dpi, Area(edge), edge, config, specs, Layout.CellPx(dpi, config), editing, tiles: tiles);
    }

    private static float Along(Layout layout, RectF rect) => layout.Vertical ? rect.Y : rect.X;

    private static float Reach(Layout layout, RectF rect) => layout.Vertical ? rect.Bottom : rect.Right;

    /// <summary>The room between part <paramref name="after"/>'s block and part <paramref name="before"/>'s.</summary>
    private static float Gap(Layout layout, int before, int after) =>
        Along(layout, layout.BlockRect(after)) - Reach(layout, layout.BlockRect(before));

    /// <summary>Every two neighbours of a section stand a widget's gap apart, along every edge, in both looks.</summary>
    [Theory]
    [InlineData(Edge.Bottom, BarStyle.Islands)]
    [InlineData(Edge.Top, BarStyle.Islands)]
    [InlineData(Edge.Left, BarStyle.Islands)]
    [InlineData(Edge.Right, BarStyle.Islands)]
    [InlineData(Edge.Bottom, BarStyle.Floating)]
    public void NeighboursStandAWidgetsGapApart(Edge edge, BarStyle style)
    {
        var layout = Bar([Widget("a"), Widget("b", BarKind.NightLight), Widget("c", BarKind.EnergySaver)], style, edge);

        Assert.True(layout.WidgetGap > 0f);
        for (var part = 1; part < 3; part++)
        {
            Assert.Equal(layout.WidgetGap, Gap(layout, part - 1, part));
        }
    }

    /// <summary>
    /// §501, §506: a widget's gap is 6 DIP — over the 5 between two faces on one button, so two buttons read as two — in whole
    /// px at every scale, in both looks; two sections of a docked bar stand at least 14 DIP apart.
    /// </summary>
    [Theory]
    [InlineData(96u, 6f, 14f)]
    [InlineData(144u, 9f, 21f)]
    [InlineData(192u, 12f, 28f)]
    public void AWidgetsGapIsSixDipAndTwoSectionsFourteen(uint dpi, float widget, float section)
    {
        foreach (var style in new[] { BarStyle.Islands, BarStyle.Floating })
        {
            var layout = Bar([Widget("a"), Widget("b", BarKind.NightLight)], style, dpi: dpi);

            Assert.Equal(widget, layout.WidgetGap);
            Assert.Equal(widget, Gap(layout, 0, 1));
        }
        var islands = Bar([Widget("a")], dpi: dpi);
        Assert.Equal(section, islands.SectionGap);
        Assert.True(FolderIsland.Between(new Fit(new IslandStyle(dpi / 96f, false, islands.CellSide, islands.CellSide, default))) < islands.WidgetGap);
    }

    /// <summary>§512: every widget wears its own card in its own rectangle (its card's room, else its block), a widget's gap of the bar between two.</summary>
    [Theory]
    [InlineData(Edge.Bottom, 96u, BarStyle.Islands)]
    [InlineData(Edge.Top, 144u, BarStyle.Islands)]
    [InlineData(Edge.Left, 120u, BarStyle.Islands)]
    [InlineData(Edge.Right, 96u, BarStyle.Islands)]
    [InlineData(Edge.Bottom, 144u, BarStyle.Floating)]
    public void EachWidgetWearsItsOwnCard(Edge edge, uint dpi, BarStyle style)
    {
        var layout = Bar([Widget("a"), Widget("b", BarKind.NightLight), Widget("c", BarKind.EnergySaver), Widget("d", BarKind.TouchKeyboard, BarSection.End)], style, edge, dpi);
        RectF Own(int part) => layout.CardRect(part) is { IsEmpty: false } card ? card : layout.BlockRect(part);

        foreach (var part in new[] { 0, 1, 2, 3 })
        {
            Assert.Equal(Own(part), layout.Plate(part));
        }
        for (var part = 1; part < 3; part++)
        {
            Assert.Equal(layout.WidgetGap, Along(layout, layout.Plate(part)) - Reach(layout, layout.Plate(part - 1)));
        }
    }

    /// <summary>
    /// §514: what stood apart before stands as every widget does — a bare corner (§302) and a search box (§325) a widget's gap
    /// from their neighbours, no wider room beside them.
    /// </summary>
    [Fact]
    public void ABareCornerAndASearchBoxStandAsEveryWidget()
    {
        var layout = Bar([
            Widget("a"),
            Widget("s", BarKind.Search) with { Box = true },
            Widget("b", BarKind.NightLight),
            Widget("d", BarKind.ShowDesktop) with { Bare = true },
        ]);

        Assert.True(layout.SearchBoxed(1));
        for (var part = 1; part < 4; part++)
        {
            Assert.Equal(layout.WidgetGap, Gap(layout, part - 1, part));
        }
    }

    /// <summary>The sections are laid out each on its own, at least a section's gap apart: a widget's gap never runs over a section's end.</summary>
    [Fact]
    public void SectionsStandApart()
    {
        var layout = Bar([
            Widget("a"),
            Widget("b", BarKind.NightLight),
            Widget("c", BarKind.EnergySaver, BarSection.Middle),
            Widget("d", BarKind.TouchKeyboard, BarSection.End),
            Widget("e", BarKind.DoNotDisturb, BarSection.End),
        ]);

        Assert.Equal(layout.WidgetGap, Gap(layout, 0, 1));
        Assert.True(Gap(layout, 1, 2) >= layout.SectionGap);
        Assert.True(Gap(layout, 2, 3) >= layout.SectionGap);
        Assert.Equal(layout.WidgetGap, Gap(layout, 3, 4));
    }

    /// <summary>A widget of another section does not part two widgets of this one, whatever the order the list holds them in.</summary>
    [Fact]
    public void AnotherSectionsWidgetPartsNothing()
    {
        var layout = Bar([Widget("a"), Widget("z", BarKind.EnergySaver, BarSection.End), Widget("b", BarKind.NightLight)]);

        Assert.Equal(layout.WidgetGap, Gap(layout, 0, 2));
    }

    // --- The app buttons ---------------------------------------------------------------

    /// <summary>
    /// The pins and the open apps stand as every widget: Start, two pins, the open apps and a widget after them a widget's gap
    /// apart, and inside the Apps widget every button a widget's gap from the next too.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Left)]
    public void PinsAndTheOpenAppsStandAsEveryWidget(Edge edge)
    {
        var layout = Bar(
            [Widget("s", BarKind.Start), Widget("p", BarKind.Pin), Widget("q", BarKind.Pin), Widget("apps", BarKind.Apps), Widget("b", BarKind.NightLight)],
            edge: edge,
            tiles: 2 + 3);

        for (var part = 1; part < 5; part++)
        {
            Assert.Equal(layout.WidgetGap, Gap(layout, part - 1, part));
        }
        for (var tile = 1; tile < 5; tile++)
        {
            Assert.Equal(layout.WidgetGap, Along(layout, layout.TileCell(tile)) - Reach(layout, layout.TileCell(tile - 1)));
        }
    }

    /// <summary>A pin and the same app running unpinned are the same button: either takes the same room, so the bar is as long whichever it is.</summary>
    [Theory]
    [InlineData(96u)]
    [InlineData(144u)]
    public void APinAndARunningAppTakeTheSameRoom(uint dpi)
    {
        var pinned = Bar([Widget("a"), Widget("p", BarKind.Pin), Widget("b", BarKind.NightLight)], dpi: dpi, tiles: 1);
        var running = Bar([Widget("a"), Widget("apps", BarKind.Apps), Widget("b", BarKind.NightLight)], dpi: dpi, tiles: 1);

        Assert.Equal(pinned.BlockRect(1), running.BlockRect(1));
        Assert.Equal(pinned.TileCell(0), running.TileCell(0));
        Assert.Equal(pinned.BlockRect(2), running.BlockRect(2));
    }

    /// <summary>The Apps widget with nothing open is no part of the bar at rest: the pins before it and the widget after it a widget's gap apart.</summary>
    [Fact]
    public void AnEmptyAppsWidgetLeavesNoSeam()
    {
        var layout = Bar([Widget("p", BarKind.Pin), Widget("apps", BarKind.Apps), Widget("b", BarKind.NightLight)], tiles: 1);

        Assert.False(layout.Placed(1));
        Assert.Equal(layout.WidgetGap, Gap(layout, 0, 2));
    }

    // --- Edit mode -------------------------------------------------------------------

    /// <summary>
    /// Entering edit mode moves nothing: every widget stands where it stood at rest, and a floating bar's docks too, in
    /// both looks, along the bottom and down the side, at three scales.
    /// </summary>
    [Theory]
    [InlineData(BarStyle.Islands, Edge.Bottom, 96u)]
    [InlineData(BarStyle.Islands, Edge.Bottom, 144u)]
    [InlineData(BarStyle.Islands, Edge.Left, 120u)]
    [InlineData(BarStyle.Floating, Edge.Bottom, 96u)]
    [InlineData(BarStyle.Floating, Edge.Bottom, 144u)]
    [InlineData(BarStyle.Floating, Edge.Left, 120u)]
    public void EnteringEditModeMovesNothing(BarStyle style, Edge edge, uint dpi)
    {
        var specs = new[] { Widget("a"), Widget("b", BarKind.NightLight), Widget("c", BarKind.EnergySaver, BarSection.End) };
        var rest = Bar(specs, style, edge, dpi);
        var editing = Bar(specs, style, edge, dpi, editing: true);

        for (var part = 0; part < specs.Length; part++)
        {
            Assert.Equal(rest.BlockRect(part), editing.BlockRect(part));
        }
        Assert.Equal(rest.SectionPanels(), editing.SectionPanels());
    }

    /// <summary>The "Add" marker stands in the middle of the gap between two widgets, and past a section's last where the new widget's gap will be.</summary>
    [Theory]
    [InlineData(BarStyle.Islands)]
    [InlineData(BarStyle.Floating)]
    public void TheAddMarkerStandsInTheGap(BarStyle style)
    {
        var layout = Bar([Widget("a"), Widget("b", BarKind.NightLight), Widget("c", BarKind.EnergySaver)], style, editing: true);
        var seam = layout.InsertionRect(new BarSlot(BarSection.Start, 1));
        var join = (Reach(layout, layout.BlockRect(0)) + Along(layout, layout.BlockRect(1))) / 2f;

        Assert.True(MathF.Abs(seam.X + (seam.Width / 2f) - join) <= 1f, "the line stands in the middle of the gap between the two blocks");
        var pair = Bar([Widget("a"), Widget("b", BarKind.NightLight)], style, editing: true);
        var after = pair.InsertionRect(new BarSlot(BarSection.Start, 2));
        Assert.True(MathF.Abs(after.X + (after.Width / 2f) - (Reach(pair, pair.BlockRect(1)) + (pair.WidgetGap / 2f))) <= 1f, "the line stands where the next widget's gap will be");
    }
}
