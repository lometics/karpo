// The markets widget (docs\widgets.md §246, §258): one widget for a share, a currency's rate and a coin. The
// person, 2026-10-19: "i want to merge them so that there is only one stock and currency widget. crypto
// should be able to be shown as well"; then, 2026-10-20: "remove the from and to in the stock widget and
// just let me search for currency pairs as it is already possible".
//
// So what a markets widget tracks is one symbol, as the price service writes it, and what it is follows
// from the symbol (MarketSymbols): "EURCHF=X" a currency's rate, "BTC-EUR" a coin in a currency, anything
// else a share, a fund, an index or a commodity ("AAPL", "^GSPC", "GC=F"). One search finds all of them
// (MarketSearch). Beside the symbol: its Label and the service's Detail; the Holding owned, a share's or a
// coin's; the Amount a converter starts at, a rate's in the converter style; the Range, the Style and the
// Chart.

using System.Globalization;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>What a markets widget tracks, as its symbol says (<see cref="MarketSymbols.Kind"/>).</summary>
public enum MarketKind
{
    /// <summary>A share, a fund, an index or a commodity, by its symbol ("AAPL", "^GSPC", "GC=F").</summary>
    Stock,

    /// <summary>One currency's rate in another ("EURCHF=X").</summary>
    Currency,

    /// <summary>A coin's price in a currency ("BTC-USD").</summary>
    Crypto,
}

/// <summary>How a markets widget shows what it tracks, on its line and on its face.</summary>
[JsonConverter(typeof(TolerantEnumConverter<MarketStyle>))]
public enum MarketStyle
{
    /// <summary>The price, how far it moved over its range in the up or down colour, and the range as a chart.</summary>
    [JsonStringEnumMemberName("full")] Full,

    /// <summary>The price alone: no change, no chart.</summary>
    [JsonStringEnumMemberName("compact")] Compact,

    /// <summary>A currency's only: two amounts, either typed and the other worked out; "100 EUR = 94.12 CHF" on the bar.</summary>
    [JsonStringEnumMemberName("converter")] Converter,
}

/// <summary>
/// How a markets widget draws its range (2026-10-20: "add different chart styles to the stock widget,
/// optionally a candle chart"). A name this build does not know reads as the area.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<MarketChartStyle>))]
public enum MarketChartStyle
{
    /// <summary>The line with its area under it, strongest under the line and fading to the foot, as Windows' own finance cards draw it.</summary>
    [JsonStringEnumMemberName("area")] Area,

    /// <summary>The line alone.</summary>
    [JsonStringEnumMemberName("line")] Line,

    /// <summary>A candle for each stretch of the range: its open, high, low and close, green where it closed up, red where down.</summary>
    [JsonStringEnumMemberName("candles")] Candles,
}

/// <summary>
/// How far back a markets widget's chart goes (<see cref="MarketOptions.Range"/>), as Windows' own stock
/// card and Yahoo Finance offer it: a day, five days, a month, six months, the year to date, a year,
/// five years, all of it — and the last so many years, which neither has as a button but a person asked
/// for. The change follows it: over a day it is against the previous close, over any other range against
/// the close before the range began. A name this build does not know reads as the default, five years.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<MarketRange>))]
public enum MarketRange
{
    [JsonStringEnumMemberName("1d")] Day,
    [JsonStringEnumMemberName("5d")] FiveDays,
    [JsonStringEnumMemberName("1mo")] Month,
    [JsonStringEnumMemberName("6mo")] SixMonths,
    [JsonStringEnumMemberName("ytd")] YearToDate,
    [JsonStringEnumMemberName("1y")] Year,
    [JsonStringEnumMemberName("5y")] FiveYears,
    [JsonStringEnumMemberName("max")] Max,

    /// <summary>The last <see cref="MarketOptions.RangeYears"/> years.</summary>
    [JsonStringEnumMemberName("years")] Years,
}

