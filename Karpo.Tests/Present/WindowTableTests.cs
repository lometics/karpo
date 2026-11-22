// The commit fence's rules (docs\widgets.md §354, WindowTable): what a frame asks of a window waits for
// the frame's commit, the latest state wins, a later frame's change waits for its own fence, the stack
// goes in one batch top first, a destroy waits out its fade, and what waits for the screen runs last.
// §359: the stack is the z-order plan (ZPlan) by strata; a window clear while out of sight comes into
// sight as its frame ends, before the commit; one that sinks to a lower stratum goes there then too, one
// that rises waits for the commit. No window: the windows are a fake that records what it is told.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Present;

public sealed class WindowTableTests
{
    /// <summary>Windows as a list, top first, each shown or not; every plan and fence recorded.</summary>
    private sealed class Fake : IWindowSystem
    {
        public readonly List<HWND> Order = [];
        public readonly HashSet<HWND> Shown = [];
        public readonly HashSet<HWND> Destroyed = [];
        public readonly List<WindowPlan> Plans = [];
        public readonly List<uint> Fences = [];
        public long? Woken;

        public long Now { get; set; } = 1000;

        public bool Visible(HWND window) => Shown.Contains(window);

        public void Run(WindowPlan plan)
        {
            Plans.Add(plan);
            foreach (var entry in plan.Stack)
            {
                if (entry.Show)
                {
                    Shown.Add(entry.Window);
                }
            }
            // The batch: each under the one before it, the first over every topmost window.
            var placed = plan.Stack.Select(entry => entry.Window).ToList();
            Order.RemoveAll(placed.Contains);
            Order.InsertRange(0, placed);
            foreach (var window in plan.Hidden)
            {
                Shown.Remove(window);
            }
            foreach (var then in plan.Then)
            {
                then();
            }
            foreach (var (window, closed) in plan.Destroyed)
            {
                Shown.Remove(window);
                closed?.Invoke();
                Destroyed.Add(window);
                Order.Remove(window);
            }
            foreach (var (window, rects) in plan.Regions)
            {
                Region(window, rects);
            }
            foreach (var action in plan.Presented)
            {
                action();
            }
        }

        public void Fence(uint generation) => Fences.Add(generation);

        /// <summary>Every region set, in order: the window and the rectangles of its union.</summary>
        public readonly List<(HWND Window, string Rects)> Regions = [];

        public void Region(HWND window, IReadOnlyList<RECT> rects) => Regions.Add((window, Said([.. rects])));

        public void Wake(long at) => Woken = at;
    }

    private static HWND W(int n) => (HWND)(nint)n;

    private static (WindowTable Table, Fake System) Made(params int[] order)
    {
        var system = new Fake();
        system.Order.AddRange(order.Select(W));
        return (new WindowTable(system), system);
    }

    [Fact]
    public void OutsideAFrameWhatIsAskedIsDoneAtOnce()
    {
        var (table, system) = Made(1);
        table.Show(W(1));
        Assert.Contains(W(1), system.Shown);
        Assert.Empty(system.Fences);
        table.Hide(W(1));
        Assert.DoesNotContain(W(1), system.Shown);
        Assert.True(table.Settled);
    }

    [Fact]
    public void AFrameShowsItsWindowsOnlyOnceItsCommitHasLanded()
    {
        var (table, system) = Made(1);
        table.Begin();
        table.Show(W(1));
        Assert.True(table.Visible(W(1)), "asked: shown, for whatever asks next in the frame");
        table.End();
        Assert.DoesNotContain(W(1), system.Shown);
        Assert.Equal([1u], system.Fences);
        table.Landed(1);
        Assert.Contains(W(1), system.Shown);
        Assert.True(table.Settled);
    }

    [Fact]
    public void AFrameThatAsksNothingAsksNoFence()
    {
        var (table, system) = Made(1);
        table.Begin();
        table.End();
        Assert.Empty(system.Fences);
    }

    [Fact]
    public void FramesNestAndTheOutermostEndsThem()
    {
        var (table, system) = Made(1);
        table.Begin();
        table.Begin();
        table.Show(W(1));
        table.End();
        Assert.Empty(system.Fences);
        table.End();
        Assert.Equal([1u], system.Fences);
    }

