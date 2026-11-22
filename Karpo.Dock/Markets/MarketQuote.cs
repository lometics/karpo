// One answer of a markets widget's sources (docs\widgets.md §246), whatever it tracks: the price now, the
// closes a change is measured from, the session it trades in where it has one, and the range's prices —
// a share's from Yahoo's chart endpoint as it answered, a rate's or a coin's the same or worked out from
// other answers (its inverse, a cross through the dollar) or from the ECB's or CoinGecko's.
//
// Yahoo's endpoint is undocumented, which is the whole reason this file is careful: every field is read
// as "if it is there and it is a number", a missing one degrades to nothing rather than to a wrong
// number. It is read with JsonDocument rather than a generated contract: the payload is large, mostly
// unread, and not ours to pin down — a shape that changes should cost a field, not a parse.

using System.Text.Json;

namespace Karpo.Dock.Markets;

/// <summary>
/// One price in a chart's series, at its own moment: its close, and where the answer gave them, the
/// stretch's open, high and low (Yahoo's chart does; the ECB's and CoinGecko's give a price alone, NaN).
/// </summary>
internal readonly record struct MarketPoint(DateTime At, double Price, double Open = double.NaN, double High = double.NaN, double Low = double.NaN)
{
    /// <summary>Whether it has its open, high and low: a candle of its own.</summary>
    public bool HasBar => Open > 0 && High > 0 && Low > 0 && double.IsFinite(Open) && double.IsFinite(High) && double.IsFinite(Low);

    /// <summary>One over itself, the high and the low trading places: the same stretch of a rate the other way round.</summary>
    public MarketPoint Inverse() => new(At, 1 / Price, 1 / Open, 1 / Low, 1 / High);

    /// <summary>Divided by <paramref name="under"/>, the open, high and low with it: a leg of a cross met with the other's price at that moment.</summary>
    public MarketPoint Over(double under) => new(At, Price / under, Open / under, High / under, Low / under);

    /// <summary>§494: times <paramref name="factor"/>, the open, high and low with it: the same stretch in another currency.</summary>
    public MarketPoint Times(double factor) => new(At, Price * factor, Open * factor, High * factor, Low * factor);
}

/// <summary>How an answer was come by: which source, and which way round.</summary>
internal enum MarketRoute
{
    /// <summary>Yahoo's own symbol for it.</summary>
    Direct,

    /// <summary>Yahoo's symbol for the pair the other way round, every figure one over itself.</summary>
    Inverse,

    /// <summary>Through the dollar: two of Yahoo's answers divided.</summary>
    Cross,

    /// <summary>The ECB's daily reference rates.</summary>
    Ecb,

    /// <summary>CoinGecko's prices.</summary>
    CoinGecko,
}

/// <summary>
/// What one answer said: the quote, where it was traded, and the range's prices. Every number is
/// optional, because the services are not ours.
/// </summary>
internal sealed class MarketQuote
{
    public required string Symbol { get; init; }

    /// <summary>The company's, fund's or coin's name, where the answer carried one.</summary>
    public string Name { get; init; } = "";

    /// <summary>"USD", "EUR": the service's own code. Karpo does not guess a symbol for it.</summary>
    public string Currency { get; init; } = "";

    public double? Price { get; init; }

    /// <summary>The previous day's close: what a day's change is against.</summary>
    public double? PreviousClose { get; init; }

    /// <summary>The close before the chart's first point (meta.chartPreviousClose): what a longer range's change is against.</summary>
    public double? ChartPreviousClose { get; init; }

    /// <summary>How far it has moved since the previous close, in per cent, where the service said.</summary>
    public double? ChangePercent { get; init; }

    /// <summary>
    /// How many decimals this instrument is quoted to, as the service says (`priceHint`): 2 for an ordinary
    /// share, more for a penny stock or a coin of small units. Karpo does not decide this per kind — a rule
    /// of its own would be wrong for the next instrument somebody adds.
    /// </summary>
    public int? PriceHint { get; init; }

    /// <summary>When the price was traded, and how far the exchange's own clock is from UTC.</summary>
    public DateTime? TradedUtc { get; init; }

    public int OffsetSeconds { get; init; }

    /// <summary>The session the answer described, so the market's state comes from the data.</summary>
    public DateTime? SessionOpensUtc { get; init; }

    public DateTime? SessionClosesUtc { get; init; }

    /// <summary>The chart's prices — the day's, or the range's — oldest first.</summary>
    public IReadOnlyList<MarketPoint> Points { get; init; } = [];

    public MarketRoute Route { get; init; }

    /// <summary>The exchange's own local time for an instant.</summary>
    public DateTime Local(DateTime utc) => utc.AddSeconds(OffsetSeconds);

    /// <summary>
    /// Whether it has a chart: two points at least. Yahoo keeps no history for some symbols (CHFVND=X,
    /// AEDSAR=X answer five years as one point, checked 2026-10-19), so an answer without one gives way to
    /// the next route or source that has one, and is the answer only where none has.
    /// </summary>
    public bool Charted => Points.Count >= 2;

