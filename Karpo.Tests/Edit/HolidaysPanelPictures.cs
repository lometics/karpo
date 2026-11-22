// §554: the public holidays on the settings panel's Karpo page as Karpo draws them (SettingsPanelPictures' display and
// edit bar): two calendars, Switzerland with its region Zürich and France, which has none. §609 Switzerland's region
// dropdown in its row before its colour button and Remove, France's buttons where Switzerland's stand: Public holidays open
// (panel-holidays-<dark|light>.png), then the region's dropdown open over the panel, hung from its box as the dock hangs it
// (Dock.PanelPopup; panel-holidays-region-<dark|light>.png). To KARPO_PNG_DIR. Needs Direct2D and a window station:
// KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void HolidaysPanelSheets()
    {
        var holidays = "Karpo/" + S.SettingsSectionsServices + "/" + S.CoreHolidaysName;
        var swiss = holidays + "/" + HolidayPlaces.Name("CH");
        var region = swiss + "/" + S.SettingsHolidaysRegionHeader;
        static void Two(Config config) => config.Holidays = new HolidayOptions
        {
            Calendars = [new HolidayCountry { Country = "CH", Region = "CH-ZH" }, new HolidayCountry { Country = "FR", Colour = "#0078D4" }],
        };
        WindowShots.OnHiddenDesktop("holidays-panel", (gfx, instance) =>
        {
            const uint dpi = 144;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var content = SettingsPanelTests.Content(dark: dark, edit: Two);
                var mode = dark ? "dark" : "light";
                void Panel(EditDeck deck)
                {
                    deck.Memory = new PanelMemory();
                    deck.Memory.Show(PanelPage.Karpo);
                    deck.Memory.Open([holidays]);
                    deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), content);
                    deck.Land(PanelPage.Karpo, swiss, light: false);
                    Assert.Equal(PanelPage.Karpo, deck.Page);
                    // §609: the region in its country's line, before its buttons.
                    Assert.Same(deck.Layout!.LineOf(swiss), deck.Layout.LineOf(region));
                    Assert.Equal((PanelRowKind.Choice, "Zürich"), (deck.Layout.Row(region)!.Kind, deck.Layout.Row(region)!.Value));
                }
                EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"panel-holidays-{mode}.png", dips: new Vector2(1920f, 1080f), panel: Panel);
                EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"panel-holidays-region-{mode}.png", dips: new Vector2(1920f, 1080f),
                    panel: Panel,
                    panelPopup: deck =>
                    {
                        var box = deck.OnScreen(deck.Layout!.Controls(deck.Layout.LineOf(region)!).Single(each => each.Hit.Key == region).Hit);
                        var menu = new Flyout(gfx, instance, 0);
                        var anchor = new POINT { x = (box.left + box.right) / 2, y = box.bottom };
                        var screen = new RECT { left = 0, top = 0, right = (int)(1920 * 1.5f), bottom = (int)(1080 * 1.5f) };
                        menu.Show(gfx, look, dpi / 96f, deck.Layout.Row(region)!.Item.Children!, FlyoutLayout.List, anchor, screen, Edge.Top, control: box, within: deck.PanelOnScreen);
                        Assert.True(menu.IsOpen);
                        return menu;
                    });
            }
        });
    }
}
