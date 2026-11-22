// What a click on an app does, wherever the app is: a pinned app or an open
// one on the bar, a pin in a folder (as a row, a list or a grid), a window's
// preview, a window's row in the desktop flyout, the app's line in its jump
// list. One rule, Windows' taskbar's, decided here and nowhere else:
//
// - not running, or asked for a new one (a middle click, the jump list's app
//   line): start it;
// - one window, in front: minimize it; one window, not in front (behind
//   others, or minimized): bring it to the front;
// - several windows: their thumbnails, nothing brought forward — the person
//   picks one, and a click on the button again puts the list away (Windows'
//   default). With Ctrl held, or where Windows' own LastActiveClick is 1, a
//   click walks through them instead: none in front, the most recently used
//   comes to the front; one of them in front, the next, the least recently
//   used, so clicking again reaches them all. Where no button shows the list
//   (a pin in a folder, which closes; Win+N), the walk is the rule.
//
// "In front" is the window in front, or the window that owns it: an app's
// dialog in front (Save As, Find) is the app in front (WindowOwners).
//
// "In front" is the window in front of every app's, which Karpo's own popups
// don't count as: a folder or a jump list takes the focus while it is open,
// and the app that had it is still the one the click is about (Dock.Front).
// §619: never a minimized window, and read as the button goes down, as
// Windows' taskbar reads its active button (ActiveButton, Dock.FrontAtPress).
// What a click does is done as Windows' taskbar does it: a restore and a
// minimize asked of the app (SC_RESTORE, SC_MINIMIZE) — one run as administrator
// too, which Windows lets those two reach (§662) —, done without it where it is
// hung or its window disabled, and the last active popup activated (Apps).
//
// app clicks: what the keys held add, as on Windows 11's taskbar (Asked): Shift
// — or the middle button — starts another whatever is open, and Ctrl+Shift
// starts another as administrator where the target is something that can run
// as one (CanElevate). And a right click opens the app's jump list, or with
// Shift held, on a button with windows open, the window's own menu — or its
// windows', for a button of several (MenuFor).

namespace Karpo.Dock;

/// <summary>What a click on an app entry does.</summary>
public enum AppClickKind
{
    /// <summary>Start the app (or open the file, folder or link): it has no window, or a new one was asked for.</summary>
    Launch,

    /// <summary>Bring <see cref="AppClick.Window"/> to the front, restoring it when minimized.</summary>
    Activate,

    /// <summary>Minimize <see cref="AppClick.Window"/>, the one in front.</summary>
    Minimize,

    /// <summary>Show the windows' thumbnails and bring nothing forward: a plain click on a button of several.</summary>
    ShowList,
}

/// <param name="Kind">What to do.</param>
/// <param name="Window">For Activate and Minimize: the index among the windows given to <see cref="AppClicks.Decide(IReadOnlyList{AppWindow}, nint, bool)"/>; -1 for Launch and ShowList.</param>
/// <param name="Elevated">For Launch: as administrator (Ctrl+Shift, <see cref="AppAsk.Elevated"/>).</param>
public readonly record struct AppClick(AppClickKind Kind, int Window = -1, bool Elevated = false);

/// <summary>app clicks: what a click asks for beyond the one rule, by the keys held and the button pressed.</summary>
public enum AppAsk
{
    /// <summary>Nothing more: the one rule decides.</summary>
    Usual,

    /// <summary>Another one, whatever is open: Shift, or the middle button.</summary>
    New,

    /// <summary>Another one, run as administrator: Ctrl+Shift.</summary>
    Elevated,

    /// <summary>The next of its windows rather than their list: Ctrl, as Windows 11's Ctrl+click walks a group.</summary>
    Cycle,
}

/// <summary>app clicks: which menu a right click on an app's button opens.</summary>
public enum AppMenuKind
{
    /// <summary>The app's jump list: its own lines, pinning, closing.</summary>
    JumpList,

    /// <summary>The window's own menu — restore, move, size, minimize, maximize, close —, for a button of one window.</summary>
    Window,

    /// <summary>Its windows' menu — restore all, minimize all, close all —, for a button of several.</summary>
    Windows,
}

/// <summary>One window of an app, as the rule sees it.</summary>
/// <param name="Handle">The window.</param>
/// <param name="Minimized">Whether it is minimized: then it is never the one in front.</param>
public readonly record struct AppWindow(nint Handle, bool Minimized);

public static class AppClicks
{
    /// <summary>
    /// What a click on an app with <paramref name="windows"/> (most recently used first) does, the
    /// window in front being <paramref name="front"/> (see the top of the file); with
    /// <paramref name="newInstance"/> it always starts another.
    /// </summary>
    public static AppClick Decide(IReadOnlyList<AppWindow> windows, nint front, bool newInstance = false) =>
        Decide(windows, front, newInstance ? AppAsk.New : AppAsk.Usual);

