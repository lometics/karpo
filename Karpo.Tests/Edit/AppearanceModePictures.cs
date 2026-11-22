// edit, §613 (the person, 2026-11-09: "the appearance should actually only show for dark or light mode including all the
// settings ... a switcher at the top, either dark light or system"; "the color selector should be wider but not as tall";
// "you should be able to reset individual properties to the style default"; "the appearance preview should show more widgets
// ... the background image of the desktop ... a light and dark mode icon"): the Appearance page as Karpo draws it, Windows'
// mode injected (dark, then light: its real setting never touched), on a display 1920 DIP wide at 150 % in edit mode, over a
// test wallpaper made here (a dusk landscape; never the person's own, EditDeck.Wallpaper given it). Graphite worn, its cards'
// background changed in both modes, so the Taskbar widgets tab shows that value's reset beside it. To KARPO_PNG_DIR:
//
//   appearance613-page-<system|dark|light>-<dark|light>.png   the whole page, Mode as named, Windows as named last: the
//                                                            switcher, Palette, the preview in the mode in force, the tab
//   appearance613-reset-<dark|light>.png                     landed on Background: its reset's icon button before its colour
//   appearance613-picker-<dark|light>.png                    Background's picker open under its button, 400 DIP wide
//   appearance613-preview-<dark|light>.png                   landed on the preview: the bar, the folder open, the badge
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
    /// <summary>
    /// §613: a test wallpaper, written once to the temp folder as a 24-bit BMP, 1920 × 1080: a dusk sky from night blue to
    /// amber, a low sun, three ranges of hills darker as they come nearer — what a desktop looks like, made here.
    /// </summary>
    private static string DuskWallpaper()
    {
        var path = Path.Combine(Path.GetTempPath(), "karpo-613-test-wallpaper.bmp");
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
        static (double R, double G, double B) Mix((double R, double G, double B) a, (double R, double G, double B) b, double t) =>
            (a.R + ((b.R - a.R) * t), a.G + ((b.G - a.G) * t), a.B + ((b.B - a.B) * t));
        (double, double, double) night = (22, 36, 74), amber = (242, 166, 90), rose = (196, 98, 120);
        (double, double, double)[] hills = [(92, 64, 112), (58, 40, 84), (28, 22, 48)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var t = y / (double)height;
                var sky = t < 0.45 ? Mix(night, rose, t / 0.45) : Mix(rose, amber, Math.Min(1, (t - 0.45) / 0.25));
                var sun = Math.Sqrt(Math.Pow(x - 1320, 2) + Math.Pow(y - 640, 2));
                var pixel = sun < 70 ? (255, 226, 170) : sun < 160 ? Mix((255, 214, 150), sky, (sun - 70) / 90) : sky;
                for (var at = 0; at < hills.Length; at++)
                {
                    var crest = (height * (0.58 + (at * 0.1))) + (Math.Sin((x / (260.0 - (at * 50))) + (at * 1.7)) * (60 - (at * 12)));
                    if (y >= crest)
                    {
                        pixel = Mix(hills[at], (0, 0, 0), Math.Min(0.4, (y - crest) / 900));
                    }
                }
                var offset = 54 + (y * stride) + (x * 3);
                (bytes[offset], bytes[offset + 1], bytes[offset + 2]) = ((byte)pixel.Item3, (byte)pixel.Item2, (byte)pixel.Item1);
            }
        }
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>§613: Graphite worn, its cards' background changed in both modes: a reset beside it in either.</summary>
    private static Config ModedAppearance(TaskbarMode mode)
    {
        var config = MenuCorpus.Fresh();
        Assert.True(LookPalettes.Wear(config, LookPalettes.Of(LookPalettes.GraphiteId)!));
        var widgets = Looks.Section(config, LookPart.Widgets);
        Assert.True(Looks.WritePaint(widgets, Tone.Background, dark: true, Karpo.Core.Paint.Parse("#1E3A5F")));
        Assert.True(Looks.WritePaint(widgets, Tone.Background, dark: false, Karpo.Core.Paint.Parse("#F1EBDF")));
        _ = Looks.WriteMode(config, mode);
        return config;
    }

    [UiFact]
    public void AppearanceModeSheets()
    {
        var wallpaper = DuskWallpaper();
        var source = new WallpaperSource(wallpaper, File.GetLastWriteTimeUtc(wallpaper).Ticks, WallpaperFit.Fill, new Argb(255, 0, 0, 0));
        WindowShots.OnHiddenDesktop("appearance-613", (gfx, instance) =>
        {
            const uint dpi = 144;
            const float scale = dpi / 96f;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var windows = dark ? "dark" : "light";
                var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
                PanelPageView Shown(Config config) =>
                    PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), TheDock.LooksItems(config, bar, dark, [], LooksScope.Appearance));
                string WidgetsTab(PanelPageView page) => page.Rows.Single(row => row.Kind == PanelRowKind.Tabs).Rows[1].Key;
                PanelRow Background(PanelPageView page) => PanelRows.All(page.Rows).First(row => row.Text == S.SettingsAppearanceCustomBackgroundHeader && row.Key.StartsWith(WidgetsTab(page) + "/", StringComparison.Ordinal));
                void Open(EditDeck deck, PanelPageView page, string? land)
                {
                    deck.Wallpaper = () => source;
                    deck.Memory = new PanelMemory();
                    deck.Memory.Show(PanelPage.Appearance);
                    deck.Memory.Open([WidgetsTab(page)]);
                    deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), new SettingsContent([page]));
                    deck.Land(PanelPage.Appearance, land, light: false);
                }

                // The whole page in each Mode: the switcher's choice, the preview's mode, the values of that mode.
                foreach (var mode in LookRows.Modes)
                {
                    var config = ModedAppearance(mode);
                    var page = Shown(config);
                    var file = $"appearance613-page-{mode.ToString().ToLowerInvariant()}-{windows}.png";
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), file, dips: new Vector2(1920f, 2600f), panel: deck =>
                    {
                        Open(deck, page, null);
                        var switcher = page.Rows[1];
                        // §672: Mode's picker inline, the mode chosen what its head says.
                        Assert.Equal((PanelRowKind.Picker, LookRows.ModeName(mode)), (switcher.Kind, switcher.Value));
                        var shownDark = Looks.Dark(config.Appearance, dark);
                        Assert.Equal(shownDark, Assert.Single(deck.Previews!.Modes).Value);
                        Assert.True(Assert.Single(deck.Previews.OnWallpaper).Value, $"{file}: the preview is not on the desktop's picture");
                        var line = deck.Layout!.LineOf(Background(page).Key)!;
                        // §670: its reset, then its picker's head (inline: no popup).
                        Assert.Equal([PanelHitKind.Act, PanelHitKind.Head], deck.Layout.Controls(line).Select(each => each.Hit.Kind));
                    });
                }

                // A row with its reset, landed on.
                {
                    var page = Shown(ModedAppearance(TaskbarMode.System));
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"appearance613-reset-{windows}.png", dips: new Vector2(1920f, 1080f), panel: deck =>
                    {
                        Open(deck, page, Background(page).Key);
                        var line = deck.Layout!.LineOf(Background(page).Key)!;
                        Assert.True(line.Row.Pair is { Item.Subtle: true });
                    });
                }

                // §670: the colour's picker open inline, in its card.
                {
                    var page = Shown(ModedAppearance(TaskbarMode.System));
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"appearance613-picker-{windows}.png", dips: new Vector2(1920f, 1080f),
                        panel: deck =>
                        {
                            deck.Wallpaper = () => source;
                            deck.Memory = new PanelMemory();
                            deck.Memory.Show(PanelPage.Appearance);
                            deck.Memory.Open([WidgetsTab(page), Background(page).Key]);
                            deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), new SettingsContent([page]));
                            deck.Land(PanelPage.Appearance, Background(page).Key, light: false);
                            Assert.NotNull(deck.Layout!.LineOf(Background(page).Key + "/" + PanelPickers.BodyKey));
                        });
                }

                // The preview, landed on: the desktop under it, the bar's widgets, the folder open in its middle, the badge.
                {
                    var page = Shown(ModedAppearance(TaskbarMode.System));
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"appearance613-preview-{windows}.png", dips: new Vector2(1920f, 1080f), panel: deck =>
                    {
                        Open(deck, page, page.Rows.Single(row => row.Kind == PanelRowKind.Preview).Key);
                        Assert.Equal(dark, Assert.Single(deck.Previews!.Modes).Value);
                        Assert.True(Assert.Single(deck.Previews.OnWallpaper).Value);
                        var (room, lines) = Assert.Single(deck.Previews.Rooms).Value;
                        Assert.True(lines is { } folder && Math.Abs(folder.X + (folder.Width / 2) - (room.X / 2)) <= 1, $"the folder {lines} is not centred in {room}");
                    });
                }
            }
        });
    }
}
