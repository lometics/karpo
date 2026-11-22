// markets: the chart's styles (docs\widgets.md §258) — the area, the line and candles — on answers shaped as
// Yahoo gives them, open, high, low and close, nothing fetched: each point's open, high and low read, kept in
// markets.json and turned round with a rate; a day's candles a quarter of an hour each, a month's a day each,
// a longer range's the answer's own points; an answer of closes alone (the ECB's, CoinGecko's) its candles made
// of them, each opening at the last close; and neighbours joined, from the newest, where the chart is narrow.

using System.Globalization;
using System.Text;
using Karpo.Dock.Markets;

namespace Karpo.Tests.Markets;

public sealed class MarketChartTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// A chart answer as Yahoo gives one, every point's open, high, low and close: <paramref name="count"/>
    /// points <paramref name="step"/> seconds apart ending at <paramref name="end"/> (now where none is given),
    /// from <paramref name="first"/> to <paramref name="last"/> with a market's wander between and each point's
    /// stretch moving about a third of the wander; <paramref name="day"/>: the session of a market that never
    /// closes, so a day is drawn in hours.
    /// </summary>
    internal static string Bars(string symbol, int count, int step, double first, double last, double wander, string name, string currency = "USD", DateTime? end = null, bool day = false)
    {
        var until = (long)((end ?? DateTime.UtcNow) - DateTime.UnixEpoch).TotalSeconds;
        var (times, opens, highs, lows, closes) = (new StringBuilder(), new StringBuilder(), new StringBuilder(), new StringBuilder(), new StringBuilder());
        var previous = first;
        for (var index = 0; index < count; index++)
        {
            var comma = index == 0 ? "" : ",";
            times.Append(comma).Append((until - ((count - 1 - index) * (long)step)).ToString(Invariant));
            var close = first + ((last - first) * index / Math.Max(1, count - 1))
                + (wander * Math.Sin(Math.PI * index / Math.Max(1, count - 1)) * ((Math.Sin(index / 7.0) * 0.7) + (Math.Sin(index / 1.9) * 0.3)));
            var open = previous + (wander * 0.08 * Math.Sin(index * 1.7));
            var reach = wander * (0.06 + (0.1 * Math.Abs(Math.Sin(index * 2.3))));
            opens.Append(comma).Append(open.ToString("0.######", Invariant));
            highs.Append(comma).Append((Math.Max(open, close) + reach).ToString("0.######", Invariant));
            lows.Append(comma).Append((Math.Min(open, close) - (reach * 0.8)).ToString("0.######", Invariant));
            closes.Append(comma).Append(close.ToString("0.######", Invariant));
            previous = close;
        }
        var period = day ? $"\"currentTradingPeriod\": {{ \"regular\": {{ \"start\": {until - ((count - 1) * (long)step)}, \"end\": {until - ((count - 1) * (long)step) + 86340} }} }}," : "";
        return $$"""
            {
              "chart": {
                "result": [
                  {
                    "meta": {
                      "currency": "{{currency}}", "symbol": "{{symbol}}", "regularMarketTime": {{until}}, "gmtoffset": 0,
                      "regularMarketPrice": {{last.ToString("0.######", Invariant)}}, "regularMarketChangePercent": 0.42,
                      "chartPreviousClose": {{first.ToString("0.######", Invariant)}}, "previousClose": {{first.ToString("0.######", Invariant)}},
                      {{period}}
                      "longName": "{{name}}"
                    },
                    "timestamp": [{{times}}],
                    "indicators": { "quote": [ { "open": [{{opens}}], "high": [{{highs}}], "low": [{{lows}}], "close": [{{closes}}] } ] }
                  }
                ],
                "error": null
              }
            }
            """;
    }

    /// <summary>Five minutes of a pair, as Yahoo answered EURCHF=X: an open, a high, a low and a close each, one minute with no trade.</summary>
    private const string Day = """
        { "chart": { "result": [ {
          "meta": { "currency": "CHF", "symbol": "EURCHF=X", "regularMarketPrice": 0.9271, "previousClose": 0.9262, "gmtoffset": 0, "regularMarketTime": 1760918100 },
          "timestamp": [1760918400, 1760918700, 1760919000, 1760919300, 1760919600, 1760919900, 1760920200],
          "indicators": { "quote": [ {
            "open":  [0.9262, 0.9265, 0.9270, null,   0.9268, 0.9266, 0.9269],
            "high":  [0.9266, 0.9272, 0.9271, null,   0.9269, 0.9270, 0.9273],
            "low":   [0.9260, 0.9264, 0.9265, null,   0.9263, 0.9265, 0.9268],
            "close": [0.9265, 0.9270, 0.9266, 0.9267, 0.9264, 0.9269, 0.9271]
          } ] } } ], "error": null } }
        """;

    private static MarketQuote Quote() => MarketQuotes.Parse(Day);

    private static IReadOnlyList<(double, MarketPoint)> Placed(MarketQuote quote) => [.. quote.Points.Select((point, index) => ((double)index, point))];

    /// <summary>Each point's open, high and low are read beside its close; a point with a close alone has none, and is still a point.</summary>
    [Fact]
    public void EachPointHasItsOpenHighAndLow()
    {
        var quote = Quote();
        Assert.Equal(7, quote.Points.Count);
        Assert.Equal((0.9262, 0.9266, 0.9260, 0.9265), (quote.Points[0].Open, quote.Points[0].High, quote.Points[0].Low, quote.Points[0].Price));
        Assert.True(quote.Points[0].HasBar);
        Assert.False(quote.Points[3].HasBar);
        Assert.Equal(0.9267, quote.Points[3].Price);

        // Kept in markets.json and read again, the same; a file from before candles, closes alone.
        var again = MarketSaved.Of(quote).Read()!;
        Assert.Equal(quote.Points, again.Points);
        var old = MarketSaved.Of(quote);
        old.Bars = null;
        Assert.DoesNotContain(old.Read()!.Points, point => point.HasBar);

        // The pair the other way round: every figure one over itself, the high the low's.
        var turned = quote.Inverted("CHFEUR=X", "EUR").Points[0];
        Assert.Equal((1 / 0.9262, 1 / 0.9260, 1 / 0.9266, 1 / 0.9265), (turned.Open, turned.High, turned.Low, turned.Price));
        // Through the dollar: each figure by the other leg's price at that moment.
        var leg = new MarketQuote { Symbol = "USDXXX=X", Price = 2, Points = [new MarketPoint(quote.Points[0].At.AddHours(-1), 2)] };
        var cross = MarketQuote.Divided(quote, leg, "EURXXX=X", "XXX")!.Points[0];
        Assert.Equal((0.9262 / 2, 0.9266 / 2, 0.9260 / 2, 0.9265 / 2), (cross.Open, cross.High, cross.Low, cross.Price));
    }

    /// <summary>
    /// A day's candles stand for a quarter of an hour each: the first point's open, the highest high, the lowest
    /// low, the last close; a point with no open of its own opens at the close before it.
    /// </summary>
    [Fact]
    public void ADaysCandlesAreAQuarterOfAnHourEach()
    {
        var quote = Quote();
        var candles = MarketCandles.Of(Placed(quote), quote, MarketCandles.Unit(MarketRange.Day));
        // 00:00, 00:05, 00:10 | 00:15, 00:20, 00:25 | 00:30.
        Assert.Equal(3, candles.Count);
        Assert.Equal(new MarketCandle(1, 0.9262, 0.9272, 0.9260, 0.9266), candles[0]);
        Assert.Equal(new MarketCandle(4, 0.9266, 0.9270, 0.9263, 0.9269), candles[1]);
        Assert.Equal(new MarketCandle(6, 0.9269, 0.9273, 0.9268, 0.9271), candles[2]);
        Assert.True(candles[0].Up);
        Assert.Equal((TimeSpan.FromMinutes(15), TimeSpan.FromHours(1), TimeSpan.FromDays(1), (TimeSpan?)null),
            (MarketCandles.Unit(MarketRange.Day), MarketCandles.Unit(MarketRange.FiveDays), MarketCandles.Unit(MarketRange.Month), MarketCandles.Unit(MarketRange.Year)));

        // Each point its own candle over a longer range.
        Assert.Equal(7, MarketCandles.Of(Placed(quote), quote, null).Count);
    }

    /// <summary>
    /// Closes alone (the ECB's, CoinGecko's): each candle opens at the close before it and reaches as high and
    /// as low as the closes in it go — candles all the same, not a line in their place.
    /// </summary>
    [Fact]
    public void ClosesAloneMakeCandlesOfTheirOwn()
    {
        var at = new DateTime(2026, 10, 1, 15, 0, 0, DateTimeKind.Utc);
        double[] closes = [1.10, 1.12, 1.09, 1.11];
        var quote = new MarketQuote { Symbol = "EURUSD=X", Points = [.. closes.Select((close, day) => new MarketPoint(at.AddDays(day), close))], Route = MarketRoute.Ecb };
        var candles = MarketCandles.Of(Placed(quote), quote, null);
        Assert.Equal(
            [new MarketCandle(0, 1.10, 1.10, 1.10, 1.10), new MarketCandle(1, 1.10, 1.12, 1.10, 1.12), new MarketCandle(2, 1.12, 1.12, 1.09, 1.09), new MarketCandle(3, 1.09, 1.11, 1.09, 1.11)],
            candles);
        // Two days in one: the first's open, the closes' highest and lowest, the last's close.
        var month = MarketCandles.Of([.. quote.Points.Select((point, index) => ((double)index, point with { At = at.Date.AddHours(index * 5) }))], quote, TimeSpan.FromDays(1));
        Assert.Equal(new MarketCandle(1.5, 1.10, 1.12, 1.09, 1.11), Assert.Single(month));
    }

    /// <summary>Where the chart is narrow, neighbours are joined from the newest, so the last candle is whole and only the first may be short.</summary>
    [Fact]
    public void NeighboursAreJoinedFromTheNewest()
    {
        MarketCandle[] candles = [.. Enumerable.Range(0, 7).Select(index => new MarketCandle(index, 10 + index, 11 + index, 9 + index, 10.5 + index))];
        Assert.Same(candles, MarketCandles.Fit(candles, 1));
        var joined = MarketCandles.Fit(candles, 3);
        Assert.Equal([new MarketCandle(0, 10, 11, 9, 10.5), new MarketCandle(2, 11, 14, 10, 13.5), new MarketCandle(5, 14, 17, 13, 16.5)], joined);
        // The chart's least room for one candle grows with its height: a bar's face fits fewer than a tall line.
        Assert.True(MarketChart.Pitch(36) < MarketChart.Pitch(120));
        Assert.Equal(5, MarketChart.Pitch(36));
    }

    /// <summary>
    /// The reading carries the chart it is set to and its candles on the line's axis: a month of hourly
    /// answers a candle a day; the converter's swap turns them over with the rate.
    /// </summary>
    [Fact]
    public void TheReadingCarriesItsChart()
    {
        var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        var quote = MarketQuotes.Parse(Bars("EURCHF=X", 24 * 30, 3600, 0.94, 0.93, 0.004, "EUR/CHF", "CHF", now));
        var options = new MarketOptions { Symbol = "EURCHF=X", Range = MarketRange.Month, Chart = MarketChartStyle.Candles, Style = MarketStyle.Converter };
        var reading = MarketReading.Of(new MarketCache { FetchedUtc = now, Key = "currency:EURCHF 1mo", Quote = quote }, options, now, null);
        Assert.Equal(MarketChartStyle.Candles, reading.Chart);
        Assert.InRange(reading.Candles.Count, 30, 31);
        Assert.Equal(reading.Day[^1].Price, reading.Candles[^1].Close);
        var swapped = reading.Swapped();
        Assert.Equal(1 / reading.Candles[^1].Close, swapped.Candles[^1].Close, 12);
        Assert.Equal(1 / reading.Candles[^1].Low, swapped.Candles[^1].High, 12);
        // A chart nobody chose is the area, as it always was; a word a newer build wrote reads as it.
        Assert.Equal(MarketChartStyle.Area, new MarketOptions().ChartOf);
        Assert.Equal(MarketChartStyle.Area, new MarketOptions { Chart = (MarketChartStyle)7 }.ChartOf);
    }
}
