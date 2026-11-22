// markets: each type's sources against the real services (docs\widgets.md §246) — Yahoo's chart endpoint,
// the ECB's rates through api.frankfurter.dev and CoinGecko's prices — as the dock asks them. Off the
// network and in CI it does nothing: it runs only with KARPO_LIVE_TESTS=1, and says what each answered by
// which route.

using Karpo.Dock.Markets;
using Xunit.Abstractions;

namespace Karpo.Tests.Markets;

/// <summary>A fact that asks the real services: run only with KARPO_LIVE_TESTS=1.</summary>
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("KARPO_LIVE_TESTS") != "1")
        {
            Skip = "Asks Yahoo, the ECB and CoinGecko over the network; set KARPO_LIVE_TESTS=1.";
        }
    }
}

public sealed class MarketLiveTests(ITestOutputHelper output)
{
    /// <summary>
    /// Forty-three pairs: the person's own (PEN/NGN), the majors and their crosses, and exotic pairs Yahoo
    /// quotes no symbol for, each of which must answer by one route or another.
    /// </summary>
    private static readonly (string From, string To)[] Pairs =
    [
        ("EUR", "CHF"), ("USD", "JPY"), ("GBP", "INR"), ("CHF", "PLN"), ("AUD", "NZD"), ("EUR", "TRY"), ("USD", "BRL"),
        ("CNY", "JPY"), ("SEK", "NOK"), ("ZAR", "EUR"), ("PEN", "NGN"), ("KWD", "ISK"), ("VND", "CHF"), ("EUR", "USD"),
        ("GBP", "USD"), ("USD", "CAD"), ("EUR", "GBP"), ("CHF", "JPY"), ("AUD", "USD"), ("NZD", "CHF"), ("USD", "MXN"),
        ("EUR", "HUF"), ("EUR", "CZK"), ("PLN", "HUF"), ("DKK", "SEK"), ("USD", "HKD"), ("SGD", "MYR"), ("THB", "IDR"),
        ("KRW", "JPY"), ("TWD", "USD"), ("INR", "PKR"), ("AED", "SAR"), ("QAR", "KWD"), ("EGP", "MAD"), ("ILS", "EUR"),
        ("ARS", "CLP"), ("COP", "PEN"), ("UAH", "PLN"), ("RON", "CZK"), ("NGN", "ZAR"), ("ISK", "NOK"), ("PHP", "VND"),
        ("MYR", "IDR"), ("CHF", "USD"),
    ];

    /// <summary>The coins and the currencies they are priced in: the dollar and the euro Yahoo quotes, the franc and the yen it does not.</summary>
    private static readonly (string Coin, string To)[] Coins =
    [
        ("BTC", "USD"), ("ETH", "USD"), ("SOL", "USD"), ("XRP", "USD"), ("DOGE", "USD"), ("BTC", "EUR"), ("SOL", "EUR"),
        ("BTC", "CHF"), ("ETH", "CHF"), ("BTC", "JPY"), ("ADA", "GBP"), ("SHIB", "USD"),
    ];

    private static readonly string[] Shares = ["MSFT", "AAPL", "^GSPC", "GC=F", "VOD.L", "NESN.SW", "SPY"];

    /// <summary>Asks <paramref name="ask"/> of the dock's sources; what it answered, or why not, said and kept.</summary>
    private async Task<MarketQuote?> Ask(MarketAsk ask, List<string> failed, List<string> bare)
    {
        try
        {
            var quote = await MarketSources.Web.AskAsync(ask, default);
            var change = MarketReading.ChangeOver(quote, ask.Range);
            output.WriteLine($"{ask.Symbol,-10} {ask.Word,-3} {quote.Route,-9} {MarketText.Price(quote.Now, MarketText.Places(ask.Kind, quote.Now, quote.PriceHint)),14} {quote.Currency,-4} {quote.Points.Count,4} points {MarketText.Percent(change),8}");
            if (quote.Now is not > 0)
            {
                failed.Add($"{ask.Symbol} {ask.Word}: no price");
            }
            if (!quote.Charted)
            {
                bare.Add($"{ask.Symbol} {ask.Word}");
            }
            return quote;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            output.WriteLine($"{ask.Symbol,-10} {ask.Word,-3} FAILED {error.GetType().Name} {error.Message}");
            failed.Add($"{ask.Symbol} {ask.Word}: {error.GetType().Name}");
            return null;
        }
    }

