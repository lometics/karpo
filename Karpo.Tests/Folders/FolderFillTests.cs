// Fill (the person, 2026-10-14: "a new size that you can give a widget which is grow / take up
// remaining space which makes it grow to fill the full space ... only in the folder i meant"; §168),
// and then "fill remaining space should actually fill all the remaining space ... the notifications
// should go up to the top of the screen" (§173): a folder with a Fill line is as tall as the room it
// has beside the bar, to the work area's far end less the margin (FlyoutPlacement.Room), and a list
// with Fill lines shares what it has left at that height among them equally — no kind has a most of
// its own since §192 — and the other lists keep their heights. Since §185 every folder may reach that
// room (FolderFlyout.Reach), and since §320 all of it: no row is kept back for an arranged list's
// buttons, which stand inside a list as tall as the room. Held here:
//
//  - **The plan** (pure): the share, a fixed kind filling as any other, the room and the lines left
//    out under it, several lists, a list that changes beside it, rest and arranged the same, the bar's
//    sides, and a Fill line in a list past the room giving way, to one row, before any line is left
//    out (§171).
//  - **The open folder**: a month and an agenda filling a list beside a shorter one, at 100 and 150 %,
//    in both themes, at rest and arranged, up to the placement's room, and a widget added to the
//    other list leaving them as they are.
//    Pictures: folder-fill-<dark|light>-<scale>-<rest|edit>.png in KARPO_PNG_DIR.
//
// The open folder's half needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed class FolderFillTests
{
    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));

    /// <summary>A line of <paramref name="kind"/> whose height is Fill.</summary>
    private static (ListKind Kind, bool Fill) F(ListKind kind) => (kind, true);

    /// <summary>A folder of lists, each its lines of <paramref name="lists"/>.</summary>
    private static FolderOptions Lists(params (ListKind Kind, bool Fill)[][] lists)
    {
        var options = new FolderOptions { Lists = [] };
        for (var list = 0; list < lists.Length; list++)
        {
            options.Lists.Add([]);
            foreach (var (kind, fill) in lists[list])
            {
                var line = kind.New();
                if (fill)
                {
                    line.Height = LineHeight.For(line).As(LineHeightMode.Fill);
                }
                options.Put(line, list);
            }
        }
        return options;
    }

    /// <summary><paramref name="count"/> lines of one row (40 px at 100 %), none Fill.</summary>
    private static (ListKind, bool)[] Lines(int count) => [.. Enumerable.Repeat((ListKind.EnergySaver, false), count)];

    private static FolderPlan Plan(FolderOptions options, float work = 1040, bool editing = false, Edge edge = Edge.Bottom, float room = 0) =>
        FolderFlyout.Build(Dark, 1f, FolderView.List, options, options.Cells, work, editing: editing, edge: edge, room: room);

    /// <summary>The room a folder drawn without a placement reaches on a <paramref name="work"/> px work area at 100 %: less the gap and the far margin, 12 px each, and nothing more (§320).</summary>
    private static float Room(float work) => work - 24;

    /// <summary>A count of rows in px at 100 %, as the plan lays them out: two cells each (§442).</summary>
    private static float Rows(int rows) => rows * LineHeight.Row * FolderOptions.CellDip;

    private static float Tall(FolderPlan plan, int index) => plan.Cards[index].bottom - plan.Cards[index].top;

    /// <summary>The indices of list <paramref name="list"/>'s lines in the plan, in its order.</summary>
    private static List<int> Of(FolderPlan plan, int list) => [.. Enumerable.Range(0, plan.Cards.Count).Where(index => plan.CardList[index] == list)];

    /// <summary>
    /// Two Fill lines share what their list has left under the room equally: the list is then the
    /// room, each line its content and half what was left; the list beside it keeps its height.
    /// </summary>
    [Fact]
    public void FillLinesShareTheRoomLeftEqually()
    {
        var options = Lists(Lines(14), [F(ListKind.Calendar), F(ListKind.Mail)]);
        var plan = Plan(options);
        var fills = Of(plan, 1);

        Assert.Equal(14 * 40, plan.Lists[0].Height, 1);
        Assert.Equal(Room(1040), plan.Lists[1].Height, 1);
        Assert.Equal(Room(1040), plan.Height, 1);
        var room = Room(1040) - (Rows(6) + Rows(4));
        Assert.Equal(Rows(6) + (room / 2), Tall(plan, fills[0]), 1);
        Assert.Equal(Rows(4) + (room / 2), Tall(plan, fills[1]), 1);
        Assert.Equal(plan.Cards[fills[0]].bottom, plan.Cards[fills[1]].top, 1);
    }

    /// <summary>A line not Fill keeps its height; only the Fill ones of a list grow.</summary>
    [Fact]
    public void OnlyTheFillLinesGrow()
    {
        var options = Lists(Lines(14), [(ListKind.Mail, false), F(ListKind.Calendar), (ListKind.EnergySaver, false)]);
        var plan = Plan(options);
        var lines = Of(plan, 1);

        Assert.Equal(Rows(4), Tall(plan, lines[0]), 1);
        Assert.Equal(40, Tall(plan, lines[2]), 1);
        Assert.Equal(Room(1040), plan.Lists[1].Height, 1);
        Assert.Equal(plan.Lists[1].Height - Rows(4) - 40, Tall(plan, lines[1]), 1);
    }

    /// <summary>
    /// A kind whose content does not vary fills as any other (§192: a day view stopped at the rows its kind
    /// had, a month at ten): two such Fill lines take the room left, the list the room, nothing left empty at
    /// its end. (An energy saver was one; a switch has no Height since §432.)
    /// </summary>
    [Fact]
    public void AFixedKindFillsTheRoomAsAnyOther()
    {
        var options = Lists(Lines(16), [F(ListKind.DayView), F(ListKind.Month)]);
        var plan = Plan(options);
        var fills = Of(plan, 1);

        Assert.Equal(Room(1040), plan.Lists[1].Height, 1);
        Assert.True(Tall(plan, fills[0]) > Rows(3) + 100, $"{Tall(plan, fills[0])} px");
        Assert.True(Tall(plan, fills[1]) > Rows(10), $"{Tall(plan, fills[1])} px");
        Assert.Equal(plan.Lists[1].Height, Tall(plan, fills[0]) + Tall(plan, fills[1]), 1);
    }

    /// <summary>On a tall screen a Fill line reaches its top, past the 32 rows a line could once be set to.</summary>
    [Fact]
    public void AFillLineFillsATallScreen()
    {
        var plan = Plan(Lists([F(ListKind.Month), (ListKind.Clock, false)]), work: 2112);

        Assert.Equal(Room(2112), plan.Height, 1);
        Assert.True(Tall(plan, 0) > Rows(32), $"{Tall(plan, 0)} px");
    }

    /// <summary>
    /// A line set taller than the room (Settings knows no screen) is given what the lines over it leave
    /// of the room and no more, and the lines under it are left out as any line past the room is — it
    /// never leaves itself out (§192). First in its list, it is the whole room.
    /// </summary>
    [Fact]
    public void ALineSetPastTheRoomIsGivenTheRoom()
    {
        var options = Lists([(ListKind.EnergySaver, false), (ListKind.DayView, false), (ListKind.Mail, false)]);
        options.Cells[1].Island.Height = LineHeight.Exactly(300);
        var plan = Plan(options);

        Assert.Equal(Room(1040), plan.Height, 1);
        Assert.Equal(Room(1040) - 40, Tall(plan, 1), 1);
        Assert.Equal([2], plan.Lists[0].Hidden);

        var first = Lists([(ListKind.DayView, false), (ListKind.Mail, false)]);
        first.Cells[0].Island.Height = LineHeight.Exactly(300);
        var alone = Plan(first);
        Assert.Equal(Room(1040), Tall(alone, 0), 1);
        Assert.Equal([1], alone.Lists[0].Hidden);
    }

    /// <summary>
    /// A folder with a Fill line and one without reach the same room (§185): a list beside the Fill
    /// line leaves out what it would leave out without it, under the room, and the Fill line is the room.
    /// </summary>
    [Fact]
    public void AFillLineFillsTheRoomEveryFolderReaches()
    {
        var plan = Plan(Lists(Lines(30), [F(ListKind.Calendar)]), work: 600);
        var without = Plan(Lists(Lines(30), [(ListKind.Calendar, false)]), work: 600);

        Assert.NotEmpty(plan.Lists[0].Hidden);
        Assert.Equal(without.Lists[0].Hidden, plan.Lists[0].Hidden);
        Assert.True(plan.Lists[0].Height <= Room(600));
        Assert.Equal(Room(600), plan.Lists[1].Height, 1);
        Assert.Equal(Room(600), plan.Height, 1);
        Assert.Equal(plan.Lists[0].Height, without.Height, 1);
    }

    /// <summary>The room a placement has is what the folder is laid out against, where one is given, on any bar.</summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    public void TheRoomGivenIsWhatIsFilled(Edge edge)
    {
        var plan = Plan(Lists([F(ListKind.Calendar)], Lines(3)), edge: edge, room: 700);

        Assert.Equal(700, plan.Height, 1);
        Assert.Equal(700, plan.Lists[0].Height, 1);
        Assert.Equal(3 * 40, plan.Lists[1].Height, 1);
        // Lined up on the bar's side: on a top bar the tops, on a bottom bar the bottoms, beside a side bar the middles (§303).
        var left = 700 - plan.Lists[1].Height;
        Assert.Equal(edge switch { Edge.Top => 0, Edge.Left => MathF.Round(left / 2), _ => left }, plan.Lists[1].Box.top, 1);
    }

    /// <summary>
    /// A placement's room: from the anchor and its gap to the work area's far end less the margin,
    /// or the height less a margin at either end beside a side bar.
    /// </summary>
    [Fact]
    public void APlacementsRoomReachesTheFarEnd()
    {
        var work = new RECT { left = 0, top = 0, right = 1920, bottom = 1032 };
        Assert.Equal(1032 - 12 - 12, new FlyoutPlacement(new POINT { x = 1900, y = 1032 }, work, Edge.Bottom, Align: FlyoutAlign.End).Room(1));
        Assert.Equal(1032 - 12 - 12, new FlyoutPlacement(new POINT { x = 960, y = 1032 }, work, Edge.Bottom).Room(1));
        Assert.Equal(1032 - 18 - 18, new FlyoutPlacement(new POINT { x = 960, y = 0 }, work, Edge.Top).Room(1.5f));
        Assert.Equal(1032 - 24, new FlyoutPlacement(new POINT { x = 0, y = 500 }, work, Edge.Left).Room(1));
        Assert.Equal(1032 - 18 - 18, FlyoutPlacement.Room(1032, 1.5f));
    }

    /// <summary>
    /// A Fill line in a list past the room gives way before any line is left out (§171): the
    /// notifications over the calendar, the calendar never pushed off. The list is then the room, the
    /// Fill line what the others leave, and nothing goes that Automatic would have left out.
    /// </summary>
    [Fact]
    public void AFillLineInAnOverfullListGivesWayFirst()
    {
        var plan = Plan(Lists([F(ListKind.Calendar), .. Lines(4)]), work: 400);
        var without = Plan(Lists([(ListKind.Calendar, false), .. Lines(4)]), work: 400);

        Assert.NotEmpty(without.Lists[0].Hidden);
        Assert.Empty(plan.Lists[0].Hidden);
        Assert.Equal(Room(400), plan.Lists[0].Height, 1);
        Assert.Equal(plan.Lists[0].Height - (4 * 40), Tall(plan, 0), 1);
        Assert.Equal(40, Tall(plan, 1), 1);
    }

    /// <summary>Two Fill lines in a list past the room give way equally, as they share the room.</summary>
    [Fact]
    public void FillLinesGiveWayEqually()
    {
        var plan = Plan(Lists([F(ListKind.Calendar), F(ListKind.Mail), .. Lines(6)]), work: 600);
        var over = Rows(6) + Rows(4) + (6 * 40) - Room(600);

        Assert.Empty(plan.Lists[0].Hidden);
        Assert.Equal(Rows(6) - (over / 2), Tall(plan, 0), 1);
        Assert.Equal(Rows(4) - (over / 2), Tall(plan, 1), 1);
    }

    /// <summary>
    /// A Fill line gives way to one row and no further: past that the lines left out are left out as
    /// before, fewer than with it Automatic, and the Fill line takes back the sliver under the last
    /// line shown, too short for the next; a list beside it is as it was.
    /// </summary>
    [Fact]
    public void AFillLineGivesWayToOneRowThenLinesAreLeftOut()
    {
        var plan = Plan(Lists([F(ListKind.Calendar), .. Lines(30)], Lines(2)), work: 600);
        var without = Plan(Lists([(ListKind.Calendar, false), .. Lines(30)], Lines(2)), work: 600);

        // The lines of one row that fit over the Fill line's one row, each 40 px, edge to edge (§410).
        var shown = (int)((Room(600) - 40) / 40);
        Assert.Equal(Room(600) - (shown * 40), Tall(plan, 0), 1);
        Assert.True(Tall(plan, 0) < 40 + 40);
        Assert.Equal(Room(600), plan.Lists[0].Height, 1);
        Assert.Equal(30 - shown, plan.Lists[0].Hidden.Count);
        Assert.True(plan.Lists[0].Hidden.Count < without.Lists[0].Hidden.Count);
        Assert.Equal(without.Lists[1].Height, plan.Lists[1].Height, 1);
    }

    /// <summary>Several lists: each with Fill lines is the room, whichever is tallest; the others keep their heights.</summary>
    [Fact]
    public void EveryListWithFillLinesIsTheRoom()
    {
        var options = Lists([F(ListKind.Mail)], Lines(12), [(ListKind.EnergySaver, false), F(ListKind.Calendar)], Lines(2));
        var plan = Plan(options);

        Assert.Equal(Room(1040), plan.Lists[0].Height, 1);
        Assert.Equal(12 * 40, plan.Lists[1].Height, 1);
        Assert.Equal(Room(1040), plan.Lists[2].Height, 1);
        Assert.Equal(2 * 40, plan.Lists[3].Height, 1);
        Assert.Equal(Room(1040), plan.Height, 1);
    }

    /// <summary>
    /// A folder of one list is the room too (the clock button's notifications over the calendar), and
    /// where every list is driven by Fill lines each is; a folder without one is its content.
    /// </summary>
    [Fact]
    public void AFillLineAloneTakesTheRoom()
    {
        var alone = Plan(Lists([F(ListKind.Calendar), F(ListKind.Mail)]));
        Assert.Equal(Room(1040), alone.Lists[0].Height, 1);

        var both = Plan(Lists([F(ListKind.Calendar)], [F(ListKind.Mail)]));
        Assert.Equal(Room(1040), both.Lists[0].Height, 1);
        Assert.Equal(Room(1040), both.Lists[1].Height, 1);

        var without = Plan(Lists([(ListKind.Calendar, false)], [(ListKind.Mail, false)]));
        Assert.Equal(Rows(6), without.Height, 1);
    }

    /// <summary>
    /// A list that changes beside a Fill line leaves it as it is: the room is the work area's, not the
    /// other list's, and the other list is its content under it.
    /// </summary>
    [Fact]
    public void AListChangingBesideAFillLineLeavesIt()
    {
        foreach (var count in new[] { 3, 8, 14, 30 })
        {
            var plan = Plan(Lists(Lines(count), [F(ListKind.Calendar)]));
            Assert.Equal(Room(1040), Tall(plan, Of(plan, 1)[0]), 1);
            Assert.Equal(Math.Min(count, (int)(Room(1040) / 40)) * 40, plan.Lists[0].Height, 1);
        }
    }

    /// <summary>
    /// Arranged the Fill lines are as at rest, an empty list's slot changing nothing, so nothing moves
    /// when edit mode begins; on a top bar too.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    public void ArrangedTheFillLinesAreAsAtRest(Edge edge)
    {
        var options = Lists([F(ListKind.Month), F(ListKind.Calendar)], Lines(16), []);
        var rest = Plan(options, edge: edge);
        var arranged = Plan(options, editing: true, edge: edge);

        Assert.Equal(Room(1040), rest.Lists[0].Height, 1);
        foreach (var index in Of(rest, 0))
        {
            Assert.Equal(Tall(rest, index), Tall(arranged, index), 1);
            Assert.Equal(rest.Cards[index].top, arranged.Cards[index].top, 1);
        }
        Assert.Equal(rest.Height, arranged.Height, 1);
    }

    /// <summary>
    /// §329: the edit bar stands over a Fill folder, which takes the full height under it. A list's
    /// buttons it would cover — just inside the list's far end — stand just past it instead, the
    /// buttons' gap off it: under an edit bar at the top (off a bottom bar), over one at the bottom (off
    /// a top bar). Ones beside it, and an edit bar beside the list, are left where they are.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    public void ButtonsTheEditBarWouldCoverStandPastIt(Edge edge)
    {
        var plan = Plan(Lists([F(ListKind.Month), (ListKind.Clock, false)], Lines(2), []), editing: true, edge: edge);
        var box = plan.Lists[0].Box;
        var room = new D2D_RECT_F { left = -600, top = 0, right = plan.Width + 600, bottom = plan.Height };
        var bare = FolderFlyout.WithSlots(plan, 1, room, edge);
        // The edit bar 48 DIPs tall, 8 off the screen's edge, over the Fill list's middle.
        var middle = (box.left + box.right) / 2;
        var cover = edge == Edge.Bottom
            ? new D2D_RECT_F { left = middle - 150, top = room.top - 4, right = middle + 150, bottom = room.top + 44 }
            : new D2D_RECT_F { left = middle - 150, top = room.bottom - 44, right = middle + 150, bottom = room.bottom + 4 };
        var covered = FolderFlyout.WithSlots(plan, 1, room, edge, cover);

        var own = covered.Adds.Where(add => add.List == 0).ToList();
        Assert.Equal(2, own.Count);
        Assert.All(own, add => Assert.Equal(edge == Edge.Bottom ? cover.bottom + FolderFlyout.AddApartDip : cover.top - FolderFlyout.AddApartDip - FolderFlyout.AddDip, add.Box.top));
        // Under it they were.
        Assert.All(bare.Adds.Where(add => add.List == 0), add => Assert.True(add.Box.top < cover.bottom && add.Box.bottom > cover.top));
        // The rest where they were, the second list's and the slots'; an edit bar beside the list moves nothing.
        Assert.Equal(bare.Adds.Where(add => add.List != 0), covered.Adds.Where(add => add.List != 0));
        Assert.Equal(bare.Adds, FolderFlyout.WithSlots(plan, 1, room, edge, cover with { left = box.right + 400, right = box.right + 700 }).Adds);
    }

    /// <summary>The share: equal, in whole px, the first a px more where it does not divide (and the fraction left); past a line's room to the others; what none can take not given.</summary>
    [Theory]
    [InlineData(10f, new[] { 100f, 100f, 100f }, new[] { 4f, 3f, 3f })]
    [InlineData(50f, new[] { 5f, 100f }, new[] { 5f, 45f })]
    [InlineData(50f, new[] { 5f, 5f }, new[] { 5f, 5f })]
    [InlineData(7f, new[] { 0f, 10f }, new[] { 0f, 7f })]
    [InlineData(0.5f, new[] { 10f }, new[] { 0.5f })]
    [InlineData(9.5f, new[] { 2.5f, 20f }, new[] { 2.5f, 7f })]
    [InlineData(10f, new[] { float.PositiveInfinity, 3f }, new[] { 7f, 3f })]
    public void TheShareIsEqualAndHeldToEachRoom(float left, float[] rooms, float[] given) =>
        Assert.Equal(given, FolderFlyout.FillShares(left, rooms));

    /// <summary>
    /// The person, 2026-10-23: "when the taskbar is shown on the top the fill space does not properly
    /// extend all the way to the bottom" (§296), and then "show full height, in edit mode just display
    /// the edit bar over the folder" (§329). A folder with a Fill line, at rest and arranged, at 100 and
    /// 150 %, off a bar on each edge of a 1600 by 1040 DIP display, its room the work area less the bars
    /// alone, as the dock hands it over (DockWindow.FolderWork): the Fill list reaches the work area's far
    /// end less the flyouts' margin, and the folder stands the same off every bar — its far end as far
    /// from a top bar's screen edge as from a bottom bar's, a left bar's folder where a right bar's is —
    /// and the same at rest and arranged, nothing moving as edit mode starts. Arranged, the edit bar
    /// stands over it where the dock stands it (across from the bars, §296), the popups kept off it
    /// (FlyoutHost.Avoided) but not the folder, and none of the folder's buttons is under it
    /// (FolderFlyout.Covered). Once the folder stood short of the edit bar's band (§310, §326).
    /// Pictures: folder-edgefill-&lt;edge&gt;-&lt;rest|edit&gt;-dark-150.png, the edit bar over the folder.
    /// </summary>
    [UiFact]
    public void AFolderFillsAsFarOffEveryBar()
    {
        WindowShots.OnHiddenDesktop("folder-edgefill", (gfx, instance) =>
        {
            using var flyout = new FolderFlyout(gfx, instance, 0);
            using var deck = new EditDeck(gfx, instance, 0);
            foreach (var scale in new[] { 1f, 1.5f })
            {
                var screen = new RECT { left = 0, top = 0, right = (int)(1600 * scale), bottom = (int)(1040 * scale) };
                var strip = (int)MathF.Round(48 * scale);
                var shown = new Dictionary<(Edge, bool), RECT>();
                foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
                {
                    var (work, anchor) = edge switch
                    {
                        Edge.Top => (screen with { top = strip }, new POINT { x = screen.right / 2, y = strip }),
                        Edge.Left => (screen with { left = strip }, new POINT { x = strip, y = screen.bottom / 2 }),
                        Edge.Right => (screen with { right = screen.right - strip }, new POINT { x = screen.right - strip, y = screen.bottom / 2 }),
                        _ => (screen with { bottom = screen.bottom - strip }, new POINT { x = screen.right / 2, y = screen.bottom - strip }),
                    };
                    var room = FolderFlyout.Reach(0, new FlyoutPlacement(anchor, work, edge).Room(scale), scale);
                    foreach (var editing in new[] { false, true })
                    {
                        var holder = BarKind.FolderList.New();
                        holder.Folder!.Lists = [[]];
                        var fill = ListKind.Month.New();
                        fill.Height = LineHeight.For(fill).As(LineHeightMode.Fill);
                        holder.Folder.Put(fill, 0);
                        holder.Folder.Put(ListKind.Clock.New(), 0);
                        var config = new Config();
                        using var folder = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, config);
                        // Arranged, the edit bar with the folder's buttons as the dock stands it (Dock.ShowDecks):
                        // across from the bars (§296) — at the bottom off a top bar, at the top otherwise —, over
                        // the folder, the popups kept off it.
                        RECT over = default;
                        if (editing)
                        {
                            deck.Show(gfx, Dark, scale, new EditDeckRoom(new RectI(0, 0, screen.right, screen.bottom), edge == Edge.Top ? strip : 0, edge == Edge.Bottom ? strip : 0), state: EditDeckState.Plain with { Folder = new EditDeckSubject(OptionGlyphs.FolderOpen, "News") });
                            over = deck.PanelOnScreen;
                        }
                        flyout.Covered = over;
                        FlyoutHost.Avoided = editing ? [over] : [];
                        try
                        {
                            flyout.Show(gfx, Dark, scale, anchor, work, edge, folder, editing: editing, display: screen);
                        }
                        finally
                        {
                            FlyoutHost.Avoided = [];
                        }
                        WindowShots.Settle(100);
                        var name = $"{edge} {(editing ? "arranged" : "at rest")} at {scale * 100} %";
                        Assert.Equal(room, flyout.Plan!.Lists[0].Height, 1);
                        var on = flyout.PanelOnScreen;
                        // To the work area's far end less the flyouts' margin and nothing more (§320), the
                        // edit bar or not (§329).
                        var margin = PopupPaint.Round(12 * scale);
                        switch (edge)
                        {
                            case Edge.Bottom: Assert.Equal(work.top + margin, on.top, 1); break;
                            case Edge.Top: Assert.Equal(work.bottom - margin, on.bottom, 1); break;
                            default: Assert.Equal((work.top + margin, work.bottom - margin), (on.top, on.bottom)); break;
                        }
                        if (editing)
                        {
                            // Off a top or a bottom bar the edit bar stands over its Fill list (§329)...
                            Assert.True(edge is Edge.Left or Edge.Right || (on.left < over.right && on.right > over.left && on.top < over.bottom && on.bottom > over.top), $"{name}: the edit bar {over.top}..{over.bottom} is not over the folder {on.top}..{on.bottom}");
                            // ...and every button of the folder's shown stands inside the work area, none under it.
                            var buttons = 0;
                            for (var index = 0; index < flyout.Plan.Adds.Count; index++)
                            {
                                if (flyout.Plan.Adds[index].Box is var add && add.right > add.left)
                                {
                                    var at = flyout.ScreenOfAdd(index);
                                    var half = (int)MathF.Ceiling((add.right - add.left) / 2);
                                    Assert.True(at.y > work.top && at.y < work.bottom, $"{name}: button {index} at {at.y} outside {work.top}..{work.bottom}");
                                    Assert.False(at.x + half > over.left && at.x - half < over.right && at.y + half > over.top && at.y - half < over.bottom, $"{name}: button {index} at {at.x},{at.y} under the edit bar {over.left},{over.top}..{over.right},{over.bottom}");
                                    buttons++;
                                }
                            }
                            Assert.True(buttons >= 2, $"{name}: {buttons} buttons");
                        }
                        shown[(edge, editing)] = on;
                        if (scale == 1.5f)
                        {
                            List<(HWND, RECT)> parts = [(flyout.Hwnd, screen)];
                            if (editing)
                            {
                                parts.Add((deck.Hwnd, over));
                            }
                            Assert.True(WindowShots.SaveTogether(parts, $"folder-edgefill-{edge.ToString().ToLowerInvariant()}-{(editing ? "edit" : "rest")}-dark-150.png", (32, 32, 32), over: [flyout.Hwnd]));
                        }
                        flyout.Hide();
                        deck.Hide();
                    }
                }
                foreach (var editing in new[] { false, true })
                {
                    var (bottom, top, left, right) = (shown[(Edge.Bottom, editing)], shown[(Edge.Top, editing)], shown[(Edge.Left, editing)], shown[(Edge.Right, editing)]);
                    // A top bar's folder the mirror of a bottom bar's, its far gap the same.
                    Assert.Equal(bottom.top - screen.top, screen.bottom - top.bottom, 1d);
                    Assert.Equal(bottom.bottom - bottom.top, top.bottom - top.top, 1d);
                    // A left bar's the mirror of a right bar's.
                    Assert.Equal((left.top, left.bottom), (right.top, right.bottom));
                    Assert.Equal(left.left - strip, screen.right - strip - right.right, 1d);
                }
                // §326, §329: it stands the same at rest and arranged off every bar, the edit bar over it
                // arranged, so nothing jumps as edit mode starts or ends.
                foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
                {
                    Assert.Equal(shown[(edge, false)], shown[(edge, true)]);
                }
            }
        });
    }

    /// <summary>
    /// The open folder: a month and an agenda filling the first list beside a shorter second, at 100
    /// and 150 %, in both themes, at rest and arranged — the first list the placement's room, the
    /// lines where they stand at rest — and a widget added to the second list leaving the first as it
    /// is as the folder is configured again.
    /// </summary>
    [UiFact]
    public void TheOpenFolderFillsAndReflows()
    {
        WindowShots.OnHiddenDesktop("folder-fill", (gfx, instance) =>
        {
            var was = (Clock.Now, CalendarIsland.Made);
            Clock.Now = () => ListFormCases.Frozen;
            try
            {
                using var flyout = new FolderFlyout(gfx, instance, 0);
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    foreach (var dark in new[] { true, false })
                    {
                        CalendarIsland.Made = ListFormCases.Store(ListFormCases.Week);
                        var holder = BarKind.FolderList.New();
                        holder.Folder!.Lists = [[], []];
                        foreach (var kind in new[] { ListKind.Month, ListKind.Calendar })
                        {
                            var line = kind.New();
                            line.Height = LineHeight.For(line).As(LineHeightMode.Fill);
                            holder.Folder.Put(line, 0);
                        }
                        foreach (var kind in new[] { ListKind.Clock, ListKind.EnergySaver, ListKind.NightLight, ListKind.AirplaneMode, ListKind.BluetoothSwitch, ListKind.WifiSwitch, ListKind.MicrophoneSwitch, ListKind.SoundVolume, ListKind.Cpu, ListKind.Battery, ListKind.Brightness })
                        {
                            holder.Folder.Put(kind.New(), 1);
                        }
                        var config = new Config();
                        using var folder = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, config);
                        var work = new RECT { left = 0, top = 0, right = (int)(1600 * scale), bottom = (int)(1040 * scale) };
                        var anchor = new POINT { x = work.right / 2, y = work.bottom };
                        var room = FolderFlyout.Reach(0, new FlyoutPlacement(anchor, work, Edge.Bottom).Room(scale), scale);
                        var name = $"folder-fill-{(dark ? "dark" : "light")}-{(int)(scale * 100)}";

                        flyout.Show(gfx, TestLooks.LookFor(dark), scale, anchor, work, Edge.Bottom, folder);
                        WindowShots.Settle(300);
                        var rest = flyout.Plan!;
                        Assert.True(!rest.Hidden.Any(), $"left out {string.Join(",", rest.Hidden)}; lines {string.Join(",", rest.Cards.Select(card => card.bottom - card.top))}");
                        Assert.Equal(room, rest.Lists[0].Height, 1);
                        Assert.True(rest.Lists[1].Height < room - 1, $"{rest.Lists[1].Height} of {room}");
                        Assert.True(WindowShots.Save(flyout.Hwnd, $"{name}-rest.png"));
                        flyout.Hide();

                        flyout.Show(gfx, TestLooks.LookFor(dark), scale, anchor, work, Edge.Bottom, folder, editing: true);
                        WindowShots.Settle(300);
                        var arranged = flyout.Plan!;
                        foreach (var index in Of(rest, 0))
                        {
                            Assert.Equal(Tall(rest, index), Tall(arranged, index), 1);
                        }
                        Assert.True(WindowShots.Save(flyout.Hwnd, $"{name}-edit.png"));

                        // A widget added to the other list: it grows, the Fill lines stay the room.
                        var before = arranged.Lists[1].Height;
                        holder.Folder.Put(ListKind.Memory.New(), 1);
                        folder.Configure(holder, config);
                        WindowShots.Settle(100);
                        var grown = flyout.Plan!;
                        Assert.True(grown.Lists[1].Height > before + 1, $"{grown.Lists[1].Height} after {before}");
                        Assert.Equal(room, grown.Lists[0].Height, 1);
                        flyout.Hide();
                    }
                }
            }
            finally
            {
                (Clock.Now, CalendarIsland.Made) = was;
            }
        });
    }
}
