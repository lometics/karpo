// Where a dragged tray icon would land, read off the pointer (TrayDrags): the
// place among the shown icons, the chevron that hides it, and the pointer that
// is over neither.

using System.Numerics;
using Karpo.Dock;

// Not Karpo.Tests.Tray: a local named "tray" elsewhere in the tests would shadow a namespace of that name.
namespace Karpo.Tests.TrayIcons;

public sealed class TrayDragsTests
{
    // A tray block 200 px long that starts 100 px along the bar: the chevron takes the first 32 px
    // and one icon's cell to the next is 32 px.
    private const float Left = 100;
    private const float Width = 200;
    private const float Chevron = 32;
    private const float Step = 32;

    private static TrayDrop? OnBar(float along, int shown = 4, int hidden = 2, float chevron = Chevron) =>
        TrayDrags.OnBar(along, Left, Width, chevron, Step, shown, hidden);

    [Fact]
    public void ADropTakesThePlaceItIsNearest()
    {
        // The first cell runs from 132 to 164: its left half puts the icon before it, its right half after.
        Assert.Equal(new TrayDrop(TrayArea.Shown, 0), OnBar(134));
        Assert.Equal(new TrayDrop(TrayArea.Shown, 1), OnBar(150));
        Assert.Equal(new TrayDrop(TrayArea.Shown, 1), OnBar(166));
        Assert.Equal(new TrayDrop(TrayArea.Shown, 2), OnBar(182));
    }

    [Fact]
    public void PastTheLastIconItGoesToTheEnd()
    {
        Assert.Equal(new TrayDrop(TrayArea.Shown, 4), OnBar(290));
        // Never past the icons there are.
        Assert.Equal(new TrayDrop(TrayArea.Shown, 2), OnBar(290, shown: 2));
    }

