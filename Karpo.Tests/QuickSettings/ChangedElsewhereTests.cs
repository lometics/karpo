// Widgets that show the PC's state follow it when it is changed outside Karpo (docs\widgets.md §439;
// the person: "when i change the brightness on my keyboard it does not change it on the widget. fix
// that. also fix that for other widgets if there is a similar bug for them"). The brightness has its
// own (BrightnessTests, BrightnessWidgetTests); here the others the audit found not following: the
// mobile hotspot switched in Windows' own panel (its switch, and the source of its face and page),
// the magnifier started or stopped by its shortcuts, and the power mode a battery line says.
//
// Nothing here reaches the PC: the hotspot is handed nothing to find and its news by hand, the
// magnifier's tools are made up, and the battery is SampleBattery.

using System.Runtime.InteropServices;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.QuickSettings;

public sealed unsafe class ChangedElsewhereTests
{
    /// <summary>
    /// The hotspot switched in Windows' own panel tells the switch only through the network — its
    /// adapter and address coming or going (InterfaceWatch): the news finds the manager again and
    /// draws the button at once, and once more a moment after the last of a burst, when the hotspot
    /// has settled. What it follows is let go with it.
    /// </summary>
    [Fact]
    public void TheHotspotFollowsTheNetworksNews()
    {
        var clock = new TestClock();
        Action? heard = null;
        var followed = new Followed();
        var found = 0;
        var hotspot = new HotspotSwitch(() =>
        {
            Interlocked.Increment(ref found);
            return null;
        }, news =>
        {
            heard = news;
            return followed;
        }, clock);
        var drawn = 0;
        hotspot.Start(() => Interlocked.Increment(ref drawn));
        Wait(() => Volatile.Read(ref drawn) == 1);
        Assert.NotNull(heard);
        Assert.Null(hotspot.On);

        // A burst of news, as the hotspot's adapter comes up and is given its address: drawn, a look
        // for each at most — one waiting to start reads what the news after it would.
        heard!();
        heard!();
        heard!();
        Wait(() => Volatile.Read(ref drawn) >= 2);
        Thread.Sleep(100);
        var burst = Volatile.Read(ref drawn);
        Assert.InRange(burst, 2, 4);
        Assert.Equal(burst, Volatile.Read(ref found));

        // Once more after the last of it, when the hotspot has settled; then nothing.
        clock.Advance(TimeSpan.FromMilliseconds(HotspotSwitch.AgainMs - 1));
        Thread.Sleep(50);
        Assert.Equal(burst, Volatile.Read(ref drawn));
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Wait(() => Volatile.Read(ref drawn) == burst + 1);
        clock.Advance(TimeSpan.FromMilliseconds(HotspotSwitch.AgainMs * 2));
        Thread.Sleep(50);
        Assert.Equal(burst + 1, Volatile.Read(ref drawn));

        hotspot.Dispose();
        Assert.True(followed.Gone);
        // Windows' own is made the same way, and asks nothing until it starts.
        using var real = QuickSwitches.Windows(Karpo.Core.QuickSwitch.MobileHotspot);
        Assert.IsType<HotspotSwitch>(real);
    }

