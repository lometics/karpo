// What G20's tray fix left undone, as Windows 11's notification area does it:
// the chevron to drop onto while a drag goes on, the overflow a drag opens at
// once and keeps open (TrayDragOverflow), the icon carried under the pointer
// (TrayCarrying), a name for each of two icons that shared one
// (TrayNames), and the icons a tray of a set size has no room for, which go into
// the overflow instead of nowhere (Layout.TraySpills, TrayDrags.Overflow).

using Karpo.Dock;
using TerraFX.Interop.Windows;

// Not Karpo.Tests.Tray: a local named "tray" elsewhere in the tests would shadow a namespace of that name.
namespace Karpo.Tests.TrayIcons;

public sealed class TrayOverflowDragTests
{
    // --- Two icons, two names ---------------------------------------------------------

    private static List<string> Names(TrayNames names, params (string Key, string Identity)[] icons) => names.Name(icons);

    [Fact]
    public void ALoneIconKeepsThePlainName()
    {
        var names = new TrayNames();

        Assert.Equal(["app|1", "{guid}"], Names(names, ("A/1", "app|1"), ("{guid}", "{guid}")));
    }

    [Fact]
    public void TwoWindowsOfOneAppWithOneIdGetANameEach()
    {
        // The defect: two windows of one app, each adding an icon with uID 1, were both "app|1",
        // so a drag of one moved the other with it. Windows tells them apart by their windows.
        var names = new TrayNames();

        Assert.Equal(["app|1", "app|1#2"], Names(names, ("A/1", "app|1"), ("B/1", "app|1")));
    }

    [Fact]
    public void TwoGuidsOfOneAppsFamilyGetANameEach()
    {
        // Two GUID icons of one packaged app both fall to "family|guid" (TrayIdentity.Of, kept so
        // that a new build's GUID finds the old one's place). Windows tells them apart by GUID.
        const string family = @"c:\program files\windowsapps\app__p\app.exe|guid";
        var names = new TrayNames();

        Assert.Equal([family, family + "#2"], Names(names, ("{one}", family), ("{two}", family)));
    }

    [Fact]
    public void ANameStaysWithItsIconWhileItLives()
    {
        var names = new TrayNames();
        _ = Names(names, ("A/1", "app|1"), ("B/1", "app|1"));

        // The first window closes: the second keeps "#2", so its place and choice don't jump to the first's.
        Assert.Equal(["app|1#2"], Names(names, ("B/1", "app|1")));
        Assert.Equal("app|1#2", names.Of("B/1"));
        Assert.Null(names.Of("A/1"));

        // A window that comes back takes the plain name it left free.
        Assert.Equal(["app|1#2", "app|1"], Names(names, ("B/1", "app|1"), ("C/1", "app|1")));
    }

    [Fact]
    public void ANewcomerNeverTakesANameAnotherIconHolds()
    {
        var names = new TrayNames();
        _ = Names(names, ("A/1", "app|1"));

        // The newcomer is listed first; the icon that had the name keeps it.
        Assert.Equal(["app|1#2", "app|1"], Names(names, ("B/1", "app|1"), ("A/1", "app|1")));
    }

    [Fact]
    public void AnIconWhoseNameChangesUnderneathIsNamedAfresh()
    {
        // Its executable was not known yet the first time ("unknown|1").
        var names = new TrayNames();
        _ = Names(names, ("A/1", "unknown|1"));

        Assert.Equal(["app|1"], Names(names, ("A/1", "app|1")));
    }

    [Fact]
    public void TheMarkIsTakenOffOnlyWhereItIsOne()
    {
        Assert.Equal("app|1", TrayNames.Base("app|1#2"));
        Assert.Equal("app|1", TrayNames.Base("app|1"));
        Assert.Equal(@"c:\c#\app.exe|1", TrayNames.Base(@"c:\c#\app.exe|1"));
        Assert.Equal("app|1#", TrayNames.Base("app|1#"));
    }

