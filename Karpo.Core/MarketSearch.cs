// markets: a markets widget's one search for what it tracks (docs\widgets.md §258; 2026-10-20, the person:
// "remove the from and to in the stock widget and just let me search for currency pairs as it is already
// possible"). One field finds a share, a fund, an index or a commodity, a currency pair and a coin, and
// what is picked says what the widget is (MarketSymbols): there is no type to choose first.
//
//  - **Pairs and coins, worked out here** (Own), before and without asking anybody: two currencies in any
//    spelling — "EURCHF", "EUR CHF", "EUR/CHF", "eur-chf", "EUR to CHF", "euro franc", "swiss franc us
//    dollar" — are that pair, any two ISO 4217 codes Windows knows, quoted or not (the rate's sources find a
//    way, MarketSources); a coin and a currency — "BTC EUR", "bitcoin euro", "BTCEUR", "BTC-EUR" — that coin
//    in that currency; one currency alone its rates against the region's, the dollar and the euro; one coin
//    alone its price in the dollar, the region's currency and the euro.
//  - **The price service's listings** (Symbols.SearchAsync) after them: shares, funds, indices, futures, and
//    the pairs and coins Yahoo lists, each once. Words that already named a pair or a coin whole ask nothing.
//
// Each row says what it is: its type's glyph, and at its end "Currency pair", "Crypto", or a share's symbol
// and exchange. A share picked is looked up before it is kept (its name, exchange and currency kept as its
// Detail); a pair or a coin needs no proving.

namespace Karpo.Core;

/// <summary>A pair or a coin a markets search offers: its symbol, as kept ("EURCHF=X", "BTC-EUR").</summary>
public sealed record MarketFound(string Symbol);

/// <summary>The markets widget's search; see the top of the file.</summary>
public static class MarketSearch
{
    /// <summary>The most rows the words alone make: the six rows' room a search's level keeps (§271).</summary>
    private const int MostOwn = 6;

    /// <summary>The fewest letters a currency's or a coin's name is matched by ("yen").</summary>
    private const int LeastName = 3;

    /// <summary>Words between two names that join them and name nothing: "EUR to CHF", "bitcoin in francs".</summary>
    private static readonly string[] Joins = ["TO", "IN", "INTO", "VS", "PER", "AND"];

