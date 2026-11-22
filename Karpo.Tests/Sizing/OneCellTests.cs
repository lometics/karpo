// A kind that says it is one cell has to be one cell.
//
// WidgetSize.OneCell is a measurement: the kind draws a single glyph centred in
// its square and has nothing anywhere in its drawing that reads more room, so a
// second cell would leave the glyph adrift rather than make the widget bigger.
// Twelve kinds say it. (The bar's separator was the thirteenth and was the one that
// was wrong: a line reads no square at all. It left cells, and is gone since §511.)
//
// Saying it is cheap and being it is not, so this holds the claim from the other
// end. The registry cannot be asked whether a drawing has a branch that uses the
// room — but it can be asked what the widget measures when nothing is capping
// it, and a kind whose content genuinely fits one cell measures one cell however
// much room it is offered. A kind marked OneCell that really wanted two would be
// having its content cut off by its own descriptor, quietly, and this is where
// that shows. The descriptor no longer caps what a person chooses — it is where a
// new widget starts (WidgetBounds.DefaultFor) — but every new one starts there, so
// a kind that wanted two would still arrive cut off.

using Karpo.Dock;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Sizing;

public sealed class OneCellTests
{
    private static Look LookFor(float textScale) => new(
        Glass: false,
        Dark: true,
        Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));

    /// <summary>
    /// Every kind capped at one cell, measured with nothing capping it: at each
    /// bar thickness, both ways round, and at the largest text anybody can ask
    /// Windows for, because a glyph that fits at one size and not another is the
    /// same bug arriving later.
    /// </summary>
    [UiFact]
    public void AKindCappedAtOneCellMeasuresOneCell()
    {
        var kinds = BarEntries.All.Where(entry => entry.Size.MaxCells == 1).Select(entry => entry.Kind).ToList();
        Assert.NotEmpty(kinds);

        var measured = 0;
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var height in (int[])[28, 41, 48, 72, 160])
            {
                var config = new Config { Height = height };
                var cell = Layout.CellPx(96, config);
                foreach (var vertical in new[] { false, true })
                {
                    foreach (var textScale in new[] { 1f, 2.25f })
                    {
                        // Any width at all, which is what makes this a test of the
                        // kind rather than of the cap: with the cap applied every
                        // kind would measure one cell whatever it wanted.
                        var style = new IslandStyle(1f, vertical, cell, Layout.IconPx(96, config), LookFor(textScale))
                        {
                            Room = WidgetBounds.Any,
                        };
                        foreach (var kind in kinds)
                        {
                            var key = Config.IslandKey(null, "one");
                            if (Karpo.Dock.Islands.Create(key, kind.New(), gfx, config) is not { } island)
                            {
                                // Drawn by the bar itself rather than by an island
                                // (Start, the pins): nothing to measure here.
                                continue;
                            }
                            using (island)
                            {
                                _ = island.Refresh(gfx);
                                var cells = island.Measure(style);
                                // Each of the island's cells, which is one for a
                                // kind like these, but summed rather than assumed.
                                var wide = 0f;
                                for (var index = 0; index < cells.Count; index++)
                                {
                                    wide += cells.Lengths is { } list && index < list.Count
                                        ? list[index]
                                        : cells.CellLength;
                                }
                                Assert.True(
                                    wide <= cell + 0.5f,
                                    $"{kind} wants {wide / cell:0.##} cells at {height} DIP"
                                        + $"{(vertical ? " vertical" : "")}, text {textScale}: it is not a one-cell kind");
                                measured++;
                            }
                        }
                    }
                }
            }
        });

        // The loop has to have met some of them, or the claim is untested — unless every one is
        // drawn by the bar itself, which sizes it to its cell: the contacts were the last island
        // of one cell (2026-10-11, the contacts preset), the rest Windows' buttons and the pins.
        Assert.True(
            measured > 0 || kinds.All(kind => BarEntries.Find(kind)!.Drawing == WidgetSource.None),
            "no kind capped at one cell was an island that could be measured");
    }

    /// <summary>
    /// A one-cell kind is one cell on the bar, always: nobody sets an entry's width
    /// (2026-10-08), and four to six that a file still holds is dropped where it is read.
    /// </summary>
    [Fact]
    public void AOneCellKindIsOneCellWhateverAFileSays()
    {
        var kinds = BarEntries.All.Where(entry => entry.Size.MaxCells == 1).ToList();
        Assert.NotEmpty(kinds);
        foreach (var kind in kinds)
        {
            Assert.Equal(new WidgetBounds(1, 1), WidgetBounds.For(kind.Kind.New()));

            var island = kind.Kind.New();
            island.Height = LineHeight.Exactly(8);
            island.Normalize();

            Assert.Equal(new WidgetBounds(1, 1), WidgetBounds.For(island));
            Assert.Null(island.Height);
        }
    }
}
