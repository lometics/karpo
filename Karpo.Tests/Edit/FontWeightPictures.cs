// edit, §638 (the person, 2026-11-09: "font weight should be selectable individually but on the same row"; "the about should
// show the name Lometics GmbH as title over the description ... remove the exact location gais."; "the accounts when
// collapsed should show the amount of items totally synced."): the three as Karpo draws them, dark and light, on a display
// 1920 × 1080 DIP at 150 % in edit mode, this PC's font families (SystemFonts). To KARPO_PNG_DIR:
//
//   fontweight-row-<dark|light>.png       Appearance › Taskbar widgets, its Font in Georgia Bold: the reset, the family's
//                                         dropdown, the weight's beside it, the weight's list open under its box
//   fontweight-family-<dark|light>.png    the same, the family's list open: its filter, each family in its face
//   fontweight-about-<dark|light>.png     the Karpo page's About Karpo: Lometics GmbH over its line, its site's button
//   fontweight-accounts-<dark|light>.png  the Accounts page, every account shut: the Microsoft one's items synced at its end
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
    public void FontWeightSheets()
    {
        var was = InstalledFonts.Reader;
        InstalledFonts.Reader = SystemFonts.Read;
        try
        {
            WindowShots.OnHiddenDesktop("fontweight-638", (gfx, instance) =>
            {
                const uint dpi = 144;
                const float scale = dpi / 96f;
                foreach (var dark in new[] { true, false })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var mode = dark ? "dark" : "light";
                    var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };

                    // The Font in Georgia Bold: its reset, its family, its weight; each list open in turn.
                    foreach (var (scene, pair) in new[] { ("row", true), ("family", false) })
                    {
                        var config = MenuCorpus.Fresh();
                        Assert.True(Looks.WriteFont(Looks.Section(config, LookPart.Widgets), GeorgiaBold));
                        var page = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), TheDock.LooksItems(config, bar, dark, [], LooksScope.Appearance));
                        var tab = page.Rows.Single(row => row.Kind == PanelRowKind.Tabs).Rows[1].Key;
                        var font = PanelRows.All(page.Rows).First(row => row.Text == S.SettingsAppearanceStylesFont && row.Key.StartsWith(tab + "/", StringComparison.Ordinal));
                        EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"fontweight-{scene}-{mode}.png", dips: new Vector2(1920f, 1080f),
                            panel: deck =>
                            {
                                deck.Memory = new PanelMemory();
                                deck.Memory.Show(PanelPage.Appearance);
                                deck.Memory.Open([tab]);
                                deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), new SettingsContent([page]));
                                deck.Land(PanelPage.Appearance, font.Key, light: false);
                                var line = deck.Layout!.LineOf(font.Key)!;
                                Assert.Equal([PanelHitKind.Act, PanelHitKind.Pop, PanelHitKind.Pop], deck.Layout.Controls(line).Select(each => each.Hit.Kind));
                            },
                            panelPopup: deck =>
                            {
                                var line = deck.Layout!.LineOf(font.Key)!;
                                var (hit, _) = deck.Layout.Controls(line).Where(each => each.Hit.Kind == PanelHitKind.Pop).ElementAt(pair ? 1 : 0);
                                var row = line.Row.Own(hit.Key);
                                var box = deck.OnScreen(hit);
                                var menu = new Flyout(gfx, instance, 0) { Configures = true, FitsContent = true };
                                var screen = new RECT { left = 0, top = 0, right = (int)(1920 * scale), bottom = (int)(1080 * scale) };
                                menu.Show(gfx, look, scale, row.Item.Children!, FlyoutLayout.List, new POINT { x = box.left, y = box.bottom }, screen, Edge.Top, control: box, within: deck.Within(box));
                                Assert.True(menu.IsOpen);
                                // Georgia's weights, Bold ticked; or every family, Georgia ticked.
                                Assert.Equal(pair ? S.SettingsAppearanceStylesWeightBold : "Georgia", row.Item.Children!.Single(item => item.Checked == true).Text);
                                return menu;
                            });
                    }

                    // About Karpo, landed on Lometics.
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"fontweight-about-{mode}.png", dips: new Vector2(1920f, 1080f), panel: deck =>
                    {
                        var view = PanelRows.Page(PanelPage.Karpo, PanelRows.SideOf(PanelPage.Karpo), PanelRows.GlyphOf(PanelPage.Karpo), TheDock.SettingsItems(MenuCorpus.Fresh(), MenuCorpus.Services(), []));
                        deck.Memory = new PanelMemory();
                        deck.Memory.Show(PanelPage.Karpo);
                        deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), new SettingsContent([view]), PanelPage.Karpo);
                        var lometics = "Karpo/" + S.DockSettingsAbout + "/Lometics GmbH";
                        deck.Land(PanelPage.Karpo, lometics, light: false);
                        Assert.Equal(PanelHitKind.Act, Assert.Single(deck.Layout!.Controls(deck.Layout.LineOf(lometics)!)).Hit.Kind);
                    });

                    // The Accounts page, every account shut.
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"fontweight-accounts-{mode}.png", dips: new Vector2(1920f, 1080f), panel: deck =>
                    {
                        var view = PanelRows.Page(PanelPage.Accounts, S.SettingsNavAccounts, PanelRows.GlyphOf(PanelPage.Accounts), TheDock.SettingsItems(MenuCorpus.Fresh(), MenuCorpus.Services(), [], page: KarpoPage.Accounts));
                        deck.Memory = new PanelMemory();
                        deck.Memory.Show(PanelPage.Accounts);
                        deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), new SettingsContent([view]), PanelPage.Accounts);
                        deck.Land(PanelPage.Accounts, null, light: false);
                        var head = deck.Layout!.Blocks.SelectMany(block => block.Lines).Single(line => line.Row.Text == "me@outlook.com");
                        Assert.Equal(MenuCorpus.MsHolds, head.Row.Value);
                        Assert.False(deck.Layout.StandsOpen(head.Row));
                    });
                }
            });
        }
        finally
        {
            InstalledFonts.Reader = was;
        }
    }
}
