// edit, §704 (the owner, 2026-11-15: a "Liquid glass" look, Apple's): Liquid glass worn, the Appearance page's preview as
// Karpo draws it — the bar, its widgets and the clock's folder open over a desktop — over a bright wallpaper and a dark one
// (test pictures made here, never the person's own), in the dark mode and in the light one (Windows' mode injected), on a
// display 1920 × 1080 DIP at 150 % in edit mode. Its tint follows the wallpaper under the bar: clear over the bright one in
// the light mode, clouded over the dark one. To KARPO_PNG_DIR:
//
//   liquid-glass-<bright|dark>-<dark|light>.png   the preview alone, cut from the display
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void LiquidGlassSheets()
    {
        var palette = LookPalettes.Of(LookPalettes.LiquidGlassId)!;
        WindowShots.OnHiddenDesktop("liquid-glass", (gfx, instance) =>
        {
            const uint dpi = 144;
            const float scale = dpi / 96f;
            foreach (var (name, path) in new[] { ("bright", GlassWallpaper(bright: true)), ("dark", GlassWallpaper(bright: false)) })
            {
                var source = new WallpaperSource(path, File.GetLastWriteTimeUtc(path).Ticks, WallpaperFit.Fill, new Argb(255, 0, 0, 0));
                foreach (var dark in new[] { true, false })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = MenuCorpus.Fresh();
                    _ = LookPalettes.Wear(config, palette);
                    var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
                    var page = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), TheDock.LooksItems(config, bar, dark, [], LooksScope.Appearance));
                    var file = $"liquid-glass-{name}-{(dark ? "dark" : "light")}.png";
                    (byte[] Pixels, int Width, int Height)? seen = null;
                    _ = EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), file, dips: new Vector2(1920f, 1080f), keep: true, panel: deck =>
                    {
                        deck.Wallpaper = () => source;
                        deck.Memory = new PanelMemory();
                        deck.Memory.Show(PanelPage.Appearance);
                        deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), new SettingsContent([page]));
                        var preview = page.Rows.Single(row => row.Kind == PanelRowKind.Preview);
                        deck.Land(PanelPage.Appearance, preview.Key, light: false);
                        Assert.True(Assert.Single(deck.Previews!.OnWallpaper).Value, $"{file}: the preview is not on the desktop's picture");
                        var line = deck.Layout!.LineOf(preview.Key)!;
                        var box = deck.Layout.OnPanel(deck.Layout.PreviewBox(line));
                        var (flyout, window) = (deck.FlyoutOnScreen, WindowShots.Extent(deck.FlyoutHwnd));
                        var cut = new RECT { left = flyout.left - window.left + (int)box.X, top = flyout.top - window.top + (int)box.Y };
                        (cut.right, cut.bottom) = (cut.left + (int)MathF.Round(box.Width), cut.top + (int)MathF.Round(box.Height));
                        WindowShots.Settle(300);
                        Assert.True(WindowShots.Save(deck.FlyoutHwnd, file, cut, (pixels, wide) => seen = (pixels, wide, pixels.Length / 4 / wide)), $"{file}: no picture of the flyout");
                    });
                    var (pixels, w, h) = seen!.Value;
                    WindowShots.WritePng(pixels, w, h, Path.Combine(Offscreen.Pictures, file));
                }
            }
        });
    }

    /// <summary>§704: a test wallpaper, 1920 × 1080: a bright sky of soft colours, or a dark night of deep ones, with shapes to see blurred.</summary>
    private static string GlassWallpaper(bool bright)
    {
        var path = Path.Combine(Path.GetTempPath(), $"karpo-702-{(bright ? "bright" : "dark")}-wallpaper.bmp");
        const int width = 1920, height = 1080;
        var stride = width * 3;
        var bytes = new byte[54 + (stride * height)];
        void Int(int at, int value) => BitConverter.GetBytes(value).CopyTo(bytes, at);
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        Int(2, bytes.Length);
        Int(10, 54);
        Int(14, 40);
        Int(18, width);
        Int(22, -height);
        bytes[26] = 1;
        bytes[28] = 24;
        (double R, double G, double B)[] blobs = bright
            ? [(255, 196, 214), (170, 220, 255), (255, 236, 160), (190, 250, 210)]
            : [(40, 20, 90), (0, 60, 90), (90, 10, 50), (10, 40, 30)];
        (double X, double Y)[] at = [(300, 900), (900, 1000), (1500, 950), (1200, 300)];
        (double R, double G, double B) ground = bright ? (246, 244, 250) : (8, 10, 18);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = ground;
                for (var n = 0; n < blobs.Length; n++)
                {
                    var d = Math.Sqrt(Math.Pow(x - at[n].X, 2) + Math.Pow(y - at[n].Y, 2));
                    var t = Math.Clamp(1 - (d / 520), 0, 1);
                    t *= t;
                    (r, g, b) = (r + ((blobs[n].R - r) * t), g + ((blobs[n].G - g) * t), b + ((blobs[n].B - b) * t));
                }
                var offset = 54 + (y * stride) + (x * 3);
                (bytes[offset], bytes[offset + 1], bytes[offset + 2]) = ((byte)b, (byte)g, (byte)r);
            }
        }
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
