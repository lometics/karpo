// keys: the one way the keyboard moves between stops (FocusNav.cs, docs\widgets.md "One keyboard"): round
// and held steps, the nearest stop that way, reading order, a line's pages and where its stops show.

using Karpo.Dock;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Keys;

public sealed class FocusNavTests
{
    private static RectF R(float x, float y, float w, float h) => new(x, y, w, h);

    /// <summary>Tab goes round, from nowhere to the first (back: the last); a held step stops at the ends; nothing to walk is nowhere.</summary>
    [Fact]
    public void StepsGoRoundOrHoldAtTheEnds()
    {
        Assert.Equal(1, FocusNav.Step(4, 0, 1));
        Assert.Equal(0, FocusNav.Step(4, 3, 1));
        Assert.Equal(3, FocusNav.Step(4, 0, -1));
        Assert.Equal(3, FocusNav.Step(4, 3, 1, wrap: false));
        Assert.Equal(0, FocusNav.Step(4, 0, -1, wrap: false));
        Assert.Equal(0, FocusNav.Step(4, -1, 1));
        Assert.Equal(3, FocusNav.Step(4, -1, -1));
        Assert.Equal(-1, FocusNav.Step(0, -1, 1));
    }

    /// <summary>The arrows' ways; any other key is none.</summary>
    [Fact]
    public void TheArrowsHaveTheirWays()
    {
        Assert.Equal(FocusWay.Left, FocusNav.WayOf(VK.VK_LEFT));
        Assert.Equal(FocusWay.Down, FocusNav.WayOf(VK.VK_DOWN));
        Assert.Null(FocusNav.WayOf(VK.VK_RETURN));
    }

    /// <summary>
    /// A list's items, each the line's whole width, a button on the trailing end of each: Down from an item
    /// is the next item, never its own button nor the next one's; Right from an item is its own button; Down
    /// from a button the button under it; Left from a button its own item; nothing past the ends.
    /// </summary>
    [Fact]
    public void AListOfItemsWithAButtonEachIsWalkedAsItStands()
    {
        // item 0, its button 1, item 2, its button 3.
        List<RectF> stops = [R(0, 0, 300, 40), R(260, 8, 32, 24), R(0, 40, 300, 40), R(260, 48, 32, 24)];

        Assert.Equal(2, FocusNav.Toward(stops, 0, FocusWay.Down));
        Assert.Equal(1, FocusNav.Toward(stops, 0, FocusWay.Right));
        Assert.Equal(3, FocusNav.Toward(stops, 1, FocusWay.Down));
        Assert.Equal(0, FocusNav.Toward(stops, 1, FocusWay.Left));
        Assert.Equal(0, FocusNav.Toward(stops, 2, FocusWay.Up));
        Assert.Equal(-1, FocusNav.Toward(stops, 2, FocusWay.Down));
        Assert.Equal(-1, FocusNav.Toward(stops, 0, FocusWay.Left));
        Assert.Equal(0, FocusNav.Toward(stops, -1, FocusWay.Down));
        Assert.Equal(-1, FocusNav.Toward([], 0, FocusWay.Down));
    }

    /// <summary>A grid (a month's days): the arrows keep the column and the row, never jump diagonally.</summary>
    [Fact]
    public void AGridKeepsItsColumnAndRow()
    {
        var days = new List<RectF>();
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 7; column++)
            {
                days.Add(R(column * 40, row * 40, 40, 40));
            }
        }

        Assert.Equal(10, FocusNav.Toward(days, 3, FocusWay.Down));
        Assert.Equal(4, FocusNav.Toward(days, 3, FocusWay.Right));
        Assert.Equal(9, FocusNav.Toward(days, 10, FocusWay.Left));
        Assert.Equal(3, FocusNav.Toward(days, 10, FocusWay.Up));
        Assert.Equal(-1, FocusNav.Toward(days, 6, FocusWay.Right));
    }

    /// <summary>Reading order: by rows top to bottom, a row's stops left to right, a row's tops within the slack.</summary>
    [Fact]
    public void ReadingOrderIsByRowsThenAcross()
    {
        List<RectF> stops = [R(260, 41, 32, 24), R(0, 40, 300, 40), R(100, 0, 20, 20), R(0, 1, 20, 20)];

        Assert.Equal([3, 2, 1, 0], FocusNav.ReadingOrder(stops, row: 2));
    }

    /// <summary>A page down from a stop is the first a window further on, the last at the most; up the same way back.</summary>
    [Fact]
    public void APageIsAWindowOfStops()
    {
        var parts = Enumerable.Range(0, 10).Select(index => new FocusPart(index + 1, new D2D_RECT_F(0, index * 40, 300, (index + 1) * 40))).ToList();

        Assert.Equal(3, LineFocus.Page(parts, 0, 120, down: true));
        Assert.Equal(9, LineFocus.Page(parts, 8, 120, down: true));
        Assert.Equal(2, LineFocus.Page(parts, 5, 120, down: false));
        Assert.Equal(0, LineFocus.Page(parts, 1, 120, down: false));
        Assert.Equal(9, LineFocus.Page(parts, -1, 120, down: true));
        Assert.Equal(-1, LineFocus.Page([], 0, 120, down: true));
    }
}
