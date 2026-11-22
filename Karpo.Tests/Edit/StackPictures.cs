// No surface over another (docs\widgets.md §464; the person, 2026-10-31: "i also dont want that items stack their background,
// with that i mean that if a folder face is transparent for example it should not show the background of the taskbar but it
// should show the desktop directly. same for the widgets in the folder"): a real bar in each of ours, both modes, at 150 %,
// the Appearance's Taskbar widgets half see-through (§567: every card alike, no widget's own), the folder open over it, and
// a menu over Widgets in folders of None. Each
// window's pixels as the compositor draws them (WindowShots, premultiplied, with their alpha) are laid over a busy wallpaper
// of the test's own, as the desktop would be under them, to KARPO_PNG_DIR as stack-<style>-<look>.png; and read back:
//
//  - the bar draws where no card is;
//  - a see-through widget is its colour over the desktop, nothing of the bar's under it;
//  - the folder draws nothing between its sections: the wallpaper shows in the gap;
//  - a menu over a None background is Windows' own flyout, never see-through.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>The wallpaper the pictures lay the windows over: diagonal bands of four colours, busy enough to show through anything.</summary>
    private static (byte B, byte G, byte R) Wallpaper(int x, int y) => ((x + (2 * y)) / 23 % 4) switch
    {
        0 => (0x2A, 0x9D, 0xE9),
        1 => (0x8F, 0x3E, 0x1C),
        2 => (0x5A, 0xC8, 0x6F),
        _ => (0xE8, 0xE4, 0xF2),
    };

    /// <summary>§464: no surface over another, in each of ours and both modes; see the top of the file.</summary>
    [UiFact]
    public void NoSurfaceStandsOnAnother()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("stack", (gfx, instance) =>
        {
            foreach (var id in LookShorthand.Palettes)
            {
                foreach (var dark in new[] { false, true })
                {
                    StackSheet(gfx, instance, id, dark, failures);
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private void StackSheet(Gfx gfx, HINSTANCE instance, string? id, bool dark, List<string> failures)
    {
        var name = $"stack-{id ?? "windows"}-{(dark ? "dark" : "light")}";
        var config = new Config { Height = 48 };
        if (LookPalettes.Of(id) is { } palette)
        {
            _ = LookPalettes.Wear(config, palette);
        }
        var drawn = Looks.Of(config, dark: true);
        // §567: the Taskbar widgets half see-through, every card alike.
        var half = new Karpo.Core.Paint((drawn.Widgets.Background.Color ?? new Argb(255, 0x80, 0x80, 0x80)).At(0.5)).Text!;
        Assert.True(Looks.Write(Looks.Section(config, LookPart.Widgets), look => look.Background = LookShorthand.Pair(half)));

        var bar = Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch, windows: WindowsTaskbar.OutOfTheBox);
        var start = Math.Max(0, bar.FindIndex(item => Sections.Known(item.Section) == BarSection.Start));
        var folder = BarKind.FolderList.New();
        folder.Id = "stacked";
        folder.Section = BarSection.Start;
        foreach (var kind in new[] { ListKind.NightLight, ListKind.AirplaneMode, ListKind.Divider, ListKind.EnergySaver, ListKind.Battery })
        {
            _ = folder.Folder!.Put(kind.New());
        }
        var widget = BarKind.NightLight.New();
        widget.Id = "stacked-widget";
        widget.Section = BarSection.Start;
        bar.Insert(start, folder);
        bar.Insert(start + 1, widget);
        config.DefaultLayout.Islands = bar;
        gfx.Typeface = Looks.MenuText(config);
        var look = (TestLooks.LookFor(dark) with { Animations = false, Glass = true }).Wearing(config);
        var (specs, islands) = Made(gfx, config, bar);
        var window = MakeWindow();
        DesktopWindowTarget? target = null;
        Scene? scene = null;
        FolderIsland? open = null;
        try
        {
            var area = new RECT { left = 0, top = 0, right = ThemeScreenW, bottom = 1440 };
            var content = new Content { Islands = islands };
            var layout = new Layout(gfx, 144, area, area, Edge.Bottom, config, look, specs, content, editing: false);
            var panelRect = layout.PanelRect;
            var windowTop = ThemeScreenH - (int)panelRect.Bottom;
            var top = windowTop + (int)panelRect.Y;
            _ = SetWindowPos(window, HWND.NULL, 0, windowTop, (int)layout.Width, (int)layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            target = Target(gfx, window);
            scene = Scenes.Build(gfx, target, layout, content, look, new Icons(), shown: true);
            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);

            open = new FolderIsland($"layout:default/{folder.Id}", folder, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var work = new RECT { left = 0, top = 0, right = ThemeScreenW, bottom = top };
            var faceRect = layout.CardRect(specs.FindIndex(spec => spec.Key.EndsWith("/stacked", StringComparison.Ordinal)));
            var widgetRect = layout.CardRect(specs.FindIndex(spec => spec.Key.EndsWith("/stacked-widget", StringComparison.Ordinal)));
            flyout.Show(gfx, look, 1.5f, new POINT { x = (int)(faceRect.X + (faceRect.Width / 2)), y = top }, work, Edge.Bottom, open);

            // A menu of a layout whose lines (and so its menus) are None.
            var plainConfig = new Config();
            plainConfig.Appearance = config.Appearance?.Copy();
            var bareLines = Looks.Section(plainConfig, LookPart.FolderWidgets);
            Assert.True(LookShorthand.Both(bareLines, Tone.Background, Karpo.Core.Paint.Nothing));
            var plain = (TestLooks.LookFor(dark) with { Animations = false, Glass = true }).Wearing(plainConfig);
            using var menu = new Flyout(gfx, instance, 0);
            List<FlyoutItem> rows = [new FlyoutItem(1, "Open"), new FlyoutItem(2, "Refresh"), new FlyoutItem(3, "Settings")];
            menu.Show(gfx, plain, 1.5f, rows, FlyoutLayout.List, new POINT { x = 1900, y = top }, work, Edge.Bottom, null);
            WindowShots.Settle(400);

            // Each window over the wallpaper, as the desktop is under it.
            var frame = new RECT { left = 0, top = 0, right = ThemeScreenW, bottom = ThemeScreenH };
            var (w, h) = (frame.right, frame.bottom);
            var canvas = new byte[w * h * 4];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var (b, g, r) = Wallpaper(x, y);
                    var at = ((y * w) + x) * 4;
                    (canvas[at], canvas[at + 1], canvas[at + 2], canvas[at + 3]) = (b, g, r, 255);
                }
            }
            var barPart = new RECT { left = (int)panelRect.X, top = top, right = (int)panelRect.Right, bottom = ThemeScreenH };
            var barPixels = Over(canvas, w, h, window, barPart);
            var folderPixels = Over(canvas, w, h, flyout.Hwnd, flyout.PanelOnScreen);
            var menuPixels = Over(canvas, w, h, menu.Hwnd, menu.OpenPanels[0].Panel);
            WindowShots.WritePng(canvas, w, h, Path.Combine(Offscreen.Pictures, $"{name}.png"));

            // What a window drew at a point of the screen: its premultiplied pixel there, or none.
            (byte B, byte G, byte R, byte A)? At(Dictionary<(int, int), (byte, byte, byte, byte)> drawn, int x, int y) => drawn.TryGetValue((x, y), out var pixel) ? pixel : null;
            void Expect(bool held, string what)
            {
                if (!held)
                {
                    failures.Add($"{name}: {what}");
                }
            }
            var y0 = windowTop;
            // The bar: drawn where no card is (its corner).
            var ground = At(barPixels, (int)panelRect.X + 1, y0 + (int)panelRect.Y + 1);
            Expect(ground is { } plate && !Bare(plate), $"the bar draws {ground} where no card is");
            // The see-through widget: its colour alone over the desktop, nothing of the bar's under it.
            var card = Looks.Of(config, dark: true).Widgets.Background.Color!.Value;
            var seen = At(barPixels, (int)(widgetRect.X + (widgetRect.Width / 2)), y0 + (int)widgetRect.Y + 2);
            Expect(seen is { } pixel && Alone(pixel, card), $"the half see-through widget draws {seen}, not its colour {card} alone");
            // The folder: nothing between its two sections.
            if (flyout.PiecesShown is { Count: >= 2 } pieces)
            {
                var panel = flyout.PanelOnScreen;
                var gapY = (int)(pieces[0].bottom <= pieces[1].top ? (pieces[0].bottom + pieces[1].top) / 2 : (pieces[1].bottom + pieces[0].top) / 2);
                var gap = At(folderPixels, panel.left + (int)((pieces[0].left + pieces[0].right) / 2), panel.top + gapY);
                Expect(gap is not { } drawnThere || Bare(drawnThere), $"the folder draws {gap} between its sections");
            }
            else
            {
                Expect(false, "the folder has no two sections");
            }
            // The menu over a None background: Windows' own flyout, solid enough, never see-through.
            var said = menu.OpenPanels[0].Panel;
            var inMenu = At(menuPixels, (said.left + said.right) / 2, said.bottom - 4);
            var tint = menu.Theme.Tint;
            Expect(inMenu is { } lit && Alone(lit, new Argb(tint.A, tint.R, tint.G, tint.B)) && tint.A >= 200, $"the menu over None draws {inMenu}, not Windows' flyout {tint}");
            // §568: of the mode Windows is in, the palette's half of it drawn so.
            Expect(menu.Theme.Base == FlyoutTheme.For(TestLooks.LookFor(dark) with { Glass = true }).Base, "the menu over None is not Windows' own flyout");
            flyout.Hide();
            menu.Hide();
        }
        finally
        {
            gfx.Typeface = default;
            open?.Dispose();
            scene?.Dispose();
            _ = DestroyWindow(window);
            foreach (var island in islands.Values)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>
    /// Whether a captured pixel is nothing drawn: the capture (PrintWindow) gives the room a window leaves clear as opaque
    /// black, its alpha lost — as it does what DWM's blur would show; nothing Karpo draws is pure black.
    /// </summary>
    private static bool Bare((byte B, byte G, byte R, byte A) pixel) => pixel.A == 0 || pixel is (0, 0, 0, _);

    /// <summary>Whether a captured pixel is <paramref name="color"/> alone over nothing (premultiplied, as the capture has it).</summary>
    private static bool Alone((byte B, byte G, byte R, byte A) pixel, Argb color)
    {
        int Of(byte channel) => (int)Math.Round(channel * color.A / 255.0);
        return Math.Abs(pixel.R - Of(color.R)) <= 4 && Math.Abs(pixel.G - Of(color.G)) <= 4 && Math.Abs(pixel.B - Of(color.B)) <= 4;
    }

    /// <summary>
    /// <paramref name="window"/>'s <paramref name="part"/> (screen px) laid over <paramref name="canvas"/> by its alpha, as the
    /// desktop is under it; what it drew, premultiplied, by each screen point of the part.
    /// </summary>
    private static Dictionary<(int, int), (byte B, byte G, byte R, byte A)> Over(byte[] canvas, int width, int height, HWND window, RECT part)
    {
        var drawn = new Dictionary<(int, int), (byte, byte, byte, byte)>();
        var box = WindowShots.Extent(window);
        var cut = new RECT { left = part.left - box.left, top = part.top - box.top, right = part.right - box.left, bottom = part.bottom - box.top };
        byte[]? pixels = null;
        var w = 0;
        _ = WindowShots.Save(window, "stack-part.png", cut, (shown, shownWidth) => (pixels, w) = (shown, shownWidth), alpha: true);
        if (pixels is null || w == 0)
        {
            return drawn;
        }
        var x0 = Math.Max(part.left, box.left);
        var y0 = Math.Max(part.top, box.top);
        for (var y = 0; y < pixels.Length / 4 / w; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var (sx, sy) = (x0 + x, y0 + y);
                if (sx < 0 || sy < 0 || sx >= width || sy >= height)
                {
                    continue;
                }
                var from = ((y * w) + x) * 4;
                var pixel = (pixels[from], pixels[from + 1], pixels[from + 2], pixels[from + 3]);
                drawn[(sx, sy)] = pixel;
                var to = ((sy * width) + sx) * 4;
                var keep = Bare(pixel) ? 255 : 255 - pixel.Item4;
                canvas[to] = (byte)Math.Min(255, pixel.Item1 + (canvas[to] * keep / 255));
                canvas[to + 1] = (byte)Math.Min(255, pixel.Item2 + (canvas[to + 1] * keep / 255));
                canvas[to + 2] = (byte)Math.Min(255, pixel.Item3 + (canvas[to + 2] * keep / 255));
            }
        }
        return drawn;
    }
}