    /// <summary>
    /// The hotspot face's and page's source (WindowsHotspot) hears the same news as the switch: switched
    /// in Windows' own panel, it reads the hotspot again and says so at once — one read for a burst —
    /// and once more a moment after the last, rather than at the face's next 30 s look. What it
    /// followed is let go with it.
    /// </summary>
    [Fact]
    public void TheHotspotsFaceFollowsTheNetworksNews()
    {
        var clock = new TestClock();
        Action? heard = null;
        var followed = new Followed();
        var on = 0;
        var reads = 0;
        var said = 0;
        var source = new WindowsHotspot(news =>
        {
            heard = news;
            return followed;
        }, () =>
        {
            Interlocked.Increment(ref reads);
            return HotspotState.Unknown with { Can = HotspotCan.Yes, On = Volatile.Read(ref on) == 1 };
        }, clock);
        var news = new Changes(_ => Interlocked.Increment(ref said));
        source.Start(news);
        Wait(() => Volatile.Read(ref said) == 1);
        Assert.NotNull(heard);
        Assert.False(source.Read().On ?? true);
        _ = news.Take();

        // Switched on in Windows' own panel: its adapter comes up and is given its address.
        Volatile.Write(ref on, 1);
        heard!();
        heard!();
        heard!();
        Wait(() => Volatile.Read(ref said) == 2);
        Assert.True(source.Read().On ?? false);
        Thread.Sleep(100);
        var burst = Volatile.Read(ref reads);
        Assert.InRange(burst, 2, 4);
        _ = news.Take();

        // Once more after the last of it; then nothing.
        clock.Advance(TimeSpan.FromMilliseconds(HotspotSwitch.AgainMs - 1));
        Thread.Sleep(50);
        Assert.Equal(burst, Volatile.Read(ref reads));
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Wait(() => Volatile.Read(ref reads) == burst + 1);
        clock.Advance(TimeSpan.FromMilliseconds(HotspotSwitch.AgainMs * 2));
        Thread.Sleep(50);
        Assert.Equal(burst + 1, Volatile.Read(ref reads));

        source.Dispose();
        Assert.True(followed.Gone);
    }

    /// <summary>
    /// The magnifier started or stopped by Win+Plus or Win+Esc says so only through its window, which
    /// the dock's shell hook hands on: its switch reads it a moment later — going, the window still
    /// stands as the news comes — and the other two switches take no notice.
    /// </summary>
    [Fact]
    public void TheMagnifierIsReadAgainWhenItsWindowComesOrGoes()
    {
        var tools = new Tools();
        using var magnifier = new AssistiveSwitch(Karpo.Core.QuickSwitch.Magnifier, tools);
        using var narrator = new AssistiveSwitch(Karpo.Core.QuickSwitch.Narrator, tools);
        var heard = 0;
        var told = 0;
        magnifier.Start(() => Interlocked.Increment(ref heard));
        narrator.Start(() => Interlocked.Increment(ref told));
        Assert.False(magnifier.On);

        // Started by its shortcut, not by the widget.
        tools.Magnifying = true;
        magnifier.Heard(AssistiveSwitch.WM_KARPO_MAGNIFIER, 0);
        narrator.Heard(AssistiveSwitch.WM_KARPO_MAGNIFIER, 0);
        Assert.Equal(0, Volatile.Read(ref heard));
        Wait(() => Volatile.Read(ref heard) == 1);
        Assert.True(magnifier.On);
        Thread.Sleep(AssistiveSwitch.MagnifierAgainMs + 100);
        Assert.Equal(1, Volatile.Read(ref heard));
        Assert.Equal(0, Volatile.Read(ref told));
    }

    /// <summary>What the dock hands on from its shell hook: the magnifier's window, by its class, and no other.</summary>
    [Fact]
    public void OnlyTheMagnifiersWindowIsTheMagnifiers()
    {
        Assert.False(WindowsAssistive.IsMagnifier(HWND.NULL));
        var magnifier = Window("MagUIClass");
        var other = Window("KarpoTestNotTheMagnifier");
        try
        {
            Assert.True(WindowsAssistive.IsMagnifier(magnifier));
            Assert.False(WindowsAssistive.IsMagnifier(other));
        }
        finally
        {
            DestroyWindow(magnifier);
            DestroyWindow(other);
        }
    }

