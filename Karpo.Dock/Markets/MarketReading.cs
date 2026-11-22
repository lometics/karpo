// Everything a markets widget shows (docs\widgets.md §246), worked out from one answer, its options and the
// time, and from nothing else — no window, no network and no clock of its own — so the words, the
// rounding, the market's state and the chart's series can all be checked against saved answers. One
// reading for every type: what differs is what it is called, how finely its price is written and what a
// holding is of.
//
//   Stock      "MSFT", "Microsoft Corporation", "412.34" in its quote's currency, its holding in shares
//   Currency   "EUR/CHF" or "100 JPY/CHF", "Euro → Swiss Franc", the amount's worth at the rate, "0.9269"
//   Crypto     "BTC", "Bitcoin", "81,917.38" in its currency, its holding in coins
//
// The reading always carries the time it was fetched, so a line can say how old it is instead of
// pretending it is live; a failure leaves the last price standing with its age, never a number made up.

using System.Globalization;
using Karpo.Core;
using Karpo.Dock.Panels;
using Karpo.Dock.Weather;

namespace Karpo.Dock.Markets;

/// <summary>What the market was doing when the price was traded.</summary>
internal enum MarketState
{
    /// <summary>The answer did not say enough to tell.</summary>
    Unknown,

    /// <summary>Trading, and the price is live.</summary>
    Open,

    /// <summary>Shut. The price is the last close, and the line says which day's.</summary>
    Closed,
}

