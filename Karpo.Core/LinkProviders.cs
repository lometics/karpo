// The sites Karpo opens what a widget shows on: one choice per kind, all in one place, under "Links" on
// the General page — the map every "Open on map" goes to (Config.MapProvider, the person, 2026-10-23:
// "for opening a place on a map you should be able to set the default map provider in which it will get
// opened. provide a few to select from."; docs\widgets.md §319), the weather site "Open forecast" goes to
// (Config.WeatherProvider) and the finance site a share's "Open website" goes to (Config.MarketsProvider;
// §322). How each address is built is here, plain text work, tested without a window or the network.
//
// Maps: each address shows the place marked, at the zoom asked where the map takes one — every one
// checked against the provider's own documentation (2026-10-23):
//   OpenStreetMap  www.openstreetmap.org/?mlat=&mlon=#map=z/lat/lon      a marker, the zoom
//   Google Maps    www.google.com/maps/search/?api=1&query=lat,lon       a pin; Google's documented
//                                                                        Maps URLs take no zoom there
//   Bing Maps      www.bing.com/maps?cp=lat~lon&lvl=z&sp=point.lat_lon_t a pushpin titled, the zoom
//   Apple Maps     maps.apple.com/place?coordinate=lat,lon&name=t        a place card and pin (the
//                                                                        unified URLs, on the web too)
//   HERE WeGo      share.here.com/l/lat,lon,t?z=z                        a marker titled, the zoom
// Windows' own Maps app (bingmaps:) is not offered: Microsoft took it off the Store and made it stop
// working in July 2025.
//
// Weather: each takes the coordinates themselves, every one opened for Zürich, Sydney and New York
// (2026-10-23) and showing that place, not where the request came from:
//   weather.com    weather.com/weather/today/l/lat,lon                  air: forecast/air-quality/l/lat,lon
//   MSN Weather    www.msn.com/en-us/weather/forecast?lat=&lon=         Windows' own weather; only the
//                                                                       en-us market keeps the place (others
//                                                                       redirect by the visitor's address and
//                                                                       drop it); no air quality page of its own
//   Yr             www.yr.no/en/forecast/daily-table/lat,lon            no air quality outside Norway
//   meteoblue      www.meteoblue.com/en/weather/week/<lat>N<lon>E       air: weather/outdoorsports/airquality/…;
//                                                                       south and west as negative numbers
// A site with no air quality page of its own opens its forecast. Not offered: AccuWeather (an address
// needs its own location key), MeteoSwiss (postcodes), Weather Underground (no coordinates: 404).
//
// Markets: a symbol as the price service writes it (Yahoo's: "AAPL", "NESN.SW", "EURCHF=X", "BTC-EUR",
// "^GSPC"), checked on each site 2026-10-23:
//   Yahoo Finance  finance.yahoo.com/quote/<symbol>                     everything the widget reads
//   Google Finance www.google.com/finance?q=<symbol> (a US share, its own exchange found), quote/<sym>:<exch>
//                  for another exchange's, quote/EUR-CHF, quote/BTC-EUR
//   TradingView    www.tradingview.com/symbols/<symbol>/ (a US share, a class "BRK.B"), symbols/<exch>-<sym>/
//                  for another exchange's (Hong Kong without its leading zeros, the Nordic classes "VOLV_B"),
//                  symbols/EURCHF/, symbols/BTCEUR/
// What a site cannot name — an index ("^GSPC", which Google finds only sometimes and TradingView names
// otherwise), a future ("GC=F"), an exchange not in the table — opens on Yahoo Finance, where the widget
// reads it, rather than on a page that is not it.
//
// Sports: ESPN only. A match's and a team's page are ESPN's own ids, which no other site takes, and no
// other site has a documented address by a team's name for every sport ESPN covers; there is nothing
// to choose (SportsTile.SiteOf).

using System.Globalization;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// A map a place is opened on. A name this build doesn't know reads as a value outside the enum, which
/// <see cref="Config.Repair"/> turns into <see cref="LinkProviders.DefaultMap"/>.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<MapProvider>))]
public enum MapProvider
{
    [JsonStringEnumMemberName("openStreetMap")] OpenStreetMap,
    [JsonStringEnumMemberName("google")] Google,
    [JsonStringEnumMemberName("bing")] Bing,
    [JsonStringEnumMemberName("apple")] Apple,
    [JsonStringEnumMemberName("here")] Here,
}

/// <summary>A weather site a place's forecast is opened on; one this build doesn't know is <see cref="LinkProviders.DefaultWeather"/>.</summary>
[JsonConverter(typeof(TolerantEnumConverter<WeatherProvider>))]
public enum WeatherProvider
{
    [JsonStringEnumMemberName("weatherCom")] WeatherCom,
    [JsonStringEnumMemberName("msn")] Msn,
    [JsonStringEnumMemberName("yr")] Yr,
    [JsonStringEnumMemberName("meteoblue")] Meteoblue,
}

