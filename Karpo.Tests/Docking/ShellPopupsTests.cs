// Windows' notifications and panels kept off the bars: where a window of one of Windows' panels goes when
// it reaches into a strip the work area leaves out — Start's own place is StartPlacementTests' (§666) —
// and how often one window is moved (Karpo.Dock\ShellPopups.cs, docs/widgets.md §540, §580). Pure: no
// window is ever moved here.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Docking;

public sealed class ShellPopupsTests
{
    private static RECT R(int left, int top, int right, int bottom) => new() { left = left, top = top, right = right, bottom = bottom };

    // The user's primary display, 3840x2160 at 150 %, and the notification's window as Windows placed it
    // there (seen 2026-11-07): flush in the bottom right corner, Explorer's taskbar auto-hidden.
    private static readonly RECT Display = R(0, 0, 3840, 2160);
    private static readonly RECT Toast = R(3246, 1848, 3840, 2160);

    [Fact]
    public void ANotificationOnABarAtTheRightGoesLeftByTheBarsThickness()
    {
        // Karpo's bar on the right holds 63 px (42 DIP at 150 %): the work area ends at 3777.
        Assert.Equal((3183, 1848), ShellPopupPlace.Clear(Toast, Display, R(0, 0, 3777, 2160)));
    }

    [Fact]
    public void ANotificationOnABarAtTheBottomGoesUpByTheBarsThickness()
    {
        Assert.Equal((3246, 1785), ShellPopupPlace.Clear(Toast, Display, R(0, 0, 3840, 2097)));
    }

    [Fact]
    public void AtAHundredPercentTheSameRuleHoldsInItsOwnPixels()
    {
        // 1920x1080, a 48 px bar at the bottom, a 396x208 window in the corner.
        Assert.Equal((1524, 824), ShellPopupPlace.Clear(R(1524, 872, 1920, 1080), R(0, 0, 1920, 1080), R(0, 0, 1920, 1032)));
    }

    [Theory]
    [InlineData(0, 63, 3840, 2160)] // a bar at the top
    [InlineData(63, 0, 3840, 2160)] // a bar at the left
    [InlineData(0, 0, 3840, 2160)] // no bar holding anything: a bar that hides, or none
    public void ABarAwayFromTheCornerMovesNothing(int left, int top, int right, int bottom)
    {
        Assert.Null(ShellPopupPlace.Clear(Toast, Display, R(left, top, right, bottom)));
    }

    [Fact]
    public void BarsAtTheRightAndTheBottomMoveItBothWays()
    {
        Assert.Equal((3183, 1785), ShellPopupPlace.Clear(Toast, Display, R(0, 0, 3777, 2097)));
    }

    [Fact]
    public void WindowsOwnGapToTheScreensEndIsKeptToTheWorkAreas()
    {
        // 12 px between the window and the screen's right end, so 12 px between it and the bar.
        Assert.Equal((3171, 1848), ShellPopupPlace.Clear(R(3234, 1848, 3828, 2160), Display, R(0, 0, 3777, 2160)));
    }

    [Fact]
    public void AWindowAlreadyClearOfTheBarStays()
    {
        Assert.Null(ShellPopupPlace.Clear(R(3183, 1848, 3777, 2160), Display, R(0, 0, 3777, 2160)));
    }

    [Theory]
    [InlineData(3246, 2160, 3840, 2472)] // a notification that went: below the screen
    [InlineData(3246, 2160, 3840, 2160)] // and then of no height
    [InlineData(3840, 1848, 4434, 2160)] // past the screen's right end
    public void AWindowOffTheScreenIsNeverBroughtBack(int left, int top, int right, int bottom)
    {
        Assert.Null(ShellPopupPlace.Clear(R(left, top, right, bottom), Display, R(0, 0, 3777, 2097)));
    }

