// A markets widget's candles (docs\widgets.md §258; 2026-10-20: "add different chart styles to the stock
// widget, optionally a candle chart"): its range's prices as a candle for each stretch — its open, its high,
// its low and its close — worked out from its answer and nothing else, so they can be checked against saved
// answers.
//
// A stretch is the range's own (Unit): a quarter of an hour over a day, an hour over five days, a day over a
// month, and over a longer range each of the answer's points, which is a day, a week or a month already
// (MarketAsk.Interval) — as Yahoo Finance's own candles are. Yahoo's chart answers each point's open, high
// and low; the ECB's and CoinGecko's give a price alone, and a cross through the dollar has its legs' (each
// divided by the other's price then), so where a point has no open, high and low of its own its candle opens
// at the close before it and reaches as high and as low as the closes in it go. Where the chart has not the
// room for every candle, neighbours are joined (Fit), counted from the newest, so the last candle is whole.

using Karpo.Core;

namespace Karpo.Dock.Markets;

/// <summary>One candle: where it stands on the chart's axis, and its stretch's open, high, low and close.</summary>
internal readonly record struct MarketCandle(double At, double Open, double High, double Low, double Close)
{
    /// <summary>Whether it closed at or above where it opened: drawn in the up colour.</summary>
    public bool Up => Close >= Open;

    /// <summary>The same stretch of a rate the other way round: every figure one over itself, the high and the low trading places.</summary>
    public MarketCandle Inverse() => new(At, 1 / Open, 1 / Low, 1 / High, 1 / Close);
}

internal static class MarketCandles
{
    /// <summary>The stretch one candle stands for over <paramref name="range"/>; null for each of the answer's points.</summary>
    public static TimeSpan? Unit(MarketRange range) => MarketRanges.Known(range) switch
    {
        MarketRange.Day => TimeSpan.FromMinutes(15),
        MarketRange.FiveDays => TimeSpan.FromHours(1),
        MarketRange.Month => TimeSpan.FromDays(1),
        _ => null,
    };

    /// <summary>
    /// The candles of <paramref name="points"/>, each at its place on the chart's axis, oldest first: a candle
    /// for each <paramref name="unit"/> of the exchange's own clock (<paramref name="quote"/>'s), or for each
    /// point where there is none. A point with no open, high and low of its own opens at the close before it.
    /// </summary>
    public static List<MarketCandle> Of(IReadOnlyList<(double At, MarketPoint Point)> points, MarketQuote quote, TimeSpan? unit)
    {
        var candles = new List<MarketCandle>();
        long? stretch = null;
        double? previous = null;
        var (first, last, open, high, low, close) = (0d, 0d, 0d, 0d, 0d, 0d);
        for (var index = 0; index < points.Count; index++)
        {
            var (at, point) = points[index];
            if (!(point.Price > 0) || !double.IsFinite(point.Price))
            {
                continue;
            }
            var price = point.Price;
            var opened = point.HasBar ? point.Open : previous ?? price;
            var top = Math.Max(Math.Max(opened, price), point.HasBar ? point.High : 0);
            var bottom = Math.Min(Math.Min(opened, price), point.HasBar ? point.Low : double.MaxValue);
            previous = price;
            var local = quote.Local(point.At);
            long key = unit is not { } every ? index : every >= TimeSpan.FromDays(1) ? local.Date.Ticks : local.Ticks / every.Ticks;
            if (key == stretch)
            {
                (last, high, low, close) = (at, Math.Max(high, top), Math.Min(low, bottom), price);
                continue;
            }
            if (stretch is not null)
            {
                candles.Add(new MarketCandle((first + last) / 2, open, high, low, close));
            }
            (stretch, first, last, open, high, low, close) = (key, at, at, opened, top, bottom, price);
        }
        if (stretch is not null)
        {
            candles.Add(new MarketCandle((first + last) / 2, open, high, low, close));
        }
        return candles;
    }

    /// <summary>
    /// <paramref name="candles"/> with each <paramref name="every"/> neighbours joined into one — its first's
    /// open, its last's close, the highest high and the lowest low between —, counted from the newest, so the
    /// last stands for whole stretches and only the oldest may stand for fewer. The same candles for every 1.
    /// </summary>
    public static IReadOnlyList<MarketCandle> Fit(IReadOnlyList<MarketCandle> candles, int every)
    {
        if (every <= 1 || candles.Count < 2)
        {
            return candles;
        }
        var joined = new List<MarketCandle>((candles.Count / every) + 1);
        for (var end = candles.Count; end > 0; end -= every)
        {
            var start = Math.Max(0, end - every);
            var (high, low) = (double.MinValue, double.MaxValue);
            for (var index = start; index < end; index++)
            {
                (high, low) = (Math.Max(high, candles[index].High), Math.Min(low, candles[index].Low));
            }
            joined.Add(new MarketCandle((candles[start].At + candles[end - 1].At) / 2, candles[start].Open, high, low, candles[end - 1].Close));
        }
        joined.Reverse();
        return joined;
    }
}
