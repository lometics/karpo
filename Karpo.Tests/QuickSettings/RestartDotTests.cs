// Windows' red dot on the status icon for a restart waiting (Islands\RestartDot.cs, IndicatorsIsland):
// the person, 2026-10-04 — draw it as Windows' tray icon for it (WindowsUpdateImportant.ico) is
// drawn: the arrows in the primary text colour, and Windows' red dot in Windows' critical red
// (SystemFillColorCriticalBrush), in the same place and size as Windows' icon; in high contrast, the
// system's colours.
//
// The icon file, read at each of its sizes, has the dot half the icon across with its middle three
// quarters of the way across and down, and the arrows cut away a sixteenth of the icon clear of it.
// Held here as the numbers, as the colours, and as the pixels: the dot where Windows' is and in its
// red, the gap around it empty, everything else the glyph's colour.
//
// KARPO_PNG_DIR\restart-dot-{scale}-{theme}.png: the status icon for a restart waiting on the bar's
// colour, at the bar's size and four times over, at 100 and 150 %, in dark, light and high contrast.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.QuickSettings;

public sealed unsafe class RestartDotTests
{
    private static readonly Look Dark = Karpo.Tests.Folders.TestLooks.LookFor(true);

    private static readonly Look Light = Karpo.Tests.Folders.TestLooks.LookFor(false);

    /// <summary>High contrast whose highlight and text are neither red nor white nor black, so the system's colours show.</summary>
    private static readonly Look Contrast = Dark with
    {
        HighContrast = true,
        HcWindow = Color.FromArgb(255, 0, 0, 0),
        HcText = Color.FromArgb(255, 0xFF, 0xFF, 0x00),
        HcHighlight = Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
        HcHighlightText = Color.FromArgb(255, 0, 0, 0),
    };

    private static readonly (string Name, Look Look)[] Looks = [("dark", Dark), ("light", Light), ("hc", Contrast)];

    private static (float R, float G, float B, float A) Rgba(DXGI_RGBA color) => (color.r, color.g, color.b, color.a);

    /// <summary>
    /// Where and how big, as the icon file has it: in the tray's 16 px square, an 8 px dot whose middle
    /// is at (12, 12), the arrows cut 1 px clear of it; at 24 px (150 %) the same, half again.
    /// </summary>
    [Fact]
    public void TheDotIsWhereWindowsIconHasIt()
    {
        var (centre, radius, clear) = RestartDot.In(PopupPaint.Rect(0, 0, 16, 16));
        Assert.Equal(new Vector2(12, 12), centre);
        Assert.Equal(4f, radius);
        Assert.Equal(5f, clear);

        (centre, radius, clear) = RestartDot.In(PopupPaint.Rect(10, 20, 24, 24));
        Assert.Equal(new Vector2(28, 38), centre);
        Assert.Equal(6f, radius);
        Assert.Equal(7.5f, clear);

        // In a cell, the icon's square is the tray's, in the middle of it.
        var square = RestartDot.Centred(40, 48, 16);
        Assert.Equal((12f, 16f, 28f, 32f), (square.left, square.top, square.right, square.bottom));
        // A box that is not square holds the largest square in its middle.
        (centre, radius, _) = RestartDot.In(PopupPaint.Rect(0, 0, 32, 16));
        Assert.Equal((new Vector2(20, 12), 4f), (centre, radius));
    }