    /// <summary>
    /// What a click asking <paramref name="ask"/> does: another one, as administrator where it asks
    /// that, or else the one rule for <paramref name="windows"/> and <paramref name="front"/>: with
    /// <paramref name="list"/>, a plain click on several shows their list (<see cref="AppClickKind.ShowList"/>);
    /// without it, or with Ctrl (<see cref="AppAsk.Cycle"/>), it walks through them.
    /// </summary>
    public static AppClick Decide(IReadOnlyList<AppWindow> windows, nint front, AppAsk ask, bool list = false)
    {
        if (ask == AppAsk.Elevated)
        {
            return new AppClick(AppClickKind.Launch, Elevated: true);
        }
        if (ask == AppAsk.New || windows.Count == 0)
        {
            return new AppClick(AppClickKind.Launch);
        }
        var inFront = -1;
        for (var index = 0; index < windows.Count; index++)
        {
            if (front != 0 && windows[index].Handle == front && !windows[index].Minimized)
            {
                inFront = index;
                break;
            }
        }
        if (windows.Count == 1)
        {
            return new AppClick(inFront == 0 ? AppClickKind.Minimize : AppClickKind.Activate, 0);
        }
        if (list && ask != AppAsk.Cycle)
        {
            return new AppClick(AppClickKind.ShowList);
        }
        // Several: from the one in front on to the least recently used, which walks through them all.
        return new AppClick(AppClickKind.Activate, inFront >= 0 ? windows.Count - 1 : 0);
    }

    /// <summary>
    /// §619: the window whose button is the active one — what a click on it minimizes, and what its
    /// plate shows —, as Windows' taskbar keeps it: the window an app marked active itself
    /// (<paramref name="activeAlt"/>, ITaskbarList::ActivateTab/SetActiveAlt, 0 for none); else, while
    /// Karpo's own window has the foreground (<paramref name="ours"/>: a folder, a menu — the taskbar's own
    /// never counts as an app), the app window that had it last (<paramref name="mostRecent"/>); else
    /// the button the foreground window <paramref name="foreground"/> stands for (<paramref name="buttonOf"/>:
    /// itself, or the owner of its dialog). Never a minimized window (<paramref name="minimized"/>): the
    /// window minimized last may keep the foreground where nothing else takes it, and Windows' taskbar
    /// shows no button active then, and a click on it restores it. 0 for none.
    /// </summary>
    public static nint ActiveButton(nint foreground, bool ours, nint mostRecent, nint activeAlt, Func<nint, nint> buttonOf, Func<nint, bool> minimized)
    {
        var front = activeAlt != 0 ? activeAlt
            : ours ? mostRecent
            : foreground == 0 ? 0
            : buttonOf(foreground);
        return front != 0 && minimized(front) ? 0 : front;
    }

    /// <summary>
    /// What a click asks with these keys held, as Windows 11's taskbar reads them: Ctrl+Shift another
    /// one as administrator, Shift — or the middle button — another one, Ctrl the next window rather
    /// than the list, anything else the one rule.
    /// </summary>
    public static AppAsk Asked(bool middle, bool shift, bool control) =>
        shift && control ? AppAsk.Elevated
        : shift || middle ? AppAsk.New
        : control ? AppAsk.Cycle
        : AppAsk.Usual;

    /// <summary>
    /// Whether a plain click on a button of several windows shows their list: Windows' default, unless
    /// its LastActiveClick (Explorer\Advanced) is 1, which has every click walk them as Ctrl+click does.
    /// <paramref name="lastActiveClick"/> is that value, null where it is not set.
    /// </summary>
    public static bool Lists(uint? lastActiveClick) => lastActiveClick != 1;

    /// <summary>
    /// Whether <paramref name="target"/> can be started as administrator: a program, a shortcut
    /// (to one), a script, or an app of the Apps folder. A document, a folder or a web address opens
    /// as it always does, as there is nobody to run it as administrator.
    /// </summary>
    public static bool CanElevate(string target)
    {
        var trimmed = target.Trim().Trim('"');
        if (trimmed.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return Path.GetExtension(trimmed).ToLowerInvariant() is ".exe" or ".lnk" or ".bat" or ".cmd" or ".msc";
    }

    /// <summary>
    /// Which menu a right click opens on a button with <paramref name="windows"/> open: with Shift
    /// held, the window's own menu for one and its windows' for several; else — or with none open —
    /// the jump list.
    /// </summary>
    public static AppMenuKind MenuFor(bool shift, int windows) =>
        !shift || windows <= 0 ? AppMenuKind.JumpList
        : windows == 1 ? AppMenuKind.Window
        : AppMenuKind.Windows;
}
