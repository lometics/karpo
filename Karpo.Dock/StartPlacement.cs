// Where Windows' Start stands while Karpo runs: hung from Karpo's Start button (docs/widgets.md §666, which
// replaces §580's, §591's, §601's and §640's rules for Start).
//
// The user, 2026-11-10: "the native start menu is still placed at wrong locations. fix this once and for all and also
// thorouhgly test this".
//
// What Start is made of (26300; §666 has the evidence, read off appbar.log and Windhawk's Taskbar on top mod):
//
// - Start's host is a CoreWindow of StartMenuExperienceHost as wide as its display. Start (the redesigned one's
//   FrameRoot) is laid out inside it by XAML alignment: at its foot, and at its left (TaskbarAl 0) or in its middle.
//   Nothing outside Start's process can change that alignment, so Start goes where its host goes, and only there.
// - Start's search box, SearchHost's CoreWindow shown over Start, is Start's frame: Start plus Windows' gap all round
//   (its search field centred in it, 68 px in from its left and 67 from its right at 150 %). Windows places it twice as
//   Start opens — first against the screen's end (0,825..1287,2160), then, settled, against the host's end
//   (0,753..1287,2088 under a host 0,0..3840,2088) — so its place says nothing, but its size is Start's size plus the
//   gap twice, at every showing and as Start resizes itself.
// - Windows' gap is 12 DIP: Start stands 18 px in from the frame at 150 %.
//
// So Start's place is one pure function of the display, Karpo's bar and button, and Start's size (Target), and the host
// goes by exactly as far as Start has to (Host): measured from where Windows lays Start out in the host as the host
// stands, whoever put it there — so it is moved once however often it is heard, and nothing about Explorer's hidden
// taskbar, where Windows first put the host or the box, or an earlier opening is needed. The box goes where Start is
// (Box). The one thing remembered is Start's size in DIPs (StartSize), for the few ms between the host showing and the
// box showing.

using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>§601: Karpo's Start button Start hangs from: the button and its bar's panel on the screen, the edge the bar stands on.</summary>
internal readonly record struct StartButton(RECT Button, RECT Bar, Edge Edge);

/// <summary>§666: a display as Start's place is worked out on: its rectangle, its work area, its DPI.</summary>
internal readonly record struct StartDisplay(RECT Bounds, RECT Work, uint Dpi);

/// <summary>
/// §666: everything where Start stands depends on but its size: the <paramref name="Display"/> Start opens on, Karpo's Start
/// button there (<paramref name="Button"/>, null where no bar there has one), whether Windows lays Start out in the middle
/// of its host (<paramref name="Centred"/>, TaskbarAl 1) and every display (<paramref name="Displays"/>).
/// </summary>
internal sealed record StartScene(StartDisplay Display, StartButton? Button, bool Centred, IReadOnlyList<StartDisplay> Displays)
{
    /// <summary>Windows' gap round Start on this display, in its pixels.</summary>
    public int Gap => StartPlacement.Gap(Display.Dpi);
}

/// <summary>§666: where Windows' Start, its host and its search box go. Pure.</summary>
internal static class StartPlacement
{
    /// <summary>Windows' gap between Start and the screen's end or its taskbar, in DIPs.</summary>
    public const int GapDip = 12;

    /// <summary><see cref="GapDip"/> in the pixels of a display at <paramref name="dpi"/>.</summary>
    public static int Gap(uint dpi) => (int)((GapDip * dpi + 48) / 96);

    /// <summary>Start's size from its search box's <paramref name="box"/>: the box less the gap all round; null where nothing is left.</summary>
    public static (int Width, int Height)? SizeOfBox(RECT box, int gap)
    {
        var (width, height) = (box.right - box.left - 2 * gap, box.bottom - box.top - 2 * gap);
        return width > 0 && height > 0 ? (width, height) : null;
    }

    /// <summary>
    /// Where Windows lays Start of <paramref name="size"/> out inside its <paramref name="host"/>: its frame (Start and the
    /// gap round it) at the host's foot, at its left or in its middle (<see cref="StartScene.Centred"/>).
    /// </summary>
    public static RECT InHost(RECT host, StartScene scene, (int Width, int Height) size)
    {
        var gap = scene.Gap;
        var frame = size.Width + 2 * gap;
        var left = (scene.Centred ? host.left + (host.right - host.left - frame) / 2 : host.left) + gap;
        var foot = host.bottom - gap;
        return new RECT { left = left, top = foot - size.Height, right = left + size.Width, bottom = foot };
    }

