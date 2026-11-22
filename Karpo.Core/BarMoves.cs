// §534 (the person, 2026-11-07: "you should be able to lock the taskbar, on the right click menu"; "you should be able
// to drag and drop the taskbar to move it to the different edges of the screen"): Windows' "Lock the taskbar", and the
// bar dragged to another edge of its display while it is not locked.
//
// One setting, Karpo's, every bar's alike as Windows' is: "lockTaskbar", on (locked) in a new config, as Windows ships
// its taskbar. Its two homes write the one value: the bar's right click ("Lock the taskbar", ticked while locked,
// BarMenuAction.LockTaskbar) and the Taskbar page of the settings panel (LockRow, a switch under "Bar").
//
// What it locks is what Windows' lock locks, the bar's place and size, at rest (Arranges, Carries): a locked bar's grip
// along its inner edge drags nothing and shows no resize cursor — it stays the bar's room, where the bar's menu opens
// on a bar its widgets cover end to end — and a press on its empty room is only a press;
// unlocked, that press dragged past the system's drag threshold carries the bar to the edge of its display nearest the
// pointer (Nearest), the strip it would stand in shown as it goes (Landing), and the release writes that edge where the
// Display page's Position writes it (BarLooks.WriteEdge): one value, one path, saved and laid out as every change is.
// §663 (the person, 2026-11-10: "when the taskbar is locked dragging a widget should not go into edit mode anymore /
// should not be possible anymore. but dragging contents inside of widgets around should still work normally"): and its
// widgets, at rest (Arranges): a widget, face or pin pressed on a locked bar, or a line in a folder open from one, and
// dragged is no drag — no edit mode, nothing moved along the bar or out of the folder. What a widget drags itself (a
// slider's thumb, the tray's icons) is the widget's and was taken before the lock is asked.
// Edit mode is not locked: it is the way to arrange the bar on purpose, its grip drags the height there whatever the
// lock says, its widgets are carried as ever, and its empty room's drag draws the selection rectangle, so the bar is
// not dragged to an edge in it.
//
// Each display has its bar on an edge of its own (Screen.Edge): a bar dragged over another display stays on its own,
// on the edge of its own display nearest the pointer.

using System.Text.Json.Serialization;

namespace Karpo.Core;

public sealed partial class Config
{
    /// <summary>
    /// §534: Windows' "Lock the taskbar": no bar is dragged to another edge, nor its height dragged, at rest. On in a new
    /// config, as Windows' own taskbar comes locked. Every bar's alike; edit mode is not locked. Written last in the file
    /// (§576): this file compiles before Config.cs, so in declaration order it went above "version".
    /// </summary>
    [JsonPropertyOrder(1)]
    public bool LockTaskbar { get; set; } = true;
}

/// <summary>§534: the lock, and a bar dragged to another edge of its display; see the top of the file.</summary>
public static class BarMoves
{
    /// <summary>The Taskbar page's "Lock the taskbar" row.</summary>
    public const string LockRowKey = "LockTaskbar";

    /// <summary>"Lock the taskbar" set to <paramref name="locked"/>; false where it was.</summary>
    public static bool WriteLock(Config config, bool locked)
    {
        if (config.LockTaskbar == locked)
        {
            return false;
        }
        config.LockTaskbar = locked;
        return true;
    }

    /// <summary>Whether a press on a bar's empty room may carry it to another edge: unlocked, and at rest (edit mode's selects).</summary>
    public static bool Carries(bool locked, bool editing) => !locked && !editing;

    /// <summary>
    /// Whether a drag arranges the bar: its grip drags its height, §663 a widget pressed on it or in its folder is carried
    /// (into edit mode, along the bar, out of the folder). In edit mode always; at rest where it is not locked.
    /// </summary>
    public static bool Arranges(bool locked, bool editing) => editing || !locked;

