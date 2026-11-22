// An arranged folder drawn out across the screen (the person, 2026-10-12: "lines going from the top
// to the bottom of the screen which mark the left and right edge of the folders ... they should
// repeat all the way to the edges of the screen"): guide lines at every list's edges, and empty
// slots of a medium list's width either side out to the work area's edges, each with its guide
// lines, its "+" and room to drop a line on — adding or dropping there makes it a list where it
// stood, the slots nearer the lists empty lists, kept (FolderSlot, FolderPlace.Slot). The side "+"
// that added an empty column went.
//
// The plan's half is pure; the open folder's (the window to the screen's edges, what lets a press
// through, the lists held where they stand) needs Direct2D and a window station: KARPO_UI_TESTS=1.
// The pictures: folder-slots-<bottom|left>-<100|150>-<dark|light>.png in KARPO_PNG_DIR.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Present;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.FolderEditClickTests;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderSlotTests
{
    private const string Owner = "layout:default";

    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly Look Light = new(Glass: false, Dark: false, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly POINT Anchor = new() { x = 960, y = 1040 };
    private static readonly RECT Work = new() { right = 1920, bottom = 1040 };

    /// <summary>
    /// The guide lines are faint (the person, 2026-10-23: "make the column lines more faint"): under a
    /// third of the scroll indicator's ink in either look, still there; high contrast keeps the system's
    /// colour whole, a hairline.
    /// </summary>
    [Fact]
    public void TheGuideLinesAreFaint()
    {
        foreach (var look in new[] { Dark, Light })
        {
            var theme = FlyoutTheme.For(look);
            var ink = FolderFlyout.GuideInk(theme);
            Assert.Equal(theme.ScrollIndicator with { A = 0 }, ink with { A = 0 });
            Assert.InRange(ink.A, 1, theme.ScrollIndicator.A / 3);
        }
        var contrast = FlyoutTheme.For(Dark with { HighContrast = true });
        Assert.Equal(contrast.ScrollIndicator, FolderFlyout.GuideInk(contrast));
    }

    /// <summary>A folder of lists of lines, each named by its id: "a b | c", an empty list "".</summary>
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

    private static FolderCell Of(FolderOptions folder, string id) => Karpo.Core.Folders.CellOf(folder, id)!;

    private static FolderPlan Plan(FolderOptions options, float scale, Edge edge = Edge.Bottom) =>
        FolderFlyout.Build(Dark, scale, FolderView.List, options, options.Cells, 1040, editing: true, edge: edge);

    // --- The slots ---------------------------------------------------------------------------

    /// <summary>
    /// The slots repeat out to both edges of the room, a medium list wide and the lists' gap apart,
    /// the nearest first on either side; the last on each side the one the edge cuts where its
    /// buttons still fit, and no room for another past it. Each slot's "+" and width button are the
    /// plan's last ones, in the slots' order, centred on what shows of its slot, level with an empty
    /// list's.
    /// </summary>
    [Theory]
    [InlineData(1f, Edge.Bottom)]
    [InlineData(1.5f, Edge.Bottom)]
    [InlineData(1.5f, Edge.Top)]
    public void TheSlotsRepeatToBothEdges(float scale, Edge edge)
    {
        var plan = Plan(Lists("a b | c"), scale, edge);
        // The room off the bar: past the lists' far end, short of the bar's side.
        var room = edge == Edge.Top
            ? new D2D_RECT_F { left = -1000 * scale, top = -12, right = plan.Width + (1300 * scale), bottom = plan.Height + (600 * scale) }
            : new D2D_RECT_F { left = -1000 * scale, top = -600 * scale, right = plan.Width + (1300 * scale), bottom = plan.Height + 12 };
        var slotted = FolderFlyout.WithSlots(plan, scale, room, edge);
        var width = PopupPaint.Round(FolderListWidths.MediumDip * scale);
        var (gap, side, apart) = (plan.ListGap, FolderFlyout.ButtonsWidth(scale, 2), PopupPaint.Round(FolderFlyout.AddApartDip * scale));

        var left = slotted.Slots.Where(slot => slot.Place.List == 0).ToList();
        var right = slotted.Slots.Where(slot => slot.Place.List == 2).ToList();
        Assert.Equal(slotted.Slots.Count, left.Count + right.Count);
        for (var at = 0; at < left.Count; at++)
        {
            Assert.Equal(FolderPlace.Slot(0, at), left[at].Place);
            Assert.Equal(-gap - (at * (width + gap)), left[at].Box.right, 2);
            Assert.Equal(width, left[at].Box.right - left[at].Box.left, 2);
            Assert.Equal((room.top, room.bottom), (left[at].Box.top, left[at].Box.bottom));
        }
        for (var at = 0; at < right.Count; at++)
        {
            Assert.Equal(FolderPlace.Slot(2, at), right[at].Place);
            Assert.Equal(plan.Width + gap + (at * (width + gap)), right[at].Box.left, 2);
            Assert.Equal(width, right[at].Box.right - right[at].Box.left, 2);
        }
        // Out to the edges: the last of each side reaches past it, or the room past it holds no buttons.
        Assert.True(left[^1].Box.left <= room.left || left[^1].Box.left - gap - room.left < side + (2 * apart));
        Assert.True(right[^1].Box.right >= room.right || room.right - right[^1].Box.right - gap < side + (2 * apart));
        Assert.True(left.Count >= 2 && right.Count >= 3, "several either side");

        // Their "+" and width button (§246): after the plan's own, the slots' order, the pair centred
        // on what shows, level with an empty list's.
        var buttons = slotted.Adds.Skip(plan.Adds.Count).ToList();
        Assert.Equal(plan.Adds, slotted.Adds.Take(plan.Adds.Count));
        Assert.Equal(slotted.Slots.SelectMany(slot => new[] { slot.Place, slot.Place }), buttons.Select(add => add.Place));
        Assert.Equal(slotted.Slots.SelectMany(_ => new[] { FolderAddKind.Add, FolderAddKind.Width }), buttons.Select(add => add.Kind));
        var y = edge == Edge.Top ? apart : plan.Height - apart - PopupPaint.Round(FolderFlyout.AddDip * scale);
        for (var at = 0; at < slotted.Slots.Count; at++)
        {
            var box = slotted.Slots[at].Box;
            var middle = (MathF.Max(box.left, room.left) + MathF.Min(box.right, room.right)) / 2;
            var (plus, sizer) = (buttons[2 * at].Box, buttons[(2 * at) + 1].Box);
            Assert.InRange((plus.left + sizer.right) / 2, middle - 1, middle + 1);
            Assert.Equal(y, plus.top, 2);
            Assert.Equal(y, sizer.top, 2);
        }
        // At rest there are none.
        var resting = FolderFlyout.Build(Dark, scale, FolderView.List, Lists("a b | c"), Lists("a b | c").Cells, 1040, editing: false, edge: edge);
        Assert.Empty(FolderFlyout.WithSlots(resting, scale, room, edge).Slots);
    }

    /// <summary>A side bar's room ends at the bar: no slot on that side, the other side's out to its edge.</summary>
    [Fact]
    public void BesideALeftBarTheSlotsAreOnTheRight()
    {
        var plan = Plan(Lists("a"), 1f, Edge.Left);
        var room = new D2D_RECT_F { left = -12, top = -300, right = plan.Width + 800, bottom = plan.Height + 300 };
        var slotted = FolderFlyout.WithSlots(plan, 1f, room, Edge.Left);

        Assert.NotEmpty(slotted.Slots);
        Assert.All(slotted.Slots, slot => Assert.Equal(FolderPlace.Slot(1, slot.Place.Apart), slot.Place));
    }

    /// <summary>
    /// The guide lines: a hairline down the room at each list's left and right edge, just inside it
    /// — over and under its panel, not across it — and at each slot's, the whole room down.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheGuideLinesMarkEveryEdge(float scale)
    {
        var plan = Plan(Lists("a b | c"), scale);
        var room = new D2D_RECT_F { left = -900 * scale, top = -700 * scale, right = plan.Width + (900 * scale), bottom = plan.Height + 12 };
        var slotted = FolderFlyout.WithSlots(plan, scale, room, Edge.Bottom);
        var hair = plan.Hairline;
        var lines = FolderFlyout.Guides(slotted, room);

        Assert.All(lines, line => Assert.Equal(hair, line.right - line.left, 3));
        var xs = lines.Select(line => line.left).Distinct().Order().ToList();
        var wanted = slotted.Lists.SelectMany(list => new[] { list.Box.left, list.Box.right - hair })
            .Concat(slotted.Slots.SelectMany(slot => new[] { slot.Box.left, slot.Box.right - hair }))
            .Where(x => x >= room.left && x + hair <= room.right)
            .Order().ToList();
        Assert.Equal(wanted, xs);
        foreach (var list in slotted.Lists)
        {
            var mine = lines.Where(line => line.left == list.Box.left).ToList();
            // Over the panel to the room's top, and under it to the room's bottom: never across it.
            Assert.Contains(mine, line => line.top == room.top && line.bottom == list.Box.top);
            Assert.Contains(mine, line => line.top == list.Box.bottom && line.bottom == room.bottom);
            Assert.DoesNotContain(mine, line => line.top < list.Box.bottom && line.bottom > list.Box.top);
        }
        foreach (var slot in slotted.Slots.Where(slot => slot.Box.left >= room.left))
        {
            Assert.Contains(lines, line => line.left == slot.Box.left && line.top == room.top && line.bottom == room.bottom);
        }
    }

    // --- The model -----------------------------------------------------------------------------

    /// <summary>
    /// A line dropped into a slot is a list where the slot stood: the nearest slot next to the lists,
    /// one further out with empty lists between — kept, spacers — on the left before the first, on
    /// the right after the last; a widget added into one the same. A list a line leaves stays, empty,
    /// in the middle as at an end.
    /// </summary>
    [Fact]
    public void DroppingIntoASlotMakesAListThereAndKeepsTheEmptyOnes()
    {
        var folder = Lists("a b | c");
        Assert.True(Karpo.Core.Folders.Move(folder, Of(folder, "a"), FolderPlace.Slot(0, 0)));
        Assert.Equal("a | b | c", Shown(folder));
        Assert.True(Karpo.Core.Folders.Move(folder, Of(folder, "b"), FolderPlace.Slot(3, 1)));
        Assert.Equal("a |  | c |  | b", Shown(folder));
        Assert.True(Karpo.Core.Folders.Move(folder, Of(folder, "c"), FolderPlace.Slot(0, 2)));
        Assert.Equal("c |  |  | a |  |  |  | b", Shown(folder));
        Assert.Equal(8, folder.Lists.Count);
        Assert.Null(folder.Widths);

        var added = Lists("a | b");
        Assert.NotNull(Karpo.Core.Folders.InsertLine(BarKind.FolderList, added, Line("n"), FolderPlace.Slot(0, 1)));
        Assert.Equal("n |  | a | b", Shown(added));
        Assert.NotNull(Karpo.Core.Folders.InsertLine(BarKind.FolderList, added, Line("m"), FolderPlace.Slot(4, 0)));
        Assert.Equal("n |  | a | b | m", Shown(added));

        // A list's widths go with it: the wide one stays the wide one, the new ones are medium.
        var wide = Lists("a | b");
        Assert.True(Karpo.Core.Folders.SetWidth(wide, 1, FolderListWidth.Wide));
        Assert.True(Karpo.Core.Folders.Move(wide, Of(wide, "a"), FolderPlace.Slot(0, 1)));
        Assert.Equal("a |  |  | b", Shown(wide));
        Assert.Equal([FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Wide], wide.Widths);
    }

    /// <summary>
    /// The list a slot becomes (<see cref="Karpo.Core.Folders.ListOfSlot"/>) is where adding into it
    /// and giving it a width put that list, on either side and further out alike.
    /// </summary>
    [Fact]
    public void TheListASlotBecomesIsWhereItStood()
    {
        foreach (var slot in new[] { FolderPlace.Slot(0, 0), FolderPlace.Slot(0, 2), FolderPlace.Slot(2, 0), FolderPlace.Slot(2, 1) })
        {
            var at = Karpo.Core.Folders.ListOfSlot(2, slot);
            var added = Lists("a | b");
            Assert.NotNull(Karpo.Core.Folders.InsertLine(BarKind.FolderList, added, Line("n"), slot));
            Assert.Equal(new FolderPlace(at, 0), Karpo.Core.Folders.PlaceOf(added, Of(added, "n")));
            var sized = Lists("a | b");
            Assert.True(Karpo.Core.Folders.SetSlotWidth(sized, slot, FolderListWidth.Wide));
            Assert.Equal(FolderListWidth.Wide, sized.WidthOf(at));
            Assert.Equal(1, sized.Widths!.Count(width => width != FolderListWidth.Medium));
        }
    }

    /// <summary>
    /// A slot's width picked makes it an empty list that wide where it stood, the slots between it
    /// and the lists empty medium ones, as adding into it does (the person, 2026-10-12: "every column
    /// that you see on the screen, even if empty you should be able to change its width"); medium, a
    /// slot's own width, changes nothing, nor does a place that is no slot.
    /// </summary>
    [Fact]
    public void ASlotsWidthMakesItAnEmptyListThatWide()
    {
        var folder = Lists("a | b");
        Assert.True(Karpo.Core.Folders.SetSlotWidth(folder, FolderPlace.Slot(2, 1), FolderListWidth.Wide));
        Assert.Equal("a | b |  | ", Shown(folder));
        Assert.Equal([FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Wide], folder.Widths);

        Assert.True(Karpo.Core.Folders.SetSlotWidth(folder, FolderPlace.Slot(0, 0), FolderListWidth.Narrow));
        Assert.Equal(" | a | b |  | ", Shown(folder));
        Assert.Equal([FolderListWidth.Narrow, FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Wide], folder.Widths);

        Assert.True(Karpo.Core.Folders.SetSlotWidth(folder, FolderPlace.Slot(0, 2), FolderListWidth.Wide));
        Assert.Equal(FolderListWidth.Wide, folder.WidthOf(0));
        Assert.Equal(8, folder.Lists.Count);
        Assert.Equal([FolderListWidth.Wide, FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Narrow], folder.Widths!.Take(4));

        var before = Shown(folder);
        Assert.False(Karpo.Core.Folders.SetSlotWidth(folder, FolderPlace.Slot(0, 0), FolderListWidth.Medium));
        Assert.False(Karpo.Core.Folders.SetSlotWidth(folder, new FolderPlace(1, 0), FolderListWidth.Wide));
        Assert.Equal(before, Shown(folder));

        // Then it is a list like any: a widget dropped in it, its width kept; the list the widget left
        // empty outside goes (§328).
        var plain = Lists("a | b");
        Assert.True(Karpo.Core.Folders.SetSlotWidth(plain, FolderPlace.Slot(2, 0), FolderListWidth.Narrow));
        Assert.True(Karpo.Core.Folders.Move(plain, Of(plain, "a"), new FolderPlace(2, 0)));
        Assert.Equal("b | a", Shown(plain));
        Assert.Equal(FolderListWidth.Narrow, plain.WidthOf(1));
    }

    /// <summary>
    /// A line alone in its list dropped in the slot next to it moves there, its list staying behind
    /// empty, as into a slot further out (the person, 2026-10-12: "not every column in the folder edit
    /// view is a drop target" — that slot was dead) — and the landing the folder shows agrees with
    /// the move every time.
    /// </summary>
    [Fact]
    public void TheLandingShownInASlotIsWhereTheMoveChangesSomething()
    {
        foreach (var shape in new[] { "a", "a | b", " | a", "a b | c" })
        {
            var count = Lists(shape).Lists.Count;
            foreach (var id in Lists(shape).Cells.Select(cell => cell.Island.Id))
            {
                foreach (var want in new[] { FolderPlace.Slot(0, 0), FolderPlace.Slot(0, 1), FolderPlace.Slot(count, 0), FolderPlace.Slot(count, 2) })
                {
                    var folder = Lists(shape);
                    var cell = Of(folder, id);
                    // A slot always moves the line: the list it leaves stays, so even the next slot is elsewhere.
                    Assert.True(Karpo.Core.Folders.Move(folder, cell, want));
                }
            }
        }
        var lone = Lists("a | b");
        Assert.True(Karpo.Core.Folders.Move(lone, Of(lone, "b"), FolderPlace.Slot(2, 0)));
        Assert.Equal("a |  | b", Shown(lone));
        Assert.True(Karpo.Core.Folders.Move(lone, Of(lone, "a"), FolderPlace.Slot(0, 0)));
        Assert.Equal("a |  |  | b", Shown(lone));
        var further = Lists("a | b");
        Assert.True(Karpo.Core.Folders.Move(further, Of(further, "b"), FolderPlace.Slot(2, 1)));
        Assert.Equal("a |  |  | b", Shown(further));
    }

    // --- The open folder -----------------------------------------------------------------------

    /// <summary>"box": the clock and the battery in the first list, the airplane switch in the second.</summary>
    private static BarItem TwoLists()
    {
        var island = List();
        var airplane = ListKind.AirplaneMode.New();
        airplane.Id = "airplane";
        airplane.View = FolderView.List;
        island.Folder!.Put(airplane, list: 1);
        return island;
    }

    /// <summary>"box": the clock and the battery in a list between an empty narrow list and an empty wide one.</summary>
    private static BarItem BetweenEmpties()
    {
        var island = List();
        island.Folder!.Lists.Insert(0, []);
        island.Folder.Lists.Add([]);
        island.Folder.Widths = [FolderListWidth.Narrow, FolderListWidth.Medium, FolderListWidth.Wide];
        return island;
    }

    private static POINT Middle(FolderFlyout flyout, D2D_RECT_F box, float y)
    {
        var on = flyout.PanelOnScreen;
        return new POINT { x = on.left + (int)MathF.Round((box.left + box.right) / 2), y = on.top + (int)MathF.Round(y) };
    }

    /// <summary>
    /// Arranged, the folder's window reaches the work area's edges on every side but the bar's, and
    /// only its own things take a press: the lists, a "+" — a slot's too — while the room out there
    /// lets it through to what is under it. At rest the window is the panel's and lets nothing through.
    /// </summary>
    [UiFact]
    public void TheArrangedWindowSpansTheScreenAndLetsTheRoomThrough()
    {
        WindowShots.OnHiddenDesktop("folder-slots-window", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", TwoLists(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            RECT window;
            window = WindowShots.Extent(flyout.Hwnd);
            Assert.Equal((Work.left, Work.top, Work.right, Anchor.y), (window.left, window.top, window.right, window.bottom));
            var plan = flyout.Plan!;
            Assert.NotEmpty(plan.Slots);
            Assert.Contains(plan.Slots, slot => slot.Place == FolderPlace.Slot(0, 0));
            Assert.Contains(plan.Slots, slot => slot.Place == FolderPlace.Slot(2, 0));

            Assert.True(flyout.PassesThrough(new POINT { x = 40, y = 40 }), "the room far from the folder");
            Assert.False(flyout.PassesThrough(flyout.ScreenOfCard(0)), "a line");
            var sizer = plan.Adds.Select((add, index) => (add, index)).First(entry => entry.add.Place.New).index;
            Assert.False(flyout.PassesThrough(flyout.ScreenOfAdd(sizer)), "a slot's width button");
            Assert.True(flyout.PassesThrough(Middle(flyout, plan.Slots[0].Box, -300)), "a slot away from its width button");
            flyout.Hide();

            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: false);
            window = WindowShots.Extent(flyout.Hwnd);
            Assert.True(window.right - window.left < Work.right / 2, "at rest the window is the panel's");
            Assert.Empty(flyout.Plan!.Slots);
            Assert.False(flyout.PassesThrough(new POINT { x = 40, y = 40 }));
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// The guide lines run down the display to the bars' edges and no further (§246; the person,
    /// 2026-10-22: "the column lines in edit mode still go over the taskbar, this should not be";
    /// §287): arranged, the window is the display's, under the bars, and every line reaches the far
    /// edge of the work area the dock hands over — the display less every bar of it, the folder's own
    /// too (DockWindow.FolderWork) — and the bar's edge, never into its strip; the buttons stay in the
    /// work area too, and a press on the bar goes through the folder to it (the bar, locked, keeps it).
    /// </summary>
    [UiTheory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void TheGuideLinesEndAtTheBars(Edge edge)
    {
        WindowShots.OnHiddenDesktop("folder-slots-through", (gfx, instance) =>
        {
            var display = new RECT { right = 1920, bottom = 1080 };
            var (anchor, work, onBar) = edge switch
            {
                Edge.Top => (new POINT { x = 960, y = 48 }, display with { top = 48 }, new POINT { x = 300, y = 20 }),
                Edge.Left => (new POINT { x = 48, y = 540 }, display with { left = 48 }, new POINT { x = 20, y = 300 }),
                Edge.Right => (new POINT { x = 1872, y = 540 }, display with { right = 1872 }, new POINT { x = 1900, y = 300 }),
                _ => (new POINT { x = 960, y = 1032 }, display with { bottom = 1032 }, new POINT { x = 300, y = 1060 }),
            };
            var folder = new FolderIsland($"{Owner}/box", TwoLists(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, anchor, work, edge, folder, editing: true, display: display);
            WindowShots.Settle(150);
            RECT window;
            window = WindowShots.Extent(flyout.Hwnd);
            Assert.Equal((display.left, display.top, display.right, display.bottom), (window.left, window.top, window.right, window.bottom));

            var lines = flyout.GuidesOnScreen;
            Assert.NotEmpty(lines);
            foreach (var x in lines.Select(line => line.left).Distinct())
            {
                var at = lines.Where(line => line.left == x).ToList();
                Assert.Contains(at, line => line.top == work.top);
                Assert.Contains(at, line => line.bottom == work.bottom);
            }
            // None on the bar: every line inside the work area, which leaves the bar's strip out.
            Assert.All(lines, line => Assert.True(line.left >= work.left && line.right <= work.right && line.top >= work.top && line.bottom <= work.bottom,
                $"a line {line.left},{line.top}..{line.right},{line.bottom} runs onto the bar"));
            // The buttons off the bar, in the work area; a press on the bar is the bar's.
            for (var index = 0; index < flyout.Plan!.Adds.Count; index++)
            {
                var button = flyout.ScreenOfAdd(index);
                Assert.True(button.x >= work.left && button.x < work.right && button.y >= work.top && button.y < work.bottom, $"button {index} at {button.x},{button.y}");
                Assert.False(flyout.PassesThrough(button));
            }
            Assert.True(flyout.PassesThrough(onBar), "the bar under the folder");
            Assert.True(flyout.PassesThrough(anchor with { x = edge is Edge.Left or Edge.Right ? anchor.x + (edge == Edge.Left ? -10 : 10) : anchor.x, y = edge is Edge.Top or Edge.Bottom ? anchor.y + (edge == Edge.Top ? -10 : 10) : anchor.y }), "the bar by the folder's button");
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// Where everything of the open, arranged folder stands on the screen: its window, each list's
    /// panel, each slot and each button — what the folder shows after a change, and what opening it
    /// again shows.
    /// </summary>
    private static string Standing(FolderFlyout flyout)
    {
        RECT window;
        window = WindowShots.Extent(flyout.Hwnd);
        var on = flyout.PanelOnScreen;
        var plan = flyout.Plan!;
        string Box(D2D_RECT_F box) => $"{on.left + box.left:0},{on.top + box.top:0},{on.left + box.right:0},{on.top + box.bottom:0}";
        return $"window {window.left},{window.top},{window.right},{window.bottom}; lists "
            + string.Join(" ", plan.Lists.Select(list => Box(list.Box)))
            + "; slots " + string.Join(" ", plan.Slots.Select(slot => $"{slot.Place.List}/{slot.Place.Apart}@{on.left + slot.Box.left:0}"))
            + "; buttons " + string.Join(" ", plan.Adds.Select(add => $"{add.Kind}:{Box(add.Box)}"));
    }

    /// <summary>
    /// The airplane carried into the slot left of the folder, high up over the slot: it is a list
    /// there, its ghost in the slot meanwhile; laid out for the move (as the dock does it) the folder
    /// stands at once as opening it again shows it — centred on its button, the list it left gone
    /// (§328) — and not where it stood before (the person, 2026-10-12: "the column does not
    /// immediately shift, you have to reopen the layout").
    /// </summary>
    [UiFact]
    public void ALineDroppedInASlotIsLaidOutAsReopeningShowsIt()
    {
        WindowShots.OnHiddenDesktop("folder-slots-drop", (gfx, instance) =>
        {
            var item = TwoLists();
            var config = new Config();
            var folder = new FolderIsland($"{Owner}/box", item, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            var plan = flyout.Plan!;
            var slot = plan.Slots.Single(each => each.Place == FolderPlace.Slot(0, 0));
            var slop = GetSystemMetrics(SM.SM_CYDRAG);

            var press = flyout.ScreenOfCard(2);
            var over = Middle(flyout, slot.Box, -200);
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y - slop - 2 }));
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, over)).Kind);
            Assert.Null(flyout.Aimed);
            var placed = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, over));
            Assert.Equal(FolderFlyoutAction.Place, placed.Kind);
            Assert.Equal($"{Owner}/box/airplane", placed.Widget?.Key);
            Assert.Equal(FolderPlace.Slot(0, 0), placed.Place);

            Assert.True(Karpo.Core.Folders.Move(item.Folder!, Karpo.Core.Folders.CellOf(item.Folder!, "airplane")!, placed.Place));
            folder.Configure(item, config);
            Assert.Equal(2, flyout.Plan!.Lists.Count);
            var after = Standing(flyout);
            // Its lists with widgets centred, the emptied one gone (§328).
            var on = flyout.PanelOnScreen;
            var core = flyout.Plan.Core;
            Assert.Equal((flyout.Plan.Lists[0].Left, flyout.Plan.Lists[1].Box.right), (core.left, core.right));
            Assert.InRange(on.left + ((core.left + core.right) / 2) - Anchor.x, -1, 1);
            flyout.Hide();
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            Assert.Equal(Standing(flyout), after);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// A widget added — into a slot on either side, near or further out, into a list with widgets,
    /// into an empty one — or a slot's width picked, as the dock does each: the open folder stands at
    /// once exactly as opening it again shows it, its lists, slots, guide lines' places and buttons
    /// (the person, 2026-10-12: "when adding a widget to a column the column does not immediately
    /// shift, you have to reopen the layout"; the left edge held through a layout was why).
    /// </summary>
    [UiFact]
    public void AChangeIsLaidOutAsReopeningShowsIt()
    {
        WindowShots.OnHiddenDesktop("folder-slots-add", (gfx, instance) =>
        {
            var anchor = Anchor with { x = 1600 };
            var work = Work with { right = 2560 };
            foreach (var change in new[] { "slot-left-1", "slot-right-0", "list-0", "empty", "slot-width" })
            {
                var item = TwoLists();
                if (change == "empty")
                {
                    item.Folder!.Lists.Add([]);
                }
                var config = new Config();
                var folder = new FolderIsland($"{Owner}/box", item, gfx, config);
                using var flyout = new FolderFlyout(gfx, instance, 0);
                flyout.Show(gfx, Dark, 1f, anchor, work, Edge.Bottom, folder, editing: true);
                var before = Standing(flyout);
                var lists = item.Folder!.Lists.Count;
                var want = change switch
                {
                    "slot-left-1" => FolderPlace.Slot(0, 1),
                    "slot-right-0" or "slot-width" => FolderPlace.Slot(lists, 0),
                    "list-0" => new FolderPlace(0, 0),
                    _ => new FolderPlace(2, 0),
                };
                if (change == "slot-width")
                {
                    // Pressed as the person presses it: the slot's width button.
                    var sizer = flyout.Plan!.Adds.Select((add, index) => (add, index)).First(entry => entry.add.Place == want && entry.add.Kind == FolderAddKind.Width).index;
                    var at = Client(flyout.Hwnd, flyout.ScreenOfAdd(sizer));
                    _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, at);
                    var asked = flyout.Message(WM.WM_LBUTTONUP, 0, at);
                    Assert.Equal(FolderFlyoutAction.Width, asked.Kind);
                    Assert.Equal(want, asked.Place);
                    Assert.True(Karpo.Core.Folders.SetSlotWidth(item.Folder!, asked.Place, FolderListWidth.Wide));
                }
                else
                {
                    // A line of one row: an empty list's room is three, a clock's line as tall — where
                    // the edit bar adds it, or a line carried there (a slot) puts it.
                    var line = ListKind.AirplaneMode.New();
                    line.Id = "second-airplane";
                    line.View = FolderView.List;
                    Assert.NotNull(Karpo.Core.Folders.InsertLine(item.Kind, item.Folder!, line, want));
                }
                folder.Configure(item, config);
                flyout.MenuClosed();
                var after = Standing(flyout);
                Assert.True(before != after, $"{change}: {before}");
                flyout.Hide();
                flyout.Show(gfx, Dark, 1f, anchor, work, Edge.Bottom, folder, editing: true);
                var reopened = Standing(flyout);
                Assert.True(reopened == after, $"{change}: {after} | reopened: {reopened}");
                flyout.Hide();
                folder.Dispose();
            }
        });
    }

    // The folder whose window's messages the routed class hands it (RoutedClass).
    private static FolderFlyout? routed;

    /// <summary>A window class whose messages go to <see cref="routed"/> first, as the dock's window procedure hands a folder its own.</summary>
    private static string RoutedClass(HINSTANCE instance)
    {
        const string name = "karpo.test.folder.routed";
        fixed (char* className = name)
        {
            var type = new WNDCLASSW { lpfnWndProc = &Routed, hInstance = instance, lpszClassName = className };
            _ = RegisterClassW(&type);
        }
        return name;
    }

    // Whether the dock is busy handling a message of its own (Dock's window procedure): nothing reaches
    // the folder then — a menu closing in that handling gives the folder the focus back.
    private static bool busy;

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static LRESULT Routed(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (routed is { } flyout && flyout.Hwnd == hwnd)
        {
            // Its place in the z-order, busy or not, as Dock.WndProc keeps every window's (Surfaces.Keep).
            if (msg == WM.WM_WINDOWPOSCHANGING)
            {
                Surfaces.Keep(hwnd, (WINDOWPOS*)(nint)lParam);
            }
            if (!busy)
            {
                _ = flyout.Message(msg, wParam, lParam);
            }
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    /// <summary>
    /// The edit bar stands over the arranged folder whatever moves the folder's window (the person,
    /// 2026-10-24: "the top edit bar sometimes clips behind the folder column lines"; §342): opened,
    /// laid out again, activated by a click, given the focus back by a menu closing inside the dock's
    /// own handling of a message — where nothing of the dock's reaches the folder —, raised by
    /// anything, the edit bar shown again, stepping aside and back, and another folder opened in its
    /// place. The bar stays over it too, and the folder over its dim.
    /// </summary>
    [UiFact]
    public void TheEditBarStaysOverTheArrangedFolder()
    {
        WindowShots.OnHiddenDesktop("folder-under-edit-bar", (gfx, instance) =>
        {
            var bounds = new RectI(0, 0, 1920, 1080);
            Karpo.Tests.Signals.PreviewToolbarTests.Register(Preview.ClassName, instance);
            using var shade = new EditShade(gfx, instance, 0);
            shade.Show(bounds, dimmed: true, Motion.Still);
            var bar = Bar();
            using var deck = new EditDeck(gfx, instance, 0);
            var room = new EditDeckRoom(bounds, 0, 48);
            var look = Dark with { Animations = false };
            deck.Show(gfx, look, 1f, room, EditDeckState.Plain with { Folder = new EditDeckSubject(OptionGlyphs.FolderOpen, "box") });
            // A menu's stand-in, which takes the focus as a menu does.
            var menu = Activating();
            var display = new RECT { right = bounds.Right, bottom = bounds.Bottom };
            FolderFlyout? flyout = null;
            FolderIsland? folder = null;
            try
            {
                var item = TwoLists();
                var config = new Config();
                FolderFlyout Open(BarItem what)
                {
                    folder?.Dispose();
                    flyout?.Dispose();
                    folder = new FolderIsland($"{Owner}/box", what, gfx, config);
                    var opened = new FolderFlyout(gfx, instance, 0, RoutedClass(instance));
                    routed = opened;
                    opened.Show(gfx, look, 1f, Anchor, Work, Edge.Bottom, folder, editing: true, display: display);
                    return opened;
                }
                void Stands(string when)
                {
                    Assert.True(IsWindowVisible(flyout!.Hwnd) && deck.IsOpen, when);
                    Assert.True(Z.Above(deck.Hwnd, flyout.Hwnd), $"{when}: the folder over the edit bar");
                    Assert.True(Z.Above(bar, flyout.Hwnd), $"{when}: the folder over the bar");
                    Assert.True(Z.Above(flyout.Hwnd, shade.Hwnd), $"{when}: the folder under its dim");
                }

                flyout = Open(item);
                Stands("opened");
                // Active, for the keys, all the same (FlyoutHost.Open).
                Assert.Equal(flyout.Hwnd, GetActiveWindow());

                // Laid out again for a line added.
                var clock = ListKind.Clock.New();
                clock.Id = "second-clock";
                Assert.NotNull(Karpo.Core.Folders.InsertLine(item.Kind, item.Folder!, clock, FolderPlace.Slot(2, 0)));
                folder!.Configure(item, config);
                Stands("laid out again");

                // A menu over it takes the focus; a click on the folder takes it back.
                _ = SetActiveWindow(menu);
                _ = SetActiveWindow(flyout.Hwnd);
                Stands("activated by a click");

                // A menu over it closes inside the dock's handling of a message (a pick, Escape, the
                // edit bar's buttons) and gives the folder the focus back (FlyoutHost.Close's restore),
                // the dock still busy.
                _ = SetActiveWindow(menu);
                busy = true;
                try
                {
                    _ = SetActiveWindow(flyout.Hwnd);
                }
                finally
                {
                    busy = false;
                }
                Stands("given the focus back by a menu");

                // Anything that raises it, with no activation at all.
                _ = SetWindowPos(flyout.Hwnd, HWND.HWND_TOPMOST, 0, 0, 0, 0, SWP.SWP_NOMOVE | SWP.SWP_NOSIZE | SWP.SWP_NOACTIVATE);
                Stands("raised");

                // The edit bar shown again — its subject changed (EditChrome).
                deck.Show(gfx, look, 1f, room, EditDeckState.Plain with { Folder = new EditDeckSubject(OptionGlyphs.FolderOpen, "box, renamed") });
                Stands("the edit bar shown again");

                // The bar lifted to the top by anything.
                _ = SetWindowPos(bar, HWND.HWND_TOPMOST, 0, 0, 0, 0, SWP.SWP_NOMOVE | SWP.SWP_NOSIZE | SWP.SWP_NOACTIVATE);
                Stands("the bar lifted");

                // Another folder opened in its place (§289).
                flyout = Open(TwoLists());
                Stands("another folder opened");
            }
            finally
            {
                routed = null;
                flyout?.Dispose();
                folder?.Dispose();
                _ = DestroyWindow(menu);
                Surfaces.Destroy(bar);
            }
        });

        // The dock's window procedure keeps it there as Routed does, before the busy dock turns
        // anything away; nothing puts it back after a move any more.
        var repo = RepoSource.Root();
        var procedure = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.cs"));
        var stands = procedure.IndexOf("Surfaces.Keep(hwnd, (WINDOWPOS*)(nint)lParam);", StringComparison.Ordinal);
        Assert.True(stands > 0 && stands < procedure.IndexOf("if (!dock.busy)", StringComparison.Ordinal));
        Assert.DoesNotContain("Restand", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "FlyoutHost.cs")));
    }

    /// <summary>A plain topmost window in sight that takes the focus, as a menu does.</summary>
    private static HWND Activating()
    {
        fixed (char* kind = "STATIC")
        {
            var window = CreateWindowExW(
                WS.WS_EX_TOOLWINDOW | WS.WS_EX_TOPMOST, kind, null, WS.WS_POPUP | WS.WS_VISIBLE,
                0, 0, 100, 100, HWND.NULL, HMENU.NULL, HINSTANCE.NULL, null);
            Assert.True(window != HWND.NULL);
            return window;
        }
    }

    /// <summary>A bar's stand-in: a plain topmost window in sight, in the bars' stratum (ZPlan, §359).</summary>
    private static HWND Bar()
    {
        fixed (char* kind = "STATIC")
        {
            var window = CreateWindowExW(
                WS.WS_EX_TOOLWINDOW | WS.WS_EX_TOPMOST | WS.WS_EX_NOACTIVATE, kind, null, WS.WS_POPUP,
                0, 0, 100, 100, HWND.NULL, HMENU.NULL, HINSTANCE.NULL, null);
            Assert.True(window != HWND.NULL);
            Surfaces.Enlist(window, Stratum.Bar);
            Surfaces.Show(window);
            return window;
        }
    }

    /// <summary>A plain topmost window in sight, over the others, for the z-order: a dim's or another app's stand-in.</summary>
    private static HWND Topmost()
    {
        fixed (char* kind = "STATIC")
        {
            var window = CreateWindowExW(
                WS.WS_EX_TOOLWINDOW | WS.WS_EX_TOPMOST | WS.WS_EX_NOACTIVATE, kind, null, WS.WS_POPUP | WS.WS_VISIBLE,
                0, 0, 100, 100, HWND.NULL, HMENU.NULL, HINSTANCE.NULL, null);
            Assert.True(window != HWND.NULL);
            _ = SetWindowPos(window, HWND.HWND_TOPMOST, 0, 0, 0, 0, SWP.SWP_NOMOVE | SWP.SWP_NOSIZE | SWP.SWP_NOACTIVATE);
            return window;
        }
    }

    /// <summary>
    /// Arranged, the folder's window — the whole screen — stands on its dim, at the level of edit mode's own
    /// windows (the person, 2026-10-12: "the overlay that shows the individual lines is infront of the
    /// configuration window, it should just be on the same level as the other stuff"), and under the bars
    /// (2026-10-22: "the taskbar still is behind the folder edit view. this should not be"; §279): shown,
    /// laid out again, a bar lifted to the top by anything. Its stratum's place (ZPlan, §359), nothing of
    /// the folder's own computing it. At rest it is a popup, over the bars.
    /// </summary>
    [UiFact]
    public void TheArrangedFolderStandsUnderTheBars()
    {
        WindowShots.OnHiddenDesktop("folder-slots-level", (gfx, instance) =>
        {
            var dim = Topmost();
            Surfaces.Enlist(dim, Stratum.Dim);
            Surfaces.Show(dim);
            var bar = Bar();
            try
            {
                Assert.True(Z.Above(bar, dim));
                var item = TwoLists();
                var config = new Config();
                var folder = new FolderIsland($"{Owner}/box", item, gfx, config);
                // Its messages come to it, as the dock's window procedure hands them over.
                using var flyout = new FolderFlyout(gfx, instance, 0, RoutedClass(instance));
                routed = flyout;
                flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
                Assert.True(Z.Above(bar, flyout.Hwnd), "under the bar");
                Assert.True(Z.Above(flyout.Hwnd, dim), "over its dim");

                // Laid out again for a change: where it was.
                var clock = ListKind.Clock.New();
                clock.Id = "second-clock";
                Assert.NotNull(Karpo.Core.Folders.InsertLine(item.Kind, item.Folder!, clock, FolderPlace.Slot(2, 0)));
                folder.Configure(item, config);
                Assert.True(Z.Above(bar, flyout.Hwnd) && Z.Above(flyout.Hwnd, dim));

                // The bar lifted to the top by anything: still over the folder.
                _ = SetWindowPos(bar, HWND.HWND_TOPMOST, 0, 0, 0, 0, SWP.SWP_NOMOVE | SWP.SWP_NOSIZE | SWP.SWP_NOACTIVATE);
                Assert.True(Z.Above(bar, flyout.Hwnd));

                // Raised or activated it stays where it stands (TheEditBarStaysOverTheArrangedFolder).
                flyout.Hide();

                // At rest: a popup, over the bars.
                flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: false);
                Assert.True(Z.Above(flyout.Hwnd, bar), "at rest, over the bars");
                flyout.Hide();
                folder.Dispose();
            }
            finally
            {
                routed = null;
                Surfaces.Destroy(bar);
                Surfaces.Destroy(dim);
            }
        });

        // The dock's folder stands where its stratum puts it, nothing of its own or the dock's computing
        // it (§359: StandsUnder, Floor and EditStack gone), and nothing puts the bars back under it any more
        // (§247's KeepBarsUnderFolder, gone with §279).
        var repo = RepoSource.Root();
        var folders = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.Folders.cs"));
        Assert.DoesNotContain("StandsUnder", folders);
        Assert.False(File.Exists(Path.Combine(repo, "Karpo.Dock", "EditStack.cs")));
        // Its guide lines end at the bars (§287): placed in the work area less its own bar too, and no
        // bar draws them over itself any more (§279's Scene.Guides, gone).
        Assert.Contains("window.FolderWork(),", folders);
        Assert.DoesNotContain("GuidesMoved", folders);
        Assert.False(File.Exists(Path.Combine(repo, "Karpo.Dock", "Scene.Guides.cs")));
        Assert.DoesNotContain("KeepBarsUnderFolder", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.EditDeck.cs")));
    }

    // --- Pictures --------------------------------------------------------------------------------

    /// <summary>
    /// The arranged folder as the compositor draws it, the window being the work area: a folder of
    /// one list, of two, of two with a line carried over the first slot on the right, and of a list
    /// between an empty narrow one and an empty wide one (slots of their widths, no panels) — over a
    /// bottom bar and beside a left one, at 100 and 150 %, dark and light. Looked at by hand:
    /// folder-slots-&lt;bottom|left&gt;-&lt;100|150&gt;-&lt;dark|light&gt;.png.
    /// </summary>
    [UiFact]
    public void TheSlotsArePictured()
    {
        WindowShots.OnHiddenDesktop("folder-slots-pictures", (gfx, instance) =>
        {
            foreach (var edge in new[] { Edge.Bottom, Edge.Left })
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    foreach (var dark in new[] { true, false })
                    {
                        var work = new RECT { right = (int)(1280 * scale), bottom = (int)(720 * scale) };
                        var anchor = edge == Edge.Bottom ? new POINT { x = work.right / 2, y = work.bottom } : new POINT { x = 0, y = work.bottom / 2 };
                        var pictures = new List<(byte[] Canvas, int Width, int Height)>();
                        foreach (var shape in new[] { 1, 2, 3, 4 })
                        {
                            var item = shape == 1 ? List() : shape == 4 ? BetweenEmpties() : TwoLists();
                            var folder = new FolderIsland($"{Owner}/box", item, gfx, new Config());
                            using var flyout = new FolderFlyout(gfx, instance, 0);
                            flyout.Show(gfx, dark ? Dark : Light, scale, anchor, work, edge, folder, editing: true);
                            if (shape == 3)
                            {
                                var plan = flyout.Plan!;
                                var slot = plan.Slots.First(each => each.Place.List == plan.Lists.Count);
                                var press = flyout.ScreenOfCard(0);
                                _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
                                _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y - GetSystemMetrics(SM.SM_CYDRAG) - 2 }));
                                _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, Middle(flyout, slot.Box, plan.Height - (60 * scale))));
                            }
                            WindowShots.Settle(250);
                            // The capture's clear room comes out black: a light theme's guide lines, dark ink, do not show in it.
                            Assert.True(WindowShots.Save(flyout.Hwnd, "folder-slots-frame.png", seen: (pixels, width) => pictures.Add((pixels, width, pixels.Length / 4 / width))));
                            flyout.Hide();
                            folder.Dispose();
                        }
                        WindowShots.SaveSideBySide(
                            pictures,
                            $"folder-slots-{(edge == Edge.Bottom ? "bottom" : "left")}-{scale * 100:0}-{(dark ? "dark" : "light")}.png",
                            (0x80, 0x40, 0x40),
                            gap: 12);
                    }
                }
            }
        });
    }
}
