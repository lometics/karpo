// A folder's lines edge to edge (the person, 2026-10-29: "the widgets themselves in the folder have a gap between
// them, this should not be, there should bascially be no folder background visible at all. the widgets themselves
// should also not have rounded corners, only the folder sections should have them"; docs\widgets.md §410).
//
// Held here, pure, at 100, 125, 150 and 200 %: every section (a run of lines, FolderListPlan.Runs; a separator, empty
// space, parts a column into two, §505) is tiled by its lines with no gap, each as wide as its section; the sections
// alone are rounded. §624: a line has no outline of its own (§410's seams and §444's rings went with it). Pictured in
// FolderFlushPictures.

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Folders;

public sealed class FolderFlushTests
{
    private static Look Look => new(Glass: false, Dark: true, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));

    /// <summary>Two columns: three switches, a separator, the battery and the clock; the CPU and memory, a separator, a note.</summary>
    private static FolderOptions Folder()
    {
        var options = new FolderOptions { Lists = [] };
        ListKind[][] lists =
        [
            [ListKind.NightLight, ListKind.AirplaneMode, ListKind.EnergySaver, ListKind.Divider, ListKind.Battery, ListKind.Clock],
            [ListKind.Cpu, ListKind.Memory, ListKind.Divider, ListKind.Note],
        ];
        for (var list = 0; list < lists.Length; list++)
        {
            foreach (var kind in lists[list])
            {
                options.Put(kind.New(), list);
            }
        }
        return options;
    }

    public static TheoryData<float, bool> Cases()
    {
        var cases = new TheoryData<float, bool>();
        foreach (var scale in new[] { 1f, 1.25f, 1.5f, 2f })
        {
            foreach (var editing in new[] { false, true })
            {
                cases.Add(scale, editing);
            }
        }
        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ALinesSectionIsTiledByItsLinesAndOnlyTheSectionIsRound(float scale, bool editing)
    {
        var options = Folder();
        var plan = FolderFlyout.Build(Look, scale, FolderView.List, options, options.Cells, 2000, editing: editing);
        // A separator splits each column into two sections (§505: empty space).
        Assert.All(plan.Lists, list => Assert.Equal(2, list.Runs.Count));
        Assert.True(plan.Radius > 0, "the sections are rounded");
        for (var list = 0; list < plan.Lists.Count; list++)
        {
            foreach (var run in plan.Lists[list].Runs)
            {
                // What stands on the section, top to bottom: its lines.
                var on = Enumerable.Range(0, plan.Cards.Count)
                    .Where(index => plan.CardList[index] == list && plan.Shows(index))
                    .Where(index => plan.Cards[index].top >= run.top - 0.5f && plan.Cards[index].bottom <= run.bottom + 0.5f)
                    .OrderBy(index => plan.Cards[index].top)
                    .ToList();
                Assert.NotEmpty(on);
                Assert.Equal(run.top, plan.Cards[on[0]].top, 0.01f);
                Assert.Equal(run.bottom, plan.Cards[on[^1]].bottom, 0.01f);
                for (var at = 1; at < on.Count; at++)
                {
                    var gap = plan.Cards[on[at]].top - plan.Cards[on[at - 1]].bottom;
                    Assert.True(Math.Abs(gap) < 0.01f, $"{scale * 100:0} %: line {on[at - 1]} and line {on[at]} stand {gap} px apart, not edge to edge");
                }
                foreach (var index in on)
                {
                    var card = plan.Cards[index];
                    Assert.Equal(run.left, card.left, 0.01f);
                    Assert.Equal(run.right, card.right, 0.01f);
                }
            }
        }
    }
}
