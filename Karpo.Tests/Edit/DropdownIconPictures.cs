// §578 (the person, 2026-11-09: "the online dropdown selectors should all receive icons if possible"): a dropdown of the
// settings' flyout open, hung from its box as the dock hangs it (Dock.PanelPopup), each choice its icon in the list and the
// one in force in the box — Karpo › General › Animations, and Online's map and finance site, each site's own logo (§597).
// On SettingsPanelPictures' display and edit bar. To KARPO_PNG_DIR as panel-dropdown-<animations|map|markets>-<dark|light|contrast>.png.
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void DropdownIconSheets()
    {
        WindowShots.OnHiddenDesktop("dropdown-icons", (gfx, instance) =>
        {
            const uint dpi = 144;
            foreach (var (scene, name) in new[] { ("animations", S.SettingsGeneralAnimationsHeader), ("map", S.SettingsMapProviderHeader), ("markets", S.SettingsLinksMarketsHeader) })
            {
                // §597: and in high contrast, where the sites' logos keep their colours on their own shapes.
                foreach (var (tone, dark, look) in new[]
                {
                    ("dark", true, AddListPictures.LookFor(true) with { Animations = false }),
                    ("light", false, AddListPictures.LookFor(false) with { Animations = false }),
                    ("contrast", true, EditDeckPlanTests.HighContrast),
                })
                {
                    var content = SettingsPanelTests.Content(dark: dark);
                    var page = content.Of(PanelPage.Karpo)!;
                    var row = PanelRows.All(page.Rows).First(each => each.Text == name && each.Kind == PanelRowKind.Choice);
                    var file = $"panel-dropdown-{scene}-{tone}.png";
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), file, dips: new Vector2(1920f, 1080f),
                        panel: deck =>
                        {
                            deck.Memory = new PanelMemory();
                            deck.Memory.Show(PanelPage.Karpo);
                            deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), content);
                            deck.Land(PanelPage.Karpo, row.Key, light: false);
                            Assert.Equal(PanelPage.Karpo, deck.Page);
                            Assert.NotNull(deck.Layout!.LineOf(row.Key)!.Row.ValueIcon);
                        },
                        panelPopup: deck =>
                        {
                            var line = deck.Layout!.LineOf(row.Key)!;
                            var box = deck.OnScreen(deck.Layout.Controls(line).Single(each => each.Hit.Kind == PanelHitKind.Pop).Hit);
                            var menu = new Flyout(gfx, instance, 0);
                            var anchor = new POINT { x = (box.left + box.right) / 2, y = box.bottom };
                            var screen = new RECT { left = 0, top = 0, right = (int)(1920 * 1.5f), bottom = (int)(1080 * 1.5f) };
                            Assert.All(line.Row.Item.Children!.Where(kid => kid.Checked is not null), kid => Assert.NotNull(kid.Icon));
                            menu.Show(gfx, look, dpi / 96f, line.Row.Item.Children!, FlyoutLayout.List, anchor, screen, Edge.Top, control: box, within: deck.FlyoutOnScreen);
                            Assert.True(menu.IsOpen);
                            return menu;
                        });
                }
            }
        });
    }
}
