// menus: a markets widget's settings as rows (docs\widgets.md §246, §258), the one description of them for
// its line's menu and its face's (OptionRows.Lines, OptionRows.OfFace). What it tracks first — one search for
// a share, a currency pair or a coin (MarketSearch), what is picked saying which it is — and under it the
// rows that mean something for that, in the menus' one order (§292: what it is of, what of it, its style,
// its look, its words):
//
//   Symbol          the search; what it tracks at the row's end ("AAPL", "EUR/CHF", "BTC/EUR")
//   Amount you own  a share and a coin, on a line (a face never says what a holding is worth)
//   Currency        a share and a coin (§494): as listed, or any currency the rates reach, searched
//   Chart range     every one (and Years for the last so many)
//   Style           price and chart, price only; a pair's in a rate's words, and the converter
//   Chart           area, line or candles, wherever a chart is drawn
//   Amount          a pair in the converter style: what its first field starts at
//   Name on the bar every one
//
// Each row reads what is shown and writes where it is kept, made only then (kept), as a place's rows do:
// showing the menu changes nothing in the file.

namespace Karpo.Core;

/// <summary>The rows of a markets widget, a line's or a face's; see the top of the file.</summary>
public static class MarketRows
{
    public const string Symbol = "MarketSymbol";
    public const string Label = "MarketLabel";
    public const string Amount = "MarketAmount";
    public const string Range = "MarketRange";
    public const string Years = "MarketYears";
    public const string Holding = "MarketHolding";
    public const string Currency = "MarketCurrency";
    public const string Style = "MarketStyle";
    public const string Chart = "MarketChart";

    /// <summary>Segoe Fluent Icons' Switch, two arrows each way: the converter's swap button.</summary>
    public const string SwapGlyph = "";

    /// <summary>The styles what <paramref name="kind"/> is offers: the converter only a pair's.</summary>
    public static IReadOnlyList<MarketStyle> Styles(MarketKind kind) =>
        kind == MarketKind.Currency ? [MarketStyle.Full, MarketStyle.Compact, MarketStyle.Converter] : [MarketStyle.Full, MarketStyle.Compact];

    /// <summary>The charts offered, in the order offered.</summary>
    public static IReadOnlyList<MarketChartStyle> Charts { get; } = [MarketChartStyle.Area, MarketChartStyle.Line, MarketChartStyle.Candles];

    /// <summary>§249: a style's icon: the price with its chart, the price alone, the converter's calculator.</summary>
    public static string StyleGlyph(MarketStyle style) => style switch
    {
        MarketStyle.Compact => OptionGlyphs.Compact,
        MarketStyle.Converter => OptionGlyphs.Calculator,
        _ => OptionGlyphs.Graph,
    };

    /// <summary>What a style is called for <paramref name="kind"/>: a rate's in a rate's words, a price's in a price's.</summary>
    public static string StyleName(MarketKind kind, MarketStyle style) => (kind, style) switch
    {
        (_, MarketStyle.Converter) => S.SettingsWidgetMarketStyleConverter,
        (MarketKind.Currency, MarketStyle.Compact) => S.SettingsWidgetMarketStyleRateOnly,
        (MarketKind.Currency, _) => S.SettingsWidgetMarketStyleRate,
        (_, MarketStyle.Compact) => S.SettingsWidgetMarketStylePriceOnly,
        _ => S.SettingsWidgetMarketStylePrice,
    };

    /// <summary>What a chart is called.</summary>
    public static string ChartName(MarketChartStyle chart) => chart switch
    {
        MarketChartStyle.Line => S.SettingsWidgetMarketChartLine,
        MarketChartStyle.Candles => S.SettingsWidgetMarketChartCandles,
        _ => S.SettingsWidgetMarketChartArea,
    };

    /// <summary>§249: a chart's icon: the area chart, the trend's line, a sheet of bars for the candles.</summary>
    public static string ChartGlyph(MarketChartStyle chart) => chart switch
    {
        MarketChartStyle.Line => OptionGlyphs.Trend,
        MarketChartStyle.Candles => OptionGlyphs.Bars,
        _ => OptionGlyphs.Graph,
    };