    /// <summary>
    /// The pairs and coins <paramref name="query"/> names, best first, as symbols ("EURCHF=X", "BTC-EUR"),
    /// worked out without asking anybody (see the top of the file); and whether its words named them whole —
    /// two parts, each a currency or a coin —, so nothing else is worth asking for. <paramref name="region"/>:
    /// the region whose currency one alone is offered against first; Windows' where none is given.
    /// </summary>
    public static (IReadOnlyList<string> Symbols, bool Whole) Own(string? query, string? region = null)
    {
        var text = (query ?? "").Trim();
        if (text.Length == 0 || text.Contains('^'))
        {
            return ([], false);
        }
        // The service's own way of writing it: "EURCHF=X", "CHF=X", "BTC-EUR".
        if (MarketSymbols.Kind(text) != MarketKind.Stock)
        {
            return ([MarketSymbols.Canonical(text)], true);
        }
        if (text.EndsWith("=X", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^2];
        }
        var words = text.Split([' ', '/', '\\', '-', '_', ',', ':', ';', '|', '+', '>', '<', '=', '→', '·'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.ToUpperInvariant()).ToList();
        if (words.Count > 2)
        {
            words.RemoveAll(word => Joins.Contains(word));
        }
        var home = Currencies.Code(region ?? Currencies.Region());
        List<string> found = [];
        void Add(string symbol)
        {
            if (!found.Contains(symbol) && found.Count < MostOwn)
            {
                found.Add(symbol);
            }
        }
        // Run together: "EURCHF", "BTCEUR".
        if (words.Count == 1 && words[0].All(char.IsAsciiLetter))
        {
            var word = words[0];
            if (word.Length == 6 && IsCurrency(word[..3]) && IsCurrency(word[3..]) && word[..3] != word[3..])
            {
                return ([MarketSymbols.PairSymbol(word[..3], word[3..])], true);
            }
            if (word.Length >= 5 && Currencies.Offers(word[^3..]) && Coins.Known(word[..^3]))
            {
                return ([Coins.Symbol(word[..^3], word[^3..])], true);
            }
        }
        // Two parts, each a currency or a coin, wherever the words part.
        for (var split = 1; split < words.Count; split++)
        {
            var (firstCurrencies, firstCoins) = Named(words[..split], region);
            var (secondCurrencies, secondCoins) = Named(words[split..], region);
            foreach (var from in firstCurrencies)
            {
                foreach (var to in secondCurrencies.Where(to => to != from))
                {
                    Add(MarketSymbols.PairSymbol(from, to));
                }
            }
            foreach (var coin in firstCoins)
            {
                foreach (var to in secondCurrencies.Where(Currencies.Offers))
                {
                    Add(Coins.Symbol(coin, to));
                }
            }
            // "EUR BTC": the coin in the currency named first.
            foreach (var coin in secondCoins)
            {
                foreach (var to in firstCurrencies.Where(Currencies.Offers))
                {
                    Add(Coins.Symbol(coin, to));
                }
            }
        }
        if (found.Count > 0)
        {
            return (found, true);
        }
        // One currency or one coin alone: its usual company.
        var (currencies, coins) = Named(words, region);
        string[] others = [.. new[] { home, "USD", "EUR" }.OfType<string>().Distinct()];
        foreach (var code in currencies.Take(2))
        {
            foreach (var other in others.Where(other => other != code))
            {
                Add(MarketSymbols.PairSymbol(code, other));
            }
            foreach (var other in others.Where(other => other != code))
            {
                Add(MarketSymbols.PairSymbol(other, code));
            }
        }
        foreach (var coin in coins.Take(2))
        {
            foreach (var to in new[] { "USD", home, "EUR" }.OfType<string>().Where(Currencies.Offers).Distinct())
            {
                Add(Coins.Symbol(coin, to));
            }
        }
        return (found, false);
    }

    /// <summary>
    /// What <paramref name="words"/> name, best first: the currency their code is, else those whose name in
    /// Karpo's language or in English holds them ("franc", "us dollar"), the most traded first; the coin their
    /// ticker is, else the common coins whose name holds them ("bitcoin").
    /// </summary>
    private static (List<string> Currencies, List<string> Coins) Named(List<string> words, string? region)
    {
        var joined = string.Join(' ', words);
        List<string> currencies = [];
        List<string> coins = [];
        if (words.Count == 1 && IsCurrency(joined))
        {
            currencies.Add(joined);
        }
        if (words.Count == 1 && Coins.Known(joined))
        {
            coins.Add(joined);
        }
        if (currencies.Count == 0 && coins.Count == 0 && joined.Length >= LeastName)
        {
            currencies.AddRange(Currencies.Ordered(region ?? Currencies.Region())
                .Where(code => CurrencyNames.Of(code).Contains(joined, StringComparison.CurrentCultureIgnoreCase)
                    || CurrencyNames.Of(code, "en").Contains(joined, StringComparison.OrdinalIgnoreCase))
                .Take(3));
            coins.AddRange(Coins.Common.Where(coin => coin.Name.Contains(joined, StringComparison.OrdinalIgnoreCase)).Select(coin => coin.Code).Take(2));
        }
        return (currencies, coins);
    }

    /// <summary>Whether <paramref name="code"/> is a currency's ISO 4217 code: one Karpo offers, or one Windows has a name for.</summary>
    public static bool IsCurrency(string code) =>
        Currencies.Code(code) is { } upper && (Currencies.Offers(upper) || !string.Equals(CurrencyNames.Of(upper, "en"), upper, StringComparison.Ordinal));

    /// <summary>
    /// The row a search offers for <paramref name="symbol"/>, its type's glyph on it: a pair "EUR/CHF" with
    /// "Currency pair · Euro → Swiss Franc" at its end, a coin "BTC/EUR" with "Crypto · Bitcoin" — what it is
    /// first, as the end is cut where the menu is short of room —; a share by <paramref name="name"/>, its
    /// symbol and <paramref name="exchange"/> at its end. Ticked where it is <paramref name="own"/>.
    /// </summary>
    public static OptionFound Row(string symbol, string name = "", string exchange = "", string? own = null)
    {
        var chosen = string.Equals(MarketSymbols.Canonical(symbol), MarketSymbols.Canonical(own), StringComparison.OrdinalIgnoreCase);
        if (MarketSymbols.Pair(symbol) is ({ } from, { } to))
        {
            return new OptionFound($"{from}/{to}", $"{S.SettingsWidgetMarketFoundPair} · {CurrencyNames.Pair(from, to)}", new MarketFound(MarketSymbols.Canonical(symbol)))
            {
                Glyph = Glyph(MarketKind.Currency), Chosen = chosen,
            };
        }
        if (MarketSymbols.Coin(symbol) is ({ } coin, { } currency))
        {
            var called = Coins.Name(coin) ?? Plain(name, currency);
            var what = called.Length > 0 && called != coin ? $"{S.SettingsWidgetMarketFoundCrypto} · {called}" : S.SettingsWidgetMarketFoundCrypto;
            return new OptionFound($"{coin}/{currency}", what, new MarketFound(MarketSymbols.Canonical(symbol)))
            {
                Glyph = Glyph(MarketKind.Crypto), Chosen = chosen,
            };
        }
        var said = SymbolQuery.Display(symbol);
        return new OptionFound(name.Length > 0 ? name : said, string.Join(" · ", new[] { name.Length > 0 ? said : "", exchange }.Where(part => part.Length > 0)), new Listing(symbol, name, exchange, ""))
        {
            Glyph = Glyph(MarketKind.Stock), Chosen = chosen,
        };
    }

    /// <summary>§249: what a symbol is, as an icon: a share the market's trend, a pair the two arrows of an exchange, a coin money.</summary>
    public static string Glyph(MarketKind kind) => kind switch
    {
        MarketKind.Currency => OptionGlyphs.Exchange,
        MarketKind.Crypto => OptionGlyphs.Money,
        _ => OptionGlyphs.Trend,
    };

    /// <summary>A coin's name as the service lists it, without the currency it lists it in ("Bitcoin USD").</summary>
    private static string Plain(string name, string currency) =>
        name.EndsWith(" " + currency, StringComparison.OrdinalIgnoreCase) ? name[..^(currency.Length + 1)].Trim() : name.Trim();
}

/// <summary>
/// A markets widget's search (see the top of the file): before anything is typed, what it tracks, ticked,
/// then what the person's other markets widgets track (§230); then the pairs and coins the words name and
/// the service's listings. A share is looked up before it is kept.
/// </summary>
/// <param name="mine">What it tracks now, as the service named it (<see cref="MarketRows.Share"/>); null for nothing.</param>
/// <param name="used">What the person's other widgets track (<see cref="OptionFields.SharesInUse"/>).</param>
/// <param name="region">The region whose currency a currency alone is offered against; Windows' where none is given.</param>
/// <param name="search">The service's search: <see cref="Symbols.SearchAsync"/>, or a test's answers.</param>
internal sealed class MarketFinder(Listing? mine, IReadOnlyList<Listing> used, string? region = null, Func<string, CancellationToken, Task<List<Listing>>>? search = null) : OptionFinder
{
    private readonly string own = mine?.Symbol ?? "";

