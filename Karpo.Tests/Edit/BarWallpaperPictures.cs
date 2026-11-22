// §547: what the person's taskbar shows, whatever Windows' mode (docs\widgets.md §547): the dock's real scene in Karpo's
// own look — nothing set: #202020 at 85 % over the wallpaper blurred, each widget on its solid card — over a test
// wallpaper (a picture made here, never the person's own), its strip composed and blurred as the dock does it
// (WallpaperStrips), at the Taskbar's Background blur 0, Karpo's 30 and 100; an open folder and a widget's menu; each in
// Windows' dark mode and its light one, captured as the compositor draws them (WindowShots) to KARPO_PNG_DIR:
// bar-wallpaper-blur<n>-<mode>.png, folder-547-<mode>.png, menu-547-<mode>.png. Each the same in both modes, pixel for pixel.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>The test wallpaper's display: the bar's picture's area, 1600 × 960 px.</summary>
    private static readonly RectI TestDisplay = new(0, 0, 1600, 960);

    /// <summary>
    /// A test wallpaper, written once to the temp folder as a 24-bit BMP: hue bands across, a dark diagonal stripe every 40
    /// px and a white square grid, so what is sharp and what is blurred shows at a glance.
    /// </summary>
    internal static string TestWallpaper()
    {
        var path = Path.Combine(Path.GetTempPath(), "karpo-547-test-wallpaper.bmp");
        var (width, height) = (TestDisplay.Width, TestDisplay.Height);
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
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var hue = x / (double)width * 6;
                var (r, g, b) = ((int)(255 * Math.Clamp(Math.Abs(hue - 3) - 1, 0, 1)), (int)(255 * Math.Clamp(2 - Math.Abs(hue - 2), 0, 1)), (int)(255 * Math.Clamp(2 - Math.Abs(hue - 4), 0, 1)));
                if ((x + y) / 40 % 2 == 0)
                {
                    (r, g, b) = (r / 3, g / 3, b / 3);
                }
                if (x % 80 < 4 || y % 80 < 4)
                {
                    (r, g, b) = (255, 255, 255);
                }
                var at = 54 + (y * stride) + (x * 3);
                (bytes[at], bytes[at + 1], bytes[at + 2]) = ((byte)b, (byte)g, (byte)r);
            }
        }
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>
    /// The default bar — Karpo's own: nothing set but the Taskbar's Background blur — over the test wallpaper, at blur 0,
    /// Karpo's 30 and the most, in Windows' dark mode and light: the same pixels in both; sharper to blurrier as the
    /// blur rises; the wallpaper showing through the bar's 85 %.
    /// </summary>
    [UiFact]
    public void TheBarOverTheWallpaperIsTheSameInBothModes()
    {
        var wallpaper = TestWallpaper();
        var shots = new Dictionary<(int Blur, bool Dark), (byte[] Pixels, int Width)>();
        WindowShots.OnHiddenDesktop("bar-wallpaper", (gfx, instance) =>
        {
            foreach (var blur in new[] { 0, KarpoLook.Blur, KarpoLook.MostBlur })
            {
                foreach (var dark in new[] { true, false })
                {
                    var config = new Config();
                    _ = Looks.WriteBlur(Looks.Section(config, LookPart.Taskbar), blur == KarpoLook.Blur ? null : blur);
                    // Windows' own as it may be: its mode, an orange accent in its light one, transparency effects on in its dark one.
                    var windows = AddListPictures.LookFor(dark) with { Animations = false, Glass = dark };
                    if (!dark)
                    {
                        windows = windows with { Accent = Windows.UI.Color.FromArgb(255, 0xCA, 0x50, 0x10), AccentLight2 = Windows.UI.Color.FromArgb(255, 0xF7, 0x95, 0x4F) };
                    }
                    var look = windows.Plain().Wearing(config);
                    Assert.Equal(Material.Translucent, look.Material);
                    WithColorBar(gfx, look, (layout, content) =>
                        shots[(blur, dark)] = ShootOverWallpaper(gfx, layout, content, look, wallpaper, $"bar-wallpaper-blur{blur}-{(dark ? "dark" : "light")}.png"));
                }
            }
        });
        // Pixel for pixel, but the last tenth of the bar: the notifications' badge there is this PC's own, live, and may come
        // in between two pictures.
        foreach (var blur in new[] { 0, KarpoLook.Blur, KarpoLook.MostBlur })
        {
            var ((dark, width), (light, _)) = (shots[(blur, true)], shots[(blur, false)]);
            Assert.Equal(dark.Length, light.Length);
            var differing = Enumerable.Range(0, dark.Length / 4)
                .Where(at => at % width < width * 9 / 10)
                .Count(at => !dark.AsSpan(at * 4, 4).SequenceEqual(light.AsSpan(at * 4, 4)));
            Assert.True(differing == 0, $"blur {blur}: {differing} px of the bar differ between Windows' modes");
        }
        // Along the bar's top row, over the wallpaper: its colours vary less the more it is blurred.
        double Spread(int blur)
        {
            var (pixels, width) = shots[(blur, true)];
            var row = Enumerable.Range(0, width).Select(x => (double)pixels[(width + x) * 4 + 1]).ToList();
            var mean = row.Average();
            return Math.Sqrt(row.Average(value => (value - mean) * (value - mean)));
        }
        var (sharp, some, most) = (Spread(0), Spread(KarpoLook.Blur), Spread(KarpoLook.MostBlur));
        Assert.True(sharp > some && some > most, $"the bar's top row spreads {sharp:F1}, {some:F1}, {most:F1} at blur 0, 30, 100");
        // The wallpaper shows through: not the one grey of a bar over nothing.
        Assert.True(sharp > 4, $"the bar's top row spreads {sharp:F1} over the wallpaper at blur 0");
    }

    /// <summary>
    /// The scene of <paramref name="layout"/> in <paramref name="look"/> with its strip of the wallpaper at
    /// <paramref name="wallpaper"/> composed and blurred as the dock does, over a black desk, captured and saved to
    /// <paramref name="file"/>: the panel's pixels and their width.
    /// </summary>
    private static (byte[] Pixels, int Width) ShootOverWallpaper(Gfx gfx, Layout layout, Content content, Look look, string wallpaper, string file)
    {
        var window = MakeWindow();
        var c = gfx.Compositor;
        var desk = c.CreateSpriteVisual();
        Scene? scene = null;
        Windows.UI.Composition.CompositionDrawingSurface? surface = null;
        try
        {
            _ = SetWindowPos(window, HWND.NULL, 0, 0, (int)layout.Width, (int)layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            var target = Target(gfx, window);
            desk.Size = new Vector2(layout.Width, layout.Height);
            desk.Brush = c.CreateColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
            scene = new Scene(gfx, target, desk);
            scene.Update(layout, content, look, new Icons(), shown: true, null, default);
            // The bar at the bottom of the test display, as the dock maps a bar's panel to its screen.
            var panel = layout.PanelRect;
            var bar = new RectI(0, TestDisplay.Bottom - (int)panel.Height, (int)panel.Width, TestDisplay.Bottom);
            var key = new WallpaperKey(WallpaperStrips.Source(wallpaper, WallpaperFit.Fill, new Argb(255, 0, 0, 0)), TestDisplay, TestDisplay, bar, Wallpapers.Margin(layout.Scale));
            var strip = WallpaperStrips.Compose(key);
            Assert.NotNull(strip);
            surface = WallpaperStrips.Blurred(gfx, strip, key.Margin, KarpoLook.Sigma(look.Dressing.Ground.Blur ?? KarpoLook.Blur, layout.Scale), null);
            scene.Wallpaper(surface);
            Assert.Same(surface, scene.WallpaperShown);
            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
            WindowShots.Settle(300);
            var part = new RECT { left = (int)panel.X, top = (int)panel.Y, right = (int)panel.Right, bottom = (int)panel.Bottom };
            byte[] pixels = [];
            var width = 0;
            Assert.True(WindowShots.Save(window, file, part, (shown, w) => (pixels, width) = (shown, w)), $"{file} came back blank");
            return (pixels, width);
        }
        finally
        {
            scene?.Dispose();
            surface?.Dispose();
            desk.Dispose();
            _ = DestroyWindow(window);
        }
    }

    /// <summary>
    /// An open folder and a widget's menu, nothing set, in Windows' dark mode and its light one (an orange accent in it): the
    /// same pixels in both — Karpo's own dark, solid surfaces.
    /// </summary>
    [UiFact]
    public void AnOpenFolderAndAWidgetsMenuAreTheSameInBothModes()
    {
        var shots = new Dictionary<string, byte[]>();
        WindowShots.OnHiddenDesktop("folder-menu-547", (gfx, instance) =>
        {
            var work = new RECT { left = 0, top = 0, right = 1600, bottom = 912 };
            var anchor = new POINT { x = 800, y = 912 };
            foreach (var dark in new[] { true, false })
            {
                var mode = dark ? "dark" : "light";
                var config = new Config();
                var windows = AddListPictures.LookFor(dark) with { Animations = false, Glass = !dark };
                if (!dark)
                {
                    windows = windows with { Accent = Windows.UI.Color.FromArgb(255, 0xCA, 0x50, 0x10), AccentDark1 = Windows.UI.Color.FromArgb(255, 0x8E, 0x38, 0x0B) };
                }
                var look = windows.Plain().Wearing(config);
                var island = BarKind.FolderList.New();
                island.Id = "folder-547";
                island.Folder!.Lists = [];
                foreach (var kind in new[] { ListKind.NightLight, ListKind.AirplaneMode, ListKind.Divider, ListKind.Battery, ListKind.Note })
                {
                    island.Folder.Put(kind.New(), 0);
                }
                using (var folder = new FolderIsland($"layout:default/{island.Id}", island, gfx, config))
                using (var flyout = new FolderFlyout(gfx, instance, 0))
                {
                    flyout.Show(gfx, look.Flyout(), 1f, anchor, work, Edge.Bottom, folder, FlyoutAlign.Center, false);
                    Assert.True(flyout.IsOpen);
                    WindowShots.Settle(250);
                    byte[]? pixels = null;
                    Assert.True(WindowShots.Save(flyout.Hwnd, $"folder-547-{mode}.png", default, (shown, _) => pixels = shown), "the folder came back blank");
                    shots[$"folder-{mode}"] = pixels!;
                    flyout.Hide();
                }
                using (var menu = new Flyout(gfx, instance, 0))
                {
                    FlyoutItem[] items = [new(1, "Open"), new(2, "Pin to taskbar"), new(0, "", Separator: true), new(3, "Settings")];
                    menu.Show(gfx, look, 1f, items, FlyoutLayout.List, anchor, work, Edge.Bottom);
                    Assert.True(menu.IsOpen);
                    WindowShots.Settle(250);
                    byte[]? pixels = null;
                    Assert.True(WindowShots.Save(menu.Hwnd, $"menu-547-{mode}.png", default, (shown, _) => pixels = shown), "the menu came back blank");
                    shots[$"menu-{mode}"] = pixels!;
                    menu.Hide();
                }
            }
        });
        foreach (var what in new[] { "folder", "menu" })
        {
            Assert.True(shots[$"{what}-dark"].AsSpan().SequenceEqual(shots[$"{what}-light"]), $"the {what} differs between Windows' modes");
        }
    }
}