/// <summary>A range's words and reach, in one place for every type.</summary>
public static class MarketRanges
{
    /// <summary>Every range, in the order the menu offers them.</summary>
    public static IReadOnlyList<MarketRange> All { get; } =
    [
        MarketRange.Day, MarketRange.FiveDays, MarketRange.Month, MarketRange.SixMonths,
        MarketRange.YearToDate, MarketRange.Year, MarketRange.FiveYears, MarketRange.Max, MarketRange.Years,
    ];

    /// <summary>
    /// What a widget shows that was never set to a range: five years, the person's choice ("default chart
    /// range should be 5 years"). A range somebody picked is theirs and is kept, a day included.
    /// </summary>
    public const MarketRange Default = MarketRange.FiveYears;

    /// <summary>A range as a known one: anything outside the enum — a word a newer build wrote — is the <see cref="Default"/>.</summary>
    public static MarketRange Known(MarketRange range) => Enum.IsDefined(range) ? range : Default;

    /// <summary>What the menu calls a range: "1 day", "Year to date", "Custom number of years".</summary>
    public static string Choice(MarketRange range) => Known(range) switch
    {
        MarketRange.FiveDays => S.CoreMarketChoiceFiveDays,
        MarketRange.Month => S.CoreMarketChoiceMonth,
        MarketRange.SixMonths => S.CoreMarketChoiceSixMonths,
        MarketRange.YearToDate => S.CoreMarketChoiceYearToDate,
        MarketRange.Year => S.CoreMarketChoiceYear,
        MarketRange.FiveYears => S.CoreMarketChoiceFiveYears,
        MarketRange.Max => S.CoreMarketChoiceMax,
        MarketRange.Years => S.CoreMarketChoiceYears,
        _ => S.CoreMarketChoiceDay,
    };

    /// <summary>What the change is over, said beside it: "Today", "Past 5 days", "Past 3 years", "All time".</summary>
    public static string Span(MarketRange range, int years) => Known(range) switch
    {
        MarketRange.FiveDays => S.CoreMarketSpanFiveDays,
        MarketRange.Month => S.CoreMarketSpanMonth,
        MarketRange.SixMonths => S.CoreMarketSpanSixMonths,
        MarketRange.YearToDate => S.CoreMarketSpanYearToDate,
        MarketRange.Year => S.CoreMarketSpanYear,
        MarketRange.FiveYears => S.CoreMarketSpanFiveYears,
        MarketRange.Max => S.CoreMarketSpanMax,
        MarketRange.Years => Years(years) == 1 ? S.CoreMarketSpanYear : S.CoreMarketSpanYears(Years(years)),
        _ => S.CoreMarketSpanDay,
    };

    /// <summary>The range as the small label beside the change: "1D", "5D", "1M", "6M", "YTD", "1Y", "5Y", "Max", "3Y".</summary>
    public static string Label(MarketRange range, int years) => Known(range) switch
    {
        MarketRange.FiveDays => S.CoreMarketLabelFiveDays,
        MarketRange.Month => S.CoreMarketLabelMonth,
        MarketRange.SixMonths => S.CoreMarketLabelSixMonths,
        MarketRange.YearToDate => S.CoreMarketLabelYearToDate,
        MarketRange.Year => S.CoreMarketLabelYear,
        MarketRange.FiveYears => S.CoreMarketLabelFiveYears,
        MarketRange.Max => S.CoreMarketLabelMax,
        MarketRange.Years => S.CoreMarketLabelYears(Years(years)),
        _ => S.CoreMarketLabelDay,
    };

    /// <summary>A custom number of years held to what the widget offers.</summary>
    public static int Years(int years) => Math.Clamp(years, MarketOptions.LeastYears, MarketOptions.MostYears);

