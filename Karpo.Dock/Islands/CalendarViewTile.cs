// The calendar flyout's day, week and year views as lines of a list (docs\widgets.md §80), one kind
// each (ListKind.DayView, WeekView, YearView), drawn by the flyout's own layout and painter
// (Calendar\CalendarLayout.cs, CalendarPainter.cs) at the line's width and on the list's gutter:
// the page's title with its arrows, and under it the day's hours, the week's days with their hours,
// or the year's twelve months, each event in its calendar's colour.
//
// **The folder's day.** Each follows the folder's day and publishes it (Channels.Day): a click on a
// day, a week's heading or a year's day picks it, the arrows step it — a day, a week, a year — and a
// month, the day's events or the date's header beside it go along — always (§240). The folder
// forgets the day when it closes, and the view comes back to today.
//
// **What moves inside it.** The day's and the week's whole day, midnight to midnight, scroll in their
// window, opened an hour before now; the year's months scroll where the line is shorter than they
// are. The wheel scrolls them and, at either end, is the folder's again; Up and Down scroll the hours
// an hour. Left and Right, Page Up and Down and Home move the day picked as the flyout's keys did.
// §471: the arrows and every Join seen are the folder's stops beside the days; the year's days are its
// stops too, walked where they stand, Enter picking one.
// The flyout glided its days sideways on the compositor; a line has no animation of its own, so a
// step is drawn where it lands.
//
// **Join.** A meeting not over has its Join on its block where the block holds one (CalendarLayout.JoinIn,
// JoinButton.cs, docs\widgets.md §206): a click there joins it and picks no day; Enter or Space joins the
// lit one in view; a right click on a meeting's block, with a Join or too small for one, has "Join
// meeting". The year's months show no events, so no Join.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Panels;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using CalendarView = Karpo.Core.CalendarView;

namespace Karpo.Dock;

internal sealed unsafe class CalendarViewTile : CalendarLineTile, IListContent, ILinePieces, IMeetingLine, ILineTargets
{
    /// <summary>
    /// What a click is on, by what it is and never by its place among the targets, so a layout made
    /// between a press and its release (events arriving, the strip laid out anew) cannot hand the
    /// release to something else: the arrows, and a day by its day (<see cref="DayTarget"/> plus its DayNumber).
    /// </summary>
    internal const int PreviousTarget = 1;

    internal const int NextTarget = 2;

    internal const int DayTarget = 10;

    /// <summary>A block's Join, plus the block's place among the grid's: past every day's code.</summary>
    internal const int JoinTarget = JoinButton.Target;

    private readonly EventChoice joins = new();
    private RowPress press;

    // The meeting whose block the pointer is on, for the menu's "Join meeting".
    private (string Title, Karpo.Accounts.MeetingLink Link)? pointed;

    private CalendarView view = CalendarView.Week;

    // The day picked, for a view that stands in no folder.
    private DateOnly? own;

    // The week's window onto the days, where a step put it; null for the picked day's week.
    private DateOnly? window;

    // The year stepped to, and the day picked when it was; once another widget picks another day the
    // year is that day's again.
    private int? year;
    private DateOnly steppedFrom;

    // How far the scrolling part is scrolled, px; null for where it opens (an hour before now).
    private float? scroll;

    // The strip of days laid out last, kept while the window is well inside it.
    private DateOnly? strip;

    // What the pointer is on (Code), and the plan last laid out.
    private int hot;
    private CalendarPlan? last;

    public CalendarViewTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    public override void Configure(WidgetItem island, Config config)
    {
        // By its word, as the sound's and quick settings' pages tell theirs: a widget not stood in its
        // folder's view yet has no line (CellItem.List), and was taken for a week view (§205).
        view = ListWidgets.Find(island.Info?.Id)?.Kind switch
        {
            ListKind.DayView => CalendarView.Day,
            ListKind.YearView => CalendarView.Year,
            _ => CalendarView.Week,
        };
        base.Configure(island, config);
    }

    /// <summary>Which view it is: its kind's.</summary>
    internal CalendarView View => view;

