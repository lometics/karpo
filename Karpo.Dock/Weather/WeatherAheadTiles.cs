// The hourly and the daily forecast as lines of a list (docs\widgets.md §80, §590): two widgets of a style each, every
// form the same data at both of its sizes (§590: the person, 2026-11-09, "these widgets should be broken down into
// smaller ones that always show the same amount of data ... bring back the graph version for the todays weather and
// also bring a graph version for a multi day forecast one as well"):
//
//   Hourly · Graph  today's curve, midnight to midnight, the hours' times, glyphs and temperatures over it, now marked
//                   (WeatherHoursTile, the weather flyout's day graph, Graphs\DayGraph.cs; §572 no night shaded):
//                   Compact on the flyout's 64-DIP band, Full on a band half as tall again
//   Hourly · List   the next five hours across, past midnight where it comes, each its time, glyph and temperature
//                   (WeatherHoursListTile): Compact the glyph beside the temperature, Full the glyph large over it
//   Daily · List    the seven days, each its name, glyph, chance of rain, low and high (WeatherDaysTile): Compact a
//                   column a day across the line, Full a row a day with its range on the scale the week shares
//   Daily · Graph   the seven days a column each, its name and glyph over the week's highs and lows on two curves, each
//                   figure at its point, the chance of rain under them (WeatherDaysGraphTile): the curves' band 64 DIP
//                   at Compact, 96 at Full
//
// A line's height is its size's form, whatever the forecast says: nothing is dropped where the line is short and
// nothing is added where it is tall, since a line is never other than its size.
//
// Each day and each hour is a target of its own (docs\widgets.md §131): it lights as a list item does, says itself in a
// tooltip and searches the weather — on that day for a day. Before there is a forecast each says, in a line, what the
// current weather says at length: that the place is to be set, that the forecast is on its way, or why it is not there.

using Karpo.Core;
using Karpo.Dock.Graphs;
using Karpo.Dock.Panels;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.Weather;

/// <summary>The hourly forecast's Graph: the weather flyout's day graph, as a line of a list, its band by the line's size.</summary>
internal sealed unsafe class WeatherHoursTile : WeatherLineTile, IListContent
{
    // The graph is shared with the stock widget (Graphs\DayGraph.cs): the weather fills it with
    // hours and glyphs.
    private readonly DayGraph hours = new() { GlyphInk = WeatherCodes.Ink };

    public WeatherHoursTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    /// <summary>
    /// Hour <paramref name="target"/> (from 1): the weather at the place searched for. The graph is
    /// today's, and the plain search is today's, hour by hour, in every engine; so every hour asks it.
    /// </summary>
    protected override void Act(int target)
    {
        if (target >= 1 && target <= Read().Hours.Count)
        {
            SearchTheWeb();
        }
    }

    /// <summary>The hours, across the day.</summary>
    protected override IReadOnlyList<int> KeyOrder() =>
        Read() is { State: WeatherState.Ready } reading ? [.. Enumerable.Range(1, reading.Hours.Count)] : [];

    /// <summary>Across the graph: Left and Right.</summary>
    protected override (int Back, int On) WalkKeys => (TerraFX.Interop.Windows.VK.VK_LEFT, TerraFX.Interop.Windows.VK.VK_RIGHT);

    /// <summary>§590: the curve's band at the line's size: the flyout's at Compact, half as tall again at Full.</summary>
    internal float Band => LineSizes.Of(Settings) == LineSize.Compact ? DayGraph.BandDip : DayGraph.TallBandDip;

    /// <summary>The graph at its size and the flyout's air over it, whatever the forecast (§590).</summary>
    public float ListHeight(float width, in TileStyle style)
    {
        var context = Context(style);
        return context.D(4) + DayGraph.Measure(context, 1, glyphs: true, Band);
    }

