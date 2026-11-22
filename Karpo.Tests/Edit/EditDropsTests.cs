// Where a widget carried in edit mode lands (2026-10-07): anywhere on the screen, on the surface of
// the display nearest the pointer (EditDrops.Nearest), the one it came from keeping a tie; and how the
// surface it goes to lays itself out with it in, at the size it will have there (Layout.Carry,
// Layout.Landing), while the one it left closes up behind it (Layout.Closed). Pure: no window.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class EditDropsTests
{
    private static RECT Rect(int left, int top, int right, int bottom) => new() { left = left, top = top, right = right, bottom = bottom };

    private static POINT At(int x, int y) => new() { x = x, y = y };

    // A display 1920 by 1080: the bar along the bottom, a place in the top's middle third, one in the left's start.
    private static readonly RECT[] Areas = [Rect(0, 1032, 1920, 1080), Rect(640, 0, 1280, 56), Rect(0, 40, 56, 370)];

    private static readonly bool[] AllOpen = [true, true, true];

    [Fact]
    public void ThePointerOnASurfaceGoesToIt()
    {
        Assert.Equal(0, EditDrops.Nearest(At(900, 1050), Areas, AllOpen));
        Assert.Equal(1, EditDrops.Nearest(At(900, 10), Areas, AllOpen));
        Assert.Equal(2, EditDrops.Nearest(At(20, 200), Areas, AllOpen));
    }

    /// <summary>Anywhere on the screen there is somewhere to land: the nearest surface.</summary>
    [Fact]
    public void AnywhereElseTheNearestSurfaceHasIt()
    {
        // The screen's middle: the top place is 484 px off, the bar 492.
        Assert.Equal(1, EditDrops.Nearest(At(960, 540), Areas, AllOpen));
        // Low on the screen: the bar.
        Assert.Equal(0, EditDrops.Nearest(At(960, 800), Areas, AllOpen));
        // Near the top left corner, beside the left place.
        Assert.Equal(2, EditDrops.Nearest(At(200, 120), Areas, AllOpen));
        // The top right corner, where nothing stands: the top place, which is nearer than the bar.
        Assert.Equal(1, EditDrops.Nearest(At(1900, 20), Areas, AllOpen));
    }

    /// <summary>A tie goes to the surface the block came from, the first; one that takes nothing is passed over.</summary>
    [Fact]
    public void ATieKeepsItsSourceAndAClosedSurfaceTakesNothing()
    {
        RECT[] twins = [Rect(0, 0, 100, 10), Rect(0, 31, 100, 41)];
        Assert.Equal(0, EditDrops.Nearest(At(50, 20), twins, [true, true]));
        Assert.Equal(1, EditDrops.Nearest(At(50, 20), twins, [false, true]));
        Assert.Equal(-1, EditDrops.Nearest(At(50, 20), twins, [false, false]));
    }

    [Fact]
    public void TheDistanceIsNothingInsideAndSquaredOutside()
    {
        var area = Rect(10, 10, 20, 20);
        Assert.Equal(0L, EditDrops.Distance(area, At(10, 10)));
        Assert.Equal(0L, EditDrops.Distance(area, At(19, 19)));
        Assert.Equal(1L, EditDrops.Distance(area, At(20, 15)));
        Assert.Equal(25L, EditDrops.Distance(area, At(5, 15)));
        Assert.Equal(2L, EditDrops.Distance(area, At(9, 9)));
    }

    /// <summary>
    /// A widget carried from the bar to an empty place: the place lays itself out with it in, its
    /// ghost the widget's own length there, the place's empty room gone; and the bar it left closes up,
    /// the section it was alone in an empty room again.
    /// </summary>
    [Fact]
    public void ACarriedWidgetLandsAtItsOwnSizeAndItsBarClosesUp()
    {
        var config = new Config { Height = 48 };
        var screen = new RECT { right = 1920, bottom = 1080 };
        PartSpec[] bar =
        [
            new(Config.IslandKey(null, "w"), BarKind.WidgetsBoard) { Section = BarSection.Start },
            new(Config.IslandKey(null, "m"), BarKind.WidgetsBoard) { Section = BarSection.Middle },
            new(Config.IslandKey(null, "c"), BarKind.NightLight) { Section = BarSection.End },
        ];
        var from = Layout.ForTests(96, screen, Edge.Bottom, config, bar, 40f, editing: true, cells: [1, 3, 1]);
        var place = Layout.ForTests(96, new RECT { left = 640, right = 1280, bottom = 1080 }, Edge.Top, config, [], 40f, editing: true, only: BarSection.Middle);
        Assert.Single(place.EmptyRooms());

        var carried = Assert.IsType<Layout.Carried>(from.Carry(bar[1].Key));
        var landed = place.Landing(new BarSlot(BarSection.Middle, 0), carried);

        Assert.False(landed.Rect.IsEmpty);
        Assert.Equal(from.BlockRect(1).Width, landed.Rect.Width, 0.5f);
        Assert.Empty(landed.Rooms);
        Assert.Empty(landed.Lefts);

        var closed = from.Closed(1);
        Assert.Single(closed.Rooms);
        Assert.Equal(from.PartAt(0).Left, closed.Lefts[0]);
        Assert.Equal(from.PartAt(2).Left, closed.Lefts[2]);
        Assert.Null(from.Carry("nothing/here"));
    }

    /// <summary>Carried into a place that holds widgets, the others open the room it will take, on the side it goes to.</summary>
    [Fact]
    public void TheOthersOpenTheRoomItWillTake()
    {
        var config = new Config { Height = 48 };
        PartSpec[] held =
        [
            new(Config.IslandKey(null, "a"), BarKind.EnergySaver) { Section = BarSection.Middle },
            new(Config.IslandKey(null, "b"), BarKind.WidgetsBoard) { Section = BarSection.Middle },
        ];
        var place = Layout.ForTests(96, new RECT { left = 640, right = 1280, bottom = 1080 }, Edge.Top, config, held, 40f, editing: true, only: BarSection.Middle);
        var from = Layout.ForTests(96, new RECT { right = 1920, bottom = 1080 }, Edge.Bottom, config, [new(Config.IslandKey(null, "c"), BarKind.NightLight)], 40f, editing: true);

        var landed = place.Landing(new BarSlot(BarSection.Middle, 1), Assert.IsType<Layout.Carried>(from.Carry(Config.IslandKey(null, "c"))));

        Assert.Equal(2, landed.Lefts.Length);
        // The middle stays centred as it grows: the first moves back, the second on, past the ghost.
        Assert.True(landed.Lefts[0] < place.PartAt(0).Left);
        Assert.True(landed.Lefts[1] > place.PartAt(1).Left);
        Assert.True(landed.Rect.X >= landed.Lefts[0] + place.PartAt(0).Width - 1f && landed.Rect.Right <= landed.Lefts[1] + 1f);
    }
}
