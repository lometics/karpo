// The sites Karpo opens what a widget shows on (Config.MapProvider, .WeatherProvider, .MarketsProvider;
// Karpo.Core\LinkProviders.cs; docs\widgets.md §319, §322): what each site opens for a place, a forecast
// and a symbol, a title that cannot break an address, the file keeping each choice and reading one it
// doesn't know as none, the settings app's Links rows, and the dock's rows following the choice. The
// sites the dock uses are the process's (LinkProviders.Use), so these run alone.

using System.Text.Json.Nodes;
using System.Xml.Linq;
using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Searching;

[CollectionDefinition(Collection, DisableParallelization = true)]
public sealed class LinkProvidersCollection
{
    public const string Collection = "the sites Karpo opens a widget's place, forecast and share on";
}

[Collection(LinkProvidersCollection.Collection)]
public sealed class LinkProvidersTests
{
    // --- Maps -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(MapProvider.OpenStreetMap, "https://www.openstreetmap.org/?mlat=47.3769&mlon=8.5417#map=12/47.3769/8.5417")]
    [InlineData(MapProvider.Google, "https://www.google.com/maps/search/?api=1&query=47.3769%2C8.5417")]
    [InlineData(MapProvider.Bing, "https://www.bing.com/maps?cp=47.3769~8.5417&lvl=12&sp=point.47.3769_8.5417_Z%C3%BCrich")]
    [InlineData(MapProvider.Apple, "https://maps.apple.com/place?coordinate=47.3769%2C8.5417&name=Z%C3%BCrich")]
    [InlineData(MapProvider.Here, "https://share.here.com/l/47.3769,8.5417,Z%C3%BCrich?z=12")]
    public void EveryMapShowsThePlaceMarked(MapProvider provider, string expected) =>
        Assert.Equal(expected, LinkProviders.Place(provider, 47.37690, 8.541694, 12, "Zürich"));

    /// <summary>
    /// A title's own marks never part the address — Bing parts a point by "_" and points by "~", HERE
    /// the location by commas —; a place with no title has none; the coordinates are a point's, in
    /// range, and the zoom one every map takes.
    /// </summary>
    [Fact]
    public void ATitleNeverBreaksTheAddress()
    {
        Assert.Equal("https://www.bing.com/maps?cp=-33.8688~151.2093&lvl=19&sp=point.-33.8688_151.2093_Sydney%20%20NSW%20%20x",
            LinkProviders.Place(MapProvider.Bing, -33.8688, 151.2093, 40, "Sydney_~NSW_~x"));
        Assert.Equal("https://share.here.com/l/-33.8688,151.2093,Sydney%20%20NSW?z=1", LinkProviders.Place(MapProvider.Here, -33.8688, 151.2093, 0, "Sydney, NSW"));
        Assert.Equal("https://maps.apple.com/place?coordinate=1%2C2&name=A%26B%3Dc", LinkProviders.Place(MapProvider.Apple, 1, 2, 10, "A&B=c"));
        Assert.Equal("https://www.bing.com/maps?cp=1~2&lvl=10&sp=point.1_2", LinkProviders.Place(MapProvider.Bing, 1, 2, 10, "  "));
        Assert.Equal("https://maps.apple.com/place?coordinate=1%2C2", LinkProviders.Place(MapProvider.Apple, 1, 2, 10));
        Assert.Equal("https://share.here.com/l/1,2?z=10", LinkProviders.Place(MapProvider.Here, 1, 2, 10, null));
        Assert.Equal("https://www.openstreetmap.org/?mlat=90&mlon=-180#map=10/90/-180", LinkProviders.Place(MapProvider.OpenStreetMap, 91, -200));
        Assert.Equal(LinkProviders.Place(MapProvider.OpenStreetMap, 1, 2), LinkProviders.Place((MapProvider)99, 1, 2));
    }

    // --- Weather --------------------------------------------------------------------------------------

