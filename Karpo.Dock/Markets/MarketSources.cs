// Where a markets widget's prices come from (docs\widgets.md §246): one question (MarketAsk) and, for each
// type, the sources that answer it, tried in order until one does.
//
//   Stock      Yahoo's chart endpoint, the share's own symbol ("MSFT", "^GSPC", "GC=F").
//   Currency   Yahoo, the pair's own symbol ("EURCHF=X"); its inverse ("CHFEUR=X", every figure one over
//              itself); through the dollar, (dollar in B) / (dollar in A), each leg its symbol or its
//              inverse, the two series met point by point; then the ECB's reference rates
//              (api.frankfurter.dev, no key), a working day each, for its thirty currencies.
//   Crypto     Yahoo, the coin in its currency ("BTC-EUR"); the coin in dollars times the dollar in its
//              currency ("ETH-USD" and "USDCHF=X": Yahoo quotes no coin in francs); then CoinGecko's
//              prices (no key), which keep a year of history for anybody without a plan.
//
// Why so many routes (checked live 2026-10-19): Yahoo quotes a pair's own symbol only for the pairs a
// market trades — every currency against the dollar both ways, nearly every one against the euro, the
// majors' crosses — and answers a cross of two others (PENNGN=X, KWDISK=X, VNDCHF=X) with a 404 at every
// range; and it quotes coins in a few currencies only (BTC-USD and SOL-EUR, but no ETH-CHF). A route that
// answered with a chart is remembered, so a quoted pair is one request and a cross two. An answer
// without a chart (fewer than two points) gives way to the next route or source that has one, and is the
// answer only where none has: the price and its change, no chart.
//
// Every request is a plain GET with Karpo's own agent and no cookies (IslandWeb). **Nothing here
// harvests a cookie or a crumb, pretends to be a browser, or reads an HTML page.** If a service answers
// with a gate — a crumb, a consent redirect, a bot check — the fetch fails like any other failure, the
// widget says so, and that is where it stops: going around a gate is a different thing from calling an
// endpoint that answers. Nothing is sent but what is asked for and its range.

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Karpo.Core;

namespace Karpo.Dock.Markets;

/// <summary>A source of prices.</summary>
internal interface IMarketSource
{
    /// <summary>
    /// The answer to <paramref name="ask"/>. Throws <see cref="MarketNotQuotedException"/> where this source
    /// has no such thing, <see cref="MarketBusyException"/> where it asked for a slower pace, and anything
    /// else for a failure.
    /// </summary>
    Task<MarketQuote> AskAsync(MarketAsk ask, CancellationToken cancel);
}

/// <summary>The source has no prices for what was asked: the next one may.</summary>
internal sealed class MarketNotQuotedException : Exception;

/// <summary>The source asked for the requests to slow down.</summary>
internal sealed class MarketBusyException : Exception;

/// <summary>The source answered with a gate rather than a price; Karpo does not go around one.</summary>
internal sealed class MarketGateException : Exception;

/// <summary>An address asked as an ordinary client: its text; null for a 404, which is "no such thing" and no failure.</summary>
internal delegate Task<string?> MarketGet(string url, CancellationToken cancel);

/// <summary>The one GET every source makes.</summary>
internal static class MarketWeb
{
    /// <summary>A plain GET with Karpo's own agent and no cookies (IslandWeb): its text, null for a 404.</summary>
    public static async Task<string?> Get(string url, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await IslandWeb.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return null;
            case HttpStatusCode.TooManyRequests:
                throw new MarketBusyException();
            // A gate rather than an answer: a refusal, or a sign-in. Reported, and nothing goes past it.
            case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                throw new MarketGateException();
        }
        response.EnsureSuccessStatusCode();
        return await IslandWeb.Text(response, 4 << 20, cancel).ConfigureAwait(false)
            ?? throw new InvalidDataException("the answer was too long");
    }
}

/// <summary>Each type's sources, by the type asked for; see the top of the file.</summary>
internal sealed class MarketSources : IMarketSource
{
    /// <summary>The dock's: the network.</summary>
    public static IMarketSource Web { get; } = new MarketSources(MarketWeb.Get);

    private readonly IMarketSource shares;
    private readonly IMarketSource rates;
    private readonly IMarketSource coins;

    /// <param name="get">How an address is asked: the network's, or a test's answers.</param>
    /// <param name="clock">The time now, for the requests that name a day; the UTC clock's where none is given.</param>
    public MarketSources(MarketGet get, Func<DateTime>? clock = null)
    {
        var chart = new YahooChart(get, clock);
        var yahooRates = new YahooRates(chart);
        shares = chart;
        rates = new FirstCharted(yahooRates, new EcbRates(get, clock));
        coins = new FirstCharted(new YahooCoins(chart, yahooRates), new CoinGecko(get, clock));
    }

