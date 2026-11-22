// markets: a markets widget's menu (docs\widgets.md §246, §258) — one search for what it tracks, a share, a
// currency pair or a coin, what is picked saying which it is, and under it the rows that mean something for
// that — for its line and its face alike (no holding on a face, no converter's chart on a face); the search's
// words worked out without a service (pairs in every spelling, coins, a currency or a coin alone), mixed with
// the service's listings; and what a pick writes.

namespace Karpo.Tests.Markets;

public sealed class MarketRowsTests
{
    /// <summary>One of each: a share, a pair in the converter style (every row a pair has), a coin; what another test walks every row with.</summary>
    public static readonly MarketOptions[] Samples =
    [
        new() { Symbol = "MSFT" },
        new() { Symbol = "EURCHF=X", Style = MarketStyle.Converter },
        new() { Symbol = "BTC-USD" },
    ];

    /// <summary>A Yahoo search answer for "eur", trimmed: pairs, a share, an index and a coin.</summary>
    internal const string EurAnswer = """
        { "quotes": [
          { "symbol": "EURUSD=X", "shortname": "EUR/USD", "quoteType": "CURRENCY", "exchange": "CCY", "exchDisp": "CCY", "typeDisp": "Currency" },
          { "symbol": "EEFT", "longname": "Euronet Worldwide, Inc.", "quoteType": "EQUITY", "exchange": "NMS", "exchDisp": "NASDAQ", "typeDisp": "Equity" },
          { "symbol": "EURCHF=X", "shortname": "EUR/CHF", "quoteType": "CURRENCY", "exchange": "CCY", "exchDisp": "CCY", "typeDisp": "Currency" },
          { "symbol": "^STOXX50E", "shortname": "ESTX 50 PR.EUR", "quoteType": "INDEX", "exchange": "STO", "exchDisp": "STOXX", "typeDisp": "Index" },
          { "symbol": "EURC-USD", "shortname": "EURC USD", "quoteType": "CRYPTOCURRENCY", "exchange": "CCC", "exchDisp": "CCC", "typeDisp": "Cryptocurrency" }
        ] }
        """;

    private static CellItem Line(MarketOptions market)
    {
        var line = ListKind.Markets.New();
        line.Market = market.Copy();
        return line;
    }

    private static string[] Keys(IEnumerable<OptionRow> rows) => [.. rows.Select(row => row.Key)];

    /// <summary>What a symbol is, read off the symbol alone, and how it is kept.</summary>
    [Theory]
    [InlineData("AAPL", MarketKind.Stock, "AAPL")]
    [InlineData("^GSPC", MarketKind.Stock, "^GSPC")]
    [InlineData("GC=F", MarketKind.Stock, "GC=F")]
    [InlineData("BRK-B", MarketKind.Stock, "BRK-B")]
    [InlineData("EURCHF=X", MarketKind.Currency, "EURCHF=X")]
    [InlineData(" chf=x ", MarketKind.Currency, "USDCHF=X")]
    [InlineData("eureur=x", MarketKind.Stock, "eureur=x")]
    [InlineData("btc-eur", MarketKind.Crypto, "BTC-EUR")]
    [InlineData("PEPE24478-USD", MarketKind.Crypto, "PEPE24478-USD")]
    [InlineData("EUR-CHF", MarketKind.Stock, "EUR-CHF")]
    public void WhatASymbolIs(string symbol, MarketKind kind, string kept)
    {
        Assert.Equal(kind, MarketSymbols.Kind(symbol));
        Assert.Equal(kept, MarketSymbols.Canonical(symbol));
        var options = new MarketOptions { Symbol = symbol };
        options.Normalize();
        Assert.Equal((kind, kept), (options.KindOf, options.Symbol));
    }

