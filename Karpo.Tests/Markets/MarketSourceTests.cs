// markets: where each type's prices come from (docs\widgets.md §246), on answers shaped as the services
// give them (Yahoo's chart, the ECB's through Frankfurter and CoinGecko's, as they answered 2026-10-19),
// nothing fetched: a share its own symbol; a rate its own symbol, its inverse, a cross through the dollar
// and the ECB's day rates; a coin its own symbol, the coin in dollars times the dollar in its currency, and
// CoinGecko's prices — each route that answered remembered, a chart preferred to none.

using System.Globalization;
using Karpo.Dock.Markets;

namespace Karpo.Tests.Markets;

public sealed class MarketSourceTests
{
    private static readonly DateTime Start = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// A chart answer as Yahoo gives one: its meta — the price, the previous close and the close before the
    /// range, in <paramref name="currency"/> — and a close every five minutes from <see cref="Start"/>,
    /// <paramref name="closes"/>.
    /// </summary>
    internal static string Chart(string symbol, string currency, string type, double price, double previous, double opening, params double?[] closes)
    {
        var times = string.Join(",", closes.Select((_, index) => ((long)(Start.AddMinutes(5 * index) - DateTime.UnixEpoch).TotalSeconds).ToString(CultureInfo.InvariantCulture)));
        var values = string.Join(",", closes.Select(close => close is { } some ? some.ToString(CultureInfo.InvariantCulture) : "null"));
        var seconds = (long)(Start - DateTime.UnixEpoch).TotalSeconds;
        var meta = string.Create(CultureInfo.InvariantCulture,
            $"\"currency\":\"{currency}\",\"symbol\":\"{symbol}\",\"exchangeName\":\"CCY\",\"instrumentType\":\"{type}\",\"regularMarketPrice\":{price},"
            + $"\"previousClose\":{previous},\"chartPreviousClose\":{opening},\"priceHint\":4,\"regularMarketTime\":{seconds + 7200}");
        var period = string.Create(CultureInfo.InvariantCulture, $"\"start\":{seconds},\"end\":{seconds + 86400}");
        return "{\"chart\":{\"result\":[{\"meta\":{" + meta + ",\"currentTradingPeriod\":{\"regular\":{" + period + "}}},"
            + "\"timestamp\":[" + times + "],\"indicators\":{\"quote\":[{\"close\":[" + values + "]}]}}],\"error\":null}}";
    }

    /// <summary>A pair's chart: its currency the symbol's second.</summary>
    internal static string Pair(string symbol, double price, double previous, double opening, params double?[] closes) =>
        Chart(symbol, symbol[3..6], "CURRENCY", price, previous, opening, closes);

    /// <summary>A coin's chart: "BTC-USD" in its currency.</summary>
    internal static string Coin(string symbol, double price, double previous, double opening, params double?[] closes) =>
        Chart(symbol, symbol[^3..], "CRYPTOCURRENCY", price, previous, opening, closes);

    /// <summary>
    /// The services: Yahoo's chart answers by the symbol in its address, the ECB's and CoinGecko's by
    /// what the address asks — none for the rest, as a 404 — and what was asked counted, in order.
    /// </summary>
    private sealed class Service(Dictionary<string, string> answers, Func<string, Exception?>? fails = null)
    {
        public List<string> Asked { get; } = [];

        /// <summary>CoinGecko's address as asked: "gecko:search", or "gecko:" and the coin's id.</summary>
        private static string GeckoAsked(string[] path) => "gecko:" + (path[3] == "search" ? "search" : path[4]);

        public Task<string?> Get(string url, CancellationToken cancel)
        {
            var asked = url.Contains("/v8/finance/chart/", StringComparison.Ordinal)
                ? Uri.UnescapeDataString(new Uri(url).Segments[^1])
                : url.Contains("coingecko", StringComparison.Ordinal) ? GeckoAsked(new Uri(url).AbsolutePath.Split('/'))
                : url.Contains("frankfurter", StringComparison.Ordinal) ? "ecb"
                : url;
            Asked.Add(asked);
            if (fails?.Invoke(asked) is { } failure)
            {
                throw failure;
            }
            return Task.FromResult(answers.GetValueOrDefault(asked));
        }
    }

