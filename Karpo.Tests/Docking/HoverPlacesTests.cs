// Where a display's hover places lie (Karpo.Dock\HoverPlaces.cs): the thirds of each edge, each
// along the whole edge, none on an edge shown all the time, the 1 px strips, and the inner edges that
// touch another display. Pure geometry: no window is made.

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Docking;

public sealed class HoverPlacesTests
{
    // A 1920x1080 display.
    private static readonly RectI Display = new(0, 0, 1920, 1080);

    private static Func<Edge, BarSection, bool> Filled(params (Edge, BarSection)[] places) =>
        (edge, section) => places.Contains((edge, section));

    private static readonly Func<Edge, BarSection, bool> Nothing = (_, _) => false;
    private static readonly Func<Edge, BarSection, bool> Everything = (_, _) => true;

    private static HoverPlace Place(IReadOnlyList<HoverPlace> places, Edge edge, BarSection section) =>
        places.Single(p => p.Edge == edge && p.Section == section);

    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void TheEdgeShownAllTheTimeHasNoHoverPlaces(Edge standing)
    {
        var places = HoverPlaces.For(Display, [standing], Everything, []);

        Assert.Equal(9, places.Count);
        Assert.DoesNotContain(places, p => p.Edge == standing);
    }

    /// <summary>A display with no bar shown all the time has three on each edge, in the edges' order; which the layout has is the dock's to pick (ScreenEdges.Has).</summary>
    [Fact]
    public void ADisplayWithNoBarShownAllTheTimeHasTwelve()
    {
        var places = HoverPlaces.For(Display, [], Everything, []);

        Assert.Equal(12, places.Count);
        Assert.Equal(12, places.Select(p => (p.Edge, p.Section)).Distinct().Count());
        Assert.Equal(ScreenEdges.All, places.Select(p => p.Edge).Distinct());
    }

    /// <summary>A side's places run down it: its thirds of the display's height, their strips the column of pixels along it, their areas the whole display across.</summary>
    [Theory]
    [InlineData(Edge.Left, 0)]
    [InlineData(Edge.Right, 1919)]
    public void ASidesPlacesRunDownIt(Edge side, int column)
    {
        var places = HoverPlaces.For(Display, [], Everything, []);

        Assert.Equal(new EdgeRun(0, 360), Place(places, side, BarSection.Start).Run);
        Assert.Equal(new EdgeRun(360, 720), Place(places, side, BarSection.Middle).Run);
        Assert.Equal(new EdgeRun(720, 1080), Place(places, side, BarSection.End).Run);
        Assert.Equal(new RectI(column, 360, column + 1, 720), Place(places, side, BarSection.Middle).Strip);
        Assert.Equal(new RectI(0, 720, 1920, 1080), HoverPlaces.Area(Display, Place(places, side, BarSection.End)));
    }

    [Fact]
    public void TheThirdsMeetAndCoverTheWholeRun()
    {
        foreach (var length in new[] { 1920, 1080, 1000, 1001, 1002, 2, 0 })
        {
            var run = new EdgeRun(100, 100 + length);
            var start = HoverPlaces.Third(run, BarSection.Start);
            var middle = HoverPlaces.Third(run, BarSection.Middle);
            var end = HoverPlaces.Third(run, BarSection.End);

            Assert.Equal(run.From, start.From);
            Assert.Equal(start.To, middle.From);
            Assert.Equal(middle.To, end.From);
            Assert.Equal(run.To, end.To);
            // No third is more than a pixel longer than another.
            var lengths = new[] { start.Length, middle.Length, end.Length };
            Assert.True(lengths.Max() - lengths.Min() <= 1, $"{length}: {string.Join(",", lengths)}");
        }
    }

    /// <summary>
    /// Both edges' places are the thirds of the whole edge: a bar shown all the time along the other
    /// edge never shortens them, as nothing stands across their ends.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    public void ThePlacesAreTheThirdsOfTheWholeEdge(Edge? standing)
    {
        var places = HoverPlaces.For(Display, standing is { } edge ? [edge] : [], Nothing, []);

        foreach (var along in ScreenEdges.All.Where(other => other != standing && !other.IsVertical()))
        {
            Assert.Equal(new EdgeRun(0, 640), Place(places, along, BarSection.Start).Run);
            Assert.Equal(new EdgeRun(640, 1280), Place(places, along, BarSection.Middle).Run);
            Assert.Equal(new EdgeRun(1280, 1920), Place(places, along, BarSection.End).Run);
        }
    }

    [Fact]
    public void ADisplayAwayFromTheOriginCountsInScreenPixels()
    {
        var display = new RectI(-1920, 200, 0, 1280);
        var places = HoverPlaces.For(display, [], Filled((Edge.Top, BarSection.Middle)), []);

        var top = Place(places, Edge.Top, BarSection.Middle);
        Assert.Equal(new EdgeRun(-1280, -640), top.Run);
        Assert.Equal(new RectI(-1280, 200, -640, 201), top.Strip);
        Assert.Equal(new EdgeRun(-1920, -1280), Place(places, Edge.Bottom, BarSection.Start).Run);
    }

