// edit, §634 (the person, 2026-11-09: "the pattern selector should actually not be a dropdown but a grid of the patterns
// where you can select the pattern you want."; "the background while a folder is open preview does not properly show."): the
// Appearance page as Karpo draws it, Windows' mode injected (dark, then light: its real setting never touched), on a display
// 1920 DIP wide at 150 % in edit mode, over the test dusk wallpaper (AppearanceModeSheets'). Taskbar Widgets wear Dots; the
// Flyouts' backdrop is black at half in both modes. To KARPO_PNG_DIR:
//
//   patterngrid-open-<dark|light>.png      Taskbar Widgets' Pattern open: None and the eight patterns, three by three, each
//                                          on the card it would be drawn on, Dots ringed in the accent
//   patterngrid-backdrop-<dark|light>.png  landed on the preview: the desktop darkened by the backdrop under the bar and the
//                                          folder open
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
    public void PatternGridSheets()
    {
        var wallpaper = DuskWallpaper();
        var source = new WallpaperSource(wallpaper, File.GetLastWriteTimeUtc(wallpaper).Ticks, WallpaperFit.Fill, new Argb(255, 0, 0, 0));
        var half = new Argb(0x80, 0, 0, 0);
        WindowShots.OnHiddenDesktop("pattern-grid-634", (gfx, instance) =>
        {
            const uint dpi = 144;
            const float scale = dpi / 96f;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var windows = dark ? "dark" : "light";
                var config = MenuCorpus.Fresh();
                Assert.True(Looks.WritePattern(Looks.Section(config, LookPart.Widgets), Pattern.Dots));
                var flyouts = Looks.Section(config, LookPart.Flyouts);
                Assert.True(Looks.WritePaint(flyouts, Tone.Backdrop, dark: true, new Karpo.Core.Paint(half)));
                Assert.True(Looks.WritePaint(flyouts, Tone.Backdrop, dark: false, new Karpo.Core.Paint(half)));
                var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
                var page = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), TheDock.LooksItems(config, bar, dark, [], LooksScope.Appearance));
                var widgetsTab = page.Rows.Single(row => row.Kind == PanelRowKind.Tabs).Rows[1].Key;
                var pattern = PanelRows.All(page.Rows).First(row => row.Text == S.SettingsAppearanceStylesPattern && row.Key.StartsWith(widgetsTab + "/", StringComparison.Ordinal));
                void Open(EditDeck deck, string? land)
                {
                    deck.Wallpaper = () => source;
                    deck.Memory = new PanelMemory();
                    deck.Memory.Show(PanelPage.Appearance);
                    deck.Memory.Open([widgetsTab]);
                    deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), new SettingsContent([page]));
                    deck.Land(PanelPage.Appearance, land, light: false);
                }

                // The grid open under its box.
                EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"patterngrid-open-{windows}.png", dips: new Vector2(1920f, 1080f),
                    panel: deck => Open(deck, pattern.Key),
                    panelPopup: deck =>
                    {
                        var line = deck.Layout!.LineOf(pattern.Key)!;
                        Assert.Equal(PanelRowKind.Choice, line.Row.Kind);
                        var hit = deck.Layout.Controls(line).Single(each => each.Hit.Kind == PanelHitKind.Pop).Hit;
                        var box = deck.OnScreen(hit);
                        var rows = PanelRows.PopupOf(line.Row.Item, hit.Id) ?? line.Row.Item.Children!;
                        Assert.Equal(9, rows.Count(Flyout.IsTile));
                        var menu = new Flyout(gfx, instance, 0) { Configures = true, FitsContent = true };
                        var screen = new RECT { left = 0, top = 0, right = (int)(1920 * scale), bottom = (int)(1080 * scale) };
                        menu.Show(gfx, look, scale, rows, FlyoutLayout.List, new POINT { x = box.left, y = box.bottom }, screen, Edge.Top, control: box, within: deck.PanelOnScreen);
                        Assert.True(menu.IsOpen);
                        Assert.Equal((int)MathF.Round(PanelMetrics.ControlWide * scale), menu.PanelOnScreen.right - menu.PanelOnScreen.left);
                        Console.WriteLine($"patterngrid {windows}: popup {(menu.PanelOnScreen.right - menu.PanelOnScreen.left) / scale} × {(menu.PanelOnScreen.bottom - menu.PanelOnScreen.top) / scale} DIP");
                        return menu;
                    });

                // The preview with the backdrop: the desktop darkened under the bar and the folder.
                EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"patterngrid-backdrop-{windows}.png", dips: new Vector2(1920f, 1080f), panel: deck =>
                {
                    Open(deck, page.Rows.Single(row => row.Kind == PanelRowKind.Preview).Key);
                    Assert.Equal(half, Assert.Single(deck.Previews!.Backdrops).Value);
                    Assert.True(Assert.Single(deck.Previews.OnWallpaper).Value);
                });
            }
        });
    }
}