    [Fact]
    public void TheLatestStateWinsAndIsNeverReplayedStale()
    {
        var (table, system) = Made(1);
        // Shown and hidden in one frame: never shown.
        table.Begin();
        table.Show(W(1));
        table.Hide(W(1));
        table.End();
        table.Landed(1);
        Assert.DoesNotContain(W(1), system.Shown);
        Assert.All(system.Plans, plan => Assert.DoesNotContain(plan.Stack, entry => entry.Show));
        // Shown in one frame, hidden in the next before the first's commit landed: the first fence
        // shows nothing (the window's state is the second frame's), the second hides nothing.
        table.Begin();
        table.Show(W(1));
        table.End();
        table.Begin();
        table.Hide(W(1));
        table.End();
        table.Landed(2);
        Assert.DoesNotContain(W(1), system.Shown);
        table.Landed(3);
        Assert.DoesNotContain(W(1), system.Shown);
        Assert.True(table.Settled);
    }

    [Fact]
    public void AFenceAppliesItsFramesAndThoseBeforeNotLaterOnes()
    {
        var (table, system) = Made(1, 2);
        table.Begin();
        table.Show(W(1));
        table.End();
        table.Begin();
        table.Show(W(2));
        table.End();
        Assert.Equal([1u, 2u], system.Fences);
        table.Landed(1);
        Assert.Contains(W(1), system.Shown);
        Assert.DoesNotContain(W(2), system.Shown);
        table.Landed(2);
        Assert.Contains(W(2), system.Shown);
    }

    [Fact]
    public void TheStackGoesInOneBatchByStrata()
    {
        var (table, system) = Made(9, 1, 2, 3, 4);
        table.Enlist(W(1), Stratum.Dim);
        table.Enlist(W(2), Stratum.Bar);
        table.Enlist(W(3), Stratum.Menu);
        table.Enlist(W(4), Stratum.Menu);
        system.Plans.Clear();
        table.Begin();
        // Asked in any order: the plan is by stratum, the last shown on top of its own.
        table.Show(W(3));
        table.Show(W(4));
        table.Show(W(1));
        table.Show(W(2));
        table.End();
        Assert.Empty(system.Plans);
        table.Landed(1);
        var plan = Assert.Single(system.Plans);
        Assert.Equal([W(4), W(3), W(2), W(1)], plan.Stack.Select(entry => entry.Window));
        Assert.All(plan.Stack, entry => Assert.True(entry.Show));
        Assert.Equal([W(4), W(3), W(2), W(1), W(9)], system.Order);
        // Brought to the front of its stratum: over the other menu, never over a higher stratum's.
        table.Begin();
        table.Front(W(3));
        table.End();
        table.Landed(2);
        Assert.Equal([W(3), W(4), W(2), W(1), W(9)], system.Order);
    }

    [Fact]
    public void WhatAFrameAsksIsWhatItsQuestionsAnswer()
    {
        // The dim (1) comes in a frame with the edit bar (2) and an arranged folder (3): in the frame the plan
        // the frame leaves is what PlannedAfter answers — what a folder's WM_WINDOWPOSCHANGING reads.
        var (table, _) = Made(2, 1, 3);
        table.Enlist(W(1), Stratum.Dim);
        table.Enlist(W(2), Stratum.EditBar);
        table.Enlist(W(3), Stratum.ArrangedFolder);
        table.Begin();
        table.Show(W(1));
        table.Show(W(2));
        table.Show(W(3));
        Assert.Equal(W(3), table.PlannedAfter(W(1)));
        Assert.Equal(W(2), table.PlannedAfter(W(3)));
        Assert.Equal(HWND.HWND_TOPMOST, table.PlannedAfter(W(2)));
        Assert.Equal(Stratum.ArrangedFolder, table.StratumOf(W(3)));
        // Never enlisted: a popup.
        Assert.Equal(Stratum.Popup, table.StratumOf(W(9)));
        table.End();
    }

    [Fact]
    public void AHideLetsGoOfItsContentOnceTheWindowIsOutOfSight()
    {
        var (table, system) = Made(1);
        system.Shown.Add(W(1));
        var closed = 0;
        table.Begin();
        table.Hide(W(1), () => closed++);
        table.End();
        Assert.Equal(0, closed);
        table.Landed(1);
        Assert.Equal(1, closed);
        Assert.DoesNotContain(W(1), system.Shown);
    }

