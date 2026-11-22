// What the weather's lines of a list share (docs\widgets.md §9; the weather preset, §80): the
// conditions now (WeatherPartTile), the next hours (WeatherHoursTile) and the week (WeatherDaysTile).
// Each is a piece of what the weather flyout was, drawn by the flyout's own control, and each reads
// the one forecast the store keeps for its place.
//
// **The place.** Each line has a place of its own (WeatherOptions), set on its page as on any weather
// widget, and nothing else: a line with none shows its "Set up in Settings", whatever the other
// lines of its folder show (2026-10-10, WeatherPlaces). So two lines of a folder can show two places.
//
// **One fetch for one place, however many ask.** Every line registers with WeatherStore under its
// place, and the store keeps one entry, one fetch and one cache per place — so the three lines of
// the weather preset and its face are one fetch and read one reading. Each asks WeatherReading.From,
// which asks WeatherAge.IsStale with WeatherReading.Refresh, so none of them can call a reading fresh
// that another calls past due.
//
// **Nothing is asked until somebody looks** (docs\widgets.md §11). Making a line asks nothing: it
// registers its place with the store, and asks for it if it is due, the first time it is looked at
// — the folder opening, a measurement for its line, a paint, a hover. A line in a folder nobody
// opens fetches nothing, and ticks nothing: a folder ticks its widgets only while it is open, and
// then on each line's own interval, which is when the store is asked again whether the place is due.
//
// **Only its parts are pressed** (2026-10-11: "you should be able to hover / click the individual days
// ... same for the graph, you should be able to hover the individual hours ... if the whole widget is
// clickable it generally is just weird"; docs\widgets.md §131). The hours and the week light and act
// by their parts (LightsItsRows): each day and each hour is a target of its own, numbered from 1, each
// its band of the line edge to edge (Bands, §611), lit under the pointer and pressed (RowPress). The conditions
// now are one thing to do, so their line is it whole, lit by the folder (WeatherPartTile, §379). The keys walk the targets (KeyOrder) and Enter or Space does what the one they are on does —
// the first where they are on none. Each is read again from "Refresh" on its right click (IRefreshable, §451).

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock.Weather;

/// <summary>
/// Where a weather line's forecast comes from, where it is not the store: a test's, so nothing is
/// fetched and no clock is waited on.
/// </summary>
internal sealed class WeatherSource
{
    /// <summary>The last forecast, why a newer one isn't there, and whether one is on its way.</summary>
    public required Func<(WeatherCache? Shown, string? Problem, bool Fetching)> Read { get; init; }

    /// <summary>Fetches now, whatever its age: "Refresh" (IRefreshable). False when one is already on its way.</summary>
    public required Func<bool> Ask { get; init; }
}

/// <summary>A weather line of a list: its place, the store it reads, and what the store holds for it now.</summary>
internal abstract unsafe class WeatherLineTile : CellIsland, IRefreshable
{
    /// <summary>The age line's name, so a test can read the ink a stale forecast puts it in.</summary>
    internal static string UpdatedName => S.DockWeatherUpdated;

    // The line's place, as it shows it.
    private WeatherOptions options = new();

    // The settings a click searches the weather with: the app's search engine (Config.SearchEngine).
    private Config? searchWith;

    // Whether this line has been looked at, and so registered with the store (Look). Until then it
    // has asked nobody anything.
    private bool looked;

    protected WeatherLineTile(string key, WidgetItem island, Gfx gfx)
        : base(key, island, gfx)
    {
    }

    /// <summary>The last forecast for the place, as the store had it at the last <see cref="Take"/>.</summary>
    protected WeatherCache? Shown { get; private set; }

    /// <summary>Why a newer forecast is not there; null while nothing failed.</summary>
    protected string? Problem { get; private set; }

    /// <summary>Whether a fetch is on its way.</summary>
    protected bool Fetching { get; private set; }

    /// <summary>
    /// Where this line's forecast comes from; the shared store when nothing says otherwise. A test
    /// hands in its own, so nothing is fetched and no clock is waited on. Handing one source to
    /// every line of a folder is also how a test says out loud what the store does for them in
    /// earnest: they read one reading.
    /// </summary>
    internal WeatherSource? Source { get; set; } = Samples.On ? Samples.WeatherSource() : null;

