// edit: what a click does while the bar is arranged (the person, 2026-10-19; docs\widgets.md §212),
// in pictures of a display at 150 %, dark and light, to KARPO_PNG_DIR:
//
//  - editmenu-<edge>-<look>-150.png: a Taskbar along each edge in edit mode, the clock folder
//    right-clicked (§253), its menu beside it — over it on a bottom bar, under it on a top bar, right
//    of it on a bar down the left, left of it on one down the right: the four sides — never over the
//    bar or the widget;
//  - editmode-folder-<edge>-<look>-150.png: the clock folder open arranged, every bar locked and
//    shown as it is, not dimmed, in front of the folder's window (§279), the folder's face drawn as
//    itself, selected (§267); no close button (§287); nothing at the top of the display; each
//    column's "+" and width button over it, and the guide lines down the screen to the bars' edges,
//    none on a bar (§246, §287);
//  - editmode-folder-menu-<look>-150.png: and a line clicked in it, its menu beside the line.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void EditMenuBesideTheWidgetSheets()
    {
        WindowShots.OnHiddenDesktop("edit-menu-beside", (gfx, instance) =>
        {
            const uint dpi = 144;
            var config = new Config();
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var name = dark ? "dark" : "light";
                foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
                {
                    var layout = PictureLayout(LayoutType.Taskbar, edge);
                    var side = edge.ToString().ToLowerInvariant();
                    EditedDisplay(gfx, instance, dpi, config, look, layout, $"editmenu-{side}-{name}-150.png", besideMenu: true);
                    if (edge is Edge.Bottom or Edge.Top)
                    {
                        EditedDisplay(gfx, instance, dpi, config, look, layout, $"editmode-folder-{side}-{name}-150.png", folder: true);
                    }
                }
                EditedDisplay(gfx, instance, dpi, config, look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"editmode-folder-menu-{name}-150.png", folder: true, lineMenu: true);
            }
        });
    }

    /// <summary>
    /// §481: the pictures of a folder's two menus to KARPO_PNG_DIR, in English and German, dark and light, at 100 and 150 %:
    /// editfolder-face-menu-*.png, a clock folder clicked in edit mode, its face's menu beside it ("Open" first in its top row);
    /// toprow-&lt;widget&gt;-icons-*.png, a few menus' top rows, icons alone.
    /// </summary>
    [UiFact]
    public void FolderMenusSheets()
    {
        WindowShots.OnHiddenDesktop("edit-folder-menus", (gfx, instance) =>
        {
            foreach (var language in new[] { "en", "de" })
            {
                using var speaking = Localization.InLanguage.Use(language);
                foreach (var dark in new[] { true, false })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var name = $"{language}-{(dark ? "dark" : "light")}";
                    foreach (var dpi in new uint[] { 96, 144 })
                    {
                        var pct = dpi * 100 / 96;
                        var layout = PictureLayout(LayoutType.Taskbar, Edge.Bottom);
                        EditedDisplay(gfx, instance, dpi, new Config(), look, layout, $"editfolder-face-menu-{name}-{pct}.png", besideMenu: true);
                        var scale = dpi / 96f;
                        var place = new OptionPlace(new Config(), Dark: dark);
                        var night = BarKind.NightLight.New();
                        var pin = BarKind.Pin.New();
                        pin.Pin!.Target = @"C:\Windows\notepad.exe";
                        var menus = new (string Widget, string Type, List<FlyoutItem> Rows)[]
                        {
                            ("folder", S.CoreWidgetFolderListTitle, FolderMenuRows(FolderSide.Face, place)),
                            ("nightlight", night.Kind.Title(), [.. TheDock.WidgetRowsFor(night), .. OptionMenu.Settings(night, [], new OptionDevices(), place, FolderSide.Face, null)]),
                            ("pin", pin.Kind.Title(), [.. TheDock.WidgetRowsFor(pin), .. OptionMenu.Settings(pin, [], new OptionDevices(), place, FolderSide.Face, null)]),
                        };
                        foreach (var (widget, type, rows) in menus)
                        {
                            using var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true, TypeTitle = type };
                            var work = new RECT { right = (int)(1280 * scale), bottom = (int)(720 * scale) };
                            menu.Show(gfx, look, scale, rows, FlyoutLayout.List, new POINT { x = work.right / 2, y = work.bottom }, work, Edge.Bottom);
                            WindowShots.Settle();
                            var file = $"toprow-{widget}-icons-{name}-{pct}.png";
                            Assert.True(WindowShots.Save(menu.Hwnd, file), $"{file} came back blank");
                            menu.Hide();
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// The menu's rows for a folder clicked while the bar is arranged (Dock.WidgetMenu): §481 its face's — Cut, Copy and
    /// Remove, its faces, its Face style — or its panel's, Folder ▾'s.
    /// </summary>
    private static List<FlyoutItem> FolderMenuRows(FolderSide side = FolderSide.Face, OptionPlace? place = null)
    {
        var folder = ClockFolder();
        return [.. TheDock.WidgetRowsFor(folder), .. OptionMenu.Settings(folder, [], new OptionDevices(), place ?? new OptionPlace(new Config(), Dark: true), side, null)];
    }

    /// <summary>
    /// The clock folder's block of the bar shown all the time clicked while it is arranged: its menu
    /// beside the block, as the dock places it (Dock.BesideMenu, MenuSides): away from the bar's edge.
    /// Nothing of it over the block. The menu, which the caller disposes.
    /// </summary>
    private static Flyout MenuBesideABlock(Gfx gfx, HINSTANCE instance, Look look, float scale, List<(Layout Layout, bool Standing)> surfaces, RectI bounds, string file)
    {
        var (from, _) = surfaces.Where(entry => entry.Standing).First();
        var part = Math.Max(0, from.Specs.ToList().FindIndex(spec => spec.Kind == BarKind.FolderList));
        var r = from.BlockRect(part);
        var block = new RECT
        {
            left = from.X + (int)MathF.Floor(r.X),
            top = from.Y + (int)MathF.Floor(r.Y),
            right = from.X + (int)MathF.Ceiling(r.X + r.Width),
            bottom = from.Y + (int)MathF.Ceiling(r.Y + r.Height),
        };
        var screen = new RECT { left = bounds.Left, top = bounds.Top, right = bounds.Right, bottom = bounds.Bottom };
        var work = TheDock.PopupRoom(screen, surfaces.Where(entry => entry.Standing).Select(entry => (entry.Layout.Edge, Strip(entry.Layout, bounds))));
        // §481: its face's menu, a configuration menu as the dock shows it.
        var menu = new Flyout(gfx, instance, 0)
        {
            Configures = true,
            Configuration = true,
            TypeTitle = S.CoreWidgetFolderListTitle,
            BesideWidget = new MenuBeside(block, MenuSides.Order(onBar: true, from.Edge, mirrored: false)),
        };
        menu.Show(gfx, look, scale, FolderMenuRows(FolderSide.Face, new OptionPlace(new Config(), Dark: look.Dark)), FlyoutLayout.List, new POINT { x = (block.left + block.right) / 2, y = block.top }, work, from.Edge);
        Assert.True(menu.IsOpen);
        var window = EditDeckClickTests.WindowOf(menu.Hwnd);
        var beside = from.Edge switch
        {
            Edge.Top => window.top >= block.bottom,
            Edge.Left => window.left >= block.right,
            Edge.Right => window.right <= block.left,
            _ => window.bottom <= block.top,
        };
        Assert.True(beside, $"{file}: the menu's window {window.left},{window.top}..{window.right},{window.bottom} is not beside the block {block.left},{block.top}..{block.right},{block.bottom} away from the {from.Edge} edge");
        // The panel the person sees: the window's shadow room round it may hang past the display's edge, as the
        // panel is placed by itself (FlyoutHost.Arrange, §303) — on a side bar a block near the screen's foot puts it there.
        var panel = menu.PanelOnScreen;
        Assert.True(panel.left >= bounds.Left && panel.top >= bounds.Top && panel.right <= bounds.Right && panel.bottom <= bounds.Bottom, $"{file}: the menu {panel.left},{panel.top}..{panel.right},{panel.bottom} runs off the display {bounds.Left},{bounds.Top}..{bounds.Right},{bounds.Bottom}");
        return menu;
    }

    /// <summary>
    /// The first line of <paramref name="folder"/>, open arranged, clicked: its menu beside the line,
    /// after it where there is the room (Dock.LineMenu, MenuSides). The menu, which the caller disposes.
    /// </summary>
    private static Flyout MenuBesideALine(Gfx gfx, HINSTANCE instance, Look look, float scale, FolderFlyout folder, List<(Layout Layout, bool Standing)> surfaces, RectI bounds, string file)
    {
        var line = folder.ScreenRectOfCard(0);
        Assert.True(line.right > line.left, $"{file}: the folder has no first line");
        var screen = new RECT { left = bounds.Left, top = bounds.Top, right = bounds.Right, bottom = bounds.Bottom };
        var work = TheDock.PopupRoom(screen, surfaces.Where(entry => entry.Standing).Select(entry => (entry.Layout.Edge, Strip(entry.Layout, bounds))));
        var rows = TheDock.WidgetRowsFor(new BarItem { Id = "c", Kind = BarKind.NightLight });
        var menu = new Flyout(gfx, instance, 0) { BesideWidget = new MenuBeside(line, MenuSides.Order(onBar: false, Edge.Bottom, mirrored: false)) };
        menu.Show(gfx, look, scale, rows, FlyoutLayout.List, new POINT { x = (line.left + line.right) / 2, y = (line.top + line.bottom) / 2 }, work, Edge.Bottom);
        var window = EditDeckClickTests.WindowOf(menu.Hwnd);
        Assert.True(window.left >= line.right || window.right <= line.left, $"{file}: the line's menu {window.left}..{window.right} stands over the line {line.left}..{line.right}");
        return menu;
    }
}