    [Fact]
    public void OnlyAFilledPlaceHasAStrip()
    {
        var places = HoverPlaces.For(Display, [Edge.Bottom], Filled((Edge.Top, BarSection.Middle)), []);

        Assert.Equal(new RectI(640, 0, 1280, 1), Place(places, Edge.Top, BarSection.Middle).Strip);
        Assert.All(places.Where(p => p != Place(places, Edge.Top, BarSection.Middle)),
            p => Assert.Equal(default, p.Strip));
    }

    /// <summary>Each strip is one pixel inside the display along its edge, the corner places' running into the corners.</summary>
    [Fact]
    public void EveryStripIsOnePixelOnItsEdgeInsideTheDisplay()
    {
        var places = HoverPlaces.For(Display, [], Everything, []);

        Assert.Equal(new RectI(0, 1079, 640, 1080), Place(places, Edge.Bottom, BarSection.Start).Strip);
        Assert.Equal(new RectI(640, 1079, 1280, 1080), Place(places, Edge.Bottom, BarSection.Middle).Strip);
        Assert.Equal(new RectI(1280, 1079, 1920, 1080), Place(places, Edge.Bottom, BarSection.End).Strip);
        Assert.Equal(new RectI(0, 0, 640, 1), Place(places, Edge.Top, BarSection.Start).Strip);
        Assert.Equal(new RectI(640, 0, 1280, 1), Place(places, Edge.Top, BarSection.Middle).Strip);
        Assert.Equal(new RectI(1280, 0, 1920, 1), Place(places, Edge.Top, BarSection.End).Strip);
    }

    /// <summary>A place's bar is laid out along its stretch, the whole display across it.</summary>
    [Fact]
    public void APlacesAreaIsItsStretchTheWholeDisplayAcross()
    {
        var places = HoverPlaces.For(Display, [], Everything, []);

        Assert.Equal(new RectI(640, 0, 1280, 1080), HoverPlaces.Area(Display, Place(places, Edge.Top, BarSection.Middle)));
        Assert.Equal(new RectI(0, 0, 640, 1080), HoverPlaces.Area(Display, Place(places, Edge.Bottom, BarSection.Start)));
    }

    [Fact]
    public void AnInnerEdgeIsUnavailable()
    {
        // A second display to the right, a third above.
        RectI[] others = [new(1920, 0, 3840, 1080), new(0, -1080, 1920, 0)];
        var places = HoverPlaces.For(Display, [Edge.Bottom], Everything, others);

        Assert.All(places.Where(p => p.Edge == Edge.Top), p =>
        {
            Assert.False(p.Available);
            Assert.Equal(1, p.SharedWith);
            Assert.Equal(default, p.Strip);
        });
        // An unavailable place still has its third, for edit mode to show.
        Assert.Equal(new EdgeRun(0, 640), Place(places, Edge.Top, BarSection.Start).Run);
    }

    /// <summary>A display beside this one touches no edge a place stands on: only one above or below does.</summary>
    /// <summary>
    /// Displays beside it leave the top and the bottom available, and make the sides they touch inner
    /// edges: the pointer crosses there to the other display, so no place waits on them.
    /// </summary>
    [Fact]
    public void ADisplayBesideItMakesThatSideInner()
    {
        RectI[] others = [new(1920, 0, 3840, 1080), new(-1920, 0, 0, 1080)];
        var places = HoverPlaces.For(Display, [], Everything, others);

        Assert.All(places.Where(p => !p.Edge.IsVertical()), p =>
        {
            Assert.True(p.Available);
            Assert.Null(p.SharedWith);
            Assert.NotEqual(default, p.Strip);
        });
        Assert.All(places.Where(p => p.Edge == Edge.Right), p => Assert.Equal((false, (int?)0, default(RectI)), (p.Available, p.SharedWith, p.Strip)));
        Assert.All(places.Where(p => p.Edge == Edge.Left), p => Assert.Equal((false, (int?)1, default(RectI)), (p.Available, p.SharedWith, p.Strip)));
    }

    [Fact]
    public void ADisplayTouchingPartOfAnEdgeMakesItInner()
    {
        RectI[] others = [new(900, 1080, 2820, 2160)];
        Assert.Equal(0, HoverPlaces.Touching(Display, Edge.Bottom, others));
        Assert.Null(HoverPlaces.Touching(Display, Edge.Top, others));
    }

    [Fact]
    public void ADisplayMeetingOnlyAtACornerOrAwayDoesNotCount()
    {
        RectI[] others = [new(1920, 1080, 3840, 2160), new(2000, 0, 3920, 1080), new(-1920, -1080, 0, 0)];
        foreach (var edge in ScreenEdges.All)
            Assert.Null(HoverPlaces.Touching(Display, edge, others));
    }

    [Fact]
    public void TheEdgeShownAllTheTimeIsLeftOutEvenWhenInner()
    {
        RectI[] others = [new(0, 1080, 1920, 2160)];
        var places = HoverPlaces.For(Display, [Edge.Bottom], Everything, others);

        Assert.DoesNotContain(places, p => p.Edge == Edge.Bottom);
        Assert.All(places, p => Assert.True(p.Available));
    }
}
