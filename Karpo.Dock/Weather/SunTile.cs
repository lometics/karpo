// The sun as a line of a list (docs\widgets.md §368), Themia's Sun in Karpo's list: one widget of two
// styles (WidgetStyles.Sun), each a form of its own — the arc (the sun's real path across the sky, flat in
// winter, tall in summer, the elapsed daylight filled behind the sun and the night under the horizon
// dashed) and the day bar (always the clock's 24 hours, midnight to midnight, §657; lit wherever the sun is
// up in them, a tick at each sunrise and sunset, the sun's dot at now). Worked out on this PC (Sun.cs): nothing is fetched, and nothing
// ticks but the dock's minute (MinuteChanged), which moves the sun and the countdown.
//
// Each style at its size (§432, §603):
//   the arc      the picture over the strip — the sunrise, the daylight and the sunset, each under the point of the
//                curve it names, its glyph where a column holds it — the place in its top corner; the same at both
//                sizes, the picture shorter at Compact (§603: no row of its own, no countdown: the next sunrise or sunset
//                and how soon are the sunrise and sunset widgets)
//   the day bar  Compact the next event's glyph, "Sunset in 3 h 10 min" (or "Sunset" where that does not fit), the bar
//                small beside its time ([S-170]); Full the bar over the strip, how soon under the next event's time
//
// **Each time a widget of its own** (§404; the person, 2026-10-29: "the daylight, sunset, solar noon, sunrise,
// etc. metrics should actually be individual widgets"). The rows that stood under the picture, and the times
// style that was those rows alone, are six widgets, each one row of the kit (§401) — its glyph in the leading
// column, its name where every name begins, its note as a caption under it (at the row's end, before the time,
// where it is one row: the kit's detail, §497), its time in the Body at the
// trailing gutter — and Automatic where it starts, as tall as that row:
//   sunrise, sunset  the next one and how soon ("in 15 h 20 min"), whichever comes first
//   daylight         today's, and while the sun is up how much is left ("1 h 23 min left"), else its change
//                    since yesterday in words ("3 min 34 s shorter than yesterday")
//   solar noon       its time and how high the sun stands then
//   dawn, dusk       civil twilight's
// Every line and face of one place reads one reckoning of its day a minute (SunDays). Nothing on it is pressed.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Graphs;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>Which of the sun widget's styles a line is drawn in.</summary>
internal enum SunLook
{
    Arc,
    DayBar,
}

/// <summary>One of the sun's times, a widget of its own (§404).</summary>
internal enum SunRow
{
    Sunrise,
    Sunset,
    Daylight,
    Noon,
    Dawn,
    Dusk,
}

/// <summary>
/// What a sun line draws at its size (<see cref="SunTile.Plan"/>): one row (the day bar's Compact), or the picture (how
/// tall, from the top) over the strip, which ends the line, its side columns saying how soon under the times or not.
/// </summary>
internal readonly record struct SunPlan(bool One, float GraphicHeight, float StripHeight, bool StripCountdown);

/// <summary>The sun for a place, as a line of a list: its picture, or one of its times (§404).</summary>
internal sealed unsafe class SunTile : CellIsland, IListContent
{
    private WeatherOptions options = new();

    public SunTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    /// <summary>Tests only: the zone the day bar's day and the times are read in; null for this PC's.</summary>
    internal TimeZoneInfo? Zone { get; set; }

    /// <summary>Tests only: where the line is, whatever its place says, so a picture of one following this PC asks nobody.</summary>
    internal (double Latitude, double Longitude)? Stand { get; set; } = Samples.On ? Samples.Zurich : null;

    /// <summary>The style its picture is drawn in: its form's.</summary>
    internal SunLook Look => Info == WidgetInfos.SunDayBar ? SunLook.DayBar : SunLook.Arc;

    /// <summary>§404: the time it is, for a time's widget; null for the picture.</summary>
    internal SunRow? Time => Times.TryGetValue(Info, out var time) ? time : null;