    [Fact]
    public void TwoNamedIconsOfOneAppAreDraggedApart()
    {
        // With a name each, a drag moves the one dragged and leaves the other where it was.
        var config = new TrayIconOptions();
        TrayEntry[] icons = [new("app|1", false), new("app|1#2", false), new("other|0", false)];

        Assert.True(TrayLayout.Apply(config, icons, "app|1#2", new TrayDrop(TrayArea.Overflow, 0)));
        var (shown, hidden) = TrayLayout.Split(config, icons);

        Assert.Equal(["app|1", "other|0"], shown.Select(icon => icon.Identity));
        Assert.Equal(["app|1#2"], hidden.Select(icon => icon.Identity));
    }

    // --- A tray of a set size -----------------------------------------------------------

    private static readonly string TrayKey = Config.IslandKey(null, "tray");

    private static Layout Bar(uint dpi, int icons, bool overflow, int cells = WidgetItem.AutomaticWidth)
    {
        var config = new Config { Height = 48 };
        List<PartSpec> specs =
        [
            new(Config.IslandKey(null, "battery"), BarKind.EnergySaver),
            new(TrayKey, BarKind.Tray, cells),
            new(Config.IslandKey(null, "wifi"), BarKind.TouchKeyboard),
        ];
        return Layout.ForTests(dpi, new RECT { right = 2600, bottom = 1200 }, Edge.Bottom, config, specs, Layout.CellPx(dpi, config), tray: icons, trayOverflow: overflow);
    }

    [Theory]
    [InlineData(96u)]
    [InlineData(144u)]
    public void ATrayTooSmallForItsIconsShowsTheChevron(uint dpi)
    {
        // The defect: three cells of room, more icons to show than they hold and none hidden: the
        // last ones showed neither on the bar nor behind a chevron, as there was none.
        var layout = Bar(dpi, icons: 8, overflow: false, cells: 3);

        Assert.True(layout.TrayOverflow);
        Assert.False(layout.ItemRect(Hit.TrayOverflow).IsEmpty);
        // The chevron takes one of the tray cells the room holds; the icons stand in the rest. The
        // room is three of the bar's cells, as many tray cells as fit in them side by side.
        var cells = (int)MathF.Floor(3 * layout.CellSide / layout.TrayStep);
        Assert.Equal(cells - 1, layout.PartAt(layout.PartOf(Hit.TrayOverflow)).Count);
        Assert.InRange(cells - 1, 1, 4);
    }

    [Fact]
    public void ATrayWithRoomForItsIconsNeedsNoChevron()
    {
        // Three cells of room hold two tray cells side by side.
        Assert.False(Bar(96, icons: 2, overflow: false, cells: 3).TrayOverflow);
        // Without a set size the tray grows with its icons.
        Assert.False(Bar(96, icons: 9, overflow: false).TrayOverflow);
        Assert.False(Layout.TraySpills(new PartSpec(TrayKey, BarKind.Tray, 3), 0, 44, 44));
    }

    [Fact]
    public void TheIconsWithoutRoomOpenTheOverflowFirst()
    {
        // Five to show, two drawn, one hidden: the three that don't fit, then the hidden one.
        Assert.Equal(["c", "d", "e", "h"], TrayDrags.Overflow(["a", "b", "c", "d", "e"], 2, ["h"]));
        Assert.Equal(["h"], TrayDrags.Overflow(["a", "b"], 2, ["h"]));
        Assert.Equal(["a", "h"], TrayDrags.Overflow(["a"], 0, ["h"]));
    }

