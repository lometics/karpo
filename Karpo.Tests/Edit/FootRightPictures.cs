// §599 (the person, 2026-11-09: "the button like add layout is currently aligned on the left side in the settings, they should
// be aligned on the right side. the dropdowns from these menus should also have a fixed width like all the others"): rows that
// only act at the end of their lines, each list open under its button, flush with its right, a value's box's 240 DIP — hung
// as the dock hangs it (Dock.PanelPopup: its button the control, the flyout what it stands in). The taskbar menu's Add an
// entry over Reset to default (Taskbar › Behavior); Add a country (the Karpo page's public holidays). (§628: Add a layout
// opens a page, no list: AddLayoutPictures.) To KARPO_PNG_DIR as footright-<barmenu|country>-<dark|light>.png. Needs Direct2D
// and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void FootRightSheets()
    {
        WindowShots.OnHiddenDesktop("foot-right-sheets", (gfx, instance) =>
        {
            const uint dpi = 144;
            const float scale = dpi / 96f;
            foreach (var dark in new[] { true, false })
            {
                var tone = dark ? "dark" : "light";
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var content = SettingsPanelTests.Content(dark: dark);
                foreach (var (scene, page, words, open) in new[]
                {
                    ("barmenu", PanelPage.Taskbar, S.SettingsBarMenuAdd, (string?)SettingsPanelTests.BarMenuKey),
                    ("country", PanelPage.Karpo, S.CoreHolidaysAdd, null),
                })
                {
                    var key = PanelRows.All(content.Of(page)!.Rows).Single(row => row.Foot && row.Text == words).Key;
                    PanelLine Foot(EditDeck deck) => deck.Layout!.LineOf(key) ?? throw new InvalidOperationException($"{key} not laid out");
                    int? listRight = null;
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"footright-{scene}-{tone}.png", dips: new Vector2(1920f, 1080f),
                        panel: deck =>
                        {
                            deck.Memory = new PanelMemory();
                            if (open is not null)
                            {
                                deck.Memory.Toggle(open);
                            }
                            deck.Memory.Show(page);
                            deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), content);
                            deck.Land(page, key, light: false);
                            // At its line's end, where the value controls end.
                            var line = Foot(deck);
                            var button = deck.Layout!.Controls(line).Single().Rect;
                            Assert.Equal(line.Inner ? deck.Layout.ControlEnd(line) : line.Rect.Right, button.Right);
                        },
                        panelPopup: deck =>
                        {
                            var line = Foot(deck);
                            var hit = deck.Layout!.Controls(line).Single(each => each.Hit.Kind == PanelHitKind.Pop).Hit;
                            var box = deck.OnScreen(hit);
                            var menu = new Flyout(gfx, instance, 0) { Configures = true };
                            var screen = new RECT { left = 0, top = 0, right = (int)(1920 * scale), bottom = (int)(1080 * scale) };
                            menu.Show(gfx, look, scale, PanelRows.PopupOf(line.Row.Item, hit.Id)!, FlyoutLayout.List, new POINT { x = box.left, y = box.bottom }, screen, Edge.Top, control: box, within: deck.FlyoutOnScreen);
                            Assert.True(menu.IsOpen);
                            // The list a value's box's width, flush with its button's right.
                            var on = menu.PanelOnScreen;
                            Assert.Equal(((int)MathF.Round(PanelMetrics.ControlWide * scale), box.right), (on.right - on.left, on.right));
                            listRight = on.right;
                            return menu;
                        });
                    Assert.NotNull(listRight);
                }
            }
        });
    }
}
