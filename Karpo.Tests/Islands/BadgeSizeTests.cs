// The notifications bell's badge — the notifications preset's bell face since the bell went
// (docs\widgets.md §100) — measured from the pixels the folder's button draws.
//
// The bug the vertical rule found (docs\widgets.md §12.4): the badge's size came from the
// text scale while the bell's glyph comes from the cell, so at double text the pill was twice the
// bell it hangs on and swallowed it. It is measured from the bell now, and the number inside it
// shrinks into the pill rather than the pill growing to the number — so the mark looks the same
// whatever the text scale, which is what a mark on an icon should do.
//
// Needs Direct2D: KARPO_UI_TESTS=1; the picture goes to KARPO_PNG_DIR.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Islands;

public sealed unsafe class BadgeSizeTests
{
    private static readonly Look Dark = new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    private static IslandStyle Style(float thickness, bool vertical, float textScale) =>
        new(1f, vertical, thickness, MathF.Round(thickness / 4) * 2, Dark with { TextScale = textScale });

    /// <summary>A bell face whose store has <paramref name="count"/> notifications waiting.</summary>
    private static FolderIsland Bell(Gfx gfx, string folder, int count)
    {
        var database = Path.Combine(folder, $"db{count}", "wpndatabase.db");
        FlyoutOffscreenTests.BuildDatabase(database, count);
        var store = new NotificationStore(database, Path.Combine(folder, $"copy{count}"), Path.Combine(folder, $"state{count}"), app => (app, null));
        store.Prepare();
        return BellFace(gfx, store, $"/n{count}", new Config());
    }

    /// <summary>The notifications preset's folder, wearing the bell, on <paramref name="store"/>: what the bell on the bar is now.</summary>
    internal static FolderIsland BellFace(Gfx gfx, NotificationStore store, string key, Config config)
    {
        NotificationPartTile.Stand = store;
        try
        {
            var item = Presets.Folder(Presets.Find(Presets.NotificationsId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
            var bell = new FolderIsland(key, item, gfx, config);
            _ = bell.Refresh(gfx);
            return bell;
        }
        finally
        {
            NotificationPartTile.Stand = null;
        }
    }

    /// <summary>What the cell puts ink on, and how big the cell is.</summary>
    private static (float Width, float Height, (float Left, float Top, float Right, float Bottom) Ink) Drawn(
        FolderIsland bell, IslandStyle style)
    {
        var cells = bell.Measure(style);
        var length = cells.Lengths is { Count: > 0 } lengths ? lengths[0] : cells.CellLength;
        var width = MathF.Ceiling(style.Vertical ? style.Thickness : length);
        var height = MathF.Ceiling(style.Vertical ? length : style.Thickness);
        (float Left, float Top, float Right, float Bottom) ink = default;
        Offscreen.Bitmap(
            width,
            height,
            dc => bell.PaintCell(dc, 0, style, width, height),
            bitmap => ink = Offscreen.InkOf(bitmap, (uint)width, (uint)height));
        return (width, height, ink);
    }

    /// <summary>
    /// The badge is the same mark at every text scale: it follows the bell, which follows the
    /// cell. Before, at double text it was twice the glyph and covered it.
    /// </summary>
    [UiFact]
    public void TheBadgeDoesNotGrowWithTheTextScale()
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "badge-" + Guid.NewGuid().ToString("N"));
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                using var bell = Bell(gfx, folder, 5);
                foreach (var vertical in new[] { false, true })
                {
                    foreach (var thickness in new float[] { 28, 48, 72 })
                    {
                        var one = Drawn(bell, Style(thickness, vertical, 1f));
                        foreach (var scale in new[] { 1.5f, 2f })
                        {
                            var grown = Drawn(bell, Style(thickness, vertical, scale));
                            Assert.Equal(one.Width, grown.Width);
                            Assert.Equal(one.Height, grown.Height);
                            var mine = grown.Ink;
                            var was = one.Ink;
                            Assert.True(
                                MathF.Abs((mine.Right - mine.Left) - (was.Right - was.Left)) <= 1
                                && MathF.Abs((mine.Bottom - mine.Top) - (was.Bottom - was.Top)) <= 1,
                                $"a {thickness} px {(vertical ? "vertical" : "horizontal")} bell at x{scale} text draws "
                                    + $"{mine.Right - mine.Left}x{mine.Bottom - mine.Top}, against {was.Right - was.Left}x{was.Bottom - was.Top} at x1");
                        }
                    }
                }
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>The bell and its badge stay inside the cell, at every text scale and either way round.</summary>
    [UiFact]
    public void TheBadgeStaysInsideTheCell()
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "badge-" + Guid.NewGuid().ToString("N"));
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                foreach (var count in new[] { 5, 150 })
                {
                    using var bell = Bell(gfx, folder, count);
                    foreach (var vertical in new[] { false, true })
                    {
                        foreach (var scale in new[] { 1f, 1.5f, 2f })
                        {
                            var (width, height, ink) = Drawn(bell, Style(48, vertical, scale));
                            Assert.True(ink.Right <= width && ink.Bottom <= height && ink.Left >= 0 && ink.Top >= 0,
                                $"the bell drew {ink.Left}..{ink.Right} x {ink.Top}..{ink.Bottom} in a {width}x{height} cell");
                            // And it drew a bell and a badge, not one of them: the ink is wider
                            // than the glyph alone but not the whole cell.
                            Assert.True(ink.Right - ink.Left > 8, "nothing was drawn");
                        }
                    }
                }
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>The bell with a badge at x1, x1.5 and x2 text, across the bar and down it.</summary>
    [UiFact]
    public void ASheetOfTheBellAtEveryTextScale()
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "badge-" + Guid.NewGuid().ToString("N"));
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                using var few = Bell(gfx, folder, 5);
                using var many = Bell(gfx, folder, 150);
                foreach (var vertical in new[] { false, true })
                {
                    const float gap = 10;
                    const float thickness = 48;
                    float[] scales = [1f, 1.5f, 2f];
                    var cells = new List<(FolderIsland Bell, IslandStyle Style, float Width, float Height)>();
                    foreach (var scale in scales)
                    {
                        foreach (var bell in new[] { few, many })
                        {
                            var style = Style(thickness, vertical, scale);
                            var measured = bell.Measure(style);
                            var length = measured.Lengths is { Count: > 0 } lengths ? lengths[0] : measured.CellLength;
                            cells.Add((bell, style, vertical ? thickness : length, vertical ? length : thickness));
                        }
                    }
                    var width = cells.Sum(cell => cell.Width + gap) + gap;
                    var height = cells.Max(cell => cell.Height) + (2 * gap);
                    Offscreen.Bitmap(
                        width,
                        height,
                        dc =>
                        {
                            var shade = new DXGI_RGBA(0.13f, 0.13f, 0.13f, 1);
                            dc->Clear(&shade);
                            var x = gap;
                            foreach (var (bell, style, cellWidth, cellHeight) in cells)
                            {
                                var at = (x, bell, style, cellWidth, cellHeight);
                                Offscreen.Shifted(dc, at.x, gap, () => at.bell.PaintCell(dc, 0, at.style, at.cellWidth, at.cellHeight));
                                x += cellWidth + gap;
                            }
                        },
                        bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"notification-badge-{(vertical ? "down" : "across")}.png")));
                }
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    private static void Clean(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (IOException)
        {
            // A file the store still holds: the temp folder can keep it.
        }
    }
}
