// The app's one search engine (Config.SearchEngine): what each engine searches with, how a query is
// encoded into one, what a custom address may be, the weather's search, and the web search widget,
// which is gone (2026-10-11).

using System.Text.Json.Nodes;
using Karpo.Tests.Core;

namespace Karpo.Tests.Searching;

public sealed class SearchUrlTests
{
    [Theory]
    [InlineData(SearchEngine.Google, "https://www.google.com/search?q=hello+world")]
    [InlineData(SearchEngine.Bing, "https://www.bing.com/search?q=hello+world")]
    [InlineData(SearchEngine.DuckDuckGo, "https://duckduckgo.com/?q=hello+world")]
    [InlineData(SearchEngine.Ecosia, "https://www.ecosia.org/search?q=hello+world")]
    [InlineData(SearchEngine.Startpage, "https://www.startpage.com/sp/search?query=hello+world")]
    [InlineData(SearchEngine.Brave, "https://search.brave.com/search?q=hello+world")]
    public void EveryEngineHasItsSearchAddress(SearchEngine engine, string expected) =>
        Assert.Equal(expected, SearchUrls.Search(engine, null, "hello world"));

    [Fact]
    public void AnEmptyQuerySearchesForNothing()
    {
        Assert.Null(SearchUrls.Search(SearchEngine.Google, null, "   "));
        Assert.Null(SearchUrls.Search(new Config(), ""));
    }

    [Theory]
    // The characters a query string may not hold, and the ones engines read as their own.
    [InlineData("c# & f#", "c%23+%26+f%23")]
    [InlineData("a+b", "a%2Bb")]
    [InlineData("100% sure?", "100%25+sure%3F")]
    [InlineData("\"quoted\"", "%22quoted%22")]
    [InlineData("café münchen", "caf%C3%A9+m%C3%BCnchen")]
    [InlineData("日本語", "%E6%97%A5%E6%9C%AC%E8%AA%9E")]
    [InlineData("a/b?c=d#e", "a%2Fb%3Fc%3Dd%23e")]
    public void QueriesAreEncoded(string query, string expected) =>
        Assert.Equal($"https://www.google.com/search?q={expected}", SearchUrls.Search(SearchEngine.Google, null, query));

    [Fact]
    public void ACustomAddressTakesTheQueryWhereverItStands() =>
        Assert.Equal("https://example.com/red+shoes/page?also=red+shoes",
            SearchUrls.Search(SearchEngine.Custom, "https://example.com/{q}/page?also={q}", "red shoes"));

    [Fact]
    public void ACustomAddressWithoutThePlaceholderSearchesForNothing()
    {
        Assert.Null(SearchUrls.Search(SearchEngine.Custom, "https://example.com/search", "anything"));
        Assert.Equal("Put {q} where the search words go, as in https://example.com/search?q={q}.", SearchUrls.Problem("https://example.com/search"));
    }

    [Fact]
    public void ACustomAddressHasToBeAWebAddress()
    {
        Assert.Null(SearchUrls.Search(SearchEngine.Custom, "ftp://example.com/?q={q}", "x"));
        Assert.Equal("The address has to start with https:// or http://.", SearchUrls.Problem("example.com?q={q}"));
        // Nothing typed yet is not a mistake.
        Assert.Null(SearchUrls.Problem("   "));
        Assert.Null(SearchUrls.Problem("http://localhost:8080/s?q={q}"));
    }

    // --- The app's engine ----------------------------------------------------------------------------

