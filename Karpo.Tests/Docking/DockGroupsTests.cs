// A floating bar's docks (§511): the bar has no separator, so nothing splits a section — each section that holds a
// widget is one dock, its own floating panel (Layout.SectionPanels), with the room between two docks between it and the
// next and the desktop showing there; its widgets stand a widget's gap apart inside it. A press between two docks is the
// desktop's. Until §511 a separator split a section's dock in two (the person, 2026-10-10: "it should look like they are
// separate 'docks'/groups"); the sections are the docks now. Pure: no window is made.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Docking;

public sealed class DockGroupsTests
{
    /// <summary>Start at the start; search and Task view in the middle; at the end the Widgets button, the touch keyboard and energy saver.</summary>
    private static List<BarItem> Bar()
    {
        List<BarItem> islands =
        [
            BarKind.Start.New(),
            BarKind.Search.New(),
            BarKind.TaskView.New(),
            BarKind.WidgetsBoard.New(),
            BarKind.TouchKeyboard.New(),
            BarKind.EnergySaver.New(),
        ];
        foreach (var island in islands)
        {
            island.Section = island.Kind switch
            {
                BarKind.Start => BarSection.Start,
                BarKind.Search or BarKind.TaskView => BarSection.Middle,
                _ => BarSection.End,
            };
        }
        return islands;
    }

    /// <summary>The bar a display along <paramref name="screen"/> lays out for <paramref name="islands"/> as a Dock, as DockSectionsTests does.</summary>
    private static Layout Laid(List<BarItem> islands, Edge screen = Edge.Bottom, uint dpi = 96, bool editing = false)
    {
        var layout = new TaskbarLayout { Type = LayoutType.Dock, Islands = islands };
        var config = new Config().WithEdge(layout.Edges(screen)[screen]);
        var shown = TheDock.Shows(layout, screen, null, screen);
        var specs = shown.Select(island => PartSpec.For(Config.IslandKey(null, island.Id), island)).ToList();
        var area = new RECT { left = 0, top = 0, right = 1920 * (int)dpi / 96, bottom = 1080 * (int)dpi / 96 };
        return Layout.ForTests(dpi, area, screen, config, specs, Layout.CellPx(dpi, config), editing: editing);
    }

    private static float Px(uint dpi, float dip) => MathF.Round(dip * dpi / 96f, MidpointRounding.AwayFromZero);

    private static int IndexOf(Layout layout, BarKind kind) => layout.Specs.ToList().FindIndex(spec => spec.Kind == kind);

    /// <summary>
    /// Each section that holds a widget is one dock: the end's three widgets stand in one panel, the bar's thickness
    /// and its lift off the edge as any dock, a widget's gap apart and the panel's own room inside either end; a
    /// section holding nothing has none. At the bottom and the top, 100 % and 150 %.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, 96u)]
    [InlineData(Edge.Top, 96u)]
    [InlineData(Edge.Bottom, 144u)]
    [InlineData(Edge.Top, 144u)]
    public void EachSectionIsOneDock(Edge screen, uint dpi)
    {
        var layout = Laid(Bar(), screen, dpi);
        var docks = layout.SectionPanels();
        var end = docks[(int)BarSection.End];

        Assert.Equal(3, docks.Count(dock => !dock.IsEmpty));
        Assert.Equal(layout.Thickness, end.Height);
        var parts = new[] { BarKind.WidgetsBoard, BarKind.TouchKeyboard, BarKind.EnergySaver }.Select(kind => layout.BlockRect(IndexOf(layout, kind))).ToList();
        Assert.All(parts, block => Assert.True(block.X > end.X && block.Right < end.Right, "the end's widgets stand in its one dock"));
        Assert.Equal(layout.WidgetGap, parts[1].X - parts[0].Right);
        Assert.Equal(layout.WidgetGap, parts[2].X - parts[1].Right);
        // The panel's own room inside each end, as at a section's ends.
        Assert.Equal(parts[0].X - end.X, end.Right - parts[2].Right);

        var alone = Bar().Where(island => island.Section != BarSection.Middle).ToList();
        Assert.True(Laid(alone, screen, dpi).SectionPanels()[(int)BarSection.Middle].IsEmpty, "an empty section has no dock");
    }

    /// <summary>
    /// At rest a press lands on a dock or the room under it, to the screen's edge, and nowhere between two docks: the
    /// gap between the middle's dock and the end's is the desktop's.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, 96u)]
    [InlineData(Edge.Top, 144u)]
    public void APressBetweenTwoDocksIsTheDesktops(Edge screen, uint dpi)
    {
        var layout = Laid(Bar(), screen, dpi);
        var reach = layout.PanelReach();
        var docks = layout.SectionPanels().Where(dock => !dock.IsEmpty).ToList();
        var (middle, end) = (layout.SectionPanels()[(int)BarSection.Middle], layout.SectionPanels()[(int)BarSection.End]);
        var gap = middle.Center with { X = (middle.Right + end.X) / 2f };

        Assert.True(end.X - middle.Right >= Px(dpi, 8), "two docks stand at least the room between two docks apart");
        Assert.Equal(docks.Count, reach.Count);
        Assert.DoesNotContain(reach, rect => rect.Contains(gap.X, gap.Y));
        // Under the gap, by the screen's edge, too.
        var edgeY = screen == Edge.Top ? 0f : layout.Height - 1f;
        Assert.DoesNotContain(reach, rect => rect.Contains(gap.X, edgeY));
        // Each dock is pressed, and so is the room it floats over.
        foreach (var dock in docks)
        {
            Assert.Contains(reach, rect => rect.Contains(dock.Center.X, dock.Center.Y));
            Assert.Contains(reach, rect => rect.Contains(dock.Center.X, edgeY));
        }
    }

    /// <summary>Edit mode moves nothing: every widget and every dock stands where it stood at rest. Both edges, 100 % and 150 %.</summary>
    [Theory]
    [InlineData(Edge.Bottom, 96u)]
    [InlineData(Edge.Top, 96u)]
    [InlineData(Edge.Bottom, 144u)]
    [InlineData(Edge.Top, 144u)]
    public void EditModeMovesNothing(Edge screen, uint dpi)
    {
        var rest = Laid(Bar(), screen, dpi);
        var edit = Laid(Bar(), screen, dpi, editing: true);

        Assert.Equal(rest.SectionPanels(), edit.SectionPanels());
        for (var part = 0; part < rest.Specs.Count; part++)
        {
            Assert.Equal(rest.BlockRect(part), edit.BlockRect(part));
        }
    }

    /// <summary>
    /// A block carried to another section joins that section's dock while it is carried: the touch keyboard carried
    /// to the middle grows the middle's dock round it and shrinks the end's, still one dock each.
    /// </summary>
    [Fact]
    public void CarryingToAnotherSectionJoinsItsDock()
    {
        var edit = Laid(Bar(), editing: true);
        var keyboard = IndexOf(edit, BarKind.TouchKeyboard);
        var toTheMiddle = edit.EndSlot(BarSection.Middle);

        var carried = edit.SectionPanels(keyboard, toTheMiddle);
        var lefts = edit.Arrange(keyboard, toTheMiddle);

        Assert.Equal(3, carried.Count(dock => !dock.IsEmpty));
        var middle = carried[(int)BarSection.Middle];
        Assert.True(middle.X < lefts[keyboard] && lefts[keyboard] < middle.Right, "the carried block stands in the middle's dock");
        Assert.True(middle.Width > edit.SectionPanels()[(int)BarSection.Middle].Width);
        Assert.True(carried[(int)BarSection.End].Width < edit.SectionPanels()[(int)BarSection.End].Width);
    }
}
