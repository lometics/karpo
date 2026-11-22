// rss: the RSS widget (docs\widgets.md §39), one news feed: a line of a list folder, and what the
// news face on the bar draws.
//
// In a list folder: the feed's line, laid out for the height it is given as the mail's is
// (docs\widgets.md §9, "A line at every height"): the newest headline and its age on one row; the
// feed's name over its newest headlines a line each; and the whole widget — what the headlines
// panel that a click on the entry opened was until it went (2026-10-10, §98): the feed's name with
// the stale mark at its end while a read failed and the last one's headlines are shown (StaleMark.cs); each
// headline over where it is from and how old it is, its picture in the leading column where the others
// show their icons (§408; fetched off the dock's thread by the store, the feed's mark there until it
// comes) — every headline, scrolling under the name where there are more than the line holds
// (ListScroll.cs), in the item style chosen (ItemForm). Nothing stands under them: the footer's "Open
// news.example.com" went into the feed's name, which opens the site (2026-10-29: "the open xyz button
// should not be on the bottom but should be when i click on the title of the feed").
//
// **A click in a folder opens a story, never a popup** (docs\widgets.md §15): a headline is a target
// of its own (CardTarget), and its click is an app opening — the story in the browser — which the
// folder carries out as it carries out a pin's (Dock.FolderForward), closing as the browser comes.
// The feed's name is the same, with the site. Only an http or https link is ever opened (Feeds.Opens).
//
// Headlines that came since the folder was last opened wear the accent's dot — on the face's glyph,
// and on a line on the corner of a new headline's picture. Opening the folder is seeing them, as
// opening the panel was: the dots on the line stay for that showing, and the face's goes at once.
//
// **The headlines' words stand at the text edge** (§401), their pictures before them in the leading
// column (§408; 2026-10-11 they were at the line's end, and the leading column stood empty). A headline,
// and the feed's name, light under the pointer and pressed as a Windows 11 list item does (RowPress).
//
// On the bar the news face draws the entry as it was (PaintFace): the news glyph, and where the face
// is set to, the newest headline beside it in a room of its own that never follows the headline; the
// glyph alone down a bar on its side.
//
// **Its mark** (§306, the person: "for the rss show the favicon of the website from which the news
// come from. alternatively you should also be able to select an icon to override the selection"):
// on the line and the face alike, the icon chosen in its menu (RssOptions.Glyph), else its site's own
// favicon at the glyph's size (RssStore.Icon), else the news glyph, as before it came or where the
// site has none (Mark).
//
// The feed is read through RssStore, once per address however many widgets show it, and RssData keeps
// it read from the dock's start; nothing is fetched until an address is set, and its first draw shows
// what rss.json kept from the last run.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.Rss;

/// <summary>What a widget's feed is doing, for what it draws.</summary>
internal enum RssState
{
    /// <summary>No address is set.</summary>
    NoFeed,

    /// <summary>Set, and nothing read yet.</summary>
    Loading,

    /// <summary>Set, the read failed, and nothing was read before.</summary>
    Failed,

    /// <summary>Read, and the feed has nothing in it.</summary>
    Empty,

    /// <summary>Read, with headlines.</summary>
    Ready,
}

internal sealed unsafe class RssIsland : CellIsland, IListContent, IListScroll, ILinePieces, IRefreshable, ILineTargets
{
    /// <summary>Segoe Fluent Icons: News, the widget's glyph wherever it stands.</summary>
    internal const string NewsGlyph = "";

    /// <summary>
    /// Headlines a new list line asks the list for room for, at most: where a new line starts. A
    /// line made taller shows more, as many as the feed has and the height holds.
    /// </summary>
    private const int LineMost = 8;

    /// <summary>
    /// The room a headline is given on the bar, measured as a template and never as the headline:
    /// about twenty-six letters of the bar's text. A width somebody chose gives it more.
    /// </summary>
    internal static readonly string HeadlineTemplate = new('n', 26);

    /// <summary>The site's link's target: past any headline's number.</summary>
    internal const int SiteTarget = 100_002;

    private readonly RssStore store;
    private RssOptions options = new();
    private bool configured;
    private RssRead read = new(null, null, null, false, new HashSet<string>());

