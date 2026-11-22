// The markets widget's reading, without a window and without the network: a saved
// answer goes in, and everything its line and its face say comes out.
//
// The fixture is a trimmed copy of a real answer from Yahoo's chart endpoint
// (query1.finance.yahoo.com/v8/finance/chart/AAPL?range=1d&interval=5m), so the
// field names and their shapes are the ones Karpo actually gets. Nothing here
// reaches the network, and no key or cookie exists to be committed.

using Karpo.Core;
using Karpo.Dock.Markets;
using Karpo.Dock.Weather;

namespace Karpo.Tests.Forecasts;

public sealed class MarketReadingTests
{
    /// <summary>New York in summer: four hours behind UTC.</summary>
    private const int Offset = -14400;

    /// <summary>The session in the fixture: 09:30 to 16:00 in New York.</summary>
    private static readonly DateTime Opens = DateTime.UnixEpoch.AddSeconds(1790170200);
    private static readonly DateTime Closes = DateTime.UnixEpoch.AddSeconds(1790193600);

    /// <summary>The last trade in it, at 15:33 in New York — half an hour before the bell.</summary>
    private static readonly DateTime Traded = DateTime.UnixEpoch.AddSeconds(1790192026);

    /// <summary>A widget over a day: what this file reads (a range's reading is MarketRangeTests').</summary>
    private static MarketOptions Apple(string symbol = "AAPL", string label = "") => new()
    {
        Symbol = symbol,
        Label = label,
        Range = MarketRange.Day,
    };

    private const string Sample = """
        {
          "chart": {
            "result": [
              {
                "meta": {
                  "currency": "USD", "symbol": "AAPL", "exchangeName": "NMS",
                  "regularMarketTime": 1790192026, "gmtoffset": -14400,
                  "timezone": "EDT", "exchangeTimezoneName": "America/New_York",
                  "regularMarketPrice": 336.16, "regularMarketChangePercent": -1.057,
                  "previousClose": 339.75, "chartPreviousClose": 339.75,
                  "longName": "Apple Inc.", "shortName": "Apple Inc.",
                  "currentTradingPeriod": {
                    "pre": { "start": 1790150400, "end": 1790170200 },
                    "regular": { "start": 1790170200, "end": 1790193600 },
                    "post": { "start": 1790193600, "end": 1790208000 }
                  }
                },
                "timestamp": [1790170200, 1790170500, 1790170800, 1790171100, 1790171400, 1790171700],
                "indicators": { "quote": [ { "close": [340.445, 338.845, null, 338.625, 337.545, 337.48] } ] }
              }
            ],
            "error": null
          }
        }
        """;

    // --- Reading the answer -----------------------------------------------------------

    [Fact]
    public void ReadsWhatTheEndpointSends()
    {
        var quote = MarketQuotes.Parse(Sample);
        Assert.Equal("AAPL", quote.Symbol);
        Assert.Equal("Apple Inc.", quote.Name);
        Assert.Equal("USD", quote.Currency);
        Assert.Equal(336.16, quote.Price);
        Assert.Equal(339.75, quote.PreviousClose);
        Assert.Equal(-1.057, quote.ChangePercent);
        Assert.Equal(Traded, quote.TradedUtc);
        Assert.Equal(Offset, quote.OffsetSeconds);
        Assert.Equal(Opens, quote.SessionOpensUtc);
        Assert.Equal(Closes, quote.SessionClosesUtc);
        // Five prices, not six: a five-minute slot with no trade in it comes back null and is left
        // out rather than drawn at zero.
        Assert.Equal(5, quote.Points.Count);
        Assert.Equal(340.445, quote.Points[0].Price);
        Assert.Equal(Opens, quote.Points[0].At);
    }

