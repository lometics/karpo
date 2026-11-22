// edit, §579, §585, §595: pages of the settings as Karpo draws them, dark and light (docs\widgets.md §579, §585, §595), on a display
// 1920 × 1080 DIP at 150 % in edit mode, the flyout of the page hung from its entry. To KARPO_PNG_DIR as
// pages-<scene>-<dark|light>.png:
//
//  - about: the Karpo page's foot, its About Karpo section: Karpo's logo and its version; Lometics' mark and its line (§630: no Open-Meteo);
//  - displays: §585 the Layouts page of a PC with two displays, in Windows' order (Two, the primary, first), the
//    one it was opened from (One) marked "This display"; under Layouts the list, §595 Work the default layout, its badge
//    "Default" after its name, the one this display shows "This display";
//  - display-open: the same, Two's expander open: its Layout and its Position;
//  - layout-open: §595 the same, Work's expander open: its Name, its Icon, its Default layout switch, on;
//  - taskbar-menu: §585 the Taskbar page at Behavior's last row, the expander Taskbar menu open: its preview, its entries,
//    Add an entry, Reset to default.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void PagesSheets()
    {
        WindowShots.OnHiddenDesktop("pages", (gfx, instance) =>
        {
            const uint dpi = 144;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                foreach (var scene in new[] { "about", "displays", "display-open", "layout-open", "taskbar-menu" })
                {
                    var file = $"pages-{scene}-{(dark ? "dark" : "light")}.png";
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), file, dips: new Vector2(1920f, 1080f), panel: deck =>
                    {
                        var config = MenuCorpus.Fresh();
                        var bar = MenuCorpus.TwoDisplays(config);
                        // Work the default layout, apart from the one this display shows: each marked, §595 Work by its badge.
                        Assert.True(BarLooks.WriteDefault(config, config.Layouts.Single(each => each.Name == "Work").Id));
                        var layouts = PanelRows.Page(PanelPage.Layouts, PanelRows.SideOf(PanelPage.Layouts), PanelRows.GlyphOf(PanelPage.Layouts), TheDock.LooksItems(config, bar, dark, [], LooksScope.Layouts));
                        var menu = "Taskbar/" + S.DockTaskbarBehaviour + "/" + S.SettingsBarMenuHeader;
                        var (page, view, open, land) = scene switch
                        {
                            "about" => (PanelPage.Karpo, PanelRows.Page(PanelPage.Karpo, PanelRows.SideOf(PanelPage.Karpo), PanelRows.GlyphOf(PanelPage.Karpo), TheDock.SettingsItems(config, MenuCorpus.Services(), [])),
                                "", "Karpo/" + S.DockSettingsAbout + "/Lometics GmbH"),
                            "displays" => (PanelPage.Layouts, layouts, "", ""),
                            "display-open" => (PanelPage.Layouts, layouts, "Layouts/" + S.SettingsSectionsDisplays + "/Two", ""),
                            "layout-open" => (PanelPage.Layouts, layouts, "Layouts/" + S.CoreBarMenuLayouts + "/Work", "Layouts/" + S.CoreBarMenuLayouts + "/Work/" + S.SettingsLayoutsDefaultSet),
                            _ => (PanelPage.Taskbar, PanelRows.Page(PanelPage.Taskbar, PanelRows.SideOf(PanelPage.Taskbar), PanelRows.GlyphOf(PanelPage.Taskbar), TheDock.LooksItems(config, bar, dark, [], LooksScope.Taskbar, services: MenuCorpus.Services())),
                                menu, menu),
                        };
                        deck.Memory = new PanelMemory();
                        deck.Memory.Show(page);
                        if (open.Length > 0)
                        {
                            deck.Memory.Open([open]);
                        }
                        deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), new SettingsContent([view]), page);
                        Assert.True(deck.FlyoutOpen);
                        // The About section's foot in sight, low on the Karpo page; the taskbar menu's expander at the top.
                        deck.Land(page, land.Length > 0 ? land : null, light: false);
                    });
                }
            }
        });
    }
}
