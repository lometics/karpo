// §629 (the person, 2026-11-09: "the add widget modal should be semi transparent in case the ghost preview is directly behind it.
// but it should only be semi transparent for the ghost preview and not for everything actually."): where the add page lets its
// ghost show through — the ghost's own rectangles ∩ the page, nothing else.

using System.Numerics;
using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class SeeThroughPlanTests
{
    private static readonly RECT Page = new() { left = 100, top = 50, right = 900, bottom = 650 };

    private static GhostSight Sight(int x, int y, float radius, params RectF[] rects) => new(null!, new POINT { x = x, y = y }, rects, radius);

    [Fact]
    public void ThePageIsSeeThroughWhereTheGhostsRectanglesRunUnderItAlone()
    {
        // A folder's window at the display's corner, its ghost's lines in it: one wholly under the page, one half under its foot,
        // one beside it.
        var folder = Sight(0, 0, 8,
            new RectF(200, 100, 300, 60),
            new RectF(200, 620, 300, 60),
            new RectF(950, 100, 200, 60));
        var cuts = SeeThroughPlan.Of(Page, [folder]);
        Assert.Equal(2, cuts.Count);
        // Wholly under it: the whole line, in page px; where it is in its window.
        Assert.Equal(new RectF(100, 50, 300, 60), cuts[0].At);
        Assert.Equal(cuts[0].At, cuts[0].Whole);
        Assert.Equal(new Vector2(200, 100), cuts[0].From);
        // Half under it: the part in the page alone, its whole rectangle kept for its shape.
        Assert.Equal(new RectF(100, 570, 300, 30), cuts[1].At);
        Assert.Equal(new RectF(100, 570, 300, 60), cuts[1].Whole);
        Assert.Equal(new Vector2(200, 620), cuts[1].From);
        Assert.All(cuts, cut => Assert.Equal(8, cut.Radius));
    }

    [Fact]
    public void AGhostOutsideThePageShowsNowhere()
    {
        // The bar's ghost along the display's foot, the page at its top: nothing of the page see-through.
        var bar = Sight(0, 1032, 4, new RectF(600, 4, 40, 40));
        Assert.Empty(SeeThroughPlan.Of(Page, [bar]));
        // Touching its edge alone is not under it.
        Assert.Empty(SeeThroughPlan.Of(Page, [Sight(0, 0, 4, new RectF(900, 50, 40, 40))]));
        Assert.Empty(SeeThroughPlan.Of(Page, []));
    }

    [Fact]
    public void EachSightIsMappedFromItsOwnWindow()
    {
        // A bar's window at its own place on the screen: its rectangle there, where it is in that window.
        var bar = Sight(400, 600, 4, new RectF(10, 20, 40, 40));
        var folder = Sight(0, 0, 8, new RectF(120, 60, 100, 50));
        var cuts = SeeThroughPlan.Of(Page, [bar, folder]);
        Assert.Equal([0, 1], cuts.Select(cut => cut.Sight));
        // The bar's: 410..450 × 620..660 on the screen, the page's foot at 650.
        Assert.Equal(new RectF(310, 570, 40, 30), cuts[0].At);
        Assert.Equal(new Vector2(10, 20), cuts[0].From);
        Assert.Equal(new RectF(20, 10, 100, 50), cuts[1].At);
        Assert.Equal(new Vector2(120, 60), cuts[1].From);
    }

    [Fact]
    public void ARectanglesCornersAreNeverRounderThanItsHalf()
    {
        var cuts = SeeThroughPlan.Of(Page, [Sight(0, 0, 40, new RectF(200, 100, 300, 30), new RectF(200, 200, 300, 120))]);
        Assert.Equal([15f, 40f], cuts.Select(cut => cut.Radius));
    }
}
