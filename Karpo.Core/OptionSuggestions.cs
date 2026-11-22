// menus: what a field a person types in offers before and while they type (2026-10-23, the person: "for
// everywhere where you can enter text freely there should be some suggestions on the input (e.g. the feed,
// there should be some feeds already to preselect)"; "suggestions for text fields like rss feed should also
// be other rss feeds contents"; docs\widgets.md §304). One finder for every such field (SuggestFinder), as
// §292's pin target had it:
//
//  - **Nothing typed**: what the person's other widgets hold in a field of the same kind (WordsInUse), then
//    what the field knows of its own — a feed's well-known feeds (KnownFeeds), a clock's label the places
//    of its zone, a share's label its name —; the one it holds now ticked.
//  - **Typed**: the words themselves first where they are a value of their own (any words for a name; an
//    address for a feed), then what holds them, by its name, its quieter line or its value. Enter keeps the
//    first: the words typed, so they stay usable.
//  - **No field with nothing to offer** but what it holds already: it stays a plain line of text.
//  - **The words are the value** (§332, the person: "clearing a label in the clock widget does not work, it
//    just keeps its old value"): what a line of text offers only suggests (OptionFinder.Suggests), so its
//    field is a line of text still — Enter keeps the words, empty too, and so does leaving it; Escape puts
//    the setting back (Flyout.Fields.cs). §304 had made these fields searches, whose words go back as they
//    are left, so a label cleared and left came back.
//  - **A number typed** (§332, the person: "add input recommendatations for number fields as well. (e.g. how
//    many stocks you own)"): an amount owned and a converter's amount (AmountFinder) are offered the amounts
//    the person's other widgets hold — of the same thing first — then round ones; typed, the number a row of
//    its own, written as the field writes one in the person's own marks; words that are no number none.
//
// Free names with nothing sensible to offer have none: a pin's name (its field says the name Windows gives
// what it opens), a list's or a note's name.

namespace Karpo.Core;

/// <summary>
/// §304: a field's suggestions (see the top of the file): <paramref name="offered"/> for nothing typed, the
/// value <paramref name="current"/> ticked; typed, the words as a row of their own (<see cref="Own"/>) and
/// what of <paramref name="offered"/> holds them. Each row's value is the text it keeps.
/// </summary>
public class SuggestFinder(string current, IReadOnlyList<OptionFound> offered) : OptionFinder
{
    /// <summary>A finder for a line of text <paramref name="current"/>, offering <paramref name="offered"/>; null where it offers nothing else.</summary>
    public static SuggestFinder? Of(string current, IEnumerable<OptionFound> offered)
    {
        List<OptionFound> rows = [.. offered.Where(row => KeyOf(row).Length > 0).DistinctBy(KeyOf, StringComparer.CurrentCultureIgnoreCase)];
        return rows.Exists(row => !string.Equals(KeyOf(row), current.Trim(), StringComparison.CurrentCultureIgnoreCase)) ? new SuggestFinder(current, rows) : null;
    }

    /// <summary>Words, each a row whose value is itself: what a field of text is offered.</summary>
    public static IEnumerable<OptionFound> Words(IEnumerable<string?> words) =>
        words.Select(word => (word ?? "").Trim()).Where(word => word.Length > 0).Select(word => new OptionFound(word, "", word));

    /// <summary>Nothing typed asks nothing: <see cref="Suggested"/> shows, and Enter on an empty field keeps it empty.</summary>
    public override int LeastLetters => 1;

    /// <summary>§332: what it offers only suggests; the words typed are the value.</summary>
    public override bool Suggests => true;

    public override IReadOnlyList<OptionFound> Suggested => Ticked(offered);