    /// <summary>The graph filled with <paramref name="reading"/> and placed in <paramref name="box"/>, as the paint and every hit test place it; false without hours to show.</summary>
    private bool Fill(WeatherReading reading, D2D_RECT_F box, in TileStyle style, QuickContext context)
    {
        if (reading.State != WeatherState.Ready || reading.Hours.Count == 0)
        {
            return false;
        }
        hours.Band = Band;
        hours.Points = [.. reading.Hours.Select(hour => new GraphPoint(hour.Hour, hour.Time, hour.Glyph, hour.Temperature, hour.Chance, hour.Value))];
        hours.NowAt = reading.NowAt;
        hours.NowValue = reading.NowValue;
        // §632: the line's whole width, an hour's slice each, what it writes inside the list's gutter.
        hours.Gutter = ListForm.Gutter(style);
        hours.Bounds = PopupPaint.Rect(0, context.D(4), TilePaint.Width(box), DayGraph.Measure(context, 1, glyphs: true, Band));
        return true;
    }

    /// <summary>
    /// §611, §632: the hours' bands: the line's width in the graph's slices (<see cref="DayGraph.Slices"/>), each hour its
    /// own, top to foot, as wide as every other, its point in its middle; the first from the line's leading edge, the last
    /// to its trailing one.
    /// </summary>
    protected override List<D2D_RECT_F> Bands(D2D_RECT_F box, in TileStyle style)
    {
        if (!Fill(Read(), box, style, Context(style)))
        {
            return [];
        }
        var slices = LineItem.Across(LineAround(box, style), hours.Slices);
        return [.. Enumerable.Range(0, hours.Points.Count).Select(index => slices[hours.SliceOf(index)])];
    }

    /// <summary>The hour in words: its time, its temperature, its condition and its chance of rain.</summary>
    protected override string Said(int target) => Read().Hours is var all && target >= 1 && target <= all.Count ? WeatherColumns.Said(all[target - 1]) : "";

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Take();
        var reading = Read();
        var context = Context(style);
        if (!Fill(reading, box, style, context))
        {
            WeatherNote.Paint(dc, Gfx, box, style, reading);
            return;
        }
        // The hour under the pointer or the keys: its band under the graph, a ring on its point.
        var lit = -1;
        for (var index = 0; index < reading.Hours.Count; index++)
        {
            if (Lit(index + 1))
            {
                lit = index;
            }
        }
        var bands = Bands(box, style);
        hours.LitPoint = lit;
        var line = LineAround(box, style);
        dc->PushAxisAlignedClip(&line, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            Light(dc, bands, style, context.Theme);
            var bounds = hours.Bounds;
            TilePaint.Placed(dc, box.left + bounds.left, box.top + bounds.top, 1, () => hours.Paint(dc, context));
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }
}

/// <summary>
/// The hourly forecast's List (§590): the next five hours across the line, a column each — its time, its glyph and its
/// temperature —, the glyph beside the temperature at Compact and large over it at Full.
/// </summary>
internal sealed unsafe class WeatherHoursListTile : WeatherLineTile, IListContent
{
    public WeatherHoursListTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    /// <summary>Hour <paramref name="target"/> (from 1): the weather at the place searched for.</summary>
    protected override void Act(int target)
    {
        if (target >= 1 && target <= Read().Next.Count)
        {
            SearchTheWeb();
        }
    }

    protected override IReadOnlyList<int> KeyOrder() =>
        Read() is { State: WeatherState.Ready } reading ? [.. Enumerable.Range(1, reading.Next.Count)] : [];

    protected override (int Back, int On) WalkKeys => (TerraFX.Interop.Windows.VK.VK_LEFT, TerraFX.Interop.Windows.VK.VK_RIGHT);

    private bool Compact => LineSizes.Of(Settings) == LineSize.Compact;

