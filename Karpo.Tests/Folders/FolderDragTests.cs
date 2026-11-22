// edit: where a line carried in an arranged folder lands, read from the line itself (FolderDrag.Gap;
// the person, 2026-10-12: reordering in a list "as smooth as the taskbar"). Pure: plans made by hand,
// lines of the heights each case needs.
//
//  - in its own list a line passes another once its leading edge is past that line's middle — its
//    bottom going down, its top going up — whatever the two lines' heights;
//  - regression: a tall line carried over the middle of a shorter one under it lands under it (read
//    from the pointer it went back where it was: the old Aim);
//  - headings and dividers, of no row's height, are passed the same way, by their own middles;
//  - in another list its middle against the lines' middles decides; an empty list takes it first;
//  - the copy a move is planned on leaves the folder alone; carried away, its list closes up;
//  - Ctrl+arrows: a place up, down, or into the list beside;
//  - a list scrolls itself for a line held near its end, harder the deeper in.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Folders;

public sealed class FolderDragTests
{
    /// <summary>A plan of one list or more whose lines stand at <paramref name="cards"/>: all <see cref="FolderDrag.Gap"/> reads.</summary>
    private static FolderPlan Plan(params D2D_RECT_F[] cards) => new()
    {
        Width = 360,
        Height = cards.Length == 0 ? 0 : cards.Max(card => card.bottom),
        Side = 40,
        Pad = 12,
        Radius = 8,
        Hairline = 1,
        ListGap = 6,
        Lists = [],
        Cards = cards,
        CardList = [.. cards.Select(card => card.left < 360 ? 0 : 1)],
        Empty = [],
        Adds = [],
        TextPx = 14,
        SmallPx = 12,
    };

    /// <summary>A line of list 0 from <paramref name="top"/>, <paramref name="height"/> px tall.</summary>
    private static D2D_RECT_F Line(float top, float height, int list = 0) => new() { left = list * 366, top = top, right = (list * 366) + 360, bottom = top + height };

    private static FolderPlace?[] Places(params (int List, int Index)[] places) => [.. places.Select(place => (FolderPlace?)new FolderPlace(place.List, place.Index))];

    /// <summary>Lines of equal height: past the next one's middle with its bottom, the carried line goes under it; short of it, it stays.</summary>
    [Fact]
    public void ALineGoesUnderTheNextOnceItsBottomIsPastThatLinesMiddle()
    {
        var plan = Plan(Line(0, 40), Line(42, 40), Line(84, 40));
        var places = Places((0, 0), (0, 1), (0, 2));

        // The next line's middle is at 62: the carried line's bottom at 40 + 21.
        Assert.Equal(0, FolderDrag.Gap(plan, places, 0, 0, 21, 61));
        Assert.Equal(2, FolderDrag.Gap(plan, places, 0, 0, 23, 63));
        // On past the last one's middle (104): the list's end.
        Assert.Equal(3, FolderDrag.Gap(plan, places, 0, 0, 65, 105));
        // Up again: back where it was.
        Assert.Equal(0, FolderDrag.Gap(plan, places, 0, 0, 5, 45));
    }

    /// <summary>Going up, the carried line's top past a line's middle puts it over that line.</summary>
    [Fact]
    public void ALineGoesOverTheOneAboveOnceItsTopIsPastThatLinesMiddle()
    {
        var plan = Plan(Line(0, 40), Line(42, 40), Line(84, 40));
        var places = Places((0, 0), (0, 1), (0, 2));

        // The last line, up: over the middle one (middle 62), then over the first (middle 20).
        Assert.Equal(2, FolderDrag.Gap(plan, places, 2, 0, 63, 103));
        Assert.Equal(1, FolderDrag.Gap(plan, places, 2, 0, 61, 101));
        Assert.Equal(0, FolderDrag.Gap(plan, places, 2, 0, 19, 59));
    }

