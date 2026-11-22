// The bar's own menu (Karpo.Dock\Dock.BarMenu.cs), by default (BarMenus.Default) and §532 the person's own, on
// real menus on a desktop of their own, captured as the compositor draws them (Edit\WindowShots) to KARPO_PNG_DIR as
// bar-menu-<menu>-<look>-<scale>.png: §581 Windows 11's, with one layout (it, ticked, §561; then behind a rule Task
// Manager, behind another §663 Lock the taskbar, ticked, and Taskbar settings), with two (the layouts, then the same),
// and own (Taskbar settings, a heading over Windows' tools and a shortcut, the layouts, Quit Karpo, Task Manager).
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class BarMenuPictures
{
    [UiFact]
    public void TheMenuSheets()
    {
        FlyoutItem[] layouts =
        [
            new(5600, "Work", new FlyoutIcon.Glyph(""), Checked: true),
            new(5601, "Play", new FlyoutIcon.Glyph(""), Checked: false),
        ];
        WindowShots.OnHiddenDesktop("bar-menu", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var menus = new (string Name, List<FlyoutItem> Rows)[]
                    {
                        ("one-layout", TheDock.BarRows(BarMenus.Default(), layouts[..1], windowsTerminal: true, ShortcutLooks.None).Items),
                        ("layouts", TheDock.BarRows(BarMenus.Default(), layouts, windowsTerminal: true, ShortcutLooks.None).Items),
                        // §532: the person's own, as the settings panel's pictures change it (SceneTreeTests.PersonsBarMenu).
                        ("own", TheDock.BarRows(Own().BarMenuEntries, layouts, windowsTerminal: true, ShortcutLooks.None).Items),
                    };
                    foreach (var (name, rows) in menus)
                    {
                        Assert.False(rows[0].Separator);
                        Assert.False(rows[^1].Separator);
                        using var menu = new Flyout(gfx, instance, 0);
                        var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                        menu.Show(gfx, AddListPictures.LookFor(dark), scale, rows, FlyoutLayout.List, new POINT { x = work.right / 2, y = work.bottom }, work, Edge.Bottom, null);
                        WindowShots.Settle();
                        var file = $"bar-menu-{name}-{(dark ? "dark" : "light")}-{(int)(scale * 100)}.png";
                        Assert.True(WindowShots.Save(menu.Hwnd, file), $"{file} came back blank");
                        menu.Hide();
                    }
                }
            }
        });

        static Config Own()
        {
            var config = new Config();
            SceneTreeTests.PersonsBarMenu(config);
            return config;
        }
    }
}
