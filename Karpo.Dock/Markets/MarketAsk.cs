// What a markets widget asks for (docs\widgets.md §246): what it tracks — a share's symbol, a pair of
// currencies, a coin and its currency — over how far back its chart goes (MarketRange), as one question
// to the sources of its type (MarketSources.cs).
//
// Yahoo's chart endpoint, which every type asks first, takes the ranges 1d, 5d, 1mo, 3mo, 6mo, 1y, 2y,
// 5y, 10y, ytd and max (its answer lists them as meta.validRanges), and the intervals 1m to 90m, 1h, 1d,
// 5d, 1wk, 1mo and 3mo. Minutes are kept only so far back — a minute for a week, five to thirty minutes
// for sixty days, an hour for two years — so each range is asked at the finest interval that reaches
// back that far and still draws a few hundred points at most. "The last 3 years" is no range of the
// endpoint's, so it is asked as two moments, period1 and period2 (checked 2026-10-03: MSFT, 3 years at
// 1wk, 158 points). A source asked for days rather than a range (the ECB's, CoinGecko's) asks from when
// the range begins (MarketRanges.Since).

using System.Globalization;
using Karpo.Core;

namespace Karpo.Dock.Markets;

/// <summary>
/// One widget's question: of <paramref name="Kind"/>, <paramref name="Name"/> — a share's symbol, a
/// rate's first currency, a coin — in <paramref name="To"/> (a rate's and a coin's currency, empty for a
/// share), over <paramref name="Range"/>, and for <see cref="MarketRange.Years"/> how many.
/// </summary>
internal readonly record struct MarketAsk(MarketKind Kind, string Name, string To, MarketRange Range, int Years)
{
    /// <summary>
    /// What <paramref name="options"/> ask for, by what their symbol is (<see cref="MarketSymbols"/>), over a
    /// known range; null where there is nothing to ask: no symbol yet.
    /// </summary>
    public static MarketAsk? Of(MarketOptions options)
    {
        var range = MarketRanges.Known(options.Range);
        var years = range == MarketRange.Years ? MarketRanges.Years(options.RangeYears) : 0;
        if (MarketSymbols.Pair(options.Symbol) is ({ } from, { } to))
        {
            return new MarketAsk(MarketKind.Currency, from, to, range, years);
        }
        if (MarketSymbols.Coin(options.Symbol) is ({ } coin, { } currency))
        {
            return new MarketAsk(MarketKind.Crypto, coin, currency, range, years);
        }
        var symbol = (options.Symbol ?? "").Trim().ToUpperInvariant();
        return symbol.Length == 0 ? null : new MarketAsk(MarketKind.Stock, symbol, "", range, years);
    }

    /// <summary>
    /// What it is fetched and kept once for, however many widgets ask: its type, what it is and its
    /// range's word ("stock:MSFT 5y", "currency:EURCHF 1mo", "crypto:BTC-USD 3y").
    /// </summary>
    public string Key => $"{Kind switch { MarketKind.Currency => "currency", MarketKind.Crypto => "crypto", _ => "stock" }}:{Subject} {Word}";

    /// <summary>What it is, as the key and the price service write it: "MSFT", "EURCHF", "BTC-USD".</summary>
    private string Subject => Kind switch
    {
        MarketKind.Currency => Name + To,
        MarketKind.Crypto => Coins.Symbol(Name, To),
        _ => Name,
    };

    /// <summary>The price service's symbol for it: "MSFT", "EURCHF=X", "BTC-USD" — its page, and its first route.</summary>
    public string Symbol => Kind == MarketKind.Currency ? Name + To + "=X" : Subject;

    /// <summary>The same question of another symbol: a route of a rate or a coin (its inverse, a leg through the dollar).</summary>
    public MarketAsk Of(string name, string to) => this with { Name = name, To = to };

    /// <summary>The range in the endpoint's words, and "3y" for the last three years.</summary>
    public string Word => Range switch
    {
        MarketRange.FiveDays => "5d",
        MarketRange.Month => "1mo",
        MarketRange.SixMonths => "6mo",
        MarketRange.YearToDate => "ytd",
        MarketRange.Year => "1y",
        MarketRange.FiveYears => "5y",
        MarketRange.Max => "max",
        MarketRange.Years => $"{Years.ToString(CultureInfo.InvariantCulture)}y",
        _ => "1d",
    };

    /// <summary>
    /// The interval the chart is asked at: the finest the endpoint keeps that far back that still draws
    /// a few hundred points — five minutes for a day (78 in a session), fifteen for five days (130), an
    /// hour for a month (about 150), a day for six months to a year (126 to 252), a week for five years
    /// (261) and a month for all of it. The last N years: a day up to two, a week up to ten, a month past.
    /// </summary>
    public string Interval => Range switch
    {
        MarketRange.Day => "5m",
        MarketRange.FiveDays => "15m",
        MarketRange.Month => "60m",
        MarketRange.SixMonths or MarketRange.YearToDate or MarketRange.Year => "1d",
        MarketRange.FiveYears => "1wk",
        MarketRange.Max => "1mo",
        _ => Years <= 2 ? "1d" : Years <= 10 ? "1wk" : "1mo",
    };

    /// <summary>When the range begins, seen at <paramref name="utcNow"/> (<see cref="MarketRanges.Since"/>).</summary>
    public DateTime Since(DateTime utcNow) => MarketRanges.Since(Range, Years, utcNow);

    /// <summary>
    /// The request's query: the range and the interval, or for the last N years the moments the range
    /// begins and ends — from midnight UTC N years before <paramref name="utcNow"/>'s day, so one day's
    /// asks are one request, to now.
    /// </summary>
    public string Query(DateTime utcNow)
    {
        if (Range != MarketRange.Years)
        {
            return $"range={Word}&interval={Interval}";
        }
        var from = new DateTimeOffset(utcNow.Date.AddYears(-Years), TimeSpan.Zero).ToUnixTimeSeconds();
        var to = new DateTimeOffset(utcNow, TimeSpan.Zero).ToUnixTimeSeconds();
        return string.Create(CultureInfo.InvariantCulture, $"period1={from}&period2={to}&interval={Interval}");
    }

    /// <summary>Yahoo's chart request for <paramref name="symbol"/> over the range.</summary>
    public string YahooUrl(string symbol, DateTime utcNow) =>
        "https://query1.finance.yahoo.com/v8/finance/chart/" + Uri.EscapeDataString(symbol) + "?" + Query(utcNow);
}