    public override int LeastLetters => SymbolQuery.LeastLetters;

    public override TimeSpan Wait => SymbolQuery.Wait;

    public override bool Remote => true;

    /// <summary>
    /// §230: its own, ticked, then what the person's other markets show; §411: then well-known shares, a pair, a coin
    /// and gold, so the search lists something before anything is typed.
    /// </summary>
    public override IReadOnlyList<OptionFound> Suggested
    {
        get
        {
            List<Listing> listed = [.. mine is null ? Array.Empty<Listing>() : [mine], .. used.Where(one => !Same(one.Symbol, own))];
            foreach (var popular in Popular)
            {
                if (!listed.Exists(one => Same(one.Symbol, popular.Symbol)))
                {
                    listed.Add(popular);
                }
            }
            return [.. listed.Select(one => MarketSearch.Row(one.Symbol, one.Name, "", own))];
        }
    }

    /// <summary>§411: what a search for a share offers after the person's own: well-known shares, a pair, a coin, gold.</summary>
    internal static readonly IReadOnlyList<Listing> Popular =
    [
        new("AAPL", "Apple Inc.", "", ""), // not-ui: a company's name
        new("MSFT", "Microsoft Corporation", "", ""), // not-ui: a company's name
        new("NVDA", "NVIDIA Corporation", "", ""), // not-ui: a company's name
        new("GOOGL", "Alphabet Inc.", "", ""), // not-ui: a company's name
        new("AMZN", "Amazon.com, Inc.", "", ""), // not-ui: a company's name
        new("EURUSD=X", "", "", ""), new("BTC-USD", "", "", ""), new("GC=F", "", "", ""),
    ];

