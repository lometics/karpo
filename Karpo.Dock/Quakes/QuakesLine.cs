// The earthquakes as a line of a list (docs\widgets.md §240): the past week's near the line's own place
// (WeatherOptions, as every weather line's), over its least magnitude and within its radius, read from
// the USGS's feed through the earthquakes' store, which keeps one fetch for every earthquake widget.
//
// At every height (§9, "A line at every height"), each piece only where it fits whole, on the kit's blocks (§401):
//   one row      the kit's Row: the newest's dot in the leading column, its magnitude and how far, its age at the end
//   from two     the kit's Header — the earthquakes' glyph, "Earthquakes", how far it looks at the end — and
//                under it an item per earthquake, newest first, as many as fit: the dot, "M4.8" and where in
//                the USGS's words, and under them how far, which way and how long ago
// With nothing to list — nothing come, a quiet week, out of reach — the header stands over the kit's one Status
// (the places' mockup, §401), the Status alone on a line of one row. Automatic (§388), it is as tall as the
// header and its rows, or the header and the Status. A row lights under the pointer, and its click opens the
// earthquake's page on the USGS's site once the folder has gone; "Refresh" on its right click fetches
// the feed again (IRefreshable, §451).

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.Weather;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>The earthquakes near a place, as a line of a list.</summary>
internal sealed unsafe class QuakesLine : CellIsland, IRefreshable, IListContent
{
    private WeatherOptions options = new();
    private QuakeSettings settings = QuakeSettings.Default;
    private bool looked;
    private QuakeHeld held = QuakeHeld.None;

    // The row under the pointer and pressed, from 1; the page a click chose, for once the folder has gone.
    private int hot;
    private RowPress press;
    private string? chosen;

    public QuakesLine(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    /// <summary>Tests only: where the answer comes from instead of the store.</summary>
    internal Func<QuakeHeld>? Source { get; set; } = Samples.On ? () => Samples.Quakes() : null;

    /// <summary>Where a page opens: the browser, through the shell. A test puts its own here.</summary>
    internal Action<string> Opened { get; set; } = static link => Launch.Open(link);

    /// <summary>Its ticks: while it looks somewhere, as often as the store fetches; the minute's own moves the ages on.</summary>
    public override int IntervalMs => Looks ? (int)QuakeStore.Every.TotalMilliseconds : 0;

    protected override int Count => 1;

    /// <summary>Whether it looks anywhere: a place set, or the whole world.</summary>
    private bool Looks => options.IsSet || settings.Worldwide;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        var next = WeatherPlaces.Settled(island.Weather);
        var device = next.UseDeviceLocation != options.UseDeviceLocation;
        options = next;
        settings = QuakeChoices.Of(island.Weather);
        if (looked && device && Source is null)
        {
            QuakeStore.Shared.Use(this, options.UseDeviceLocation);
            QuakeStore.Shared.Poll();
        }
        Take();
        Forget();
    }

    /// <summary>Nothing set and not the world, or this PC's location off with nothing known of it: "Set up in Settings" at the place.</summary>
    internal override SetupNeed? Unset =>
        settings.Worldwide ? null
        : !options.IsSet ? base.Unset
        : options.UseDeviceLocation && QuakeReading.OriginOf(options, held) is null && WeatherStore.IsLocationOff(held.DeviceProblem) ? SetupNeed.Of(Key, SetupField.Place)
        : null;

    public override string? Tooltip(int index) => null;

    /// <summary>Its rows light under the pointer themselves.</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>Somewhere to look ("Refresh", IRefreshable): the world, or a place.</summary>
    public bool CanRefresh => Looks;

    /// <summary>A fetch is on its way.</summary>
    public bool Refreshing
    {
        get
        {
            Take();
            return held.Fetching;
        }
    }

