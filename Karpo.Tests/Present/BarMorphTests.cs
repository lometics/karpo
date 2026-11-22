// §637: a bar becoming another morphs (Karpo.Dock\BarMorph.cs) — the model, pure: what glides (the same widget, else the next
// of its kind along the bar) and what fades; where each ground starts and ends (a strip splitting into a dock's panels, the
// panels merging into a strip); each step's share of the one time and its curve, never a frame with neither the old nor the
// new; the time by the one motion, nothing with it off; the strip a bar holds changing once, after a bar has grown into it or
// as it starts to shrink; and a switch between layouts that show alike keeping the display's window.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Docking;
using static Karpo.Tests.Docking.BarWorld;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Present;

public sealed class BarMorphTests
{
    private static MorphPart Part(string key, BarKind kind, float x, float width = 40) => new(key, kind, new RectF(x, 4, width, 40));

    private static MorphGround Strip(float width = 1280) => new(-1, new RectF(0, 0, width, 48), 0);

    private static MorphGround Dock(int section, float x, float width) => new(section, new RectF(x, 4, width, 48), 8);

    /// <summary>
    /// The same widget glides wherever it stands (its key); else the next of its kind along the bar not taken yet; what is left
    /// goes (the old bar's) or comes (the new bar's).
    /// </summary>
    [Fact]
    public void TheSameWidgetGlidesElseTheNextOfItsKindTheRestFade()
    {
        var from = new BarPicture(
        [
            Part("start", BarKind.Start, 0),
            Part("apps", BarKind.Apps, 50, 200),
            Part("clock-a", BarKind.FolderList, 300),
            Part("clock-b", BarKind.FolderList, 350),
            Part("desk", BarKind.ShowDesktop, 1270, 10),
        ], [Strip()]);
        var to = new BarPicture(
        [
            Part("d-start", BarKind.Start, 500),
            Part("apps", BarKind.Apps, 560, 120),
            Part("d-clock", BarKind.FolderList, 700),
            Part("snip", BarKind.SnippingTool, 760),
        ], [Dock(1, 480, 340)]);
        var plan = BarMorph.Plan(from, to);
        // apps by its key; Start and the first folder by kind, in order along the bar.
        Assert.Equal([(0, 0), (1, 1), (2, 2)], plan.Glides);
        Assert.Equal([3, 4], plan.Leaving);
        Assert.Equal([3], plan.Entering);

        // A key wins over a kind standing nearer: the second folder is the one the new bar has, by its key.
        var keyed = BarMorph.Plan(from, to with { Parts = [Part("clock-b", BarKind.FolderList, 700), Part("x", BarKind.FolderList, 760)] });
        Assert.Equal([(3, 0), (2, 1)], keyed.Glides);

        // Nothing on the one side: everything comes, or goes.
        var empty = BarMorph.Plan(BarPicture.Empty, to);
        Assert.Empty(empty.Glides);
        Assert.Equal([0, 1, 2, 3], empty.Entering);
        Assert.Equal([0, 1, 2, 3, 4], BarMorph.Plan(from, BarPicture.Empty).Leaving);
    }

    /// <summary>
    /// A taskbar's strip into a dock's three panels: each panel starts as its own stretch of the strip, cut at the middles of the
    /// gaps between the panels (the strip splits, §705), the strip ends as the panels together, its corners rounding; back, the
    /// strip starts as the panels together (they merge) and each panel ends as its stretch of the strip, its corners squaring —
    /// never every panel grown over the whole strip in full sight. A panel to a panel goes to the nearest.
    /// </summary>
    [Fact]
    public void AStripSplitsIntoPanelsAndPanelsMergeIntoAStrip()
    {
        var strip = new BarPicture([], [Strip()]);
        var docks = new BarPicture([], [Dock(0, 8, 200), Dock(1, 440, 400), Dock(2, 1072, 200)]);
        var split = BarMorph.Plan(strip, docks);
        RectF[] stretches = [new(0, 0, 324, 48), new(324, 0, 632, 48), new(956, 0, 324, 48)];
        Assert.Equal(stretches, split.GroundsFrom.Select(start => start.Rect));
        Assert.All(split.GroundsFrom, start => Assert.Equal(0f, start.Radius));
        Assert.Equal((new RectF(8, 4, 1264, 48), 8f), (split.GroundsTo[0].Rect, split.GroundsTo[0].Radius));
        Assert.Equal([0, 1, 2], split.GroundsFrom.Select(ground => ground.Slot));

        var merge = BarMorph.Plan(docks, strip);
        Assert.Equal((new RectF(8, 4, 1264, 48), 8f), (merge.GroundsFrom[0].Rect, merge.GroundsFrom[0].Radius));
        Assert.Equal(stretches, merge.GroundsTo.Select(end => end.Rect));
        Assert.All(merge.GroundsTo, end => Assert.Equal(0f, end.Radius));

        // One dock in the middle to the three: the middle from the middle, the others from it too (nothing else is there).
        var middle = new BarPicture([], [Dock(1, 500, 280)]);
        var grown = BarMorph.Plan(middle, docks);
        Assert.All(grown.GroundsFrom, start => Assert.Equal(new RectF(500, 4, 280, 48), start.Rect));
        // The three to the middle one: each to it, it from the three together.
        var shrunk = BarMorph.Plan(docks, middle);
        Assert.Equal(new RectF(8, 4, 1264, 48), shrunk.GroundsFrom[0].Rect);
        Assert.All(shrunk.GroundsTo, end => Assert.Equal(new RectF(500, 4, 280, 48), end.Rect));

        // No ground before (a bar of nothing): it comes in where it stands, by its fade.
        Assert.Equal(docks.Grounds, BarMorph.Plan(BarPicture.Empty, docks).GroundsFrom);
    }

