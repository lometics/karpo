// A month calendar as a line of a list folder (docs\widgets.md §9, "Month"), as the calendar of
// Windows 11's clock flyout draws it: the month and its year at the top with the two arrows that
// step it, the weekdays' shortest names from the day this region starts its weeks on, and six weeks
// — the days of the months either side quieter — so the line never changes height from one month to
// the next. Today is ringed in the accent; the day picked is filled with it.
//
// It is the first widget that publishes to a folder's channel (Karpo.Core\Channels.cs): a click on
// a day makes it the folder's day, and an agenda beside it shows that day's events. It follows the
// day too, always (§240), so two months in a folder agree and the one clicked in stands still; a
// day of its own is only for a month in no folder. The folder forgets the day when it closes, and
// the month comes back to today's.
//
// A day with events has a dot under its number for each calendar with something on it, in the
// calendar's colour, as the calendar flyout's month had them (2026-10-09, the calendar a preset folder:
// its month is this one). The events are the calendar lines' (CalendarLines.Store), and the month is
// woken when they change.
//
// **The weeks scroll** (§424, "the month calendar should scroll smoothly"), as Windows' flyout scrolls
// them (Calendar\MonthScroll.cs): a wheel's notch and the arrows glide to the month before or after,
// a touchpad moves them by what it says and they settle on the nearest month; the heading is the month
// most in view, the others' days quieter. In a folder the weeks are a strip of their own on the
// compositor (LineStrip.cs): drawn once, slid by a property set the compositor animates, drawn again
// only when the heading changes or the strip no longer holds what is coming into view. Anywhere else
// (a picture) the weeks are drawn where they stand.
//
// The pointer: a day under the pointer lights. The keyboard on the line: the arrows move the day
// picked by a day or a week, Page Up and Page Down by a month, Home back to today — each one
// published, as a click is, and the weeks glide to its month. Up and Down stay in the month shown and
// let go at its ends; the two arrows are the folder's stops beside the days (§471).

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;

namespace Karpo.Dock;

/// <summary>
/// A month laid out: the title's row, the two arrows, the weekdays' row and the 42 days' cells, in the
/// line's px. A day not shown in a line too short for six weeks has an empty cell; the weekdays' row
/// goes where not even it fits (<paramref name="WeekdaysShown"/>).
/// </summary>
internal sealed record MonthPlan(
    D2D_RECT_F Title,
    D2D_RECT_F Previous,
    D2D_RECT_F Next,
    IReadOnlyList<D2D_RECT_F> Weekdays,
    IReadOnlyList<D2D_RECT_F> Days,
    float Height,
    bool WeekdaysShown = true);

internal sealed unsafe class MonthTile : CellIsland, IListContent, ILinePieces, IStripLine, ILineTargets
{
    /// <summary>The weeks a month is drawn in, whatever it needs: Windows' own six, so nothing moves from month to month.</summary>
    internal const int Weeks = MonthScroll.Weeks;

    internal const int PreviousTarget = 1;
    internal const int NextTarget = 2;

    /// <summary>
    /// A day is <c>DayTarget</c> plus its DayNumber (<see cref="Target"/>): by what it is, never by its place, so a
    /// press and its release either side of a glide are on the same day.
    /// </summary>
    internal const int DayTarget = 100;

    /// <summary>The weeks the strip holds: the six in view and a dozen either side, so a glide of a month or two passes through it.</summary>
    internal const int StripWeeks = 30;

    /// <summary>The ms the scroll counts in: the high-resolution clock, a test's own where it sets one.</summary>
    internal static Func<long> Ticks = static () => (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);

    // Where the weeks stand, and the day picked when they were last put somewhere: once another widget picks
    // another day, they are that day's month again.
    private MonthScroll? scroll;
    private DateOnly steppedFrom;

    // The day this month picked, for a month that stands in no folder.
    private DateOnly? own;

    // What the pointer is on, and whether the keyboard has moved the day (which rings it).
    private int hot;
    private bool keyed;

    // Where the dots' events come from, and whether anybody has looked yet (the store is opened once).
    private readonly CalendarStore store = CalendarLines.Store;
    private bool looked;

    // §424: on the compositor. What slides the strip ("y", "top"), and what glides it; whether a host shows the strip
    // (and so the weeks glide rather than land), whether the strip is the host's to draw now, and the look's animations.
    private CompositionPropertySet? motion;
    private ScalarKeyFrameAnimation? slide;
    private CompositionEasingFunction? ease;
    private bool hosted;
    private bool hosting;
    private bool animations;

    // The strip last laid out: the line placed, its first week, and the px of a week "y" counts in.
    private Placing? stripPlaced;
    private int stripFirst;
    private float motionRow;

    // The heading last drawn: another at the quick tick is the line drawn again.
    private DateOnly drawnHeading;

