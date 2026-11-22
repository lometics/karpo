// Whether a display's front window is a full screen app its bars make way for (Karpo.Dock\FullscreenApps.cs).
//
// The regressions these are for: showing the desktop puts the desktop itself in front, covering the
// display, and the shell reports that to an appbar as a full screen app — so the bar hid itself on
// every Win+D and every click of a show desktop corner; Task view did the same. And Anno 1800 (§272):
// its exclusive full screen switched the display to 640x480, where the game's window covered the
// display as it was then and not as the bar had last listed it, so the bar never made way.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.DesktopsArea;

public sealed class FullscreenTests
{
    private static RECT Monitor => new() { left = 0, top = 0, right = 2560, bottom = 1440 };

    private static RECT Rect(int left, int top, int right, int bottom) =>
        new() { left = left, top = top, right = right, bottom = bottom };

    /// <summary>A window of class <paramref name="className"/> at <paramref name="rect"/> on a display at <paramref name="display"/> (the 2560x1440 one unless said).</summary>
    private static WindowFacts Window(string className, RECT rect, RECT? display = null, uint? band = 1, bool maximized = false, bool captioned = false) =>
        new(className, @"\\.\DISPLAY1", rect, display ?? Monitor, band, Visible: true, Minimized: false, Cloaked: false, maximized, captioned, Own: false);

    [Theory]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    [InlineData("workerw")]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    [InlineData("karpo.dock")]
    public void TheDesktopAndTheBarsAreNotApps(string className) =>
        Assert.True(FullscreenApps.IsShellBackdrop(className));

    [Theory]
    [InlineData("Chrome_WidgetWin_1")]
    [InlineData("CabinetWClass")]
    [InlineData("UnityWndClass")]
    [InlineData("")]
    public void AnAppsWindowIs(string className) =>
        Assert.False(FullscreenApps.IsShellBackdrop(className));

    /// <summary>The whole point: showing the desktop must not take the bar with it.</summary>
    [Fact]
    public void ShowingTheDesktopIsNeverAFullScreenApp()
    {
        // The desktop covers the display exactly, which on its own looks full screen.
        Assert.True(FullscreenApps.Covers(Monitor, Monitor));
        Assert.False(FullscreenApps.Fills(Window("WorkerW", Monitor)));
        Assert.False(FullscreenApps.Fills(Window("Progman", Monitor)));
        // Nor is it one under Direct3D's word.
        Assert.False(FullscreenApps.Fills(Window("Progman", Monitor), direct3D: true));
    }

    [Fact]
    public void AFullScreenAppTakesTheBarAway()
    {
        Assert.True(FullscreenApps.Fills(Window("UnityWndClass", Monitor)));
        // Some apps run past the edges; still full screen.
        Assert.True(FullscreenApps.Fills(Window("UnityWndClass", Rect(-8, -8, 2568, 1448))));
    }

    /// <summary>A maximised window stops at the bar, so it is not full screen and the bar stays.</summary>
    [Fact]
    public void AMaximisedWindowLeavesTheBarAlone()
    {
        Assert.False(FullscreenApps.Fills(Window("CabinetWClass", Rect(0, 0, 2560, 1400), maximized: true, captioned: true)));
        Assert.False(FullscreenApps.Covers(Rect(0, 0, 2560, 1400), Monitor));
    }

    /// <summary>
    /// With no strip held (a bar over the windows, or one that comes in on hover), a maximised window
    /// covers the whole display, borders and all: with its caption it is still a window, never a full
    /// screen app. A browser's own full screen (F11, a video) drops the caption, and is one.
    /// </summary>
    [Fact]
    public void AMaximisedWindowWithACaptionIsNeverFullScreenEvenWhereItCoversTheDisplay()
    {
        var maximized = Rect(-8, -8, 2568, 1448);
        Assert.False(FullscreenApps.Fills(Window("Chrome_WidgetWin_1", maximized, maximized: true, captioned: true)));
        Assert.True(FullscreenApps.Fills(Window("Chrome_WidgetWin_1", Monitor, maximized: true, captioned: false)));
        Assert.True(FullscreenApps.Fills(Window("Chrome_WidgetWin_1", Monitor, maximized: false, captioned: true)));
    }