    /// <summary>A glide's size: the smaller side's share — another width alone only glides —, held to half and twice.</summary>
    [Fact]
    public void AGlideGrowsByItsSmallerSideHeldToHalfAndTwice()
    {
        Assert.Equal(1f, BarMorph.ScaleFrom(new RectF(0, 0, 120, 40), new RectF(0, 0, 80, 40)));
        Assert.Equal(0.75f, BarMorph.ScaleFrom(new RectF(0, 0, 30, 30), new RectF(0, 0, 40, 40)));
        Assert.Equal(2f, BarMorph.ScaleFrom(new RectF(0, 0, 400, 400), new RectF(0, 0, 40, 40)));
        Assert.Equal(0.5f, BarMorph.ScaleFrom(new RectF(0, 0, 4, 4), new RectF(0, 0, 40, 40)));
        Assert.Equal(1f, BarMorph.ScaleFrom(default, new RectF(0, 0, 40, 40)));
        Assert.Equal(new RectF(5, 6, 10, 10), BarMorph.Moved(new RectF(1, 2, 10, 10), new Vector2(4, 4)));
        Assert.Equal(new RectF(0, 4, 50, 46), BarMorph.Hull([new RectF(0, 10, 10, 40), new RectF(40, 4, 10, 10)]));
    }

    /// <summary>
    /// The one time: 333 ms at Normal (Fluent's point-to-point), 500 Slower, 222 Faster, nothing with motion off; every step on
    /// Windows' curves — glides and what comes decelerate, what goes accelerates over a large surface's Hide (half: 167 ms), what
    /// comes over a large surface's Show (three quarters: 250 ms) —, each inside the time.
    /// </summary>
    [Fact]
    public void TheMorphTakesTheOneMotionsTimeOnItsCurves()
    {
        Assert.Equal(333, Motion.Normal.Ms(MotionKind.Morph));
        Assert.Equal(500, new Motion(true, Animation.Slower).Ms(MotionKind.Morph));
        Assert.Equal(222, new Motion(true, Animation.Faster).Ms(MotionKind.Morph));
        Assert.Equal(0, Motion.Still.Ms(MotionKind.Morph));
        Assert.Equal(0, Motion.Of(windows: false, new Config()).Ms(MotionKind.Morph));
        Assert.Equal(Motion.Decelerate, Motion.CurveOf(MotionKind.Morph));
        Assert.Equal(Motion.Decelerate, BarMorph.Glide.Curve);
        Assert.Equal((0f, 1f), (BarMorph.Glide.Start, BarMorph.Glide.End));
        Assert.Equal((Motion.Accelerate, 0f, 0.5f), (BarMorph.Leave.Curve, BarMorph.Leave.Start, BarMorph.Leave.End));
        Assert.Equal(Motion.Normal.Ms(MotionKind.Hide), Motion.Normal.Ms(MotionKind.Morph) * (BarMorph.Leave.End - BarMorph.Leave.Start), 1d);
        Assert.Equal((Motion.Decelerate, 1f), (BarMorph.Enter.Curve, BarMorph.Enter.End));
        Assert.Equal(Motion.Normal.Ms(MotionKind.Show), Motion.Normal.Ms(MotionKind.Morph) * (BarMorph.Enter.End - BarMorph.Enter.Start), 1d);
        foreach (var step in new[] { BarMorph.Glide, BarMorph.CrossIn, BarMorph.CrossOut, BarMorph.Leave, BarMorph.Enter, BarMorph.GroundIn, BarMorph.GroundOut })
        {
            Assert.InRange(step.Start, 0f, 1f);
            Assert.InRange(step.End, step.Start, 1f);
            Assert.Equal(0f, step.At(0f));
            Assert.Equal(1f, step.At(1f));
        }
        Assert.InRange(BarMorph.Shrunk, 0.85f, 0.97f);
    }

