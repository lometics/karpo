// A note's rows laid out (Karpo.Dock\Notes\NoteLayout.cs, docs\widgets.md §669): one hanging indent for
// every block — a wrapped item, task, number, quote or heading goes on under its words, not under its
// marker; a nested item's marker stands at its parent's words; a numbered list's markers end
// together; a quote's words a step in per quote; code in from its plate — and every place in the text
// found again where its caret is drawn. Needs DirectWrite, so it runs only with KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Tests.Folders;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Islands;

public sealed unsafe class NoteLayoutTests
{
    private const string Long = "and its words run on long enough to wrap more than once in a narrow note";

    private static void Laid(string note, float width, Action<NoteLayout> body, float textScale = 1) =>
        OnStaThread(gfx =>
        {
            var look = ListFormPictures.LookFor(dark: false, textScale);
            var style = FolderFlyout.StyleFor(1, look, FlyoutTheme.For(look));
            using var layout = NoteLayout.Lay(gfx, note, Markdown.Lines(note), width, style);
            body(layout);
        });

    private static float X(NoteLayout layout, int at) => layout.CaretAt(at).X;

    /// <summary>Where row <paramref name="index"/>'s second wrapped line starts, in the note.</summary>
    private static int SecondLine(NoteLayout layout, int index)
    {
        var line = layout.Rows[index].Line;
        var first = layout.CaretAt(line.Content).Top;
        for (var at = line.Content; at <= line.End; at++)
        {
            if (layout.CaretAt(at).Top > first + 1)
            {
                return at;
            }
        }
        Assert.Fail($"row {index} does not wrap");
        return -1;
    }

    [UiFact]
    public void AWrappedLineGoesOnUnderItsWords()
    {
        foreach (var marks in new[] { "- ", "* ", "- [ ] ", "- [x] ", "1. ", "10) ", "> ", "> - ", "> 2. ", "# ", "### ", "> > " })
        {
            Laid(marks + Long, 180, layout =>
            {
                var row = layout.Rows[0];
                Assert.True(row.Origin.X > 0, $"\"{marks}\": the words stand after the marks");
                Assert.True(X(layout, row.Line.Start) < row.Origin.X, $"\"{marks}\": the marks before the words");
                Assert.Equal(row.Origin.X, X(layout, row.Line.Content), 0.5f);
                // Every wrapped line after the first starts in the words' column, not under the marker.
                var second = SecondLine(layout, 0);
                Assert.Equal(row.Origin.X, X(layout, second), 0.5f);
                Assert.Equal(row.Origin.X, X(layout, layout.LineEdge(row.Line.End, end: false)), 0.5f);
            });
        }
    }

    [UiFact]
    public void AParagraphNeverStandsIn() =>
        Laid(Long + "\n  two spaces, no list", 180, layout =>
        {
            Assert.All(layout.Rows, row => Assert.Equal(0, row.Origin.X));
            Assert.Equal(0, X(layout, SecondLine(layout, 0)), 0.5f);
        });

    [UiFact]
    public void ANestedMarkerStandsAtItsParentsWords()
    {
        const string note = "- parent\n  - child\n    - grandchild\n- [ ] task\n  - [ ] child task\n1. one\n   - child of a number\n  and a line that goes on with the number";
        Laid(note, 400, layout =>
        {
            var rows = layout.Rows;
            int Marker(int index) => rows[index].Line.Start + note[rows[index].Line.Start..].TakeWhile(c => c == ' ').Count();
            foreach (var (child, parent) in new[] { (1, 0), (2, 1), (4, 3), (6, 5) })
            {
                Assert.Equal(rows[parent].Origin.X, X(layout, Marker(child)), 0.5f);
                Assert.True(rows[child].Origin.X > rows[parent].Origin.X, $"row {child} stands in from row {parent}");
            }
            // A depth's words are one column, whichever parent: a bullet at the slot's least.
            Assert.Equal(layout.Slot, rows[0].Origin.X, 0.5f);
            Assert.Equal(rows[0].Origin.X, X(layout, Marker(5) + "1. ".Length), 0.5f);
            // A paragraph's line indented under the number goes on in its words' column.
            Assert.Equal(rows[5].Origin.X, rows[7].Origin.X, 0.5f);
        });
    }