    /// <summary>A column's three pieces and the air over and under them, at the line's size, whatever the forecast.</summary>
    public float ListHeight(float width, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var hair = style.Px(LineSpace.HairDip);
        var middle = Compact ? MathF.Max(form.Line, LineGlyph.Row(style)) : LineGlyph.Feature(style) + hair + form.Line;
        return form.Pad + form.SmallLine + hair + middle + form.Pad;
    }

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Take();
        var reading = Read();
        if (reading.State != WeatherState.Ready || reading.Next.Count == 0)
        {
            WeatherNote.Paint(dc, Gfx, box, style, reading);
            return;
        }
        var theme = style.Theme;
        var form = ListForm.Of(Gfx, style);
        var context = Context(style);
        var hair = style.Px(LineSpace.HairDip);
        var columns = WeatherColumns.Of(box, reading.Next.Count);
        var line = LineAround(box, style);
        dc->PushAxisAlignedClip(&line, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            Light(dc, Bands(box, style), style, context.Theme);
            for (var index = 0; index < reading.Next.Count; index++)
            {
                var hour = reading.Next[index];
                var at = columns[index];
                var y = at.top + form.Pad;
                WeatherColumns.Centred(dc, Gfx, hour.Time, form.SmallPx, Weight.Regular, theme.TextSecondary, at, y);
                y += form.SmallLine + hair;
                var ink = WeatherCodes.Ink(hour.Glyph, theme);
                if (Compact)
                {
                    // The glyph and the temperature side by side, centred as one in the column.
                    var glyph = LineGlyph.Row(style);
                    var gap = style.Px(LineSpace.HairDip) * 2;
                    var figure = MathF.Min(PopupPaint.Measure(Gfx, hour.Temperature, form.TextPx, Weight.Semibold).X + 1, at.right - at.left - glyph - gap);
                    var left = at.left + PopupPaint.Round((at.right - at.left - glyph - gap - figure) / 2);
                    var middle = MathF.Max(form.Line, glyph);
                    PopupPaint.Glyph(dc, Gfx, hour.Glyph, glyph, ink, PopupPaint.Rect(left, y + PopupPaint.Round((middle - glyph) / 2), glyph, glyph));
                    PopupPaint.Text(dc, Gfx, hour.Temperature, form.TextPx, Weight.Semibold, theme.Text, left + glyph + gap, y + PopupPaint.Round((middle - form.Line) / 2), figure);
                }
                else
                {
                    var glyph = LineGlyph.Feature(style);
                    PopupPaint.Glyph(dc, Gfx, hour.Glyph, glyph, ink, PopupPaint.Rect(at.left, y, at.right - at.left, glyph));
                    y += glyph + hair;
                    WeatherColumns.Centred(dc, Gfx, hour.Temperature, form.TextPx, Weight.Semibold, theme.Text, at, y);
                }
            }
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>§611, §632: an hour's band its column, the line's whole height, each as wide as the others, the first and the last to its edges.</summary>
    protected override List<D2D_RECT_F> Bands(D2D_RECT_F box, in TileStyle style) =>
        Read() is { State: WeatherState.Ready } reading ? LineItem.Across(LineAround(box, style), reading.Next.Count) : [];

    protected override string Said(int target) => Read().Next is var next && target >= 1 && target <= next.Count ? WeatherColumns.Said(next[target - 1]) : "";
}

/// <summary>
/// The daily forecast's List (§590): the seven days, each its name, glyph, chance of rain, low and high — a column a day
/// across the line at Compact, the weather flyout's row a day with its range on the week's scale at Full.
/// </summary>
internal sealed unsafe class WeatherDaysTile : WeatherLineTile, IListContent
{
    /// <summary>The day rows by their place in the week, kept across redraws as the flyout kept them.</summary>
    private readonly List<WeatherDayRow> days = [];

    public WeatherDaysTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    private bool Compact => LineSizes.Of(Settings) == LineSize.Compact;

    /// <summary>
    /// Day <paramref name="target"/> (from 1): the weather at the place searched for on that day —
    /// "weather Bern Wednesday", by the region's name for it; today's is the plain search, which is
    /// today's anyway.
    /// </summary>
    protected override void Act(int target) => WeatherColumns.SearchDay(Read(), target, SearchTheWeb);