/// <summary>A finance site a share, a rate or a coin is opened on; one this build doesn't know is <see cref="LinkProviders.DefaultMarkets"/>.</summary>
[JsonConverter(typeof(TolerantEnumConverter<MarketsProvider>))]
public enum MarketsProvider
{
    [JsonStringEnumMemberName("yahoo")] Yahoo,
    [JsonStringEnumMemberName("google")] Google,
    [JsonStringEnumMemberName("tradingView")] TradingView,
}

/// <summary>Building the addresses Karpo opens a place, a forecast or a share on. Nothing here touches the network or the shell.</summary>
public static class LinkProviders
{
    /// <summary>The map of a config that never chose: OpenStreetMap, the one Karpo always opened.</summary>
    public const MapProvider DefaultMap = MapProvider.OpenStreetMap;

    /// <summary>The weather site of a config that never chose: weather.com, the one Karpo always opened (§308).</summary>
    public const WeatherProvider DefaultWeather = WeatherProvider.WeatherCom;

    /// <summary>The finance site of a config that never chose: Yahoo Finance, where the prices come from (§308).</summary>
    public const MarketsProvider DefaultMarkets = MarketsProvider.Yahoo;

    /// <summary>Every map, in the order the settings app lists them.</summary>
    public static readonly MapProvider[] Maps = Enum.GetValues<MapProvider>();

    /// <summary>Every weather site, in the order the settings app lists them.</summary>
    public static readonly WeatherProvider[] Weathers = Enum.GetValues<WeatherProvider>();

    /// <summary>Every finance site, in the order the settings app lists them.</summary>
    public static readonly MarketsProvider[] Markets = Enum.GetValues<MarketsProvider>();

    /// <summary>The zoom a map opens at where none is asked: a town and its surroundings.</summary>
    public const int TownZoom = 10;

    /// <summary>The map the dock opens places on now (<see cref="Use"/>).</summary>
    public static MapProvider Map { get; private set; } = DefaultMap;

    /// <summary>The weather site the dock opens forecasts on now (<see cref="Use"/>).</summary>
    public static WeatherProvider Weather { get; private set; } = DefaultWeather;

    /// <summary>The finance site the dock opens shares on now (<see cref="Use"/>).</summary>
    public static MarketsProvider Market { get; private set; } = DefaultMarkets;

    /// <summary>Takes <paramref name="config"/>'s sites as the ones the dock opens; none chosen, or one this build doesn't know, is each kind's default.</summary>
    public static void Use(Config config)
    {
        Map = Known(config.MapProvider) ?? DefaultMap;
        Weather = Known(config.WeatherProvider) ?? DefaultWeather;
        Market = Known(config.MarketsProvider) ?? DefaultMarkets;
    }

    /// <summary><paramref name="chosen"/> where this build knows it; null for none or a newer build's (<see cref="Config.Repair"/>).</summary>
    public static T? Known<T>(T? chosen) where T : struct, Enum => chosen is { } value && Enum.IsDefined(value) ? value : null;

    // --- Names: each its own, in no language ---------------------------------------------------------

    /// <summary>What a map is called, as the settings app lists it.</summary>
    public static string Name(this MapProvider provider) => provider switch
    {
        MapProvider.Google => "Google Maps", // not-ui: the map's name
        MapProvider.Bing => "Bing Maps", // not-ui: the map's name
        MapProvider.Apple => "Apple Maps", // not-ui: the map's name
        MapProvider.Here => "HERE WeGo", // not-ui: the map's name
        _ => "OpenStreetMap", // not-ui: the map's name
    };

    /// <summary>What a weather site is called, as the settings app lists it.</summary>
    public static string Name(this WeatherProvider provider) => provider switch
    {
        WeatherProvider.Msn => "MSN Weather", // not-ui: the site's name
        WeatherProvider.Yr => "Yr", // not-ui: the site's name
        WeatherProvider.Meteoblue => "meteoblue", // not-ui: the site's name
        _ => "weather.com", // not-ui: the site's name
    };

    /// <summary>What a finance site is called, as the settings app lists it.</summary>
    public static string Name(this MarketsProvider provider) => provider switch
    {
        MarketsProvider.Google => "Google Finance", // not-ui: the site's name
        MarketsProvider.TradingView => "TradingView", // not-ui: the site's name
        _ => "Yahoo Finance", // not-ui: the site's name
    };

    // --- A place on a map -----------------------------------------------------------------------------

