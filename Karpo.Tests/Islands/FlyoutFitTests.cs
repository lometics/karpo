// The one rule every flyout's height follows, held against the host itself: a flyout is as tall as
// what it shows, up to one most (FlyoutPlacement.MostHeight, 70 % of the work area and never past
// it), and it follows what it shows while it stands — it grows and it shrinks at once, the pointer
// on it or not (FlyoutHost.Fit).
// The flyouts' own tests hold each of them to it; this holds the host to it with a bare panel,
// measured as Windows has the window.
//
// And one thing the host keeps per thread that it used to keep per process: which windows handle
// their own presses (FlyoutHost.PressesHandledBy).
//
// The runs need a window station and composition, so they only run with KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using Edge = Karpo.Core.Edge;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Islands;

public sealed unsafe class FlyoutFitTests
{
    /// <summary>A window class of this test's own (see the note at the top of FlyoutOffscreenTests).</summary>
    private const string ClassName = "karpo.fit.test";

    private static readonly RECT Work = new() { left = 0, top = 0, right = 1920, bottom = 1040 };
    private static readonly POINT Anchor = new() { x = 960, y = 1040 };

    private static readonly Look Dark = new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    // --- The most ----------------------------------------------------------------

    /// <summary>Seventy per cent of the work area, whichever edge the bar is on and at every scale.</summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void TheMostIsSeventyPerCentOfTheWorkArea(float scale)
    {
        Assert.Equal(0.7f, FlyoutPlacement.MostShare);
        var seventy = MathF.Floor(0.7f * 1040);
        Assert.Equal(seventy, new FlyoutPlacement(Anchor, Work, Edge.Bottom).MostHeight(scale));
        Assert.Equal(seventy, new FlyoutPlacement(new POINT { x = 960, y = 0 }, Work, Edge.Top).MostHeight(scale));
        Assert.Equal(seventy, new FlyoutPlacement(new POINT { x = 0, y = 520 }, Work, Edge.Left).MostHeight(scale));
        Assert.Equal(seventy, new FlyoutPlacement(new POINT { x = 1920, y = 520 }, Work, Edge.Right).MostHeight(scale));
        // Flush with the far end or not, the work area's share is the same.
        Assert.Equal(seventy, new FlyoutPlacement(Anchor, Work, Edge.Bottom, Align: FlyoutAlign.End).MostHeight(scale));
    }

    /// <summary>
    /// Never past the work area: a bar standing inside a taller work area (a display beside a taller
    /// one) leaves less than the share between it and the far end, and the most is what is left.
    /// </summary>
    [Fact]
    public void TheMostNeverReachesPastTheWorkArea()
    {
        // The room over an anchor at 400: less the 12 px gap, and the 12 px kept from the top.
        Assert.Equal(400 - 12 - 12, new FlyoutPlacement(new POINT { x = 960, y = 400 }, Work, Edge.Bottom).MostHeight(1));
        // Under an anchor at 800 on a top bar: less the gap and the 12 px kept from the bottom.
        Assert.Equal(1040 - 12 - (800 + 12), new FlyoutPlacement(new POINT { x = 960, y = 800 }, Work, Edge.Top).MostHeight(1));
        // The layouts drawn without a placement use the rule itself.
        Assert.Equal(MathF.Floor(0.7f * 1040), FlyoutPlacement.MostHeight(1040, 1040, 1));
        Assert.Equal(300, FlyoutPlacement.MostHeight(1040, 300, 1));
    }

    /// <summary>A work area too short for its share to hold a header and a row or two still gets them.</summary>
    [Fact]
    public void ATinyWorkAreaStillGetsAHeaderAndARowOrTwo()
    {
        var tiny = new RECT { left = 0, top = 0, right = 400, bottom = 200 };
        var anchor = new POINT { x = 200, y = 200 };
        Assert.Equal(FlyoutPlacement.MostFloorDip, new FlyoutPlacement(anchor, tiny, Edge.Bottom).MostHeight(1));
        Assert.Equal(FlyoutPlacement.MostFloorDip * 1.5f, new FlyoutPlacement(anchor, tiny, Edge.Bottom).MostHeight(1.5f));
    }

    // --- Following what it shows -------------------------------------------------

    /// <summary>
    /// The rule, on a bare panel: taller at once, and shorter at once — the pointer on it or not
    /// (the person: "it should always update the layout. not holding back when i am there with my
    /// mouse"). A shrink used to wait for the pointer to leave; nothing waits now. Never a cut.
    /// Measured in the window's own pixels.
    /// </summary>
    [UiFact]
    public void APanelFollowsWhatItShowsAtOnceWhereverThePointerIs()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            FlyoutOffscreenTests.Register(instance, ClassName);
            var placement = new FlyoutPlacement(Anchor, Work, Edge.Bottom);
            using var host = new FlyoutHost(gfx, instance, 0, FlyoutMode.Menu, ClassName);
            host.Show(gfx, Dark, 1, new Vector2(360, 200), placement);
            Assert.Equal(new Vector2(360, 200), host.PanelSize);
            // The shadow's room around the panel, which no height changes; and the edge on the bar.
            var room = Fitting.WindowHeight(host.Hwnd) - 200;
            var bar = Fitting.WindowBottom(host.Hwnd);
            Assert.Equal(Anchor.y, bar);