    /// <summary>
    /// The room Start may stand in on its display: the work area less Windows' gap all round — what the bars leave, as far
    /// from them and from the screen's ends as Windows keeps Start from its taskbar and the screen's ends.
    /// </summary>
    public static RECT Room(StartScene scene)
    {
        var (gap, work) = (scene.Gap, scene.Display.Work);
        return new RECT { left = work.left + gap, top = work.top + gap, right = work.right - gap, bottom = work.bottom - gap };
    }

    /// <summary>
    /// Where Start of <paramref name="size"/> belongs: against the inner side of the bar whose Start button opened it,
    /// Windows' gap from it, centred on the button along the bar, inside <see cref="Room"/>. Windows 11's two looks come
    /// from where the button stands: at a bar's start Start stands at that end, Windows' gap from the screen's end (its
    /// left-aligned Start); in a bar's middle, in the middle (its centred one). Where no bar of Karpo's on the display has a
    /// Start button: where Windows would stand it over a taskbar at the work area's end — at its left, or in its middle.
    /// A Start larger than the room stands at the room's start.
    /// </summary>
    public static RECT Target(StartScene scene, (int Width, int Height) size)
    {
        var gap = scene.Gap;
        var room = Room(scene);
        var (width, height) = size;
        int x, y;
        if (scene.Button is { } at)
        {
            var (button, bar) = (at.Button, at.Bar);
            var (alongX, alongY) = (Middle(button.left, button.right) - width / 2, Middle(button.top, button.bottom) - height / 2);
            (x, y) = at.Edge switch
            {
                Edge.Top => (alongX, bar.bottom + gap),
                Edge.Left => (bar.right + gap, alongY),
                Edge.Right => (bar.left - gap - width, alongY),
                _ => (alongX, bar.top - gap - height),
            };
        }
        else
        {
            x = scene.Centred ? Middle(room.left, room.right) - width / 2 : room.left;
            y = room.bottom - height;
        }
        x = Within(x, width, room.left, room.right);
        y = Within(y, height, room.top, room.bottom);
        return new RECT { left = x, top = y, right = x + width, bottom = y + height };
    }

    private static int Middle(int start, int end) => start + (end - start) / 2;

    /// <summary><paramref name="start"/> of a run <paramref name="size"/> long kept between <paramref name="low"/> and <paramref name="high"/>; at <paramref name="low"/> where it is longer.</summary>
    private static int Within(int start, int size, int low, int high) => Math.Max(low, Math.Min(start, high - size));

    /// <summary>
    /// Where Start's <paramref name="host"/> goes for Start of <paramref name="size"/> to stand at <see cref="Target"/>; null
    /// where it is there already, or the host is not on its display. Its size is never changed. Never so far that the host
    /// stands as much on another display as on its own (<see cref="Keeps"/>): Windows would take Start to be on that one
    /// — another scale, the next hearing's display. Where the whole way would, Start goes as far towards its place as it
    /// may from where Windows would stand it with no bar of Karpo's (<see cref="Target"/> without a button), never
    /// outside the room.
    /// </summary>
    public static (int X, int Y)? Host(RECT host, StartScene scene, (int Width, int Height) size)
    {
        var bounds = scene.Display.Bounds;
        if (host.right <= host.left || host.bottom <= host.top || Overlap(host, bounds) == 0)
        {
            return null;
        }
        var (x, y) = Kept(host, scene, size);
        return x == host.left && y == host.top ? null : (x, y);
    }

    /// <summary>
    /// Where Start's search <paramref name="box"/> goes while Start's <paramref name="host"/> shows: where Start stands once
    /// the host is where <see cref="Host"/> puts it, less the gap all round. Null where it is there already, or the box is
    /// too small to be Start's frame.
    /// </summary>
    public static (int X, int Y)? Box(RECT box, RECT host, StartScene scene)
    {
        var gap = scene.Gap;
        if (SizeOfBox(box, gap) is not { } size)
        {
            return null;
        }
        if (Host(host, scene, size) is { } to)
        {
            host = Moved(host, to.X, to.Y);
        }
        var start = InHost(host, scene, size);
        var (x, y) = (start.left - gap, start.top - gap);
        return x == box.left && y == box.top ? null : (x, y);
    }