    /// <summary>
    /// The rows of <paramref name="shown"/>, what a line or a face shows (a record it has none of yet read as a
    /// new one): see the top of the file. A write goes into <paramref name="kept"/>, the record made where it
    /// had none. <paramref name="shares"/>: what the person's other widgets track, which the search offers
    /// first; <paramref name="face"/>: a face's rows, which have no holding and draw no converter's chart.
    /// <paramref name="words"/>: what the person's widgets hold in each field (WordsInUse), the amounts
    /// owned and the converters' amounts offered as an amount is typed (§332).
    /// </summary>
    public static IReadOnlyList<OptionRow> Of(MarketOptions shown, Func<MarketOptions> kept, IReadOnlyList<Listing>? shares = null, bool face = false, IReadOnlyDictionary<string, IReadOnlyList<OptionFound>>? words = null)
    {
        var kind = shown.KindOf;
        var marks = NumberMarks.Region();
        var what = shown.IsSet ? MarketSymbols.Display(shown.Symbol) : "";
        var style = shown.StyleOf;
        var rows = new List<OptionRow>
        {
            new(Symbol, OptionRowKind.Search, S.SettingsWidgetMarketSymbolHeader)
            {
                Glyph = MarketSearch.Glyph(kind),
                Slot = OptionSlot.Subject,
                Field = OptionField.Symbol,
                Text = shown.IsSet ? MarketSymbols.Display(shown.Symbol) : "",
                Placeholder = S.SettingsWidgetMarketSymbolPlaceholder,
                Finder = new MarketFinder(shown.IsSet ? Share(shown) : null, shares ?? []),
                Pick = found => found.Value switch
                {
                    MarketFound pair => Choose(kept(), pair.Symbol, ""),
                    Instrument instrument => Choose(kept(), instrument),
                    _ => false,
                },
            },
        };
        if (kind != MarketKind.Currency && !face)
        {
            var held = MarketHoldings.Holds(shown.Holding) ? Currencies.Typed(shown.Holding, marks) : "";
            rows.Add(new OptionRow(Holding, OptionRowKind.Text, S.SettingsWidgetMarketHoldingHeader)
            {
                Glyph = OptionGlyphs.Bank,
                Field = OptionField.Text,
                Text = held,
                Placeholder = S.SettingsWidgetMarketHoldingNone,
                Longest = Currencies.Longest,
                SetText = typed => MarketHoldings.Parse(typed, marks) is { } amount
                    && Changed(MarketHoldings.From(shown.Holding), amount, value => kept().Holding = value),
                Slot = OptionSlot.Scope,
                // §332: what the person owns of this and of the rest, then round amounts.
                Finder = new AmountFinder(held, Amounts(WordsInUse.For(words, Holding), what, kind == MarketKind.Crypto ? CoinAmounts : ShareAmounts, marks), marks),
            });
        }
        if (kind != MarketKind.Currency)
        {
            // §494: the currency its amounts are shown in, on a line and a face alike: as listed, or any the rates reach.
            var shownIn = shown.ShownIn;
            var listed = MarketMoney.Listed(shown);
            rows.Add(new OptionRow(Currency, OptionRowKind.Search, S.SettingsWidgetMarketCurrencyHeader)
            {
                Glyph = OptionGlyphs.Money,
                Slot = OptionSlot.Scope,
                Field = OptionField.Search,
                Text = shownIn.Length > 0 ? CurrencyFinder.Name(shownIn) : AsListed(listed),
                Summary = shownIn.Length > 0 ? shownIn : AsListed(listed),
                Placeholder = S.SettingsWidgetMarketCurrencyPlaceholder,
                Finder = new CurrencyFinder(shownIn, listed),
                Pick = found => found.Value is string code && Changed(shownIn, MarketMoney.Kept(code) ?? "", value => kept().Currency = value.Length > 0 ? value : null),
            });
        }
        var ranges = MarketRanges.All;
        var range = MarketRanges.Known(shown.Range);
        rows.Add(new OptionRow(Range, OptionRowKind.Choice, S.SettingsWidgetMarketRangeHeader)
        {
            Glyph = OptionGlyphs.Month,
            Slot = OptionSlot.Scope,
            Choices = [.. ranges.Select(MarketRanges.Choice)],
            ChoiceGlyphs = [.. ranges.Select(OptionGlyphs.RangeOf)],
            Chosen = ranges.ToList().IndexOf(range),
            Set = at => at >= 0 && at < ranges.Count && Changed(range, ranges[at], value => kept().Range = value),
        });
        if (range == MarketRange.Years)
        {
            var years = MarketRanges.Years(shown.RangeYears);
            rows.Add(new OptionRow(Years, OptionRowKind.Number, S.SettingsWidgetMarketYearsHeader)
            {
                Glyph = OptionGlyphs.History,
                Value = years,
                Least = MarketOptions.LeastYears,
                Most = MarketOptions.MostYears,
                Set = typed => MarketRanges.YearsFrom(typed) is { } next && Changed(years, next, value => kept().RangeYears = value),
                Slot = OptionSlot.Scope,
            });
        }
        var styles = Styles(kind);
        rows.Add(new OptionRow(Style, OptionRowKind.Choice, S.SettingsWidgetStyleHeader)
        {
            Glyph = OptionGlyphs.Style,
            Slot = OptionSlot.Style,
            Choices = [.. styles.Select(each => StyleName(kind, each))],
            ChoiceGlyphs = [.. styles.Select(StyleGlyph)],
            Chosen = styles.ToList().IndexOf(style),
            Set = at => at >= 0 && at < styles.Count && Changed(style, styles[at], value => kept().Style = value),
        });
        // The chart wherever one is drawn: the full style's, on a line and on a face; the converter's, on a line.
        if (style == MarketStyle.Full || (style == MarketStyle.Converter && !face))
        {
            var chart = shown.ChartOf;
            rows.Add(new OptionRow(Chart, OptionRowKind.Choice, S.SettingsWidgetMarketChartHeader)
            {
                Glyph = OptionGlyphs.Graph,
                Slot = OptionSlot.Look,
                Choices = [.. Charts.Select(ChartName)],
                ChoiceGlyphs = [.. Charts.Select(ChartGlyph)],
                Chosen = Charts.ToList().IndexOf(chart),
                Set = at => at >= 0 && at < Charts.Count && Changed(chart, Charts[at], value => kept().Chart = value),
            });
        }
        if (style == MarketStyle.Converter)
        {
            rows.Add(new OptionRow(Amount, OptionRowKind.Text, S.SettingsWidgetMarketAmountHeader)
            {
                Glyph = OptionGlyphs.Calculator,
                Slot = OptionSlot.Look,
                Field = OptionField.Text,
                Text = Currencies.Typed(shown.AmountOrOne, marks),
                Placeholder = "1",
                Longest = Currencies.Longest,
                SetText = typed =>
                {
                    var amount = string.IsNullOrWhiteSpace(typed) ? 1 : Currencies.Parse(typed, marks);
                    return amount is { } given && given > 0
                        && Changed(shown.AmountOrOne, Currencies.Amount(given), value => kept().Amount = value == 1 ? null : value);
                },
                // §332: the other converters' amounts, then round ones.
                Finder = new AmountFinder(Currencies.Typed(shown.AmountOrOne, marks), Amounts(WordsInUse.For(words, Amount), what, ConverterAmounts, marks), marks),
            });
        }
        rows.Add(new OptionRow(Label, OptionRowKind.Text, S.SettingsWidgetCountdownNameHeader)
        {
            Glyph = OptionGlyphs.Name,
            Slot = OptionSlot.Words,
            Field = OptionField.Text,
            Text = shown.Label,
            Placeholder = shown.IsSet ? MarketSymbols.Display(shown.Symbol) : "",
            Longest = MarketOptions.LabelLength,
            SetText = typed => Changed(shown.Label, MarketOptions.Labelled(typed), value => kept().Label = value),
            // §304: what the service called it, "Apple Inc.", offered.
            Finder = shown.IsSet ? SuggestFinder.Of(shown.Label, SuggestFinder.Words([Share(shown).Name])) : null,
        });
        return rows;
    }

