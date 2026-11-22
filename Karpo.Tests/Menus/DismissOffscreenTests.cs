// Karpo's popups closing for real, on a desktop of their own that nobody sees:
// the Windows key and Escape, and the mouse hook's part, which leaves a press on
// a bar to the bar and closes the popup for a press anywhere else on its own
// desktop. The press itself can't be sent (input only ever goes to the desktop
// in use, and nothing is found under a point on another), so the hook's handler
// is called with what the hook hears (FlyoutHost.Pressed), or with the point and
// the window there (PointerDown), as the hook calls it.
//
// The hook hears the presses of the desktop in use: the person's, while these
// run. Those are let go by (InputDesktop), so for a press on the popup's own
// desktop a test says that its desktop is the one in use.
//
// Runs only with KARPO_UI_TESTS=1, like the other offscreen tests.

using System.Numerics;
using System.Runtime.InteropServices;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Menus;

// The popups' window class is registered once per process by whichever test comes
// first, with that test's window procedure: these run in the islands' offscreen
// tests' collection, one after the other, and take their class away again when done.
[Collection("Test collection for Karpo.Tests.Islands.FlyoutOffscreenTests")]
public sealed unsafe class DismissOffscreenTests
{
    /// <summary>
    /// A window class of this test's own. It used to register Flyout.ClassName — the name the
    /// clock, the calendar, the notification list and the folders' offscreen tests also use — with
    /// the procedure below, which is a dead end: it answers every message with DefWindowProc and
    /// routes nothing. A class belongs to whoever registers it first and RegisterClassW is silent
    /// when the name is taken, so on the runs where this one got there first, those tests' windows
    /// answered nothing and their tests passed having exercised nothing at all.
    /// </summary>
    private const string ClassName = "karpo.dismiss.test";

    // Small enough for any screen: the hidden desktop has the displays' size.
    private static readonly RECT Work = new() { left = 0, top = 0, right = 800, bottom = 560 };
    private static readonly POINT Anchor = new() { x = 400, y = 560 };

    private static readonly Look Dark = new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    private static readonly FlyoutItem[] Items =
    [
        new(1, "Task Manager", new FlyoutIcon.Glyph("\uE9D9")),
        new(0, "", Separator: true),
        new(2, "Taskbar settings", new FlyoutIcon.Glyph("\uE713")),
    ];

    /// <summary>A point well away from the menu, where no window of the test's is.</summary>
    private static readonly POINT Away = new() { x = 720, y = 120 };

    /// <summary>Every button going down, as a low-level mouse hook hears it.</summary>
    private static readonly uint[] Presses = [WM.WM_LBUTTONDOWN, WM.WM_RBUTTONDOWN, WM.WM_MBUTTONDOWN, WM.WM_XBUTTONDOWN];

