// Words drawn cut short say themselves whole in a tooltip (the person, 2026-11-03: "when hovering over text that is
// partially hidden (like when i search for vanguard ftse all world etfs for example) i would like the tooltip to show the
// text after hovering for a while over it"). One rule, the kit's (Karpo.Dock\CutWords.cs): a line DirectWrite trimmed at
// its room. A painted line is heard where it is drawn, with its box; a menu's row asks as it is laid out.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed unsafe class CutWordsTests
{
    private const string Long = "Vanguard FTSE All-World UCITS ETF USD Accumulating (VWCE) on Xetra";

    [Fact]
    public void ATooltipSaysTheWholeWordsFirstAndItsOwnAfter()
    {
        Assert.Null(CutWords.Said(null, null));
        Assert.Equal("Cut (Ctrl+X)", CutWords.Said(null, "Cut (Ctrl+X)"));
        Assert.Equal(Long, CutWords.Said(Long, null));
        Assert.Equal($"{Long}\nBack", CutWords.Said(Long, "Back"));
        Assert.Equal(Long, CutWords.Said(Long, Long));
    }

    [Fact]
    public void TheCutLineUnderThePointIsFoundByItsBox()
    {
        CutRun[] runs = [new("first", new D2D_RECT_F(0, 0, 50, 10)), new("second", new D2D_RECT_F(40, 0, 90, 10))];
        Assert.Equal("first", CutWords.At(runs, new Vector2(10, 5))?.Text);
        // Overlapping: the last drawn, on top.
        Assert.Equal("second", CutWords.At(runs, new Vector2(45, 5))?.Text);
        Assert.Null(CutWords.At(runs, new Vector2(95, 5)));
        Assert.Null(CutWords.At(runs, new Vector2(10, 12)));
        Assert.Null(CutWords.At(null, new Vector2(10, 5)));
    }

    /// <summary>
    /// A line painted cut at its room is heard with its whole words and its box — through either of the kit's places that
    /// draw one (PopupPaint, TextLine) —; a line that fits, or one with no room to be cut at, is not.
    /// </summary>
    [UiFact]
    public void APaintedLineCutShortIsHeardWithItsBoxAndOneThatFitsIsNot()
    {
        WindowShots.OnHiddenDesktop("cut-words-painted", (gfx, _) =>
        {
            var ink = Gfx.Ink(1, 1, 1, 1);
            IReadOnlyList<CutRun> runs = [];
            using (var surface = gfx.Draw(400, 100, dc =>
            {
                runs = CutWords.While(() =>
                {
                    PopupPaint.Text(dc, gfx, Long, 14, Weight.Regular, ink, 10, 10, 120);
                    PopupPaint.Text(dc, gfx, "Short", 14, Weight.Regular, ink, 10, 40, 120);
                    PopupPaint.Text(dc, gfx, Long, 14, Weight.Regular, ink, 10, 70);
                    var line = TextLine.Lay(gfx, Long, 14, Weight.Regular, 100);
                    var brush = Gfx.Brush(dc, ink);
                    try
                    {
                        line.Draw(dc, brush, ink, 200, 10);
                    }
                    finally
                    {
                        brush->Release();
                        line.Release();
                    }
                });
            }))
            {
            }
            Assert.Equal(2, runs.Count);
            Assert.All(runs, run => Assert.Equal(Long, run.Text));
            Assert.Equal(Long, CutWords.At(runs, new Vector2(30, 18))?.Text);
            Assert.True(runs[0].Box.right <= 10 + 120 + 1, $"the cut line's box ends at its room: {runs[0].Box.right}");
            Assert.Equal(Long, CutWords.At(runs, new Vector2(220, 18))?.Text);
            Assert.Null(CutWords.At(runs, new Vector2(20, 48)));
            Assert.Null(CutWords.At(runs, new Vector2(300, 78)));
            // Nobody listening: nothing is heard, and nothing is kept.
            Assert.Empty(CutWords.While(() => { }));

            Assert.True(gfx.Cuts(Long, 14, Weight.Regular, 120));
            Assert.False(gfx.Cuts("Short", 14, Weight.Regular, 120));
        });
    }

    /// <summary>
    /// A menu's row whose words were cut — a search's result, the markets' "Vanguard FTSE All-World…" — says them whole in
    /// its tooltip; a row that fits says nothing; an icon button keeps its own, its name and its shortcut.
    /// </summary>
    [UiFact]
    public void AMenusRowCutShortSaysItsWordsWholeInItsTooltip()
    {
        WindowShots.OnHiddenDesktop("cut-words-menu", (gfx, instance) =>
        {
            using var menu = new Flyout(gfx, instance, 0);
            var work = new RECT { right = 1920, bottom = 1040 };
            menu.Show(gfx, AddListPictures.LookFor(true), 1f, [.. TheDock.WidgetButtons(), new(7, Long), new(8, "Short")], FlyoutLayout.List, new POINT { x = 960, y = 1040 }, work, Edge.Bottom, null);
            WindowShots.Settle(30);
            string? TipOf(uint id) => menu.TipOfRow(id);
            Assert.Equal(Long, TipOf(7));
            Assert.Null(TipOf(8));
            var cut = TheDock.WidgetButtons().Single(row => row.Keys == MenuChord.Cut);
            Assert.Equal(Flyout.TipText(cut), TipOf(cut.Id));
            menu.Hide();
        });
    }

    private const string Vanguard = "Vanguard FTSE All-World UCITS ETF USD Accumulation";

    /// <summary>
    /// The person's own case: a markets line for Vanguard's all-world fund in a folder's list, its company cut at the
    /// list's width, is heard with the company whole, and the point over it finds it (FolderFlyout.TipOver asks the same).
    /// </summary>
    [UiFact]
    public void AMarketsLinesCompanyCutShortIsHeardWhole()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var store = MarketOffscreenTests.Settled(new() { ["VWCE.DE"] = MarketOffscreenTests.Answer("VWCE.DE", Vanguard, currency: "EUR", price: 141.2, percent: 0.4, previous: 140.6) });
            using var line = MarketOffscreenTests.Made(gfx, new Config(), store, "s", MarketOffscreenTests.Held("VWCE.DE"), answers: false);
            var look = MarketOffscreenTests.LookFor(true);
            var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
            var width = FolderFlyout.ListWidth(1f);
            var height = LineRows.Px(LineSizes.CellsOf(ListWidgets.Of(ListKind.Markets), LineSize.Full), style);
            IReadOnlyList<CutRun> runs = [];
            Offscreen.Bitmap(width, height, dc => runs = CutWords.While(() => line.PaintOn(dc, PopupPaint.Rect(0, 0, width, height), style, WidgetSurface.List)), _ => { });
            var company = runs.Where(run => run.Text.Contains("Vanguard", StringComparison.Ordinal)).ToList();
            Assert.True(company.Count == 1, $"heard: {string.Join(" | ", runs.Select(run => run.Text))}");
            var box = company[0].Box;
            Assert.True(box.right <= width + 1, $"the company's box stays in the line: {box.right} of {width}");
            Assert.Equal(company[0].Text, CutWords.At(runs, new Vector2((box.left + box.right) / 2, (box.top + box.bottom) / 2))?.Text);
            Assert.StartsWith(Vanguard, company[0].Text, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// The bar's half: a face painted whole remembers what it drew cut, by its cell, in the cell's px (CellIsland.CutAt, what
    /// Dock.TooltipFor says over it); painted again with the room for all of it, it forgets it.
    /// </summary>
    [UiFact]
    public void ABarFaceRemembersWhatItDrewCutByItsCell()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var config = new Config { Height = 48 };
            var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), MarketOffscreenTests.LookFor(true));
            using var face = new WordsFace(gfx, Vanguard, length: 80);
            _ = face.Measure(style);
            var size = face.CellSize(0, style);
            _ = face.Surface(0, style);
            // Over the line, drawn from 4 px down.
            var middle = new Vector2(size.Width / 2, 10);
            Assert.Equal(Vanguard, face.CutAt(0, middle)?.Text);
            Assert.Null(face.CutAt(0, new Vector2(size.Width / 2, size.Height - 1)));
            Assert.Null(face.CutAt(1, middle));

            using var roomy = new WordsFace(gfx, "VWCE", length: 80);
            _ = roomy.Measure(style);
            _ = roomy.Surface(0, style);
            Assert.Null(roomy.CutAt(0, middle));
        });
    }

    /// <summary>A bar face of one cell <paramref name="length"/> px long that says <paramref name="words"/> on one line across it.</summary>
    private sealed class WordsFace(Gfx gfx, string words, float length)
        : CellIsland("test/words", MarketOffscreenTests.Widget("w", null), gfx)
    {
        public override int IntervalMs => 0;

        protected override int Count => 1;

        // A stand-in has nothing to set up: its own drawing, never "Set up in Settings".
        internal override SetupNeed? Unset => null;

        public override void Configure(WidgetItem island, Config config)
        {
        }

        public override string? Tooltip(int index) => null;

        public override IslandAction Click(int index) => default;

        protected override bool Sample(bool everything) => false;

        protected override float Length(int index, in IslandStyle style) => length;

        internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
            FaceDrawings.Put(dc, Gfx, words, 14, Gfx.Ink(1, 1, 1, 1), new D2D_RECT_F(0, 0, width, height), 4);
    }
}
