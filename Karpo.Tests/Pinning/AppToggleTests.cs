// §619: a click on an app's button again and again does what it does on Windows 11's taskbar, every
// time: the window in front minimizes, the same click brings it back as it was (maximized, if it was),
// and so on. Which button is the active one (AppClicks.ActiveButton, read at the press), what is asked of
// the app and what is done without it (Apps.Asks; an app above Karpo, AboveKarpoTests), when an app's own mark of the active window ends
// (AppSignals.ActiveAltEnds); then the same clicks carried out on real windows the test makes itself, on
// a hidden desktop (Apps.SwitchTo, Apps.Minimize, Apps.Restore), where nothing of the person's is touched.

using System.Runtime.InteropServices;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Pinning;

public sealed unsafe class AppToggleTests
{
    // --- The active button ----------------------------------------------------------------------

    private static nint ButtonOf(nint window) => window switch
    {
        // 30 is a dialog owned by 3; 40 a window with no button of its own and no owner.
        30 => 3,
        _ => window,
    };

    private static nint Active(nint foreground, bool ours = false, nint mostRecent = 0, nint alt = 0, params nint[] minimized) =>
        AppClicks.ActiveButton(foreground, ours, mostRecent, alt, ButtonOf, window => minimized.Contains(window));

    /// <summary>The foreground window's button is the active one; its dialog in front stands for it; nothing in front, none.</summary>
    [Fact]
    public void TheForegroundWindowsButtonIsTheActiveOne()
    {
        Assert.Equal(3, Active(3));
        Assert.Equal(3, Active(30));
        Assert.Equal(40, Active(40));
        Assert.Equal(0, Active(0));
    }

    /// <summary>
    /// Regression: the window minimized last can keep the foreground where nothing else takes it; its
    /// button showed active, and Windows' never does. A minimized window is never the active button,
    /// whichever way it was found.
    /// </summary>
    [Fact]
    public void AMinimizedWindowIsNeverTheActiveButton()
    {
        Assert.Equal(0, Active(3, minimized: 3));
        Assert.Equal(0, Active(30, minimized: 3));
        Assert.Equal(0, Active(99, ours: true, mostRecent: 3, minimized: 3));
        Assert.Equal(0, Active(5, alt: 3, minimized: 3));
        Assert.Equal(3, Active(3, minimized: 4));
    }

    /// <summary>Karpo's own window in front (a folder, a menu) stands for the app window that had the foreground before it.</summary>
    [Fact]
    public void KarposOwnWindowStandsForTheAppBeforeIt()
    {
        Assert.Equal(3, Active(99, ours: true, mostRecent: 3));
        Assert.Equal(0, Active(99, ours: true, mostRecent: 0));
    }

    /// <summary>A window an app marked active itself (ITaskbarList::ActivateTab, SetActiveAlt) is, whatever has the foreground.</summary>
    [Fact]
    public void AnAppsOwnMarkIsTheActiveButton()
    {
        Assert.Equal(7, Active(3, alt: 7));
        Assert.Equal(7, Active(99, ours: true, mostRecent: 3, alt: 7));
    }

    // --- Again and again ------------------------------------------------------------------------

    /// <summary>One window and the foreground as Windows leaves them after each click.</summary>
    private sealed class World
    {
        public bool Minimized;
        public nint Foreground;

        /// <summary>What the click on window 1's button does, carried out; <paramref name="ours"/>: a popup of Karpo's had the focus as it went down.</summary>
        public AppClickKind Click(bool ours, nint nextInLine)
        {
            var front = AppClicks.ActiveButton(ours ? 99 : Foreground, ours, mostRecent: 1, activeAlt: 0, window => window, window => window == 1 && Minimized);
            var decision = AppClicks.Decide([new AppWindow(1, Minimized)], front);
            if (decision.Kind == AppClickKind.Minimize)
            {
                Minimized = true;
                // Windows activates the next window in line; with none, the minimized one may keep it.
                Foreground = nextInLine;
            }
            else
            {
                Minimized = false;
                Foreground = 1;
            }
            return decision.Kind;
        }
    }

