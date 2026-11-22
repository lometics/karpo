// The air quality as lines of a list (docs\widgets.md §210, §590): what the air is like at its place, read from
// Open-Meteo's air-quality service through the air's store (AirQualityStore), which keeps one fetch per place for every
// air-quality widget showing it.
//
// The place is the line's own (WeatherOptions, as every weather line's), and so is its index
// (WeatherOptions.AirIndex; the place's region's where nobody chose). Nothing is asked until the line
// is looked at (§11), and then on the folder's ticks at the online interval, as the weather's lines.
//
// §590 (the person, 2026-11-09: "these widgets should be broken down into smaller ones that always show the same amount
// of data ... same for other widgets that have a similar behaviour"): what a taller line showed piece by piece is four
// widgets, one class, each the same at both of its sizes, on the kit's blocks (§401):
//
//   airQuality     the air's glyph, "Air quality" over the place and the index's name, the level and the index at the
//                  end (Compact); the same over the index's scale, its bands in their colours, a mark where it stands (Full)
//   airPollutants  PM2.5, PM10, ozone and nitrogen dioxide with their units: two columns (Compact), a row each (Full)
//   airPollen      the six plants' pollen, grains per m³: three columns (Compact), a row each (Full); where the place has
//                  no pollen forecast (outside Europe) its one Status says so
//   airForecast    the place's day, a bar an hour in its level's colour from midnight to midnight on the weather's axis,
//                  now marked and moving left to right, the hours gone quiet (§501): 28 DIP tall (Compact), 64 (Full)
//
// Nothing come yet or out of reach, each is the kit's one Status on its top row, at its size's height all the same.
// "Refresh" on the right click fetches it again (IRefreshable, §451).
//
// §614 (the person, 2026-11-09: "i should also be able to click on the air quality widget to do a search for it"): each
// of the four is one thing to press, lit whole; its click searches the air quality at its place with the app's engine
// (SearchUrls.Air: "air quality Zürich, …"), opened once the folder has gone (IOpensOnceGone), as a clock's (§377).

using System.Globalization;
using Karpo.Core;
using Karpo.Dock.Weather;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>
/// Where an air-quality line's answer comes from, where it is not the store: a test's, so nothing is
/// fetched and no clock is waited on.
/// </summary>
internal sealed class AirSource
{
    public required Func<(AirCache? Shown, string? Problem, bool Fetching)> Read { get; init; }
}

/// <summary>The air quality for a place, as a line of a list.</summary>
internal sealed unsafe class AirQualityTile : CellIsland, IRefreshable, IListContent, IOpensOnceGone
{
    /// <summary>Segoe Fluent Icons: Leaf, beside the pollen.</summary>
    private const string LeafGlyph = "";

    private WeatherOptions options = new();
    private bool looked;

    public AirQualityTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    /// <summary>The last answer for the place, why a newer one is not there, and whether one is on its way.</summary>
    private (AirCache? Shown, string? Problem, bool Fetching) held;

    /// <summary>Tests only: where the answer comes from instead of the store.</summary>
    internal AirSource? Source { get; set; } = Samples.On ? Samples.AirSource() : null;

    /// <summary>The store this line registers with: the shared one, or a test's own.</summary>
    internal AirQualityStore Store { get; set; } = AirQualityStore.Shared;

    /// <summary>The place it shows, and its index.</summary>
    internal WeatherOptions Options => options;