    /// <summary>Google until somebody chooses (2026-10-16): first in the list, and not written to the file.</summary>
    [Fact]
    public void ANewConfigSearchesWithGoogle()
    {
        var config = new Config();
        Assert.Null(config.SearchEngine);
        Assert.Equal(SearchEngine.Google, config.SearchEngineOrDefault);
        Assert.Equal(SearchEngine.Google, SearchUrls.All[0]);
        Assert.Equal("https://www.google.com/search?q=cats", SearchUrls.Search(config, "cats"));
        Assert.DoesNotContain("searchEngine", config.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>Bing was the default before Google; a file where somebody chose it keeps it, an unset one takes Google.</summary>
    [Fact]
    public void AFileThatChoseBingKeepsIt()
    {
        var chosen = Configs.Repaired("""{ "searchEngine": "bing", "layouts": [] }""");
        Assert.Equal(SearchEngine.Bing, chosen.SearchEngineOrDefault);
        Assert.Equal("bing", JsonNode.Parse(chosen.ToJson())!["searchEngine"]!.GetValue<string>());

        var unset = Configs.Repaired("""{ "layouts": [] }""");
        Assert.Null(unset.SearchEngine);
        Assert.Equal(SearchEngine.Google, unset.SearchEngineOrDefault);
    }

    [Fact]
    public void TheEngineAndItsAddressAreKeptInTheFile()
    {
        var config = Configs.With(BarKind.Start.New());
        config.SearchEngine = SearchEngine.Custom;
        config.SearchCustomUrl = "https://example.com/?q={q}";
        var read = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(SearchEngine.Custom, read.SearchEngine);
        Assert.Equal("https://example.com/?q={q}", read.SearchCustomUrl);
        Assert.Equal("https://example.com/?q=cats", SearchUrls.Search(read, "cats"));
    }

    /// <summary>An engine of a newer Karpo reads as none chosen, and the file still opens.</summary>
    [Fact]
    public void AnUnknownEngineReadsAsNoneChosen()
    {
        var config = Configs.Repaired("""{ "searchEngine": "kagi", "layouts": [] }""");
        Assert.Null(config.SearchEngine);
        Assert.Equal(SearchEngine.Google, config.SearchEngineOrDefault);
    }

    // --- The weather's search (2026-10-11) ------------------------------------------------------------

    [Fact]
    public void TheWeatherIsSearchedWithGoogleWhileNoEngineIsChosen() =>
        Assert.Equal("https://www.google.com/search?q=weather+Z%C3%BCrich", SearchUrls.Weather(new Config(), " Zürich "));

    [Fact]
    public void TheWeatherIsSearchedWithTheAppsEngine() =>
        Assert.Equal("https://duckduckgo.com/?q=weather+Bern", SearchUrls.Weather(new Config { SearchEngine = SearchEngine.DuckDuckGo }, "Bern"));

    [Fact]
    public void TheDevicesLocationIsTheWeatherAlone() =>
        Assert.Equal("https://www.google.com/search?q=weather", SearchUrls.Weather(new Config { SearchEngine = SearchEngine.Google }, ""));

    [Fact]
    public void ACustomEngineWithoutAnAddressFallsBackToGoogle() =>
        Assert.Equal("https://www.google.com/search?q=weather+Bern",
            SearchUrls.Weather(new Config { SearchEngine = SearchEngine.Custom, SearchCustomUrl = "not an address" }, "Bern"));

    [Fact]
    public void ACustomEngineSearchesTheWeather() =>
        Assert.Equal("https://example.com/?q=weather+Bern",
            SearchUrls.Weather(new Config { SearchEngine = SearchEngine.Custom, SearchCustomUrl = "https://example.com/?q={q}" }, "Bern"));

    // --- The web search widget, gone (2026-10-11) ------------------------------------------------------

    /// <summary>Gone from every registry: no entry, no line, no options, nothing to add.</summary>
    [Fact]
    public void TheWidgetIsInNoRegistry()
    {
        Assert.Null(WidgetInfos.Find("webSearch"));
        Assert.DoesNotContain(BarEntries.All, entry => entry.Info.Id == "webSearch");
        Assert.DoesNotContain(ListWidgets.All, line => line.Info.Id == "webSearch");
        Assert.DoesNotContain(Presets.All, preset => Presets.Folder(preset, new PresetMachine(false, [])).Folder!.Lists
            .SelectMany(list => list).Any(cell => cell.Island?.Kind == "webSearch"));
        Assert.DoesNotContain("webSearch", Sections.DefaultLayout().Select(island => island.Info?.Id));
    }
}