    /// <summary>Each weather site at the place's coordinates, south and west too; its air quality's where it has a page for it, else its forecast.</summary>
    [Theory]
    [InlineData(WeatherProvider.WeatherCom, false, "https://weather.com/weather/today/l/47.3769,8.5417", "https://weather.com/weather/today/l/-33.8688,-74.006")]
    [InlineData(WeatherProvider.WeatherCom, true, "https://weather.com/forecast/air-quality/l/47.3769,8.5417", "https://weather.com/forecast/air-quality/l/-33.8688,-74.006")]
    [InlineData(WeatherProvider.Msn, false, "https://www.msn.com/en-us/weather/forecast?lat=47.3769&lon=8.5417", "https://www.msn.com/en-us/weather/forecast?lat=-33.8688&lon=-74.006")]
    [InlineData(WeatherProvider.Msn, true, "https://www.msn.com/en-us/weather/forecast?lat=47.3769&lon=8.5417", "https://www.msn.com/en-us/weather/forecast?lat=-33.8688&lon=-74.006")]
    [InlineData(WeatherProvider.Yr, false, "https://www.yr.no/en/forecast/daily-table/47.3769,8.5417", "https://www.yr.no/en/forecast/daily-table/-33.8688,-74.006")]
    [InlineData(WeatherProvider.Yr, true, "https://www.yr.no/en/forecast/daily-table/47.3769,8.5417", "https://www.yr.no/en/forecast/daily-table/-33.8688,-74.006")]
    [InlineData(WeatherProvider.Meteoblue, false, "https://www.meteoblue.com/en/weather/week/47.3769N8.5417E", "https://www.meteoblue.com/en/weather/week/-33.8688N-74.006E")]
    [InlineData(WeatherProvider.Meteoblue, true, "https://www.meteoblue.com/en/weather/outdoorsports/airquality/47.3769N8.5417E", "https://www.meteoblue.com/en/weather/outdoorsports/airquality/-33.8688N-74.006E")]
    public void EveryWeatherSiteOpensThePlace(WeatherProvider provider, bool air, string zurich, string southWest)
    {
        Assert.Equal(zurich, LinkProviders.Forecast(provider, 47.37690, 8.541694, air));
        Assert.Equal(southWest, LinkProviders.Forecast(provider, -33.8688, -74.006, air));
    }

    /// <summary>Coordinates kept to the globe, written with a point; a site this build doesn't know is weather.com.</summary>
    [Fact]
    public void AForecastsCoordinatesAreAlwaysAddressable()
    {
        Assert.Equal("https://www.yr.no/en/forecast/daily-table/90,-180", LinkProviders.Forecast(WeatherProvider.Yr, 95, -181));
        Assert.Equal("https://www.meteoblue.com/en/weather/week/1.5N2.25E", LinkProviders.Forecast(WeatherProvider.Meteoblue, 1.5, 2.25));
        Assert.Equal(LinkProviders.Forecast(WeatherProvider.WeatherCom, 1, 2, air: true), LinkProviders.Forecast((WeatherProvider)99, 1, 2, air: true));
    }

    // --- Markets --------------------------------------------------------------------------------------

    /// <summary>
    /// Each finance site's page for a symbol as the price service writes it: a US share, a class, another
    /// exchange's share (Hong Kong's zeros, a Nordic class), a rate, a coin; what a site cannot name — an
    /// index, a future, an exchange not in its table — on Yahoo Finance, where the widget reads it.
    /// </summary>
    [Theory]
    [InlineData(MarketsProvider.Yahoo, "aapl", "https://finance.yahoo.com/quote/AAPL")]
    [InlineData(MarketsProvider.Yahoo, "^GSPC", "https://finance.yahoo.com/quote/%5EGSPC")]
    [InlineData(MarketsProvider.Yahoo, "chf=x", "https://finance.yahoo.com/quote/USDCHF%3DX")]
    [InlineData(MarketsProvider.Google, "AAPL", "https://www.google.com/finance?q=AAPL")]
    [InlineData(MarketsProvider.Google, "BRK-B", "https://www.google.com/finance?q=BRK-B")]
    [InlineData(MarketsProvider.Google, "NESN.SW", "https://www.google.com/finance/quote/NESN:SWX")]
    [InlineData(MarketsProvider.Google, "0700.HK", "https://www.google.com/finance/quote/0700:HKG")]
    [InlineData(MarketsProvider.Google, "VOLV-B.ST", "https://www.google.com/finance/quote/VOLV-B:STO")]
    [InlineData(MarketsProvider.Google, "EURCHF=X", "https://www.google.com/finance/quote/EUR-CHF")]
    [InlineData(MarketsProvider.Google, "BTC-EUR", "https://www.google.com/finance/quote/BTC-EUR")]
    [InlineData(MarketsProvider.Google, "EQNR.OL", "https://finance.yahoo.com/quote/EQNR.OL")]
    [InlineData(MarketsProvider.Google, "^GSPC", "https://finance.yahoo.com/quote/%5EGSPC")]
    [InlineData(MarketsProvider.Google, "GC=F", "https://finance.yahoo.com/quote/GC%3DF")]
    [InlineData(MarketsProvider.TradingView, "MSFT", "https://www.tradingview.com/symbols/MSFT/")]
    [InlineData(MarketsProvider.TradingView, "BRK-B", "https://www.tradingview.com/symbols/BRK.B/")]
    [InlineData(MarketsProvider.TradingView, "NESN.SW", "https://www.tradingview.com/symbols/SIX-NESN/")]
    [InlineData(MarketsProvider.TradingView, "0700.HK", "https://www.tradingview.com/symbols/HKEX-700/")]
    [InlineData(MarketsProvider.TradingView, "VOLV-B.ST", "https://www.tradingview.com/symbols/OMXSTO-VOLV_B/")]
    [InlineData(MarketsProvider.TradingView, "EQNR.OL", "https://www.tradingview.com/symbols/OSL-EQNR/")]
    [InlineData(MarketsProvider.TradingView, "EURCHF=X", "https://www.tradingview.com/symbols/EURCHF/")]
    [InlineData(MarketsProvider.TradingView, "btc-usd", "https://www.tradingview.com/symbols/BTCUSD/")]
    [InlineData(MarketsProvider.TradingView, "^GSPC", "https://finance.yahoo.com/quote/%5EGSPC")]
    [InlineData(MarketsProvider.TradingView, "SHOP.XX", "https://finance.yahoo.com/quote/SHOP.XX")]
    public void EveryFinanceSiteOpensTheSymbolOrYahooWhereItCannot(MarketsProvider provider, string symbol, string expected) =>
        Assert.Equal(expected, LinkProviders.Quote(provider, symbol));

