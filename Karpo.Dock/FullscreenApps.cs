// Whether the bars on a display should make way for the window in front of it.
//
// The bars hide on a display while a full screen app is in front there, as Windows' own taskbar does
// — hidden, not only put behind, so a game has the display to itself (independent flip, and no window
// over an exclusive full screen one to make it give the display up). What decides it is Karpo's own
// look at the window (Fills), never the shell's ABN_FULLSCREENAPP alone: the shell counts the desktop
// (Win+D, the show desktop corner) and Task view as full screen apps, and says nothing of which display.
//
// Each display has a front window: the last app window that came to the front shown on it (BarKeeper
// keeps them, from the foreground; Fronts). A display is full screen while its front window is shown, not
// minimized or cloaked, an app's (not the desktop, a bar, the shell's views, a band over the apps'),
// and covers the whole display as it is now — or, while the foreground app runs Direct3D full screen
// (SHQueryUserNotificationState), whatever its rectangle says on the way into its display mode. A
// maximized window with a caption is never full screen, though with no strip held it covers the
// display. So a video full screen on one display keeps its bars away while the person works on the
// other, and comes back the moment a window there comes to the front, the video ends, or it leaves
// full screen. A Chromium window in full screen that lost the foreground stands one pixel row short of
// its display, and is full screen still (StandsBack, §708). The decisions are pure and live here, so
// they can be tested without a screen.
//
// Task view is the shell's own view over the display (Overlays), and so is any window in a band over
// the apps' (IsAppBand, WindowBand.cs): never an app to make way for, nor a display's front.

using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>
/// What the bars need to know of a window to tell whether it is a display's full screen app
/// (<see cref="FullscreenApps.Fills"/>): read off the window as it is now (FrontWatch), made up in the tests.
/// </summary>
/// <param name="Class">Its window class.</param>
/// <param name="Display">The display it is on (MonitorFromWindow's device name, \\.\DISPLAYn).</param>
/// <param name="Rect">Where it is, in screen px.</param>
/// <param name="DisplayBounds">That display as it is now, in screen px: after a change of display mode, the new mode's.</param>
/// <param name="Band">Its z-band (GetWindowBand); null where it could not be read.</param>
/// <param name="Visible">IsWindowVisible.</param>
/// <param name="Minimized">IsIconic.</param>
/// <param name="Cloaked">DWM keeps it out of sight: another virtual desktop's, a suspended app's.</param>
/// <param name="Maximized">IsZoomed.</param>
/// <param name="Captioned">It has a caption (WS_CAPTION).</param>
/// <param name="Own">One of Karpo's own windows.</param>
public readonly record struct WindowFacts(
    string Class,
    string Display,
    RECT Rect,
    RECT DisplayBounds,
    uint? Band,
    bool Visible,
    bool Minimized,
    bool Cloaked,
    bool Maximized,
    bool Captioned,
    bool Own);