    [Fact]
    public void AWindowOverTheWholeScreenIsLeftAlone()
    {
        Assert.Null(ShellPopupPlace.Clear(Display, Display, R(0, 0, 3777, 2160)));
        Assert.Null(ShellPopupPlace.Clear(Display, Display, R(0, 0, 3840, 2097)));
    }

    [Fact]
    public void ATallFlyoutStopsAtTheWorkAreasTop()
    {
        // The notification centre 2100 px tall, 12 px from the top; a 63 px bar at the bottom leaves it 2097:
        // the window does not fit, and stays.
        Assert.Null(ShellPopupPlace.Clear(R(3246, 12, 3840, 2112), Display, R(0, 0, 3840, 2097)));
        // 2090 px tall, 30 px above the screen's end: Windows' gap kept would take it past the work area's
        // top, so it goes up to that top, no further.
        Assert.Equal((3246, 0), ShellPopupPlace.Clear(R(3246, 40, 3840, 2130), Display, R(0, 0, 3840, 2097)));
        // Flush at the bottom, it fits with 7 px to spare.
        Assert.Equal((3246, 7), ShellPopupPlace.Clear(R(3246, 70, 3840, 2160), Display, R(0, 0, 3840, 2097)));
    }

    [Fact]
    public void ADisplayLeftOfThePrimaryWithABarAtItsLeftPushesRight()
    {
        // A window in that display's bottom left corner, 12 px in; a 63 px bar on its left.
        var display = R(-2560, 0, 0, 1440);
        Assert.Equal((-2485, 1100), ShellPopupPlace.Clear(R(-2548, 1100, -2148, 1440), display, R(-2497, 0, 0, 1440)));
    }

    // §580: the user's main display, 3840x2160 at 150 %, a 63 px bar (42 DIP) at its left or its right.
    private static readonly RECT LeftBar = R(63, 0, 3840, 2160);
    private static readonly RECT RightBar = R(0, 0, 3777, 2160);

    [Fact]
    public void AWindowPastTheScreensEndWhereNoBarStandsIsStillComingIn()
    {
        // An Explorer menu shown below the main display and moved up 5 ms later (seen 2026-11-09), with Karpo's bar at the
        // right: nothing at the bottom holds a strip, so it is left to come in.
        Assert.Null(ShellPopupPlace.Clear(R(2495, 2097, 2765, 2236), Display, RightBar));
        // With a bar at the bottom it stands on the bar's strip, and goes above it.
        Assert.Equal((2495, 1958), ShellPopupPlace.Clear(R(2495, 2097, 2765, 2236), Display, R(0, 0, 3840, 2097)));
    }

    [Fact]
    public void APanelOfItsOwnSizeStandingOnABarAtTheLeftGoesRightByTheBarsThickness()
    {
        // Search on its own (Win+S), 18 px (12 DIP) in from the display's left: as far from the bar.
        Assert.Equal((81, 900), ShellPopupPlace.Clear(R(18, 900, 1218, 2052), Display, LeftBar));
        // The Widgets board, from top to bottom at the left.
        Assert.Equal((81, 18), ShellPopupPlace.Clear(R(18, 18, 1218, 2052), Display, LeftBar));
        // The quick link menu (Win+X), where Explorer keeps it (seen 2026-11-09, hidden: 3,1248 339x849 at 150 %).
        Assert.Equal((66, 1248), ShellPopupPlace.Clear(R(3, 1248, 342, 2097), Display, LeftBar));
    }

    [Fact]
    public void APanelAtTheRightStandingOnABarAtTheRightGoesLeftByTheBarsThickness()
    {
        // Quick settings and the notification centre, 18 px (12 DIP) in from the display's right.
        Assert.Equal((2637, 1100), ShellPopupPlace.Clear(R(2700, 1100, 3822, 2070), Display, RightBar));
        Assert.Equal((2637, 18), ShellPopupPlace.Clear(R(2700, 18, 3822, 2070), Display, RightBar));
    }

