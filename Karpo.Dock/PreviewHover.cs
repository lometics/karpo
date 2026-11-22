// preview: when the window list opens and closes by pointing, as rules over two timers.
//
// With "Window previews" on hover, the pointer coming to rest on an open app's button opens its
// window list after a short guard, the pointer sliding to the next app's button while the list is up
// swaps it at once, and the list closes a moment after the pointer has left both the button and the
// list. Those used to be timer calls spread through the dock's hover handlers, with a 420 ms wait
// (about Windows' 400 ms hover time) before anything showed, which read as the list lagging behind
// the pointer. The rules live here now, against a timer seam, so a test can hold them with a clock of
// its own and the dock only says where the pointer is and opens or closes the list when told.
//
// Those rules hear the pointer through window messages, and a leave they never hear keeps the list
// up under nothing ("sometimes the window preview pops up and does not go away again although i am
// not there with my mouse anymore"): WM_MOUSELEAVE not coming (a bar or a list whose tracking was
// lost, capture held, a list laid out again under a pointer that never moved), or a pointer counted
// in the list or on its button that is long gone. So while a list is up a third timer looks where
// the pointer really is, every WatchMs, and closes the list once it has been away from both the
// button and the list for the grace; the messages still close it on time, and this only makes sure.

namespace Karpo.Dock;

/// <summary>The window list's timers: the wait before it opens, the grace before it closes, and the look at the pointer while it is up.</summary>
public enum PreviewTimer
{
    Open,
    Close,
    Watch,
}

/// <summary>Where <see cref="PreviewHover"/> arms and kills its timers: the dock's window, or a test's clock.</summary>
public interface IPreviewTimers
{
    /// <summary>Arms <paramref name="timer"/> to go off once, <paramref name="ms"/> from now; one already armed starts again.</summary>
    void Arm(PreviewTimer timer, uint ms);

    /// <summary>Stops <paramref name="timer"/>; nothing where it isn't armed.</summary>
    void Kill(PreviewTimer timer);
}

/// <summary>
/// The rules the window list follows under the pointer. Holds no state of its own but the watch's
/// (on or not, and how long it has found the pointer away): what is open is passed in, and what comes back is whether the
/// list should swap now; the waits go through <see cref="IPreviewTimers"/>, and the dock opens the
/// list when <see cref="PreviewTimer.Open"/> goes off, asks <see cref="Stays"/> when
/// <see cref="PreviewTimer.Close"/> does, and <see cref="Watched"/> when <see cref="PreviewTimer.Watch"/> does.
/// </summary>
public sealed class PreviewHover(IPreviewTimers timers)
{
    /// <summary>
    /// The pointer rests on an app's button this long before its list opens. Only a guard, so that
    /// a pointer merely crossing the bar on its way to the tray, the clock or Start flashes nothing:
    /// at the speed a mouse crosses a screen (one to two and a half px a ms) a 40–48 px button is
    /// passed in about 20–50 ms, and 80 ms outlasts that, while staying under the 100 ms or so below
    /// which a response reads as instant. Windows' own hover time (400 ms, SPI_GETMOUSEHOVERTIME)
    /// is what this used to wait, and it was the whole of the delay people saw.
    /// </summary>
    public const uint OpenMs = 80;

    /// <summary>
    /// The list closes this long after the pointer left both the button and the list: enough to
    /// cross the gap from the button up into the list (its bridge is the list's already), or to clip
    /// a corner on the way, without it going.
    /// </summary>
    public const uint CloseMs = 380;

    /// <summary>
    /// How often the pointer is looked at while a list is up (<see cref="Watched"/>): a cursor
    /// position and two rectangles, ten times a second, and only while a list shows.
    /// </summary>
    public const uint WatchMs = 100;

    // Whether the watch is on, and the looks in a row that found the pointer away from the button
    // and the list.
    private bool watching;
    private int away;

    /// <summary>
    /// The pointer came onto the button of tile <paramref name="tile"/>, an app with windows open.
    /// <paramref name="openTile"/> is the tile whose list this bar has up, null when none is: with
    /// one up, the next button's list replaces it at once, without the guard, as the taskbar's
    /// thumbnails move between buttons. True when the list should be shown for
    /// <paramref name="tile"/> now; false when it already is, or when the guard is waiting.
    /// </summary>
    public bool OnButton(int tile, int? openTile)
    {
        timers.Kill(PreviewTimer.Close);
        if (openTile is { } open)
        {
            timers.Kill(PreviewTimer.Open);
            return open != tile;
        }
        timers.Arm(PreviewTimer.Open, OpenMs);
        return false;
    }

    /// <summary>
    /// The pointer is somewhere else: off the bar, or on a part of it that has no list (a pin with
    /// nothing open, a widget, the bare panel). Nothing waits to open any more, and a list this bar
    /// has up (<paramref name="listOpen"/>) goes after the grace.
    /// </summary>
    public void Off(bool listOpen)
    {
        timers.Kill(PreviewTimer.Open);
        if (listOpen)
        {
            timers.Arm(PreviewTimer.Close, CloseMs);
        }
    }

    /// <summary>The pointer came into the list: it stays, however long the pointer does.</summary>
    public void Entered() => timers.Kill(PreviewTimer.Close);

    /// <summary>The pointer left the list: it goes after the grace, unless the pointer is back by then.</summary>
    public void LeftList() => timers.Arm(PreviewTimer.Close, CloseMs);

    /// <summary>Nothing waits to open or close: the list went another way, or a press or a bar going away ended it all.</summary>
    public void Stop()
    {
        timers.Kill(PreviewTimer.Open);
        timers.Kill(PreviewTimer.Close);
        StopWatching();
    }

    /// <summary>
    /// A list came up, or was laid out again: the pointer is looked at from now on, as long as it is
    /// up. A list laid out again while watched keeps its watch as it was, count and beat: an app
    /// renaming its window every moment must not keep the list up by it.
    /// </summary>
    public void Shown()
    {
        if (watching)
        {
            return;
        }
        watching = true;
        away = 0;
        timers.Arm(PreviewTimer.Watch, WatchMs);
    }

    /// <summary>
    /// <see cref="PreviewTimer.Watch"/> went off. <paramref name="listOpen"/> is whether this bar's
    /// list is still up, <paramref name="home"/> whether the pointer is, as the cursor says and not as
    /// the last message did, in the list or on the button it lists. True when the list should close
    /// now: the pointer has been away for at least the grace, whatever messages did or did not come.
    /// A look comes up to <see cref="WatchMs"/> after the pointer left, so the grace is counted from
    /// the second look on: never sooner than <see cref="CloseMs"/>, at most a look later. The watch
    /// stops with the list.
    /// </summary>
    public bool Watched(bool listOpen, bool home)
    {
        if (!listOpen)
        {
            StopWatching();
            return false;
        }
        away = home ? 0 : away + 1;
        if (away > 0 && (away - 1) * WatchMs >= CloseMs)
        {
            Stop();
            return true;
        }
        timers.Arm(PreviewTimer.Watch, WatchMs);
        return false;
    }

    private void StopWatching()
    {
        timers.Kill(PreviewTimer.Watch);
        watching = false;
        away = 0;
    }

    /// <summary>
    /// Whether the list stays when its grace runs out: the pointer is in the list
    /// (<paramref name="insideList"/>) or back on the very button it lists
    /// (<paramref name="onItsButton"/>). Anything else lets it go.
    /// </summary>
    public static bool Stays(bool insideList, bool onItsButton) => insideList || onItsButton;
}