    /// <summary>
    /// Never a frame with neither: a glide's new self is all there before its old self begins to go, and the new ground before the
    /// old one; at every moment what shows of the two together is whole.
    /// </summary>
    [Fact]
    public void NeverAFrameWithNeitherTheOldNorTheNew()
    {
        for (var share = 0f; share <= 1f; share += 0.01f)
        {
            Assert.True(BarMorph.CrossIn.At(share) + (1 - BarMorph.CrossOut.At(share)) >= 0.999f, $"a glide shows neither at {share:P0}");
            Assert.True(BarMorph.GroundIn.At(share) + (1 - BarMorph.GroundOut.At(share)) >= 0.999f, $"the ground shows neither at {share:P0}");
        }
        Assert.True(BarMorph.CrossOut.Start >= BarMorph.CrossIn.Start);
        Assert.True(BarMorph.GroundOut.Start >= BarMorph.GroundIn.Start && BarMorph.GroundOut.End <= 1f);
    }

    /// <summary>
    /// §637: the strip a bar holds changes once. Growing as it morphs (a taskbar of 48 into a dock of 56) it holds the new one only
    /// once the morph is over — the work area changed once, after the bar has grown; shrinking it lets go at the start, and the
    /// end changes nothing more.
    /// </summary>
    [Fact]
    public void TheWorkAreaChangesOnceAfterGrowingOrAsItShrinks()
    {
        Assert.True(BarMorph.HoldsNow(morphing: false, 48, 56));
        Assert.False(BarMorph.HoldsNow(morphing: true, 48, 56));
        Assert.True(BarMorph.HoldsNow(morphing: true, 56, 48));
        Assert.True(BarMorph.HoldsNow(morphing: true, 48, 48));

        foreach (var (held, wanted) in new[] { (48, 56), (56, 48) })
        {
            var world = new BarWorld(two: false) { KeptAutoHide = true };
            ((ITaskbarShell)world).SetState(1);
            var bar = world.AddBar(One);
            bool Ask(int thickness) => bar.Holder.Hold(Edge.Bottom, thickness, world.Of(One).Bounds, world.Of(One).Monitor);
            Ask(held);
            var works = new List<int> { world.Of(One).Work.bottom };
            var setpos = world.Calls.Count(made => made.Call == "setpos");
            // The switch's lay out and the reconcile straight after it, the morph running; then its end.
            foreach (var morphing in new[] { true, true, false, false })
            {
                if (BarMorph.HoldsNow(morphing, held: Thickness(world), wanted))
                {
                    Ask(wanted);
                }
                works.Add(world.Of(One).Work.bottom);
            }
            Assert.Equal(setpos + 1, world.Calls.Count(made => made.Call == "setpos"));
            Assert.Equal(2, works.Distinct().Count());
            Assert.Equal(2160 - wanted, works[^1]);
            // Growing: the work area as it was until the end; shrinking: changed at the start.
            Assert.Equal(wanted > held ? 2160 - held : 2160 - wanted, works[1]);
        }

        static int Thickness(BarWorld world) => 2160 - world.Of(One).Work.bottom;
    }

    /// <summary>
    /// A switch between layouts that show alike — a taskbar and a dock, both holding their strip — keeps the display's window:
    /// the arrange keeps a window by its display and its edge (Dock.StandingEdges), and the two have the same. Another way of
    /// showing (over the windows) the same edge, kept too; one that comes in on hover has none standing, its anchor instead.
    /// </summary>
    [Fact]
    public void LayoutsThatShowAlikeKeepTheWindow()
    {
        var taskbar = new TaskbarLayout { Id = "t", Type = LayoutType.Taskbar };
        var dock = new TaskbarLayout { Id = "d", Type = LayoutType.Dock, Size = 56 };
        var over = new TaskbarLayout { Id = "o", Type = LayoutType.Dock, Show = EdgeMode.Float };
        var hover = new TaskbarLayout { Id = "h", Type = LayoutType.Taskbar, Show = EdgeMode.Hover };
        foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left })
        {
            var standing = Karpo.Dock.Dock.StandingEdges(taskbar.Edges(edge));
            Assert.Equal([edge], standing.Select(each => each!.Value));
            Assert.Equal(standing, Karpo.Dock.Dock.StandingEdges(dock.Edges(edge)));
            Assert.Equal(standing, Karpo.Dock.Dock.StandingEdges(over.Edges(edge)));
            Assert.Equal([null], Karpo.Dock.Dock.StandingEdges(hover.Edges(edge)));
        }
    }
}