    /// <summary>
    /// The store this line registers with: the shared one, or a test's own
    /// (<see cref="WeatherStore(Func{WeatherStore.Request, CancellationToken, Task{WeatherCache}}, bool, OnlineUpdates?)"/>),
    /// which answers without the network and counts what it is asked. Set before the first look.
    /// </summary>
    internal WeatherStore Store { get; set; } = WeatherStore.Shared;

    /// <summary>The place it shows: its own.</summary>
    internal WeatherOptions Options => options;

    /// <summary>
    /// Every online widget's interval (<see cref="WeatherStore.Every"/>): each tick asks the store
    /// whether the place is due. A folder ticks its widgets only while it is open, so a line in a
    /// shut folder asks for nothing; one with no place has nothing to ask about.
    /// </summary>
    public override int IntervalMs => options.IsSet ? (int)Store.Every.TotalMilliseconds : 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        // The shape is a function of the settings, so the shape is given the settings just handed
        // over (SettingsFollowTests).
        Settings = island;
        searchWith = config;
        Settle(WeatherPlaces.Settled(island.Weather));
    }

    /// <summary>The place it shows now; another place, for a line already looked at, is registered and asked for.</summary>
    private void Settle(WeatherOptions next)
    {
        var same = next.IsSet == options.IsSet && WeatherStore.KeyOf(next) == WeatherStore.KeyOf(options);
        options = next;
        if (looked && !same && Source is null)
        {
            // Another place, for a line already being looked at: the store is told what it shows
            // now, asked for it if it is due, and hands back what it has. A line nobody has looked
            // at yet only remembers — its first look does the asking.
            Store.Use(this, options);
            Store.Poll(this);
            Take();
        }
    }

    /// <summary>
    /// What it still needs before it shows anything: nothing where it has a place; else what every
    /// widget without one says ("Set up in Settings"). The device's location, which a new line is
    /// for (WeatherOptions.New), is no place while Windows will not give it and nothing was ever
    /// fetched for it: then the line asks for a place too, as one with none does (2026-10-11).
    /// </summary>
    internal override SetupNeed? Unset =>
        !options.IsSet ? base.Unset
        : LocationOff ? SetupNeed.Of(Key, SetupField.Place)
        : null;

    /// <summary>
    /// Whether it is for the device's location, Windows did not give it at the last ask, and there is
    /// no forecast to show instead. What the store held at the last <see cref="Take"/>: the folder's
    /// ticks go on taking (the place is set), so the line follows location being switched on.
    /// </summary>
    internal bool LocationOff => options.UseDeviceLocation && Shown is null && WeatherStore.IsLocationOff(Problem);

    /// <summary>Nothing: the line already says everything it has to say about its place.</summary>
    public override string? Tooltip(int index) => null;

    /// <summary>Tests only, on their thread: where a click's search goes instead of the browser.</summary>
    [ThreadStatic]
    internal static Action<string>? Opening;

    /// <summary>
    /// A click on a day, an hour or the conditions (2026-10-11: "when clicking on a weather widget it
    /// should search for the weather in the default browser"): the weather at its place searched for
    /// in the default browser, with the app's search engine (SearchUrls.Weather) — on
    /// <paramref name="day"/> where one is named. Nothing for a line not set up, whose click is its
    /// "Set up in Settings".
    /// </summary>
    protected void SearchTheWeb(string? day = null)
    {
        if (Unset is not null)
        {
            return;
        }
        var url = SearchUrls.Weather(searchWith ?? new Config(), PlaceNames.Whole(options), day);
        if (Opening is { } test)
        {
            test(url);
            return;
        }
        _ = Launch.Open(url);
    }

    // --- Its targets ----------------------------------------------------------------------------

    // The target under the pointer, the one a press is down on and the one the keys are on; 0 for none.
    private int hot;
    private RowPress press;
    private int keyed;

    /// <summary>Its parts light themselves; the line as a whole is no one thing to press (FolderFlyout.LightsWhole).</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>What target <paramref name="target"/> (from 1) does.</summary>
    protected abstract void Act(int target);

    /// <summary>The targets the keys walk, in their order; the first is what Enter does on none.</summary>
    protected abstract IReadOnlyList<int> KeyOrder();

    /// <summary>The keys that walk back and on: Up and Down for rows, Left and Right across.</summary>
    protected virtual (int Back, int On) WalkKeys => (VK.VK_UP, VK.VK_DOWN);

    /// <summary>Whether target <paramref name="target"/> wears its plate: under the pointer or the keys.</summary>
    protected bool Lit(int target) => target != 0 && (hot == target || keyed == target);

    /// <summary>Whether a press is down on target <paramref name="target"/>.</summary>
    protected bool Held(int target) => press.On(target);

    /// <summary>Whether the keys are on target <paramref name="target"/>, which wears the focus ring.</summary>
    protected bool Keyed(int target) => target != 0 && keyed == target;

    /// <summary>tests: the target under the pointer, the one pressed and the one the keys are on; 0 for none.</summary>
    internal (int Hot, int Pressed, int Keyed) Lighted => (hot, press.Held, keyed);

    /// <summary>keys: the hour or the day its keys are on, by its target.</summary>
    protected internal override int? OwnKeyedTarget => keyed;

    /// <summary>keys: the hour or the day its keys are on is ringed (one ring, §471).</summary>
    protected internal override bool RingsOwnKeys => keyed != 0;

    /// <summary>keys: gone to another line, the hour or the day they were on is ringed no more.</summary>
    protected internal override void OwnKeys(bool on)
    {
        if (!on && keyed != 0)
        {
            keyed = 0;
            Redrawn();
        }
    }

    /// <summary>A target acts; anywhere else — the air between them — does nothing.</summary>
    public override IslandAction Click(int index)
    {
        if (press.Up())
        {
            _ = Wake();
        }
        if (index > 0 && Unset is null)
        {
            Take();
            Act(index);
        }
        return default;
    }

    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            hot = keyed = 0;
            _ = press.Up();
        }
    }

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var target = point is { } at ? CardTarget(at, box, style, surface) : 0;
        if (target != 0)
        {
            // The pointer and the keys light one target at a time.
            keyed = 0;
        }
        var let = press.Over(target);
        if (target == hot && !let)
        {
            return;
        }
        hot = target;
        _ = Wake();
    }

    /// <summary>A press on a target lights it pressed; the press stays the folder's, which makes it the click (RowPress).</summary>
    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (press.Down(CardTarget(point, box, style, surface)))
        {
            _ = Wake();
        }
    }

    /// <summary>keys: the line walks its hours or days itself (CellIsland.WalksItsOwnKeys).</summary>
    protected internal override bool WalksItsOwnKeys => true;

    /// <summary>
    /// The walking keys go from target to target — past either end they are the folder's again, and
    /// move on to the next line; Enter and Space do what the target they are on does, the first where
    /// they are on none, so the line is never a dead stop for the keys.
    /// </summary>
    protected internal override bool CardKey(int key, WidgetSurface surface)
    {
        if (Unset is not null)
        {
            return false;
        }
        Take();
        var order = KeyOrder();
        if (key is VK.VK_RETURN or VK.VK_SPACE)
        {
            var target = keyed != 0 ? keyed : order.Count > 0 ? order[0] : 0;
            if (target != 0)
            {
                Act(target);
                Redrawn();
            }
            return target != 0;
        }
        var (back, on) = WalkKeys;
        if (key != back && key != on)
        {
            return false;
        }
        var at = keyed == 0 ? -1 : IndexOf(order, keyed);
        var next = at < 0 ? (key == on ? 0 : order.Count - 1) : at + (key == on ? 1 : -1);
        if (next < 0 || next >= order.Count)
        {
            keyed = 0;
            Redrawn();
            return false;
        }
        keyed = order[next];
        hot = 0;
        Redrawn();
        return true;
    }

    private static int IndexOf(IReadOnlyList<int> order, int target)
    {
        for (var index = 0; index < order.Count; index++)
        {
            if (order[index] == target)
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>
    /// §611: where its targets are in <paramref name="box"/> — an hour's or a day's band, target 1 first —, the one
    /// answer what lights, what a press acts on and what a tooltip stands by all ask. Each a list's item
    /// (<see cref="LineItem"/>): reaching the line's edges, top to foot (<see cref="CellIsland.LineAround"/>), touching its
    /// neighbours; what it shows stays on the gutter. None by default, and none before there is a forecast.
    /// </summary>
    protected virtual List<D2D_RECT_F> Bands(D2D_RECT_F box, in TileStyle style) => [];

    /// <summary>Target <paramref name="target"/> (from 1) in words, as its tooltip says it.</summary>
    protected virtual string Said(int target) => "";

    /// <summary>The target whose band is under the pointer, from 1; 0 for none.</summary>
    protected internal sealed override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Take();
        return LineItem.At(Bands(box, style), point);
    }

    /// <summary>The target under the pointer in words, by its band.</summary>
    protected internal sealed override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Take();
        var bands = Bands(box, style);
        return LineItem.At(bands, point) is > 0 and var target && Said(target) is { Length: > 0 } said ? (said, bands[target - 1]) : null;
    }

    /// <summary>
    /// The targets' <paramref name="bands"/> lit as a list's items are (§428, §611): the subtle fill edge to edge under the
    /// pointer or the keys, the pressed one while a press is down on it — square, the line's clip rounding what reaches a
    /// corner — and the focus ring just inside it where the keys are, so the clip leaves it whole.
    /// </summary>
    protected void Light(ID2D1DeviceContext* dc, List<D2D_RECT_F> bands, in TileStyle style, QuickTheme theme)
    {
        for (var index = 0; index < bands.Count; index++)
        {
            if (!Lit(index + 1))
            {
                continue;
            }
            LineItem.Light(dc, bands[index], theme, Held(index + 1));
            if (Keyed(index + 1))
            {
                var radius = style.Px(4);
                TogglesTile.FocusRing(dc, LineItem.Inside(bands[index], radius), radius, style, theme);
            }
        }
    }

    /// <summary>A place to fetch for ("Refresh", IRefreshable).</summary>
    public bool CanRefresh => options.IsSet;

    /// <summary>A fetch is on its way.</summary>
    public bool Refreshing
    {
        get
        {
            Take();
            return Fetching;
        }
    }

    /// <summary>When the forecast shown was fetched.</summary>
    public DateTime? UpdatedUtc => Shown?.FetchedUtc;

    /// <summary>Why a newer forecast is not there.</summary>
    string? IRefreshable.Problem
    {
        get
        {
            Take();
            return Problem;
        }
    }

    /// <summary>"Refresh": a fetch now, whatever the age.</summary>
    public void RefreshNow()
    {
        Look();
        _ = Source is { } from ? from.Ask() : Store.Ask(this);
        Take();
    }

    /// <summary>
    /// Nothing to do: an open folder arms its own tick at the next minute and draws its lines, so the
    /// age, the mark for now and the staleness that goes with them turn over there.
    /// </summary>
    public override bool MinuteChanged(Gfx gfx) => false;

    public override void Dispose()
    {
        if (looked)
        {
            looked = false;
            // The last line or face to go for a place drops what is still on its way for it.
            Store.Release(this);
        }
        base.Dispose();
    }

    /// <summary>
    /// The folder opening, one of its ticks, or the store posting: a fetch is started if the place
    /// is due, news is raised once for everybody showing this place, and the line reads what the
    /// store holds.
    /// </summary>
    protected override bool Sample(bool everything)
    {
        Look();
        if (Source is null)
        {
            if (everything)
            {
                Store.Deliver(this);
            }
            Store.Poll(this);
        }
        Take();
        return false;
    }

    /// <summary>
    /// The first look at the line: its place is registered with the store and asked for if it is due
    /// (docs\widgets.md §11). Once. A line with a source of its own — a test's — has nothing to
    /// register, because nothing about it reaches the store.
    /// </summary>
    private void Look()
    {
        if (looked || Source is not null)
        {
            return;
        }
        looked = true;
        Store.Use(this, options);
        Store.Poll(this);
    }

    /// <summary>What the store holds for this line's place now; the first call is the line's first look.</summary>
    protected void Take()
    {
        Look();
        (Shown, Problem, Fetching) = Source is { } from ? from.Read() : Store.Read(this);
    }

    /// <summary>
    /// The whole forecast in words, for what the store holds now. Through <see cref="Clock.Now"/> and
    /// never DateTime.Now, so a test can freeze the mark for now and the age line.
    /// </summary>
    protected WeatherReading Read() =>
        WeatherReading.From(Shown, options, Clock.Now().ToUniversalTime(), Problem);

    /// <summary>Never drawn on a bar: the weather's pieces are lines of a list and nothing else.</summary>
    internal override void PaintCell(TerraFX.Interop.DirectX.ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    /// <summary>The colours and sizes the flyout's controls paint with, for this line's style.</summary>
    protected QuickContext Context(in TileStyle style) => new()
    {
        Gfx = Gfx,
        Theme = QuickTheme.For(style.Theme),
        Scale = MathF.Max(0.1f, style.Scale),
        TextScale = style.TextScale,
    };
}