    public MonthTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    /// <summary>The folder's tick at a new minute is enough: today changes at midnight.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        // Only to be woken when the events change: attaching reads nothing and opens nothing.
        store.Attach(this);
        Forget();
    }

    public override string? Tooltip(int index) => null;

    public override bool MinuteChanged(Gfx gfx) => Redraw(gfx);

    /// <summary>The store posted: what arrived is taken, and the redraw that follows shows its dots.</summary>
    protected override bool Sample(bool everything)
    {
        if (everything)
        {
            store.Deliver();
        }
        return false;
    }

    public override void Dispose()
    {
        store.Detach(this);
        slide?.Dispose();
        ease?.Dispose();
        motion?.Dispose();
        base.Dispose();
    }

    /// <summary>The first look asks the store for events, once (docs\widgets.md §11).</summary>
    private void Look()
    {
        if (looked)
        {
            return;
        }
        looked = true;
        store.Open();
    }

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a list's line; on the bar the calendar is the date.");

    /// <summary>The folder closed: the month it shows and the day it picked are this look's, and go with it.</summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            scroll = null;
            own = null;
            hot = 0;
            keyed = false;
            hosted = false;
            stripPlaced = null;
            turned = 0;
            motion?.StopAnimation("y");
        }
    }

    /// <summary>§357: the month the weeks were going to and the day picked, kept as the folder closes in edit mode.</summary>
    private sealed record Kept(DateOnly? Month, DateOnly SteppedFrom, DateOnly? Own);

    internal override object? ViewMemory() => new Kept(scroll?.Target, steppedFrom, own);

    internal override void RecallView(object memory)
    {
        if (memory is Kept kept)
        {
            (steppedFrom, own) = (kept.SteppedFrom, kept.Own);
            scroll = kept.Month is { } month ? new MonthScroll(month, Panels.Nls.FirstDayOfWeek()) : null;
            stripPlaced = null;
        }
    }

    // --- The day ------------------------------------------------------------------------------------

    private static DateOnly Today => DateOnly.FromDateTime(Clock.Now());

    private static long Now => Ticks();

    /// <summary>The day picked: the folder's where it follows it, else its own, else today.</summary>
    internal DateOnly Picked => Followed(Channels.Day) ?? own ?? Today;

    /// <summary>The month heading the weeks: the one most in view where they stand now.</summary>
    internal DateOnly Shown => Scroll.Heading(Now);

    /// <summary>The target of <paramref name="day"/>'s cell (<see cref="DayTarget"/>).</summary>
    internal static int Target(DateOnly day) => DayTarget + day.DayNumber;

    private static DateOnly First(DateOnly day) => new(day.Year, day.Month, 1);

    /// <summary>
    /// Where the weeks stand: kept while the day picked is the one they were put somewhere for, else at rest on the
    /// picked day's month — another widget picked a day, or the folder opened — as before the scroll was there.
    /// </summary>
    private MonthScroll Scroll
    {
        get
        {
            var picked = Picked;
            var first = Panels.Nls.FirstDayOfWeek();
            if (scroll is null || steppedFrom != picked || scroll.First != first)
            {
                scroll = new MonthScroll(First(picked), first);
                steppedFrom = picked;
                stripPlaced = null;
                Sync();
            }
            return scroll;
        }
    }

    /// <summary>
    /// Whether the weeks glide: a host shows them on the compositor, and the look animates. Not in a line too short
    /// for six weeks: the weeks it shows are the picked day's of the heading's month, which turns half-way.
    /// </summary>
    private bool Animate => hosted && animations && motion is not null && stripPlaced is { Rows: Weeks };

    // A line too short for six weeks: a touchpad's deltas not yet a notch.
    private int turned;

    /// <summary>Picks <paramref name="day"/>: kept, shown — the weeks gliding to its month — and published as the folder's day.</summary>
    internal void Pick(DateOnly day)
    {
        var weeks = Scroll;
        own = day;
        steppedFrom = day;
        Glided(weeks.To(First(day), Now, Animate));
        Publish(Channels.Day, day);
        Drawn();
    }

    /// <summary>Glides the weeks <paramref name="by"/> months on from where they are going; the day picked stays.</summary>
    internal void Step(int by)
    {
        Glided(Scroll.Step(by, Now, Animate));
        Drawn();
    }

    /// <summary>Drawn again wherever it shows: on the bar's surfaces it keeps, and by the folder it stands in.</summary>
    private void Drawn()
    {
        Redraw(Gfx);
        Redrawn();
    }

    /// <summary>
    /// The 42 days a month is drawn in: from the first day of the week its first day falls in, the
    /// week starting on <paramref name="first"/> (the region's), six weeks. Pure, for a test.
    /// </summary>
    internal static DateOnly[] Grid(DateOnly month, DayOfWeek first)
    {
        var start = First(month);
        var lead = ((int)start.DayOfWeek - (int)first + 7) % 7;
        var from = start.AddDays(-lead);
        return [.. Enumerable.Range(0, Weeks * 7).Select(from.AddDays)];
    }

    public override IslandAction Click(int index)
    {
        switch (index)
        {
            case PreviousTarget:
                Step(-1);
                break;
            case NextTarget:
                Step(1);
                break;
            case >= DayTarget:
                Pick(DateOnly.FromDayNumber(index - DayTarget));
                break;
        }
        return default;
    }

    /// <summary>The wheel's notches glide the weeks a month each: up for the one before, as Windows' calendar scrolls.</summary>
    protected internal override bool CardWheel(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface, int notches)
    {
        if (notches == 0)
        {
            return false;
        }
        Step(-notches);
        return true;
    }

    /// <summary>
    /// The wheel as it turned (WHEEL_DELTA a notch): whole notches glide a month each; a touchpad's fine deltas
    /// move the weeks by what they say — three lines of text a notch's worth, as a list's items move — and they
    /// settle on the nearest month once it stops.
    /// </summary>
    protected internal override bool CardWheelDelta(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface, int delta)
    {
        if (delta == 0)
        {
            return false;
        }
        var placed = Placed(box, style);
        if (delta % 120 == 0)
        {
            turned = 0;
            return CardWheel(point, box, style, surface, delta / 120);
        }
        if (placed.Rows < Weeks)
        {
            // Too short for six weeks: the month steps as the deltas add up to a notch.
            turned += delta;
            var notches = turned / 120;
            turned -= notches * 120;
            return notches == 0 || CardWheel(point, box, style, surface, notches);
        }
        var px = ListScrolled.Notch(style, placed.Window.bottom - placed.Window.top) * (-delta / 120f);
        Scroll.Pan(px / placed.Row, Now);
        Sync();
        if (!Animate)
        {
            Drawn();
        }
        return true;
    }

    /// <summary>keys: the line walks its days, by day, week and month itself (CellIsland.WalksItsOwnKeys).</summary>
    protected internal override bool WalksItsOwnKeys => true;

    /// <summary>keys: its days are its own keys'; its arrows are the folder's stops beside them (§471).</summary>
    protected internal override bool OwnKeysReach(int target) => target >= DayTarget;

    /// <summary>keys: the day picked, while the keys ring it.</summary>
    protected internal override int? OwnKeyedTarget => keyed ? Target(Picked) : 0;

    /// <summary>keys: the day picked is ringed while the keys are on it and its month shows.</summary>
    protected internal override bool RingsOwnKeys => keyed && Picked is var day && Shown is var shown && day.Year == shown.Year && day.Month == shown.Month;

    /// <summary>keys: come onto the days, the day picked is ringed; gone, it is not.</summary>
    protected internal override void OwnKeys(bool on)
    {
        if (keyed != on)
        {
            keyed = on;
            Drawn();
        }
    }

    /// <summary>
    /// The arrows move the day by a day or a week, Page Up and Down by a month, Home to today. Up and Down stay in
    /// the month shown: from its first week and its last they let go, and the folder's keys go on, to the arrows
    /// above, the line below (§471); Left and Right go on into the months either side.
    /// </summary>
    protected internal override bool CardKey(int key, WidgetSurface surface)
    {
        var day = Picked;
        if (key is VK.VK_UP or VK.VK_DOWN && day.AddDays(key == VK.VK_UP ? -7 : 7) is var week && Shown is var shown
            && (week.Year != shown.Year || week.Month != shown.Month))
        {
            return false;
        }
        DateOnly? next = key switch
        {
            VK.VK_LEFT => day.AddDays(-1),
            VK.VK_RIGHT => day.AddDays(1),
            VK.VK_UP => day.AddDays(-7),
            VK.VK_DOWN => day.AddDays(7),
            VK.VK_PRIOR => day.AddMonths(-1),
            VK.VK_NEXT => day.AddMonths(1),
            VK.VK_HOME => Today,
            _ => null,
        };
        if (next is not { } moved)
        {
            return false;
        }
        keyed = true;
        Pick(moved);
        return true;
    }

    // --- On the compositor (LineStrip.cs) -----------------------------------------------------------

    /// <summary>While the weeks glide or a touchpad has them, the host's quick tick asks <see cref="Slid"/>.</summary>
    internal override bool Animating => scroll?.Moving ?? false;

    bool IStripLine.Hosting
    {
        set => hosting = value;
    }

    public CompositionPropertySet Motion(Compositor compositor)
    {
        if (motion is null)
        {
            motion = compositor.CreatePropertySet();
            motion.InsertScalar("y", 0);
            motion.InsertScalar("top", 0);
        }
        // Shown from now on: where the weeks stand. Asked again at every drawing, it leaves a glide on its way alone.
        if (!hosted)
        {
            hosted = true;
            Sync();
        }
        return motion;
    }

    /// <summary>"y" to where the weeks stand now and "top" to the strip's first week, nothing gliding.</summary>
    private void Sync()
    {
        if (motion is null || scroll is null || stripPlaced is not { } placed)
        {
            return;
        }
        motionRow = placed.Row;
        motion.StopAnimation("y");
        motion.InsertScalar("y", scroll.At(Now) * placed.Row);
        motion.InsertScalar("top", (stripFirst - placed.FirstRow) * placed.Row);
    }

    /// <summary><paramref name="glide"/> started on the compositor, as the scroll started it; none: the weeks are where they landed.</summary>
    private void Glided(MonthScroll.Glide? glide)
    {
        if (glide is not { } on || motion is null || stripPlaced is not { } placed)
        {
            Sync();
            return;
        }
        var compositor = motion.Compositor;
        ease ??= MotionEase.Of(compositor, MotionKind.Glide);
        slide ??= compositor.CreateScalarKeyFrameAnimation();
        slide.InsertKeyFrame(0, on.From * placed.Row);
        slide.InsertKeyFrame(1, on.To * placed.Row, ease);
        slide.Duration = Karpo.Dock.Motion.Span(on.Ms);
        motion.StopAnimation("y");
        motion.StartAnimation("y", slide);
    }

    public LineStrip? Strip(D2D_RECT_F box, in TileStyle style)
    {
        animations = style.Look.Animations;
        // §489: the weeks glide at the look's speed.
        scroll?.Motion = style.Look.Motion;
        var placed = Placed(box, style);
        if (placed.Rows == 0)
        {
            stripPlaced = null;
            return null;
        }
        var weeks = Scroll;
        var moved = stripPlaced is not { } was || was.Row != placed.Row || was.FirstRow != placed.FirstRow;
        stripPlaced = placed;
        if (moved && weeks.Moving)
        {
            // Laid out again mid-glide (another width, another text): it lands, rather than glide in another size.
            _ = weeks.Step(0, Now, animate: false);
        }
        if (moved || !Covers(weeks, Now))
        {
            stripFirst = Around(weeks, Now);
            Sync();
            if (weeks.Gliding is { } going && Animate)
            {
                // The glide goes on: "top" moved, and the keys frames, in the same px, stay as they are.
                motion!.InsertScalar("top", (stripFirst - placed.FirstRow) * placed.Row);
                Glided(going with { From = weeks.At(Now), Start = Now, Ms = (int)Math.Max(1, going.Start + going.Ms - Now) });
            }
        }
        return new LineStrip(Relative(placed.Window, box), new Vector2(placed.Window.right - placed.Window.left, StripWeeks * placed.Row));
    }

    public void PaintStrip(ID2D1DeviceContext* dc, in TileStyle style)
    {
        if (stripPlaced is not { } placed)
        {
            return;
        }
        PaintWeeks(dc, placed, -placed.Window.left, 0, stripFirst, StripWeeks, style);
    }

    public bool Slid()
    {
        var weeks = Scroll;
        var now = Now;
        var was = weeks.At(now);
        if (weeks.Tick(now, Animate) is { } settle)
        {
            Glided(settle);
        }
        else if (weeks.At(now) != was)
        {
            Sync();
        }
        return weeks.Heading(now) != drawnHeading || (stripPlaced is not null && !Covers(weeks, now));
    }

    /// <summary>Whether the strip holds the weeks in view now and those on the way to where they are going.</summary>
    private bool Covers(MonthScroll weeks, long now)
    {
        var (from, to) = Needed(weeks, now);
        if (to - from > StripWeeks)
        {
            var at = weeks.At(now);
            (from, to) = ((int)MathF.Floor(at), (int)MathF.Ceiling(at) + Weeks);
        }
        return from >= stripFirst && to <= stripFirst + StripWeeks;
    }

    /// <summary>The strip's first week: the weeks needed in its middle, or, where they are more than it holds, from those in view on towards the end.</summary>
    private static int Around(MonthScroll weeks, long now)
    {
        var (from, to) = Needed(weeks, now);
        if (to - from <= StripWeeks)
        {
            return from - ((StripWeeks - (to - from)) / 2);
        }
        var at = weeks.At(now);
        return (weeks.Gliding?.To ?? at) > at ? (int)MathF.Floor(at) - 2 : (int)MathF.Ceiling(at) + Weeks + 2 - StripWeeks;
    }

    /// <summary>The weeks in view now and on the way to where the weeks are going: from, and to (not included).</summary>
    private static (int From, int To) Needed(MonthScroll weeks, long now)
    {
        var at = weeks.At(now);
        var end = weeks.Gliding?.To ?? at;
        return ((int)MathF.Floor(MathF.Min(at, end)), (int)MathF.Ceiling(MathF.Max(at, end)) + Weeks);
    }

    private static D2D_RECT_F Relative(D2D_RECT_F rect, D2D_RECT_F box) =>
        new(rect.left - box.left, rect.top - box.top, rect.right - box.left, rect.bottom - box.top);

    // --- Laid out -----------------------------------------------------------------------------------

    /// <summary>
    /// The month on a line <paramref name="width"/> px wide: between the list's gutters, the title's
    /// row 40 DIP tall with the arrows at its end, the weekdays' row 32, and six weeks of cells as
    /// wide as a seventh of the room and at most 40 DIP tall (all at the text's scale). Pure.
    /// </summary>
    internal static MonthPlan Lay(Gfx gfx, float width, in TileStyle style)
    {
        var gutter = ListForm.Gutter(style);
        var room = MathF.Max(7, width - (2 * gutter));
        var pad = style.Px(4);
        var titleHeight = PopupPaint.Round(style.Text(40));
        // The arrows: the lines' one button (LineButton), subtle; a seventh of the room at the most, so a
        // large text leaves the title its room.
        var buttons = LineButton.Of(gfx, style);
        var arrow = MathF.Min(buttons.Height, PopupPaint.Round(room / 7));
        var arrowTop = pad + PopupPaint.Round((titleHeight - arrow) / 2);
        var next = PopupPaint.Rect(gutter + room - arrow, arrowTop, arrow, arrow);
        var previous = PopupPaint.Rect(next.left - buttons.Gap - arrow, arrowTop, arrow, arrow);
        var title = PopupPaint.Rect(gutter, pad, MathF.Max(1, previous.left - style.Px(8) - gutter), titleHeight);
        var across = room / 7;
        var down = MathF.Min(PopupPaint.Round(across), PopupPaint.Round(style.Text(40)));
        var weekdayTop = pad + titleHeight;
        var weekdayHeight = MathF.Min(PopupPaint.Round(style.Text(32)), down);
        var weekdays = new List<D2D_RECT_F>(7);
        var days = new List<D2D_RECT_F>(Weeks * 7);
        for (var column = 0; column < 7; column++)
        {
            var left = PopupPaint.Round(gutter + (column * across));
            var right = PopupPaint.Round(gutter + ((column + 1) * across));
            weekdays.Add(new D2D_RECT_F(left, weekdayTop, right, weekdayTop + weekdayHeight));
            for (var week = 0; week < Weeks; week++)
            {
                var top = weekdayTop + weekdayHeight + (week * down);
                days.Add(new D2D_RECT_F(left, top, right, top + down));
            }
        }
        // In reading order: a week a row.
        var ordered = Enumerable.Range(0, Weeks * 7).Select(index => days[((index % 7) * Weeks) + (index / 7)]).ToList();
        return new MonthPlan(title, previous, next, weekdays, ordered, weekdayTop + weekdayHeight + (Weeks * down) + pad);
    }

    public float ListHeight(float width, in TileStyle style) => Lay(Gfx, width, style).Height;

    /// <summary>
    /// The month placed in a box (line px): its plan, the window the weeks show through — rows
    /// <paramref name="FirstRow"/> to <paramref name="FirstRow"/> + <paramref name="Rows"/> of the six, each
    /// <paramref name="Row"/> px tall — and the columns' edges.
    /// </summary>
    private readonly record struct Placing(MonthPlan Plan, D2D_RECT_F Window, float Row, int FirstRow, int Rows, float[] Lefts, float[] Rights);

    /// <summary>Tests only: the plan, the weeks' window (px of the box) and a week's height, as last drawn.</summary>
    internal (MonthPlan Plan, D2D_RECT_F Window, float Row)? Laid { get; private set; }

    /// <summary>
    /// The plan in <paramref name="box"/>, at its top: a line taller than the month leaves its room under
    /// it. A line shorter than the month (no kind has a fewest, §166) shows the whole weeks that fit,
    /// the picked day's week among them (or today's, or the first), then without the weekdays' row, and
    /// at its shortest the title and the arrows alone, centred in the line: never a week cut through.
    /// </summary>
    private Placing Placed(D2D_RECT_F box, in TileStyle style)
    {
        var plan = Lay(Gfx, box.right - box.left, style);
        var given = box.bottom - box.top;
        var titleDy = 0f;
        var first = 0;
        var weeks = Weeks;
        var weekdays = true;
        var down = plan.Days[0].bottom - plan.Days[0].top;
        var weekdayHeight = plan.Weekdays[0].bottom - plan.Weekdays[0].top;
        if (given > 0 && given < plan.Height - 0.5f)
        {
            var pad = style.Px(4);
            var room = given - plan.Weekdays[0].bottom;
            weeks = Math.Clamp((int)MathF.Floor((room + 0.5f) / down), 0, Weeks);
            if (weeks == 0)
            {
                // No week under the weekdays: without them, as many as fit under the title.
                weekdays = false;
                weeks = Math.Clamp((int)MathF.Floor((given - plan.Weekdays[0].top + 0.5f) / down), 0, Weeks);
            }
            if (weeks == 0)
            {
                // The title and its arrows alone, centred in a line shorter than their row and its air.
                titleDy = MathF.Min(0, PopupPaint.Round((given - (plan.Title.bottom + pad)) / 2));
            }
            var grid = Grid(Shown, Panels.Nls.FirstDayOfWeek());
            var at = Array.IndexOf(grid, Picked) is >= 0 and var picked ? picked : Math.Max(0, Array.IndexOf(grid, Today));
            first = weeks == 0 ? 0 : Math.Clamp((at / 7) - ((weeks - 1) / 2), 0, Weeks - weeks);
        }
        D2D_RECT_F At(D2D_RECT_F at) => new(box.left + at.left, box.top + at.top, box.left + at.right, box.top + at.bottom);
        D2D_RECT_F Top(D2D_RECT_F at) => At(at with { top = at.top + titleDy, bottom = at.bottom + titleDy });
        var gridTop = box.top + plan.Weekdays[0].bottom - (weekdays ? 0 : weekdayHeight);
        var lefts = Enumerable.Range(0, 7).Select(column => box.left + plan.Days[column].left).ToArray();
        var rights = Enumerable.Range(0, 7).Select(column => box.left + plan.Days[column].right).ToArray();
        var window = weeks == 0 ? default : new D2D_RECT_F(lefts[0], gridTop, rights[6], gridTop + (weeks * down));
        var placed = plan with
        {
            Title = Top(plan.Title),
            Previous = Top(plan.Previous),
            Next = Top(plan.Next),
            Weekdays = [.. plan.Weekdays.Select(At)],
            WeekdaysShown = weekdays,
        };
        return new Placing(placed, window, down, first, weeks, lefts, rights);
    }

    /// <summary>Day <paramref name="column"/> of week <paramref name="week"/>; null past the calendar's ends.</summary>
    private static DateOnly? Day(MonthScroll weeks, int week, int column)
    {
        var number = (long)weeks.Origin.DayNumber + (7L * week) + column;
        return number < DateOnly.MinValue.DayNumber || number > DateOnly.MaxValue.DayNumber ? null : DateOnly.FromDayNumber((int)number);
    }

    /// <summary><paramref name="day"/>'s cell where the weeks stand now (line px), whether or not it is in the window.</summary>
    private D2D_RECT_F CellOf(Placing placed, DateOnly day)
    {
        var weeks = Scroll;
        var column = ((int)day.DayOfWeek - (int)weeks.First + 7) % 7;
        var top = placed.Window.top + ((weeks.WeekOf(day) - weeks.At(Now) - placed.FirstRow) * placed.Row);
        return new D2D_RECT_F(placed.Lefts[column], top, placed.Rights[column], top + placed.Row);
    }

    /// <summary>"Last updated 14:05 — couldn't refresh" while a calendar could not be read and its dots are its last good read (StaleMark.cs).</summary>
    internal override string? StaleTip => store.Freshness.Tip(Clock.Now());

    /// <summary>
    /// The stale mark before the arrows, the title kept clear of it (StaleMark.cs): the arrows and the
    /// days stand where they always do. Drawn where <paramref name="dc"/> is not null; the plan with its
    /// title's room, and the mark's piece (null while not stale).
    /// </summary>
    private (MonthPlan Plan, LinePiece? Mark) Marked(ID2D1DeviceContext* dc, MonthPlan plan, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        if (!store.Freshness.Stale)
        {
            return (plan, null);
        }
        var end = plan.Previous.left - style.Px(8);
        var sketch = new LineSketch(dc, Gfx);
        StaleMark.Sketch(sketch, new D2D_RECT_F(plan.Title.left, plan.Title.top, end + form.Inset, plan.Title.bottom), style, form, plan.Title.top, plan.Title.bottom - plan.Title.top);
        var title = plan.Title with { right = MathF.Max(plan.Title.left + 1, plan.Title.right - StaleMark.Room(style, form, stale: true)) };
        return (plan with { Title = title }, sketch.Pieces[^1]);
    }

    // --- Drawn -----------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        animations = style.Look.Animations;
        scroll?.Motion = style.Look.Motion;
        var theme = QuickTheme.For(style.Theme);
        var placed = Placed(box, style);
        var (plan, _) = Marked(dc, placed.Plan, style);
        Laid = (plan, Relative(placed.Window, box), placed.Row);
        var shown = Shown;
        drawnHeading = shown;
        var firstDay = Panels.Nls.FirstDayOfWeek();
        var px = style.Text(14);

        // The month and its year, and the arrows that step it.
        // The month and its year, or the month alone where the room is short (the largest text).
        var title = Panels.Nls.YearMonth(shown.Year, shown.Month);
        if (PopupPaint.Measure(Gfx, title, px, Weight.Semibold).X > plan.Title.right - plan.Title.left)
        {
            title = Panels.Nls.MonthName(shown.Month);
        }
        var titleLine = TilePaint.LineHeight(Gfx, px, Weight.Semibold);
        TilePaint.Line(dc, Gfx, title, px, Weight.Semibold, theme.Text, plan.Title.left,
            plan.Title.top + PopupPaint.Round((plan.Title.bottom - plan.Title.top - titleLine) / 2), plan.Title.right - plan.Title.left);
        foreach (var (at, glyph, target) in new[] { (plan.Previous, CalendarLayout.LeftGlyph, PreviousTarget), (plan.Next, CalendarLayout.RightGlyph, NextTarget) })
        {
            LineButton.Of(Gfx, style).Paint(dc, Gfx, style.Theme, at, "", glyph, ButtonStyle.Subtle, hot == target);

        }

        // The weekdays at the list's text, or as much smaller as a seventh of the room keeps two figures apart.
        var across = plan.Weekdays[0].right - plan.Weekdays[0].left;
        px = DayText(style, across);
        for (var column = 0; column < 7 && plan.WeekdaysShown; column++)
        {
            var name = Panels.Nls.ShortestDayName((DayOfWeek)(((int)firstDay + column) % 7));
            if (PopupPaint.Measure(Gfx, name, px, Weight.Regular).X > across - style.Px(4))
            {
                name = name[..1];
            }
            PopupPaint.Centred(dc, Gfx, name, px, Weight.Regular, theme.Text, plan.Weekdays[column], ink: false);
        }

        // The weeks where they stand, clipped to their window — unless they are a strip the host draws and slides.
        if (placed.Rows == 0 || hosting)
        {
            return;
        }
        var window = placed.Window;
        var position = Scroll.At(Now) + placed.FirstRow;
        var from = (int)MathF.Floor(position);
        dc->PushAxisAlignedClip(&window, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            PaintWeeks(dc, placed, 0, window.top - ((position - from) * placed.Row), from, placed.Rows + 1, style);
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>The days' text: the list's, or as much smaller as a seventh of the room keeps two figures apart (at the largest text the cells are narrower than the figures).</summary>
    private static float DayText(in TileStyle style, float across) => MathF.Min(style.Text(14), MathF.Floor(across * 0.42f));

    /// <summary>
    /// <paramref name="count"/> weeks from week <paramref name="from"/>, the first at <paramref name="top"/>, the
    /// columns <paramref name="dx"/> px along: the heading's month's days, the others' quieter, today ringed, the day
    /// picked filled, the one under the pointer lit, and a dot for each calendar with events that day.
    /// </summary>
    private void PaintWeeks(ID2D1DeviceContext* dc, Placing placed, float dx, float top, int from, int count, in TileStyle style)
    {
        var theme = QuickTheme.For(style.Theme);
        var weeks = Scroll;
        var shown = Shown;
        var picked = Picked;
        var today = Today;
        var px = DayText(style, placed.Rights[0] - placed.Lefts[0]);
        Look();
        if (Day(weeks, from, 0) is not { } start)
        {
            return;
        }
        var busy = CalendarLayout.ByDay(CalendarLines.EventsIn(store, Settings), start, count * 7);
        for (var week = from; week < from + count; week++)
        {
            var y = top + ((week - from) * placed.Row);
            for (var column = 0; column < 7; column++)
            {
                if (Day(weeks, week, column) is not { } day)
                {
                    continue;
                }
                var cell = new D2D_RECT_F(placed.Lefts[column] + dx, y, placed.Rights[column] + dx, y + placed.Row);
                var centre = new Vector2((cell.left + cell.right) / 2, (cell.top + cell.bottom) / 2);
                var size = MathF.Min(cell.right - cell.left, cell.bottom - cell.top);
                var circle = (size / 2) - style.Px(2);
                var isPicked = day == picked;
                var isHot = hot == Target(day);
                var other = day.Month != shown.Month || day.Year != shown.Year;
                var ink = other ? theme.TextTertiary : theme.Text;
                if (isPicked)
                {
                    PopupPaint.Circle(dc, centre, circle, isHot ? theme.AccentHover : theme.Accent);
                    ink = theme.TextOnAccent;
                }
                else if (isHot)
                {
                    PopupPaint.Circle(dc, centre, circle, theme.SubtleHover);
                }
                if (day == today && !isPicked)
                {
                    var ring = MathF.Max(1, style.Px(2));
                    PopupPaint.Ring(dc, centre, circle - (ring / 2), ring, theme.Accent);
                }
                if (isPicked && keyed)
                {
                    var square = PopupPaint.Rect(centre.X - circle, centre.Y - circle, 2 * circle, 2 * circle);
                    TogglesTile.FocusRing(dc, square, circle, style, theme);
                }
                PopupPaint.Centred(dc, Gfx, day.Day.ToString(CultureInfo.CurrentCulture), px, isPicked ? Weight.Semibold : Weight.Regular, ink, cell, ink: false);
                if (busy.TryGetValue(day, out var colours))
                {
                    // A dot per calendar with events that day, at most three, side by side under the number.
                    var dots = colours.Take(3).ToList();
                    var step = style.Px(5);
                    var dot = MathF.Max(2, style.Px(2));
                    var x = centre.X - ((dots.Count - 1) * step / 2);
                    foreach (var colour in dots)
                    {
                        var dotInk = isPicked ? theme.TextOnAccent : other ? theme.TextTertiary : CalendarPainter.Solid(colour, style.Theme);
                        PopupPaint.Circle(dc, new Vector2(x, centre.Y + (circle * 0.62f)), dot, dotInk);
                        x += step;
                    }
                }
            }
        }
    }

    /// <summary>An arrow, a day, or nothing at <paramref name="point"/>.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        LineFocus.At(LineTargets(box, style), point);

    /// <summary>
    /// The arrows, then every day where it stands in the weeks' window now, as much of its cell as shows (the keys'
    /// stops, LineFocus; the hit test).
    /// </summary>
    public IReadOnlyList<(int Target, D2D_RECT_F Box)> LineTargets(D2D_RECT_F box, in TileStyle style)
    {
        var placed = Placed(box, style);
        var targets = new List<(int Target, D2D_RECT_F Box)> { (PreviousTarget, placed.Plan.Previous), (NextTarget, placed.Plan.Next) };
        if (placed.Rows == 0)
        {
            return targets;
        }
        var weeks = Scroll;
        var at = weeks.At(Now) + placed.FirstRow;
        var window = placed.Window;
        for (var week = (int)MathF.Floor(at); week <= at + placed.Rows; week++)
        {
            for (var column = 0; column < 7; column++)
            {
                if (Day(weeks, week, column) is { } day && CellOf(placed, day) is var cell
                    && MathF.Max(cell.top, window.top) is var top && MathF.Min(cell.bottom, window.bottom) is var bottom && bottom > top)
                {
                    targets.Add((Target(day), cell with { top = top, bottom = bottom }));
                }
            }
        }
        return targets;
    }

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var at = point is { } where ? CardTarget(where, box, style, surface) : 0;
        if (point is not null)
        {
            keyed = false;
        }
        if (at != hot)
        {
            hot = at;
            Redraw(Gfx);
        }
    }

    /// <summary>The days and the arrows light themselves; the line does not light as well.</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>The arrows say which way they step; a holiday what holiday it is; the stale mark when the events were last read.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var placed = Placed(box, style);
        var (plan, mark) = Marked(null, placed.Plan, style);
        if (mark is { } piece && StaleMark.Tip([piece], point, StaleTip, style) is { } said)
        {
            return said;
        }
        return CardTarget(point, box, style, surface) switch
        {
            PreviousTarget => (S.WidgetMonthPrevious, plan.Previous),
            NextTarget => (S.WidgetMonthNext, plan.Next),
            >= DayTarget and var day when CalendarLines.HolidayTip(store, DateOnly.FromDayNumber(day - DayTarget)) is { } holiday
                => (holiday, CellOf(placed, DateOnly.FromDayNumber(day - DayTarget))),
            _ => null,
        };
    }

    // --- As pieces ----------------------------------------------------------------------------------

    public string MainPiece => "title";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var placed = Placed(box, style);
        var (plan, mark) = Marked(null, placed.Plan, style);
        var shown = Shown;
        var pieces = new List<LinePiece>
        {
            new("title", plan.Title, Panels.Nls.YearMonth(shown.Year, shown.Month)),
            new("previous", plan.Previous),
            new("next", plan.Next),
        };
        // The days whose middles are in the weeks' window where they stand now.
        if (placed.Rows > 0)
        {
            var weeks = Scroll;
            var at = weeks.At(Now) + placed.FirstRow;
            for (var week = (int)MathF.Floor(at); week <= at + placed.Rows; week++)
            {
                for (var column = 0; column < 7; column++)
                {
                    if (Day(weeks, week, column) is { } day && CellOf(placed, day) is var cell
                        && LineRows.Inside(placed.Window, new Vector2((cell.left + cell.right) / 2, (cell.top + cell.bottom) / 2)))
                    {
                        pieces.Add(new LinePiece($"day {day:yyyy-MM-dd}", cell, day.Day.ToString(CultureInfo.InvariantCulture)));
                    }
                }
            }
        }
        if (mark is { } stale)
        {
            pieces.Add(stale);
        }
        return pieces;
    }
}
