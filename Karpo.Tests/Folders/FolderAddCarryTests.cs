// edit: a widget picked in the add list of a folder standing open arranged, for no place of it (a
// column's or a slot's "+" knows its place and takes the pick at once, §297), follows the pointer, and
// the Escape after the folder has closed leaves edit mode (the person, 2026-10-19: "when in folder
// edit mode and i add a widget it should not immediately place the widget, it should actually pin it
// to my mouse ... when i am in the folder edit mode and click esc i cannot click esc again to get out
// of edit mode"; docs\widgets.md §230). On real folder windows on a desktop of their own, the dock's
// half done by hand (Folders.AddNew where the folder's edit bar adds, as Dock.AddChosen does, then the
// carry of a pasted line, FollowInFolder):
//
//  - added, the line is carried with no button held, its landing following the pointer, and a click
//    puts it down where that shows; called off, the folder carries nothing and nothing lands;
//    folder-add-carry.png in KARPO_PNG_DIR, dark at 150 %: the new line carried over the empty column;
//  - Escape is a popup's own only while it stands open: a folder closed — its fade, or the menu it
//    was opened from, closed and hidden, that the keys went back to — leaves it to edit mode's hook;
//  - the marching ants take an Escape only where they show.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1 (but the ants).

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.FolderEditClickTests;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderAddCarryTests
{
    private const string Owner = "layout:default";

    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly POINT Anchor = new() { x = 960, y = 1040 };
    private static readonly RECT Work = new() { right = 1920, bottom = 1040 };

    /// <summary>A list folder: the clock's line and the battery's in one list, and an empty column beside it.</summary>
    private static BarItem Folder()
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        foreach (var (line, id) in new[] { (ListKind.Clock.New(), "clock"), (ListKind.Battery.New(), "battery") })
        {
            line.Id = id;
            line.View = Karpo.Core.Folders.View(island.Kind);
            island.Folder!.Put(line);
        }
        island.Folder!.Lists.Add([]);
        return island;
    }

    private static POINT ScreenOfList(FolderFlyout flyout, int list)
    {
        var box = flyout.Plan!.Lists[list].Box;
        var on = flyout.PanelOnScreen;
        return new POINT { x = on.left + (int)MathF.Round((box.left + box.right) / 2), y = on.top + (int)MathF.Round((box.top + box.bottom) / 2) };
    }

    [UiFact]
    public void AWidgetAddedInAnArrangedFolderFollowsThePointerUntilAClickPutsItDown()
    {
        WindowShots.OnHiddenDesktop("folder-add-carry", (gfx, instance) =>
        {
            var item = Folder();
            var config = new Config();
            config.ListFor(Owner).Add(item);
            var folder = new FolderIsland($"{Owner}/box", item, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1.5f, Anchor, Work, Edge.Bottom, folder, editing: true);

            // The pick: in where the folder's edit bar adds, the first list's end, then carried.
            var place = flyout.AddPlace();
            Assert.NotNull(place);
            var path = Karpo.Core.Folders.AddNew(config.ListFor(Owner), item, ListKind.NightLight.New(), place);
            Assert.NotNull(path);
            var key = Config.IslandKey(Owner, path!);
            folder.Configure(item, config);
            var column = ScreenOfList(flyout, 1);
            _ = SetCursorPos(column.x, column.y);
            Assert.True(flyout.Carry(key));
            Assert.True(flyout.Carrying);

            // No button held: the moves aim it, over the empty column.
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_MOUSEMOVE, 0, Client(flyout.Hwnd, column)).Kind);
            Assert.Equal(-1, flyout.Pressed);
            WindowShots.Settle(300);
            Assert.True(WindowShots.Save(flyout.Hwnd, "folder-add-carry.png"));

            // A click puts it down where the landing shows: the empty column's first.
            var landed = flyout.LandCarry(column);
            Assert.Equal(FolderFlyoutAction.Place, landed.Kind);
            Assert.Equal(key, landed.Widget?.Key);
            Assert.Equal(new FolderPlace(1, 0), landed.Place);
            Assert.False(flyout.Carrying);

            // Called off: carried no more, and nothing lands (the dock takes it out again, DropFollow).
            Assert.True(flyout.Carry(key));
            flyout.EndCarry();
            Assert.False(flyout.Carrying);
            Assert.Equal(FolderFlyoutAction.None, flyout.LandCarry(column).Kind);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// The bug: Escape closed the folder, which handed the keys back to the window that had them as it
    /// opened — the menu its "Open folder" was picked in, closed by then — and the hook let the next
    /// Escape go there, to a window of the dock's own, which heard nothing. A popup takes Escape only
    /// while it stands open.
    /// </summary>
    [UiFact]
    public void EscapeIsAPopupsOwnOnlyWhileItStandsOpen()
    {
        WindowShots.OnHiddenDesktop("folder-escape", (gfx, instance) =>
        {
            var item = Folder();
            var config = new Config();
            var folder = new FolderIsland($"{Owner}/box", item, gfx, config);
            var menu = new FolderIsland($"{Owner}/box", Folder(), gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            using var closed = new FolderFlyout(gfx, instance, 0);
            static bool None(HWND _) => false;

            // The menu the folder was opened from: shown, then closed and out of sight.
            closed.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, menu);
            Assert.True(TheDock.EscapeTheirs(closed.Hwnd, None));
            closed.Hide();
            WindowShots.Settle(400);
            Assert.False(TheDock.EscapeTheirs(closed.Hwnd, None));

            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            Assert.True(TheDock.EscapeTheirs(flyout.Hwnd, None));
            // An edit mode's surface never closes on it, open or not.
            Assert.False(TheDock.EscapeTheirs(flyout.Hwnd, hwnd => hwnd == flyout.Hwnd));
            // Escape in the arranged folder, nothing carried: it closes, and the next Escape is edit
            // mode's — through the fade, while the window still shows, too.
            _ = flyout.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
            Assert.False(flyout.IsOpen);
            Assert.False(TheDock.EscapeTheirs(flyout.Hwnd, None));
            Assert.False(TheDock.EscapeTheirs(HWND.NULL, None));
            folder.Dispose();
            menu.Dispose();
        });
    }

    /// <summary>
    /// The marching ants take an Escape of their own only where they show: lines copied in a folder
    /// that has closed show none, so the Escape after the folder closed leaves edit mode at once.
    /// </summary>
    [Fact]
    public void TheAntsTakeAnEscapeOnlyWhereTheyShow()
    {
        string[] bar = [$"{Owner}/a", $"{Owner}/box"];
        string[] line = [$"{Owner}/box/clock"];
        Assert.False(TheDock.AntsInSight([], bar, [$"{Owner}/box"]));
        Assert.True(TheDock.AntsInSight([$"{Owner}/A"], bar, []));
        Assert.True(TheDock.AntsInSight(line, bar, [$"{Owner}/box"]));
        Assert.False(TheDock.AntsInSight(line, bar, []));
        Assert.False(TheDock.AntsInSight(line, bar, [$"{Owner}/other"]));
        // A folder copied on the bar shows its ants on the bar.
        Assert.True(TheDock.AntsInSight([$"{Owner}/box"], bar, []));
        Assert.Equal(EditEscape.Leave, EditKeys.Escape(carrying: false, copied: TheDock.AntsInSight(line, bar, [])));
    }
}
