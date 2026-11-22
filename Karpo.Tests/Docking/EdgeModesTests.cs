// A layout's bar shows as its layout says (the person, 2026-10-08: "show style per bar"): all the time
// holding its strip, all the time over the windows, or only when the pointer comes near. What the dock
// makes of that, apart from any window: the edge a layout's bar stands on (TaskbarLayout.Edges), whether
// it is a bar of its own or the display's anchor (Dock.StandingEdges), what strip it holds
// (AppbarSpace), where the hover places lie (HoverPlaces.For), and that the bar wears its type's look
// and its layout's thickness (Config.WithEdge). Pure: no window is made.

using Karpo.Core;
using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Docking;

public sealed class EdgeModesTests
{
    private static readonly RectI Display = new(0, 0, 1920, 1080);

    private static ScreenEdges Of(LayoutType type, Edge edge = Edge.Bottom, EdgeMode show = EdgeMode.Reserve) =>
        new TaskbarLayout { Type = type, Show = show }.Edges(edge);

    // --- Which edges are bars -----------------------------------------------------------------

    /// <summary>
    /// A layout's bar is on its display's edge, and nowhere else. A dock has all three sections of its
    /// edge, each a dock of its own.
    /// </summary>
    [Fact]
    public void ALayoutHasBarsOnTheEdgesOfItsType()
    {
        Assert.Equal([Edge.Top], Of(LayoutType.Taskbar, Edge.Top).Shown);
        Assert.Equal([Edge.Bottom], Of(LayoutType.Dock, Edge.Bottom).Shown);

        var dock = Of(LayoutType.Dock, Edge.Bottom);
        Assert.All(new[] { BarSection.Start, BarSection.Middle, BarSection.End }, section => Assert.True(dock.Has(Edge.Bottom, section)));
        Assert.False(dock.Has(Edge.Top, BarSection.Middle));
        var taskbar = Of(LayoutType.Taskbar, Edge.Top);
        Assert.All(new[] { BarSection.Start, BarSection.Middle, BarSection.End }, section => Assert.True(taskbar.Has(Edge.Top, section)));
        Assert.False(taskbar.Has(Edge.Bottom, BarSection.Start));
    }

    /// <summary>
    /// An edge is a place only where the layout has a bar on it: the sides of a bar along the top or
    /// the bottom are none (a side once read as the bottom, and edit mode made places down both sides
    /// of a bottom bar), the top and the bottom of a bar down a side none either, and an edge this
    /// build does not know never.
    /// </summary>
    [Theory]
    [InlineData(LayoutType.Taskbar, Edge.Bottom)]
    [InlineData(LayoutType.Taskbar, Edge.Top)]
    [InlineData(LayoutType.Dock, Edge.Bottom)]
    [InlineData(LayoutType.Taskbar, Edge.Left)]
    [InlineData(LayoutType.Dock, Edge.Right)]
    public void OnlyAnEdgeWithABarIsAPlace(LayoutType type, Edge edge)
    {
        var edges = Of(type, edge);
        foreach (var other in ScreenEdges.All.Append((Edge)99))
        {
            var has = other == edge;
            Assert.All(new[] { BarSection.Start, BarSection.Middle, BarSection.End }, section => Assert.Equal(has, edges.Has(other, section)));
        }
    }

    /// <summary>
    /// A bar shown all the time is a bar window of its own, the display's main bar; a display whose bar
    /// comes in on hover has one window all the same, its anchor.
    /// </summary>
    [Fact]
    public void ABarShownAllTheTimeIsABar()
    {
        var reserving = Of(LayoutType.Taskbar, Edge.Top);
        Assert.Equal([Edge.Top], TheDock.StandingEdges(reserving));
        Assert.Equal(Edge.Top, reserving.Main);

        var floating = Of(LayoutType.Taskbar, Edge.Bottom, EdgeMode.Float);
        Assert.Equal([Edge.Bottom], TheDock.StandingEdges(floating));
        Assert.Equal(Edge.Bottom, floating.Main);

        var hovering = Of(LayoutType.Dock, Edge.Top, EdgeMode.Hover);
        Assert.Equal([(Edge?)null], TheDock.StandingEdges(hovering));
        Assert.Null(hovering.Main);
        Assert.True(hovering.Top.Hovers);
    }

    /// <summary>Only a bar that reserves holds its strip; over the windows, and on hover, it holds none.</summary>
    [Theory]
    [InlineData(EdgeMode.Reserve, 72)]
    [InlineData(EdgeMode.Float, 0)]
    [InlineData(EdgeMode.Hover, 0)]
    public void OnlyAReservingBarHoldsItsStrip(EdgeMode mode, int held) =>
        Assert.Equal(held, AppbarSpace.Thickness(mode, 72f));

    // --- Where the hover places lie ------------------------------------------------------------

    /// <summary>
    /// A bar along the top that comes in on hover has its three places along the top, each along the
    /// whole edge, and none on the bottom or down a side.
    /// </summary>
    [Theory]
    [InlineData(LayoutType.Taskbar)]
    [InlineData(LayoutType.Dock)]
    public void ATopOnHoverHasItsPlacesAlongTheWholeTop(LayoutType type)
    {
        var edges = Of(type, Edge.Top, EdgeMode.Hover);

        // The places the layout has: none down a side it has no bar on (Dock.Arrange).
        var places = HoverPlaces.For(Display, edges.Standing, (_, _) => true, []).Where(place => edges.Has(place.Edge, place.Section)).ToList();

        Assert.Equal(3, places.Count);
        Assert.All(places, place => Assert.Equal(Edge.Top, place.Edge));
        Assert.Equal(0, places.Min(place => place.Run.From));
        Assert.Equal(1920, places.Max(place => place.Run.To));
        Assert.All(places, place => Assert.Equal(0, place.Strip.Top));
    }

    // --- Every bar wears its type's look ---------------------------------------------------------

    /// <summary>
    /// Each bar has its layout's thickness and its type's look, in every way it shows, on hover too: a
    /// dock floats, a bar wears islands. The person's config is never changed.
    /// </summary>
    [Theory]
    [InlineData(LayoutType.Taskbar, EdgeMode.Reserve, BarStyle.Islands, 40)]
    [InlineData(LayoutType.Taskbar, EdgeMode.Float, BarStyle.Islands, 56)]
    [InlineData(LayoutType.Taskbar, EdgeMode.Hover, BarStyle.Islands, 64)]
    [InlineData(LayoutType.Dock, EdgeMode.Hover, BarStyle.Floating, 32)]
    [InlineData(LayoutType.Dock, EdgeMode.Reserve, BarStyle.Floating, 48)]
    public void EveryBarWearsItsTypesLookAndItsLayoutsThickness(LayoutType type, EdgeMode mode, BarStyle style, int size)
    {
        var config = new Config();
        var layout = new TaskbarLayout { Type = type, Show = mode, Size = size };
        var edge = layout.Edges(Edge.Bottom)[Edge.Bottom];

        var laidOut = config.WithEdge(edge);

        Assert.Equal(mode, edge.Mode);
        Assert.Equal(style, laidOut.StyleOrDefault);
        Assert.Equal(size, laidOut.Height);
        Assert.Null(config.Style);
        Assert.Equal(Config.DefaultHeight, config.Height);
    }
}