    /// <summary>When the earthquakes shown were fetched.</summary>
    public DateTime? UpdatedUtc => held.FetchedUtc;

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
            _ = QuakeStore.Shared.Ask();
        }
        Take();
        Redrawn();
    }

    public override void Dispose()
    {
        if (looked && Source is null)
        {
            QuakeStore.Shared.Release(this);
        }
        looked = false;
        base.Dispose();
    }

    protected override bool Sample(bool everything)
    {
        Look();
        if (Source is null)
        {
            if (everything)
            {
                QuakeStore.Shared.Deliver();
            }
            QuakeStore.Shared.Poll();
        }
        Take();
        return false;
    }

    /// <summary>The ages move on with the clock.</summary>
    public override bool MinuteChanged(Gfx gfx)
    {
        Take();
        return false;
    }

    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            hot = 0;
            _ = press.Up();
        }
    }

    /// <summary>The first look: registered with the store and asked for where due (§11). Once.</summary>
    private void Look()
    {
        if (looked || Source is not null || !Looks)
        {
            return;
        }
        looked = true;
        QuakeStore.Shared.Use(this, options.UseDeviceLocation);
        QuakeStore.Shared.Poll();
    }

    private void Take() => held = Source is { } from ? from() : looked ? QuakeStore.Shared.Read() : held;

    /// <summary>What it says now, through Clock.Now so a test can hold the hour still.</summary>
    internal QuakeReading Read()
    {
        Look();
        Take();
        return QuakeReading.From(held, settings, options, Clock.Now().ToUniversalTime());
    }

    /// <summary>Never drawn on a bar: on the bar it is the earthquake face.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- Where things are ------------------------------------------------------------------------

    /// <summary>An item per earthquake: the kit's row with its caption.</summary>
    private static float RowHeight(in TileStyle style) => LineRow.Height(style, caption: true);

    /// <summary>The header's height: the kit's row.</summary>
    private static float HeadHeight(in TileStyle style) => LineRow.Height(style, caption: false);

    /// <summary>
    /// What a line <paramref name="box"/> tall draws: one row where the head and an earthquake's row
    /// would not fit, else the head and the rows that do, each row's box from its top.
    /// </summary>
    internal QuakesPlan Plan(D2D_RECT_F box, in TileStyle style, QuakeReading reading)
    {
        var height = TilePaint.Height(box);
        var rowHeight = RowHeight(style);
        var head = HeadHeight(style);
        var headed = height + 0.5f >= head + rowHeight + style.Px(4);
        // Nothing to list — nothing come, a quiet week, out of reach — is the kit's one Status: under the header
        // where the two fit (§401), else alone on the top row (§388).
        if (reading.State != QuakeState.Ready || reading.Rows.Count == 0)
        {
            return height + 0.5f >= 2 * head ? new QuakesPlan(One: false, Rows: []) : new QuakesPlan(One: true, Rows: []);
        }
        if (!headed && height + 0.5f < rowHeight + style.Px(4))
        {
            return new QuakesPlan(One: true, Rows: []);
        }
        // Two rows: the rows alone, as many as fit, in the line's middle; from three, under the head.
        var top = headed ? box.top + head
            : box.top + PopupPaint.Round((height - (MathF.Floor((height + 0.5f) / rowHeight) * rowHeight)) / 2);
        var bottom = box.bottom - style.Px(4);
        var rows = new List<D2D_RECT_F>();
        foreach (var _ in reading.Rows)
        {
            if (top + rowHeight > bottom + 0.5f)
            {
                break;
            }
            rows.Add(new D2D_RECT_F(box.left, top, box.right, top + rowHeight));
            top += rowHeight;
        }
        return new QuakesPlan(One: false, Rows: rows) { Headed = headed };
    }

    /// <summary>
    /// list: as tall as what it draws at the most its line may be (IListContent, §388): the header and every
    /// earthquake's item that fits, and the air under the last; the header and the Status where it has nothing
    /// to list — nothing come yet, a quiet week, out of reach (§401); one row not set up or with no room for
    /// the header; the whole of the most where its items stand alone on two rows.
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        var most = LineRows.Most(Settings, style);
        var reading = Read();
        var plan = Plan(PopupPaint.Rect(0, 0, width, most), style, reading);
        return Unset is not null || plan.One ? LineRows.Px(1, style)
            : plan.Rows.Count == 0 ? 2 * HeadHeight(style)
            : plan.Headed ? MathF.Ceiling(plan.Rows[^1].bottom + style.Px(4))
            : most;
    }

    // --- Clicks ----------------------------------------------------------------------------------

    /// <summary>The number the folder carries for the row at <paramref name="point"/>: its place plus one, 0 for none.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Plan(box, style, Read());
        if (plan.One)
        {
            return Read().Rows.Count > 0 ? 1 : 0;
        }
        for (var index = 0; index < plan.Rows.Count; index++)
        {
            if (LineRows.Inside(plan.Rows[index], point))
            {
                return index + 1;
            }
        }
        return 0;
    }

    /// <summary>Row <paramref name="index"/>'s earthquake (from 1): its page opens once the folder has gone; anything else does nothing.</summary>
    public override IslandAction Click(int index)
    {
        _ = press.Up();
        var rows = Read().Rows;
        chosen = index >= 1 && index <= rows.Count && Feeds.Opens(rows[index - 1].Quake.Url) ? rows[index - 1].Quake.Url : null;
        Redrawn();
        return chosen is null ? default : new IslandAction(View: IslandView.App);
    }

    /// <summary>The earthquake under the pointer, for its menu's "Open on map" (Dock.MapPage, §319); null over anything else.</summary>
    internal Quake? MenuQuake => Read().Rows is var rows && hot >= 1 && hot <= rows.Count ? rows[hot - 1].Quake : null;

    /// <summary>The dock, once the folder has gone: opens the page the click chose, once.</summary>
    internal void Open()
    {
        if (chosen is { } link)
        {
            chosen = null;
            Opened(link);
        }
    }

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var target = point is { } at ? CardTarget(at, box, style, surface) : 0;
        var let = press.Over(target);
        if (target == hot && !let)
        {
            return;
        }
        hot = target;
        _ = Wake();
    }

    /// <summary>A press on a row lights it pressed; the press stays the folder's, which makes it the click (RowPress).</summary>
    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (press.Down(CardTarget(point, box, style, surface)))
        {
            _ = Wake();
        }
    }

    /// <summary>A row's place in the USGS's words, whole, and how deep, where the row cuts it.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var reading = Read();
        var plan = Plan(box, style, reading);
        if (plan.One || CardTarget(point, box, style, surface) is not (> 0 and var target) || target > plan.Rows.Count)
        {
            return null;
        }
        var quake = reading.Rows[target - 1].Quake;
        var tip = quake.Depth is { } depth
            ? quake.Place + "\n" + S.DockQuakesDepth(Math.Round(depth).ToString("0", global::System.Globalization.CultureInfo.InvariantCulture))
            : quake.Place;
        return tip.Length > 0 ? (tip, plan.Rows[target - 1]) : null;
    }

    // --- Drawn -----------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var reading = Read();
        var plan = Plan(box, style, reading);
        var form = ListForm.Of(Gfx, style);
        dc->PushAxisAlignedClip(&box, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            if (plan.One)
            {
                One(dc, box, style, reading);
                return;
            }
            if (plan.Headed)
            {
                Head(dc, box, style, reading);
            }
            if (plan.Rows.Count == 0)
            {
                var head = HeadHeight(style);
                LineStatus.Draw(new LineSketch(dc, Gfx), box with { top = box.top + head }, style, Info.Glyph, Why(reading), []);
                return;
            }
            var theme = QuickTheme.For(style.Theme);
            for (var index = 0; index < plan.Rows.Count; index++)
            {
                Row(dc, plan.Rows[index], style, theme, reading, reading.Rows[index], index + 1);
            }
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>What it says where it has nothing to list: why, or that the week was quiet.</summary>
    internal static string Why(QuakeReading reading) =>
        reading.State == QuakeState.Ready ? S.DockQuakesNoneWeek : reading.Problem ?? S.DockQuakesLoading;

    /// <summary>The magnitude's dot in the leading column on a row's middle, with a hairline of the text's ink so the yellow reads on a light list; faded for none.</summary>
    private void Dot(ID2D1DeviceContext* dc, in TileStyle style, QuakeRow? row, bool stale, float left, float middle)
    {
        var form = ListForm.Of(Gfx, style);
        var size = PopupPaint.Round(form.Glyph * 0.75f);
        var column = form.IconBox(left, middle - (form.Icon / 2), form.Icon);
        var at = PopupPaint.Rect(PopupPaint.Round(((column.left + column.right) / 2) - (size / 2)), PopupPaint.Round(middle - (size / 2)), size, size);
        var colour = row is { } shown ? QuakeLevels.Ink(shown.Level, stale ? 0.45f : 1) : IslandInk.Faded(style.Theme.TextSecondary, 0.5f);
        PopupPaint.Fill(dc, at, size / 2, colour);
        PopupPaint.Stroke(dc, at, size / 2, MathF.Max(1, style.Px(1)), IslandInk.Faded(style.Theme.Text, 0.18f));
    }

    /// <summary>One row: the kit's row — the newest's dot, its magnitude and how far, its age at the end; or why there is none, the kit's Status (§388).</summary>
    private void One(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, QuakeReading reading)
    {
        var sketch = new LineSketch(dc, Gfx);
        var newest = reading.State == QuakeState.Ready ? reading.Rows.FirstOrDefault() : null;
        if (newest is null)
        {
            LineStatus.Draw(sketch, box, style, Info.Glyph, Why(reading), []);
            return;
        }
        var row = LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec(newest.Away.Length > 0 ? LineRow.Join(newest.Magnitude, newest.Away) : newest.Magnitude)
        {
            NameInk = reading.Stale ? style.Theme.TextSecondary : null,
            Reading = S.DockQuakesAgo(newest.Age),
            Look = RowReading.Meta,
        }, MathF.Min(TilePaint.Height(box), LineRow.Height(style, caption: false)));
        Dot(dc, style, newest, reading.Stale, box.left, row.Middle);
    }

    /// <summary>The header: the earthquakes' glyph, "Earthquakes", and how far it looks — with the place where both fit — at its end.</summary>
    private void Head(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, QuakeReading reading)
    {
        var form = ListForm.Of(Gfx, style);
        var place = PlaceNames.Of(options);
        var within = settings.Worldwide ? QuakeChoices.RadiusName(QuakeChoices.World) : S.DockQuakesWithin(QuakeChoices.RadiusName(settings.Radius));
        var room = TilePaint.Width(box) - form.TextLeft - form.Inset - style.Px(LineSpace.GapDip) - PopupPaint.Measure(Gfx, Info.Title, form.TextPx, Weight.Semibold).X;
        var meta = ListTiers.Whole(Gfx, [LineRow.Join(place, within), within], style.Text(LineType.CaptionDip), Weight.Regular, room);
        LineRow.Draw(new LineSketch(dc, Gfx), box.left, box.right, box.top, style, new RowSpec(Info.Title)
        {
            Glyph = Info.Glyph,
            Strong = true,
            Reading = meta,
            Look = RowReading.Meta,
            ReadingInk = reading.Stale && reading.Rows.Count > 0 ? new IslandInk(style.Look).Alarm : null,
            NamePiece = "subject",
            ReadingPiece = "within",
        });
    }

    /// <summary>An earthquake's item: its plate where lit, the dot, "M4.8" and where, and under them how far and when.</summary>
    private void Row(ID2D1DeviceContext* dc, D2D_RECT_F at, in TileStyle style, QuickTheme theme, QuakeReading reading, QuakeRow row, int target)
    {
        if (hot == target)
        {
            var plate = new D2D_RECT_F(at.left + style.Px(4), at.top + style.Px(1), at.right - style.Px(4), at.bottom - style.Px(1));
            PopupPaint.Fill(dc, plate, style.Px(4), press.On(target) ? theme.SubtlePressed : theme.SubtleHover);
        }
        var laid = LineRow.Draw(new LineSketch(dc, Gfx), at.left, at.right, at.top, style, new RowSpec(row.Quake.Place.Length > 0 ? LineRow.Join(row.Magnitude, row.Quake.Place) : row.Magnitude)
        {
            NameInk = reading.Stale ? style.Theme.TextSecondary : null,
            Caption = row.Under,
        }, at.bottom - at.top);
        Dot(dc, style, row, reading.Stale, at.left, laid.Middle);
    }
}

/// <summary>What an earthquake line draws at its height (<see cref="QuakesLine.Plan"/>): one row, or a box per row that fits, under the head where it fits too.</summary>
internal readonly record struct QuakesPlan(bool One, IReadOnlyList<D2D_RECT_F> Rows)
{
    /// <summary>Whether the head stands over the rows: from three rows, not on two.</summary>
    public bool Headed { get; init; } = true;
}