    /// <summary>
    /// The power mode moved in Windows' own Power &amp; battery page: the battery line — whose tallest
    /// form says the mode — follows its source from the first time it is drawn, and the
    /// source's news asks for it to be drawn again, as the power mode's own line is.
    /// </summary>
    [UiFact]
    public void ABatteryLineFollowsThePowerModeMovedElsewhere()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var battery = new SampleBattery();
            BatteryIsland.Made = () => battery;
            BatteryIsland.Probe = () => new SystemStatus.Battery(76, Plugged: false, Saver: false);
            var window = HWND.NULL;
            try
            {
                var cell = Karpo.Core.ListKind.Battery.New();
                cell.Id = "battery";
                using var line = Assert.IsType<BatteryIsland>(Karpo.Dock.Islands.CreateLine("layout:default/folder/battery", cell, Karpo.Core.ListKind.Battery, gfx, new Karpo.Core.Config()));
                fixed (char* cls = "STATIC")
                {
                    window = CreateWindowExW(0, cls, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, HINSTANCE.NULL, null);
                }
                line.Attach((nint)window.Value, 11);
                Assert.False(battery.Followed);
                var look = TestLooks.LookFor(dark: true);
                var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                var width = FolderFlyout.ListWidth(1.5f);
                // Measured, it asks the source nothing; drawn — tall enough to say the mode — it follows it from then.
                var height = line.ListHeight(width, style) * 2;
                Assert.False(battery.Followed);
                Offscreen.Bitmap(width, height, dc => line.PaintCard(dc, PopupPaint.Rect(0, 0, width, height), style, WidgetSurface.List), _ => { });
                Assert.True(battery.Followed);
                Drain(window);

                battery.Turn(PowerMode.Performance);
                Assert.True(Posted(window, 11), "the battery line was not asked to be drawn again");
                Assert.Equal(PowerMode.Performance, battery.Read().Mode);
            }
            finally
            {
                BatteryIsland.Made = null;
                BatteryIsland.Probe = SystemStatus.ReadBatteryNow;
                if (window != HWND.NULL)
                {
                    DestroyWindow(window);
                }
            }
        });
    }

    private static void Drain(HWND window)
    {
        MSG msg;
        while (PeekMessageW(&msg, window, Karpo.Dock.Islands.WM_KARPO_ISLAND, Karpo.Dock.Islands.WM_KARPO_ISLAND, PM.PM_REMOVE))
        {
            // §658: taken, as the dock takes it, so the widget's next news posts again.
            Karpo.Dock.Islands.Taken((nint)window, (nuint)msg.wParam);
        }
    }

    /// <summary>Whether the widget of <paramref name="token"/> asked to be drawn again, within five seconds.</summary>
    private static bool Posted(HWND window, uint token)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < until)
        {
            MSG msg;
            while (PeekMessageW(&msg, window, Karpo.Dock.Islands.WM_KARPO_ISLAND, Karpo.Dock.Islands.WM_KARPO_ISLAND, PM.PM_REMOVE))
            {
                Karpo.Dock.Islands.Taken((nint)window, (nuint)msg.wParam);
                if ((uint)(nuint)msg.wParam == token)
                {
                    return true;
                }
            }
            Thread.Sleep(2);
        }
        return false;
    }

    /// <summary>A message-only window of the class <paramref name="name"/>, registered here for it.</summary>
    private static HWND Window(string name)
    {
        fixed (char* className = name)
        {
            var wc = new WNDCLASSW { lpfnWndProc = &Proc, lpszClassName = className, hInstance = GetModuleHandleW(null) };
            _ = RegisterClassW(&wc);
            var window = CreateWindowExW(0, className, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, wc.hInstance, null);
            Assert.NotEqual(HWND.NULL, window);
            return window;
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT Proc(HWND window, uint msg, WPARAM wParam, LPARAM lParam) => DefWindowProcW(window, msg, wParam, lParam);

    private static void Wait(Func<bool> done)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!done())
        {
            Assert.True(DateTime.UtcNow < until, "it did not get there");
            Thread.Sleep(5);
        }
    }

    /// <summary>What the hotspot followed, let go of or not.</summary>
    private sealed class Followed : IDisposable
    {
        public bool Gone { get; private set; }

        public void Dispose() => Gone = true;
    }

    private sealed class Tools : IAssistiveTools
    {
        public bool Magnifying { get; set; }

        public bool? Reading => false;

        public bool? Sticky => false;

        public void Magnify(bool on) => Magnifying = on;

        public void ToggleNarrator()
        {
        }

        public bool Stick(bool on) => true;
    }
}