    /// <summary>
    /// What a number field's <paramref name="value"/> sets the custom years to: the whole number it rounds
    /// to (a half up), held to what the widget offers; null where it sets nothing.
    /// </summary>
    public static int? YearsFrom(double value) =>
        double.IsFinite(value) ? (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), MarketOptions.LeastYears, MarketOptions.MostYears) : null;

    /// <summary>
    /// When <paramref name="range"/> begins, seen at <paramref name="utcNow"/>: what a source that is
    /// asked for days rather than a range (the ECB's, CoinGecko's) asks from. All of it is fifty years.
    /// </summary>
    public static DateTime Since(MarketRange range, int years, DateTime utcNow) => Known(range) switch
    {
        MarketRange.Day => utcNow.AddDays(-1),
        MarketRange.FiveDays => utcNow.AddDays(-7),
        MarketRange.Month => utcNow.AddMonths(-1),
        MarketRange.SixMonths => utcNow.AddMonths(-6),
        MarketRange.YearToDate => new DateTime(utcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        MarketRange.Year => utcNow.AddYears(-1),
        MarketRange.Max => utcNow.AddYears(-MarketOptions.MostYears),
        MarketRange.Years => utcNow.Date.AddYears(-Years(years)),
        _ => utcNow.AddYears(-5),
    };
}

/// <summary>markets: what one widget tracks and how it shows it; see the top of the file.</summary>
public sealed class MarketOptions
{
    /// <summary>
    /// What it tracks, as the price service writes it: a share's "AAPL", "^GSPC", "GC=F"; a rate's
    /// "EURCHF=X"; a coin's "BTC-EUR" (<see cref="MarketSymbols"/>); empty = not set.
    /// </summary>
    public string Symbol { get; set; } = "";

    /// <summary>What to call it on the bar instead of its title; empty = the title (<see cref="MarketWords.Title"/>).</summary>
    public string Label { get; set; } = "";

    /// <summary>
    /// What the service called the listing when it was chosen — "Apple Inc. · NASDAQ · USD" — so the search
    /// can name it again without asking. Never a price.
    /// </summary>
    public string Detail { get; set; } = "";

    /// <summary>A rate in the converter style: the amount its first field starts at, 100 for a currency of small units; null for 1.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Amount { get; set; }

    /// <summary>
    /// A share or a coin: how much of it the person owns — shares or coins, decimals allowed — so the widget
    /// says what that is worth at the price now. 0 is none, and is not written. Never what was paid.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Holding { get; set; }

    /// <summary>
    /// §494, a share or a coin: the currency every amount it shows is in — "CHF" for a share listed in
    /// dollars —, worked out at the rate now (<see cref="MarketMoney"/>); null for the currency it is listed
    /// in, and not written. One of <see cref="Currencies.Codes"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Currency { get; set; }

    /// <summary>How far back the chart goes, and so what its change is measured from; five years where never set.</summary>
    public MarketRange Range { get; set; } = MarketRanges.Default;

    /// <summary>For <see cref="MarketRange.Years"/>, how many.</summary>
    public int RangeYears { get; set; } = 3;

    /// <summary>How it shows what it tracks; the converter only for a rate (<see cref="StyleOf"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public MarketStyle Style { get; set; }

    /// <summary>How its chart is drawn; the area where never set.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public MarketChartStyle Chart { get; set; }

    public const int LeastYears = 1;

    public const int MostYears = 50;

    /// <summary>The longest a name on the bar (<see cref="Label"/>) may be.</summary>
    public const int LabelLength = 12;

    /// <summary>Whether it has something to show: a symbol chosen.</summary>
    [JsonIgnore]
    public bool IsSet => Symbol.Trim().Length > 0;

    /// <summary>What it tracks, as its symbol says.</summary>
    [JsonIgnore]
    public MarketKind KindOf => MarketSymbols.Kind(Symbol);

    /// <summary>Its style as what it tracks offers it: the converter only for a rate, anything else the full one.</summary>
    [JsonIgnore]
    public MarketStyle StyleOf => Style switch
    {
        MarketStyle.Compact => MarketStyle.Compact,
        MarketStyle.Converter when KindOf == MarketKind.Currency => MarketStyle.Converter,
        _ => MarketStyle.Full,
    };

