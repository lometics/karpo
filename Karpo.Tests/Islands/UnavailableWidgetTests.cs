// A widget whose thing this PC hasn't got (docs\widgets.md §247): it draws its own layout with a
// stand-in reading — the brightness's slider at half, a full battery on the wall, a switch that is
// off — every pixel of it in the disabled ink (grey, no hue, no stronger than the disabled text),
// takes no press, wheel or click, and its tooltip says why. The same layout as where the thing is
// there, at the same reading, pixel box for pixel box.
//
// Pictures: unavailable-{brightness,battery,switch}-150-{dark,light}.png in KARPO_PNG_DIR, the
// widget available beside it greyed.
//
// Needs Direct2D, so the drawing tests run only with KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Folders;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Islands;

public sealed unsafe class UnavailableWidgetTests
{
    private const float Scale = 1.5f;
    private const nint Television = 0x30;
    private const nint Monitor = 0x10;

    /// <summary>The text's ink comes out as exactly the disabled text's, and the accent as a grey quieter than it, in both looks.</summary>
    [Fact]
    public void TheTextGoesToTheDisabledInkAndNothingKeepsAHue()
    {
        foreach (var dark in new[] { true, false })
        {
            var look = TestLooks.LookFor(dark);
            var ink = new IslandInk(look);
            var text = Unavailable.Greyed(look, ink.Text);
            var disabled = ink.Disabled;
            Assert.Equal(disabled.a, text.a, 3);
            Assert.Equal(disabled.r * disabled.a, text.r, 3);
            Assert.Equal(disabled.g * disabled.a, text.g, 3);
            Assert.Equal(disabled.b * disabled.a, text.b, 3);
            var accent = Unavailable.Greyed(look, ink.Accent);
            Assert.Equal(accent.r, accent.g, 3);
            Assert.Equal(accent.g, accent.b, 3);
            Assert.InRange(accent.a, 0.05f, disabled.a);
            // Nothing drawn, nothing greyed.
            Assert.Equal(0, Unavailable.Greyed(look, new DXGI_RGBA(0, 0, 0, 0)).a, 3);
        }
    }

    /// <summary>
    /// The brightness with no display to set: on the bar and as a line, the slider at half, greyed,
    /// the same pixel box as a display at half draws; no press, wheel or target anywhere; its tooltip why.
    /// </summary>
    [UiFact]
    public void TheBrightnessWithNothingToSetIsItsSliderGreyed()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                var look = TestLooks.LookFor(dark);
                using var none = Brightness(gfx, new SampleScreens().Add(Television, @"\\.\DISPLAY3", "TV", null), line: false);
                using var half = Brightness(gfx, new SampleScreens().Add(Monitor, @"\\.\DISPLAY1", "DELL", BrightnessIsland.UnavailableLevel), line: false);
                var bar = new IslandStyle(Scale, false, 48 * Scale, 24 * Scale, look) { Screen = Television };
                var along = BrightnessIsland.Plan(bar, bar.Thickness).Along;
                Assert.NotNull(none.UnavailableOn(Television));
                Assert.Null(half.UnavailableOn(Monitor));
                var greyed = Bar(none, bar, along);
                var shown = Bar(half, bar with { Screen = Monitor }, along);
                Alike(greyed, shown, dark, "the bar's brightness");
                Assert.False(((ISliderIsland)none).Wheel(120, bar));