    private static MarketAsk Rate(string from, string to, Karpo.Core.MarketRange range = Karpo.Core.MarketRange.Day) => new(Karpo.Core.MarketKind.Currency, from, to, range, 0);

    private static MarketAsk Crypto(string coin, string to, Karpo.Core.MarketRange range = Karpo.Core.MarketRange.Day) => new(Karpo.Core.MarketKind.Crypto, coin, to, range, 0);

    private static MarketAsk Share(string symbol) => new(Karpo.Core.MarketKind.Stock, symbol, "", Karpo.Core.MarketRange.Day, 0);

    /// <summary>A rate's or a coin's price now, to compare.</summary>
    private static double Now(MarketQuote quote) => quote.Now!.Value;

    // --- Routed by type ------------------------------------------------------------------------------

    /// <summary>Each type asks its own: a share its symbol and nothing else, a rate its pair, a coin its coin.</summary>
    [Fact]
    public async Task EachTypeAsksItsOwnSources()
    {
        var service = new Service(new()
        {
            ["MSFT"] = Chart("MSFT", "USD", "EQUITY", 412.34, 410, 400, 410, 412.34),
            ["EURCHF=X"] = Pair("EURCHF=X", 0.9269, 0.9255, 0.9255, 0.9250, 0.9269),
            ["BTC-USD"] = Coin("BTC-USD", 81917, 81000, 80000, 81000, 81917),
        });
        var sources = new MarketSources(service.Get, () => Start);
        Assert.Equal(412.34, Now(await sources.AskAsync(Share("MSFT"), default)));
        Assert.Equal(["MSFT"], service.Asked);
        service.Asked.Clear();
        Assert.Equal(0.9269, Now(await sources.AskAsync(Rate("EUR", "CHF"), default)));
        Assert.Equal(["EURCHF=X"], service.Asked);
        service.Asked.Clear();
        var coin = await sources.AskAsync(Crypto("BTC", "USD"), default);
        Assert.Equal((81917.0, MarketRoute.Direct, "BTC-USD", "USD"), (Now(coin), coin.Route, coin.Symbol, coin.Currency));
        Assert.Equal(["BTC-USD"], service.Asked);
    }

    /// <summary>A share Yahoo has no symbol for is said to be quoted by nobody; its own words where it said some.</summary>
    [Fact]
    public async Task AShareNobodyQuotesSaysSo()
    {
        var sources = new MarketSources(new Service([]).Get, () => Start);
        await Assert.ThrowsAsync<MarketNotQuotedException>(() => sources.AskAsync(Share("NOPE"), default));
        var said = new MarketSources(new Service(new() { ["ODD"] = """{"chart":{"error":{"description":"Invalid symbol"}}}""" }).Get, () => Start);
        Assert.Equal("Invalid symbol", (await Assert.ThrowsAsync<InvalidDataException>(() => said.AskAsync(Share("ODD"), default))).Message);
        Assert.Equal(Karpo.S.DockMarketsNotQuoted, MarketStore.Explain(new MarketNotQuotedException()));
    }

    // --- Rates ---------------------------------------------------------------------------------------

    /// <summary>Yahoo's answer for a symbol it does not have, which it gives with a 404 (and so the get as null).</summary>
    private const string NoData = """{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found, symbol may be delisted"}}}""";

    private static YahooRates Rates(Service service) => new(new YahooChart(service.Get, () => Start));

