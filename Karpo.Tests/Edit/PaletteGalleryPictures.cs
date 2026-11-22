// edit, §618 (the person, 2026-11-09: "add some more styles. the newly added styles should also incorporate stuff like
// transparency, patterns. dark backgrounds, light background. high contrast etc."): every palette Karpo designed worn in turn,
// the Appearance page's preview as Karpo draws it — the desktop (a test wallpaper made here, never the person's own), the bar
// and its widgets, the clock's folder open over it — in the dark mode and in the light one (Windows' mode injected: its real
// setting never touched), on a display 1920 × 1080 DIP at 150 % in edit mode. To KARPO_PNG_DIR:
//
//   palette-<id>-<dark|light>.png   the preview alone, cut from the display
//   palette-sheet.png               every palette a row, in the Palette dropdown's order, its dark preview, then its light one
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
    public void PaletteGallerySheets()
    {
        var wallpaper = DuskWallpaper();
        var source = new WallpaperSource(wallpaper, File.GetLastWriteTimeUtc(wallpaper).Ticks, WallpaperFit.Fill, new Argb(255, 0, 0, 0));
        var shots = new List<(string Id, bool Dark, byte[] Pixels, int Width, int Height)>();
        WindowShots.OnHiddenDesktop("palette-gallery", (gfx, instance) =>
        {
            const uint dpi = 144;
            const float scale = dpi / 96f;
            foreach (var palette in LookPalettes.All)
            {
                foreach (var dark in new[] { true, false })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = MenuCorpus.Fresh();
                    _ = LookPalettes.Wear(config, palette);
                    var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
                    var page = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), TheDock.LooksItems(config, bar, dark, [], LooksScope.Appearance));
                    var file = $"palette-{palette.Id}-{(dark ? "dark" : "light")}.png";
                    (byte[] Pixels, int Width, int Height)? seen = null;
                    _ = EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), file, dips: new Vector2(1920f, 1080f), keep: true, panel: deck =>
                    {
                        deck.Wallpaper = () => source;
                        deck.Memory = new PanelMemory();
                        deck.Memory.Show(PanelPage.Appearance);
                        deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), new SettingsContent([page]));
                        var preview = page.Rows.Single(row => row.Kind == PanelRowKind.Preview);
                        deck.Land(PanelPage.Appearance, preview.Key, light: false);
                        // The preview in the palette's mode, on the desktop's picture; the palette chosen in its row.
                        Assert.Equal(dark, Assert.Single(deck.Previews!.Modes).Value);
                        Assert.True(Assert.Single(deck.Previews.OnWallpaper).Value, $"{file}: the preview is not on the desktop's picture");
                        Assert.Equal(palette.Name, PanelRows.All(page.Rows).Single(row => row.Text == S.SettingsLookPalette && row.Kind == PanelRowKind.Choice).Value);
                        // The preview cut from the flyout's own window, opaque as it is: the display's picture lays a flyout over
                        // what is under it taking its pure black for clear room, and Contrast's and Midnight's black is drawn.
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
                    shots.Add((palette.Id, dark, pixels, w, h));
                }
            }
        });

        // The sheet: a row a palette, in the dropdown's order, its dark preview and its light one side by side.
        const int gap = 16;
        var rows = shots.GroupBy(shot => shot.Id).Select(each => each.OrderByDescending(shot => shot.Dark).ToList()).ToList();
        Assert.Equal(LookPalettes.All.Select(palette => palette.Id), rows.Select(row => row[0].Id));
        var sheetWide = gap + rows.Max(row => row.Sum(shot => shot.Width + gap));
        var sheetTall = gap + rows.Sum(row => row.Max(shot => shot.Height) + gap);
        var sheet = new byte[sheetWide * sheetTall * 4];
        for (var at = 0; at < sheet.Length; at += 4)
        {
            (sheet[at], sheet[at + 1], sheet[at + 2], sheet[at + 3]) = (0x80, 0x80, 0x80, 255);
        }
        var top = gap;
        foreach (var row in rows)
        {
            var left = gap;
            foreach (var (_, _, pixels, w, h) in row)
            {
                for (var y = 0; y < h; y++)
                {
                    Buffer.BlockCopy(pixels, y * w * 4, sheet, (((top + y) * sheetWide) + left) * 4, w * 4);
                }
                left += w + gap;
            }
            top += row.Max(shot => shot.Height) + gap;
        }
        WindowShots.WritePng(sheet, sheetWide, sheetTall, Path.Combine(Offscreen.Pictures, "palette-sheet.png"));
    }
}