    [Fact]
    public void AShowingThatTakesAHidesPlaceLetsGoOfTheOldContentAtOnce()
    {
        var (table, system) = Made(1);
        system.Shown.Add(W(1));
        var closed = 0;
        table.Begin();
        table.Hide(W(1), () => closed++);
        table.Show(W(1));
        Assert.Equal(1, closed);
        table.End();
        table.Landed(1);
        Assert.Equal(1, closed);
        Assert.Contains(W(1), system.Shown);
    }

    [Fact]
    public void ADestroyWaitsForItsFadeAndClosesItsContentWithTheWindow()
    {
        var (table, system) = Made(1, 2);
        system.Shown.Add(W(1));
        var closed = 0;
        table.Begin();
        table.Destroy(W(1), () => closed++, afterMs: 100);
        // Its replacement, shown in the same frame.
        table.Show(W(2));
        Assert.False(table.Visible(W(1)));
        table.End();
        table.Landed(1);
        // The new one came; the old one stays, fading, until its fade is over.
        Assert.Contains(W(2), system.Shown);
        Assert.DoesNotContain(W(1), system.Destroyed);
        Assert.Equal(0, closed);
        Assert.Equal(1100, system.Woken);
        system.Now = 1100;
        table.Woken();
        Assert.Contains(W(1), system.Destroyed);
        Assert.Equal(1, closed);
        Assert.True(table.Settled);
    }

    /// <summary>
    /// §612: a hide that waits for its fade (the edit dim's): out of sight once the fade is over, its then with it; shown
    /// again meanwhile, it stays in sight and the hide is called off.
    /// </summary>
    [Fact]
    public void AHideWaitsForItsFadeAndAShowingCallsItOff()
    {
        var (table, system) = Made(1);
        system.Shown.Add(W(1));
        var gone = 0;
        table.Begin();
        table.Hide(W(1), () => gone++, afterMs: 167);
        table.End();
        table.Landed(1);
        Assert.Contains(W(1), system.Shown);
        Assert.Equal(0, gone);
        Assert.Equal(1167, system.Woken);
        system.Now = 1167;
        table.Woken();
        Assert.DoesNotContain(W(1), system.Shown);
        Assert.Equal(1, gone);
        Assert.True(table.Settled);

        system.Shown.Add(W(1));
        system.Woken = null;
        table.Begin();
        table.Hide(W(1), afterMs: 167);
        table.End();
        table.Landed(2);
        table.Begin();
        table.Show(W(1));
        table.End();
        table.Landed(3);
        system.Now = 2000;
        table.Woken();
        Assert.Contains(W(1), system.Shown);
        Assert.True(table.Settled);
    }

    [Fact]
    public void AWindowLetGoOfIsNeverShownAgain()
    {
        var (table, system) = Made(1);
        table.Begin();
        table.Destroy(W(1));
        table.Show(W(1));
        table.Front(W(1));
        table.End();
        table.Landed(1);
        Assert.DoesNotContain(W(1), system.Shown);
        Assert.Contains(W(1), system.Destroyed);
        Assert.DoesNotContain(system.Plans, plan => plan.Stack.Any(entry => entry.Window == W(1)));
    }

    [Fact]
    public void WhatWaitsForTheScreenRunsOnceEverythingAskedBeforeItIsDone()
    {
        var (table, system) = Made(1);
        system.Shown.Add(W(1));
        var snipped = false;
        table.Begin();
        table.Destroy(W(1), afterMs: 100);
        table.AfterPresent(() => snipped = system.Destroyed.Contains(W(1)));
        table.End();
        table.Landed(1);
        Assert.False(snipped);
        system.Now = 1200;
        table.Woken();
        Assert.True(snipped);
    }

    [Fact]
    public void TheFocusWaitsForItsShowing()
    {
        var (table, system) = Made(1);
        table.Begin();
        table.Show(W(1));
        table.Focus(W(1));
        table.End();
        Assert.Empty(system.Plans);
        table.Landed(1);
        var plan = Assert.Single(system.Plans);
        Assert.Equal([W(1)], plan.Focused);
    }

    [Fact]
    public void AModalLoopEndsTheFramesAskingEarly()
    {
        var (table, system) = Made(1, 2);
        system.Shown.Add(W(1));
        table.Begin();
        table.Hide(W(1));
        table.Present();
        Assert.Equal([1u], system.Fences);
        // The loop pumps: the commit lands while the handler is still in its dialog.
        table.Landed(1);
        Assert.DoesNotContain(W(1), system.Shown);
        // What the frame asks after the dialog waits for the frame's own end.
        table.Show(W(2));
        table.Landed(1);
        Assert.DoesNotContain(W(2), system.Shown);
        table.End();
        table.Landed(2);
        Assert.Contains(W(2), system.Shown);
    }