    public override Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel) =>
        Task.FromResult(query.Trim() is { Length: > 0 } typed ? Ticked(Find(typed, cancel)) : Suggested);

    /// <summary>Enter on words nothing was found for: the words where they are a value of their own.</summary>
    public override OptionChecked? Typed(string text) => Own(text.Trim()) is { } own ? new OptionChecked(own) : null;

    public override string NoneFound(string query) => S.DockMenuNoMatch;

    /// <summary>What <paramref name="typed"/> finds: the words as a row of their own first, then <see cref="More"/>, then what is offered that holds them, what starts with them first.</summary>
    protected IEnumerable<OptionFound> Find(string typed, CancellationToken cancel) =>
        [.. Own(typed) is { } own ? [own] : Array.Empty<OptionFound>(), .. More(typed, cancel), .. Holding(typed)];

    /// <summary>The words typed as a row of their own, which Enter keeps: any words, here; null where they are only words to find by.</summary>
    protected virtual OptionFound? Own(string typed) => new(typed, "", typed);

    /// <summary>What a finder finds for <paramref name="typed"/> beyond what it offers: a path's folders.</summary>
    protected virtual IEnumerable<OptionFound> More(string typed, CancellationToken cancel) => [];

    /// <summary>What it offers that holds <paramref name="typed"/> — in its words, its quieter line or its value —, those whose words start with it first.</summary>
    protected IEnumerable<OptionFound> Holding(string typed) =>
        offered.Where(row => Holds(row, typed)).OrderByDescending(row => row.Title.StartsWith(typed, StringComparison.CurrentCultureIgnoreCase));

    /// <summary>Whether <paramref name="row"/> holds <paramref name="typed"/>: in its words, its quieter line or its value.</summary>
    protected virtual bool Holds(OptionFound row, string typed) =>
        row.Title.Contains(typed, StringComparison.CurrentCultureIgnoreCase)
        || row.Detail.Contains(typed, StringComparison.CurrentCultureIgnoreCase)
        || (row.Value as string ?? "").Contains(typed, StringComparison.OrdinalIgnoreCase);

    /// <summary>Two values the same where these are: a path without its last "\".</summary>
    protected virtual string Same(string value) => value.Trim();

    /// <summary><paramref name="rows"/> each once, the one the field holds now ticked.</summary>
    protected IReadOnlyList<OptionFound> Ticked(IEnumerable<OptionFound> rows) =>
        [.. rows.DistinctBy(row => Same(row.Value as string ?? ""), StringComparer.CurrentCultureIgnoreCase)
            .Select(row => row with { Chosen = string.Equals(Same(row.Value as string ?? ""), Same(current), StringComparison.CurrentCultureIgnoreCase) })];

    private static string KeyOf(OptionFound row) => (row.Value as string ?? "").Trim();
}

/// <summary>
/// §332: a number typed — the amount of a share owned, a converter's amount —: offered <paramref name="offered"/>
/// (the amounts the person's other widgets hold, then round ones), the one held ticked; typed, the number a row
/// of its own, written as the field writes one (<see cref="Currencies.Typed(double, NumberMarks)"/>, in
/// <paramref name="marks"/>), then the amounts that start with its figures; words that are no number have no row.
/// </summary>
public sealed class AmountFinder(string current, IReadOnlyList<OptionFound> offered, NumberMarks marks) : SuggestFinder(current, offered)
{
    /// <summary><paramref name="amount"/> as a row: written as the field writes one, <paramref name="detail"/> beside it ("AAPL").</summary>
    public static OptionFound Row(double amount, NumberMarks marks, string detail = "") =>
        new(Currencies.Typed(amount, marks), detail, Currencies.Typed(amount, marks));

    protected override OptionFound? Own(string typed) => Currencies.Parse(typed, marks) is { } amount ? Row(amount, marks) : null;

    /// <summary>An amount holds the figures it starts with: "2" finds 25, not 12.5.</summary>
    protected override bool Holds(OptionFound row, string typed) => row.Title.StartsWith(typed, StringComparison.Ordinal);

    /// <summary>Two amounts the same where they are one number: "2.50" is "2.5".</summary>
    protected override string Same(string value) =>
        Currencies.Parse(value, marks) is { } amount ? amount.ToString(System.Globalization.CultureInfo.InvariantCulture) : value.Trim();
}

