// folders: an open folder has no title, and a list taller than the screen scrolls.
//
// The title was a panel of its own between the lists and the bar (2026-10-09); the person took it
// away the next day: "remove the folder names completely. they just dont look good." (A folder has
// no name at all since §427.) The open folder is its lists alone, the one
// next to the bar as near it as a flyout stands, no room kept for a row that is not there.

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Folders;

public sealed class FolderTitleTests
{
    private static Look Look => new(Glass: false, Dark: true, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));

    private static FolderOptions List(int lines, int lists = 1)
    {
        var options = new FolderOptions();
        for (var list = 0; list < lists; list++)
        {
            for (var row = 0; row < lines + list; row++)
            {
                options.Put(ListKind.Battery.New(), list);
            }
        }
        return options;
    }

    private static FolderPlan Plan(FolderOptions options, bool editing = true, float workHeight = 1040, Edge edge = Edge.Bottom) =>
        FolderFlyout.Build(Look, 1f, FolderView.List, options, options.Cells, workHeight, editing: editing, edge: edge);

    /// <summary>
    /// On a display too short for it, a tall folder shows what fits rather than running off the
    /// screen: the folder never reaches past the room beside the bar less an arranged list's "+" (§185).
    /// </summary>
    [Fact]
    public void ATallFolderOnAShortScreenShowsWhatFits()
    {
        var plan = Plan(List(FolderOptions.MostCells), editing: false, workHeight: 600);

        var reach = FolderFlyout.Reach(600, 0, 1);
        Assert.True(plan.Height <= reach, $"the folder is {plan.Height} of a 600 px work area");
        Assert.NotEmpty(plan.Lists[0].Hidden);
        // All of the reach is the list's: no row is kept back for a title, so less than a line of it is left.
        Assert.InRange(reach - plan.Lists[0].Height, 0, plan.Cards[0].bottom - plan.Cards[0].top + 2);
    }

    /// <summary>
    /// No title: the folder is as tall as its tallest list, its panels are its lists' cards and
    /// nothing else, and the lists stand on the bar's side of the box — their bottoms its bottom
    /// over a bottom bar, their tops its top under a top bar, their middles its middle beside a side
    /// bar (§303) — at rest and arranged alike.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, 1)]
    [InlineData(Edge.Bottom, 3)]
    [InlineData(Edge.Top, 1)]
    [InlineData(Edge.Top, 3)]
    [InlineData(Edge.Left, 2)]
    public void AnOpenFolderIsItsListsAlone(Edge edge, int lists)
    {
        foreach (var editing in new[] { false, true })
        {
            var plan = Plan(List(2, lists), editing, edge: edge);

            Assert.Equal(plan.Lists.Max(list => list.Height), plan.Height, 3);
            Assert.Equal(plan.Lists.Sum(list => list.Runs.Count), plan.Pieces.Count);
            Assert.All(plan.Pieces, piece => Assert.Contains(plan.Lists, list => list.Box.left == piece.left && piece.top >= list.Box.top && piece.bottom <= list.Box.bottom));
            foreach (var list in plan.Lists)
            {
                switch (edge)
                {
                    case Edge.Top:
                        Assert.Equal(0, list.Box.top, 3);
                        break;
                    case Edge.Left or Edge.Right:
                        Assert.InRange((list.Box.top + list.Box.bottom) / 2, (plan.Height / 2) - 0.5f, (plan.Height / 2) + 0.5f);
                        break;
                    default:
                        Assert.Equal(plan.Height, list.Box.bottom, 3);
                        break;
                }
            }
        }
    }
}