    /// <summary>
    /// A full screen app on another display is no reason for this display's bar to go: the window
    /// is measured against the display it is on.
    /// </summary>
    [Fact]
    public void AnotherDisplaysFullScreenAppIsMeasuredOnItsOwn()
    {
        var other = Rect(-2560, 0, 0, 1440);
        Assert.False(FullscreenApps.Fills(Window("UnityWndClass", other)));
        Assert.True(FullscreenApps.Fills(Window("UnityWndClass", other, other)));
    }

    [Fact]
    public void ADisplayWithNoSizeNeverCounts()
    {
        Assert.False(FullscreenApps.Covers(Monitor, Rect(0, 0, 0, 0)));
        Assert.False(FullscreenApps.Fills(Window("UnityWndClass", Monitor, Rect(0, 0, 0, 0))));
    }

    /// <summary>
    /// Anno 1800's exclusive full screen: the display switched to 640x480 and the game's window with it.
    /// Measured against the display as it is now, it fills it; against the 2560x1440 the bar had last
    /// listed, it did not, and the bar stayed in front of the game until it gave the display up. On
    /// the way into the mode, Windows' word for Direct3D full screen is enough.
    /// </summary>
    [Fact]
    public void AnExclusiveFullScreenGameFillsItsDisplayInItsOwnMode()
    {
        var mode = Rect(0, 0, 640, 480);
        Assert.True(FullscreenApps.Fills(Window("Anno1800", mode, mode)));
        Assert.False(FullscreenApps.Fills(Window("Anno1800", mode, Monitor)));
        Assert.True(FullscreenApps.Fills(Window("Anno1800", mode, Monitor), direct3D: true));
    }

    /// <summary>Out of sight is never full screen: hidden, minimized, cloaked (another desktop's), or Karpo's own.</summary>
    [Fact]
    public void AWindowOutOfSightOrKarposOwnIsNeverFullScreen()
    {
        var shown = Window("UnityWndClass", Monitor);
        Assert.True(FullscreenApps.Fills(shown));
        Assert.False(FullscreenApps.Fills(shown with { Visible = false }));
        Assert.False(FullscreenApps.Fills(shown with { Minimized = true }, direct3D: true));
        Assert.False(FullscreenApps.Fills(shown with { Cloaked = true }));
        Assert.False(FullscreenApps.Fills(shown with { Own = true }));
    }

    /// <summary>
    /// Regression: Task view is Explorer's window over the whole display, in front, so the bar took
    /// it for a full screen app and hid — Windows' own taskbar stays over Task view.
    /// </summary>
    [Theory]
    [InlineData("XamlExplorerHostIslandWindow")]
    [InlineData("MultitaskingViewFrame")]
    public void TaskViewIsNeverAFullScreenApp(string className)
    {
        Assert.False(FullscreenApps.Fills(Window(className, Monitor)));
        Assert.False(FullscreenApps.Fills(Window(className, Rect(-8, -8, 2568, 1448))));
        Assert.True(FullscreenApps.IsShellBackdrop(className));
        Assert.True(FullscreenApps.IsShellOverlay(className.ToLowerInvariant()));
        // Nor does it become the display's front: what was in front stays so under it.
        Assert.False(FullscreenApps.Fronts(Window(className, Monitor)));
    }

    /// <summary>Only the shell's views are stood over again; the desktop, the bars and apps are not.</summary>
    [Theory]
    [InlineData("Progman")]
    [InlineData("Shell_TrayWnd")]
    [InlineData("karpo.dock")]
    [InlineData("Windows.UI.Core.CoreWindow")]
    [InlineData("ApplicationFrameWindow")]
    [InlineData("UnityWndClass")]
    [InlineData("")]
    public void OnlyTheShellsViewsAreOverlays(string className) =>
        Assert.False(FullscreenApps.IsShellOverlay(className));

    /// <summary>Explorer's stand-in that holds the front as Task view opens is one of the shell's views too.</summary>
    [Fact]
    public void ExplorersForegroundStagingIsAnOverlay()
    {
        Assert.True(FullscreenApps.IsShellOverlay("ForegroundStaging"));
        Assert.False(FullscreenApps.Fills(Window("ForegroundStaging", Monitor)));
    }