    [UiFact]
    public void ANumberedListsMarkersEndTogether()
    {
        const string note = "8. eight\n9. nine\n10. ten\n11. eleven\n\nA paragraph between.\n\n1. one\n2. two";
        Laid(note, 400, layout =>
        {
            var rows = layout.Rows;
            int Dot(int index) => rows[index].Line.Start + rows[index].Line.Marker.Length;
            foreach (var index in new[] { 1, 2, 3 })
            {
                Assert.Equal(rows[0].Origin.X, rows[index].Origin.X, 0.5f);
                Assert.Equal(X(layout, Dot(0)), X(layout, Dot(index)), 0.5f);
            }
            // "9." right-aligned with "10.": its first digit further in.
            Assert.True(X(layout, rows[1].Line.Start) > X(layout, rows[2].Line.Start) + 1);
            // A list of its own after a paragraph is as wide as its own numbers.
            Assert.Equal(rows[7].Origin.X, rows[8].Origin.X, 0.5f);
            Assert.True(rows[7].Origin.X < rows[0].Origin.X, "1. and 2. need less room than 10. and 11.");
            Assert.Equal(X(layout, Dot(7)), X(layout, Dot(8)), 0.5f);
        });
    }

    [UiFact]
    public void AQuoteAndCodeStandIn()
    {
        const string note = "> " + Long + "\n> > deeper\n> ---\n```\nsome code " + Long + "\n```";
        Laid(note, 200, layout =>
        {
            var rows = layout.Rows;
            Assert.Equal(layout.QuoteStep, rows[0].Origin.X, 0.5f);
            Assert.Equal(layout.QuoteStep, X(layout, SecondLine(layout, 0)), 0.5f);
            // The > after the bar's room, and a second quote's a step further.
            Assert.Equal(layout.BarRoom, X(layout, rows[0].Line.Start), 0.5f);
            Assert.Equal(2 * layout.QuoteStep, rows[1].Origin.X, 0.5f);
            Assert.Equal(layout.BarRoom + layout.QuoteStep, X(layout, rows[1].Line.Start + 2), 0.5f);
            // A rule in a quote stands in the quote.
            Assert.Equal(layout.QuoteStep, rows[2].Origin.X, 0.5f);
            // Code: in from its plate's edge, every wrapped line too.
            var code = rows[4];
            Assert.Equal(MdKind.Code, code.Line.Kind);
            Assert.Equal(8, code.Origin.X, 0.5f);
            Assert.Equal(8, X(layout, SecondLine(layout, 4)), 0.5f);
            Assert.Equal(8, rows[3].Origin.X, 0.5f);
        });
    }

    /// <summary>A to-do's ticked box has its check mark: since §87 its glyph was an empty string, an accent square with nothing on it.</summary>
    [UiFact]
    public void ATickedBoxHasItsCheckMark() =>
        OnStaThread(gfx =>
        {
            var look = ListFormPictures.LookFor(dark: false);
            var style = FolderFlyout.StyleFor(1, look, FlyoutTheme.For(look));
            var light = 0;
            Bitmap(40, 40, dc => NoteLayout.CheckBox(dc, gfx, new D2D_RECT_F(10, 10, 30, 30), ticked: true, hot: false, QuickTheme.For(look), style), bitmap =>
            {
                IWICBitmapLock* locked = null;
                try
                {
                    var all = new WICRect { X = 0, Y = 0, Width = 40, Height = 40 };
                    ComUtil.Check(bitmap->Lock(&all, (uint)WICBitmapLockFlags.WICBitmapLockRead, &locked));
                    uint stride = 0;
                    ComUtil.Check(locked->GetStride(&stride));
                    uint size = 0;
                    byte* data = null;
                    ComUtil.Check(locked->GetDataPointer(&size, &data));
                    for (var y = 12u; y < 28; y++)
                    {
                        for (var x = 12u; x < 28; x++)
                        {
                            // BGRA: the accent (0078D4) has little green, the mark on it is white.
                            light += data[(y * stride) + (x * 4) + 1] > 200 ? 1 : 0;
                        }
                    }
                }
                finally
                {
                    ComUtil.Release(ref locked);
                }
            });
            Assert.True(light > 5, $"{light} px of the check mark");
        });

    /// <summary>The editor's mapping holds with the indents: every place's caret, clicked, is that place again; Home and End stay in the row.</summary>
    [UiFact]
    public void EveryPlaceIsFoundWhereItsCaretIs()
    {
        var note = NotebookLinesTests.Showcase.ReplaceLineEndings("\n");
        foreach (var textScale in new[] { 1f, 2.25f })
        {
            foreach (var width in new[] { 160f, 420f })
            {
                Laid(note, width, layout =>
                {
                    for (var at = 0; at <= note.Length; at++)
                    {
                        var (x, top, tall) = layout.CaretAt(at);
                        Assert.True(tall > 0);
                        Assert.Equal(at, layout.PositionAt(new Vector2(x, top + (tall / 2))));
                        var row = layout.Rows[layout.RowOf(at)];
                        Assert.InRange(layout.LineEdge(at, end: false), row.Line.Start, row.Line.End);
                        Assert.InRange(layout.LineEdge(at, end: true), row.Line.Start, row.Line.End);
                    }
                }, textScale);
            }
        }
    }
}
