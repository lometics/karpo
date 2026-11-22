// A Dock layout shows its start, middle and end (the person, 2026-10-09: "dock mode should get the
// left and right slot as well" — as three separate floating docks): the start a dock at the left end
// of the display's edge, the middle one centred, the end one at the right end, each as long as what
// it holds; a section with nothing in it no dock at rest, and its empty room in edit mode. The bar
// is still one strip along the edge, as deep as a dock and its lift, which a reserving dock holds
// (Layout.Reserve); on hover each dock is a place of its own (HoverPlaces). Pure: no window is made.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Docking;

public sealed class DockSectionsTests
{
    /// <summary>Start at the start, search, Task view and the open apps in the middle, a graph and the clock at the end.</summary>
    private static List<BarItem> Three()
    {
        List<BarItem> islands = [BarKind.Start.New(), BarKind.Search.New(), BarKind.TaskView.New(), BarKind.Apps.New(), BarKind.WidgetsBoard.New(), BarKind.NightLight.New()];
        foreach (var island in islands)
        {
            island.Section = (island.Kind switch
            {
                BarKind.Start => BarSection.Start,
                BarKind.WidgetsBoard or BarKind.NightLight => BarSection.End,
                _ => BarSection.Middle,
            });
        }
        Sections.Order(islands);
        return islands;
    }

    private static TaskbarLayout Dock(List<BarItem> islands) => new() { Type = LayoutType.Dock, Islands = islands };

    /// <summary>
    /// The bar a display along <paramref name="screen"/> lays out for <paramref name="layout"/>, as the
    /// dock does it (Dock.Shows, Config.WithEdge, DockWindow's Layout with no place).
    /// </summary>
    private static Layout Laid(TaskbarLayout layout, Edge screen, uint dpi, bool editing = false)
    {
        var config = new Config().WithEdge(layout.Edges(screen)[screen]);
        var shown = TheDock.Shows(layout, screen, null, screen);
        var specs = shown.Select(island => PartSpec.For(Config.IslandKey(null, island.Id), island)).ToList();
        var area = new RECT { left = 0, top = 0, right = 1920 * (int)dpi / 96, bottom = 1080 * (int)dpi / 96 };
        return Layout.ForTests(dpi, area, screen, config, specs, Layout.CellPx(dpi, config), editing: editing, tiles: 3);
    }

    private static float Px(uint dpi, float dip) => MathF.Round(dip * dpi / 96f, MidpointRounding.AwayFromZero);

    private static float OffTheEdge(Layout layout, RectF rect) => layout.Edge == Edge.Top ? rect.Y : layout.Height - rect.Bottom;

    /// <summary>A Dock shows its three sections, all of its widgets there on its one bar.</summary>
    [Fact]
    public void ADockShowsAllThreeSections()
    {
        var layout = Dock(Three());

        Assert.Equal(layout.Islands.Select(island => island.Id), TheDock.Shows(layout, Edge.Bottom, null, Edge.Bottom).Select(island => island.Id));
        var edges = layout.Edges(Edge.Bottom);
        Assert.All(new[] { BarSection.Start, BarSection.Middle, BarSection.End }, section => Assert.True(edges.Has(Edge.Bottom, section)));
        Assert.Equal(BarStyle.Floating, edges.Bottom.Style);
    }

    /// <summary>
    /// Three docks, each round its own widgets, the bar's thickness across and 8 DIPs off the screen's
    /// edge: the start's 8 DIPs from the left end, the end's 8 from the right, the middle's centred —
    /// at the bottom and the top, at 100 % and 150 %.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, 96u)]
    [InlineData(Edge.Top, 96u)]
    [InlineData(Edge.Bottom, 144u)]
    [InlineData(Edge.Top, 144u)]
    public void ThreeDocksStandLeftCentredAndRight(Edge screen, uint dpi)
    {
        var layout = Laid(Dock(Three()), screen, dpi);
        var panels = layout.SectionPanels();
        var margin = Px(dpi, 8);

        Assert.True(layout.Floating);
        Assert.Equal(screen, layout.Edge);
        Assert.All(panels, panel => Assert.False(panel.IsEmpty));
        Assert.All(panels, panel => Assert.Equal(layout.Thickness, panel.Height));
        Assert.All(panels, panel => Assert.Equal(margin, OffTheEdge(layout, panel)));
        Assert.Equal(margin, panels[0].X);
        Assert.Equal(layout.Length - margin, panels[2].Right);
        Assert.Equal(layout.Length / 2f, panels[1].X + (panels[1].Width / 2f), 1f);
        // Each as long as what it holds, and well apart: no dock runs along the edge.
        Assert.True(panels[1].X - panels[0].Right > margin);
        Assert.True(panels[2].X - panels[1].Right > margin);
        for (var part = 0; part < layout.Specs.Count; part++)
        {
            var panel = panels[(int)layout.Specs[part].Section];
            var block = layout.BlockRect(part);
            Assert.True(block.X >= panel.X && block.Right <= panel.Right, $"{layout.Specs[part].Kind} outside its dock");
        }
    }

