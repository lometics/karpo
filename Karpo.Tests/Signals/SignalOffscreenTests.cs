// App buttons with their signals, drawn offscreen: each progress state, one with nothing done
// (Spotify's pin, which keeps its running indicator), an indeterminate run, an
// overlay, and badges — counts of one and two figures, "99+" and status glyphs — on a dark and a light bar at 100 % and
// 150 %, with the pictures in KARPO_PNG_DIR (app-signals-*.png).
//
// Like the bar's other sheets (BarCardsOffscreenTests), every rectangle comes from the real code:
// the buttons from a real Layout, the signals' places from SignalShape and their colours from
// SignalInk, which is what Scene asks; the badge is painted by SignalPaint itself, the painter the
// bar's badge surfaces are drawn with. The pixels are then read back where each thing should be.
//
// Needs Direct2D, so it runs only with KARPO_UI_TESTS=1. Nothing is shown on the person's desktop.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.S;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Signals;

public sealed unsafe class SignalOffscreenTests
{
    /// <summary>What each button of the sheet shows, left to right.</summary>
    private static readonly (string Label, TileSignal Signal)[] Buttons =
    [
        ("open", default),
        // Spotify's pin: its window at TBPF_NORMAL with nothing done, as the band hears it.
        ("nothing done", NothingDone()),
        ("45 %", new TileSignal(ProgressKind.Normal, 0.45f)),
        ("paused", new TileSignal(ProgressKind.Paused, 0.7f)),
        ("error", new TileSignal(ProgressKind.Error, 0.3f)),
        ("busy", new TileSignal(ProgressKind.Indeterminate)),
        ("overlay", new TileSignal(Overlay: Dot(), OverlayText: "Do not disturb")),
        ("1", new TileSignal(Badge: Badge.Of("1"))),
        ("9", new TileSignal(Badge: Badge.Of("9"))),
        ("42", new TileSignal(Badge: Badge.Of("42"))),
        ("99+", new TileSignal(Badge: Badge.Of("250"))),
        ("busy glyph", new TileSignal(Badge: Badge.Of("busy"))),
        ("alert glyph", new TileSignal(Badge: Badge.Of("alert"))),
        ("attention glyph", new TileSignal(Badge: Badge.Of("attention"))),
        ("both", new TileSignal(ProgressKind.Normal, 0.8f, Dot(), "Recording")),
    ];

    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    /// <summary>
    /// What Spotify's button carries: its window set TBPF_NORMAL and a value of 0 of 100, through
    /// the same AppSignals the band writes. Nothing done is nothing to draw: the running indicator.
    /// </summary>
    private static TileSignal NothingDone()
    {
        const nint window = 0x5F0A1E;
        var signals = new AppSignals();
        signals.ProgressState(window, 0x2);
        signals.ProgressValue(window, 0);
        return signals.For([window]);
    }

