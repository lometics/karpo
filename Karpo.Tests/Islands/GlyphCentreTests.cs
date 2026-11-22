// quick: the speaker sitting in the middle of its widget at every volume, measured from the pixels
// the widget really draws.
//
// The bug: Segoe Fluent gives every icon the same advance and not the same ink. The speaker's waves
// stretch to the right edge of the advance as the volume rises, so the quietest speaker (E992) is
// ink from 0 to 8.3 of a 19 px box while the loudest (E995) fills all 19. The system widgets placed
// their glyph by the box, so at the lowest setting the speaker sat a third of a glyph left of the
// middle, which is what "the sound icon looks off center" was. Everything else in Karpo places a
// glyph by its ink (Gfx.InkOrigin); these widgets do now too.
//
// Where two glyphs are drawn one over the other — the Wi-Fi arcs a signal reaches over the faint
// full set — both take the fuller one's origin, so the one with less ink sits inside the other
// rather than being centred on its own.
//
// Needs Direct2D, so it runs only with KARPO_UI_TESTS=1; the picture goes to KARPO_PNG_DIR.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Islands;

public sealed unsafe class GlyphCentreTests
{
    private const string Mute = "\uE74F";
    private static readonly string[] Speakers = ["\uE992", "\uE993", "\uE994", "\uE995"];
    private static readonly string[] WifiBars = ["\uE872", "\uE873", "\uE874", "\uE701"];

