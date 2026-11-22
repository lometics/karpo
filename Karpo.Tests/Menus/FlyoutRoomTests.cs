// Where the add list and a menu open when the side of their anchor they open to has no room
// (FlyoutPlacement.Roomiest; the person, 2026-10-16: "when the add button is all the way on the top of
// a screen the size of the flyout add widgets is squished. then you cannot see anything except for the
// search box"): on the other side where that has the room, else on the roomier side where that has
// the least they need, else over the anchor, inside the work area. Anchors at every edge and corner,
// on a display at 100 % and on one beside it at 150 %.

using Karpo.Dock;
using TerraFX.Interop.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Menus;

public sealed class FlyoutRoomTests
{
    /// <summary>A 1920 × 1080 display at 100 % with a 40 px bar, and a 2560 × 1440 one at 150 % right of it with a 72 px bar.</summary>
    public static TheoryData<int, int, int, int, float> Displays => new()
    {
        { 0, 0, 1920, 1040, 1f },
        { 1920, 0, 4480, 1368, 1.5f },
    };

    private static RECT Work(int left, int top, int right, int bottom) => new() { left = left, top = top, right = right, bottom = bottom };

    private static POINT At(int x, int y) => new() { x = x, y = y };

    /// <summary>Room where it opens: it opens there, whatever the other side has.</summary>
    [Theory]
    [MemberData(nameof(Displays))]
    public void WithRoomItStays(int left, int top, int right, int bottom, float scale)
    {
        var work = Work(left, top, right, bottom);
        var middle = (left + right) / 2;
        var up = new FlyoutPlacement(At(middle, bottom), work, Edge.Bottom);
        Assert.Equal(up, up.Roomiest(600, 300, scale));
        var down = new FlyoutPlacement(At(middle, top), work, Edge.Top);
        Assert.Equal(down, down.Roomiest(600, 300, scale));
        var right1 = new FlyoutPlacement(At(left, (top + bottom) / 2), work, Edge.Left);
        Assert.Equal(right1, right1.Roomiest(1000, 500, scale));
    }

    /// <summary>
    /// Regression: a "+" at the top of the display, opening upwards (a bottom bar's folder grown to the
    /// top), had room for the search field only; it opens downwards, and has all it wants there.
    /// </summary>
    [Theory]
    [MemberData(nameof(Displays))]
    public void AtTheTopItOpensDown(int left, int top, int right, int bottom, float scale)
    {
        var work = Work(left, top, right, bottom);
        foreach (var x in new[] { left, (left + right) / 2, right })
        {
            var placed = new FlyoutPlacement(At(x, top + 40), work, Edge.Bottom).Roomiest(700, 400, scale);
            Assert.Equal(Edge.Top, placed.Edge);
            Assert.False(placed.Over);
            Assert.True(placed.Room(scale) >= 700);
        }
    }

    /// <summary>A top bar's "+" at the bottom of the display (its folder grown to the bottom) opens upwards.</summary>
    [Theory]
    [MemberData(nameof(Displays))]
    public void AtTheBottomItOpensUp(int left, int top, int right, int bottom, float scale)
    {
        var work = Work(left, top, right, bottom);
        foreach (var x in new[] { left, (left + right) / 2, right })
        {
            var placed = new FlyoutPlacement(At(x, bottom - 30), work, Edge.Top).Roomiest(700, 400, scale);
            Assert.Equal(Edge.Bottom, placed.Edge);
            Assert.True(placed.Room(scale) >= 700);
        }
    }