    /// <summary>The Taskbar page's switch: the same setting as the bar's right click's row, in its words.</summary>
    public static OptionRow LockRow(Config config) => new(LockRowKey, OptionRowKind.Toggle, S.CoreBarMenuLockTaskbar)
    {
        Glyph = OptionGlyphs.Lock,
        Slot = OptionSlot.Behaviour,
        On = config.LockTaskbar,
        Set = on => WriteLock(config, on != 0),
    };

    /// <summary>
    /// The edge of <paramref name="display"/> (screen px) nearest the pointer at <paramref name="x"/>, <paramref name="y"/>,
    /// held to the display first, so a pointer over another display picks an edge of this one. Where two edges are as near
    /// (a corner, a square's middle) <paramref name="current"/> stays if it is one of them, else the first of
    /// <see cref="ScreenEdges.All"/>. In px of the screen, so the same pointer on the same display picks the same edge at
    /// any scale.
    /// </summary>
    public static Edge Nearest(RectI display, int x, int y, Edge current)
    {
        if (display.Width <= 0 || display.Height <= 0)
        {
            return current;
        }
        x = Math.Clamp(x, display.Left, display.Right - 1);
        y = Math.Clamp(y, display.Top, display.Bottom - 1);
        int Distance(Edge edge) => edge switch
        {
            Edge.Top => y - display.Top,
            Edge.Left => x - display.Left,
            Edge.Right => display.Right - 1 - x,
            _ => display.Bottom - 1 - y,
        };
        var nearest = ScreenEdges.All.Min(Distance);
        return Distance(current) == nearest && ScreenEdges.All.Contains(current)
            ? current
            : ScreenEdges.All.First(edge => Distance(edge) == nearest);
    }

    /// <summary>The strip of <paramref name="display"/> a bar <paramref name="thickness"/> px thick stands in along <paramref name="edge"/>.</summary>
    public static RectI Landing(RectI display, Edge edge, int thickness)
    {
        var across = Math.Clamp(thickness, 0, edge is Edge.Left or Edge.Right ? display.Width : display.Height);
        return edge switch
        {
            Edge.Top => display with { Bottom = display.Top + across },
            Edge.Left => display with { Right = display.Left + across },
            Edge.Right => display with { Left = display.Right - across },
            _ => display with { Top = display.Bottom - across },
        };
    }
}

/// <summary>
/// §534: a press on a bar's empty room, unlocked, from where it went down (screen px) on <paramref name="display"/>, whose
/// bar stands on <paramref name="from"/>. Past the drag threshold it is the bar carried (<see cref="Moving"/>), to the
/// edge nearest the pointer (<see cref="Target"/>); short of it, a press like any other, its release a click.
/// </summary>
public sealed class BarMove(RectI display, Edge from, int x, int y)
{
    public RectI Display { get; } = display;

    /// <summary>The edge the bar stood on when it was pressed.</summary>
    public Edge From { get; } = from;

    /// <summary>Whether the pointer has left the drag threshold: the bar is carried, and its release is no click.</summary>
    public bool Moving { get; private set; }

    /// <summary>The edge it lands on if let go now: where it stands until it moves.</summary>
    public Edge Target { get; private set; } = from;

    /// <summary>
    /// The pointer at <paramref name="atX"/>, <paramref name="atY"/>: past the system's drag threshold
    /// (<paramref name="dragX"/>, <paramref name="dragY"/>: SM_CXDRAG, SM_CYDRAG; a move of more than either, as every
    /// drag of Karpo's) the bar is carried from here on, to the edge nearest it. True where the bar began to move or
    /// its landing changed.
    /// </summary>
    public bool Step(int atX, int atY, int dragX, int dragY)
    {
        if (!Moving)
        {
            if (Math.Abs(atX - x) <= dragX && Math.Abs(atY - y) <= dragY)
            {
                return false;
            }
            Moving = true;
            Target = BarMoves.Nearest(Display, atX, atY, Target);
            return true;
        }
        var next = BarMoves.Nearest(Display, atX, atY, Target);
        if (next == Target)
        {
            return false;
        }
        Target = next;
        return true;
    }
}