    public Task<MarketQuote> AskAsync(MarketAsk ask, CancellationToken cancel) => ask.Kind switch
    {
        MarketKind.Currency => rates.AskAsync(ask, cancel),
        MarketKind.Crypto => coins.AskAsync(ask, cancel),
        _ => shares.AskAsync(ask, cancel),
    };
}

/// <summary>Sources tried in order: the first answer with a chart is the answer.</summary>
internal sealed class FirstCharted(params IMarketSource[] sources) : IMarketSource
{
    /// <summary>
    /// The first answer of the sources, in order, that has a chart (<see cref="MarketQuote.Charted"/>); else
    /// the first that has a price. Where none answers: that none quotes it, if that is all they said; else
    /// the first failure that was not "no such thing", which says more.
    /// </summary>
    public async Task<MarketQuote> AskAsync(MarketAsk ask, CancellationToken cancel)
    {
        Exception? failed = null;
        MarketQuote? bare = null;
        foreach (var source in sources)
        {
            try
            {
                var quote = await source.AskAsync(ask, cancel).ConfigureAwait(false);
                if (quote.Charted)
                {
                    return quote;
                }
                bare ??= quote;
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                if (error is not MarketNotQuotedException)
                {
                    failed ??= error;
                }
            }
        }
        return bare ?? throw failed ?? new MarketNotQuotedException();
    }
}

/// <summary>Yahoo's chart endpoint: a share's own symbol, and every route a rate or a coin takes there.</summary>
internal sealed class YahooChart(MarketGet get, Func<DateTime>? clock = null) : IMarketSource
{
    /// <summary>A share: its own symbol. What the endpoint says it cannot read is said as it is.</summary>
    public async Task<MarketQuote> AskAsync(MarketAsk ask, CancellationToken cancel) =>
        await get(ask.YahooUrl(ask.Symbol, Now()), cancel).ConfigureAwait(false) is { } json
            ? MarketQuotes.Parse(json)
            : throw new MarketNotQuotedException();