    /// <summary>
    /// A window in a band of the system's own (Task view, Alt+Tab, the lock screen: ZBID_SYSTEM_TOOLS
    /// and the like) is never an app to make way for, whatever its class, nor a display's front; one in
    /// an app's band, or whose band could not be read, is measured as before.
    /// </summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData(0u, true)]
    [InlineData(1u, true)]
    [InlineData(2u, false)]
    [InlineData(7u, false)]
    [InlineData(16u, false)]
    public void OnlyAnAppsBandMakesWay(uint? band, bool makesWay)
    {
        Assert.Equal(makesWay, FullscreenApps.IsAppBand(band));
        Assert.Equal(makesWay, FullscreenApps.Fills(Window("Windows.UI.Core.CoreWindow", Monitor, band: band)));
        Assert.Equal(makesWay, FullscreenApps.Fronts(Window("Windows.UI.Core.CoreWindow", Monitor, band: band)));
        // A maximised window does not, in any band.
        Assert.False(FullscreenApps.Fills(Window("CabinetWClass", Rect(0, 0, 2560, 1400), band: band, maximized: true, captioned: true)));
    }

    /// <summary>
    /// §708, the person (2026-11-20): "karpo is currently moving over my fullscreen video sometimes. the last 5mins like 100
    /// times". appbar.log 18:09–18:12: "its window left full screen: at 0,0..3840,2159". Chromium makes its full screen window
    /// one pixel shorter at the bottom while a window on the same display has the foreground: it is full screen still.
    /// </summary>
    [Theory]
    [InlineData("Chrome_WidgetWin_1")]
    [InlineData("Chrome_WidgetWin_0")]
    public void AChromiumFullScreenWindowOutOfTheForegroundIsFullScreenStill(string className)
    {
        var back = Window(className, Rect(0, 0, 2560, 1439));
        Assert.False(FullscreenApps.Covers(back.Rect, Monitor));
        Assert.True(FullscreenApps.StandsBack(back));
        Assert.True(FullscreenApps.Fills(back));
        // On its own display, measured against that one.
        var other = Rect(-3840, 0, 0, 2160);
        Assert.True(FullscreenApps.Fills(Window(className, Rect(-3840, 0, 0, 2159), other)));
        Assert.False(FullscreenApps.Fills(Window(className, Rect(-3840, 0, 0, 2159))));
        // Out of sight it is nothing, as any window.
        Assert.False(FullscreenApps.Fills(back with { Minimized = true }));
        Assert.False(FullscreenApps.Fills(back with { Visible = false }));
        Assert.False(FullscreenApps.Fills(back with { Cloaked = true }));
    }

    /// <summary>
    /// §709, the person (2026-11-20, §708 installed an hour before): "karpo again is moving itself over a fullscreen view".
    /// appbar.log 18:58–19:08: "its window left full screen: at 0,0..3840,2159" over fifty times more. The browser was
    /// maximized when its video went full screen, and Chromium's full screen leaves WS_MAXIMIZE on the window (read off
    /// the screen: style 170B0000, IsZoomed, no caption): §708 asked for a window not maximized and never held for it.
    /// </summary>
    [Theory]
    [InlineData("Chrome_WidgetWin_1")]
    [InlineData("Chrome_WidgetWin_0")]
    public void AMaximizedChromiumWindowGoneFullScreenAndOutOfTheForegroundIsFullScreenStill(string className)
    {
        var whole = Window(className, Monitor, maximized: true);
        Assert.True(FullscreenApps.Fills(whole));
        var back = whole with { Rect = Rect(0, 0, 2560, 1439) };
        Assert.True(FullscreenApps.StandsBack(back));
        Assert.True(FullscreenApps.Fills(back));
        // Out of full screen it is maximized with its caption again, past the display's edges: no full screen app.
        Assert.False(FullscreenApps.Fills(Window(className, Rect(-8, -8, 2568, 1448), maximized: true, captioned: true)));
        // Nor is another program's maximized window a row short.
        Assert.False(FullscreenApps.Fills(Window("WinUIDesktopWin32WindowClass", Rect(0, 0, 2560, 1439), maximized: true)));
    }

