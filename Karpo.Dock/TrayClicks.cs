// What a button on a tray icon comes to — a click its app hears, or a drag it never hears of —
// decided in one place for every tray: each tray of a bar, at rest, and the overflow.
//
// Windows' taskbar tells an icon's app of a click as the button comes up within the drag
// threshold of where it went down: the down, the up and what follows the up (NIN_SELECT,
// WM_CONTEXTMENU) then, together. The press itself sends nothing, so an icon picked up to be
// reordered or hidden never opens its app; a press that becomes a drag sends nothing at all.
// Karpo sent the down as the button went down (from the rewrite on, 2298bf5, and in the overflow
// from 61bba08): an app that opens on WM_LBUTTONDOWN opened under every drag, and one that came
// forward closed the overflow under the held icon (worked around by Flyout.Hold, 7e6305c).

using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>A click on a tray icon, sent whole as the button comes up (<see cref="TrayClicks"/>).</summary>
internal readonly record struct TrayClick(TrayIcon Icon, bool Right, bool Doubled)
{
    /// <summary>What the app hears, in order (<see cref="TrayMessages.For"/>): a second left click within the double-click time goes down as a double click.</summary>
    public TrayGesture[] Gestures =>
        Right ? [TrayGesture.RightDown, TrayGesture.RightUp]
        : [Doubled ? TrayGesture.LeftDoubleDown : TrayGesture.LeftDown, TrayGesture.LeftUp];
}

/// <summary>
/// The button held on a tray icon, on a bar or in the overflow, and what it comes to: a click as it
/// comes up on the same icon (<see cref="Released"/>), or nothing once it has moved past the drag
/// threshold (<see cref="Moved"/>, <see cref="Dragged"/>). A press sends nothing. The bars' icons
/// and the overflow's share the one record, as Windows' taskbar and its overflow share the one mouse.
/// </summary>
internal sealed class TrayClicks
{
    private sealed class Held(TrayIcon icon, bool right, bool doubled, POINT at)
    {
        public TrayIcon Icon { get; } = icon;

        public bool Right { get; } = right;

        public bool Doubled { get; } = doubled;

        public POINT At { get; } = at;

        public bool Dragged { get; set; }
    }

    private Held? held;

    // The last left click, to make a second one a double click.
    private (string Key, uint At)? last;

    /// <summary>Whether a press is held that may still be a click.</summary>
    public bool Holding => held is { Dragged: false };

    /// <summary>
    /// A button went down on <paramref name="icon"/> at <paramref name="at"/> (screen px) at
    /// <paramref name="now"/> (ms): kept, and nothing sent. A second left press on the same icon within
    /// <paramref name="doubleClickTime"/> ms of the last click is a double click.
    /// </summary>
    public void Pressed(TrayIcon icon, bool right, POINT at, uint now, uint doubleClickTime)
    {
        var doubled = !right && Dock.Doubled(ref last, icon.Key, now, doubleClickTime);
        if (right)
        {
            last = null;
        }
        held = new Held(icon, right, doubled, at);
    }

    /// <summary>
    /// The pointer moved to <paramref name="at"/> (screen px) with the left button held: past
    /// <paramref name="dragX"/> or <paramref name="dragY"/> (SM_CXDRAG, SM_CYDRAG) it is a drag, and
    /// stays one. True while it is; no click comes of it.
    /// </summary>
    public bool Moved(POINT at, int dragX, int dragY)
    {
        if (held is not { Right: false } press)
        {
            return false;
        }
        if (!press.Dragged && Math.Abs(at.x - press.At.x) <= dragX && Math.Abs(at.y - press.At.y) <= dragY)
        {
            return false;
        }
        Dragged();
        return true;
    }

    /// <summary>The press became a drag (the overflow tells it so itself): no click, and the next press starts a pair of its own.</summary>
    public void Dragged()
    {
        if (held is { } press)
        {
            press.Dragged = true;
        }
        last = null;
    }

    /// <summary>
    /// The button came up on the icon <paramref name="key"/>: the click to send, where it went down
    /// on that icon with that button and was not dragged; null otherwise. Either way the press is over.
    /// </summary>
    public TrayClick? Released(string? key, bool right)
    {
        var press = held;
        held = null;
        return press is { Dragged: false } && press.Right == right && key is not null && press.Icon.Key == key
            ? new TrayClick(press.Icon, press.Right, press.Doubled)
            : null;
    }

    /// <summary>The press is off without a release of its own (Escape, the mouse lost, a press elsewhere).</summary>
    public void Forget() => held = null;
}