/// <summary>§304: what the person's widgets hold in each field of text, which the same field of another widget offers.</summary>
public static class WordsInUse
{
    /// <summary>
    /// What <paramref name="config"/>'s widgets hold in each field of text, by the key of its row
    /// (<see cref="OptionFields"/>' keys): their lines' and their faces', each once, in the order first met.
    /// A feed's are named as it named itself, under its site. §332: the amounts of shares and coins owned and
    /// the converters' amounts, written in <paramref name="marks"/> (the region's, for null), what each is of beside it.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<OptionFound>> Of(Config config, NumberMarks? marks = null)
    {
        var region = marks ?? NumberMarks.Region();
        Dictionary<string, List<OptionFound>> found = [];
        void Add(string key, OptionFound? row)
        {
            if (row is null || (row.Value as string ?? "").Trim().Length == 0)
            {
                return;
            }
            var rows = found.TryGetValue(key, out var had) ? had : found[key] = [];
            if (!rows.Exists(each => string.Equals(each.Value as string, row.Value as string, StringComparison.CurrentCultureIgnoreCase)))
            {
                rows.Add(row);
            }
        }
        void Word(string key, string? word) => Add(key, SuggestFinder.Words([word]).FirstOrDefault());
        // A line's amount owned and a converter's amount, a line's or a face's.
        void Market(MarketOptions? market)
        {
            if (market is not { IsSet: true })
            {
                return;
            }
            if (MarketHoldings.Holds(market.Holding))
            {
                Add(MarketRows.Holding, AmountFinder.Row(MarketHoldings.From(market.Holding), region, MarketSymbols.Display(market.Symbol)));
            }
            if (market.StyleOf == MarketStyle.Converter)
            {
                Add(MarketRows.Amount, AmountFinder.Row(market.AmountOrOne, region, MarketSymbols.Display(market.Symbol)));
            }
        }
        foreach (var (_, item) in config.Layouts.SelectMany(layout => Folders.All(layout.Islands)))
        {
            Word(OptionFields.ClockLabel, item.Clock?.Label);
            Word(OptionFields.Heading, item.Heading?.Text);
            Word(OptionFields.CountdownName, item.Countdown?.Name);
            Word(OptionFields.AlarmName, item.Alarm?.Name);
            Market(item.Market);
            if (item.Rss is { IsSet: true } rss)
            {
                Add(OptionFields.RssAddress, KnownFeeds.Row(rss.Title.Trim().Length > 0 ? rss.Title.Trim() : Feeds.HostOf(rss.Address), Feeds.HostOf(rss.Address), rss.Address));
            }
            // camera (§331): the other cameras, by the names they go by, and the names given them.
            if (item.Camera is { IsSet: true } camera)
            {
                Add(OptionFields.CameraUrl, KnownCameras.Row(Cameras.NameOf(camera), Cameras.HostOf(camera.Url), camera.Url));
                Word(OptionFields.CameraName, camera.Name);
            }
            // A clock face's label is a clock's label; a markets face's amount a converter's.
            foreach (var face in (item as BarItem)?.Folder?.Faces ?? [])
            {
                Market(face?.Market);
                if (face?.Kind == FolderFaces.ClockId && face.Info.Option(ClockFace.Label) is { } label)
                {
                    Word(OptionFields.ClockLabel, face.Text(label));
                }
            }
        }
        return found.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<OptionFound>)pair.Value);
    }

    /// <summary>What <paramref name="words"/> holds for the field <paramref name="key"/>; none for none.</summary>
    public static IReadOnlyList<OptionFound> For(IReadOnlyDictionary<string, IReadOnlyList<OptionFound>>? words, string key) =>
        words?.GetValueOrDefault(key) ?? [];

    /// <summary>The places of the zone <paramref name="zone"/> (a Windows id, empty for this PC's): what a clock's label is offered of its own, "Osaka", "Sapporo", "Tokyo".</summary>
    public static IEnumerable<OptionFound> ZonePlaces(string? zone)
    {
        var id = (zone ?? "").Trim();
        var name = id.Length == 0 ? TimeZoneInfo.Local.DisplayName
            : TimeZoneSearch.Windows.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? "";
        return SuggestFinder.Words(TimeZoneSearch.Places(name));
    }
}

/// <summary>
/// §304: well-known news feeds a feed's field offers after the person's own: a few for news, technology,
/// science, business and sport, and the main news of a few languages; each one read and proved a feed when
/// this was written (2026-10-23). Those in the person's language and region first.
/// </summary>
public static class KnownFeeds
{
    /// <summary>A feed offered: its name, its site, its address, its language and the region it is of (empty for the world).</summary>
    public sealed record Known(string Name, string Site, string Address, string Language, string Region = "");