    /// <summary>No symbol is no page; a site this build doesn't know is Yahoo Finance; nothing typed breaks out of the address.</summary>
    [Fact]
    public void AQuoteNeedsASymbolAndStaysInItsAddress()
    {
        Assert.Null(LinkProviders.Quote(MarketsProvider.Google, "  "));
        Assert.Null(LinkProviders.Quote(MarketsProvider.Yahoo, null));
        Assert.Equal("https://finance.yahoo.com/quote/AAPL", LinkProviders.Quote((MarketsProvider)99, "AAPL"));
        Assert.Equal("https://finance.yahoo.com/quote/A%2FB%3FC", LinkProviders.Quote(MarketsProvider.TradingView, "a/b?c"));
        Assert.Equal("https://www.google.com/finance?q=A%26B", LinkProviders.Quote(MarketsProvider.Google, "a&b"));
    }

    // --- Names, defaults, the file ----------------------------------------------------------------------

    [Fact]
    public void EverySiteIsOfferedUnderItsOwnNameAndNoneChosenIsWhatKarpoAlwaysOpened()
    {
        Assert.Equal(["OpenStreetMap", "Google Maps", "Bing Maps", "Apple Maps", "HERE WeGo"], LinkProviders.Maps.Select(provider => provider.Name()));
        Assert.Equal(["weather.com", "MSN Weather", "Yr", "meteoblue"], LinkProviders.Weathers.Select(provider => provider.Name()));
        Assert.Equal(["Yahoo Finance", "Google Finance", "TradingView"], LinkProviders.Markets.Select(provider => provider.Name()));
        Assert.Equal(MapProvider.OpenStreetMap, LinkProviders.DefaultMap);
        Assert.Equal(WeatherProvider.WeatherCom, LinkProviders.DefaultWeather);
        Assert.Equal(MarketsProvider.Yahoo, LinkProviders.DefaultMarkets);
    }

    /// <summary>A file that never chose says nothing and opens what Karpo always opened; each choice is kept under its own key, "mapProvider" as §319 wrote it.</summary>
    [Fact]
    public void EachChoiceIsKeptInTheFileAndNoneIsTheDefault()
    {
        var unset = Configs.Repaired("""{ "layouts": [] }""");
        Assert.Null(unset.MapProvider);
        Assert.Null(unset.WeatherProvider);
        Assert.Null(unset.MarketsProvider);
        Assert.Equal(MapProvider.OpenStreetMap, unset.MapProviderOrDefault);
        Assert.Equal(WeatherProvider.WeatherCom, unset.WeatherProviderOrDefault);
        Assert.Equal(MarketsProvider.Yahoo, unset.MarketsProviderOrDefault);
        var empty = JsonNode.Parse(unset.ToJson())!;
        Assert.Null(empty["mapProvider"]);
        Assert.Null(empty["weatherProvider"]);
        Assert.Null(empty["marketsProvider"]);

        var config = Configs.With(BarKind.Start.New());
        config.MapProvider = MapProvider.Here;
        config.WeatherProvider = WeatherProvider.Meteoblue;
        config.MarketsProvider = MarketsProvider.TradingView;
        var read = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal((MapProvider.Here, WeatherProvider.Meteoblue, MarketsProvider.TradingView), (read.MapProvider, read.WeatherProvider, read.MarketsProvider));
        var file = JsonNode.Parse(read.ToJson())!;
        Assert.Equal("here", file["mapProvider"]!.GetValue<string>());
        Assert.Equal("meteoblue", file["weatherProvider"]!.GetValue<string>());
        Assert.Equal("tradingView", file["marketsProvider"]!.GetValue<string>());
    }