    [Fact]
    public void ADropAmongTheHiddenCountsAfterTheIconsWithoutRoom()
    {
        // Rows: c, d (no room on the bar), then h1, h2 (hidden). From the bar:
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 0), TrayDrags.InOverflow(0, 4, spilled: 2));
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 0), TrayDrags.InOverflow(2, 4, spilled: 2));
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 1), TrayDrags.InOverflow(3, 4, spilled: 2));
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 2), TrayDrags.InOverflow(4, 4, spilled: 2));

        // d itself (row 1) dragged past h1: first among the hidden after c, which keeps its row.
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 1), TrayDrags.InOverflow(3, 4, from: 1, spilled: 2));
        // h2 (row 3) dragged before h1: first among the hidden.
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 0), TrayDrags.InOverflow(2, 4, from: 3, spilled: 2));
        // With nothing spilled it is G20's rule unchanged.
        Assert.Equal(TrayDrags.InOverflow(3, 4, from: 1), TrayDrags.InOverflow(3, 4, from: 1, spilled: 0));
    }

    // --- The overflow opened by a drag ---------------------------------------------------

    [Fact]
    public void ADragOpensTheOverflowAtOnceEvenEmpty()
    {
        // Past the drag's threshold (a click begins no drag, TrayClicks): open on the first move,
        // with nothing hidden behind the chevron as much as with icons.
        var keep = new TrayDragOverflow(wasOpen: false);

        Assert.True(keep.Opens(open: false, chevron: true));
        Assert.True(keep.Opened);
        // It stands open for the drag: wherever the icon goes, nothing more.
        Assert.False(keep.Opens(open: true, chevron: true));
        Assert.False(keep.Opens(open: true, chevron: true));
        Assert.True(keep.Opened);
    }

    [Fact]
    public void TheOverflowWaitsForTheChevronTheDragBrings()
    {
        // Nothing hidden: the chevron comes with the drag, a layout later; the overflow opens from it then.
        var keep = new TrayDragOverflow(wasOpen: false);

        Assert.False(keep.Opens(open: false, chevron: false));
        Assert.False(keep.Opened);
        Assert.True(keep.Opens(open: false, chevron: true));
    }

    [Fact]
    public void TheOverflowTheDragOpenedClosesWithIt()
    {
        // Let go anywhere (in it, on the bar) or called off: it goes, as it was closed before.
        var keep = new TrayDragOverflow(wasOpen: false);
        _ = keep.Opens(open: false, chevron: true);

        Assert.True(keep.Ended());
        Assert.False(keep.Opened);
    }

    [Fact]
    public void AnOverflowThatWasOpenAlreadyIsLeftAlone()
    {
        // The drag began in the overflow: it stays open for it and after it.
        var keep = new TrayDragOverflow(wasOpen: true);

        Assert.False(keep.Opens(open: true, chevron: true));
        Assert.False(keep.Ended());
        // Even closed under the drag, it isn't the drag's to open again.
        Assert.False(keep.Opens(open: false, chevron: true));
    }

    [Fact]
    public void AnOverflowClosedBySomethingElseIsNoLongerTheDragsOwn()
    {
        var keep = new TrayDragOverflow(wasOpen: false);
        _ = keep.Opens(open: false, chevron: true);

        // Escape closed it: it isn't opened again, and there is nothing to close at the end.
        Assert.False(keep.Opens(open: false, chevron: true));
        Assert.False(keep.Opened);
        Assert.False(keep.Ended());
    }

    // --- The icon carried under the pointer ------------------------------------------------

    [Theory]
    [InlineData(500, 1050, 24, 488, 1038)]
    // Up off a bottom bar into the overflow, and sideways: both axes follow the pointer.
    [InlineData(430, 960, 24, 418, 948)]
    [InlineData(10, 300, 36, -8, 282)]
    public void TheCarriedIconIsHeldByItsMiddleWhereverThePointerIs(int x, int y, int size, int left, int top)
    {
        var corner = TrayCarrying.Corner(new POINT { x = x, y = y }, size);

        Assert.Equal(left, corner.x);
        Assert.Equal(top, corner.y);
    }

    // --- Where a drop lands in the overflow, and the room the icons make ------------------------

    // Two lines of three cells, 40 px fills 44 px apart, and a fourth on the second line.
    private static readonly (float X, float Y, float W, float H)[] Grid =
        [(2, 2, 40, 40), (46, 2, 40, 40), (90, 2, 40, 40), (2, 46, 40, 40)];

    [Theory]
    [InlineData(10, 20, 0)]
    [InlineData(30, 20, 1)]
    [InlineData(50, 20, 1)]
    [InlineData(80, 20, 2)]
    [InlineData(125, 20, 3)]
    // In the air between two cells, and past the last on its line: the nearest cell decides.
    [InlineData(44, 20, 1)]
    [InlineData(70, 60, 4)]
    [InlineData(10, 60, 3)]
    public void AnIconFromTheBarLandsBeforeOrAfterTheNearestCell(float x, float y, int expected) =>
        Assert.Equal(expected, Flyout.DropAt(Grid, x, y, grid: true));

    [Fact]
    public void ThePlaceItHasHoldsAFifthOfACellPastTheMiddle()
    {
        // The second cell's middle is at 66: before it is 1, after it 2.
        Assert.Equal(1, Flyout.DropAt(Grid, 64, 20, grid: true));
        Assert.Equal(2, Flyout.DropAt(Grid, 68, 20, grid: true));
        // Having been 1, it stays 1 a little past the middle, and 2 a little before it.
        Assert.Equal(1, Flyout.DropAt(Grid, 68, 20, grid: true, current: 1));
        Assert.Equal(1, Flyout.DropAt(Grid, 66 + 7.9f, 20, grid: true, current: 1));
        Assert.Equal(2, Flyout.DropAt(Grid, 66 + 8.1f, 20, grid: true, current: 1));
        Assert.Equal(2, Flyout.DropAt(Grid, 64, 20, grid: true, current: 2));
        Assert.Equal(1, Flyout.DropAt(Grid, 66 - 8.1f, 20, grid: true, current: 2));
        // A place it had far away is no reason to stay.
        Assert.Equal(1, Flyout.DropAt(Grid, 50, 20, grid: true, current: 3));
    }

    [Fact]
    public void AnIconTakenFromHereGoesToTheCellItIsOver()
    {
        // The second (held) dragged over the fourth: it goes where the fourth is, so the index is
        // after the fourth (its own place counted), which InOverflow counts out again.
        Assert.Equal(4, Flyout.DropAt(Grid, 20, 60, grid: true, held: 1));
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 3), TrayDrags.InOverflow(4, 4, from: 1));
        // Over its own place, and over the first.
        Assert.Equal(2, Flyout.DropAt(Grid, 66, 20, grid: true, held: 1));
        Assert.Equal(0, Flyout.DropAt(Grid, 20, 20, grid: true, held: 1));
        // Having the third (index 3), a pointer just over the edge into the second keeps it.
        Assert.Equal(3, Flyout.DropAt(Grid, 86, 20, grid: true, held: 0, current: 3));
        Assert.Equal(2, Flyout.DropAt(Grid, 70, 20, grid: true, held: 0, current: 3));
    }

    [Fact]
    public void TheIconsAfterTheGapMoveACellOn()
    {
        var places = Flyout.GapPlaces(Grid, 1, held: -1, grid: true);
        Assert.Equal((2f, 2f), places[0]);
        Assert.Equal((90f, 2f), places[1]);
        Assert.Equal((2f, 46f), places[2]);
        // The last goes on along its line.
        Assert.Equal((46f, 46f), places[3]);

        // No gap: every icon in its place.
        Assert.Equal(Grid.Select(fill => (fill.X, fill.Y)), Flyout.GapPlaces(Grid, -1, held: -1, grid: true));
    }

    [Fact]
    public void TheIconTakenFromHereLeavesItsPlaceClosedUpBehindIt()
    {
        // The first taken, to land before the fourth (index 3): the second and third close up into
        // the first two places, the gap is the third, and the fourth stays.
        var places = Flyout.GapPlaces(Grid, 3, held: 0, grid: true);
        Assert.Equal((2f, 2f), places[1]);
        Assert.Equal((46f, 2f), places[2]);
        Assert.Equal((2f, 46f), places[3]);
        // Its own entry is its place: it follows the pointer, not a cell.
        Assert.Equal((2f, 2f), places[0]);
    }

    [Fact]
    public void AFullLinesGapOpensTheNextLine()
    {
        (float X, float Y, float W, float H)[] three = [(2, 2, 40, 40), (46, 2, 40, 40), (90, 2, 40, 40)];
        var places = Flyout.GapPlaces(three, 0, held: -1, grid: true);
        Assert.Equal([(46f, 2f), (90f, 2f), (2f, 46f)], places);

        // A list: a row's step down.
        (float X, float Y, float W, float H)[] list = [(4, 2, 200, 32), (4, 38, 200, 32)];
        Assert.Equal([(4f, 38f), (4f, 74f)], Flyout.GapPlaces(list, 0, held: -1, grid: false));
        Assert.Equal(1, Flyout.DropAt(list, 50, 30, grid: false));
    }
}