    /// <summary>The days, down the week or across it.</summary>
    protected override IReadOnlyList<int> KeyOrder() =>
        Read() is { State: WeatherState.Ready } reading ? [.. Enumerable.Range(1, reading.Days.Count)] : [];

    protected override (int Back, int On) WalkKeys => Compact
        ? (TerraFX.Interop.Windows.VK.VK_LEFT, TerraFX.Interop.Windows.VK.VK_RIGHT)
        : (TerraFX.Interop.Windows.VK.VK_UP, TerraFX.Interop.Windows.VK.VK_DOWN);

    /// <summary>
    /// §611: the days' bands: at Compact a column's each, the line's whole height; at Full the line's height in a week of
    /// <see cref="WeatherReading.ListDays"/> rows as tall as each other, the first from its top and the last to its foot,
    /// each the line's whole width. Every day the forecast has.
    /// </summary>
    protected override List<D2D_RECT_F> Bands(D2D_RECT_F box, in TileStyle style)
    {
        if (Read() is not { State: WeatherState.Ready } reading)
        {
            return [];
        }
        var line = LineAround(box, style);
        return Compact
            ? LineItem.Across(line, reading.Days.Count)
            : [.. LineItem.Down(line, WeatherReading.ListDays).Take(reading.Days.Count)];
    }

    /// <summary>
    /// Where a day's words stand in its <paramref name="band"/> at Full, as the flyout placed them: the panel's width
    /// housed on the list's gutter, the row half the panel's gutter in, so its own half lands its content on the gutter;
    /// the row's height in the band's middle.
    /// </summary>
    private static D2D_RECT_F RowIn(D2D_RECT_F box, D2D_RECT_F band, in TileStyle style, QuickContext context)
    {
        var panel = ListForm.Housed(ListForm.Gutter(style), context.D(Widget.PanelPadDip), 0, TilePaint.Width(box), 0);
        var rowHeight = WeatherDayRow.RowHeight(context);
        return PopupPaint.Rect(box.left + panel.left + context.D(8), band.top + PopupPaint.Round((band.bottom - band.top - rowHeight) / 2),
            panel.right - panel.left - (2 * context.D(8)), rowHeight);
    }

    /// <summary>The day's name in full with its condition in words, which the row says by a glyph alone.</summary>
    protected override string Said(int target) => Read().Days is var all && target >= 1 && target <= all.Count ? WeatherColumns.Said(all[target - 1]) : "";

    /// <summary>The week at the line's size, whatever the forecast (§590): five lines a column at Compact, the flyout's air over a row a day and under them at Full.</summary>
    public float ListHeight(float width, in TileStyle style)
    {
        if (Compact)
        {
            return WeatherColumns.DayHeight(Gfx, style);
        }
        var context = Context(style);
        return context.D(4) + (WeatherReading.ListDays * WeatherDayRow.RowHeight(context)) + context.D(8);
    }

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Take();
        var reading = Read();
        if (reading.State != WeatherState.Ready || reading.Days.Count == 0)
        {
            WeatherNote.Paint(dc, Gfx, box, style, reading);
            return;
        }
        var context = Context(style);
        var bands = Bands(box, style);
        var columns = Compact ? WeatherColumns.Of(box, reading.Days.Count) : [];
        while (days.Count < reading.Days.Count)
        {
            days.Add(new WeatherDayRow());
        }
        var line = LineAround(box, style);
        dc->PushAxisAlignedClip(&line, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            // Each day's band under it, under the pointer, pressed or with the keys on it.
            Light(dc, bands, style, context.Theme);
            for (var index = 0; index < bands.Count; index++)
            {
                var day = reading.Days[index];
                if (Compact)
                {
                    WeatherColumns.Day(dc, Gfx, style, columns[index], day);
                    continue;
                }
                var at = RowIn(box, bands[index], style, context);
                var row = days[index];
                row.Day = day.Name;
                row.Glyph = day.Glyph;
                row.Chance = day.Chance;
                row.Low = day.Low;
                row.High = day.High;
                row.From = day.From;
                row.To = day.To;
                row.Today = day.Today;
                row.Bounds = PopupPaint.Rect(at.left - box.left, at.top - box.top, at.right - at.left, at.bottom - at.top);
                TilePaint.Placed(dc, at.left, at.top, 1, () => row.Paint(dc, context));
            }
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }
}

/// <summary>
/// The daily forecast's Graph (§590): the seven days a column each — its name and glyph — over the week's highs and lows
/// on two curves on the scale the week shares, each figure at its point, the high over it and the low under it, the
/// chance of rain at the foot. The band 64 DIP at Compact, 96 at Full: the same figures on a flatter or a taller curve.
/// </summary>
internal sealed unsafe class WeatherDaysGraphTile : WeatherLineTile, IListContent
{
    /// <summary>The curves' band at Compact and at Full, DIP: the hourly graph's own two.</summary>
    internal static float BandOf(LineSize size) => size == LineSize.Compact ? DayGraph.BandDip : DayGraph.TallBandDip;