    /// <summary>
    /// §708: only what Chromium makes. Themia's widget surface is a display less its last row on purpose, and no full
    /// screen app; nor is a window with a caption, or one short by more than the row or on another edge.
    /// </summary>
    [Fact]
    public void OnlyChromiumsOwnRowShortWindowIsFullScreen()
    {
        var row = Rect(0, 0, 2560, 1439);
        Assert.False(FullscreenApps.Fills(Window("WinUIDesktopWin32WindowClass", row)));
        Assert.False(FullscreenApps.Fills(Window("UnityWndClass", row)));
        Assert.False(FullscreenApps.Fills(Window("Chrome_WidgetWin_1", row, captioned: true)));
        Assert.False(FullscreenApps.Fills(Window("Chrome_WidgetWin_1", row, maximized: true, captioned: true)));
        Assert.False(FullscreenApps.Fills(Window("Chrome_WidgetWin_1", Rect(0, 0, 2560, 1438))));
        Assert.False(FullscreenApps.Fills(Window("Chrome_WidgetWin_1", Rect(0, 0, 2559, 1440))));
        Assert.False(FullscreenApps.Fills(Window("Chrome_WidgetWin_1", Rect(0, 1, 2560, 1440))));
        Assert.False(FullscreenApps.Fills(Window("Chrome_WidgetWin_1", Rect(1, 0, 2560, 1439))));
        Assert.False(FullscreenApps.Fills(Window("Chrome_WidgetWin_1", Rect(0, 0, 2560, 1399), maximized: true, captioned: true)));
        // A display with no size, or one row high, never counts.
        Assert.False(FullscreenApps.Fills(Window("Chrome_WidgetWin_1", Rect(0, 0, 0, -1), Rect(0, 0, 0, 0))));
        Assert.False(FullscreenApps.Fills(Window("Chrome_WidgetWin_1", Rect(0, 0, 2560, 0), Rect(0, 0, 2560, 1))));
    }

    /// <summary>A window that covers nothing of the display, however it is classed.</summary>
    [Fact]
    public void ASmallWindowIsNotFullScreen()
    {
        Assert.False(FullscreenApps.Fills(Window("Chrome_WidgetWin_1", Rect(100, 100, 900, 700))));
    }

    /// <summary>Karpo's own windows — a flyout, a menu, a bar in edit mode — never become a display's front.</summary>
    [Fact]
    public void KarposOwnWindowsNeverBecomeADisplaysFront()
    {
        Assert.False(FullscreenApps.Fronts(Window("karpo.flyout", Rect(0, 1000, 400, 1400)) with { Own = true }));
        Assert.True(FullscreenApps.Fronts(Window("Notepad", Rect(0, 0, 400, 400))));
        // Nor does a window whose display could not be read.
        Assert.False(FullscreenApps.Fronts(Window("Notepad", Rect(0, 0, 400, 400)) with { Display = "" }));
    }

    /// <summary>A window in front that nobody sees on its display never becomes the display's front.</summary>
    [Fact]
    public void AWindowNobodySeesNeverBecomesADisplaysFront()
    {
        var notepad = Window("Notepad", Rect(0, 0, 400, 400));
        Assert.False(FullscreenApps.Fronts(notepad with { Visible = false }));
        Assert.False(FullscreenApps.Fronts(notepad with { Minimized = true }));
        Assert.False(FullscreenApps.Fronts(notepad with { Cloaked = true }));
        Assert.False(FullscreenApps.Fronts(notepad with { Rect = Rect(-32000, -32000, -31840, -31973) }));
        Assert.False(FullscreenApps.Fronts(notepad with { Rect = Rect(2560, 0, 2960, 400) }));
        Assert.True(FullscreenApps.Fronts(notepad with { Rect = Rect(2500, 0, 2900, 400) }));
    }

    /// <summary>
    /// §692 (the person, 2026-11-12: "right now karpo is not actually showing at all"): a window of another Karpo — a stage, a
    /// test's dock, its flyout as big as a display — is Karpo's own, never a full screen app the bars make way for; any other
    /// app's window is not.
    /// </summary>
    [Theory]
    [InlineData("karpo.flyout", false, true)]
    [InlineData("karpo.dock", false, true)]
    [InlineData("Karpo.Taskband", false, true)]
    [InlineData("Chrome_WidgetWin_1", true, true)]
    [InlineData("Chrome_WidgetWin_1", false, false)]
    [InlineData("UnrealWindow", false, false)]
    public void AnotherKarposWindowIsNeverAFullScreenApp(string className, bool ownProcess, bool karpos) =>
        Assert.Equal(karpos, FullscreenApps.IsKarpos(className, ownProcess));
}