    /// <summary>
    /// <paramref name="symbol"/> over <paramref name="ask"/>'s range; null where Yahoo has no such symbol (a
    /// 404, or "No data found" in its place) or no price in it — the next route, or source, may have one.
    /// </summary>
    public async Task<MarketQuote?> Route(string symbol, MarketAsk ask, CancellationToken cancel)
    {
        if (await get(ask.YahooUrl(symbol, Now()), cancel).ConfigureAwait(false) is not { } json)
        {
            return null;
        }
        try
        {
            var quote = MarketQuotes.Parse(json);
            return quote.Now is null ? null : quote;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private DateTime Now() => (clock ?? (() => Clock.RealUtc()))();
}

/// <summary>Rates from Yahoo: the pair's symbol, its inverse, or through the dollar (see the top of the file).</summary>
internal sealed class YahooRates(YahooChart chart) : IMarketSource
{
    private readonly ConcurrentDictionary<string, MarketRoute> routes = new(StringComparer.Ordinal);

    /// <summary>The route a pair took last time; null for one not asked yet.</summary>
    internal MarketRoute? RouteOf(string from, string to) => routes.TryGetValue(from + to, out var route) ? route : null;

    /// <summary>
    /// The pair by the route that answered with a chart before, else by each route in turn: the first with a
    /// chart is remembered and the answer; else the first with a price is (no chart), and nothing is
    /// remembered, so the next ask tries them all again.
    /// </summary>
    public async Task<MarketQuote> AskAsync(MarketAsk ask, CancellationToken cancel)
    {
        var pair = ask.Name + ask.To;
        MarketQuote? bare = null;
        var tried = routes.TryGetValue(pair, out var known);
        if (tried && await By(known, ask, cancel).ConfigureAwait(false) is { } again)
        {
            if (again.Charted)
            {
                return again;
            }
            bare = again;
        }
        foreach (var route in (MarketRoute[])[MarketRoute.Direct, MarketRoute.Inverse, MarketRoute.Cross])
        {
            if (tried && route == known)
            {
                continue;
            }
            if (await By(route, ask, cancel).ConfigureAwait(false) is not { } found)
            {
                continue;
            }
            if (found.Charted)
            {
                routes[pair] = route;
                return found;
            }
            bare ??= found;
        }
        routes.TryRemove(pair, out _);
        return bare ?? throw new MarketNotQuotedException();
    }

    /// <summary>The rate by <paramref name="route"/>; null where Yahoo has none that way.</summary>
    private async Task<MarketQuote?> By(MarketRoute route, MarketAsk ask, CancellationToken cancel) => route switch
    {
        MarketRoute.Direct => (await chart.Route(Symbol(ask.Name, ask.To), ask, cancel).ConfigureAwait(false))?.As(MarketRoute.Direct, ask.Symbol, ask.To),
        MarketRoute.Inverse => (await chart.Route(Symbol(ask.To, ask.Name), ask, cancel).ConfigureAwait(false))?.Inverted(ask.Symbol, ask.To),
        MarketRoute.Cross => await Cross(ask, cancel).ConfigureAwait(false),
        _ => null,
    };

    /// <summary>Through the dollar (neither side being it): the dollar in each, the second over the first. Null where either leg is not quoted.</summary>
    private async Task<MarketQuote?> Cross(MarketAsk ask, CancellationToken cancel)
    {
        if (ask.Name == "USD" || ask.To == "USD"
            || await Leg(ask.Name, ask, cancel).ConfigureAwait(false) is not { } from
            || await Leg(ask.To, ask, cancel).ConfigureAwait(false) is not { } to)
        {
            return null;
        }
        return MarketQuote.Divided(to, from, ask.Symbol, ask.To);
    }

    /// <summary>The dollar in <paramref name="code"/> over <paramref name="ask"/>'s range, by its own symbol or its inverse; null for neither.</summary>
    internal async Task<MarketQuote?> Leg(string code, MarketAsk ask, CancellationToken cancel) =>
        await chart.Route(Symbol("USD", code), ask, cancel).ConfigureAwait(false)
        ?? (await chart.Route(Symbol(code, "USD"), ask, cancel).ConfigureAwait(false))?.Inverted(Symbol("USD", code), code);

    /// <summary>Yahoo's symbol for one <paramref name="from"/> in <paramref name="to"/>: "EURCHF=X".</summary>
    public static string Symbol(string from, string to) => $"{from}{to}=X";
}

/// <summary>
/// Coins from Yahoo: the coin in its currency ("BTC-EUR"), else the coin in dollars times the dollar in its
/// currency, the route that answered with a chart remembered.
/// </summary>
internal sealed class YahooCoins(YahooChart chart, YahooRates rates) : IMarketSource
{
    private readonly ConcurrentDictionary<string, MarketRoute> routes = new(StringComparer.Ordinal);

    public async Task<MarketQuote> AskAsync(MarketAsk ask, CancellationToken cancel)
    {
        var symbol = ask.Symbol;
        MarketQuote? bare = null;
        MarketRoute[] order = routes.TryGetValue(symbol, out var known) && known == MarketRoute.Cross
            ? [MarketRoute.Cross, MarketRoute.Direct]
            : [MarketRoute.Direct, MarketRoute.Cross];
        foreach (var route in order)
        {
            var found = route == MarketRoute.Direct
                ? (await chart.Route(symbol, ask, cancel).ConfigureAwait(false))?.As(MarketRoute.Direct, symbol, ask.To)
                : await Cross(ask, cancel).ConfigureAwait(false);
            if (found is null)
            {
                continue;
            }
            if (found.Charted)
            {
                routes[symbol] = route;
                return found;
            }
            bare ??= found;
        }
        return bare ?? throw new MarketNotQuotedException();
    }

    /// <summary>The coin in dollars over the dollar's worth of its currency; null for a coin in dollars, or a leg not quoted.</summary>
    private async Task<MarketQuote?> Cross(MarketAsk ask, CancellationToken cancel)
    {
        if (ask.To == "USD"
            || await chart.Route(Coins.Symbol(ask.Name, "USD"), ask, cancel).ConfigureAwait(false) is not { } coin
            || await rates.Leg(ask.To, ask, cancel).ConfigureAwait(false) is not { } dollar)
        {
            return null;
        }
        return MarketQuote.Divided(coin, dollar.Inverted(YahooRates.Symbol(ask.To, "USD"), "USD"), ask.Symbol, ask.To);
    }
}

/// <summary>
/// The ECB's reference rates through api.frankfurter.dev (no key): one rate a working day, published about
/// 16:00 CET, for the thirty currencies it lists, any against any.
/// </summary>
internal sealed class EcbRates(MarketGet get, Func<DateTime>? clock = null) : IMarketSource
{
    /// <summary>What the ECB publishes (api.frankfurter.dev/v1/currencies, 2026-10-19).</summary>
    public static readonly IReadOnlySet<string> Codes = new HashSet<string>(StringComparer.Ordinal)
    {
        "AUD", "BRL", "CAD", "CHF", "CNY", "CZK", "DKK", "EUR", "GBP", "HKD", "HUF", "IDR", "ILS", "INR", "ISK",
        "JPY", "KRW", "MXN", "MYR", "NOK", "NZD", "PHP", "PLN", "RON", "SEK", "SGD", "THB", "TRY", "USD", "ZAR",
    };

    /// <summary>The most points kept of an answer: five years of working days, every fourth.</summary>
    internal const int MostPoints = 400;

    /// <summary>The request for <paramref name="ask"/> at <paramref name="utcNow"/>: from a week before the range began, so a day has its previous.</summary>
    public static string Url(MarketAsk ask, DateTime utcNow) =>
        string.Create(CultureInfo.InvariantCulture, $"https://api.frankfurter.dev/v1/{ask.Since(utcNow).Date.AddDays(-7):yyyy-MM-dd}..?base={ask.Name}&symbols={ask.To}");

    public async Task<MarketQuote> AskAsync(MarketAsk ask, CancellationToken cancel)
    {
        if (ask.Kind != MarketKind.Currency || !Codes.Contains(ask.Name) || !Codes.Contains(ask.To))
        {
            throw new MarketNotQuotedException();
        }
        var now = (clock ?? (() => Clock.RealUtc()))();
        var json = await get(Url(ask, now), cancel).ConfigureAwait(false) ?? throw new MarketNotQuotedException();
        return Parse(json, ask, now);
    }

    /// <summary>
    /// An answer read: each day's rate at 15:00 UTC, about when it is published; the range's days as its
    /// points — the last two at least, so a day has a line —, every n-th of them where there are more than
    /// <see cref="MostPoints"/>; the previous day's rate, and the last before the range as its opening.
    /// </summary>
    internal static MarketQuote Parse(string json, MarketAsk ask, DateTime utcNow)
    {
        var days = new List<MarketPoint>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("rates", out var rates) || rates.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("no rates in the answer");
            }
            foreach (var day in rates.EnumerateObject())
            {
                if (DateTime.TryParseExact(day.Name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)
                    && day.Value.ValueKind == JsonValueKind.Object
                    && day.Value.TryGetProperty(ask.To, out var value)
                    && value.ValueKind == JsonValueKind.Number
                    && value.TryGetDouble(out var rate)
                    && double.IsFinite(rate) && rate > 0)
                {
                    days.Add(new MarketPoint(DateTime.SpecifyKind(date.Date.AddHours(15), DateTimeKind.Utc), rate));
                }
            }
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("the answer could not be read", error);
        }
        days.Sort((one, other) => one.At.CompareTo(other.At));
        if (days.Count == 0)
        {
            throw new MarketNotQuotedException();
        }
        var start = ask.Since(utcNow);
        var inside = days.FindIndex(day => day.At >= start);
        var first = Math.Max(0, inside < 0 ? days.Count - 1 : Math.Min(inside, days.Count - 2));
        var range = days.GetRange(first, days.Count - first);
        return new MarketQuote
        {
            Symbol = ask.Symbol,
            Currency = ask.To,
            Price = days[^1].Price,
            PreviousClose = days.Count > 1 ? days[^2].Price : null,
            ChartPreviousClose = first > 0 ? days[first - 1].Price : days[0].Price,
            TradedUtc = days[^1].At,
            Points = Thinned(range, MostPoints),
            Route = MarketRoute.Ecb,
        };
    }

