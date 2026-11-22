// The rest of Windows 11's app buttons (docs\widgets.md §362): which button a dialog in front stands
// for and which window bringing one forward activates (WindowOwners), the one way a window is
// minimized (Apps.Minimize, WindowMenus.Minimizes), the list's order (Dock.InOpenOrder), a button's
// attention (AttentionFlash), the arrows in a list (Dock.ListStep) and the active mark an app makes
// (AppSignals.ActiveTab). Rules first; then the few that need real windows, on a hidden desktop, with
// windows the test makes itself.

using System.Runtime.InteropServices;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Pinning;

public sealed unsafe class AppButtonRulesTests
{
    // --- Owners ---------------------------------------------------------------------------------

    /// <summary>A dialog in front stands for the first of its owners with a button; a window with none, for itself.</summary>
    [Fact]
    public void ADialogInFrontStandsForItsOwnersButton()
    {
        // 3 is owned by 2, owned by 1.
        nint OwnerOf(nint at) => at switch { 3 => 2, 2 => 1, _ => 0 };
        Assert.Equal(1, WindowOwners.ButtonOf(3, OwnerOf, at => at == 1));
        Assert.Equal(2, WindowOwners.ButtonOf(3, OwnerOf, at => at is 1 or 2));
        Assert.Equal(3, WindowOwners.ButtonOf(3, OwnerOf, at => at is 1 or 3));
        Assert.Equal(3, WindowOwners.ButtonOf(3, OwnerOf, _ => false));
        Assert.Equal(0, WindowOwners.ButtonOf(0, OwnerOf, _ => true));
        // A chain that loops ends where it began, never hangs.
        Assert.Equal(5, WindowOwners.ButtonOf(5, at => at == 5 ? 6 : 5, _ => false));
    }

    /// <summary>Bringing a window forward activates its last active popup where that is another window, shown and enabled.</summary>
    [Fact]
    public void AModalDialogIsWhatComesForward()
    {
        Assert.Equal(5, WindowOwners.ActivationTarget(1, 5, shown: true, enabled: true));
        Assert.Equal(1, WindowOwners.ActivationTarget(1, 1, shown: true, enabled: true));
        Assert.Equal(1, WindowOwners.ActivationTarget(1, 5, shown: false, enabled: true));
        Assert.Equal(1, WindowOwners.ActivationTarget(1, 5, shown: true, enabled: false));
        Assert.Equal(1, WindowOwners.ActivationTarget(1, 0, shown: true, enabled: true));
    }

    /// <summary>Read off real windows: an owned dialog has no button of its own and stands for its owner's.</summary>
    [UiFact]
    public void ARealDialogStandsForItsOwner() => OnHiddenDesktop(instance =>
    {
        var owner = Make(instance, WS.WS_OVERLAPPEDWINDOW, HWND.NULL);
        var dialog = Make(instance, WS.WS_POPUP | WS.WS_CAPTION | WS.WS_SYSMENU, owner);
        try
        {
            Assert.True(Apps.Listed(owner));
            Assert.False(Apps.Listed(dialog));
            Assert.Equal(owner, WindowOwners.ButtonOf(dialog, hwnd => Apps.Listed(hwnd)));
            Assert.Equal(owner, WindowOwners.ButtonOf(owner, hwnd => Apps.Listed(hwnd)));
        }
        finally
        {
            _ = DestroyWindow(dialog);
            _ = DestroyWindow(owner);
        }
    });

    // --- Minimizing -----------------------------------------------------------------------------

    /// <summary>Only a window with a minimize button that is not minimized already is minimized from its button.</summary>
    [Fact]
    public void OnlyAWindowWithAMinimizeButtonIsMinimized()
    {
        Assert.True(WindowMenus.Minimizes(new WindowShape()));
        Assert.True(WindowMenus.Minimizes(new WindowShape(Maximized: true)));
        Assert.False(WindowMenus.Minimizes(new WindowShape(CanMinimize: false)));
        Assert.False(WindowMenus.Minimizes(new WindowShape(Minimized: true)));
    }

