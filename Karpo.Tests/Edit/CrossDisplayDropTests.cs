// §519: a widget carried in edit mode lands on any display (the person, 2026-11-06: "dragging and
// dropping widgets across displays does not work but this should obviously work in all the places and
// for all the widget types"). One drop path for every surface of every display: the surface nearest the
// pointer on the display it is over (EditDrops.Nearest), the ghost at the size the widget will have
// there whatever that display's scale (Layout.Landing rescales what Layout.Carry measured), and the move
// from one layout's list to another's — or along one list where both displays show the same layout —
// in one change (IslandEdits.MoveTo). Pure: no window.

using Karpo.Dock;
using Karpo.Tests.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class CrossDisplayDropTests
{
    private static RECT Rect(int left, int top, int right, int bottom) => new() { left = left, top = top, right = right, bottom = bottom };

    private static POINT At(int x, int y) => new() { x = x, y = y };

    private static BarItem Entry(string id, BarKind kind, BarSection section = BarSection.Start) => new() { Id = id, Kind = kind, Section = section };

    /// <summary>Display A shows layout "work", display B layout "home"; or both "work" where <paramref name="same"/>.</summary>
    private static Config TwoDisplays(bool same = false) => new()
    {
        Layouts =
        [
            new TaskbarLayout
            {
                Id = "work",
                Name = "Work",
                Islands =
                [
                    Entry("start", BarKind.Start),
                    Entry("night", BarKind.NightLight),
                    Entry("tray", BarKind.Tray, BarSection.End),
                    Entry("energy", BarKind.EnergySaver, BarSection.End),
                ],
            },
            new TaskbarLayout
            {
                Id = "home",
                Name = "Home",
                Islands =
                [
                    Entry("apps", BarKind.Apps),
                    Entry("volume", BarKind.Volume, BarSection.End),
                ],
            },
        ],
        DefaultLayoutId = "work",
        Screens =
        [
            new Screen { Id = "A", ActiveLayoutId = "work" },
            new Screen { Id = "B", ActiveLayoutId = same ? "work" : "home" },
        ],
    };

    private static string A(string id) => Config.IslandKey(Config.DisplayOwner("A"), id);

    private static string B(string id) => Config.IslandKey(Config.DisplayOwner("B"), id);

    private static List<string> Ids(IEnumerable<BarItem> list) => [.. list.Select(island => island.Id)];

    // --- The move, in the file -------------------------------------------------------------------

    /// <summary>
    /// A widget let go on another display's bar leaves its layout for that display's, at the place it
    /// showed, in that section, with its own id and options; the layout it left closes up.
    /// </summary>
    [Fact]
    public void AWidgetLetGoOnAnotherDisplayMovesToThatDisplaysLayout()
    {
        var config = TwoDisplays();
        var night = config.Layouts[0].Islands[1];

        Assert.True(IslandEdits.MoveTo(config, [A("night")], Config.DisplayOwner("B"), new BarSlot(BarSection.End, 1)));

        Assert.Equal(["start", "tray", "energy"], Ids(config.IslandsFor("A")));
        Assert.Equal(["apps", "night", "volume"], Ids(config.IslandsFor("B")));
        var moved = config.IslandsFor("B")[1];
        Assert.Same(night, moved);
        Assert.Equal("night", moved.Id);
        Assert.Equal(BarSection.End, moved.Section);
    }

    /// <summary>Every kind moves so: the tray, a folder with its lines, whatever the layout it goes to holds.</summary>
    [Theory]
    [InlineData("tray")]
    [InlineData("energy")]
    [InlineData("start")]
    public void EveryKindMovesAcross(string id)
    {
        var config = TwoDisplays();

        Assert.True(IslandEdits.MoveTo(config, [A(id)], Config.DisplayOwner("B"), new BarSlot(BarSection.Start, 0)));

        Assert.DoesNotContain(id, Ids(config.IslandsFor("A")));
        Assert.Equal(id, config.IslandsFor("B")[0].Id);
    }

    /// <summary>A folder carried to another display takes its lines with it, each with its own id.</summary>
    [Fact]
    public void AFolderMovesWithItsLines()
    {
        var config = TwoDisplays();
        var folder = Configs.FolderKind(FolderView.List).New();
        folder.Id = "folder";
        var timer = ListKind.Timer.New();
        timer.Id = "timer";
        folder.Folder!.Put(timer);
        config.Layouts[0].Islands.Insert(2, folder);

        Assert.True(IslandEdits.MoveTo(config, [A("folder")], Config.DisplayOwner("B"), new BarSlot(BarSection.Start, 1)));

        Assert.Null(config.FindIsland(Config.DisplayOwner("A"), "folder/timer"));
        Assert.Same(timer, config.FindIsland(Config.DisplayOwner("B"), "folder/timer"));
        Assert.Equal(["apps", "folder", "volume"], Ids(config.IslandsFor("B")));
    }

    /// <summary>Several carried together land side by side in their order on the other display, and leave the first.</summary>
    [Fact]
    public void SeveralCarriedTogetherMoveAcrossInTheirOrder()
    {
        var config = TwoDisplays();

        Assert.True(IslandEdits.MoveTo(config, [A("energy"), A("night")], Config.DisplayOwner("B"), new BarSlot(BarSection.Start, 1)));

        Assert.Equal(["start", "tray"], Ids(config.IslandsFor("A")));
        Assert.Equal(["apps", "energy", "night", "volume"], Ids(config.IslandsFor("B")));
        Assert.All(config.IslandsFor("B").Take(3), island => Assert.Equal(BarSection.Start, island.Section));
    }

    /// <summary>
    /// Two displays showing the same layout show one list: a widget carried from one to the other moves
    /// along that list, never into it twice.
    /// </summary>
    [Fact]
    public void ToADisplayShowingTheSameLayoutItMovesAlongTheOneList()
    {
        var config = TwoDisplays(same: true);

        Assert.True(IslandEdits.MoveTo(config, [A("start")], Config.DisplayOwner("B"), new BarSlot(BarSection.End, 3)));

        Assert.Equal(["night", "tray", "energy", "start"], Ids(config.IslandsFor("B")));
        Assert.Equal(4, config.Layouts[0].Islands.Count);
        Assert.Empty(config.Layouts[1].Islands.Where(island => island.Id == "start"));
    }

    /// <summary>On its own display it is the move it always was; nothing changes where nothing moves.</summary>
    [Fact]
    public void OnItsOwnDisplayItIsTheMoveItAlwaysWas()
    {
        var config = TwoDisplays();

        Assert.True(IslandEdits.MoveTo(config, [A("start")], Config.DisplayOwner("A"), new BarSlot(BarSection.Start, 1)));
        Assert.Equal(["night", "start", "tray", "energy"], Ids(config.IslandsFor("A")));

        Assert.False(IslandEdits.MoveTo(config, [A("start")], Config.DisplayOwner("A"), new BarSlot(BarSection.Start, 1)));
        Assert.False(IslandEdits.MoveTo(config, [A("gone")], Config.DisplayOwner("B"), new BarSlot(BarSection.Start, 0)));
        Assert.False(IslandEdits.MoveTo(config, [A("start")], "nothing", new BarSlot(BarSection.Start, 0)));
        Assert.Equal(["apps", "volume"], Ids(config.IslandsFor("B")));
    }

    // --- Where it lands -----------------------------------------------------------------------------

    // Display A 1920×1080 at the origin, its bar along the bottom; display B to its right, 2560×1440,
    // its bar along the top.
    private static readonly RECT ScreenA = Rect(0, 0, 1920, 1080);
    private static readonly RECT ScreenB = Rect(1920, 0, 4480, 1440);
    private static readonly RECT[] Bars = [Rect(0, 1032, 1920, 1080), Rect(1920, 0, 4480, 64)];
    private static readonly RECT[] Screens = [ScreenA, ScreenB];

    /// <summary>The pointer on another display's bar: that bar has it.</summary>
    [Fact]
    public void ThePointerOnAnotherDisplaysBarGoesToIt()
    {
        Assert.Equal(1, EditDrops.Nearest(At(3000, 30), Bars, [true, true], Screens));
        Assert.Equal(0, EditDrops.Nearest(At(900, 1050), Bars, [true, true], Screens));
    }

    /// <summary>
    /// Anywhere on a display, a surface of that display has it, however near another display's is: the
    /// pointer low on B, beside A's bar, still goes to B's bar along B's top.
    /// </summary>
    [Fact]
    public void TheDisplayUnderThePointerHasItBeforeANearerSurfaceOfAnother()
    {
        Assert.Equal(1, EditDrops.Nearest(At(1930, 1060), Bars, [true, true], Screens));
        // A display with nothing to take it: the nearest surface of any other.
        Assert.Equal(0, EditDrops.Nearest(At(1930, 1060), Bars, [true, false], Screens));
        // Off every display (between them): the nearest surface, the one it came from keeping a tie.
        Assert.Equal(0, EditDrops.Nearest(At(1000, 1200), Bars, [true, true], Screens));
    }

    /// <summary>
    /// A widget measured on a display at 100 % and carried to one at 200 % shows its ghost there at the
    /// size it will have there: as wide as that display lays the same widget out itself.
    /// </summary>
    [Fact]
    public void TheGhostIsTheSizeTheWidgetWillHaveAtTheOtherDisplaysScale()
    {
        var config = new Config { Height = 48 };
        var key = Config.IslandKey(Config.DisplayOwner("A"), "w");
        PartSpec[] carried = [new(key, BarKind.WidgetsBoard) { Section = BarSection.Start }];
        var from = Layout.ForTests(96, ScreenA, Edge.Bottom, config, carried, 40f, editing: true, cells: [3]);
        var onB = Layout.ForTests(192, ScreenB, Edge.Top, config, [], 80f, editing: true);
        var itsOwn = Layout.ForTests(192, ScreenB, Edge.Top, config, carried, 80f, editing: true, cells: [3]);

        var landed = onB.Landing(new BarSlot(BarSection.Start, 0), Assert.IsType<Layout.Carried>(from.Carry(key)));

        Assert.Equal(itsOwn.BlockRect(0).Width, landed.Rect.Width, 1f);
        Assert.Equal(itsOwn.BlockRect(0).Height, landed.Rect.Height, 1f);

        // Several together, too.
        var group = onB.Landing(new BarSlot(BarSection.Start, 0), [from.Carry(key)!, from.Carry(key)!]);
        Assert.True(group.Rect.Width >= 2 * itsOwn.BlockRect(0).Width - 1f);
    }

    // --- The selection follows ----------------------------------------------------------------------

    /// <summary>What was selected on A and moved to B is selected on B, under its new key, the anchor too.</summary>
    [Fact]
    public void TheSelectionFollowsTheMoveUnderItsNewKeys()
    {
        var selection = new EditSelection();
        selection.Set([A("night"), A("energy"), A("start")], []);

        selection.Rename(new Dictionary<string, string> { [A("night")] = B("night"), [A("energy")] = B("energy") });

        Assert.Equal([B("night"), B("energy"), A("start")], selection.Keys);
        Assert.Equal(B("night"), selection.Anchor);
    }
}
