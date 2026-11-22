// Searching the web: the one engine every web search Karpo opens goes to (Config.SearchEngine, on
// the General page), and how its address is built. Plain text work, shared by the dock (the
// weather's click, the air quality's, a clock's, a markets widget's) and the settings app, and tested without a window or the network.
//
// There was a web search widget, a box on the bar with an engine of its own; it was taken out
// (2026-10-11: "remove the web search widget alltogether", "you should be able to select the
// default serach engine globally for the whole application"). Karpo's
// Search kind is the Windows taskbar's search button, which opens Windows' own search, and stays.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// A search engine. A name this build doesn't know reads as a value outside the enum, which
/// <see cref="Config.Repair"/> turns into <see cref="SearchUrls.Default"/>.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<SearchEngine>))]
public enum SearchEngine
{
    [JsonStringEnumMemberName("google")] Google,
    [JsonStringEnumMemberName("bing")] Bing,
    [JsonStringEnumMemberName("duckduckgo")] DuckDuckGo,
    [JsonStringEnumMemberName("ecosia")] Ecosia,
    [JsonStringEnumMemberName("startpage")] Startpage,
    [JsonStringEnumMemberName("brave")] Brave,

    /// <summary>An address of the person's own, with <see cref="SearchUrls.Placeholder"/> where the query goes.</summary>
    [JsonStringEnumMemberName("custom")] Custom,
}

/// <summary>
/// Windows' own search button on the bar (<see cref="BarKind.Search"/>): whether it is the search
/// box Windows 11's taskbar can show, or only its icon. Absent means the icon, and it is only
/// written for a button somebody set to the box, so a file keeps nothing it does not need.
/// </summary>
public sealed class WindowsSearchOptions
{
    /// <summary>The box, where the bar has room for one; off = only the glyph, as before.</summary>
    public bool ShowBox { get; set; }

    /// <summary>These options, or null — the options dropped — where only the icon is chosen.</summary>
    public WindowsSearchOptions? Normalized() => ShowBox ? this : null;
}

/// <summary>What one engine is called and where it searches.</summary>
/// <param name="Name">Its name, as the settings app shows it.</param>
/// <param name="Search">Its search address, with <see cref="SearchUrls.Placeholder"/> for the query.</param>
public readonly record struct EngineInfo(string Name, string Search);

/// <summary>Building the addresses a web search opens. Nothing here touches the network or the shell.</summary>
public static class SearchUrls
{
    /// <summary>What a custom address holds where the query goes.</summary>
    public const string Placeholder = "{q}";

    /// <summary>
    /// The engine of a new config, and the one a search falls back on: Google (2026-10-16: "default
    /// search engine should be google"; Bing, Windows' own, before). The
    /// default browser's own engine is not asked — it lives in each browser's profile, in a shape of
    /// its own, and is no setting of Windows'.
    /// </summary>
    public const SearchEngine Default = SearchEngine.Google;

    /// <summary>Every engine, in the order the settings app lists them.</summary>
    public static readonly SearchEngine[] All =
    [
        SearchEngine.Google, SearchEngine.Bing, SearchEngine.DuckDuckGo,
        SearchEngine.Ecosia, SearchEngine.Startpage, SearchEngine.Brave, SearchEngine.Custom,
    ];

    /// <summary>What an engine is called and where it searches.</summary>
    public static EngineInfo Info(this SearchEngine engine) => engine switch
    {
        SearchEngine.Google => new EngineInfo("Google", "https://www.google.com/search?q={q}"),
        SearchEngine.DuckDuckGo => new EngineInfo("DuckDuckGo", "https://duckduckgo.com/?q={q}"),
        SearchEngine.Ecosia => new EngineInfo("Ecosia", "https://www.ecosia.org/search?q={q}"),
        SearchEngine.Startpage => new EngineInfo("Startpage", "https://www.startpage.com/sp/search?query={q}"),
        SearchEngine.Brave => new EngineInfo("Brave Search", "https://search.brave.com/search?q={q}"), // not-ui: the engine's name
        SearchEngine.Custom => new EngineInfo(S.CoreWebSearchCustom, ""),
        _ => new EngineInfo("Bing", "https://www.bing.com/search?q={q}"),
    };

    /// <summary>
    /// The address that searches for <paramref name="query"/> with <paramref name="engine"/>, or null
    /// where it can't: an empty query, or a custom engine without a usable address.
    /// </summary>
    public static string? Search(SearchEngine engine, string? customUrl, string query)
    {
        var typed = query.Trim();
        if (typed.Length == 0)
        {
            return null;
        }
        var template = engine == SearchEngine.Custom ? customUrl?.Trim() ?? "" : engine.Info().Search;
        return Fill(template, typed);
    }

    /// <summary>
    /// The address that searches for <paramref name="query"/> with the app's engine
    /// (<see cref="Config.SearchEngine"/>), or with <see cref="Default"/> where that can't — a custom
    /// engine with no usable address. Null only for an empty query. Every web search Karpo opens goes
    /// through here.
    /// </summary>
    public static string? Search(Config config, string query) =>
        Search(config.SearchEngineOrDefault, config.SearchCustomUrl, query) ?? Search(Default, null, query);

