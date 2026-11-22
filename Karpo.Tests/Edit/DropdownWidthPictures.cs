// §587 (the person, 2026-11-09: "the dropdowns sometimes change their width with their contents and then never change back.
// the dropdowns generally should have a fixed width"): a colour filters widget's Filter dropdown on its page of the settings'
// flyout, set to its longest choice — its box cut short — closed and open, its list its box's width, the long choice cut; then
// set to a short one and opened again: the same width. Hung from its box as the dock hangs it (Dock.PanelPopup). To
// KARPO_PNG_DIR as dropdown-<long-closed|long-open|short-open>-<dark|light>.png. Needs Direct2D and a window station:
// KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void DropdownWidthSheets()
    {
        WindowShots.OnHiddenDesktop("dropdown-width-sheets", (gfx, instance) =>
        {
            const uint dpi = 144;
            const float scale = dpi / 96f;
            foreach (var dark in new[] { true, false })
            {
                var tone = dark ? "dark" : "light";
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var config = MenuCorpus.Fresh();
                var place = new OptionPlace(config, Dark: dark);
                var widget = ListKind.ColorFilters.New();
                var glyph = widget.Info?.Glyph ?? "";
                void SetFilter(string choice)
                {
                    var filter = OptionRows.Of(widget, MenuCorpus.Sources, place).Single(row => row.Title == S.SettingsWidgetColorFilterHeader);
                    _ = filter.Set!(filter.Choices.ToList().IndexOf(choice));
                }
                foreach (var (scene, choice, open) in new[]
                {
                    ("long-closed", S.SettingsWidgetColorFilterDeuteranopia, false),
                    ("long-open", S.SettingsWidgetColorFilterDeuteranopia, true),
                    ("short-open", S.SettingsWidgetColorFilterInverted, true),
                })
                {
                    SetFilter(choice);
                    var page = PanelGroups.Page(PanelPage.Widget, IslandEdits.Kind(widget), glyph, MenuCorpus.Arranged(widget, place)) with
                    {
                        Header = PanelRows.HeaderOf([(glyph, IslandEdits.Kind(widget))], PanelRows.PlaceOf(LayoutType.Taskbar, inFolder: false)),
                    };
                    var content = new SettingsContent([.. SettingsPanelTests.Content(dark: dark).Pages, page]);
                    var row = PanelRows.All(page.Rows).First(each => each.Text == S.SettingsWidgetColorFilterHeader && each.Kind == PanelRowKind.Choice);
                    Assert.Equal(choice, row.Value);
                    int? listWide = null;
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"dropdown-{scene}-{tone}.png", dips: new Vector2(1920f, 1080f),
                        panel: deck =>
                        {
                            deck.Memory = new PanelMemory();
                            deck.Memory.Show(PanelPage.Widget);
                            deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), content);
                            deck.Land(PanelPage.Widget, row.Key, light: false);
                            var line = deck.Layout!.LineOf(row.Key)!;
                            var pop = deck.Layout.Controls(line).Single(each => each.Hit.Kind == PanelHitKind.Pop);
                            // Its box its one width whatever it says; the long choice cut short in it, whole in its tooltip.
                            Assert.Equal(MathF.Round(PanelMetrics.ControlWide * scale), pop.Rect.Width);
                            Assert.Equal(choice == S.SettingsWidgetColorFilterDeuteranopia ? choice : null, deck.Layout.TipOf(pop.Hit));
                        },
                        panelPopup: !open ? null : deck =>
                        {
                            var line = deck.Layout!.LineOf(row.Key)!;
                            var hit = deck.Layout.Controls(line).Single(each => each.Hit.Kind == PanelHitKind.Pop).Hit;
                            var box = deck.OnScreen(hit);
                            var menu = new Flyout(gfx, instance, 0) { Configures = true };
                            var screen = new RECT { left = 0, top = 0, right = (int)(1920 * scale), bottom = (int)(1080 * scale) };
                            menu.Show(gfx, look, scale, PanelRows.PopupOf(line.Row.Item, hit.Id)!, FlyoutLayout.List, new POINT { x = box.left, y = box.bottom }, screen, Edge.Top, control: box, within: deck.FlyoutOnScreen);
                            Assert.True(menu.IsOpen);
                            // The list its box's width, the long choice and the short alike.
                            listWide = menu.PanelOnScreen.right - menu.PanelOnScreen.left;
                            Assert.Equal(box.right - box.left, listWide);
                            return menu;
                        });
                    Assert.True(!open || listWide is not null);
                }
            }
        });
    }
}
