// §633 (the person, 2026-11-09: "make the style of the ones that open on the topbar settings pages more similar to the
// settings pages themselve"): a search's popup of the settings' flyout open over its page, hung from its box as the dock
// hangs it (Dock.PanelPopup) — Karpo › General › Language, its field over every language, the one in force marked. With
// DropdownIconSheets (a dropdown), FootRightSheets (an Add's list) and TheFontListPictures (the font list) the settings
// popups' sheets; BarMenuPictures and ExplorerMenuPictures the right-click menus', which keep their look. To KARPO_PNG_DIR
// as panel-search-language-<dark|light>.png. Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void SettingsSearchPopupSheets()
    {
        WindowShots.OnHiddenDesktop("settings-search-popup", (gfx, instance) =>
        {
            const uint dpi = 144;
            const float scale = dpi / 96f;
            foreach (var dark in new[] { true, false })
            {
                var tone = dark ? "dark" : "light";
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var content = SettingsPanelTests.Content(dark: dark);
                var row = PanelRows.All(content.Of(PanelPage.Karpo)!.Rows).First(each => each.Text == S.SettingsLanguageSection);
                EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"panel-search-language-{tone}.png", dips: new Vector2(1920f, 1080f),
                    panel: deck =>
                    {
                        deck.Memory = new PanelMemory();
                        deck.Memory.Show(PanelPage.Karpo);
                        deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), content);
                        deck.Land(PanelPage.Karpo, row.Key, light: false);
                    },
                    panelPopup: deck =>
                    {
                        var line = deck.Layout!.LineOf(row.Key)!;
                        var hit = deck.Layout.Controls(line).Single(each => each.Hit.Kind == PanelHitKind.Pop).Hit;
                        var box = deck.OnScreen(hit);
                        var menu = new Flyout(gfx, instance, 0) { Configures = true };
                        var screen = new RECT { left = 0, top = 0, right = (int)(1920 * scale), bottom = (int)(1080 * scale) };
                        var rows = PanelRows.PopupOf(line.Row.Item, hit.Id)!;
                        Assert.Contains(rows, item => item.Control is FlyoutField { Finder: not null });
                        menu.Show(gfx, look, scale, rows, FlyoutLayout.List, new POINT { x = box.left, y = box.bottom }, screen, Edge.Top, control: box, within: deck.FlyoutOnScreen);
                        Assert.True(menu.IsOpen);
                        return menu;
                    });
            }
        });
    }
}