    public WeatherDaysGraphTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    protected override void Act(int target) => WeatherColumns.SearchDay(Read(), target, SearchTheWeb);

    protected override IReadOnlyList<int> KeyOrder() =>
        Read() is { State: WeatherState.Ready } reading ? [.. Enumerable.Range(1, reading.Days.Count)] : [];

    protected override (int Back, int On) WalkKeys => (TerraFX.Interop.Windows.VK.VK_LEFT, TerraFX.Interop.Windows.VK.VK_RIGHT);

    /// <summary>The name, the glyph, the band and the chance's line, with the air over and under them, at the line's size.</summary>
    public float ListHeight(float width, in TileStyle style) => Plan(PopupPaint.Rect(0, 0, width, 0), style).Height;

    /// <summary>Where the graph's rows stand from <paramref name="box"/>'s top, px.</summary>
    internal readonly record struct GraphPlan(float Name, float Glyph, float BandTop, float BandBottom, float Chance, float Height);

    internal GraphPlan Plan(D2D_RECT_F box, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var hair = style.Px(LineSpace.HairDip);
        var name = box.top + form.Pad;
        var glyph = name + form.SmallLine + hair;
        var bandTop = glyph + LineGlyph.Row(style) + hair;
        var bandBottom = bandTop + style.Px(BandOf(LineSizes.Of(Settings)));
        var chance = bandBottom + hair;
        return new GraphPlan(name, glyph, bandTop, bandBottom, chance, chance + form.SmallLine + form.Pad - box.top);
    }