    /// <summary>Its chart as a known one: a word a newer build wrote is the area.</summary>
    [JsonIgnore]
    public MarketChartStyle ChartOf => Enum.IsDefined(Chart) ? Chart : MarketChartStyle.Area;

    /// <summary>
    /// §494: the currency it is shown in where that is not the one it is listed in — a share's and a coin's
    /// <see cref="Currency"/>, one Karpo offers —; empty for as listed, and always for a rate, whose figure is
    /// a rate between the two currencies it tracks.
    /// </summary>
    [JsonIgnore]
    public string ShownIn => KindOf != MarketKind.Currency && Currencies.Code(Currency) is { } code && Currencies.Offers(code) ? code : "";

    /// <summary>A new widget, nothing chosen.</summary>
    public static MarketOptions New() => new();

    /// <summary>A rate in the converter style: the amount its first field starts at, 1 where none is set; 1 in any other style.</summary>
    [JsonIgnore]
    public double AmountOrOne => StyleOf == MarketStyle.Converter ? Currencies.Amount(Amount ?? 1) : 1;

    /// <summary>A copy, its values its own.</summary>
    public MarketOptions Copy() => new()
    {
        Symbol = Symbol, Label = Label, Detail = Detail, Amount = Amount, Holding = Holding, Currency = Currency,
        Range = Range, RangeYears = RangeYears, Style = Style, Chart = Chart,
    };

    /// <summary>
    /// As a file may hold it: the symbol as the service writes it, its words trimmed and held to their
    /// lengths, amounts that are amounts, a known range, years, style and chart.
    /// </summary>
    public void Normalize()
    {
        Symbol = MarketSymbols.Canonical(Symbol);
        Label = Labelled(Label);
        var detail = (Detail ?? "").Trim();
        Detail = detail.Length <= 120 ? detail : detail[..120];
        Amount = Amount is { } amount && Currencies.Amount(amount) != 1 ? Currencies.Amount(amount) : null;
        Holding = MarketHoldings.From(Holding);
        Currency = MarketMoney.Kept(Currency);
        Range = MarketRanges.Known(Range);
        RangeYears = MarketRanges.Years(RangeYears);
        Style = Enum.IsDefined(Style) ? Style : MarketStyle.Full;
        Chart = ChartOf;
    }

    /// <summary>A name for it on the bar as it is kept: trimmed, held to <see cref="LabelLength"/>; empty for its title.</summary>
    public static string Labelled(string? typed)
    {
        var text = (typed ?? "").Trim();
        return text.Length > LabelLength ? text[..LabelLength] : text;
    }
}

/// <summary>
/// markets: what a symbol is, read off the symbol alone, as the price service writes them: a currency pair
/// "EURCHF=X" (and "CHF=X", the dollar in francs), a coin "BTC-USD" — its ticker, a dash and a currency's
/// three letters; a share class after a dash is a letter or two ("BRK-B") and stays a share, and two
/// currencies with a dash between are no coin —, and anything else a share, a fund, an index or a commodity.
/// </summary>
public static class MarketSymbols
{
    /// <summary>What <paramref name="symbol"/> is.</summary>
    public static MarketKind Kind(string? symbol) =>
        Pair(symbol) is not null ? MarketKind.Currency : Coin(symbol) is not null ? MarketKind.Crypto : MarketKind.Stock;

    /// <summary>A pair's two currencies: "EURCHF=X" the euro in francs, "CHF=X" the dollar in francs; null for anything else.</summary>
    public static (string From, string To)? Pair(string? symbol)
    {
        var name = (symbol ?? "").Trim().ToUpperInvariant();
        if (!name.EndsWith("=X", StringComparison.Ordinal))
        {
            return null;
        }
        var body = name[..^2];
        var (from, to) = body.Length switch
        {
            6 => (body[..3], body[3..]),
            3 => ("USD", body),
            _ => ("", ""),
        };
        return Currencies.Code(from) is { } a && Currencies.Code(to) is { } b && a != b ? (a, b) : null;
    }