    /// <summary>
    /// Ten clicks on the button of the window in front: minimized, back, minimized, back — with another
    /// window to take the foreground, with none (the minimized one keeping it), and with a popup of
    /// Karpo's holding the focus as the button goes down. Never two of the same in a row.
    /// </summary>
    [Theory]
    [InlineData(false, 2)]
    [InlineData(false, 1)]
    [InlineData(false, 0)]
    [InlineData(true, 2)]
    [InlineData(true, 1)]
    public void TenClicksToggleTheWindow(bool ours, int nextInLine)
    {
        var world = new World { Foreground = 1 };
        for (var click = 0; click < 10; click++)
        {
            var did = world.Click(ours, nextInLine);
            Assert.Equal(click % 2 == 0 ? AppClickKind.Minimize : AppClickKind.Activate, did);
            Assert.Equal(click % 2 == 0, world.Minimized);
        }
    }

    /// <summary>A window shown but behind another one comes forward and is not minimized; clicked again, it is.</summary>
    [Fact]
    public void AWindowBehindAnotherComesForwardFirst()
    {
        var world = new World { Foreground = 2 };
        Assert.Equal(AppClickKind.Activate, world.Click(ours: false, nextInLine: 2));
        Assert.Equal(AppClickKind.Minimize, world.Click(ours: false, nextInLine: 2));
        Assert.Equal(AppClickKind.Activate, world.Click(ours: false, nextInLine: 2));
    }

    // --- Asked, or done without the app ---------------------------------------------------------

    /// <summary>
    /// A system command is asked of the app (posted) unless it is hung or the window is disabled (a
    /// modal dialog's owner, whose DefWindowProc ignores it); then it is done without it. An app above
    /// Karpo is asked as any other (§662, AboveKarpoTests): nothing done without asking reaches it.
    /// </summary>
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void ACommandIsAskedOnlyOfAnAppThatCanAnswer(bool hung, bool disabled, bool asks) =>
        Assert.Equal(asks, Apps.Asks(hung, disabled));

    /// <summary>A minimized owner's modal dialog — its last active popup, enabled, over the owner it disabled — is what gets the keys after the restore.</summary>
    [Fact]
    public void AMinimizedOwnersModalDialogGetsTheKeysBack()
    {
        Assert.Equal(5, WindowOwners.ModalOf(1, 5, popupEnabled: true, ownerEnabled: false));
        Assert.Equal(0, WindowOwners.ModalOf(1, 5, popupEnabled: true, ownerEnabled: true));
        Assert.Equal(0, WindowOwners.ModalOf(1, 5, popupEnabled: false, ownerEnabled: false));
        Assert.Equal(0, WindowOwners.ModalOf(1, 1, popupEnabled: true, ownerEnabled: false));
        Assert.Equal(0, WindowOwners.ModalOf(1, 0, popupEnabled: true, ownerEnabled: false));
    }

    // --- An app's own mark ----------------------------------------------------------------------

    /// <summary>
    /// The mark an app made (ActivateTab/SetActiveAlt) lasts while the window activated stands for the
    /// marked one (itself, its tab's, its dialog), or has no button and is the app's own (its full screen);
    /// regression: another window of the same app with a button — every store app's frame is one process,
    /// ApplicationFrameHost's — kept the mark, the marked window's button lit and the one in front dark.
    /// </summary>
    [Fact]
    public void AnAppsMarkEndsWithTheNextWindowWithAButtonOfItsOwn()
    {
        var signals = new AppSignals();
        _ = signals.ActiveTab(20);
        Assert.False(signals.ActiveAltEnds(button: 20, listed: true, sameApp: true));
        Assert.False(signals.ActiveAltEnds(button: 25, listed: false, sameApp: true));
        Assert.Equal(20, signals.ActiveAlt);
        Assert.True(signals.ActiveAltEnds(button: 21, listed: true, sameApp: true));
        Assert.Equal(0, signals.ActiveAlt);

        _ = signals.ActiveTab(20);
        Assert.True(signals.ActiveAltEnds(button: 25, listed: false, sameApp: false));
        _ = signals.ActiveTab(20);
        Assert.True(signals.ActiveAltEnds(button: 8, listed: true, sameApp: false));
        Assert.False(signals.ActiveAltEnds(button: 8, listed: true, sameApp: false));
    }

    // --- Real windows ---------------------------------------------------------------------------

    private const string Class = "karpo.tests.app.toggle";