    [Fact]
    public void APanelAwayFromTheBarStays()
    {
        Assert.Null(ShellPopupPlace.Clear(R(2700, 1100, 3822, 2070), Display, LeftBar));
        Assert.Null(ShellPopupPlace.Clear(R(18, 18, 1218, 2052), Display, RightBar));
    }

    [Fact]
    public void AWindowAsWideAsTheDisplayIsNoPanelsOwnAndStays()
    {
        // Start's host is placed by StartPlacement (§666); any other window over the whole display stays.
        Assert.True(ShellPopupPlace.Spans(R(0, 0, 3840, 2088), Display));
        Assert.False(ShellPopupPlace.Spans(R(0, 753, 1287, 2088), Display));
        Assert.Null(ShellPopupPlace.Clear(Display, Display, LeftBar));
    }

    [Fact]
    public void EveryPanelsProcessIsHeard()
    {
        // The panels a flyout of Karpo's closes (WindowsPanels) are the panels kept off the bars: each one's process is
        // among the hosts hooked, by the one answer for which window is which panel.
        string[] classes = ["Windows.UI.Core.CoreWindow", "ControlCenterWindow", "Xaml_WindowedPopupClass", "WindowsDashboard"];
        foreach (var panel in WindowsPanels.All)
        {
            Assert.Contains(ShellPopups.Hosts, host => classes.Any(type => WindowsPanels.PanelOf(host.Image + ".exe", type) == panel));
        }
    }

    [Fact]
    public void AWindowItsHostPutsBackIsMovedAgainTwiceAShowingThenLetBe()
    {
        long now = 0;
        var moves = new ShellPopupMoves(() => now);
        var windows = R(-3840, 0, 0, 2088);
        // Start opens where Windows puts it: moved; put back 200 ms in and again later: moved again each time.
        Assert.True(moves.May(1, windows));
        for (var back = 0; back < ShellPopupMoves.PutBacks; back++)
        {
            now += 200;
            Assert.True(moves.May(1, windows));
        }
        // Put back once more while it still shows: let be, however long since.
        now += 60_000;
        Assert.False(moves.May(1, windows));
        // Placed somewhere else by its host (a notification's next step) is no putting back.
        Assert.True(moves.May(1, R(-3840, 100, 0, 2088)));
        // It shows again (closed and opened): moved as it opens, and its put-backs count from none.
        moves.Shown(1);
        Assert.True(moves.May(1, windows));
        Assert.True(moves.May(1, windows));
    }

    [Fact]
    public void AWindowIsMovedAtMostTwelveTimesInTenSeconds()
    {
        long now = 0;
        var moves = new ShellPopupMoves(() => now);
        for (var each = 0; each < ShellPopupMoves.PerWindow; each++)
        {
            Assert.True(moves.May(1));
            now += 100;
        }
        // A host that puts it back each time is let be.
        Assert.False(moves.May(1));
        // Another window is counted apart.
        Assert.True(moves.May(2));
        // Once the first moves are old enough, it may be moved again.
        now = ShellPopupMoves.WindowMs;
        Assert.True(moves.May(1));
    }

    /// <summary>
    /// §666: Start opened again and again within ten seconds is placed every time: a showing is an opening of its own, so
    /// its moves count from it (before, the fifth quick opening in ten seconds stood where Windows put it).
    /// </summary>
    [Fact]
    public void StartOpenedQuicklyAgainAndAgainIsPlacedEachTime()
    {
        long now = 0;
        var moves = new ShellPopupMoves(() => now);
        for (var opening = 0; opening < 10; opening++)
        {
            moves.Shown(1);
            // Windows places the host as it opens, again 200 ms in, and as it closes: three moves an opening.
            for (var each = 0; each < 3; each++)
            {
                Assert.True(moves.May(1, R(0, each, 3840, 2088)), $"opening {opening + 1}, move {each + 1}");
                now += 100;
            }
        }
    }
}