    [Fact]
    public void AClearWindowComesIntoSightAsItsFrameEndsAndGoesAfterItsCommit()
    {
        // The dim (1) is clear while out of sight: shown as the frame ends, before its commit — nothing of it
        // shows until the commit brings its dim, with the bars' edit dress. A plain window (2) waits for it.
        var (table, system) = Made(1, 2);
        table.Enlist(W(1), Stratum.Dim, clear: true);
        system.Plans.Clear();
        table.Begin();
        table.Show(W(1));
        table.Show(W(2));
        Assert.Empty(system.Plans);
        table.End();
        var early = Assert.Single(system.Plans);
        Assert.True(early.Early);
        Assert.Contains(W(1), system.Shown);
        Assert.DoesNotContain(W(2), system.Shown);
        Assert.Equal([new Stacked(W(1), Show: true)], early.Stack);
        Assert.False(table.Settled);
        table.Landed(1);
        Assert.Contains(W(2), system.Shown);
        // Going, it is clear in the commit (its owner's) and out of sight at the fence, as every window.
        table.Begin();
        table.Hide(W(1));
        table.End();
        Assert.Contains(W(1), system.Shown);
        table.Landed(2);
        Assert.DoesNotContain(W(1), system.Shown);
        Assert.True(table.Settled);
    }

    [Fact]
    public void AWindowSinksBeforeItsCommitAndRisesAfterIt()
    {
        // A folder (3) over the bars (2) arranged for edit mode: under them as the frame ends, its spread
        // content never over the bars. Back at rest: over them only once its content is at rest.
        var (table, system) = Made(1, 2, 3);
        table.Enlist(W(2), Stratum.Bar);
        table.Enlist(W(3), Stratum.Popup, clear: true);
        table.Show(W(2));
        table.Show(W(3));
        Assert.Equal([W(3), W(2), W(1)], system.Order);
        table.Begin();
        table.Enlist(W(3), Stratum.ArrangedFolder, clear: true);
        table.End();
        Assert.Equal([W(2), W(3), W(1)], system.Order);
        table.Landed(1);
        Assert.Equal([W(2), W(3), W(1)], system.Order);
        table.Begin();
        table.Enlist(W(3), Stratum.Popup, clear: true);
        table.End();
        Assert.Equal([W(2), W(3), W(1)], system.Order);
        table.Landed(2);
        Assert.Equal([W(3), W(2), W(1)], system.Order);
    }

    [Fact]
    public void ThePlanGoesOnlyWhereItChangedOrIsAskedAgain()
    {
        var (table, system) = Made(1, 2);
        table.Enlist(W(1), Stratum.Bar);
        table.Show(W(1));
        system.Plans.Clear();
        // Nothing changed: no stack.
        table.Begin();
        table.Show(W(1));
        table.End();
        table.Landed(1);
        Assert.All(system.Plans, plan => Assert.Empty(plan.Stack));
        // Another app's window came up over it: asked again, the plan goes whatever it is.
        system.Order.Remove(W(2));
        system.Order.Insert(0, W(2));
        table.Begin();
        table.Restack();
        table.End();
        Assert.False(table.Settled);
        table.Landed(2);
        Assert.Equal([new Stacked(W(1), Show: false)], system.Plans[^1].Stack);
        Assert.Equal(W(1), system.Order[0]);
        Assert.True(table.Settled);
    }

    [Fact]
    public void TheOutermostFrameEndsWithItsEnding()
    {
        var (table, system) = Made(1);
        var ended = 0;
        table.Begin();
        table.Begin();
        table.End(() => ended++);
        Assert.Equal(0, ended);
        // The ending asks inside the frame: what it asks waits for the frame's fence.
        table.End(() =>
        {
            ended++;
            table.Show(W(1));
        });
        Assert.Equal(1, ended);
        Assert.DoesNotContain(W(1), system.Shown);
        Assert.Equal([1u], system.Fences);
        table.Landed(1);
        Assert.Contains(W(1), system.Shown);
    }