    /// <summary>
    /// The address that shows <paramref name="latitude"/>, <paramref name="longitude"/> marked, called
    /// <paramref name="label"/> where the map names a marker, at <paramref name="zoom"/> where it takes
    /// one, on the map the person chose (<see cref="Map"/>). Every "Open on map" goes through here.
    /// </summary>
    public static string Place(double latitude, double longitude, int zoom = TownZoom, string? label = null) =>
        Place(Map, latitude, longitude, zoom, label);

    /// <summary>The address that shows the place on <paramref name="provider"/>; a map this build doesn't know is <see cref="DefaultMap"/>.</summary>
    public static string Place(MapProvider provider, double latitude, double longitude, int zoom = TownZoom, string? label = null)
    {
        var (lat, lon) = Coordinates(latitude, longitude);
        var z = Math.Clamp(zoom, 1, 19).ToString(CultureInfo.InvariantCulture);
        var name = (label ?? "").Trim();
        return provider switch
        {
            MapProvider.Google => $"https://www.google.com/maps/search/?api=1&query={lat}%2C{lon}",
            // A point's parts are parted by "_" and points by "~": neither may stand in its title.
            MapProvider.Bing => $"https://www.bing.com/maps?cp={lat}~{lon}&lvl={z}&sp=point.{lat}_{lon}"
                + (name.Length > 0 ? $"_{Uri.EscapeDataString(Plain(name, '_', '~'))}" : ""),
            MapProvider.Apple => $"https://maps.apple.com/place?coordinate={lat}%2C{lon}" + (name.Length > 0 ? $"&name={Uri.EscapeDataString(name)}" : ""),
            // The title is the location's third part, after a comma.
            MapProvider.Here => $"https://share.here.com/l/{lat},{lon}" + (name.Length > 0 ? $",{Uri.EscapeDataString(Plain(name, ','))}" : "") + $"?z={z}",
            _ => $"https://www.openstreetmap.org/?mlat={lat}&mlon={lon}#map={z}/{lat}/{lon}",
        };
    }

    // --- A place's forecast ---------------------------------------------------------------------------

    /// <summary>
    /// The forecast for <paramref name="latitude"/>, <paramref name="longitude"/> — its air quality's,
    /// <paramref name="air"/>, where the site has a page for it — on the weather site the person chose
    /// (<see cref="Weather"/>). Every "Open forecast" goes through here.
    /// </summary>
    public static string Forecast(double latitude, double longitude, bool air = false) => Forecast(Weather, latitude, longitude, air);

    /// <summary>The forecast on <paramref name="provider"/>; a site this build doesn't know is <see cref="DefaultWeather"/>.</summary>
    public static string Forecast(WeatherProvider provider, double latitude, double longitude, bool air = false)
    {
        var (lat, lon) = Coordinates(latitude, longitude);
        return provider switch
        {
            WeatherProvider.Msn => $"https://www.msn.com/en-us/weather/forecast?lat={lat}&lon={lon}",
            WeatherProvider.Yr => $"https://www.yr.no/en/forecast/daily-table/{lat},{lon}",
            WeatherProvider.Meteoblue => (air ? "https://www.meteoblue.com/en/weather/outdoorsports/airquality/" : "https://www.meteoblue.com/en/weather/week/") + $"{lat}N{lon}E",
            _ => (air ? "https://weather.com/forecast/air-quality/l/" : "https://weather.com/weather/today/l/") + $"{lat},{lon}",
        };
    }

    // --- A share, a rate, a coin ----------------------------------------------------------------------

    /// <summary>
    /// The page of <paramref name="symbol"/> (the price service's, <see cref="MarketSymbols"/>) on the
    /// finance site the person chose (<see cref="Market"/>); null with no symbol. Every share's "Open
    /// website" goes through here.
    /// </summary>
    public static string? Quote(string? symbol) => Quote(Market, symbol);

    /// <summary>
    /// The page of <paramref name="symbol"/> on <paramref name="provider"/>: on Yahoo Finance where the
    /// site cannot name it (an index, a future, an exchange it is not known on here) or doesn't know the
    /// site; null with no symbol.
    /// </summary>
    public static string? Quote(MarketsProvider provider, string? symbol)
    {
        var name = MarketSymbols.Canonical(symbol).ToUpperInvariant();
        if (name.Length == 0)
        {
            return null;
        }
        var page = provider switch
        {
            MarketsProvider.Google => Google(name),
            MarketsProvider.TradingView => TradingView(name),
            _ => null,
        };
        return page ?? "https://finance.yahoo.com/quote/" + Uri.EscapeDataString(name);
    }

