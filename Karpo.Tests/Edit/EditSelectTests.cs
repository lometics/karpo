// Several widgets selected in edit mode, and what is done to them together (the person, 2026-10-07):
// Ctrl+click, Shift+click and a drag over the frame select (EditSelection); dragging one carries
// them all, each surface closing up behind its own and the one they go to opening the room for all
// of them under one landing (Layout.Without, Layout.Landing with a group, Layout.Remaining), and they
// land side by side in their order (IslandEdits.MoveGroup); Ctrl+V puts in copies with new ids
// (IslandEdits.Paste); the carry window holds them side by side (EditCarryPlan); a widget's menu has
// "Copy" and "Paste" (Dock.CopyPasteRows). Pure: no window.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class EditSelectTests
{
    private const string Owner = "display:home";

    private static string Key(string id) => Config.IslandKey(Owner, id);

    private static readonly string[] Order = [Key("a"), Key("b"), Key("c"), Key("d"), Key("e")];

    // --- Scoped: a bar's widgets or one folder's lines, never both (2026-10-12) -------------------

    private static string Line(string folder, string id) => $"{Key(folder)}/{id}";

    [Fact]
    public void ALinesFolderIsItsKeyWithoutTheLast()
    {
        Assert.Equal(Key("f"), EditScope.FolderOf(Line("f", "x")));
        Assert.Null(EditScope.FolderOf(Key("a")));
        Assert.Null(EditScope.FolderOf("a"));
        Assert.Equal("layout:main/f", EditScope.FolderOf("layout:main/f/x"));
    }

    /// <summary>
    /// "when a folder is open the lasso select should always only select widgets from the folder";
    /// "when the folder is closed the same should be true but for the taskbar widgets".
    /// </summary>
    [Fact]
    public void AnOpenFoldersLinesAloneOrTheBarsWidgetsAlone()
    {
        // No folder open: a bar's widgets, of any display, never a line.
        Assert.True(EditScope.In(Key("a"), open: null));
        Assert.True(EditScope.In("display:other/b", open: null));
        Assert.False(EditScope.In(Line("f", "x"), open: null));
        // A folder open: its lines alone — not the bar's, not another folder's, not the folder itself.
        Assert.True(EditScope.In(Line("f", "x"), Key("f")));
        Assert.True(EditScope.In(Line("F", "x"), Key("f")));
        Assert.False(EditScope.In(Key("a"), Key("f")));
        Assert.False(EditScope.In(Key("f"), Key("f")));
        Assert.False(EditScope.In(Line("g", "x"), Key("f")));

        Assert.True(EditScope.Together(Key("a"), "display:other/b"));
        Assert.True(EditScope.Together(Line("f", "x"), Line("f", "y")));
        Assert.False(EditScope.Together(Key("a"), Line("f", "x")));
        Assert.False(EditScope.Together(Line("g", "x"), Line("f", "x")));
    }

    /// <summary>A Ctrl+click on one of the other kind starts the selection over among its kind; never the two together.</summary>
    [Fact]
    public void CtrlClickOnTheOtherKindStartsOver()
    {
        var selection = new EditSelection();
        selection.Toggle(Key("a"));
        selection.Toggle(Key("b"));
        selection.Toggle(Line("f", "x"));
        Assert.Equal([Line("f", "x")], selection.Keys);
        Assert.Equal(Line("f", "x"), selection.Anchor);

        selection.Toggle(Line("f", "y"));
        Assert.Equal([Line("f", "x"), Line("f", "y")], selection.Keys);
        // Another folder's line is the other kind too.
        selection.Toggle(Line("g", "z"));
        Assert.Equal([Line("g", "z")], selection.Keys);

        selection.Toggle(Key("c"));
        Assert.Equal([Key("c")], selection.Keys);
        Assert.Equal(Key("c"), selection.Anchor);
    }

    /// <summary>
    /// A folder opening keeps of the bar's only itself, the one clicked, which wears its ring; closing,
    /// its lines go and nothing else.
    /// </summary>
    [Fact]
    public void AFolderOpeningOrClosingDropsTheOtherKind()
    {
        var selection = new EditSelection();
        selection.Set([Key("a"), Key("f"), Line("f", "x"), Line("g", "y")], []);
        selection.Keep(key => EditScope.StaysOpening(key, Key("f")));
        Assert.Equal([Key("f"), Line("f", "x")], selection.Keys);

        selection.Keep(key => EditScope.StaysClosing(key, Key("f")));
        Assert.Equal([Key("f")], selection.Keys);
        Assert.Equal(Key("f"), selection.Anchor);
    }

    // --- Selecting -------------------------------------------------------------------------------

    [Fact]
    public void AClickSelectsOneAlone()
    {
        var selection = new EditSelection();
        selection.Only(Key("b"));
        selection.Only(Key("d"));

        Assert.Equal([Key("d")], selection.Keys);
        Assert.Equal(Key("d"), selection.Anchor);
        selection.Only(null);
        Assert.Equal(0, selection.Count);
        Assert.Null(selection.Anchor);
    }

    /// <summary>Ctrl+click adds a widget, and takes one out again; the anchor follows the last one added.</summary>
    [Fact]
    public void CtrlClickAddsAndTakesOut()
    {
        var selection = new EditSelection();
        selection.Only(Key("a"));
        selection.Toggle(Key("c"));
        selection.Toggle(Key("e"));

        Assert.Equal([Key("a"), Key("c"), Key("e")], selection.Keys);
        Assert.Equal(Key("e"), selection.Anchor);

        selection.Toggle(Key("E"));
        Assert.Equal([Key("a"), Key("c")], selection.Keys);
        Assert.Equal(Key("c"), selection.Anchor);
        Assert.True(selection.Contains(Key("A")));
    }

    /// <summary>Shift+click selects the run from the widget last clicked, in the display's order, either way, and keeps its anchor.</summary>
    [Fact]
    public void ShiftClickSelectsTheRunFromTheAnchor()
    {
        var selection = new EditSelection();
        selection.Only(Key("b"));
        selection.Range(Key("d"), Order);

        Assert.Equal([Key("b"), Key("c"), Key("d")], selection.Keys);
        Assert.Equal(Key("b"), selection.Anchor);

        selection.Range(Key("a"), Order);
        Assert.Equal([Key("a"), Key("b")], selection.Keys);
        Assert.Equal(Key("b"), selection.Anchor);
    }

    /// <summary>With nothing clicked before, or the anchor on another display, Shift+click selects the one alone.</summary>
    [Fact]
    public void ShiftClickWithoutAnAnchorHereSelectsOne()
    {
        var selection = new EditSelection();
        selection.Range(Key("c"), Order);
        Assert.Equal([Key("c")], selection.Keys);

        selection.Only(Config.IslandKey("display:work", "z"));
        selection.Range(Key("d"), Order);
        Assert.Equal([Key("d")], selection.Keys);
    }

    /// <summary>A drag over the frame selects what it runs over; with Ctrl or Shift held, what was selected stays besides.</summary>
    [Fact]
    public void ADragSelectsWhatItRunsOverAndKeepsWhatWasSelectedWithAModifier()
    {
        var selection = new EditSelection();
        selection.Only(Key("a"));

        selection.Set([Key("c"), Key("d")], []);
        Assert.Equal([Key("c"), Key("d")], selection.Keys);
        Assert.Equal(Key("c"), selection.Anchor);

        selection.Only(Key("a"));
        selection.Set([Key("d"), Key("a")], [Key("a")]);
        Assert.Equal([Key("a"), Key("d")], selection.Keys);
        Assert.Equal(Key("a"), selection.Anchor);

        // Run over nothing, nothing is selected.
        selection.Set([], []);
        Assert.Equal(0, selection.Count);
    }

    /// <summary>
    /// A press on the bar while it is arranged: on a widget it is carried, as before; on the bar's own
    /// room — the gaps between and round the widgets and the sections, its background — the selection
    /// rectangle begins, as on the dim (2026-10-12: "the lasso select should also work inside the
    /// taskbar"); the resize grip resizes, whatever stands under it.
    /// </summary>
    [Theory]
    [InlineData(false, true, EditPress.Own)]
    [InlineData(false, false, EditPress.Select)]
    [InlineData(true, false, EditPress.Resize)]
    [InlineData(true, true, EditPress.Resize)]
    public void APressOnTheBarsRoomSelectsAndOnAWidgetCarries(bool grip, bool onWidget, EditPress begins) =>
        Assert.Equal(begins, EditClicks.OnPress(grip, onWidget));

    /// <summary>What a selection rectangle runs over: a box it overlaps by a pixel or more; one that only touches its edge, never.</summary>
    [Fact]
    public void TheRectangleSelectsWhatItOverlaps()
    {
        var area = new RECT { left = 100, top = 50, right = 200, bottom = 90 };
        Assert.True(EditClicks.Overlaps(area, 190, 60, 250, 80));
        Assert.True(EditClicks.Overlaps(area, 120, 60, 130, 80));
        Assert.True(EditClicks.Overlaps(area, 0, 0, 400, 400));
        Assert.False(EditClicks.Overlaps(area, 200, 60, 250, 80));
        Assert.False(EditClicks.Overlaps(area, 120, 90, 130, 120));
        Assert.False(EditClicks.Overlaps(area, 20, 60, 100, 80));
    }

    /// <summary>A widget gone from the file leaves the selection; the order a display has them in is the list's.</summary>
    [Fact]
    public void AWidgetGoneLeavesTheSelection()
    {
        var selection = new EditSelection();
        selection.Set([Key("e"), Key("b"), Key("c")], []);
        selection.Keep(key => key != Key("c"));

        Assert.Equal([Key("e"), Key("b")], selection.Keys);
        Assert.Equal([Key("b"), Key("e")], selection.InOrder(Order));
    }

    // --- Landing together ------------------------------------------------------------------------

    private static BarItem Entry(string id, BarKind kind, BarSection section) =>
        new() { Id = id, Kind = kind, Section = section };

    /// <summary>
    /// Three widgets from three places land side by side in a fourth, in their order, before the entry
    /// the slot names; the rest keep their order.
    /// </summary>
    [Fact]
    public void SeveralLandSideBySideInTheirOrder()
    {
        var weather = Entry("w", BarKind.WidgetsBoard, BarSection.End);
        var clock = Entry("c", BarKind.NightLight, BarSection.End);
        var battery = Entry("b", BarKind.EnergySaver, BarSection.Middle);
        var media = Entry("m", BarKind.WidgetsBoard, BarSection.Start);
        var date = Entry("d", BarKind.NightLight, BarSection.Start);
        List<BarItem> list = [weather, clock, battery, media, date];
        Sections.Order(list);

        // Before the date, in the start: the list without them is [media, date] there.
        var rest = list.Where(island => island != weather && island != clock && island != battery).ToList();
        var at = rest.IndexOf(date);
        Assert.True(IslandEdits.MoveGroup(list, [weather, clock, battery], new BarSlot(BarSection.Start, at)));

        Assert.Equal(["m", "w", "c", "b", "d"], list.Select(island => island.Id));
        Assert.All(list, island => Assert.Equal(BarSection.Start, island.Section));
        Assert.False(IslandEdits.MoveGroup(list, [weather, clock, battery], new BarSlot(BarSection.Start, 1)));
        Assert.False(IslandEdits.MoveGroup(list, [Entry("x", BarKind.NightLight, BarSection.End)], default));
    }

    /// <summary>Copies go in side by side at the place, each a deep copy with an id nothing in the list has; the originals stay.</summary>
    [Fact]
    public void PastedCopiesHaveNewIds()
    {
        var clock = Entry("c", BarKind.NightLight, BarSection.End);
        clock.Clock = new ClockOptions { Label = "Home" };
        var weather = Entry("w", BarKind.WidgetsBoard, BarSection.Start);
        List<BarItem> list = [weather, clock];
        Sections.Order(list);
        var copied = new[] { clock.Clone(), weather.Clone() };

        var added = IslandEdits.Paste(list, copied, new BarSlot(BarSection.Middle, 1));

        Assert.Equal(2, added.Count);
        Assert.Equal(4, list.Count);
        Assert.Equal([BarKind.NightLight, BarKind.WidgetsBoard], added.Select(island => island.Kind));
        Assert.All(added, island => Assert.Equal(BarSection.Middle, island.Section));
        Assert.Equal(list.Count, list.Select(island => island.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(added, island => island.Id is "c" or "w");
        Assert.Equal("Home", added[0].Clock!.Label);
        // A deep copy: the copy's options are its own.
        added[0].Clock!.Label = "Away";
        Assert.Equal("Home", clock.Clock!.Label);
        // Pasted again, new ids again.
        var again = IslandEdits.Paste(list, copied, new BarSlot(BarSection.Middle, 0));
        Assert.Equal(6, list.Select(island => island.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(again, island => added.Any(first => first.Id == island.Id));
    }

    // --- How the bars stand meanwhile ------------------------------------------------------------

    private static readonly Config Bar48 = new() { Height = 48 };

    private static PartSpec Spec(string id, BarKind kind, BarSection section) => new(Key(id), kind) { Section = section };

    /// <summary>
    /// Carried from the bar into an empty place: the place shows one landing round them all, as long
    /// as both of them and the room between; the bar closes up behind both, the section they left
    /// empty an empty room again.
    /// </summary>
    [Fact]
    public void TheLandingIsSizedForTheGroupAndTheBarClosesUpBehindIt()
    {
        PartSpec[] parts =
        [
            Spec("w", BarKind.WidgetsBoard, BarSection.Start),
            Spec("m", BarKind.WidgetsBoard, BarSection.Middle),
            Spec("c", BarKind.NightLight, BarSection.End),
        ];
        var bar = Layout.ForTests(96, new RECT { right = 1920, bottom = 1080 }, Edge.Bottom, Bar48, parts, 40f, editing: true, cells: [1, 3, 1]);
        var place = Layout.ForTests(96, new RECT { left = 640, right = 1280, bottom = 1080 }, Edge.Top, Bar48, [], 40f, editing: true, only: BarSection.Middle);
        var group = new[] { bar.Carry(parts[0].Key)!, bar.Carry(parts[2].Key)! };

        var landed = place.Landing(new BarSlot(BarSection.Middle, 0), group);

        Assert.True(landed.Rect.Width >= bar.BlockRect(0).Width + bar.BlockRect(2).Width - 1f, $"{landed.Rect.Width} wide for {bar.BlockRect(0).Width} and {bar.BlockRect(2).Width}");
        Assert.Empty(landed.Rooms);

        int[] mine = [0, 2];
        var without = bar.Without(mine);
        Assert.Equal([parts[1].Key], without.Specs.Select(spec => spec.Key));
        var closed = without.Landing(default, []);
        Assert.True(closed.Rect.IsEmpty);
        // The start and the end are empty rooms again.
        Assert.Equal(2, closed.Rooms.Count);
        var lefts = bar.Remaining(mine, closed.Lefts);
        Assert.Equal(3, lefts.Length);
        Assert.Equal(bar.PartAt(0).Left, lefts[0]);
        Assert.Equal(bar.PartAt(2).Left, lefts[2]);
        Assert.Equal(without.PartAt(0).Left, lefts[1]);
    }

    /// <summary>Carried along their own bar, the others open the room for both together where they would land.</summary>
    [Fact]
    public void AlongTheirOwnBarTheOthersOpenTheRoomForAll()
    {
        PartSpec[] parts =
        [
            Spec("a", BarKind.EnergySaver, BarSection.Middle),
            Spec("b", BarKind.WidgetsBoard, BarSection.Middle),
            Spec("c", BarKind.NightLight, BarSection.Middle),
            Spec("d", BarKind.NightLight, BarSection.Middle),
        ];
        var bar = Layout.ForTests(96, new RECT { right = 1920, bottom = 1080 }, Edge.Bottom, Bar48, parts, 40f, editing: true);
        int[] mine = [1, 3];
        var without = bar.Without(mine);
        var group = new[] { bar.Carry(parts[1].Key)!, bar.Carry(parts[3].Key)! };

        // Before the first that is left: the battery.
        var landed = without.Landing(new BarSlot(BarSection.Middle, 0), group);
        var lefts = bar.Remaining(mine, landed.Lefts);

        Assert.Equal(2, landed.Lefts.Length);
        Assert.True(landed.Rect.Right <= lefts[0] + 1f, "the landing before the battery");
        Assert.True(lefts[2] > lefts[0], "the date after it");
        Assert.True(landed.Rect.Width >= bar.BlockRect(1).Width + bar.BlockRect(3).Width - 1f);
    }

    // --- The carry window -------------------------------------------------------------------------

    [Fact]
    public void TheCarriedStandSideBySideOnTheirMiddles()
    {
        var (at, size) = EditCarryPlan.Row([new Vector2(40, 40), new Vector2(120, 32), new Vector2(40, 40)], vertical: false, gap: 4);

        Assert.Equal([new Vector2(0, 0), new Vector2(44, 4), new Vector2(168, 0)], at);
        Assert.Equal(new Vector2(208, 40), size);

        var (down, tall) = EditCarryPlan.Row([new Vector2(40, 40), new Vector2(32, 80)], vertical: true, gap: 6);
        Assert.Equal([new Vector2(0, 0), new Vector2(4, 46)], down);
        Assert.Equal(new Vector2(40, 126), tall);
        Assert.Equal(Vector2.Zero, EditCarryPlan.Row([], false, 4).Size);
    }

    // --- The widget's menu -------------------------------------------------------------------------

    /// <summary>
    /// A widget's menu while the bar is arranged has one "Copy", the row's button, and no "Paste"
    /// (the person, 2026-10-11: "in edit mode the copy and paste context menu actions are duplicated
    /// ... pasting should actually not be an option"): its rows are the buttons alone, and "Copy"
    /// hands the screen to no other program, so a folder's menu stays over it.
    /// </summary>
    [Fact]
    public void AWidgetsMenuHasOneCopyAndNoPaste()
    {
        var rows = TheDock.WidgetRowsFor(new BarItem { Id = "c", Kind = BarKind.NightLight });

        Assert.Equal(["Cut", "Copy", "Remove"], rows.Select(row => row.Text));
        Assert.All(rows, row => Assert.True(row.Primary));
        Assert.False(TheDock.PickHandsAway([], rows[0].Id));
    }
}
