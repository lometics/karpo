// Karpo's menus drawn as Explorer's context menu (Flyout.cs, Flyout.Primary.cs, ExplorerIcons.cs; the
// person, 2026-10-16: "use the icons that the file explorer uses ... make sure that the design of the
// right click looks exactly the same"), on real menus on a desktop of their own, captured as the
// compositor draws them (Edit\WindowShots) to KARPO_PNG_DIR as explorer-menu-<menu>-<look>-<scale>.png:
// Explorer's own file menu built of Karpo's rows ("file", to lay over a capture of Explorer's), a
// widget's at rest, the bar's own, and a widget's while the bar is arranged.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class ExplorerMenuPictures
{
    /// <summary>Explorer's menu on a text file, as Windows 11 26H2 draws it, in Karpo's rows.</summary>
    internal static List<FlyoutItem> FileMenu() =>
    [
        new(1, "Cut", new FlyoutIcon.Glyph(""), Primary: true),
        new(2, "Copy", new FlyoutIcon.Glyph(""), Primary: true, Keys: MenuChord.Copy),
        new(3, "Rename", new FlyoutIcon.Glyph(""), Primary: true),
        new(4, "Share", new FlyoutIcon.Glyph(""), Primary: true),
        new(5, "Delete", new FlyoutIcon.Glyph(""), Primary: true, Keys: MenuChord.Delete),
        new(6, "Open", new FlyoutIcon.Glyph(""), Accelerator: "Enter"),
        new(7, "Open with", new FlyoutIcon.Glyph(""), Children: [new(71, "Notepad")]),
        new(8, "Share with", new FlyoutIcon.Glyph(""), Children: [new(81, "Specific people")]),
        new(9, "Add to Favourites", new FlyoutIcon.Glyph("")),
        new(10, "Compress to...", new FlyoutIcon.Glyph(""), Children: [new(101, "ZIP File")]),
        new(11, "Copy as path", new FlyoutIcon.Glyph(""), Accelerator: "Ctrl+Shift+C"),
        new(12, "Properties", new FlyoutIcon.Glyph(""), Accelerator: "Alt+Enter"),
        new(0, "", Separator: true),
        new(13, "Edit in Notepad", new FlyoutIcon.Glyph("")),
        new(0, "", Separator: true),
        new(14, "Show more options", new FlyoutIcon.Glyph("")),
    ];

    [UiFact]
    public void ExplorerMenuSheets()
    {
        var sound = Karpo.Core.Presets.Folder(Karpo.Core.Presets.Find(Karpo.Core.Presets.SoundId)!, new Karpo.Core.PresetMachine(HasBattery: false, FixedDrives: []));
        var cpu = ListKind.Cpu.New();
        var menus = new (string Name, List<FlyoutItem> Rows)[]
        {
            ("file", FileMenu()),
            ("sound", TheDock.RestMenuRows(sound, inFolder: false).Items),
            ("bar", TheDock.BarRows(BarMenus.Default(), [new(5600, "Work", new FlyoutIcon.Glyph(""), Checked: true), new(5601, "Play", new FlyoutIcon.Glyph(""), Checked: false)], windowsTerminal: true, ShortcutLooks.None).Items),
            ("arranged", TheDock.WidgetRowsFor(cpu)),
        };
        WindowShots.OnHiddenDesktop("explorer-menu", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    foreach (var (name, rows) in menus)
                    {
                        using var menu = new Flyout(gfx, instance, 0);
                        var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                        // Down from where it was asked for, as Explorer's opens under the pointer.
                        menu.Show(gfx, AddListPictures.LookFor(dark), scale, rows, FlyoutLayout.List, new POINT { x = work.right / 2, y = 0 }, work, Edge.Top, null);
                        WindowShots.Settle();
                        var file = $"explorer-menu-{name}-{(dark ? "dark" : "light")}-{(int)(scale * 100)}.png";
                        Assert.True(WindowShots.Save(menu.Hwnd, file), $"{file} came back blank");
                        menu.Hide();
                    }
                }
            }
        });
    }

    /// <summary>
    /// A pin's menu on the bar and in a folder, at rest and while the bar is arranged, at 150 %
    /// (docs\widgets.md §202), to KARPO_PNG_DIR as pin-menu-&lt;bar|folder&gt;-&lt;rest|arranged&gt;-&lt;look&gt;-150.png.
    /// At rest no row of buttons and no rule stand on top, as Windows' own app menu has; arranged the
    /// row is there.
    /// </summary>
    [UiFact]
    public void PinMenuSheets()
    {
        FlyoutItem[] jumps = [new(0, "Recent", Header: true), new(9000, "notes.txt", new FlyoutIcon.Glyph("")), new(9001, "budget.xlsx", new FlyoutIcon.Glyph("")), new(0, "Tasks", Header: true), new(9002, "New window", new FlyoutIcon.Glyph(""))];
        FlyoutItem[] file = [new(TheDock.CMD_WIDGET_ACT, "Run as administrator", new FlyoutIcon.Glyph("")), new(TheDock.CMD_WIDGET_ACT + 1, "Open file location", new FlyoutIcon.Glyph("")), new(TheDock.CMD_WIDGET_ACT + 2, "Properties", new FlyoutIcon.Glyph(""))];
        var icon = new FlyoutIcon.Glyph("");
        var pin = BarKind.Pin.New();
        var line = ListKind.Pin.New();
        var menus = new (string Name, bool Arranged, List<FlyoutItem> Rows)[]
        {
            ("bar-rest", false, TheDock.AppRows(jumps, "Notepad", icon, pinned: true, open: 2, TheDock.PinButtons(pin: true, arranged: false), Edge.Bottom, endTask: true)),
            ("folder-rest", false, TheDock.AppRows([], "Notepad", icon, pinned: true, open: 0, TheDock.PinButtons(pin: true, arranged: false), Edge.Bottom, file: file)),
            ("bar-arranged", true, TheDock.WidgetRowsFor(pin)),
            ("folder-arranged", true, TheDock.WidgetRowsFor(line)),
        };
        WindowShots.OnHiddenDesktop("pin-menu", (gfx, instance) =>
        {
            const float scale = 1.5f;
            foreach (var dark in new[] { true, false })
            {
                foreach (var (name, arranged, rows) in menus)
                {
                    using var menu = new Flyout(gfx, instance, 0);
                    var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                    menu.Show(gfx, AddListPictures.LookFor(dark), scale, rows, FlyoutLayout.List, new POINT { x = work.right / 2, y = work.bottom }, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    var png = $"pin-menu-{name}-{(dark ? "dark" : "light")}-150.png";
                    Assert.True(WindowShots.Save(menu.Hwnd, png), $"{png} came back blank");
                    // The topmost row a button of the row arranged alone; at rest no rule over the first.
                    var first = menu.Looks.MinBy(look => look.Plate.Y);
                    Assert.Equal(arranged, rows.Any(row => row.Primary && row.Id == first.Id));
                    Assert.False(Flyout.WithPrimaryRow(rows)[0].Separator);
                    menu.Hide();
                }
            }
        });
    }
}