/// <summary>Prices and percentages as they are written.</summary>
internal static class MarketText
{
    /// <summary>
    /// A price, to <paramref name="places"/> decimals where they are known, and otherwise to a size that suits
    /// the number: two for an ordinary share, more for something that trades in fractions of a penny, fewer
    /// for something in the thousands. The decimals are always written out, never trimmed, so a price
    /// moving keeps its shape; no currency symbol is guessed — the service's code stands beside it.
    /// </summary>
    public static string Price(double? value, int? places = null)
    {
        if (value is not { } price || !double.IsFinite(price))
        {
            return "–";
        }
        var size = Math.Abs(price);
        var decimals = places is { } given and >= 0 and <= 10 ? given : size >= 1000 ? 0 : size >= 1 ? 2 : 4;
        return price.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The decimals a price of <paramref name="kind"/> is written to: a rate's by its size (<see cref="Currencies.RatePlaces"/>);
    /// a share's as its service says (<see cref="MarketQuote.PriceHint"/>); a coin's as its service says, else
    /// two from one, and below one at least as many as its first figures need (a coin of small units).
    /// </summary>
    public static int? Places(MarketKind kind, double? price, int? hint) => kind switch
    {
        MarketKind.Currency => price is { } rate ? Currencies.RatePlaces(rate) : null,
        // Under one, never fewer than its first figures need: Yahoo's hint for SHIB is too few to show one.
        MarketKind.Crypto => price is { } coin && Math.Abs(coin) < 1 ? Math.Max(hint ?? 0, Currencies.RatePlaces(coin)) : hint ?? 2,
        _ => hint,
    };

    /// <summary>"+1.24%", "-0.86%", "0.00%"; the sign is always there, so up and down read apart.</summary>
    public static string Percent(double? value)
    {
        if (value is not { } change || !double.IsFinite(change))
        {
            return "";
        }
        var rounded = Math.Round(change, 2, MidpointRounding.AwayFromZero);
        var sign = rounded > 0 ? "+" : rounded < 0 ? "-" : "";
        return sign + Math.Abs(rounded).ToString("0.00", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>
    /// The widest a change of <paramref name="percent"/>'s length can be, as its room is measured
    /// (docs\widgets.md §262): every figure an eight and the sign a plus, which is wider than the minus —
    /// "+12.34%" and "-12.34%" are both "+88.88%", "+112.34%" is "+888.88%". Measured beside the ordinary
    /// day's "+88.88%" the room so changes only with a figure more, never as the change ticks or turns (§12.2).
    /// </summary>
    public static string ChangeShape(string percent)
    {
        var bare = percent.TrimStart('+', '-');
        return bare.Length == 0 ? "" : "+" + MarketIsland.Shape(bare);
    }

    /// <summary>
    /// <paramref name="percent"/> and its shorter forms, fullest first (§262): "+12.34%", "+12.3%",
    /// "+12%" — what a room too narrow for the change says before it says nothing, never a change cut.
    /// </summary>
    public static IReadOnlyList<string> ChangeForms(string percent)
    {
        List<string> forms = [percent];
        var sign = percent.StartsWith('+') || percent.StartsWith('-') ? percent[..1] : "";
        if (percent.EndsWith('%') && double.TryParse(percent.TrimEnd('%').TrimStart('+', '-'), NumberStyles.Float, CultureInfo.InvariantCulture, out var change))
        {
            foreach (var places in new[] { 1, 0 })
            {
                // A change that rounds to nothing there has no shorter form: "-0%" would say it did not move.
                var rounded = Math.Round(change, places, MidpointRounding.AwayFromZero);
                var form = sign + rounded.ToString(places == 1 ? "0.0" : "0", CultureInfo.InvariantCulture) + "%";
                if (rounded > 0 && !forms.Contains(form))
                {
                    forms.Add(form);
                }
            }
        }
        return forms;
    }

    /// <summary>
    /// <paramref name="price"/> as it reads and its shorter forms, fullest first (§262), for a room too
    /// narrow for it at the smallest text: without its decimals where it has three figures or more before
    /// them ("734,568"), then without the thousands' marks ("734568"), then in thousands, millions or
    /// billions to a tenth ("734.6K") from ten thousand. Each says the price whole as far as it goes —
    /// never a price cut to its first figures. A price under a hundred has only itself.
    /// </summary>
    public static IReadOnlyList<string> PriceForms(string price)
    {
        List<string> forms = [price];
        var plain = price.Replace(",", "", StringComparison.Ordinal);
        if (!double.TryParse(plain, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
        {
            return forms;
        }
        var size = Math.Abs(value);
        void Add(string form)
        {
            if (!forms.Contains(form))
            {
                forms.Add(form);
            }
        }
        if (size >= 100 && price.Contains('.', StringComparison.Ordinal))
        {
            Add(value.ToString("N0", CultureInfo.InvariantCulture));
        }
        if (size >= 1000)
        {
            Add(value.ToString("0", CultureInfo.InvariantCulture));
        }
        if (size >= 10_000)
        {
            var (unit, mark) = size >= 1e9 ? (1e9, "B") : size >= 1e6 ? (1e6, "M") : (1e3, "K");
            Add((value / unit).ToString("0.0", CultureInfo.InvariantCulture) + mark); // not-ui: a figure's unit
        }
        return forms;
    }

    /// <summary>The same without its percent sign, for a narrow cell: "+1.24".</summary>
    public static string Change(double? value)
    {
        var percent = Percent(value);
        return percent.Length == 0 ? "" : percent[..^1];
    }

    /// <summary>Which way it went: up, down, or neither, for the colour.</summary>
    public static int Direction(double? value) =>
        value is not { } change || !double.IsFinite(change) ? 0
        : Math.Round(change, 2, MidpointRounding.AwayFromZero) switch
        {
            > 0 => 1,
            < 0 => -1,
            _ => 0,
        };
}

/// <summary>What a markets widget is showing.</summary>
internal enum MarketShows
{
    /// <summary>A share whose symbol has not been set yet.</summary>
    NoSymbol,

    /// <summary>The first answer has not arrived.</summary>
    Loading,

    /// <summary>Nothing could be fetched and nothing was kept.</summary>
    Failed,

    /// <summary>There is a price.</summary>
    Ready,
}

/// <summary>A markets widget in words; see the top of the file.</summary>
internal sealed record MarketReading
{
    public required MarketShows State { get; init; }

    public MarketKind Kind { get; init; }

    /// <summary>What it is called: a share's label or symbol, "EUR/CHF", "100 JPY/CHF", "BTC" (<see cref="MarketWords.Title"/>).</summary>
    public required string Symbol { get; init; }

    /// <summary>What it is, under its name: a company's, "Euro → Swiss Franc", "Bitcoin".</summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// The main figure: "336.04", a rate's amount's worth "0.9269"; §494: with an amount owned what it is
    /// worth, "3,360.40" — the price of one is then in <see cref="Holding"/>; "–" with no price.
    /// </summary>
    public string Price { get; init; } = "";

    /// <summary>One of it in <see cref="Currency"/> now: the converter's rate; null with no price.</summary>
    public double? Rate { get; init; }

    /// <summary>The currency the price is in, beside it.</summary>
    public string Currency { get; init; } = "";

    /// <summary>A rate's two currencies and the amount it is said for; empty and 1 for the other types.</summary>
    public string From { get; init; } = "";

    public string To { get; init; } = "";

    public double Amount { get; init; } = 1;

    /// <summary>"-1.09%".</summary>
    public string Percent { get; init; } = "";

    /// <summary>The same without its percent sign, for a cell too narrow for it.</summary>
    public string Change { get; init; } = "";

    /// <summary>1 up, -1 down, 0 neither: the colour.</summary>
    public int Direction { get; init; }

    public MarketState Market { get; init; }

    /// <summary>"At Friday's close" when the market is shut; "ECB reference rate, daily" where a rate is the ECB's; else empty.</summary>
    public string MarketLine { get; init; } = "";

    /// <summary>
    /// The chart's prices as a graph wants them. Over a day of a market that keeps a session, at hours since
    /// the trading day's midnight; otherwise one step a point (0, 1, 2…) — trading time, as Yahoo draws a
    /// range, so five days are five sessions side by side and not five sessions and four nights.
    /// </summary>
    public IReadOnlyList<(double At, double Price)> Day { get; init; } = [];

    /// <summary>The same stretch as candles on the same axis (<see cref="MarketCandles.Of"/>), for the candles chart.</summary>
    public IReadOnlyList<MarketCandle> Candles { get; init; } = [];

    /// <summary>How the chart is drawn (<see cref="MarketOptions.Chart"/>).</summary>
    public MarketChartStyle Chart { get; init; }

    /// <summary>How far back the chart goes, which the change is measured over.</summary>
    public MarketRange Range { get; init; }

    /// <summary>What the change is over, in words: "Today", "Past 5 days", "Past 3 years".</summary>
    public string Span { get; init; } = "";

    /// <summary>The same as the small label beside the change: "1D", "5Y", "3Y".</summary>
    public string RangeLabel { get; init; } = "";

    /// <summary>
    /// §494: where the person owns some of it, what stands where its worth stood before the worth became the
    /// main figure (<see cref="Price"/>): the amount and the price of one, "10 shares × 412.34 USD", and the
    /// price of one alone for less room. Null with no holding set, or no price to work it out from.
    /// </summary>
    public (string Long, string Short)? Holding { get; init; }

    /// <summary>
    /// §494: where it is shown in another currency than it is listed in, its main figure as listed —
    /// "640.00 USD", a holding's "6,400.00 USD" — and the rate it was converted at; null where it is shown as listed.
    /// </summary>
    public (string Listed, double Rate)? Exchange { get; init; }

    /// <summary>
    /// §494: what hovering says where it is shown in another currency: its main figure in both and the rate,
    /// "512.30 CHF (640.00 USD × 0.8000)", then what stands beside it, "10 shares × 512.30 CHF"; null where it
    /// is shown as listed — the line says all of it.
    /// </summary>
    public string? ExchangeTip()
    {
        if (!Ready || Exchange is not { } exchange)
        {
            return null;
        }
        var main = MarketMoney.Both($"{Price} {Currency}", exchange.Listed, exchange.Rate);
        return Holding is { } each ? $"{main}\n{each.Long}" : main;
    }

    /// <summary>Over a range, the points a graph writes under itself, and what it writes (<see cref="TicksOf"/>).</summary>
    public IReadOnlyList<(int Index, string Label)> Ticks { get; init; } = [];

    /// <summary>Where the last trade falls on that axis, or <see cref="NoMark"/>.</summary>
    public double NowAt { get; init; } = NoMark;

    public double? NowValue { get; init; }

    /// <summary>The hours the market was open, for the session the chart draws; null for a market that never closes.</summary>
    public (double From, double To)? Session { get; init; }

    /// <summary>The stretch the chart draws, in its own steps: hours over a day, points over a range.</summary>
    public (double From, double To) Axis { get; init; } = (0, 24);

    /// <summary>"Updated 3 minutes ago"; empty while nothing has ever been fetched.</summary>
    public string Updated { get; init; } = "";

    public string? Problem { get; init; }

    /// <summary>The reading is old enough to say so, or a refresh has failed since.</summary>
    public bool Stale { get; init; }

    /// <summary>Whether there is a price.</summary>
    public bool Ready => State == MarketShows.Ready;

    public const double NoMark = -1;

    /// <summary>What it says while there is no price: why, or that one is coming.</summary>
    public string Waiting => Problem is { Length: > 0 } why ? why : Kind == MarketKind.Currency ? S.DockMarketsLoadingRate : S.DockMarketsLoading;

    /// <summary>
    /// How often a price really asks, in minutes: every online widget's interval (<paramref name="updates"/>,
    /// the dock's own where none is given), held to the markets' floor (<see cref="MarketStore.LeastMinutes"/>).
    /// </summary>
    public static int Refresh(OnlineUpdates? updates = null) =>
        (int)(updates ?? OnlineUpdates.Shared).Every(MarketStore.LeastMinutes).TotalMinutes;

    /// <summary>
    /// The reading of <paramref name="cache"/> as <paramref name="options"/> want it at <paramref name="utcNow"/>;
    /// <paramref name="problem"/> is what the last attempt ran into.
    /// </summary>
    /// <param name="rate">
    /// §494: the rate of the currency it is listed in, in the one it is shown in (<see cref="MarketExchange"/>),
    /// where one has come; its figures stand as listed until then, and where the rate is of another pair.
    /// </param>
    public static MarketReading Of(MarketCache? cache, MarketOptions options, DateTime utcNow, string? problem, OnlineUpdates? updates = null, MarketRate? rate = null)
    {
        var kind = options.KindOf;
        var (from, to) = MarketSymbols.Pair(options.Symbol) ?? ("", "");
        var amount = options.AmountOrOne;
        if (MarketWords.Title(options) is not { } title)
        {
            return new MarketReading { State = MarketShows.NoSymbol, Kind = kind, Symbol = S.DockMarketsNoSymbolTitle };
        }
        var names = MarketWords.Names(options);
        if (cache?.Quote is not { Now: { } listedNow } listed)
        {
            return new MarketReading
            {
                State = problem is { Length: > 0 } ? MarketShows.Failed : MarketShows.Loading,
                Kind = kind, Symbol = title, Name = names, From = from, To = to, Amount = amount, Currency = PricedIn(options, ""),
                Problem = problem,
            };
        }
        var stale = WeatherAge.IsStale(cache.FetchedUtc, utcNow, Refresh(updates), problem);
        // §494: in the currency it is shown in, every figure at the rate now — its chart and candles too —,
        // where a rate has come for the currency it is listed in; as listed until then.
        var listedIn = PricedIn(options, listed.Currency);
        var shownIn = options.ShownIn;
        var factor = rate is { } given && given.From == MarketMoney.Whole(listedIn).Code && given.To == shownIn
            ? MarketMoney.Factor(listedIn, shownIn, given.Rate)
            : null;
        var quote = factor is { } times ? listed.Times(times, shownIn) : listed;
        var currency = factor is null ? listedIn : shownIn;
        var now = quote.Now ?? listedNow;
        var (market, line) = MarketOf(quote, utcNow);
        var range = MarketRanges.Known(options.Range);
        var years = options.RangeYears;
        var worth = kind == MarketKind.Currency ? amount * now : now;
        var places = factor is null ? MarketText.Places(kind, worth, quote.PriceHint) : MarketMoney.Places(shownIn, worth);
        var price = MarketText.Price(worth, places);
        var change = ChangeOver(quote, range);
        var coin = MarketSymbols.Coin(options.Symbol)?.Coin ?? "";
        // §494: what a holding is worth is the main figure, the price of one where the worth stood.
        var held = kind == MarketKind.Currency ? null : MarketHoldings.Worth(options.Holding, now, currency);
        var reading = new MarketReading
        {
            State = MarketShows.Ready,
            Kind = kind,
            Symbol = title,
            Name = kind == MarketKind.Stock ? quote.Name : names.Length > 0 ? names : quote.Name,
            Price = held is { } total ? MarketHoldings.Figure(total.Value, total.Currency) : price,
            Rate = now,
            Currency = held?.Currency ?? currency,
            Exchange = factor is null || rate is not { } used ? null : (Listed(options, listed, listedIn, kind), used.Rate),
            From = from,
            To = to,
            Amount = amount,
            Percent = MarketText.Percent(change),
            Change = MarketText.Change(change),
            Direction = MarketText.Direction(change),
            Market = market,
            MarketLine = quote.Route switch
            {
                MarketRoute.Ecb => S.DockMarketsEcb,
                MarketRoute.CoinGecko => S.DockMarketsCoinGecko,
                _ => line,
            },
            Range = range,
            Span = MarketRanges.Span(range, years),
            RangeLabel = MarketRanges.Label(range, years),
            Holding = held is null ? null : MarketHoldings.Each(options.Holding, kind, coin, price, currency),
            Updated = WeatherAge.Ago(cache.FetchedUtc, utcNow),
            Problem = problem,
            Stale = stale,
            Chart = options.ChartOf,
        };
        // A day in hours where the answer described the day's session (Yahoo's do, a cross's and the ECB's
        // and CoinGecko's do not); else one step a point, as a range.
        return range == MarketRange.Day && quote.SessionOpensUtc is not null && quote.SessionClosesUtc is not null
            ? Hourly(reading, quote, stale)
            : Ranged(reading, quote, stale);
    }

    /// <summary>
    /// §494: the main figure as listed, in the whole currency, for what hovering says beside the one shown:
    /// a holding's worth, "6,400.00 USD", else the price of one, "640.00 USD" (London's pence in pounds).
    /// </summary>
    private static string Listed(MarketOptions options, MarketQuote listed, string listedIn, MarketKind kind)
    {
        var now = listed.Now ?? 0;
        if (MarketHoldings.Worth(options.Holding, now, listedIn) is { } worth)
        {
            return MarketHoldings.Money(worth.Value, worth.Currency);
        }
        var (whole, units) = MarketMoney.Whole(listedIn);
        return units != 1
            ? MarketHoldings.Money(now / units, whole)
            : $"{MarketText.Price(now, MarketText.Places(kind, now, listed.PriceHint))} {listedIn}";
    }

    /// <summary>The currency a price is in: a rate's second currency, a coin's own, a share's quote's (<paramref name="quoted"/>).</summary>
    internal static string PricedIn(MarketOptions options, string quoted) =>
        MarketSymbols.Pair(options.Symbol)?.To ?? MarketSymbols.Coin(options.Symbol)?.To ?? quoted;

    /// <summary>
    /// The same pair the other way round: what the converter's swap shows, its rate one over itself and its
    /// chart turned over with it. A reading of another type is itself.
    /// </summary>
    public MarketReading Swapped() => Kind != MarketKind.Currency ? this : this with
    {
        From = To,
        To = From,
        Currency = From,
        Rate = Rate is { } rate && rate != 0 ? 1 / rate : null,
        Day = [.. Day.Where(point => point.Price > 0).Select(point => (point.At, 1 / point.Price))],
        Candles = [.. Candles.Where(candle => candle.Low > 0).Select(candle => candle.Inverse())],
        Direction = -Direction,
        NowValue = NowValue is double now && now > 0 ? 1 / now : null,
    };

    /// <summary>
    /// What hovering a converter says: "100 EUR = 94.12 CHF", then "1 EUR = 0.9412 CHF · Updated 3 minutes
    /// ago", then why a newer rate is not there.
    /// </summary>
    public string Tooltip()
    {
        var lines = new List<string> { Currencies.Sentence(Amount, From, To, Rate) };
        if (Rate is { } rate)
        {
            lines.Add(Updated.Length > 0 ? $"{Currencies.RateText(From, To, rate)}{FaceDrawings.Dot}{Updated}" : Currencies.RateText(From, To, rate));
        }
        if (Problem is { Length: > 0 } why)
        {
            lines.Add(why);
        }
        return string.Join('\n', lines);
    }

    /// <summary>A day of a market that keeps a session: hours since its midnight, the session shaded, the last trade marked.</summary>
    private static MarketReading Hourly(MarketReading reading, MarketQuote quote, bool stale)
    {
        var points = DayOf(quote);
        List<(double At, double Price)> day = [.. points.Select(point => (point.At, point.Point.Price))];
        // The mark is the last trade, not the clock: outside trading hours the clock has moved on and the
        // price has not, and a dot at "now" would claim a trade that never happened.
        var mark = !stale && day.Count > 0 && quote.TradedUtc is { } lastTrade ? Hour(quote, lastTrade) : NoMark;
        return reading with
        {
            Day = day,
            Candles = MarketCandles.Of(points, quote, MarketCandles.Unit(reading.Range)),
            Axis = AxisOf(Hours(quote), day),
            NowAt = mark,
            NowValue = mark > NoMark ? quote.Now : null,
            Session = Hours(quote),
        };
    }

    /// <summary>A range, or a day of a market with no session: one step a point, the last marked, and the points written under the chart.</summary>
    private static MarketReading Ranged(MarketReading reading, MarketQuote quote, bool stale)
    {
        var series = new List<(double At, double Price)>(quote.Points.Count);
        var times = new List<DateTime>(quote.Points.Count);
        foreach (var point in quote.Points)
        {
            series.Add((series.Count, point.Price));
            times.Add(quote.Local(point.At));
        }
        var mark = !stale && series.Count > 0 ? series.Count - 1 : NoMark;
        return reading with
        {
            Day = series,
            Candles = MarketCandles.Of([.. quote.Points.Select((point, index) => ((double)index, point))], quote, MarketCandles.Unit(reading.Range)),
            Axis = (0, Math.Max(1, series.Count - 1)),
            NowAt = mark,
            NowValue = mark > NoMark ? series[^1].Price : null,
            Ticks = TicksOf(times),
        };
    }

    /// <summary>
    /// How far the price has moved over <paramref name="range"/>, in per cent, as Yahoo's page and Windows'
    /// stock card say it: over a day, against the previous close — the answer's own figure where it gave one;
    /// over any other range, from the close before the range began, or the range's first price where the
    /// answer gave no such close. Null where there is nothing to measure from.
    /// </summary>
    internal static double? ChangeOver(MarketQuote quote, MarketRange range)
    {
        var day = MarketRanges.Known(range) == MarketRange.Day;
        if (day && quote.ChangePercent is { } said && double.IsFinite(said))
        {
            return said;
        }
        double? from = day ? quote.PreviousClose : quote.ChartPreviousClose;
        if (from is not { } close || close <= 0 || !double.IsFinite(close))
        {
            from = quote.Points.Count > 0 && quote.Points[0].Price > 0 ? quote.Points[0].Price : null;
        }
        return quote.Now is { } price && from is { } start ? (price - start) / start * 100 : null;
    }

    /// <summary>
    /// The points a range's graph writes under itself, from the exchange-local times of its points: the
    /// first point of each day where the range is a week or so, of each week where it is a month or two, of
    /// each month up to two years, and of each year past that — each with what it is written as ("Mon",
    /// "Sep 8", "Oct", "2024").
    /// </summary>
    internal static List<(int Index, string Label)> TicksOf(IReadOnlyList<DateTime> times)
    {
        var ticks = new List<(int, string)>();
        if (times.Count < 2)
        {
            return ticks;
        }
        var span = times[^1] - times[0];
        var unit = span <= TimeSpan.FromDays(8) ? 0 : span <= TimeSpan.FromDays(62) ? 1 : span <= TimeSpan.FromDays(800) ? 2 : 3;
        for (var index = 1; index < times.Count; index++)
        {
            var was = times[index - 1];
            var now = times[index];
            var turned = unit switch
            {
                0 => now.Date != was.Date,
                1 => Monday(now) != Monday(was),
                2 => now.Month != was.Month || now.Year != was.Year,
                _ => now.Year != was.Year,
            };
            if (!turned)
            {
                continue;
            }
            var label = unit switch
            {
                0 => Nls.ShortDayName(now.DayOfWeek),
                1 => Nls.MonthDay(DateOnly.FromDateTime(now)),
                2 => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(now.Month),
                _ => now.Year.ToString(CultureInfo.InvariantCulture),
            };
            ticks.Add((index, label));
        }
        return ticks;

        static DateTime Monday(DateTime at) => at.Date.AddDays(-(((int)at.DayOfWeek + 6) % 7));
    }

    /// <summary>
    /// Whether the market was open when the price was traded, taken from the session the answer itself
    /// described — never from a calendar of holidays written here, which would be wrong in a year and wrong
    /// for every exchange it did not know about.
    /// </summary>
    internal static (MarketState State, string Line) MarketOf(MarketQuote quote, DateTime utcNow)
    {
        if (quote.SessionOpensUtc is not { } opens || quote.SessionClosesUtc is not { } closes || quote.TradedUtc is not { } traded)
        {
            return (MarketState.Unknown, "");
        }
        // Currencies and coins trade around the clock, and the answer says so by giving a session that is
        // the whole day. Such a market is never shut: without this, the minute between one day's session
        // ending and the next beginning would have the line say that EUR/USD closed on a Tuesday afternoon.
        if (Ceaseless(opens, closes) || (utcNow >= opens && utcNow < closes && traded >= opens))
        {
            return (MarketState.Open, "");
        }
        // Shut: the price is the close of the session the last trade belonged to, and the day it belonged
        // to is the exchange's own, not this PC's.
        var day = quote.Local(traded);
        var today = quote.Local(utcNow).Date;
        var line = day.Date == today ? S.DockMarketsClosedToday
            : day.Date == today.AddDays(-1) ? S.DockMarketsClosedYesterday
            : S.DockMarketsClosedOn(Nls.DayName(day.DayOfWeek));
        return (MarketState.Closed, line);
    }

    /// <summary>The day's prices on an hours-since-midnight axis, in the exchange's own day.</summary>
    private static List<(double At, MarketPoint Point)> DayOf(MarketQuote quote)
    {
        var points = new List<(double, MarketPoint)>();
        if (quote.Points.Count == 0)
        {
            return points;
        }
        var day = quote.Local(quote.Points[0].At).Date;
        foreach (var point in quote.Points)
        {
            var local = quote.Local(point.At);
            if (local.Date == day)
            {
                points.Add(((local - day).TotalHours, point));
            }
        }
        return points;
    }

    private static double Hour(MarketQuote quote, DateTime utc)
    {
        var local = quote.Local(utc);
        return (local - local.Date).TotalHours;
    }

    /// <summary>
    /// The session's own hours on the same axis, for the stretch the chart draws; null where there is
    /// nothing to narrow it to, which includes a market that never closes.
    /// </summary>
    private static (double From, double To)? Hours(MarketQuote quote)
    {
        if (quote.SessionOpensUtc is not { } opens || quote.SessionClosesUtc is not { } closes || Ceaseless(opens, closes))
        {
            return null;
        }
        var from = Hour(quote, opens);
        var to = Hour(quote, closes);
        return to > from ? (from, to) : null;
    }

    /// <summary>
    /// Whether the session the answer described is the whole day, give or take the minute Yahoo leaves
    /// between one and the next: a currency pair or a coin rather than an exchange.
    /// </summary>
    private static bool Ceaseless(DateTime opens, DateTime closes) => closes - opens >= TimeSpan.FromHours(23);

    /// <summary>
    /// The stretch of the day the chart draws: the session with half an hour of air either side, so a market
    /// that trades for six and a half hours does not spend three quarters of the chart saying nothing; the
    /// whole day for one that never closes, once its prices cover most of it, else what they cover, padded.
    /// </summary>
    private static (double From, double To) AxisOf((double From, double To)? session, List<(double At, double Price)> day)
    {
        const double Whole = 24;
        if (session is { } open)
        {
            return (Math.Max(0, open.From - 0.5), Math.Min(Whole, Math.Max(open.To + 0.5, open.From + 1)));
        }
        if (day.Count < 2 || day[^1].At - day[0].At >= 8)
        {
            return (0, Whole);
        }
        return (Math.Max(0, day[0].At - 0.25), Math.Min(Whole, Math.Max(day[^1].At + 0.25, day[0].At + 1)));
    }
}