    /// <summary>A coin and the currency it is priced in: "BTC-EUR"; null for anything else.</summary>
    public static (string Coin, string To)? Coin(string? symbol)
    {
        var name = (symbol ?? "").Trim().ToUpperInvariant();
        var dash = name.LastIndexOf('-');
        return dash > 0 && Currencies.Code(name[(dash + 1)..]) is { } to && Currencies.Offers(to)
            && Coins.Code(name[..dash]) is { } coin && !Currencies.Offers(coin)
            ? (coin, to)
            : null;
    }

    /// <summary>The symbol of <paramref name="from"/> in <paramref name="to"/>: "EURCHF=X".</summary>
    public static string PairSymbol(string from, string to) => $"{from}{to}=X";

    /// <summary>A symbol as it is kept: a pair's and a coin's as the service writes them ("USDCHF=X" for "chf=x"), a share's trimmed.</summary>
    public static string Canonical(string? symbol) =>
        Pair(symbol) is { } pair ? PairSymbol(pair.From, pair.To)
        : Coin(symbol) is { } coin ? Coins.Symbol(coin.Coin, coin.To)
        : (symbol ?? "").Trim();

    /// <summary>A symbol as a person reads it: "AAPL", "EUR/CHF", "BTC/EUR".</summary>
    public static string Display(string? symbol) =>
        Pair(symbol) is { } pair ? $"{pair.From}/{pair.To}"
        : Coin(symbol) is { } coin ? $"{coin.Coin}/{coin.To}"
        : SymbolQuery.Display(symbol);
}

/// <summary>
/// markets: what a holding (<see cref="MarketOptions.Holding"/>) is and how it is written. What it is
/// worth is the amount times the price, in the currency the price is quoted in, with that code after it
/// ("4,123.40 USD"). A share quoted in a currency's hundredths — London in pence ("GBp"), Johannesburg in
/// cents ("ZAc"), Tel Aviv in agorot ("ILA") — is worth its amount in the currency itself.
/// </summary>
public static class MarketHoldings
{
    /// <summary>The most decimals an amount is kept and written to: a satoshi's worth of a coin.</summary>
    public const int MostDecimals = 8;

    /// <summary>The largest amount kept, so a slip of the finger is not a number past what a price can be multiplied by.</summary>
    public const double Most = 1e12;

    /// <summary>An amount owned as it is kept: to <see cref="MostDecimals"/> places, at most <see cref="Most"/>; 0 — none — for nothing, a negative or not a number.</summary>
    public static double From(double value) =>
        double.IsFinite(value) && value > 0 ? Math.Min(Most, Math.Round(value, MostDecimals, MidpointRounding.AwayFromZero)) : 0;

    /// <summary>Whether <paramref name="amount"/> is a holding at all: a number, more than nothing.</summary>
    public static bool Holds(double amount) => From(amount) > 0;

    /// <summary>
    /// What a field's <paramref name="text"/> sets the holding to: 0 — none — for nothing typed; null for
    /// words that are no amount, which change nothing (<see cref="Currencies.Parse"/>, in <paramref name="marks"/>).
    /// </summary>
    public static double? Parse(string text, NumberMarks marks) =>
        string.IsNullOrWhiteSpace(text) ? 0 : Currencies.Parse(text, marks) is { } amount ? From(amount) : null;

    /// <summary>The amount and what it is of: "1 share", "10 shares" of a share; "0.5 BTC" of a coin.</summary>
    public static string Units(double amount, MarketKind kind, string coin) =>
        kind == MarketKind.Crypto ? $"{Currencies.Plain(From(amount))} {coin}"
        : From(amount) == 1 ? S.CoreMarketShare : S.CoreMarketShares(Currencies.Plain(From(amount)));