    /// <summary>
    /// Regression (the person, 2026-10-12): a tall line carried down over the middle of a shorter one
    /// under it did not count as a move — read from the pointer, which stood in the tall line's
    /// middle, it had to be carried right past the short one. Read from its bottom it goes under it
    /// once it covers half of it, and the drop is a move (<see cref="FolderDrag.Moves"/>).
    /// </summary>
    [Fact]
    public void ATallLineCarriedOverTheMiddleOfAShorterOneLandsUnderIt()
    {
        // A line three rows tall, then a single row: its middle at 142.
        var plan = Plan(Line(0, 120), Line(122, 40));
        var places = Places((0, 0), (0, 1));

        Assert.Equal(0, FolderDrag.Gap(plan, places, 0, 0, 21, 141));
        var gap = FolderDrag.Gap(plan, places, 0, 0, 30, 150);
        Assert.Equal(2, gap);
        var folder = Folder(2);
        Assert.True(FolderDrag.Moves(folder, [folder.Lists[0][0]], new FolderPlace(0, gap)));
        // Where the old reading put it, carried by its middle with the tall line's middle (90) short
        // of the short line's (142): back where it was, no move.
        Assert.False(FolderDrag.Moves(folder, [folder.Lists[0][0]], new FolderPlace(0, 1)));
    }

    /// <summary>A short line carried up past a tall one goes over it once its top reaches the tall one's middle.</summary>
    [Fact]
    public void AShortLineCarriedUpPassesATallOneAtItsMiddle()
    {
        var plan = Plan(Line(0, 120), Line(122, 40));
        var places = Places((0, 0), (0, 1));

        Assert.Equal(1, FolderDrag.Gap(plan, places, 1, 0, 61, 101));
        Assert.Equal(0, FolderDrag.Gap(plan, places, 1, 0, 59, 99));
    }

    /// <summary>
    /// A heading and a divider are of no row's height — a heading its words and their air, a divider
    /// Appearance's gap — and are passed by their own middles, as any line.
    /// </summary>
    [Fact]
    public void HeadingsAndDividersArePassedByTheirOwnMiddles()
    {
        // A line, a heading 28 px tall, a divider of 6, a line.
        var plan = Plan(Line(0, 40), Line(42, 28), Line(70, 6), Line(76, 40));
        var places = Places((0, 0), (0, 1), (0, 2), (0, 3));

        // Down past the heading (middle 56), then the divider (middle 73), then the last line (middle 96).
        Assert.Equal(0, FolderDrag.Gap(plan, places, 0, 0, 15, 55));
        Assert.Equal(2, FolderDrag.Gap(plan, places, 0, 0, 17, 57));
        Assert.Equal(3, FolderDrag.Gap(plan, places, 0, 0, 34, 74));
        Assert.Equal(4, FolderDrag.Gap(plan, places, 0, 0, 57, 97));
        // The divider itself carried up past the heading: its top past the heading's middle.
        Assert.Equal(1, FolderDrag.Gap(plan, places, 2, 0, 55, 61));
        // The heading carried down past the divider: its bottom past the divider's middle.
        Assert.Equal(3, FolderDrag.Gap(plan, places, 1, 0, 46, 74));
    }

    /// <summary>
    /// In another list nothing has given way for it yet: it goes over the first line whose middle is
    /// under its own middle, at the list's end past them all, first in an empty one.
    /// </summary>
    [Fact]
    public void InAnotherListItsMiddleDecides()
    {
        // List 0: one line; list 1: a tall line and a short one.
        var plan = Plan(Line(0, 40), Line(0, 120, list: 1), Line(122, 40, list: 1));
        var places = Places((0, 0), (1, 0), (1, 1));

        Assert.Equal(0, FolderDrag.Gap(plan, places, 0, 1, 30, 70));
        Assert.Equal(1, FolderDrag.Gap(plan, places, 0, 1, 50, 90));
        Assert.Equal(1, FolderDrag.Gap(plan, places, 0, 1, 115, 155));
        Assert.Equal(2, FolderDrag.Gap(plan, places, 0, 1, 130, 170));
        // A list of nothing but it: first.
        Assert.Equal(0, FolderDrag.Gap(plan, places, 0, 2, 300, 340));
    }