    /// <summary>The price now: the answer's, else its last point's; null for neither.</summary>
    public double? Now => Positive(Price) ?? (Points.Count > 0 ? Positive(Points[^1].Price) : null);

    /// <summary>The same with another route, symbol and currency: what a route made of it says it is.</summary>
    public MarketQuote As(MarketRoute route, string symbol, string currency) => new()
    {
        Symbol = symbol,
        Name = Name,
        Currency = currency,
        Price = Price,
        PreviousClose = PreviousClose,
        ChartPreviousClose = ChartPreviousClose,
        ChangePercent = ChangePercent,
        PriceHint = PriceHint,
        TradedUtc = TradedUtc,
        OffsetSeconds = OffsetSeconds,
        SessionOpensUtc = SessionOpensUtc,
        SessionClosesUtc = SessionClosesUtc,
        Points = Points,
        Route = route,
    };

    /// <summary>The same rate the other way round, every figure one over itself: <paramref name="symbol"/>, in <paramref name="currency"/>.</summary>
    public MarketQuote Inverted(string symbol, string currency) => new()
    {
        Symbol = symbol,
        Currency = currency,
        Price = Over(Now),
        PreviousClose = Over(PreviousClose),
        ChartPreviousClose = Over(ChartPreviousClose),
        TradedUtc = TradedUtc,
        OffsetSeconds = OffsetSeconds,
        SessionOpensUtc = SessionOpensUtc,
        SessionClosesUtc = SessionClosesUtc,
        Points = [.. Points.Where(point => point.Price > 0).Select(point => point.Inverse())],
        Route = MarketRoute.Inverse,
    };

    /// <summary>
    /// §494: the same in another currency, every figure times <paramref name="factor"/> — the rate now, of the
    /// listed currency in <paramref name="currency"/> (<see cref="MarketMoney.Factor"/>): the price, the closes
    /// and each point with its open, high and low, so the chart and its candles read in that currency and a
    /// change in per cent is the same. The service's decimals go with its currency.
    /// </summary>
    public MarketQuote Times(double factor, string currency) => new()
    {
        Symbol = Symbol,
        Name = Name,
        Currency = currency,
        Price = Price * factor,
        PreviousClose = PreviousClose * factor,
        ChartPreviousClose = ChartPreviousClose * factor,
        ChangePercent = ChangePercent,
        TradedUtc = TradedUtc,
        OffsetSeconds = OffsetSeconds,
        SessionOpensUtc = SessionOpensUtc,
        SessionClosesUtc = SessionClosesUtc,
        Points = [.. Points.Select(point => point.Times(factor))],
        Route = Route,
    };

    /// <summary>
    /// <paramref name="over"/> divided by <paramref name="under"/>: now, at the previous close and before
    /// the range; and at each point of the first, its open, high and low with it, by the second's last price at
    /// or before it (its first where it has none before) — how a cross through the dollar is made, as a dealer
    /// makes it. Null where
    /// either has no price.
    /// </summary>
    public static MarketQuote? Divided(MarketQuote over, MarketQuote under, string symbol, string currency)
    {
        if (over.Now is not { } top || under.Now is not { } bottom)
        {
            return null;
        }
        var points = new List<MarketPoint>(over.Points.Count);
        var below = under.Points;
        var at = 0;
        foreach (var point in over.Points)
        {
            while (at + 1 < below.Count && below[at + 1].At <= point.At)
            {
                at++;
            }
            if (below.Count > 0 && below[at].Price != 0)
            {
                points.Add(point.Over(below[at].Price));
            }
        }
        return new MarketQuote
        {
            Symbol = symbol,
            Name = over.Name,
            Currency = currency,
            Price = top / bottom,
            PreviousClose = over.PreviousClose / under.PreviousClose,
            ChartPreviousClose = over.ChartPreviousClose / under.ChartPreviousClose,
            TradedUtc = over.TradedUtc,
            OffsetSeconds = over.OffsetSeconds,
            Points = points,
            Route = MarketRoute.Cross,
        };
    }

    private static double? Over(double? value) => value is { } some && some != 0 && double.IsFinite(some) ? 1 / some : null;

    private static double? Positive(double? value) => value is { } some && double.IsFinite(some) && some > 0 ? some : null;
}

/// <summary>Reading Yahoo's chart answer. The only place the endpoint's shape is known.</summary>
internal static class MarketQuotes
{
    /// <summary>
    /// The most points a series is kept to, so one answer cannot grow without bound: more than any range is
    /// asked for (MarketAsk.Interval), and where an answer has more, its newest are kept.
    /// </summary>
    public const int MostPoints = 800;