    /// <summary>
    /// What <paramref name="amount"/> is worth at <paramref name="price"/>, and the currency it is in: the
    /// quote's code, or for a currency's hundredths the currency itself and a hundredth of the product.
    /// Null with no holding, no price, or no number.
    /// </summary>
    public static (double Value, string Currency)? Worth(double amount, double? price, string? currency)
    {
        if (!Holds(amount) || price is not { } each || !double.IsFinite(each))
        {
            return null;
        }
        var (whole, units) = MarketMoney.Whole(currency);
        var value = From(amount) * each / units;
        return double.IsFinite(value) ? (value, whole) : null;
    }

    /// <summary>
    /// A sum of money's figure as the widget writes it: grouped, to its currency's minor unit (§494: the
    /// yen none, the dinar three, two where the code says nothing) — "4,123.40", "51,230".
    /// </summary>
    public static string Figure(double value, string currency) =>
        Currencies.Format(value, Currencies.MinorUnits(currency));

    /// <summary>A sum of money as the widget writes it: its <see cref="Figure"/>, the currency's code after it — "4,123.40 USD".</summary>
    public static string Money(double value, string currency) =>
        Figure(value, currency) + (currency.Length > 0 ? " " + currency : "");

    /// <summary>
    /// §494: what stands where the worth stood before — the amount and the price of one, "10 shares × 412.34
    /// USD", "0.5 BTC × 81,917.38 USD" — and the price of one alone for less room; <paramref name="price"/>
    /// written as the widget writes it, in <paramref name="currency"/>. The worth itself is the main figure.
    /// </summary>
    public static (string Long, string Short) Each(double amount, MarketKind kind, string coin, string price, string currency)
    {
        var one = currency.Length > 0 ? $"{price} {currency}" : price;
        return ($"{Units(amount, kind, coin)} × {one}", one);
    }
}

/// <summary>
/// §494: a markets widget's amounts in another currency than the one it is listed in (the person,
/// 2026-11-03: "you should be able to override the currency in which the stock for example is shown, it
/// should then lookup the conversion in the background of course and do the currency conversion for
/// you"). Every amount is the listed one times the rate now, one of the listed currency in the shown one;
/// a currency's hundredths (London's pence) a hundredth of that. A percentage is the same in any currency.
/// </summary>
public static class MarketMoney
{
    /// <summary>
    /// The currency a quote's <paramref name="code"/> counts in, and how many of its units make one: London's
    /// pence ("GBp", "GBX") a hundred to the pound, Johannesburg's cents ("ZAc") to the rand, Tel Aviv's
    /// agorot ("ILA") to the shekel; any other code itself and one.
    /// </summary>
    public static (string Code, double Units) Whole(string? code)
    {
        var said = (code ?? "").Trim();
        return said switch
        {
            "GBp" or "GBX" => ("GBP", 100),
            "ZAc" or "ZAC" => ("ZAR", 100),
            "ILA" => ("ILS", 100),
            _ => (said, 1),
        };
    }

    /// <summary>A currency as <see cref="MarketOptions.Currency"/> keeps it: a code Karpo offers, "CHF" from " chf "; null for any other.</summary>
    public static string? Kept(string? code) => Currencies.Code(code) is { } upper && Currencies.Offers(upper) ? upper : null;

    /// <summary>
    /// The currency <paramref name="options"/> are listed in before a price says so: a rate's and a coin's
    /// second, a share's as the search named it when it was picked ("Apple Inc. · NASDAQ · USD"); empty
    /// where nothing says.
    /// </summary>
    public static string Listed(MarketOptions options)
    {
        if ((MarketSymbols.Pair(options.Symbol)?.To ?? MarketSymbols.Coin(options.Symbol)?.To) is { } to)
        {
            return to;
        }
        var last = (options.Detail ?? "").Split(" · ")[^1].Trim();
        return Currencies.Code(Whole(last).Code) ?? "";
    }