    /// <summary>Off a side bar, the width: at the far side it opens back towards the bar.</summary>
    [Theory]
    [MemberData(nameof(Displays))]
    public void AtTheFarSideItOpensBack(int left, int top, int right, int bottom, float scale)
    {
        var work = Work(left, top, right, bottom);
        foreach (var y in new[] { top, (top + bottom) / 2, bottom })
        {
            var leftward = new FlyoutPlacement(At(right - 50, y), work, Edge.Left).Roomiest(1000, 1000, scale);
            Assert.Equal(Edge.Right, leftward.Edge);
            Assert.True(leftward.Depth(scale) >= 1000);
            var rightward = new FlyoutPlacement(At(left + 50, y), work, Edge.Right).Roomiest(1000, 1000, scale);
            Assert.Equal(Edge.Left, rightward.Edge);
            Assert.True(rightward.Depth(scale) >= 1000);
        }
    }

    /// <summary>
    /// Neither side has all it wants: the roomier one, where that has the least it needs — the side it
    /// was to open to where both have as much.
    /// </summary>
    [Theory]
    [MemberData(nameof(Displays))]
    public void ShortOfBothTheRoomierHasIt(int left, int top, int right, int bottom, float scale)
    {
        var work = Work(left, top, right, bottom);
        var height = bottom - top;
        var anchor = At((left + right) / 2, top + (height * 2 / 5));
        var up = new FlyoutPlacement(anchor, work, Edge.Bottom).Roomiest(height, height / 4, scale);
        Assert.Equal(Edge.Top, up.Edge);
        Assert.False(up.Over);
        var down = new FlyoutPlacement(At(anchor.x, top + (height * 3 / 5)), work, Edge.Top).Roomiest(height, height / 4, scale);
        Assert.Equal(Edge.Bottom, down.Edge);
    }

    /// <summary>
    /// Neither side has even the least it needs: it stands over the anchor, with the whole work area's
    /// room, centred on the anchor and inside the work area — at the middle and at both ends.
    /// </summary>
    [Theory]
    [MemberData(nameof(Displays))]
    public void ShortOfTheLeastItStandsOver(int left, int top, int right, int bottom, float scale)
    {
        var work = Work(left, top, right, bottom);
        var height = bottom - top;
        var keep = (int)MathF.Round(12 * scale, MidpointRounding.AwayFromZero);
        // More than the whole work area less the gap at one end and a margin at the other.
        var needs = height - (2 * keep) + 1;
        foreach (var edge in new[] { Edge.Top, Edge.Bottom })
        {
            foreach (var y in new[] { top, top + (height / 2), bottom })
            {
                var placed = new FlyoutPlacement(At(left + 10, y), work, edge).Roomiest(needs, needs, scale);
                Assert.True(placed.Over);
                Assert.Equal(height - 2 * keep, placed.Room(scale));
                var tall = height - 4 * keep;
                var start = placed.OverStart(tall, keep);
                Assert.InRange(start, top + keep, bottom - keep - tall);
                // Over stays over.
                Assert.Equal(placed, placed.Roomiest(needs, needs, scale));
            }
        }
        // Centred on the anchor where it can be.
        var middle = new FlyoutPlacement(At(left + 10, top + (height / 2)), work, Edge.Bottom).Roomiest(height, height, scale);
        Assert.Equal(top + (height / 2) - 100, middle.OverStart(200, keep));
        // Too wide for either side of a side bar's anchor: over it, inside the display.
        var wide = new FlyoutPlacement(At((left + right) / 2, top + 100), work, Edge.Left).Roomiest(right - left - 50, right - left - 50, scale);
        Assert.True(wide.Over);
        Assert.InRange(wide.OverStart(right - left - 100, keep), left + keep, right - keep - (right - left - 100));
    }

    /// <summary>A shorter want than the least (a short menu) is all it needs: a side with that much is enough.</summary>
    [Fact]
    public void AShortListNeedsNoMoreThanItWants()
    {
        var work = Work(0, 0, 1920, 1040);
        var placed = new FlyoutPlacement(At(960, 260), work, Edge.Bottom).Roomiest(200, 400, 1f);
        Assert.Equal(Edge.Bottom, placed.Edge);
        Assert.False(placed.Over);
    }
}
