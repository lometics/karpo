// Where a popup opens from: the widget's whole block for a flyout that belongs to the widget, the
// item's own rectangle for a popup that belongs to one item.
//
// The bug this was written for: "the now playing flyover is not properly centered above the
// widget". DockWindow.Anchor resolved through Layout.ItemRect, which is the *cell* that was
// pressed. Every one-cell widget has the same rectangle either way, so nothing showed it until the
// now-playing widget — seven cells — opened its panel under the artwork when the artwork was
// clicked and under the next-track button when that was.
//
// The two halves are tested together on purpose: a rule that says "always use the block" would put
// a pinned app's jump list in the middle of the whole run of pins, and a rule that says "always use
// the cell" is the bug. The pins' tiles and the tray's icons are cells of one part, which is what
// makes the difference visible here.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Menus;

public sealed class FlyoutAnchorTests
{
    private static RECT Area(int width, int height) => new() { left = 0, top = 0, right = width, bottom = height };

    private static PartSpec Spec(string id, BarKind kind) => new(Config.IslandKey(null, id), kind);

    /// <summary>A bar with a seven-cell widget on it, as the now-playing widget is.</summary>
    private static (Layout Layout, int Part) WithWideWidget(Edge edge = Edge.Bottom, int width = 1920)
    {
        var specs = new List<PartSpec>
        {
            Spec("s", BarKind.Start),
            Spec("m", BarKind.FolderList),
            Spec("c", BarKind.NightLight),
        };
        var layout = Layout.ForTests(96, Area(width, 1080), edge, new Config(), specs, 40f, cells: [1, 7, 1]);
        return (layout, 1);
    }

    /// <summary>
    /// The point every cell of one widget opens its flyout from is the same point, and it is the
    /// middle of the widget: pressing the first cell and pressing the last put the panel in one
    /// place.
    /// </summary>
    [Fact]
    public void AWidgetsFlyoutOpensUnderTheMiddleOfTheWholeWidget()
    {
        var (layout, part) = WithWideWidget();
        var cells = layout.PartAt(part).Count;
        Assert.Equal(7, cells);

        var block = layout.BlockRect(part);
        for (var cell = 0; cell < cells; cell++)
        {
            Assert.Equal(block, layout.FlyoutRect(Hit.Cell(part, cell)));
        }
    }

    /// <summary>
    /// And the cells really do differ, so the rule above is not true by accident: the first cell's
    /// middle and the last cell's middle are a widget's width apart, which is exactly how far the
    /// panel used to move between two presses on one widget.
    /// </summary>
    [Fact]
    public void TheCellsOfOneWidgetAreNotTheSamePoint()
    {
        var (layout, part) = WithWideWidget();

        var first = layout.ItemRect(Hit.Cell(part, 0));
        var last = layout.ItemRect(Hit.Cell(part, 6));

        Assert.False(first.IsEmpty);
        Assert.False(last.IsEmpty);
        Assert.NotEqual(Middle(layout, first), Middle(layout, last));
        // The block's middle is between the two, which is what "centred above the widget" means.
        var middle = Middle(layout, layout.BlockRect(part));
        Assert.InRange(middle, Middle(layout, first), Middle(layout, last));
    }

    /// <summary>Every edge, since the anchor is taken on the bar's inner side: the rule is the same on all four.</summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void TheBlockRuleHoldsOnEveryEdge(Edge edge)
    {
        var (layout, part) = WithWideWidget(edge);

        Assert.Equal(layout.BlockRect(part), layout.FlyoutRect(Hit.Cell(part, 0)));
        Assert.Equal(layout.BlockRect(part), layout.FlyoutRect(Hit.Cell(part, layout.PartAt(part).Count - 1)));
    }

    /// <summary>
    /// A popup about one item keeps that item's rectangle: the pins are cells of one part, so the
    /// block there is the middle of the whole run, which is not the tile anybody is pointing at.
    /// This is why the fix is two rules and not one.
    /// </summary>
    [Fact]
    public void APinnedTilesPopupStaysOnItsOwnTile()
    {
        var specs = new List<PartSpec> { Spec("s", BarKind.Start), Spec("apps", BarKind.Apps) };
        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Bottom, new Config(), specs, 40f, tiles: 4);

        var first = layout.ItemRect(Hit.Tile(0));
        var third = layout.ItemRect(Hit.Tile(2));
        Assert.False(first.IsEmpty);
        Assert.False(third.IsEmpty);
        Assert.NotEqual(Middle(layout, first), Middle(layout, third));

        // The block of the part they live in is one point for all four tiles — the answer a tile's
        // tooltip or jump list must not be given.
        var block = layout.BlockRect(1);
        Assert.Equal(block, layout.FlyoutRect(Hit.Tile(0)));
        Assert.Equal(block, layout.FlyoutRect(Hit.Tile(2)));
        Assert.NotEqual(Middle(layout, block), Middle(layout, first));
    }

    /// <summary>A widget of one cell answers the same either way, which is why this went unnoticed.</summary>
    [Fact]
    public void AOneCellWidgetsBlockIsItsCell()
    {
        var specs = new List<PartSpec> { Spec("w", BarKind.FolderList), Spec("c", BarKind.NightLight) };
        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Bottom, new Config(), specs, 40f);

        Assert.Equal(layout.ItemRect(Hit.Cell(0, 0)), layout.FlyoutRect(Hit.Cell(0, 0)));
    }

    /// <summary>A hit in no part of this bar anchors on nothing, and the dock falls back to the pointer.</summary>
    [Fact]
    public void AHitInNoPartHasNoRectangle()
    {
        var specs = new List<PartSpec> { Spec("c", BarKind.NightLight) };
        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Bottom, new Config(), specs, 40f);

        Assert.True(layout.FlyoutRect(Hit.Cell(7, 0)).IsEmpty);
        Assert.True(layout.FlyoutRect(Hit.Tile(0)).IsEmpty);
    }

    /// <summary>The middle of a rectangle along the bar: x on a horizontal bar, y on a vertical one.</summary>
    private static float Middle(Layout layout, RectF rect) =>
        layout.Vertical ? rect.Y + rect.Height / 2f : rect.X + rect.Width / 2f;
}