    private static readonly Look Dark = new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    /// <summary>
    /// The sound widget's ink sits in the middle of its cell at every level, and at every bar
    /// thickness. Half a pixel of slack: the origin is rounded to whole pixels, and antialiasing
    /// puts a faint column at each end of the ink.
    /// </summary>
    [UiFact]
    public void TheSoundWidgetsSpeakerIsCentredAtEveryLevel()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var thickness in new float[] { 28, 40, 48, 72 })
            {
                string[] levels = [Mute, .. Speakers];
                foreach (var glyph in levels)
                {
                    var (width, height, ink) = Measure(gfx, thickness, State(volume: glyph));
                    var middle = width / 2;
                    var centre = (ink.Left + ink.Right) / 2;
                    Assert.True(
                        MathF.Abs(centre - middle) <= 1,
                        $"speaker U+{char.ConvertToUtf32(glyph, 0):X4} in a {thickness} px cell: ink {ink.Left}..{ink.Right} centres on {centre}, the cell on {middle}");
                    // And it is a glyph's worth of ink, not a stray pixel; the button no longer than the
                    // bar is thick: the glyph and its ends (§275).
                    // The quietest speaker is the narrowest of them: a third of the glyph's box.
                    Assert.True(ink.Right - ink.Left > thickness / 10, $"the widget drew next to nothing: ink {ink.Left}..{ink.Right} in {width}x{height} for U+{char.ConvertToUtf32(glyph, 0):X4}");
                    Assert.True(width <= height, $"a {width} by {height} button");
                }
            }
        });
    }

    /// <summary>
    /// The speaker keeps its height and its vertical place as the level changes: the waves grow
    /// sideways, so nothing should move up or down.
    /// </summary>
    [UiFact]
    public void TheSpeakerDoesNotMoveUpOrDownWithTheLevel()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var tops = new List<float>();
            var bottoms = new List<float>();
            foreach (var glyph in Speakers)
            {
                var (_, _, ink) = Measure(gfx, 48, State(volume: glyph));
                tops.Add(ink.Top);
                bottoms.Add(ink.Bottom);
            }
            Assert.True(tops.Max() - tops.Min() <= 2, $"the speaker's top moved: {string.Join(", ", tops)}");
            Assert.True(bottoms.Max() - bottoms.Min() <= 2, $"the speaker's foot moved: {string.Join(", ", bottoms)}");
        });
    }

    /// <summary>
    /// The Wi-Fi face draws the arcs a signal reaches over the faint full set, with the drawing the
    /// network button had (SystemGlyph.Paint). Whatever the strength, the ink the two make
    /// together is the full set's, centred: the front glyph is placed by the back one, so it never
    /// slides out of it.
    /// </summary>
    [UiFact]
    public void TheWifiArcsStayInsideTheFaintFullSet()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var full = MeasureWifi(gfx, 48, State(network: WifiBars[^1], back: WifiBars[^1]));
            foreach (var bars in WifiBars)
            {
                var (width, _, ink) = MeasureWifi(gfx, 48, State(network: bars, back: WifiBars[^1]));
                Assert.True(MathF.Abs(((ink.Left + ink.Right) / 2) - (width / 2)) <= 1, $"the arcs are off centre for {bars}");
                // The pair's ink is the full set's, however weak the signal.
                Assert.True(MathF.Abs(ink.Left - full.Ink.Left) <= 1 && MathF.Abs(ink.Right - full.Ink.Right) <= 1,
                    $"the pair's ink moved for {bars}: {ink.Left}..{ink.Right} against {full.Ink.Left}..{full.Ink.Right}");
            }
        });
    }

    /// <summary>The sound widget at every level, side by side, with a line through the middle of each cell.</summary>
    [UiFact]
    public void ASheetOfTheSoundWidgetAtEveryLevel()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                var look = dark ? Dark : Dark with { Dark = false };
                const float thickness = 48;
                string[] levels = [Mute, .. Speakers];
                Sheet(gfx, look, thickness, levels, $"sound-widget-levels-{(dark ? "dark" : "light")}.png");
            }
        });
    }

    // --- Drawing a widget's cell and reading its ink back ----------------------------

    private static SystemState State(string? volume = null, string? network = null, string? back = null) =>
        new(network ?? WifiBars[^1], volume ?? Speakers[^1], back);

    private static IslandStyle Style(Look look, float thickness) =>
        new(1f, false, thickness, MathF.Round(thickness / 2 / 2) * 2, look);

    /// <summary>The sound preset's folder, its face showing <paramref name="state"/>: the speaker the sound button was.</summary>
    private static FolderIsland Island(Gfx gfx, SystemState state)
    {
        FolderIsland.StatusStand = state;
        try
        {
            var holder = Presets.Folder(Presets.Find(Presets.SoundId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
            return new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
        }
        finally
        {
            FolderIsland.StatusStand = null;
        }
    }

    /// <summary>The one cell's length along the bar, as the layout would give it.</summary>
    private static float Length(FolderIsland island, IslandStyle style)
    {
        var cells = island.Measure(style);
        return cells.Lengths is { Count: > 0 } lengths ? lengths[0] : cells.CellLength;
    }

    /// <summary>The widget's cell drawn into a bitmap, and the box of the pixels it put ink on.</summary>
    private static (float Width, float Height, (float Left, float Top, float Right, float Bottom) Ink) Measure(
        Gfx gfx, float thickness, SystemState state)
    {
        using var island = Island(gfx, state);
        var style = Style(Dark, thickness);
        var width = MathF.Ceiling(Length(island, style));
        var height = MathF.Ceiling(thickness);
        (float Left, float Top, float Right, float Bottom) ink = default;
        Offscreen.Bitmap(
            width,
            height,
            dc => island.PaintCell(dc, 0, style, width, height),
            bitmap => ink = Offscreen.InkOf(bitmap, (uint)width, (uint)height));
        return (width, height, ink);
    }

    /// <summary>
    /// The Wi-Fi face's glyphs for <paramref name="state"/> drawn into a cell as long as the sound
    /// face's (both are one glyph's square), and the box of the pixels they put ink on.
    /// </summary>
    private static (float Width, float Height, (float Left, float Top, float Right, float Bottom) Ink) MeasureWifi(
        Gfx gfx, float thickness, SystemState state)
    {
        using var sound = Island(gfx, state);
        var style = Style(Dark, thickness);
        var width = MathF.Ceiling(Length(sound, style));
        var height = MathF.Ceiling(thickness);
        var (glyph, back, dim, badge) = StatusFaceDrawing.Glyphs(FolderFaces.WifiId, state);
        (float Left, float Top, float Right, float Bottom) ink = default;
        Offscreen.Bitmap(
            width,
            height,
            dc => SystemGlyph.Paint(dc, gfx, style, glyph, back, dim, badge, new D2D_RECT_F(0, 0, width, height)),
            bitmap => ink = Offscreen.InkOf(bitmap, (uint)width, (uint)height));
        return (width, height, ink);
    }

    private static void Sheet(Gfx gfx, Look look, float thickness, string[] glyphs, string file)
    {
        var style = Style(look, thickness);
        var islands = glyphs.Select(glyph => Island(gfx, State(volume: glyph))).ToList();
        try
        {
            var widths = islands.Select(island => MathF.Ceiling(Length(island, style))).ToList();
            const float gap = 8;
            var width = widths.Sum() + (gap * (widths.Count - 1));
            Offscreen.Bitmap(
                width,
                thickness,
                dc =>
                {
                    var background = look.Dark ? new DXGI_RGBA(0.13f, 0.13f, 0.13f, 1) : new DXGI_RGBA(0.96f, 0.96f, 0.96f, 1);
                    dc->Clear(&background);
                    var at = 0f;
                    for (var index = 0; index < islands.Count; index++)
                    {
                        var cell = widths[index];
                        var drawn = dc;
                        Offscreen.Shifted(dc, at, 0, () => islands[index].PaintCell(drawn, 0, style, cell, thickness));
                        // The middle of the cell, so any drift shows against it.
                        var line = PopupPaint.Rect(at + MathF.Round(cell / 2), 0, 1, thickness);
                        PopupPaint.Fill(dc, line, 0, new DXGI_RGBA(1, 0.3f, 0.3f, 0.65f));
                        at += cell + gap;
                    }
                },
                bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
        }
        finally
        {
            foreach (var island in islands)
            {
                island.Dispose();
            }
        }
    }
}
