// The window list with an app's thumbnail toolbar (Karpo.Dock\Preview.cs, ThumbBar.cs): the real
// popup, shown on a desktop of its own, asked where its buttons stand and then clicked there. Clicks
// go through a fake sender (IThumbSender), so nothing is posted to any window; the windows listed are
// this test's own, never shown.
//
// What it holds: a window whose app made a toolbar shows that toolbar and not Karpo's media buttons,
// while one without keeps them; hidden buttons take no room; a click reaches the window through the
// sender with the button's id, a disabled or non-interactive one sends nothing, and one that
// dismisses closes the list; no button shows its tooltip under the pointer — any app's, Spotify's
// media buttons among them (2026-10-09) — though each keeps it as its name.
//
// Needs windows and a compositor, so it runs only with KARPO_UI_TESTS=1.

using System.Runtime.InteropServices;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Signals;

public sealed unsafe class PreviewToolbarTests
{
    private static readonly Look Dressed = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0, 0x78, 0xD4), Animations: false);

    /// <summary>A player's toolbar as ThumbBar.Merge keeps it: previous, pause (dismisses), a disabled next, a hidden one, a picture.</summary>
    private static readonly ThumbButton[] Toolbar =
    [
        new(101, ThumbFlags.None, "Previous", null, 0),
        new(102, ThumbFlags.DismissOnClick, "Pause", null, 1),
        new(103, ThumbFlags.Disabled, "Next", null, 2),
        new(104, ThumbFlags.Hidden, "Never seen", null, 3),
        new(105, ThumbFlags.NonInteractive | ThumbFlags.NoBackground, "Now playing", null, 4),
    ];

    [UiFact]
    public void TheAppsToolbarStandsUnderItsPreviewAndItsClicksGoToTheWindow()
    {
        OnAHiddenDesktop(gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            Register(Preview.ClassName, instance);
            Register("karpo.tests.preview.app", instance);
            var player = Window(instance);
            var plain = Window(instance);
            var sender = new Sender();
            var session = new MediaControls("app", true, true, true, true);
            using var preview = new Preview(gfx, instance, 0);
            try
            {
                preview.Show(
                    gfx,
                    Dressed,
                    1f,
                    0,
                    [(player, "Player"), (plain, "Plain")],
                    new POINT { x = 400, y = 1000 },
                    new RECT { right = 1920, bottom = 1040 },
                    media: _ => session,
                    thumbs: hwnd => hwnd == player ? new ThumbInfo(Toolbar, "Tooltip text", null) : null,
                    sender: sender);

                // The player's card: its own toolbar, the hidden button left out, and no media row.
                Assert.False(preview.MediaRowAt(0));
                var shown = Enumerable.Range(0, 5).Select(slot => preview.ToolbarButton(0, slot)).TakeWhile(button => button is not null).ToList();
                Assert.Equal([101u, 102u, 103u, 105u], shown.Select(button => button!.Value.Button.Id));
                // The other card keeps Karpo's media buttons: its app made no toolbar.
                Assert.True(preview.MediaRowAt(1));
                Assert.Null(preview.ToolbarButton(1, 0));

                // A click on "Previous" reaches the player with its id; the list stays open.
                var previous = Middle(preview.ToolbarButton(0, 0)!.Value.Bounds);
                var before = preview.VisibleMarks;
                preview.Moved(previous.x, previous.y);
                Assert.Equal(before, preview.VisibleMarks);
                Assert.Equal("Previous", preview.ToolbarButton(0, 0)!.Value.Button.Tip);
                Assert.Equal(PreviewEventKind.Nothing, preview.Clicked(previous.x, previous.y, false).Kind);
                Assert.Equal([((nint)player, 101u)], sender.Clicks);

                // Disabled, and a picture that takes no clicks: nothing sent.
                foreach (var slot in new[] { 2, 3 })
                {
                    var at = Middle(preview.ToolbarButton(0, slot)!.Value.Bounds);
                    Assert.Equal(PreviewEventKind.Nothing, preview.Clicked(at.x, at.y, false).Kind);
                }
                Assert.Single(sender.Clicks);

                // "Pause" dismisses: sent, and the list is to close.
                var pause = Middle(preview.ToolbarButton(0, 1)!.Value.Bounds);
                Assert.Equal(PreviewEventKind.Dismiss, preview.Clicked(pause.x, pause.y, false).Kind);
                Assert.Equal(((nint)player, 102u), sender.Clicks[^1]);
            }
            finally
            {
                preview.Hide();
                _ = DestroyWindow(player);
                _ = DestroyWindow(plain);
            }
        });
    }

    internal static POINT Middle(RECT box) => new() { x = (box.left + box.right) / 2, y = (box.top + box.bottom) / 2 };

    internal static void Register(string className, HINSTANCE instance)
    {
        fixed (char* type = className)
        {
            var register = new WNDCLASSW { lpfnWndProc = &Plain, hInstance = instance, lpszClassName = type };
            _ = RegisterClassW(&register);
        }
    }

    internal static HWND Window(HINSTANCE instance)
    {
        fixed (char* type = "karpo.tests.preview.app")
        fixed (char* title = "Preview test window")
        {
            var window = CreateWindowExW(0, type, title, WS.WS_OVERLAPPEDWINDOW, 0, 0, 400, 300, HWND.NULL, HMENU.NULL, instance, null);
            Assert.True(window != HWND.NULL, $"CreateWindowExW failed: {Marshal.GetLastSystemError()}");
            return window;
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT Plain(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam) => DefWindowProcW(hwnd, msg, wParam, lParam);

    /// <summary>On a thread of its own, on a desktop nobody sees, with a dispatcher queue (which makes it an STA) and a Gfx.</summary>
    internal static void OnAHiddenDesktop(Action<Gfx> body)
    {
        Exception? failed = null;
        HDESK hidden = default;
        var thread = new Thread(() =>
        {
            try
            {
                fixed (char* name = "karpo-preview-test-" + Guid.NewGuid().ToString("N"))
                {
                    hidden = CreateDesktopW(name, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
                }
                Assert.True(hidden != HDESK.NULL, "no hidden desktop");
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
                }
            }
            catch (Exception error)
            {
                failed = error;
            }
        });
        // No SetApartmentState: an STA thread may own a window before its first line runs, and
        // SetThreadDesktop refuses a thread that does. The dispatcher queue makes it an STA.
        thread.Start();
        thread.Join();
        HiddenDesktop.Close(hidden);
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the hidden desktop: " + failed);
        }
    }

    private sealed class Sender : IThumbSender
    {
        public List<(nint Window, uint Id)> Clicks { get; } = [];

        public void Clicked(nint window, uint id) => Clicks.Add((window, id));
    }
}