    /// <summary>Every one, in the order they are offered among those of the same standing.</summary>
    public static readonly IReadOnlyList<Known> All =
    [
        // News.
        new("BBC News", "bbc.co.uk", "https://feeds.bbci.co.uk/news/rss.xml", "en"), // not-ui: a feed's name
        new("The Guardian", "theguardian.com", "https://www.theguardian.com/world/rss", "en"), // not-ui: a feed's name
        new("The New York Times", "nytimes.com", "https://rss.nytimes.com/services/xml/rss/nyt/HomePage.xml", "en"), // not-ui: a feed's name
        new("NPR News", "npr.org", "https://feeds.npr.org/1001/rss.xml", "en"), // not-ui: a feed's name
        new("Al Jazeera", "aljazeera.com", "https://www.aljazeera.com/xml/rss/all.xml", "en"), // not-ui: a feed's name
        // Technology.
        new("The Verge", "theverge.com", "https://www.theverge.com/rss/index.xml", "en"), // not-ui: a feed's name
        new("Ars Technica", "arstechnica.com", "https://feeds.arstechnica.com/arstechnica/index", "en"), // not-ui: a feed's name
        new("Hacker News", "news.ycombinator.com", "https://news.ycombinator.com/rss", "en"), // not-ui: a feed's name
        new("WIRED", "wired.com", "https://www.wired.com/feed/rss", "en"),
        new("TechCrunch", "techcrunch.com", "https://techcrunch.com/feed/", "en"),
        // Science.
        new("Nature", "nature.com", "https://www.nature.com/nature.rss", "en"),
        new("NASA", "nasa.gov", "https://www.nasa.gov/news-release/feed/", "en"),
        new("ScienceDaily", "sciencedaily.com", "https://www.sciencedaily.com/rss/all.xml", "en"),
        // Business, sport.
        new("CNBC", "cnbc.com", "https://www.cnbc.com/id/100003114/device/rss/rss.html", "en"),
        new("BBC Sport", "bbc.co.uk", "https://feeds.bbci.co.uk/sport/rss.xml", "en"), // not-ui: a feed's name
        // German, and Switzerland's.
        new("tagesschau", "tagesschau.de", "https://www.tagesschau.de/xml/rss2/", "de"),
        new("heise online", "heise.de", "https://www.heise.de/rss/heise-atom.xml", "de"),
        new("DER SPIEGEL", "spiegel.de", "https://www.spiegel.de/schlagzeilen/index.rss", "de"), // not-ui: a feed's name
        new("SRF News", "srf.ch", "https://www.srf.ch/news/bnf/rss/1646", "de", "CH"), // not-ui: a feed's name
        new("NZZ", "nzz.ch", "https://www.nzz.ch/recent.rss", "de", "CH"),
        new("RTS Info", "rts.ch", "https://www.rts.ch/info/?format=rss/news", "fr", "CH"), // not-ui: a feed's name
        // French, Spanish, Italian, Japanese.
        new("Le Monde", "lemonde.fr", "https://www.lemonde.fr/rss/une.xml", "fr"), // not-ui: a feed's name
        new("EL PAÍS", "elpais.com", "https://feeds.elpais.com/mrss-s/pages/ep/site/elpais.com/portada", "es"), // not-ui: a feed's name
        new("ANSA", "ansa.it", "https://www.ansa.it/sito/ansait_rss.xml", "it"),
        new("NHKニュース", "nhk.or.jp", "https://www3.nhk.or.jp/rss/news/cat0.xml", "ja"),
    ];

    /// <summary>
    /// <see cref="All"/> as a feed's field offers them to a person reading Karpo in <paramref name="language"/>
    /// (Karpo's, "de", "pt-BR", for null) in <paramref name="region"/> (the country Windows' region is set to,
    /// "CH", for null): their language's of their region first, then their language's, then their region's,
    /// then English, then the rest; each in the list's order.
    /// </summary>
    public static IReadOnlyList<OptionFound> For(string? language = null, string? region = null)
    {
        var own = (language ?? Strings.Language).Split('-')[0];
        var where = region ?? PublicHolidays.Home();
        int Standing(Known feed)
        {
            var speaks = string.Equals(feed.Language, own, StringComparison.OrdinalIgnoreCase);
            var here = feed.Region.Length > 0 && string.Equals(feed.Region, where, StringComparison.OrdinalIgnoreCase);
            return speaks && here ? 0 : speaks && feed.Region.Length == 0 ? 1 : speaks || here ? 2 : feed.Language == "en" ? 3 : 4;
        }
        return [.. All.OrderBy(Standing).Select(feed => Row(feed.Name, feed.Site, feed.Address))];
    }

    /// <summary>A feed as a row of a feed's field: its name, its site under it, its address what it keeps.</summary>
    public static OptionFound Row(string name, string site, string address) => new(name, site, address) { Glyph = OptionGlyphs.Feed };
}