    /// <summary>
    /// Two currencies in any spelling are that pair, any two ISO codes (the lev nobody quotes too); a coin and
    /// a currency that coin in it — all worked out whole, nothing asked of the service.
    /// </summary>
    [Theory]
    [InlineData("EURCHF", "EURCHF=X")]
    [InlineData("eur chf", "EURCHF=X")]
    [InlineData("EUR/CHF", "EURCHF=X")]
    [InlineData("eur-chf", "EURCHF=X")]
    [InlineData("EUR to CHF", "EURCHF=X")]
    [InlineData("EURCHF=X", "EURCHF=X")]
    [InlineData("chf=x", "USDCHF=X")]
    [InlineData("euro franc", "EURCHF=X")]
    [InlineData("Euro Swiss Franc", "EURCHF=X")]
    [InlineData("swiss franc us dollar", "CHFUSD=X")]
    [InlineData("CHF USD", "CHFUSD=X")]
    [InlineData("BGN USD", "BGNUSD=X")]
    [InlineData("BTC EUR", "BTC-EUR")]
    [InlineData("btc-eur", "BTC-EUR")]
    [InlineData("BTCEUR", "BTC-EUR")]
    [InlineData("bitcoin euro", "BTC-EUR")]
    [InlineData("EUR BTC", "BTC-EUR")]
    [InlineData("eth in chf", "ETH-CHF")]
    public void WordsNamingAPairOrACoinAreItWhole(string query, string symbol)
    {
        var (found, whole) = MarketSearch.Own(query, "CHF");
        Assert.True(whole);
        Assert.Equal(symbol, found[0]);
    }

    /// <summary>A currency alone is its rates against the region's, the dollar and the euro, each way; a coin alone its price in them; a name nothing of its own.</summary>
    [Fact]
    public void ACurrencyOrACoinAloneIsItsUsualCompany()
    {
        Assert.Equal("EURCHF=X EURUSD=X CHFEUR=X USDEUR=X", Said(MarketSearch.Own("eur", "CHF")));
        Assert.Equal("JPYCHF=X JPYUSD=X JPYEUR=X CHFJPY=X USDJPY=X EURJPY=X", Said(MarketSearch.Own("yen", "CHF")));
        Assert.Equal("BTC-USD BTC-CHF BTC-EUR BCH-USD BCH-CHF BCH-EUR", Said(MarketSearch.Own("bitcoin", "CHF")));
        Assert.Equal("ETH-USD ETH-EUR", Said(MarketSearch.Own("ETH", "USD")));
        foreach (var share in new[] { "apple", "AAPL", "^GSPC", "gold", "MSFT" })
        {
            Assert.Empty(MarketSearch.Own(share, "CHF").Symbols);
        }

        // The symbols, none of them whole: the service is asked too.
        static string Said((IReadOnlyList<string> Symbols, bool Whole) own)
        {
            Assert.False(own.Whole);
            return string.Join(' ', own.Symbols);
        }
    }

    /// <summary>
    /// The search's rows say what they are — a pair's glyph and "Currency pair", a coin's and "Crypto", a share's
    /// symbol and exchange —, what the words name first, then the service's listings, each once.
    /// </summary>
    [Fact]
    public async Task TheSearchMixesWhatTheWordsNameWithTheServicesListings()
    {
        var asked = new List<string>();
        var finder = new MarketFinder(null, [], "CHF", (query, _) =>
        {
            asked.Add(query);
            return Task.FromResult(SymbolQuery.Listings(EurAnswer));
        });
        var rows = await finder.FindAsync("eur", CancellationToken.None);
        Assert.Equal(
            [
                ("EUR/CHF", $"{S.SettingsWidgetMarketFoundPair} · Euro → Swiss Franc", OptionGlyphs.Exchange),
                ("EUR/USD", $"{S.SettingsWidgetMarketFoundPair} · Euro → US Dollar", OptionGlyphs.Exchange),
                ("CHF/EUR", $"{S.SettingsWidgetMarketFoundPair} · Swiss Franc → Euro", OptionGlyphs.Exchange),
                ("USD/EUR", $"{S.SettingsWidgetMarketFoundPair} · US Dollar → Euro", OptionGlyphs.Exchange),
                ("Euronet Worldwide, Inc.", "EEFT · NASDAQ", OptionGlyphs.Trend),
                ("ESTX 50 PR.EUR", "^STOXX50E · STOXX", OptionGlyphs.Trend),
                ("EURC/USD", S.SettingsWidgetMarketFoundCrypto, OptionGlyphs.Money),
            ],
            rows.Select(row => (row.Title, row.Detail, row.Glyph)));
        Assert.Equal(["eur"], asked);

        // Words that name a pair or a coin whole ask nothing; a coin's row names it.
        var pair = Assert.Single(await finder.FindAsync("EUR / CHF", CancellationToken.None));
        Assert.Equal(new MarketFound("EURCHF=X"), pair.Value);
        Assert.Equal(
            [("BTC/EUR", $"{S.SettingsWidgetMarketFoundCrypto} · Bitcoin"), ("BCH/EUR", $"{S.SettingsWidgetMarketFoundCrypto} · Bitcoin Cash")],
            (await finder.FindAsync("bitcoin in euro", CancellationToken.None)).Select(row => (row.Title, row.Detail)));
        Assert.Equal(["eur"], asked);

        // Offline, what the words named stands; with nothing named, no service is said.
        var offline = new MarketFinder(null, [], "CHF", (_, _) => throw new HttpRequestException("offline"));
        Assert.Equal(4, (await offline.FindAsync("eur", CancellationToken.None)).Count);
        await Assert.ThrowsAsync<HttpRequestException>(() => offline.FindAsync("apple", CancellationToken.None));

        // Enter on words alone: the pair they name; a symbol written out looked up as it is.
        Assert.Equal(new MarketFound("CHFUSD=X"), finder.Typed("chf usd")!.Value.Found!.Value);
        Assert.Equal("F", Assert.IsType<Listing>(finder.Typed("f")!.Value.Found!.Value).Symbol);
        // A pair needs no proving; a pair the service listed is kept as one.
        Assert.Equal(pair, (await finder.CheckAsync(pair, CancellationToken.None)).Found);
        Assert.Equal(new MarketFound("EURUSD=X"), (await finder.CheckAsync(MarketSearch.Row("EURUSD=X") with { Value = new Listing("EURUSD=X", "EUR/USD", "CCY", "") }, CancellationToken.None)).Found!.Value);
    }