    /// <summary>The times' widgets and what each says.</summary>
    private static readonly Dictionary<WidgetInfo, SunRow> Times = new()
    {
        [WidgetInfos.Sunrise] = SunRow.Sunrise,
        [WidgetInfos.Sunset] = SunRow.Sunset,
        [WidgetInfos.Daylight] = SunRow.Daylight,
        [WidgetInfos.SolarNoon] = SunRow.Noon,
        [WidgetInfos.Dawn] = SunRow.Dawn,
        [WidgetInfos.Dusk] = SunRow.Dusk,
    };

    /// <summary>The place it shows.</summary>
    internal WeatherOptions Options => options;

    /// <summary>Nothing ticks for it but the dock's minute.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        options = WeatherPlaces.Settled(island.Weather);
    }

    /// <summary>Nothing set, or this PC's location off with no place kept beside it: "Set up" at the place.</summary>
    internal override SetupNeed? Unset =>
        !options.IsSet ? base.Unset
        : Stand is null && SunPlaces.Off(options) ? SetupNeed.Of(Key, SetupField.Place)
        : null;

    public override string? Tooltip(int index) => null;

    public override IslandAction Click(int index) => default;

    /// <summary>A new minute: the sun, the day bar's dot and the countdown move on.</summary>
    public override bool MinuteChanged(Gfx gfx) => Redraw(gfx);

    protected override bool Sample(bool everything) => false;

    public override void Dispose()
    {
        SunPlaces.Release(this);
        base.Dispose();
    }

    /// <summary>
    /// What it says now, through Clock.Now so a test can hold the minute still: the place's day as every line and
    /// face of that place reads it this minute (SunDays, §404); null while its place is not known.
    /// </summary>
    internal SunDay? Read() =>
        (Stand ?? SunPlaces.Where(options, this)) is var (latitude, longitude)
            ? SunDays.Of(new DateTimeOffset(Clock.Now()), latitude, longitude, Zone ?? TimeZoneInfo.Local)
            : null;

    /// <summary>Never drawn on a bar: on the bar it is the sun face.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- The plan ---------------------------------------------------------------------------------

    /// <summary>§603: the arc's picture at Compact, DIP, over its strip ([S-170]'s 46 and air for the sun's dot).</summary>
    private const float CompactArcDip = 56;

    /// <summary>§603: the arc's picture at Full, DIP.</summary>
    private const float FullArcDip = 96;

    /// <summary>The day bar's picture at Full, DIP: the bar with the place over it.</summary>
    private const float DayBarDip = 64;

    /// <summary>The strip: the list's text with a little air over it, and a small line under it for how soon.</summary>
    private static float StripPx(in ListForm form, in TileStyle style, bool countdown) => style.Px(4) + form.Line + (countdown ? form.SmallLine : 0);

    /// <summary>
    /// The pieces the picture draws at its size (see the top of the file), the picture taking what the strip leaves of
    /// <paramref name="box"/>: the day bar's Compact its one row; the arc its picture over the strip of the times alone,
    /// at both sizes; the day bar's Full its bar over the strip with how soon under the next event's time.
    /// </summary>
    internal SunPlan Plan(D2D_RECT_F box, in TileStyle style)
    {
        if (Look == SunLook.DayBar && LineSizes.Of(Settings) == LineSize.Compact)
        {
            return new SunPlan(One: true, 0, 0, StripCountdown: false);
        }
        var countdown = Look == SunLook.DayBar;
        var strip = StripPx(ListForm.Of(Gfx, style), style, countdown);
        return new SunPlan(One: false, TilePaint.Height(box) - strip, strip, countdown);
    }

    /// <summary>
    /// list: its size's form, whatever the minute (§432, §603): a time as tall as its row (§404), 60 DIP with its note
    /// (Full, where it has one) and 40 without (Compact; or the one row while its place is not known); the day bar its
    /// next event on a row (Compact) or its bar with the place over it and the times with how soon under them (Full); the
    /// arc its picture over the strip of its times, shorter at Compact.
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        var compact = LineSizes.Of(Settings) == LineSize.Compact;
        if (Time is { } time)
        {
            return compact ? LineRow.Height(style, caption: false)
                : Read() is { } day ? LineRow.Height(style, caption: Parts(day, time).Note is not null)
                : LineRow.Height(style, caption: false);
        }
        var form = ListForm.Of(Gfx, style);
        return Look == SunLook.DayBar
            ? compact ? LineRow.Height(style, caption: false) : style.Px(DayBarDip) + StripPx(form, style, countdown: true)
            : style.Px(compact ? CompactArcDip : FullArcDip) + StripPx(form, style, countdown: false);
    }

    // --- The drawing ------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var day = Read();
        var form = ListForm.Of(Gfx, style);
        dc->PushAxisAlignedClip(&box, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            if (day is null)
            {
                Waiting(dc, box, style, form);
                return;
            }
            if (Time is { } time)
            {
                RowOf(new LineSketch(dc, Gfx), box, style, day, time);
                return;
            }
            var plan = Plan(box, style);
            if (plan.One)
            {
                One(dc, box, style, form, day);
                return;
            }
            var room = PopupPaint.Rect(box.left, box.top, TilePaint.Width(box), plan.GraphicHeight);
            if (Look == SunLook.DayBar)
            {
                Bar(dc, room, style, form, day);
            }
            else
            {
                Arc(dc, room, style, form, day);
            }
            Place(dc, room, style, form);
            Strip(dc, box, style, form, day, box.top + plan.GraphicHeight, plan.StripCountdown, plan.GraphicHeight);
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>The place not known yet, or Windows giving none: the kit's one Status, the pin and why.</summary>
    private void Waiting(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in ListForm form) =>
        LineStatus.Draw(new LineSketch(dc, Gfx), box, style, OptionGlyphs.Place, SunPlaces.Off(options) ? S.WidgetWeatherLocationOff : S.DockSunLocating, []);

    /// <summary>The day bar's one row (Compact): the kit's row — the next event's glyph and name with how soon, the bar small beside its time at the end.</summary>
    private void One(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in ListForm form, SunDay day)
    {
        var valuePx = ListTiers.ValuePx(style);
        string glyph;
        string[] names;
        string value;
        if (day.Next is { } next)
        {
            glyph = SunText.Glyph(next.Kind);
            names = [SunText.NamedIn(next.Kind, next.At - day.Now), SunText.Word(next.Kind)];
            value = SunText.Time(next.At);
        }
        else
        {
            var up = day.Times.Polar == PolarState.Day;
            glyph = up ? SunText.Sun : SunText.Moon;
            names = [up ? S.DockSunPolarDay : S.DockSunPolarNight];
            value = SunText.Daylight(day.Times);
        }
        var valueWidth = PopupPaint.Measure(Gfx, value, valuePx, Weight.Semibold).X;
        // The picture is never dropped (Themia's [S-170]): it keeps its room beside the time, and the name takes what is left.
        var picture = ListTiers.LeastPicture(style) * 1.5f;
        var gap = style.Px(LineSpace.GapDip);
        var trailing = picture + gap;
        var room = TilePaint.Width(box) - form.TextLeft - form.Inset - valueWidth - gap - trailing;
        var name = ListTiers.Name(Gfx, names, form.TextPx, Weight.Regular, room) ?? "";
        var row = LineRow.Draw(new LineSketch(dc, Gfx), box.left, box.right, box.top, style, new RowSpec(name) { Glyph = glyph, Reading = value },
            MathF.Min(TilePaint.Height(box), LineRow.Height(style, caption: false)));
        // The picture small before the time, the time on the trailing gutter as every row's reading.
        var tall = MathF.Min(TilePaint.Height(box) - style.Px(8), style.Px(28));
        var end = (row.Reading?.left ?? box.right - form.Inset) - gap;
        var room2 = new D2D_RECT_F(MathF.Max(end - picture, (row.Name?.right ?? box.left + form.TextLeft) + gap), row.Middle - (tall / 2), end, row.Middle + (tall / 2));
        if (room2.right - room2.left >= style.Px(16))
        {
            BarIn(dc, room2, style, day, small: true);
        }
    }

    /// <summary>The place's name in the picture's top corner, quiet: at every size the picture is drawn at (§603).</summary>
    private void Place(ID2D1DeviceContext* dc, D2D_RECT_F room, in TileStyle style, in ListForm form)
    {
        if (PlaceNames.Of(options) is not { Length: > 0 } named)
        {
            return;
        }
        // Up to the arc's left foot's column: the curve rises from 13 % across, the name keeps under a quarter.
        var width = (TilePaint.Width(room) - (2 * form.Inset)) * 0.3f;
        if (ListTiers.Name(Gfx, [named], form.SmallPx, Weight.Regular, width) is { } shown)
        {
            TilePaint.Line(dc, Gfx, shown, form.SmallPx, Weight.Regular, style.Theme.TextSecondary, room.left + form.Inset, room.top + style.Px(6), width);
        }
    }

    /// <summary>The arc in the picture's room, inset to the list's gutters, the horizon from gutter to gutter.</summary>
    private void Arc(ID2D1DeviceContext* dc, D2D_RECT_F room, in TileStyle style, in ListForm form, SunDay day)
    {
        var line = MathF.Max(1, style.Px(1));
        var plot = PopupPaint.Rect(room.left + form.Inset, room.top, TilePaint.Width(room) - (2 * form.Inset), TilePaint.Height(room));
        var horizon = ArcIn(dc, plot, style, day);
        PopupPaint.Fill(dc, PopupPaint.Rect(plot.left, PopupPaint.Round(horizon), TilePaint.Width(plot), line), 0, PopupPaint.Ink(style.Theme.Divider));
    }

    /// <summary>
    /// The sun's real path in <paramref name="plot"/> (Themia's SunPathPlot, [S-167], [S-168]): the curve
    /// between 13 % and 87 % across, the day's above the horizon to the highest the sun gets here all year,
    /// the night's under it squeezed into a sixth of the room; what the sun has travelled today in the
    /// accent with its daylight filled behind it, the rest in the quiet ink, the night dashed; the sun's
    /// dot, dimmed while it is down. The horizon's y, which the caller rules.
    /// </summary>
    private static float ArcIn(ID2D1DeviceContext* dc, D2D_RECT_F plot, in TileStyle style, SunDay day)
    {
        var theme = style.Theme;
        var width = TilePaint.Width(plot);
        var height = TilePaint.Height(plot);
        var below = height * 0.16f;
        var horizon = plot.bottom - below;
        var top = plot.top + (height * 0.12f);
        var x0 = plot.left + (width * 0.13f);
        var x1 = plot.left + (width * 0.87f);
        var declination = day.Sun.DeclinationDegrees;
        var latitude = day.Latitude;
        var axis = SkyPath.YearHighest(latitude);
        var depth = Math.Max(1, -SkyPath.AltitudeAtHourAngle(180, declination, latitude));
        Vector2 At(SkySample sample)
        {
            var x = x0 + ((x1 - x0) * (float)Math.Clamp(sample.Progress, 0, 1));
            return sample.AltitudeDegrees >= 0
                ? new Vector2(x, horizon - ((float)Math.Clamp(sample.AltitudeDegrees / axis, 0, 1) * (horizon - top)))
                : new Vector2(x, horizon + ((float)Math.Clamp(-sample.AltitudeDegrees / depth, 0, 1) * below));
        }
        var arc = SkyPath.SemiDiurnalArc(declination, latitude);
        var accent = theme.HighContrast ? theme.Text : PopupPaint.Ink(theme.Accent);
        var stroke = MathF.Max(1, style.Px(1));
        const int steps = 48;
        if (arc > 0)
        {
            SunInk.Path(dc, [.. SkyPath.Samples(declination, latitude, false, steps).Select(At)], theme.TextTertiary, stroke, dashed: false);
        }
        if (arc < 180)
        {
            SunInk.Path(dc, [.. SkyPath.Samples(declination, latitude, true, steps).Select(At)], theme.TextDisabled, stroke, dashed: true);
        }
        var position = day.Position;
        if (position.Up && arc > 0)
        {
            var done = SkyPath.Samples(declination, latitude, false, Math.Max(2, (int)(steps * position.Progress)), 0, position.Progress).Select(At).ToArray();
            if (done.Length >= 2)
            {
                SunInk.Area(dc, done, accent, plot.top, horizon);
                SunInk.Path(dc, done, accent, MathF.Max(1.5f, style.Px(2)), dashed: false);
            }
        }
        var centre = At(new SkySample(position.Progress, position.AltitudeDegrees));
        var radius = MathF.Min(width, height) >= style.Px(160) ? style.Px(6) : MathF.Min(width, height) >= style.Px(96) ? style.Px(5) : style.Px(4);
        PopupPaint.Circle(dc, centre, radius + MathF.Max(1, style.Px(1.5f)), PopupPaint.Ink(theme.Base));
        PopupPaint.Circle(dc, centre, radius, position.Up ? accent : IslandInk.Faded(accent, 0.45f));
        return horizon;
    }

    /// <summary>The day bar in the picture's room, inset to the list's gutters, at the room's foot (Themia's [S-6046]).</summary>
    private void Bar(ID2D1DeviceContext* dc, D2D_RECT_F room, in TileStyle style, in ListForm form, SunDay day) =>
        BarIn(dc, PopupPaint.Rect(room.left + form.Inset, room.top, TilePaint.Width(room) - (2 * form.Inset), TilePaint.Height(room)), style, day, small: false);

    /// <summary>
    /// The local day as a rounded bar in the track's ink, thicker on a taller room (8 to 20 DIP); its
    /// daylight in the accent's lighter step between the sunrise and sunset ticks, which stand a little
    /// over and under it; the sun's dot at now, ringed in the ground so it reads on the daylight, dimmed while it is down.
    /// </summary>
    private static void BarIn(ID2D1DeviceContext* dc, D2D_RECT_F room, in TileStyle style, SunDay day, bool small)
    {
        var theme = style.Theme;
        var plan = day.Bar;
        var height = TilePaint.Height(room);
        var band = small ? MathF.Max(2, style.Px(6)) : Math.Clamp(MathF.Floor(height * 0.3f), style.Px(8), style.Px(20));
        var marker = Marker(room, style, small);
        var top = small ? room.top + PopupPaint.Round((height - band) / 2) : MathF.Max(room.top + style.Px(4), MathF.Floor(room.bottom - band - style.Px(8)));
        var (left, span) = BarSpan(room, style, small);
        var accent = theme.HighContrast ? theme.Text : PopupPaint.Ink(theme.Accent);
        // The track from gutter to gutter, its round ends past the day's ends by the dot's ring.
        PopupPaint.Fill(dc, PopupPaint.Rect(room.left, top, TilePaint.Width(room), band), band / 2, TilePaint.Track(style));
        foreach (var (from, to) in plan.Lit)
        {
            if (to > from)
            {
                var lit = (float)(to - from) * span;
                PopupPaint.Fill(dc, PopupPaint.Rect(left + ((float)from * span), top, lit, band), MathF.Min(band / 2, lit / 2), IslandInk.Faded(accent, 0.5f));
            }
        }
        var tick = MathF.Max(1, style.Px(1));
        // A tick at each sunrise and sunset on the bar, standing a little over and under it; none on the small bar.
        for (var index = 0; !small && index < plan.Marks.Count; index++)
        {
            var x = PopupPaint.Round(left + ((float)plan.Marks[index].Share * span));
            PopupPaint.Fill(dc, PopupPaint.Rect(x, top - style.Px(4), tick, band + style.Px(8)), 0, theme.TextTertiary);
        }
        var centre = new Vector2(left + ((float)plan.Now * span), top + (band / 2));
        var radius = marker - MathF.Max(1, style.Px(1));
        PopupPaint.Circle(dc, centre, marker + MathF.Max(1, style.Px(1)), PopupPaint.Ink(theme.Base));
        PopupPaint.Circle(dc, centre, radius, plan.NowLit ? accent : IslandInk.Faded(accent, 0.45f));
    }

    /// <summary>
    /// The strip under the picture: the sunrise, the daylight and the sunset, each centred under what it
    /// names — under the arc the ends of the curve, at 13 % and 87 % of the room inside the gutters, under
    /// the day bar its ticks — and kept inside the gutters; each time with its glyph where a side column
    /// (26 % of the room) holds both, how soon under the next one where the plan says, the daylight only
    /// where it stands clear of the times either side of it ([S-649]).
    /// </summary>
    private void Strip(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in ListForm form, SunDay day, float top, bool countdown, float graphic)
    {
        var theme = style.Theme;
        var innerLeft = box.left + form.Inset;
        var inner = TilePaint.Width(box) - (2 * form.Inset);
        var innerRight = innerLeft + inner;
        var side = inner * 0.26f;
        var y = top + style.Px(4);
        var glyph = form.Glyph;
        var rise = innerLeft + (inner * 0.13f);
        var set = innerLeft + (inner * 0.87f);
        var (riseAt, setAt) = (day.Times.Sunrise, day.Times.Sunset);
        var daylight = (rise + set) / 2;
        if (Look == SunLook.DayBar)
        {
            // §657: under the bar's own sunrise and sunset ticks, their times; the daylight under its longest lit stretch.
            var (left, span) = BarSpan(PopupPaint.Rect(innerLeft, 0, inner, graphic), style, small: false);
            float Across(double share) => left + ((float)share * span);
            var bar = day.Bar;
            (rise, riseAt) = bar.First(SunEventKind.Sunrise) is { } r ? (Across(r.Share), r.At) : (rise, riseAt);
            (set, setAt) = bar.First(SunEventKind.Sunset) is { } s ? (Across(s.Share), s.At) : (set, setAt);
            daylight = bar.Lit.Count > 0 && bar.Lit.MaxBy(stretch => stretch.To - stretch.From) is var (from, to) ? Across((from + to) / 2) : (rise + set) / 2;
        }
        string? Soon(SunEventKind kind) => countdown && day.Next?.Kind == kind ? SunText.In(day.Until ?? TimeSpan.Zero) : null;
        var labels = new (float Centre, float Room, string Text, string Glyph, string? Under)[]
        {
            (rise, side, SunText.Time(riseAt), SunText.Sunrise, Soon(SunEventKind.Sunrise)),
            (set, side, SunText.Time(setAt), SunText.Sunset, Soon(SunEventKind.Sunset)),
            (daylight, inner * 0.48f, SunText.Daylight(day.Times), SunText.Clock, null),
        };
        var taken = new List<(float Left, float Right)>();
        foreach (var (centre, room, text, mark, under) in labels)
        {
            var textWidth = PopupPaint.Measure(Gfx, text, form.TextPx, Weight.Semibold).X;
            var withGlyph = textWidth + glyph + style.Px(4) <= room - style.Px(4);
            var whole = textWidth + (withGlyph ? glyph + style.Px(4) : 0);
            var x = PopupPaint.Round(Math.Clamp(centre - (whole / 2), innerLeft, MathF.Max(innerLeft, innerRight - whole)));
            // The daylight, drawn last, only clear of the times by a gap either side.
            var gap = style.Gap;
            if (taken.Any(other => x < other.Right + gap && x + whole > other.Left - gap))
            {
                continue;
            }
            taken.Add((x, x + whole));
            if (withGlyph)
            {
                PopupPaint.Glyph(dc, Gfx, mark, glyph, theme.TextSecondary, PopupPaint.Rect(x, y + PopupPaint.Round((form.Line - glyph) / 2), glyph, glyph));
                x += glyph + style.Px(4);
            }
            TilePaint.Line(dc, Gfx, text, form.TextPx, Weight.Semibold, theme.Text, x, y, MathF.Max(1, textWidth + 1));
            if (under is not null && PopupPaint.Measure(Gfx, under, form.SmallPx, Weight.Regular).X is var underWidth && underWidth <= room)
            {
                var underLeft = Math.Clamp(centre - (underWidth / 2), innerLeft, MathF.Max(innerLeft, innerRight - underWidth));
                TilePaint.Line(dc, Gfx, under, form.SmallPx, Weight.Regular, theme.TextSecondary, underLeft, y + form.Line, underWidth + 1);
            }
        }
    }

    /// <summary>Where the day bar runs in <paramref name="room"/>: inset by its dot's ring, so the dot at midnight is not cut.</summary>
    private static (float Left, float Span) BarSpan(D2D_RECT_F room, in TileStyle style, bool small)
    {
        var marker = Marker(room, style, small);
        return (room.left + marker, MathF.Max(1, TilePaint.Width(room) - (2 * marker)));
    }

    /// <summary>The day bar's dot with its ring: 5 DIP on a tall room, 4 on a lower one, 3 small.</summary>
    private static float Marker(D2D_RECT_F room, in TileStyle style, bool small) =>
        (small ? MathF.Max(2, style.Px(3)) : style.Px(TilePaint.Height(room) >= style.Px(96) ? 5 : 4)) + MathF.Max(1, style.Px(1));

    /// <summary>
    /// A time's parts (§404): its glyph, its name, its time or figure, its note — how soon under the next sunrise
    /// and sunset, what is left of the daylight or its change since yesterday, the sun's height under noon — and
    /// whether it is the next event, whose glyph is the text's ink.
    /// </summary>
    private static (string Glyph, string Name, string Value, string? Note, bool Next) Parts(SunDay day, SunRow kind) => kind switch
    {
        SunRow.Sunrise => Event(day, SunEventKind.Sunrise, S.DockSunSunrise),
        SunRow.Sunset => Event(day, SunEventKind.Sunset, S.DockSunSunset),
        SunRow.Daylight => (SunText.Clock, S.DockSunDaylight, SunText.Daylight(day.Times), SunText.DaylightNote(day), false),
        SunRow.Noon => (SunText.Sun, S.DockSunSolarNoon, SunText.Time(day.Noon.At), SunText.Degrees(day.Noon.AltitudeDegrees), false),
        SunRow.Dawn => (Dawn, S.DockSunDawn, SunText.Time(day.Twilight.Sunrise), null, false),
        _ => (SunText.Moon, S.DockSunDusk, SunText.Time(day.Twilight.Sunset), null, false),
    };

    /// <summary>The next sunrise or sunset and how soon; inside the polar circles a dash, and which it is.</summary>
    private static (string Glyph, string Name, string Value, string? Note, bool Next) Event(SunDay day, SunEventKind kind, string name) =>
        day.NextOf(kind) is { } next
            ? (SunText.Glyph(kind), name, SunText.Time(next.At), SunText.In(next.At - day.Now), day.Next?.Kind == kind)
            : (SunText.Glyph(kind), name, SunText.None, day.Times.Polar == PolarState.Day ? S.DockSunPolarDay : S.DockSunPolarNight, false);

    /// <summary>Tests only: what a time's row lays out in <paramref name="box"/>, piece by piece; nothing for the picture or while its place is not known.</summary>
    internal IReadOnlyList<LinePiece> TimePieces(D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(null, Gfx);
        if (Time is { } time && Read() is { } day)
        {
            RowOf(sketch, box, style, day, time);
        }
        return sketch.Pieces;
    }

    /// <summary>A time's one row at the top of <paramref name="box"/>, the kit's (see the top of the file); its note at the row's end where the line is one row of the list (§497).</summary>
    private static void RowOf(LineSketch sketch, D2D_RECT_F box, in TileStyle style, SunDay day, SunRow kind)
    {
        var (glyph, name, value, note, next) = Parts(day, kind);
        // §497: its note is the kit's detail — under the name where the line holds a caption, at the end on one row.
        var row = new RowSpec(name)
        {
            Glyph = glyph,
            GlyphInk = next ? style.Theme.Text : style.Theme.TextSecondary,
            Detail = note,
            Reading = value,
            Look = RowReading.Body,
        };
        _ = LineRow.Draw(sketch, box.left, box.right, box.top, style, row, MathF.Min(TilePaint.Height(box), LineRow.Height(style, row)));
    }

    /// <summary>Civil dawn: the sun half lit, coming.</summary>
    private const string Dawn = "";
}

