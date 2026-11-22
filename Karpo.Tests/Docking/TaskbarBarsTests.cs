// Which windows Karpo takes for Explorer's taskbars (Karpo.Dock\Taskbar.cs, Bars and TopLevel).
// Regression (2026-10-11): they were listed by EnumWindows, which skips every window outside band 1,
// and Explorer lifts its taskbars to band 6 while Task view is up, so Cover and Restore missed them
// there. A band-6 window cannot be made here (only a
// signed uiAccess app makes one), so what is held is the listing's other half: every top-level window
// of the class, hidden ones included, and never Karpo's own tray spy of the same class.

using System.Runtime.InteropServices;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Docking;

public sealed unsafe class TaskbarBarsTests
{
    [UiFact]
    public void EveryTopLevelWindowOfTheClassIsFoundHiddenOnesIncluded()
    {
        const string name = "KarpoTests.TaskbarBars.TopLevel";
        var first = Make(name);
        var second = Make(name);
        try
        {
            var found = Taskbar.TopLevel(name);
            Assert.Contains(first, found);
            Assert.Contains(second, found);
            Assert.False(IsWindowVisible(first));
        }
        finally
        {
            _ = DestroyWindow(first);
            _ = DestroyWindow(second);
        }
    }

    [UiFact]
    public void KarposOwnTrayWindowIsNeverOneOfExplorersBars()
    {
        // Karpo's tray spy is a Shell_TrayWnd of its own, empty and at 0,0: found by the class, never a bar.
        var own = Make("Shell_TrayWnd");
        var secondary = Make("Shell_SecondaryTrayWnd");
        try
        {
            Assert.Contains(own, Taskbar.TopLevel("Shell_TrayWnd"));
            Assert.Contains(secondary, Taskbar.TopLevel("Shell_SecondaryTrayWnd"));
            var bars = Taskbar.Bars();
            Assert.DoesNotContain(own, bars);
            Assert.DoesNotContain(secondary, bars);
            Assert.NotEqual(own, Taskbar.ExplorerTray());
        }
        finally
        {
            _ = DestroyWindow(own);
            _ = DestroyWindow(secondary);
        }
    }

    /// <summary>
    /// §542: a taskbar window blanked has an empty region — shown, DWM draws nothing of it — and put back it
    /// has none, its own. On a window of the test's own, never shown, as Karpo blanks Explorer's hidden taskbars.
    /// </summary>
    [UiFact]
    public void ABlankedWindowHasAnEmptyRegionAndPutBackNone()
    {
        var window = Make("KarpoTests.TaskbarBars.Blank");
        try
        {
            Assert.False(Surfaces.IsBlank(window));
            Surfaces.OtherAppBlank(window, blank: true);
            Assert.True(Surfaces.IsBlank(window));
            // Blanked twice is blanked still.
            Surfaces.OtherAppBlank(window, blank: true);
            Assert.True(Surfaces.IsBlank(window));
            Surfaces.OtherAppBlank(window, blank: false);
            Assert.False(Surfaces.IsBlank(window));
            var probe = CreateRectRgn(0, 0, 0, 0);
            try
            {
                // ERROR (0): the window has no region at all.
                Assert.Equal(0, GetWindowRgn(window, probe));
            }
            finally
            {
                _ = DeleteObject(probe);
            }
        }
        finally
        {
            _ = DestroyWindow(window);
        }
    }

    private static HWND Make(string name)
    {
        var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
        fixed (char* className = name)
        {
            var type = new WNDCLASSW { lpfnWndProc = &Procedure, hInstance = instance, lpszClassName = className };
            _ = RegisterClassW(&type);
            // A top-level window, never shown, as Karpo hides Explorer's taskbars.
            var hwnd = CreateWindowExW(WS.WS_EX_TOOLWINDOW | WS.WS_EX_NOACTIVATE, className, null, WS.WS_POPUP, 0, 0, 0, 0, HWND.NULL, HMENU.NULL, instance, null);
            Assert.True(hwnd != HWND.NULL, $"CreateWindowExW failed: error {Marshal.GetLastSystemError()}");
            return hwnd;
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT Procedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam) => DefWindowProcW(hwnd, msg, wParam, lParam);
}