    /// <summary>
    /// Every pair has a rate over a day and over five years, and nearly every one a chart: a few thin markets
    /// keep no history at a day's five minutes, and those show the rate and its change alone.
    /// </summary>
    [LiveFact]
    public async Task EveryPairAnswersOverADayAndOverFiveYears()
    {
        List<string> failed = [];
        List<string> bare = [];
        foreach (var (from, to) in Pairs)
        {
            foreach (var range in new[] { Karpo.Core.MarketRange.Day, Karpo.Core.MarketRange.FiveYears })
            {
                _ = await Ask(new MarketAsk(Karpo.Core.MarketKind.Currency, from, to, range, 0), failed, bare);
            }
        }
        output.WriteLine($"No chart: {string.Join(", ", bare)}");
        Assert.True(failed.Count == 0, string.Join("\n", failed));
        Assert.True(bare.Count <= Pairs.Length / 10, $"No chart for {string.Join(", ", bare)}");
    }

    /// <summary>Every coin has a price over a day, a month and five years, in a currency Yahoo quotes it in or through the dollar.</summary>
    [LiveFact]
    public async Task EveryCoinAnswersInItsCurrency()
    {
        List<string> failed = [];
        List<string> bare = [];
        foreach (var (coin, to) in Coins)
        {
            foreach (var range in new[] { Karpo.Core.MarketRange.Day, Karpo.Core.MarketRange.Month, Karpo.Core.MarketRange.FiveYears })
            {
                _ = await Ask(new MarketAsk(Karpo.Core.MarketKind.Crypto, coin, to, range, 0), failed, bare);
            }
        }
        output.WriteLine($"No chart: {string.Join(", ", bare)}");
        Assert.True(failed.Count == 0, string.Join("\n", failed));
        Assert.True(bare.Count <= Coins.Length / 4, $"No chart for {string.Join(", ", bare)}");
    }

    /// <summary>Shares, an index, a future and listings abroad over a day and five years, by their own symbols.</summary>
    [LiveFact]
    public async Task EveryShareAnswers()
    {
        List<string> failed = [];
        List<string> bare = [];
        foreach (var symbol in Shares)
        {
            foreach (var range in new[] { Karpo.Core.MarketRange.Day, Karpo.Core.MarketRange.FiveYears })
            {
                _ = await Ask(new MarketAsk(Karpo.Core.MarketKind.Stock, symbol, "", range, 0), failed, bare);
            }
        }
        Assert.True(failed.Count == 0, string.Join("\n", failed));
    }

    /// <summary>The fallbacks on their own, as they are asked where Yahoo cannot answer: the ECB's pairs, CoinGecko's coins.</summary>
    [LiveFact]
    public async Task TheFallbacksAnswer()
    {
        var ecb = await new EcbRates(MarketWeb.Get).AskAsync(new MarketAsk(Karpo.Core.MarketKind.Currency, "CHF", "PLN", Karpo.Core.MarketRange.Month, 0), default);
        output.WriteLine($"CHF/PLN 1mo ECB {ecb.Now} {ecb.Points.Count} points");
        Assert.Equal(MarketRoute.Ecb, ecb.Route);
        Assert.InRange(ecb.Points.Count, 15, 25);

        var gecko = new CoinGecko(MarketWeb.Get);
        foreach (var (coin, to, range) in new[] { ("BTC", "CHF", Karpo.Core.MarketRange.Month), ("ETH", "EUR", Karpo.Core.MarketRange.Day), ("SOL", "USD", Karpo.Core.MarketRange.FiveYears), ("PEPE", "USD", Karpo.Core.MarketRange.Month) })
        {
            var ask = new MarketAsk(Karpo.Core.MarketKind.Crypto, coin, to, range, 0);
            MarketQuote quote;
            try
            {
                quote = await gecko.AskAsync(ask, default);
            }
            catch (MarketBusyException)
            {
                // Its keyless service answers a handful a minute: the dock waits out a busy answer, so does this.
                output.WriteLine($"{coin}-{to} {range} CoinGecko busy, asked again in a minute");
                await Task.Delay(TimeSpan.FromSeconds(65));
                quote = await gecko.AskAsync(ask, default);
            }
            output.WriteLine($"{coin}-{to} {range} CoinGecko {quote.Now} {quote.Points.Count} points");
            Assert.Equal(MarketRoute.CoinGecko, quote.Route);
            Assert.True(quote.Now > 0);
            Assert.Equal(range != Karpo.Core.MarketRange.FiveYears, quote.Charted);
        }
    }
}