    /// <summary>A 32-px red disc, an overlay as a chat app would set one: premultiplied, with a soft edge.</summary>
    private static Pixels Dot()
    {
        const int side = 32;
        var data = new byte[side * side * 4];
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(side / 2f, side / 2f));
                var alpha = Math.Clamp(side / 2f - d, 0f, 1f);
                var at = (y * side + x) * 4;
                data[at] = (byte)(0x1C * alpha);
                data[at + 1] = (byte)(0x2B * alpha);
                data[at + 2] = (byte)(0xC4 * alpha);
                data[at + 3] = (byte)(255 * alpha);
            }
        }
        return new Pixels { Data = data, Width = side, Height = side };
    }

    private static Layout Measured(uint dpi)
    {
        var config = new Config { Height = 48 };
        return Layout.ForTests(
            dpi,
            new RECT { right = 3000, bottom = 1200 },
            Edge.Bottom,
            config,
            [new PartSpec(Config.IslandKey(null, "apps"), BarKind.Apps)],
            Layout.CellPx(dpi, config),
            tiles: Buttons.Length);
    }

    [UiFact]
    public void SignalPictures()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var look = LookFor(dark);
                    var layout = Measured(dpi);
                    // The bar's panel is the picture: the window around it has room above for what pops up.
                    var panel = layout.PanelRect;
                    var right = layout.ItemRect(Hit.Tile(Buttons.Length - 1)).Right + 16 - panel.X;
                    Offscreen.Bitmap(right, panel.Height, dc => Offscreen.Shifted(dc, -panel.X, -panel.Y, () => Bar(dc, gfx, layout, look)), bitmap =>
                    {
                        Check(bitmap, gfx, layout, look);
                        Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"app-signals-{(dark ? "dark" : "light")}-{dpi * 100 / 96}.png"));
                    });
                }
            }
        });
    }

    /// <summary>The bar and its buttons, each with its signal where the bar puts it.</summary>
    private static void Bar(ID2D1DeviceContext* dc, Gfx gfx, Layout layout, Look look)
    {
        var background = look.Dark ? Color.FromArgb(255, 0x1C, 0x1C, 0x1C) : Color.FromArgb(255, 0xEE, 0xEE, 0xEE);
        var panel = layout.PanelRect;
        PopupPaint.Fill(dc, PopupPaint.Rect(panel.X, panel.Y, panel.Width, panel.Height), 0, PopupPaint.Ink(background));
        var running = look.Dark ? Color.FromArgb(139, 255, 255, 255) : Color.FromArgb(114, 0, 0, 0);
        var radius = layout.UnitPx(4);
        for (var index = 0; index < Buttons.Length; index++)
        {
            var signal = Buttons[index].Signal;
            var highlight = layout.ItemRect(Hit.Tile(index));
            var iconBox = layout.IconBox(Hit.Tile(index), layout.Icon);
            if (Icon(index, (int)layout.Icon) is { } icon)
            {
                PopupPaint.Picture(dc, icon, Rect(iconBox), 0);
            }
            var run = SignalShape.Run(highlight, layout.Edge, layout.Pill, layout.Stroke, radius);
            if (!SignalShape.Shows(signal, run, false))
            {
                // The running indicator, as Scene sets it for one window: 6 DIPs, where the run is.
                var length = layout.UnitPx(6);
                var pill = new RectF(MathF.Round(highlight.Center.X - length / 2f, MidpointRounding.AwayFromZero), run.Y, length, run.Height);
                PopupPaint.Fill(dc, Rect(pill), layout.Pill / 2f, PopupPaint.Ink(running));
            }
            else
            {
                PopupPaint.Fill(dc, Rect(SignalShape.Track(run, false, layout.Stroke)), 0, PopupPaint.Ink(SignalInk.Track(look)));
                var ink = PopupPaint.Ink(SignalInk.Progress(signal.Progress, look));
                if (signal.Progress == ProgressKind.Indeterminate)
                {
                    // The runner as it passes the middle of its lap.
                    var runner = SignalShape.RunnerLength(run, false);
                    PopupPaint.Fill(dc, Rect(new RectF(run.X + MathF.Round((run.Width - runner) / 2f), run.Y, runner, run.Height)), layout.Pill / 2f, ink);
                }
                else
                {
                    PopupPaint.Fill(dc, Rect(SignalShape.Done(run, false, signal.Value)), layout.Pill / 2f, ink);
                }
            }
            var side = SignalShape.CornerSide(layout.Icon);
            if (signal.Badge is { } badge)
            {
                var plan = SignalPaint.Plan(gfx, badge, side);
                var corner = SignalShape.Corner(iconBox, plan.Size);
                Offscreen.Shifted(dc, corner.X, corner.Y, () => SignalPaint.Badge(dc, gfx, badge, plan, look));
            }
            else if (signal.Overlay is { } overlay)
            {
                PopupPaint.Picture(dc, overlay, Rect(SignalShape.Corner(iconBox, new Vector2(side, side))), 0);
            }
        }
    }

    /// <summary>Each signal is where it should be, in its colour.</summary>
    private static void Check(IWICBitmap* bitmap, Gfx gfx, Layout layout, Look look)
    {
        origin = new Vector2(layout.PanelRect.X, layout.PanelRect.Y);
        var radius = layout.UnitPx(4);
        for (var index = 0; index < Buttons.Length; index++)
        {
            var (label, signal) = Buttons[index];
            var highlight = layout.ItemRect(Hit.Tile(index));
            var run = SignalShape.Run(highlight, layout.Edge, layout.Pill, layout.Stroke, radius);
            if (!SignalShape.Shows(signal, run, false))
            {
                // The running indicator in the middle, and no hairline track out at the run's ends:
                // the bar's own background there, as under any running app's button.
                var background = look.Dark ? Color.FromArgb(255, 0x1C, 0x1C, 0x1C) : Color.FromArgb(255, 0xEE, 0xEE, 0xEE);
                var running = look.Dark ? Color.FromArgb(139, 255, 255, 255) : Color.FromArgb(114, 0, 0, 0);
                AssertNear(Over(running, background), Pixel(bitmap, highlight.Center.X, run.Y + run.Height / 2f), $"{label}: the running indicator");
                var track = SignalShape.Track(run, false, layout.Stroke);
                AssertNear(background, Pixel(bitmap, track.X + 1f, track.Y), $"{label}: a track at the run's start");
                AssertNear(background, Pixel(bitmap, track.Right - 2f, track.Y), $"{label}: a track at the run's end");
            }
            if (signal.Progress is ProgressKind.Normal or ProgressKind.Paused or ProgressKind.Error && SignalShape.Shows(signal, run, false))
            {
                var done = SignalShape.Done(run, false, signal.Value);
                var expected = SignalInk.Progress(signal.Progress, look);
                AssertNear(expected, Pixel(bitmap, done.X + done.Width / 2f, done.Y + done.Height / 2f), $"{label}: the part done");
                // Past the part done, only the hairline track: nothing of the progress's colour.
                var after = Pixel(bitmap, done.Right + (run.Right - done.Right) / 2f, run.Y);
                Assert.False(Near(expected, after), $"{label}: the colour ran past the part done");
            }
            var iconBox = layout.IconBox(Hit.Tile(index), layout.Icon);
            var side = SignalShape.CornerSide(layout.Icon);
            if (signal.Badge is { } badge)
            {
                var plan = SignalPaint.Plan(gfx, badge, side);
                var corner = SignalShape.Corner(iconBox, plan.Size);
                // The pill's own colour at its left end, clear of the figures.
                AssertNear(SignalInk.BadgeFill(look), Pixel(bitmap, corner.X + plan.Size.Y * 0.15f, corner.Y + plan.Size.Y / 2f), $"{label}: the badge's pill");
                Assert.True(Inked(bitmap, corner, SignalInk.BadgeText(look)), $"{label}: the badge's text");
            }
            else if (signal.Overlay is not null)
            {
                var corner = SignalShape.Corner(iconBox, new Vector2(side, side));
                AssertNear(Color.FromArgb(255, 0xC4, 0x2B, 0x1C), Pixel(bitmap, corner.X + side / 2f, corner.Y + side / 2f), $"{label}: the overlay");
            }
        }
    }

    // --- Pixels --------------------------------------------------------------------------------------

    /// <summary>The panel's origin in the window, which the picture starts at (set per sheet).</summary>
    [ThreadStatic]
    private static Vector2 origin;

    private static Color Pixel(IWICBitmap* bitmap, float x, float y)
    {
        x -= origin.X;
        y -= origin.Y;
        IWICBitmapLock* locked = null;
        uint width;
        uint height;
        ComUtil.Check(bitmap->GetSize(&width, &height));
        Assert.True(x >= 0 && y >= 0 && x < width && y < height, $"({x}, {y}) is off the {width} x {height} picture");
        try
        {
            var one = new WICRect { X = (int)x, Y = (int)y, Width = 1, Height = 1 };
            ComUtil.Check(bitmap->Lock(&one, (uint)WICBitmapLockFlags.WICBitmapLockRead, &locked));
            uint size = 0;
            byte* data = null;
            ComUtil.Check(locked->GetDataPointer(&size, &data));
            // Premultiplied BGRA; everything checked here is drawn opaque.
            return Color.FromArgb(data[3], data[2], data[1], data[0]);
        }
        finally
        {
            ComUtil.Release(ref locked);
        }
    }

    /// <summary>Whether any pixel in <paramref name="box"/> is near <paramref name="ink"/>.</summary>
    private static bool Inked(IWICBitmap* bitmap, RectF box, Color ink)
    {
        for (var y = box.Y; y < box.Bottom; y++)
        {
            for (var x = box.X; x < box.Right; x++)
            {
                if (Near(ink, Pixel(bitmap, x, y), 40))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool Near(Color expected, Color actual, int within = 6) =>
        Math.Abs(expected.R - actual.R) <= within && Math.Abs(expected.G - actual.G) <= within && Math.Abs(expected.B - actual.B) <= within;

    private static void AssertNear(Color expected, Color actual, string what) =>
        Assert.True(Near(expected, actual), $"{what}: expected #{expected.R:X2}{expected.G:X2}{expected.B:X2}, found #{actual.R:X2}{actual.G:X2}{actual.B:X2}");

    /// <summary><paramref name="top"/> drawn over the opaque <paramref name="under"/>.</summary>
    private static Color Over(Color top, Color under)
    {
        var a = top.A / 255f;
        byte Mix(byte over, byte back) => (byte)MathF.Round(over * a + back * (1f - a));
        return Color.FromArgb(255, Mix(top.R, under.R), Mix(top.G, under.G), Mix(top.B, under.B));
    }

    private static D2D_RECT_F Rect(RectF box) => PopupPaint.Rect(box.X, box.Y, box.Width, box.Height);

    /// <summary>Icons in shell32.dll that look like an app's.</summary>
    private static Pixels? Icon(int which, int size)
    {
        int[] icons = [13, 15, 18, 20, 23, 41, 47, 77, 78, 106];
        var icon = HICON.NULL;
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");
        fixed (char* name = path)
        {
            if (SHDefExtractIconW(name, icons[which % icons.Length], 0, &icon, null, (uint)size).Value != S_OK || icon == HICON.NULL)
            {
                return null;
            }
        }
        try
        {
            return Tray.Render(icon, size);
        }
        finally
        {
            _ = DestroyIcon(icon);
        }
    }
}