    /// <summary>The pairs and coins the words name; and, unless they named them whole, the service's listings after them, each once.</summary>
    public override async Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel)
    {
        var (symbols, whole) = MarketSearch.Own(query, region);
        var found = symbols.Select(symbol => MarketSearch.Row(symbol, own: own)).ToList();
        if (whole)
        {
            return found;
        }
        IReadOnlyList<Listing> listings;
        try
        {
            listings = await (search ?? Symbols.SearchAsync)(SymbolQuery.Clean(query), cancel).ConfigureAwait(false);
        }
        catch (Exception error) when (Expected(error) && found.Count > 0 && !cancel.IsCancellationRequested)
        {
            // Offline: what the words named stands.
            return found;
        }
        foreach (var listing in listings)
        {
            var symbol = MarketSymbols.Canonical(listing.Symbol);
            if (!symbols.Contains(symbol, StringComparer.OrdinalIgnoreCase) && !found.Exists(row => Same(SymbolOf(row), symbol)))
            {
                found.Add(MarketSearch.Row(symbol, listing.Name, listing.Exchange, own));
            }
        }
        return found;
    }

    /// <summary>Words entered without a pick: the pair or the coin they name, else a symbol written out — "F" is Ford — looked up as it is.</summary>
    public override OptionChecked? Typed(string text)
    {
        if (MarketSearch.Own(text, region).Symbols is [var first, ..])
        {
            return new OptionChecked(MarketSearch.Row(first, own: own));
        }
        return SymbolQuery.Acceptable(text) ? new OptionChecked(MarketSearch.Row(SymbolQuery.Symbol(text))) : null;
    }

    public override string? Checking(OptionFound found) =>
        found.Value is Listing listing ? S.SettingsWidgetMarketSymbolLookingUp(SymbolQuery.Display(listing.Symbol)) : null;

    /// <summary>A pair or a coin as it is; a share looked up — no prices is said and nothing kept; a symbol that turns out a pair or a coin kept as one.</summary>
    public override async Task<OptionChecked> CheckAsync(OptionFound found, CancellationToken cancel)
    {
        if (found.Value is not Listing { Symbol: var symbol })
        {
            return new OptionChecked(found);
        }
        if (MarketSymbols.Kind(symbol) != MarketKind.Stock)
        {
            return new OptionChecked(found with { Value = new MarketFound(MarketSymbols.Canonical(symbol)) });
        }
        if (!SymbolQuery.Acceptable(symbol))
        {
            return new OptionChecked(null, S.SettingsWidgetMarketSymbolNotASymbol);
        }
        var instrument = await Symbols.LookUpAsync(symbol, cancel).ConfigureAwait(false);
        if (instrument is null)
        {
            return new OptionChecked(null, S.SettingsWidgetMarketSymbolNoPrices(SymbolQuery.Display(symbol)));
        }
        return new OptionChecked(found with
        {
            Value = MarketSymbols.Kind(instrument.Symbol) != MarketKind.Stock ? new MarketFound(MarketSymbols.Canonical(instrument.Symbol)) : instrument,
        });
    }

    public override string NoneFound(string query) => S.SettingsWidgetMarketSymbolNotFound(SymbolQuery.Clean(query));

    public override string Failed => S.SettingsWidgetMarketSymbolCantCheck;

    private static bool Same(string one, string other) =>
        string.Equals(MarketSymbols.Canonical(one), MarketSymbols.Canonical(other), StringComparison.OrdinalIgnoreCase);

    private static string SymbolOf(OptionFound row) => row.Value switch
    {
        MarketFound pick => pick.Symbol,
        Listing listing => listing.Symbol,
        _ => "",
    };
}
