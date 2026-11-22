// What rule R2 leans on (docs\widgets.md §355: a popup's window spans its display and never moves while
// it shows; where the popup is, is composition; its region is what takes the pointer), each checked as
// the spike before it found it:
//   - a window's region takes the pointer inside it and lets it through everywhere else, over what the
//     window draws there too — and DWM shows nothing of the window outside it, so the region is where
//     the window stood (the panel and its shadow's room), not the panel alone;
//   - the window never covers its whole display, which Windows takes for a full screen app (do not
//     disturb turning on, a taskbar stepping back), and Karpo's own bars never make way for it;
//   - UI Automation reads a menu's bounds as its panel's, not its window's.

using System.Numerics;
using System.Runtime.InteropServices;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using Edge = Karpo.Core.Edge;
using ICompositorDesktopInterop = TerraFX.Interop.WinRT.ICompositorDesktopInterop;
using IDesktopWindowTarget = TerraFX.Interop.WinRT.IDesktopWindowTarget;

namespace Karpo.Tests.Present;

public sealed unsafe class FixedFrameTests
{
    private const string FrameClass = "karpo.tests.frame";

    private static readonly RECT Display = new() { right = 1280, bottom = 720 };

    [UiFact]
    public void ARegionTakesThePointerThereAndLetsItThroughElsewhere()
    {
        WindowShots.OnHiddenDesktop("frame-region", (gfx, instance) =>
        {
            Register(instance);
            using var under = new Plain(gfx, instance, Display, Color.FromArgb(255, 0, 0, 255));
            using var over = new Plain(gfx, instance, Display, Color.FromArgb(0, 0, 0, 0));
            // Drawn inside the region and outside it.
            var inside = Square(gfx, Color.FromArgb(255, 0, 255, 0), new Vector2(150, 150));
            var outside = Square(gfx, Color.FromArgb(255, 255, 0, 0), new Vector2(600, 400));
            over.Root.Children.InsertAtTop(inside);
            over.Root.Children.InsertAtTop(outside);
            Surfaces.Region(over.Hwnd, new RECT { left = 100, top = 100, right = 400, bottom = 300 });
            WindowShots.Settle(150);
            Assert.Equal(over.Hwnd, WindowFromPoint(new POINT { x = 200, y = 200 }));
            Assert.Equal(under.Hwnd, WindowFromPoint(new POINT { x = 50, y = 50 }));
            // Over what the window draws, outside its region: the pointer goes through.
            Assert.Equal(under.Hwnd, WindowFromPoint(new POINT { x = 650, y = 450 }));
            byte[] pixels = [];
            var width = 0;
            Assert.True(WindowShots.Save(over.Hwnd, "frame-region.png", new RECT { right = 1280, bottom = 720 }, (seen, w) => (pixels, width) = (seen, w)));
            byte Green(int x, int y) => pixels[(((y * width) + x) * 4) + 1];
            byte Red(int x, int y) => pixels[(((y * width) + x) * 4) + 2];
            Assert.Equal(255, Green(175, 175));
            // Nothing of the window shows outside its region: the region is where the popup is drawn.
            Assert.Equal(0, Red(650, 450));
        });
    }

    [Fact]
    public void APopupsWindowNeverCoversItsDisplay()
    {
        foreach (var display in new[] { Display, new RECT { left = -2560, right = 0, bottom = 1440 }, new RECT { left = 1920, top = -200, right = 3840, bottom = 880 } })
        {
            var frame = FlyoutHost.FrameOn(display);
            Assert.False(FullscreenApps.Covers(frame, display));
            Assert.Equal((display.left, display.top, display.right - 1, display.bottom - 1), (frame.left, frame.top, frame.right, frame.bottom));
            // Nor would Karpo's own bars make way for it, covering or not: it is one of Karpo's.
            var facts = new WindowFacts("Karpo.Flyout", @"\\.\DISPLAY1", display, display, 0, Visible: true, Minimized: false, Cloaked: false, Maximized: false, Captioned: false, Own: true);
            Assert.False(FullscreenApps.Fills(facts));
            Assert.False(FullscreenApps.Fronts(facts));
        }
    }

