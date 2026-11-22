// Screen time's lines of a list folder (docs\widgets.md §69, §603): today's total, the fortnight's bars, and the
// apps the day went to. One class for the three, because they read the one tracker (ScreenTimeTracker) and show the
// one day — the folder's (Channels.Day).
//
// What the person missed when screen time became pieces of a folder the first time (2026-09-25:
// "just does not feel as good as before"): the app list drew nothing at all, and nothing lit under
// the pointer. Here every app is a row of its own on the list's edges — its logo, its name over a
// bar of its share, its time on the trailing gutter — lit under the pointer as a menu's row is. The
// total's step back and on a day and the days of its bars light the same way.
//
// The app (2026-10-10: "when you click on an application it should not open it but select it ...
// this does not get shared with other widgets yet but it should. both states, hover and
// selected"): a click on an app's row, or on its piece of a bar, picks it as the folder's app
// (Channels.App), a second click or Escape lets it go, and the row's right click is where it opens.
// The app under the pointer is the folder's too (Channels.AppHover), while the pointer is on it. So
// the bars light the app pointed at and, picked, draw it alone in full colour, and the total says its
// time for the day; the apps' line marks the row picked as a list marks its selection, and lights the
// row of the app pointed at in the bars. Both are forgotten when the folder closes.
//
// Each says the same at both of its sizes (§603; the person, 2026-11-09: "split all of them"), never more where its
// line is taller:
//
//   * today (screenTime): the day's name, its total and the daily average — an app picked, the app's time that day,
//     its name and the day's total it is part of. Compact the kit's row: the glyph, the day's name, the total at the
//     trailing gutter, the average as its caption. Full the day's name strong with ‹ › at the trailing
//     gutter over the total large, the average beside it — its label over its figure — or, where the width leaves no
//     room there, under it. No graph: the fortnight is its history.
//   * history (screenTimeHistory): the fortnight ending at the day shown as a bar a day split by app
//     (ScreenTimeChart), the legend under the bars, the shown day marked, a day lit under the pointer and shown by a
//     click, a piece of a bar saying its app in a tooltip. Always bars (the person, 2026-11-09: "it should always
//     show the bar chart and not the area chart"); Compact the bars' band shorter, Full taller.
//   * the apps: a row each at a list row — Compact, Standard or Large as the item style is — from
//     the top, scrolling where there are more than the line holds (ListScroll.cs); one row, the most
//     used app. Each share's bar is in the app's colour on the
//     fortnight's bars (ChartOf, SlotsOf: one ranking for both lines), an app outside the chart's
//     top apps in "Other"'s grey, as it is in the chart.
//
// The day: the folder's where the line follows it ("Follow the folder's day"), else the one it
// stepped to, else today. A step or a click on a bar publishes it as the folder's day
// (Channels.Day), so the apps follow the total, and all follow a month calendar in the same
// folder. A day nothing was counted for says so, rather than stepping somewhere else. A day of the
// bars under the pointer is the folder's day pointed at (Channels.DayHover) until the pointer
// leaves it, which the ring (ScreenTimeDonut) shows meanwhile.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>Which of screen time's lines a <see cref="ScreenTimeLine"/> is.</summary>
internal enum ScreenTimePart
{
    /// <summary>Today's total (or the day shown) and the daily average.</summary>
    Total,

    /// <summary>The fortnight's bars by app (§603).</summary>
    History,

    /// <summary>The day's apps, most used first.</summary>
    Apps,
}

internal sealed unsafe class ScreenTimeLine : CellIsland, IListContent, IListScroll, ILineTargets
{
    /// <summary>Segoe Fluent Icons: a screen with a clock.</summary>
    internal const string Glyph = "";

    /// <summary>An app without a logo of its own.</summary>
    private const string AppGlyph = "";

    private const string PreviousGlyph = "";
    private const string NextGlyph = "";

    /// <summary>How many days the total's graph shows: two weeks, ending today or at the day shown.</summary>
    internal const int GraphDays = 14;

    /// <summary>How many days the daily average is taken over: the week ending at the day shown.</summary>
    internal const int AverageDays = 7;

    // A press's targets: the step back and on, the day's name (back to today), a day of the graph,
    // an app's row.
    internal const int NoTarget = 0;
    internal const int PreviousTarget = 1;
    internal const int NextTarget = 2;
    internal const int TodayTarget = 3;
    internal const int DayTarget = 100;
    internal const int AppTarget = 1000;

    /// <summary>A top app's piece of a bar: this, and the bar's place times <see cref="SegmentStride"/>, and the app's place in the chart.</summary>
    internal const int SegmentTarget = 10_000;
    internal const int SegmentStride = 8;

    /// <summary>§603: the history's bars at Compact and at Full, in DIPs: a band, then a taller one.</summary>
    private const float CompactBarsDips = 40;
    private const float FullBarsDips = 100;
    private ScreenTimeSnapshot? snapshot;
    private int hot;

    // The app under the pointer here, which this line published as the folder's (AppHover); and the
    // app picked where the line does not follow the folder's (or stands in none).
    private AppId? hotApp;
    private AppId? ownApp;

    // The day of the graph under the pointer, which this line published as the folder's (DayHover).
    private DateOnly? hotDay;

    // The day shown where somebody stepped to one: what a line not following the folder's day
    // shows, and gone when the folder closes, as the folder's day is.
    private DateOnly? own;

    public ScreenTimeLine(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Which = island.Info?.Id switch
        {
            var id when id == WidgetInfos.ScreenTimeApps.Id => ScreenTimePart.Apps,
            var id when id == WidgetInfos.ScreenTimeHistory.Id => ScreenTimePart.History,
            _ => ScreenTimePart.Total,
        };
        Configure(island, config);
    }

    /// <summary>Which line it is.</summary>
    internal ScreenTimePart Which { get; }

    /// <summary>Where the days come from: the tracker's, or a test's stand-in.</summary>
    internal Func<ScreenTimeSnapshot> Source { get; set; } = ScreenTimeTracker.Shared.Snapshot;

