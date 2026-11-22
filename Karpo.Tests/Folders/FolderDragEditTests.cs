// edit: a widget dragged in a folder standing open at rest turns edit mode on, as a widget dragged
// on the bar does (the person, 2026-10-05: "when a widget is dragged in the grid or list view it
// should also automatically switch into edit mode"; FolderFlyout.HandOver,
// Dock.BeginEditFromFolderDrag). The folder asks the dock at the drag threshold
// (FolderFlyout.ArrangeFromDrag) and, told yes, is arranged where it stands and carries on with the
// same line, by edit mode's rules (the person, 2026-10-08: dragging a widget into edit mode keeps
// the drag; for a day before it the line was let go as edit mode came, while edit mode still shrank
// the screen under the pointer). Grid folders are gone (2026-10-09): a list is the folder.
//
// Held here on real folder windows on a desktop of their own, the dock's half faked by the
// ArrangeFromDrag seam, which counts how often it was asked:
//
//  - a press carried past the threshold on a list's line asks once, leaves the folder open and
//    arranged, and carries that line: let go past another line it goes there, and into another list;
//  - a press and release inside the threshold asks nothing: no edit mode, and no settings page;
//  - after the hand-over, carried off the folder, it is dragged out — the folder says so, and the
//    dock answers: into another open folder, or refused on the bar, since nothing moves between
//    the taskbar and a folder (Partners) — and the folder stays open and arranged;
//  - where the dock says no, the drag is what it was: the widget carried out, the folder at rest.
//
// A slider dragged in a line is the slider's and asks nothing: FolderSliderTests. Needs Direct2D
// and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.FolderEditClickTests;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderDragEditTests
{
    private const string Owner = "layout:default";

    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly Look Light = new(Glass: false, Dark: false, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly POINT Anchor = new() { x = 960, y = 1040 };
    private static readonly RECT Work = new() { right = 1920, bottom = 1040 };

    /// <summary>
    /// A list at rest: a press on the clock's line and a move past the threshold turn edit mode on;
    /// let go on the lower half of the battery's line, the clock goes under it — the reorder an arranged list makes.
    /// </summary>
    [UiFact]
    public void ALineDraggedPastTheThresholdInAListTurnsEditModeOnAndReorders()
    {
        WindowShots.OnHiddenDesktop("folder-drag-edit-list", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var asked = 0;
            flyout.ArrangeFromDrag = () => { asked++; return true; };
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder);
            var press = flyout.ScreenOfCard(0);
            var slop = GetSystemMetrics(SM.SM_CYDRAG);

            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            // Down the list, past the threshold: the line is carried in edit mode.
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y + slop + 2 })).Kind);
            Assert.Equal(1, asked);
            Assert.True(flyout.IsOpen);
            Assert.True(flyout.Arranged);
            Assert.Equal(0, flyout.Pressed);

            // Onto the lower half of the battery's line, where it stands in the arranged list, and
            // let go: the clock goes under it, the list's end.
            var other = flyout.ScreenOfCard(1);
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, other with { y = other.y + 4 }));
            var placed = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, other with { y = other.y + 4 }));
            Assert.Equal(FolderFlyoutAction.Place, placed.Kind);
            Assert.Equal($"{Owner}/box/clock", placed.Widget?.Key);
            Assert.Equal(new FolderPlace(0, 2), placed.Place);
            Assert.True(flyout.IsOpen);
            Assert.True(flyout.Arranged);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// §663 ("when the taskbar is locked dragging a widget should not go into edit mode anymore / should not be possible
    /// anymore"): on a locked bar a line pressed at rest and carried far past the threshold, down the list and off the
    /// folder onto the bar, asks nothing and goes nowhere: no edit mode, nothing dragged out, nothing placed; the folder
    /// at rest and open. Unlocked, the same press is edit mode's at the threshold (above).
    /// </summary>
    [UiFact]
    public void LockedALineDraggedAtRestStaysWhereItIs()
    {
        WindowShots.OnHiddenDesktop("folder-drag-locked", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var asked = 0;
            flyout.ArrangeFromDrag = () => { asked++; return true; };
            flyout.Locked = () => true;
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder);
            var press = flyout.ScreenOfCard(0);
            var slop = GetSystemMetrics(SM.SM_CYDRAG);
            var away = new POINT { x = 100, y = Work.bottom + 20 };

            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            List<FolderFlyoutEvent> said =
            [
                flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y + slop + 2 })),
                flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, flyout.ScreenOfCard(1))),
                flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, away)),
                flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, away)),
            ];

            Assert.Equal(0, asked);
            Assert.False(flyout.Arranged, "a locked bar's line dragged at rest turned edit mode on");
            Assert.All(said, each => Assert.DoesNotContain(each.Kind, new[] { FolderFlyoutAction.DragMove, FolderFlyoutAction.DragDrop, FolderFlyoutAction.Place }));
            Assert.True(flyout.IsOpen);

            // Unlocked again, the same press is edit mode's at the threshold.
            flyout.Locked = () => false;
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y + slop + 2 }));
            Assert.Equal(1, asked);
            Assert.True(flyout.Arranged);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>The list folder of <see cref="FolderEditClickTests.List"/> with a second list: the airplane switch.</summary>
    private static BarItem TwoLists()
    {
        var island = List();
        var airplane = ListKind.AirplaneMode.New();
        airplane.Id = "airplane";
        airplane.View = FolderView.List;
        island.Folder!.Put(airplane, list: 1);
        return island;
    }

    /// <summary>
    /// Arranged, a line carried from one list to another lands there, above the line whose upper
    /// half it was let go on; carried into the empty slot beside the folder it is a list of its own there.
    /// The folder says where (FolderFlyoutAction.Place with the place), and the dock moves it.
    /// </summary>
    [UiFact]
    public void ALineIsCarriedIntoAnotherListOrANewOne()
    {
        WindowShots.OnHiddenDesktop("folder-drag-lists", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", TwoLists(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            var plan = flyout.Plan!;
            Assert.Equal(2, plan.Lists.Count);
            var slop = GetSystemMetrics(SM.SM_CYDRAG);

            // The battery (list 0, second line) onto the upper half of the airplane's line (list 1).
            var press = flyout.ScreenOfCard(1);
            var airplane = flyout.ScreenOfCard(2);
            var aim = airplane with { y = airplane.y - 4 };
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y + slop + 2 }));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, aim));
            var placed = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, aim));
            Assert.Equal(FolderFlyoutAction.Place, placed.Kind);
            Assert.Equal($"{Owner}/box/battery", placed.Widget?.Key);
            Assert.Equal(new FolderPlace(1, 0), placed.Place);

            // The clock into the slot right of the folder, high over its width button: a new list after the last.
            var right = plan.Adds.Select((add, index) => (add, index)).Single(entry => entry.add.Place == FolderPlace.Slot(2, 0) && entry.add.Kind == FolderAddKind.Width).index;
            var plus = flyout.ScreenOfAdd(right);
            plus.y -= 200;
            press = flyout.ScreenOfCard(0);
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y + slop + 2 }));
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, plus)).Kind);
            placed = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, plus));
            Assert.Equal(FolderFlyoutAction.Place, placed.Kind);
            Assert.Equal($"{Owner}/box/clock", placed.Widget?.Key);
            Assert.Equal(FolderPlace.NewList(2), placed.Place);
            Assert.True(flyout.IsOpen);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// Each list is a panel of its own, apart from the next, and of its own height (2026-10-09). A
    /// line carried from one across the gap between them is still being arranged there — it is not
    /// carried out of the folder — and lands in the other; into the second slot left of the folder it
    /// is a new list there, an empty one between it and the others. Only the panels are the folder
    /// for a press: the gap, and the room over the shorter list, are not.
    /// </summary>
    [UiFact]
    public void ALineIsCarriedAcrossTheGapBetweenTwoPanels()
    {
        WindowShots.OnHiddenDesktop("folder-drag-panels", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", TwoLists(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            var plan = flyout.Plan!;
            Assert.True(plan.Lists[1].Height < plan.Lists[0].Height, "the second list, of one line, is the shorter panel");
            var on = flyout.PanelOnScreen;
            var slop = GetSystemMetrics(SM.SM_CYDRAG);
            var gapX = on.left + (int)MathF.Round((plan.Lists[0].Box.right + plan.Lists[1].Box.left) / 2);
            var gap = new POINT { x = gapX, y = on.top + (int)MathF.Round(plan.Lists[0].Top + 4) };
            var overShort = new POINT { x = on.left + (int)MathF.Round((plan.Lists[1].Box.left + plan.Lists[1].Box.right) / 2), y = on.top + (int)MathF.Round(plan.Lists[0].Top + 4) };

            Assert.False(flyout.Contains(gap), "the gap between two panels is not the folder");
            Assert.False(flyout.Contains(overShort), "nor is the room over the shorter list");
            Assert.True(flyout.Contains(flyout.ScreenOfCard(0)));
            Assert.True(flyout.Contains(flyout.ScreenOfCard(2)));

            // The airplane (list 1) across the gap, high up where only list 0 reaches, onto the upper
            // half of the clock's line (list 0, first).
            var press = flyout.ScreenOfCard(2);
            var clock = flyout.ScreenOfCard(0);
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y + slop + 2 }));
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, overShort)).Kind);
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, gap)).Kind);
            var aim = clock with { y = clock.y - 4 };
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, aim)).Kind);
            var placed = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, aim));
            Assert.Equal(FolderFlyoutAction.Place, placed.Kind);
            Assert.Equal($"{Owner}/box/airplane", placed.Widget?.Key);
            Assert.Equal(new FolderPlace(0, 0), placed.Place);

            // The battery into the second slot on the left: a new list there, an empty one between.
            var further = plan.Slots.Single(slot => slot.Place == FolderPlace.Slot(0, 1)).Box;
            var plus = new POINT { x = on.left + (int)MathF.Round((further.left + further.right) / 2), y = on.top + (int)MathF.Round(plan.Height / 2) };
            Assert.True(plus.x < on.left - (int)plan.Lists[0].Width, "a list's width and more left of the folder");
            press = flyout.ScreenOfCard(1);
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y + slop + 2 }));
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, plus)).Kind);
            placed = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, plus));
            Assert.Equal(FolderFlyoutAction.Place, placed.Kind);
            Assert.Equal($"{Owner}/box/battery", placed.Widget?.Key);
            Assert.Equal(FolderPlace.Slot(0, 1), placed.Place);
            Assert.True(flyout.IsOpen);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// A click at rest on a line — a press and release that never leave the threshold — asks nothing: no edit mode, no settings page, and the folder is still at rest after it.
    /// </summary>
    [UiFact]
    public void AClickAtRestDoesNotTurnEditModeOn()
    {
        WindowShots.OnHiddenDesktop("folder-drag-edit-click", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var asked = 0;
            flyout.ArrangeFromDrag = () => { asked++; return true; };
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder);
            // The clock's line: its own click is refused in a folder, so nothing opens.
            var press = flyout.ScreenOfCard(0);
            var slop = GetSystemMetrics(SM.SM_CXDRAG);

            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            // A wobble of the hand, up to the threshold each way and no further.
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { x = press.x + slop }));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { x = press.x - slop }));
            var clicked = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, press));

            Assert.NotEqual(FolderFlyoutAction.Settings, clicked.Kind);
            Assert.Equal(0, asked);
            Assert.False(flyout.Arranged, "a click turned edit mode on");
            Assert.False(flyout.StaysOpen, "a folder at rest holds itself open after a click");
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// After the hand-over, carried off the folder and let go there, the widget is dragged out: the
    /// folder says so (DragMove, then DragDrop with the widget and where it was let go) for the dock
    /// to answer, and stays open and arranged, as a folder does while its bar is arranged.
    /// </summary>
    [UiFact]
    public void AfterTheHandOverALineCarriedOffTheFolderIsDraggedOut()
    {
        WindowShots.OnHiddenDesktop("folder-drag-edit-out", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.ArrangeFromDrag = () => true;
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder);
            var press = flyout.ScreenOfCard(0);
            var slop = GetSystemMetrics(SM.SM_CXDRAG);

            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { x = press.x + slop + 2 }));
            Assert.True(flyout.Arranged);

            // Off the folder's window, which reaches the work area's edges while it is arranged: on
            // the bar, under the work area, for the dock.
            var away = new POINT { x = 100, y = Work.bottom + 20 };
            var carried = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, away));
            Assert.Equal(FolderFlyoutAction.DragMove, carried.Kind);
            Assert.Equal($"{Owner}/box/clock", carried.Widget?.Key);
            var dropped = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, away));
            Assert.Equal(FolderFlyoutAction.DragDrop, dropped.Kind);
            Assert.Equal($"{Owner}/box/clock", dropped.Widget?.Key);
            Assert.Equal(away.x, dropped.At.x);
            Assert.Equal(away.y, dropped.At.y);
            Assert.True(flyout.IsOpen, "the folder closed when its widget was dragged out in edit mode");
            Assert.True(flyout.Arranged);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// Where the dock says no (another bar is being arranged), nothing changes: the folder stays at
    /// rest, closes for a press elsewhere again, and the drag carries the widget out as it did
    /// before edit mode took drags over.
    /// </summary>
    [UiFact]
    public void RefusedTheDragCarriesTheWidgetOutAsBefore()
    {
        WindowShots.OnHiddenDesktop("folder-drag-edit-refused", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var asked = 0;
            flyout.ArrangeFromDrag = () => { asked++; return false; };
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder);
            var press = flyout.ScreenOfCard(0);
            var slop = GetSystemMetrics(SM.SM_CXDRAG);

            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            var first = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { x = press.x + slop + 2 }));
            Assert.Equal(FolderFlyoutAction.DragMove, first.Kind);
            Assert.Equal(1, asked);
            Assert.False(flyout.Arranged);
            Assert.False(flyout.StaysOpen, "a refused hand-over left the folder holding itself open");
            // Asked once, not on every move.
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { x = press.x + slop + 6 }));
            Assert.Equal(1, asked);
            _ = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, press with { x = press.x + slop + 6 }));
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// The folder just after the hand-over, mid-drag: arranged where it stands, in edit mode's dress,
    /// the carried line over the line under the pointer. A list at 100 % and 150 %, dark and light,
    /// to KARPO_PNG_DIR as folder-drag-edit-list-&lt;look&gt;-&lt;scale&gt;.png.
    /// </summary>
    [UiFact]
    public void TheFolderMidHandOverIsDrawn()
    {
        WindowShots.OnHiddenDesktop("folder-drag-edit-pictures", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
                    using var flyout = new FolderFlyout(gfx, instance, 0);
                    flyout.ArrangeFromDrag = () => true;
                    flyout.Show(gfx, dark ? Dark : Light, scale, Anchor, Work, Edge.Bottom, folder);
                    var press = flyout.ScreenOfCard(0);
                    _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
                    _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y + GetSystemMetrics(SM.SM_CYDRAG) + 2 }));
                    Assert.True(flyout.Arranged);
                    var to = flyout.ScreenOfCard(1);
                    _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, to));
                    WindowShots.Settle(250);
                    var file = $"folder-drag-edit-list-{(dark ? "dark" : "light")}-{(int)(scale * 100)}.png";
                    Assert.True(WindowShots.Save(flyout.Hwnd, file), $"{file} came back blank");
                    _ = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, to));
                    flyout.Hide();
                    folder.Dispose();
                }
            }
        });
    }
}
