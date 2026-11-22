// rss: a feed wears its site's own icon (docs\widgets.md §306) — the site's favicon, fetched and kept as
// a pinned site's is (FaviconStore), else the icon the feed names for itself — and an icon chosen in its
// menu wears in place of it; the news glyph where there is neither. No test reaches the network or the
// person's favicons.json: the sites here are the test's own.

using Karpo.Dock;
using Karpo.Dock.Favicons;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.Rss;
using Karpo.Tests.Edit;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Rss;

public sealed class RssIconTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 15, 0, 0);

    private const string Favicon = "https://news.example.com/favicon.ico";

    // --- What a feed names for itself -------------------------------------------------------------------

    /// <summary>
    /// Atom's icon (square by its spec), and an RSS image that says it is square, read against where
    /// the feed is or its channel's link; an RSS image of no size (88 by 31 by the spec), a banner's size, Atom's logo and
    /// a link Karpo does not open are no icon. rss.json keeps it.
    /// </summary>
    [Fact]
    public void AFeedNamesItsSquareIconAndNoBanner()
    {
        var from = new Uri("https://feeds.example.com/news/feed.xml");
        static string Rss(string image) =>
            $"<rss version=\"2.0\"><channel><title>T</title><link>https://example.com/</link>{image}<item><title>A</title></item></channel></rss>";
        static string Atom(string head) =>
            $"<feed xmlns=\"http://www.w3.org/2005/Atom\"><title>T</title>{head}<entry><title>A</title><id>1</id></entry></feed>";

        Assert.Equal("https://feeds.example.com/icons/news.png", Feeds.Parse(Atom("<icon>/icons/news.png</icon><logo>/logo.png</logo>"), from).Icon);
        Assert.Equal("", Feeds.Parse(Atom("<logo>/logo.png</logo>"), from).Icon);
        Assert.Equal("https://example.com/i.png", Feeds.Parse(Rss("<image><url>https://example.com/i.png</url><width>144</width><height>144</height></image>"), from).Icon);
        // A relative one against the channel's link, as its items' are.
        Assert.Equal("https://example.com/i.png", Feeds.Parse(Rss("<image><url>i.png</url><width>32</width><height>32</height></image>"), from).Icon);
        Assert.Equal("", Feeds.Parse(Rss("<image><url>https://example.com/i.png</url></image>"), from).Icon);
        Assert.Equal("", Feeds.Parse(Rss("<image><url>https://example.com/i.png</url><width>144</width><height>96</height></image>"), from).Icon);
        Assert.Equal("", Feeds.Parse(Rss("<image><url>javascript:alert(1)</url><width>16</width><height>16</height></image>"), from).Icon);
        Assert.Equal("", Feeds.Parse(Rss(""), from).Icon);

        var feed = Feeds.Parse(Atom("<icon>https://example.com/i.png</icon>"), from);
        Assert.Equal("https://example.com/i.png", RssSaved.Of(feed).Read().Icon);
        Assert.Equal("", new RssSaved { Title = "T", Icon = "file:///C:/x.png" }.Read().Icon);
    }

    // --- Where the icon comes from ----------------------------------------------------------------------

    /// <summary>
    /// The site the feed names is asked first, its /favicon.ico (the page answering nothing here), and
    /// nothing more once it answers; the picture is drawn at the size asked for.
    /// </summary>
    [Fact]
    public async Task TheSitesOwnFaviconIsAskedFirst()
    {
        var asked = new List<string>();
        var sites = RssSamples.Sites(new Dictionary<string, byte[]> { [Favicon] = RssSamples.Bitmap(200, 32, 32) }, asked);
        var store = RssSamples.Settled(Now, sites: sites);
        var owner = new object();
        store.Use(owner, new RssOptions { Address = RssSamples.Address });

        Assert.Null(store.Icon(owner, 24));
        await (sites.Working("https://news.example.com") ?? Task.CompletedTask);
        Assert.Equal((24, 24), store.Icon(owner, 24) is { } icon ? (icon.Width, icon.Height) : default);
        Assert.Equal([Favicon], asked);
        Assert.Equal(["https://news.example.com/"], store.Sites());
    }

    /// <summary>
    /// A feed that names no site wears the icon of the host it is read from; where that host has none,
    /// the icon the feed names for itself, asked after the site's own.
    /// </summary>
    [Fact]
    public async Task WithoutTheSitesOwnTheFeedsIconIsWorn()
    {
        const string own = "https://cdn.example.net/news-icon.png";
        var asked = new List<string>();
        var sites = RssSamples.Sites(new Dictionary<string, byte[]> { [own] = RssSamples.Bitmap(40, 64, 64) }, asked);
        var held = RssSamples.Feed(Now) with { Site = "", Icon = own };
        var store = RssSamples.Settled(Now, sites: sites, held: held);
        var owner = new object();
        store.Use(owner, new RssOptions { Address = RssSamples.Address });

        Assert.Null(store.Icon(owner, 32));
        await (sites.Working("https://news.example.com") ?? Task.CompletedTask);
        Assert.NotNull(store.Icon(owner, 32));
        Assert.Equal([Favicon, own], asked);
    }

    /// <summary>
    /// A site's icon is kept on disk as a pinned site's is: a store started again draws it at once and
    /// asks the site nothing; a site with none draws nothing, which leaves the news glyph.
    /// </summary>
    [Fact]
    public async Task TheIconIsKeptAndASiteWithNoneHasNone()
    {
        string? kept = null;
        var disk = new FaviconDisk { Read = () => kept, Write = text => kept = text };
        var first = RssSamples.Sites(new Dictionary<string, byte[]> { [Favicon] = RssSamples.Bitmap(200, 32, 32) }, disk: disk);
        var store = RssSamples.Settled(Now, sites: first);
        var owner = new object();
        store.Use(owner, new RssOptions { Address = RssSamples.Address });
        _ = store.Icon(owner, 16);
        await (first.Working("https://news.example.com") ?? Task.CompletedTask);
        Assert.Contains("https://news.example.com", kept);

        var asked = new List<string>();
        var again = RssSamples.Sites(new Dictionary<string, byte[]>(), asked, disk);
        var later = RssSamples.Settled(Now, sites: again);
        later.Use(owner, new RssOptions { Address = RssSamples.Address });
        Assert.NotNull(later.Icon(owner, 16));
        Assert.Null(again.Working("https://news.example.com"));
        Assert.Empty(asked);

        var none = RssSamples.Sites(new Dictionary<string, byte[]>());
        var bare = RssSamples.Settled(Now, sites: none);
        bare.Use(owner, new RssOptions { Address = RssSamples.Address });
        Assert.Null(bare.Icon(owner, 16));
        await (none.Working("https://news.example.com") ?? Task.CompletedTask);
        Assert.Null(bare.Icon(owner, 16));
    }

    // --- What is drawn ----------------------------------------------------------------------------------

    private static void Frozen(Action<Gfx> body) => Offscreen.OnStaThread(gfx =>
    {
        Clock.Now = () => Now;
        body(gfx);
    });

    /// <summary>
    /// The mark a feed is drawn with: the news glyph with no site icon, the site's once it has come, and
    /// an icon chosen over either; the line's and the face's alike. Pictures (150 %, dark and light):
    /// rss-icons-&lt;look&gt;.png, each at one row, three and six, and the face.
    /// </summary>
    [UiFact]
    public void TheChosenIconWinsOverTheSitesWhichWinsOverTheNewsGlyph()
    {
        Frozen(gfx =>
        {
            var sites = RssSamples.Sites(new Dictionary<string, byte[]> { [Favicon] = RssSamples.Bitmap(15, 32, 32) });
            using var plain = RssSamples.Made(gfx, ListKind.Rss.New(), RssSamples.Settled(Now));
            using var site = RssSamples.Made(gfx, ListKind.Rss.New(), RssSamples.Settled(Now, sites: sites));
            var item = ListKind.Rss.New();
            using var chosen = RssSamples.Made(gfx, item, RssSamples.Settled(Now, sites: sites));
            item.Rss!.Glyph = "\uE734";
            chosen.Configure(item, new Config());

            Assert.Equal((RssIsland.NewsGlyph, false), (plain.Mark(24).Glyph, plain.Mark(24).Picture is not null));
            Assert.True(Patience.Until(() => site.Mark(24).Picture is not null), "the site's icon never came");
            Assert.Equal("", site.Mark(24).Glyph);
            Assert.True(Patience.Until(() => chosen.Store.Icon(chosen, 24) is not null));
            Assert.Equal(("\uE734", false), (chosen.Mark(24).Glyph, chosen.Mark(24).Picture is not null));

            foreach (var dark in new[] { true, false })
            {
                Sheet(gfx, [("No site icon", plain), ("Its site's", site), ("Chosen", chosen)], 1.5f, dark, $"rss-icons-{(dark ? "dark" : "light")}.png");
            }
        });
    }

    /// <summary>
    /// The feed's icon on a real menu (§306): a row among its words, its site's mark at its end, its
    /// level the one picker — the field, the cross ringed while none is chosen, the gallery in one grid.
    /// Pictures: rss-icons-picker-&lt;look&gt;.png at 150 %.
    /// </summary>
    [UiFact]
    public void TheFeedsIconIsTheOnePicker()
    {
        WindowShots.OnHiddenDesktop("rss-icons", (gfx, instance) =>
        {
            const float scale = 1.5f;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark);
                var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                var feed = ListKind.Rss.New();
                feed.Rss = new RssOptions { Address = RssSamples.Address, Title = "Example News" };
                List<OptionPick> picks = [];
                var items = OptionMenu.Section(OptionRows.Of(feed), picks);
                var icon = items.Single(item => OptionMenu.PickOf(picks, item.Id)?.Key == OptionPickers.RssIcon);
                Assert.Equal(new FlyoutIcon.Glyph(OptionGlyphs.Feed), icon.Value!.Icon);
                Assert.DoesNotContain(icon.Children!, child => child.Header);
                var cross = icon.Children!.First(Flyout.IsTile);
                Assert.Equal((OptionGlyphs.Clear, true), (((FlyoutTile)cross.Icon!).Code, cross.Checked == true));
                using var menu = new Flyout(gfx, instance, 0) { Configures = true };
                menu.Show(gfx, look, scale, items, FlyoutLayout.List, new POINT { x = work.right / 4, y = work.bottom }, work, Edge.Bottom, null);
                WindowShots.Settle();
                for (var step = 0; step < 40 && menu.HotId != icon.Id; step++)
                {
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                }
                Assert.Equal(icon.Id, menu.HotId);
                _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                WindowShots.Settle();
                Assert.Equal(1, menu.InlineDepth);
                Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], $"rss-icons-picker-{(dark ? "dark" : "light")}.png", dark ? ((byte)0, (byte)0, (byte)0) : ((byte)243, (byte)243, (byte)243)));
                menu.Hide();
            }
        });
    }

    private static unsafe void Sheet(Gfx gfx, (string Caption, RssIsland Island)[] feeds, float scale, bool dark, string file)
    {
        var look = ListFormPictures.LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var bar = new IslandStyle(scale, false, Layout.CellPx((uint)(96 * scale), new Config()), Layout.IconPx((uint)(96 * scale), new Config()), look);
        var width = FolderFlyout.ListWidth(scale);
        var pad = PopupPaint.Round(12 * scale);
        var label = PopupPaint.Round(10 * scale);
        var labelLine = TilePaint.LineHeight(gfx, label);
        int[] rows = [1, 3, 6];
        var heights = rows.Select(count => ListFormPictures.Rows(count, scale, 1f)).ToList();
        var face = RssIsland.FaceLength(gfx, bar, headline: true).Along;
        var column = MathF.Max(width, face) + pad;
        var sheetWidth = pad + (feeds.Length * column);
        var sheetHeight = pad + labelLine + bar.Cell + pad + heights.Sum(height => height + pad);
        Offscreen.Bitmap(sheetWidth, sheetHeight, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(theme.Base));
            for (var at = 0; at < feeds.Length; at++)
            {
                var (caption, island) = feeds[at];
                var x = pad + (at * column);
                var y = pad;
                PopupPaint.Text(dc, gfx, caption, label, Weight.Regular, theme.TextSecondary, x, y);
                y += labelLine;
                RssIsland.PaintFace(dc, gfx, bar, PopupPaint.Rect(x, y, face, bar.Cell), island, headline: true);
                y += bar.Cell + pad;
                foreach (var height in heights)
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(x, y, width, height), PopupPaint.Round(4 * scale), PopupPaint.Ink(theme.CardBackground));
                    Offscreen.Shifted(dc, x, y, () => island.PaintOn(dc, PopupPaint.Rect(0, 0, width, height), style, WidgetSurface.List));
                    y += height + pad;
                }
            }
        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
    }
}