    /// <summary>A symbol on Google Finance; null where it names none.</summary>
    private static string? Google(string symbol)
    {
        if (MarketSymbols.Pair(symbol) is ({ } from, { } to))
        {
            return $"https://www.google.com/finance/quote/{from}-{to}";
        }
        if (MarketSymbols.Coin(symbol) is ({ } coin, { } priced))
        {
            return $"https://www.google.com/finance/quote/{Uri.EscapeDataString(coin)}-{priced}";
        }
        return Share(symbol) switch
        {
            (var share, null) => "https://www.google.com/finance?q=" + Uri.EscapeDataString(share),
            (var share, { Google: { } exchange }) => $"https://www.google.com/finance/quote/{Uri.EscapeDataString(share)}:{exchange}",
            _ => null,
        };
    }

    /// <summary>A symbol on TradingView; null where it names none.</summary>
    private static string? TradingView(string symbol)
    {
        if (MarketSymbols.Pair(symbol) is ({ } from, { } to))
        {
            return $"https://www.tradingview.com/symbols/{from}{to}/";
        }
        if (MarketSymbols.Coin(symbol) is ({ } coin, { } priced))
        {
            return $"https://www.tradingview.com/symbols/{Uri.EscapeDataString(coin)}{priced}/";
        }
        return Share(symbol) switch
        {
            // A US share's class is "BRK.B" there; the site finds its exchange.
            (var share, null) => $"https://www.tradingview.com/symbols/{Uri.EscapeDataString(share.Replace('-', '.'))}/",
            (var share, { TradingView: { } exchange } listed) =>
                $"https://www.tradingview.com/symbols/{exchange}-{Uri.EscapeDataString(listed.Yahoo == "HK" ? share.TrimStart('0') : listed.Nordic ? share.Replace('-', '_') : share)}/",
            _ => null,
        };
    }

    /// <summary>
    /// A share's symbol and the exchange its suffix names ("NESN.SW": "NESN" on SIX; "AAPL": "AAPL" and
    /// no exchange); null for what is no plain share — an index ("^"), a future ("="), a suffix not in
    /// <see cref="Exchanges"/>.
    /// </summary>
    private static (string Share, Exchange? Exchange)? Share(string symbol)
    {
        if (symbol.IndexOfAny(['^', '=', '/', ' ']) >= 0)
        {
            return null;
        }
        var dot = symbol.LastIndexOf('.');
        if (dot < 0)
        {
            return (symbol, null);
        }
        var suffix = symbol[(dot + 1)..];
        return dot > 0 && Array.Find(Exchanges, exchange => exchange.Yahoo == suffix) is { } listed ? (symbol[..dot], listed) : null;
    }

    /// <summary>An exchange by the suffix the price service gives its shares, and its code on Google Finance and on TradingView (null where that site's page was not found).</summary>
    private sealed record Exchange(string Yahoo, string? Google, string? TradingView, bool Nordic = false);

    /// <summary>The exchanges a share's suffix is known for, each code opened on its site with a share of it (2026-10-23).</summary>
    private static readonly Exchange[] Exchanges =
    [
        new("SW", "SWX", "SIX"),
        new("L", "LON", "LSE"),
        new("DE", "ETR", "XETR"),
        new("F", "FRA", "FWB"),
        new("PA", "EPA", "EURONEXT"),
        new("AS", "AMS", "EURONEXT"),
        new("BR", "EBR", "EURONEXT"),
        new("MI", "BIT", "MIL"),
        new("MC", "BME", "BME"),
        new("VI", "VIE", "VIE"),
        new("TO", "TSE", "TSX"),
        new("V", "CVE", "TSXV"),
        new("AX", "ASX", "ASX"),
        new("HK", "HKG", "HKEX"),
        new("T", "TYO", "TSE"),
        new("ST", "STO", "OMXSTO", Nordic: true),
        new("CO", "CPH", "OMXCOP", Nordic: true),
        new("HE", "HEL", "OMXHEX", Nordic: true),
        new("OL", null, "OSL"),
    ];

    // --- Parts ----------------------------------------------------------------------------------------

    /// <summary>A coordinate as every site reads it: a point, never a comma, and four places (about 11 m).</summary>
    public static string Coordinate(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>A place's two coordinates, each kept to the globe, as <see cref="Coordinate"/> writes them.</summary>
    private static (string Latitude, string Longitude) Coordinates(double latitude, double longitude) =>
        (Coordinate(Math.Clamp(latitude, -90, 90)), Coordinate(Math.Clamp(longitude, -180, 180)));

    /// <summary><paramref name="text"/> with each of <paramref name="parting"/> a space: a title that parts nothing.</summary>
    private static string Plain(string text, params char[] parting)
    {
        foreach (var mark in parting)
        {
            text = text.Replace(mark, ' ');
        }
        return text.Trim();
    }
}