    [Fact]
    public async Task APairYahooQuotesIsItsOwnSymbolOneRequest()
    {
        var yahoo = new Service(new() { ["EURCHF=X"] = Pair("EURCHF=X", 0.9269, 0.9255, 0.9255, 0.9250, 0.9260, 0.9269) });
        var quote = await Rates(yahoo).AskAsync(Rate("EUR", "CHF"), default);
        Assert.Equal((MarketRoute.Direct, 0.9269, 0.9255), (quote.Route, Now(quote), quote.PreviousClose));
        Assert.Equal([0.9250, 0.9260, 0.9269], quote.Points.Select(point => point.Price));
        Assert.Equal(["EURCHF=X"], yahoo.Asked);
        Assert.Equal(0.9269 / 0.9255 * 100 - 100, MarketReading.ChangeOver(quote, Karpo.Core.MarketRange.Day)!.Value, 9);
    }

    [Fact]
    public async Task APairQuotedOnlyTheOtherWayIsItsInverse()
    {
        var yahoo = new Service(new() { ["EURUAH=X"] = Pair("EURUAH=X", 48.0, 50.0, 40.0, 40.0, 48.0) });
        var quote = await Rates(yahoo).AskAsync(Rate("UAH", "EUR", Karpo.Core.MarketRange.Month), default);
        Assert.Equal((MarketRoute.Inverse, "UAHEUR=X", "EUR"), (quote.Route, quote.Symbol, quote.Currency));
        Assert.Equal(1 / 48.0, Now(quote), 12);
        Assert.Equal(1 / 50.0, quote.PreviousClose!.Value, 12);
        Assert.Equal(1 / 40.0, quote.ChartPreviousClose!.Value, 12);
        Assert.Equal([1 / 40.0, 1 / 48.0], quote.Points.Select(point => point.Price));
        Assert.Equal(["UAHEUR=X", "EURUAH=X"], yahoo.Asked);
        // The change a day is worked out from the inverted closes, the inverse's own figure meaning the other way.
        Assert.Null(quote.ChangePercent);
        Assert.Equal((1 / 48.0 - 1 / 50.0) / (1 / 50.0) * 100, MarketReading.ChangeOver(quote, Karpo.Core.MarketRange.Day)!.Value, 9);
    }

    /// <summary>
    /// The pair the stock widget said "no data" for (2026-10-19): Yahoo has neither PENNGN=X nor NGNPEN=X,
    /// so it is the dollar in naira over the dollar in soles, point by point, each naira point with the
    /// sol's last at or before it — and the route is remembered, so the next ask is the two legs alone.
    /// </summary>
    [Fact]
    public async Task APairYahooDoesNotQuoteIsWorkedOutThroughTheDollar()
    {
        var yahoo = new Service(new()
        {
            ["PENNGN=X"] = NoData,
            ["USDPEN=X"] = Pair("USDPEN=X", 3.40, 3.50, 3.60, 3.50, null, 3.40),
            ["USDNGN=X"] = Pair("USDNGN=X", 1325.0, 1330.0, 1300.0, 1330.0, 1328.0, 1325.0),
        });
        var source = Rates(yahoo);
        var quote = await source.AskAsync(Rate("PEN", "NGN"), default);
        Assert.Equal(MarketRoute.Cross, quote.Route);
        Assert.Equal(1325.0 / 3.40, Now(quote), 9);
        Assert.Equal(1330.0 / 3.50, quote.PreviousClose!.Value, 9);
        Assert.Equal(1300.0 / 3.60, quote.ChartPreviousClose!.Value, 9);
        // The sol had no close at the second point: the naira's is met with the sol's last before it.
        Assert.Equal([1330.0 / 3.50, 1328.0 / 3.50, 1325.0 / 3.40], quote.Points.Select(point => Math.Round(point.Price, 9)), new Rounded());
        Assert.Equal(["PENNGN=X", "NGNPEN=X", "USDPEN=X", "USDNGN=X"], yahoo.Asked);
        Assert.Equal(MarketRoute.Cross, source.RouteOf("PEN", "NGN"));

        yahoo.Asked.Clear();
        _ = await source.AskAsync(Rate("PEN", "NGN"), default);
        Assert.Equal(["USDPEN=X", "USDNGN=X"], yahoo.Asked);
    }