    /// <summary>
    /// Ten clicks on the button of a maximized window of the test's own, on a hidden desktop, carried out
    /// with the very calls a click makes: minimized, then back maximized and active, and again. (The
    /// foreground is the input desktop's alone; here the thread's active window stands for it.)
    /// </summary>
    [UiFact]
    public void TenRealClicksMinimizeAndRestoreAMaximizedWindow() => OnHiddenDesktop(instance =>
    {
        var window = Make(instance, WS.WS_OVERLAPPEDWINDOW, HWND.NULL);
        var other = Make(instance, WS.WS_OVERLAPPEDWINDOW, HWND.NULL);
        try
        {
            _ = ShowWindow(window, SW.SW_SHOWMAXIMIZED);
            Pump(() => GetActiveWindow() == window);
            for (var click = 0; click < 10; click++)
            {
                var front = AppClicks.ActiveButton((nint)GetActiveWindow(), false, 0, 0, at => at, at => IsIconic((HWND)at));
                var decision = AppClicks.Decide([new AppWindow((nint)window, IsIconic(window))], front);
                if (decision.Kind == AppClickKind.Minimize)
                {
                    Apps.Minimize(window);
                    Pump(() => IsIconic(window));
                    Assert.True(IsIconic(window), $"click {click + 1}: not minimized");
                }
                else
                {
                    Assert.Equal(AppClickKind.Activate, decision.Kind);
                    _ = Apps.SwitchTo(window, nudge: false);
                    Pump(() => !IsIconic(window) && GetActiveWindow() == window);
                    Assert.False(IsIconic(window), $"click {click + 1}: not restored");
                    Assert.True(IsZoomed(window), $"click {click + 1}: not maximized again");
                    Assert.Equal(window, GetActiveWindow());
                }
                Assert.Equal(click % 2 == 0 ? AppClickKind.Minimize : AppClickKind.Activate, decision.Kind);
            }
        }
        finally
        {
            _ = DestroyWindow(window);
            _ = DestroyWindow(other);
        }
    });

    /// <summary>A window shown behind another is brought forward, not minimized, and stays as it is (not restored from maximized).</summary>
    [UiFact]
    public void ARealWindowBehindAnotherComesForward() => OnHiddenDesktop(instance =>
    {
        var window = Make(instance, WS.WS_OVERLAPPEDWINDOW, HWND.NULL);
        var other = Make(instance, WS.WS_OVERLAPPEDWINDOW, HWND.NULL);
        try
        {
            _ = ShowWindow(window, SW.SW_SHOWMAXIMIZED);
            _ = SetActiveWindow(other);
            Pump(() => GetActiveWindow() == other);
            var front = AppClicks.ActiveButton((nint)GetActiveWindow(), false, 0, 0, at => at, at => IsIconic((HWND)at));
            Assert.Equal(new AppClick(AppClickKind.Activate, 0), AppClicks.Decide([new AppWindow((nint)window, false)], front));
            _ = Apps.SwitchTo(window, nudge: false);
            Pump(() => GetActiveWindow() == window);
            Assert.Equal(window, GetActiveWindow());
            Assert.True(IsZoomed(window));
        }
        finally
        {
            _ = DestroyWindow(window);
            _ = DestroyWindow(other);
        }
    });

    /// <summary>"Restore all windows" brings every minimized one back as it was, through the one restore.</summary>
    [UiFact]
    public void RestoreAllBringsEachBackAsItWas() => OnHiddenDesktop(instance =>
    {
        var maximized = Make(instance, WS.WS_OVERLAPPEDWINDOW, HWND.NULL);
        var plain = Make(instance, WS.WS_OVERLAPPEDWINDOW, HWND.NULL);
        try
        {
            _ = ShowWindow(maximized, SW.SW_SHOWMAXIMIZED);
            WindowMenus.MinimizeAll([maximized, plain]);
            Pump(() => IsIconic(maximized) && IsIconic(plain));
            Assert.True(IsIconic(maximized) && IsIconic(plain));
            foreach (var each in new[] { plain, maximized })
            {
                Apps.Restore(each);
            }
            Pump(() => !IsIconic(maximized) && !IsIconic(plain));
            Assert.True(IsZoomed(maximized));
            Assert.False(IsIconic(plain));
            Assert.False(IsZoomed(plain));
        }
        finally
        {
            _ = DestroyWindow(maximized);
            _ = DestroyWindow(plain);
        }
    });

