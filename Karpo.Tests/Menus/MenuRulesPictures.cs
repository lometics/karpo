// menus: the menu rules' pictures (§411): five menus as the dock shows them — Layout ▾ › Appearance, a folder line's
// menu, ⚙ › Accounts, a place's search before anything is typed, Display ▾ — dark, at 150 %, each a configuration
// menu of one height; §423 the root menus' feet — Layout ▾, the folder room's right click, a separator's menu — and
// ⚙ › Public holidays with a calendar's colour's swatches opened. To KARPO_PNG_DIR as menurules-<what>.png. Needs
// Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class MenuRulesPictures
{
    private const float Scale = 1.5f;

    private static readonly RECT Work = new() { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };

    private static readonly POINT Anchor = new() { x = Work.right / 3, y = Work.bottom };

    [UiFact]
    public void TheMenuRulesSheets()
    {
        WindowShots.OnHiddenDesktop("menu-rules", (gfx, instance) =>
        {
            var config = new Config();
            config.EditScreen("display", "One");
            _ = BarLooks.Add(config, "Work");
            var bar = new BarSpot("display", "One") { Attached = ["display"] };
            var place = new OptionPlace(config, Dark: true);

            // §423: and then the palette button of the row <paramref name="tool"/> names on the level reached, its swatches opened in place.
            void Shot(string name, List<FlyoutItem> items, string? type, params Func<FlyoutItem, bool>[] path) => ShotTool(name, items, type, null, path);

            void ShotTool(string name, List<FlyoutItem> items, string? type, Func<FlyoutItem, bool>? tool, params Func<FlyoutItem, bool>[] path)
            {
                using var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true, TypeTitle = type };
                menu.Show(gfx, AddListPictures.LookFor(true), Scale, items, FlyoutLayout.List, Anchor, Work, Edge.Bottom, null);
                WindowShots.Settle();
                IReadOnlyList<FlyoutItem> level = items;
                foreach (var step in path)
                {
                    var row = level.First(item => !item.Separator && step(item));
                    _ = menu.Click(row.Id);
                    WindowShots.Settle();
                    level = row.Children ?? [];
                }
                if (tool is not null)
                {
                    var row = level.First(item => !item.Separator && tool(item));
                    var at = menu.ToolMiddle(row.Id, 0)!.Value;
                    var lParam = (LPARAM)(nint)((at.y << 16) | (at.x & 0xFFFF));
                    _ = menu.Message(WM.WM_MOUSEMOVE, 0, lParam);
                    _ = menu.Message(WM.WM_LBUTTONDOWN, 1, lParam);
                    _ = menu.Message(WM.WM_LBUTTONUP, 0, lParam);
                    WindowShots.Settle();
                    Assert.Equal(path.Length + 1, menu.InlineDepth);
                }
                WindowShots.Settle(300);
                Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], $"menurules-{name}.png", (0, 0, 0)));
                menu.Hide();
            }

            // §517: the edit bar's menus, each under its headings (§583 the Appearance's sections and parts too, no level).
            Shot("look", TheDock.LooksItems(config, bar, dark: true, [], scope: LooksScope.Appearance), BarLooks.Kind(config, bar, LooksScope.Appearance));
            Shot("display", TheDock.LooksItems(config, bar, dark: true, [], scope: LooksScope.Layouts), BarLooks.Kind(config, bar, LooksScope.Layouts));
            Shot("taskbar", TheDock.LooksItems(config, bar, dark: true, [], scope: LooksScope.Taskbar), BarLooks.Kind(config, bar, LooksScope.Taskbar));
            Shot("folder-room", TheDock.LooksItems(config, bar, dark: true, [], scope: LooksScope.Folders), BarLooks.Kind(config, bar, LooksScope.Folders));
            config.Holidays = new HolidayOptions { Calendars = [new HolidayCountry { Country = "CH", Region = "CH-AR", Colour = "#498205" }, new HolidayCountry { Country = "CH", Region = "CH-AI" }] };
            Shot("holidays", TheDock.SettingsItems(config, MenuCorpus.Services(), []), S.DockEditSettings, item => item.Text == S.CoreHolidaysName);
            ShotTool("holidays-colour", TheDock.SettingsItems(config, MenuCorpus.Services(), []), S.DockEditSettings, item => item.Text == HolidayPlaces.Name("CH") && item.Children?.Any(region => region.Children?.Any(choice => choice.Checked == true && choice.Text == HolidayPlaces.RegionName("CH-AI")) == true) == true, item => item.Text == S.CoreHolidaysName);

            var calendar = ListKind.Calendar.New();
            Shot("folder-line", MenuCorpus.Arranged(calendar, place), IslandEdits.Kind(calendar));

            var weather = ListKind.WeatherNow.New();
            Shot("search-before-typing", MenuCorpus.Arranged(weather, place), IslandEdits.Kind(weather), item => item.Text == S.SettingsWidgetWeatherPlaceHeader);

            // §535: the Accounts page's rows.
            Shot("settings-accounts", TheDock.SettingsItems(config, MenuCorpus.Services(), [], page: KarpoPage.Accounts), S.SettingsNavAccounts);
        });
    }
}