    /// <summary>A leg Yahoo has only as its inverse ("SEKUSD=X") is inverted before it is divided.</summary>
    [Fact]
    public async Task ALegQuotedTheOtherWayIsInvertedFirst()
    {
        var yahoo = new Service(new()
        {
            ["SEKUSD=X"] = Pair("SEKUSD=X", 0.10, 0.10, 0.10, 0.10),
            ["USDNOK=X"] = Pair("USDNOK=X", 9.5, 9.5, 9.5, 9.5),
        });
        var quote = await Rates(yahoo).AskAsync(Rate("SEK", "NOK"), default);
        Assert.Equal(MarketRoute.Cross, quote.Route);
        Assert.Equal(0.95, Now(quote), 9);
    }

    [Fact]
    public async Task APairNobodyQuotesSaysSo()
    {
        var yahoo = new Service([]);
        await Assert.ThrowsAsync<MarketNotQuotedException>(() => Rates(yahoo).AskAsync(Rate("KWD", "ISK"), default));
        // Against the dollar there is no cross to make: its own symbol and its inverse are all there is.
        yahoo.Asked.Clear();
        await Assert.ThrowsAsync<MarketNotQuotedException>(() => Rates(yahoo).AskAsync(Rate("USD", "KWD"), default));
        Assert.Equal(["USDKWD=X", "KWDUSD=X"], yahoo.Asked);
    }

    private static string Frankfurter(string to, params (string Day, double Rate)[] days) =>
        "{\"amount\":1.0,\"base\":\"CHF\",\"rates\":{" + string.Join(",", days.Select(day => string.Create(CultureInfo.InvariantCulture, $"\"{day.Day}\":{{\"{to}\":{day.Rate}}}"))) + "}}";

    [Fact]
    public void TheEcbsDaysAreTheRangesPointsTheDayBeforeItsOpening()
    {
        var json = Frankfurter("PLN", ("2026-10-09", 4.70), ("2026-10-12", 4.709), ("2026-10-13", 4.7002), ("2026-10-16", 4.7131), ("2026-10-19", 4.7201));
        var ask = Rate("CHF", "PLN", Karpo.Core.MarketRange.FiveDays);
        var quote = EcbRates.Parse(json, ask, new DateTime(2026, 10, 19, 18, 0, 0, DateTimeKind.Utc));
        // Five days reach back to the 12th at six in the evening: its rate, at three, is the opening.
        Assert.Equal((MarketRoute.Ecb, 4.7201, 4.7131, 4.709), (quote.Route, Now(quote), quote.PreviousClose, quote.ChartPreviousClose));
        Assert.Equal([4.7002, 4.7131, 4.7201], quote.Points.Select(point => point.Price));
        Assert.Equal(new DateTime(2026, 10, 19, 15, 0, 0, DateTimeKind.Utc), quote.TradedUtc);
        // A day: the last two days, so it has a line, and the change against the day before.
        var day = EcbRates.Parse(json, ask with { Range = Karpo.Core.MarketRange.Day }, new DateTime(2026, 10, 19, 18, 0, 0, DateTimeKind.Utc));
        Assert.Equal([4.7131, 4.7201], day.Points.Select(point => point.Price));
        Assert.Contains("api.frankfurter.dev/v1/2026-10-05..?base=CHF&symbols=PLN", EcbRates.Url(ask, new DateTime(2026, 10, 19)), StringComparison.Ordinal);
    }

