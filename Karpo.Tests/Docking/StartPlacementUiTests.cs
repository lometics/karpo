// §666: Karpo's side of Start's placement on real windows (KARPO_UI_TESTS=1): two windows of the test's own on a hidden
// desktop stand in for Start's host and its search box, and ShellPopups moves them as it moves Windows' — the same move,
// counted the same way, said the same way in appbar.log. Nothing of Windows' Start is opened or moved.

using System.Runtime.InteropServices;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Docking;

public sealed unsafe class StartPlacementUiTests
{
    private static RECT R(int left, int top, int right, int bottom) => new() { left = left, top = top, right = right, bottom = bottom };

    private static string Show(RECT rect) => $"{rect.left},{rect.top}..{rect.right},{rect.bottom}";

    /// <summary>
    /// The user's main display (3840x2160, 150 %, Karpo's bar at the right, Start its first button): Start's host heard as
    /// it shows, before its box — no size known, nothing moves; the box shows — both go where Start hangs from the button,
    /// once; heard again, nothing; Start grows (the box taller) — the host goes again so Start keeps its top, the box round it.
    /// </summary>
    [UiFact]
    public void StartsHostAndBoxAreMovedOnceAsTheyShowAndAgainAsStartResizes()
    {
        var display = new StartDisplay(R(0, 0, 3840, 2160), R(0, 0, 3777, 2160), 144);
        var scene = new StartScene(display, new StartButton(R(3783, 6, 3834, 57), R(3777, 0, 3840, 2160), Edge.Right), false, [display]);
        var said = new List<string>();
        var popups = new ShellPopups(said.Add, () => false, _ => null, () => { });
        string? failure = null;
        HDESK hidden = default;
        var thread = new Thread(() =>
        {
            try
            {
                fixed (char* name = "karpo-start-" + Guid.NewGuid().ToString("N"))
                {
                    hidden = CreateDesktopW(name, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
                }
                Assert.True(hidden != HDESK.NULL, $"CreateDesktopW failed: {Marshal.GetLastSystemError()}");
                Assert.True(SetThreadDesktop(hidden));
                var host = Make(R(0, 0, 3840, 2088));
                var box = Make(R(0, 825, 1287, 2160));
                try
                {
                    // The host first: Start's size not known yet.
                    popups.PlaceStart(host, Rect(host), scene, null);
                    Assert.Equal("0,0..3840,2088", Show(Rect(host)));
                    Assert.Empty(said);
                    // The box, where Windows first puts it: both go.
                    popups.PlaceStart(host, Rect(host), scene, (box, Rect(box)));
                    Assert.Equal("2490,-753..6330,1335", Show(Settled(host)));
                    Assert.Equal("2490,0..3777,1335", Show(Settled(box)));
                    Assert.Equal(2, said.Count);
                    Assert.All(said, line => Assert.Contains("moved for Karpo's Start button to", line));
                    // Heard again (Windows placing it again, the box's own move): nothing.
                    popups.PlaceStart(host, Rect(host), scene, (box, Rect(box)));
                    popups.PlaceStart(host, Rect(host), scene, null);
                    Assert.Equal(2, said.Count);
                    // Start grows by 150 px (its All view): Windows lays it out from the host's foot and the box says so.
                    Assert.True(SetWindowPos(box, HWND.NULL, 0, 0, 1287, 1485, SWP.SWP_NOMOVE | SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE));
                    popups.PlaceStart(host, Rect(host), scene, (box, Rect(box)));
                    Assert.Equal("2490,-603..6330,1485", Show(Settled(host)));
                    Assert.Equal("2490,0..3777,1485", Show(Settled(box)));
                    // The host heard on its own now goes by the size the box last said.
                    popups.PlaceStart(host, Rect(host), scene, null);
                    Assert.Equal("2490,-603..6330,1485", Show(Settled(host)));
                }
                finally
                {
                    _ = DestroyWindow(box);
                    _ = DestroyWindow(host);
                }
            }
            catch (Exception error)
            {
                failure = error.ToString();
            }
        });
        thread.Start();
        thread.Join();
        HiddenDesktop.Close(hidden);
        Assert.True(failure is null, failure);
    }

    private static HWND Make(RECT rect)
    {
        fixed (char* type = "Static")
        {
            var window = CreateWindowExW(WS.WS_EX_TOOLWINDOW, type, null, WS.WS_POPUP | WS.WS_VISIBLE,
                rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, HWND.NULL, HMENU.NULL, HINSTANCE.NULL, null);
            Assert.True(window != HWND.NULL);
            return window;
        }
    }

    private static RECT Rect(HWND window)
    {
        RECT rect;
        Assert.True(GetWindowRect(window, &rect));
        return rect;
    }

    /// <summary>The window's place once the asynchronous moves asked of it are done.</summary>
    private static RECT Settled(HWND window)
    {
        MSG msg;
        for (var turn = 0; turn < 20; turn++)
        {
            while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
            {
                _ = TranslateMessage(&msg);
                _ = DispatchMessageW(&msg);
            }
            Thread.Sleep(10);
        }
        return Rect(window);
    }
}
