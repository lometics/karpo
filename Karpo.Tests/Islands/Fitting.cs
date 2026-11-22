// What the flyouts' height tests measure, measured the same way for every flyout: the window as
// Windows has it (its height and where its edges are, in screen px), and a picture of what the
// panel draws (how many rows at its foot have nothing on them). Each flyout's own test drives its
// content; these say what came of it in pixels, not in the numbers that went in.

using Karpo.Dock;
using Karpo.Tests.Edit;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Islands;

internal static unsafe class Fitting
{
    /// <summary>The window's height in screen px: the panel and the shadow's room around it.</summary>
    internal static int WindowHeight(HWND window) => Bounds(window).bottom - Bounds(window).top;

    /// <summary>Where the window's top edge is: what a panel hanging off a bottom bar moves when it changes height.</summary>
    internal static int WindowTop(HWND window) => Bounds(window).top;

    /// <summary>Where the window's bottom edge is: on a bottom bar, the edge that stays on the bar.</summary>
    internal static int WindowBottom(HWND window) => Bounds(window).bottom;

    /// <summary>
    /// How many rows at the foot of a <paramref name="width"/> × <paramref name="height"/> picture
    /// of the panel have nothing drawn on them, <paramref name="paint"/> drawing its content (and
    /// not its chrome) over nothing. A panel as tall as what it shows has no more than its own
    /// padding there; a panel with room it does not need has the room.
    /// </summary>
    internal static float EmptyFoot(float width, float height, Offscreen.Painter paint)
    {
        var foot = 0f;
        Offscreen.Bitmap(width, height, paint, bitmap =>
        {
            var ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height));
            foot = MathF.Ceiling(height) - ink.Bottom;
        });
        return foot;
    }

    /// <summary>A point in a window's client px as a mouse message's lParam.</summary>
    internal static nint Point(int x, int y) => (nint)(((y & 0xFFFF) << 16) | (x & 0xFFFF));

    /// <summary>
    /// Dispatches what the flyouts post themselves (WM_USER and up) and nothing else. A pointer a
    /// test moves over a window on a hidden desktop is not really over it, so the tracking the move
    /// starts has Windows post WM_MOUSELEAVE at once, and an ordinary pump would deliver that leave
    /// before the news the test is waiting for: this leaves the pointer where the test put it.
    /// </summary>
    internal static void PumpPosts()
    {
        MSG message;
        while (PeekMessageW(&message, HWND.NULL, WM.WM_USER, 0xBFFF, PM.PM_REMOVE))
        {
            _ = TranslateMessage(&message);
            _ = DispatchMessageW(&message);
        }
    }

    private static RECT Bounds(HWND window)
    {
        RECT bounds;
        bounds = WindowShots.Extent(window);
        return bounds;
    }
}
