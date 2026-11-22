// Which windows get a button (Apps.Listed, §204), each row as Explorer's own taskbar answered for a
// window made with those facts on Windows 11 (build 26300, 2026-10-17), read from its buttons by UI
// Automation. Regression: WS_EX_APPWINDOW won over WS_EX_TOOLWINDOW, so Raycast's launcher (both,
// and WS_EX_NOACTIVATE) had a button on Karpo's bar and none on Windows'.

using System.Runtime.InteropServices;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.DesktopsArea;

public sealed unsafe class TaskbarListedTests
{
    private const uint Tool = (uint)WS.WS_EX_TOOLWINDOW;
    private const uint App = (uint)WS.WS_EX_APPWINDOW;
    private const uint NoActivate = (uint)WS.WS_EX_NOACTIVATE;
    private const uint Layered = (uint)WS.WS_EX_LAYERED;
    private const uint Transparent = (uint)WS.WS_EX_TRANSPARENT;
    private const uint Topmost = (uint)WS.WS_EX_TOPMOST;

    [Theory]
    // extended style, owned, DeleteTab called, Explorer's button
    [InlineData(0u, false, false, true)]
    [InlineData(Tool, false, false, false)]
    [InlineData(App, false, false, true)]
    [InlineData(Tool | App, false, false, false)]
    [InlineData(NoActivate, false, false, false)]
    [InlineData(NoActivate | App, false, false, true)]
    [InlineData(NoActivate | Tool, false, false, false)]
    // Raycast's launcher, with and without WS_EX_NOACTIVATE.
    [InlineData(NoActivate | Layered | App | Tool, false, false, false)]
    [InlineData(Layered | App | Tool, false, false, false)]
    [InlineData(0u, true, false, false)]
    [InlineData(App, true, false, true)]
    [InlineData(Tool | App, true, false, false)]
    [InlineData(Layered, false, false, true)]
    [InlineData(Layered | Transparent, false, false, true)]
    [InlineData(Topmost, false, false, true)]
    [InlineData(Topmost | App, false, false, true)]
    [InlineData(0u, false, true, false)]
    [InlineData(App, false, true, false)]
    public void AWindowGetsAButtonAsOnExplorersTaskbar(uint extended, bool owned, bool deleted, bool listed)
    {
        Assert.Equal(listed, Apps.Listed(extended, owned, deleted));
    }

    [UiFact]
    public void DeleteTabsMarkTakesTheButtonAwayAndAddTabGivesItBack()
    {
        // Explorer leaves this property on a window ITaskbarList::DeleteTab names and removes it on
        // AddTab; set here by hand, as Explorer is not there to call.
        const string name = "KarpoTests.TaskbarListed";
        var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
        fixed (char* className = name)
        fixed (char* deleted = "ITaskList_Deleted")
        {
            var type = new WNDCLASSW { lpfnWndProc = &Procedure, hInstance = instance, lpszClassName = className };
            _ = RegisterClassW(&type);
            var hwnd = CreateWindowExW(0, className, null, WS.WS_POPUP, 0, 0, 0, 0, HWND.NULL, HMENU.NULL, instance, null);
            Assert.True(hwnd != HWND.NULL, $"CreateWindowExW failed: error {Marshal.GetLastSystemError()}");
            try
            {
                Assert.True(Apps.Listed(hwnd));
                _ = SetPropW(hwnd, deleted, (HANDLE)1);
                Assert.False(Apps.Listed(hwnd));
                _ = RemovePropW(hwnd, deleted);
                Assert.True(Apps.Listed(hwnd));
            }
            finally
            {
                _ = DestroyWindow(hwnd);
            }
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT Procedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam) => DefWindowProcW(hwnd, msg, wParam, lParam);
}