/// <summary>The sun's lines and fills, through Direct2D's paths.</summary>
internal static unsafe class SunInk
{
    /// <summary>A line through <paramref name="points"/>, round at its joins, dashed or whole.</summary>
    public static void Path(ID2D1DeviceContext* dc, Vector2[] points, DXGI_RGBA color, float width, bool dashed)
    {
        if (points.Length < 2)
        {
            return;
        }
        ID2D1Factory* factory = null;
        ID2D1PathGeometry* path = null;
        ID2D1StrokeStyle* joins = null;
        ID2D1SolidColorBrush* brush = null;
        try
        {
            dc->GetFactory(&factory);
            if (factory == null || factory->CreatePathGeometry(&path).FAILED || !Build(path, points, null))
            {
                return;
            }
            var properties = new D2D1_STROKE_STYLE_PROPERTIES
            {
                startCap = D2D1_CAP_STYLE.D2D1_CAP_STYLE_ROUND,
                endCap = D2D1_CAP_STYLE.D2D1_CAP_STYLE_ROUND,
                dashCap = D2D1_CAP_STYLE.D2D1_CAP_STYLE_FLAT,
                lineJoin = D2D1_LINE_JOIN.D2D1_LINE_JOIN_ROUND,
                miterLimit = 1,
                dashStyle = dashed ? D2D1_DASH_STYLE.D2D1_DASH_STYLE_CUSTOM : D2D1_DASH_STYLE.D2D1_DASH_STYLE_SOLID,
            };
            // Themia's 3 on, 3 off, in the line's widths.
            var dashes = stackalloc float[] { 3, 3 };
            if (factory->CreateStrokeStyle(&properties, dashed ? dashes : null, dashed ? 2u : 0u, &joins).FAILED)
            {
                return;
            }
            brush = Gfx.Brush(dc, color);
            dc->DrawGeometry((ID2D1Geometry*)path, (ID2D1Brush*)brush, width, joins);
        }
        finally
        {
            ComUtil.Release(ref brush);
            ComUtil.Release(ref joins);
            ComUtil.Release(ref path);
            ComUtil.Release(ref factory);
        }
    }

