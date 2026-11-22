// edit: a widget's menu that a click opened while the bar is arranged stands beside the widget, on
// one of its four sides, rather than at the pointer (the person, 2026-10-19: "left click in edit mode
// when i select a widget ... should bring up the current right click menu that is placed at one of four
// places around the widget"). Which side:
//
//  - **A widget on a bar** opens it away from the bar's edge, where its other popups open — over a
//    bottom bar's widget, under a top bar's, beside a side bar's — then along the bar, after it and
//    before it, and last towards the edge, where a bar off its edge (a floating one)
//    may have the room.
//  - **A line in a folder** opens it beside the line, after it and then before it, where the lines
//    under and over it stay in sight; then under it and over it.
//  - **After and before** are the reading direction's: right and left, mirrored in Arabic and Urdu.
//
// The first side with the room for the whole menu has it; with none, the side with the most of what the
// menu needs, where it is cut to the room there. The menu is anchored on the widget's rectangle — the
// middle of that side — never on the pointer, so it stands in the same place however the widget was
// clicked, and a keyboard's menu key opens it there too. A side counts as having the room only where the
// widget's middle along it is inside the work area: a bar's widget has no room after itself in the strip
// the bar holds, which the work area leaves out.
//
// Plain arithmetic, so the tests can reach it; Flyout places the menu by it once it knows its size.

using System.Numerics;
using TerraFX.Interop.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Dock;

/// <summary>A side of a widget a menu can stand on (<see cref="MenuSides"/>).</summary>
public enum MenuSide
{
    Above,
    Below,
    Right,
    Left,
}

/// <summary>
/// A menu to stand beside <paramref name="Widget"/> (screen px), on the first of <paramref name="Order"/>
/// with the room (<see cref="MenuSides.Pick"/>).
/// </summary>
public sealed record MenuBeside(RECT Widget, IReadOnlyList<MenuSide> Order);

/// <summary>Which side of a widget its menu stands on, and where that puts it; see the top of the file.</summary>
public static class MenuSides
{
    /// <summary>Between the widget and its menu, in DIPs.</summary>
    public const float GapDip = 8;

    /// <summary>Kept from the work area's edges, in DIPs: a menu's, nearer than a flyout's (<see cref="FlyoutPlacement.MarginDip"/>), as its gap is.</summary>
    public const float MarginDip = 8;

    /// <summary>
    /// The sides in the order they are tried: for a widget on a bar along <paramref name="edge"/>
    /// (<paramref name="onBar"/>) away from the edge first, then along the bar, then towards the edge;
    /// for a folder's line after it, before it, under it, over it. <paramref name="mirrored"/>: the
    /// display reads right to left, and after is the left.
    /// </summary>
    public static IReadOnlyList<MenuSide> Order(bool onBar, Edge edge, bool mirrored)
    {
        var after = mirrored ? MenuSide.Left : MenuSide.Right;
        var before = mirrored ? MenuSide.Right : MenuSide.Left;
        if (!onBar)
        {
            return [after, before, MenuSide.Below, MenuSide.Above];
        }
        return edge switch
        {
            Edge.Top => [MenuSide.Below, after, before, MenuSide.Above],
            Edge.Left => [MenuSide.Right, MenuSide.Below, MenuSide.Above, MenuSide.Left],
            Edge.Right => [MenuSide.Left, MenuSide.Below, MenuSide.Above, MenuSide.Right],
            _ => [MenuSide.Above, after, before, MenuSide.Below],
        };
    }

    /// <summary>
    /// The room on <paramref name="side"/> of <paramref name="widget"/>, in px: from the widget, past
    /// <paramref name="gap"/>, to <paramref name="work"/>'s edge less <paramref name="margin"/>.
    /// </summary>
    public static int Room(MenuSide side, RECT widget, RECT work, int gap, int margin) => side switch
    {
        MenuSide.Above => widget.top - gap - (work.top + margin),
        MenuSide.Below => work.bottom - margin - (widget.bottom + gap),
        MenuSide.Right => work.right - margin - (widget.right + gap),
        _ => widget.left - gap - (work.left + margin),
    };

