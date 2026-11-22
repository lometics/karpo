// A markets widget as a line of a list folder (docs\widgets.md §246): a share, a currency's rate or a coin,
// in its style.
//
// The ticker (the full and the compact style), drawn for the height it is given (§9, "A line at every
// height"): one row, its name and the figures with the chart behind them; from two rows, the name and what
// it is over the price and the change, a third row with the market's state — or where a rate is from — and
// the holding where it fits, and the chart under them in the colour of the move, never taller than
// MarketChart.ChartDip: a taller line keeps its words at the top and its chart at the foot (2026-10-11). The
// compact style is the price alone: no change, no chart. The change is a percentage, not an absolute
// figure: a percent compares across what is followed, and stays about one length whatever the price is.
//
// A currency in the converter style is the converter instead (MarketConverter): two fields typed either
// way, the swap between them, the rate under them and the chart at the foot.
//
// Prices come from MarketStore, which fetches once per question however many widgets ask, and which
// MarketData keeps fetched from the dock's start. Nothing is asked until there is something to ask: a
// share with no symbol asks nothing, and says so. A ticker's click searches what it tracks in the
// browser (§263). "Refresh" on its right click fetches it again (IRefreshable, §451); where a refresh
// failed and the last price stands, the price is in the quiet ink and the line's tooltip says "Last
// updated 14:05 — couldn't refresh" (§77). A converter's rate is fetched on its own clock.
//
// §494: shown in another currency (MarketOptions.Currency), its figures are at the rate MarketExchange
// follows in the same store, the listed ones until it has come; with an amount owned the main figure is
// the holding's worth and the price of one stands where the worth stood (MarketReading).

using Karpo.Core;
using Karpo.Dock.Markets;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>A figure a markets line drew: its text, its size and weight, and the room it was given, in px.</summary>
internal readonly record struct MarketFigure(string Text, float Px, Weight Weight, float Room);

internal sealed unsafe class MarketIsland : CellIsland, IListContent, IRefreshable, IOpensOnceGone
{
    /// <summary>list: the chart runs under a folder's rim from the line's left edge, as a reading's graph does.</summary>
    protected internal override bool UnderRim => true;

    private readonly MarketStore store;
    private MarketOptions options = MarketOptions.New();
    private MarketAsk? asked;
    private bool configured;
    private MarketCache? shown;
    private string? problem;
    private MarketConverter? converter;

    // §494: the rate its amounts are shown in another currency at, where one is chosen.
    private MarketExchange? exchange;

