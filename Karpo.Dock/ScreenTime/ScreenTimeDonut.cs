// Screen time's ring (docs\widgets.md §141): one day's screen time as a donut split by app, the
// person's words (2026-10-12): "add a screen time donut chart as well, which shows the current day /
// selected day / hovered over day".
//
//   * Which day: the day under the pointer in the folder (Channels.DayHover — a bar of the total's
//     fortnight), else the folder's day (Channels.Day — a step or a click on the total, a month's
//     pick), else today (DayOf). The pointer leaving the bars goes back to the day shown, live.
//   * What it shows: the day's top apps (ScreenTimeRing.MostApps), most used first clockwise from
//     twelve o'clock, then "Other" — smaller apps, an app too thin to see (under LeastShare), and
//     time counted for no app — in the neutral grey; the day's total in the middle with the day's
//     name under it; a legend of the same pieces with their times beside the ring, or under it where
//     the line is too narrow for both and tall enough.
//   * Which colours: the bars' (ScreenTimeChart): an app that is one of the fortnight's top apps
//     wears the colour it wears there, so a folder with both never shows an app in two colours; an
//     app of the day's top that is not takes a colour still free. High contrast: the text's colour,
//     the pieces told apart by their gaps, the one lit in the highlight.
//   * The pointer: a piece (or its legend row) under it lights — drawn a little out from the ring,
//     the rest dimmed — and says its app, time and share in a tooltip; it is the folder's app pointed
//     at (Channels.AppHover), so the apps' row and the bars light it too, and a click picks it
//     (Channels.App) as a click on its row does. The app picked or pointed at elsewhere lights here.
//
// One drawing whatever the height (the line is Fixed, ListWidgets.cs): the ring as large as the line
// holds, up to MostDiameter. A line too short for the least ring (one row: no kind has a fewest,
// §166) has no ring: the total and the day, as the legend's head, alone.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>One piece of the ring: a top app's (its place in <see cref="ScreenTimeRing.Top"/>) or the rest (<see cref="ScreenTimeLine.OtherPart"/>), from <see cref="Start"/> clockwise through <see cref="Sweep"/> degrees, twelve o'clock 0.</summary>
internal sealed record ScreenTimeSlice(int Part, string? App, TimeSpan Time, float Share, float Start, float Sweep);

/// <summary>A day's ring: its total, the apps with pieces of their own, and the pieces round it.</summary>
internal sealed record ScreenTimeRing(DateOnly Day, TimeSpan Total, IReadOnlyList<string> Top, IReadOnlyList<ScreenTimeSlice> Slices)
{
    /// <summary>How many apps have pieces of their own; every other is in "Other".</summary>
    public const int MostApps = 5;

    /// <summary>The least share an app's piece is: under it (7.2°) a piece is a sliver nobody can point at, and is "Other".</summary>
    public const float LeastShare = 0.02f;

    /// <summary>
    /// <paramref name="day"/>'s ring in <paramref name="log"/>: the apps with the most time (a minute
    /// at least, <see cref="ScreenTimeDay.LeastShown"/>, and <paramref name="leastShare"/> of the day),
    /// up to <paramref name="most"/>, then the rest as "Other" where there is any. The pieces go round
    /// once, each from where the one before ended, the last ending at 360 exactly. No pieces for a day
    /// with no time.
    /// </summary>
    public static ScreenTimeRing Of(ScreenTimeLog log, DateOnly day, int most = MostApps, float leastShare = LeastShare)
    {
        var counted = log.Day(day);
        var ranked = counted?.Ranked() ?? [];
        var apps = TimeSpan.Zero;
        foreach (var (_, time) in counted?.Apps ?? [])
        {
            apps += time;
        }
        // Apps' times add up to the total or less, as the bars have it; a total under them is their sum.
        var total = counted is null ? TimeSpan.Zero : counted.Total > apps ? counted.Total : apps;
        if (total <= TimeSpan.Zero)
        {
            return new ScreenTimeRing(day, TimeSpan.Zero, [], []);
        }
        var seconds = total.TotalSeconds;
        string[] top = [.. ranked.Where(pair => pair.Time.TotalSeconds / seconds >= leastShare).Take(Math.Max(0, most)).Select(pair => pair.App)];
        var times = new List<(int Part, string? App, TimeSpan Time)>(top.Length + 1);
        var named = TimeSpan.Zero;
        for (var part = 0; part < top.Length; part++)
        {
            var time = counted!.Apps[top[part]];
            times.Add((part, top[part], time));
            named += time;
        }
        if (total - named > TimeSpan.Zero)
        {
            times.Add((ScreenTimeLine.OtherPart, null, total - named));
        }
        var slices = new List<ScreenTimeSlice>(times.Count);
        var start = 0f;
        for (var index = 0; index < times.Count; index++)
        {
            var (part, app, time) = times[index];
            var share = (float)(time.TotalSeconds / seconds);
            var sweep = index == times.Count - 1 ? 360 - start : share * 360;
            slices.Add(new ScreenTimeSlice(part, app, time, share, start, sweep));
            start += sweep;
        }
        return new ScreenTimeRing(day, total, top, slices);
    }