    private static FolderOptions Folder(params int[] lists)
    {
        var options = new FolderOptions { Lists = [] };
        foreach (var count in lists)
        {
            options.Lists.Add([.. Enumerable.Range(0, count).Select(_ => new FolderCell { Island = ListKind.EnergySaver.New() })]);
        }
        return options;
    }

    /// <summary>The move the lines give way for is planned on a copy: the folder is as it was until the line is let go.</summary>
    [Fact]
    public void TheMovePlannedLeavesTheFolderAlone()
    {
        var options = Folder(3, 1);
        var first = options.Lists[0][0];
        var lists = options.Lists.Select(list => list.ToList()).ToList();

        var moved = FolderDrag.Moved(options, [first], new FolderPlace(1, 1));

        Assert.Equal(lists[0], options.Lists[0]);
        Assert.Equal(lists[1], options.Lists[1]);
        Assert.Equal([lists[0][1], lists[0][2]], moved.Lists[0]);
        Assert.Equal([lists[1][0], first], moved.Lists[1]);
        Assert.False(FolderDrag.Moves(options, [first], new FolderPlace(0, 1)));
        Assert.Equal(lists[0], options.Lists[0]);
    }

    /// <summary>Ctrl+arrows: a place up or down its list, or into the list beside at the same place, its end where that one is shorter; nothing past an end.</summary>
    [Fact]
    public void CtrlArrowsMoveALinePlaceByPlace()
    {
        var options = Folder(3, 1);

        Assert.Equal(new FolderPlace(0, 0), FolderDrag.KeyPlace(options, new FolderPlace(0, 1), VK.VK_UP));
        Assert.Null(FolderDrag.KeyPlace(options, new FolderPlace(0, 0), VK.VK_UP));
        // Down is the gap under the next line, as Folders.Move reads it.
        Assert.Equal(new FolderPlace(0, 3), FolderDrag.KeyPlace(options, new FolderPlace(0, 1), VK.VK_DOWN));
        Assert.Null(FolderDrag.KeyPlace(options, new FolderPlace(0, 2), VK.VK_DOWN));
        Assert.Equal(new FolderPlace(1, 1), FolderDrag.KeyPlace(options, new FolderPlace(0, 2), VK.VK_RIGHT));
        Assert.Equal(new FolderPlace(0, 0), FolderDrag.KeyPlace(options, new FolderPlace(1, 0), VK.VK_LEFT));
        Assert.Null(FolderDrag.KeyPlace(options, new FolderPlace(0, 0), VK.VK_LEFT));
        Assert.Null(FolderDrag.KeyPlace(options, new FolderPlace(1, 0), VK.VK_RIGHT));
        Assert.Null(FolderDrag.KeyPlace(options, new FolderPlace(0, 0), VK.VK_HOME));
    }

    /// <summary>
    /// The lines that give way stand where the folder will lay them out, held to the bar's side as
    /// the lists are: on a bottom bar a plan grown taller stands as much higher, on a top bar where it
    /// is, on a side bar half as much higher, its middle where it was (§303).
    /// </summary>
    [Fact]
    public void TheLinesGivingWayStandAsTheListsLineUpOnTheBarsSide()
    {
        var rest = Plan(Line(0, 40), Line(42, 40));
        var taller = Plan(Line(0, 40), Line(42, 40), Line(84, 40));

        Assert.Equal(-42, FolderDrag.Down(rest, taller, Edge.Bottom));
        Assert.Equal(0, FolderDrag.Down(rest, taller, Edge.Top));
        Assert.Equal(-21, FolderDrag.Down(rest, taller, Edge.Left));
        Assert.Equal(-21, FolderDrag.Down(rest, taller, Edge.Right));
        Assert.Equal(new System.Numerics.Vector2(0, 42), FolderDrag.At(rest, taller, 2, Edge.Bottom));
        Assert.Equal(new System.Numerics.Vector2(0, 84), FolderDrag.At(rest, taller, 2, Edge.Top));
        Assert.Equal(new System.Numerics.Vector2(0, 63), FolderDrag.At(rest, taller, 2, Edge.Left));
    }
}