    [Fact]
    public void FiveYearsOfDaysAreThinnedTheNewestKept()
    {
        var days = Enumerable.Range(0, 1300).Select(index => (new DateTime(2021, 10, 19).AddDays(index).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 4 + (index / 1000.0))).ToArray();
        var quote = EcbRates.Parse(Frankfurter("PLN", days), Rate("CHF", "PLN", Karpo.Core.MarketRange.FiveYears), new DateTime(2025, 5, 22, 18, 0, 0, DateTimeKind.Utc));
        Assert.InRange(quote.Points.Count, 300, EcbRates.MostPoints);
        Assert.Equal(Now(quote), quote.Points[^1].Price);
    }

    [Fact]
    public async Task TheEcbHasThirtyCurrenciesAndNoOthers()
    {
        var asked = 0;
        var ecb = new EcbRates((_, _) => { asked++; return Task.FromResult<string?>(null); });
        await Assert.ThrowsAsync<MarketNotQuotedException>(() => ecb.AskAsync(Rate("PEN", "NGN", Karpo.Core.MarketRange.Month), default));
        Assert.Equal(0, asked);
        Assert.Equal(30, EcbRates.Codes.Count);
        Assert.All(EcbRates.Codes, code => Assert.Contains(code, Karpo.Core.Currencies.Codes));
    }

    // --- The chain ------------------------------------------------------------------------------------

    private sealed class Fixed(Func<MarketAsk, MarketQuote> answer) : IMarketSource
    {
        public int Asked { get; private set; }

        public Task<MarketQuote> AskAsync(MarketAsk ask, CancellationToken cancel)
        {
            Asked++;
            return Task.FromResult(answer(ask));
        }
    }

    private static MarketQuote Bare(MarketAsk ask, MarketRoute route = MarketRoute.Ecb) => new() { Symbol = ask.Symbol, Currency = ask.To, Price = 4.72, Route = route };

    private static MarketQuote Charted(MarketAsk ask, MarketRoute route = MarketRoute.Direct) => new()
    {
        Symbol = ask.Symbol, Currency = ask.To, Price = 0.9269, Route = route,
        Points = [new MarketPoint(DateTime.UnixEpoch, 0.9250), new MarketPoint(DateTime.UnixEpoch.AddMinutes(5), 0.9269)],
    };

    [Fact]
    public async Task WhereYahooCannotAnswerTheNextDoes()
    {
        // A refusal, a gate, an outage, or no such pair: each falls to the next source.
        foreach (var failure in new Exception[] { new MarketBusyException(), new MarketGateException(), new HttpRequestException("down"), new MarketNotQuotedException() })
        {
            var next = new Fixed(ask => Bare(ask));
            var quote = await new FirstCharted(new Fixed(_ => throw failure), next).AskAsync(Rate("EUR", "CHF"), default);
            Assert.Equal(MarketRoute.Ecb, quote.Route);
        }
        // Yahoo answering with a chart, the next is not asked.
        var unasked = new Fixed(ask => Bare(ask));
        var yahoo = await new FirstCharted(new Fixed(ask => Charted(ask)), unasked).AskAsync(Rate("EUR", "CHF"), default);
        Assert.Equal((MarketRoute.Direct, 0), (yahoo.Route, unasked.Asked));
    }

    /// <summary>
    /// An answer with no chart — a symbol Yahoo keeps no history for — gives way to one with a chart, and is
    /// the answer only where no source has one: the price and its change, rather than nothing.
    /// </summary>
    [Fact]
    public async Task AnAnswerWithNoChartGivesWayToOneWithAChart()
    {
        var bare = new Fixed(ask => Bare(ask, MarketRoute.Direct));
        Assert.Equal(MarketRoute.Ecb, (await new FirstCharted(bare, new Fixed(ask => Charted(ask, MarketRoute.Ecb))).AskAsync(Rate("EUR", "CHF"), default)).Route);
        var alone = await new FirstCharted(bare, new Fixed(_ => throw new MarketNotQuotedException())).AskAsync(Rate("EUR", "CHF"), default);
        Assert.Equal((4.72, false), (Now(alone), alone.Charted));

        // Yahoo's own routes the same: a symbol answering one point gives way to the cross, which is remembered.
        var yahoo = new Service(new()
        {
            ["CHFVND=X"] = Pair("CHFVND=X", 31000, 31000, 31000, 31000),
            ["USDVND=X"] = Pair("USDVND=X", 26000, 26000, 26000, 26000, 26100),
            ["USDCHF=X"] = Pair("USDCHF=X", 0.80, 0.80, 0.80, 0.80, 0.81),
        });
        var source = Rates(yahoo);
        var quote = await source.AskAsync(Rate("VND", "CHF", Karpo.Core.MarketRange.FiveYears), default);
        Assert.Equal((MarketRoute.Cross, 2), (quote.Route, quote.Points.Count));
        Assert.Equal(MarketRoute.Cross, source.RouteOf("VND", "CHF"));
    }