    [Fact]
    public void FlushDoesEverythingNow()
    {
        var (table, system) = Made(1, 2);
        system.Shown.Add(W(1));
        var closed = false;
        table.Begin();
        table.Destroy(W(1), () => closed = true, afterMs: 500);
        table.Show(W(2));
        table.End();
        table.Flush();
        Assert.True(closed);
        Assert.Contains(W(2), system.Shown);
        Assert.True(table.Settled);
    }

    // --- Regions (§355: a popup's window stands still; its region is where the popup is) -------------

    private static RECT R(int left, int top, int right, int bottom) => new() { left = left, top = top, right = right, bottom = bottom };

    private static string Said(params RECT[] rects) => string.Join(" + ", rects.Select(rect => $"{rect.left},{rect.top},{rect.right},{rect.bottom}"));

    [Fact]
    public void AWindowOutOfSightTakesItsRegionAtOnce()
    {
        var (table, system) = Made(1);
        table.Begin();
        table.Region(W(1), R(10, 10, 110, 60));
        table.End();
        Assert.Equal([(W(1), Said(R(10, 10, 110, 60)))], system.Regions);
        Assert.Empty(system.Fences);
    }

    [Fact]
    public void ARegionInSightGrowsAtOnceAndShrinksOnceTheContentIsThere()
    {
        var (table, system) = Made(1);
        table.Region(W(1), R(100, 100, 300, 200));
        system.Shown.Add(W(1));
        system.Regions.Clear();
        table.Begin();
        // A menu shown again taller, up from its foot: the old place and the new one, at once.
        table.Region(W(1), R(100, 40, 300, 200));
        table.End();
        Assert.Equal([(W(1), Said(R(100, 100, 300, 200), R(100, 40, 300, 200)))], system.Regions);
        table.Landed(1);
        Assert.Equal((W(1), Said(R(100, 40, 300, 200))), system.Regions[^1]);
        Assert.Equal(Said(R(100, 40, 300, 200)), Said([.. table.RegionOf(W(1))!]));
        Assert.True(table.Settled);
    }

    [Fact]
    public void ARegionThatShrinksWaitsForItsCommit()
    {
        var (table, system) = Made(1);
        table.Region(W(1), R(0, 0, 400, 400));
        system.Shown.Add(W(1));
        system.Regions.Clear();
        table.Begin();
        table.Region(W(1), R(100, 100, 200, 200));
        table.End();
        Assert.Empty(system.Regions);
        table.Landed(1);
        Assert.Equal([(W(1), Said(R(100, 100, 200, 200)))], system.Regions);
    }

    [Fact]
    public void ALaterFramesRegionWaitsForItsOwnFenceTheUnionHoldingBoth()
    {
        var (table, system) = Made(1);
        table.Region(W(1), R(0, 0, 100, 100));
        system.Shown.Add(W(1));
        system.Regions.Clear();
        table.Begin();
        table.Region(W(1), R(200, 0, 300, 100));
        table.End();
        table.Begin();
        table.Region(W(1), R(400, 0, 500, 100));
        table.End();
        Assert.Equal(Said(R(0, 0, 100, 100), R(200, 0, 300, 100), R(400, 0, 500, 100)), Said([.. table.RegionOf(W(1))!]));
        // The first frame's fence: the second's region still to come, so nothing shrinks to the first.
        table.Landed(1);
        Assert.Equal(Said(R(0, 0, 100, 100), R(200, 0, 300, 100), R(400, 0, 500, 100)), Said([.. table.RegionOf(W(1))!]));
        table.Landed(2);
        Assert.Equal(Said(R(400, 0, 500, 100)), Said([.. table.RegionOf(W(1))!]));
    }

    [Fact]
    public void TheSameRegionAskedAgainAsksNothing()
    {
        var (table, system) = Made(1);
        table.Region(W(1), R(0, 0, 100, 100));
        system.Shown.Add(W(1));
        system.Regions.Clear();
        table.Begin();
        table.Region(W(1), R(0, 0, 100, 100));
        table.End();
        Assert.Empty(system.Regions);
        Assert.Empty(system.Fences);
    }

    [Fact]
    public void ADestroyedWindowsRegionIsForgotten()
    {
        var (table, system) = Made(1);
        table.Region(W(1), R(0, 0, 100, 100));
        table.Destroy(W(1));
        Assert.Null(table.RegionOf(W(1)));
    }
}