    /// <summary>
    /// Where the host goes: its top left corner that puts Start at <see cref="Target"/>; where that would hand the host to
    /// another display, the nearest to it of the furthest points it may reach from where Start stands with no bar to hang
    /// from (<c>plain</c>) — along one axis, the other at the target, or along both. Each of those has Start between two
    /// places inside the room, so inside it too. Worked out from where Windows lays Start out in the host, never from
    /// where the host happens to stand, so the same however often heard.
    /// </summary>
    private static (int X, int Y) Kept(RECT host, StartScene scene, (int Width, int Height) size)
    {
        var now = InHost(host, scene, size);
        var target = Target(scene, size);
        var (x, y) = (host.left + target.left - now.left, host.top + target.top - now.top);
        if (Keeps(Moved(host, x, y), scene))
        {
            return (x, y);
        }
        var plain = Target(scene with { Button = null }, size);
        var (fromX, fromY) = (host.left + plain.left - now.left, host.top + plain.top - now.top);
        // Against the bar first, as far along it as may be: Start then stands off the bar whatever else holds it back.
        var acrossFirst = scene.Button is { Edge: Edge.Left or Edge.Right } ? (x, fromY) : (fromX, y);
        if (scene.Button is not null && Furthest(host, scene, acrossFirst, (x, y)) is { } along)
        {
            return along;
        }
        (int X, int Y)? best = null;
        foreach (var start in new[] { (fromX, y), (x, fromY), (fromX, fromY) })
        {
            if (Furthest(host, scene, start, (x, y)) is { } reached
                && (best is not { } known || Math.Abs(reached.X - x) + Math.Abs(reached.Y - y) < Math.Abs(known.X - x) + Math.Abs(known.Y - y)))
            {
                best = reached;
            }
        }
        return best ?? (host.left, host.top);
    }

    /// <summary>The point nearest <paramref name="to"/> on the way there from <paramref name="from"/> (in 64 steps) at which the host keeps to its display; null where none.</summary>
    private static (int X, int Y)? Furthest(RECT host, StartScene scene, (int X, int Y) from, (int X, int Y) to)
    {
        const int Steps = 64;
        for (var step = Steps - 1; step >= 0; step--)
        {
            var (atX, atY) = (from.X + (int)((long)(to.X - from.X) * step / Steps), from.Y + (int)((long)(to.Y - from.Y) * step / Steps));
            if (Keeps(Moved(host, atX, atY), scene))
            {
                return (atX, atY);
            }
        }
        return null;
    }

    private static RECT Moved(RECT rect, int x, int y) =>
        new() { left = x, top = y, right = x + rect.right - rect.left, bottom = y + rect.bottom - rect.top };

    /// <summary>
    /// Whether <paramref name="host"/> keeps more of itself on its display than on any other: the display a window is on
    /// is the one holding most of it (MonitorFromWindow), and Start's own process goes by that.
    /// </summary>
    public static bool Keeps(RECT host, StartScene scene)
    {
        var own = Overlap(host, scene.Display.Bounds);
        foreach (var display in scene.Displays)
        {
            if (!Same(display.Bounds, scene.Display.Bounds) && Overlap(host, display.Bounds) >= own)
            {
                return false;
            }
        }
        return own > 0;
    }

    private static bool Same(RECT one, RECT other) =>
        one.left == other.left && one.top == other.top && one.right == other.right && one.bottom == other.bottom;

    internal static long Overlap(RECT one, RECT other) =>
        (long)Math.Max(0, Math.Min(one.right, other.right) - Math.Max(one.left, other.left))
        * Math.Max(0, Math.Min(one.bottom, other.bottom) - Math.Max(one.top, other.top));
}

/// <summary>
/// §666: Start's size as its search box last said it, in DIPs: what its host is placed by in the few ms between the host
/// showing and the box showing, on any display (Start is the same size in DIPs on each). Nothing before the first box.
/// </summary>
internal sealed class StartSize
{
    private (double Width, double Height)? dips;

    /// <summary>Start's search <paramref name="box"/> seen on a display at <paramref name="dpi"/>: its size kept.</summary>
    public (int Width, int Height)? Seen(RECT box, uint dpi)
    {
        if (StartPlacement.SizeOfBox(box, StartPlacement.Gap(dpi)) is not { } size || dpi == 0)
        {
            return null;
        }
        dips = (size.Width * 96.0 / dpi, size.Height * 96.0 / dpi);
        return size;
    }

    /// <summary>Start's last size in the pixels of a display at <paramref name="dpi"/>; null before any box was seen.</summary>
    public (int Width, int Height)? On(uint dpi) =>
        dips is { } known ? ((int)Math.Round(known.Width * dpi / 96), (int)Math.Round(known.Height * dpi / 96)) : null;
}