    /// <summary>
    /// The one minimize, a click's and "Minimize all windows" alike: a window with a minimize button is
    /// asked (SC_MINIMIZE) and minimizes; one without stays as it is, as on Windows' taskbar.
    /// </summary>
    [UiFact]
    public void AWindowWithoutAMinimizeButtonStays() => OnHiddenDesktop(instance =>
    {
        var plain = Make(instance, WS.WS_OVERLAPPEDWINDOW, HWND.NULL);
        var borderless = Make(instance, WS.WS_POPUP | WS.WS_CAPTION | WS.WS_SYSMENU, HWND.NULL);
        var other = Make(instance, WS.WS_OVERLAPPEDWINDOW, HWND.NULL);
        try
        {
            Apps.Minimize(plain);
            Apps.Minimize(borderless);
            Pump(() => IsIconic(plain));
            Assert.True(IsIconic(plain));
            Assert.False(IsIconic(borderless));
            WindowMenus.MinimizeAll([borderless, other]);
            Pump(() => IsIconic(other));
            Assert.True(IsIconic(other));
            Assert.False(IsIconic(borderless));
            // Restored without waiting on the app, as a click on a minimized window's button does.
            Surfaces.RestoreOther(plain);
            Pump(() => !IsIconic(plain));
            Assert.False(IsIconic(plain));
        }
        finally
        {
            _ = DestroyWindow(plain);
            _ = DestroyWindow(borderless);
            _ = DestroyWindow(other);
        }
    });

    // --- The list's order -----------------------------------------------------------------------

    /// <summary>A button's list shows its windows as they were opened, however recently each was used.</summary>
    [Fact]
    public void TheListKeepsTheOrderTheWindowsOpenedIn()
    {
        var all = new[] { 10, 20, 30, 40 }.Select(hwnd => new Win { Hwnd = (HWND)(nint)hwnd }).ToList();
        // Most recently used first, as the button keeps them for its clicks.
        HWND[] open = [(HWND)(nint)40, (HWND)(nint)10, (HWND)(nint)30];
        Assert.Equal([10, 30, 40], TheDock.InOpenOrder(all, open).Select(win => (int)(nint)win.Hwnd));
    }

    // --- Attention ------------------------------------------------------------------------------

    /// <summary>A button asking for attention blinks the flash count, lit first, then stays lit.</summary>
    [Fact]
    public void AttentionBlinksTheFlashCountThenStaysLit()
    {
        const uint beat = 500;
        bool[] beats = [.. Enumerable.Range(0, 8).Select(at => AttentionFlash.Lit(at * beat + 10, 3, beat))];
        Assert.Equal([true, false, true, false, true, true, true, true], beats);
        Assert.True(AttentionFlash.Blinking(10, 3, beat));
        Assert.True(AttentionFlash.Blinking(4 * beat + 10, 3, beat));
        Assert.False(AttentionFlash.Blinking(5 * beat, 3, beat));
        // No count: lit at once, for good.
        Assert.True(AttentionFlash.Lit(beat + 10, 0, beat));
        Assert.False(AttentionFlash.Blinking(10, 0, beat));
        // It bounces once, in its first beat.
        Assert.True(AttentionFlash.Began(10, beat));
        Assert.False(AttentionFlash.Began(2 * beat + 10, beat));
    }

    /// <summary>
    /// A redraw that may be the flashing going off is a stop only once no flash came for the settle: a
    /// flashing still going on lights again within a beat (measured: 530 ms) and never ends there.
    /// </summary>
    [Fact]
    public void AFlashingStopsOnlyOnceNoFlashCameForTheSettle()
    {
        Assert.False(AttentionFlash.Stopped(0, 99_999));
        Assert.False(AttentionFlash.Stopped(1_000, 1_000 + 530));
        Assert.False(AttentionFlash.Stopped(1_000, 1_000 + 2 * 530));
        Assert.True(AttentionFlash.Stopped(1_000, 1_000 + AttentionFlash.SettleMs));
    }

    /// <summary>The beat timer wakes for the next blink, or the end of a settle, and not at all for a button lit for good.</summary>
    [Fact]
    public void TheBeatTimerWakesOnlyWhenAButtonChangesByItself()
    {
        Assert.Equal(400, AttentionFlash.NextMs(100, null, 3, 500));
        Assert.Equal(200, AttentionFlash.NextMs(100, AttentionFlash.SettleMs - 200, 3, 500));
        Assert.Equal(AttentionFlash.SettleMs - 100, AttentionFlash.NextMs(10_000, 100, 3, 500));
        Assert.Null(AttentionFlash.NextMs(10_000, null, 3, 500));
    }

    // --- The keys in a list ---------------------------------------------------------------------