    /// <summary>
    /// What one of <paramref name="listed"/>, the quote's own code, is worth in <paramref name="shown"/> at
    /// <paramref name="rate"/> (one of the whole currency in the shown one): the rate, or a hundredth of it for
    /// a currency's hundredths. Null where nothing is converted: no currency to show, the one it is listed in,
    /// or no rate yet — the listed figures stand until one has come.
    /// </summary>
    public static double? Factor(string? listed, string shown, double? rate)
    {
        var (whole, units) = Whole(listed);
        return shown.Length > 0 && whole.Length > 0 && !string.Equals(whole, shown, StringComparison.Ordinal)
            && rate is { } known && double.IsFinite(known) && known > 0
            ? known / units
            : null;
    }

    /// <summary>
    /// How many decimals a price shown in <paramref name="code"/> is written to: the currency's minor unit
    /// (412.34 dollars, 61,850 yen, 1.234 dinars), and under one as many as its first figures need (a coin of
    /// small units, <see cref="Currencies.RatePlaces"/>).
    /// </summary>
    public static int Places(string code, double value) =>
        double.IsFinite(value) && value != 0 && Math.Abs(value) < 1
            ? Math.Max(Currencies.MinorUnits(code), Currencies.RatePlaces(value))
            : Currencies.MinorUnits(code);

    /// <summary>
    /// What hovering says of a figure shown in another currency: it, then the listed figure and the rate —
    /// "512.30 CHF (640.00 USD × 0.8000)".
    /// </summary>
    public static string Both(string shown, string listed, double rate) => $"{shown} ({listed} × {Currencies.Rate(rate)})";
}

/// <summary>markets: the markets face's own record (<see cref="FolderFace.Market"/>), as the weather face keeps its place.</summary>
public static class MarketFaces
{
    /// <summary>What the markets face of <paramref name="folder"/> tracks, as kept; null where it wears none.</summary>
    public static MarketOptions? Of(FolderOptions folder) =>
        folder.FaceOf(FolderFaces.MarketsId) is { } face ? face.Market ?? MarketOptions.New() : null;

    /// <summary>The face's own record, made where it has none yet.</summary>
    public static MarketOptions Kept(FolderFace face) => face.Market ??= MarketOptions.New();
}


/// <summary>markets: what a widget is called, in one place for its line, its face and its menu.</summary>
public static class MarketWords
{
    /// <summary>The longest a share's name for itself is shown on the bar before it is cut.</summary>
    public const int MostSymbol = 6;

    /// <summary>
    /// What it is called on its line and its face: its label where it has one; else a share's symbol, a
    /// pair "EUR/CHF" — a converter's with its amount where that is not one, "100 JPY/CHF" —, a coin's
    /// ticker ("BTC"). Null with no symbol yet.
    /// </summary>
    public static string? Title(MarketOptions options)
    {
        if (!options.IsSet)
        {
            return null;
        }
        if (options.Label.Trim().Length > 0)
        {
            return options.Label.Trim();
        }
        if (MarketSymbols.Pair(options.Symbol) is ({ } from, { } to))
        {
            var amount = options.AmountOrOne;
            return amount == 1 ? $"{from}/{to}" : $"{Currencies.Plain(amount)} {from}/{to}";
        }
        return MarketSymbols.Coin(options.Symbol) is ({ } coin, _) ? coin : SymbolQuery.Display(options.Symbol);
    }

    /// <summary>
    /// What the line under the title says: a pair's two currencies ("Euro → Swiss Franc"), a coin's name and
    /// its currency ("Bitcoin · USD"); a share's is its company's, which only its quote knows (empty here).
    /// </summary>
    public static string Names(MarketOptions options)
    {
        if (MarketSymbols.Pair(options.Symbol) is ({ } from, { } to))
        {
            return CurrencyNames.Pair(from, to);
        }
        if (MarketSymbols.Coin(options.Symbol) is ({ } coin, { } currency))
        {
            return Coins.Name(coin) is { } name ? $"{name} · {currency}" : currency;
        }
        return "";
    }

    /// <summary>A share's title cut to the bar's room (<see cref="MostSymbol"/>); a pair and a coin whole.</summary>
    public static string Short(MarketOptions options, string title) =>
        options.KindOf == MarketKind.Stock && title.Length > MostSymbol ? title[..MostSymbol] : title;
}
