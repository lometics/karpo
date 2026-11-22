// A folder widget's height down its list (Karpo.Core\LineHeight.cs, docs\widgets.md §76): Automatic,
// its content up to a most, or Fixed, exactly its rows. The person, 2026-10-09: "either the height is
// automatic, then you can just set a max height, or the height is fixed, then you just set the
// height", for every widget in a folder, and "the folder itself should always just grow to its
// contents".
//
// This file holds the parts of that which are pure: the rule itself, the file's words for it and how
// a file from before the two modes reads, where a new line starts, and the list's own arithmetic in
// FolderFlyout.Build, which takes the content's height as a number measured elsewhere and so can be
// held here without a device. What each kind draws at the heights it is given is
// LineHeightPicturesTests'.

using System.Text.Json;
using Karpo.Dock;

namespace Karpo.Tests.Sizing;

public sealed class ListBoundsTests
{
    private static Look Look => new(Glass: false, Dark: true, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));

    /// <summary>n of the folder's cells at 100 %, 20 DIP each, with no gap (§442).</summary>
    private static float Cells(int cells) => cells * FolderOptions.CellDip;

    /// <summary>n rows of the list at 100 %, two cells each.</summary>
    private static float Rows(int rows) => Cells(rows * LineHeight.Row);

    /// <summary>A list of <paramref name="lines"/>, one line each, at 100 %.</summary>
    private static FolderPlan Plan(IReadOnlyList<float>? natural, params CellItem[] lines)
    {
        var options = new FolderOptions();
        for (var row = 0; row < lines.Length; row++)
        {
            options.Put(lines[row]);
        }
        // A work area tall enough for every line: one that does not fit is not shown, and has no height (2026-10-12).
        return FolderFlyout.Build(Look, 1f, FolderView.List, options, options.Cells, 100_000, natural: natural);
    }

    private static float Tall(FolderPlan plan, int index) => plan.Cards[index].bottom - plan.Cards[index].top;

    /// <summary>A line of <paramref name="kind"/> at <paramref name="height"/>, or where a new line of it starts.</summary>
    private static CellItem Sized(ListKind kind, LineHeight? height = null)
    {
        var line = kind.New();
        if (height is { } given)
        {
            line.Height = given;
        }
        line.Normalize();
        return line;
    }

    // --- The rule ---------------------------------------------------------------------------------

    /// <summary>
    /// Automatic is the content, from one row up to its most; Fixed is its rows, whatever the
    /// content. Over contents under a row, between, at and past the most.
    /// </summary>
    [Theory]
    [InlineData(false, 12, 12f, 40f)]
    [InlineData(false, 12, 125f, 140f)]
    [InlineData(false, 12, 120f, 120f)]
    [InlineData(false, 12, 240f, 240f)]
    [InlineData(false, 12, 900f, 240f)]
    [InlineData(false, 13, 900f, 260f)]
    [InlineData(false, 2, 900f, 40f)]
    [InlineData(true, 8, 12f, 160f)]
    [InlineData(true, 8, 160f, 160f)]
    [InlineData(true, 8, 1200f, 160f)]
    [InlineData(true, 7, 1200f, 140f)]
    [InlineData(true, 2, 300f, 40f)]
    public void TheRule(bool fixedHeight, int cells, float content, float given) =>
        Assert.Equal(given, new LineHeight(fixedHeight, cells).Given(content, FolderOptions.CellDip), 1);

    /// <summary>
    /// Rows out of 1 to <see cref="LineHeight.Most"/>, as a hand-edited file may hold them, are the
    /// nearest that are; 99, past the 32 that was the most once, is kept (§192).
    /// </summary>
    [Fact]
    public void RowsAreHeldToOneToTheMost()
    {
        Assert.Equal(LineHeight.Exactly(2), new LineHeight(true, 0).Within());
        Assert.Equal(LineHeight.Automatic(99), new LineHeight(false, 99).Within());
        Assert.Equal(LineHeight.Automatic(LineHeight.Most), new LineHeight(false, 999).Within());
        Assert.Equal(Cells(LineHeight.Most), new LineHeight(true, 400).Given(10, FolderOptions.CellDip), 1);
    }

    // --- The file ---------------------------------------------------------------------------------

    /// <summary>The height is written in its own words, "fixed" and "cells", and read back as written.</summary>
    [Fact]
    public void TheHeightIsWrittenInItsOwnWords()
    {
        foreach (var height in new[] { LineHeight.Automatic(12), LineHeight.Exactly(6) })
        {
            var line = Sized(ListKind.Calendar, height);
            var json = JsonSerializer.Serialize(line, ConfigJson.Default.CellItem);
            using (var document = JsonDocument.Parse(json))
            {
                var written = document.RootElement.GetProperty("height");
                Assert.Equal(height.Fixed, written.GetProperty("fixed").GetBoolean());
                Assert.Equal(height.Cells, written.GetProperty("cells").GetInt32());
            }
            Assert.DoesNotContain("atLeast", json, StringComparison.Ordinal);
            Assert.DoesNotContain("atMost", json, StringComparison.Ordinal);
            var back = JsonSerializer.Deserialize(json, ConfigJson.Default.CellItem)!;
            back.View = FolderView.List;
            back.Normalize();
            Assert.Equal(height, back.Height);
        }
    }

    /// <summary>
    /// A file's height reads as it says, held to what the line allows: automatic or fixed, and its
    /// rows; rows that are none are where the line starts. Written back, the same.
    /// </summary>
    [Theory]
    [InlineData("\"height\": { \"fixed\": false, \"cells\": 18 }", false, 18)]
    [InlineData("\"height\": { \"fixed\": true, \"cells\": 13 }", true, 13)]
    [InlineData("\"height\": { \"fixed\": false, \"cells\": 1800 }", false, LineHeight.Most)]
    [InlineData("\"height\": { \"cells\": 10 }", false, 10)]
    [InlineData("\"height\": { \"fixed\": true }", false, 24)]
    public void AFilesHeightIsReadAsItSays(string said, bool fixedHeight, int rows)
    {
        var config = Karpo.Tests.Core.Configs.Repaired($$"""
            { "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "l", "kind": "folderList", "folder": { "lists": [ [
                { "island": { "id": "a", "kind": "calendar", {{said}} } } ] ] } } ] } ] }
            """);
        var line = Assert.Single(config.Layouts[0].Islands.Single(island => island.Id == "l").Folder!.Cells).Island;

        Assert.Equal(new LineHeight(fixedHeight, rows), line.Height);
        var again = Karpo.Tests.Core.Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(new LineHeight(fixedHeight, rows), Assert.Single(again.Layouts[0].Islands.Single(island => island.Id == "l").Folder!.Cells).Island.Height);
    }

    /// <summary>
    /// Every component in a list says where a new line of it starts (<see cref="ListWidget.Range"/>):
    /// from one row, so the content is its own floor, and the rows it is drawn at where nothing has
    /// measured it within its most. A new line is automatic up to that most.
    /// </summary>
    [Fact]
    public void EveryComponentInAListSaysWhereANewLineStarts()
    {
        var cards = ListWidgets.All.Where(widget => widget.Kind.HasHeight()).ToList();
        Assert.NotEmpty(cards);
        foreach (var widget in cards)
        {
            var (least, most) = widget.Range;
            Assert.True(least == LineHeight.Fewest, $"{widget.Kind} starts a new line at {least} cells, not at a row: its content is its own floor");
            Assert.True(most >= least && most <= FolderOptions.MostCells, $"{widget.Kind} starts at {least}..{most}");
            Assert.True(widget.Cells <= most, $"{widget.Kind} is drawn at {widget.Cells} rows unmeasured, past the {most} a new line may have");
            // Automatic up to its most where its content varies; else fixed at its rows (§138).
            var start = widget.Grows ? LineHeight.Automatic(most) : LineHeight.Exactly(widget.Cells);
            Assert.Equal(start, widget.Kind.New().Height);
            Assert.Equal(start, LineHeight.For(widget.Kind.New()));
        }
    }

    /// <summary>
    /// The list's own row of an icon and a name, and a divider, have no height: none in the file,
    /// one line where it is read (the divider its hairline), whatever a file or code says. Nor has a sized
    /// line (§432): its size's rows where nothing measures it.
    /// </summary>
    [Fact]
    public void OnlyACardHasAHeight()
    {
        foreach (var kind in ListWidgets.All.Select(widget => widget.Kind).Where(kind => !kind.HasHeight()))
        {
            var line = kind.New();
            line.Height = LineHeight.Exactly(10);
            line.Normalize();
            Assert.Null(line.Height);
            Assert.Equal(LineHeight.Exactly(kind.Sized() ? LineSizes.CellsOf(line) : LineHeight.Row), LineHeight.For(line));
        }
        Assert.Contains(ListWidgets.All, widget => !widget.Kind.HasHeight());
    }

    // --- The list ---------------------------------------------------------------------------------

    /// <summary>
    /// A new line is its content — exactly, and not rounded up to a row. This is the Today card's
    /// complaint as a test: a curve 125 px tall given four rows was 34 px of nothing under a graph.
    /// </summary>
    [Fact]
    public void ANewLineIsItsContentAndNothingMore()
    {
        // §442: rounded up to whole cells, 20 px each at 100 %.
        var plan = Plan([125f, 211f], Sized(ListKind.SoundInput), Sized(ListKind.NotificationsList));
        Assert.Equal(140f, Tall(plan, 0), 1);
        Assert.Equal(220f, Tall(plan, 1), 1);
        // And never less than a line of the list, however little the content says it needs.
        Assert.Equal(40f, Tall(Plan([12f], Sized(ListKind.SoundVolume)), 0), 1);
    }

    /// <summary>
    /// Automatic holds the line at its most: content past it is given the most and the card fills
    /// and summarises; content under it is the content, with no floor but a row.
    /// </summary>
    [Fact]
    public void AutomaticIsTheContentUpToItsMost()
    {
        var list = Sized(ListKind.NotificationsList, LineHeight.Automatic(12));
        Assert.Equal(Rows(6), Tall(Plan([900f], list), 0), 1);
        Assert.Equal(60f, Tall(Plan([60f], list), 0), 1);
        Assert.Equal(200f, Tall(Plan([200f], list), 0), 1);
    }

    /// <summary>Fixed is exactly its rows, whatever the content says, for every component in a list.</summary>
    [Fact]
    public void FixedIsExactlyItsRows()
    {
        foreach (var kind in ListWidgets.All.Select(widget => widget.Kind).Where(kind => kind.HasHeight()))
        {
            foreach (var rows in new[] { 1, 3, 7 })
            {
                var line = Sized(kind, LineHeight.Exactly(rows));
                foreach (var content in new[] { 20f, 159f, 1200f })
                {
                    // The rows held to the kind's limits (ListWidget.Heights).
                    Assert.Equal(Cells(LineHeight.Exactly(rows).Within(kind).Cells), Tall(Plan([content], line), 0), 1);
                }
            }
        }
    }

    /// <summary>
    /// Without measurements every line is what its kind declares, held to its height: a new line's
    /// most holds it, so a picture or a test that measures nothing sees what it always did.
    /// </summary>
    [Fact]
    public void WithoutMeasurementsEveryLineIsWhatItsKindDeclares()
    {
        var kinds = ListWidgets.All.Select(widget => widget.Kind).ToArray();
        var plan = Plan(null, [.. kinds.Select(kind => Sized(kind))]);
        for (var index = 0; index < kinds.Length; index++)
        {
            // A divider is its gap, whatever it declares (FolderFlyout.SeparatorRoom).
            // A heading is its own height, a section heading's (HeadingTile.Height).
            var declared = kinds[index] switch
            {
                ListKind.Divider => FolderFlyout.SeparatorRoom(1f),
                ListKind.Heading => HeadingTile.Height(1f, 1f),
                _ => Cells(kinds[index].ListCellsOf()),
            };
            Assert.Equal(declared, Tall(plan, index), 1);
        }
    }

    /// <summary>
    /// The folder has no height of its own: it is as tall as its tallest list, whatever heights its
    /// lines have, and a line's height changing changes the folder by exactly as much.
    /// </summary>
    [Fact]
    public void TheFolderGrowsToItsLines()
    {
        var short1 = Plan([100f, 80f], Sized(ListKind.Calendar, LineHeight.Automatic(24)), Sized(ListKind.Month, LineHeight.Exactly(4)));
        var tall = Plan([100f, 80f], Sized(ListKind.Calendar, LineHeight.Exactly(16)), Sized(ListKind.Month, LineHeight.Exactly(4)));
        Assert.Equal(short1.Cards[1].bottom, short1.Lists[0].Box.bottom, 1);
        Assert.Equal(tall.Cards[1].bottom, tall.Lists[0].Box.bottom, 1);
        Assert.Equal(Rows(8) - 100f, tall.Lists[0].Height - short1.Lists[0].Height, 1);
        Assert.Equal(Rows(8) - 100f, tall.Height - short1.Height, 1);
    }

    /// <summary>
    /// A switch's line is one row until a person makes it Full (§432), and then the whole rows its name and
    /// what a click does take — never a height given: a height in the file is no height.
    /// </summary>
    [Fact]
    public void ASwitchIsOneLineUntilItIsGivenMore()
    {
        Assert.Equal(40f, Tall(Plan(null, Sized(ListKind.EnergySaver)), 0), 1);
        Assert.Equal(40f, Tall(Plan(null, Sized(ListKind.EnergySaver, LineHeight.Exactly(6))), 0), 1);
        var full = Sized(ListKind.EnergySaver);
        full.Size = LineSize.Full;
        Assert.Equal(Rows(2), Tall(Plan(null, full), 0), 1);
        Assert.Equal(Cells(5), Tall(Plan([90f], full), 0), 1);
    }

}
