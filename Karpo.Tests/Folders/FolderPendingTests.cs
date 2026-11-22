// edit: the one model of an arranged folder's geometry (docs\widgets.md §239): its columns — the
// lists, empty ones too, and the slots beside them — each its width and its lines, flush on the bar's
// side, laid out by one function of the folder and what is about to change in it (FolderPending: lines
// carried, new or lifted, and where they would land; FolderFlyout.Build). Rest, a drag, a paste, a
// widget added and carried, all ask it; a click lands them all through one move (Folders.MoveGroup).
//
// The folders of the window tests are the person's own shapes: empty columns kept as spacers either
// side of the one with widgets, and one of several widths.
//
// Pictures (KARPO_PNG_DIR): folder-model-bottom.png — at rest, a widget added and carried over the
// column, a line dragged over a slot, and the drop; dark at 150 %.
//
// The window tests need Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.FolderEditClickTests;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderPendingTests
{
    private const string Owner = "layout:L";
    private const float Scale = 1.5f;
    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));

    /// <summary>
    /// Two of the person's folders as their file has them (2026-10-19), what is private left out: three
    /// empty columns, one of widgets, two more empty; and four empty columns of their own widths before
    /// one of notifications that fills it. Marked as kept (§328: as a slot's width keeps them), so the
    /// one-time trim of a file's empty columns outside its widgets leaves them.
    /// </summary>
    private static Config Theirs() => Configs.Repaired("""
        { "version": 38, "migrated": [ "outerColumns" ], "layouts": [ { "id": "L", "islands": [
          { "id": "spaced", "kind": "folderList", "section": "middle", "folder": { "name": "Test", "lists": [ [], [], [],
            [ { "island": { "id": "calendar", "kind": "calendar", "height": { "fixed": false, "cells": 24 } } },
              { "island": { "id": "divider", "kind": "divider" } },
              { "island": { "id": "gpu", "kind": "performance", "reading": "gpu", "style": "graph", "height": { "fixed": true, "cells": 4 } } },
              { "island": { "id": "cpu", "kind": "performance", "reading": "cpu", "style": "graph", "height": { "fixed": true, "cells": 4 } } },
              { "island": { "id": "air", "kind": "airQuality", "height": { "fixed": true, "cells": 8 } } } ],
            [], [] ] } },
          { "id": "news", "kind": "folderList", "section": "end", "folder": { "name": "Notifications & calendar", "lists": [ [], [], [], [],
            [ { "island": { "id": "notes", "kind": "notificationsList", "height": { "fixed": false, "cells": 32, "fill": true } } },
              { "island": { "id": "rule", "kind": "divider" } },
              { "island": { "id": "date", "kind": "clock", "style": "date", "height": { "fixed": true, "cells": 2 } } },
              { "island": { "id": "month", "kind": "calendar", "style": "month", "height": { "fixed": true, "cells": 16 } } },
              { "island": { "id": "focus", "kind": "focus", "height": { "fixed": true, "cells": 2 } } } ] ],
            "widths": [ "medium", "narrow", "narrow", "extraWide", "medium" ] } } ] } ] }
        """);

    private static (POINT Anchor, RECT Work) Placed(Edge edge) => edge switch
    {
        Edge.Top => (new POINT { x = 960, y = 48 }, new RECT { top = 48, right = 1920, bottom = 1080 }),
        Edge.Left => (new POINT { x = 48, y = 540 }, new RECT { left = 48, right = 1920, bottom = 1080 }),
        Edge.Right => (new POINT { x = 1872, y = 540 }, new RECT { right = 1872, bottom = 1080 }),
        _ => (new POINT { x = 960, y = 1032 }, new RECT { right = 1920, bottom = 1032 }),
    };

    /// <summary>Where on the screen each line of <paramref name="flyout"/> stands, by its widget's key: what is seen.</summary>
    private static Dictionary<string, Vector2> Seen(FolderFlyout flyout)
    {
        var on = flyout.PanelOnScreen;
        var seen = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < flyout.Lines.Count; index++)
        {
            if (flyout.Plan!.Shows(index))
            {
                seen[flyout.Lines[index].Island.Key] = new Vector2(on.left, on.top) + flyout.LineAt(index);
            }
        }
        return seen;
    }

    /// <summary>The middle of list <paramref name="list"/>'s panel of <paramref name="flyout"/>, on the screen.</summary>
    private static POINT ScreenOfList(FolderFlyout flyout, int list)
    {
        var box = flyout.Plan!.Lists[list].Box;
        var on = flyout.PanelOnScreen;
        return new POINT { x = on.left + (int)MathF.Round((box.left + box.right) / 2), y = on.top + (int)MathF.Round((box.top + box.bottom) / 2) };
    }

    /// <summary>
    /// The bug (the person, 2026-10-19: "when adding a widget in the folder edit mode somehow the
    /// alignment goes back to as if there are hidden columns there"): "Add a widget" put the line into
    /// the folder where its edit bar adds — the first column showing everything it has, an empty spacer
    /// of theirs — and laid the folder out with it there before it was carried; the spacer became one of
    /// the columns with widgets, which are what is centred on the button, and every column moved over.
    /// Come in, the line is in no column until it lands: every line stands where it stood, on every bar,
    /// through the commit, the carry and the aim over another column; let go over no place it lands
    /// where the edit bar adds, as a drop lands (FolderFlyoutAction.Place). The dock's half by hand, as
    /// Dock.AddChosen and Dock.CarryInto do it.
    /// </summary>
    [UiTheory]
    [InlineData(Edge.Bottom, "spaced")]
    [InlineData(Edge.Bottom, "news")]
    [InlineData(Edge.Top, "spaced")]
    [InlineData(Edge.Left, "spaced")]
    [InlineData(Edge.Right, "news")]
    public void AWidgetAddedAndCarriedLeavesEveryColumnWhereItStands(Edge edge, string id)
    {
        WindowShots.OnHiddenDesktop("folder-model-add", (gfx, instance) =>
        {
            var (anchor, work) = Placed(edge);
            var config = Theirs();
            var item = config.ListFor(Owner).First(each => each.Id == id);
            var folder = new FolderIsland($"{Owner}/{id}", item, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, Scale, anchor, work, edge, folder, editing: true);
            var before = Seen(flyout);
            var widgets = flyout.Plan!.CardList[0];

            // Where the edit bar adds — a spacer of theirs — is its home; it goes in where it makes no column.
            var home = flyout.AddPlace();
            Assert.Equal(new FolderPlace(0, 0), home);
            var path = Karpo.Core.Folders.AddNew(config.ListFor(Owner), item, ListKind.NightLight.New());
            var key = Config.IslandKey(Owner, path!);
            var away = edge switch
            {
                Edge.Top => new POINT { x = 4, y = 20 },
                Edge.Left => new POINT { x = 20, y = 4 },
                Edge.Right => new POINT { x = 1900, y = 4 },
                _ => new POINT { x = 4, y = 1060 },
            };
            _ = SetCursorPos(away.x, away.y);
            flyout.Expect([key]);
            folder.Configure(item, config);
            Assert.Equal(before, Seen(flyout).Where(each => each.Key != key).ToDictionary());
            Assert.True(flyout.Carry([key], home));
            Assert.True(flyout.Carrying);
            Assert.Equal(before, Seen(flyout).Where(each => each.Key != key).ToDictionary());

            // Over the column of widgets: it lands between two of its lines; the others stand.
            var column = ScreenOfList(flyout, widgets);
            _ = SetCursorPos(column.x, column.y);
            _ = flyout.Message(WM.WM_MOUSEMOVE, 0, Client(flyout.Hwnd, column));
            Assert.Equal(widgets, flyout.Aimed?.List);

            // Let go away from the folder: home, as the move a drop makes.
            var landed = flyout.LandCarry(away);
            Assert.Equal(FolderFlyoutAction.Place, landed.Kind);
            Assert.Equal(home, landed.Place);
            Assert.Equal(key, landed.Widget?.Key);
            Assert.False(flyout.Carrying);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// Pictures of the person's spaced folder on a bottom bar, dark at 150 %, the window to the work
    /// area's edges as it is arranged: folder-model-rest.png; folder-model-add.png, a widget added and
    /// carried over the column, its landing open between two lines; folder-model-drag.png, the calendar
    /// dragged over the slot right of the folder; folder-model-drop.png, let go there. The columns of
    /// widgets stand still through all four but the last, where the calendar's new column joins them.
    /// </summary>
    [UiFact]
    public void ThePersonsFolderAtRestCarriedDraggedAndDropped()
    {
        WindowShots.OnHiddenDesktop("folder-model-pictures", (gfx, instance) =>
        {
            var (anchor, work) = Placed(Edge.Bottom);
            var config = Theirs();
            var item = config.ListFor(Owner).First(each => each.Id == "spaced");
            var folder = new FolderIsland($"{Owner}/spaced", item, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, Scale, anchor, work, Edge.Bottom, folder, editing: true);
            var widgets = flyout.Plan!.CardList[0];
            WindowShots.Settle(300);
            Assert.True(WindowShots.Save(flyout.Hwnd, "folder-model-rest.png"));
            var before = Seen(flyout);

            // A widget added, carried over the column of widgets.
            var home = flyout.AddPlace();
            var path = Karpo.Core.Folders.AddNew(config.ListFor(Owner), item, ListKind.NightLight.New());
            var key = Config.IslandKey(Owner, path!);
            var column = ScreenOfList(flyout, widgets);
            _ = SetCursorPos(column.x, column.y);
            flyout.Expect([key]);
            folder.Configure(item, config);
            Assert.True(flyout.Carry([key], home));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 0, Client(flyout.Hwnd, column));
            WindowShots.Settle((int)Motion.NormalMs(MotionKind.Move) + 120);
            Assert.True(WindowShots.Save(flyout.Hwnd, "folder-model-add.png"));
            flyout.EndCarry();
            Assert.True(Karpo.Core.Folders.Remove(config.ListFor(Owner), path!) is not null);
            folder.Configure(item, config);
            WindowShots.Settle((int)Motion.NormalMs(MotionKind.Move) + 50);
            _ = flyout.Message(WM.WM_TIMER, FolderFlyout.SettleTimer, 0);
            Assert.Equal(before, Seen(flyout));
            // Taken out again, it took the empty columns outside the widgets with it (§328); the widgets stand.
            Assert.Single(item.Folder!.Lists);

            // The person's folder as their file has it again, its spacers kept.
            flyout.Hide();
            folder.Dispose();
            config = Theirs();
            item = config.ListFor(Owner).First(each => each.Id == "spaced");
            folder = new FolderIsland($"{Owner}/spaced", item, gfx, config);
            flyout.Show(gfx, Dark, Scale, anchor, work, Edge.Bottom, folder, editing: true);
            WindowShots.Settle(150);

            // The calendar dragged over the empty column right of the widgets (its spacer; no slot has room on this screen).
            var rest = flyout.Plan!;
            var spacer = rest.Lists[widgets + 1].Box;
            var panel = flyout.PanelOnScreen;
            var over = new POINT { x = panel.left + (int)MathF.Round((spacer.left + spacer.right) / 2), y = panel.top + (int)MathF.Round(rest.Height / 2) };
            var press = flyout.ScreenOfCard(0);
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { x = press.x + 40 }));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, over));
            WindowShots.Settle((int)Motion.NormalMs(MotionKind.Move) + 120);
            Assert.True(WindowShots.Save(flyout.Hwnd, "folder-model-drag.png"));

            Assert.Equal(new FolderPlace(widgets + 1, 0), flyout.Aimed);

            // Let go: the dock's half by hand (Dock.PlaceInFolder).
            var placed = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, over));
            Assert.Equal(FolderFlyoutAction.Place, placed.Kind);
            Assert.Equal(new FolderPlace(widgets + 1, 0), placed.Place);
            Assert.True(Karpo.Core.Folders.Move(item.Folder!, Karpo.Core.Folders.CellOf(item.Folder!, "calendar")!, placed.Place));
            folder.Configure(item, config);
            WindowShots.Settle((int)Motion.NormalMs(MotionKind.Move) + 120);
            _ = flyout.Message(WM.WM_TIMER, FolderFlyout.SettleTimer, 0);
            Assert.True(WindowShots.Save(flyout.Hwnd, "folder-model-drop.png"));
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// Every way lines come to a place is laid out by the one function, alike (§239): a line dropped
    /// there from where it stood, a widget added (in at the end of the last list) and a line pasted
    /// (in at the end of the first) carried there — into a gap of a list and into a slot — lay the
    /// folder out exactly as it is once the move is made, at rest and arranged, on every bar.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void ADropAPasteAndAnAddToOnePlaceAreLaidOutAlike(Edge edge)
    {
        foreach (var editing in new[] { false, true })
        {
            foreach (var to in new[] { new FolderPlace(1, 1), new FolderPlace(3, 0), FolderPlace.Slot(0, 1), FolderPlace.Slot(4, 0) })
            {
                // The folder once n is let go at <to>, every list where it stands — the empty ones
                // outside go after the move (Folders.Trim, §328), not while it is shown: the reference.
                var landed = Lists(" | a b c |  | d");
                var n = Line("n");
                _ = Karpo.Core.Folders.InsertLine(BarKind.FolderList, landed, n, new FolderPlace(1, 3));
                Assert.True(Karpo.Core.Folders.MoveKeepingLists(landed, [Karpo.Core.Folders.CellOf(landed, "n")!], to));
                var after = Plan(landed, editing, edge, null);

                // A drop: n stood at the column's end. An add: in at the last list's end. A paste: the first's.
                foreach (var (shape, isNew) in new[] { (" | a b c n |  | d", false), (" | a b c |  | d n", true), ("n | a b c |  | d", true) })
                {
                    var folder = Lists(shape);
                    var cells = folder.Cells;
                    var index = cells.ToList().FindIndex(cell => cell.Island.Id == "n");
                    var plan = Plan(folder, editing, edge, new FolderPending([index], to, isNew));
                    AssertLaidAlike(after, plan, landed, folder);
                }
            }
        }
    }

    /// <summary>
    /// Lines come in and not landed are nowhere (§239): the folder with them in no list is laid out as
    /// the folder without them — its columns, the empty spacers at rest and arranged, what is centred on
    /// the button (Core) — wherever they went in, a spacer at either end of the person's folder too;
    /// and they keep their own size, outside every panel, for their plates.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    public void LinesComeInAreLaidOutNowhere(Edge edge)
    {
        foreach (var editing in new[] { false, true })
        {
            var without = Lists(" |  | a b |  | ");
            var bare = Plan(without, editing, edge, null);
            foreach (var shape in new[] { "n |  | a b |  | ", " |  | a b |  | n", " |  | a n b |  | " })
            {
                var folder = Lists(shape);
                var index = folder.Cells.ToList().FindIndex(cell => cell.Island.Id == "n");
                var plan = Plan(folder, editing, edge, new FolderPending([index], New: true).AtRest);
                AssertLaidAlike(bare, plan, without, folder);
                Assert.Equal(bare.Core, plan.Core);
                // The columns' buttons too, an outer spacer's on what shows of it at a screen's edge.
                var screen = PopupPaint.Rect(-100, 0, bare.Width + 150, bare.Height);
                Assert.Equal(FolderFlyout.WithSlots(bare, Scale, screen, edge).Adds, FolderFlyout.WithSlots(plan, Scale, screen, edge).Adds);
                var card = plan.Cards[index];
                Assert.True(card.bottom - card.top > 0, "it keeps its height");
                var list = plan.Lists[plan.CardList[index]].Box;
                Assert.True(card.bottom <= list.top || card.top >= list.bottom, "outside its list's panel");
            }
        }
        // A line lifted from its place stands there at rest: only new lines are left out.
        Assert.Null(new FolderPending([0]).AtRest);
    }

    /// <summary>
    /// Empty columns are spacers (§148, §158): at rest one between two with widgets keeps its width and
    /// one outside them has none; arranged every one keeps its width and the outer ones hang off the
    /// side of what is centred (Core), which is the same box both ways; the slots carry on at a medium
    /// column's width, the lists' gap apart, and each is a list of its own there, the slots between it
    /// and the lists empty ones.
    /// </summary>
    [Fact]
    public void EmptyColumnsAreSpacersAndSlotsCarryThemOn()
    {
        var folder = Lists(" | a |  | b | ");
        var rest = Plan(folder, editing: false, Edge.Bottom, null);
        var arranged = Plan(folder, editing: true, Edge.Bottom, null);
        Assert.Equal(0, rest.Lists[0].Width);
        Assert.Equal(0, rest.Lists[4].Width);
        Assert.Equal(rest.Lists[1].Width, rest.Lists[2].Width);
        Assert.All(arranged.Lists, list => Assert.Equal(arranged.Lists[1].Width, list.Width));
        Assert.Equal(rest.Core.right - rest.Core.left, arranged.Core.right - arranged.Core.left);
        Assert.Equal(arranged.Lists[1].Left, arranged.Core.left);
        Assert.Equal(arranged.Lists[3].Left + arranged.Lists[3].Width, arranged.Core.right);

        var slotted = FolderFlyout.WithSlots(arranged, Scale, PopupPaint.Rect(-2000, 0, 4000 + arranged.Width, arranged.Height), Edge.Bottom);
        var right = slotted.Slots.Where(slot => slot.Place.List == 5).ToList();
        Assert.Equal(FolderPlace.Slot(5, 0), right[0].Place);
        Assert.Equal(arranged.Width + arranged.ListGap, right[0].Box.left);
        Assert.Equal(right[0].Box.right + arranged.ListGap, right[1].Box.left);
        // The second slot out landed in: two lists more, the first of them empty; the empty ones left
        // before b go (§328).
        var into = Lists(" | a |  | b | ");
        Assert.True(Karpo.Core.Folders.Move(into, Karpo.Core.Folders.CellOf(into, "a")!, right[1].Place));
        Assert.Equal("b |  |  | a", Shown(into));
    }

    // --- The model's own helpers ----------------------------------------------------------------

    /// <summary>A folder of lists of lines, each named by its id: "a b | c"; " | a" has an empty first list.</summary>
    private static FolderOptions Lists(string shape)
    {
        var folder = new FolderOptions { Lists = [] };
        foreach (var list in shape.Split('|'))
        {
            folder.Lists.Add([.. list.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(id => new FolderCell { Island = Line(id) })]);
        }
        return folder;
    }

    private static CellItem Line(string id)
    {
        var island = ListKind.Battery.New();
        island.Id = id;
        island.View = FolderView.List;
        return island;
    }

    private static string Shown(FolderOptions folder) =>
        string.Join(" | ", folder.Lists.Select(list => string.Join(" ", list.Select(cell => cell.Island.Id))));

    private static FolderPlan Plan(FolderOptions folder, bool editing, Edge edge, FolderPending? pending) =>
        FolderFlyout.Build(Dark, Scale, FolderView.List, folder, folder.Cells, 1032, editing, edge: edge, pending: pending);

    /// <summary>
    /// <paramref name="plan"/> of <paramref name="folder"/> is <paramref name="expected"/> of
    /// <paramref name="options"/>: the same columns where they are, and each line of the one where it
    /// is in the other, by its id.
    /// </summary>
    private static void AssertLaidAlike(FolderPlan expected, FolderPlan plan, FolderOptions options, FolderOptions folder)
    {
        Assert.Equal(expected.Lists.Select(list => list.Box), plan.Lists.Select(list => list.Box));
        Assert.Equal(expected.Width, plan.Width);
        Assert.Equal(expected.Height, plan.Height);
        Assert.Equal(expected.Core, plan.Core);
        var cells = options.Cells.ToList();
        var others = folder.Cells.ToList();
        for (var index = 0; index < cells.Count; index++)
        {
            var at = others.FindIndex(cell => cell.Island.Id == cells[index].Island.Id);
            Assert.Equal((cells[index].Island.Id, expected.Cards[index]), (others[at].Island.Id, plan.Cards[at]));
        }
    }
}