    /// <summary>What it tracks says which rows it has, in the menus' one order (§292); a face's the same but the holding and a converter's chart; §494: a share's and a coin's currency.</summary>
    [Theory]
    [InlineData("MSFT", MarketStyle.Full, new[] { MarketRows.Symbol, MarketRows.Holding, MarketRows.Currency, MarketRows.Range, MarketRows.Style, MarketRows.Chart, MarketRows.Label })]
    [InlineData("MSFT", MarketStyle.Compact, new[] { MarketRows.Symbol, MarketRows.Holding, MarketRows.Currency, MarketRows.Range, MarketRows.Style, MarketRows.Label })]
    [InlineData("EURCHF=X", MarketStyle.Full, new[] { MarketRows.Symbol, MarketRows.Range, MarketRows.Style, MarketRows.Chart, MarketRows.Label })]
    [InlineData("EURCHF=X", MarketStyle.Converter, new[] { MarketRows.Symbol, MarketRows.Range, MarketRows.Style, MarketRows.Chart, MarketRows.Amount, MarketRows.Label })]
    [InlineData("BTC-EUR", MarketStyle.Full, new[] { MarketRows.Symbol, MarketRows.Holding, MarketRows.Currency, MarketRows.Range, MarketRows.Style, MarketRows.Chart, MarketRows.Label })]
    [InlineData("", MarketStyle.Full, new[] { MarketRows.Symbol, MarketRows.Holding, MarketRows.Currency, MarketRows.Range, MarketRows.Style, MarketRows.Chart, MarketRows.Label })]
    public void WhatItTracksSaysItsRows(string symbol, MarketStyle style, string[] keys)
    {
        var market = new MarketOptions { Symbol = symbol, Style = style };
        Assert.Equal(keys, Keys(OptionRows.Of(Line(market))));
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [FolderFace.Of(FolderFaces.MarketsId)];
        folder.Folder.Faces[0].Market = market.Copy();
        string[] face = [.. keys.Where(key => key != MarketRows.Holding && !(style == MarketStyle.Converter && key == MarketRows.Chart))];
        Assert.Equal(face, Keys(OptionRows.OfFace(folder.Folder, 0)));
        // The converter is a pair's style alone: a share or a coin set to it shows the full style.
        var converter = OptionRows.Of(Line(new MarketOptions { Symbol = symbol, Style = MarketStyle.Converter })).Single(row => row.Key == MarketRows.Style);
        var pair = MarketSymbols.Kind(symbol) == MarketKind.Currency;
        Assert.Equal((pair ? 3 : 2, pair ? 2 : 0), (converter.Choices.Count, converter.Chosen));
    }