    [Fact]
    public async Task WhereNoneAnswersTheFailureThatSaysMostStands()
    {
        await Assert.ThrowsAsync<MarketBusyException>(() => new FirstCharted(new Fixed(_ => throw new MarketBusyException()), new Fixed(_ => throw new MarketNotQuotedException())).AskAsync(Rate("EUR", "CHF"), default));
        await Assert.ThrowsAsync<MarketNotQuotedException>(() => new FirstCharted(new Fixed(_ => throw new MarketNotQuotedException()), new Fixed(_ => throw new MarketNotQuotedException())).AskAsync(Rate("EUR", "CHF"), default));
    }

    // --- Coins ---------------------------------------------------------------------------------------

    /// <summary>
    /// Yahoo quotes no coin in francs (ETH-CHF is a 404, 2026-10-19): the coin in dollars over the dollar's
    /// worth of a franc, point by point — remembered, so the next ask goes there first.
    /// </summary>
    [Fact]
    public async Task ACoinInACurrencyYahooDoesNotQuoteIsWorkedOutThroughTheDollar()
    {
        var yahoo = new Service(new()
        {
            ["ETH-USD"] = Coin("ETH-USD", 4000, 3900, 3500, 3900, 3950, 4000),
            ["USDCHF=X"] = Pair("USDCHF=X", 0.80, 0.81, 0.82, 0.81, 0.80),
        });
        var sources = new MarketSources(yahoo.Get, () => Start);
        var quote = await sources.AskAsync(Crypto("ETH", "CHF"), default);
        Assert.Equal((MarketRoute.Cross, "ETH-CHF", "CHF"), (quote.Route, quote.Symbol, quote.Currency));
        Assert.Equal(4000 * 0.80, Now(quote), 9);
        Assert.Equal(3900 * 0.81, quote.PreviousClose!.Value, 9);
        Assert.Equal([3900 * 0.81, 3950 * 0.80, 4000 * 0.80], quote.Points.Select(point => point.Price), new Rounded());
        Assert.Equal(["ETH-CHF", "ETH-USD", "USDCHF=X"], yahoo.Asked);

        yahoo.Asked.Clear();
        _ = await sources.AskAsync(Crypto("ETH", "CHF"), default);
        Assert.Equal(["ETH-USD", "USDCHF=X"], yahoo.Asked);
    }

    private static string Gecko(params (DateTime At, double Price)[] prices) =>
        "{\"prices\":[" + string.Join(",", prices.Select(point => string.Create(CultureInfo.InvariantCulture, $"[{(long)(point.At - DateTime.UnixEpoch).TotalMilliseconds},{point.Price}]"))) + "],\"market_caps\":[],\"total_volumes\":[]}";