public static class FullscreenApps
{
    /// <summary>
    /// §692 (the person, 2026-11-12: "right now karpo is not actually showing at all"): whether a window is Karpo's own —
    /// this dock's (<paramref name="ownProcess"/>), or any Karpo's, its class one of Karpo's ("karpo.dock", "karpo.flyout"…):
    /// another Karpo running beside this one (a stage, a test's dock) is never a full screen app to make way for. Its flyout
    /// as big as a display hid every bar here, and they stayed hidden once it was gone.
    /// </summary>
    public static bool IsKarpos(string className, bool ownProcess) =>
        ownProcess || className.StartsWith("karpo.", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <paramref name="window"/>, come to the front, becomes its display's front window: an
    /// app's, in an app's band, shown on that display. Karpo's own windows (a flyout, a menu, a bar in
    /// edit mode), the shell's views (Task view, Start, Alt+Tab's stand-in) and windows in the system's
    /// bands come and go over whatever is there, and leave the display's front as it was. So does a
    /// window in front that nobody sees there — hidden, minimized, cloaked, or parked off the screen
    /// (MonitorFromWindow names the nearest display for it, the leftmost for one at -32000): an app in
    /// the background takes the foreground with one now and then, and it took a full screen video's
    /// display from it, the bars coming back over the video a while after the person clicked away.
    /// </summary>
    public static bool Fronts(WindowFacts window) =>
        !window.Own && !IsShellOverlay(window.Class) && IsAppBand(window.Band) && window.Display.Length > 0
        && window.Visible && !window.Minimized && !window.Cloaked && Meets(window.Rect, window.DisplayBounds);

    /// <summary>
    /// Whether <paramref name="window"/>, a display's front window, is a full screen app there: shown,
    /// not minimized or cloaked, an app's and not the desktop or a bar, and covering the whole display
    /// as it is now — unless it is maximized with a caption, which covers the display only because
    /// nothing holds a strip on it. <paramref name="direct3D"/>: it is the foreground window and
    /// Windows says a Direct3D app runs full screen, whatever its rectangle says on the way into its
    /// display mode.
    /// </summary>
    public static bool Fills(WindowFacts window, bool direct3D = false) =>
        window.Visible && !window.Minimized && !window.Cloaked && !window.Own
        && !IsShellBackdrop(window.Class) && IsAppBand(window.Band)
        && (direct3D || (Covers(window.Rect, window.DisplayBounds) && !(window.Maximized && window.Captioned)) || StandsBack(window));

    /// <summary>
    /// §708 (the person, 2026-11-20: "karpo is currently moving over my fullscreen video sometimes. the last 5mins like 100
    /// times"): whether <paramref name="window"/> is a Chromium window in full screen that lost the foreground to a window
    /// on its own display. appbar.log, 2026-11-20 18:09:02 to 18:12:27, a video full screen in Brave on DISPLAY1: "full screen
    /// app gone: its bars come back (its window left full screen: at 0,0..3840,2159)" thirty-five times, the video "in front"
    /// at 0,0..3840,2160 again half a second to a second after each. Chromium does it on purpose
    /// (HWNDMessageHandler::PostProcessActivateMessage, its background_fullscreen_hack_): a full screen window that loses
    /// activation to a window on the same monitor is made one pixel shorter at the bottom, so that Windows' taskbar stops
    /// counting its thread as full screen, and gets its whole display back when it is activated again. The window that took
    /// the foreground need not be in sight there: the desktop (it spans every display and counts as the primary's — a click
    /// on the other display's desktop or on a widget there, §706), a widget under the video (§702), a hidden helper window.
    /// The video still covers the display, all but that row, and is still its front, so it keeps the display; a window
    /// that does come over it takes the display's front as ever (BarKeeper.Look).
    /// <para>
    /// Told by what only Chromium makes: its window class (Chrome_WidgetWin_0, _1: Brave, Chrome, Edge, Electron), no
    /// caption, and the display's rectangle exactly, less the one row. Never by the shape alone: Themia's widget surface
    /// is a display less its last row too, on purpose, and is no full screen app.
    /// </para>
    /// <para>
    /// §709 (the person, 2026-11-20, an hour after §708 was installed: "karpo again is moving itself over a fullscreen
    /// view"): maximized or not. appbar.log 18:58–19:08, the same line as before, over fifty times. Chromium's full screen
    /// takes the caption and the sizing frame off its window and leaves WS_MAXIMIZE as it was (FullscreenHandler), so a
    /// browser that was maximized when its video went full screen — the usual way — is zoomed all through: the window
    /// read off the screen then, style 170B0000, IsZoomed. §708 asked for a window not maximized and so never held for
    /// it. A maximized window that is no full screen app has its caption (Fills), or stands at the work area and beyond,
    /// never at the display less one row.
    /// </para>
    /// </summary>
    public static bool StandsBack(WindowFacts window) =>
        window.Class.StartsWith(Chromium, StringComparison.OrdinalIgnoreCase)
        && !window.Captioned
        && window.DisplayBounds.right > window.DisplayBounds.left && window.DisplayBounds.bottom - 1 > window.DisplayBounds.top
        && window.Rect.left == window.DisplayBounds.left && window.Rect.top == window.DisplayBounds.top
        && window.Rect.right == window.DisplayBounds.right && window.Rect.bottom == window.DisplayBounds.bottom - 1;

    /// <summary>§708: what every top-level window of Chromium's own (views) is classed, a number after it.</summary>
    private const string Chromium = "Chrome_WidgetWin_";

    /// <summary>§714: whether <paramref name="windowClass"/> is the desktop's own (Progman, a WorkerW): what Show desktop raises.</summary>
    public static bool IsDesktop(string windowClass) =>
        windowClass.Equals("Progman", StringComparison.OrdinalIgnoreCase) || windowClass.Equals("WorkerW", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The shell's own backdrops and bars, which are never an app to make way for. Progman and
    /// WorkerW are the desktop (the wallpaper host, and the layer icons sit on); the tray windows
    /// are Explorer's taskbars, which cover a display's edge and never the display.
    /// </summary>
    private static readonly string[] Shell =
    [
        "progman",
        "workerw",
        "shell_traywnd",
        "shell_secondarytraywnd",
        // Karpo's own bars, so one Karpo window never hides another.
        "karpo.dock",
    ];

    /// <summary>
    /// The shell's own views that cover the display with the taskbar left over them: Task view
    /// (Win+Tab), and the switcher and snap layouts that share its window on Windows 11
    /// (XamlExplorerHostIslandWindow, Explorer's); Windows 10's Task view (MultitaskingViewFrame).
    /// Task view comes up as a window over the whole display, in front, so it read as a full
    /// screen app and the bar hid itself for it — where Windows' own taskbar stays.
    /// </summary>
    private static readonly string[] Overlays =
    [
        "xamlexplorerhostislandwindow",
        "multitaskingviewframe",
        // Explorer's stand-in that holds the front while Task view or Alt+Tab opens and closes,
        // in the system tools band (seen 2026-10-11, empty and unshown at rest).
        "foregroundstaging",
    ];

    /// <summary>Whether <paramref name="className"/> is the desktop or a bar rather than an app's window.</summary>
    public static bool IsShellBackdrop(string className) =>
        Array.Exists(Shell, name => string.Equals(name, className, StringComparison.OrdinalIgnoreCase))
        || IsShellOverlay(className);

    /// <summary>
    /// Whether <paramref name="className"/> is one of the shell's views the taskbar stays over
    /// (<see cref="Overlays"/>): never an app to make way for.
    /// </summary>
    public static bool IsShellOverlay(string className) =>
        Array.Exists(Overlays, name => string.Equals(name, className, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether <paramref name="window"/> covers <paramref name="monitor"/>: every edge of the
    /// display is inside it. A maximised window stops short of the bar and so does not count;
    /// a full screen one runs to all four edges, and beyond them on some apps.
    /// </summary>
    public static bool Covers(RECT window, RECT monitor) =>
        monitor.right > monitor.left && monitor.bottom > monitor.top
        && window.left <= monitor.left && window.top <= monitor.top
        && window.right >= monitor.right && window.bottom >= monitor.bottom;

    /// <summary>Whether <paramref name="window"/> has any part on <paramref name="monitor"/>.</summary>
    public static bool Meets(RECT window, RECT monitor) =>
        window.left < monitor.right && window.right > monitor.left
        && window.top < monitor.bottom && window.bottom > monitor.top;

    /// <summary>
    /// Whether a window in z-band <paramref name="band"/> (GetWindowBand; null where it could not
    /// be read) can be an app's: ZBID_DEFAULT or ZBID_DESKTOP, where every app's window is, full
    /// screen ones and games included. Higher bands are the system's own — Task view, Alt+Tab, the
    /// lock screen, Start — never an app to make way for, and over the bar whatever it does.
    /// </summary>
    public static bool IsAppBand(uint? band) => band is null or <= 1;
}
