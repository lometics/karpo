// §603: the tides and the sun's arc as widgets of fixed content (docs\widgets.md §603). The person, 2026-11-09: "split all
// of them". The tides now — "Tides", the height now, which way the sea goes and the next turn with its height and how
// soon — and the tide graph — the day's curve with its highs' and lows' times — each say the same at Compact and at Full;
// so does the sun's arc, its picture over the sunrise, the daylight and the sunset. A box taller than the size makes a
// line say nothing more, and its height is its size's whatever the answer.
//
// Drawn on the gutter tests' stand-ins (ListGutterTests.StoodIn, Before): a frozen clock, the saved tides (Brest) and
// the sun at Zürich.
//
// Pictures (KARPO_PNG_DIR): tides-sun-split-<dark|light>.png — the tides now, the tide graph and the sun's arc, each a
// folder of its Compact over its Full, at 150 %; tides-sun-split-text225-dark.png at 225 % text.
// Needs Direct2D: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed unsafe class TidesSunSplitTests
{
    private static readonly LineSize[] Sizes = [LineSize.Compact, LineSize.Full];

    /// <summary>The tides' and the sun's split kinds.</summary>
    private static readonly ListKind[] Kinds = [ListKind.Tides, ListKind.TideGraph, ListKind.SunArc];

    /// <summary>A line of <paramref name="kind"/> with its place set, in <paramref name="size"/>.</summary>
    private static CellItem Made(ListKind kind, LineSize size)
    {
        var line = ListGutterTests.Made(kind);
        line.Size = size;
        line.Normalize();
        return line;
    }

    /// <summary>What a line says drawn into <paramref name="width"/> × <paramref name="height"/>: every word and figure, one by one.</summary>
    private static HashSet<string> Said(CellIsland line, float width, float height, in TileStyle style)
    {
        var said = new List<string>();
        var box = PopupPaint.Rect(0, 0, width, height);
        var drawn = style;
        PaintWatch.Said = (text, _) => said.Add(text);
        try
        {
            Bitmap(width, height, dc => line.PaintOn(dc, box, drawn, WidgetSurface.List), _ => { });
        }
        finally
        {
            PaintWatch.Said = null;
        }
        // One word or figure a token: a caption joined with " · " says what one row says at its end. Segoe Fluent Icons'
        // glyphs (the private use area) are pictures, not words.
        return [.. said.SelectMany(text => text.Split([" · ", " ", " ", " "], StringSplitOptions.RemoveEmptyEntries))
            .Where(word => !word.All(c => c is >= '' and <= ''))];
    }

    /// <summary>Each kind at Compact and at Full says the same words and figures, and drawn twice as tall says nothing more.</summary>
    [UiFact]
    public void EachSizeSaysTheSameAndATallerLineNoMore()
    {
        var failures = new List<string>();
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            foreach (var kind in Kinds)
            {
                var said = new Dictionary<LineSize, HashSet<string>>();
                foreach (var size in Sizes)
                {
                    var folder = ListFormPictures.Folder(gfx, ListGutterTests.Before, Made(kind, size));
                    try
                    {
                        var look = ListFormPictures.LookFor(dark: true);
                        var (_, plan) = ListFormPictures.Plan(folder, look, 1f);
                        var card = plan.Cards[0];
                        var (width, height) = (card.right - card.left, card.bottom - card.top);
                        var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
                        var line = (CellIsland)folder.Tiles[0].Island;
                        var at = Said(line, width, height, style);
                        var taller = Said(line, width, height * 2, style);
                        if (!at.SetEquals(taller))
                        {
                            failures.Add($"{kind} {size}: twice as tall it says {string.Join(", ", taller.Except(at))} more and {string.Join(", ", at.Except(taller))} less");
                        }
                        if (at.Count == 0)
                        {
                            failures.Add($"{kind} {size}: says nothing");
                        }
                        said[size] = at;
                    }
                    finally
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
                var onlyFull = said[LineSize.Full].Except(said[LineSize.Compact]).ToList();
                var onlyCompact = said[LineSize.Compact].Except(said[LineSize.Full]).ToList();
                if (onlyFull.Count > 0 || onlyCompact.Count > 0)
                {
                    failures.Add($"{kind}: Full alone says {string.Join(", ", onlyFull)}; Compact alone {string.Join(", ", onlyCompact)}");
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// The tides' height is its size's whatever the answer — the saved tides, none to be had, or a place off the sea —, at
    /// 100 % and 225 % text; the sun's arc its size's at any place; and Compact is the smaller.
    /// </summary>
    [UiFact]
    public void ItsHeightIsItsSizesWhateverTheAnswer()
    {
        var failures = new List<string>();
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            foreach (var textScale in ListFormPictures.TextScales)
            {
                var look = ListFormPictures.LookFor(dark: true, textScale);
                var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                var width = FolderFlyout.ListWidth(1.5f);
                foreach (var kind in Kinds)
                {
                    var heights = new List<float>();
                    foreach (var size in Sizes)
                    {
                        var answers = new List<float>();
                        foreach (var answer in new[] { "saved", "failed", "inland" })
                        {
                            using var line = (CellIsland)Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, answer), Made(kind, size), kind, gfx, new Config())!;
                            switch (line)
                            {
                                case TidesTile tides:
                                    tides.Imperial = false;
                                    tides.Source = answer switch
                                    {
                                        "saved" => SavedTides.Source(SavedTides.Cache()),
                                        "inland" => SavedTides.Source(SavedTides.Cache(inland: true)),
                                        _ => new TideSource { Read = () => (null, "offline", false) },
                                    };
                                    break;
                                case SunTile sun:
                                    sun.Stand = answer switch { "saved" => (47.3769, 8.5417), "inland" => (69.6492, 18.9553), _ => (-33.8688, 151.2093) };
                                    break;
                            }
                            answers.Add(((IListContent)line).ListHeight(width, style));
                        }
                        if (answers.Distinct().Count() != 1)
                        {
                            failures.Add($"{kind} {size} at {textScale * 100:0} % text: {string.Join(", ", answers.Select(px => $"{px:0}"))} px by its answer");
                        }
                        heights.Add(answers[0]);
                    }
                    if (!(heights[0] < heights[1]))
                    {
                        failures.Add($"{kind} at {textScale * 100:0} % text: Compact {heights[0]:0} px, Full {heights[1]:0}");
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>The tides now's detail at the end of Compact's one row, under the name at Full: the same words.</summary>
    [UiFact]
    public void TheTidesNowSaysItsNextTurnAtTheRowsEndOrUnderItsName()
    {
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            var look = ListFormPictures.LookFor(dark: true);
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
            var width = FolderFlyout.ListWidth(1.5f);
            var laid = new Dictionary<LineSize, IReadOnlyList<LinePiece>>();
            foreach (var size in Sizes)
            {
                using var tides = (TidesTile)Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, "tides"), Made(ListKind.Tides, size), ListKind.Tides, gfx, new Config())!;
                tides.Source = SavedTides.Source(SavedTides.Cache());
                tides.Imperial = false;
                laid[size] = tides.RowPieces(PopupPaint.Rect(0, 0, width, tides.ListHeight(width, style)), style);
            }
            var compact = laid[LineSize.Compact];
            var full = laid[LineSize.Full];
            var name = compact.Single(piece => piece.Name == "name");
            var next = compact.Single(piece => piece.Name == "next");
            Assert.True(next.Box.left > name.Box.right, "on one row the next turn stands after the name");
            Assert.Equal(name.Box.top, next.Box.top, 1);
            var under = full.Single(piece => piece.Name == "next");
            Assert.True(under.Box.top >= full.Single(piece => piece.Name == "name").Box.bottom - 0.5f, "at Full the next turn stands under the name");
            Assert.Equal(next.Text, under.Text);
            Assert.Equal(compact.Single(piece => piece.Name == "height").Text, full.Single(piece => piece.Name == "height").Text);
        });
    }

    /// <summary>The pictures: each kind a folder of its Compact over its Full (see the top of the file).</summary>
    [UiFact]
    public void TidesSunSplitPictures()
    {
        (string Caption, ListKind Kind)[] kinds =
        [
            (S.CoreWidgetTidesTitle, ListKind.Tides),
            (S.CoreWidgetTideGraphTitle, ListKind.TideGraph),
            (S.CoreWidgetSunArcTitle, ListKind.SunArc),
        ];
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            foreach (var (dark, textScale) in new[] { (true, 1f), (false, 1f), (true, 2.25f) })
            {
                var look = ListFormPictures.LookFor(dark, textScale);
                var folders = kinds.Select(each => (each.Caption, Folder: ListFormPictures.Folder(gfx, ListGutterTests.Before, Made(each.Kind, LineSize.Compact), Made(each.Kind, LineSize.Full)))).ToList();
                try
                {
                    var file = $"tides-sun-split{(textScale > 1 ? "-text225" : "")}-{(dark ? "dark" : "light")}.png";
                    ListFormPictures.Sheet(gfx, file, 1.5f,
                        [.. folders.Select(each => ($"{each.Caption}: Compact over Full, {textScale * 100:0} % text", look, each.Folder, ListFormPictures.Plan(each.Folder, look, 1.5f).Plan))]);
                }
                finally
                {
                    foreach (var (_, folder) in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            }
        });
    }
}
