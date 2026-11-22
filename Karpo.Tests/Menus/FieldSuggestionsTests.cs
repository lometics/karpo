// menus: every field a person types in offers what it can (2026-10-23, the person: "for everywhere where
// you can enter text freely there should be some suggestions on the input (e.g. the feed, there should be
// some feeds already to preselect)"; "suggestions for text fields like rss feed should also be other rss
// feeds contents"; Karpo.Core\OptionSuggestions.cs, docs\widgets.md §304). For each kind of field: nothing
// typed, what the person's other widgets hold first, then what it knows of its own, the value it holds
// ticked; typed, the words first and what holds them; Enter keeps the words typed.

using Karpo.Dock;
using Karpo.Tests.Localization;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class FieldSuggestionsTests
{
    private static CellItem Rss(string address, string title)
    {
        var rss = ListKind.Rss.New();
        rss.Rss = new RssOptions { Address = address, Title = title };
        return rss;
    }

    private static Config ConfigOf(params CellItem[] lines) => new() { Layouts = [new TaskbarLayout { Islands = [Holding(lines)] }] };

    /// <summary>A folder whose lines are <paramref name="lines"/>.</summary>
    private static BarItem Holding(params CellItem[] lines)
    {
        var folder = BarKind.FolderList.New();
        foreach (var line in lines)
        {
            CellItems.Put(folder.Folder!, line);
        }
        return folder;
    }

    private static OptionDevices Using(Config config) => new() { Words = WordsInUse.Of(config) };

    private static OptionRow Row(WidgetItem item, OptionDevices devices, string key) => OptionRows.Of(item, devices).Single(row => row.Key == key);

    private static async Task<IReadOnlyList<OptionFound>> Typed(OptionFinder finder, string query) => await finder.FindAsync(query, CancellationToken.None);

    /// <summary>
    /// A feed's field: nothing typed, the person's other feeds first, then the well-known ones, the one it
    /// follows ticked; typed, the feeds whose name, site or address hold the words, an address typed a row
    /// of its own first; words that are no address and find nothing are no feed.
    /// </summary>
    [Fact]
    public async Task AFeedsFieldOffersTheOtherFeedsThenWellKnownOnes()
    {
        var mine = Rss("https://www.nzz.ch/recent.rss", "NZZ");
        var other = Rss("https://blog.example.com/feed.xml", "Example Blog");
        var devices = Using(ConfigOf(mine, other));
        var finder = Row(mine, devices, OptionFields.RssAddress).Finder!;
        Assert.True(finder.Finds);

        var offered = finder.Suggested;
        Assert.Equal(("NZZ", true), (offered[0].Title, offered[0].Chosen));
        Assert.Equal(("Example Blog", "blog.example.com", "https://blog.example.com/feed.xml"), (offered[1].Title, offered[1].Detail, offered[1].Value));
        // The well-known after them, each once: NZZ's own is the person's already.
        Assert.Equal(KnownFeeds.All.Count + 1, offered.Count);
        Assert.Single(offered, row => row.Value as string == "https://www.nzz.ch/recent.rss");
        Assert.Single(offered, row => row.Chosen);
        Assert.All(offered, row => Assert.Equal(OptionGlyphs.Feed, row.Glyph));

        Assert.Equal(["heise online"], (await Typed(finder, "heise")).Select(row => row.Title));
        Assert.Equal(["Example Blog"], (await Typed(finder, "example.com/f")).Skip(1).Select(row => row.Title));
        var address = (await Typed(finder, "example.com/f"))[0];
        Assert.Equal(("example.com", "https://example.com/f"), (address.Title, address.Value));
        Assert.Empty(await Typed(finder, "zzzz"));
        Assert.Equal(S.SettingsRssAddressNotAddress, finder.Typed("no feed here")!.Value.Said);
        Assert.Equal("https://example.com/feed", finder.Typed("example.com/feed")!.Value.Found!.Value);
    }

    /// <summary>The well-known feeds: each an address Karpo opens, each once, the person's language's and region's first.</summary>
    [Fact]
    public void TheWellKnownFeedsComeInThePersonsLanguageFirst()
    {
        Assert.All(KnownFeeds.All, feed => Assert.True(Feeds.Opens(feed.Address), feed.Name));
        Assert.Equal(KnownFeeds.All.Count, KnownFeeds.All.Select(feed => feed.Address).Distinct().Count());
        Assert.InRange(KnownFeeds.All.Count, 15, 25);

        static string[] First(string language, string region, int count) =>
            [.. KnownFeeds.For(language, region).Take(count).Select(row => row.Title)];
        Assert.Equal(["SRF News", "NZZ", "tagesschau"], First("de", "CH", 3));
        Assert.Equal(["tagesschau", "heise online", "DER SPIEGEL", "SRF News"], First("de", "DE", 4));
        Assert.Equal(["RTS Info", "Le Monde"], First("fr", "CH", 2));
        Assert.Equal("BBC News", First("en", "US", 1)[0]);
        Assert.Equal("NHKニュース", First("ja", "JP", 1)[0]);
        // The rest after their own: English first.
        Assert.Equal("BBC News", First("ja", "JP", 2)[1]);
    }

    /// <summary>
    /// A clock's label: the other clocks' labels (their faces' and their lines', as met), then the places of its
    /// zone; typed, the words first, then what holds them; Enter keeps the words, an empty field empty; a
    /// pick or the words are what is kept.
    /// </summary>
    [Fact]
    public async Task AClocksLabelOffersTheOthersLabelsThenItsZonesPlaces()
    {
        var clock = ListKind.Clock.New();
        clock.Clock!.TimeZone = "Tokyo Standard Time";
        clock.Clock.Label = "Home";
        var other = ListKind.Clock.New();
        other.Clock!.Label = "Office";
        var folder = Holding(clock, other);
        var face = ClockFace.New();
        face.Set(FolderFaces.Clock.Option(ClockFace.Label)!, "Studio");
        folder.Folder!.Faces = [face];
        var devices = new OptionDevices { Words = WordsInUse.Of(new Config { Layouts = [new TaskbarLayout { Islands = [folder] }] }) };

        var label = Row(clock, devices, OptionFields.ClockLabel);
        Assert.Equal(OptionRowKind.Text, label.Kind);
        var finder = label.Finder!;
        Assert.Equal(["Studio", "Home", "Office", "Osaka", "Sapporo", "Tokyo"], finder.Suggested.Select(row => row.Title));
        Assert.Equal(["Home"], finder.Suggested.Where(row => row.Chosen).Select(row => row.Title));

        Assert.Equal(["o", "Office", "Osaka", "Studio", "Home", "Sapporo", "Tokyo"], (await Typed(finder, "o")).Select(row => row.Title));
        Assert.Equal(["tokyo"], (await Typed(finder, "tokyo")).Select(row => row.Title));
        Assert.Equal("Tok", finder.Typed(" Tok ")!.Value.Found!.Value);
        Assert.Equal("", finder.Typed("")!.Value.Found!.Value);

        // Kept: the words typed, or the row picked.
        var pick = new OptionPick(OptionMenu.FirstId, OptionPickKind.Field, -1, "", OptionFields.ClockLabel, 0);
        Assert.True(FieldPicks.Keep(clock, pick, new FieldCommit(pick.Id, "Tok", finder.Suggested[5]), devices));
        Assert.Equal("Tokyo", clock.Clock.Label);
        Assert.True(FieldPicks.Keep(clock, pick, new FieldCommit(pick.Id, "Tok", null), devices));
        Assert.Equal("Tok", clock.Clock.Label);

        // A clock face's label is offered alike, its own zone's places (this PC's here).
        var faceLabel = OptionRows.OfFace(folder.Folder, 0, devices).Single(row => row.Key == ClockFace.Label);
        Assert.Equal(["Studio", "Home", "Office"], faceLabel.Finder!.Suggested.Take(3).Select(row => row.Title));
        Assert.Equal(["Studio"], faceLabel.Finder.Suggested.Where(row => row.Chosen).Select(row => row.Title));
    }

    /// <summary>
    /// The names: a heading's words, a timer's, a focus session's and an alarm's name are offered what the
    /// others hold; a field with nothing else to offer is a plain line of text, as a pin's name, a list's
    /// and an amount always are. A share's label is offered its name.
    /// </summary>
    [Fact]
    public void ANamesFieldOffersTheOthersNamesWhereThereAreAny()
    {
        CellItem Headed(string text)
        {
            var item = ListKind.Heading.New();
            item.Heading = new HeadingOptions { Text = text };
            return item;
        }
        var work = Headed("Work");
        var timer = ListKind.Timer.New();
        timer.Countdown = new CountdownOptions { Minutes = 5, Name = "Tea" };
        var alarm = ListKind.Alarm.New();
        alarm.Alarm = new AlarmOptions { Name = "Wake up" };
        var config = ConfigOf(work, Headed("Media"), Headed("work"), timer, alarm);
        var devices = Using(config);

        Assert.Equal(["Work", "Media"], Row(Headed(""), devices, OptionFields.Heading).Finder!.Suggested.Select(row => row.Title));
        Assert.Equal(["Tea"], Row(ListKind.Focus.New(), devices, OptionFields.CountdownName).Finder!.Suggested.Select(row => row.Title));
        Assert.Equal(["Wake up"], Row(ListKind.Alarm.New(), devices, OptionFields.AlarmName).Finder!.Suggested.Select(row => row.Title));
        // Nothing but what it holds itself: no suggestions, a plain field.
        Assert.Null(Row(alarm, devices, OptionFields.AlarmName).Finder);
        Assert.Null(Row(ListKind.Alarm.New(), new OptionDevices(), OptionFields.AlarmName).Finder);

        var share = ListKind.Markets.New();
        share.Market = new MarketOptions { Symbol = "MSFT", Detail = "Microsoft Corporation · NASDAQ · USD" };
        Assert.Equal(["Microsoft Corporation"], Row(share, devices, MarketRows.Label).Finder!.Suggested.Select(row => row.Title));
    }

    /// <summary>
    /// Enter in a field with suggestions keeps the words typed even where a suggestion holds them ("Ho" is
    /// not "Home"), a suggestion only where it is picked; on an empty field, nothing but the empty field.
    /// </summary>
    [Fact]
    public void EnterKeepsTheWordsTyped()
    {
        var finder = SuggestFinder.Of("Office", SuggestFinder.Words(["Home", "Office"]))!;
        List<Action> posted = [];
        var search = new MenuSearch(finder, posted.Add, () => { });
        search.Opened();
        // §411: the field's own words first, ticked.
        Assert.Equal(["Office", "Home"], search.Rows.Select(row => row.Title));
        Assert.False(search.Typed("Ho", Environment.TickCount64));
        Assert.Equal(["Ho", "Home"], search.Rows.Select(row => row.Title));
        OptionFound? kept = null;
        search.Enter("Ho", found => kept = found);
        Assert.Equal("Ho", kept!.Value);

        search.Typed("", Environment.TickCount64);
        Assert.Equal(["Office", "Home"], search.Rows.Select(row => row.Title));
        search.Enter("", found => kept = found);
        Assert.Equal("", kept.Value);

        // Nothing to offer but what it holds: none.
        Assert.Null(SuggestFinder.Of("Home", SuggestFinder.Words(["Home", " ", null])));
    }

    /// <summary>
    /// §332 (the person: "clearing a label in the clock widget does not work, it just keeps its old value.
    /// this should be fixed for similar input fields as well."): every line of text a widget's menu keeps
    /// is emptied by nothing kept, back to its default — a clock's label and a clock face's, a heading's
    /// words, a timer's and an alarm's name, a pin's name, a share's name on the bar, the amount owned (none)
    /// and a converter's amount (1). A line's offers only suggest; a search's finders do not.
    /// </summary>
    [Fact]
    public void EveryLineOfTextIsEmptiedByNothingKept()
    {
        static bool Empty(WidgetItem item, string key, int face = -1, string faceKind = "") =>
            FieldPicks.Keep(item, new OptionPick(OptionMenu.FirstId, OptionPickKind.Field, face, faceKind, key, 0), new FieldCommit(OptionMenu.FirstId, "", null));

        var clock = ListKind.Clock.New();
        clock.Clock!.Label = "Home";
        Assert.True(Empty(clock, OptionFields.ClockLabel));
        Assert.Equal("", clock.Clock.Label);

        var heading = ListKind.Heading.New();
        heading.Heading = new HeadingOptions { Text = "Work" };
        Assert.True(Empty(heading, OptionFields.Heading));
        Assert.Equal("", heading.Heading.Text);

        var timer = ListKind.Timer.New();
        timer.Countdown = new CountdownOptions { Minutes = 5, Name = "Tea" };
        Assert.True(Empty(timer, OptionFields.CountdownName));
        Assert.Equal("", timer.Countdown.Name);

        var alarm = ListKind.Alarm.New();
        alarm.Alarm = new AlarmOptions { Name = "Wake up" };
        Assert.True(Empty(alarm, OptionFields.AlarmName));
        Assert.Equal("", alarm.Alarm.Name);

        var pin = ListKind.Pin.New();
        pin.Pin!.Name = "Docs";
        Assert.True(Empty(pin, OptionFields.PinName));
        Assert.Equal("", pin.Pin.Name);

        var share = ListKind.Markets.New();
        share.Market = new MarketOptions { Symbol = "MSFT", Holding = 10, Label = "Soft" };
        Assert.True(Empty(share, MarketRows.Label));
        Assert.True(Empty(share, MarketRows.Holding));
        Assert.Equal(("", 0d), (share.Market.Label, share.Market.Holding));

        var converter = ListKind.Markets.New();
        converter.Market = new MarketOptions { Symbol = "EURCHF=X", Style = MarketStyle.Converter, Amount = 100 };
        Assert.True(Empty(converter, MarketRows.Amount));
        Assert.Equal((null, 1d), (converter.Market.Amount, converter.Market.AmountOrOne));

        var folder = Holding();
        var face = ClockFace.New();
        face.Set(FolderFaces.Clock.Option(ClockFace.Label)!, "Studio");
        folder.Folder!.Faces = [face];
        Assert.True(Empty(folder, ClockFace.Label, 0, FolderFaces.ClockId));
        Assert.Equal("", ClockFace.Read(face).Label);

        // What a line offers only suggests; a search's finder finds what is kept.
        var devices = new OptionDevices { Words = WordsInUse.Of(ConfigOf(clock, Rss("https://www.nzz.ch/recent.rss", "NZZ"))) };
        clock.Clock.TimeZone = "Tokyo Standard Time";
        Assert.True(Row(clock, devices, OptionFields.ClockLabel).Finder!.Suggests);
        Assert.False(Row(Rss("https://blog.example.com/feed.xml", "Blog"), devices, OptionFields.RssAddress).Finder!.Suggests);
        Assert.False(Row(clock, devices, OptionFields.ClockZone).Finder!.Suggests);
    }

    /// <summary>
    /// §332 (the person: "add input recommendatations for number fields as well. (e.g. how many stocks you
    /// own)"): the amount owned is offered what the person owns — of the same share first — then round
    /// amounts, a coin's smaller ones; a converter's amount the other converters' then 1, 10, 100, 1000; each
    /// once, the one held ticked. Typed, the number is a row of its own written as the field writes one, words
    /// that are no number none; Enter keeps the words.
    /// </summary>
    [Fact]
    public async Task AnAmountsFieldOffersTheAmountsOwnedThenRoundOnes()
    {
        var swiss = new NumberMarks(".", "’");
        MarketOptions Owning(string symbol, double holding) => new() { Symbol = symbol, Holding = holding };
        CellItem Line(MarketOptions market)
        {
            var line = ListKind.Markets.New();
            line.Market = market;
            return line;
        }
        var apple = Line(Owning("AAPL", 12.5));
        var config = ConfigOf(Line(Owning("MSFT", 3)), apple, Line(Owning("AAPL", 40)), Line(Owning("BTC-USD", 0.25)), Line(new MarketOptions { Symbol = "EURCHF=X", Style = MarketStyle.Converter, Amount = 250 }));
        var devices = new OptionDevices { Words = WordsInUse.Of(config, swiss) };

        var held = Row(apple, devices, MarketRows.Holding);
        Assert.Equal(OptionRowKind.Text, held.Kind);
        var finder = held.Finder!;
        Assert.True(finder.Suggests);
        // AAPL's first (its own ticked), then the rest as met, then the round amounts, 5 once.
        Assert.Equal(["12.5", "40", "3", "0.25", "1", "5", "10", "25", "50", "100"], finder.Suggested.Select(row => row.Title));
        Assert.Equal(["AAPL", "AAPL", "MSFT", "BTC/USD"], finder.Suggested.Take(4).Select(row => row.Detail));
        Assert.Equal(["12.5"], finder.Suggested.Where(row => row.Chosen).Select(row => row.Title));

        // Typed: the number first, then the amounts holding its figures; words that are no number find none of their own.
        Assert.Equal(["2", "25"], (await Typed(finder, "2")).Select(row => row.Title));
        Assert.Equal("12.5", (await Typed(finder, "12.50"))[0].Value);
        Assert.Empty(await Typed(finder, "ten"));

        // A coin: smaller round amounts. A converter: the others' amounts, then 1, 10, 100, 1000.
        var coin = Line(Owning("ETH-USD", 0));
        Assert.Equal(["0.01", "0.1", "0.5"], Row(coin, new OptionDevices(), MarketRows.Holding).Finder!.Suggested.Select(row => row.Title).Take(3));
        var converter = Line(new MarketOptions { Symbol = "USDJPY=X", Style = MarketStyle.Converter });
        var amounts = Row(converter, devices, MarketRows.Amount).Finder!.Suggested;
        Assert.Equal(["250", "1", "10", "100", "1000"], amounts.Select(row => row.Title));
        Assert.Equal(["1"], amounts.Where(row => row.Chosen).Select(row => row.Title));
    }

    /// <summary>
    /// §332: an amount is written for its field in the person's own marks, so it reads back as itself: in
    /// Germany "0,125" — "0.125" read there was 125 —; in Switzerland and the US "0.125".
    /// </summary>
    [Fact]
    public void AnAmountIsWrittenToReadBackAsItself()
    {
        var german = new NumberMarks(",", ".");
        var swiss = new NumberMarks(".", "’");
        Assert.Equal("0,125", Currencies.Typed(0.125, german));
        Assert.Equal(0.125, Currencies.Parse(Currencies.Typed(0.125, german), german));
        Assert.Equal(1234.5, Currencies.Parse(Currencies.Typed(1234.5, german), german));
        Assert.Equal("1234.5", Currencies.Typed(1234.5, swiss));
        Assert.Equal(0.125, Currencies.Parse(Currencies.Typed(0.125, swiss), swiss));
        Assert.Equal("0,125", AmountFinder.Row(0.125, german).Value);
    }
}
