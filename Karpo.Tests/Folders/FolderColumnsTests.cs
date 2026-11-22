// A folder's columns as the person had them on 2026-10-11 — the "+" beside the lists went on
// 2026-10-12 for the empty slots out to the screen's edges (FolderSlotTests); the "+" at a column's
// own end picks what goes in;
// empty columns are kept; at rest an empty column is its width of room and nothing else, arranged it
// looks like a slot of its width — no panel, no words, its guide lines down the screen, its "+" and
// width button level with a slot's (2026-10-12: "an empty column should not display a drag widgets
// here text in edit mode. it should keep its width though"); every column and slot has the width
// button, and none a remove (2026-10-12: "deleting a column should not even be possible anymore").
// The model's half is FolderModelTests'.
//
// The open folder's half needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderColumnsTests
{
    private const string Owner = "layout:default";

    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly POINT Anchor = new() { x = 960, y = 1040 };
    private static readonly RECT Work = new() { right = 1920, bottom = 1040 };

    /// <summary>A folder of lists, each its widgets of <paramref name="lists"/>, an empty array an empty list.</summary>
    private static FolderOptions Lists(params ListKind[][] lists)
    {
        var options = new FolderOptions { Lists = [] };
        for (var list = 0; list < lists.Length; list++)
        {
            options.Lists.Add([]);
            foreach (var kind in lists[list])
            {
                options.Put(kind.New(), list);
            }
        }
        return options;
    }

    private static FolderPlan Plan(FolderOptions options, bool editing, Edge edge = Edge.Bottom, float scale = 1f) =>
        FolderFlyout.Build(Dark, scale, FolderView.List, options, options.Cells, 1000, editing: editing, edge: edge);

    // --- At rest and arranged ------------------------------------------------------------------

    /// <summary>
    /// At rest an empty column draws nothing — no panel, no height, no words — and keeps its width as
    /// room between the others, so they stand where they would with it filled; arranged it draws
    /// nothing either, as a slot does, and has no height either (§297): its "+" and width button stand
    /// at its end, where its first line would, as far from it as a filled column's from its last line,
    /// level with the slots'; its guide lines run the whole way down.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, 1f)]
    [InlineData(Edge.Top, 1.5f)]
    public void AnEmptyColumnIsRoomAtRestAndASlotArranged(Edge edge, float scale)
    {
        var options = Lists([ListKind.EnergySaver, ListKind.AirplaneMode], [], [ListKind.Cpu]);
        options.Widths = [FolderListWidth.Medium, FolderListWidth.Wide, FolderListWidth.Narrow];

        var view = Plan(options, editing: false, edge, scale);
        var edit = Plan(options, editing: true, edge, scale);

        // At rest: its width, nothing else.
        Assert.Equal(PopupPaint.Round(FolderListWidths.WideDip * scale), view.Lists[1].Width, 1);
        Assert.Equal(0, view.Lists[1].Height);
        Assert.Empty(view.Empty);
        Assert.Equal(view.Lists[0].Box.right + view.ListGap, view.Lists[1].Left, 1);
        Assert.Equal(view.Lists[1].Box.right + view.ListGap, view.Lists[2].Left, 1);
        // Its pieces are the other two lists' alone: nothing of the folder's is drawn there.
        Assert.Equal(2, view.Pieces.Count);
        Assert.DoesNotContain(view.Pieces, piece => piece.left < view.Lists[1].Box.right && view.Lists[1].Left < piece.right);
        Assert.Equal(-1, view.ListOn(new System.Numerics.Vector2(view.Lists[1].Left + 10, view.Height - 2)));

        // Arranged: no height, no panel, no words.
        Assert.Equal(0, edit.Lists[1].Height);
        Assert.Empty(edit.Empty);
        Assert.Empty(edit.Lists[1].Runs);
        Assert.Equal(2, edit.Pieces.Count);
        Assert.DoesNotContain(edit.Pieces, piece => piece.left < edit.Lists[1].Box.right && edit.Lists[1].Left < piece.right);
        // Its guide lines down the whole room, as a slot's; a list's stop at its panel.
        // The room off the bar: past the lists' far end, short of the bar's side.
        var room = edge == Edge.Top
            ? new D2D_RECT_F { left = -400, top = -10, right = edit.Width + 400, bottom = edit.Height + 500 }
            : new D2D_RECT_F { left = -400, top = -500, right = edit.Width + 400, bottom = edit.Height + 10 };
        var lines = FolderFlyout.Guides(edit, room);
        Assert.Contains(lines, line => line.left == edit.Lists[1].Left && line.top == room.top && line.bottom == room.bottom);
        Assert.DoesNotContain(lines, line => line.left == edit.Lists[0].Left && line.top == room.top && line.bottom == room.bottom);
        if (edge == Edge.Top)
        {
            Assert.Equal(0, edit.Lists[1].Top);
        }
        else
        {
            Assert.Equal(edit.Height, edit.Lists[1].Box.bottom, 1);
        }

        // Its buttons at its end, as far off it as a filled column's are off its last line (§297), and
        // a slot's level with them.
        var slotted = FolderFlyout.WithSlots(edit, scale, room, edge);
        float Off(int list)
        {
            var plus = slotted.Adds.Single(add => add.List == list && add.Kind == FolderAddKind.Add).Box;
            var box = slotted.Lists[list].Box;
            return edge == Edge.Top ? plus.top - box.bottom : box.top - plus.bottom;
        }
        Assert.Equal(PopupPaint.Round(FolderFlyout.AddApartDip * scale), Off(0), 1);
        Assert.Equal(Off(0), Off(1), 1);
        Assert.Equal(Off(0), Off(2), 1);
        var emptyPlus = slotted.Adds.Single(add => add.List == 1 && add.Kind == FolderAddKind.Add).Box.top;
        Assert.NotEmpty(slotted.Slots);
        Assert.All(slotted.Adds.Where(add => add.List < 0), add => Assert.Equal(emptyPlus, add.Box.top, 1));
    }

    /// <summary>
    /// What arranging may change is an outer empty column's width, which it has only arranged; an empty
    /// column has no height either way (§297). The columns with widgets stand as they stood to one
    /// another, measured from the first of them and from the bar's side, which is where the window
    /// stands on the screen; every width is the same.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    public void ArrangingMovesNothingButAnEmptyColumn(Edge edge)
    {
        foreach (var options in new[]
        {
            Lists([ListKind.EnergySaver], []),
            Lists([], [ListKind.EnergySaver, ListKind.AirplaneMode, ListKind.Cpu, ListKind.Memory]),
            Lists([ListKind.EnergySaver, ListKind.Divider, ListKind.Cpu], [], [ListKind.Battery], []),
        })
        {
            var view = Plan(options, editing: false, edge);
            var edit = Plan(options, editing: true, edge);
            // Measured from the bar's side: from the top under a top bar, from the bottom over any other.
            float Down(FolderPlan plan, float y) => edge == Edge.Top ? y : plan.Height - y;
            var shown = Enumerable.Range(0, options.Lists.Count).Where(list => options.Lists[list].Count > 0).ToList();
            var from = edit.Lists[shown[0]].Left;

            Assert.Equal(edit.Lists[shown[^1]].Box.right - from, view.Width, 1);
            Assert.Equal(shown.Select(list => (edit.Lists[list].Left - from, edit.Lists[list].Width)), shown.Select(list => (view.Lists[list].Left, view.Lists[list].Width)));
            for (var list = 0; list < options.Lists.Count; list++)
            {
                if (options.Lists[list].Count == 0)
                {
                    Assert.Equal((0f, 0f), (view.Lists[list].Height, edit.Lists[list].Height));
                    continue;
                }
                Assert.Equal(Down(view, view.Lists[list].Top), Down(edit, edit.Lists[list].Top), 1);
                Assert.Equal(Down(view, view.Lists[list].Box.bottom), Down(edit, edit.Lists[list].Box.bottom), 1);
            }
            for (var card = 0; card < view.Cards.Count; card++)
            {
                Assert.Equal(Down(view, view.Cards[card].top), Down(edit, edit.Cards[card].top), 1);
                Assert.Equal((view.Cards[card].left, view.Cards[card].right), (edit.Cards[card].left - from, edit.Cards[card].right - from));
            }
        }
    }

    /// <summary>
    /// At rest an empty column before the first with widgets or after the last takes no room: the
    /// folder's box — what the host centres on its button — is exactly the columns that show, so a
    /// trailing empty column no longer pushes them left of the button, nor a leading one right of it
    /// (the person, 2026-10-12: "an empty column is sometimes not viewed as non existing column which
    /// leads to the folder being shifted to the left without actually having widgets on the right
    /// side"). One between two columns with widgets keeps its width, a spacer. Arranged each keeps
    /// its width. Narrow and wide empty ones alike.
    /// </summary>
    [Theory]
    [InlineData("a | ", Edge.Bottom, 1f)]
    [InlineData(" | a", Edge.Bottom, 1f)]
    [InlineData("a |  | ", Edge.Bottom, 1.5f)]
    [InlineData(" | a | ", Edge.Top, 1f)]
    [InlineData(" |  | a b | c |  | ", Edge.Bottom, 1.25f)]
    [InlineData("a |  | b", Edge.Bottom, 1f)]
    public void AtRestAnOuterEmptyColumnTakesNoRoom(string shape, Edge edge, float scale)
    {
        var parts = shape.Split('|').Select(part => part.Trim()).ToArray();
        var options = Lists([.. parts.Select(part => part.Length == 0 ? Array.Empty<ListKind>() : [.. part.Split(' ').Select(_ => ListKind.AirplaneMode)])]);
        // The empty ones narrow and wide by turns, the others medium.
        options.Widths = [.. parts.Select((part, list) => part.Length > 0 ? FolderListWidth.Medium : list % 2 == 0 ? FolderListWidth.Narrow : FolderListWidth.Wide)];
        var shown = Enumerable.Range(0, parts.Length).Where(list => parts[list].Length > 0).ToList();

        var view = Plan(options, editing: false, edge, scale);
        var edit = Plan(options, editing: true, edge, scale);

        Assert.Equal(0, view.Lists[shown[0]].Left);
        Assert.Equal(view.Width, view.Lists[shown[^1]].Box.right, 1);
        // What lies between the first shown and the last is as arranged: a spacer keeps its room.
        var from = edit.Lists[shown[0]].Left;
        Assert.Equal(edit.Lists[shown[^1]].Box.right - from, view.Width, 1);
        // What is centred on the button (Core): at rest the whole box, arranged the same columns.
        Assert.Equal(PopupPaint.Rect(0, 0, view.Width, view.Height), view.Core);
        Assert.Equal((from, edit.Lists[shown[^1]].Box.right), (edit.Core.left, edit.Core.right));
        for (var list = 0; list < parts.Length; list++)
        {
            var outer = list < shown[0] || list > shown[^1];
            Assert.Equal(outer ? 0 : edit.Lists[list].Width, view.Lists[list].Width, 1);
            Assert.InRange(view.Lists[list].Left, 0, view.Width);
            Assert.Equal(PopupPaint.Round(FolderListWidths.Dip(options.WidthOf(list)) * scale), edit.Lists[list].Width, 1);
            // Nothing of an outer one is found under the pointer.
            if (outer)
            {
                Assert.Equal(0, view.Lists[list].Height);
                Assert.NotEqual(list, view.ListAt(view.Lists[list].Left));
            }
        }
        // The same box as the folder of the shown columns alone, with the spacers between.
        var alone = Lists([.. parts[shown[0]..(shown[^1] + 1)].Select(part => part.Length == 0 ? Array.Empty<ListKind>() : [.. part.Split(' ').Select(_ => ListKind.AirplaneMode)])]);
        alone.Widths = [.. options.Widths.Skip(shown[0]).Take(shown[^1] - shown[0] + 1)];
        var bare = Plan(alone, editing: false, edge, scale);
        Assert.Equal((bare.Width, bare.Height), (view.Width, view.Height));
        Assert.Equal(bare.Cards, view.Cards);
    }

    /// <summary>A folder with no widget at all shows its one list's panel at rest too: opening it shows something.</summary>
    [Fact]
    public void AFolderWithNothingInItShowsItsEmptyPanelAtRest()
    {
        var plan = Plan(new FolderOptions(), editing: false);

        Assert.Single(plan.Empty);
        Assert.True(plan.Height > 0);
        Assert.Single(plan.Pieces);
    }

    // --- The buttons --------------------------------------------------------------------------

    /// <summary>
    /// Every list — an empty one too — and every slot has its "+" and on its right its width button,
    /// the pair centred on the column (the person, 2026-10-19: "add back the add button per column";
    /// §246); an empty list's stand level with a slot's. No list has a remove, a folder of several
    /// neither.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, 1f)]
    [InlineData(Edge.Top, 1.5f)]
    public void EveryColumnAndSlotHasItsPlusAndItsWidthAndNoRemove(Edge edge, float scale)
    {
        var options = Lists([ListKind.EnergySaver], [], [ListKind.Cpu, ListKind.Memory]);
        options.Widths = [FolderListWidth.Narrow, FolderListWidth.Narrow, FolderListWidth.Narrow];
        var plan = Plan(options, editing: true, edge, scale);
        FolderAddKind[] pair = [FolderAddKind.Add, FolderAddKind.Width];

        Assert.Equal([.. pair, .. pair, .. pair], plan.Adds.Select(add => add.Kind));
        for (var list = 0; list < 3; list++)
        {
            var plus = plan.Adds.Single(add => add.Kind == FolderAddKind.Add && add.List == list).Box;
            var width = plan.Adds.Single(add => add.Kind == FolderAddKind.Width && add.List == list);
            Assert.Equal(new FolderPlace(list, 0), width.Place);
            Assert.True(plus.right < width.Box.left, "the \"+\" first, the width on its right");
            Assert.Equal(plus.top, width.Box.top);
            var middle = (plan.Lists[list].Box.left + plan.Lists[list].Box.right) / 2;
            Assert.InRange((plus.left + width.Box.right) / 2, middle - 1, middle + 1);
        }
        // The empty list's buttons where a slot's stand; each slot its "+" and its width.
        var slotted = FolderFlyout.WithSlots(plan, scale, new D2D_RECT_F { left = -2000, top = -800, right = plan.Width + 2000, bottom = plan.Height + 800 }, edge);
        var slotWidth = slotted.Adds.First(add => add.Place.New).Box;
        Assert.Equal(slotWidth.top, plan.Adds.First(add => add.List == 1).Box.top, 1);
        Assert.NotEmpty(slotted.Slots);
        foreach (var slot in slotted.Slots)
        {
            Assert.Equal(pair, slotted.Adds.Where(add => add.Place == slot.Place).Select(add => add.Kind));
        }
        for (var one = 0; one < slotted.Adds.Count; one++)
        {
            for (var other = one + 1; other < slotted.Adds.Count; other++)
            {
                var (a, b) = (slotted.Adds[one].Box, slotted.Adds[other].Box);
                Assert.False(a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom, $"{slotted.Adds[one].Kind} and {slotted.Adds[other].Kind} overlap");
            }
        }

        var empty = Plan(new FolderOptions(), editing: true, edge, scale);
        Assert.Equal(pair, empty.Adds.Select(add => add.Kind));
    }

    // --- The open folder -----------------------------------------------------------------------

    /// <summary>"box": a clock and the battery in the first list, an empty second.</summary>
    private static BarItem WithAnEmptyList()
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        var clock = ListKind.Clock.New();
        clock.Id = "clock";
        var battery = ListKind.Battery.New();
        battery.Id = "battery";
        island.Folder!.Put(clock, 0);
        island.Folder.Put(battery, 0);
        island.Folder.Lists.Add([]);
        return island;
    }

    private static int AddOf(FolderPlan plan, Func<FolderAdd, bool> which) =>
        plan.Adds.Select((add, index) => (add, index)).Single(entry => which(entry.add)).index;

    private static FolderFlyoutEvent Press(FolderFlyout flyout, POINT screen)
    {
        var at = FolderEditClickTests.Client(flyout.Hwnd, screen);
        _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, at);
        return flyout.Message(WM.WM_LBUTTONUP, 0, at);
    }

    /// <summary>
    /// No column is picked by a click (the person, 2026-10-19: "a column should not be selectable in
    /// general"): a click on an empty column's room, on the room between two, is the room's — the
    /// selection rectangle's press and release, nothing else — and where the folder's edit bar adds
    /// stays the first column with room. A slot's room is not the folder's at all: it goes through to
    /// the dim. A line selected still steers the edit bar's add into its column, and a line's "Add"
    /// names its own.
    /// </summary>
    [UiFact]
    public void AClickOnAColumnsRoomPicksNothing()
    {
        WindowShots.OnHiddenDesktop("folder-columns-no-pick", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", WithAnEmptyList(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            var plan = flyout.Plan!;
            var on = flyout.PanelOnScreen;
            var empty = plan.Lists[1].Box;

            Assert.Equal(new FolderPlace(0, 0), flyout.AddPlace());
            // The empty column has no height (§297): its room beside the other's lines, half way up the panel.
            Assert.Equal(0, empty.bottom - empty.top);
            var room = new POINT { x = on.left + (int)((empty.left + empty.right) / 2), y = on.top + (int)(plan.Height / 2) };
            var between = new POINT { x = on.left + (int)((plan.Lists[0].Box.right + empty.left) / 2), y = room.y };
            foreach (var at in new[] { room, between })
            {
                Assert.Equal(FolderFlyoutAction.BandReleased, Press(flyout, at).Kind);
                Assert.Null(flyout.ChosenColumn());
                Assert.Equal(new FolderPlace(0, 0), flyout.AddPlace());
                Assert.True(flyout.IsOpen);
            }
            var slot = plan.Slots.Single(each => each.Place == FolderPlace.Slot(2, 0)).Box;
            Assert.True(flyout.PassesThrough(new POINT { x = on.left + (int)((slot.left + slot.right) / 2), y = room.y }), "a slot's room is the dim's");

            // A line of the first column selected: its column. A line's "Add" names its own list.
            flyout.Picked = flyout.Keys[0];
            Assert.Equal(0, flyout.ChosenColumn());
            Assert.Equal(new FolderPlace(0, 0), flyout.AddPlace());
            Assert.Equal(new FolderPlace(1, 0), flyout.AddPlace(1));
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// Every column's "+" and every slot's (§246) asks the dock for the add list for that column (§593 at the edit bar's top,
    /// as wide), holding the folder open, the button pressed until the list has gone: a list's at its end away from the bar (where the edit bar would add into it,
    /// <see cref="FolderFlyout.AddPlace"/>), a slot's a column of its own there. That place is known,
    /// so what is picked goes in there at once, carried by nothing (the person, 2026-10-23: "when i
    /// click the add on a specific column or specific slot then it should immediately place it there
    /// and not stick to my mouse"; §297), as Dock.AddChosen adds it: <see cref="AddSpot.InFolder"/>,
    /// then <see cref="AddList.Add(List{BarItem}, AddSpot, CatalogueEntry)"/>.
    /// </summary>
    [UiFact]
    public void EveryColumnsPlusOpensTheAddListAndWhatIsPickedGoesInThereAtOnce()
    {
        WindowShots.OnHiddenDesktop("folder-columns-plus", (gfx, instance) =>
        {
            var clock = Catalogue.On(WidgetType.List).Single(entry => entry.List == ListKind.Clock);
            foreach (var want in new[] { new FolderPlace(0, 0), new FolderPlace(1, 0), FolderPlace.Slot(0, 1), FolderPlace.Slot(2, 0) })
            {
                var item = WithAnEmptyList();
                var config = new Config();
                config.ListFor(Owner).Add(item);
                var folder = new FolderIsland($"{Owner}/box", item, gfx, config);
                using var flyout = new FolderFlyout(gfx, instance, 0);
                flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
                Assert.Equal(new FolderPlace(0, 0), flyout.AddPlace());
                var lists = item.Folder!.Lists.Count;

                var which = AddOf(flyout.Plan!, add => add.Kind == FolderAddKind.Add && (want.New ? add.Place == want : add.List == want.List && !add.Place.New));
                var button = flyout.ScreenOfAdd(which);
                var asked = Press(flyout, button);
                Assert.Equal(FolderFlyoutAction.Add, asked.Kind);
                Assert.Equal(want, asked.Place);
                // §576: the "+" itself, §593 which a press on closes the add page it opened (§430), the page at the edit bar's top.
                Assert.True(asked.Field.left <= button.x && button.x <= asked.Field.right && asked.Field.top <= button.y && button.y <= asked.Field.bottom,
                    $"the \"+\" asked as {asked.Field.left},{asked.Field.top}..{asked.Field.right},{asked.Field.bottom}, not the one at {button.x},{button.y}");
                Assert.True(flyout.IsOpen && flyout.StaysOpen);
                flyout.MenuClosed();

                // The pick, as the dock brings it: the "+"'s place is the spot's.
                var spot = AddSpot.InFolder(Owner, "box", asked.Place);
                var path = AddList.Add([item], spot, clock);
                Assert.NotNull(path);
                var cell = Karpo.Core.Folders.CellOf(item.Folder!, path![(path.IndexOf('/') + 1)..])!;
                var column = want.New ? new FolderPlace(Karpo.Core.Folders.ListOfSlot(lists, want), 0) : want;
                Assert.Equal(column, Karpo.Core.Folders.PlaceOf(item.Folder!, cell));
                Assert.Equal(want.New ? lists + 1 + want.Apart : lists, item.Folder!.Lists.Count);
                flyout.Hide();
                folder.Dispose();
            }
        });
    }

    /// <summary>
    /// Arranged, the width button of an empty list, and of an empty slot, asks the dock for the
    /// widths menu, at the button and away from the bar — a slot's for its place, which a pick makes
    /// a list — and the folder holds itself open for the menu until it ends.
    /// </summary>
    [UiFact]
    public void AnEmptyListsAndASlotsWidthAsk()
    {
        WindowShots.OnHiddenDesktop("folder-columns-width", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", WithAnEmptyList(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            var plan = flyout.Plan!;

            foreach (var (want, which) in new (FolderPlace, int)[]
            {
                (new FolderPlace(1, 0), AddOf(plan, add => add is { Kind: FolderAddKind.Width, List: 1 })),
                (FolderPlace.Slot(0, 1), AddOf(plan, add => add.Kind == FolderAddKind.Width && add.Place == FolderPlace.Slot(0, 1))),
            })
            {
                var asked = Press(flyout, flyout.ScreenOfAdd(which));
                Assert.Equal(FolderFlyoutAction.Width, asked.Kind);
                Assert.Equal(want, asked.Place);
                var button = flyout.ScreenOfAdd(which);
                Assert.InRange(asked.At.x, button.x - 1, button.x + 1);
                Assert.True(asked.At.y < button.y, "the menu opens away from a bottom bar");
                Assert.True(flyout.IsOpen && flyout.StaysOpen);
                flyout.MenuClosed();
                Assert.True(flyout.IsOpen);
            }
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// Arranged, a line carried onto an empty list lands in it, at its top, though it has no panel; at
    /// rest the room it keeps is no part of the folder for a press either.
    /// </summary>
    [UiFact]
    public void ALineIsCarriedIntoAnEmptyColumn()
    {
        WindowShots.OnHiddenDesktop("folder-columns-drag", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", WithAnEmptyList(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            var plan = flyout.Plan!;
            var on = flyout.PanelOnScreen;
            var empty = plan.Lists[1].Box;
            var middle = new POINT
            {
                x = on.left + (int)MathF.Round((empty.left + empty.right) / 2),
                y = on.top + (int)MathF.Round((empty.top + empty.bottom) / 2),
            };
            Assert.False(flyout.Contains(middle), "arranged, the empty list has no panel, as a slot has none");
            var slop = GetSystemMetrics(SM.SM_CYDRAG);

            var press = flyout.ScreenOfCard(1);
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, FolderEditClickTests.Client(flyout.Hwnd, press));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, FolderEditClickTests.Client(flyout.Hwnd, press with { y = press.y - slop - 2 }));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, FolderEditClickTests.Client(flyout.Hwnd, middle));
            var placed = flyout.Message(WM.WM_LBUTTONUP, 0, FolderEditClickTests.Client(flyout.Hwnd, middle));
            Assert.Equal(FolderFlyoutAction.Place, placed.Kind);
            Assert.Equal($"{Owner}/box/battery", placed.Widget?.Key);
            Assert.Equal(new FolderPlace(1, 0), placed.Place);
            flyout.Hide();

            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: false);
            var rest = flyout.Plan!;
            on = flyout.PanelOnScreen;
            var room = new POINT
            {
                x = on.left + (int)MathF.Round((rest.Lists[1].Box.left + rest.Lists[1].Box.right) / 2),
                y = on.bottom - 4,
            };
            Assert.False(flyout.Contains(room), "at rest the empty list is room, not the folder");
            Assert.True(flyout.Contains(flyout.ScreenOfCard(0)));
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>"box": a list per part of <paramref name="shape"/> split at "|", each of a line per id in it ("a | " a line and an empty list).</summary>
    private static BarItem Shaped(string shape)
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        island.Folder!.Lists.Clear();
        island.Folder.Widths?.Clear();
        foreach (var part in shape.Split('|'))
        {
            island.Folder.Lists.Add([.. part.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(id =>
            {
                var line = ListKind.AirplaneMode.New();
                line.Id = id;
                line.View = FolderView.List;
                return new FolderCell { Island = line };
            })]);
        }
        return island;
    }

    /// <summary>Where the middle of the columns that show stands across the screen, against the folder's button.</summary>
    private static float OffCentre(FolderFlyout flyout, POINT anchor)
    {
        var plan = flyout.Plan!;
        var on = flyout.PanelOnScreen;
        var shown = plan.Lists.Where(list => list.Height > 0).ToList();
        return on.left + ((shown[0].Left + shown[^1].Box.right) / 2) - anchor.x;
    }

    /// <summary>
    /// On a real folder at rest the columns that show are centred on the folder's button, whatever
    /// empty column stands before, after or between them — opened at rest, opened at rest after it
    /// was arranged (edit mode ending closes it, and the next opening is at rest), and laid out again
    /// at rest when its right column's last line goes, leaving that column empty (the person,
    /// 2026-10-12: "the folder being shifted to the left without actually having widgets on the
    /// right side"). Arranged too (FolderPlan.Core, §158).
    /// </summary>
    [UiFact]
    public void AtRestTheColumnsThatShowAreCentredOnTheButton()
    {
        WindowShots.OnHiddenDesktop("folder-columns-centred", (gfx, instance) =>
        {
            // The picture: each shape at rest over the bar's strip of the screen, a red line down
            // from the folder's button (folder-rest-centred.png).
            var pictures = new List<(byte[] Canvas, int Width, int Height)>();
            foreach (var shape in new[] { "a | ", " | a", "a |  | ", "a |  | b", " | a b |  | " })
            {
                var config = new Config();
                var folder = new FolderIsland($"{Owner}/box", Shaped(shape), gfx, config);
                using var flyout = new FolderFlyout(gfx, instance, 0);
                flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: false);
                WindowShots.Settle();
                var panel = flyout.PanelOnScreen;
                var frame = new RECT { left = Anchor.x - 640, top = panel.top - 16, right = Anchor.x + 640, bottom = Anchor.y };
                var (canvas, _) = WindowShots.Together([(flyout.Hwnd, panel)], frame, "folder-rest-centred.png", (0x30, 0x30, 0x30));
                var width = frame.right - frame.left;
                for (var y = 0; y < frame.bottom - frame.top; y++)
                {
                    for (var x = width / 2 - 1; x <= width / 2; x++)
                    {
                        var at = ((y * width) + x) * 4;
                        (canvas[at], canvas[at + 1], canvas[at + 2]) = ((byte)0x20, (byte)0x20, (byte)0xE0);
                    }
                }
                pictures.Add((canvas, width, frame.bottom - frame.top));
                Assert.InRange(OffCentre(flyout, Anchor), -1, 1);
                flyout.Hide();
                flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
                Assert.InRange(CoreOffCentre(flyout, Anchor), -1, 1);
                flyout.Hide();
                flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: false);
                Assert.InRange(OffCentre(flyout, Anchor), -1, 1);
                flyout.Hide();
                folder.Dispose();
            }
            WindowShots.SaveSideBySide(pictures, "folder-rest-centred.png", (0x80, 0x80, 0x80));

            var item = Shaped("a b | c");
            var config2 = new Config();
            var changing = new FolderIsland($"{Owner}/box", item, gfx, config2);
            using var open = new FolderFlyout(gfx, instance, 0);
            open.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, changing, editing: false);
            Assert.InRange(OffCentre(open, Anchor), -1, 1);
            var wide = open.PanelOnScreen;
            item.Folder!.Lists[1].Clear();
            changing.Configure(item, config2);
            Assert.Equal(2, open.Plan!.Lists.Count);
            Assert.InRange(OffCentre(open, Anchor), -1, 1);
            Assert.True(open.PanelOnScreen.right - open.PanelOnScreen.left < wide.right - wide.left, "the emptied column still takes room");
            open.Hide();
            changing.Dispose();
        });
    }

    /// <summary>Where the middle of the folder's core — the columns from the first with widgets to the last — stands along the bar, against its button.</summary>
    private static float CoreOffCentre(FolderFlyout flyout, POINT anchor, Edge edge = Edge.Bottom)
    {
        var core = flyout.Plan!.Core;
        var on = flyout.PanelOnScreen;
        return edge is Edge.Left or Edge.Right
            ? on.top + ((core.top + core.bottom) / 2) - anchor.y
            : on.left + ((core.left + core.right) / 2) - anchor.x;
    }

    /// <summary>Each column with widgets of the open folder, its box on the screen.</summary>
    private static List<(int Left, int Top, int Right, int Bottom)> FilledOnScreen(FolderFlyout flyout, IReadOnlyList<int> filled)
    {
        var plan = flyout.Plan!;
        var on = flyout.PanelOnScreen;
        return [.. filled.Select(list => plan.Lists[list].Box).Select(box => (
            on.left + (int)MathF.Round(box.left),
            on.top + (int)MathF.Round(box.top),
            on.left + (int)MathF.Round(box.right),
            on.top + (int)MathF.Round(box.bottom)))];
    }

    /// <summary>
    /// What is centred on the folder's button is the columns from the first with widgets to the
    /// last, an empty one between them included as its spacer, and no empty one outside them — at
    /// rest, where those take no room, and arranged, where they are drawn and hang off the side of
    /// the centred block as the slots do (the person, 2026-10-13: "even if a column is empty it is
    /// part of the container that gets centered, this should not be . (only for columns that have a
    /// widget to their left and right)"). So no column with widgets moves when edit mode begins —
    /// arranged in place, or opened arranged — or ends; on bottom and top bars, at the middle of the
    /// screen and pushed in at its edge, narrow, medium and wide empty columns alike. A side bar's
    /// columns run away from it, the first next to it on either side, and an empty one hangs off
    /// either side of them as along a bottom bar: on the bar's side under the bar (§303).
    /// </summary>
    [UiTheory]
    [InlineData("a | ", Edge.Bottom, 960)]
    [InlineData(" | a", Edge.Bottom, 960)]
    [InlineData(" | a |  | ", Edge.Bottom, 960)]
    [InlineData("a |  | b", Edge.Bottom, 960)]
    [InlineData(" | a |  | b | ", Edge.Bottom, 960)]
    [InlineData(" | a |  | b | ", Edge.Top, 960)]
    [InlineData(" |  | a | ", Edge.Bottom, 60)]
    [InlineData("a |  |  | ", Edge.Top, 1880)]
    [InlineData("a b |  | ", Edge.Left, 540)]
    [InlineData("a b |  | ", Edge.Right, 540)]
    [InlineData(" |  | a b | ", Edge.Left, 540)]
    [InlineData(" |  | a b | ", Edge.Right, 540)]
    public void OnlyTheColumnsWithWidgetsAreCentredAndArrangingMovesNoneOfThem(string shape, Edge edge, int along)
    {
        WindowShots.OnHiddenDesktop("folder-columns-core", (gfx, instance) =>
        {
            var parts = shape.Split('|');
            var filled = Enumerable.Range(0, parts.Length).Where(list => parts[list].Trim().Length > 0).ToList();
            var item = Shaped(shape);
            // The empty ones narrow, medium and wide by turns.
            item.Folder!.Widths = [.. parts.Select((part, list) => part.Trim().Length > 0 ? FolderListWidth.Medium : (FolderListWidth)(list % 3))];
            var (anchor, work) = edge switch
            {
                Edge.Top => (new POINT { x = along, y = 48 }, new RECT { top = 48, right = 1920, bottom = 1080 }),
                Edge.Left => (new POINT { x = 48, y = along }, new RECT { left = 48, right = 1920, bottom = 1080 }),
                Edge.Right => (new POINT { x = 1872, y = along }, new RECT { right = 1872, bottom = 1080 }),
                _ => (new POINT { x = along, y = 1040 }, Work),
            };
            var vertical = edge is Edge.Left or Edge.Right;
            var middle = along is > 200 and < 1720;
            var config = new Config();
            var folder = new FolderIsland($"{Owner}/box", item, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);

            flyout.Show(gfx, Dark, 1f, anchor, work, edge, folder, editing: false);
            var rest = FilledOnScreen(flyout, filled);
            if (middle)
            {
                Assert.InRange(CoreOffCentre(flyout, anchor, edge), -1, 1);
            }
            // At rest the core is the whole panel: the columns that show.
            Assert.Equal((0f, flyout.Plan!.Width), (flyout.Plan.Core.left, flyout.Plan.Core.right));

            // Arranged where it stands.
            flyout.Arrange();
            Assert.True(flyout.Plan!.Editing);
            Assert.Equal(rest, FilledOnScreen(flyout, filled));
            if (middle)
            {
                Assert.InRange(CoreOffCentre(flyout, anchor, edge), -1, 1);
            }
            // Opened arranged, and at rest again after.
            flyout.Hide();
            flyout.Show(gfx, Dark, 1f, anchor, work, edge, folder, editing: true);
            WindowShots.Settle();
            Assert.Equal(rest, FilledOnScreen(flyout, filled));
            var plan = flyout.Plan!;
            var on = flyout.PanelOnScreen;
            // The empty ones outside hang off the side, and the slots past them: the first column on
            // the right of a right bar, next to it (FolderFlyout.FirstOnRight, §303).
            var rightward = !FolderFlyout.FirstOnRight(edge);
            for (var list = 0; list < parts.Length; list++)
            {
                var (left, right) = (on.left + plan.Lists[list].Box.left, on.left + plan.Lists[list].Box.right);
                if (list < filled[0])
                {
                    Assert.True(rightward ? right <= rest[0].Left : left >= rest[0].Right, $"list {list} hangs off before");
                }
                if (list > filled[^1])
                {
                    Assert.True(rightward ? left >= rest[^1].Right : right <= rest[^1].Left, $"list {list} hangs off after");
                }
            }
            Assert.All(plan.Slots, slot => Assert.True(slot.Box.right <= 0 || slot.Box.left >= plan.Width, "a slot stands outside every column"));
            SavePicture(flyout, anchor, edge, $"folder-core-{Name(shape)}-{edge}-{along}-arranged.png");
            flyout.Hide();
            flyout.Show(gfx, Dark, 1f, anchor, work, edge, folder, editing: false);
            WindowShots.Settle();
            Assert.Equal(rest, FilledOnScreen(flyout, filled));
            SavePicture(flyout, anchor, edge, $"folder-core-{Name(shape)}-{edge}-{along}-rest.png");
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// Arranged, a column emptied, or a widget put into an empty column outside the others, centres
    /// the columns with widgets now: the block is laid out again round them at once.
    /// </summary>
    [UiFact]
    public void ArrangedTheBlockFollowsTheColumnsThatHaveWidgets()
    {
        WindowShots.OnHiddenDesktop("folder-columns-core-change", (gfx, instance) =>
        {
            var item = Shaped(" | a b | c | ");
            var config = new Config();
            var folder = new FolderIsland($"{Owner}/box", item, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            Assert.InRange(CoreOffCentre(flyout, Anchor), -1, 1);
            var two = FilledOnScreen(flyout, [1, 2]);

            // The last column with widgets emptied: the one before it alone is centred.
            item.Folder!.Lists[2].Clear();
            folder.Configure(item, config);
            Assert.InRange(CoreOffCentre(flyout, Anchor), -1, 1);
            Assert.True(FilledOnScreen(flyout, [1])[0].Left > two[0].Left, "the column left moves right, to the middle");
            // A widget into the empty column before it: both centred, the empty one after them hanging off.
            item.Folder.Lists[0].Add(item.Folder.Lists[1][1]);
            item.Folder.Lists[1].RemoveAt(1);
            folder.Configure(item, config);
            Assert.InRange(CoreOffCentre(flyout, Anchor), -1, 1);
            var plan = flyout.Plan!;
            Assert.Equal((plan.Lists[0].Left, plan.Lists[1].Box.right), (plan.Core.left, plan.Core.right));
            flyout.Hide();
            folder.Dispose();
        });
    }

    private static string Name(string shape) =>
        string.Join("-", shape.Split('|').Select(part => part.Trim().Length > 0 ? part.Trim().Replace(' ', '+') : "e"));

    /// <summary>The open folder's window as far as the screen shows it round its panel, on a dark backdrop, a red line through its button.</summary>
    private static void SavePicture(FolderFlyout flyout, POINT anchor, Edge edge, string file)
    {
        var panel = flyout.PanelOnScreen;
        var vertical = edge is Edge.Left or Edge.Right;
        var frame = vertical
            ? new RECT { left = 0, top = anchor.y - 360, right = 1920, bottom = anchor.y + 360 }
            : new RECT { left = 0, top = edge == Edge.Top ? anchor.y : panel.top - 160, right = 1920, bottom = edge == Edge.Top ? panel.bottom + 160 : anchor.y };
        var (canvas, _) = WindowShots.Together([(flyout.Hwnd, frame)], frame, file, (0x30, 0x30, 0x30));
        var width = frame.right - frame.left;
        var height = frame.bottom - frame.top;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (vertical ? frame.top + y == anchor.y : frame.left + x == anchor.x)
                {
                    var at = ((y * width) + x) * 4;
                    (canvas[at], canvas[at + 1], canvas[at + 2]) = ((byte)0x20, (byte)0x20, (byte)0xE0);
                }
            }
        }
        WindowShots.SaveSideBySide([(canvas, width, height)], file, (0x80, 0x80, 0x80));
    }
}