    /// <summary>§332: the round amounts an amount owned of a share is offered, and of a coin, and a converter's.</summary>
    public static readonly IReadOnlyList<double> ShareAmounts = [1, 5, 10, 25, 50, 100];

    public static readonly IReadOnlyList<double> CoinAmounts = [0.01, 0.1, 0.5, 1, 5, 10];

    public static readonly IReadOnlyList<double> ConverterAmounts = [1, 10, 100, 1000];

    /// <summary>
    /// §332: what an amount's field offers: the person's (<paramref name="used"/>), those of
    /// <paramref name="what"/> first, then <paramref name="round"/>.
    /// </summary>
    private static IReadOnlyList<OptionFound> Amounts(IReadOnlyList<OptionFound> used, string what, IReadOnlyList<double> round, NumberMarks marks) =>
        [.. used.OrderByDescending(row => what.Length > 0 && row.Detail == what), .. round.Select(amount => AmountFinder.Row(amount, marks))];

    /// <summary>
    /// <paramref name="symbol"/> picked for <paramref name="market"/>, with what the service called it
    /// (<paramref name="detail"/>, a share's): kept as the service writes it. A holding of something else — a
    /// share's shares on a coin — is none. False where nothing changed.
    /// </summary>
    public static bool Choose(MarketOptions market, string symbol, string detail)
    {
        var next = MarketSymbols.Canonical(symbol);
        if (next.Length == 0 || (market.Symbol == next && market.Detail == detail))
        {
            return false;
        }
        if (MarketSymbols.Kind(next) != market.KindOf)
        {
            market.Holding = 0;
        }
        (market.Symbol, market.Detail) = (next, detail);
        return true;
    }

