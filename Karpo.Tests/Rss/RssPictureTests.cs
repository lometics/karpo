// rss: the RSS widget's line drawn (docs\widgets.md §39, §98), over one fixed sample feed (RssSamples)
// and a frozen clock: at every height from one row to eight, at 100 % and 150 %, dark and light, to
// KARPO_PNG_DIR (else %TEMP%\karpo-tests\pictures) as rss-*.png, for looking at. And what it is held
// to while it is drawn:
//
//   * a line draws inside itself at every height;
//   * a click on a line's headline, or on its feed's name (its site's link, §408), is an app opening of that link — which
//     the folder carries out — and a click anywhere else asks for nothing a folder lets through.
//
// The news preset and its face are NewsPresetTests'. Needs Direct2D, so it runs only with
// KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.Rss;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Rss;

public sealed unsafe class RssPictureTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 15, 0, 0);

    private static readonly float[] Scales = [1f, 1.5f];

    private const string ClassName = "karpo.rss.test";

    /// <summary>The states a widget can be in, each on a store of its own.</summary>
    private static IEnumerable<(string Id, Func<RssStore> Store, bool Feed)> States()
    {
        yield return ("news", () => RssSamples.Settled(Now), true);
        yield return ("no-feed", RssSamples.Waiting, false);
        yield return ("loading", RssSamples.Waiting, true);
        yield return ("failed", RssSamples.Broken, true);
    }

    private static void Frozen(Action<Gfx> body) => Offscreen.OnStaThread(gfx =>
    {
        Clock.Now = () => Now;
        body(gfx);
    });

    // --- As a line of a list ------------------------------------------------------------

    /// <summary>
    /// A click on a line's headline is an app opening — the story's link, which the folder carries
    /// out, closing as the browser comes — and the link is that headline's, or the site's for the
    /// feed's name; a click beside them asks for nothing (FolderFlyout.Forwards). Nothing that is not
    /// an http or https link is ever handed on.
    /// </summary>
    [UiFact]
    public void AClickOnAHeadlineOpensItsStoryAndNothingElse()
    {
        Frozen(gfx =>
        {
            using var island = RssSamples.Made(gfx, ListKind.Rss.New(), RssSamples.Settled(Now));
            var opened = new List<string>();
            island.Opened = opened.Add;
            var scale = 1.5f;
            var look = TestLooks.LookFor(true);
            var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(scale), ListFormPictures.Rows(6, scale, 1f));
            var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
            var hit = new HashSet<int>();
            // At the headlines' start and scrolled to their end (ListScroll.cs).
            foreach (var scroll in new[] { 0, float.MaxValue })
            {
                island.ListScroll = scroll;
                for (var y = box.top; y < box.bottom; y += 3)
                {
                    for (var x = box.left; x < box.right; x += 3)
                    {
                        hit.Add(island.TargetOn(new Vector2(x, y), box, style, WidgetSurface.List));
                    }
                }
            }
            island.ListScroll = 0;
            Assert.True(hit.Contains(1), "no point of the line opens the newest headline");
            foreach (var target in hit)
            {
                var action = island.Click(target);
                if (target == 0)
                {
                    Assert.False(FolderFlyout.Forwards(action, CellAct.Action), "a click beside the headlines is let through the folder");
                    continue;
                }
                Assert.True(FolderFlyout.Forwards(action, CellAct.Action), $"target {target} is not opened");
                opened.Clear();
                island.Open(target);
                Assert.Equal([target == RssIsland.SiteTarget ? island.Site! : island.Headlines[target - 1].Link], opened);
            }
            Assert.Contains(RssIsland.SiteTarget, hit);
            // Nowhere else opens anything.
            opened.Clear();
            Assert.Equal(IslandView.None, island.Click(0).View);
            island.Open(0);
            Assert.Empty(opened);
        });
    }

    /// <summary>Every height from one row to eight, side by side, at 100 and 150 %, dark and light.</summary>
    [UiFact]
    public void EveryLineHeightIsPictured()
    {
        Frozen(gfx =>
        {
            foreach (var (id, store, feed) in States())
            {
                using var island = RssSamples.Made(gfx, ListKind.Rss.New(), store(), feed);
                foreach (var scale in Scales)
                {
                    foreach (var dark in new[] { true, false })
                    {
                        LineSheet(gfx, island, scale, dark, $"rss-list-{id}-{scale * 100:0}-{(dark ? "dark" : "light")}.png");
                    }
                }
            }
        });
    }

    private static void LineSheet(Gfx gfx, RssIsland island, float scale, bool dark, string file)
    {
        var look = ListFormPictures.LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var width = FolderFlyout.ListWidth(scale);
        var pad = PopupPaint.Round(12 * scale);
        var label = PopupPaint.Round(10 * scale);
        var labelLine = TilePaint.LineHeight(gfx, label);
        var heights = Enumerable.Range(1, 8).Select(rows => ListFormPictures.Rows(rows, scale, 1f)).ToList();
        var columns = 4;
        var columnWidth = width + pad;
        var rowHeights = Enumerable.Range(0, 2).Select(row => heights.Skip(row * columns).Take(columns).Max() + labelLine + pad).ToList();
        var sheetWidth = pad + (columns * columnWidth);
        var sheetHeight = pad + rowHeights.Sum();
        Offscreen.Bitmap(sheetWidth, sheetHeight, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(theme.Base));
            var y = pad;
            for (var row = 0; row < 2; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var rows = (row * columns) + column + 1;
                    var x = pad + (column * columnWidth);
                    var height = heights[rows - 1];
                    PopupPaint.Text(dc, gfx, $"{rows}", label, Weight.Regular, theme.TextSecondary, x, y);
                    PopupPaint.Fill(dc, PopupPaint.Rect(x, y + labelLine, width, height), PopupPaint.Round(4 * scale), PopupPaint.Ink(theme.CardBackground));
                    Offscreen.Shifted(dc, x, y + labelLine, () => island.PaintOn(dc, PopupPaint.Rect(0, 0, width, height), style, WidgetSurface.List));
                }
                y += rowHeights[row];
            }
        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
    }

    /// <summary>At every height, scale and text size, a line draws something and all of it inside itself.</summary>
    [UiFact]
    public void EveryLineStaysInItsLine()
    {
        var wrong = new List<string>();
        Frozen(gfx =>
        {
            foreach (var (id, store, feed) in States())
            {
                using var island = RssSamples.Made(gfx, ListKind.Rss.New(), store(), feed);
                foreach (var scale in new[] { 1f, 1.5f, 2f })
                {
                    foreach (var textScale in ListFormPictures.TextScales)
                    {
                        var look = ListFormPictures.LookFor(true, textScale);
                        var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                        var width = FolderFlyout.ListWidth(scale);
                        for (var rows = 1; rows <= ListWidgets.Of(ListKind.Rss).Range.Most; rows++)
                        {
                            var height = ListFormPictures.Rows(rows, scale, textScale);
                            var margin = PopupPaint.Round(24 * scale);
                            (float Left, float Top, float Right, float Bottom) ink = default;
                            Offscreen.Bitmap(width + (2 * margin), height + (2 * margin),
                                dc => Offscreen.Shifted(dc, margin, margin, () => island.PaintOn(dc, PopupPaint.Rect(0, 0, width, height), style, WidgetSurface.List)),
                                bitmap => ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width + (2 * margin)), (uint)MathF.Ceiling(height + (2 * margin))));
                            var at = $"rss {id} at {rows} rows, {scale * 100:0} %, text {textScale * 100:0} %";
                            if (ink.Right <= ink.Left)
                            {
                                wrong.Add($"{at}: drew nothing");
                            }
                            else if (ink.Left < margin - 0.5f || ink.Top < margin - 0.5f || ink.Right > margin + width + 0.5f || ink.Bottom > margin + height + 0.5f)
                            {
                                wrong.Add($"{at}: ink outside its {width:0}x{height:0} line");
                            }
                        }
                    }
                }
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong.Take(60)));
    }

    /// <summary>A line's height follows what it has to show: taller with more headlines, and no more than eight of them.</summary>
    [UiFact]
    public void ALineIsTallerWithMoreHeadlinesAndStopsAtEight()
    {
        Frozen(gfx =>
        {
            var look = TestLooks.LookFor(dark: true);
            var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
            using var none = RssSamples.Made(gfx, ListKind.Rss.New(), RssSamples.Waiting());
            using var news = RssSamples.Made(gfx, ListKind.Rss.New(), RssSamples.Settled(Now));
            Assert.True(news.ListHeight(360, style) > none.ListHeight(360, style));
            Assert.Equal(8, news.Headlines.Count);
        });
    }

}