    /// <summary>
    /// §590: where a day's high and low stand in the band, px from its top to its foot: <paramref name="from"/> and
    /// <paramref name="to"/> are the low and the high on the week's scale, 0 to 1 (<see cref="DayRow.From"/>); the
    /// band keeps a figure's line clear over the highest high and under the lowest low, so every figure stays inside.
    /// </summary>
    internal static (float High, float Low) Points(float top, float bottom, float figure, float from, float to)
    {
        var upper = top + figure;
        var lower = bottom - figure;
        var span = MathF.Max(0, lower - upper);
        return (lower - (Math.Clamp(to, 0, 1) * span), lower - (Math.Clamp(from, 0, 1) * span));
    }

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Take();
        var reading = Read();
        if (reading.State != WeatherState.Ready || reading.Days.Count == 0)
        {
            WeatherNote.Paint(dc, Gfx, box, style, reading);
            return;
        }
        var theme = style.Theme;
        var form = ListForm.Of(Gfx, style);
        var context = Context(style);
        var plan = Plan(box, style);
        var columns = WeatherColumns.Of(box, reading.Days.Count);
        var figure = form.SmallLine;
        // A figure stands a dot's radius and a pixel off its point: the band keeps that and a hair clear at its ends.
        var dot = style.Px(3);
        var keep = figure + dot + style.Px(1) + style.Px(LineSpace.HairDip);
        var points = reading.Days.Select(day => Points(plan.BandTop, plan.BandBottom, keep, day.From, day.To)).ToArray();
        var xs = columns.Select(column => (column.left + column.right) / 2).ToArray();
        var quiet = theme.HighContrast ? theme.Text : theme.TextSecondary;
        var accent = theme.HighContrast ? theme.Text : context.Theme.Accent;
        var line = LineAround(box, style);
        dc->PushAxisAlignedClip(&line, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            Light(dc, Bands(box, style), style, context.Theme);
            var stroke = MathF.Max(1.5f, style.Px(2));
            DayGraph.Smooth(dc, [(xs, [.. points.Select(point => point.High)])], stroke, accent);
            DayGraph.Smooth(dc, [(xs, [.. points.Select(point => point.Low)])], stroke, quiet);
            for (var index = 0; index < columns.Count; index++)
            {
                var day = reading.Days[index];
                var at = columns[index];
                WeatherColumns.Centred(dc, Gfx, day.Name, form.SmallPx, day.Today ? Weight.Semibold : Weight.Regular, theme.Text, at, plan.Name);
                var glyph = LineGlyph.Row(style);
                PopupPaint.Glyph(dc, Gfx, day.Glyph, glyph, WeatherCodes.Ink(day.Glyph, theme), PopupPaint.Rect(at.left, plan.Glyph, at.right - at.left, glyph));
                var (high, low) = points[index];
                PopupPaint.Circle(dc, new System.Numerics.Vector2(xs[index], high), dot, accent);
                PopupPaint.Circle(dc, new System.Numerics.Vector2(xs[index], low), dot, quiet);
                // The high over its point, the low under its: each figure in its column, the low in the quiet ink.
                WeatherColumns.Centred(dc, Gfx, day.High, form.SmallPx, Weight.Semibold, theme.Text, at, high - dot - style.Px(1) - figure);
                WeatherColumns.Centred(dc, Gfx, day.Low, form.SmallPx, Weight.Regular, theme.TextSecondary, at, low + dot + style.Px(1));
                WeatherColumns.Centred(dc, Gfx, day.Chance, form.SmallPx, Weight.Regular, quiet, at, plan.Chance);
            }
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>§611, §632: a day's band its column, the line's whole height, each as wide as the others, the first and the last to its edges.</summary>
    protected override List<D2D_RECT_F> Bands(D2D_RECT_F box, in TileStyle style) =>
        Read() is { State: WeatherState.Ready } reading ? LineItem.Across(LineAround(box, style), reading.Days.Count) : [];

    protected override string Said(int target) => Read().Days is var all && target >= 1 && target <= all.Count ? WeatherColumns.Said(all[target - 1]) : "";
}

/// <summary>What the forecast's lines laid out in columns share (§590): the columns, their bands, a column's words, a day's column, what a hover says.</summary>
internal static unsafe class WeatherColumns
{
    /// <summary>
    /// §632: <paramref name="count"/> columns across <paramref name="box"/>, the form's height: each its item's band across
    /// the line (<see cref="LineItem.Across"/>), as wide as every other, where its words stand in its middle.
    /// </summary>
    public static List<D2D_RECT_F> Of(D2D_RECT_F box, int count) =>
        [.. LineItem.Across(box, count).Select(band => band with { top = box.top, bottom = box.bottom })];

    /// <summary>One line of words centred in <paramref name="column"/> from <paramref name="top"/>, cut to its width.</summary>
    public static void Centred(ID2D1DeviceContext* dc, Gfx gfx, string text, float px, Weight weight, DXGI_RGBA ink, D2D_RECT_F column, float top)
    {
        if (text.Length == 0)
        {
            return;
        }
        var room = MathF.Max(1, column.right - column.left);
        var wide = MathF.Min(room, PopupPaint.Measure(gfx, text, px, weight).X + 1);
        PopupPaint.Text(dc, gfx, text, px, weight, ink, column.left + PopupPaint.Round((room - wide) / 2), top, wide);
    }