    [UiFact]
    public void The_Windows_key_and_Escape_close_a_menu()
    {
        OnHiddenDesktop(gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            Register(instance);
            using var menu = new Flyout(gfx, instance, 0, className: ClassName);
            foreach (var key in new[] { VK.VK_LWIN, VK.VK_RWIN, VK.VK_ESCAPE })
            {
                menu.Show(gfx, Dark, 1, Items, FlyoutLayout.List, Anchor, Work, Karpo.Core.Edge.Bottom);
                Assert.True(menu.IsOpen);
                Assert.Contains(FlyoutHost.OpenMenus, host => host.Hwnd == menu.Hwnd);
                Assert.Equal(new FlyoutEvent(FlyoutEventKind.Dismissed), menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0));
                Assert.False(menu.IsOpen);
                Assert.DoesNotContain(FlyoutHost.OpenMenus, host => host.Hwnd == menu.Hwnd);
                Pump();
            }
            // Keys that choose or move leave it open.
            menu.Show(gfx, Dark, 1, Items, FlyoutLayout.List, Anchor, Work, Karpo.Core.Edge.Bottom);
            Assert.Equal(default(FlyoutEvent), menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_DOWN, 0));
            Assert.True(menu.IsOpen);
            menu.Hide();
            Pump();
        });
    }

    /// <summary>
    /// The regression behind an intermittent failure of the test above. FlyoutHost kept one list
    /// of open Menu hosts for the whole process, and xunit runs the offscreen classes in parallel,
    /// each on a thread of its own, so another class's popups were added to and removed from that
    /// list while this one read it: a null in OpenMenus's own predicate, or this thread's menu
    /// missing from it. A thread's open menus are its own now — the hook that reads them is called
    /// on the thread that put it in — and this checks that property in order rather than by
    /// racing for it: a menu held open on one thread is not in another thread's list, and the
    /// other thread opening and closing a menu of its own leaves the first thread's list alone.
    /// </summary>
    [UiFact]
    public void Each_thread_sees_only_the_menus_it_opened()
    {
        OnHiddenDesktop(gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            Register(instance);
            using var mine = new Flyout(gfx, instance, 0, className: ClassName);
            mine.Show(gfx, Dark, 1, Items, FlyoutLayout.List, Anchor, Work, Karpo.Core.Edge.Bottom);
            Assert.Single(FlyoutHost.OpenMenus, host => host.Hwnd == mine.Hwnd);

            // Another thread, as another test class's is, with a menu of its own: what its list
            // holds while that menu is open, and once it has closed. The class is the process's,
            // registered above; this thread's windows are still up, so it stays registered.
            HWND[] whileOpen = [];
            HWND[] afterClosing = [];
            OnHiddenDesktop(other =>
            {
                using var theirs = new Flyout(other, instance, 0, className: ClassName);
                theirs.Show(other, Dark, 1, Items, FlyoutLayout.List, Anchor, Work, Karpo.Core.Edge.Bottom);
                whileOpen = [.. FlyoutHost.OpenMenus.Select(host => host.Hwnd)];
                theirs.Hide();
                afterClosing = [.. FlyoutHost.OpenMenus.Select(host => host.Hwnd)];
                Pump();
            });

            Assert.DoesNotContain(mine.Hwnd, whileOpen);
            Assert.Single(whileOpen);
            Assert.Empty(afterClosing);
            // And this thread's list is as it was.
            Assert.Equal(new[] { mine.Hwnd }, FlyoutHost.OpenMenus.Select(host => host.Hwnd).ToArray());
            mine.Hide();
            Assert.Empty(FlyoutHost.OpenMenus);
            Pump();
        });
    }

    [UiFact]
    public void The_Windows_key_closes_any_menu_mode_popup()
    {
        OnHiddenDesktop(gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            Register(instance);
            using var popup = new FlyoutHost(gfx, instance, 0, FlyoutMode.Menu, ClassName);
            popup.Show(gfx, Dark, 1, new Vector2(360, 200), new FlyoutPlacement(Anchor, Work, Karpo.Core.Edge.Bottom));
            Assert.True(popup.IsOpen);
            Assert.Equal(FlyoutHostEventKind.Dismissed, popup.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_LWIN, 0).Kind);
            Assert.False(popup.IsOpen);
            // A tool popup (the edit strip) never closes by itself.
            using var tool = new FlyoutHost(gfx, instance, 0, FlyoutMode.Tool, ClassName);
            tool.Show(gfx, Dark, 1, new Vector2(360, 48), new FlyoutPlacement(Anchor, Work, Karpo.Core.Edge.Bottom));
            Assert.Equal(FlyoutHostEventKind.Nothing, tool.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_LWIN, 0).Kind);
            Assert.True(tool.IsOpen);
            Assert.DoesNotContain(FlyoutHost.OpenMenus, host => host.Hwnd == tool.Hwnd);
            tool.Hide();
            Pump();
        });
    }

    /// <summary>
    /// search: every flyout of Karpo's closes Windows Search and Start on its way in, from the one
    /// place every popup opens through (FlyoutHost.Opening): a popup that takes the focus asks once as
    /// it opens, with the window that was in front; not again while it stays open and only changes,
    /// again when it opens anew; and a popup that never takes the focus — the edit strip — doesn't ask.
    /// Nothing here reads the person's real Search: the hook is the test's. (It was the web search
    /// box's test until that widget went, 2026-10-11.)
    /// </summary>
    [UiFact]
    public void Opening_a_flyout_closes_Windows_Search_first()
    {
        OnHiddenDesktop(gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            Register(instance);
            var asked = new List<HWND>();
            var before = FlyoutHost.Opening;
            FlyoutHost.Opening = foreground =>
            {
                asked.Add(foreground);
                return false;
            };
            try
            {
                using var popup = new FlyoutHost(gfx, instance, 0, FlyoutMode.Menu, ClassName);
                var foreground = GetForegroundWindow();
                void Show() => popup.Show(gfx, Dark, 1, new Vector2(360, 200), new FlyoutPlacement(Anchor, Work, Karpo.Core.Edge.Bottom));
                Show();
                Assert.Equal([foreground], asked);
                // Shown again while open: the same popup, new content.
                Show();
                Assert.Single(asked);
                popup.Hide();
                Show();
                Assert.Equal(2, asked.Count);
                popup.Hide();

                using var strip = new FlyoutHost(gfx, instance, 0, FlyoutMode.Tool, ClassName);
                strip.Show(gfx, Dark, 1, new Vector2(200, 40), new FlyoutPlacement(Anchor, Work, Karpo.Core.Edge.Bottom));
                strip.Hide();
                Assert.Equal(2, asked.Count);
            }
            finally
            {
                FlyoutHost.Opening = before;
            }
            Pump();
        });
    }

    [UiFact]
    public void The_hook_leaves_a_press_on_a_bar_to_the_bar_and_closes_for_the_rest()
    {
        OnHiddenDesktop(gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            Register(instance);
            // A bar along the bottom and another window of this process above it.
            var bar = Plain(0, 560, 800, 40);
            var other = Plain(20, 20, 100, 60);
            using var menu = new Flyout(gfx, instance, 0, className: ClassName);
            FlyoutHost.PressesHandledBy = window => window == bar;
            try
            {
                menu.Show(gfx, Dark, 1, Items, FlyoutLayout.List, Anchor, Work, Karpo.Core.Edge.Bottom);
                var host = Assert.Single(FlyoutHost.OpenMenus, candidate => candidate.Hwnd == menu.Hwnd);
                Pump();

                // On the bar: nothing posted; the bar's press closes the menu (Dock.PressedOn).
                host.PointerDown(new POINT { x = 50, y = 580 }, bar);
                Assert.Null(Dismissal(menu.Hwnd));

                // On the menu itself: nothing either.
                var panel = host.PanelOnScreen;
                host.PointerDown(new POINT { x = (panel.left + panel.right) / 2, y = (panel.top + panel.bottom) / 2 }, menu.Hwnd);
                Assert.Null(Dismissal(menu.Hwnd));

                // On another window of Karpo's: dismissed, and the focus goes back.
                host.PointerDown(new POINT { x = 60, y = 50 }, other);
                var ours = Dismissal(menu.Hwnd);
                Assert.NotNull(ours);
                Assert.Equal(1, (nint)ours.Value.lParam);

                // Anywhere else: dismissed, and whatever was clicked keeps the focus.
                host.PointerDown(new POINT { x = 720, y = 120 }, HWND.NULL);
                var elsewhere = Dismissal(menu.Hwnd);
                Assert.NotNull(elsewhere);
                Assert.Equal(0, (nint)elsewhere.Value.lParam);

                // The posted message closes it, as the dock's window procedure hands it over.
                Assert.Equal(new FlyoutEvent(FlyoutEventKind.Dismissed), menu.Message(elsewhere.Value.message, elsewhere.Value.wParam, elsewhere.Value.lParam));
                Assert.False(menu.IsOpen);
                // This thread's list: the other offscreen test classes run alongside on threads of
                // their own, and their popups are in lists of their own (Each_thread_sees_only_the_menus_it_opened).
                Assert.DoesNotContain(FlyoutHost.OpenMenus, candidate => candidate.Hwnd == menu.Hwnd);
            }
            finally
            {
                FlyoutHost.PressesHandledBy = null;
                _ = DestroyWindow(bar);
                _ = DestroyWindow(other);
                Pump();
            }
        });
    }

    /// <summary>
    /// A press on another desktop leaves a menu open, handed over as the hook hands it. The hook
    /// hears the presses of the desktop in use — the person's while this runs, never this one — and
    /// every one of them is outside a menu nobody sees: each would post the menu its dismissal, and a
    /// test's pump would close its panel mid-test, as it closed the weather panel before its forecast
    /// came ("Loading" where "Ready" was due).
    /// </summary>
    [UiFact]
    public void A_press_on_another_desktop_leaves_a_menu_open()
    {
        OnHiddenDesktop(gfx =>
        {
            // Windows' own answer: a desktop made here never has the input.
            Assert.False(InputDesktop.Here());
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            Register(instance);
            using var menu = new Flyout(gfx, instance, 0, className: ClassName);
            menu.Show(gfx, Dark, 1, Items, FlyoutLayout.List, Anchor, Work, Karpo.Core.Edge.Bottom);
            Assert.Contains(FlyoutHost.OpenMenus, host => host.Hwnd == menu.Hwnd);
            Pump();

            foreach (var press in Presses)
            {
                FlyoutHost.Pressed(press, Away);
                Assert.Null(Dismissal(menu.Hwnd));
            }
            Pump();
            Assert.True(menu.IsOpen);
            Assert.Contains(FlyoutHost.OpenMenus, host => host.Hwnd == menu.Hwnd);

            menu.Hide();
            Pump();
        });
    }

    /// <summary>
    /// A press on the menu's own desktop still closes it, handed over as the hook hands it: this
    /// desktop stands for the one in use, which a desktop made here never is. Every button going
    /// down closes it; the pointer moving or a button coming up closes nothing.
    /// </summary>
    [UiFact]
    public void A_press_on_the_menus_own_desktop_still_closes_it()
    {
        OnHiddenDesktop(gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            Register(instance);
            using var menu = new Flyout(gfx, instance, 0, className: ClassName);
            InputDesktop.Holds = () => true;
            try
            {
                menu.Show(gfx, Dark, 1, Items, FlyoutLayout.List, Anchor, Work, Karpo.Core.Edge.Bottom);
                Pump();
                FlyoutHost.Pressed(WM.WM_MOUSEMOVE, Away);
                FlyoutHost.Pressed(WM.WM_LBUTTONUP, Away);
                Assert.Null(Dismissal(menu.Hwnd));
                Assert.True(menu.IsOpen);

                foreach (var press in Presses)
                {
                    menu.Show(gfx, Dark, 1, Items, FlyoutLayout.List, Anchor, Work, Karpo.Core.Edge.Bottom);
                    Pump();
                    FlyoutHost.Pressed(press, Away);
                    var dismissal = Dismissal(menu.Hwnd);
                    Assert.NotNull(dismissal);
                    // The posted message closes it, as the dock's window procedure hands it over.
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Dismissed), menu.Message(dismissal.Value.message, dismissal.Value.wParam, dismissal.Value.lParam));
                    Assert.False(menu.IsOpen);
                    Assert.DoesNotContain(FlyoutHost.OpenMenus, host => host.Hwnd == menu.Hwnd);
                }
            }
            finally
            {
                InputDesktop.Holds = InputDesktop.Here;
                Pump();
            }
        });
    }

    /// <summary>The dismissal the hook posted to <paramref name="window"/>, taken off the queue; null when none.</summary>
    private static MSG? Dismissal(HWND window)
    {
        MSG message;
        return PeekMessageW(&message, window, FlyoutHost.WM_KARPO_HOST_DISMISS, FlyoutHost.WM_KARPO_HOST_DISMISS, PM.PM_REMOVE)
            ? message
            : null;
    }

    /// <summary>A visible window of this process at a place on the screen, standing in for a bar or a popup.</summary>
    private static HWND Plain(int x, int y, int width, int height)
    {
        HWND window;
        fixed (char* type = "Static")
        {
            window = CreateWindowExW((uint)(WS.WS_EX_TOOLWINDOW | WS.WS_EX_NOACTIVATE), type, null, WS.WS_POPUP | WS.WS_VISIBLE,
                x, y, width, height, HWND.NULL, HMENU.NULL, HINSTANCE.NULL, null);
        }
        Assert.True(window != HWND.NULL);
        return window;
    }

    /// <summary>
    /// A message only this test's procedure answers, and what it answers. The islands' offscreen
    /// tests ask the same message of their class and expect an answer of their own, so a class
    /// held by either procedure is told apart from the other by what it says.
    /// </summary>
    private const uint WM_PROBE = WM.WM_APP + 0x1D5;

    private const nint ProbeAnswer = 0x444953; // "DIS"

    private static void Register(HINSTANCE instance)
    {
        fixed (char* name = ClassName)
        {
            var type = new WNDCLASSW { lpfnWndProc = &Procedure, hInstance = instance, lpszClassName = name, hCursor = LoadCursorW(HINSTANCE.NULL, IDC.IDC_ARROW) };
            _ = RegisterClassW(&type);
            // The same check the islands' offscreen tests make: a class belongs to whoever
            // registered it first, so a name this test does not own would leave its menus
            // answering somebody else's procedure and this test passing on nothing. It is asked
            // of a window of the class rather than by comparing procedure addresses: two pointers
            // to one function need not be equal (CS8909), and the question is what the windows run.
            var probe = CreateWindowExW(0, name, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, instance, null);
            Assert.True(probe != HWND.NULL, $"the window class {ClassName} is not registered");
            nint answer;
            try
            {
                answer = SendMessageW(probe, WM_PROBE, 0, 0);
            }
            finally
            {
                _ = DestroyWindow(probe);
            }
            Assert.True(answer == ProbeAnswer, $"the window class {ClassName} is held by another test's procedure");
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT Procedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam) =>
        msg == WM_PROBE ? ProbeAnswer : DefWindowProcW(hwnd, msg, wParam, lParam);

    private static void Pump()
    {
        MSG message;
        while (PeekMessageW(&message, HWND.NULL, 0, 0, PM.PM_REMOVE))
        {
            _ = TranslateMessage(&message);
            _ = DispatchMessageW(&message);
        }
    }

    /// <summary>
    /// Runs <paramref name="body"/> on a new thread on a desktop of its own, made an STA by its
    /// dispatcher queue (the compositor needs one), with a GPU-less Gfx.
    /// </summary>
    private static void OnHiddenDesktop(Action<Gfx> body)
    {
        Exception? failed = null;
        HDESK hidden = default;
        var thread = new Thread(() =>
        {
            try
            {
                fixed (char* name = "karpo-menus-test-" + Guid.NewGuid().ToString("N"))
                {
                    hidden = CreateDesktopW(name, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
                }
                Assert.True(hidden != HDESK.NULL, "no hidden desktop");
                // Before this thread owns any window: its windows are then made there.
                Assert.True(SetThreadDesktop(hidden));
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                try
                {
                    using var gfx = new Gfx(gpu: false);
                    body(gfx);
                }
                finally
                {
                    controller->Release();
                    // Its windows are gone: the next test registers the class with its own procedure.
                    fixed (char* name = ClassName)
                    {
                        _ = UnregisterClassW(name, (HINSTANCE)(void*)GetModuleHandleW(null));
                    }
                }
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
            throw new Xunit.Sdk.XunitException("On the offscreen thread: " + failed);
        }
    }
}
