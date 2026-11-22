// edit: the menu a click opens while the bar is arranged stands beside the widget, on one of its four
// sides (the person, 2026-10-19: "it should bring up the current right click menu that is placed at
// one of four places around the widget"; Karpo.Dock\MenuSides.cs, §212). Held here: which side is tried
// first for a bar's widget on every edge and for a folder's line, mirrored in Arabic and Urdu; that the
// first side with the room has it, and with none the roomiest; where each side anchors the menu; and
// that a folder's menu while arranged no longer has "Open folder" (§253: a click opens it).

using Karpo.Dock;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class MenuSidesTests
{
    /// <summary>A 1920 by 1080 display whose bottom 48 px a bar holds: the work area above it.</summary>
    private static readonly RECT Work = new() { left = 0, top = 0, right = 1920, bottom = 1032 };

    private static readonly System.Numerics.Vector2 Menu = new(300, 200);

    private static RECT Box(int left, int top, int right, int bottom) => new() { left = left, top = top, right = right, bottom = bottom };

    [Theory]
    [InlineData(Edge.Bottom, false, new[] { MenuSide.Above, MenuSide.Right, MenuSide.Left, MenuSide.Below })]
    [InlineData(Edge.Top, false, new[] { MenuSide.Below, MenuSide.Right, MenuSide.Left, MenuSide.Above })]
    [InlineData(Edge.Left, false, new[] { MenuSide.Right, MenuSide.Below, MenuSide.Above, MenuSide.Left })]
    [InlineData(Edge.Right, false, new[] { MenuSide.Left, MenuSide.Below, MenuSide.Above, MenuSide.Right })]
    [InlineData(Edge.Bottom, true, new[] { MenuSide.Above, MenuSide.Left, MenuSide.Right, MenuSide.Below })]
    public void ABarsWidgetTriesAwayFromItsEdgeFirst(Edge edge, bool mirrored, MenuSide[] order)
    {
        Assert.Equal(order, MenuSides.Order(onBar: true, edge, mirrored));
    }

    [Fact]
    public void AFolderLineTriesAfterItThenBefore()
    {
        Assert.Equal([MenuSide.Right, MenuSide.Left, MenuSide.Below, MenuSide.Above], MenuSides.Order(onBar: false, Edge.Bottom, mirrored: false));
        // Arabic and Urdu read right to left: after a line is its left.
        Assert.Equal([MenuSide.Left, MenuSide.Right, MenuSide.Below, MenuSide.Above], MenuSides.Order(onBar: false, Edge.Bottom, mirrored: true));
    }

    /// <summary>
    /// A bottom bar's widget: its menu over it. Beside it there is no room — the strip the bar holds is
    /// out of the work area — and under it none either; so where over it is too short, the menu still
    /// stands over it, cut to the room there, rather than slid off sideways.
    /// </summary>
    [Fact]
    public void ABottomBarsWidgetHasItsMenuOverIt()
    {
        var clock = Box(1800, 1036, 1900, 1076);
        var order = MenuSides.Order(onBar: true, Edge.Bottom, mirrored: false);
        Assert.Equal(MenuSide.Above, MenuSides.Pick(order, clock, Work, Menu, 8, 8));
        Assert.False(MenuSides.Fits(MenuSide.Right, clock, Work, Menu, 8, 8));
        Assert.False(MenuSides.Fits(MenuSide.Left, clock, Work, Menu, 8, 8));
        Assert.Equal(MenuSide.Above, MenuSides.Pick(order, clock, Work, Menu with { Y = 5000 }, 8, 8));
    }

    /// <summary>A floating bottom bar's widget with a short screen over it, room under it: under it.</summary>
    [Fact]
    public void WithNoRoomOverItTheNextSideWithRoomHasIt()
    {
        var work = Box(0, 0, 1920, 1080);
        var low = Box(900, 150, 1000, 190);
        var order = MenuSides.Order(onBar: true, Edge.Bottom, mirrored: false);
        // Over it 142 px less the gap and margin: too short. After it there is the room.
        Assert.Equal(MenuSide.Right, MenuSides.Pick(order, low, work, Menu, 8, 8));
        // And with the screen's end right of it too, before it.
        Assert.Equal(MenuSide.Left, MenuSides.Pick(order, Box(1700, 150, 1800, 190), work, Menu, 8, 8));
    }

    /// <summary>A folder's line: beside it, after it where there is the room, else before it, else under or over it.</summary>
    [Fact]
    public void AFolderLinesMenuStandsBesideIt()
    {
        var order = MenuSides.Order(onBar: false, Edge.Bottom, mirrored: false);
        Assert.Equal(MenuSide.Right, MenuSides.Pick(order, Box(800, 600, 1160, 660), Work, Menu, 8, 8));
        Assert.Equal(MenuSide.Left, MenuSides.Pick(order, Box(1500, 600, 1860, 660), Work, Menu, 8, 8));
        // A line as wide as the screen: under it, else over it.
        Assert.Equal(MenuSide.Below, MenuSides.Pick(order, Box(0, 300, 1920, 360), Work, Menu, 8, 8));
        Assert.Equal(MenuSide.Above, MenuSides.Pick(order, Box(0, 800, 1920, 860), Work, Menu, 8, 8));
        // Mirrored: before is the right.
        Assert.Equal(MenuSide.Left, MenuSides.Pick(MenuSides.Order(onBar: false, Edge.Bottom, mirrored: true), Box(800, 600, 1160, 660), Work, Menu, 8, 8));
    }

    /// <summary>
    /// Each side anchors the menu on the middle of that side of the widget, never on the pointer, and
    /// opens it away from the widget: a placement's edge is the side it opens away from.
    /// </summary>
    [Theory]
    [InlineData(MenuSide.Above, Edge.Bottom, 150, 100)]
    [InlineData(MenuSide.Below, Edge.Top, 150, 140)]
    [InlineData(MenuSide.Right, Edge.Left, 200, 120)]
    [InlineData(MenuSide.Left, Edge.Right, 100, 120)]
    public void EachSideAnchorsOnTheMiddleOfThatSide(MenuSide side, Edge edge, int x, int y)
    {
        var placed = MenuSides.Place(side, Box(100, 100, 200, 140), Work);
        Assert.Equal(edge, placed.Edge);
        Assert.Equal(x, placed.Anchor.x);
        Assert.Equal(y, placed.Anchor.y);
        Assert.Equal(MenuSides.GapDip, placed.GapDip);
        Assert.False(placed.Over);
    }

    /// <summary>
    /// §253: its menu while the bar is arranged has no "Open folder" nor "Close folder" under its buttons any more
    /// (§212 had them); its face closes it (§252). §527: a click on its face opens it, so no menu of it has "Open"
    /// (§481 to §526 its face's menu had it first).
    /// </summary>
    [Fact]
    public void NoFoldersMenuOpensTheFolder()
    {
        Assert.DoesNotContain("OpenFolder", Enum.GetNames<WidgetAct>());
        Assert.DoesNotContain("CloseFolder", Enum.GetNames<WidgetAct>());
        var folder = new BarItem { Id = "f", Kind = BarKind.FolderList };
        Assert.DoesNotContain(TheDock.WidgetRowsFor(folder), item => item.Text is "Open folder" or "Close folder" || item.Text == S.DockWidgetMenuOpen);
    }
}