    [UiFact]
    public void AMenusWindowStandsStillOnItsDisplayAndItsRegionIsWhereItStands()
    {
        WindowShots.OnHiddenDesktop("frame-menu", (gfx, instance) =>
        {
            Register(instance);
            using var menu = new Flyout(gfx, instance, 0, className: FrameClass);
            var look = AddListPictures.LookFor(dark: true) with { Animations = false };
            menu.Show(gfx, look, 1f, Rows(4), FlyoutLayout.List, new POINT { x = 640, y = 720 }, Display, Edge.Bottom, null);
            FrameFilm.Settle();
            RECT window;
            Assert.True(GetWindowRect(menu.Hwnd, &window));
            var monitor = MonitorFromRect(&window, MONITOR.MONITOR_DEFAULTTONEAREST);
            var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
            Assert.True(GetMonitorInfoW(monitor, &info));
            Assert.Equal(FlyoutHost.FrameOn(info.rcMonitor), window);
            var shown = menu.PanelOnScreen;
            Assert.Equal(menu.Hwnd, WindowFromPoint(new POINT { x = (shown.left + shown.right) / 2, y = (shown.top + shown.bottom) / 2 }));
            Assert.NotEqual(menu.Hwnd, WindowFromPoint(new POINT { x = 20, y = 20 }));
            menu.Refresh(Rows(9));
            FrameFilm.Settle();
            var taller = menu.PanelOnScreen;
            Assert.True(taller.top < shown.top);
            Assert.Equal(menu.Hwnd, WindowFromPoint(new POINT { x = (taller.left + taller.right) / 2, y = taller.top + 8 }));
            // Placed once, as it was made; the panel grew in it.
            Assert.Equal(1, menu.HostMade.Frames);
        });
    }

    [UiFact]
    public void UiAutomationReadsAMenuAsItsPanel()
    {
        WindowShots.OnHiddenDesktop("frame-uia", (gfx, instance) =>
        {
            Register(instance);
            using var menu = new Flyout(gfx, instance, 0, className: FrameClass);
            var look = AddListPictures.LookFor(dark: true) with { Animations = false };
            menu.Show(gfx, look, 1f, Rows(4), FlyoutLayout.List, new POINT { x = 640, y = 720 }, Display, Edge.Bottom, null);
            FrameFilm.Settle();
            var panel = menu.PanelOnScreen;
            RECT read = default;
            Exception? failed = null;
            var desktop = GetThreadDesktop(GetCurrentThreadId());
            var client = new Thread(() =>
            {
                try
                {
                    _ = SetThreadDesktop(desktop);
                    read = BoundsOf(menu.Hwnd);
                }
                catch (Exception error)
                {
                    failed = error;
                }
            });
            client.SetApartmentState(ApartmentState.MTA);
            client.Start();
            while (client.IsAlive)
            {
                FrameFilm.Pump();
                Thread.Sleep(5);
            }
            Assert.Null(failed);
            Assert.Equal(panel, read);
        });
    }

    // --- Helpers ---------------------------------------------------------------------------------

    private static List<FlyoutItem> Rows(int count) =>
        [.. Enumerable.Range(1, count).Select(index => new FlyoutItem((uint)index, $"Row {index}", new FlyoutIcon.Glyph("\uE8A5")))];

    private static SpriteVisual Square(Gfx gfx, Color color, Vector2 at)
    {
        var square = gfx.Compositor.CreateSpriteVisual();
        square.Brush = gfx.Compositor.CreateColorBrush(color);
        square.Size = new Vector2(100, 100);
        square.Offset = new Vector3(at, 0);
        return square;
    }