    [Fact]
    public void AnAnswerThatIsNotOneIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => MarketQuotes.Parse("{ not json"));
        Assert.Throws<InvalidDataException>(() => MarketQuotes.Parse("""{"chart":{"result":[],"error":null}}"""));
        Assert.Throws<InvalidDataException>(() => MarketQuotes.Parse("""{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found, symbol may be delisted"}}}"""));
        Assert.Throws<InvalidDataException>(() => MarketQuotes.Parse("""{"chart":{"result":[{"meta":{}}],"error":null}}"""));
        // The endpoint's own words are kept, so a delisted symbol says why.
        var said = Assert.Throws<InvalidDataException>(
            () => MarketQuotes.Parse("""{"chart":{"error":{"description":"No data found, symbol may be delisted"}}}"""));
        Assert.Contains("delisted", said.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheRequestIsOneUrlWithNothingPersonalInIt()
    {
        static string Url(string symbol) => MarketAsk.Of(new MarketOptions { Symbol = symbol }) is { } ask ? ask.YahooUrl(ask.Symbol, DateTime.UtcNow) : "";
        var url = Url("BRK-B");
        Assert.StartsWith("https://query1.finance.yahoo.com/v8/finance/chart/", url, StringComparison.Ordinal);
        Assert.Contains("BRK-B", url, StringComparison.Ordinal);
        // A widget never set to a range asks for five years, a week a point.
        Assert.Contains("range=5y&interval=1wk", url, StringComparison.Ordinal);
        // A symbol with a character that means something in a URL is escaped rather than pasted.
        Assert.Contains("%5EGSPC", Url("^GSPC"), StringComparison.Ordinal);
        Assert.DoesNotContain(" ", Url(" AAPL "), StringComparison.Ordinal);
    }

    [Fact]
    public void OnePriceIsFetchedPerSymbolWhateverItIsCalled()
    {
        static string KeyOf(MarketOptions options) => MarketAsk.Of(options)!.Value.Key;
        Assert.Equal("stock:AAPL 1d", KeyOf(Apple("AAPL")));
        Assert.Equal(KeyOf(Apple("AAPL")), KeyOf(Apple("aapl")));
        Assert.Equal(KeyOf(Apple("AAPL")), KeyOf(Apple(" AAPL ")));
        Assert.Equal(KeyOf(Apple("AAPL")), KeyOf(Apple("AAPL", label: "Apple")));
        Assert.NotEqual(KeyOf(Apple("AAPL")), KeyOf(Apple("MSFT")));
    }

    // --- The words --------------------------------------------------------------------

    [Fact]
    public void PricesAreWrittenToTheirOwnSize()
    {
        // An ordinary share to the penny, something in the thousands whole, something that trades
        // in fractions of one to four places: the number stays about as long whatever it is.
        Assert.Equal("336.16", MarketText.Price(336.16));
        Assert.Equal("1.00", MarketText.Price(1));
        Assert.Equal("0.0431", MarketText.Price(0.04312));
        Assert.Equal("41,238", MarketText.Price(41238.4));
        Assert.Equal("–", MarketText.Price(null));
        Assert.Equal("–", MarketText.Price(double.NaN));
    }

    [Fact]
    public void TheChangeAlwaysCarriesItsSign()
    {
        Assert.Equal("-1.06%", MarketText.Percent(-1.057));
        Assert.Equal("+2.50%", MarketText.Percent(2.495));
        Assert.Equal("0.00%", MarketText.Percent(0));
        Assert.Equal("0.00%", MarketText.Percent(0.001));
        Assert.Equal("", MarketText.Percent(null));
        // The narrow form is the same without its percent sign.
        Assert.Equal("-1.06", MarketText.Change(-1.057));
        Assert.Equal("", MarketText.Change(null));
        // And the direction, for the colour.
        Assert.Equal(-1, MarketText.Direction(-1.057));
        Assert.Equal(1, MarketText.Direction(0.02));
        Assert.Equal(0, MarketText.Direction(0.001));
        Assert.Equal(0, MarketText.Direction(null));
    }

    // --- The market -------------------------------------------------------------------

    [Fact]
    public void WhetherTheMarketIsOpenComesFromTheAnswerItself()
    {
        var quote = MarketQuotes.Parse(Sample);
        // Mid-session: trading, and nothing is said about a close.
        var (state, line) = MarketReading.MarketOf(quote, Opens.AddHours(2));
        Assert.Equal(MarketState.Open, state);
        Assert.Equal("", line);

        // After the bell on the same day: the price is that day's close.
        (state, line) = MarketReading.MarketOf(quote, Closes.AddMinutes(30));
        Assert.Equal(MarketState.Closed, state);
        Assert.Equal("At today's close", line);

        // The next morning, before the open.
        (state, line) = MarketReading.MarketOf(quote, Closes.AddHours(18));
        Assert.Equal(MarketState.Closed, state);
        Assert.Equal("At yesterday's close", line);

        // And over a weekend it is named by its day, in the exchange's own zone.
        (state, line) = MarketReading.MarketOf(quote, Closes.AddDays(3));
        Assert.Equal(MarketState.Closed, state);
        Assert.StartsWith("At ", line, StringComparison.Ordinal);
        Assert.EndsWith("'s close", line, StringComparison.Ordinal);
        Assert.DoesNotContain("today", line, StringComparison.Ordinal);

        // An answer that did not say cannot be guessed at.
        var quiet = MarketQuotes.Parse(Sample.Replace("\"currentTradingPeriod\"", "\"wasTradingPeriod\""));
        Assert.Equal(MarketState.Unknown, MarketReading.MarketOf(quiet, Opens.AddHours(2)).State);
    }

    // --- The whole reading ------------------------------------------------------------

    [Fact]
    public void TheReadingSaysWhatTheWidgetShows()
    {
        var reading = MarketReading.Of(Cache(), Apple(), Opens.AddHours(2), problem: null);
        Assert.Equal(MarketShows.Ready, reading.State);
        Assert.Equal("AAPL", reading.Symbol);
        Assert.Equal("Apple Inc.", reading.Name);
        Assert.Equal("336.16", reading.Price);
        Assert.Equal("USD", reading.Currency);
        Assert.Equal("-1.06%", reading.Percent);
        Assert.Equal("-1.06", reading.Change);
        Assert.Equal(-1, reading.Direction);
        Assert.Equal(MarketState.Open, reading.Market);
        Assert.False(reading.Stale);
        // The day, on an hours-since-midnight axis in the exchange's own time: 09:30 onwards.
        Assert.Equal(5, reading.Day.Count);
        Assert.Equal(9.5, reading.Day[0].At, 3);
        Assert.Equal(340.445, reading.Day[0].Price);
        // The mark is the last trade, not the clock — outside trading hours the clock moves on and
        // the price does not, and a dot at "now" would claim a trade that never happened.
        Assert.Equal(15.5628, reading.NowAt, 3);
        Assert.Equal(336.16, reading.NowValue);
        Assert.NotNull(reading.Session);
        Assert.Equal(9.5, reading.Session!.Value.From, 3);
        Assert.Equal(16, reading.Session!.Value.To, 3);
        Assert.NotEqual("", reading.Updated);
    }

    [Fact]
    public void ALabelStandsInForTheSymbolAndTheChangeCanBeTurnedOff()
    {
        var named = MarketReading.Of(Cache(), Apple(label: "Apple"), Opens.AddHours(2), null);
        Assert.Equal("Apple", named.Symbol);
        // Lower case in the settings still reads as a symbol on the bar.
        var shouted = MarketReading.Of(Cache(), Apple("aapl"), Opens.AddHours(2), null);
        Assert.Equal("AAPL", shouted.Symbol);
    }

    [Fact]
    public void WithoutASymbolItSaysSoAndWithoutAPriceItWaits()
    {
        var nothing = MarketReading.Of(null, new MarketOptions(), Opens, null);
        Assert.Equal(MarketShows.NoSymbol, nothing.State);
        Assert.Equal(S.DockMarketsNoSymbolTitle, nothing.Symbol);
        Assert.Empty(nothing.Day);

        var waiting = MarketReading.Of(null, Apple(), Opens, null);
        Assert.Equal(MarketShows.Loading, waiting.State);
        Assert.Equal("AAPL", waiting.Symbol);
        Assert.Equal("", waiting.Updated);

        var failed = MarketReading.Of(null, Apple(), Opens, "The price service can't be reached");
        Assert.Equal(MarketShows.Failed, failed.State);
        Assert.Equal("The price service can't be reached", failed.Problem);
        Assert.Equal("", failed.Price);
    }

    [Fact]
    public void APriceTooOldToTrustIsStillShownAndMarksNothing()
    {
        // The rule the weather has: a failure never empties the panel. What was fetched stays with
        // the time it was fetched, and nothing claims to be the price now.
        var old = Cache(fetchedUtc: Opens.AddDays(-2));
        var reading = MarketReading.Of(old, Apple(), Opens.AddHours(2), problem: "The price service didn't answer");
        Assert.Equal(MarketShows.Ready, reading.State);
        Assert.Equal("336.16", reading.Price);
        Assert.True(reading.Stale);
        Assert.Equal("The price service didn't answer", reading.Problem);
        Assert.Equal(MarketReading.NoMark, reading.NowAt);
        Assert.Null(reading.NowValue);
        // The day is still drawn: it is what the old answer honestly has.
        Assert.NotEmpty(reading.Day);
    }

    [Fact]
    public void EveryFailureSaysSomethingTrue()
    {
        Assert.Contains("wait", MarketStore.Explain(new MarketBusyException()), StringComparison.OrdinalIgnoreCase);
        // A gate is named as one, and Karpo does not go around it.
        Assert.Contains("sign-in", MarketStore.Explain(new MarketGateException()), StringComparison.OrdinalIgnoreCase);
        // No source has it: said as such, not as a failure to reach one.
        Assert.Equal(S.DockMarketsNotQuoted, MarketStore.Explain(new MarketNotQuotedException()));
        Assert.Contains("reached", MarketStore.Explain(new HttpRequestException("x")), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("didn't answer", MarketStore.Explain(new TaskCanceledException()), StringComparison.OrdinalIgnoreCase);
        // What the endpoint said about the symbol is passed on as it is, with a capital on it.
        Assert.Equal("Nothing for that symbol", MarketStore.Explain(new InvalidDataException("nothing for that symbol")));
    }

    [Fact]
    public void APriceSurvivesBeingWrittenOutAndReadBack()
    {
        // markets.json keeps the numbers, not the endpoint's whole payload.
        var quote = MarketQuotes.Parse(Sample);
        var again = MarketSaved.Of(quote).Read()!;
        Assert.Equal(quote.Symbol, again.Symbol);
        Assert.Equal(quote.Name, again.Name);
        Assert.Equal(quote.Currency, again.Currency);
        Assert.Equal(quote.Price, again.Price);
        Assert.Equal(quote.ChangePercent, again.ChangePercent);
        Assert.Equal(quote.TradedUtc, again.TradedUtc);
        Assert.Equal(quote.OffsetSeconds, again.OffsetSeconds);
        Assert.Equal(quote.SessionOpensUtc, again.SessionOpensUtc);
        Assert.Equal(quote.SessionClosesUtc, again.SessionClosesUtc);
        Assert.Equal(quote.Points.Count, again.Points.Count);
        Assert.Equal(quote.Points[0].At, again.Points[0].At);
        Assert.Equal(quote.Points[0].Price, again.Points[0].Price);
    }

    /// <summary>
    /// Staleness follows the refresh the person actually chose. A stock may be set to ask every
    /// five minutes, and one that is set that way goes stale after three of those — a quarter of
    /// an hour — not after three of the weather's fifteen.
    /// <para>
    /// It did the second of those. <c>MarketReading.From</c> held the setting to the stock's own
    /// 5–180, and then <c>WeatherAge.IsStale</c> held it again to 15–180 — the weather's range,
    /// which had followed the shared code across — so the 5 was thrown away and a price asked for
    /// every five minutes was shown as current for forty-five. The person who set five minutes had
    /// said, in the only way the settings let them, that they wanted it fresher than that.
    /// </para>
    /// </summary>
    [Fact]
    public void AFiveMinuteStockGoesStaleAfterFifteenMinutesAndNotFortyFive()
    {
        var fetched = Opens;
        var apple = Apple();
        // The online interval set to five minutes (2026-10-05: one interval for every online
        // widget; a stock has none of its own any more).
        var every5 = new OnlineUpdates();
        every5.Set(5);

        // Three refreshes of five minutes: fresh at fourteen, stale at fifteen.
        Assert.False(MarketReading.Of(Cache(fetched), apple, fetched.AddMinutes(14), problem: null, every5).Stale);
        Assert.True(MarketReading.Of(Cache(fetched), apple, fetched.AddMinutes(15), problem: null, every5).Stale);
        // The figure the old double clamp used. Anything at all between the two was the bug.
        Assert.True(MarketReading.Of(Cache(fetched), apple, fetched.AddMinutes(44), problem: null, every5).Stale);

        // The rule itself is the interval times three, and it is taken as given now: nothing in
        // IsStale second-guesses a caller that has already held the setting to its own range.
        Assert.Equal(5, MarketReading.Refresh(every5));
        Assert.False(WeatherAge.IsStale(fetched, fetched.AddMinutes(14), 5));
        Assert.True(WeatherAge.IsStale(fetched, fetched.AddMinutes(15), 5));

        // Half an hour: stale at an hour and a half.
        var every30 = new OnlineUpdates();
        every30.Set(30);
        Assert.False(MarketReading.Of(Cache(fetched), apple, fetched.AddMinutes(89), problem: null, every30).Stale);
        Assert.True(MarketReading.Of(Cache(fetched), apple, fetched.AddMinutes(90), problem: null, every30).Stale);

        // And the weather is where it was, because its floor is what it hands over itself: fifteen,
        // so three of them is three quarters of an hour either way.
        Assert.Equal(15, WeatherReading.Refresh(every5));
        var every15 = new OnlineUpdates();
        every15.Set(15);
        Assert.Equal(15, WeatherReading.Refresh(every15));
        Assert.Equal(30, WeatherReading.Refresh(every30));
    }

    private static MarketCache Cache(DateTime? fetchedUtc = null) => new()
    {
        FetchedUtc = fetchedUtc ?? Opens.AddHours(2),
        Key = "stock:AAPL 1d",
        Quote = MarketQuotes.Parse(Sample),
    };

    // --- Currency pairs and crypto ----------------------------------------------------

    /// <summary>
    /// A market that never shuts, written the way the endpoint writes one: the session it gives is
    /// the whole day but for the minute between one and the next. The clock here is UTC, so the
    /// hours on the axis are the timestamps themselves.
    /// </summary>
    private const string Ceaseless = """
        {
          "chart": {
            "result": [
              {
                "meta": {
                  "currency": "USD", "symbol": "EURUSD=X", "exchangeName": "CCY",
                  "regularMarketTime": 1790263800, "gmtoffset": 0,
                  "timezone": "UTC", "exchangeTimezoneName": "Europe/London",
                  "regularMarketPrice": 1.1395, "regularMarketChangePercent": 0.057,
                  "previousClose": 1.1388, "chartPreviousClose": 1.1388, "priceHint": 4,
                  "longName": "EUR/USD", "shortName": "EUR/USD",
                  "currentTradingPeriod": {
                    "regular": { "start": 1790208000, "end": 1790294340 }
                  }
                },
                "timestamp": [1790208000, 1790229600, 1790251200, 1790272800, 1790290800],
                "indicators": { "quote": [ { "close": [1.1388, 1.1401, 1.1372, 1.1395, 1.1399] } ] }
              }
            ],
            "error": null
          }
        }
        """;

    /// <summary>A rate: the pair's currencies, EUR in USD.</summary>
    private static MarketOptions Rate(double? amount = null) => new() { Symbol = "EURUSD=X", Amount = amount, Range = MarketRange.Day, Style = amount is null ? MarketStyle.Full : MarketStyle.Converter };

    /// <summary>
    /// The same endpoint serves a currency pair, and nothing about reading it is special: a rate is named
    /// by its currencies and written to its size's decimals, the amount it is said for times the rate.
    /// </summary>
    [Fact]
    public void ARateIsReadLikeAnythingElse()
    {
        var quote = MarketQuotes.Parse(Ceaseless);
        Assert.Equal("EURUSD=X", quote.Symbol);
        Assert.Equal(4, quote.PriceHint);
        Assert.Equal(5, quote.Points.Count);

        var cache = new MarketCache { FetchedUtc = Traded, Key = "currency:EURUSD 1d", Quote = quote };
        var reading = MarketReading.Of(cache, Rate(), Traded, problem: null);
        Assert.Equal("EUR/USD", reading.Symbol);
        Assert.Equal(CurrencyNames.Pair("EUR", "USD"), reading.Name);
        Assert.Equal("1.1395", reading.Price);
        Assert.Equal(1.1395, reading.Rate);
        Assert.Equal("USD", reading.Currency);
        // The change is the provider's own, measured against the previous day's close, exactly as it is
        // for a share. A pair has one; it is simply the level at the end of the last session.
        Assert.Equal("+0.06%", reading.Percent);
        Assert.Equal(1, reading.Direction);
        // A converter said for a hundred: the pair is named with it, and the price is the hundred's worth.
        var hundred = MarketReading.Of(cache, Rate(100), Traded, problem: null);
        Assert.Equal("100 EUR/USD", hundred.Symbol);
        Assert.Equal("113.95", hundred.Price);
        Assert.Equal(1.1395, hundred.Rate);
        // Never a holding: a rate's amount is what it is said for.
        Assert.Null(hundred.Holding);
    }

    /// <summary>
    /// The user's question, in a test: a widget that says EUR/USD is closed on a Tuesday afternoon
    /// would be wrong. The answer's own trading period is the whole day, and that is what says so —
    /// not a rule here about which symbols are currencies, which would be wrong for the next kind
    /// of instrument somebody adds. Crypto comes free with it: BTC-USD is described the same way.
    /// </summary>
    [Fact]
    public void AMarketThatNeverShutsIsNeverShut()
    {
        var quote = MarketQuotes.Parse(Ceaseless);
        var opens = DateTime.UnixEpoch.AddSeconds(1790208000);

        // Mid-session, at the very end of it, and in the minute between one day's and the next:
        // trading, every time, and nothing said about a close.
        foreach (var at in new[] { opens.AddHours(4), opens.AddHours(23), opens.AddHours(23.99) })
        {
            var (state, line) = MarketReading.MarketOf(quote, at);
            Assert.Equal(MarketState.Open, state);
            Assert.Equal("", line);
        }

        var reading = MarketReading.Of(
            new MarketCache { FetchedUtc = Traded, Key = "currency:EURUSD 1d", Quote = quote },
            Rate(),
            opens.AddHours(23.99),
            problem: null);
        Assert.Equal(MarketState.Open, reading.Market);
        Assert.Equal("", reading.MarketLine);
        // Nothing is shaded and no bell is drawn: a faint band over the whole graph says nothing,
        // and there is no opening bell to mark.
        Assert.Null(reading.Session);
        // The axis is the whole day, because that is when it trades.
        Assert.Equal(0, reading.Axis.From, 3);
        Assert.Equal(24, reading.Axis.To, 3);
    }

    /// <summary>
    /// An exchange that opens and shuts gets its own session and half an hour of air either side.
    /// A market that trades for six and a half hours of the twenty-four would otherwise spend three
    /// quarters of the panel saying nothing.
    /// </summary>
    [Fact]
    public void AnExchangesDayIsWhatTheGraphDraws()
    {
        var quote = MarketQuotes.Parse(Sample);
        var reading = MarketReading.Of(
            new MarketCache { FetchedUtc = Traded, Key = "stock:AAPL 1d", Quote = quote },
            Apple(),
            Traded,
            problem: null);
        Assert.Equal((9.5, 16.0), reading.Session);
        Assert.Equal(9.0, reading.Axis.From, 3);
        Assert.Equal(16.5, reading.Axis.To, 3);
    }

    /// <summary>
    /// The rule that already caught a widget changing size between loading and ready, applied to
    /// the number itself: the decimals are always written out, so a rate that moves does not
    /// change the width of the cell it is drawn in.
    /// </summary>
    [Fact]
    public void APriceKeepsItsWidthAsTheMarketMoves()
    {
        Assert.Equal("1.1000", MarketText.Price(1.1, 4));
        Assert.Equal("1.0832", MarketText.Price(1.0832, 4));
        Assert.Equal(MarketText.Price(1.1, 4).Length, MarketText.Price(1.0832, 4).Length);
        Assert.Equal("336.20", MarketText.Price(336.2, 2));
        Assert.Equal("83,924.20", MarketText.Price(83924.2, 2));
        // No hint at all: the number's own size decides, as it did before pairs existed here.
        Assert.Equal("6,417", MarketText.Price(6417.29));
        Assert.Equal("\u2013", MarketText.Price(null, 4));
    }

    /// <summary>What each type is called on its line and its face, one definition in Karpo.Core: a share by its label or its symbol, a pair by its currencies, a coin by its ticker.</summary>
    [Fact]
    public void EachTypeIsNamedTheWayAPersonSaysIt()
    {
        Assert.Equal("AAPL", MarketWords.Title(new MarketOptions { Symbol = " aapl " }));
        Assert.Equal("Apple", MarketWords.Title(new MarketOptions { Symbol = "AAPL", Label = "Apple" }));
        Assert.Null(MarketWords.Title(new MarketOptions()));
        Assert.Equal("GBP/JPY", MarketWords.Title(new MarketOptions { Symbol = "GBPJPY=X" }));
        Assert.Equal("USD/CHF", MarketWords.Title(new MarketOptions { Symbol = "CHF=X" }));
        Assert.Equal("100 JPY/CHF", MarketWords.Title(new MarketOptions { Symbol = "JPYCHF=X", Amount = 100, Style = MarketStyle.Converter }));
        Assert.Equal("JPY/CHF", MarketWords.Title(new MarketOptions { Symbol = "JPYCHF=X", Amount = 100 }));
        Assert.Equal("Yen", MarketWords.Title(new MarketOptions { Symbol = "JPYCHF=X", Label = "Yen" }));
        Assert.Equal("ETH", MarketWords.Title(new MarketOptions { Symbol = "eth-chf" }));
        Assert.Equal("Bitcoin · USD", MarketWords.Names(new MarketOptions { Symbol = "BTC-USD" }));
        Assert.Equal(CurrencyNames.Pair("EUR", "CHF"), MarketWords.Names(new MarketOptions { Symbol = "EURCHF=X" }));
    }
}