            // Taller content: the panel grows, the window by as much, and the edge on the bar stays.
            Assert.Equal(new Vector2(360, 320), host.Fit(new Vector2(360, 320)));
            Assert.Equal(320 + room, Fitting.WindowHeight(host.Hwnd));
            Assert.Equal(bar, Fitting.WindowBottom(host.Hwnd));

            // Shorter content with the pointer elsewhere: at once.
            Assert.Equal(new Vector2(360, 240), host.Fit(new Vector2(360, 240)));
            Assert.Equal(240 + room, Fitting.WindowHeight(host.Hwnd));

            // The pointer on the panel: a shrink is laid out at once all the same, the edge on the
            // bar kept, the far edge coming down.
            Assert.Equal(FlyoutHostEventKind.Entered, host.Message(WM.WM_MOUSEMOVE, 0, Fitting.Point(180, 140)).Kind);
            Assert.True(host.Inside);
            Assert.Equal(new Vector2(360, 100), host.Fit(new Vector2(360, 100)));
            Assert.Equal(100 + room, Fitting.WindowHeight(host.Hwnd));
            Assert.Equal(bar, Fitting.WindowBottom(host.Hwnd));
            // Shorter and taller again, still with the pointer on it: each at once.
            Assert.Equal(new Vector2(360, 300), host.Fit(new Vector2(360, 300)));
            Assert.Equal(300 + room, Fitting.WindowHeight(host.Hwnd));
            Assert.Equal(new Vector2(400, 150), host.Fit(new Vector2(400, 150)));
            Assert.Equal(150 + room, Fitting.WindowHeight(host.Hwnd));
            Assert.Equal(bar, Fitting.WindowBottom(host.Hwnd));
            // A fraction is a whole px, and no more.
            Assert.Equal(new Vector2(400, 121), host.Fit(new Vector2(400, 120.25f)));

            // The pointer leaving changes nothing: there is nothing left to settle.
            Assert.Equal(FlyoutHostEventKind.Left, host.Message(WM.WM_MOUSELEAVE, 0, 0).Kind);
            Assert.Equal(new Vector2(400, 121), host.PanelSize);
            Assert.Equal(121 + room, Fitting.WindowHeight(host.Hwnd));

            // Never a cut: past the most, what is asked is given. The overflow is the owner's.
            var most = placement.MostHeight(1);
            Assert.Equal(most + 40, host.Fit(new Vector2(400, most + 40)).Y);

            // A new showing starts at the size it is shown at.
            host.Show(gfx, Dark, 1, new Vector2(360, 200), placement);
            Assert.Equal(new Vector2(360, 200), host.PanelSize);

            // Closed, a fit asks nothing of a window that is not there.
            host.Hide();
            Assert.Equal(new Vector2(360, 90), host.Fit(new Vector2(360, 90)));
            Pump();
        });
    }

    // --- Per thread ----------------------------------------------------------------

    /// <summary>
    /// Which windows handle their own presses is the setting thread's own, as the open menus and
    /// the hook are. It was one static for the process, so a test that said "this window is the
    /// bar" on its thread had the hook of every other thread believe it too, and whichever thread
    /// set it last decided for all of them.
    /// </summary>
    [Fact]
    public void WhichWindowsHandleTheirOwnPressesIsEachThreadsOwn()
    {
        var bar = (HWND)(void*)0x1234;
        var other = (HWND)(void*)0x5678;
        FlyoutHost.PressesHandledBy = window => window == bar;
        try
        {
            var seen = true;
            var thread = new Thread(() =>
            {
                seen = FlyoutHost.PressesHandledBy is not null;
                // Another thread's own answer, which must stay its own.
                FlyoutHost.PressesHandledBy = _ => true;
            });
            thread.Start();
            thread.Join();
            Assert.False(seen, "another thread saw this thread's answer");
            Assert.True(FlyoutHost.PressesHandledBy!(bar));
            Assert.False(FlyoutHost.PressesHandledBy!(other));
        }
        finally
        {
            FlyoutHost.PressesHandledBy = null;
        }
    }

    // --- The thread it runs on -------------------------------------------------------

    private static void Pump()
    {
        MSG message;
        while (PeekMessageW(&message, HWND.NULL, 0, 0, PM.PM_REMOVE))
        {
            _ = TranslateMessage(&message);
            _ = DispatchMessageW(&message);
        }
    }

    /// <summary>Runs <paramref name="body"/> on an STA thread of its own, on a desktop of its own that nobody sees.</summary>
    private static void OnHiddenDesktop(Action<Gfx, HINSTANCE> body)
    {
        Exception? failed = null;
        HDESK hidden = default;
        var thread = new Thread(() =>
        {
            try
            {
                fixed (char* name = "karpo-fit-test-" + Guid.NewGuid().ToString("N"))
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
                    body(gfx, (HINSTANCE)(void*)GetModuleHandleW(null));
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
        thread.Start();
        thread.Join();
        HiddenDesktop.Close(hidden);
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the hidden desktop: " + failed);
        }
    }
}