    public MarketIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, Made ?? (Samples.On ? Samples.Markets() : MarketStore.Shared))
    {
    }

    [ThreadStatic]
    private static MarketStore? made;

    /// <summary>
    /// A test's own store, per thread, read as a widget is made the ordinary way — by a folder, which makes
    /// its widgets itself: its prices are the widget's, and nothing reaches the network. Unset, the shared one.
    /// </summary>
    internal static MarketStore? Made
    {
        get => made;
        set => made = value;
    }

    /// <param name="store">Where its price comes from; a test hands in one built on its own source, so nothing reaches the network.</param>
    internal MarketIsland(string key, WidgetItem island, Gfx gfx, Config config, MarketStore store)
        : base(key, island, gfx)
    {
        this.store = store;
        exchange = new MarketExchange(store, this);
        Configure(island, config);
    }

    /// <summary>What it was set to.</summary>
    internal MarketOptions Options => options;

    /// <summary>§494: the rate its amounts are shown at, made where the base's construction asks before ours ran.</summary>
    internal MarketExchange Exchange => exchange ??= new MarketExchange(store, this);

    /// <summary>The store this widget reads.</summary>
    internal MarketStore Store => store;

    /// <summary>The last answer, why a newer one isn't there, and whether one is coming.</summary>
    internal (MarketCache? Shown, string? Problem, bool Fetching) Quote => store.Read(this);

    /// <summary>The converter, where it is a currency in the converter style; null else.</summary>
    internal MarketConverter? Converter => converter;

    /// <summary>
    /// Every online widget's interval, held to the markets' floor (<see cref="MarketStore.Every"/>), while
    /// there is something to ask; the caret's half second while a converter's field is typed in.
    /// </summary>
    public override int IntervalMs => converter is { Typing: >= 0 } ? 500 : asked is not null ? (int)store.Every.TotalMilliseconds : 0;

    protected override int Count => 1;

    /// <summary>A converter's parts light themselves; a ticker does not light at all.</summary>
    protected internal override bool LightsItsRows => converter is not null;

    public override void Configure(WidgetItem island, Config config)
    {
        // The shape is a function of the settings, so it must be given the settings just handed over.
        Settings = island;
        searchWith = config;
        var next = (island.Market ?? MarketOptions.New()).Copy();
        next.Normalize();
        // §635: a ghost's line, tracking nothing yet, tracks the samples' share, or a converter their rate.
        if (MarketAsk.Of(next) is null && Sampled)
        {
            next.Symbol = Samples.MarketSymbol(next.StyleOf == MarketStyle.Converter);
        }
        var ask = MarketAsk.Of(next);
        var same = configured && ask == asked;
        options = next;
        asked = ask;
        configured = true;
        if (options.StyleOf == MarketStyle.Converter)
        {
            converter ??= new MarketConverter(() => Gfx, Redrawn);
            converter.Configure(options);
        }
        else
        {
            converter = null;
        }
        if (!same)
        {
            store.Use(this, ask);
            store.Poll(this);
            (shown, problem, _) = store.Read(this);
        }
        Exchange.Follow(options, shown?.Quote);
        Forget();
    }

    /// <summary>No hover text but a stale price's (<see cref="CardTip"/>): the line says all of it.</summary>
    public override string? Tooltip(int index) => null;

    // The settings a click searches with (the app's engine, Config.SearchEngine), and the address a click
    // chose, opened once the folder has gone (Open).
    private Config? searchWith;
    private string? chosen;

    /// <summary>Where a search opens: the browser, through the shell. A test puts its own here.</summary>
    public Action<string> Opened { get; set; } = static link => Launch.Open(link);

    /// <summary>
    /// A ticker's click searches what it tracks in the default browser, with the app's engine (2026-10-20:
    /// "when i left click on the stock widget it should open the stock in the default search in the
    /// browser", §263; <see cref="SearchUrls.Market"/>), once the folder has gone, as a headline's story
    /// opens; it did nothing before. A converter's click is its swap and its fields', as it was; one with
    /// no symbol yet does nothing. The quote's page is the line's menu's.
    /// </summary>
    public override IslandAction Click(int index)
    {
        if (converter is not null)
        {
            converter.Click(index, Reading());
            return default;
        }
        chosen = asked is { } ask ? SearchUrls.Market(searchWith ?? new Config(), ask.Symbol) : null;
        return chosen is null ? default : new IslandAction(View: IslandView.App);
    }

    /// <summary>The dock, once the folder has gone: opens the search the click chose, once.</summary>
    public void Open()
    {
        if (chosen is { } link)
        {
            chosen = null;
            Opened(link);
        }
    }

    /// <summary>A ticker with something to ask ("Refresh", IRefreshable); a converter's rate comes on its own clock.</summary>
    public bool CanRefresh => asked is not null && converter is null;

    /// <summary>A fetch is on its way.</summary>
    public bool Refreshing => store.Read(this).Fetching;

    /// <summary>When the price shown was fetched.</summary>
    public DateTime? UpdatedUtc => shown?.FetchedUtc;

    /// <summary>Why a newer price is not there.</summary>
    string? IRefreshable.Problem => store.Read(this).Problem;

    void IRefreshable.RefreshNow() => Refresh();

    /// <summary>"Refresh": a fetch now whatever the age. False where none started.</summary>
    internal bool Refresh()
    {
        var started = store.Ask(this);
        Exchange.Refresh();
        (shown, problem, _) = store.Read(this);
        Redrawn();
        return started;
    }

    /// <summary>
    /// Whether a row ending at <paramref name="end"/> shows the change beside the price (§262: "it cuts off
    /// the price and price change in percent but there would still be space"): where it holds both beside
    /// the least the name still reads as ("BRK…", ListTiers.Name), or where the name has no room even
    /// beside the price alone and the two figures fit the row. The change once gave way wherever the name
    /// did not stand whole. Always, before there is a price: the row says why.
    /// </summary>
    private bool Roomy(D2D_RECT_F box, in TileStyle style, MarketReading reading, float end)
    {
        if (!reading.Ready)
        {
            return true;
        }
        var form = ListForm.Of(Gfx, style);
        var left = box.left + form.TextLeft;
        var (price, valuePx) = PriceFit(reading.Price, ListTiers.ValuePx(style), form.TextPx, style.Scale, box.right - form.Inset - left);
        var priceRoom = FigureWidth(price, valuePx, Weight.Semibold);
        var both = priceRoom + (options.StyleOf == MarketStyle.Full ? style.Gap + ChangeRoom(form, reading) : 0);
        var nameLeast = Least(reading.Symbol.Length > 0 ? reading.Symbol : Info.Title, form.TextPx);
        return left + nameLeast + style.Gap + both <= end
            || (left + nameLeast + style.Gap + priceRoom > end && left + both <= end);
    }

    /// <summary>The change's room: its widest shape, its sign a plus, never under an ordinary day's (§262).</summary>
    private float ChangeRoom(in ListForm form, MarketReading reading) =>
        MathF.Max(FigureWidth("+88.88%", form.TextPx, Weight.Regular), FigureWidth(MarketText.ChangeShape(reading.Percent), form.TextPx, Weight.Regular));

    /// <summary>
    /// How a ticker in <paramref name="box"/> is laid out: one row, or its rows from the top; and how
    /// tall its first row is — the price's line at the size it fits at, or the list's text's.
    /// </summary>
    private (ListTier Tier, float Row) Rows(D2D_RECT_F box, in TileStyle style, MarketReading reading)
    {
        var form = ListForm.Of(Gfx, style);
        var ready = reading.Ready;
        var valuePx = ready ? PriceFit(reading.Price, ListTiers.ValuePx(style), form.TextPx, style.Scale, TilePaint.Width(box) - form.TextLeft - form.Inset).Px : ListTiers.ValuePx(style);
        var row = MathF.Max(TilePaint.LineHeight(Gfx, valuePx, Weight.Semibold), form.Line);
        var hasDay = options.StyleOf == MarketStyle.Full && ready && reading.Day.Count >= 2;
        var compact = form.Pad + row + (hasDay ? form.Gap + style.Px(20) : form.Pad);
        // Never the whole widget: the compact line is the line's look at every height.
        return (ListTiers.Of(TilePaint.Height(box), compact, float.PositiveInfinity), row);
    }

    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            converter?.Rest();
        }
    }

    public override void Dispose()
    {
        store.Release(this);
        Exchange.Release();
        base.Dispose();
    }

    protected override bool Sample(bool everything)
    {
        if (!everything && converter is { Typing: >= 0 } typing)
        {
            typing.Editor.Blink();
        }
        if (everything)
        {
            store.Deliver(this);
            Exchange.Deliver();
        }
        store.Poll(this);
        (shown, problem, _) = store.Read(this);
        Exchange.Follow(options, shown?.Quote);
        return false;
    }

    /// <summary>What it reads now, through Clock.Now so a test can hold it still.</summary>
    internal MarketReading Reading() =>
        MarketReading.Of(shown, options, Clock.Now().ToUniversalTime(), problem, store.Updates, Exchange.Rate);

    /// <summary>Never drawn on a bar: on the bar it is the markets face (MarketFaceDrawing).</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- As a line of a list ----------------------------------------------------

    /// <summary>
    /// list: the line a new widget asks for, where nobody set its height: a ticker's words at the top and
    /// the chart at its tallest under them (<see cref="MarketChart.ChartDip"/>); a converter's names, fields,
    /// rate and chart.
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        if (converter is not null)
        {
            return converter.Height(style);
        }
        // §432: the ticker on one row (Compact), or its words over its chart (Full).
        if (LineSizes.Of(Settings) == LineSize.Compact)
        {
            return LineRows.Px(1, style);
        }
        var form = ListForm.Of(Gfx, style);
        var row = MathF.Max(TilePaint.LineHeight(Gfx, ListTiers.ValuePx(style), Weight.Semibold), form.Line);
        return options.StyleOf == MarketStyle.Compact
            ? MathF.Ceiling(form.Pad + row + form.Line + form.Pad)
            : MathF.Ceiling(form.Pad + row + form.Line + style.Px(MarketChart.ChartDip));
    }

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (converter is not null)
        {
            converter.Paint(dc, box, style, Reading());
            return;
        }
        Ticker(dc, box, style);
    }

    /// <summary>A ticker is one thing; a converter's fields and its swap are the things on it.</summary>
    protected internal override int CardTarget(System.Numerics.Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        converter?.Target(point, box, style, Reading()) ?? 0;

    /// <summary>
    /// "Last updated 14:05 — couldn't refresh" over a ticker, where a refresh failed and the last price stands
    /// (docs\widgets.md §77), after — §494 — its figure in both currencies and the rate where it is shown in
    /// another; a converter's swap's name and its rate's age.
    /// </summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(System.Numerics.Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (converter is not null)
        {
            return converter.Tip(point, box, style, Reading(), StaleWords());
        }
        // §494: shown in another currency, the figure in both and the rate; and a stale price's words.
        string?[] said = [Reading().ExchangeTip(), StaleWords()];
        var tip = string.Join('\n', said.OfType<string>());
        return tip.Length > 0 ? (tip, box) : null;
    }

    protected internal override void CardHover(System.Numerics.Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        converter?.Hover(point, box, style, Reading());

    protected internal override void CardDown(System.Numerics.Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        converter?.Down(point, box, style, Reading());

    protected internal override bool CardPress(System.Numerics.Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        converter?.Press(point, box, style, Reading()) ?? false;

    protected internal override void CardDrag(System.Numerics.Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        converter?.Drag(point, box, style, Reading());

    protected internal override void CardRelease() => converter?.Release();

    protected internal override bool CardText(System.Numerics.Vector2 point, D2D_RECT_F box, in TileStyle style) =>
        converter?.OverField(point, box, style, Reading()) ?? false;

    protected internal override bool CardTyping => converter is { Typing: >= 0 };

    protected internal override void CardStopTyping() => converter?.StopTyping();

    protected internal override bool CardChar(char c, WidgetSurface surface) => converter?.Char(c, Reading()) ?? false;

    /// <summary>keys: a field typed in has the keys (its caret, Escape, Enter, Tab out); else the folder walks the fields and the swap.</summary>
    protected internal override bool CardKey(int key, WidgetSurface surface) => converter?.Key(key, Reading()) ?? false;

    /// <summary>The stale words: where the last refresh failed and a price from before stands; else null.</summary>
    private string? StaleWords() =>
        Reading().Ready && problem is not null && shown is { } cache
            ? new Pim.Freshness(cache.FetchedUtc, true).Tip(Clock.Real())
            : null;

    /// <summary>
    /// The ticker, at whatever height it was given (docs\widgets.md §9, "A line at every height"):
    /// <list type="bullet">
    /// <item>One row: its glyph in the list's leading column, its name, the chart in the room between, and the
    /// price with the change against the trailing gutter. The price always stands, whole — a size smaller or
    /// a shorter form where the line is too narrow for it (§262); the change next, where the row holds it
    /// beside the least the name reads as; the name in what is left, so two lines tell apart; the chart
    /// behind them.</item>
    /// <item>From two rows: the name with the price at its end, what it is with the change, and a third row
    /// with the market's state and the holding where the line has the height; the chart under them, edge to
    /// edge, no taller than <see cref="MarketChart.ChartDip"/>.</item>
    /// </list>
    /// The compact style draws no change and no chart. Before there is a price, what is keeping it, where
    /// the price would stand.
    /// </summary>
    private void Ticker(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var theme = style.Theme;
        var height = TilePaint.Height(box);
        var reading = Reading();
        var ready = reading.Ready;
        var full = options.StyleOf == MarketStyle.Full;
        var left = box.left + form.TextLeft;
        var right = box.right - form.Inset;
        figured.Clear();
        // The price is never cut (§262): at the list's figure where its shape fits the line, else a size
        // down at a time to the list's text, else a shorter form of it there.
        var (price, valuePx) = ready ? PriceFit(reading.Price, ListTiers.ValuePx(style), form.TextPx, style.Scale, right - left) : ("", ListTiers.ValuePx(style));
        var valueLine = TilePaint.LineHeight(Gfx, valuePx, Weight.Semibold);
        var row = MathF.Max(valueLine, form.Line);
        var least = style.Px(20);
        var hasDay = full && ready && reading.Day.Count >= 2;
        var tier = Rows(box, style, reading).Tier;
        var ink = new IslandInk(style.Look);
        var moved = reading.Stale ? theme.TextSecondary : MarketChart.Moved(reading.Direction, style.Look, ink);
        var symbol = reading.Symbol.Length > 0 ? reading.Symbol : Info.Title;
        var why = reading.State switch
        {
            MarketShows.NoSymbol => S.WidgetMarketsNoSymbol,
            MarketShows.Loading => S.WidgetMarketsLoading,
            _ => S.WidgetMarketsNoPrice,
        };
        // The figures: the price in the room its widest shape keeps, its currency after it, the change, and
        // what the change is over ("525.74 USD +1.59% 5Y"): the currency and the range quiet, and the first
        // to give way, the range before the currency. The compact style has no change and no range. The
        // change's room is its widest shape, its sign a plus, never under an ordinary day's (§262): it was
        // "-88.88%", narrower than any rise, and "+12.34%" was cut to "+12.3…". Both in tabular figures,
        // measured and drawn (Figure), so a shape's eights are as wide as the figures it stands for (§277).
        var priceRoom = ready ? FigureWidth(price, valuePx, Weight.Semibold) : 0;
        var changeRoom = ready && full ? ChangeRoom(form, reading) : 0;
        var currency = ready ? reading.Currency : "";
        var range = ready && full ? reading.RangeLabel : "";
        var currencyRoom = currency.Length > 0 ? PopupPaint.Measure(Gfx, currency, form.TextPx, Weight.Regular).X : 0;
        var rangeRoom = range.Length > 0 ? PopupPaint.Measure(Gfx, range, form.TextPx, Weight.Regular).X : 0;
        var whyRoom = ready ? 0 : PopupPaint.Measure(Gfx, why, form.TextPx, Weight.Regular).X;
        var both = ready ? priceRoom + (full ? style.Gap + changeRoom : 0) : whyRoom;
        var spacing = style.Gap;
        var textPx = form.TextPx;
        var textLine = form.Line;
        var priceInk = reading.Stale ? theme.TextSecondary : theme.Text;
        var quiet = theme.TextSecondary;
        // The figures on the row whose middle is at middle, in room px against the trailing gutter: the price,
        // its currency where withCurrency says so, and the change after them where change says so, its range
        // after it where withRange does.
        void Figures(float middle, bool change, float room, bool withCurrency = false, bool withRange = false)
        {
            if (!ready)
            {
                TilePaint.LineRight(dc, Gfx, why, textPx, Weight.Regular, quiet, right, PopupPaint.Round(middle - (textLine / 2)), MathF.Max(1, MathF.Min(whyRoom, room)));
                return;
            }
            change &= full;
            var end = right;
            var textTop = PopupPaint.Round(middle - (textLine / 2));
            if (change && withRange)
            {
                TilePaint.LineRight(dc, Gfx, range, textPx, Weight.Regular, quiet, end, textTop, rangeRoom);
                end -= rangeRoom + spacing;
            }
            if (change)
            {
                Figure(reading.Percent, textPx, Weight.Regular, moved, end, textTop, changeRoom);
                end -= changeRoom + spacing;
            }
            if (withCurrency)
            {
                TilePaint.LineRight(dc, Gfx, currency, textPx, Weight.Regular, quiet, end, textTop, currencyRoom);
                end -= currencyRoom + spacing;
            }
            Figure(price, valuePx, Weight.Semibold, priceInk, end, PopupPaint.Round(middle - (valueLine / 2)), MathF.Max(1, MathF.Min(priceRoom, end - (right - room))));
        }
        // A figure, the price or the change, against end in room px, in tabular figures as it was measured,
        // and what the tests hold to its room (Figured).
        void Figure(string text, float px, Weight weight, DXGI_RGBA colour, float end, float y, float room)
        {
            figured.Add(new MarketFigure(text, px, weight, room));
            if (text.Length == 0 || room < 1)
            {
                return;
            }
            ID2D1SolidColorBrush* brush = null;
            var line = TextLine.Lay(Gfx, text, px, weight, room);
            try
            {
                brush = Gfx.Brush(dc, colour);
                line.Draw(dc, brush, colour, end - line.Width, y);
            }
            finally
            {
                line.Release();
                ComUtil.Release(ref brush);
            }
        }
        var glyphInk = !ready || reading.Direction == 0 || reading.Stale || !full ? theme.TextSecondary : moved;
        var symbolWidth = PopupPaint.Measure(Gfx, symbol, form.TextPx, Weight.Semibold).X;
        // Whether the row shows the change beside the price (Roomy).
        var roomy = Roomy(box, style, reading, right);
        if (tier == ListTier.One)
        {
            var field = roomy ? both : priceRoom;
            var one = ListTiers.Row(Gfx, box, style, [symbol], Weight.Semibold, field);
            if (hasDay)
            {
                // The chart behind the row, edge to edge, faded under the name and the figures.
                MarketChart.Behind(dc, box, reading, moved, MarketChart.Coloured(reading, style.Look), down: false, [(box.left, one.Name is null ? one.NameLeft : one.NameRight), (one.FieldLeft, box.right)], style.Px(16));
            }
            PopupPaint.Glyph(dc, Gfx, Info.Glyph, form.Glyph, glyphInk, form.IconBox(box.left, one.Middle - (row / 2), row));
            if (one.Name is { } said)
            {
                TilePaint.Line(dc, Gfx, said, form.TextPx, Weight.Semibold, theme.Text, one.NameLeft, one.TextTop, one.NameRoom);
            }
            Figures(one.Middle, roomy, one.Field);
            return;
        }
        var top = box.top + form.Pad;
        var rowMiddle = top + (row / 2);
        var y = top + row;
        // Two rows wherever the line has the height for them (2026-10-11: "all the information is crammed into
        // one line"): the name and the price, then what it is and the change; one row only where it has not.
        var chartRoom = hasDay ? form.Gap + least : form.Pad;
        var twoRows = height + 0.5f >= form.Pad + row + form.Line + form.Pad;
        // A third under them where the line has the height: whether the market trades, and the holding at its
        // end. Else the holding in what-it-is's place, which is the least of what the line says.
        var holding = reading.Holding;
        var detailRoom = height + 0.5f >= form.Pad + row + (twoRows ? form.Line : 0) + form.Line + chartRoom;
        var holdingRow = holding is not null && detailRoom;
        var marketRow = twoRows && ready && reading.MarketLine.Length > 0 && detailRoom;
        if (hasDay)
        {
            // The chart at the line's foot, no taller than ChartDip, faded where it reaches up behind the words.
            MarketChart.Behind(dc, MarketChart.Box(box, style), reading, moved, MarketChart.Coloured(reading, style.Look), down: true, [(box.top, y + (twoRows ? form.Line : 0) + (holdingRow || marketRow ? form.Line : 0))], style.Px(28));
        }
        PopupPaint.Glyph(dc, Gfx, Info.Glyph, ListTiers.GlyphPx(form, style, tier), glyphInk, form.IconBox(box.left, top, row));
        // What follows the name, or begins the second row, at x in room px: the holding where it has no row of
        // its own and stands whole there, else what it is.
        void Named(float x, float at, float room)
        {
            if (!ready || room < 1)
            {
                return;
            }
            if (!holdingRow && holding is { } held && Whole(held, room) is { } worth)
            {
                TilePaint.Line(dc, Gfx, worth, textPx, Weight.Regular, quiet, x, at, room);
                return;
            }
            if (reading.Name.Length > 0 && ListTiers.Name(Gfx, [reading.Name], textPx, Weight.Regular, room) is { } name)
            {
                TilePaint.Line(dc, Gfx, name, textPx, Weight.Regular, quiet, x, at, room);
            }
        }
        // The holding's fullest form that stands whole in room px, or none.
        string? Whole((string Long, string Short) held, float room) =>
            PopupPaint.Measure(Gfx, held.Long, textPx, Weight.Regular).X <= room ? held.Long
            : PopupPaint.Measure(Gfx, held.Short, textPx, Weight.Regular).X <= room ? held.Short
            : null;
        if (twoRows)
        {
            // Two rows: the name with the price at its end, and its currency where the row has the room; under
            // them what it is with the change at its end, and its range.
            var withCurrency = currencyRoom > 0 && left + symbolWidth + style.Gap + priceRoom + spacing + currencyRoom <= right;
            var priceField = MathF.Min(priceRoom + (withCurrency ? spacing + currencyRoom : 0), right - left);
            if (ListTiers.Name(Gfx, [symbol], form.TextPx, Weight.Semibold, right - priceField - style.Gap - left) is { } said)
            {
                TilePaint.Line(dc, Gfx, said, form.TextPx, Weight.Semibold, theme.Text, left, PopupPaint.Round(rowMiddle - (form.Line / 2)), MathF.Max(1, right - priceField - style.Gap - left));
            }
            Figures(rowMiddle, false, right - left, withCurrency);
            var withRange = rangeRoom > 0 && changeRoom + spacing + rangeRoom <= right - left;
            var changeEnd = right - (withRange ? rangeRoom + spacing : 0);
            if (withRange)
            {
                TilePaint.LineRight(dc, Gfx, range, form.TextPx, Weight.Regular, quiet, right, y, rangeRoom);
            }
            if (full && ready)
            {
                Figure(reading.Percent, form.TextPx, Weight.Regular, moved, changeEnd, y, MathF.Min(changeRoom, changeEnd - left));
            }
            Named(left, y, (full && ready ? changeEnd - changeRoom - style.Gap : right) - left);
            y += form.Line;
        }
        else
        {
            // The name and what it is on the row, the figures at its end — the range giving way first where the
            // row is short of the room for all of them, then the currency, then the change. A holding in
            // what-it-is's place keeps the room for its worth before either, and the currency gives way first:
            // the worth says the currency too.
            var keep = !holdingRow && holding is { } inRow ? style.Gap + PopupPaint.Measure(Gfx, inRow.Short, textPx, Weight.Regular).X : 0;
            bool Fits(float more) => roomy && ready && left + symbolWidth + spacing + both + more + keep <= right;
            bool withCurrency;
            bool withRange;
            if (keep > 0)
            {
                withRange = rangeRoom > 0 && Fits(spacing + rangeRoom);
                withCurrency = currencyRoom > 0 && Fits((withRange ? spacing + rangeRoom : 0) + spacing + currencyRoom);
            }
            else
            {
                withCurrency = currencyRoom > 0 && Fits(spacing + currencyRoom);
                withRange = rangeRoom > 0 && Fits((withCurrency ? spacing + currencyRoom : 0) + spacing + rangeRoom);
            }
            var extra = (withCurrency ? spacing + currencyRoom : 0) + (withRange ? spacing + rangeRoom : 0);
            var figures = MathF.Min(roomy ? both + extra : priceRoom, right - left);
            var nameRoom = MathF.Max(1, right - figures - style.Gap - left);
            var said = ListTiers.Name(Gfx, [symbol], form.TextPx, Weight.Semibold, nameRoom);
            if (said is not null)
            {
                TilePaint.Line(dc, Gfx, said, form.TextPx, Weight.Semibold, theme.Text, left, PopupPaint.Round(rowMiddle - (form.Line / 2)), nameRoom);
                var after = left + symbolWidth + style.Gap;
                if (after + style.Px(40) <= left + nameRoom)
                {
                    Named(after, PopupPaint.Round(rowMiddle - (form.Line / 2)), left + nameRoom - after);
                }
            }
            Figures(rowMiddle, roomy, figures, withCurrency, withRange);
        }
        var market = right;
        if (holdingRow && holding is { } own && Whole(own, right - left) is { } worthNow)
        {
            // The holding under the figures, at the line's end, as a portfolio's row has its worth.
            var worth = TilePaint.LineRight(dc, Gfx, worthNow, textPx, Weight.Regular, quiet, right, y, right - left);
            market = right - worth - style.Gap;
        }
        if (marketRow && market - left >= style.Px(40))
        {
            // "At Friday's close" where the market is shut, or where a rate is from, quiet, at the start of that row.
            TilePaint.Line(dc, Gfx, reading.MarketLine, textPx, Weight.Regular, quiet, left, y, market - left);
        }
    }

    // The figures the last ticker drew, each with the room it was drawn in (Figured).
    private readonly List<MarketFigure> figured = [];

    /// <summary>tests: the price and the change the last ticker drew, each with its size and the room it was given (§262).</summary>
    internal IReadOnlyList<MarketFigure> Figured => figured;

    /// <summary>The least <paramref name="name"/> still reads as at <paramref name="px"/>: whole, or its first four letters and "…" (<see cref="ListTiers.Name"/>).</summary>
    private float Least(string name, float px)
    {
        var whole = PopupPaint.Measure(Gfx, name, px, Weight.Semibold).X;
        var letters = new System.Globalization.StringInfo(name);
        if (letters.LengthInTextElements <= 4)
        {
            return whole;
        }
        return MathF.Min(whole, PopupPaint.Measure(Gfx, letters.SubstringByTextElements(0, 4).TrimEnd() + "…", px, Weight.Semibold).X);
    }

    /// <summary>
    /// The price and the size it is drawn at in <paramref name="room"/> px (§262): at <paramref name="px"/>,
    /// the list's figure, where its shape fits; else a size down at a time — a DIP each — to
    /// <paramref name="least"/>, the list's text; else at that the first of its shorter forms whose shape
    /// fits (<see cref="MarketText.PriceForms"/>), the shortest where none does. On its shape, so it keeps
    /// its size and form as it ticks (§12.2).
    /// </summary>
    private (string Price, float Px) PriceFit(string price, float px, float least, float scale, float room)
    {
        var step = MathF.Max(0.5f, scale);
        for (var size = px; size >= least - 0.01f; size -= step)
        {
            if (FigureWidth(price, size, Weight.Semibold) <= room)
            {
                return (price, size);
            }
        }
        var forms = MarketText.PriceForms(price);
        return (forms.FirstOrDefault(form => FigureWidth(form, least, Weight.Semibold) <= room) ?? forms[^1], least);
    }

    /// <summary>
    /// How wide figures shaped like <paramref name="text"/> draw: their widest shape, every digit an eight,
    /// in tabular figures (<see cref="TextLine.Template"/>), as a figure is drawn (§277). In the font's own
    /// figures an eight is not the widest: a "2" is wider at a large size, and "262.24" overran the room
    /// "888.88" kept and was cut to "262.…".
    /// </summary>
    private float FigureWidth(string text, float px, Weight weight) => TextLine.Template(Gfx, text, px, weight).Width;

    /// <summary>A price's widest shape: every figure an eight.</summary>
    internal static string Shape(string price) =>
        string.Create(price.Length, price, static (span, text) =>
        {
            for (var index = 0; index < span.Length; index++)
            {
                span[index] = char.IsAsciiDigit(text[index]) ? '8' : text[index];
            }
        });
}