    /// <summary>The piece at <paramref name="degrees"/> clockwise from twelve o'clock, or -1 for none.</summary>
    public int SliceAt(float degrees)
    {
        var at = ((degrees % 360) + 360) % 360;
        for (var index = 0; index < Slices.Count; index++)
        {
            var slice = Slices[index];
            if (at >= slice.Start && at < slice.Start + slice.Sweep)
            {
                return index;
            }
        }
        return Slices.Count > 0 ? Slices.Count - 1 : -1;
    }
}

internal sealed unsafe class ScreenTimeDonut : CellIsland, IListContent, ILineTargets
{
    /// <summary>
    /// list, §432: its form for its size: the day's total on a row, no ring (Compact); or the ring beside a legend
    /// of every piece it can have — the top apps and the rest — in the whole rows that hold them, which the ring
    /// grows into (Full).
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        if (LineSizes.Of(Settings) == LineSize.Compact)
        {
            return LineRows.Px(1, style);
        }
        var form = ListForm.Of(Gfx, style);
        var air = MathF.Min(form.Pad, style.Px(10));
        var pop = MathF.Max(2, style.Px(3));
        var legend = (ScreenTimeRing.MostApps + 1) * (form.SmallLine + style.Px(6));
        return LineRows.Whole((2 * air) + MathF.Max((2 * pop) + style.Px(LeastDiameter), legend), style);
    }

    /// <summary>A press's targets: nothing, or a piece (this and its place in the ring's pieces), on the ring or its legend row.</summary>
    internal const int NoTarget = 0;
    internal const int SliceTarget = 100;

    /// <summary>The largest the ring is drawn, in DIPs: a taller line leaves the rest as air round it.</summary>
    private const float MostDiameter = 168;

    /// <summary>The least the ring is drawn at, in DIPs.</summary>
    private const float LeastDiameter = 48;

    private ScreenTimeSnapshot? snapshot;
    private int hot;
    private AppId? hotApp;
    private AppId? ownApp;

    public ScreenTimeDonut(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx) => Configure(island, config);

    /// <summary>Where the days come from: the tracker's, or a test's stand-in.</summary>
    internal Func<ScreenTimeSnapshot> Source { get; set; } = ScreenTimeTracker.Shared.Snapshot;

    /// <summary>A minute: today's ring grows while somebody looks at it.</summary>
    public override int IntervalMs => 60_000;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config) => Settings = island;

    /// <summary>Nothing as a whole: its pieces say theirs (<see cref="CardTip"/>).</summary>
    public override string? Tooltip(int index) => null;

    /// <summary>The folder closed: what was pointed at and picked here goes with it.</summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            ownApp = null;
            hotApp = null;
            hot = NoTarget;
        }
    }

    /// <summary>The days as last read.</summary>
    internal ScreenTimeSnapshot Days => snapshot ??= Source();

    protected override bool Sample(bool everything)
    {
        snapshot = Source();
        return false;
    }

    public override bool MinuteChanged(Gfx gfx)
    {
        snapshot = Source();
        return Redraw(gfx);
    }

    /// <summary>Never drawn on a bar: it is a line of a list and nothing else.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- The day and the app ----------------------------------------------------------------------

    internal DateOnly Today => Days.Today;

    /// <summary>The day the ring shows: the day pointed at, else the day picked, else today. Pure, so the rule is held without a folder.</summary>
    internal static DateOnly DayOf(DateOnly? pointed, DateOnly? picked, DateOnly today) => pointed ?? picked ?? today;

    /// <summary>The day it shows now (<see cref="DayOf"/>), from the folder's channels where it follows them.</summary>
    internal DateOnly Shown => DayOf(Followed(Channels.DayHover), Followed(Channels.Day), Today);

    /// <summary>The ring of the day shown.</summary>
    internal ScreenTimeRing Ring => ScreenTimeRing.Of(Days.Log, Shown);

    /// <summary>The app picked: the folder's where the line follows it, else its own.</summary>
    internal AppId? Selected => Following(Channels.App) ? Followed(Channels.App) : ownApp;

    /// <summary>The app pointed at: here, else in the folder where the line follows it.</summary>
    internal AppId? Hovered => hotApp ?? (Following(Channels.AppHover) ? Followed(Channels.AppHover) : null);

    /// <summary>The piece under the pointer here, by its place in the ring's pieces; -1 for none.</summary>
    internal int HotSlice => hot >= SliceTarget ? hot - SliceTarget : -1;

    /// <summary>What a press on <paramref name="target"/> lands on: a top app's piece's app, or null ("Other" is no app).</summary>
    internal AppId? AppAt(int target)
    {
        var ring = Ring;
        var at = target - SliceTarget;
        return target >= SliceTarget && at < ring.Slices.Count && ring.Slices[at].App is { } app ? new AppId(app) : null;
    }

    /// <summary>What the line's right click opens: the app under the pointer, else the one picked (<see cref="WidgetAct.OpenApp"/>).</summary>
    internal AppId? MenuApp => AppAt(hot) ?? Selected;

    /// <summary>A click on a top app's piece picks it, or lets it go where it is the one picked; nothing else does anything.</summary>
    public override IslandAction Click(int index)
    {
        if (AppAt(index) is not { } app)
        {
            return default;
        }
        AppId? next = Selected == app ? null : app;
        ownApp = next;
        if (next is { } picked)
        {
            Publish(Channels.App, picked);
        }
        else
        {
            Withdraw(Channels.App);
        }
        Redraw(Gfx);
        return default;
    }

    /// <summary>Only its pieces light under the pointer, never the line around them.</summary>
    protected internal override bool LightsItsRows => true;

    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) => Plan(box, style).TargetAt(point);

    /// <summary>
    /// keys: each piece once, where a press is that piece (ILineTargets): its legend row, or where the legend has none
    /// for it, a box on the piece itself — never the box round its arc, whose middle is the hole or another piece.
    /// </summary>
    public IReadOnlyList<(int Target, D2D_RECT_F Box)> LineTargets(D2D_RECT_F box, in TileStyle style) => Plan(box, style).Stops();

    /// <summary>The piece under the pointer lights; its app is the folder's app pointed at until the pointer leaves it.</summary>
    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var target = point is { } at ? CardTarget(at, box, style, surface) : NoTarget;
        if (target == hot)
        {
            return;
        }
        hot = target;
        var app = AppAt(target);
        if (hotApp != app)
        {
            hotApp = app;
            if (app is { } pointed)
            {
                Publish(Channels.AppHover, pointed);
            }
            else
            {
                Withdraw(Channels.AppHover);
            }
        }
        _ = Wake();
    }

    /// <summary>What the pointer is over now (a target), for a test.</summary>
    internal int Hot => hot;

    /// <summary>A piece, on the ring or its legend row: its app (or "Other"), its time and its share of the day.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Plan(box, style);
        var target = plan.TargetAt(point);
        if (target < SliceTarget)
        {
            return null;
        }
        var slice = plan.Ring.Slices[target - SliceTarget];
        var name = slice.App is { } app ? ScreenTimeNames.Of(app).Name : S.WidgetScreenTimeOther;
        var percent = slice.Time > TimeSpan.Zero ? Math.Max(1, (int)MathF.Round(slice.Share * 100)) : 0;
        var part = plan.Legend.FirstOrDefault(row => Inside(row.Box, point)) is { Box: var row } && row.right > row.left
            ? row
            : plan.PieceBox(target - SliceTarget);
        return (S.WidgetScreenTimeShareTip(name: name, time: ScreenTimeText.Duration(slice.Time), percent: percent), part);
    }

    // --- The layout -------------------------------------------------------------------------------

    /// <summary>One run of words, and where.</summary>
    internal readonly record struct Words(string Text, float Px, Weight Weight, bool Quiet, float Left, float Top, float Room, bool Right = false);

    /// <summary>A legend row: the piece it stands for (its place in the ring's pieces), its box, its swatch, its name and its time.</summary>
    internal readonly record struct LegendRow(int Slice, D2D_RECT_F Box, D2D_RECT_F Swatch, Words Name, Words Time);

    /// <summary>The ring laid out in a line.</summary>
    internal sealed class Layout
    {
        public required ScreenTimeRing Ring { get; init; }

        public required Vector2 Centre { get; init; }

        /// <summary>The ring's outer and inner radius, at rest; a piece lit stands <see cref="Pop"/> further out.</summary>
        public required float Outer { get; init; }

        public required float Inner { get; init; }

        public required float Pop { get; init; }

        /// <summary>The gap between two pieces, in degrees.</summary>
        public required float Gap { get; init; }

        /// <summary>Each piece's colour's place in the palette, by the piece's part (a top app's place in <see cref="ScreenTimeRing.Top"/>).</summary>
        public required int[] Slots { get; init; }

        /// <summary>The total and the day's name in the middle, where they fit.</summary>
        public List<Words> Middle { get; } = [];

        /// <summary>The total and the day over the legend, where the middle has no room for them.</summary>
        public List<Words> Head { get; } = [];

        public List<LegendRow> Legend { get; } = [];

        /// <summary>"No apps" where the legend has nothing to list.</summary>
        public Words? Empty { get; set; }

        /// <summary>The legend beside the ring, rather than under it.</summary>
        public bool Beside { get; init; }

        public int TargetAt(Vector2 point)
        {
            if (Ring.Slices.Count > 0 && Outer > 0)
            {
                var from = point - Centre;
                var distance = from.Length();
                var reach = Pop;
                if (distance >= Inner - reach && distance <= Outer + reach)
                {
                    var degrees = MathF.Atan2(from.X, -from.Y) * 180 / MathF.PI;
                    return SliceTarget + Ring.SliceAt(degrees);
                }
            }
            foreach (var row in Legend)
            {
                if (Inside(row.Box, point))
                {
                    return SliceTarget + row.Slice;
                }
            }
            return NoTarget;
        }

        /// <summary>keys: each piece's stop — its legend row, else its <see cref="PieceBox"/> on the ring — in the pieces' order.</summary>
        public List<(int Target, D2D_RECT_F Box)> Stops()
        {
            var stops = new List<(int Target, D2D_RECT_F Box)>();
            for (var index = 0; index < Ring.Slices.Count; index++)
            {
                if (Legend.FirstOrDefault(row => row.Slice == index && row.Box.right > row.Box.left) is { Box: var listed } && listed.right > listed.left)
                {
                    stops.Add((SliceTarget + index, listed));
                }
                else if (Outer > 0)
                {
                    stops.Add((SliceTarget + index, PieceBox(index)));
                }
            }
            return stops;
        }

        /// <summary>A small box on the ring's outer edge at the middle of piece <paramref name="index"/>: where its tooltip points.</summary>
        public D2D_RECT_F PieceBox(int index)
        {
            var slice = Ring.Slices[index];
            var middle = (slice.Start + (slice.Sweep / 2)) * MathF.PI / 180;
            var radius = (Outer + Inner) / 2;
            var at = Centre + new Vector2(MathF.Sin(middle) * radius, -MathF.Cos(middle) * radius);
            var half = (Outer - Inner) / 2;
            return new D2D_RECT_F(at.X - half, at.Y - half, at.X + half, at.Y + half);
        }
    }

    /// <summary>The ring at the size of <paramref name="box"/> (see the note at the top).</summary>
    internal Layout Plan(D2D_RECT_F box, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var ring = Ring;
        var width = TilePaint.Width(box);
        var height = TilePaint.Height(box);
        var pop = MathF.Max(2, style.Px(3));
        var air = MathF.Min(form.Pad, style.Px(10));
        var left = box.left + form.Inset;
        var right = box.right - form.Inset;
        var rowHeight = form.SmallLine + style.Px(6);
        var swatch = MathF.Max(4, style.Px(10));
        var legendLeast = MathF.Max(style.Px(120) * MathF.Min(style.TextScale, 1.5f), 4 * swatch);
        var entries = ring.Slices.Count;

        // Beside: the ring at the leading edge as tall as the line holds, the legend in what is left.
        var most = style.Px(MostDiameter);
        var least = style.Px(LeastDiameter);
        var tall = height - (2 * air) - (2 * pop);
        var besideDiameter = MathF.Min(MathF.Min(tall, most), right - left - style.Px(16) - legendLeast);
        var beside = besideDiameter >= least;
        // Too short for the least ring beside or under: no ring, the words alone in the line.
        var ringless = !beside && tall < least;
        float diameter;
        Vector2 centre;
        if (ringless)
        {
            beside = true;
            air = MathF.Min(air, style.Px(4));
            diameter = 0;
            centre = new Vector2(left, box.top + (height / 2));
        }
        else if (beside)
        {
            diameter = PopupPaint.Round(besideDiameter);
            // At the line's top: in a line taller than the largest ring, it stands where the
            // list's other cards begin, not in the middle (ListScrollTests).
            centre = new Vector2(left + pop + (diameter / 2), box.top + air + pop + (diameter / 2));
        }
        else
        {
            // Under: the ring centred, the legend's rows under it where the line is tall enough.
            var under = Math.Max(1, entries) * rowHeight;
            var withLegend = tall - under - style.Px(8);
            diameter = PopupPaint.Round(MathF.Min(MathF.Min(withLegend >= least ? withLegend : tall, most), right - left - (2 * pop)));
            centre = new Vector2(box.left + (width / 2), box.top + air + pop + (diameter / 2));
        }
        diameter = ringless ? 0 : MathF.Max(diameter, 2);
        var outer = diameter / 2;
        var thickness = Math.Clamp(PopupPaint.Round(diameter * 0.16f), MathF.Max(4, style.Px(8)), style.Px(22));
        var inner = ringless ? 0 : MathF.Max(1, outer - thickness);
        var gapPx = MathF.Max(1, style.Px(2));
        var slots = SlotsOf(ring);
        var layout = new Layout
        {
            Ring = ring, Centre = centre, Outer = outer, Inner = inner, Pop = pop,
            Gap = ring.Slices.Count > 1 ? gapPx / ((outer + inner) / 2) * 180 / MathF.PI : 0,
            Slots = slots, Beside = beside,
        };

        // The middle: the total over the day's name, as large as the hole holds.
        var hole = 2 * inner * 0.82f;
        var dayName = ScreenTimeText.DayName(ring.Day, Today, day => Panels.Nls.DayAndShortDate(day, year: day.Year != Today.Year));
        var value = ScreenTimeText.Duration(ring.Total);
        // The kit's Value, a step down the ramp where the hole is narrower (the Caption at the least, §388).
        var measure = Gfx;
        var valuePx = LineType.Largest(style, style.Text(LineType.ValueDip), px => PopupPaint.Measure(measure, value, px, Weight.Semibold).X <= hole);
        var valueLine = TilePaint.LineHeight(Gfx, valuePx, Weight.Semibold);
        var dayFits = valueLine + form.SmallLine <= hole * 0.9f && PopupPaint.Measure(Gfx, dayName, form.SmallPx, Weight.Regular).X <= hole;
        var middle = PopupPaint.Measure(Gfx, value, valuePx, Weight.Semibold).X <= hole && valueLine <= hole;
        if (middle)
        {
            var block = valueLine + (dayFits ? form.SmallLine : 0);
            var top = PopupPaint.Round(centre.Y - (block / 2));
            layout.Middle.Add(new Words(value, valuePx, Weight.Semibold, ring.Total <= TimeSpan.Zero, centre.X - (hole / 2), top, hole));
            if (dayFits)
            {
                layout.Middle.Add(new Words(dayName, form.SmallPx, Weight.Regular, true, centre.X - (hole / 2), top + valueLine, hole));
            }
        }

        // The legend: a row a piece, its swatch, its name and its time, as many as the room holds.
        var legendLeft = ringless ? left : beside ? PopupPaint.Round(centre.X + outer + pop + style.Px(16)) : left;
        var legendTop = beside ? box.top + air : centre.Y + outer + pop + style.Px(8);
        var legendBottom = box.bottom - air;
        // Where the hole is too small for the total (a short line, the largest text), the total and
        // the day head the legend instead, the total in the list's text and the day at its end.
        var head = 0f;
        var headLine = TilePaint.LineHeight(Gfx, form.TextPx, Weight.Semibold);
        if (!middle && beside && legendBottom - legendTop >= headLine)
        {
            head = headLine + style.Px(4);
        }
        var fit = (int)MathF.Floor((legendBottom - legendTop - head + 0.5f) / rowHeight);
        var shown = Math.Clamp(fit, 0, entries);
        if (beside)
        {
            // Centred against the ring.
            legendTop = PopupPaint.Round(MathF.Max(box.top + air, centre.Y - (((Math.Max(1, shown) * rowHeight) + head) / 2)));
        }
        if (head > 0)
        {
            var valueWidth = MathF.Min(PopupPaint.Measure(Gfx, value, form.TextPx, Weight.Semibold).X, right - legendLeft);
            var top = PopupPaint.Round(legendTop);
            layout.Head.Add(new Words(value, form.TextPx, Weight.Semibold, ring.Total <= TimeSpan.Zero, legendLeft, top, valueWidth));
            var dayRoom = right - legendLeft - valueWidth - style.Gap;
            if (PopupPaint.Measure(Gfx, dayName, form.SmallPx, Weight.Regular).X <= dayRoom)
            {
                layout.Head.Add(new Words(dayName, form.SmallPx, Weight.Regular, true, right, top + PopupPaint.Round((headLine - form.SmallLine) / 2), dayRoom, Right: true));
            }
            legendTop += head;
        }
        if (entries == 0 && fit > 0)
        {
            layout.Empty = new Words(S.WidgetScreenTimeNoApps, form.SmallPx, Weight.Regular, true, legendLeft, PopupPaint.Round(legendTop + ((rowHeight - form.SmallLine) / 2)), MathF.Max(1, right - legendLeft));
        }
        if (shown > 0)
        {
            var timeField = 0f;
            for (var index = 0; index < shown; index++)
            {
                timeField = MathF.Max(timeField, PopupPaint.Measure(Gfx, ScreenTimeText.Duration(ring.Slices[index].Time), form.SmallPx, Weight.Regular).X);
            }
            var textLeft = legendLeft + swatch + style.Px(8);
            var nameRoom = MathF.Max(1, right - timeField - style.Gap - textLeft);
            for (var index = 0; index < shown; index++)
            {
                var slice = ring.Slices[index];
                var y = legendTop + (index * rowHeight);
                var textTop = PopupPaint.Round(y + ((rowHeight - form.SmallLine) / 2));
                var name = slice.App is { } app ? ScreenTimeNames.Of(app).Name : S.WidgetScreenTimeOther;
                layout.Legend.Add(new LegendRow(
                    index,
                    new D2D_RECT_F(legendLeft - style.Px(4), y, right + style.Px(4), y + rowHeight),
                    PopupPaint.Rect(legendLeft, PopupPaint.Round(y + ((rowHeight - swatch) / 2)), swatch, swatch),
                    new Words(name, form.SmallPx, Weight.Regular, false, textLeft, textTop, nameRoom),
                    new Words(ScreenTimeText.Duration(slice.Time), form.SmallPx, Weight.Regular, true, right, textTop, timeField, Right: true)));
            }
        }
        return layout;
    }

    /// <summary>
    /// Each top app's place in the palette: the bars' where it is one of their top apps (the
    /// fortnight's chart, <see cref="ScreenTimeLine.ChartOf"/>), so it wears one colour in a folder;
    /// a colour still free otherwise.
    /// </summary>
    internal int[] SlotsOf(ScreenTimeRing ring)
    {
        var chart = ScreenTimeLine.ChartOf(Days.Log, ring.Day, Today);
        var order = chart.Top.ToList();
        order.AddRange(ring.Top.Where(app => !chart.Top.Contains(app, StringComparer.OrdinalIgnoreCase)));
        if (order.Count > ScreenTimeColours.Count)
        {
            order = [.. ring.Top];
        }
        var all = ScreenTimeColours.Slots(order, [.. order.Select(app => ScreenTimeColours.HueOf(ScreenTimeNames.Of(app).Logo))]);
        return [.. ring.Top.Select(app => all[order.FindIndex(each => string.Equals(each, app, StringComparison.OrdinalIgnoreCase))])];
    }

    // --- Drawing ----------------------------------------------------------------------------------

    /// <summary>The piece lit — under the pointer here, else the app pointed at in the folder — and the app picked's, by place in the ring's pieces; -1 for none.</summary>
    internal (int Pointed, int Picked) Lit(ScreenTimeRing ring)
    {
        var pointed = HotSlice;
        if (pointed < 0 && Hovered is { } at)
        {
            pointed = IndexOf(ring, at);
        }
        var picked = Selected is { } app ? IndexOf(ring, app) : -1;
        return (pointed, picked);
    }

    private static int IndexOf(ScreenTimeRing ring, AppId app)
    {
        for (var index = 0; index < ring.Slices.Count; index++)
        {
            if (ring.Slices[index].App is { } key && new AppId(key) == app)
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>
    /// A piece's colour: its app's, "Other" the neutral grey; with a piece lit the rest dimmed — a
    /// little for the pointer, more for a pick. In high contrast the text's, the piece lit the
    /// highlight and the rest grey.
    /// </summary>
    internal static DXGI_RGBA SliceInk(int[] slots, int part, bool on, (int Pointed, int Picked) lit, in TileStyle style)
    {
        var any = lit.Pointed >= 0 || lit.Picked >= 0;
        var theme = style.Theme;
        if (theme.HighContrast)
        {
            return !any ? ScreenTimeLine.PartInk(slots, part, false, style) : on ? PopupPaint.Ink(theme.Accent) : theme.TextDisabled;
        }
        var ink = ScreenTimeLine.PartInk(slots, part, false, style);
        if (any && !on)
        {
            ink.a *= lit.Picked >= 0 ? 0.3f : 0.45f;
        }
        return ink;
    }

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var layout = Plan(box, style);
        var theme = style.Theme;
        var ring = layout.Ring;
        var lit = Lit(ring);
        if (layout.Outer <= 0)
        {
            // No ring in a line too short for it: the words alone.
        }
        else if (ring.Slices.Count == 0)
        {
            var thickness = layout.Outer - layout.Inner;
            PopupPaint.Ring(dc, layout.Centre, layout.Outer, thickness, TilePaint.Track(style));
        }
        else
        {
            for (var index = 0; index < ring.Slices.Count; index++)
            {
                var slice = ring.Slices[index];
                var on = index == lit.Pointed || index == lit.Picked;
                var ink = SliceInk(layout.Slots, slice.Part, on, lit, style);
                var outer = layout.Outer + (on ? layout.Pop : 0);
                if (ring.Slices.Count == 1)
                {
                    PopupPaint.Ring(dc, layout.Centre, outer, outer - layout.Inner, ink);
                    continue;
                }
                // The gap between two pieces is half each's; a piece too thin for its gap is in the
                // ring's time all the same and draws nothing.
                var sweep = slice.Sweep - layout.Gap;
                if (sweep > 0.05f)
                {
                    Sector(dc, layout.Centre, outer, layout.Inner, slice.Start + (layout.Gap / 2), sweep, ink);
                }
            }
        }
        foreach (var words in layout.Middle)
        {
            TilePaint.Line(dc, Gfx, words.Text, words.Px, words.Weight, words.Quiet ? theme.TextSecondary : theme.Text, words.Left + MathF.Max(0, (words.Room - PopupPaint.Measure(Gfx, words.Text, words.Px, words.Weight).X) / 2), words.Top, words.Room);
        }
        foreach (var words in layout.Head)
        {
            var ink = words.Quiet ? theme.TextSecondary : theme.Text;
            if (words.Right)
            {
                TilePaint.LineRight(dc, Gfx, words.Text, words.Px, words.Weight, ink, words.Left, words.Top, words.Room);
            }
            else
            {
                TilePaint.Line(dc, Gfx, words.Text, words.Px, words.Weight, ink, words.Left, words.Top, words.Room);
            }
        }
        if (layout.Empty is { } empty)
        {
            TilePaint.Line(dc, Gfx, empty.Text, empty.Px, empty.Weight, theme.TextSecondary, empty.Left, empty.Top, empty.Room);
        }
        var any = lit.Pointed >= 0 || lit.Picked >= 0;
        foreach (var row in layout.Legend)
        {
            var on = row.Slice == lit.Pointed || row.Slice == lit.Picked;
            var part = ring.Slices[row.Slice].Part;
            if (row.Slice == HotSlice)
            {
                if (theme.HighContrast)
                {
                    PopupPaint.Stroke(dc, row.Box, style.Px(4), MathF.Max(1, style.Px(1)), PopupPaint.Ink(theme.Accent));
                }
                else
                {
                    PopupPaint.Fill(dc, row.Box, style.Px(4), PopupPaint.Ink(theme.SubtleHover));
                }
            }
            PopupPaint.Fill(dc, row.Swatch, style.Px(2), SliceInk(layout.Slots, part, on, lit, style));
            TilePaint.Line(dc, Gfx, row.Name.Text, row.Name.Px, row.Name.Weight, any && !on ? theme.TextSecondary : theme.Text, row.Name.Left, row.Name.Top, row.Name.Room);
            TilePaint.LineRight(dc, Gfx, row.Time.Text, row.Time.Px, row.Time.Weight, theme.TextSecondary, row.Time.Left, row.Time.Top, row.Time.Room);
        }
    }

    /// <summary>A piece of a ring: from <paramref name="start"/> clockwise through <paramref name="sweep"/> degrees (twelve o'clock 0), between the two radii.</summary>
    private static void Sector(ID2D1DeviceContext* dc, Vector2 centre, float outer, float inner, float start, float sweep, DXGI_RGBA ink)
    {
        ID2D1Factory* factory = null;
        ID2D1PathGeometry* path = null;
        ID2D1GeometrySink* sink = null;
        ID2D1SolidColorBrush* brush = null;
        try
        {
            dc->GetFactory(&factory);
            if (factory == null || factory->CreatePathGeometry(&path).FAILED || path->Open(&sink).FAILED)
            {
                return;
            }
            var from = start * MathF.PI / 180;
            var to = (start + sweep) * MathF.PI / 180;
            var large = sweep > 180 ? D2D1_ARC_SIZE.D2D1_ARC_SIZE_LARGE : D2D1_ARC_SIZE.D2D1_ARC_SIZE_SMALL;
            D2D_POINT_2F At(float angle, float radius) => new(centre.X + (MathF.Sin(angle) * radius), centre.Y - (MathF.Cos(angle) * radius));
            sink->BeginFigure(At(from, outer), D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_FILLED);
            var outerArc = new D2D1_ARC_SEGMENT
            {
                point = At(to, outer), size = new D2D_SIZE_F(outer, outer), rotationAngle = 0,
                sweepDirection = D2D1_SWEEP_DIRECTION.D2D1_SWEEP_DIRECTION_CLOCKWISE, arcSize = large,
            };
            sink->AddArc(&outerArc);
            sink->AddLine(At(to, inner));
            var innerArc = new D2D1_ARC_SEGMENT
            {
                point = At(from, inner), size = new D2D_SIZE_F(inner, inner), rotationAngle = 0,
                sweepDirection = D2D1_SWEEP_DIRECTION.D2D1_SWEEP_DIRECTION_COUNTER_CLOCKWISE, arcSize = large,
            };
            sink->AddArc(&innerArc);
            sink->EndFigure(D2D1_FIGURE_END.D2D1_FIGURE_END_CLOSED);
            if (sink->Close().FAILED)
            {
                return;
            }
            brush = Gfx.Brush(dc, ink);
            dc->FillGeometry((ID2D1Geometry*)path, (ID2D1Brush*)brush, null);
        }
        finally
        {
            ComUtil.Release(ref brush);
            ComUtil.Release(ref sink);
            ComUtil.Release(ref path);
            ComUtil.Release(ref factory);
        }
    }

    private static bool Inside(D2D_RECT_F rect, Vector2 point) =>
        point.X >= rect.left && point.X < rect.right && point.Y >= rect.top && point.Y < rect.bottom;
}
