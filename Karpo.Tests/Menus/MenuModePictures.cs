// A widget's right-click menu at rest and while the bar is arranged, on real menus on a desktop of
// their own, captured as the compositor draws them (Edit\WindowShots) to KARPO_PNG_DIR as
// menu-<widget>-<mode>-<way>-<look>-<scale>.png: a folder of three faces on the bar (a clock, the
// weather and the sound, each face's rows under its name), the sound folder's, a pin's jump list, a
// processor line's in a list folder and the bar's own, whether the menu opens up from a bottom bar
// ("up") or down from a top bar ("down"). At rest a widget's menu is its own rows alone, the floor's
// where it has none (Dock.MenuFloor.cs: a heading's Edit folder) — no icon
// buttons and no settings (§253). Arranged it has its icon buttons — "Cut", "Copy", and "Remove",
// together at the row's far end (§350) — as a row on top, ruled off from the rest, then its settings;
// no "Done editing" and no "Add a widget" (the edit bar's, §220). The room round the widgets has no
// menu arranged any more (§214). No menu has moves (2026-10-12).
// The "-keys" pictures have the keys on the first button, and its tooltip beside it as
// menu-tip-<way>-<look>-<scale>.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class MenuModePictures
{
    /// <summary>
    /// Every row of icon buttons a menu has — a widget's arranged (Cut, Copy, Remove; §507 to §513 the painter's two
    /// besides); the bar's own has
    /// none since §366 — says each button's name only when it is lit, in a tooltip as Windows 11's command bar does: none as the menu opens, the name and its shortcut
    /// once the keys rest on it, the next at once along the row; and Narrator calls each a button by
    /// its name.
    /// </summary>
    [UiFact]
    public void EveryIconButtonSaysItsNameWhenLitAndToNarrator()
    {
        WindowShots.OnHiddenDesktop("menu-tips", (gfx, instance) =>
        {
            const float scale = 1.5f;
            var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
            var anchor = new POINT { x = work.right / 2, y = work.bottom };
            foreach (var rows in new[] { TheDock.WidgetRows() })
            {
                var buttons = rows.Where(row => row.Primary).ToList();
                Assert.Equal(["Cut", "Copy", "Remove"], buttons.Select(button => button.Text));
                using var menu = new Flyout(gfx, instance, 0);
                menu.Show(gfx, AddListPictures.LookFor(true), scale, rows, FlyoutLayout.List, anchor, work, Edge.Bottom);
                WindowShots.Settle();
                Assert.Null(menu.TipShown);

                // A button that can do nothing now is drawn greyed and is none of a menu's rows: the keys pass over it and
                // Narrator is not told of it, as of any greyed row (Flyout's rows are its live ones).
                var live = buttons.Where(button => button.Enabled).ToList();
                Assert.Equal(3, live.Count);
                var heard = new List<string?>();
                _ = menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_HOME, 0);
                menu.TipDue();
                heard.Add(menu.TipShown);
                for (var at = 1; at < live.Count; at++)
                {
                    _ = menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_RIGHT, 0);
                    heard.Add(menu.TipShown);
                }
                Assert.Equal(live.Select(Flyout.TipText), heard);
                Assert.All(heard, said => Assert.False(string.IsNullOrWhiteSpace(said)));

                var named = menu.AccessTree.Root.Children.Where(node => node.Role == UiaRole.Button).Select(node => node.Name);
                Assert.Equal(live.Select(button => button.Text), named);
                menu.Hide();
                Assert.Null(menu.TipShown);
            }
        });
    }

    [UiFact]
    public void RestAndArrangedMenuSheets()
    {
        var faces = new BarItem
        {
            Id = "f",
            Kind = BarKind.FolderList,
            Folder = new FolderOptions { Faces = [ClockFace.New(), WeatherFace.New(), FolderFace.Of(FolderFaces.SoundId)] },
        };
        var sound = Karpo.Core.Presets.Folder(Karpo.Core.Presets.Find(Karpo.Core.Presets.SoundId)!, new Karpo.Core.PresetMachine(HasBattery: false, FixedDrives: []));
        var pin = BarKind.Pin.New();
        FlyoutItem[] jumps = [new(0, "Recent", Header: true), new(9000, "notes.txt"), new(9001, "budget.xlsx"), new(0, "Tasks", Header: true), new(9002, "New window")];
        var cpu = ListKind.Cpu.New();
        var heading = ListKind.Heading.New();
        var weather = ListKind.WeatherNow.New();
        var todo = ListKind.Todo.New();
        // Arranged: the row and the widget's settings (OptionMenu.cs), as the dock
        // builds it (Dock.cs, WidgetMenu); at rest its own acts alone (§253).
        List<FlyoutItem> Arranged(WidgetItem island) =>
        [
            .. TheDock.WidgetRowsFor(island), .. OptionMenu.Section(OptionRows.Of(island), []),
            .. island is BarItem { Folder: { } folder } ? OptionMenu.FaceSection(folder, []) : [],
        ];
        List<FlyoutItem> Rest(WidgetItem island) => TheDock.RestMenuRows(island, inFolder: island is CellItem).Items;
        List<FlyoutItem> Line(CellItem line) => Rest(line);
        var menus = new (string Name, string Title, List<FlyoutItem> AtRest, List<FlyoutItem> Arranged)[]
        {
            ("faces", "Folder", Rest(faces), Arranged(faces)),
            ("sound", "Sound", Rest(sound), Arranged(sound)),
            ("pin", "Notepad", TheDock.AppRows(jumps, "Notepad", new FlyoutIcon.Glyph(""), pinned: true, open: 1, TheDock.PinButtons(pin: true, arranged: false), Edge.Bottom), Arranged(pin)),
            ("folder-cpu", "Processor", Line(cpu), Arranged(cpu)),
            // The lines with little of their own: a heading's Edit folder, a to-do list's "Clear
            // completed", the weather's Refresh.
            ("folder-heading", "Heading", Line(heading), Arranged(heading)),
            ("folder-weather", "Weather", Line(weather), Arranged(weather)),
            ("folder-todo", "To do", Line(todo), Arranged(todo)),
        };
        foreach (var (name, _, rest, arranged) in menus)
        {
            string[] buttons = ["Cut", "Copy", "Remove"];
            // Nothing of edit mode's at rest: no row on top, a pin's Windows' own menu for an app (§202).
            Assert.DoesNotContain(rest, row => row.Primary);
            Assert.Equal(buttons, arranged.Where(row => row.Primary).Select(row => row.Text));
            // Never the row alone at rest; arranged, no "Done editing" anywhere.
            Assert.True(rest.Exists(row => !row.Primary && !row.Separator && !row.Header), $"{name} at rest is its row alone");
            Assert.DoesNotContain(arranged, row => row.Text == "Done editing");
        }
        var several = (Rows: TheDock.WidgetRowsFor(cpu), Name: "folder-cpu");

        var own = TheDock.BarRows([]);
        WindowShots.OnHiddenDesktop("menu-modes", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var look = AddListPictures.LookFor(dark);
                    var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                    var suffix = $"{(dark ? "dark" : "light")}-{(int)(scale * 100)}";
                    // A bottom bar's menu opens up from the bar's top; a top bar's down from its bottom.
                    var ways = new (string Way, Edge Edge, POINT Anchor)[]
                    {
                        ("up", Edge.Bottom, new POINT { x = work.right / 2, y = work.bottom }),
                        ("down", Edge.Top, new POINT { x = work.right / 2, y = 0 }),
                    };
                    foreach (var (way, edge, anchor) in ways)
                    {
                        foreach (var (name, title, rest, arranged) in menus)
                        {
                            foreach (var (mode, rows) in new[] { ("rest", rest), ("edit", arranged) })
                            {
                                // As the dock shows a widget's menu: no drawn title, its name spoken only (2026-10-10).
                                using var menu = new Flyout(gfx, instance, 0) { SpokenName = title };
                                menu.Show(gfx, look, scale, rows, FlyoutLayout.List, anchor, work, edge);
                                WindowShots.Settle();
                                var file = $"menu-{name}-{mode}-{way}-{suffix}.png";
                                Assert.True(WindowShots.Save(menu.Hwnd, file), $"{file} came back blank");
                                menu.Hide();
                            }
                        }
                        // One of several selected, arranged: "Select none" under the row.
                        {
                            using var menu = new Flyout(gfx, instance, 0) { SpokenName = "Processor" };
                            menu.Show(gfx, look, scale, several.Rows, FlyoutLayout.List, anchor, work, edge);
                            WindowShots.Settle();
                            Assert.True(WindowShots.Save(menu.Hwnd, $"menu-{several.Name}-edit-several-{way}-{suffix}.png"));
                            menu.Hide();
                        }

                        // The bar's own menu: its two buttons on top, alone in the row.
                        {
                            using var menu = new Flyout(gfx, instance, 0) { SpokenName = "Taskbar" };
                            menu.Show(gfx, look, scale, own, FlyoutLayout.List, anchor, work, edge);
                            WindowShots.Settle();
                            Assert.True(WindowShots.Save(menu.Hwnd, $"menu-bar-rest-{way}-{suffix}.png"));
                            menu.Hide();
                        }
                        // The keys on the first button (Home), in edit mode, where the row is: lit, with its tooltip.
                        {
                            using var menu = new Flyout(gfx, instance, 0) { SpokenName = "Sound" };
                            menu.Show(gfx, look, scale, menus[1].Arranged, FlyoutLayout.List, anchor, work, edge);
                            _ = menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_HOME, 0);
                            menu.TipDue();
                            Assert.Equal("Cut (Ctrl+X)", menu.TipShown);
                            WindowShots.Settle();
                            Assert.True(WindowShots.Save(menu.Hwnd, $"menu-sound-edit-{way}-keys-{suffix}.png"));
                            Assert.True(WindowShots.Save(menu.TipHwnd, $"menu-tip-{way}-{suffix}.png"));
                            menu.Hide();
                        }
                    }
                }
            }
        });
    }
}