    /// <summary>The arrow away from the bar goes into the list, the one towards it comes back, the others walk the cards.</summary>
    [Theory]
    [InlineData(Edge.Bottom, VK.VK_UP, TheDock.ListMove.Into)]
    [InlineData(Edge.Bottom, VK.VK_DOWN, TheDock.ListMove.Out)]
    [InlineData(Edge.Bottom, VK.VK_LEFT, TheDock.ListMove.Back)]
    [InlineData(Edge.Bottom, VK.VK_RIGHT, TheDock.ListMove.On)]
    [InlineData(Edge.Top, VK.VK_DOWN, TheDock.ListMove.Into)]
    [InlineData(Edge.Top, VK.VK_UP, TheDock.ListMove.Out)]
    [InlineData(Edge.Left, VK.VK_RIGHT, TheDock.ListMove.Into)]
    [InlineData(Edge.Left, VK.VK_UP, TheDock.ListMove.Back)]
    [InlineData(Edge.Right, VK.VK_LEFT, TheDock.ListMove.Into)]
    [InlineData(Edge.Right, VK.VK_DOWN, TheDock.ListMove.On)]
    [InlineData(Edge.Bottom, VK.VK_RETURN, TheDock.ListMove.None)]
    public void AnArrowMovesInTheListByTheBarsEdge(Edge edge, uint vk, TheDock.ListMove move) =>
        Assert.Equal(move, TheDock.ListStep(edge, vk));

    // --- The active mark an app makes -----------------------------------------------------------

    /// <summary>
    /// ActivateTab / SetActiveAlt marks a window active — a tab's mark goes to its window —; the window
    /// itself activated keeps it, another app's ends it (§619: AppToggleTests has the rest).
    /// </summary>
    [Fact]
    public void AnAppsActiveMarkLastsUntilAnotherAppComesForward()
    {
        var signals = new AppSignals();
        Assert.True(signals.RegisterTab(tab: 21, main: 20));
        Assert.True(signals.ActiveTab(21));
        Assert.Equal(20, signals.ActiveAlt);
        Assert.False(signals.ActiveTab(20));
        Assert.False(signals.ActiveAltEnds(button: 20, listed: true, sameApp: true));
        Assert.Equal(20, signals.ActiveAlt);
        Assert.True(signals.ActiveAltEnds(button: 8, listed: true, sameApp: false));
        Assert.Equal(0, signals.ActiveAlt);
        Assert.False(signals.ActiveAltEnds(button: 8, listed: true, sameApp: false));
    }

    // --- Real windows ---------------------------------------------------------------------------

    private const string Class = "karpo.tests.app.button";

    private static HWND Make(HINSTANCE instance, uint style, HWND owner)
    {
        fixed (char* type = Class)
        fixed (char* title = "App button test window")
        {
            var window = CreateWindowExW(0, type, title, style, 0, 0, 400, 300, owner, HMENU.NULL, instance, null);
            Assert.True(window != HWND.NULL, $"CreateWindowExW failed: {Marshal.GetLastSystemError()}");
            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
            return window;
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT Plain(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam) => DefWindowProcW(hwnd, msg, wParam, lParam);

    /// <summary>This thread's messages handled until <paramref name="done"/>, or two seconds.</summary>
    private static void Pump(Func<bool> done)
    {
        var until = Environment.TickCount64 + 2000;
        MSG msg;
        while (!done() && Environment.TickCount64 < until)
        {
            while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
            {
                _ = TranslateMessage(&msg);
                _ = DispatchMessageW(&msg);
            }
            Thread.Sleep(5);
        }
    }

    /// <summary>Runs <paramref name="body"/> on a thread of its own on a hidden desktop, which is closed after.</summary>
    private static void OnHiddenDesktop(Action<HINSTANCE> body)
    {
        HDESK hidden = default;
        Exception? failed = null;
        var thread = new Thread(() =>
        {
            try
            {
                fixed (char* name = "karpo-app-buttons-" + Guid.NewGuid().ToString("N"))
                {
                    hidden = CreateDesktopW(name, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
                }
                Assert.True(hidden != HDESK.NULL, "no hidden desktop");
                Assert.True(SetThreadDesktop(hidden));
                var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
                fixed (char* type = Class)
                {
                    var register = new WNDCLASSW { lpfnWndProc = &Plain, hInstance = instance, lpszClassName = type };
                    _ = RegisterClassW(&register);
                }
                body(instance);
            }
            catch (Exception error)
            {
                failed = error;
            }
        });
        thread.Start();
        thread.Join();
        HiddenDesktop.Close(hidden);
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the hidden desktop: " + failed);
        }
    }
}