    public override int IntervalMs => options.IsSet ? (int)Store.Every.TotalMilliseconds : 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        searchWith = config;
        var next = WeatherPlaces.Settled(island.Weather);
        var same = next.IsSet == options.IsSet && WeatherStore.KeyOf(next) == WeatherStore.KeyOf(options);
        options = next;
        if (looked && !same && Source is null)
        {
            Store.Use(this, options);
            Store.Poll(this);
            Take();
        }
    }

    /// <summary>Nothing set, or this PC's location off with nothing ever fetched: "Set up in Settings" at the place.</summary>
    internal override SetupNeed? Unset =>
        !options.IsSet ? base.Unset
        : LocationOff ? SetupNeed.Of(Key, SetupField.Place)
        : null;

    private bool LocationOff => options.UseDeviceLocation && held.Shown is null && WeatherStore.IsLocationOff(held.Problem);

    public override string? Tooltip(int index) => null;

    // §614: the settings a click searches with (the app's engine), and the search it chose, opened once the folder has gone.
    private Config? searchWith;
    private string? chosen;

    /// <summary>Where the search opens: the browser, through Karpo's one launcher (§491). A test puts its own here.</summary>
    public Action<string> Opened { get; set; } = static link => Launch.Open(link);

    /// <summary>
    /// §614: the air quality at its place searched for with the app's engine (<see cref="SearchUrls.Air"/>), once the folder
    /// has gone; the device's location the engine's own. Nothing for a line not set up, whose click is its "Set up".
    /// </summary>
    public override IslandAction Click(int index)
    {
        chosen = Unset is null ? SearchUrls.Air(searchWith ?? new Config(), PlaceNames.Whole(options)) : null;
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

    /// <summary>The folder's ticks turn the age over; nothing of its own on the minute.</summary>
    public override bool MinuteChanged(Gfx gfx) => false;

    /// <summary>A place to fetch for ("Refresh", IRefreshable).</summary>
    public bool CanRefresh => options.IsSet;

    /// <summary>A fetch is on its way.</summary>
    public bool Refreshing
    {
        get
        {
            Take();
            return held.Fetching;
        }
    }

    /// <summary>When the answer shown was fetched.</summary>
    public DateTime? UpdatedUtc => held.Shown?.FetchedUtc;

    /// <summary>Why a newer answer is not there.</summary>
    string? IRefreshable.Problem
    {
        get
        {
            Take();
            return held.Problem;
        }
    }

    /// <summary>"Refresh": a fetch now whatever the age.</summary>
    public void RefreshNow()
    {
        Look();
        if (Source is null)
        {
            _ = Store.Ask(this);
        }
        Take();
    }

    /// <summary>§590: which of the air's four this line is.</summary>
    private ListKind Part => Settings is CellItem { List: { } kind } ? kind : ListKind.AirQuality;

    private bool Compact => LineSizes.Of(Settings) == LineSize.Compact;

    /// <summary>list: the line's form at its size, whatever the answer says (§590).</summary>
    public float ListHeight(float width, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var row = LineRow.Height(style, caption: false);
        return Part switch
        {
            ListKind.AirPollutants => Compact ? form.Pad + (2 * (form.SmallLine + form.Line)) + form.Gap + form.Pad : 4 * row,
            ListKind.AirPollen => Compact ? form.Pad + (2 * (form.SmallLine + form.Line)) + form.Gap + form.Pad : PlantCount * row,
            ListKind.AirForecast => form.Pad + HoursTall(style) + style.Px(2) + form.SmallLine + form.Pad,
            _ => LineRow.Height(style, caption: true) + (Compact ? 0 : GaugeHeight(style) + form.Pad),
        };
    }

    /// <summary>The plants the pollen forecast has (<see cref="AirReading.Plants"/>).</summary>
    private const int PlantCount = 6;

    /// <summary>§590: the day's bars, 28 DIP at Compact and 64 at Full.</summary>
    private float HoursTall(in TileStyle style) => style.Px(Compact ? 28 : 64);

    public override void Dispose()
    {
        if (looked)
        {
            looked = false;
            Store.Release(this);
        }
        base.Dispose();
    }

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

    /// <summary>The first look: the place registered with the store and asked for if it is due (§11). Once.</summary>
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

    private void Take()
    {
        Look();
        held = Source is { } from ? from.Read() : Store.Read(this);
    }

    /// <summary>What it says now, through Clock.Now so a test can hold the hour still.</summary>
    internal AirReading Read()
    {
        Take();
        return AirReading.From(held.Shown, options.AirIndex, Clock.Now().ToUniversalTime(), held.Problem);
    }

    /// <summary>Never drawn on a bar: on the bar it is the air-quality face.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- The drawing ------------------------------------------------------------------------------

    /// <summary>The gauge's height: a mark over and under a 6-DIP band.</summary>
    private static float GaugeHeight(in TileStyle style) => style.Px(14);

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var reading = Read();
        var form = ListForm.Of(Gfx, style);
        var sketch = new LineSketch(dc, Gfx);
        if (reading.State != AirState.Ready)
        {
            LineStatus.Draw(sketch, box, style, Info.Glyph, reading.Problem ?? S.DockAirLoading, []);
            return;
        }
        dc->PushAxisAlignedClip(&box, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            switch (Part)
            {
                case ListKind.AirPollutants:
                    Figures(dc, sketch, box, style, form, [.. reading.Pollutants.Select(each => (each.Name, each.Value, each.Unit))], 2);
                    break;
                case ListKind.AirPollen when reading.Plants is { } plants:
                    Figures(dc, sketch, box, style, form, [.. plants.Select(each => (each.Name, each.Value, ""))], 3);
                    break;
                case ListKind.AirPollen:
                    LineStatus.Draw(sketch, box, style, LeafGlyph, S.DockAirPollenNoForecast, []);
                    break;
                case ListKind.AirForecast:
                    Hours(dc, box, style, form, reading, box.top + form.Pad);
                    break;
                default:
                    var row = Row(sketch, box, style, reading);
                    if (!Compact)
                    {
                        Gauge(dc, box, style, form, reading, row.Bottom);
                    }
                    break;
            }
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>
    /// The index: the air's glyph, "Air quality" over the place and the index's name, the level and the index at the
    /// row's end — the fullest of them that leaves the name its first letters.
    /// </summary>
    private LineRow.Laid Row(LineSketch sketch, D2D_RECT_F box, in TileStyle style, AirReading reading)
    {
        var form = ListForm.Of(Gfx, style);
        var value = reading.Value.ToString(CultureInfo.InvariantCulture);
        var (px, weight) = LineRow.Type(style, RowReading.Value);
        var room = TilePaint.Width(box) - form.TextLeft - form.Inset - style.Px(LineSpace.GapDip) - PopupPaint.Measure(Gfx, Info.Title[..Math.Min(4, Info.Title.Length)] + "…", form.TextPx, Weight.Regular).X;
        var said = ListTiers.Whole(Gfx, [LineRow.Join(reading.Word, value), LineRow.Join(reading.Short, value)], px, weight, room) ?? value;
        return LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec(Info.Title)
        {
            Glyph = Info.Glyph,
            Caption = PlaceLabel.Caption(PlaceNames.Of(Options), reading.Name),
            Reading = said,
            ReadingInk = reading.Stale ? style.Theme.TextSecondary : null,
            CaptionPiece = PlaceLabel.Piece,
            ReadingPiece = "index",
        });
    }

    /// <summary>
    /// Figures with their names: in <paramref name="columns"/> columns, each its name over its value and unit (Compact),
    /// or the kit's row each, its name and its value with its unit at the end (Full). The pollutants', the plants'.
    /// </summary>
    private void Figures(ID2D1DeviceContext* dc, LineSketch sketch, D2D_RECT_F box, in TileStyle style, in ListForm form, IReadOnlyList<(string Name, string Value, string Unit)> figures, int columns)
    {
        var theme = style.Theme;
        if (!Compact)
        {
            var row = LineRow.Height(style, caption: false);
            for (var index = 0; index < figures.Count; index++)
            {
                var (name, value, unit) = figures[index];
                LineRow.Draw(sketch, box.left, box.right, box.top + (index * row), style, new RowSpec(name)
                {
                    Reading = unit.Length > 0 ? $"{value} {unit}" : value,
                    NamePiece = "figure",
                    ReadingPiece = "value",
                });
            }
            return;
        }
        var left = box.left + form.Inset;
        var each = (TilePaint.Width(box) - (2 * form.Inset)) / columns;
        var top = box.top + form.Pad;
        for (var index = 0; index < figures.Count; index++)
        {
            var (name, value, unit) = figures[index];
            var x = left + ((index % columns) * each);
            var y = top + ((index / columns) * (form.SmallLine + form.Line + form.Gap));
            var room = MathF.Max(1, each - style.Gap);
            TilePaint.Line(dc, Gfx, name, form.SmallPx, Weight.Regular, theme.TextSecondary, x, y, room);
            TilePaint.Line(dc, Gfx, value, form.TextPx, Weight.Semibold, theme.Text, x, y + form.SmallLine, room);
            var figure = PopupPaint.Measure(Gfx, value, form.TextPx, Weight.Semibold).X;
            var unitRoom = room - figure - style.Px(3);
            if (unit.Length > 0 && unitRoom > style.Px(16))
            {
                // On the figure's baseline, near enough: the small line's foot a little over the big one's.
                TilePaint.Line(dc, Gfx, unit, form.SmallPx, Weight.Regular, theme.TextSecondary, x + figure + style.Px(3), y + form.SmallLine + PopupPaint.Round((form.Line - form.SmallLine) * 0.75f), unitRoom);
            }
        }
    }

    /// <summary>The index's scale from gutter to gutter, its bands in their colours, and a mark where the index stands.</summary>
    private void Gauge(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in ListForm form, AirReading reading, float top)
    {
        var left = box.left + form.Inset;
        var right = box.right - form.Inset;
        var span = right - left;
        var (least, most) = AirBands.Span(reading.Scale);
        var band = style.Px(6);
        var height = GaugeHeight(style);
        var y = top + PopupPaint.Round((height - band) / 2);
        float At(double value) => left + (float)(Math.Clamp((value - least) / (most - least), 0, 1) * span);
        for (var level = 0; level < AirBands.Levels; level++)
        {
            var from = At(AirBands.Start(reading.Scale, level));
            var to = level + 1 < AirBands.Levels ? At(AirBands.Start(reading.Scale, level + 1)) : right;
            if (to - from < 0.5f)
            {
                continue;
            }
            // A pixel of air between two bands, and the ends rounded.
            var piece = PopupPaint.Rect(from + (level > 0 ? style.Px(1) : 0), y, MathF.Max(1, to - from - (level > 0 ? style.Px(1) : 0)), band);
            PopupPaint.Fill(dc, piece, level == 0 || level == AirBands.Levels - 1 ? band / 2 : 0, AirBands.Ink(reading.Scale, level, level == reading.Level ? 1 : 0.55f));
        }
        // The mark: a ring in the level's colour, the text's ink round it.
        var mark = height;
        var x = Math.Clamp(At(reading.Value), left + (mark / 2), right - (mark / 2));
        var ring = PopupPaint.Rect(PopupPaint.Round(x - (mark / 2)), top, mark, mark);
        PopupPaint.Fill(dc, ring, mark / 2, PopupPaint.Ink(style.Theme.Base));
        PopupPaint.Fill(dc, PopupPaint.Rect(ring.left + style.Px(3), ring.top + style.Px(3), mark - (2 * style.Px(3)), mark - (2 * style.Px(3))), (mark / 2) - style.Px(3), AirBands.Ink(reading.Scale, reading.Level));
        PopupPaint.Stroke(dc, ring, mark / 2, MathF.Max(1, style.Px(1.5f)), style.Theme.Text);
    }

    /// <summary>
    /// §501: the place's day, midnight to midnight from left to right, the weather's axis (DayGraph): a bar an hour where
    /// its hour of the day is, as tall as its index on the scale, in its level's colour; the hours gone by quiet, the hour
    /// now whole and ringed, "Now" under it, a time under every sixth hour where that leaves it room.
    /// </summary>
    private void Hours(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in ListForm form, AirReading reading, float top)
    {
        var theme = style.Theme;
        var left = box.left + form.Inset;
        var right = box.right - form.Inset;
        var hours = reading.Hours;
        var each = (right - left) / AirReading.DayHours;
        // §590: as tall as the line's size has them, 28 DIP at Compact and 64 at Full.
        var tall = HoursTall(style);
        var (_, most) = AirBands.Span(reading.Scale);
        // The tallest hour fills the room where the day stays low: a quiet day is still read.
        var top100 = Math.Max(hours.Max(hour => hour.Value), AirBands.Start(reading.Scale, 2));
        var scale = Math.Min(most, top100);
        var foot = top + tall;
        var width = MathF.Max(1, each - MathF.Max(1, style.Px(2)));
        // The kit's control corner, a pill where a bar is narrower than two of them.
        var corner = MathF.Min(style.Px(LineCorner.ControlDip), width / 2);
        foreach (var hour in hours)
        {
            var slot = hour.At.Hour;
            var bar = MathF.Max(style.Px(2), (float)(Math.Clamp(hour.Value / scale, 0, 1) * tall));
            var x = left + (slot * each);
            var gone = reading.Now != AirReading.NoNow && slot < reading.Now;
            var plate = PopupPaint.Rect(x, foot - bar, width, bar);
            PopupPaint.Fill(dc, plate, corner, AirBands.Ink(reading.Scale, hour.Level, slot == reading.Now ? 1 : gone ? 0.35f : 0.85f));
            if (slot == reading.Now)
            {
                PopupPaint.Stroke(dc, plate, corner, MathF.Max(1, style.Px(1)), theme.Text);
            }
        }
        // Under the axis: "Now" under the hour now, and the time under every sixth hour that leaves it room.
        var room = style.Text(40);
        var under = foot + style.Px(2);
        var now = reading.Now == AirReading.NoNow ? (float?)null : Math.Clamp(left + (reading.Now * each), left, MathF.Max(left, right - room));
        if (now is { } at)
        {
            TilePaint.Line(dc, Gfx, S.DockAirNow, form.SmallPx, Weight.Semibold, theme.Text, at, under, room);
        }
        foreach (var hour in hours.Where(hour => hour.At.Hour % 6 == 0))
        {
            var x = left + (hour.At.Hour * each);
            if (x + room <= right + 0.5f && (now is not { } marked || MathF.Abs(x - marked) >= room + style.Px(4)))
            {
                TilePaint.Line(dc, Gfx, hour.Time, form.SmallPx, Weight.Regular, theme.TextSecondary, x, under, style.Text(48));
            }
        }
    }
}