    /// <summary>The folder closed: the day, the page, the scroll and the pointer are this look's, and go with it.</summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            own = null;
            window = null;
            year = null;
            scroll = null;
            strip = null;
            hot = 0;
            pointed = null;
            _ = press.Up();
            joins.Forget();
        }
    }

    /// <summary>§357: the day picked, the week's window, the year stepped to and the scroll, kept as the folder closes in edit mode.</summary>
    private sealed record Kept(DateOnly? Own, DateOnly? Window, int? Year, DateOnly SteppedFrom, float? Scroll);

    internal override object? ViewMemory() => new Kept(own, window, year, steppedFrom, scroll);

    internal override void RecallView(object memory)
    {
        if (memory is Kept kept)
        {
            (own, window, year, steppedFrom, scroll) = (kept.Own, kept.Window, kept.Year, kept.SteppedFrom, kept.Scroll);
        }
    }

    // --- The day ------------------------------------------------------------------------------------

    /// <summary>The day picked: the folder's where it follows it, else its own, else today.</summary>
    internal DateOnly Picked => Followed(Channels.Day) ?? own ?? Today;

    /// <summary>
    /// The page shown: the day picked; the week's window, where a step put it and the day picked is in
    /// it, else the picked day's week; a day of the year stepped to, else of the picked day's.
    /// </summary>
    internal DateOnly Shown
    {
        get
        {
            var picked = Picked;
            switch (view)
            {
                case CalendarView.Day:
                    return picked;
                case CalendarView.Week:
                    return window is { } first && picked >= first && picked.DayNumber < first.DayNumber + 7
                        ? first
                        : CalendarNavigation.WeekStart(picked, Nls.FirstDayOfWeek());
                default:
                    return year is { } stepped && steppedFrom == picked
                        ? new DateOnly(stepped, picked.Month, Math.Min(picked.Day, DateTime.DaysInMonth(stepped, picked.Month)))
                        : picked;
            }
        }
    }

    /// <summary>Picks <paramref name="day"/>: kept, shown, and published as the folder's day.</summary>
    internal void Pick(DateOnly day)
    {
        own = day;
        year = null;
        Publish(Channels.Day, day);
        Redraw(Gfx);
    }

    /// <summary>A page on or back: a day, a week (the window with it), a year (the day picked stays).</summary>
    internal void Step(int by)
    {
        switch (view)
        {
            case CalendarView.Day:
                Pick(DayStrip.Add(Picked, by));
                break;
            case CalendarView.Week:
            {
                var first = DayStrip.Add(Shown, 7L * by);
                Pick(DayStrip.Add(Picked, 7L * by));
                window = first;
                Redraw(Gfx);
                break;
            }
            default:
                year = Math.Clamp(Shown.Year + by, 1, 9999);
                steppedFrom = Picked;
                Redraw(Gfx);
                break;
        }
    }

    /// <summary>What <paramref name="target"/> is, as a click names it.</summary>
    internal static int Code(CalendarTarget target) => target.Kind switch
    {
        TargetKind.Previous => PreviousTarget,
        TargetKind.Next => NextTarget,
        _ => DayTarget + target.Day.DayNumber,
    };

    public override IslandAction Click(int index)
    {
        _ = press.Up();
        // A block's Join: the meeting opens once the folder has gone, and no day is picked.
        if (index >= JoinTarget)
        {
            return joins.Choose(last?.Grid is { } grid && index - JoinTarget < grid.Blocks.Count ? grid.Blocks[index - JoinTarget].Link : null);
        }
        if (index == 0 && joins.Keyed())
        {
            return joins.Choose(LitLink());
        }
        switch (index)
        {
            case PreviousTarget:
                Step(-1);
                break;
            case NextTarget:
                Step(1);
                break;
            case >= DayTarget:
            {
                var day = DateOnly.FromDayNumber(index - DayTarget);
                if (view == CalendarView.Week)
                {
                    // A day half in view, clicked, comes in whole: the window moves by as little as shows it.
                    window = DayStrip.Reveal(Shown, 7, day);
                }
                Pick(day);
                break;
            }
        }
        return default;
    }

    /// <summary>The wheel scrolls the hours or the months; at either end it is the folder's again.</summary>
    protected internal override bool CardWheel(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface, int notches)
    {
        var plan = Plan(box, style);
        if (notches == 0 || !plan.Scrolls)
        {
            return false;
        }
        return ScrollTo(Scroll(plan) - (notches * plan.WheelStep), plan);
    }

    /// <summary>
    /// keys: the day and week views walk their days and hours themselves (CellIsland.WalksItsOwnKeys), their arrows
    /// and Joins the folder's stops beside them (§471); the year's days are the folder's stops, as its arrows are.
    /// </summary>
    protected internal override bool WalksItsOwnKeys => view != CalendarView.Year;

    /// <summary>keys: the days are the day's and the week's own keys'; the arrows and the Joins are not.</summary>
    protected internal override bool OwnKeysReach(int target) => target is >= DayTarget and < JoinTarget;

    /// <summary>keys: the day picked, which its own keys move.</summary>
    protected internal override int? OwnKeyedTarget => DayTarget + Picked.DayNumber;

    /// <summary>
    /// The arrows move the day picked as the flyout's keys did (a day), Page Up and Down a day or a year, Home back
    /// to today; in the day and week views up and down scroll the hours an hour, and let go at their ends. In the
    /// year the arrows are the folder's, which walk its days: Page Up and Down still turn the year, Home goes to today.
    /// </summary>
    protected internal override bool CardKey(int key, WidgetSurface surface)
    {
        if (view == CalendarView.Year && FocusNav.WayOf(key) is not null)
        {
            return false;
        }
        // Enter and Space join the lit meeting in view, as an accent button is a dialog's default.
        if (key is VK.VK_RETURN or VK.VK_SPACE)
        {
            return joins.Key(key, LitLink());
        }
        if (view != CalendarView.Year && key is VK.VK_UP or VK.VK_DOWN && last is { } plan)
        {
            return ScrollTo(Scroll(plan) + (key == VK.VK_UP ? -plan.WheelStep : plan.WheelStep), plan);
        }
        CalendarKey? move = key switch
        {
            VK.VK_LEFT => CalendarKey.Left,
            VK.VK_RIGHT => CalendarKey.Right,
            VK.VK_UP => CalendarKey.Up,
            VK.VK_DOWN => CalendarKey.Down,
            VK.VK_PRIOR => CalendarKey.PageUp,
            VK.VK_NEXT => CalendarKey.PageDown,
            VK.VK_HOME => CalendarKey.Home,
            _ => null,
        };
        if (move is not { } pressed || CalendarNavigation.Key(view, Picked, pressed, Today, last?.Year?.Columns ?? 4) is not { } day)
        {
            return false;
        }
        if (view == CalendarView.Week)
        {
            window = DayStrip.Reveal(Shown, 7, day);
        }
        Pick(day);
        return true;
    }

    // --- Join --------------------------------------------------------------------------------------

    /// <summary>The dock, once the folder has gone: opens the meeting a Join chose, once.</summary>
    public void Open() => joins.Open();

    /// <summary>The meeting whose block the pointer is on, with a Join or too small for one.</summary>
    public (string Title, Karpo.Accounts.MeetingLink Link)? MenuMeeting() => pointed;

    /// <summary>The first lit meeting on a day in view: what Enter joins.</summary>
    private Karpo.Accounts.MeetingLink? LitLink()
    {
        if (last?.Grid is not { } grid)
        {
            return null;
        }
        foreach (var block in grid.Blocks)
        {
            var middle = (block.Box.left + block.Box.right) / 2;
            if (block is { Lit: true, Link: { } link } && middle >= grid.RestSlide && middle < grid.RestSlide + grid.WindowWidth)
            {
                return link;
            }
        }
        return null;
    }

    /// <summary>Where a block's <paramref name="part"/> (px of the strip's hours) is in the line's px, with the hours scrolled and the days slid as drawn.</summary>
    private static D2D_RECT_F Placed(GridPart grid, CalendarPlan plan, float scroll, D2D_RECT_F part)
    {
        var dx = grid.Left - grid.RestSlide;
        var dy = plan.ViewportTop - scroll;
        return new D2D_RECT_F(part.left + dx, part.top + dy, part.right + dx, part.bottom + dy);
    }

    /// <summary>Whether <paramref name="point"/> (line px) is in the hours' window, where a block can be seen.</summary>
    private static bool InHours(GridPart grid, CalendarPlan plan, Vector2 point) =>
        point.Y >= plan.ViewportTop && point.Y < plan.ViewportTop + plan.Viewport.Y && point.X >= grid.Left && point.X < grid.Left + grid.WindowWidth;


    /// <summary>The meeting whose block is at <paramref name="at"/> (line px); null over anything else.</summary>
    private static (string Title, Karpo.Accounts.MeetingLink Link)? MeetingAt(CalendarPlan plan, float scroll, Vector2 at)
    {
        if (plan.Grid is not { } grid || !InHours(grid, plan, at))
        {
            return null;
        }
        foreach (var block in grid.Blocks)
        {
            if (block.Link is { } link && LineRows.Inside(Placed(grid, plan, scroll, block.Box), at))
            {
                return (block.Title, link);
            }
        }
        return null;
    }

    /// <summary>
    /// The blocks' Join buttons, over everything else of the hours (line px, the line's origin at 0),
    /// inside the window they are seen in.
    /// </summary>
    private void PaintJoins(ID2D1DeviceContext* dc, CalendarPlan plan, float scroll, in TileStyle style)
    {
        if (plan.Grid is not { } grid || plan.Viewport.Y <= 0 || !grid.Blocks.Any(block => TilePaint.Width(block.Join) > 0))
        {
            return;
        }
        var window = new D2D_RECT_F(grid.Left, plan.ViewportTop, grid.Left + grid.WindowWidth, plan.ViewportTop + plan.Viewport.Y);
        var buttons = LineButton.Of(Gfx, style);
        var theme = style.Theme;
        dc->PushAxisAlignedClip(&window, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            for (var index = 0; index < grid.Blocks.Count; index++)
            {
                var block = grid.Blocks[index];
                if (TilePaint.Width(block.Join) > 0)
                {
                    JoinButton.Paint(dc, Gfx, buttons, theme, Placed(grid, plan, scroll, block.Join), block.Worded, block.Lit, hot == JoinTarget + index, press.On(JoinTarget + index));

                }
            }
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    private float Scroll(CalendarPlan plan) => Math.Clamp(scroll ?? plan.FirstScroll, 0, plan.MostScroll);

    /// <summary>Scrolls to <paramref name="to"/>, within the scrolling part; true where that moved it.</summary>
    private bool ScrollTo(float to, CalendarPlan plan)
    {
        var from = Scroll(plan);
        var next = Math.Clamp(PopupPaint.Round(to), 0, plan.MostScroll);
        if (next == from)
        {
            return false;
        }
        scroll = next;
        Redraw(Gfx);
        return true;
    }

    // --- Laid out -----------------------------------------------------------------------------------

    /// <summary>
    /// The view at <paramref name="width"/> × <paramref name="height"/> (0: as tall as it would be), in
    /// the line's px, the title kept clear of the stale mark before the arrows.
    /// </summary>
    internal CalendarPlan Build(float width, float height, in TileStyle style)
    {
        Look();
        var now = Clock.Now();
        var inputs = new CalendarInputs(
            view,
            DateOnly.FromDateTime(now),
            Picked,
            Shown,
            now,
            Nls.FirstDayOfWeek(),
            Nls.FirstWeekOfYear(),
            Store.HasCalendars,
            Store.Loading,
            Store.Problem,
            CalendarLines.EventsIn(Store, Settings),
            width,
            height,
            ListForm.Gutter(style),
            strip,
            StaleRoom(style));
        var plan = CalendarLayout.Build(Gfx, style.Look, style.Scale, inputs);
        strip = plan.Grid?.First;
        return plan;
    }

    private CalendarPlan Plan(D2D_RECT_F box, in TileStyle style)
    {
        var plan = Build(box.right - box.left, box.bottom - box.top, style);
        last = plan;
        return plan;
    }

    public float ListHeight(float width, in TileStyle style) => Build(width, 0, style).Height;

    /// <summary>Where <paramref name="target"/> is in the line's px, with the hours scrolled and the days slid as drawn.</summary>
    private static D2D_RECT_F Locate(CalendarTarget target, CalendarPlan plan, float scroll)
    {
        var box = target.Box;
        float dx = 0, dy = 0;
        if (target.Slides && plan.Grid is { } grid)
        {
            dx = grid.Left - grid.RestSlide;
            if (!target.Scrolls)
            {
                dy = grid.Head.top;
            }
        }
        if (target.Scrolls)
        {
            dy = plan.ViewportTop - scroll;
        }
        return new D2D_RECT_F(box.left + dx, box.top + dy, box.right + dx, box.bottom + dy);
    }

    /// <summary>Whether <paramref name="point"/> (line px) is where <paramref name="target"/> can be seen: in its window.</summary>
    private static bool Seen(CalendarTarget target, CalendarPlan plan, Vector2 point)
    {
        if (target.Scrolls && (point.Y < plan.ViewportTop || point.Y >= plan.ViewportTop + plan.Viewport.Y))
        {
            return false;
        }
        return !target.Slides || plan.Grid is not { } grid || (point.X >= grid.Left && point.X < grid.Left + grid.WindowWidth);
    }

    // --- Drawn -----------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Plan(box, style);
        var theme = style.Theme;
        var scrolled = Scroll(plan);
        dc->PushAxisAlignedClip(&box, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            TilePaint.Placed(dc, box.left, box.top, 1, () =>
            {
                // §379: everything a press there acts on lights: a day's heading and its whole column, which
                // picks it too (it lit the heading alone, the column under the pointer dark).
                foreach (var lit in hot > 0 ? plan.Targets.Where(target => Code(target) == hot && target.Box.right > target.Box.left) : [])
                {
                    Light(dc, plan, lit, scrolled, QuickTheme.For(theme).SubtleHover);
                }
                CalendarPainter.Picture(dc, Gfx, plan, theme, scrolled);
            });
            var tileStyle = style;
            TilePaint.Placed(dc, box.left, box.top, 1, () => PaintJoins(dc, plan, scrolled, tileStyle));
            StaleMarkOf(dc, box, plan, style);
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>
    /// The stale mark before the arrows, in the room the title keeps clear for it (StaleMark.cs): the
    /// arrows stand where they always do. The piece, for its tooltip; null
    /// where the line is not stale.
    /// </summary>
    private LinePiece? StaleMarkOf(ID2D1DeviceContext* dc, D2D_RECT_F box, CalendarPlan plan, in TileStyle style)
    {
        var end = box.left + plan.Previous.left - PopupPaint.Round(8 * plan.Scale);
        var before = new D2D_RECT_F(box.left, box.top, end + ListForm.Gutter(style), box.bottom);
        return StalePiece(dc, before, style, box.top + plan.NavRow.top, plan.NavRow.bottom - plan.NavRow.top);
    }

    /// <summary>The hover's fill under <paramref name="target"/>, inside the window it is seen in.</summary>
    private static void Light(ID2D1DeviceContext* dc, CalendarPlan plan, CalendarTarget target, float scroll, DXGI_RGBA fill)
    {
        var at = Locate(target, plan, scroll);
        var clip = target.Scrolls
            ? new D2D_RECT_F(0, plan.ViewportTop, plan.Width, plan.ViewportTop + plan.Viewport.Y)
            : new D2D_RECT_F(0, 0, plan.Width, plan.Height);
        if (target.Slides && plan.Grid is { } grid)
        {
            clip.left = grid.Left;
            clip.right = grid.Left + grid.WindowWidth;
        }
        dc->PushAxisAlignedClip(&clip, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            if (target.Round)
            {
                var centre = new Vector2((at.left + at.right) / 2, (at.top + at.bottom) / 2);
                PopupPaint.Circle(dc, centre, MathF.Min(at.right - at.left, at.bottom - at.top) / 2, fill);
            }
            else
            {
                PopupPaint.Fill(dc, at, plan.Radius, fill);
            }
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>A block's Join, an arrow, a day, or nothing at <paramref name="point"/> (<see cref="Code"/>): a Join before the day under it.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        LineFocus.At(LineTargets(box, style), point);

    /// <summary>
    /// The Joins seen in the hours' window, then the arrows and the days, each as much of it as can be seen (the
    /// keys' stops, LineFocus; the hit test): a Join before the day under it.
    /// </summary>
    public IReadOnlyList<(int Target, D2D_RECT_F Box)> LineTargets(D2D_RECT_F box, in TileStyle style)
    {
        var plan = Plan(box, style);
        var scrolled = Scroll(plan);
        var targets = new List<(int Target, D2D_RECT_F Box)>();
        void Add(int code, D2D_RECT_F at, D2D_RECT_F window)
        {
            var seen = new D2D_RECT_F(MathF.Max(at.left, window.left), MathF.Max(at.top, window.top), MathF.Min(at.right, window.right), MathF.Min(at.bottom, window.bottom));
            if (seen.right > seen.left && seen.bottom > seen.top)
            {
                targets.Add((code, new D2D_RECT_F(seen.left + box.left, seen.top + box.top, seen.right + box.left, seen.bottom + box.top)));
            }
        }
        if (plan.Grid is { } grid)
        {
            var hours = new D2D_RECT_F(grid.Left, plan.ViewportTop, grid.Left + grid.WindowWidth, plan.ViewportTop + plan.Viewport.Y);
            for (var index = 0; index < grid.Blocks.Count; index++)
            {
                if (TilePaint.Width(grid.Blocks[index].Join) > 0)
                {
                    Add(JoinTarget + index, Placed(grid, plan, scrolled, grid.Blocks[index].Join), hours);
                }
            }
        }
        foreach (var target in plan.Targets)
        {
            // Where it can be seen: a scrolling one in the hours' window, one on the strip of days in its window.
            var window = new D2D_RECT_F(float.MinValue, float.MinValue, float.MaxValue, float.MaxValue);
            if (target.Scrolls)
            {
                (window.top, window.bottom) = (plan.ViewportTop, plan.ViewportTop + plan.Viewport.Y);
            }
            if (target.Slides && plan.Grid is { } strip)
            {
                (window.left, window.right) = (strip.Left, strip.Left + strip.WindowWidth);
            }
            Add(Code(target), Locate(target, plan, scrolled), window);
        }
        return targets;
    }

    /// <summary>A press on a Join lights it pressed; the press stays the folder's, which makes it the click.</summary>
    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (press.Down(CardTarget(point, box, style, surface) is >= JoinTarget and var join ? join : 0))
        {
            Redraw(Gfx);
        }
    }

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var at = point is { } where ? CardTarget(where, box, style, surface) : 0;
        pointed = point is { } over && last is { } plan ? MeetingAt(plan, Scroll(plan), new Vector2(over.X - box.left, over.Y - box.top)) : null;
        var let = press.Over(at);
        if (at != hot || let)
        {
            hot = at;
            Redraw(Gfx);
        }
    }

    /// <summary>The days and the arrows light themselves; the line does not light as well.</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>The arrows say which way they step; a holiday's day or heading what holiday it is; the stale mark when it was last read.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Plan(box, style);
        if (StaleMarkOf(null, box, plan, style) is { } mark && StaleMark.Tip([mark], point, StaleTip, style) is { } said)
        {
            return said;
        }
        foreach (var (code, part) in LineTargets(box, style))
        {
            if (!LineRows.Inside(part, point))
            {
                continue;
            }
            return code switch
            {
                // A Join says the app it joins in: the camera alone says nothing of it.
                >= JoinTarget when plan.Grid is { } grid && grid.Blocks[code - JoinTarget].Link is { } link => (JoinButton.Tip(link), part),
                PreviousTarget => (Before(view), part),
                NextTarget => (After(view), part),
                >= DayTarget and < JoinTarget when CalendarLines.HolidayTip(Store, DateOnly.FromDayNumber(code - DayTarget)) is { } holiday => (holiday, part),
                _ => null,
            };
        }
        return null;
    }

    private static string Before(CalendarView view) => view switch
    {
        CalendarView.Day => S.WidgetCalendarViewPreviousDay,
        CalendarView.Week => S.WidgetCalendarViewPreviousWeek,
        _ => S.WidgetCalendarViewPreviousYear,
    };

    private static string After(CalendarView view) => view switch
    {
        CalendarView.Day => S.WidgetCalendarViewNextDay,
        CalendarView.Week => S.WidgetCalendarViewNextWeek,
        _ => S.WidgetCalendarViewNextYear,
    };

    // --- As pieces ----------------------------------------------------------------------------------

    public string MainPiece => "title";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var plan = Plan(box, style);
        var scrolled = Scroll(plan);
        D2D_RECT_F Moved(D2D_RECT_F at) => new(box.left + at.left, box.top + at.top, box.left + at.right, box.top + at.bottom);
        var pieces = new List<LinePiece>
        {
            new("title", Moved(plan.NavRow with { right = MathF.Max(plan.NavRow.left, plan.TitleEnd) }), plan.NavTitle),
            new("previous", Moved(plan.Previous)),
            new("next", Moved(plan.Next)),
        };
        if (StaleMarkOf(null, box, plan, style) is { } mark)
        {
            pieces.Add(mark);
        }
        foreach (var target in plan.Targets.Where(target => target.Kind == TargetKind.Day && !target.Quiet))
        {
            var at = Locate(target, plan, scrolled);
            var centre = new Vector2((at.left + at.right) / 2, (at.top + at.bottom) / 2);
            if (Seen(target, plan, centre))
            {
                pieces.Add(new LinePiece($"day {target.Day:yyyy-MM-dd}", Moved(at), target.Day.Day.ToString(global::System.Globalization.CultureInfo.InvariantCulture)));
            }
        }
        // The Joins seen in the hours' window, each called by its meeting.
        for (var index = 0; plan.Grid is { } grid && index < grid.Blocks.Count; index++)
        {
            var block = grid.Blocks[index];
            if (TilePaint.Width(block.Join) > 0)
            {
                var at = Placed(grid, plan, scrolled, block.Join);
                if (InHours(grid, plan, new Vector2((at.left + at.right) / 2, (at.top + at.bottom) / 2)))
                {
                    pieces.Add(new LinePiece($"join {index}", Moved(at), JoinButton.Name(block.Title)));
                }
            }
        }
        return pieces;
    }
}
