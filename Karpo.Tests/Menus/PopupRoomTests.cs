// What a popup opened from a bar stays clear of (the person, 2026-10-09: "the topbar implementation
// is also wrong"): another bar of the display, which holds no strip of the work area when it stands
// over the windows (Dock.PopupRoom), and in edit mode the display's edit bar, which stands
// right under a bar along the top where that bar's popups open (FlyoutHost.ClearOf). Pure: no
// window is made.

using Karpo.Dock;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class PopupRoomTests
{
    private static readonly RECT Work = new() { left = 0, top = 0, right = 1920, bottom = 1080 };

    private static RECT Rect(int left, int top, int right, int bottom) => new() { left = left, top = top, right = right, bottom = bottom };

    /// <summary>
    /// A popup of the bottom bar stops short of a top bar over the windows, and one of the top bar
    /// short of a bottom bar over the windows: Windows' work area keeps neither strip.
    /// </summary>
    [Fact]
    public void APopupStaysOffTheOtherBar()
    {
        var room = TheDock.PopupRoom(Work, [(Edge.Top, Rect(0, 0, 1920, 32)), (Edge.Bottom, Rect(0, 1032, 1920, 1080))]);

        Assert.Equal((32, 1032), (room.top, room.bottom));
        Assert.Equal((0, 1920), (room.left, room.right));
    }

    /// <summary>Left + right: a popup of one side's bar stops short of the other side's bar over the windows.</summary>
    [Fact]
    public void APopupStaysOffTheOtherSidesBar()
    {
        var room = TheDock.PopupRoom(Work, [(Edge.Left, Rect(0, 0, 72, 1080)), (Edge.Right, Rect(1848, 0, 1920, 1080))]);

        Assert.Equal((72, 1848), (room.left, room.right));
        Assert.Equal((0, 1080), (room.top, room.bottom));
        var crossed = TheDock.PopupRoom(Work, [(Edge.Left, Rect(0, 0, 1500, 1080)), (Edge.Right, Rect(400, 0, 1920, 1080))]);
        Assert.True(crossed.right >= crossed.left);
    }

    /// <summary>In edit mode a popup of a side bar that would reach up over the edit bar, at the top of the display, moves down under it.</summary>
    [Theory]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void APopupOfASideBarGoesUnderTheEditBar(Edge side)
    {
        RECT[] deck = [Rect(540, 12, 1380, 72)];

        Assert.Equal(72 + 18, FlyoutHost.ClearOf(800, 0, 400, 300, side, 18, deck));
        Assert.Equal(200, FlyoutHost.ClearOf(800, 200, 400, 300, side, 18, deck));
    }

    /// <summary>
    /// §580: a left bar's popup pushed against the work area's right end, where the right bar of a Top and bottom on a side
    /// stands: 12 DIP in (18 px at 150 %), its shadow's 24 DIP (36 px) of room cut to those 18, so the box that takes the
    /// pointer stops at the bar; the bar's side keeps its gap, the free sides their whole room.
    /// </summary>
    [Fact]
    public void AFlyoutsRoomStopsAtTheBarBesideIt()
    {
        var work = Rect(63, 0, 3777, 2160);
        var room = FlyoutHost.RoomInside((18, 36, 36, 36), x: 2900, top: 600, w: 859, h: 400, work);
        Assert.Equal((18f, 36f, 18f, 36f), room);
    }

    /// <summary>§580: a panel off in the middle keeps its whole room; one past its room's end (the far end that gives, §320) none there.</summary>
    [Fact]
    public void AFlyoutsRoomIsWholeInTheMiddleAndNoneWhereItRunsPast()
    {
        var work = Rect(63, 0, 3777, 2160);
        Assert.Equal((18f, 36f, 36f, 36f), FlyoutHost.RoomInside((18, 36, 36, 36), x: 81, top: 600, w: 800, h: 400, work));
        Assert.Equal((18f, 36f, 0f, 36f), FlyoutHost.RoomInside((18, 36, 36, 36), x: 81, top: 600, w: 4000, h: 400, work));
        // Of a hover bar at the left, which holds no strip: its popup's gap reaches back to the bar, inside the work area.
        Assert.Equal((18f, 36f, 36f, 36f), FlyoutHost.RoomInside((18, 36, 36, 36), x: 81, top: 600, w: 800, h: 400, Rect(0, 0, 3840, 2160)));
    }

    /// <summary>With no other bar in sight, the work area as it is; never less than nothing.</summary>
    [Fact]
    public void WithNoOtherBarTheWorkAreaIsAllOfIt()
    {
        Assert.Equal(Work, TheDock.PopupRoom(Work, []));
        var crossed = TheDock.PopupRoom(Work, [(Edge.Top, Rect(0, 0, 1920, 900)), (Edge.Bottom, Rect(0, 100, 1920, 1080))]);
        Assert.True(crossed.bottom >= crossed.top);
    }

    /// <summary>
    /// In edit mode a popup of a top bar that would stand over the edit bar moves down past it, its
    /// gap kept; one beside it keeps its place by its bar.
    /// </summary>
    [Fact]
    public void APopupOfATopBarGoesUnderTheEditBar()
    {
        RECT[] deck = [Rect(540, 84, 1380, 144)];

        // Opened from the middle of a 72 px top bar, 18 px under it: over the edit bar, so under it.
        Assert.Equal(144 + 18, FlyoutHost.ClearOf(800, 90, 400, 300, Edge.Top, 18, deck));
        // From its far end: beside the edit bar, where it was.
        Assert.Equal(90, FlyoutHost.ClearOf(1500, 90, 400, 300, Edge.Top, 18, deck));
        // Already under it: where it was.
        Assert.Equal(200, FlyoutHost.ClearOf(800, 200, 400, 300, Edge.Top, 18, deck));
        // Out of edit mode there is nothing to keep clear of.
        Assert.Equal(90, FlyoutHost.ClearOf(800, 90, 400, 300, Edge.Top, 18, []));
    }
}
