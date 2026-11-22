// What the full screen look reads off a real window (Karpo.Dock\FrontWatch.cs, §272), on a hidden
// desktop: a borderless window over a whole display, a maximized one, a minimized one, a hidden one, one
// gone. The decisions on what is read are FullscreenTests' and BarKeeperTests'; this is that the
// reading itself is right, against real windows.
//
// What can't be had here: a Direct3D swap chain in exclusive full screen. It takes the output of a
// display on the desktop the person sees — on a hidden desktop SetFullscreenState has nothing to take
// —, and anywhere else it would change the person's own display mode; the BarWorld scenarios stand in
// for it (BarKeeperTests.AnExclusiveFullScreenGameIsNeverDisturbed).

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.DesktopsArea;

public sealed unsafe class FrontWatchTests
{
    /// <summary>Runs <paramref name="body"/> on a thread of its own on a hidden desktop, which is closed after.</summary>
    private static void OnHiddenDesktop(Action body)
    {
        HDESK hidden = default;
        Exception? failed = null;
        var thread = new Thread(() =>
        {
            try
            {
                fixed (char* name = "karpo-front-" + Guid.NewGuid().ToString("N"))
                {
                    hidden = CreateDesktopW(name, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
                }
                Assert.True(hidden != HDESK.NULL, "no hidden desktop");
                Assert.True(SetThreadDesktop(hidden));
                body();
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

    private static HWND Window(uint style, RECT rect)
    {
        fixed (char* type = "Static")
        {
            return CreateWindowExW(0, type, null, style, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, HWND.NULL, HMENU.NULL, HINSTANCE.NULL, null);
        }
    }

    /// <summary>The primary display, as Windows has it.</summary>
    private static (RECT Bounds, string Device) Primary()
    {
        var monitor = MonitorFromPoint(new POINT { x = 0, y = 0 }, MONITOR.MONITOR_DEFAULTTOPRIMARY);
        var info = new MONITORINFOEXW();
        info.Base.cbSize = (uint)sizeof(MONITORINFOEXW);
        Assert.True(GetMonitorInfoW(monitor, (MONITORINFO*)&info));
        return (info.Base.rcMonitor, new string(&info.szDevice.e0));
    }

    /// <summary>A borderless window over the whole display: read as covering it, on that display, with no caption — a full screen app but for being Karpo's own.</summary>
    [UiFact]
    public void ABorderlessWindowOverTheDisplayReadsAsFullScreen()
    {
        OnHiddenDesktop(() =>
        {
            var (bounds, device) = Primary();
            var window = Window(WS.WS_POPUP, bounds);
            Assert.True(window != HWND.NULL);
            try
            {
                ShowWindow(window, SW.SW_SHOWNOACTIVATE);
                var facts = FrontWatch.Read(window);
                Assert.NotNull(facts);
                var seen = facts.Value;
                Assert.Equal(device, seen.Display);
                Assert.Equal(bounds, seen.DisplayBounds);
                Assert.Equal(bounds, seen.Rect);
                Assert.True(seen.Visible);
                Assert.False(seen.Minimized);
                Assert.False(seen.Maximized);
                Assert.False(seen.Captioned);
                Assert.Equal("Static", seen.Class);
                // The test's own process made it: Karpo's own, never a full screen app...
                Assert.True(seen.Own);
                Assert.False(FullscreenApps.Fills(seen));
                Assert.False(FullscreenApps.Fronts(seen));
                // ...and another app's window read the same would be.
                Assert.True(FullscreenApps.Fills(seen with { Own = false }));
                Assert.True(FullscreenApps.Fronts(seen with { Own = false }));
            }
            finally
            {
                DestroyWindow(window);
            }
        });
    }

    /// <summary>A maximized window with a caption, minimized, hidden: none of them a full screen app, read off the window as each is.</summary>
    [UiFact]
    public void MaximizedMinimizedAndHiddenAreReadSo()
    {
        OnHiddenDesktop(() =>
        {
            var (bounds, _) = Primary();
            var window = Window(WS.WS_OVERLAPPEDWINDOW, new RECT { left = bounds.left + 100, top = bounds.top + 100, right = bounds.left + 900, bottom = bounds.top + 700 });
            Assert.True(window != HWND.NULL);
            try
            {
                ShowWindow(window, SW.SW_SHOWMAXIMIZED);
                var maximized = FrontWatch.Read(window)!.Value with { Own = false };
                Assert.True(maximized.Maximized);
                Assert.True(maximized.Captioned);
                Assert.False(FullscreenApps.Fills(maximized));

                ShowWindow(window, SW.SW_SHOWMINNOACTIVE);
                var minimized = FrontWatch.Read(window)!.Value with { Own = false };
                Assert.True(minimized.Minimized);
                Assert.False(FullscreenApps.Fills(minimized));

                ShowWindow(window, SW.SW_HIDE);
                var hidden = FrontWatch.Read(window)!.Value with { Own = false };
                Assert.False(hidden.Visible);
                Assert.False(FullscreenApps.Fills(hidden));
            }
            finally
            {
                DestroyWindow(window);
            }
        });
    }

    /// <summary>A window gone reads as nothing: its display has no front any more.</summary>
    [UiFact]
    public void AWindowGoneReadsAsNothing()
    {
        OnHiddenDesktop(() =>
        {
            var (bounds, _) = Primary();
            var window = Window(WS.WS_POPUP, bounds);
            DestroyWindow(window);
            Assert.Null(FrontWatch.Read(window));
            Assert.Null(FrontWatch.Read(HWND.NULL));
        });
    }
}
