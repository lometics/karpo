// The pointer as a person moves it over the bar: to a button, rested until its tooltip or its window
// list shows, then away — and every way away: a jump (a touchpad's, a remote session's, a pen's
// leaving), to the other display, a fast swipe, across the tooltip first. Whatever came up must be
// gone within a second (Rules.Patience), whether or not Windows told the bar the pointer left.

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Resilience;

internal static unsafe class Hover
{
    /// <summary>The bar's hover windows (BarTip.cs, Preview.cs): their titles.</summary>
    public const string Tooltip = "Karpo tooltip";
    public const string WindowList = "Karpo windows";

    /// <summary>The pointer to <paramref name="x"/>,<paramref name="y"/>, and a real move there, as a mouse makes.</summary>
    public static void To(int x, int y)
    {
        SetCursorPos(x, y);
        Nudge();
    }

    /// <summary>A move of nothing: one pixel and back, so the window under the pointer hears a move.</summary>
    public static void Nudge()
    {
        var inputs = stackalloc INPUT[2];
        inputs[0].type = INPUT.INPUT_MOUSE;
        inputs[0].Anonymous.mi.dx = 1;
        inputs[0].Anonymous.mi.dwFlags = C.MOUSEEVENTF_MOVE;
        inputs[1].type = INPUT.INPUT_MOUSE;
        inputs[1].Anonymous.mi.dx = -1;
        inputs[1].Anonymous.mi.dwFlags = C.MOUSEEVENTF_MOVE;
        SendInput(2, inputs, sizeof(INPUT));
    }

    /// <summary>One relative move of <paramref name="dx"/>,<paramref name="dy"/>: a flick, no step between.</summary>
    public static void Swipe(int dx, int dy)
    {
        INPUT input = default;
        input.type = INPUT.INPUT_MOUSE;
        input.Anonymous.mi.dx = dx;
        input.Anonymous.mi.dy = dy;
        input.Anonymous.mi.dwFlags = C.MOUSEEVENTF_MOVE;
        SendInput(1, &input, sizeof(INPUT));
    }

    public static (int X, int Y) Where()
    {
        POINT at;
        GetCursorPos(&at);
        return (at.x, at.y);
    }

    /// <summary>The hover window shown now: a tooltip or a window list, or null.</summary>
    public static WindowSeen? Shown(Snapshot snapshot) => snapshot.Popups.FirstOrDefault(p => p.Title is Tooltip or WindowList);

    /// <summary>
    /// Places along <paramref name="device"/>'s bar where resting the pointer brings up a tooltip or a
    /// window list: the strip walked two cells at a time, at most <paramref name="most"/> of them.
    /// </summary>
    public static List<(int X, int Y, string Shows)> Targets(Run run, DisplayFacts display, int most)
    {
        var found = new List<(int, int, string)>();
        var strip = display.Bounds.Bottom - display.Work.Bottom;
        if (strip <= 0)
        {
            return found;
        }
        var y = display.Bounds.Bottom - strip / 2;
        for (var x = display.Bounds.Left + strip / 2; x < display.Bounds.Right && found.Count < most; x += strip * 2)
        {
            To(x, y);
            if (run.Until(s => Shown(s) is not null, 1.2) is not null && Shown(run.Watcher.Last!) is { } shown)
            {
                found.Add((x, y, shown.Title));
                x += strip * 2;
            }
        }
        return found;
    }
}