    /// <summary>
    /// The address a weather line opens (2026-10-11: "when clicking on a weather widget it should
    /// search for the weather in the default browser"): "weather <paramref name="place"/>" — or
    /// "weather" alone for the device's location, which the engine places itself — with the app's engine.
    /// <paramref name="day"/>, where given, is the day asked about after it: "weather Bern Wednesday"
    /// (a day of the week's line, docs\widgets.md §131).
    /// </summary>
    public static string Weather(Config config, string place, string? day = null)
    {
        var query = place.Trim().Length > 0 ? S.WidgetWeatherSearch(place: place.Trim()) : S.WidgetWeatherSearchHere;
        if (day?.Trim() is { Length: > 0 } named)
        {
            query = $"{query} {named}";
        }
        return Search(config, query)!;
    }

    /// <summary>
    /// The address an air quality line opens (2026-11-09: "i should also be able to click on the air quality widget to do
    /// a search for it"; docs\widgets.md §614), as a weather line's opens the weather: "air quality <paramref name="place"/>"
    /// — or "air quality" alone for the device's location, which the engine places itself — with the app's engine.
    /// </summary>
    public static string Air(Config config, string place) =>
        Search(config, place.Trim() is { Length: > 0 } named ? S.WidgetAirSearch(place: named) : S.WidgetAirSearchHere)!;

    /// <summary>
    /// The address a clock's click opens (2026-10-28: "the time widget you should be able to click as well,
    /// it should then google the current time for the timezone/place in the prefered search engine";
    /// docs\widgets.md §377): "time in <paramref name="place"/>" — or "time" alone for this PC's own clock,
    /// which the engine answers with the time where the PC is — with the app's engine.
    /// </summary>
    public static string Time(Config config, string place) =>
        Search(config, place.Trim() is { Length: > 0 } named ? S.WidgetClockSearch(place: named) : S.WidgetClockSearchHere)!;

    /// <summary>
    /// What a markets widget's click searches for (2026-10-20: "when i left click on the stock widget it
    /// should open the stock in the default search in the browser"; docs\widgets.md §263), by what its
    /// <paramref name="symbol"/> is (<see cref="MarketSymbols"/>): a pair its two codes, "EUR CHF", and a
    /// coin its ticker and currency, "BTC EUR" — what the engines answer with their converter or the coin's
    /// price, in every language —, and a share, a fund or an index its symbol and the word that makes it a
    /// quote, "AAPL stock" (<c>widget.markets.search</c>). Null where there is no symbol yet.
    /// </summary>
    public static string? MarketQuery(string? symbol) =>
        MarketSymbols.Pair(symbol) is { } pair ? $"{pair.From} {pair.To}"
        : MarketSymbols.Coin(symbol) is { } coin ? $"{coin.Coin} {coin.To}"
        : (symbol ?? "").Trim() is { Length: > 0 } share ? S.WidgetMarketsSearch(symbol: share.ToUpperInvariant())
        : null;

    /// <summary>The address a markets widget's click opens: its <see cref="MarketQuery"/> with the app's engine; null where there is no symbol yet.</summary>
    public static string? Market(Config config, string? symbol) =>
        MarketQuery(symbol) is { } query ? Search(config, query) : null;

    /// <summary>
    /// <paramref name="template"/> with <see cref="Placeholder"/> replaced by
    /// <paramref name="query"/>, encoded as a query value; null when the template is empty, has no
    /// placeholder, or isn't an http(s) address. Every occurrence is filled, so a template that
    /// repeats the query works too.
    /// </summary>
    public static string? Fill(string template, string query)
    {
        var text = template.Trim();
        if (text.Length == 0 || !text.Contains(Placeholder, StringComparison.Ordinal) || !IsWebAddress(text))
        {
            return null;
        }
        return text.Replace(Placeholder, Encode(query), StringComparison.Ordinal);
    }

    /// <summary>
    /// A query as a URL's query value: the characters a query string may not hold percent-encoded,
    /// and spaces as "+", which every engine here reads and which keeps the address readable.
    /// </summary>
    public static string Encode(string query)
    {
        var escaped = Uri.EscapeDataString(query.Trim());
        // EscapeDataString leaves no "+" of its own behind (it writes %2B), so this only maps spaces.
        return escaped.Replace("%20", "+", StringComparison.Ordinal);
    }

    /// <summary>
    /// Why <paramref name="template"/> can't be a custom search address, for the settings app;
    /// null when it is a good one. An empty address is not an error: searches go to
    /// <see cref="Default"/> until there is one.
    /// </summary>
    public static string? Problem(string template)
    {
        var text = template.Trim();
        if (text.Length == 0)
        {
            return null;
        }
        if (!IsWebAddress(text))
        {
            return S.CoreWebSearchBadScheme;
        }
        if (!text.Contains(Placeholder, StringComparison.Ordinal))
        {
            return S.CoreWebSearchNoPlace(Placeholder);
        }
        return null;
    }

    /// <summary>
    /// What <paramref name="typed"/> is worth as the custom address, under its row: what is wrong with
    /// it, that searches go to <see cref="Default"/> until there is one, or where the words go. The
    /// settings app's address box and the edit bar's ⚙ › Online › (§337) alike.
    /// </summary>
    public static string Status(string typed) =>
        Problem(typed)
        ?? (typed.Trim().Length == 0
            ? S.SettingsSearchNeeded(Default.Info().Name)
            : S.SettingsSearchBecomes(Placeholder));

    /// <summary>An address of the right shape, in the empty address box; an address, so in no language.</summary>
    public const string Example = "https://example.com/search?q=" + Placeholder;

    /// <summary>Whether <paramref name="text"/> is an http or https address.</summary>
    public static bool IsWebAddress(string text) =>
        Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