    /// <summary>
    /// The owner of a modal dialog brought forward gives the dialog the keys (its last active popup), as
    /// Windows' taskbar does. Regression: minimized, a disabled owner's button neither minimized nor
    /// restored it — the posted SC_MINIMIZE/SC_RESTORE went by unanswered —, and restored, the owner had
    /// the activation and its dialog not: now it is minimized and restored without asking, and its dialog
    /// shown and activated after the restore. The windows are a thread's of their own, as another app's
    /// are (what is queued to them comes in order); the click's calls are made from the test's thread.
    /// </summary>
    [UiFact]
    public void TheModalDialogOfAWindowBroughtForwardGetsTheKeys() => OnHiddenDesktop(instance =>
    {
        var desktop = GetThreadDesktop(GetCurrentThreadId());
        HWND owner = default, dialog = default, other = default;
        uint appThread = 0;
        var made = new ManualResetEventSlim();
        var stop = false;
        var app = new Thread(() =>
        {
            _ = SetThreadDesktop(desktop);
            appThread = GetCurrentThreadId();
            owner = Make(instance, WS.WS_OVERLAPPEDWINDOW, HWND.NULL);
            dialog = Make(instance, WS.WS_POPUP | WS.WS_CAPTION | WS.WS_SYSMENU, owner);
            other = Make(instance, WS.WS_OVERLAPPEDWINDOW, HWND.NULL);
            _ = EnableWindow(owner, BOOL.FALSE);
            _ = SetActiveWindow(dialog);
            _ = SetActiveWindow(other);
            made.Set();
            MSG msg;
            while (!Volatile.Read(ref stop))
            {
                while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
                {
                    _ = TranslateMessage(&msg);
                    _ = DispatchMessageW(&msg);
                }
                Thread.Sleep(5);
            }
            _ = EnableWindow(owner, BOOL.TRUE);
            _ = DestroyWindow(dialog);
            _ = DestroyWindow(owner);
            _ = DestroyWindow(other);
        });
        app.Start();
        try
        {
            Assert.True(made.Wait(5000));
            HWND Active()
            {
                var info = new GUITHREADINFO { cbSize = (uint)sizeof(GUITHREADINFO) };
                return GetGUIThreadInfo(appThread, &info) ? info.hwndActive : HWND.NULL;
            }
            void Await(Func<bool> done)
            {
                var until = Environment.TickCount64 + 2000;
                while (!done() && Environment.TickCount64 < until)
                {
                    Thread.Sleep(5);
                }
            }
            Await(() => Active() == other);
            // The dialog in front is the owner's button, and bringing the owner forward activates the dialog.
            Assert.Equal(owner, WindowOwners.ButtonOf(dialog, hwnd => Apps.Listed(hwnd)));
            Assert.Equal(dialog, WindowOwners.ActivationTarget(owner));
            Apps.Minimize(owner);
            Await(() => IsIconic(owner) && !IsWindowVisible(dialog));
            Assert.True(IsIconic(owner));
            Assert.Equal(owner, WindowOwners.ActivationTarget(owner));
            Assert.Equal(dialog, WindowOwners.ModalOf(owner));
            _ = Apps.SwitchTo(owner, nudge: false);
            Await(() => !IsIconic(owner) && IsWindowVisible(dialog) && Active() == dialog);
            Assert.False(IsIconic(owner));
            Assert.True(IsWindowVisible(dialog));
            Assert.Equal(dialog, Active());
            Assert.Equal(dialog, WindowOwners.ActivationTarget(owner));
        }
        finally
        {
            Volatile.Write(ref stop, true);
            app.Join();
        }
    });

    private static HWND Make(HINSTANCE instance, uint style, HWND owner)
    {
        fixed (char* type = Class)
        fixed (char* title = "App toggle test window")
        {
            var window = CreateWindowExW(0, type, title, style, 0, 0, 400, 300, owner, HMENU.NULL, instance, null);
            Assert.True(window != HWND.NULL, $"CreateWindowExW failed: {Marshal.GetLastSystemError()}");
            _ = ShowWindow(window, SW.SW_SHOWNORMAL);
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
        while (Environment.TickCount64 < until)
        {
            while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
            {
                _ = TranslateMessage(&msg);
                _ = DispatchMessageW(&msg);
            }
            if (done())
            {
                return;
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
                fixed (char* name = "karpo-app-toggle-" + Guid.NewGuid().ToString("N"))
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
