// markets: a click searches what the widget tracks in the default browser, with the app's engine
// (2026-10-20: "when i left click on the stock widget it should open the stock in the default search in
// the browser"; docs\widgets.md §263): a share its symbol and the word for a share ("MSFT stock"), a pair its
// two codes ("EUR CHF"), a coin its ticker and currency ("BTC EUR"). A ticker's line does it once the folder
// has gone, as a headline's story opens; the face on the bar there and then — anywhere on a button it is
// alone on, its own room among other faces. A converter's click is still its swap, and a widget with no
// symbol yet does what it did. Nothing reaches the browser or the network.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Markets;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Markets;

public sealed class MarketClickTests
{
    /// <summary>What each kind of symbol is searched for, with the app's engine, and nothing for no symbol.</summary>
    [Fact]
    public void EachKindIsSearchedForAsItsQuote()
    {
        Assert.Equal("MSFT stock", SearchUrls.MarketQuery("msft"));
        Assert.Equal("NESN.SW stock", SearchUrls.MarketQuery("NESN.SW"));
        Assert.Equal("EUR CHF", SearchUrls.MarketQuery("EURCHF=X"));
        Assert.Equal("USD CHF", SearchUrls.MarketQuery("CHF=X"));
        Assert.Equal("BTC EUR", SearchUrls.MarketQuery("BTC-EUR"));
        Assert.Equal("BRK-B stock", SearchUrls.MarketQuery("BRK-B"));
        Assert.Null(SearchUrls.MarketQuery(""));
        Assert.Null(SearchUrls.MarketQuery(null));

        Assert.Equal("https://www.google.com/search?q=AAPL+stock", SearchUrls.Market(new Config(), "AAPL"));
        Assert.Equal("https://www.google.com/search?q=EUR+CHF", SearchUrls.Market(new Config(), "EURCHF=X"));
        Assert.Equal("https://duckduckgo.com/?q=BTC+EUR", SearchUrls.Market(new Config { SearchEngine = SearchEngine.DuckDuckGo }, "BTC-EUR"));
        Assert.Equal("https://example.com/?q=%5EGSPC+stock", SearchUrls.Market(new Config { SearchEngine = SearchEngine.Custom, SearchCustomUrl = "https://example.com/?q={q}" }, "^GSPC"));
        Assert.Null(SearchUrls.Market(new Config(), " "));
    }

    private static Dictionary<string, string> Answers() => new()
    {
        ["MSFT"] = MarketOffscreenTests.Answer("MSFT", "Microsoft Corporation", price: 412.34, percent: 1.24, previous: 407.3),
        ["EURCHF=X"] = MarketOffscreenTests.Answer("EURCHF=X", "EUR/CHF", "CHF", price: 0.9269, percent: -0.0205, previous: 0.927, wander: 0.001),
        ["BTC-EUR"] = MarketOffscreenTests.Answer("BTC-EUR", "Bitcoin EUR", "EUR", price: 67890.12, percent: -0.05, previous: 67924.08, wander: 100),
    };

    /// <summary>
    /// A ticker's line: its click asks the folder to go and forwards (an app's opening, its line's act), and
    /// the dock's Open then searches it, once — a share, a pair and a coin each as its quote. A converter's
    /// click is its own and opens nothing; a line with no symbol yet opens nothing.
    /// </summary>
    [UiFact]
    public void ATickersLineSearchesItOnceTheFolderHasGone()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var store = MarketOffscreenTests.Settled(Answers());
            var opened = new List<string>();
            var cell = new FolderCell { Island = ListKind.Markets.New() };
            var config = new Config { SearchEngine = SearchEngine.Bing };
            foreach (var (symbol, url) in new[]
            {
                ("MSFT", "https://www.bing.com/search?q=MSFT+stock"),
                ("EURCHF=X", "https://www.bing.com/search?q=EUR+CHF"),
                ("BTC-EUR", "https://www.bing.com/search?q=BTC+EUR"),
            })
            {
                using var line = MarketOffscreenTests.Made(gfx, config, store, $"click-{symbol}", new MarketOptions { Symbol = symbol }, answers: false);
                line.Opened = opened.Add;
                var action = line.Click(0);
                Assert.Equal(IslandView.App, action.View);
                Assert.True(FolderFlyout.Forwards(action, FolderFlyout.ActOf(cell)), symbol);
                Assert.Empty(opened);
                line.Open();
                line.Open();
                Assert.Equal([url], opened);
                Assert.True(FolderFlyout.LightsWhole(cell, line), $"{symbol}: the ticker lights as a whole, its click being the line's");
                opened.Clear();
            }

            using var converter = MarketOffscreenTests.Made(gfx, config, store, "click-converter", new MarketOptions { Symbol = "EURCHF=X", Style = MarketStyle.Converter }, answers: false);
            converter.Opened = opened.Add;
            Assert.NotNull(converter.Converter);
            Assert.Equal(IslandView.None, converter.Click(0).View);
            converter.Open();
            Assert.False(FolderFlyout.LightsWhole(cell, converter), "a converter's parts light themselves");