    /// <summary>
    /// A pick writes the symbol as the service writes it, and what it is follows: a pair's rows, a coin's; a
    /// holding of one thing is none of another; the search shows it at the row's end, its type's glyph on the row.
    /// </summary>
    [Fact]
    public void APickSaysWhatItIs()
    {
        var line = Line(new MarketOptions { Symbol = "MSFT", Holding = 10, Detail = "Microsoft Corporation · NASDAQ · USD" });
        OptionRow Symbol() => OptionRows.Of(line).Single(row => row.Key == MarketRows.Symbol);
        Assert.Equal(("MSFT", OptionGlyphs.Trend), (Symbol().Text, Symbol().Glyph));
        Assert.True(Symbol().Pick!(MarketSearch.Row("EURCHF=X")));
        Assert.Equal(("EURCHF=X", "", 0.0), (line.Market!.Symbol, line.Market.Detail, line.Market.Holding));
        Assert.Equal(("EUR/CHF", OptionGlyphs.Exchange), (Symbol().Text, Symbol().Glyph));
        Assert.DoesNotContain(MarketRows.Holding, Keys(OptionRows.Of(line)));
        Assert.False(Symbol().Pick!(MarketSearch.Row("EURCHF=X")));
        Assert.True(Symbol().Pick!(MarketSearch.Row("BTC-USD")));
        Assert.Equal(("BTC/USD", OptionGlyphs.Money, MarketKind.Crypto), (Symbol().Text, Symbol().Glyph, line.Market.KindOf));
        Assert.True(Symbol().Pick!(new OptionFound("Apple", "", new Instrument("AAPL", "Apple Inc.", "USD", "NASDAQ", 255, 2))));
        Assert.Equal(("AAPL", "Apple Inc. · NASDAQ · USD"), (line.Market.Symbol, line.Market.Detail));
        // A widget nobody set reads as one with nothing chosen, and its first pick writes its record.
        var fresh = ListKind.Markets.New();
        fresh.Market = null;
        Assert.True(OptionRows.Of(fresh).Single(row => row.Key == MarketRows.Symbol).Pick!(MarketSearch.Row("CHFUSD=X")));
        Assert.Equal("CHFUSD=X", fresh.Market!.Symbol);
    }

    /// <summary>The years only while the range is the last so many; the chart a choice of three; a converter's amount typed, nothing or 1 none.</summary>
    [Fact]
    public void EachRowWritesWhatItSays()
    {
        var rate = Line(new MarketOptions { Symbol = "JPYCHF=X", Style = MarketStyle.Converter });
        Assert.DoesNotContain(MarketRows.Years, Keys(OptionRows.Of(rate)));
        Assert.True(OptionRows.Apply(OptionRows.Of(rate), MarketRows.Range, MarketRanges.All.ToList().IndexOf(MarketRange.Years)));
        Assert.True(OptionRows.Apply(OptionRows.Of(rate), MarketRows.Years, 7));
        Assert.Equal((MarketRange.Years, 7), (rate.Market!.Range, rate.Market.RangeYears));

        var chart = OptionRows.Of(rate).Single(row => row.Key == MarketRows.Chart);
        Assert.Equal([S.SettingsWidgetMarketChartArea, S.SettingsWidgetMarketChartLine, S.SettingsWidgetMarketChartCandles], chart.Choices);
        Assert.Equal(0, chart.Chosen);
        Assert.Equal([OptionGlyphs.Graph, OptionGlyphs.Trend, OptionGlyphs.Bars], chart.ChoiceGlyphs);
        Assert.True(OptionRows.Apply(OptionRows.Of(rate), MarketRows.Chart, 2));
        Assert.Equal(MarketChartStyle.Candles, rate.Market.Chart);

        OptionRow Amount() => OptionRows.Of(rate).Single(row => row.Key == MarketRows.Amount);
        Assert.Equal("1", Amount().Text);
        Assert.True(Amount().SetText!("100"));
        Assert.Equal((100, "100 JPY/CHF"), (rate.Market.Amount, MarketWords.Title(rate.Market)));
        Assert.False(Amount().SetText!("lots"));
        Assert.False(Amount().SetText!("-5"));
        // Its amount is the converter's: the rate in another style is said for one.
        rate.Market.Style = MarketStyle.Full;
        Assert.Equal("JPY/CHF", MarketWords.Title(rate.Market));
        rate.Market.Style = MarketStyle.Converter;
        Assert.True(Amount().SetText!(""));
        Assert.Null(rate.Market.Amount);
    }

    /// <summary>The converter's swap button wears Segoe Fluent's Switch, never no picture (it had none since §258).</summary>
    [Fact]
    public void TheSwapHasItsGlyph() => Assert.Equal("", MarketRows.SwapGlyph);
}