    /// <summary>
    /// Where Yahoo cannot answer — a gate, here — CoinGecko does, by the coin's id: its prices the chart,
    /// the last the price, the one a day before it the previous close; the line says where it is from.
    /// </summary>
    [Fact]
    public async Task WhereYahooCannotAnswerACoinCoinGeckoDoes()
    {
        var gecko = Gecko((Start.AddDays(-2), 60000), (Start.AddDays(-1), 61000), (Start.AddHours(-12), 61500), (Start, 62000));
        var service = new Service(new() { ["gecko:bitcoin"] = gecko }, asked => asked.EndsWith("-USD", StringComparison.Ordinal) ? new MarketGateException() : null);
        var quote = await new MarketSources(service.Get, () => Start).AskAsync(Crypto("BTC", "USD", Karpo.Core.MarketRange.FiveDays), default);
        Assert.Equal((MarketRoute.CoinGecko, 62000.0, 61000.0, 60000.0, 4), (quote.Route, Now(quote), quote.PreviousClose, quote.ChartPreviousClose, quote.Points.Count));
        Assert.Equal("Bitcoin", quote.Name);
        Assert.Contains("gecko:bitcoin", service.Asked);
        Assert.Contains("/coins/bitcoin/market_chart?vs_currency=chf&days=7", CoinGecko.Url("bitcoin", "CHF", 7), StringComparison.Ordinal);
        var reading = MarketReading.Of(new MarketCache { FetchedUtc = Start, Key = "crypto:BTC-USD 5d", Quote = quote }, new Karpo.Core.MarketOptions { Symbol = "BTC-USD", Range = Karpo.Core.MarketRange.FiveDays }, Start, null);
        Assert.Equal(Karpo.S.DockMarketsCoinGecko, reading.MarketLine);
    }

    /// <summary>CoinGecko keeps a year for anybody without a plan: a longer range is the price and its day, no chart.</summary>
    [Fact]
    public void CoinGeckoPastAYearIsThePriceAlone()
    {
        var json = Gecko((Start.AddDays(-1), 61000), (Start, 62000));
        var quote = CoinGecko.Parse(json, Crypto("BTC", "USD", Karpo.Core.MarketRange.FiveYears), charted: false);
        Assert.Equal((62000.0, 61000.0, false), (Now(quote), quote.PreviousClose!.Value, quote.Charted));
        Assert.Null(quote.ChartPreviousClose);
    }

    /// <summary>A coin not among the common ones is found by its ticker once, and its id kept.</summary>
    [Fact]
    public async Task ACoinNotAmongTheCommonOnesIsFoundByItsTicker()
    {
        var service = new Service(new()
        {
            ["gecko:search"] = """{"coins":[{"id":"pepe-unrelated","symbol":"PEPEX"},{"id":"pepe","symbol":"PEPE","name":"Pepe"}]}""",
            ["gecko:pepe"] = Gecko((Start.AddDays(-1), 0.000009), (Start, 0.00001)),
        }, asked => asked.EndsWith("-USD", StringComparison.Ordinal) ? new HttpRequestException("down") : null);
        var gecko = new CoinGecko(service.Get, () => Start);
        var quote = await gecko.AskAsync(Crypto("PEPE", "USD"), default);
        Assert.Equal(0.00001, Now(quote), 12);
        _ = await gecko.AskAsync(Crypto("PEPE", "USD"), default);
        Assert.Single(service.Asked, asked => asked == "gecko:search");
        // A coin's price below a cent is written to its first figures.
        Assert.Equal("0.0000100", MarketText.Price(0.00001, MarketText.Places(Karpo.Core.MarketKind.Crypto, 0.00001, null)));
        // Even where Yahoo's hint says fewer: SHIB came with five, which read "0.00000".
        Assert.Equal("0.0000100", MarketText.Price(0.00001, MarketText.Places(Karpo.Core.MarketKind.Crypto, 0.00001, 5)));
    }

    /// <summary>Rates compared to nine places.</summary>
    private sealed class Rounded : IEqualityComparer<double>
    {
        public bool Equals(double one, double other) => Math.Abs(Math.Round(one, 9) - Math.Round(other, 9)) < 1e-9;

        public int GetHashCode(double value) => Math.Round(value, 6).GetHashCode();
    }
}
