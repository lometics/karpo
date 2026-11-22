// The places along one edge come and go together (Karpo.Dock\EdgeReveal.cs): the pointer at any
// strip brings in the whole edge, any of them held keeps them all, and they go once none is.

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Docking;

public sealed class EdgeRevealTests
{
    private sealed class Fake(string display, EdgePlace? place) : IEdgeWindow
    {
        public string Display { get; } = display;
        public EdgePlace? Place { get; } = place;
        public bool Shown { get; set; }
        public bool Hovering { get; set; }
        public bool Flyout { get; set; }
        public bool Holds => Hovering || Flyout;
        public void Show() => Shown = true;
        public void Hide() => Shown = false;
    }

    private static Fake At(Edge edge, BarSection section, string display = "A") => new(display, new EdgePlace(edge, section));

    [Fact]
    public void A_strip_brings_in_the_whole_edge_and_nothing_else()
    {
        var start = At(Edge.Left, BarSection.Start);
        var middle = At(Edge.Left, BarSection.Middle);
        var end = At(Edge.Left, BarSection.End);
        var top = At(Edge.Top, BarSection.Middle);
        var otherDisplay = At(Edge.Left, BarSection.Middle, "B");
        var bar = new Fake("A", null);
        List<Fake> all = [bar, start, middle, end, top, otherDisplay];

        EdgeReveal.Reveal(middle, all);

        Assert.True(start.Shown && middle.Shown && end.Shown);
        Assert.False(top.Shown);
        Assert.False(otherDisplay.Shown);
        Assert.False(bar.Shown);
    }

    [Fact]
    public void Leaving_one_while_over_another_keeps_them_all()
    {
        var start = At(Edge.Bottom, BarSection.Start);
        var end = At(Edge.Bottom, BarSection.End);
        List<Fake> all = [start, end];
        EdgeReveal.Reveal(start, all);

        // The pointer went from the start over to the end, and the start's time came.
        end.Hovering = true;
        Assert.False(EdgeReveal.Settle(start, all));
        Assert.True(start.Shown && end.Shown);

        // Then it left the end too: both go.
        end.Hovering = false;
        Assert.True(EdgeReveal.Settle(end, all));
        Assert.False(start.Shown || end.Shown);
    }

    [Fact]
    public void A_flyout_of_one_holds_the_edge()
    {
        var start = At(Edge.Right, BarSection.Start);
        var middle = At(Edge.Right, BarSection.Middle);
        List<Fake> all = [start, middle];
        EdgeReveal.Reveal(middle, all);
        start.Flyout = true;

        Assert.False(EdgeReveal.Settle(middle, all));
        Assert.True(start.Shown && middle.Shown);

        start.Flyout = false;
        Assert.True(EdgeReveal.Settle(start, all));
        Assert.False(start.Shown || middle.Shown);
    }

    [Fact]
    public void A_bar_goes_alone()
    {
        var bar = new Fake("A", null) { Shown = true };
        var other = new Fake("A", null) { Shown = true };
        var place = At(Edge.Top, BarSection.Start);
        place.Shown = true;
        List<Fake> all = [bar, other, place];

        Assert.Empty(EdgeReveal.Siblings(bar, all));
        Assert.True(EdgeReveal.Settle(bar, all));
        Assert.False(bar.Shown);
        Assert.True(other.Shown && place.Shown);
    }
}