    /// <summary>How much of a menu of <paramref name="size"/> px has to fit on <paramref name="side"/>: its height over or under, its width beside.</summary>
    private static float Depth(MenuSide side, Vector2 size) => side is MenuSide.Above or MenuSide.Below ? size.Y : size.X;

    /// <summary>
    /// Whether a menu of <paramref name="size"/> px fits whole on <paramref name="side"/>: the room for
    /// it, and the widget's middle along that side inside the work area, so the menu stands beside it.
    /// </summary>
    public static bool Fits(MenuSide side, RECT widget, RECT work, Vector2 size, int gap, int margin) =>
        Room(side, widget, work, gap, margin) >= Depth(side, size) && Beside(side, widget, work);

    /// <summary>Whether the widget's middle along <paramref name="side"/> is inside the work area: a menu there stands beside it, not slid away.</summary>
    private static bool Beside(MenuSide side, RECT widget, RECT work)
    {
        var across = side is MenuSide.Above or MenuSide.Below;
        var middle = across ? (widget.left + widget.right) / 2 : (widget.top + widget.bottom) / 2;
        return across ? middle >= work.left && middle <= work.right : middle >= work.top && middle <= work.bottom;
    }

    /// <summary>
    /// The side a menu of <paramref name="size"/> px stands on: the first of <paramref name="order"/>
    /// it fits on whole (<see cref="Fits"/>); with none, of the sides it would stand beside the widget
    /// on, the one with the most of the room it needs, where it is cut to that room.
    /// </summary>
    public static MenuSide Pick(IReadOnlyList<MenuSide> order, RECT widget, RECT work, Vector2 size, int gap, int margin)
    {
        foreach (var side in order)
        {
            if (Fits(side, widget, work, size, gap, margin))
            {
                return side;
            }
        }
        var sides = order.Where(side => Beside(side, widget, work)).ToList();
        if (sides.Count == 0)
        {
            sides = [.. order];
        }
        var best = sides.Count > 0 ? sides[0] : MenuSide.Above;
        var most = float.MinValue;
        foreach (var side in sides)
        {
            var share = Room(side, widget, work, gap, margin) / MathF.Max(1, Depth(side, size));
            if (share > most)
            {
                (best, most) = (side, share);
            }
        }
        return best;
    }

    /// <summary>
    /// Where a menu on <paramref name="side"/> of <paramref name="widget"/> is placed: anchored on the
    /// middle of that side, opening away from the widget (a placement's edge is the side it opens away
    /// from), <paramref name="gapDip"/> off it, inside <paramref name="work"/>.
    /// </summary>
    public static FlyoutPlacement Place(MenuSide side, RECT widget, RECT work, float gapDip = GapDip)
    {
        var middleX = (widget.left + widget.right) / 2;
        var middleY = (widget.top + widget.bottom) / 2;
        return side switch
        {
            MenuSide.Above => new FlyoutPlacement(new POINT { x = middleX, y = widget.top }, work, Edge.Bottom, gapDip, Margin: MarginDip),
            MenuSide.Below => new FlyoutPlacement(new POINT { x = middleX, y = widget.bottom }, work, Edge.Top, gapDip, Margin: MarginDip),
            MenuSide.Right => new FlyoutPlacement(new POINT { x = widget.right, y = middleY }, work, Edge.Left, gapDip, Margin: MarginDip),
            _ => new FlyoutPlacement(new POINT { x = widget.left, y = middleY }, work, Edge.Right, gapDip, Margin: MarginDip),
        };
    }

    /// <summary>
    /// <paramref name="beside"/>'s menu, <paramref name="size"/> px, at <paramref name="scale"/>: placed
    /// on the side <see cref="Pick"/> chooses.
    /// </summary>
    public static FlyoutPlacement Place(MenuBeside beside, RECT work, Vector2 size, float scale)
    {
        var gap = (int)MathF.Round(GapDip * scale, MidpointRounding.AwayFromZero);
        var margin = (int)MathF.Round(MarginDip * scale, MidpointRounding.AwayFromZero);
        return Place(Pick(beside.Order, beside.Widget, work, size, gap, margin), beside.Widget, work);
    }
}
