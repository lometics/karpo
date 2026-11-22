// The window of a hover place (Karpo.Dock\HoverPlaces.cs, Dock.DockWindow): which places have one,
// the area its bar is laid out in, the floating look it always wears, the material that is never
// clear, and the strip a bar keeps (Layout.ReserveFor), which the places either side stop at.
// Pure: no window is made.

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Docking;

public sealed class HoverWindowsTests
{
    private static readonly RectI Display = new(0, 0, 1920, 1080);

    private static HoverPlace Place(Edge? fixedEdge, Edge edge, BarSection section) =>
        HoverPlaces.For(Display, fixedEdge is { } standing ? [standing] : [], (_, _) => true, []).Single(p => p.Edge == edge && p.Section == section);

    [Fact]
    public void OnlyAFilledPlaceThatCanComeInHasAWindow()
    {
        var place = Place(Edge.Bottom, Edge.Top, BarSection.Middle);
        Assert.True(HoverPlaces.Wanted(place, filled: true));
        Assert.False(HoverPlaces.Wanted(place, filled: false));
        // An inner edge, touching another display, has none, filled or not.
        var inner = HoverPlaces.For(Display, [Edge.Bottom], (_, _) => true, [new RectI(0, -1080, 1920, 0)])
            .Single(p => p.Edge == Edge.Top && p.Section == BarSection.Start);
        Assert.False(HoverPlaces.Wanted(inner, filled: true));
    }

    /// <summary>
    /// A hover edge in a look with a band comes in as one bar: an empty place on it has a window too,
    /// to carry the band over its third, where the edge holds anything at all (the person,
    /// 2026-10-07: "only one section is set the background for the other sections is unset").
    /// </summary>
    [Fact]
    public void AnEmptyPlaceOnABandedEdgeHasAWindowForTheBand()
    {
        bool MiddleOnly(Edge edge, BarSection section) => edge == Edge.Top && section == BarSection.Middle;
        Assert.True(HoverPlaces.Banded(BarStyle.Islands, Edge.Top, MiddleOnly));
        // Floating: each section a panel of its own, an empty one nothing.
        Assert.False(HoverPlaces.Banded(BarStyle.Floating, Edge.Top, MiddleOnly));
        // An edge with nothing on it waits for nothing.
        Assert.False(HoverPlaces.Banded(BarStyle.Islands, Edge.Left, MiddleOnly));

        var start = Place(Edge.Bottom, Edge.Top, BarSection.Start);
        Assert.True(HoverPlaces.Wanted(start, filled: false, banded: true));
        Assert.False(HoverPlaces.Wanted(start, filled: false, banded: false));
        // Its strip stays empty: the pointer brings the edge in where something is.
        var places = HoverPlaces.For(Display, [Edge.Bottom], MiddleOnly, []);
        Assert.Equal(default, places.Single(p => p.Edge == Edge.Top && p.Section == BarSection.Start).Strip);
        // Not on an inner edge, banded or not.
        var inner = HoverPlaces.For(Display, [Edge.Bottom], (_, _) => true, [new RectI(0, -1080, 1920, 0)])
            .Single(p => p.Edge == Edge.Top && p.Section == BarSection.Start);
        Assert.False(HoverPlaces.Wanted(inner, filled: false, banded: true));
    }

    [Fact]
    public void APlaceAlongTheTopStandsInItsThirdTheWholeDisplayDown()
    {
        var place = Place(Edge.Bottom, Edge.Top, BarSection.Middle);
        Assert.Equal(new RectI(640, 0, 1280, 1080), HoverPlaces.Area(Display, place));
    }

    [Fact]
    public void OnADisplayWithNoBarTheHomeEdgeIsThreePlacesEndToEnd()
    {
        var thirds = new[] { BarSection.Start, BarSection.Middle, BarSection.End }
            .Select(section => HoverPlaces.Area(Display, Place(null, Edge.Bottom, section)))
            .ToList();
        Assert.Equal(0, thirds[0].Left);
        Assert.Equal(thirds[0].Right, thirds[1].Left);
        Assert.Equal(thirds[1].Right, thirds[2].Left);
        Assert.Equal(1920, thirds[2].Right);
        Assert.All(thirds, area => Assert.Equal((0, 1080), (area.Top, area.Bottom)));
    }

    /// <summary>
    /// A place wears its edge's look and thickness, as a bar shown all the time does: they are no
    /// longer floating whatever the edge said (the person, 2026-10-07: "all modes follow the chosen
    /// style").
    /// </summary>
    [Theory]
    [InlineData(EdgeLayout.Taskbar, BarStyle.Islands)]
    [InlineData(EdgeLayout.Dock, BarStyle.Floating)]
    public void APlaceWearsItsEdgesLook(EdgeLayout bar, BarStyle style)
    {
        var config = new Config { Screens = [new Screen { Id = "D1" }] };
        var edge = new EdgeBar { Mode = EdgeMode.Hover, Size = 56, Layout = bar };
        var dressed = config.WithEdge(edge);
        Assert.Equal(style, dressed.StyleOrDefault);
        Assert.Equal(56, dressed.Height);
        // A copy for laying out, never the person's own, sharing its lists.
        Assert.Null(config.Style);
        Assert.Equal(Config.DefaultHeight, config.Height);
        Assert.Same(config.Screens, dressed.Screens);
    }

    /// <summary>§547: a place, or a bar over the windows, is never clear: a clear one shows the wallpaper Karpo blurs, Windows' effects or none.</summary>
    [Theory]
    [InlineData(Material.Clear, true, Material.Translucent)]
    [InlineData(Material.Clear, false, Material.Translucent)]
    [InlineData(Material.Translucent, false, Material.Translucent)]
    [InlineData(Material.Opaque, false, Material.Opaque)]
    public void APlaceIsNeverClear(Material material, bool glass, Material worn)
    {
        var look = new Look(Glass: glass, Dark: true, Accent: default, Material: material);
        Assert.Equal(worn, HoverPlaces.Dressed(look).Material);
    }

    [Theory]
    [InlineData(96u, BarStyle.Islands, 48, 48f)]
    [InlineData(144u, BarStyle.Islands, 48, 72f)]
    // §688: a dock keeps its size too, its panel the room it floats over thinner.
    [InlineData(96u, BarStyle.Floating, 48, 48f)]
    [InlineData(144u, BarStyle.Floating, 48, 72f)]
    [InlineData(96u, BarStyle.Floating, 42, 42f)]
    public void ABarKeepsItsThicknessAndTheRoomItFloatsOver(uint dpi, BarStyle style, int height, float reserve)
    {
        var config = new Config { Style = style, Height = height };
        Assert.Equal(reserve, Layout.ReserveFor(dpi, config));
    }
}