    /// <summary>
    /// The quote in <paramref name="json"/>. Throws <see cref="InvalidDataException"/> when the answer is
    /// not one, or says it has nothing for that symbol — a widget with no price says so rather than
    /// showing the last number it happened to have.
    /// </summary>
    public static MarketQuote Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("the answer could not be read", error);
        }
        using (document)
        {
            if (!document.RootElement.TryGetProperty("chart", out var chart))
            {
                throw new InvalidDataException("no chart in the answer");
            }
            if (chart.TryGetProperty("error", out var failed) && failed.ValueKind is not JsonValueKind.Null)
            {
                throw new InvalidDataException(Sentence(failed) ?? "the symbol was refused");
            }
            if (!chart.TryGetProperty("result", out var results)
                || results.ValueKind != JsonValueKind.Array
                || results.GetArrayLength() == 0)
            {
                throw new InvalidDataException("nothing for that symbol");
            }
            var result = results[0];
            if (!result.TryGetProperty("meta", out var meta))
            {
                throw new InvalidDataException("no quote in the answer");
            }
            var symbol = Text(meta, "symbol") ?? "";
            if (symbol.Length == 0)
            {
                throw new InvalidDataException("the answer named no symbol");
            }
            var offset = (int)(Number(meta, "gmtoffset") ?? 0);
            return new MarketQuote
            {
                Symbol = symbol,
                Name = Text(meta, "longName") ?? Text(meta, "shortName") ?? "",
                Currency = Text(meta, "currency") ?? "",
                Price = Number(meta, "regularMarketPrice"),
                PreviousClose = Number(meta, "previousClose") ?? Number(meta, "chartPreviousClose"),
                ChartPreviousClose = Number(meta, "chartPreviousClose"),
                ChangePercent = Number(meta, "regularMarketChangePercent"),
                PriceHint = Number(meta, "priceHint") is { } hint and >= 0 and <= 8 ? (int)hint : null,
                TradedUtc = Moment(Number(meta, "regularMarketTime")),
                OffsetSeconds = offset,
                SessionOpensUtc = Moment(Session(meta, "start")),
                SessionClosesUtc = Moment(Session(meta, "end")),
                Points = Series(result),
            };
        }
    }

    /// <summary>The chart's prices: the timestamps beside the closes, and each stretch's open, high and low where given, skipping the gaps.</summary>
    private static List<MarketPoint> Series(JsonElement result)
    {
        var points = new List<MarketPoint>();
        if (!result.TryGetProperty("timestamp", out var times) || times.ValueKind != JsonValueKind.Array)
        {
            return points;
        }
        if (!result.TryGetProperty("indicators", out var indicators)
            || !indicators.TryGetProperty("quote", out var quotes)
            || quotes.ValueKind != JsonValueKind.Array
            || quotes.GetArrayLength() == 0
            || !quotes[0].TryGetProperty("close", out var closes)
            || closes.ValueKind != JsonValueKind.Array)
        {
            return points;
        }
        var count = Math.Min(times.GetArrayLength(), closes.GetArrayLength());
        for (var index = 0; index < count; index++)
        {
            // A minute with no trade in it comes back null: it is left out rather than drawn at zero.
            if (times[index].ValueKind != JsonValueKind.Number
                || closes[index].ValueKind != JsonValueKind.Number
                || !times[index].TryGetDouble(out var seconds)
                || !closes[index].TryGetDouble(out var price)
                || Moment(seconds) is not { } at)
            {
                continue;
            }
            points.Add(new MarketPoint(at, price, At(quotes[0], "open", index), At(quotes[0], "high", index), At(quotes[0], "low", index)));
        }
        // The newest are the ones worth keeping: a chart ends at now.
        if (points.Count > MostPoints)
        {
            points.RemoveRange(0, points.Count - MostPoints);
        }
        return points;
    }

    /// <summary>The <paramref name="index"/>th figure of a series of the quote (its "open", "high", "low"); NaN where there is none.</summary>
    private static double At(JsonElement quote, string series, int index) =>
        quote.TryGetProperty(series, out var values) && values.ValueKind == JsonValueKind.Array && index < values.GetArrayLength()
        && values[index].ValueKind == JsonValueKind.Number && values[index].TryGetDouble(out var value)
            ? value
            : double.NaN;

    private static double? Session(JsonElement meta, string edge) =>
        meta.TryGetProperty("currentTradingPeriod", out var periods)
        && periods.TryGetProperty("regular", out var regular)
            ? Number(regular, edge)
            : null;

    private static DateTime? Moment(double? seconds) =>
        seconds is { } value && value is > 0 and < 4102444800
            ? DateTime.UnixEpoch.AddSeconds(value)
            : null;

    private static double? Number(JsonElement of, string name) =>
        of.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
            ? number
            : null;

    private static string? Text(JsonElement of, string name) =>
        of.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>The endpoint's own description of what went wrong, if it gave one.</summary>
    private static string? Sentence(JsonElement error) =>
        error.ValueKind == JsonValueKind.Object && error.TryGetProperty("description", out var said)
        && said.ValueKind == JsonValueKind.String
            ? said.GetString()
            : null;
}
