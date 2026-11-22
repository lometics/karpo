// When the pointer has left the window list (Karpo.Dock\Preview.cs): the real popup, shown on a
// desktop of its own, told where the pointer moves.
//
// The popup's window is bigger than the list it shows: on the three sides away from the bar it leaves
// the shadow room (28 px at 100 %), which draws nothing but is still the window, so the pointer was
// "inside" while it rested there, WM_MOUSELEAVE never came, and the list stayed open with the pointer
// visibly off it. The room is out of the list now; the gap between the list and the button it opened
// from is still in, so the way from a button up to its list never leaves it. Only that gap: the
// window's edge on the bar's side reaches a few px over the bar, and beside the button that strip
// was the list's too, so a pointer resting on the bar there kept the list open.
//
// Needs windows and a compositor, so it runs only with KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Signals;

public sealed unsafe class PreviewLeaveTests
{
    private static readonly Look Dressed = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0, 0x78, 0xD4), Animations: false);

    /// <summary>
    /// The way up from the button: the gap on the bar's side, as wide as the button and centred on
    /// it, reaching past the list's end over the shadow's room where the button does (a list pushed
    /// against the screen's edge), cut only at the window's, and the list's whole width where the
    /// caller did not say how wide the button is.
    /// </summary>
    [Fact]
    public void TheWayUpIsTheGapAboveTheButtonOnly()
    {
        var list = new RECT { left = 28, top = 28, right = 528, bottom = 228 };
        // A 600 × 236 window over a bottom bar: 28 px of room, 8 px of gap below.
        var bottom = Preview.BridgeOf(list, 556, 236, Edge.Bottom, 300, 236, 44);
        Assert.Equal((278, 228, 322, 236), (bottom.left, bottom.top, bottom.right, bottom.bottom));
        // Not said: the whole width, as before.
        var unsaid = Preview.BridgeOf(list, 556, 236, Edge.Bottom, 300, 236, 0);
        Assert.Equal((28, 228, 528, 236), (unsaid.left, unsaid.top, unsaid.right, unsaid.bottom));
        // A button at the list's end reaches past it, as one whose list was pushed against the
        // screen's edge does: the way is over the whole button, the shadow's room included.
        var end = Preview.BridgeOf(list, 556, 236, Edge.Bottom, 520, 236, 44);
        Assert.Equal((498, 228, 542, 236), (end.left, end.top, end.right, end.bottom));
        // Only as far as the window reaches, at either end.
        var past = Preview.BridgeOf(list, 556, 236, Edge.Bottom, 550, 236, 44);
        Assert.Equal((528, 556), (past.left, past.right));
        var before = Preview.BridgeOf(list, 556, 236, Edge.Bottom, 10, 236, 44);
        Assert.Equal((0, 32), (before.left, before.right));
        var side = Preview.BridgeOf(new RECT { left = 8, top = 28, right = 508, bottom = 428 }, 536, 456, Edge.Left, 0, 440, 40);
        Assert.Equal((0, 420, 8, 456), (side.left, side.top, side.right, side.bottom));
        // A top bar: from the window's edge down to the list.
        var top = Preview.BridgeOf(new RECT { left = 28, top = 8, right = 528, bottom = 208 }, 556, 236, Edge.Top, 300, 0, 44);
        Assert.Equal((278, 0, 322, 8), (top.left, top.top, top.right, top.bottom));
        // Bars at the side: the way runs across, as tall as the button.
        var left = Preview.BridgeOf(new RECT { left = 8, top = 28, right = 508, bottom = 428 }, 536, 456, Edge.Left, 0, 200, 40);
        Assert.Equal((0, 180, 8, 220), (left.left, left.top, left.right, left.bottom));
        var right = Preview.BridgeOf(new RECT { left = 28, top = 28, right = 528, bottom = 428 }, 536, 456, Edge.Right, 536, 200, 40);
        Assert.Equal((528, 180, 536, 220), (right.left, right.top, right.right, right.bottom));
    }

    /// <summary>
    /// A list pushed along the bar against the screen's edge — the button in the screen's corner —
    /// is no longer centred on its button: the way up still runs from the whole of the button to
    /// the list, the part of it past the list's end included, and the pointer on it has not left.
    /// </summary>
    [UiTheory]
    [InlineData(Edge.Bottom, 1f, false)]
    [InlineData(Edge.Bottom, 1.5f, true)]
    [InlineData(Edge.Top, 1f, true)]
    [InlineData(Edge.Left, 1.5f, false)]
    [InlineData(Edge.Right, 1f, true)]
    public void AListPushedAgainstTheScreensEdgeKeepsTheWayUp(Edge edge, float scale, bool far)
    {
        PreviewToolbarTests.OnAHiddenDesktop(gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            PreviewToolbarTests.Register(Preview.ClassName, instance);
            PreviewToolbarTests.Register("karpo.tests.preview.app", instance);
            var first = PreviewToolbarTests.Window(instance);
            var second = PreviewToolbarTests.Window(instance);
            using var preview = new Preview(gfx, instance, 0);
            try
            {
                var button = (int)(44 * scale);
                var work = new RECT { right = 1920, bottom = 1040 };
                // The button in the screen's corner: its middle half a button from the edge.
                var vertical = edge is Edge.Left or Edge.Right;
                var along = far ? (vertical ? work.bottom : work.right) - button / 2 : button / 2;
                var anchor = edge switch
                {
                    Edge.Top => new POINT { x = along, y = 48 },
                    Edge.Left => new POINT { x = 48, y = along },
                    Edge.Right => new POINT { x = 1872, y = along },
                    _ => new POINT { x = along, y = 1000 },
                };
                preview.Show(gfx, Dressed, scale, 0, [(first, "First"), (second, "Second")], anchor, work, edge, buttonSpan: button);
                var list = preview.ListArea;
                var bridge = preview.Bridge;
                RECT client;
                RECT placed;
                Assert.True(GetClientRect(preview.Hwnd, &client));
                Assert.True(GetWindowRect(preview.Hwnd, &placed));
                // The button, in the window's own pixels.
                var middle = vertical ? anchor.y - placed.top : anchor.x - placed.left;
                var (listFrom, listTo) = vertical ? (list.top, list.bottom) : (list.left, list.right);
                Assert.True(middle - button / 2 < listFrom || middle - button / 2 + button > listTo, "the list was pushed off its button's middle");

                // The way is the whole button, the part past the list's end included.
                var (wayFrom, wayTo) = vertical ? (bridge.top, bridge.bottom) : (bridge.left, bridge.right);
                Assert.Equal(middle - button / 2, wayFrom);
                Assert.Equal(middle - button / 2 + button, wayTo);

                var card = PreviewToolbarTests.Middle(preview.CardBounds(0)!.Value);
                Assert.Equal(PreviewEventKind.Entered, preview.Moved(card.x, card.y).Kind);
                // Over the part of the button past the list's end, in the gap: still the list's.
                var outer = far ? wayTo - 2 : wayFrom + 1;
                var gap = edge switch
                {
                    Edge.Top => new POINT { x = outer, y = 1 },
                    Edge.Left => new POINT { x = 1, y = outer },
                    Edge.Right => new POINT { x = client.right - 2, y = outer },
                    _ => new POINT { x = outer, y = client.bottom - 2 },
                };
                Assert.True(vertical ? gap.y < listFrom || gap.y >= listTo : gap.x < listFrom || gap.x >= listTo, "the point is past the list's end");
                Assert.Equal(PreviewEventKind.Nothing, preview.Moved(gap.x, gap.y).Kind);
                Assert.True(preview.Inside, "the way up from the button's outer part is the list's");
                // And straight on up past the gap, beside the list: the shadow's room, which is not.
                var room = edge switch
                {
                    Edge.Top => new POINT { x = outer, y = list.top + 4 },
                    Edge.Left => new POINT { x = list.left + 4, y = outer },
                    Edge.Right => new POINT { x = list.right - 4, y = outer },
                    _ => new POINT { x = outer, y = list.bottom - 4 },
                };
                Assert.Equal(PreviewEventKind.Left, preview.Moved(room.x, room.y).Kind);
            }
            finally
            {
                preview.Hide();
                _ = DestroyWindow(first);
                _ = DestroyWindow(second);
            }
        });
    }

    [UiTheory]
    [InlineData(Edge.Bottom, 1f)]
    [InlineData(Edge.Bottom, 1.5f)]
    [InlineData(Edge.Top, 1f)]
    [InlineData(Edge.Left, 1.5f)]
    [InlineData(Edge.Right, 1f)]
    public void ThePointerInTheShadowsRoomHasLeftTheList(Edge edge, float scale)
    {
        PreviewToolbarTests.OnAHiddenDesktop(gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            PreviewToolbarTests.Register(Preview.ClassName, instance);
            PreviewToolbarTests.Register("karpo.tests.preview.app", instance);
            var first = PreviewToolbarTests.Window(instance);
            var second = PreviewToolbarTests.Window(instance);
            using var preview = new Preview(gfx, instance, 0);
            try
            {
                var anchor = edge switch
                {
                    Edge.Top => new POINT { x = 900, y = 48 },
                    Edge.Left => new POINT { x = 48, y = 500 },
                    Edge.Right => new POINT { x = 1872, y = 500 },
                    _ => new POINT { x = 900, y = 1000 },
                };
                var button = (int)(44 * scale);
                preview.Show(gfx, Dressed, scale, 0, [(first, "First"), (second, "Second")], anchor, new RECT { right = 1920, bottom = 1040 }, edge, buttonSpan: button);
                var list = preview.ListArea;
                var bridge = preview.Bridge;
                RECT window;
                Assert.True(GetClientRect(preview.Hwnd, &window));
                var card = PreviewToolbarTests.Middle(preview.CardBounds(0)!.Value);
                var middle = PreviewToolbarTests.Middle(list);

                // The way up from the button: from the list to the window's edge, as wide as the button.
                var vertical = edge is Edge.Left or Edge.Right;
                Assert.Equal(button, vertical ? bridge.bottom - bridge.top : bridge.right - bridge.left);
                var (near, far) = edge switch
                {
                    Edge.Top => (bridge.bottom - list.top, bridge.top),
                    Edge.Left => (bridge.right - list.left, bridge.left),
                    Edge.Right => (bridge.left - list.right, window.right - bridge.right),
                    _ => (bridge.top - list.bottom, window.bottom - bridge.bottom),
                };
                Assert.Equal(0, near);
                Assert.Equal(0, far);

                // On a card: in.
                Assert.Equal(PreviewEventKind.Entered, preview.Moved(card.x, card.y).Kind);
                Assert.True(preview.Inside);

                // Each side: the bar's is the gap, still the list over the button; the other three
                // are the shadow's room, which is the window but not the list.
                var sides = new (Edge Side, POINT At)[]
                {
                    (Edge.Left, new POINT { x = list.left - 2, y = middle.y }),
                    (Edge.Right, new POINT { x = list.right + 1, y = middle.y }),
                    (Edge.Top, new POINT { x = middle.x, y = list.top - 2 }),
                    (Edge.Bottom, new POINT { x = middle.x, y = list.bottom + 1 }),
                };
                var way = PreviewToolbarTests.Middle(bridge);
                var gap = edge switch
                {
                    Edge.Top => new POINT { x = way.x, y = 1 },
                    Edge.Left => new POINT { x = 1, y = way.y },
                    Edge.Right => new POINT { x = window.right - 2, y = way.y },
                    _ => new POINT { x = way.x, y = window.bottom - 2 },
                };
                Assert.Equal(PreviewEventKind.Nothing, preview.Moved(gap.x, gap.y).Kind);
                Assert.True(preview.Inside, "the gap between the button and the list is the list's");

                // Beside the button, the same strip is over the bar: resting there has left the list.
                var beside = edge switch
                {
                    Edge.Top or Edge.Bottom => new POINT { x = list.left + 2, y = gap.y },
                    _ => new POINT { x = gap.x, y = list.top + 2 },
                };
                Assert.False(beside.x >= bridge.left && beside.x < bridge.right && beside.y >= bridge.top && beside.y < bridge.bottom, "the point beside the button is off the way up");
                Assert.Equal(PreviewEventKind.Left, preview.Moved(beside.x, beside.y).Kind);
                Assert.False(preview.Inside, "resting on the bar beside the button is off the list");
                Assert.Equal(PreviewEventKind.Entered, preview.Moved(card.x, card.y).Kind);

                foreach (var (side, at) in sides.Where(side => side.Side != edge))
                {
                    Assert.True(at.x >= 0 && at.y >= 0 && at.x < window.right && at.y < window.bottom, $"the {side} room is in the window");
                    Assert.Equal(PreviewEventKind.Left, preview.Moved(at.x, at.y).Kind);
                    Assert.False(preview.Inside, $"resting in the {side} room is off the list");
                    // Still there: nothing more to say.
                    Assert.Equal(PreviewEventKind.Nothing, preview.Moved(at.x, at.y).Kind);
                    // Back on a card: in again.
                    Assert.Equal(PreviewEventKind.Entered, preview.Moved(card.x, card.y).Kind);
                }

                // And out of the window altogether (WM_MOUSELEAVE): left.
                Assert.Equal(PreviewEventKind.Left, preview.Left().Kind);
                Assert.False(preview.Inside);
            }
            finally
            {
                preview.Hide();
                _ = DestroyWindow(first);
                _ = DestroyWindow(second);
            }
        });
    }
}