    /// <summary>The run closed down to <paramref name="foot"/>, filled with the accent's fading fill from <paramref name="top"/>, as every graph's.</summary>
    public static void Area(ID2D1DeviceContext* dc, Vector2[] points, DXGI_RGBA accent, float top, float foot)
    {
        ID2D1Factory* factory = null;
        ID2D1PathGeometry* path = null;
        try
        {
            dc->GetFactory(&factory);
            if (factory == null || factory->CreatePathGeometry(&path).FAILED || !Build(path, points, foot))
            {
                return;
            }
            GraphBackdrop.Fill(dc, (ID2D1Geometry*)path, accent, top, foot);
        }
        finally
        {
            ComUtil.Release(ref path);
            ComUtil.Release(ref factory);
        }
    }

    /// <summary>The figure through the points: open, or closed down to <paramref name="foot"/> and filled.</summary>
    private static bool Build(ID2D1PathGeometry* path, Vector2[] points, float? foot)
    {
        ID2D1GeometrySink* into = null;
        if (path->Open(&into).FAILED)
        {
            return false;
        }
        try
        {
            var first = new D2D_POINT_2F(points[0].X, points[0].Y);
            if (foot is { } bottom)
            {
                into->BeginFigure(new D2D_POINT_2F(first.x, bottom), D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_FILLED);
                into->AddLine(first);
            }
            else
            {
                into->BeginFigure(first, D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_HOLLOW);
            }
            for (var index = 1; index < points.Length; index++)
            {
                into->AddLine(new D2D_POINT_2F(points[index].X, points[index].Y));
            }
            if (foot is { } end)
            {
                into->AddLine(new D2D_POINT_2F(points[^1].X, end));
            }
            into->EndFigure(foot is null ? D2D1_FIGURE_END.D2D1_FIGURE_END_OPEN : D2D1_FIGURE_END.D2D1_FIGURE_END_CLOSED);
            return into->Close().SUCCEEDED;
        }
        finally
        {
            ComUtil.Release(ref into);
        }
    }
}
