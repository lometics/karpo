// The top of the clock's flyout as a line of a list folder (docs\widgets.md §9, "Time and date"):
// the time large with its seconds, over the long date — what Karpo's clock flyout opened with,
// so the clock made of widgets (the clock preset) starts as the clock did.
//
// It is not the clock's own line (ClockTile), which is a list form of a clock widget: its glyph,
// the time at the list's figure size, a zone or a label, a face of the time on a tall line. This is
// the flyout's header: no glyph, the time at the flyout's 34 DIP in tabular figures on the list's
// gutter — sized once for the widest the region's format reads, so it never moves as the seconds
// go (TimeFit) — and the long date under it in the secondary ink, in the longest form that fits.
// The folder ticks it every second while it is open (FolderIsland.Seconds). A click searches the
// time in its zone in the browser, as the clock's line does (TimeSearch, §377).

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class TimeHeaderTile : CellIsland, IListContent, ILinePieces, IOpensOnceGone
{
    // The settings a click searches with (the app's engine), and the search it chose (§377).
    private Config? searchWith;
    private readonly TimeSearch search = new();

    public TimeHeaderTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    /// <summary>The folder ticks it each second (FolderIsland.Seconds); nothing does while it is closed.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        searchWith = config;
        Forget();
    }

    public override string? Tooltip(int index) => null;

    public Action<string> Opened
    {
        get => search.Opened;
        set => search.Opened = value;
    }

    /// <summary>The time in its zone searched for in the browser once the folder has gone, as the clock's line does (§377).</summary>
    public override IslandAction Click(int index)
    {
        search.Choose(searchWith, Settings.Clock?.TimeZone ?? "");
        return new IslandAction(View: IslandView.App);
    }

    /// <summary>The dock, once the folder has gone: opens the search a click chose, once.</summary>
    public void Open() => search.Open();

    public override bool MinuteChanged(Gfx gfx) => Redraw(gfx);

    protected override bool Sample(bool everything) => false;

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a list's line; on the bar the time is the clock.");

    /// <summary>The time, the date, and where each stands, in the line's px.</summary>
    private readonly record struct HeaderPlan(string Time, float TimePx, D2D_RECT_F TimeRow, string Date, float DatePx, D2D_RECT_F DateRow, float Height);

    /// <summary>
    /// The header in <paramref name="box"/>, as the clock flyout laid its face out: 12 DIP of air, the
    /// time's row, 2, the date's row 22, and 12 (the rows at the text's scale); both on the list's
    /// gutter, the block at the top of a line taller than it. The time is the kit's Display, a time that
    /// is the widget (§388), its row its line; the date the kit's Body. A box with no height is the
    /// measure (ListHeight). A line given less (no kind has a fewest, §166) first gives up its air, down
    /// to 4 DIP, then the date, then steps the time down the ramp to the row it has, centred in it:
    /// never a row cut through its glyphs. Where its widest shape does not fit across, it steps down too.
    /// <para>
    /// Its sizes (§432): Full is that block; Medium the time in the kit's Hero with 6 DIP of air, the date still
    /// under it; Compact one row, the time stepped down to it and no date. §442: Full and Medium are the whole cells
    /// that hold the time and the date with that air at the least, the air the rest shared over and under them, so
    /// the block fills its cells (Full five, 100 DIP; Medium four).
    /// </para>
    /// </summary>
    private HeaderPlan Lay(D2D_RECT_F box, in TileStyle style)
    {
        var size = LineSizes.Of(Settings);
        var gutter = ListForm.Gutter(style);
        var room = MathF.Max(1, box.right - box.left - (2 * gutter));
        var air = style.Px(size == LineSize.Medium ? 6 : 8);
        var display = style.Text(size == LineSize.Medium ? LineType.HeroDip : LineType.DisplayDip);
        var timeHeight = PopupPaint.Round(TilePaint.LineHeight(Gfx, display, Weight.Semibold));
        var dateHeight = PopupPaint.Round(style.Text(22));
        var gap = style.Px(2);
        var inner = timeHeight + gap + dateHeight;
        var height = LineRows.Whole(inner + (2 * air), style);
        air = PopupPaint.Round((height - inner) / 2);
        var block = height;
        var given = box.bottom - box.top;
        if (size == LineSize.Compact)
        {
            height = LineRows.Px(1, style);
            given = given > 0 ? MathF.Min(given, height) : height;
        }
        var timeCap = display;
        var showDate = true;
        if (given > 0 && given < block)
        {
            var least = style.Px(4);
            if (given >= (2 * least) + timeHeight + gap + dateHeight)
            {
                air = PopupPaint.Round((given - timeHeight - gap - dateHeight) / 2);
            }
            else
            {
                showDate = false;
                var row = MathF.Max(1, given - (2 * least));
                air = least;
                if (row < timeHeight)
                {
                    var gfx = Gfx;
                    timeCap = LineType.Largest(style, display, px => TilePaint.LineHeight(gfx, px, Weight.Semibold) <= row + 0.5f);
                    timeHeight = row;
                }
                else
                {
                    air = PopupPaint.Round((given - timeHeight) / 2);
                }
            }
        }
        // From the top: the block keeps its own air, and a taller line leaves the rest under it.
        var top = box.top;
        // In the clock's zone, the clock widget's time style's (§230): this PC's unless set.
        var zone = Settings.Clock?.TimeZone ?? "";
        var now = Clock.Read(zone, seconds: true);
        var shapes = TimeFit.Shapes(seconds: true, now.Time);
        var measure = Gfx;
        var timePx = LineType.Largest(style, timeCap, px => shapes.All(shape => PopupPaint.MeasureFigures(measure, shape, px, Weight.Semibold).X <= room));
        var datePx = style.Text(LineType.BodyDip);
        var day = DateOnly.FromDateTime(Clock.NowIn(zone));
        string[] dates = [now.Long, Panels.Nls.LongDateWithoutYear(day), Panels.Nls.DayAndShortDate(day, year: false), Panels.Nls.ShortDateWithoutYear(day)];
        var date = !showDate ? "" : dates.FirstOrDefault(text => PopupPaint.Measure(Gfx, text, datePx, Weight.Regular).X <= room) ?? dates[^1];
        var timeRow = PopupPaint.Rect(box.left + gutter, top + air, room, timeHeight);
        var dateRow = PopupPaint.Rect(box.left + gutter, timeRow.bottom + gap, room, showDate ? dateHeight : 0);
        return new HeaderPlan(now.Time, timePx, timeRow, date, datePx, dateRow, height);
    }
    public float ListHeight(float width, in TileStyle style) => Lay(PopupPaint.Rect(0, 0, width, 0), style).Height;

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Lay(box, style);
        var theme = style.Theme;
        var big = PopupPaint.MeasureFigures(Gfx, plan.Time, plan.TimePx, Weight.Semibold);
        PopupPaint.Figures(dc, Gfx, plan.Time, plan.TimePx, Weight.Semibold, theme.Text, plan.TimeRow.left,
            plan.TimeRow.top + PopupPaint.Round((plan.TimeRow.bottom - plan.TimeRow.top - big.Y) / 2));
        if (plan.Date.Length == 0)
        {
            return;
        }
        var size = PopupPaint.Measure(Gfx, plan.Date, plan.DatePx, Weight.Regular, plan.DateRow.right - plan.DateRow.left);
        PopupPaint.Text(dc, Gfx, plan.Date, plan.DatePx, Weight.Regular, theme.TextSecondary, plan.DateRow.left,
            plan.DateRow.top + PopupPaint.Round((plan.DateRow.bottom - plan.DateRow.top - size.Y) / 2), plan.DateRow.right - plan.DateRow.left);
    }

    public string MainPiece => "time";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var plan = Lay(box, style);
        var time = PopupPaint.MeasureFigures(Gfx, plan.Time, plan.TimePx, Weight.Semibold);
        var timePiece = new LinePiece("time", PopupPaint.Rect(plan.TimeRow.left, plan.TimeRow.top, time.X, plan.TimeRow.bottom - plan.TimeRow.top), plan.Time);
        if (plan.Date.Length == 0)
        {
            return [timePiece];
        }
        var date = PopupPaint.Measure(Gfx, plan.Date, plan.DatePx, Weight.Regular, plan.DateRow.right - plan.DateRow.left);
        return
        [
            timePiece,
            new("date", PopupPaint.Rect(plan.DateRow.left, plan.DateRow.top, date.X, plan.DateRow.bottom - plan.DateRow.top), plan.Date),
        ];
    }
}