    /// <summary>Every n-th of <paramref name="points"/>, the last always among them, where there are more than <paramref name="most"/>.</summary>
    internal static List<MarketPoint> Thinned(List<MarketPoint> points, int most)
    {
        var step = (int)Math.Ceiling(points.Count / (double)most);
        return step <= 1 ? points : [.. points.Where((_, index) => (points.Count - 1 - index) % step == 0)];
    }
}

/// <summary>
/// CoinGecko's prices (api.coingecko.com, no key): a coin by its id, in any currency it lists. Without a
/// plan it keeps a year of history (checked 2026-10-19: more is a 401), so a longer range is the price
/// and its day alone, no chart. A coin not among the common ones is found by its ticker once.
/// </summary>
internal sealed class CoinGecko(MarketGet get, Func<DateTime>? clock = null) : IMarketSource
{
    /// <summary>The most days of history it gives anybody without a plan.</summary>
    internal const int MostDays = 365;

    private readonly ConcurrentDictionary<string, string> ids = new(StringComparer.Ordinal);

    /// <summary>The request for <paramref name="id"/>'s prices in <paramref name="currency"/> over the last <paramref name="days"/>.</summary>
    public static string Url(string id, string currency, int days) =>
        string.Create(CultureInfo.InvariantCulture, $"https://api.coingecko.com/api/v3/coins/{Uri.EscapeDataString(id)}/market_chart?vs_currency={currency.ToLowerInvariant()}&days={days}");

