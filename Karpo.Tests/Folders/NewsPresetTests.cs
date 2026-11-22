// The news is a preset (docs\widgets.md §98): what a click on the RSS entry gave — the headlines
// panel: the feed's name with a refresh, the newest stories with their headlines, where they are from,
// how old they are and their pictures, the new ones dotted, and a link to the feed's site — is a
// folder holding the feed's line, whose whole widget is all of it, under the news face: the glyph and
// the newest headline the entry was, its dot while something new has come. Opening the folder is
// seeing what is new, as opening the panel was. Every store here is RssSamples': nothing is fetched.
//
// Pictured beside the panel it replaces (rendered before it went: rss-panel-<state>-*.png and the
// entry's rss-bar-*.png): news-preset-<dark|light>.png at 150 %, and the face on the bar,
// news-face-<dark|light>.png.

using Karpo.Dock;
using Karpo.Dock.Rss;
using Karpo.Tests.Islands;
using Karpo.Tests.Rss;
using static Karpo.Tests.Folders.ListFormPictures;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class NewsPresetTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 15, 0, 0);

    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>The news preset's folder, as the add list makes it, its line on the sample feed where <paramref name="feed"/>.</summary>
    private static BarItem Holder(bool feed = true, bool headline = true)
    {
        var holder = Presets.Folder(Presets.Find(Presets.NewsId)!, Machine);
        holder.Id = "news";
        if (feed)
        {
            holder.Folder!.Cells.Single().Island.Rss = new RssOptions { Address = RssSamples.Address, Title = "Example News" };
        }
        if (!headline)
        {
            holder.Folder!.Face.Set(FolderFaces.News.Option(NewsFace.Headline)!, false);
        }
        holder.Normalize();
        return holder;
    }

    /// <summary>The preset's folder over <paramref name="store"/>.</summary>
    private static FolderIsland Made(Gfx gfx, RssStore store, bool feed = true, bool headline = true, string? key = null)
    {
        var holder = Holder(feed, headline);
        RssIsland.Made = store;
        try
        {
            var folder = new FolderIsland(key ?? $"{Owner}/{holder.Id}", holder, gfx, new Config());
            _ = ((IIsland)folder).Refresh(gfx);
            return folder;
        }
        finally
        {
            RssIsland.Made = null;
        }
    }

    private static void Frozen(Action<Gfx> body) => Offscreen.OnStaThread(gfx =>
    {
        Clock.Now = () => Now;
        body(gfx);
    });

    private static IslandStyle Bar(int height = 48, uint dpi = 96, bool vertical = false, float text = 1f, bool dark = true)
    {
        var config = new Config { Height = height };
        return new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), LookFor(dark, text));
    }

    // --- The preset ------------------------------------------------------------------------------------

    /// <summary>The feed's line alone at its own height, under the news face, whose one option is the entry's: the headline.</summary>
    [Fact]
    public void ThePresetIsTheFeedsLineUnderTheHeadline()
    {
        var preset = Presets.Find(Presets.NewsId)!;
        Assert.Equal(FolderFaces.NewsId, preset.Face!.Kind);
        Assert.Equal(["rss"], preset.Items.Select(item => item.Form));
        Assert.Null(preset.Items[0].Height);

        var folder = Presets.Folder(preset, Machine).Folder!;
        Assert.Same(FolderFaces.News, folder.Face.Info);
        Assert.Equal([NewsFace.Headline], folder.Face.Info.Options.Select(option => option.Key));
        Assert.True(NewsFace.ShowsHeadline(folder.Face));
        Assert.Equal(SetupField.Feed, WidgetSetup.Needs(folder.Cells.Single().Island));
        Assert.Null(BarEntries.Find("rss"));
        Assert.Null(typeof(RssOptions).GetProperty("ShowHeadline"));
    }

    /// <summary>
    /// The feed's name opens its site (2026-10-29: "the open xyz button should not be on the bottom but should
    /// be when i click on the title of the feed"): no button at the line's foot, at the start of the headlines or
    /// scrolled to their end; the header is a target of its own that opens the site, lights under the pointer and
    /// says where it goes in its tooltip. The line is as tall as its header and its headlines, nothing under them.
    /// </summary>
    [UiFact]
    public void TheFeedsNameOpensItsSite()
    {
        Frozen(gfx =>
        {
            using var folder = Made(gfx, RssSamples.Settled(Now));
            folder.Opened(gfx);
            var line = Assert.IsType<RssIsland>(Assert.Single(folder.Tiles).Island);
            var opened = new List<string>();
            line.Opened = opened.Add;
            var look = LookFor(true);
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
            var width = FolderFlyout.ListWidth(1.5f);
            var box = PopupPaint.Rect(0, 0, width, LineRows.Px(4, style));
            foreach (var offset in new[] { 0f, 40f, float.MaxValue })
            {
                line.ListScroll = offset;
                var laid = line.Scrolled(box, style, WidgetSurface.List);
                Assert.True(laid.Scrolls);
                Assert.Equal(0, TilePaint.Height(laid.FootBand));
                Assert.DoesNotContain(line.LinePieces(laid.End, style), piece => piece.Name == "site");
                var head = line.LinePieces(box, style).Single(piece => piece.Name == "head").Box;
                var middle = new System.Numerics.Vector2((head.left + head.right) / 2, (head.top + head.bottom) / 2);
                Assert.Equal(RssIsland.SiteTarget, line.TargetOn(middle, box, style, WidgetSurface.List));
                Assert.Equal(IslandView.App, line.Click(RssIsland.SiteTarget).View);
                opened.Clear();
                line.Open(RssIsland.SiteTarget);
                Assert.Equal(["https://news.example.com/"], opened);
                line.CardHover(middle, box, style, WidgetSurface.List);
                Assert.Equal(RssIsland.SiteTarget, line.Lighted.Hot);
                Assert.Equal(S.DockRssOpenSite("news.example.com"), line.TipOn(middle, box, style, WidgetSurface.List)?.Text);
            }
            line.ListScroll = 0;
            var headline = Each(gfx, line, style);
            Assert.Equal(LineRows.Px(1, style) + (line.Headlines.Count * headline), line.ScrollHeight(width, box.bottom, style), 0.5f);
            folder.Closed();
        });
    }

    /// <summary>
    /// A read that fails while the headlines read before are shown says so as the mail and the agenda do, with the
    /// stale mark at the end of the header's row and why in its tooltip — where the footer said it
    /// beside the site's button before (§408).
    /// </summary>
    [UiFact]
    public void AFailedReadMarksTheHeader()
    {
        Frozen(gfx =>
        {
            var store = RssSamples.Settled(Now, fails: true);
            using var line = RssSamples.Made(gfx, ListKind.Rss.New(), store);
            var look = LookFor(true);
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
            var width = FolderFlyout.ListWidth(1.5f);
            var box = PopupPaint.Rect(0, 0, width, line.ListHeight(width, style));
            Assert.DoesNotContain(line.LinePieces(box, style), piece => piece.Name == StaleMark.Piece);
            Assert.True(line.Ask());
            Assert.True(Patience.Until(() => store.Read(line).Problem is not null), "the failed read never came back");
            line.MinuteChanged(gfx);
            Assert.True(line.Headlines.Count > 0);
            var pieces = line.LinePieces(box, style);
            var mark = Assert.Single(pieces, piece => piece.Name == StaleMark.Piece).Box;
            var head = pieces.Single(piece => piece.Name == "head").Box;
            Assert.True(mark.top >= box.top && mark.bottom <= LineRows.Px(1, style) && mark.left >= head.right, $"the mark at {mark.left:0},{mark.top:0}");
            var tip = line.TipOn(new System.Numerics.Vector2((mark.left + mark.right) / 2, (mark.top + mark.bottom) / 2), box, style, WidgetSurface.List)?.Text;
            Assert.Contains(store.Read(line).Problem!, tip);
        });
    }

    /// <summary>A headline's height in <paramref name="line"/>'s whole layout: the distance between two titles' tops.</summary>
    private static float Each(Gfx gfx, RssIsland line, in TileStyle style)
    {
        var width = FolderFlyout.ListWidth(style.Scale);
        var pieces = line.LinePieces(PopupPaint.Rect(0, 0, width, LineRows.Px(30, style)), style);
        return pieces.Single(piece => piece.Name == "title 1").Box.top - pieces.Single(piece => piece.Name == "title 0").Box.top;
    }

    /// <summary>
    /// The whole widget is the panel: the feed's name over the headlines, each with its
    /// picture on the left where the others show their icons (2026-10-29: "the rss widget should show the images
    /// of the articles on the left side where the others show the icons"), the feed's mark there where it gives
    /// none; their words at the text edge, past the picture, on the whole line's width.
    /// </summary>
    [UiFact]
    public void TheWholeLineIsThePanel()
    {
        Frozen(gfx =>
        {
            using var folder = Made(gfx, RssSamples.Settled(Now));
            folder.Opened(gfx);
            var line = Assert.IsType<RssIsland>(Assert.Single(folder.Tiles).Island);
            Assert.Equal(5, RssSamples.Pictured(line));
            var look = LookFor(true);
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
            var width = FolderFlyout.ListWidth(1.5f);
            var box = PopupPaint.Rect(0, 0, width, line.ListHeight(width, style));
            var pieces = line.LinePieces(box, style);
            Assert.DoesNotContain(pieces, piece => piece.Name == "site");
            Assert.Equal("Example News", pieces.Single(piece => piece.Name == "head").Text);
            Assert.Contains(pieces, piece => piece.Name == "byline 0");
            var form = ListForm.Of(gfx, style);
            var feature = LineGlyph.Feature(style);
            for (var index = 0; index < line.Headlines.Count; index++)
            {
                var title = pieces.Single(piece => piece.Name == $"title {index}").Box;
                var byline = pieces.Single(piece => piece.Name == $"byline {index}").Box;
                // Their words at the text edge, as far as the trailing gutter, picture or none.
                Assert.Equal(form.TextLeft, title.left, 1);
                Assert.Equal(form.TextLeft, byline.left, 1);
                Assert.True(title.right <= width - form.Inset + 0.5f, $"title {index} runs past the gutter to {title.right:0}");
                // The picture, or the feed's mark, in the leading column on the gutter, on the headline's middle.
                var name = line.Headlines[index].Thumbnail.Length > 0 ? $"picture {index}" : $"mark {index}";
                var lead = pieces.Single(piece => piece.Name == name).Box;
                var middle = (title.top + byline.bottom) / 2;
                Assert.Equal(middle, (lead.top + lead.bottom) / 2, 1.5f);
                Assert.True(lead.left >= form.Inset - 0.5f && lead.right <= form.TextLeft - style.Px(LineSpace.GapDip) + 0.5f,
                    $"{name} at {lead.left:0}..{lead.right:0}, not in the leading column {form.Inset:0}..{form.TextLeft:0}");
                if (name.StartsWith("picture", StringComparison.Ordinal))
                {
                    Assert.Equal(feature, lead.right - lead.left, 0.5f);
                    Assert.Equal(feature, lead.bottom - lead.top, 0.5f);
                }
            }
            // No picture at the line's end any more: a long headline with one runs to the trailing gutter.
            Assert.True(pieces.Single(piece => piece.Name == "title 0").Box.right > width - form.Inset - style.Px(16), "title 0 keeps room at the line's end");
            Assert.Equal(form.TextLeft, pieces.Single(piece => piece.Name == "head").Box.left, 1);
            folder.Closed();
        });
    }

    /// <summary>A headline lights under the pointer and pressed, as a Windows 11 list item does; the click lets the press go.</summary>
    [UiFact]
    public void AHeadlineLightsPressedUntilItsClick()
    {
        Frozen(gfx =>
        {
            using var line = RssSamples.Made(gfx, ListKind.Rss.New(), RssSamples.Settled(Now));
            line.Opened = _ => { };
            var look = LookFor(true);
            var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
            var width = FolderFlyout.ListWidth(1f);
            var box = PopupPaint.Rect(0, 0, width, line.ListHeight(width, style));
            var title = line.LinePieces(box, style).Single(piece => piece.Name == "title 1").Box;
            var at = new System.Numerics.Vector2((title.left + title.right) / 2, (title.top + title.bottom) / 2);
            line.CardHover(at, box, style, WidgetSurface.List);
            Assert.Equal((2, 0), line.Lighted);
            Assert.False(line.CardPress(at, box, style, WidgetSurface.List));
            Assert.Equal((2, 2), line.Lighted);
            Assert.Equal(IslandView.App, line.Click(2).View);
            Assert.Equal((2, 0), line.Lighted);
            // A press beside the headlines is no row's.
            line.CardPress(at with { Y = box.bottom + 100 }, box, style, WidgetSurface.List);
            Assert.Equal(0, line.Lighted.Pressed);
        });
    }

    /// <summary>
    /// Opening the folder is seeing what is new, as opening the panel was: the face's dot goes at once;
    /// the line keeps its dots on what was new for that showing, and has none the next time.
    /// </summary>
    [UiFact]
    public void OpeningTheFolderSeesWhatIsNew()
    {
        Frozen(gfx =>
        {
            using var folder = Made(gfx, RssSamples.Settled(Now, fresh: 2));
            var line = folder.Feed!;
            Assert.Equal(2, line.Read.NewCount);
            Paint(folder, Bar());
            Assert.Contains("|True|", folder.FaceShown, StringComparison.Ordinal);

            folder.Opened(gfx);
            Assert.Equal(0, line.Read.NewCount);
            Assert.False(((IIsland)folder).Refresh(gfx));
            Paint(folder, Bar());
            Assert.Contains("|False|", folder.FaceShown, StringComparison.Ordinal);
            var look = LookFor(true);
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
            var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), LineRows.Px(8, style));
            Assert.Equal(2, line.LinePieces(box, style).Count(piece => piece.Name.StartsWith("new ", StringComparison.Ordinal)));
            folder.Closed();

            folder.Opened(gfx);
            Assert.Empty(line.LinePieces(box, style).Where(piece => piece.Name.StartsWith("new ", StringComparison.Ordinal)));
            folder.Closed();
        });
    }

    /// <summary>"Refresh" on its right click reads the feed now, and is left out while that read is on its way (§451).</summary>
    [UiFact]
    public void RefreshInItsMenuReadsTheFeed()
    {
        Frozen(gfx =>
        {
            var store = RssSamples.Settled(Now);
            using var line = RssSamples.Made(gfx, ListKind.Rss.New(), store);
            var (items, acts) = Karpo.Dock.Dock.FolderMenuRows(line.Settings, line);
            var refresh = Karpo.Dock.Dock.ActRowOf(acts, items.Single(item => item.Text == S.DockWidgetMenuRefresh).Id)!;
            Karpo.Dock.Dock.RunAct(refresh, line, (_, _) => { });
            // The sample's site never answers: the read stays on its way, and the menu has no Refresh meanwhile.
            Assert.True(store.Read(line).Fetching);
            Assert.DoesNotContain(Karpo.Dock.Dock.FolderMenuRows(line.Settings, line).Items, item => item.Text == S.DockWidgetMenuRefresh);
        });
    }

    // --- The face --------------------------------------------------------------------------------------

    /// <summary>
    /// The face shows the folder's first feed line: its newest headline, and to a screen reader the feed
    /// and that headline; its right click is the site (the line's own menu reads it again, §451).
    /// </summary>
    [UiFact]
    public void TheFaceReadsTheFoldersLine()
    {
        Frozen(gfx =>
        {
            using var folder = Made(gfx, RssSamples.Settled(Now));
            var line = Assert.IsType<RssIsland>(Assert.Single(folder.Tiles).Island);
            Assert.Same(line, folder.Feed);
            Assert.Same(line, folder.FaceWidget);
            Assert.Equal($"Example News\n{line.Headlines[0].Title}", folder.Tooltip(0));
            Paint(folder, Bar());
            Assert.EndsWith(line.Headlines[0].Title, folder.FaceShown, StringComparison.Ordinal);
            Assert.Equal([WidgetAct.OpenWebsite], TheDock.ActsFor(FolderFaces.News));
        });
    }

    /// <summary>
    /// The face is as wide as what it says after its glyph, up to the headline's room, past which it is
    /// cut (docs\widgets.md §244): a long headline the whole room, "No feed" or "Getting the news" less;
    /// the headline switched off is the glyph and the button's ends (§275); down a bar, the glyph alone.
    /// </summary>
    [UiFact]
    public void TheFaceIsAsWideAsWhatItSaysUpToItsRoom()
    {
        Frozen(gfx =>
        {
            using var news = Made(gfx, RssSamples.Settled(Now), key: "a");
            using var none = Made(gfx, RssSamples.Waiting(), feed: false, key: "b");
            using var waiting = Made(gfx, RssSamples.Waiting(), key: "c");
            using var glyph = Made(gfx, RssSamples.Settled(Now), headline: false, key: "d");
            foreach (var dpi in new uint[] { 96, 120, 144, 192 })
            {
                var style = Bar(48, dpi);
                var fit = new Fit(style);
                var room = (2 * MathF.Max(fit.Edge, fit.Px(6))) + Karpo.Dock.Rss.RssIsland.FaceLength(gfx, style, headline: true).Along;
                var width = news.CellSize(0, style).Width;
                Assert.True(none.CellSize(0, style).Width < width, $"\"no feed\" is as wide as a headline at {dpi} DPI");
                Assert.True(waiting.CellSize(0, style).Width <= width, $"waiting is wider than a headline at {dpi} DPI");
                Assert.True(width <= MathF.Ceiling(room) + 1, $"the headline runs past its room at {dpi} DPI");
                Assert.True(width > 3 * style.Cell, $"the headline's room is under three cells at {dpi} DPI");
                Assert.Equal(FaceWidthTests.GlyphButton(style), glyph.CellSize(0, style).Width, 0.5f);
                Assert.Equal(FaceWidthTests.GlyphButton(Bar(48, dpi, vertical: true)), news.CellSize(0, Bar(48, dpi, vertical: true)).Height, 0.5f);
            }
        });
    }

    private static void Paint(FolderIsland folder, in IslandStyle style)
    {
        var (width, height) = folder.CellSize(0, style);
        var copy = style;
        Offscreen.Bitmap(width, height, dc => folder.PaintCell(dc, 0, copy, width, height), _ => { });
    }

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>
    /// The preset as it opens: the sample with its pictures, getting the news, no feed yet, a feed that
    /// cannot be read, and at 225 % text — at 150 %, to stand beside rss-panel-*.png.
    /// </summary>
    [UiFact]
    public void NewsPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Frozen(gfx =>
            {
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, RssStore store, bool feed = true, float text = 1f, bool fails = false)
                    {
                        var look = LookFor(dark, text);
                        var folder = Made(gfx, store, feed, key: $"{Owner}/news-{panels.Count}");
                        if (fails && folder.Feed is { } line)
                        {
                            Assert.True(Patience.Until(() => store.Read(line).Problem is not null), "the failed read never came back");
                            _ = ((IIsland)folder).Refresh(gfx);
                        }
                        folders.Add(folder);
                        folder.Opened(gfx);
                        if (folder.Feed is { } shown)
                        {
                            RssSamples.Pictured(shown);
                        }
                        panels.Add((caption, look, folder, Plan(folder, look, 1.5f).Plan));
                    }

                    Add("The news", RssSamples.Settled(Now));
                    Add("Getting the news", RssSamples.Waiting());
                    Add("No feed", RssSamples.Waiting(), feed: false);
                    Add("Cannot be read", RssSamples.Broken(), fails: true);
                    Add("The news: 225 % text", RssSamples.Settled(Now), text: 2.25f);
                    Sheet(gfx, $"news-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>
    /// The face on a bar of 40 and 48 DIP and at 150 %: the newest headline with the new dot, the glyph
    /// alone, no feed, getting the news — news-face-&lt;dark|light&gt;.png, beside rss-bar-*.png.
    /// </summary>
    [UiFact]
    public void NewsFacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Frozen(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var islands = new List<FolderIsland>
                {
                    Made(gfx, RssSamples.Settled(Now), key: "a"),
                    Made(gfx, RssSamples.Settled(Now), headline: false, key: "b"),
                    Made(gfx, RssSamples.Waiting(), feed: false, key: "c"),
                    Made(gfx, RssSamples.Waiting(), key: "d"),
                    Made(gfx, RssSamples.Settled(Now, fresh: 0), key: "e"),
                };
                var styles = new[] { Bar(40, dark: dark), Bar(48, dark: dark), Bar(48, 144, dark: dark) };
                try
                {
                    const float gap = 12;
                    var width = gap + styles.Max(style => islands.Sum(island => island.CellSize(0, style).Width + gap));
                    var height = gap + styles.Sum(style => islands[0].CellSize(0, style).Height + gap);
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                        var y = gap;
                        foreach (var style in styles)
                        {
                            var x = gap;
                            foreach (var island in islands)
                            {
                                var (w, h) = island.CellSize(0, style);
                                PopupPaint.Fill(dc, PopupPaint.Rect(x, y, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                var at = (x, y);
                                Offscreen.Shifted(dc, at.x, at.y, () => island.PaintCell(dc, 0, style, w, h));
                                x += w + gap;
                            }
                            y += islands[0].CellSize(0, style).Height + gap;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"news-face-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    foreach (var island in islands)
                    {
                        island.Dispose();
                    }
                }
            });
        }
    }
}