    /// <summary>
    /// What a UI Automation client reads of <paramref name="window"/>'s element: its bounding rectangle
    /// (IUIAutomation.ElementFromHandle, IUIAutomationElement.CurrentBoundingRectangle), by hand, as the
    /// dock's other COM is.
    /// </summary>
    private static RECT BoundsOf(HWND window)
    {
        var clsid = new Guid("ff48dba4-60ef-4201-aa87-54103eef594e");
        var iid = new Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee");
        _ = CoInitializeEx(null, (uint)COINIT.COINIT_MULTITHREADED);
        void* automation = null;
        Marshal.ThrowExceptionForHR(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, &iid, &automation));
        void* element = null;
        try
        {
            // IUIAutomation: QueryInterface, AddRef, Release, CompareElements, CompareRuntimeIds, GetRootElement, ElementFromHandle.
            Marshal.ThrowExceptionForHR(((delegate* unmanaged<void*, HWND, void**, int>)(*(void***)automation)[6])(automation, window, &element));
            RECT rect;
            // IUIAutomationElement's get_CurrentBoundingRectangle, its 44th method.
            Marshal.ThrowExceptionForHR(((delegate* unmanaged<void*, RECT*, int>)(*(void***)element)[43])(element, &rect));
            return rect;
        }
        finally
        {
            if (element != null)
            {
                ((IUnknown*)element)->Release();
            }
            ((IUnknown*)automation)->Release();
        }
    }

    private static void Register(HINSTANCE instance)
    {
        fixed (char* name = FrameClass)
        {
            var type = new WNDCLASSW { lpfnWndProc = &Procedure, hInstance = instance, lpszClassName = name, hCursor = LoadCursorW(HINSTANCE.NULL, IDC.IDC_ARROW) };
            _ = RegisterClassW(&type);
        }
    }

    /// <summary>The dock's answers to UI Automation, as Dock.WndProc gives them; the rest Windows' own.</summary>
    [UnmanagedCallersOnly]
    private static LRESULT Procedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == WM.WM_GETOBJECT && Uia.Answer(hwnd, wParam, lParam) is { } provided)
        {
            return provided;
        }
        if (msg == Uia.WM_KARPO_UIA_TREE)
        {
            return Uia.Rebuild(hwnd, now: true);
        }
        if (msg == WM.WM_NCDESTROY)
        {
            Uia.Forget(hwnd);
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    /// <summary>A window of one colour over the fake display: topmost, never activated, composition only.</summary>
    private sealed class Plain : IDisposable
    {
        private readonly DesktopWindowTarget target;

        public Plain(Gfx gfx, HINSTANCE instance, RECT at, Color color)
        {
            fixed (char* name = FrameClass)
            fixed (char* title = "Karpo frame")
            {
                Hwnd = CreateWindowExW(
                    WS.WS_EX_TOOLWINDOW | WS.WS_EX_TOPMOST | WS.WS_EX_NOACTIVATE | WS.WS_EX_NOREDIRECTIONBITMAP,
                    name, title, WS.WS_POPUP, at.left, at.top, at.right - at.left, at.bottom - at.top, HWND.NULL, HMENU.NULL, instance, null);
            }
            Assert.True(Hwnd != HWND.NULL);
            var c = gfx.Compositor;
            var desktop = ComUtil.Query<ICompositorDesktopInterop>(c);
            try
            {
                IDesktopWindowTarget* raw = null;
                ComUtil.Check(desktop->CreateDesktopWindowTarget(Hwnd, BOOL.TRUE, &raw));
                target = ComUtil.Adopt<DesktopWindowTarget>(raw);
            }
            finally
            {
                desktop->Release();
            }
            Root = c.CreateContainerVisual();
            Root.Size = new Vector2(at.right - at.left, at.bottom - at.top);
            var fill = c.CreateSpriteVisual();
            fill.Brush = c.CreateColorBrush(color);
            fill.Size = Root.Size;
            Root.Children.InsertAtTop(fill);
            target.Root = Root;
            Surfaces.Show(Hwnd);
        }

        public HWND Hwnd { get; }

        public ContainerVisual Root { get; }

        public void Dispose() => Surfaces.Destroy(Hwnd, () =>
        {
            target.Root = null;
            target.Dispose();
        });
    }
}