            using var unset = MarketOffscreenTests.Made(gfx, config, MarketOffscreenTests.Waiting(), "click-unset", new MarketOptions(), answers: false);
            unset.Opened = opened.Add;
            Assert.Equal(IslandView.None, unset.Click(0).View);
            unset.Open();
            Assert.Empty(opened);
        });
    }

    /// <summary>The markets preset's folder over <paramref name="store"/>, wearing <paramref name="faces"/> where given, else its markets face showing <paramref name="face"/>.</summary>
    private static FolderIsland Folder(Gfx gfx, MarketStore store, string id, MarketOptions face, params FolderFace[] faces)
    {
        var holder = Karpo.Tests.Forecasts.MarketOffscreenTests.OneLineFolder(new PresetMachine(false, []));
        holder.Id = id;
        holder.Folder!.Face.Market = face.Copy();
        holder.Folder.Cells.Single().Island.Market = face.Copy();
        if (faces.Length > 0)
        {
            holder.Folder.Faces = [.. faces];
        }
        holder.Normalize();
        MarketIsland.Made = store;
        try
        {
            var folder = new FolderIsland($"{ListFormPictures.Owner}/{id}", holder, gfx, new Config { SearchEngine = SearchEngine.DuckDuckGo });
            _ = ((IIsland)folder).Refresh(gfx);
            return folder;
        }
        finally
        {
            MarketIsland.Made = null;
        }
    }

    /// <summary>
    /// On the bar: a button wearing the markets face alone searches what it tracks wherever it is clicked,
    /// and Narrator's Invoke too (its Click), opening no folder; across a bar and down one, its face's own
    /// room too (ClickAt). One with no symbol yet opens its folder, as every folder's click did.
    /// </summary>
    [UiFact]
    public void AMarketsFaceAloneSearchesItOnItsClick()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var opened = new List<string>();
            FolderIsland.MarketOpening = opened.Add;
            try
            {
                var store = MarketOffscreenTests.Settled(Answers());
                using var share = Folder(gfx, store, "face-share", new MarketOptions { Symbol = "MSFT" });
                Assert.Equal(IslandView.None, share.Click(0).View);
                Assert.Equal(["https://duckduckgo.com/?q=MSFT+stock"], opened);
                opened.Clear();

                using var coin = Folder(gfx, store, "face-coin", new MarketOptions { Symbol = "BTC-EUR" });
                foreach (var vertical in new[] { false, true })
                {
                    var style = new IslandStyle(1.5f, vertical, 72, 36, ListFormPictures.LookFor(true));
                    var (width, height) = coin.CellSize(0, style);
                    Assert.True(coin.ClickAt(0, new Vector2(width / 2, height / 2), new Vector2(width, height), style));
                }
                Assert.Equal(["https://duckduckgo.com/?q=BTC+EUR", "https://duckduckgo.com/?q=BTC+EUR"], opened);
                opened.Clear();

                using var unset = Folder(gfx, MarketOffscreenTests.Waiting(), "face-unset", new MarketOptions());
                Assert.Equal(IslandView.Folder, unset.Click(0).View);
                Assert.Empty(opened);
            }
            finally
            {
                FolderIsland.MarketOpening = null;
            }
        });
    }

    /// <summary>
    /// Beside other faces the markets face's own room searches, across a bar and down one, and the rest
    /// of the button — the clock's room, and the click with no point (Narrator's) — opens the folder.
    /// </summary>
    [UiFact]
    public void AMarketsFaceAmongOthersSearchesOnlyInItsOwnRoom()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var opened = new List<string>();
            FolderIsland.MarketOpening = opened.Add;
            try
            {
                var store = MarketOffscreenTests.Settled(Answers());
                var markets = new FolderFace { Kind = FolderFaces.MarketsId, Market = new MarketOptions { Symbol = "EURCHF=X" } };
                using var folder = Folder(gfx, store, "face-two", new MarketOptions { Symbol = "EURCHF=X" }, ClockFace.New(), markets);
                Assert.Equal(2, folder.FacesOnBar.Count);
                Assert.Equal(IslandView.Folder, folder.Click(0).View);
                Assert.Empty(opened);
                foreach (var vertical in new[] { false, true })
                {
                    var style = new IslandStyle(1.5f, vertical, 72, 36, ListFormPictures.LookFor(true));
                    var (width, height) = folder.CellSize(0, style);
                    var boxes = FolderIsland.FaceBoxes(gfx, folder.Options, style, width, height, folder.FacesOnBar, folder);
                    static Vector2 Middle(TerraFX.Interop.DirectX.D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);
                    var size = new Vector2(width, height);
                    Assert.False(folder.ClickAt(0, Middle(boxes[0]), size, style), "the clock's room is the folder's");
                    Assert.Empty(opened);
                    Assert.True(folder.ClickAt(0, Middle(boxes[1]), size, style), "the markets face's own room");
                    Assert.Equal(["https://duckduckgo.com/?q=EUR+CHF"], opened);
                    opened.Clear();
                }
            }
            finally
            {
                FolderIsland.MarketOpening = null;
            }
        });
    }
}