    /// <summary>
    /// A section with nothing in it is no dock at rest and takes no room; in edit mode it shows its
    /// empty room where a widget dropped there would stand — the start's at the left end, the end's
    /// at the right — and still no dock.
    /// </summary>
    [Theory]
    [InlineData(BarSection.Start)]
    [InlineData(BarSection.Middle)]
    [InlineData(BarSection.End)]
    public void AnEmptySectionIsNoDockAndAnEmptyRoomInEditMode(BarSection gone)
    {
        var islands = Three();
        islands.RemoveAll(island => island.Section == gone);
        var layout = Dock(islands);

        var rest = Laid(layout, Edge.Bottom, 96);
        Assert.True(rest.SectionPanels()[(int)gone].IsEmpty);
        Assert.Equal(2, rest.SectionPanels().Count(panel => !panel.IsEmpty));
        Assert.Empty(rest.EmptyRooms());

        var edit = Laid(layout, Edge.Bottom, 96, editing: true);
        Assert.True(edit.SectionPanels()[(int)gone].IsEmpty);
        var room = Assert.Single(edit.EmptyRooms());
        Assert.Equal(gone, edit.SectionAt(room.X + (room.Width / 2f)));
        switch (gone)
        {
            case BarSection.Start:
                Assert.True(room.X < edit.SectionPanels()[1].X);
                break;
            case BarSection.End:
                Assert.True(room.X > edit.SectionPanels()[1].Right);
                break;
            default:
                Assert.Equal(edit.Length / 2f, room.X + (room.Width / 2f), 1f);
                break;
        }
    }

    /// <summary>
    /// The strip a reserving dock holds is one, along the whole edge, a dock's thickness and its lift
    /// deep, whichever sections hold something — the tallest dock's, as all three are one thickness.
    /// </summary>
    [Theory]
    [InlineData(96u, 48)]
    [InlineData(144u, 48)]
    [InlineData(96u, 64)]
    public void TheStripHeldIsADocksThicknessAndItsLift(uint dpi, int size)
    {
        var all = Dock(Three());
        all.Size = size;
        var one = Dock([.. Three().Where(island => island.Section == BarSection.Middle)]);
        one.Size = size;

        var three = Laid(all, Edge.Bottom, dpi);
        var middle = Laid(one, Edge.Bottom, dpi);

        Assert.Equal(three.Thickness + Px(dpi, 8), three.Reserve);
        Assert.Equal(middle.Reserve, three.Reserve);
        Assert.Equal(Layout.ReserveFor(dpi, new Config().WithEdge(all.Edges(Edge.Bottom).Bottom)), three.Reserve);
        Assert.Equal(three.Length, three.Width);
    }

    /// <summary>
    /// On hover each dock comes in by itself: the dock's edge has three places, one per section that
    /// holds something, and each place lays out only its own dock.
    /// </summary>
    [Fact]
    public void OnHoverEachDockIsAPlaceOfItsOwn()
    {
        var layout = Dock(Three());
        layout.Show = EdgeMode.Hover;
        var edges = layout.Edges(Edge.Bottom);
        bool Filled(Edge edge, BarSection section) => TheDock.Shows(layout, Edge.Bottom, new EdgePlace(edge, section), null).Count > 0;

        var places = HoverPlaces.For(new RectI(0, 0, 1920, 1080), edges.Standing, Filled, [])
            .Where(place => edges.Has(place.Edge, place.Section) && HoverPlaces.Wanted(place, Filled(place.Edge, place.Section), banded: HoverPlaces.Banded(edges.Bottom.Style, place.Edge, Filled)))
            .ToList();

        Assert.Equal([BarSection.Start, BarSection.Middle, BarSection.End], places.Select(place => place.Section));
        Assert.All(places, place => Assert.Equal(Edge.Bottom, place.Edge));
        Assert.Equal(["Start"], TheDock.Shows(layout, Edge.Bottom, new EdgePlace(Edge.Bottom, BarSection.Start), null).Select(island => island.Kind.ToString()));
    }

    /// <summary>
    /// Taskbar to Dock and back moves nothing and hides nothing: the same widgets show on the main bar
    /// in both, in the same slots; only the look changes, islands to floating docks.
    /// </summary>
    [Fact]
    public void TaskbarToDockAndBackKeepsEverything()
    {
        var layout = new TaskbarLayout { Type = LayoutType.Taskbar, Islands = Sections.DefaultLayout(pins: []) };
        Sections.Order(layout.Islands);
        string Said() => string.Join(" ", layout.Islands.Select(island => $"{island.Id}:{island.Section}"));
        var before = Said();
        var onTaskbar = TheDock.Shows(layout, Edge.Bottom, null, Edge.Bottom).Select(island => island.Id).ToList();

        Assert.True(layout.Become(LayoutType.Dock));
        Assert.Equal(onTaskbar, TheDock.Shows(layout, Edge.Bottom, null, Edge.Bottom).Select(island => island.Id));
        Assert.Equal(EdgeLayout.Dock, layout.Edges(Edge.Bottom).Bottom.Layout);
        Assert.Equal(before, Said());

        Assert.True(layout.Become(LayoutType.Taskbar));
        Assert.Equal(onTaskbar, TheDock.Shows(layout, Edge.Bottom, null, Edge.Bottom).Select(island => island.Id));
        Assert.Equal(before, Said());
    }
}