    /// <summary>
    /// Its colour is Windows' critical red: #C42B1C on light and #FF99A4 on dark
    /// (SystemFillColorCritical), not the icon file's pure red and never the glyph's colour; in high
    /// contrast the system's highlight. The same on the bar and in a folder.
    /// </summary>
    [Fact]
    public void TheDotIsWindowsCriticalRed()
    {
        static (float, float, float, float) Hex(uint rgb) => (((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        Assert.Equal(Hex(0xC42B1C), Rgba(RestartDot.Ink(new IslandInk(Light))));
        Assert.Equal(Hex(0xFF99A4), Rgba(RestartDot.Ink(new IslandInk(Dark))));
        Assert.Equal(Hex(0x1AEBFF), Rgba(RestartDot.Ink(new IslandInk(Contrast))));
        foreach (var (name, look) in Looks)
        {
            var ink = new IslandInk(look);
            Assert.Equal(Rgba(ink.Critical), Rgba(RestartDot.Ink(ink)));
            Assert.NotEqual(Rgba(ink.SystemIcon), Rgba(RestartDot.Ink(ink)));
            Assert.NotEqual((1f, 0f, 0f, 1f), Rgba(RestartDot.Ink(ink)));
            // A folder's theme for the same look gives the same red.
            var theme = FlyoutTheme.For(look);
            var folder = RestartDot.Ink(theme);
            Assert.True(MathF.Abs(folder.r - ink.Critical.r) < 0.01f && MathF.Abs(folder.g - ink.Critical.g) < 0.01f && MathF.Abs(folder.b - ink.Critical.b) < 0.01f, name);
        }
    }

    /// <summary>
    /// Only the update icon wears the dot; its glyph stays the status icons' one colour. A restart for
    /// Windows wears the critical red, one for drivers alone Windows' orange (#FC9A03, its
    /// WindowsUpdateWarning.ico), updates waiting or under way its blue (#0078D7, WindowsUpdateSeeker.ico);
    /// in high contrast every one is the highlight. The same in a folder.
    /// </summary>
    [Fact]
    public void OnlyTheUpdateIconWearsTheDot()
    {
        static (float, float, float, float) Hex(uint rgb) => (((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        var everything = new IndicatorState
        {
            Microphone = ["Teams"], Camera = ["Teams"], Presence = ["Windows Hello"], AirplaneMode = true, Hotspot = true,
            Metered = true, EnergySaver = true, DoNotDisturb = true, Update = UpdateState.Due,
        };
        foreach (var (_, look) in Looks)
        {
            var ink = new IslandInk(look);
            foreach (var indicator in Indicators.Of(everything))
            {
                var dot = IndicatorsIsland.DotOf(indicator, ink);
                Assert.Equal(indicator.Kind == IndicatorKind.WindowsUpdate, dot is not null);
                Assert.Equal(Rgba(ink.SystemIcon), Rgba(IndicatorsIsland.InkOf(indicator.Kind, ink)));
            }
            DXGI_RGBA? DotFor(UpdateState state) => IndicatorsIsland.DotOf(Assert.Single(Indicators.Of(new IndicatorState { Update = state })), ink);
            Assert.Equal(Rgba(ink.Critical), Rgba(DotFor(UpdateState.Due)!.Value));
            var drivers = DotFor(UpdateState.Due with { DriversOnly = true })!.Value;
            var waiting = DotFor(UpdateState.Nothing with { Stage = UpdateStage.Downloading })!.Value;
            if (look.HighContrast)
            {
                Assert.Equal(Rgba(ink.Critical), Rgba(drivers));
                Assert.Equal(Rgba(ink.Critical), Rgba(waiting));
            }
            else
            {
                Assert.Equal(Hex(0xFC9A03), Rgba(drivers));
                Assert.Equal(Hex(0x0078D7), Rgba(waiting));
            }
            // A folder's theme: the same three.
            var theme = FlyoutTheme.For(look);
            Assert.Null(RestartDot.Ink(UpdateBadge.None, theme));
            Assert.Equal(Rgba(RestartDot.Ink(theme)), Rgba(RestartDot.Ink(UpdateBadge.Important, theme)!.Value));
            Assert.Equal(look.HighContrast ? Rgba(RestartDot.Ink(theme)) : Hex(0xFC9A03), Rgba(RestartDot.Ink(UpdateBadge.Warning, theme)!.Value));
            Assert.Equal(look.HighContrast ? Rgba(RestartDot.Ink(theme)) : Hex(0x0078D7), Rgba(RestartDot.Ink(UpdateBadge.Seeker, theme)!.Value));
            Assert.Null(RestartDot.Ink(UpdateBadge.None, ink));
        }
    }

    private static IslandStyle Style(float scale, Look look) =>
        new(scale, false, Layout.CellPx(96, new Config { Height = 48 }) * scale, Layout.IconPx(96, new Config { Height = 48 }) * scale, look);

    /// <summary>The Status icons entry showing a restart waiting alone, on a source of the test's own.</summary>
    private static IndicatorsIsland Restart(Gfx gfx)
    {
        IndicatorSources.Made = () => new SampleIndicators(new IndicatorState { Update = UpdateState.Due });
        try
        {
            var entry = BarKind.Indicators.New();
            entry.Id = "indicators";
            var island = new IndicatorsIsland(Config.IslandKey(null, entry.Id), entry, gfx, new Config()) { Opened = _ => { } };
            IIsland measured = island;
            Assert.Equal(1, measured.Measure(Style(1f, Dark)).Count);
            return island;
        }
        finally
        {
            IndicatorSources.Made = null;
        }
    }

    /// <summary>
    /// The pixels: drawn alone on a clear surface, as the bar draws a cell, the dot is Windows' red
    /// where Windows' icon has it, its middle within a pixel of the tray square's (12, 12) and as wide
    /// as half the square; the gap around it is empty, the arrows cut clear; and every other pixel is
    /// the glyph's colour — at 100 and 150 %, in dark, light and high contrast.
    /// </summary>
    [UiFact]
    public void TheDotIsDrawnInWindowsPlaceAndRed()
    {
        var wrong = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (name, look) in Looks)
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var style = Style(scale, look);
                    var ink = new IslandInk(look);
                    using var island = Restart(gfx);
                    var (width, height) = island.CellSize(0, style);
                    var (w, h) = ((uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height));
                    var square = RestartDot.Centred(width, height, new Fit(style).SystemIcon);
                    var (centre, radius, clear) = RestartDot.In(square);
                    var where = $"{scale * 100:0} % {name}";
                    Offscreen.Bitmap(width, height, dc => island.PaintCell(dc, 0, style, width, height), bitmap =>
                    {
                        var pixels = Read(bitmap, w, h);
                        var red = ink.Critical;
                        var glyph = ink.SystemIcon;
                        float sumX = 0, sumY = 0, count = 0, left = w, right = 0;
                        for (var y = 0; y < h; y++)
                        {
                            for (var x = 0; x < w; x++)
                            {
                                var pixel = pixels[(y * w) + x];
                                var a = (int)(pixel >> 24);
                                var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre);
                                if (distance <= radius - 1f)
                                {
                                    // Inside the dot: its red, whole.
                                    if (a < 250 || !Is(pixel, red))
                                    {
                                        wrong.Add($"{where}: ({x}, {y}) inside the dot is not its red");
                                    }
                                }
                                else if (distance > clear + 0.5f && a > 0 && !Is(pixel, glyph))
                                {
                                    wrong.Add($"{where}: ({x}, {y}) of the arrows is not the glyph's colour");
                                }
                                if (a > 128 && Is(pixel, red) && distance <= radius + 1f)
                                {
                                    sumX += x + 0.5f;
                                    sumY += y + 0.5f;
                                    count++;
                                    left = MathF.Min(left, x);
                                    right = MathF.Max(right, x + 1);
                                }
                            }
                        }
                        if (count == 0)
                        {
                            wrong.Add($"{where}: no dot");
                            return;
                        }
                        var middle = new Vector2(sumX / count, sumY / count);
                        var side = square.right - square.left;
                        var windows = new Vector2(square.left + (RestartDot.Middle * side), square.top + (RestartDot.Middle * side));
                        if (Vector2.Distance(middle, windows) > 1f)
                        {
                            wrong.Add($"{where}: the dot's middle is at {middle}, Windows' at {windows}");
                        }
                        if (MathF.Abs(right - left - (RestartDot.Across * side)) > 1.5f)
                        {
                            wrong.Add($"{where}: the dot is {right - left} px across, Windows' {RestartDot.Across * side}");
                        }
                    });
                }
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    /// <summary>
    /// The arrows are cut clear of the dot, as Windows' icon's are: drawn with a dot of no colour at
    /// all, nothing is left within the gap's reach of the dot's middle, where the arrows drawn without
    /// the dot do reach — at the tray's 16 px and at 24 and 64.
    /// </summary>
    [UiFact]
    public void TheArrowsAreCutClearOfTheDot()
    {
        var wrong = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var size in new[] { 16f, 24f, 64f })
            {
                var square = PopupPaint.Rect(4, 4, size, size);
                var (centre, _, clear) = RestartDot.In(square);
                var ink = new DXGI_RGBA(1, 1, 1, 1);
                int Within(DXGI_RGBA? dot)
                {
                    var inked = 0;
                    Offscreen.Bitmap(size + 8, size + 8, dc => RestartDot.Paint(dc, gfx, square, WindowsUpdate.Glyph, ink, dot), bitmap =>
                    {
                        var side = (uint)MathF.Ceiling(size + 8);
                        var pixels = Read(bitmap, side, side);
                        for (var at = 0; at < pixels.Length; at++)
                        {
                            var point = new Vector2((at % side) + 0.5f, (at / side) + 0.5f);
                            if ((pixels[at] >> 24) > 24 && Vector2.Distance(point, centre) < clear - 0.75f)
                            {
                                inked++;
                            }
                        }
                    });
                    return inked;
                }
                if (Within(null) == 0)
                {
                    wrong.Add($"{size} px: the arrows never reach the dot's place, so the cut proves nothing");
                }
                if (Within(new DXGI_RGBA(0, 0, 0, 0)) is var left and > 0)
                {
                    wrong.Add($"{size} px: {left} pixels of the arrows inside the dot's gap");
                }
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    /// <summary>
    /// The pictures: the status icon for a restart waiting on the bar's own colour, at its size and
    /// four times over, to be held beside Windows' tray icon by eye.
    /// </summary>
    [UiFact]
    public void ThePictures()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (name, look) in Looks)
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var style = Style(scale, look);
                    using var island = Restart(gfx);
                    var (width, height) = island.CellSize(0, style);
                    const float zoom = 4;
                    Offscreen.Bitmap((width * (1 + zoom)) + 24, (height * zoom) + 16, dc =>
                    {
                        var background = look.HighContrast
                            ? new DXGI_RGBA(0, 0, 0, 1)
                            : look.Dark ? new DXGI_RGBA(0x20 / 255f, 0x20 / 255f, 0x20 / 255f, 1) : new DXGI_RGBA(0xF3 / 255f, 0xF3 / 255f, 0xF3 / 255f, 1);
                        dc->Clear(&background);
                        Offscreen.Shifted(dc, 8, 8, () => island.PaintCell(dc, 0, style, width, height));
                        D2D_MATRIX_3X2_F before;
                        dc->GetTransform(&before);
                        var zoomed = new D2D_MATRIX_3X2_F { _11 = zoom, _22 = zoom, _31 = width + 16, _32 = 8 };
                        dc->SetTransform(&zoomed);
                        island.PaintCell(dc, 0, style, width, height);
                        dc->SetTransform(&before);
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"restart-dot-{scale * 100:0}-{name}.png")));
                }
            }
        });
    }

    /// <summary>How many pixels of <paramref name="bitmap"/> are mostly covered and in <paramref name="red"/>: the dot's, for the other tests.</summary>
    internal static int Reds(IWICBitmap* bitmap, uint width, uint height, DXGI_RGBA red) =>
        Read(bitmap, width, height).Count(pixel => (pixel >> 24) > 128 && Is(pixel, red));

    private static uint[] Read(IWICBitmap* bitmap, uint width, uint height)
    {
        var pixels = new uint[width * height];
        fixed (uint* into = pixels)
        {
            ComUtil.Check(bitmap->CopyPixels(null, width * 4, width * height * 4, (byte*)into));
        }
        return pixels;
    }

    /// <summary>Whether a premultiplied pixel is <paramref name="ink"/> at its own coverage, give or take the rounding.</summary>
    private static bool Is(uint pixel, DXGI_RGBA ink)
    {
        var a = (int)(pixel >> 24);
        var (r, g, b) = ((int)((pixel >> 16) & 0xFF), (int)((pixel >> 8) & 0xFF), (int)(pixel & 0xFF));
        bool Near(int channel, float want) => Math.Abs(channel - (want * a)) <= 3;
        return a > 0 && Near(r, ink.r) && Near(g, ink.g) && Near(b, ink.b);
    }
}
