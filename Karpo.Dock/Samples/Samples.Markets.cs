// §635: the sample of the markets widgets: a price for whatever a widget asks — a share, a rate, a coin, over any range — its
// session open now and its chart wandering up to it from the range's start, there the moment it is asked (a store whose
// answers are settled: MarketStore's settled), never from the network. The shares a ready-made group follows have their own
// prices and names (Microsoft, Apple, NVIDIA, the S&P 500, …); any other symbol a price of its own, the same every time.

using Karpo.Core;
using Karpo.Dock.Markets;

namespace Karpo.Dock;

internal static partial class Samples
{
    /// <summary>What a markets widget tracks in the samples where it tracks nothing yet: a share, or a converter's rate.</summary>
    public static string MarketSymbol(bool converter) => converter ? "EURUSD=X" : "MSFT";

    /// <summary>The known: name, currency, price now and the move today, per cent.</summary>
    private static readonly Dictionary<string, (string Name, string Currency, double Price, double Percent)> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MSFT"] = ("Microsoft Corporation", "USD", 412.34, 1.24),
        ["AAPL"] = ("Apple Inc.", "USD", 236.16, -1.06),
        ["NVDA"] = ("NVIDIA Corporation", "USD", 181.20, 2.10),
        ["GOOGL"] = ("Alphabet Inc.", "USD", 171.85, 0.62),
        ["AMZN"] = ("Amazon.com, Inc.", "USD", 198.40, -0.48),
        ["^GSPC"] = ("S&P 500", "USD", 6417.29, 0.32),
        ["^IXIC"] = ("NASDAQ Composite", "USD", 20412.55, 0.71),
        ["^DJI"] = ("Dow Jones Industrial Average", "USD", 44318.10, -0.12),
        ["GC=F"] = ("Gold", "USD", 2651.30, 0.44),
        ["EURUSD"] = ("EUR/USD", "USD", 1.0842, 0.18),
        ["EURCHF"] = ("EUR/CHF", "CHF", 0.9415, -0.07),
        ["USDCHF"] = ("USD/CHF", "CHF", 0.8684, -0.21),
        ["GBPUSD"] = ("GBP/USD", "USD", 1.2731, 0.09),
        ["USDJPY"] = ("USD/JPY", "JPY", 151.42, 0.35),
        ["BTC-USD"] = ("Bitcoin USD", "USD", 68412.50, 2.84),
        ["ETH-USD"] = ("Ethereum USD", "USD", 3418.90, 1.92),
    };

    /// <summary>A store whose every answer is the sample's, settled the moment it is asked: nothing fetched, nothing kept.</summary>
    public static MarketStore Markets() => new(new SampleMarkets(), MarketDisk.None, settled: Quote);

    /// <summary>The sample's answer to <paramref name="ask"/>: its price now, the day's move, and the range's chart up to now.</summary>
    public static MarketQuote Quote(MarketAsk ask)
    {
        var symbol = ask.Symbol;
        var subject = symbol.EndsWith("=X", StringComparison.Ordinal) ? symbol[..^2] : symbol;
        var (name, currency, price, percent) = Known.TryGetValue(subject, out var known) ? known : Made(subject, ask);
        var utcNow = Clock.Now().ToUniversalTime();
        var previous = price / (1 + (percent / 100));
        var since = ask.Range == MarketRange.Day ? utcNow.AddHours(-4) : ask.Since(utcNow);
        const int Count = 96;
        var start = ask.Range == MarketRange.Day ? previous : previous * (1 - Math.Sign(percent == 0 ? 1 : percent) * 0.12);
        var seed = Seed(subject);
        var points = new MarketPoint[Count];
        for (var at = 0; at < Count; at++)
        {
            var share = at / (double)(Count - 1);
            var wander = ((Math.Sin((at + seed) / 7.0) * 0.6) + (Math.Sin((at + seed) / 2.3) * 0.25)) * Math.Abs(price - previous) * (1 - share);
            points[at] = new MarketPoint(since + ((utcNow - since) * share), start + ((price - start) * share) + wander);
        }
        return new MarketQuote
        {
            Symbol = symbol,
            Name = name,
            Currency = currency,
            Price = price,
            PreviousClose = previous,
            ChartPreviousClose = start,
            ChangePercent = percent,
            PriceHint = price < 10 ? 4 : 2,
            TradedUtc = utcNow.AddMinutes(-1),
            OffsetSeconds = -4 * 3600,
            // A session on now, whatever the hour: the price live, not the close's.
            SessionOpensUtc = utcNow.AddHours(-4),
            SessionClosesUtc = utcNow.AddHours(2.5),
            Points = points,
        };
    }

    /// <summary>A price of its own for a symbol the samples do not know, the same every time.</summary>
    private static (string Name, string Currency, double Price, double Percent) Made(string subject, MarketAsk ask)
    {
        var seed = Seed(subject);
        return ask.Kind switch
        {
            MarketKind.Currency => (ask.Name + "/" + ask.To, ask.To, 0.5 + (seed % 150 / 100.0), ((seed % 9) - 4) / 10.0),
            MarketKind.Crypto => (subject, ask.To, 10 + (seed % 900), ((seed % 13) - 5) / 2.0),
            _ => (subject, "USD", 20 + (seed % 480), ((seed % 11) - 4) / 3.0),
        };
    }

    private static int Seed(string subject)
    {
        var seed = 17;
        foreach (var letter in subject)
        {
            seed = (seed * 31) + letter;
        }
        return Math.Abs(seed % 10_000);
    }

    /// <summary>The sample's markets source: the sample's answer to anything asked, at once.</summary>
    private sealed class SampleMarkets : IMarketSource
    {
        public Task<MarketQuote> AskAsync(MarketAsk asked, CancellationToken cancel) => Task.FromResult(Quote(asked));
    }
}