    // What was new as the open folder showed the line: its dots stay for that showing, as the panel's
    // did, after the feed has been told they are seen (Visible).
    private HashSet<string> fresh = new(StringComparer.Ordinal);


    // folders: the headline under the pointer on a card or a line, lit as a row is; -1 for none. The
    // headline a press is down on (from 1), lit pressed.
    private int hot = -1;
    private RowPress press;

    public RssIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, Made ?? (Samples.On ? Samples.Feeds() : RssStore.Shared))
    {
    }

    [ThreadStatic]
    private static RssStore? made;

    /// <summary>
    /// A test's own store, per thread, read as a widget is made the ordinary way — by a folder, which
    /// makes its widgets itself: its feeds are the widget's, and nothing reaches the network. Unset,
    /// the widget reads the shared store.
    /// </summary>
    internal static RssStore? Made
    {
        get => made;
        set => made = value;
    }

    /// <param name="store">Where its feed comes from; a test hands in one on its own source, so nothing reaches the network.</param>
    internal RssIsland(string key, WidgetItem island, Gfx gfx, Config config, RssStore store)
        : base(key, island, gfx)
    {
        this.store = store;
        Configure(island, config);
    }

    /// <summary>What this widget was set to: its feed.</summary>
    internal RssOptions Options => options;

    /// <summary>The store this widget reads, so its panel listens to the same one.</summary>
    internal RssStore Store => store;

    /// <summary>The last read of its feed, why a newer one isn't there, whether one is coming, what is new.</summary>
    internal RssRead Read => read;

    /// <summary>Reads the feed now, however recent the last read: "Refresh". False where none started.</summary>
    internal bool Ask() => store.Ask(this);

    /// <summary>A feed to read ("Refresh", IRefreshable, §451).</summary>
    public bool CanRefresh => options.IsSet;

    /// <summary>A read is on its way.</summary>
    public bool Refreshing => store.Read(this).Fetching;

    /// <summary>When the headlines shown were read.</summary>
    public DateTime? UpdatedUtc => read.FetchedUtc;

    /// <summary>Why the feed could not be read.</summary>
    string? IRefreshable.Problem => store.Read(this).Problem;

    /// <summary>"Refresh": the feed read now, however recent the last read.</summary>
    public void RefreshNow()
    {
        _ = Ask();
        read = store.Read(this);
    }

    /// <summary>
    /// The open folder shows the line: what it shows has been seen, and the feed is told so — the
    /// face's dot goes now — while the line's dots stay on what was new for this showing, as the
    /// panel's did.
    /// </summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            fresh.Clear();
            hot = -1;
            _ = press.Up();
            return;
        }
        read = store.Read(this);
        fresh = new HashSet<string>(Headlines.Where(read.IsNew).Select(item => item.Key), StringComparer.Ordinal);
        store.Seen(this);
        read = store.Read(this);
    }

    /// <summary>Whether <paramref name="item"/> wears the new dot on the line: new as the folder opened, or come since.</summary>
    private bool IsNew(FeedItem item) => fresh.Contains(item.Key) || read.IsNew(item);

    /// <summary>
    /// Where a story opens: the browser, through the shell, as a pin's website does. A test puts its
    /// own here and writes down what it was handed. Only ever handed an http or https address.
    /// </summary>
    internal Action<string> Opened { get; set; } = static link => Launch.Open(link);

    /// <summary>The feed's own site, for the menu's "Open website" and the feed's name; null where it named none Karpo opens.</summary>
    internal string? Site => read.Feed?.Site is { } site && Feeds.Opens(site) ? site : null;

    /// <summary>
    /// What the feed is drawn with at <paramref name="px"/> (§306): the icon chosen for it, else its
    /// site's own (RssStore.Icon), else the news glyph — a glyph, or the site's picture and no glyph.
    /// </summary>
    internal (string Glyph, Pixels? Picture) Mark(float px) =>
        options.Glyph is { Length: > 0 } chosen ? (chosen, null)
        : store.Icon(this, (int)MathF.Round(px)) is { } site ? ("", site)
        : (NewsGlyph, null);

    /// <summary>
    /// The feed's mark (<see cref="Mark"/>) in a <paramref name="px"/> square centred in
    /// <paramref name="box"/>, a glyph in <paramref name="ink"/>; the news glyph where there is no feed.
    /// </summary>
    private static void PaintMark(LineSketch sketch, RssIsland? feed, float px, DXGI_RGBA ink, D2D_RECT_F box, string name = "glyph")
    {
        var (glyph, picture) = feed?.Mark(px) ?? (NewsGlyph, null);
        if (picture is null)
        {
            sketch.Glyph(name, glyph, px, ink, box);
            return;
        }
        var side = PopupPaint.Round(px);
        var square = PopupPaint.Rect(
            PopupPaint.Round(((box.left + box.right) / 2) - (side / 2)), PopupPaint.Round(((box.top + box.bottom) / 2) - (side / 2)), side, side);
        sketch.Picture(name, square, (dc, at) => PopupPaint.Picture(dc, picture, at, 0));
    }

    /// <summary>The headlines in the order every drawing shows them: the feed's, newest first.</summary>
    internal IReadOnlyList<FeedItem> Headlines => read.Feed?.Items ?? [];

    /// <summary>What the widget's feed is doing now (<see cref="RssState"/>).</summary>
    internal RssState State =>
        !options.IsSet ? RssState.NoFeed
        : read.Feed is { } feed ? feed.Items.Count == 0 ? RssState.Empty : RssState.Ready
        : read.Problem is not null ? RssState.Failed
        : RssState.Loading;

    /// <summary>The feed's name, as the feed gives it, else as it was proved in the settings, else the widget's.</summary>
    internal string FeedTitle =>
        read.Feed?.Title is { Length: > 0 } title ? title
        : options.Title is { Length: > 0 } proved ? proved
        : Info.Title;

    /// <summary>A minute, while a feed is set: the store decides when it is due, and the ages move on.</summary>
    public override int IntervalMs => options.IsSet ? 60_000 : 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        // The shape is a function of the settings: the new ones go where it is measured from.
        Settings = island;
        var next = island.Rss ?? new RssOptions();
        var copy = new RssOptions { Address = next.Address ?? "", Title = next.Title ?? "", Glyph = next.Glyph };
        // §635: a ghost's line, following no feed yet, follows the samples' headlines.
        if (!copy.IsSet && Sampled)
        {
            copy.Address = Samples.FeedAddress;
        }
        var same = configured && RssStore.KeyOf(copy) == RssStore.KeyOf(options);
        options = copy;
        configured = true;
        if (!same)
        {
            store.Use(this, options);
            store.Poll(this);
            read = store.Read(this);
        }
        Forget();
    }

    /// <summary>
    /// What a screen reader says of the news face: the feed and its newest headline. The bar shows no
    /// tooltip over it — a click opens the folder, which says it (§367).
    /// </summary>
    public override string? Tooltip(int index) =>
        Headlines.Count > 0 ? $"{FeedTitle}\n{Headlines[0].Title}" : $"{FeedTitle}\n{Why()}";

    /// <summary>
    /// In a folder: a headline (an index from 1, the line's own target) or the site's link is an app
    /// opening — the story or the site in the browser, which the folder carries out
    /// (<see cref="Open"/>); anywhere else does nothing.
    /// </summary>
    public override IslandAction Click(int index)
    {
        if (press.Up())
        {
            _ = Wake();
        }
        return Link(index) is not null ? new(View: IslandView.App) : default;
    }

    /// <summary>Opens headline <paramref name="index"/> (from 1, as <see cref="Click"/> numbers them), or the site, in the browser; nothing for any other.</summary>
    internal void Open(int index)
    {
        if (Link(index) is { } link)
        {
            Opened(link);
        }
    }

    /// <summary>Headline <paramref name="index"/>'s link (from 1), or the site's, where it is one Karpo opens; else null.</summary>
    private string? Link(int index) =>
        index == SiteTarget ? Site
        : index >= 1 && index <= Headlines.Count && Headlines[index - 1].Link is { } link && Feeds.Opens(link) ? link : null;

    public override void Dispose()
    {
        store.Release(this);
        base.Dispose();
    }

    protected override bool Sample(bool everything)
    {
        if (everything)
        {
            store.Deliver(this);
        }
        store.Poll(this);
        read = store.Read(this);
        return false;
    }

    /// <summary>Its age words move on with the clock.</summary>
    public override bool MinuteChanged(Gfx gfx)
    {
        read = store.Read(this);
        return false;
    }

    /// <summary>Why there are no headlines to show, in the face's short words; empty when there are some.</summary>
    private string Why() => State switch
    {
        RssState.NoFeed => S.WidgetRssNoFeed,
        RssState.Loading => S.WidgetRssLoading,
        RssState.Failed => S.WidgetRssCannotRead,
        RssState.Empty => S.WidgetRssNothing,
        _ => "",
    };

    /// <summary>The same, shorter, for where there is little room: a bar's cell, a small card.</summary>
    private string WhyShort() => State switch
    {
        RssState.NoFeed => S.WidgetRssNoFeedShort,
        RssState.Loading => S.WidgetRssLoadingShort,
        RssState.Failed => S.WidgetRssNotRead,
        RssState.Empty => S.WidgetRssNothing,
        _ => "",
    };

    /// <summary>How old <paramref name="item"/> is, as the face says it: "2 h".</summary>
    private static string AgeOf(FeedItem item) => Feeds.Age(item.Published, new DateTimeOffset(Clock.Now()));

    /// <summary>The line under a headline: the feed's name and the item's age.</summary>
    private string BylineOf(FeedItem item) => Feeds.Byline(FeedTitle, AgeOf(item));

    // --- As a folder's face ------------------------------------------------------------
    //
    // What the RSS entry on the bar drew until it went (2026-10-10, docs\widgets.md §98), from the
    // folder's first feed line: the news glyph, the accent's dot on it while something new has come,
    // and where the face is set to say it, the newest headline beside it in a room of its own.

    /// <summary>
    /// The news face's length along the bar, without the button's ends: the glyph's square alone, or
    /// the glyph and <paramref name="said"/>, the headline it shows now (<see cref="FaceSaid"/>), as long
    /// as it reads up to the room of <see cref="HeadlineTemplate"/>, past which it is cut
    /// (docs\widgets.md §244); that whole room where <paramref name="said"/> is null.
    /// </summary>
    internal static FaceSize FaceLength(Gfx gfx, in IslandStyle style, bool headline, string? said = null)
    {
        var fit = new Fit(style);
        var glyph = fit.Glyph();
        if (style.Vertical || !headline)
        {
            return new(glyph, Square: true);
        }
        var most = TextLine.Template(gfx, HeadlineTemplate, fit.Primary, Weight.Regular).Width;
        var words = said is null ? most : MathF.Min(most, MathF.Ceiling(PopupPaint.Measure(gfx, said, fit.Primary, Weight.Regular).X));
        return new(glyph + fit.Gap + words, Square: false);
    }

    /// <summary>What the news face says after its glyph: the newest headline, or why there is none (quiet).</summary>
    internal static (string Said, bool Quiet) FaceSaid(RssIsland? feed) =>
        feed is { Headlines.Count: > 0 } ? (feed.Headlines[0].Title, false) : (FaceDrawings.Dash, true);

    /// <summary>What the news face shows now, for the folder to tell when to paint it again.</summary>
    internal static string FaceReading(RssIsland? feed) =>
        feed is null ? "" : $"{feed.options.IsSet}|{feed.read.NewCount > 0}|{feed.Mark(16).Glyph}|{(feed.Headlines.Count > 0 ? feed.Headlines[0].Title : feed.WhyShort())}";

    /// <summary>
    /// The news face in <paramref name="box"/>, from <paramref name="feed"/> (the glyph, quiet with no
    /// feed, where there is none): the glyph and its dot, and the newest headline — or why there is
    /// none, quietly — in the room after it, cut with "…".
    /// </summary>
    internal static void PaintFace(ID2D1DeviceContext* dc, Gfx gfx, in IslandStyle style, D2D_RECT_F box, RssIsland? feed, bool headline)
    {
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        var glyph = fit.Glyph();
        var set = feed is { options.IsSet: true };
        var colour = set ? ink.Text : ink.Disabled;
        var lit = feed is { read.NewCount: > 0 };
        var height = box.bottom - box.top;
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        var top = box.top + MathF.Floor((height - glyph) / 2);
        if (style.Vertical || !headline)
        {
            var square = PopupPaint.Rect(box.left + MathF.Floor((box.right - box.left - glyph) / 2), top, glyph, glyph);
            PaintMark(new LineSketch(dc, gfx), feed, glyph, colour, square);
            Dot(dc, square, fit, ink, lit);
            return;
        }
        var textRoom = MathF.Max(1, box.right - (box.left + glyph + fit.Gap));
        var (said, quiet) = FaceSaid(feed);
        var size = PopupPaint.Measure(gfx, said, fit.Primary, Weight.Regular, textRoom);
        var left = box.left;
        var at = PopupPaint.Rect(left, top, glyph, glyph);
        PaintMark(new LineSketch(dc, gfx), feed, glyph, colour, at);
        Dot(dc, at, fit, ink, lit);
        PopupPaint.Text(dc, gfx, said, fit.Primary, Weight.Regular, quiet ? ink.Secondary : ink.Text, left + glyph + fit.Gap, MathF.Round(box.top + ((height - size.Y) / 2)), textRoom);
    }

    /// <summary>Never drawn on a bar: a feed is a line of a folder, and the news face draws what the entry drew (<see cref="PaintFace"/>).</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    /// <summary>The accent's dot on the glyph's top right corner while something new has come.</summary>
    private static void Dot(ID2D1DeviceContext* dc, D2D_RECT_F glyph, in Fit fit, in IslandInk ink, bool lit)
    {
        if (!lit)
        {
            return;
        }
        var radius = MathF.Max(fit.Px(3), (glyph.right - glyph.left) * 0.14f);
        PopupPaint.Circle(dc, new Vector2(glyph.right - (radius * 0.4f), glyph.top + (radius * 0.6f)), radius, ink.Accent);
    }

    // --- In a folder ---------------------------------------------------------------

    /// <summary>Its headlines light under the pointer themselves, as a list's rows do: the line as a whole is not one thing to press.</summary>
    protected internal override bool LightsItsRows => true;

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        Line(new LineSketch(dc, Gfx), box, style, null);

    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        LineFocus.At(LineTargets(box, style), point);

    /// <summary>keys: its targets as it lays them out, which the pointer and the keys both read (ILineTargets).</summary>
    public IReadOnlyList<(int Target, D2D_RECT_F Box)> LineTargets(D2D_RECT_F box, in TileStyle style)
    {
        var targets = new List<(int Index, D2D_RECT_F Box)>();
        Line(new LineSketch(null, Gfx), box, style, targets);
        return targets;
    }

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var at = point is { } where ? CardTarget(where, box, style, surface) : 0;
        var let = press.Over(at);
        var target = at - 1;
        if (target == hot && !let)
        {
            return;
        }
        hot = target;
        _ = Wake();
    }

    /// <summary>A press on a headline or the feed's name lights it pressed; the press stays the folder's, which makes it the click (RowPress).</summary>
    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (press.Down(CardTarget(point, box, style, surface)))
        {
            _ = Wake();
        }
    }

    /// <summary>tests: the headline (or the feed's name, <see cref="SiteTarget"/>) under the pointer and the one pressed, from 1; 0 for none.</summary>
    internal (int Hot, int Pressed) Lighted => (hot + 1, press.Held);

    /// <summary>Target <paramref name="target"/>'s band (LineItem) while the pointer is on it: pressed while a press is down on it.</summary>
    private void Lit(LineSketch sketch, int target, D2D_RECT_F band, in TileStyle style)
    {
        if (target == hot + 1)
        {
            LineItem.Light(sketch, band, style.Theme, press.On(target));
        }
    }

    /// <summary>
    /// <paramref name="text"/> as it fits <paramref name="lines"/> lines of <paramref name="room"/>
    /// with no word broken across two lines: whole, else cut after its last word that fits, with
    /// "…"; and its height. Null where not even its first word fits the room.
    /// </summary>
    private (string Text, float Height)? Unbroken(string text, float px, Weight weight, float room, int lines)
    {
        if (text.Length == 0 || room <= 1 || lines < 1)
        {
            return null;
        }
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var (shown, height) = PopupPaint.Fit(Gfx, text, px, weight, room, lines);
        if (shown == text && words.All(word => PopupPaint.Measure(Gfx, word, px, weight).X <= room))
        {
            return (text, height);
        }
        // From as many words as the cut text kept, fewer until every one stands whole on its line.
        var kept = Math.Min(words.Length - 1, shown.TrimEnd('…').Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        for (var count = kept; count >= 1; count--)
        {
            var candidate = string.Join(' ', words[..count]) + "…";
            if (words[..(count - 1)].Any(word => PopupPaint.Measure(Gfx, word, px, weight).X > room)
                || PopupPaint.Measure(Gfx, words[count - 1] + "…", px, weight).X > room)
            {
                continue;
            }
            var (fitted, fittedHeight) = PopupPaint.Fit(Gfx, candidate, px, weight, room, lines);
            if (fitted == candidate)
            {
                return (candidate, fittedHeight);
            }
        }
        return null;
    }

    /// <summary><paramref name="text"/> where it fits <paramref name="room"/> whole, else null.</summary>
    private string? Whole(string? text, float px, Weight weight, float room) =>
        text is { Length: > 0 } && room > 1 && InkText.Whole(Gfx, text, px, weight, room) ? text : null;

    // --- As a line of a list --------------------------------------------------------

    /// <summary>What a line of one row shows first: the newest headline, or why there is none.</summary>
    public string MainPiece => "newest";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(null, Gfx);
        Line(sketch, box, style, null);
        return sketch.Pieces;
    }

    /// <summary>
    /// How tall a new line is: the feed's name over its newest headlines, each over where it is from
    /// and how old it is, up to <see cref="LineMost"/> of them, and the footer; the rest scroll.
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var count = State == RssState.Ready ? Headlines.Count : 0;
        // Nothing to list: one row, why (§401).
        return count == 0 ? LineRows.Px(1, style) : Whole(style, form, ItemForm.For(Gfx, style, Settings), Math.Min(LineMost, count));
    }

    /// <summary>Every headline, where the line lists them: the rest scroll under the feed's name (ListScroll.cs).</summary>
    public float ScrollHeight(float width, float height, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var items = ItemForm.For(Gfx, style, Settings);
        var count = State == RssState.Ready ? Headlines.Count : 0;
        return count == 0 || height + 0.5f < Lists(style, form, items) ? 0 : Whole(style, form, items, count);
    }

    /// <summary>The feed's name and the stale mark at its end stay while the headlines scroll.</summary>
    public float ScrollHead(float width, float content, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        return HeadLine(style, form);
    }

    /// <summary>The line with the feed's name over <paramref name="count"/> headlines: nothing under them (§408).</summary>
    private static float Whole(in TileStyle style, ListForm form, ItemForm items, int count) =>
        HeadLine(style, form) + (count * Each(style, items));

    /// <summary>The least a line lists the headlines at: the name and the first headline's first line; under it, the line of one row.</summary>
    private static float Lists(in TileStyle style, ListForm form, ItemForm items) =>
        HeadLine(style, form) + items.Space(style, ItemAir) + items.Line;

    /// <summary>Whether what was read before is shown because the newest read failed: the stale mark on the header (StaleMark.cs).</summary>
    private bool Stale => State == RssState.Ready && read.Problem is not null;

    /// <summary>"Last updated 14:05 — couldn't refresh" and why, while the headlines shown are the last read's (StaleMark.cs).</summary>
    internal override string? StaleTip =>
        Stale ? $"{new Pim.Freshness(read.FetchedUtc, Stale: true).Tip(Clock.Now())}\n{read.Problem}".TrimStart('\n') : null;

    /// <summary>Over the feed's name, the site it opens; over the stale mark, why the headlines are the last read's.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var sketch = new LineSketch(null, Gfx);
        var targets = new List<(int Index, D2D_RECT_F Box)>();
        Line(sketch, box, style, targets);
        if (StaleMark.Tip(sketch.Pieces, point, StaleTip, style) is { } stale)
        {
            return stale;
        }
        foreach (var (index, at) in targets)
        {
            if (index == SiteTarget && Site is { } home && LineRows.Inside(at, point))
            {
                return (S.DockRssOpenSite(Feeds.HostOf(home)), at);
            }
        }
        return null;
    }

    /// <summary>Whether a line <paramref name="height"/> px tall is one row: too short to list a headline under the name, or for the name alone where there is none.</summary>
    private bool OneRow(float height, in TileStyle style, ListForm form, ItemForm items)
    {
        var count = State == RssState.Ready ? Headlines.Count : 0;
        // No headline: one row, why, at every height (§401).
        return count == 0 || height + 0.5f < Lists(style, form, items);
    }

    /// <summary>The feed's name on a line taller than a row: the kit's Header, one of the list's rows (§401).</summary>
    private static float HeadLine(in TileStyle style, ListForm form) => LineRow.Height(style, caption: false);

    /// <summary>The air over and under a headline's words, DIP at the item style's size: a headline is the kit's item, 52 DIP at the list's.</summary>
    private const float ItemAir = 8;

    /// <summary>A headline: its words over where it is from and how old it is, with its air.</summary>
    private static float Each(in TileStyle style, ItemForm items) => (2 * items.Space(style, ItemAir)) + items.Line + items.SmallLine;

    /// <summary>
    /// The line, at whatever height it was given (docs\widgets.md §9, "A line at every height"), drawn
    /// where the sketch draws, with each headline's box put in <paramref name="targets"/> (its index
    /// from 1) where they are asked for:
    /// <list type="bullet">
    /// <item>One row: the news glyph in the list's leading column, the newest headline where every
    /// name begins, and how old it is against the trailing gutter; with no headline, only why.</item>
    /// <item>Taller — the headlines panel as it was: the feed's name, which opens its site, with the stale
    /// mark at its end, and each headline over where it is from and
    /// how old it is, its picture in the leading column where the feed gives one and the item style shows it,
    /// from the top down. Given less than the headlines take, the line is laid out as tall as all of them and
    /// scrolled (ListScroll.cs).</item>
    /// </list>
    /// </summary>
    private void Line(LineSketch sketch, D2D_RECT_F box, in TileStyle style, List<(int Index, D2D_RECT_F Box)>? targets)
    {
        var form = ListForm.Of(Gfx, style);
        var look = ItemForm.For(Gfx, style, Settings);
        var theme = style.Theme;
        var height = box.bottom - box.top;
        var left = box.left + form.TextLeft;
        var items = State == RssState.Ready ? Headlines : [];
        var lit = items.Any(IsNew);
        var glyphInk = lit ? TilePaint.Accent(style) : theme.TextSecondary;
        var headLine = HeadLine(style, form);
        var dot = MathF.Max(style.Px(6), 4);
        if (OneRow(height, style, form, look))
        {
            var newest = items.Count > 0 ? items[0] : null;
            var age = newest is null || !look.Time ? "" : AgeOf(newest);
            var field = age.Length > 0 ? PopupPaint.Measure(Gfx, age, form.SmallPx, Weight.Regular).X : 0;
            string[] names = newest is null ? [Why()] : [newest.Title];
            var one = ListTiers.Row(Gfx, box, style, names, newest is not null && IsNew(newest) ? Weight.Semibold : Weight.Regular, field);
            if (newest is not null && one.Name is not null)
            {
                // The newest headline is the line's one item: all of it (LineItem).
                var row = LineItem.Band(box, box.top, box.bottom);
                targets?.Add((1, row));
                Lit(sketch, 1, row, style);
            }
            PaintMark(sketch, this, form.Glyph, glyphInk, form.IconBox(box.left, one.TextTop, form.Line));
            if (one.Name is { } name)
            {
                sketch.Text("newest", name, form.TextPx, newest is not null && IsNew(newest) ? Weight.Semibold : Weight.Regular,
                    newest is null ? theme.TextSecondary : theme.Text, one.NameLeft, one.TextTop, one.NameRoom);
            }
            if (age.Length > 0)
            {
                sketch.TextRight("age", age, form.SmallPx, Weight.Regular, theme.TextTertiary, one.FieldRight,
                    one.TextTop + PopupPaint.Round((form.Line - form.SmallLine) / 2), one.Field + 1);
            }
            return;
        }
        // The kit's Header (§401): the feed's mark in the leading column, its name in the Body's strong weight
        // short of the stale mark at its end. Content shorter than its line stands at its
        // top. Where the feed names its site the header opens it, lit as a row is (2026-10-29: "the open xyz
        // button should not be on the bottom but should be when i click on the title of the feed", §408).
        var stale = Stale;
        var trailing = StaleMark.Room(style, form, stale);
        if (Site is not null)
        {
            // The header's row, an item as the headlines under it are (LineItem).
            var plate = LineItem.Band(box, box.top, box.top + headLine);
            targets?.Add((SiteTarget, plate));
            Lit(sketch, SiteTarget, plate, style);
        }
        PaintMark(sketch, this, form.Glyph, glyphInk, form.IconBox(box.left, box.top, headLine));
        var header = LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec(FeedTitle) { Strong = true, Trailing = trailing, NamePiece = "head" }, headLine);
        if (stale)
        {
            StaleMark.Sketch(sketch, box, style, form, box.top, headLine);
        }
        var y = header.Bottom;
        // The headlines, the kit's items: their pictures in the leading column where the others show their icons
        // (2026-10-29: "the rss widget should show the images of the articles on the left side"), as large as the
        // kit's Feature glyph grown into the room before the text edge — the feed's mark there while one comes and
        // where there is none — a new one's dot on its corner, their words where every name begins.
        var rowRoom = MathF.Max(1, box.right - form.Inset - left);
        var each = Each(style, look);
        var air = look.Space(style, ItemAir);
        var side = LineGlyph.Feature(style);
        var corner = style.Px(LineCorner.ControlDip);
        for (var index = 0; index < items.Count; index++)
        {
            // Only whole headlines: the rest is under the window the line scrolls through.
            if (y + each > box.bottom + 0.5f)
            {
                break;
            }
            if (sketch.Draws && targets is null && Hidden(y, y + each))
            {
                y += each;
                continue;
            }
            var item = items[index];
            // The headline with its air over and under it, edge to edge: the next one's begins where it ends (LineItem).
            var band = LineItem.Band(box, y, y + each);
            targets?.Add((index + 1, band));
            Lit(sketch, index + 1, band, style);
            y += air;
            var fresh = IsNew(item);
            var weight = fresh ? Weight.Semibold : Weight.Regular;
            var rowHeight = look.Line + look.SmallLine;
            var lead = ListTiers.GlyphBox(form, box.left, y, rowHeight, side);
            var square = PopupPaint.Rect(PopupPaint.Round(((lead.left + lead.right) / 2) - (side / 2)), PopupPaint.Round(y + ((rowHeight - side) / 2)), side, side);
            if (look.Pictures && item.Thumbnail.Length > 0)
            {
                sketch.Picture($"picture {index}", square, (dc, at) =>
                {
                    if (store.Picture(this, item.Thumbnail) is { } pixels)
                    {
                        PopupPaint.Covered(dc, pixels, at, corner);
                    }
                    else
                    {
                        PaintMark(new LineSketch(dc, Gfx), this, form.Glyph, theme.TextSecondary, at);
                    }
                });
            }
            else
            {
                PaintMark(sketch, this, form.Glyph, theme.TextSecondary, square, $"mark {index}");
            }
            sketch.Text($"title {index}", item.Title, look.TextPx, weight, theme.Text, left, y, rowRoom);
            // A new headline's dot on its picture's top corner, ringed in the list's ground so it reads on any picture.
            if (fresh)
            {
                var ring = MathF.Max(1, style.Px(2));
                var at = PopupPaint.Rect(square.right - (dot / 2) - ring, square.top - (dot / 2) + ring, dot, dot);
                sketch.Plate($"ring {index}", PopupPaint.Rect(at.left - ring, at.top - ring, dot + (2 * ring), dot + (2 * ring)), (dot / 2) + ring, PopupPaint.Ink(theme.Base));
                sketch.Plate($"new {index}", at, dot / 2, TilePaint.Accent(style));
            }
            sketch.Text($"byline {index}", look.Time ? BylineOf(item) : FeedTitle, look.SmallPx, Weight.Regular, theme.TextSecondary,
                left, y + look.Line, rowRoom);
            y += rowHeight + air;
        }
    }
}