    /// <summary>A site of a newer Karpo reads as none chosen, each on its own, and everything else in the file stands.</summary>
    [Fact]
    public void AnUnknownSiteReadsAsNoneChosen()
    {
        var config = Configs.Repaired("""{ "mapProvider": "waze", "weatherProvider": "accuweather", "marketsProvider": "google", "searchEngine": "bing", "layouts": [ { "id": "L", "islands": [ { "id": "s", "kind": "start" } ] } ] }""");
        Assert.Null(config.MapProvider);
        Assert.Null(config.WeatherProvider);
        Assert.Equal(MarketsProvider.Google, config.MarketsProvider);
        Assert.Equal(WeatherProvider.WeatherCom, config.WeatherProviderOrDefault);
        Assert.Equal(SearchEngine.Bing, config.SearchEngine);
        Assert.Equal(["s"], config.Layouts[0].Islands.Select(island => island.Id));
    }

    // --- The dock ---------------------------------------------------------------------------------------

    /// <summary>
    /// "Open on map" and "Open forecast" open the place on the sites chosen, as the dock takes them
    /// from the config (LinkProviders.Use); none chosen, or one this build doesn't know, is the default.
    /// </summary>
    [Fact]
    public void TheMenusFollowTheSitesChosen()
    {
        var zurich = new WeatherOptions { Place = "Zürich", Latitude = 47.3769, Longitude = 8.5417 };
        try
        {
            foreach (var provider in LinkProviders.Maps)
            {
                LinkProviders.Use(new Config { MapProvider = provider });
                Assert.Equal(LinkProviders.Place(provider, 47.3769, 8.5417, LinkProviders.TownZoom, PlaceNames.Whole(zurich)), TheDock.MapPage(zurich, device: null));
            }
            foreach (var provider in LinkProviders.Weathers)
            {
                LinkProviders.Use(new Config { WeatherProvider = provider });
                Assert.Equal(LinkProviders.Forecast(provider, 47.3769, 8.5417), TheDock.ForecastPage(zurich, air: false, device: null));
                Assert.Equal(LinkProviders.Forecast(provider, 47.3769, 8.5417, air: true), TheDock.ForecastPage(zurich, air: true, device: null));
            }
            // A place followed from the device has no name of its own: marked, untitled.
            LinkProviders.Use(new Config { MapProvider = MapProvider.Apple, WeatherProvider = WeatherProvider.Yr });
            Assert.Equal("https://maps.apple.com/place?coordinate=-33.8688%2C151.2093", TheDock.MapPage(WeatherOptions.New(), device: (-33.8688, 151.2093)));
            Assert.Equal("https://www.yr.no/en/forecast/daily-table/-33.8688,151.2093", TheDock.ForecastPage(WeatherOptions.New(), air: false, device: (-33.8688, 151.2093)));
            LinkProviders.Use(new Config { MapProvider = (MapProvider)99, WeatherProvider = (WeatherProvider)99, MarketsProvider = (MarketsProvider)99 });
            Assert.Equal((MapProvider.OpenStreetMap, WeatherProvider.WeatherCom, MarketsProvider.Yahoo), (LinkProviders.Map, LinkProviders.Weather, LinkProviders.Market));
        }
        finally
        {
            LinkProviders.Use(new Config());
        }
        Assert.Equal((MapProvider.OpenStreetMap, WeatherProvider.WeatherCom, MarketsProvider.Yahoo), (LinkProviders.Map, LinkProviders.Weather, LinkProviders.Market));
    }

    /// <summary>A share line's "Open website" is its page on the finance site chosen.</summary>
    [UiFact]
    public void AShareOpensOnTheFinanceSiteChosen()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var line = MarketOffscreenTests.Made(gfx, new Config(), MarketOffscreenTests.Waiting(), "m", MarketOffscreenTests.Held("NESN.SW"), answers: false);
            string? Opens() => Assert.Single(TheDock.ActRows([WidgetAct.OpenWebsite], line).Rows).On;
            try
            {
                Assert.Equal("https://finance.yahoo.com/quote/NESN.SW", Opens());
                LinkProviders.Use(new Config { MarketsProvider = MarketsProvider.Google });
                Assert.Equal("https://www.google.com/finance/quote/NESN:SWX", Opens());
                LinkProviders.Use(new Config { MarketsProvider = MarketsProvider.TradingView });
                Assert.Equal("https://www.tradingview.com/symbols/SIX-NESN/", Opens());
            }
            finally
            {
                LinkProviders.Use(new Config());
            }
        });
    }

    // --- The import summary -----------------------------------------------------------------------------

    /// <summary>
    /// The import summary names each link kind's key by its row's words (⚙ › Online, §335).
    /// </summary>
    [Fact]
    public void TheImportSummaryNamesEachLinkKind()
    {
        Assert.Equal("Map", TransferSummary.Title("mapProvider"));
        Assert.Equal("Weather", TransferSummary.Title("weatherProvider"));
        Assert.Equal("Markets", TransferSummary.Title("marketsProvider"));
    }
}
