// The folder's grid (docs\widgets.md §442; the person, 2026-10-30, of two columns of sized lines: "there are a lot
// of spaces in between the individual widgets. i want them to be smaller / more densly together" — "make the grid
// finer"; and before: "design on the cell height as well, so that it is possible to actually make designs where the
// widgets combined give the same folder height").
//
// Every line of a folder is a whole number of cells, half a row each (FolderOptions.CellDip, 20 DIP at 100 % and the
// list's text), and n cells are n times a cell, exactly, at every scale and text size: lines stand flush, so any two
// columns whose lines add up to the same cells end level, whatever the lines are — a line of four rows is two of two.
// Pure: FolderFlyout.Build takes no device.

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Folders;

public sealed class FolderGridTests
{
    private static Look Look(float textScale) => new(Glass: false, Dark: true, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4), TextScale: textScale);

    public static TheoryData<float, float> Scales()
    {
        var cases = new TheoryData<float, float>();
        foreach (var scale in new[] { 1f, 1.25f, 1.5f, 2f })
        {
            foreach (var textScale in new[] { 1f, 1.25f, 2.25f })
            {
                cases.Add(scale, textScale);
            }
        }
        return cases;
    }

    /// <summary>A folder of notes, a list each of <paramref name="lists"/>, each note fixed at its count.</summary>
    private static FolderOptions Notes(params int[][] lists)
    {
        var options = new FolderOptions { Lists = [] };
        for (var list = 0; list < lists.Length; list++)
        {
            foreach (var count in lists[list])
            {
                var note = ListKind.Note.New();
                note.Height = LineHeight.Exactly(count);
                options.Put(note, list);
            }
        }
        return options;
    }

    /// <summary>
    /// Columns of lines fixed at counts that add up to the same are the same height: one of four rows beside two of two,
    /// one of a row and a half beside three halves.
    /// </summary>
    [Theory]
    [MemberData(nameof(Scales))]
    public void ColumnsOfTheSameCellsEndLevel(float scale, float textScale)
    {
        var options = Notes([8], [4, 4], [2, 6], [3, 3, 2]);
        var plan = FolderFlyout.Build(Look(textScale), scale, FolderView.List, options, options.Cells, 4000);
        var tall = plan.Lists.Select(list => list.Box.bottom - list.Box.top).ToList();
        Assert.True(tall.All(each => MathF.Abs(each - tall[0]) < 0.01f),
            $"{scale * 100:0} %, {textScale * 100:0} % text: columns of the same cells are {string.Join(", ", tall.Select(each => $"{each:0.##}"))} px");
    }


    /// <summary>A line of n cells is n times a cell, exactly, and a cell a whole number of px.</summary>
    [Theory]
    [MemberData(nameof(Scales))]
    public void NCellsAreNTimesACell(float scale, float textScale)
    {
        var cell = FolderFlyout.CellPx(scale, textScale);
        Assert.Equal(MathF.Round(cell), cell);
        Assert.Equal(PopupPaint.Round(FolderOptions.CellDip * scale * textScale), cell);
        for (var cells = LineHeight.Fewest; cells <= 30; cells++)
        {
            var options = Notes([cells]);
            var plan = FolderFlyout.Build(Look(textScale), scale, FolderView.List, options, options.Cells, 4000);
            Assert.Equal(cells * cell, plan.Cards[0].bottom - plan.Cards[0].top, 0.01f);
        }
    }

}