    [Fact]
    public void TheChevronHidesItAfterTheIconsAlreadyThere()
    {
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 2), OnBar(110));
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 0), OnBar(110, hidden: 0));
    }

    [Fact]
    public void WithoutAChevronTheWholeTrayTakesTheIcon()
    {
        // Nothing is hidden: the tray's icons start at its edge.
        Assert.Equal(new TrayDrop(TrayArea.Shown, 0), OnBar(102, chevron: 0));
    }

    [Fact]
    public void BesideTheTrayADropDoesNothing()
    {
        Assert.Null(OnBar(Left - 1));
        // An icon from the overflow takes the cell after the last icon too, but no further.
        Assert.Null(OnBar(Left + Width + Step));
        Assert.Null(TrayDrags.OnBar(150, Left, 0, Chevron, Step, 4, 2));
        // One moved along the bar has its own cell there already: the block is all it has.
        Assert.Null(OnBar(Left + Width, held: true));
    }

    // --- An icon from the overflow onto the bar (regression) ------------------------------------

    [Fact]
    public void AnIconFromTheOverflowLandsInTheCellAfterTheLastIcon()
    {
        // The person: "dragging and dropping system tray icons from the hidden view into the shown
        // view is not currently possible." The tray block ends on its last cell, so "after the last
        // icon" was only that cell's right half; the cell the icon brings, past the block, was no
        // drop at all.
        Assert.Equal(new TrayDrop(TrayArea.Shown, 4), OnBar(Left + Width));
        Assert.Equal(new TrayDrop(TrayArea.Shown, 4), OnBar(Left + Width + Step - 1));
        Assert.Equal(new TrayDrop(TrayArea.Shown, 2), OnBar(Left + Width, shown: 2));
    }

    [Fact]
    public void ABarShowingNoIconTakesOneFromTheOverflowAfterTheChevron()
    {
        // With every icon behind the chevron, the tray block was the chevron alone, and every drop on
        // it went back into the overflow: no icon could come out onto the bar.
        const float chevronCell = 28;
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 3), TrayDrags.OnBar(Left + 10, Left, chevronCell, Chevron, Step, 0, 3));
        Assert.Equal(new TrayDrop(TrayArea.Shown, 0), TrayDrags.OnBar(Left + Chevron, Left, chevronCell, Chevron, Step, 0, 3));
        Assert.Equal(new TrayDrop(TrayArea.Shown, 0), TrayDrags.OnBar(Left + chevronCell + Step - 1, Left, chevronCell, Chevron, Step, 0, 3));
        Assert.Null(TrayDrags.OnBar(Left + chevronCell + Step, Left, chevronCell, Chevron, Step, 0, 3));
    }

    [Fact]
    public void AnIconFromTheOverflowDroppedOnTheBarIsShownOnTheTaskbar()
    {
        // The whole way, pointer to config: x in the overflow, let go in the cell after the bar's
        // last icon, is "Show on taskbar" there.
        var config = new TrayIconOptions();
        TrayEntry[] icons = [new("a", false), new("b", false), new("x", true)];
        var drop = OnBar(Left + 2 * Step + Chevron + 4, shown: 2, hidden: 1);

        Assert.Equal(new TrayDrop(TrayArea.Shown, 2), drop);
        Assert.True(TrayLayout.Apply(config, icons, "x", drop!.Value));

        var (shown, overflow) = TrayLayout.Split(config, icons);
        Assert.Equal(["a", "b", "x"], shown.Select(icon => icon.Identity));
        Assert.Empty(overflow);
        Assert.Equal(TrayState.Taskbar, config.States["x"]);
    }

    [Fact]
    public void WithoutIconsEveryPlaceIsTheFirst()
    {
        Assert.Equal(0, TrayDrags.IndexAt(500, 100, Step, 0));
        Assert.Equal(0, TrayDrags.IndexAt(500, 100, 0, 4));
    }

    [Fact]
    public void TheRoomBetweenTwoCellsBelongsToTheBoundaryBetweenThem()
    {
        // tray: each icon has a cell of its own now and the step carries the gap after it — 44 and
        // 6 on the taskbar's own bar. An icon let go anywhere in that gap lands between the two,
        // never before the earlier one or after the later one.
        const float step = 50;
        const float cell = 44;
        const float first = 100;

        for (var at = first + cell; at < first + step; at += 1f)
        {
            Assert.Equal(1, TrayDrags.IndexAt(at, first, step, 4));
        }
        for (var at = first + step + cell; at < first + 2 * step; at += 1f)
        {
            Assert.Equal(2, TrayDrags.IndexAt(at, first, step, 4));
        }
        // Inside a cell the turning point is its middle, as it was when the cells touched.
        Assert.Equal(0, TrayDrags.IndexAt(first + 20, first, step, 4));
        Assert.Equal(1, TrayDrags.IndexAt(first + 30, first, step, 4));
    }

    /// <summary>
    /// The cells touch (the person, 2026-10-08: "tray icons sit connected, with no gap between
    /// them"), so half a step is an icon's middle: let go exactly there, the icon lands where that
    /// icon stands, before it, and a pixel past it after it.
    /// </summary>
    [Fact]
    public void LetGoOnAnIconsMiddleItLandsWhereThatIconStands()
    {
        const float step = 54;
        const float first = 100;

        for (var icon = 0; icon < 4; icon++)
        {
            var middle = first + icon * step + step / 2f;
            Assert.Equal(icon, TrayDrags.IndexAt(middle, first, step, 4));
            Assert.Equal(icon + 1, TrayDrags.IndexAt(middle + 1, first, step, 4));
            Assert.Equal(icon, TrayDrags.IndexAt(middle - 1, first, step, 4));
        }
    }

    [Fact]
    public void TheOverflowSaysWhereAmongItsOwn()
    {
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 1), TrayDrags.InOverflow(1, 3));
        // Past the last row, and never past the rows there are.
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 3), TrayDrags.InOverflow(9, 3));
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 0), TrayDrags.InOverflow(-1, 3));
    }

    // --- A drag along the bar: the slot the held icon is over (regression) ---------------------

    [Fact]
    public void AnIconMovedAlongTheBarTakesTheSlotItsMiddleIsOver()
    {
        // The defect the person saw: four icons, the first dragged onto the third went last, and the
        // last dragged onto the second went third — the nearest boundary counted the held icon's own
        // place as if it were still there. Now the slot under the pointer is the place.
        // Icons start at 132, 32 px apart: the third's slot is 196 to 228.
        Assert.Equal(new TrayDrop(TrayArea.Shown, 2), OnBar(200, held: true));
        Assert.Equal(new TrayDrop(TrayArea.Shown, 2), OnBar(226, held: true));
        // The second's slot, from its left edge to its right.
        Assert.Equal(new TrayDrop(TrayArea.Shown, 1), OnBar(165, held: true));
        Assert.Equal(new TrayDrop(TrayArea.Shown, 1), OnBar(194, held: true));
    }

    [Fact]
    public void AHeldIconNeverGoesPastTheLastSlot()
    {
        // Four icons are four slots: the last is 3, however far right the pointer goes in the tray.
        Assert.Equal(new TrayDrop(TrayArea.Shown, 3), OnBar(299, held: true));
        Assert.Equal(new TrayDrop(TrayArea.Shown, 0), OnBar(133, held: true));
        // One icon alone has nowhere else to go.
        Assert.Equal(new TrayDrop(TrayArea.Shown, 0), OnBar(290, shown: 1, held: true));
    }

    [Fact]
    public void AHeldIconOverTheChevronIsStillHidden() =>
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 2), OnBar(110, held: true));

    [Fact]
    public void TheGapBetweenTwoCellsBelongsToTheSlotBeforeIt()
    {
        // tray: a cell and the gap after it are one slot (44 + 6 on the taskbar's own bar).
        const float step = 50;
        const float first = 100;

        for (var at = first; at < first + step; at += 1f)
        {
            Assert.Equal(0, TrayDrags.SlotAt(at, first, step, 4));
        }
        Assert.Equal(1, TrayDrags.SlotAt(first + step, first, step, 4));
        Assert.Equal(3, TrayDrags.SlotAt(first + 9 * step, first, step, 4));
        Assert.Equal(0, TrayDrags.SlotAt(first - 20, first, step, 4));
    }

    private static TrayDrop? OnBar(float along, bool held, int shown = 4, int hidden = 2) =>
        TrayDrags.OnBar(along, Left, Width, Chevron, Step, shown, hidden, held);

    // --- A drag within the overflow: its own row counted out (regression) ---------------------

    [Fact]
    public void ARowMovedForwardInTheOverflowLandsWhereItWasLetGo()
    {
        // Four rows, the first dragged to just before the third (row index 2 as the flyout reads it):
        // counted out of its own place, it is second among the other three.
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 1), TrayDrags.InOverflow(2, 4, from: 0));
        // Past the last row: the end of the other three.
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 3), TrayDrags.InOverflow(4, 4, from: 0));
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 3), TrayDrags.InOverflow(9, 4, from: 1));
    }

    [Fact]
    public void ARowMovedBackInTheOverflowNeedsNoCorrection()
    {
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 0), TrayDrags.InOverflow(0, 4, from: 3));
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 1), TrayDrags.InOverflow(1, 4, from: 3));
        // Let go on its own row, either half: where it was.
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 2), TrayDrags.InOverflow(2, 4, from: 2));
        Assert.Equal(new TrayDrop(TrayArea.Overflow, 2), TrayDrags.InOverflow(3, 4, from: 2));
    }

    // --- The gap holds still near a boundary, and the slides carry on (smooth drag) ------------

    [Fact]
    public void AHeldIconsSlotHoldsUntilThePointerIsWellPastItsEdge()
    {
        const float first = Left + Chevron;
        // Slot 1 runs from first + 32 to first + 64. Resting just past either edge keeps it ...
        Assert.Equal(1, TrayDrags.SlotAt(first + 64 + 3, first, Step, 4, current: 1));
        Assert.Equal(1, TrayDrags.SlotAt(first + 32 - 3, first, Step, 4, current: 1));
        // ... where without it the slot flicks to the neighbour.
        Assert.Equal(2, TrayDrags.SlotAt(first + 64 + 3, first, Step, 4));
        Assert.Equal(0, TrayDrags.SlotAt(first + 32 - 3, first, Step, 4));
        // Well past it, the gap moves on.
        Assert.Equal(2, TrayDrags.SlotAt(first + 64 + 8, first, Step, 4, current: 1));
        Assert.Equal(0, TrayDrags.SlotAt(first + 32 - 8, first, Step, 4, current: 1));
    }

    [Fact]
    public void AnIconFromTheOverflowKeepsItsBoundaryPlaceAgainstAJitteringPointer()
    {
        const float first = Left + Chevron;
        // Place 1 ends at first + 1.5 steps, where the pointer is resting.
        var at = first + 1.5f * Step;
        var place = TrayDrags.IndexAt(at, first, Step, 4);
        for (var wobble = -3; wobble <= 3; wobble++)
        {
            Assert.Equal(place, TrayDrags.IndexAt(at + wobble, first, Step, 4, current: place));
        }
        Assert.Equal(place + 1, TrayDrags.IndexAt(at + 10, first, Step, 4, current: place));
        Assert.Equal(place - 1, TrayDrags.IndexAt(at - 40, first, Step, 4, current: place));
    }

    [Fact]
    public void WithoutAPlaceYetTheNearestIsTaken()
    {
        const float first = Left + Chevron;
        for (var along = first - 10; along < first + 5 * Step; along += 3)
        {
            Assert.Equal(TrayDrags.SlotAt(along, first, Step, 4), TrayDrags.SlotAt(along, first, Step, 4, current: -1));
            Assert.Equal(TrayDrags.IndexAt(along, first, Step, 4), TrayDrags.IndexAt(along, first, Step, 4, current: -1));
        }
    }

    [Fact]
    public void ASlideThatIsUnderWayIsNotRestartedByTheSameTarget()
    {
        var glide = default(TrayGlide);
        Assert.True(glide.To(32f, now: 1000, ms: 167));
        var halfway = glide.At(1000 + 80);
        Assert.InRange(halfway, 1f, 31f);
        // The pointer moves on without changing the gap: nothing restarts, the slide goes on as it was.
        Assert.False(glide.To(32f, now: 1000 + 80, ms: 167));
        Assert.Equal(halfway, glide.At(1000 + 80));
        Assert.Equal(32f, glide.At(1000 + 167));
    }

    [Fact]
    public void ANewTargetTakesUpFromWhereTheIconIsByThen()
    {
        var glide = default(TrayGlide);
        glide.To(32f, now: 0, ms: 167);
        var there = glide.At(60);
        Assert.True(glide.To(0f, now: 60, ms: 167));
        // No jump at the turn: it starts where it was, then goes back.
        Assert.Equal(there, glide.At(60), 3);
        Assert.True(glide.At(60 + 80) < there);
        Assert.Equal(0f, glide.At(60 + 167));
    }

    [Fact]
    public void ADraggedIconFollowingThePointerJumpsAndLeavesNoGlideBehind()
    {
        var glide = default(TrayGlide);
        glide.To(32f, now: 0, ms: 167);
        glide.Jump(17f);
        Assert.Equal(17f, glide.At(10));
        Assert.Equal(17f, glide.Target);
    }

    /// <summary>
    /// A bar laid out again mid-drag (the chevron coming as the drag starts) takes each icon up where
    /// the last scene had it, on its way to its place, so the next move goes on from there: it used
    /// to start at the new place, and the icon jumped there and back once.
    /// </summary>
    [Fact]
    public void AnIconTakenUpByANewSceneGoesOnFromWhereItWas()
    {
        var glide = TrayGlide.Settling(-30f, 1000, 167);
        Assert.Equal(-30f, glide.At(1000));
        Assert.Equal(0f, glide.At(1167));
        Assert.Equal(0f, glide.Target);
        // Its place again changes nothing: the slide under way goes on.
        Assert.False(glide.To(0f, 1050, 167));
        // A new target takes up from where it is by then, not from its place.
        var at = glide.At(1050);
        Assert.True(at < 0f && at > -30f);
        Assert.True(glide.To(44f, 1050, 167));
        Assert.Equal(at, glide.At(1050), 3);
    }

    /// <summary>
    /// Every icon a new scene moves is taken up, not only the ones a drag had moved (the person,
    /// 2026-10-16: "not all of the tray icons get animated on their move, some just jump"): from
    /// where it was along the bar, settling into its place; on a side bar along y.
    /// </summary>
    [Fact]
    public void EveryIconANewSceneMovesIsTakenUp()
    {
        var along = TrayGlide.TakenUp(new Vector3(140f, 6f, 0f), new Vector3(108f, 6f, 0f), vertical: false, now: 500, ms: 167);
        Assert.NotNull(along);
        Assert.Equal(32f, along.Value.At(500));
        Assert.Equal(0f, along.Value.At(667));
        var down = TrayGlide.TakenUp(new Vector3(6f, 60f, 0f), new Vector3(6f, 92f, 0f), vertical: true, now: 500, ms: 167);
        Assert.NotNull(down);
        Assert.Equal(-32f, down.Value.At(500));
    }

    /// <summary>One that stays where it is has nothing to go on from; one that moved across the bar, a line of its own, can't be said along it.</summary>
    [Fact]
    public void AnIconThatStaysOrChangesLineIsNotTakenUp()
    {
        Assert.Null(TrayGlide.TakenUp(new Vector3(108f, 6f, 0f), new Vector3(108f, 6f, 0f), vertical: false, now: 0, ms: 167));
        Assert.Null(TrayGlide.TakenUp(new Vector3(108f, 6f, 0f), new Vector3(140f, 38f, 0f), vertical: false, now: 0, ms: 167));
        Assert.Null(TrayGlide.TakenUp(new Vector3(6f, 60f, 0f), new Vector3(38f, 92f, 0f), vertical: true, now: 0, ms: 167));
    }

    /// <summary>
    /// Regression: an icon a tray grown by a cell moved, sliding there, then given a gap to open by
    /// the drag's next move, went on from where it was; it had no glide, so it jumped back to its new
    /// place and slid from there.
    /// </summary>
    [Fact]
    public void ATakenUpIconOpeningAGapGoesOnFromWhereItIs()
    {
        var glide = TrayGlide.TakenUp(new Vector3(140f, 6f, 0f), new Vector3(108f, 6f, 0f), vertical: false, now: 0, ms: 167)!.Value;
        var there = glide.At(40);
        Assert.InRange(there, 1f, 31f);
        Assert.True(glide.To(32f, now: 40, ms: 167));
        Assert.Equal(there, glide.At(40), 3);
        Assert.Equal(32f, glide.At(40 + 167));
    }
}