    /// <summary>A minute: the total is a number that grows while somebody looks at it.</summary>
    public override int IntervalMs => 60_000;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config) => Settings = island;

    /// <summary>Nothing: what it says is drawn, and a folder's line has no tooltip.</summary>
    public override string? Tooltip(int index) => null;

    /// <summary>The folder closed: the day stepped to was this look's, and goes with it.</summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            own = null;
            ownApp = null;
            hotApp = null;
            hotDay = null;
            hot = NoTarget;
        }
    }

    /// <summary>The days as last read: read on the first look, then on every tick and refresh.</summary>
    internal ScreenTimeSnapshot Days => snapshot ??= Source();

    /// <summary>Reads the days again; the count of cells never changes.</summary>
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

    /// <summary>Never drawn on a bar: it is a line of a list and nothing else (<see cref="ListWidgets"/>).</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- The day ----------------------------------------------------------------------------------

    /// <summary>Today, as the days were read.</summary>
    internal DateOnly Today => Days.Today;

    /// <summary>The day it shows: the folder's where it follows it, else its own, else today.</summary>
    internal DateOnly Shown => Followed(Channels.Day) ?? own ?? Today;

    /// <summary>The first day anything was counted for, or today.</summary>
    internal DateOnly Oldest => Days.Log.Oldest is { } oldest && oldest < Today ? oldest : Today;

    /// <summary>Shows <paramref name="day"/>: kept, and published as the folder's day, so every line following it shows it.</summary>
    private void Show(DateOnly day)
    {
        own = day;
        Publish(Channels.Day, day);
        Redraw(Gfx);
    }

    // --- The app ----------------------------------------------------------------------------------

    /// <summary>The app picked: the folder's where the line follows it, else its own; null for none.</summary>
    internal AppId? Selected => Following(Channels.App) ? Followed(Channels.App) : ownApp;

    /// <summary>The app pointed at: in the folder where the line follows it, else here.</summary>
    internal AppId? Hovered => Following(Channels.AppHover) ? Followed(Channels.AppHover) ?? hotApp : hotApp;

    /// <summary>Picks <paramref name="app"/>, or lets it go where it is the one picked: kept, and published as the folder's app.</summary>
    private void Pick(AppId app)
    {
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
    }

    /// <summary>The pointer is on <paramref name="app"/> here now, or on no app: told to the folder as it changes.</summary>
    private void Point(AppId? app)
    {
        if (hotApp == app)
        {
            return;
        }
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

    /// <summary>
    /// The pointer is on a day of the graph now, or on none: told to the folder as it changes
    /// (<see cref="Channels.DayHover"/>), so the ring shows the day pointed at meanwhile.
    /// </summary>
    private void PointDay(DateOnly? day)
    {
        if (hotDay == day)
        {
            return;
        }
        hotDay = day;
        if (day is { } pointed)
        {
            Publish(Channels.DayHover, pointed);
        }
        else
        {
            Withdraw(Channels.DayHover);
        }
    }

    /// <summary>The day of the bars <paramref name="target"/> lands on — its column, a piece of its bar — or null.</summary>
    internal DateOnly? DayAt(int target)
    {
        var at = DayIndexOf(target);
        if (at < 0)
        {
            return null;
        }
        var graph = GraphOf(Days.Log, Shown, Today);
        return at < graph.Count ? graph[at].Day : null;
    }

    /// <summary>
    /// The app a press on <paramref name="target"/> lands on: an app's row, or a top app's piece of a
    /// bar; null for anything else.
    /// </summary>
    internal AppId? AppAt(int target)
    {
        if (target >= SegmentTarget)
        {
            var part = (target - SegmentTarget) % SegmentStride;
            var top = ChartOf(Days.Log, Shown, Today).Top;
            return part < top.Count ? new AppId(top[part]) : null;
        }
        if (target >= AppTarget)
        {
            var ranked = Days.Log.Day(Shown)?.Ranked() ?? [];
            var at = target - AppTarget;
            return (uint)at < (uint)ranked.Count ? new AppId(ranked[at].App) : null;
        }
        return null;
    }

    /// <summary>The day of the bar a piece's <paramref name="target"/> stands in, or of the graph's day target.</summary>
    private static int DayIndexOf(int target) =>
        target >= SegmentTarget ? (target - SegmentTarget) / SegmentStride
        : target is >= DayTarget and < AppTarget ? target - DayTarget
        : -1;

    /// <summary>
    /// What the line's right click opens: the app under the pointer, else the one picked; null for
    /// none (Dock.WidgetMenus.cs, <see cref="WidgetAct.OpenApp"/>).
    /// </summary>
    internal AppId? MenuApp => AppAt(hot) ?? Selected;

    /// <summary><paramref name="app"/>'s time on <paramref name="day"/>.</summary>
    internal static TimeSpan TimeOf(ScreenTimeLog log, DateOnly day, AppId app)
    {
        foreach (var (key, time) in log.Day(day)?.Ranked() ?? [])
        {
            if (new AppId(key) == app)
            {
                return time;
            }
        }
        return TimeSpan.Zero;
    }

    /// <summary>
    /// Where a step back or on takes <paramref name="day"/>, kept between the first day counted and
    /// today; null where it goes nowhere. Pure, so the rule is held without a folder.
    /// </summary>
    internal static DateOnly? Stepped(int target, DateOnly day, DateOnly today, DateOnly oldest)
    {
        var wanted = target switch
        {
            PreviousTarget => day > today ? today : day.AddDays(-1),
            NextTarget => day < oldest ? oldest : day.AddDays(1),
            TodayTarget => today,
            _ => day,
        };
        return wanted < oldest || wanted > today || wanted == day ? null : wanted;
    }

    /// <summary>
    /// The days the graph shows, oldest first, each with its hours: the two weeks ending today, or
    /// at the day shown where that is further back; none before the first day counted.
    /// </summary>
    internal static List<(DateOnly Day, float Hours)> GraphOf(ScreenTimeLog log, DateOnly shown, DateOnly today)
    {
        var days = new List<(DateOnly, float)>();
        if (log.Oldest is not { } oldest)
        {
            return days;
        }
        var end = shown < today.AddDays(-(GraphDays - 1)) ? shown : today;
        var start = end.AddDays(-(GraphDays - 1));
        if (start < oldest)
        {
            start = oldest;
        }
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            days.Add((day, (float)log.TotalOn(day).TotalHours));
        }
        return days;
    }

    /// <summary>
    /// The fortnight's chart for <paramref name="shown"/>: the bars the total draws, and the one
    /// ranking the apps' line colours its bars by, so the two lines of a folder always agree.
    /// </summary>
    internal static ScreenTimeChart ChartOf(ScreenTimeLog log, DateOnly shown, DateOnly today) =>
        ScreenTimeChart.Of(log, [.. GraphOf(log, shown, today).Select(point => point.Day)]);

    /// <summary>Each of <paramref name="chart"/>'s top apps' place in the palette, by its logo (<see cref="ScreenTimeColours"/>).</summary>
    internal static int[] SlotsOf(ScreenTimeChart chart) =>
        ScreenTimeColours.Slots(chart.Top, [.. chart.Top.Select(app => ScreenTimeColours.HueOf(ScreenTimeNames.Of(app).Logo))]);

    /// <summary>
    /// The daily average over the week ending at <paramref name="shown"/>, of the days something was
    /// counted for — a day the PC was not used is no day of use — and today counted so far; null
    /// where none was.
    /// </summary>
    internal static TimeSpan? AverageOf(ScreenTimeLog log, DateOnly shown)
    {
        var sum = TimeSpan.Zero;
        var used = 0;
        for (var day = shown.AddDays(-(AverageDays - 1)); day <= shown; day = day.AddDays(1))
        {
            if (log.TotalOn(day) is { Ticks: > 0 } total)
            {
                sum += total;
                used++;
            }
        }
        return used > 0 ? sum / used : null;
    }

    /// <summary>What a line calls a day: "Today", "Yesterday", else its date as the region writes it, longest first.</summary>
    private string[] DayNames(DateOnly day)
    {
        var today = Today;
        if (day == today || day == today.AddDays(-1))
        {
            return [ScreenTimeText.DayName(day, today, Panels.Nls.LongDateWithoutYear)];
        }
        return day.Year == today.Year
            ? [Panels.Nls.LongDateWithoutYear(day), Panels.Nls.DayAndShortDate(day, year: false), Panels.Nls.ShortDateWithoutYear(day)]
            : [Panels.Nls.LongDate(day), Panels.Nls.DayAndShortDate(day), Panels.Nls.ShortDate(day)];
    }

    // --- Drawing ----------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (Which == ScreenTimePart.Apps)
        {
            PaintApps(dc, AppsPlan(box, style), style);
        }
        else
        {
            PaintTotal(dc, TotalPlan(box, style), style);
        }
    }

    /// <summary>Only its own rows and buttons light under the pointer, never the line around them.</summary>
    protected internal override bool LightsItsRows => true;

    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        LineFocus.At(LineTargets(box, style), point);

    /// <summary>keys: its steps, the day's name, the days and their apps' pieces, or the apps' rows, laid out for this box (ILineTargets).</summary>
    public IReadOnlyList<(int Target, D2D_RECT_F Box)> LineTargets(D2D_RECT_F box, in TileStyle style) =>
        Which == ScreenTimePart.Apps ? AppsPlan(box, style).Targets() : TotalPlan(box, style).Targets();

    /// <summary>
    /// What is under the pointer lights; the folder draws the line again (<see cref="CellIsland.Wake"/>).
    /// An app under it — its row, its piece of a bar — is the folder's app pointed at, until it leaves.
    /// </summary>
    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var target = point is { } at ? CardTarget(at, box, style, surface) : NoTarget;
        if (target == hot)
        {
            return;
        }
        hot = target;
        Point(AppAt(target));
        PointDay(DayAt(target));
        _ = Wake();
    }

    /// <summary>What the pointer is over now (a target), for a test.</summary>
    internal int Hot => hot;

    /// <summary>
    /// A press: a step, the day's name or a day of the graph shows that day in the folder; an app's
    /// row picks the app, or lets it go where it is the one picked (<see cref="Pick"/>) — it never
    /// opens it, which is the row's right click's. A top app's piece of a bar picks that app and
    /// shows its day: on the day shown, a second click lets it go.
    /// </summary>
    public override IslandAction Click(int index)
    {
        if (index >= AppTarget)
        {
            if (AppAt(index) is not { } app)
            {
                return default;
            }
            var graph = GraphOf(Days.Log, Shown, Today);
            var day = DayIndexOf(index);
            if ((uint)day < (uint)graph.Count && graph[day].Day != Shown)
            {
                Show(graph[day].Day);
                if (Selected != app)
                {
                    Pick(app);
                }
                return default;
            }
            Pick(app);
            return default;
        }
        if (index >= DayTarget)
        {
            var graph = GraphOf(Days.Log, Shown, Today);
            var at = index - DayTarget;
            if ((uint)at < (uint)graph.Count)
            {
                Show(graph[at].Day);
            }
            return default;
        }
        if (Stepped(index, Shown, Today, Oldest) is { } wanted)
        {
            Show(wanted);
        }
        return default;
    }

    /// <summary>
    /// How tall the line is: the apps' a row for each of the day's apps (one saying there are none); today's and the
    /// history's their size's form, whatever the day (§603) — today's Compact the kit's row with its caption, its Full
    /// the day's name over the total large with room for the average under it; the history's its bars' band with the
    /// air over it, the shown day's mark and the legend under it.
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var compact = LineSizes.Of(Settings) == LineSize.Compact;
        switch (Which)
        {
            case ScreenTimePart.Apps:
                var count = Days.Log.Day(Shown)?.Ranked().Count ?? 0;
                return RowHeight(style) * Math.Clamp(count, 1, FolderOptions.MostCells / LineHeight.Row);
            case ScreenTimePart.History:
                return BarsAir(style) + style.Px(compact ? CompactBarsDips : FullBarsDips) + LegendRoom(style, form) + BarsFoot(style, form);
            default:
                return compact
                    ? LineRow.Height(style, caption: true)
                    : form.Pad + form.Line + TilePaint.LineHeight(Gfx, ListTiers.HeroPx(style), Weight.Semibold) + form.SmallLine + form.Pad;
        }
    }

    /// <summary>The history's air over its bars, px.</summary>
    private static float BarsAir(in TileStyle style) => style.Px(8);

    /// <summary>Under the bars: the gap the shown day's mark stands in, and the legend's line.</summary>
    private static float LegendRoom(in TileStyle style, in ListForm form) => style.Px(6) + form.SmallLine;

    /// <summary>Under the legend, to the line's foot.</summary>
    private static float BarsFoot(in TileStyle style, in ListForm form) => MathF.Min(form.Pad, style.Px(10));

    /// <summary>The apps: every one of the day's, a row each, where there are more than the line holds (ListScroll.cs). The total does not scroll.</summary>
    public float ScrollHeight(float width, float height, in TileStyle style)
    {
        if (Which != ScreenTimePart.Apps)
        {
            return 0;
        }
        var count = Days.Log.Day(Shown)?.Ranked().Count ?? 0;
        return count > 1 ? count * RowHeight(style) : 0;
    }

    /// <summary>A list row — 32, 40 or 48 DIP as the item style is Compact, Standard or Large: the height every app's row is, and a line of one row.</summary>
    private float RowHeight(in TileStyle style) =>
        PopupPaint.Round(ItemStyles.Of(Settings).Size switch { ItemSize.Compact => 32, ItemSize.Large => 48, _ => 40 } * style.Scale * style.TextScale);

    // --- The total --------------------------------------------------------------------------------

    /// <summary>One run of words the total's line draws, and where.</summary>
    internal readonly record struct Words(string Text, float Px, Weight Weight, bool Quiet, float Left, float Top, float Room, bool Right = false);

    /// <summary>A piece of a day's bar: a top app's time (its place in <see cref="ScreenTimeChart.Top"/>), or the rest (<see cref="OtherPart"/>).</summary>
    internal readonly record struct Segment(int Part, D2D_RECT_F Box);

    /// <summary>A day of the bars: its place in the graph's days, the column a press on it lands in, its bar, and the bar's pieces from the foot up.</summary>
    internal sealed record BarPlace(int Index, DateOnly Day, D2D_RECT_F Column, D2D_RECT_F Bar, Segment[] Segments);

    /// <summary>One entry of the legend: whose colour, its swatch and its name.</summary>
    internal readonly record struct LegendItem(int Part, D2D_RECT_F Swatch, Words Name);

    /// <summary>The part of a bar that is no top app's: smaller apps, time for no app, a day not counted by app.</summary>
    internal const int OtherPart = -1;

    /// <summary>Today's line or the history's laid out: what it draws, where a press lands, the bars' place.</summary>
    internal sealed class TotalLayout
    {
        /// <summary>Today at its Full size: the day's name with its steps.</summary>
        public bool Steps { get; init; }

        /// <summary>Where the glyph stands; empty for the history, which has none.</summary>
        public D2D_RECT_F GlyphBox { get; init; }

        public float GlyphPx { get; init; }

        public List<Words> Said { get; } = [];

        public D2D_RECT_F Previous { get; init; }

        public D2D_RECT_F Next { get; init; }

        public bool CanGoBack { get; init; }

        public bool CanGoOn { get; init; }

        /// <summary>The day's name, which goes back to today where it is another day; empty where it does not.</summary>
        public D2D_RECT_F Title { get; init; }

        /// <summary>The history's bars by app: the chart shown; null for today's line, and for a history with no day counted.</summary>
        public ScreenTimeChart? Chart { get; init; }

        /// <summary>Where the bars stand, their foot its bottom.</summary>
        public D2D_RECT_F ChartBox { get; init; }

        /// <summary>Each top app's colour, by its place in the palette (<see cref="ScreenTimeColours"/>).</summary>
        public int[] Slots { get; set; } = [];

        /// <summary>The chart's days, oldest first: each day's column, its bar and the bar's pieces.</summary>
        public List<BarPlace> Bars { get; } = [];

        /// <summary>The top apps and "Other" under the bars, as many as fit on one line.</summary>
        public List<LegendItem> Legend { get; } = [];

        /// <summary>How far under a piece a press still lands on it: the gap under it, so the gap is no hole.</summary>
        public float Reach { get; set; }

        /// <summary>The target a press at <paramref name="point"/> lands on: the first of <see cref="Targets"/> it is in.</summary>
        public int TargetAt(Vector2 point) => LineFocus.At(Targets(), point);

        /// <summary>
        /// Every target and where a press lands on it, the first a point is in its target (ILineTargets): the steps,
        /// the day's name, then each bar's top apps' pieces — down to the gap under each — before the rest of its
        /// column, which is the day's.
        /// </summary>
        public List<(int Target, D2D_RECT_F Box)> Targets()
        {
            var targets = new List<(int Target, D2D_RECT_F Box)>();
            void Add(int target, D2D_RECT_F at)
            {
                if (at.right > at.left && at.bottom > at.top)
                {
                    targets.Add((target, at));
                }
            }
            if (CanGoBack)
            {
                Add(PreviousTarget, Previous);
            }
            if (CanGoOn)
            {
                Add(NextTarget, Next);
            }
            Add(TodayTarget, Title);
            foreach (var bar in Bars)
            {
                // A top app's piece is that app's; the rest of the column, "Other" too, the day's.
                var column = bar.Column;
                var free = column.bottom;
                foreach (var segment in bar.Segments.Where(segment => segment.Part >= 0))
                {
                    var reach = segment.Box with { bottom = segment.Box.bottom + Reach };
                    var piece = new D2D_RECT_F(MathF.Max(reach.left, column.left), MathF.Max(reach.top, column.top), MathF.Min(reach.right, column.right), MathF.Min(reach.bottom, column.bottom));
                    Add(SegmentTarget + (bar.Index * SegmentStride) + segment.Part, piece);
                    free = MathF.Min(free, piece.top);
                }
                // The day's first where no piece is (above them all: the keys' stop for the day), then its whole column.
                Add(DayTarget + bar.Index, column with { bottom = free });
                Add(DayTarget + bar.Index, column);
            }
            return targets;
        }
    }

    /// <summary>The line in <paramref name="box"/>: today's at its size, or the history's bars (see the note at the top).</summary>
    internal TotalLayout TotalPlan(D2D_RECT_F box, in TileStyle style) =>
        Which == ScreenTimePart.History ? HistoryPlan(box, style) : TodayPlan(box, style);

    /// <summary>
    /// The history: the bars in the box but for the air over them, the mark's gap and the legend's line under them
    /// (none in high contrast, where the apps are not told apart by colour) and the foot.
    /// </summary>
    private TotalLayout HistoryPlan(D2D_RECT_F box, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var days = Days;
        var legend = !style.Theme.HighContrast;
        var chartTop = box.top + BarsAir(style);
        var chartBottom = box.bottom - BarsFoot(style, form) - (legend ? LegendRoom(style, form) : style.Px(6));
        var chartBox = new D2D_RECT_F(box.left + form.Inset, chartTop, box.right - form.Inset, MathF.Max(chartTop, chartBottom));
        var chart = GraphOf(days.Log, Shown, days.Today).Count > 0 ? ChartOf(days.Log, Shown, days.Today) : null;
        if (chart is null)
        {
            // Nothing counted yet: the kit's Status on its top row, the screen's glyph quiet and "No screen time", at the
            // size's height all the same (as the apps' "No apps").
            var slot = MathF.Min(LineRows.Px(1, style), MathF.Max(TilePaint.Height(box), form.Line));
            var empty = new TotalLayout { GlyphBox = form.IconBox(box.left, box.top, slot), GlyphPx = form.Glyph };
            empty.Said.Add(new Words(S.WidgetScreenTimeNothing, form.TextPx, Weight.Regular, true, box.left + form.TextLeft,
                box.top + PopupPaint.Round((slot - form.Line) / 2), form.Room(TilePaint.Width(box))));
            return empty;
        }
        var layout = new TotalLayout { Chart = chart, ChartBox = chartBox };
        Bars(layout, chart, style, legend ? chartBox.bottom + style.Px(6) : null);
        return layout;
    }

    /// <summary>Today's line at its size (see the note at the top): the same words at both, never a graph.</summary>
    private TotalLayout TodayPlan(D2D_RECT_F box, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var days = Days;
        var shown = Shown;
        var today = days.Today;
        var total = days.Log.TotalOn(shown);
        // An app picked in the folder: the figure is its time that day, under its name, and the
        // day's total what it is part of ("of 6 h 48 min") where the daily average would be.
        var picked = Selected;
        var pickedName = picked is { } app ? ScreenTimeNames.Of(app.Key).Name : null;
        var figure = picked is { } chosen ? TimeOf(days.Log, shown, chosen) : total;
        var value = ScreenTimeText.Duration(figure);
        var average = picked is not null ? S.WidgetScreenTimeOfTotal(total: ScreenTimeText.Duration(total))
            : AverageOf(days.Log, shown) is { } mean ? ScreenTimeText.Duration(mean) : null;
        var left = box.left + form.TextLeft;
        var right = box.right - form.Inset;
        var glyphPx = LineGlyph.Row(style);

        if (LineSizes.Of(Settings) == LineSize.Compact)
        {
            // The kit's row with its caption: the day's name over the average — an app picked, its name and the day's
            // total it is part of —, the total (the app's time) at the trailing gutter, in the middle of the row's three cells.
            // The total in the Value where it leaves the name half the row, else the largest of the ramp that does (a
            // narrow list at a large text), so the row never says the figure alone.
            var room = right - left;
            var valuePx = LineType.Largest(style, ListTiers.ValuePx(style), px => PopupPaint.Measure(Gfx, value, px, Weight.Semibold).X <= room / 2);
            var valueLine = TilePaint.LineHeight(Gfx, valuePx, Weight.Semibold);
            var height = LineRow.Height(style, caption: true);
            var middle = box.top + (height / 2);
            // The total's own width, not the widest a total can be: "88 h 88 min" at the largest text
            // left the name no room at all, and a total changes its width once a minute at most.
            var field = MathF.Min(PopupPaint.Measure(Gfx, value, valuePx, Weight.Semibold).X, right - left);
            var nameRoom = MathF.Max(1, right - field - style.Gap - left);
            var names = DayNames(shown);
            var caption = pickedName is { } named ? S.WidgetScreenTimeAppOfTotal(name: named, total: ScreenTimeText.Duration(total))
                : average is { } said ? $"{S.WidgetScreenTimeAverage} {said}" : null;
            var block = form.Line + (caption is not null ? form.SmallLine : 0);
            var top = PopupPaint.Round(middle - (block / 2));
            var layout = new TotalLayout
            {
                GlyphBox = form.IconBox(box.left, PopupPaint.Round(middle - (form.Line / 2)), form.Line), GlyphPx = glyphPx,
            };
            if (ListTiers.Name(Gfx, names, form.TextPx, Weight.Regular, nameRoom) is { } name)
            {
                layout.Said.Add(new Words(name, form.TextPx, Weight.Regular, false, left, top, nameRoom));
            }
            if (caption is not null)
            {
                layout.Said.Add(new Words(caption, form.SmallPx, Weight.Regular, true, left, top + form.Line, nameRoom));
            }
            layout.Said.Add(new Words(value, valuePx, Weight.Semibold, figure <= TimeSpan.Zero, right, PopupPaint.Round(middle - (valueLine / 2)), field, Right: true));
            return layout;
        }

        // Full: the day's name with the steps at the trailing gutter, the total large under it with the average
        // beside it, its label over its figure, or under it where the width leaves it no room beside.
        {
            var top = box.top + form.Pad;
            // The Hero, or the largest of the ramp under it at which the total stands whole across the line; its line
            // the Hero's all the same, so the height is the size's whatever the text.
            var bigPx = LineType.Largest(style, ListTiers.HeroPx(style), px => PopupPaint.Measure(Gfx, ScreenTimeText.DurationShape, px, Weight.Semibold).X <= right - left);
            var bigLine = TilePaint.LineHeight(Gfx, ListTiers.HeroPx(style), Weight.Semibold);
            // The steps: the lines' one button (LineButton), subtle.
            var buttons = LineButton.Of(Gfx, style);
            var button = buttons.Height;
            var buttonTop = top + PopupPaint.Round((form.Line - button) / 2);
            var next = PopupPaint.Rect(right - button, buttonTop, button, button);
            var previous = PopupPaint.Rect(next.left - buttons.Gap - button, buttonTop, button, button);
            var titleRoom = MathF.Max(1, previous.left - style.Gap - left);
            var title = ListTiers.Name(Gfx, DayNames(shown), form.TextPx, Weight.Semibold, titleRoom);
            var titleWidth = title is null ? 0 : MathF.Min(titleRoom, PopupPaint.Measure(Gfx, title, form.TextPx, Weight.Semibold).X);
            var y = top + form.Line;
            var bigField = MathF.Min(PopupPaint.Measure(Gfx, ScreenTimeText.DurationShape, bigPx, Weight.Semibold).X, right - left);
            var label = pickedName ?? S.WidgetScreenTimeAverage;
            var labelWidth = PopupPaint.Measure(Gfx, label, form.SmallPx, Weight.Regular).X;
            // The figure's widest shape, so it never moves as the minutes change.
            var figureWidth = average is null ? 0
                : PopupPaint.Measure(Gfx, picked is null ? ScreenTimeText.DurationShape : S.WidgetScreenTimeOfTotal(total: ScreenTimeText.DurationShape), form.SmallPx, Weight.Regular).X;
            var besideRoom = right - (left + PopupPaint.Measure(Gfx, value, bigPx, Weight.Semibold).X + style.Gap);
            // An app's name is cut to the room its figure leaves, rather than pushing the two under.
            if (pickedName is not null)
            {
                labelWidth = MathF.Min(labelWidth, MathF.Max(figureWidth, besideRoom));
            }
            var beside = average is not null && MathF.Max(labelWidth, figureWidth) <= besideRoom && (2 * form.SmallLine) <= bigLine + style.Px(4);
            var layout = new TotalLayout
            {
                Steps = true,
                GlyphBox = form.IconBox(box.left, top, form.Line), GlyphPx = glyphPx,
                Previous = previous, Next = next,
                CanGoBack = Stepped(PreviousTarget, shown, today, Oldest) is not null,
                CanGoOn = Stepped(NextTarget, shown, today, Oldest) is not null,
                Title = shown != today && title is not null ? PopupPaint.Rect(left - style.Px(4), buttonTop, titleWidth + style.Px(8), button) : default,
            };
            if (title is not null)
            {
                layout.Said.Add(new Words(title, form.TextPx, Weight.Semibold, false, left, top, titleRoom));
            }
            var column = MathF.Max(labelWidth, figureWidth);
            var valueTop = y + PopupPaint.Round((bigLine - TilePaint.LineHeight(Gfx, bigPx, Weight.Semibold)) / 2);
            layout.Said.Add(new Words(value, bigPx, Weight.Semibold, figure <= TimeSpan.Zero, left, valueTop, beside ? MathF.Max(1, MathF.Min(bigField, right - column - style.Gap - left)) : bigField));
            if (beside)
            {
                // The average's label is quiet over its figure; an app's name is what the figure is
                // for, and the day's total it is part of is the quiet one under it.
                var detailTop = y + PopupPaint.Round((bigLine - (2 * form.SmallLine)) / 2);
                layout.Said.Add(new Words(label, form.SmallPx, Weight.Regular, pickedName is null, right, detailTop, column, Right: true));
                layout.Said.Add(new Words(average!, form.SmallPx, Weight.Regular, pickedName is not null, right, detailTop + form.SmallLine, column, Right: true));
            }
            else if (average is not null)
            {
                var under = pickedName is { } named ? S.WidgetScreenTimeAppOfTotal(name: named, total: ScreenTimeText.Duration(total)) : $"{S.WidgetScreenTimeAverage} {average}";
                layout.Said.Add(new Words(under, form.SmallPx, Weight.Regular, true, left, y + bigLine, right - left));
            }
            return layout;
        }
    }

    /// <summary>
    /// The chart's bars in <paramref name="layout"/>'s chart box: the fortnight's days in as many
    /// columns, the last day at the trailing end, each day's bar as tall as its total and split from
    /// the foot up by the top apps, most used first, then the rest; and the legend at
    /// <paramref name="legendTop"/>, where there is one.
    /// </summary>
    private void Bars(TotalLayout layout, ScreenTimeChart chart, in TileStyle style, float? legendTop)
    {
        var form = ListForm.Of(Gfx, style);
        layout.Slots = SlotsOf(chart);
        layout.Reach = MathF.Max(1, style.Px(1));
        var area = layout.ChartBox;
        var slot = (area.right - area.left) / GraphDays;
        var width = MathF.Min(MathF.Max(1, MathF.Floor(slot) - 1), Math.Clamp(PopupPaint.Round(slot * 0.6f), MathF.Max(2, style.Px(4)), style.Px(18)));
        var height = area.bottom - area.top;
        var full = chart.FullHours;
        var gap = MathF.Max(1, style.Px(1));
        var first = GraphDays - chart.Bars.Count;
        var times = new List<(int Part, TimeSpan Time)>(chart.Top.Count + 1);
        for (var index = 0; index < chart.Bars.Count; index++)
        {
            var bar = chart.Bars[index];
            var x = area.left + ((first + index) * slot);
            var column = new D2D_RECT_F(PopupPaint.Round(x), area.top, PopupPaint.Round(x + slot), area.bottom);
            var left = PopupPaint.Round(x + ((slot - width) / 2));
            var tall = bar.Total > TimeSpan.Zero ? MathF.Min(height, MathF.Max(style.Px(2), PopupPaint.Round((float)bar.Total.TotalHours / full * height))) : 0;
            times.Clear();
            for (var part = 0; part < bar.Parts.Length; part++)
            {
                times.Add((part, bar.Parts[part]));
            }
            times.Add((OtherPart, bar.Other));
            var segments = new List<Segment>(times.Count);
            var below = area.bottom;
            var counted = TimeSpan.Zero;
            foreach (var (part, time) in times)
            {
                if (time <= TimeSpan.Zero || bar.Total <= TimeSpan.Zero)
                {
                    continue;
                }
                counted += time;
                var top = PopupPaint.Round(area.bottom - (tall * (float)(counted.TotalSeconds / bar.Total.TotalSeconds)));
                // The gap between two pieces is the upper one's; a piece too thin for its gap is
                // not drawn, and its time is in the bar's height all the same.
                var foot = below - (below < area.bottom ? gap : 0);
                if (foot - top >= 1)
                {
                    segments.Add(new Segment(part, new D2D_RECT_F(left, top, left + width, foot)));
                }
                below = top;
            }
            layout.Bars.Add(new BarPlace(index, bar.Day, column, new D2D_RECT_F(left, area.bottom - tall, left + width, area.bottom), [.. segments]));
        }
        if (legendTop is not { } y)
        {
            return;
        }
        // The legend: the top apps and the rest, as many as fit on the line; the first is shortened
        // where it alone does not.
        var swatch = MathF.Max(4, style.Px(8));
        var air = style.Px(4);
        var apart = style.Px(12);
        var at = area.left;
        var entries = chart.Top.Select((app, part) => (Part: part, Name: ScreenTimeNames.Of(app).Name)).ToList();
        if (chart.Bars.Any(bar => bar.Other > TimeSpan.Zero))
        {
            entries.Add((OtherPart, S.WidgetScreenTimeOther));
        }
        foreach (var (part, name) in entries)
        {
            var room = area.right - at - swatch - air;
            var wide = PopupPaint.Measure(Gfx, name, form.SmallPx, Weight.Regular).X;
            if (wide > room && (layout.Legend.Count > 0 || room < 3 * swatch))
            {
                break;
            }
            var text = MathF.Min(wide, room);
            var square = PopupPaint.Rect(at, y + PopupPaint.Round((form.SmallLine - swatch) / 2), swatch, swatch);
            layout.Legend.Add(new LegendItem(part, square, new Words(name, form.SmallPx, Weight.Regular, true, at + swatch + air, y, text)));
            at += swatch + air + text + apart;
        }
    }

    /// <summary>
    /// A part's colour, the bars' and the apps' line's alike: a top app's its place in the palette
    /// (<paramref name="slots"/>), the rest neutral; in high contrast the text's, the shown day the
    /// highlight's.
    /// </summary>
    internal static DXGI_RGBA PartInk(int[] slots, int part, bool shown, in TileStyle style)
    {
        var theme = style.Theme;
        if (theme.HighContrast)
        {
            return shown ? PopupPaint.Ink(theme.Accent) : part == OtherPart ? theme.TextSecondary : theme.Text;
        }
        var light = ScreenTimeColours.LightText(theme.Text);
        return part < 0 || part >= slots.Length
            ? (light ? Gfx.Ink(1, 1, 1, 0.3f) : Gfx.Ink(0, 0, 0, 0.24f))
            : ScreenTimeColours.Colour(slots[part], light);
    }

    /// <summary>How the bars draw an app picked or pointed at (<see cref="EmphasisOf"/>).</summary>
    internal readonly record struct Emphasis(int Picked, int Pointed, bool Any)
    {
        /// <summary>Whether <paramref name="part"/> is the app picked or the one pointed at.</summary>
        public bool On(int part) => Any && part != NoPart && (part == Picked || part == Pointed);

        /// <summary>
        /// How much of its colour a piece of <paramref name="part"/> keeps, on the day shown
        /// (<paramref name="shown"/>) or another: the app picked or pointed at all of it on every day
        /// ("light up across all bars"); the rest dimmed — a little for the pointer, more for a pick;
        /// and with neither, the day shown whole and the others at half, as ever.
        /// </summary>
        public float Alpha(int part, bool shown) =>
            !Any ? (shown ? 1 : 0.5f)
            : On(part) ? 1
            : Picked != NoPart ? (shown ? 0.3f : 0.16f)
            : shown ? 0.6f : 0.32f;
    }

    /// <summary>No part of the chart: nobody picked or points at an app.</summary>
    internal const int NoPart = int.MinValue;

    /// <summary>
    /// The parts of <paramref name="chart"/> the app picked and the app pointed at are: a top app's
    /// place, or <see cref="OtherPart"/> for an app inside "Other", whose piece then stands for it.
    /// </summary>
    internal Emphasis EmphasisOf(ScreenTimeChart chart)
    {
        var picked = Selected is { } app ? PartOf(chart, app.Key) : NoPart;
        var pointed = Hovered is { } at ? PartOf(chart, at.Key) : NoPart;
        return new Emphasis(picked, pointed, picked != NoPart || pointed != NoPart);
    }

    /// <summary>
    /// The bars: the day under the pointer lit, each bar's top corners round, every day but the one
    /// shown faded — or, with an app picked or pointed at, that app's pieces whole across the bars
    /// and the rest dimmed (<see cref="Emphasis"/>), its legend entry said in the text's colour. In
    /// high contrast the app's pieces are the highlight and the rest grey.
    /// </summary>
    private void PaintBars(ID2D1DeviceContext* dc, TotalLayout layout, in TileStyle style)
    {
        var theme = style.Theme;
        var shown = Shown;
        var area = layout.ChartBox;
        var chart = layout.Chart!;
        var emphasis = EmphasisOf(chart);
        var lit = DayIndexOf(hot);
        PopupPaint.Divider(dc, area.left, area.right, area.bottom, MathF.Max(1, style.Px(1)), PopupPaint.Ink(theme.Divider));
        var copy = style;
        foreach (var bar in layout.Bars)
        {
            if (lit == bar.Index)
            {
                // In high contrast ringed rather than filled: the hover's fill is the highlight, which
                // the pieces of the app lit are too.
                if (theme.HighContrast)
                {
                    PopupPaint.Stroke(dc, bar.Column, style.Px(4), MathF.Max(1, style.Px(1)), PopupPaint.Ink(theme.Accent));
                }
                else
                {
                    PopupPaint.Fill(dc, bar.Column, style.Px(4), PopupPaint.Ink(theme.SubtleHover));
                }
            }
            var isShown = bar.Day == shown;
            if (isShown)
            {
                // The day shown marked under its bar in the accent, as a bar too short to see (or a
                // day without any) is marked all the same.
                var tick = MathF.Max(2, style.Px(2));
                PopupPaint.Fill(dc, new D2D_RECT_F(bar.Bar.left - style.Px(2), area.bottom + style.Px(3), bar.Bar.right + style.Px(2), area.bottom + style.Px(3) + tick), tick / 2, PopupPaint.Ink(theme.Accent));
            }
            if (bar.Segments.Length == 0)
            {
                continue;
            }
            // An older day counted by no app is one piece of "Other": no app's at all.
            var split = chart.Bars[bar.Index].Split;
            var radius = MathF.Min(style.Px(3), (bar.Bar.right - bar.Bar.left) / 2);
            var clip = new D2D_RECT_F(bar.Bar.left, bar.Segments[^1].Box.top, bar.Bar.right, area.bottom + radius);
            Layered(dc, clip, radius, emphasis.Any || isShown || theme.HighContrast ? 1 : 0.5f, () =>
            {
                foreach (var segment in bar.Segments)
                {
                    var part = split ? segment.Part : NoPart;
                    PopupPaint.Fill(dc, segment.Box, 0, SegmentInk(layout.Slots, part, segment.Part, isShown, emphasis, copy));
                }
            });
        }
        foreach (var item in layout.Legend)
        {
            var ink = PartInk(layout.Slots, item.Part, false, style);
            if (emphasis.Any && !emphasis.On(item.Part))
            {
                ink.a *= emphasis.Alpha(item.Part, true);
            }
            PopupPaint.Fill(dc, item.Swatch, style.Px(2), ink);
            TilePaint.Line(dc, Gfx, item.Name.Text, item.Name.Px, item.Name.Weight, emphasis.On(item.Part) ? theme.Text : theme.TextSecondary, item.Name.Left, item.Name.Top, item.Name.Room);
        }
    }

    /// <summary>
    /// A piece's colour: its part's (<see cref="PartInk"/>), at what <paramref name="emphasis"/>
    /// leaves it inside a bar already drawn whole. <paramref name="part"/> is what it is for the
    /// emphasis — none for an older day's unsplit bar — and <paramref name="colour"/> what it is
    /// drawn as.
    /// </summary>
    internal static DXGI_RGBA SegmentInk(int[] slots, int part, int colour, bool shown, Emphasis emphasis, in TileStyle style)
    {
        if (!emphasis.Any)
        {
            return PartInk(slots, colour, shown, style);
        }
        var theme = style.Theme;
        if (theme.HighContrast)
        {
            return emphasis.On(part) ? PopupPaint.Ink(theme.Accent) : theme.TextDisabled;
        }
        var ink = PartInk(slots, colour, shown, style);
        ink.a *= emphasis.Alpha(part, shown);
        return ink;
    }

    /// <summary>
    /// What a day of the bars says under the pointer: an app's piece its name and time, the rest
    /// "Other" and its time, and elsewhere in the day's column (or on a day not counted by app) the
    /// day and its total.
    /// </summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (Which != ScreenTimePart.History)
        {
            return null;
        }
        var layout = TotalPlan(box, style);
        if (layout.Chart is not { } chart)
        {
            return null;
        }
        foreach (var place in layout.Bars)
        {
            if (!Inside(place.Column, point))
            {
                continue;
            }
            var bar = chart.Bars[place.Index];
            if (bar.Split)
            {
                foreach (var segment in place.Segments)
                {
                    // A piece reaches down over the gap under it, so the gap is no hole in the tooltips.
                    if (point.Y >= segment.Box.top && point.Y < segment.Box.bottom + MathF.Max(1, style.Px(1)))
                    {
                        var (name, time) = segment.Part == OtherPart
                            ? (S.WidgetScreenTimeOther, bar.Other)
                            : (ScreenTimeNames.Of(chart.Top[segment.Part]).Name, bar.Parts[segment.Part]);
                        return (S.WidgetScreenTimeTip(name: name, time: ScreenTimeText.Duration(time)), segment.Box);
                    }
                }
            }
            var today = Today;
            var day = ScreenTimeText.DayName(place.Day, today, date => Panels.Nls.DayAndShortDate(date, year: date.Year != today.Year));
            return (S.WidgetScreenTimeTip(name: day, time: ScreenTimeText.Duration(bar.Total)), place.Column);
        }
        return null;
    }

    /// <summary><paramref name="paint"/> clipped to <paramref name="box"/> with round corners, at <paramref name="opacity"/> as a whole, so pieces that touch never show a seam.</summary>
    private static void Layered(ID2D1DeviceContext* dc, D2D_RECT_F box, float radius, float opacity, Action paint)
    {
        ID2D1Factory* factory = null;
        ID2D1RoundedRectangleGeometry* shape = null;
        var layered = false;
        try
        {
            dc->GetFactory(&factory);
            var rounded = new D2D1_ROUNDED_RECT { rect = box, radiusX = radius, radiusY = radius };
            if (factory != null && factory->CreateRoundedRectangleGeometry(&rounded, &shape).SUCCEEDED)
            {
                var parameters = new D2D1_LAYER_PARAMETERS1
                {
                    contentBounds = box,
                    geometricMask = (ID2D1Geometry*)shape,
                    maskAntialiasMode = D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_PER_PRIMITIVE,
                    maskTransform = D2D_MATRIX_3X2_F.Identity,
                    opacity = opacity,
                    opacityBrush = null,
                    layerOptions = D2D1_LAYER_OPTIONS1.D2D1_LAYER_OPTIONS1_NONE,
                };
                dc->PushLayer(&parameters, null);
                layered = true;
            }
            paint();
        }
        finally
        {
            if (layered)
            {
                dc->PopLayer();
            }
            ComUtil.Release(ref shape);
            ComUtil.Release(ref factory);
        }
    }

    private void PaintTotal(ID2D1DeviceContext* dc, TotalLayout layout, in TileStyle style)
    {
        var theme = style.Theme;
        if (layout.Chart is not null)
        {
            PaintBars(dc, layout, style);
        }
        if (Which == ScreenTimePart.History && layout.Chart is not null)
        {
            return;
        }
        // The history with nothing counted says so, its glyph quiet as the apps' "No apps".
        PopupPaint.Glyph(dc, Gfx, Glyph, layout.GlyphPx, Which == ScreenTimePart.History ? theme.TextTertiary : theme.Text, layout.GlyphBox);
        if (layout.Steps)
        {
            var radius = style.Px(4);
            if (hot == TodayTarget && layout.Title.right > layout.Title.left)
            {
                PopupPaint.Fill(dc, layout.Title, radius, PopupPaint.Ink(theme.SubtleHover));
            }
            var buttons = LineButton.Of(Gfx, style);
            foreach (var (target, rect, can, glyph) in new[] { (PreviousTarget, layout.Previous, layout.CanGoBack, PreviousGlyph), (NextTarget, layout.Next, layout.CanGoOn, NextGlyph) })
            {
                buttons.Paint(dc, Gfx, theme, rect, "", glyph, ButtonStyle.Subtle, can && hot == target, enabled: can);
            }
        }
        foreach (var words in layout.Said)
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
    }

    // --- The apps ---------------------------------------------------------------------------------

    /// <summary>
    /// One app's row: its place, its band (LineItem) the pointer lights, its logo's box, its words, its bar,
    /// and the bar's colour as the chart's part (its place in the chart's top apps, or <see cref="OtherPart"/>).
    /// </summary>
    internal readonly record struct AppRow(int Index, string App, D2D_RECT_F Box, D2D_RECT_F Icon, Words Name, Words Time, D2D_RECT_F Bar, float Share, int Part);

    /// <summary>The apps' line laid out.</summary>
    internal sealed class AppsLayout
    {
        public List<AppRow> Rows { get; } = [];

        /// <summary>The fortnight chart's colours (<see cref="TotalLayout.Slots"/>), which the rows' parts index.</summary>
        public int[] Slots { get; set; } = [];

        /// <summary>"3 other apps" in the slot after the rows, or "No apps" where the day has none.</summary>
        public Words? Said { get; set; }

        /// <summary>The quiet glyph beside "No apps".</summary>
        public D2D_RECT_F Empty { get; set; }

        public float EmptyPx { get; set; }

        /// <summary>The target a press at <paramref name="point"/> lands on: the row it is in.</summary>
        public int TargetAt(Vector2 point) => LineFocus.At(Targets(), point);

        /// <summary>Every row's target and its band (ILineTargets).</summary>
        public List<(int Target, D2D_RECT_F Box)> Targets() => [.. Rows.Select(row => (AppTarget + row.Index, row.Box))];
    }

    /// <summary>
    /// The day's apps on the list's own edges: as many rows of a list row each as the line holds,
    /// room left over empty at the foot — where it scrolls, it is as tall as all of them
    /// (ListScroll.cs); a line shorter than one row shows the first.
    /// </summary>
    internal AppsLayout AppsPlan(D2D_RECT_F box, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var items = ItemForm.For(Gfx, style, Settings);
        var layout = new AppsLayout();
        var ranked = Days.Log.Day(Shown)?.Ranked() ?? [];
        var width = TilePaint.Width(box);
        var height = TilePaint.Height(box);
        var row = RowHeight(style);
        var left = box.left + form.TextLeft;
        var right = box.right - form.Inset;
        if (ranked.Count == 0)
        {
            var slot = MathF.Min(row, MathF.Max(height, form.Line));
            layout.Empty = form.IconBox(box.left, box.top, slot);
            layout.EmptyPx = form.Glyph;
            layout.Said = new Words(S.WidgetScreenTimeNoApps, form.TextPx, Weight.Regular, true, left, box.top + PopupPaint.Round((slot - form.Line) / 2), form.Room(width));
            return layout;
        }
        // As many slots of a list row each as the line holds, never taller (the person, 2026-10-10:
        // the apps "should not" stretch into spare room): a line taller than its apps leaves the
        // rest empty under them, and a taller line shows more apps, not bigger ones.
        var shown = Math.Clamp((int)MathF.Floor((MathF.Max(height, row) + 0.5f) / row), 1, ranked.Count);
        var most = ranked[0].Time.TotalSeconds;
        // The times' column: as wide as the widest time shown, so the names and their bars end in one
        // place; the widest a time can be left a name no room at the largest text.
        var timeField = 0f;
        for (var index = 0; index < shown; index++)
        {
            timeField = MathF.Max(timeField, PopupPaint.Measure(Gfx, ScreenTimeText.Duration(ranked[index].Time), items.SmallPx, Weight.Regular).X);
        }
        var bar = MathF.Max(2, style.Px(3));
        var air = style.Px(4);
        var logo = MathF.Min(form.Icon, PopupPaint.Round(24 * style.Scale * style.TextScale));
        // Each bar in its app's colour on the fortnight's chart, which the total draws whether or not
        // it is in the folder: an app that is no top app there is in its "Other", and grey here too.
        var chart = ChartOf(Days.Log, Shown, Today);
        layout.Slots = SlotsOf(chart);
        for (var index = 0; index < shown; index++)
        {
            var (app, time) = ranked[index];
            var y = box.top + (index * row);
            var info = ScreenTimeNames.Of(app);
            var timeText = ScreenTimeText.Duration(time);
            var timeWidth = MathF.Min(PopupPaint.Measure(Gfx, timeText, items.SmallPx, Weight.Regular).X, timeField);
            var textRight = right - timeField - style.Gap;
            var withBar = items.Line + air + bar <= row - (2 * style.Px(4));
            var block = items.Line + (withBar ? air + bar : 0);
            var top = y + PopupPaint.Round((row - block) / 2);
            var name = ListTiers.Name(Gfx, [info.Name], items.TextPx, Weight.Regular, MathF.Max(1, textRight - left)) ?? "";
            var icon = form.IconBox(box.left, y, row);
            var square = PopupPaint.Rect(icon.left + PopupPaint.Round((icon.right - icon.left - logo) / 2), y + PopupPaint.Round((row - logo) / 2), logo, logo);
            layout.Rows.Add(new AppRow(
                index,
                app,
                LineItem.Band(box, y, y + row),
                square,
                new Words(name, items.TextPx, Weight.Regular, false, left, top, MathF.Max(1, textRight - left)),
                new Words(timeText, items.SmallPx, Weight.Regular, true, right, top + PopupPaint.Round((items.Line - items.SmallLine) / 2), MathF.Max(timeWidth, timeField), Right: true),
                withBar ? PopupPaint.Rect(left, top + items.Line + air, MathF.Max(1, textRight - left), bar) : default,
                most > 0 ? (float)(time.TotalSeconds / most) : 0,
                PartOf(chart, app)));
        }
        return layout;
    }

    /// <summary><paramref name="app"/>'s place among <paramref name="chart"/>'s top apps, or <see cref="OtherPart"/>.</summary>
    internal static int PartOf(ScreenTimeChart chart, string app)
    {
        for (var part = 0; part < chart.Top.Count; part++)
        {
            if (string.Equals(chart.Top[part], app, StringComparison.OrdinalIgnoreCase))
            {
                return part;
            }
        }
        return OtherPart;
    }

    /// <summary>How an app's row stands: as it is, under the pointer, picked, or picked and under the pointer.</summary>
    [Flags]
    internal enum RowState
    {
        Rest = 0,

        /// <summary>The pointer is on it, here or on its app in the folder's bars (<see cref="Channels.AppHover"/>).</summary>
        Hover = 1,

        /// <summary>It is the app picked (<see cref="Channels.App"/>).</summary>
        Selected = 2,
    }

    /// <summary>How <paramref name="row"/> stands now: lit for the pointer here or the folder's, marked where it is the app picked.</summary>
    internal RowState StateOf(AppRow row)
    {
        var app = new AppId(row.App);
        var state = hot == AppTarget + row.Index || Hovered == app ? RowState.Hover : RowState.Rest;
        return Selected == app ? state | RowState.Selected : state;
    }

    /// <summary>
    /// The rows, each a list's item edge to edge (LineItem): under the pointer a subtle fill; picked,
    /// the selected fill with the accent's pill at its leading edge, a little lighter under the
    /// pointer. In high contrast the row picked is the highlight with its text the highlight's text,
    /// and the row under the pointer ringed in the highlight.
    /// </summary>
    private void PaintApps(ID2D1DeviceContext* dc, AppsLayout layout, in TileStyle style)
    {
        var theme = style.Theme;
        var quick = QuickSettings.Controls.QuickTheme.For(theme);
        if (layout.Rows.Count == 0 && layout.EmptyPx > 0)
        {
            PopupPaint.Glyph(dc, Gfx, AppGlyph, layout.EmptyPx, theme.TextTertiary, layout.Empty);
        }
        foreach (var row in layout.Rows)
        {
            var state = StateOf(row);
            var picked = state.HasFlag(RowState.Selected);
            var pointed = state.HasFlag(RowState.Hover);
            var bright = theme.HighContrast && picked;
            if (theme.HighContrast)
            {
                if (picked)
                {
                    PopupPaint.Fill(dc, row.Box, 0, PopupPaint.Ink(theme.Accent));
                }
                else if (pointed)
                {
                    var thick = MathF.Max(1, style.Px(1));
                    PopupPaint.Stroke(dc, LineItem.Inside(row.Box, thick / 2), 0, thick, PopupPaint.Ink(theme.Accent));
                }
            }
            else if (picked)
            {
                LineItem.Chosen(dc, row.Box, quick, pointed);
            }
            else if (pointed)
            {
                LineItem.Light(dc, row.Box, quick, pressed: false);
            }
            if (picked && !theme.HighContrast)
            {
                LineItem.Pill(dc, row.Box, style, PopupPaint.Ink(theme.Accent));
            }
            if (ScreenTimeNames.Of(row.App).Logo is { } logo)
            {
                PopupPaint.Picture(dc, logo, row.Icon, 0);
            }
            else
            {
                PopupPaint.Glyph(dc, Gfx, AppGlyph, ListForm.Of(Gfx, style).Glyph, bright ? theme.TextOnAccent : theme.TextSecondary, row.Icon);
            }
            TilePaint.Line(dc, Gfx, row.Name.Text, row.Name.Px, row.Name.Weight, bright ? theme.TextOnAccent : theme.Text, row.Name.Left, row.Name.Top, row.Name.Room);
            TilePaint.LineRight(dc, Gfx, row.Time.Text, row.Time.Px, row.Time.Weight, bright ? theme.TextOnAccent : theme.TextSecondary, row.Time.Left, row.Time.Top, row.Time.Room);
            if (row.Bar.right > row.Bar.left)
            {
                // The shown day's apps, so in high contrast the shown day's highlight, as its bar is —
                // and on the highlight of the row picked, the highlight's text.
                TilePaint.Bar(dc, row.Bar, row.Share, TilePaint.Track(style), bright ? theme.TextOnAccent : PartInk(layout.Slots, row.Part, true, style));
            }
        }
        if (layout.Said is { } said)
        {
            TilePaint.Line(dc, Gfx, said.Text, said.Px, said.Weight, theme.TextSecondary, said.Left, said.Top, said.Room);
        }
    }

    private static bool Inside(D2D_RECT_F rect, Vector2 point) =>
        point.X >= rect.left && point.X < rect.right && point.Y >= rect.top && point.Y < rect.bottom;
}
