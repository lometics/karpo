// edit: every column an arranged list folder shows takes a line carried from another (the person,
// 2026-10-12: "not every column in the folder edit view is a drop target for the drag and drop" —
// "it was an empty one, but only the empty one that is like one column away from the filled ones";
// "i was dragging a widget from another column"). The dead one was the slot next to a list the
// carried line was alone in: the move read a new list beside its own list as its own place, from the
// days a list a line left closed up, though that list now stays, an empty column (Folders.Move,
// FolderFlyout.Ghost). Each test presses a line, carries it over every other column — its list's
// panel, an empty list, a slot out to the screen's edge — over the panel and high above it, lets go
// and does what the dock does with the place it hears (Folders.Move, Folders.MoveGroup), and the
// line must then stand in that column.
//
// The picture: folder-drop-beside-own-<100|150>.png in KARPO_PNG_DIR, the ghost in that slot.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.FolderEditClickTests;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderDropSweepTests
{
    private const string Owner = "layout:default";
    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));

    /// <summary>
    /// One line at a time, from every list into every other column, for folders of lone lines,
    /// empty lists between and beside them, and lists of other widths.
    /// </summary>
    [UiTheory]
    [InlineData("a b | c", 1f, "bottom")]
    [InlineData("a | b", 1.5f, "bottom")]
    [InlineData("a |  |  | b", 1f, "top")]
    [InlineData(" | a b | ", 1.5f, "top")]
    [InlineData("a:n | b c |  :w", 1f, "bottom")]
    [InlineData("a:w |  | b:n", 1.5f, "bottom")]
    public void EveryColumnTakesALineFromAnother(string shape, float scale, string side) =>
        Sweep(shape, scale, side == "top" ? Edge.Top : Edge.Bottom, together: false);

    /// <summary>Every line of a list carried together, as a selection is, into every other column.</summary>
    [UiTheory]
    [InlineData("a b | c", 1f, "bottom")]
    [InlineData(" | a b |  | c d", 1.5f, "top")]
    public void EveryColumnTakesAListCarriedTogether(string shape, float scale, string side) =>
        Sweep(shape, scale, side == "top" ? Edge.Top : Edge.Bottom, together: true);

    /// <summary>
    /// The battery's line, alone in its list, carried into the slot right next to that list: the
    /// slot lit, the ghost in it — the column that was dead. At 100 % and 150 %.
    /// </summary>
    [UiFact]
    public void TheSlotBesideALoneLinesListShowsItsGhost()
    {
        WindowShots.OnHiddenDesktop("folder-drop-beside-own", (gfx, instance) =>
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                var (anchor, work) = Screen(scale, Edge.Bottom);
                var item = Shape("a c | b");
                var folder = new FolderIsland($"{Owner}/box", item, gfx, new Config());
                using var flyout = new FolderFlyout(gfx, instance, 0);
                flyout.Show(gfx, Dark, scale, anchor, work, Edge.Bottom, folder, editing: true);
                var plan = flyout.Plan!;
                var slot = plan.Slots.Single(each => each.Place == FolderPlace.Slot(2, 0));
                var on = flyout.PanelOnScreen;
                var over = new POINT { x = on.left + (int)((slot.Box.left + slot.Box.right) / 2), y = on.top + (int)(plan.Height / 2) };
                var press = flyout.ScreenOfCard(2);
                _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
                _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y - GetSystemMetrics(SM.SM_CYDRAG) - 2 }));
                _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, over));
                WindowShots.Settle(250);
                var file = $"folder-drop-beside-own-{(int)(scale * 100)}.png";
                Assert.True(WindowShots.Save(flyout.Hwnd, file), $"{file} came back blank");
                var placed = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, over));
                Assert.Equal(FolderFlyoutAction.Place, placed.Kind);
                Assert.Equal(FolderPlace.Slot(2, 0), placed.Place);
                flyout.Hide();
                folder.Dispose();
            }
        });
    }

    private static void Sweep(string shape, float scale, Edge edge, bool together)
    {
        var (anchor, work) = Screen(scale, edge);
        WindowShots.OnHiddenDesktop("folder-drop-sweep", (gfx, instance) =>
        {
            var item = Shape(shape);
            var options = item.Folder!;
            var folder = new FolderIsland($"{Owner}/box", item, gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, scale, anchor, work, edge, folder, editing: true);
            var plan = flyout.Plan!;
            var on = flyout.PanelOnScreen;
            var lists = plan.Lists.Count;
            Assert.Equal(options.Lists.Count, lists);
            // Every column on the screen, by where it stands among them: the lists from 0, the slots
            // on the left from -1 outwards, those on the right from the number of lists.
            var columns = Enumerable.Range(0, lists).Select(list => (Column: list, Box: plan.Lists[list].Box))
                .Concat(plan.Slots.Select(slot => (Column: slot.Place.List == 0 ? -1 - slot.Place.Apart : lists + slot.Place.Apart, slot.Box)))
                .ToList();
            Assert.Contains(columns, column => column.Column < 0);
            Assert.Contains(columns, column => column.Column >= lists);
            var cells = options.Cells;
            var slop = GetSystemMetrics(SM.SM_CYDRAG);
            var ys = edge == Edge.Top ? new[] { on.bottom + 200, (on.top + on.bottom) / 2 } : new[] { on.top - 200, (on.top + on.bottom) / 2 };
            for (var card = 0; card < cells.Count; card++)
            {
                var from = Karpo.Core.Folders.PlaceOf(options, cells[card])!.Value;
                // Together: the whole list, from its first line.
                var carried = together ? options.Lists[from.List] : [cells[card]];
                if (together && (from.Index != 0 || carried.Count < 2))
                {
                    continue;
                }
                foreach (var (column, box) in columns.Where(column => column.Column != from.List))
                {
                    // The middle of what shows of it on the screen.
                    var left = Math.Max(on.left + box.left, work.left);
                    var right = Math.Min(on.left + box.right, work.right);
                    foreach (var y in ys)
                    {
                        var over = new POINT { x = (int)((left + right) / 2), y = y };
                        if (together)
                        {
                            flyout.Picked = Key(carried[0]);
                            flyout.AlsoPicked = [.. carried.Skip(1).Select(Key)];
                        }
                        var press = flyout.ScreenOfCard(card);
                        _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
                        _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y - slop - 2 }));
                        _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, over));
                        var placed = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, over));
                        var what = $"{shape}: {string.Join(" ", carried.Select(cell => cell.Island.Id))} let go over column {column} at {over.x},{over.y}";
                        Assert.True(placed.Kind == FolderFlyoutAction.Place, $"{what}: {placed.Kind}");
                        // The dock's half (Dock.PlaceInFolderIn), on a copy, before the empty lists it
                        // leaves outside go (Folders.Trim, §328), so the columns are still counted as shown.
                        var copy = new FolderOptions { Lists = [.. options.Lists.Select(list => list.ToList())] };
                        var moved = Karpo.Core.Folders.MoveKeepingLists(copy, carried, placed.Place);
                        Assert.True(moved, $"{what}: {placed.Place} moved nothing");
                        var shift = placed.Place is { New: true, List: 0 } ? copy.Lists.Count - lists : 0;
                        foreach (var cell in carried)
                        {
                            Assert.True(Karpo.Core.Folders.PlaceOf(copy, cell)!.Value.List - shift == column, $"{what}: {placed.Place} put {cell.Island.Id} elsewhere");
                        }
                    }
                }
            }
            flyout.Hide();
            folder.Dispose();
        });
    }

    private static string Key(FolderCell cell) => $"{Owner}/box/{cell.Island.Id}";

    private static (POINT Anchor, RECT Work) Screen(float scale, Edge edge)
    {
        var (width, height) = scale > 1 ? (3840, 2100) : (1920, 1040);
        return edge == Edge.Top
            ? (new POINT { x = width / 2, y = 60 }, new RECT { top = 60, right = width, bottom = height })
            : (new POINT { x = width / 2, y = height }, new RECT { right = width, bottom = height });
    }

    /// <summary>
    /// "box", a list folder of <paramref name="shape"/>: its lists split by "|", each its lines by
    /// letter (a clock, b battery, c timer, d focus), a list's width after a ":" (n narrow, w wide).
    /// </summary>
    private static BarItem Shape(string shape)
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        island.Folder.Lists = [];
        var kinds = new Dictionary<char, ListKind> { ['a'] = ListKind.Clock, ['b'] = ListKind.Battery, ['c'] = ListKind.Timer, ['d'] = ListKind.Focus };
        var widths = new List<FolderListWidth>();
        foreach (var part in shape.Split('|'))
        {
            var (lines, width) = part.Split(':') is [var named, var wide] ? (named, wide.Trim()) : (part, "");
            var list = new List<FolderCell>();
            foreach (var letter in lines.Where(char.IsLetter))
            {
                var widget = kinds[letter].New();
                widget.Id = letter.ToString();
                widget.View = Karpo.Core.Folders.View(island.Kind);
                list.Add(new FolderCell { Island = widget });
            }
            island.Folder.Lists.Add(list);
            widths.Add(width switch { "n" => FolderListWidth.Narrow, "w" => FolderListWidth.Wide, _ => FolderListWidth.Medium });
        }
        if (widths.Any(width => width != FolderListWidth.Medium))
        {
            island.Folder.Widths = widths;
        }
        return island;
    }
}