    /// <summary>The search for a ticker's id.</summary>
    public static string SearchUrl(string code) => "https://api.coingecko.com/api/v3/search?query=" + Uri.EscapeDataString(code);

    public async Task<MarketQuote> AskAsync(MarketAsk ask, CancellationToken cancel)
    {
        if (ask.Kind != MarketKind.Crypto || await Id(ask.Name, cancel).ConfigureAwait(false) is not { } id)
        {
            throw new MarketNotQuotedException();
        }
        var now = (clock ?? (() => Clock.RealUtc()))();
        var span = (now - ask.Since(now)).TotalDays;
        var charted = span <= MostDays;
        var days = charted ? Math.Clamp((int)Math.Ceiling(span), 1, MostDays) : 1;
        var json = await get(Url(id, ask.To, days), cancel).ConfigureAwait(false) ?? throw new MarketNotQuotedException();
        return Parse(json, ask, charted);
    }

    /// <summary>
    /// An answer read: its prices oldest first, every n-th past <see cref="EcbRates.MostPoints"/>; the last
    /// the price, the one a day before it the previous close, the first the opening. Where the range is
    /// longer than it keeps (<paramref name="charted"/> false), the price and its day alone: no chart.
    /// </summary>
    internal static MarketQuote Parse(string json, MarketAsk ask, bool charted = true)
    {
        var points = new List<MarketPoint>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("prices", out var prices) || prices.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("no prices in the answer");
            }
            foreach (var pair in prices.EnumerateArray())
            {
                if (pair.ValueKind == JsonValueKind.Array && pair.GetArrayLength() >= 2
                    && pair[0].TryGetDouble(out var ms) && pair[1].ValueKind == JsonValueKind.Number && pair[1].TryGetDouble(out var price)
                    && double.IsFinite(price) && price > 0 && ms > 0)
                {
                    points.Add(new MarketPoint(DateTime.UnixEpoch.AddMilliseconds(ms), price));
                }
            }
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("the answer could not be read", error);
        }
        points.Sort((one, other) => one.At.CompareTo(other.At));
        if (points.Count == 0)
        {
            throw new MarketNotQuotedException();
        }
        var last = points[^1];
        var dayBefore = points.LastOrDefault(point => point.At <= last.At.AddDays(-1));
        return new MarketQuote
        {
            Symbol = ask.Symbol,
            Name = Coins.Name(ask.Name) ?? "",
            Currency = ask.To,
            Price = last.Price,
            PreviousClose = dayBefore.Price > 0 ? dayBefore.Price : points[0].Price,
            ChartPreviousClose = charted ? points[0].Price : null,
            TradedUtc = last.At,
            Points = charted ? EcbRates.Thinned(points, EcbRates.MostPoints) : [],
            Route = MarketRoute.CoinGecko,
        };
    }

    /// <summary>CoinGecko's id for <paramref name="code"/>: a common coin's, else the first coin its search finds with that ticker; null for none.</summary>
    private async Task<string?> Id(string code, CancellationToken cancel)
    {
        if (Coins.Find(code) is { GeckoId.Length: > 0 } common)
        {
            return common.GeckoId;
        }
        if (ids.TryGetValue(code, out var known))
        {
            return known;
        }
        if (await get(SearchUrl(code), cancel).ConfigureAwait(false) is not { } json)
        {
            return null;
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("coins", out var coins) && coins.ValueKind == JsonValueKind.Array)
            {
                foreach (var coin in coins.EnumerateArray())
                {
                    if (coin.TryGetProperty("symbol", out var symbol) && symbol.ValueKind == JsonValueKind.String
                        && string.Equals(symbol.GetString(), code, StringComparison.OrdinalIgnoreCase)
                        && coin.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && id.GetString() is { Length: > 0 } found)
                    {
                        ids[code] = found;
                        return found;
                    }
                }
            }
        }
        catch (JsonException)
        {
        }
        return null;
    }
}