    /// <summary>A day's column of the daily List at Compact: its name, glyph, high, low and chance of rain, a line each.</summary>
    public static float DayHeight(Gfx gfx, in TileStyle style)
    {
        var form = ListForm.Of(gfx, style);
        var hair = style.Px(LineSpace.HairDip);
        return form.Pad + form.SmallLine + hair + LineGlyph.Row(style) + hair + form.Line + (2 * form.SmallLine) + form.Pad;
    }

    /// <summary>Draws <paramref name="day"/> down <paramref name="column"/> (see <see cref="DayHeight"/>).</summary>
    public static void Day(ID2D1DeviceContext* dc, Gfx gfx, in TileStyle style, D2D_RECT_F column, DayRow day)
    {
        var theme = style.Theme;
        var form = ListForm.Of(gfx, style);
        var hair = style.Px(LineSpace.HairDip);
        var y = column.top + form.Pad;
        Centred(dc, gfx, day.Name, form.SmallPx, day.Today ? Weight.Semibold : Weight.Regular, theme.Text, column, y);
        y += form.SmallLine + hair;
        var glyph = LineGlyph.Row(style);
        PopupPaint.Glyph(dc, gfx, day.Glyph, glyph, WeatherCodes.Ink(day.Glyph, theme), PopupPaint.Rect(column.left, y, column.right - column.left, glyph));
        y += glyph + hair;
        Centred(dc, gfx, day.High, form.TextPx, Weight.Semibold, theme.Text, column, y);
        y += form.Line;
        Centred(dc, gfx, day.Low, form.SmallPx, Weight.Regular, theme.TextSecondary, column, y);
        y += form.SmallLine;
        Centred(dc, gfx, day.Chance, form.SmallPx, Weight.Regular, theme.HighContrast ? theme.Text : theme.TextSecondary, column, y);
    }

    /// <summary>An hour in words: its time, its temperature, its condition and its chance of rain.</summary>
    public static string Said(HourCell hour) =>
        string.Join(" · ", new[] { hour.Time, hour.Temperature, hour.Condition, hour.Chance }.Where(part => part.Length > 0));

    /// <summary>A day in words: its name in full, its condition, its high and low and its chance of rain.</summary>
    public static string Said(DayRow day)
    {
        var name = day.Today || day.On == default ? day.Name : Nls.DayName(day.On.DayOfWeek);
        return string.Join(" · ", new[] { name, day.Condition, LineRow.Join(day.High, day.Low), day.Chance }.Where(part => part.Length > 0));
    }

    /// <summary>Day <paramref name="target"/> (from 1) searched for: on its weekday, today's the plain search.</summary>
    public static void SearchDay(WeatherReading reading, int target, Action<string?> search)
    {
        if (target < 1 || target > reading.Days.Count)
        {
            return;
        }
        var day = reading.Days[target - 1];
        search(day.Today || day.On == default ? null : Nls.DayName(day.On.DayOfWeek));
    }
}

/// <summary>What the forecast's lines say before there is a forecast: one quiet line on the list's gutter.</summary>
internal static unsafe class WeatherNote
{
    public static void Paint(ID2D1DeviceContext* dc, Gfx gfx, D2D_RECT_F box, in TileStyle style, WeatherReading reading)
    {
        var said = reading.State switch
        {
            // Quietly, and short. Not drawn while unset: the line is its centred "Set up in Settings" then (CellIsland.PaintOn).
            WeatherState.NoPlace => WeatherReading.NoPlaceTitle,
            WeatherState.Loading => WeatherReading.LoadingText,
            _ => reading.Problem ?? S.DockWeatherRetryHint,
        };
        var form = ListForm.Of(gfx, style);
        var gutter = ListForm.Gutter(style);
        var room = MathF.Max(1, TilePaint.Width(box) - (2 * gutter));
        var top = LineRows.Top(box.top, form.Line + (2 * form.Pad), form.Line, form.Pad);
        TilePaint.Line(dc, gfx, said, form.TextPx, Weight.Regular, style.Theme.TextSecondary, box.left + gutter, top, room);
    }
}