    /// <summary>A share the price service answered for: its symbol, and what the service called it. False where nothing changed.</summary>
    public static bool Choose(MarketOptions market, Instrument instrument) => Choose(market, instrument.Symbol, SymbolQuery.Caption(instrument));

    /// <summary>§230: what a widget tracks as a search offers it: its symbol, and what the service called it when it was picked.</summary>
    public static Listing Share(MarketOptions market) => new(market.Symbol, market.Detail.Split(" · ")[0].Trim(), "", "");

    /// <summary>§494: what the currency row says where none is chosen: "As listed (USD)", or "As listed" before anything says which.</summary>
    public static string AsListed(string listed) =>
        listed.Length > 0 ? S.SettingsWidgetMarketCurrencyListed(listed) : S.SettingsWidgetMarketCurrencyListedAny;

    /// <summary>
    /// §494: the currencies a share's or a coin's amounts may be shown in — every one the rates reach
    /// (<see cref="Currencies.Codes"/>) —, found by their codes or their names in Karpo's language or in
    /// English. Before anything is typed: as listed, then the region's, the world's most traded and the rest
    /// (<see cref="Currencies.Ordered"/>), what it is shown in ticked. A pick's value is the code, empty for as listed.
    /// </summary>
    internal sealed class CurrencyFinder(string current, string listed, string? region = null) : OptionFinder
    {
        public override int LeastLetters => 0;

        /// <summary>A currency as its field shows it at rest: "CHF · Swiss Franc".</summary>
        public static string Name(string code)
        {
            var name = CurrencyNames.Of(code);
            return name != code ? $"{code} · {name}" : code;
        }

        public override Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel) => Task.FromResult(Find(query));

        public override string NoneFound(string query) => S.SettingsWidgetMarketCurrencyNotFound(query.Trim());

        internal IReadOnlyList<OptionFound> Find(string query)
        {
            var typed = query.Trim();
            var all = Currencies.Ordered(region ?? Currencies.Region());
            List<OptionFound> found = [];
            var asListed = AsListed(listed);
            if (typed.Length == 0 || asListed.Contains(typed, StringComparison.CurrentCultureIgnoreCase))
            {
                found.Add(new OptionFound(asListed, "", "") { Chosen = current.Length == 0, Glyph = OptionGlyphs.Money });
            }
            // Its code first, then a name that begins with the words, then one that holds them.
            IEnumerable<string> codes = typed.Length == 0 ? all
                : [.. all.Where(code => code.StartsWith(typed, StringComparison.OrdinalIgnoreCase)),
                    .. all.Where(code => Names(code).Any(name => name.StartsWith(typed, StringComparison.CurrentCultureIgnoreCase))),
                    .. all.Where(code => Names(code).Any(name => name.Contains(typed, StringComparison.CurrentCultureIgnoreCase)))];
            foreach (var code in codes.Distinct())
            {
                found.Add(new OptionFound(code, CurrencyNames.Of(code), code) { Chosen = code == current });
            }
            return found;
        }

        private static string[] Names(string code) => [CurrencyNames.Of(code), CurrencyNames.Of(code, "en")];
    }

    private static bool Changed<T>(T now, T next, Action<T> write)
    {
        if (EqualityComparer<T>.Default.Equals(now, next))
        {
            return false;
        }
        write(next);
        return true;
    }
}