                using var noneLine = Brightness(gfx, new SampleScreens().Add(Television, @"\\.\DISPLAY3", "TV", null), line: true);
                using var halfLine = Brightness(gfx, new SampleScreens().Add(Monitor, @"\\.\DISPLAY1", "DELL", BrightnessIsland.UnavailableLevel), line: true);
                var style = Line(look) with { Screen = Television };
                var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(Scale), PopupPaint.Round(40 * Scale));
                Alike(Card(noneLine, box, style), Card(halfLine, box, style with { Screen = Monitor }), dark, "the brightness's line");
                Untouchable(noneLine, box, style, Television);
            }
        });
    }

    /// <summary>
    /// The battery line on a PC with none: a full battery on the wall, greyed, as tall and laid out as
    /// one is; no target; its tooltip says there is no battery.
    /// </summary>
    [UiFact]
    public void TheBatteryLineWithNoBatteryIsAFullOneGreyed()
    {
        Offscreen.OnStaThread(gfx =>
        {
            BatteryIsland.Made = () => new SampleBattery { Present = false };
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    var style = Line(TestLooks.LookFor(dark));
                    using var none = BatteryLine(gfx, null);
                    using var full = BatteryLine(gfx, BatteryIsland.UnavailableCharge);
                    Assert.NotNull(none.UnavailableOn(0));
                    Assert.Null(full.UnavailableOn(0));
                    var width = FolderFlyout.ListWidth(Scale);
                    Assert.Equal(full.ListHeight(width, style), none.ListHeight(width, style));
                    foreach (var rows in new[] { 1f, 2f, 4f })
                    {
                        var box = PopupPaint.Rect(0, 0, width, PopupPaint.Round(40 * Scale * rows));
                        Alike(Card(none, box, style), Card(full, box, style), dark, $"the battery's line, {rows} rows");
                        Untouchable(none, box, style, 0);
                        Assert.Contains(S.WidgetBatteryNoBattery, none.TipOn(new Vector2(4, 4), box, style, WidgetSurface.List)!.Value.Text);
                    }
                }
            }
            finally
            {
                BatteryIsland.Made = null;
                BatteryIsland.Probe = SystemStatus.ReadBatteryNow;
            }
        });
    }

    /// <summary>
    /// A switch this PC hasn't got: still one cell on the bar (never taken off it) and a line with its
    /// switch, drawn off and greyed as one that is off is laid out; no target; its tooltip why.
    /// </summary>
    [UiFact]
    public void ASwitchThisPcHasNotGotIsDrawnOffGreyed()
    {
        Offscreen.OnStaThread(gfx =>
        {
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    var look = TestLooks.LookFor(dark);
                    using var none = Switch(gfx, available: false);
                    using var off = Switch(gfx, available: true);
                    Assert.Equal(1, none.CellCount);
                    Assert.NotNull(none.UnavailableOn(0));
                    Assert.Contains("isn't available on this PC", none.UnavailableOn(0));
                    var bar = new IslandStyle(Scale, false, 48 * Scale, 24 * Scale, look);
                    Alike(Bar(none, bar, bar.Thickness), Bar(off, bar, bar.Thickness), dark, "the switch on the bar");

                    var style = Line(look);
                    using var noneLine = Switch(gfx, available: false, line: true);
                    using var offLine = Switch(gfx, available: true, line: true);
                    foreach (var rows in new[] { 1f, 2f })
                    {
                        var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(Scale), PopupPaint.Round(40 * Scale * rows));
                        Assert.True(noneLine.DrawsSwitch, "the line draws its switch, off");
                        Alike(Card(noneLine, box, style), Card(offLine, box, style), dark, $"the switch's line, {rows} rows");
                        Untouchable(noneLine, box, style, 0);
                    }
                    Assert.Equal(default, ((IIsland)none).Click(0));
                }
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }

    /// <summary>
    /// The brightness, the battery and a switch, available and not, side by side on the bar and as
    /// lines, at 150 %, dark and light: unavailable-*-150-*.png in the pictures folder, for the eye.
    /// </summary>
    [UiFact]
    public void ThePictures()
    {
        Offscreen.OnStaThread(gfx =>
        {
            BatteryIsland.Made = () => new SampleBattery { Present = false };
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    var look = TestLooks.LookFor(dark);
                    var theme = FlyoutTheme.For(look);
                    var bar = new IslandStyle(Scale, false, 48 * Scale, 24 * Scale, look);
                    var lineWidth = FolderFlyout.ListWidth(Scale);
                    var row = PopupPaint.Round(40 * Scale);
                    var pad = PopupPaint.Round(12 * Scale);
                    var along = BrightnessIsland.Plan(bar, bar.Thickness).Along;
                    using var sunNone = Brightness(gfx, new SampleScreens().Add(Television, @"\\.\DISPLAY3", "TV", null), line: false);
                    using var sunHalf = Brightness(gfx, new SampleScreens().Add(Monitor, @"\\.\DISPLAY1", "DELL", 50), line: false);
                    using var lineNone = Brightness(gfx, new SampleScreens().Add(Television, @"\\.\DISPLAY3", "TV", null), line: true);
                    using var lineHalf = Brightness(gfx, new SampleScreens().Add(Monitor, @"\\.\DISPLAY1", "DELL", 50), line: true);
                    using var batteryNone = BatteryLine(gfx, null);
                    using var batteryFull = BatteryLine(gfx, BatteryIsland.UnavailableCharge);
                    using var switchNone = Switch(gfx, available: false);
                    using var switchOff = Switch(gfx, available: true);
                    using var switchLineNone = Switch(gfx, available: false, line: true);
                    using var switchLineOff = Switch(gfx, available: true, line: true);
                    var style = Line(look);
                    var batteryHeight = batteryFull.ListHeight(lineWidth, style);

                    // The widget available on the left, and not on the right.
                    void Sheet(string name, float height, Action<IntPtr, bool, float> draw)
                    {
                        var width = pad + lineWidth + pad + lineWidth + pad;
                        var total = pad + height + pad;
                        Offscreen.Bitmap(width, total, dc =>
                        {
                            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, total), 0, PopupPaint.Ink(theme.Base));
                            draw((IntPtr)dc, false, pad);
                            draw((IntPtr)dc, true, pad + lineWidth + pad);
                        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"unavailable-{name}-150-{(dark ? "dark" : "light")}.png")));
                    }

                    void Panel(ID2D1DeviceContext* dc, float x, float y, float height) =>
                        PopupPaint.Fill(dc, PopupPaint.Rect(x, y, lineWidth, height), PopupPaint.Round(8 * Scale), PopupPaint.Ink(theme.CardBackground));

                    Sheet("brightness", bar.Thickness + pad + row + pad + (row * 3), (handle, greyed, x) =>
                    {
                        var dc = (ID2D1DeviceContext*)handle;
                        var y = pad;
                        Panel(dc, x, y, bar.Thickness);
                        var cell = greyed ? bar with { Screen = Television } : bar with { Screen = Monitor };
                        Offscreen.Shifted(dc, x, y, () => (greyed ? sunNone : sunHalf).PaintBar(dc, 0, cell, along, bar.Thickness));
                        y += bar.Thickness + pad;
                        foreach (var rows in new[] { 1, 3 })
                        {
                            Panel(dc, x, y, row * rows);
                            var lineStyle = style with { Screen = greyed ? Television : Monitor };
                            Offscreen.Shifted(dc, x, y, () => (greyed ? lineNone : lineHalf).PaintOn(dc, PopupPaint.Rect(0, 0, lineWidth, row * rows), lineStyle, WidgetSurface.List));
                            y += (row * rows) + pad;
                        }
                    });
                    Sheet("battery", batteryHeight + pad + (row * 1) + pad + (row * 4), (handle, greyed, x) =>
                    {
                        var dc = (ID2D1DeviceContext*)handle;
                        var y = pad;
                        foreach (var height in new[] { batteryHeight, row, row * 4 })
                        {
                            Panel(dc, x, y, height);
                            Offscreen.Shifted(dc, x, y, () => (greyed ? batteryNone : batteryFull).PaintOn(dc, PopupPaint.Rect(0, 0, lineWidth, height), style, WidgetSurface.List));
                            y += height + pad;
                        }
                    });
                    Sheet("switch", bar.Thickness + pad + row + pad + (row * 2), (handle, greyed, x) =>
                    {
                        var dc = (ID2D1DeviceContext*)handle;
                        var y = pad;
                        Panel(dc, x, y, bar.Thickness);
                        Offscreen.Shifted(dc, x, y, () => (greyed ? switchNone : switchOff).PaintBar(dc, 0, bar, bar.Thickness * 3, bar.Thickness));
                        y += bar.Thickness + pad;
                        foreach (var rows in new[] { 1, 2 })
                        {
                            Panel(dc, x, y, row * rows);
                            Offscreen.Shifted(dc, x, y, () => (greyed ? switchLineNone : switchLineOff).PaintOn(dc, PopupPaint.Rect(0, 0, lineWidth, row * rows), style, WidgetSurface.List));
                            y += (row * rows) + pad;
                        }
                    });
                }
            }
            finally
            {
                BatteryIsland.Made = null;
                BatteryIsland.Probe = SystemStatus.ReadBatteryNow;
                QuickSwitches.Source = null;
            }
        });
    }

    // --- Helpers ---------------------------------------------------------------------------------

    private static TileStyle Line(Look look) => FolderFlyout.StyleFor(Scale, look, FlyoutTheme.For(look));

    private static BrightnessIsland Brightness(Gfx gfx, IScreenMachine machine, bool line)
    {
        Karpo.Dock.QuickSettings.Services.Brightness.Stand = machine;
        try
        {
            if (line)
            {
                var cell = ListKind.Brightness.New();
                cell.Id = "brightness";
                return Assert.IsType<BrightnessIsland>(Karpo.Dock.Islands.CreateLine("layout:default/folder/brightness", cell, ListKind.Brightness, gfx, new Config()));
            }
            var settings = BarKind.Brightness.New();
            settings.Id = "brightness";
            return Assert.IsType<BrightnessIsland>(Karpo.Dock.Islands.Create(Config.IslandKey(null, settings.Id), settings, gfx, new Config()));
        }
        finally
        {
            Karpo.Dock.QuickSettings.Services.Brightness.Stand = null;
        }
    }

    /// <summary>The battery's line on a PC whose charge reads <paramref name="charge"/> (null: no battery).</summary>
    private static BatteryIsland BatteryLine(Gfx gfx, SystemStatus.Battery? charge)
    {
        BatteryIsland.Probe = () => charge;
        var cell = ListKind.Battery.New();
        cell.Id = "battery";
        return Assert.IsType<BatteryIsland>(Karpo.Dock.Islands.CreateLine("layout:default/folder/battery", cell, ListKind.Battery, gfx, new Config()));
    }

    /// <summary>Night light on the bar, or as a folder's line, on a PC that has it (off) or not.</summary>
    private static SwitchIsland Switch(Gfx gfx, bool available, bool line = false)
    {
        QuickSwitches.Source = which => new SampleSwitch(which.ToString(), on: false, available: available, note: "Why not.", switches: true);
        if (line)
        {
            var cell = ListKind.NightLight.New();
            cell.Id = available ? "on" : "off";
            return Assert.IsType<SwitchIsland>(Karpo.Dock.Islands.CreateLine($"layout:default/folder/{cell.Id}", cell, ListKind.NightLight, gfx, new Config()));
        }
        var settings = BarKind.NightLight.New();
        settings.Id = available ? "on" : "off";
        return Assert.IsType<SwitchIsland>(Karpo.Dock.Islands.Create(Config.IslandKey(null, settings.Id), settings, gfx, new Config()));
    }

    /// <summary>The bar's drawing of <paramref name="island"/> in a cell <paramref name="along"/> long: its pixels.</summary>
    private static Pixels Bar(CellIsland island, IslandStyle style, float along) =>
        Draw(along, style.Thickness, dc => island.PaintBar(dc, 0, style, along, style.Thickness));

    /// <summary>A folder's drawing of <paramref name="island"/> in <paramref name="box"/>: its pixels.</summary>
    private static Pixels Card(CellIsland island, D2D_RECT_F box, TileStyle style) =>
        Draw(box.right, box.bottom, dc => island.PaintOn(dc, box, style, WidgetSurface.List));

    /// <summary>
    /// <paramref name="greyed"/> drew the layout <paramref name="shown"/> did — the same box of ink —
    /// and every pixel of it grey, no stronger than the disabled ink; <paramref name="shown"/> keeps a hue.
    /// </summary>
    private static void Alike(Pixels greyed, Pixels shown, bool dark, string what)
    {
        var (left, top, right, bottom) = greyed.Ink(4);
        var (sl, st, sr, sb) = shown.Ink(4);
        Assert.True(right > left, $"{what}: nothing drawn");
        Assert.True(MathF.Abs(left - sl) <= 2 && MathF.Abs(right - sr) <= 2 && MathF.Abs(top - st) <= 2 && MathF.Abs(bottom - sb) <= 2,
            $"{what}: greyed ink {left},{top}-{right},{bottom}, shown {sl},{st}-{sr},{sb}");
        var most = (int)MathF.Ceiling(0.41f * 255) + 2;
        foreach (var (b, g, r, a) in greyed.All())
        {
            Assert.True(a <= most, $"{what}: a pixel of alpha {a} is stronger than the disabled ink");
            Assert.True(Math.Abs(r - g) <= 3 && Math.Abs(g - b) <= 3, $"{what}: a pixel keeps a hue ({r},{g},{b},{a})");
            if (!dark)
            {
                Assert.True(r <= 3, $"{what}: a light look's greyed pixel is not dark ({r},{g},{b},{a})");
            }
        }
        Assert.Contains(shown.All(), pixel => Math.Abs(pixel.R - pixel.B) > 20 || pixel.A > most);
    }

    /// <summary>Nothing on <paramref name="island"/> in <paramref name="box"/> takes a press or the wheel, or is any target but the whole; its tooltip is why.</summary>
    private static void Untouchable(CellIsland island, D2D_RECT_F box, TileStyle style, nint screen)
    {
        for (var x = box.left + 1; x < box.right; x += 6)
        {
            for (var y = box.top + 1; y < box.bottom; y += 6)
            {
                var at = new Vector2(x, y);
                Assert.False(island.PressOn(at, box, style, WidgetSurface.List), $"a press at {x},{y} was taken");
                Assert.False(island.WheelOn(at, box, style, WidgetSurface.List, 1), $"the wheel at {x},{y} was taken");
                Assert.Equal(0, island.TargetOn(at, box, style, WidgetSurface.List));
            }
        }
        var tip = island.TipOn(new Vector2(box.left + 2, box.top + 2), box, style, WidgetSurface.List);
        Assert.Equal(island.UnavailableOn(screen), tip?.Text);
        Assert.Equal(box, tip!.Value.Part);
    }

    private delegate void Painter(ID2D1DeviceContext* dc);

    private static Pixels Draw(float width, float height, Painter paint)
    {
        Pixels pixels = default;
        Offscreen.Bitmap(width, height, dc => paint(dc), bitmap => pixels = Pixels.Of(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height)));
        return pixels;
    }

    /// <summary>A drawing's premultiplied BGRA pixels.</summary>
    private readonly record struct Pixels(byte[] Data, int Width, int Height)
    {
        public static Pixels Of(IWICBitmap* bitmap, uint width, uint height)
        {
            var data = new byte[width * height * 4];
            fixed (byte* into = data)
            {
                Karpo.Dock.Native.ComUtil.Check(bitmap->CopyPixels(null, width * 4, (uint)data.Length, into));
            }
            return new Pixels(data, (int)width, (int)height);
        }

        public IEnumerable<(byte B, byte G, byte R, byte A)> All()
        {
            for (var at = 0; at < Data.Length; at += 4)
            {
                if (Data[at + 3] > 4)
                {
                    yield return (Data[at], Data[at + 1], Data[at + 2], Data[at + 3]);
                }
            }
        }

        public (float Left, float Top, float Right, float Bottom) Ink(byte least)
        {
            float left = Width, top = Height, right = 0, bottom = 0;
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (Data[(((y * Width) + x) * 4) + 3] > least)
                    {
                        left = MathF.Min(left, x);
                        top = MathF.Min(top, y);
                        right = MathF.Max(right, x + 1);
                        bottom = MathF.Max(bottom, y + 1);
                    }
                }
            }
            return right > left ? (left, top, right, bottom) : (0, 0, 0, 0);
        }
    }
}
