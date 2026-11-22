// The calendar's day, week and year views laid out as a line of a list (CalendarViewTile,
// docs\widgets.md §80): for the state it shows (CalendarInputs), every rectangle in the line's px and
// every text. The part that stays put (the page's title with its arrows, the day and week views'
// head band and the gutter's label) and the part that scrolls (the day's and week's hours, the
// year's months) are drawn by CalendarPainter. Pure but for text measuring, so a test or an
// offscreen picture draws the same.
//
// They were the calendar flyout's views until the calendar became a preset folder (2026-10-09): the
// flyout's header went to its own line (DateHeaderTile), its month view to the month and the day's
// events (MonthTile, EventsTile), its footer to the actions line. What is left here is the flyout's
// own drawing of each view, at the line's width and on the list's gutter.
//
// The day and week views show the whole day, midnight to midnight, whatever the events
// (DayStrip.Hours), in a window as tall as the line leaves them — ten hours where nothing set its
// height — scrolled to an hour before now. Sideways they are a strip of days longer than the
// window onto it, laid out in px of their own (x along the strip from its first day), the headings
// and the all-day row in a band over the hours; the hours' labels stay put in the gutter beside
// them. The year view is its twelve months in as many columns as the width holds, scrolling where
// the line is shorter than they are.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Panels;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

/// <summary>What a view shows, apart from how it looks: the input of <see cref="CalendarLayout.Build"/>.</summary>
/// <param name="View">Day, week or year (a month is MonthTile's).</param>
/// <param name="Shown">
/// A day of the page shown: its year; in the day and week views the first day of the window onto
/// the days.
/// </param>
/// <param name="Now">The wall-clock time now, for the now-line.</param>
/// <param name="WeekRule">How the region counts weeks (<see cref="CalendarNavigation.WeekOfYear"/>).</param>
/// <param name="EventsIn">The events overlapping a range of wall-clock times (<see cref="CalendarStore.EventsIn"/>).</param>
/// <param name="Width">The line's width, px.</param>
/// <param name="Height">The line's height, px: the view's own part takes what the line has; 0 for the height the view would have.</param>
/// <param name="Pad">The list's gutter, px, which the title, the arrows and the months stand on.</param>
/// <param name="Strip">
/// The day and week views: the first day of the strip laid out last, kept while the window is well
/// inside it (<see cref="DayStrip.Origin"/>); null for a strip around the window.
/// </param>
/// <param name="Reserve">Room kept clear before the arrows, px: the stale mark's (StaleMark.cs), else 0.</param>
internal readonly record struct CalendarInputs(
    CalendarView View,
    DateOnly Today,
    DateOnly Selected,
    DateOnly Shown,
    DateTime Now,
    DayOfWeek FirstDay,
    int WeekRule,
    bool HasCalendars,
    bool Loading,
    string? Problem,
    Func<DateTime, DateTime, IReadOnlyList<CalendarEvent>> EventsIn,
    float Width,
    float Height = 0,
    float Pad = 0,
    DateOnly? Strip = null,
    float Reserve = 0);

/// <summary>What a click on a part of a view does.</summary>
internal enum TargetKind
{
    Previous,
    Next,

    /// <summary>A day, or a week's heading over it: picked, and published as the folder's day.</summary>
    Day,
}

/// <param name="Box">
/// Line px, or px of the scrolling part when <paramref name="Scrolls"/>. On the strip of days
/// (<paramref name="Slides"/>) x is along the strip, and y is the scrolling part's for the hours
/// and the head band's (<see cref="GridPart.Head"/>) for the headings.
/// </param>
/// <param name="Round">Its hover fill is a circle in the box rather than a rounded rectangle.</param>
/// <param name="Quiet">A day's whole column: no tooltip and no piece of its own; it lights with its day (§379).</param>
/// <param name="Slides">On the strip of days.</param>
internal readonly record struct CalendarTarget(
    TargetKind Kind,
    D2D_RECT_F Box,
    bool Round = false,
    bool Scrolls = false,
    bool Quiet = false,
    DateOnly Day = default,
    bool Slides = false);

/// <summary>What a note under a view is.</summary>
internal enum AgendaKind
{
    /// <summary>"No events", or the calendar still loading.</summary>
    Message,

    /// <summary>A small line: why a calendar shows nothing new, or why there is no calendar.</summary>
    Note,
}

/// <param name="Box">The note across the line.</param>
internal readonly record struct AgendaRow(D2D_RECT_F Box, string Title, AgendaKind Kind);

/// <summary>One small month of the year view (px of the scrolling part).</summary>
internal sealed record SmallMonth(D2D_RECT_F Title, string Name, D2D_RECT_F Weekdays, D2D_RECT_F Days, DateOnly First, int Month);

/// <summary>The year view: twelve small months, and the colour of each day with events.</summary>
internal sealed record YearPart(int Year, IReadOnlyList<SmallMonth> Months, string[] Weekdays, float CellWidth, float CellHeight, int Columns, IReadOnlyDictionary<DateOnly, Color> Busy);

/// <summary>A day's heading over its column in the week view (px of the head band: x along the strip).</summary>
internal readonly record struct DayHeading(D2D_RECT_F Box, string Weekday, string Number, DateOnly Day);

/// <summary>An event on the all-day row (px of the head band: x along the strip), or a count of those that don't fit.</summary>
internal readonly record struct AllDayBar(D2D_RECT_F Box, string Title, Color Colour, bool More);

/// <summary>A timed event's block in the hours (x along the strip, y in the scrolling part).</summary>
/// <param name="Text">What it says, at what size (<see cref="EventText"/>); none for a block too small to say anything.</param>
/// <param name="Link">The link its Join opens, where it is a meeting not over (JoinButton.Of); null for none.</param>
/// <param name="Join">Its Join button, in the px <paramref name="Box"/> is in; empty where the block is too small for one (§206).</param>
/// <param name="Worded">The Join says "Join" beside the camera.</param>
/// <param name="Lit">The Join is the accent's: the meeting is on, or starts within the lead.</param>
internal readonly record struct EventBlock(D2D_RECT_F Box, string Title, string Time, string? Location, Color Colour, BlockText? Text = null,
    Karpo.Accounts.MeetingLink? Link = null, D2D_RECT_F Join = default, bool Worded = false, bool Lit = false);

/// <summary>
/// The day and week views: a strip of <paramref name="Days"/> days from <paramref name="First"/>,
/// <paramref name="Visible"/> of them in the window from the one at <paramref name="Shown"/>. Each
/// column is <paramref name="ColumnWidth"/> whole px, the strip's own x running from its first day's
/// left edge, so the columns sit on pixels.
/// </summary>
/// <param name="Left">Where the window onto the strip starts, line px: the gutter's right edge.</param>
/// <param name="Head">
/// The band over the hours, line px over the window: the headings and the all-day row. Empty in a
/// day view without all-day events.
/// </param>
/// <param name="AllDayRow">The all-day row across the window, line px, for its label in the gutter.</param>
/// <param name="Map">Where each minute of the day is in the hours (<see cref="DayStrip.Hours"/>).</param>
/// <param name="NowY">The now-line, when today is on the strip; <paramref name="NowDay"/> is its column.</param>
internal sealed record GridPart(
    DateOnly First,
    int Days,
    int Visible,
    int Shown,
    float Left,
    float ColumnWidth,
    float Gutter,
    D2D_RECT_F Head,
    IReadOnlyList<DayHeading> Headings,
    D2D_RECT_F? AllDayRow,
    IReadOnlyList<AllDayBar> Bars,
    string AllDayLabel,
    HourMap Map,
    string[] Hours,
    IReadOnlyList<EventBlock> Blocks,
    float? NowY,
    int NowDay)
{
    /// <summary>The window's width: the visible days' columns.</summary>
    public float WindowWidth => Visible * ColumnWidth;

    /// <summary>All of the strip's width.</summary>
    public float StripWidth => Days * ColumnWidth;

    /// <summary>How far along the strip the window is, whole px.</summary>
    public float RestSlide => Shown * ColumnWidth;

    /// <summary>Day <paramref name="day"/>'s column's left edge along the strip.</summary>
    public float Column(int day) => day * ColumnWidth;
}

/// <summary>A view laid out: every rectangle in line px (the scrolling part's in its own px), every text.</summary>
internal sealed record CalendarPlan
{
    public required CalendarView View { get; init; }
    public required float Width { get; init; }
    public required float Height { get; init; }
    public required float Scale { get; init; }
    public required float Pad { get; init; }
    public required float Hairline { get; init; }
    public required float TextPx { get; init; }
    public required float SmallPx { get; init; }
    public required float GlyphPx { get; init; }
    public required float Radius { get; init; }
    public required DateOnly Today { get; init; }
    public required DateOnly Selected { get; init; }
    public required DateOnly Shown { get; init; }
    public required D2D_RECT_F NavRow { get; init; }
    public required string NavTitle { get; init; }

    /// <summary>A second, dimmer text after the title: the week's number.</summary>
    public required string NavNote { get; init; }

    public required D2D_RECT_F Previous { get; init; }

    /// <summary>Where the page's title must end, px: before the arrows and the room kept there (<see cref="CalendarInputs.Reserve"/>).</summary>
    public required float TitleEnd { get; init; }
    public required D2D_RECT_F Next { get; init; }
    public required string PreviousGlyph { get; init; }
    public required string NextGlyph { get; init; }

    /// <summary>Where the hairlines go across the line.</summary>
    public required float[] Dividers { get; init; }

    /// <summary>Lines under the view (line px): why there is no calendar, loading, a problem.</summary>
    public required IReadOnlyList<AgendaRow> Notes { get; init; }

    public required YearPart? Year { get; init; }
    public required GridPart? Grid { get; init; }

    /// <summary>Where the scrolling part starts, line px, and the window onto it.</summary>
    public required float ViewportTop { get; init; }

    public required Vector2 Viewport { get; init; }

    /// <summary>How tall all of the scrolling part is; the viewport is at most this.</summary>
    public required float ContentHeight { get; init; }

    /// <summary>Where the scrolling part starts scrolled to.</summary>
    public required float FirstScroll { get; init; }

    public required float WheelStep { get; init; }
    public required IReadOnlyList<CalendarTarget> Targets { get; init; }
    public required float Ring { get; init; }
    public required float Inset { get; init; }
    public required float Bar { get; init; }

    /// <summary>Whether the scrolling part is taller than its window.</summary>
    public bool Scrolls => ContentHeight > Viewport.Y + 0.5f;

    /// <summary>The furthest the scrolling part scrolls.</summary>
    public float MostScroll => MathF.Max(0, ContentHeight - Viewport.Y);
}

internal static class CalendarLayout
{
    /// <summary>
    /// The least an hour of the day and week views is, in DIP at the normal text size: Caption's two
    /// lines and their air, so a half-hour event says its title and an hour its time under it, as
    /// Outlook's hours read. Where the room holds the whole day at more, an hour is more.
    /// </summary>
    public const float HourDip = 48;

    /// <summary>The hours the day and week views show where nothing set the line's height: about what the flyout showed.</summary>
    public const int ShownHours = 10;

    // Segoe Fluent Icons: ChevronUp, ChevronDown, ChevronLeft, ChevronRight.
    public static readonly string UpGlyph = char.ConvertFromUtf32(0xE70E);
    public static readonly string DownGlyph = char.ConvertFromUtf32(0xE70D);
    public static readonly string LeftGlyph = char.ConvertFromUtf32(0xE76B);
    public static readonly string RightGlyph = char.ConvertFromUtf32(0xE76C);

    public static CalendarPlan Build(Gfx gfx, Look look, float scale, CalendarInputs inputs) => new Builder(gfx, look, scale, inputs).Build();

    private sealed class Builder(Gfx gfx, Look look, float scale, CalendarInputs inputs)
    {
        private readonly float textScale = Math.Clamp(look.TextScale > 0 ? look.TextScale : 1, 1, 2.25f);
        private readonly List<CalendarTarget> targets = [];
        private readonly List<float> dividers = [];

        private float TextPx => 14 * scale * textScale;
        /// <summary>The views' labels, a small month's days and the hours: the kit's Caption, never under it (§388).</summary>
        private float SmallPx => LineType.CaptionDip * scale * textScale;

        private float D(float dip) => PopupPaint.Round(dip * scale);

        private float T(float dip) => PopupPaint.Round(dip * scale * textScale);

        private float Hairline => MathF.Max(1, PopupPaint.Round(scale));

        private float Pad => inputs.Pad > 0 ? inputs.Pad : D(16);

        public CalendarPlan Build()
        {
            var view = inputs.View;
            var width = MathF.Max(D(120), inputs.Width);
            float y = 0;

            // The page's title, and the arrows to the page before and after, on the list's gutter: the
            // lines' one button (LineButton), subtle.
            var navRow = PopupPaint.Rect(0, y, width, T(44));
            var buttons = LineButton.Of(gfx, scale, textScale);
            var button = buttons.Height;
            var next = PopupPaint.Rect(width - Pad - button, y + PopupPaint.Round((T(44) - button) / 2), button, button);
            var previous = PopupPaint.Rect(next.left - buttons.Gap - button, next.top, button, button);
            targets.Add(new CalendarTarget(TargetKind.Previous, previous));
            targets.Add(new CalendarTarget(TargetKind.Next, next));
            var (title, note) = NavTitle();
            // The day's title as much of it as the room left of the arrows takes whole.
            if (view == CalendarView.Day)
            {
                var shown = inputs.Shown;
                title = Longest(MathF.Max(1, previous.left - D(8) - inputs.Reserve - Pad), title, Nls.LongDateWithoutYear(shown),
                    Nls.DayAndShortDate(shown), Nls.DayAndShortDate(shown, year: false));
            }
            y += T(44);

            var plan = new PlanParts();
            if (view == CalendarView.Year)
            {
                YearView(plan, width, ref y);
            }
            else
            {
                GridView(plan, width, ref y);
            }

            return new CalendarPlan
            {
                View = view,
                Width = width,
                Height = y,
                Scale = scale,
                Pad = Pad,
                Hairline = Hairline,
                TextPx = TextPx,
                SmallPx = SmallPx,
                GlyphPx = LineButton.Of(gfx, scale, textScale).GlyphPx,

                Radius = D(4),
                Today = inputs.Today,
                Selected = inputs.Selected,
                Shown = inputs.Shown,
                NavRow = navRow,
                NavTitle = title,
                NavNote = note,
                Previous = previous,
                TitleEnd = previous.left - D(8) - inputs.Reserve,
                Next = next,
                // One arrow convention (§388): ‹ › move through time in every view, the year's too.
                PreviousGlyph = LeftGlyph,
                NextGlyph = RightGlyph,
                Dividers = [.. dividers],
                Notes = plan.Notes,
                Year = plan.Year,
                Grid = plan.Grid,
                ViewportTop = plan.ViewportTop,
                Viewport = new Vector2(width, plan.ViewportHeight),
                ContentHeight = plan.ContentHeight,
                FirstScroll = plan.FirstScroll,
                WheelStep = plan.WheelStep,
                Targets = targets,
                Ring = MathF.Max(1.5f, 1.5f * scale),
                Inset = D(2),
                Bar = MathF.Max(2, D(3)),
            };
        }

        /// <summary>The first of <paramref name="forms"/> that fits <paramref name="room"/> in the title's type, else the last.</summary>
        private string Longest(float room, params string[] forms)
        {
            foreach (var form in forms)
            {
                if (PopupPaint.Measure(gfx, form, TextPx, Weight.Semibold).X <= room)
                {
                    return form;
                }
            }
            return forms[^1];
        }

        /// <summary>The page's title: the day, the week's days (and its number), the year.</summary>
        private (string Title, string Note) NavTitle()
        {
            var shown = inputs.Shown;
            switch (inputs.View)
            {
                case CalendarView.Day:
                    return (Nls.LongDate(shown), "");
                case CalendarView.Week:
                {
                    // The seven days in the window: a week, or parts of two.
                    var start = shown;
                    var end = DayStrip.Add(start, 6);
                    // The year only where it isn't this one's.
                    var title = start.Year == inputs.Today.Year && end.Year == inputs.Today.Year
                        ? $"{Nls.MonthDay(start)} – {Nls.MonthDay(end)}"
                        : $"{Nls.ShortDate(start)} – {Nls.ShortDate(end)}";
                    var week = CalendarNavigation.WeekOfYear(start, inputs.FirstDay, inputs.WeekRule);
                    var last = CalendarNavigation.WeekOfYear(end, inputs.FirstDay, inputs.WeekRule);
                    return (title, week == last ? S.DockCalendarWeek(week) : S.DockCalendarWeeks(first: week, last: last));
                }
                default:
                    return (shown.Year.ToString(System.Globalization.CultureInfo.InvariantCulture), "");
            }
        }

        // --- Year ----------------------------------------------------------

        /// <summary>A day of a small month: 22 DIP at the text's scale for its Caption figures (§388: fewer months across, never a smaller text), narrower where a month at that would not fit between the gutters.</summary>
        private float YearCellWidth => MathF.Max(1, MathF.Min(T(22), MathF.Floor((inputs.Width - 2 * Pad) / 7)));

        private float YearCellHeight => T(20);

        private float YearMonthWidth => 7 * YearCellWidth;

        private float YearMonthHeight => T(28) + YearCellHeight + 6 * YearCellHeight;

        private float YearGapX => D(16);

        private float YearGapY => D(12);

        private float YearPad => D(8);

        private float YearHeight(int columns) => 2 * YearPad + 12 / columns * YearMonthHeight + (12 / columns - 1) * YearGapY;

        /// <summary>As many months a row as the width holds between the gutters, a number twelve divides: one, two, three, four, six or twelve.</summary>
        private int YearColumns(float width)
        {
            var columns = Math.Clamp((int)((width - 2 * Pad + YearGapX) / (YearMonthWidth + YearGapX)), 1, 12);
            while (12 % columns != 0)
            {
                columns--;
            }
            return columns;
        }

        private void YearView(PlanParts plan, float width, ref float y)
        {
            var year = inputs.Shown.Year;
            var columns = YearColumns(width);
            var cellWidth = YearCellWidth;
            var cellHeight = YearCellHeight;
            var monthWidth = YearMonthWidth;
            var monthHeight = YearMonthHeight;
            var gapX = YearGapX;
            var gapY = YearGapY;
            var pad = YearPad;
            var notes = NoteRows(width);
            var below = notes.Sum(row => row.Box.bottom - row.Box.top) + (notes.Count > 0 ? Hairline : 0);
            var left = PopupPaint.Round((width - (columns * monthWidth + (columns - 1) * gapX)) / 2);

            var names = new string[7];
            for (var i = 0; i < 7; i++)
            {
                names[i] = Nls.ShortestDayName((DayOfWeek)(((int)inputs.FirstDay + i) % 7));
            }
            var start = new DateOnly(year, 1, 1);
            var end = year < 9999 ? new DateOnly(year + 1, 1, 1) : new DateOnly(9999, 12, 31);
            var busy = new Dictionary<DateOnly, Color>();
            foreach (var (day, colours) in ByDay(start, end.DayNumber - start.DayNumber))
            {
                busy[day] = colours[0];
            }
            var months = new List<SmallMonth>(12);
            for (var month = 1; month <= 12; month++)
            {
                var column = (month - 1) % columns;
                var row = (month - 1) / columns;
                var x = left + column * (monthWidth + gapX);
                var top = pad + row * (monthHeight + gapY);
                var title = PopupPaint.Rect(x, top, monthWidth, T(28));
                var weekdays = PopupPaint.Rect(x, top + T(28), monthWidth, cellHeight);
                var days = PopupPaint.Rect(x, top + T(28) + cellHeight, monthWidth, 6 * cellHeight);
                var first = CalendarGrid.First(year, month, inputs.FirstDay);
                months.Add(new SmallMonth(title, Nls.MonthName(month), weekdays, days, first, month));
                // The month's name is a name, and a day is picked, as in every other view.
                for (var i = 0; i < 42; i++)
                {
                    var day = first.AddDays(i);
                    if (day.Month != month || day.Year != year)
                    {
                        continue;
                    }
                    var box = PopupPaint.Rect(x + i % 7 * cellWidth, days.top + i / 7 * cellHeight, cellWidth, cellHeight);
                    targets.Add(new CalendarTarget(TargetKind.Day, box, Round: true, Scrolls: true, Day: day));
                }
            }
            plan.Year = new YearPart(year, months, names, cellWidth, cellHeight, columns, busy);
            var content = YearHeight(columns);
            Scrolling(plan, ref y, content, ref notes, ref below);
            plan.Notes = Placed(notes, ref y);
            plan.WheelStep = monthHeight / 2;
        }

        // --- Day and week --------------------------------------------------

        private void GridView(PlanParts plan, float width, ref float y)
        {
            var week = inputs.View == CalendarView.Week;
            var visible = week ? 7 : 1;
            // The strip: the window and the days to spare either side of it, the last one kept while
            // the window is well inside it.
            var first = DayStrip.Origin(inputs.Strip, inputs.Shown, visible, settled: true);
            var days = visible + 2 * DayStrip.Spare(visible);
            var shown = Math.Clamp(inputs.Shown.DayNumber - first.DayNumber, 0, days - visible);
            // The strip's events, asked for with the days beyond it, so a day coming into view has its
            // events already.
            var (fetchStart, fetchEnd) = DayStrip.Prefetch(first, days, visible);
            var events = inputs.EventsIn(fetchStart.ToDateTime(TimeOnly.MinValue), fetchEnd.ToDateTime(TimeOnly.MinValue));

            var hours = new string[24];
            float widest = 0;
            float labelHeight = 0;
            for (var hour = 0; hour < 24; hour++)
            {
                hours[hour] = Nls.Time(new DateTime(2000, 1, 3, hour, 0, 0));
                var label = PopupPaint.Measure(gfx, hours[hour], SmallPx, Weight.Regular);
                widest = MathF.Max(widest, label.X);
                labelHeight = MathF.Max(labelHeight, label.Y);
            }
            var allDayLabel = S.DockCalendarAllDay;
            widest = MathF.Max(widest, PopupPaint.Measure(gfx, allDayLabel, SmallPx, Weight.Regular).X);
            // The labels end a gap short of the strip; their start stands on the list's gutter.
            var gutter = MathF.Max(D(40), PopupPaint.Round(widest + D(14)));
            var left = MathF.Max(D(4) + gutter, Pad + PopupPaint.Round(widest) + D(8));
            // Whole px a column, so every day's edge is on a pixel; the last ends on the gutter.
            var columnWidth = MathF.Max(1, MathF.Floor((width - left - Pad) / visible));

            // The head band over the hours: the week's headings, weekday over date, each picking its
            // day as the column under it does.
            var headTop = y;
            float band = 0;
            var headings = new List<DayHeading>();
            // The headings only where the line holds them whole (no kind has a fewest, §166).
            if (week && (inputs.Height <= 0 || y + T(52) <= inputs.Height))
            {
                var row = T(52);
                for (var day = 0; day < days; day++)
                {
                    var date = DayStrip.Add(first, day);
                    var box = PopupPaint.Rect(day * columnWidth, 0, columnWidth, row);
                    headings.Add(new DayHeading(box, Nls.ShortDayName(date.DayOfWeek), date.Day.ToString(System.Globalization.CultureInfo.InvariantCulture), date));
                    targets.Add(new CalendarTarget(TargetKind.Day, Inset(box, D(2)), Day: date, Slides: true));
                }
                band += row;
            }

            // The all-day row: all-day events and those of a day or more, in lanes over the whole
            // strip; past three lanes, two and a count per day.
            var (bars, lanes) = TimeGrid.AllDayBars(events, first, days);
            var shownBars = new List<AllDayBar>();
            D2D_RECT_F? allDayRow = null;
            if (lanes > 0)
            {
                var lane = T(22);
                var full = lanes <= 3 ? lanes : 2;
                var rows = lanes <= 3 ? lanes : 3;
                var rowTop = band + D(4);
                allDayRow = PopupPaint.Rect(left, headTop + band, visible * columnWidth, rows * lane + D(8));
                foreach (var bar in bars.Where(bar => bar.Lane < full))
                {
                    var box = new D2D_RECT_F(
                        bar.First * columnWidth + D(2),
                        rowTop + bar.Lane * lane,
                        bar.Last * columnWidth - D(2),
                        rowTop + bar.Lane * lane + lane - D(2));
                    shownBars.Add(new AllDayBar(box, bar.Event.Title, bar.Event.Colour, More: false));
                }
                if (lanes > 3)
                {
                    for (var day = 0; day < days; day++)
                    {
                        var hidden = bars.Count(bar => bar.Lane >= full && bar.First <= day && bar.Last > day);
                        if (hidden > 0)
                        {
                            var box = PopupPaint.Rect(day * columnWidth + D(2), rowTop + 2 * lane, columnWidth - D(4), lane - D(2));
                            shownBars.Add(new AllDayBar(box, $"+{hidden}", default, More: true));
                        }
                    }
                }
                band += rows * lane + D(8);
            }
            var head = PopupPaint.Rect(left, headTop, visible * columnWidth, band);
            y += band;
            dividers.Add(y);
            y += Hairline;

            // The hours, in px of the scrolling part: the whole day, whatever the events. Their room
            // is the line's, less the notes; where nothing set the line's height, ten hours' worth.
            var notes = NoteRows(width);
            var below = notes.Sum(row => row.Box.bottom - row.Box.top) + (notes.Count > 0 ? Hairline : 0);
            var segments = TimeGrid.Segments(events, first, days);
            // The margin over the first line holds half its label, which is centred on it, however large the text.
            var margin = MathF.Max(D(10), MathF.Ceiling(labelHeight / 2) + D(1));
            var natural = ShownHours * T(HourDip);
            var room = inputs.Height > 0 ? Window(ref notes, ref below, y) : natural;
            var map = DayStrip.Hours(MathF.Max(D(96), room), margin, T(HourDip));
            var content = map.Height;

            // The events: as tall as their time, a line of title at the least, side by side where
            // they overlap as drawn.
            var least = T(22);
            var blocks = new List<EventBlock>();
            var air = D(2) + D(1);
            float LineAt(float px) => PopupPaint.Measure(gfx, "Ag", px, Weight.Semibold).Y;
            var blockPx = EventText.Px(map.Hour - 2 * D(1), air, dip => dip * scale * textScale, LineAt);
            var blockLine = LineAt(blockPx);
            var stripe = MathF.Max(2, D(3));
            for (var day = 0; day < days; day++)
            {
                var own = segments.Where(segment => segment.Day == day).ToList();
                var spans = new List<(double Start, double End)>(own.Count);
                for (var i = 0; i < own.Count; i++)
                {
                    var top = map.Y(own[i].Start);
                    var bottom = map.Y(own[i].End);
                    spans.Add((top, MathF.Max(bottom, top + least)));
                }
                var placed = TimeGrid.Arrange(spans, 0);
                var across = columnWidth - D(4);
                for (var i = 0; i < own.Count; i++)
                {
                    var (segment, place) = (own[i], placed[i]);
                    var x = day * columnWidth + D(2) + place.Column * across / place.Columns;
                    var right = day * columnWidth + D(2) + (place.Column + place.Span) * across / place.Columns;
                    var box = new D2D_RECT_F(PopupPaint.Round(x), PopupPaint.Round((float)spans[i].Start) + D(1),
                        PopupPaint.Round(right) - D(2), PopupPaint.Round((float)spans[i].End) - D(1));
                    var item = segment.Event;
                    var time = item.End > item.Start
                        ? $"{Nls.Time(item.Start)}–{Nls.Time(item.End)}"
                        : Nls.Time(item.Start);
                    // The text's room: past the stripe and its gap, short of the right edge; down the
                    // block inside its air.
                    var room2 = box.right - (box.left + stripe + D(5)) - D(3);
                    var down = box.bottom - box.top - air;
                    // A meeting not over has its Join where the block holds one, and the words make room for it.
                    var link = JoinButton.Of(item, inputs.Now);
                    var (join, worded) = link is not null ? JoinIn(box, stripe, blockLine, ref room2, ref down) : (default, false);
                    var says = EventText.Lay(item.Title, time, item.Location, blockPx, blockLine, room2, down,
                        (words, bold) => PopupPaint.Measure(gfx, words, blockPx, bold ? Weight.Semibold : Weight.Regular).X);
                    blocks.Add(new EventBlock(box, item.Title, time, item.Location, item.Colour, says,
                        link, join, worded, link is not null && JoinButton.Lit(item, inputs.Now)));
                }
            }

            // The now-line, on today's column when the strip has today.
            float? nowY = null;
            var nowDay = inputs.Today.DayNumber - first.DayNumber;
            if (nowDay >= 0 && nowDay < days)
            {
                nowY = PopupPaint.Round(map.Y(inputs.Now.TimeOfDay.TotalMinutes));
            }

            plan.Grid = new GridPart(first, days, visible, shown, left, columnWidth, gutter, head, headings, allDayRow, shownBars,
                allDayLabel, map, hours, blocks, nowY, nowDay);
            // A click in a day's column picks the day.
            for (var day = 0; day < days; day++)
            {
                targets.Add(new CalendarTarget(TargetKind.Day, PopupPaint.Rect(day * columnWidth, 0, columnWidth, content),
                    Scrolls: true, Quiet: true, Day: DayStrip.Add(first, day), Slides: true));
            }

            // The window onto the hours takes all the room it was given: the hours scroll in it.
            plan.ViewportTop = y;
            plan.ViewportHeight = MathF.Max(0, PopupPaint.Round(MathF.Min(room, content)));
            plan.ContentHeight = content;
            y += plan.ViewportHeight;
            plan.Notes = Placed(notes, ref y);
            // Opened an hour before now, as Outlook opens on the time it is (DayStrip.OpenBefore).
            plan.FirstScroll = DayStrip.FirstScroll(map, inputs.Now.TimeOfDay.TotalMinutes - DayStrip.OpenBefore,
                MathF.Max(0, content - plan.ViewportHeight));
            plan.WheelStep = map.Hour;
        }

        // How wide the Join with the camera and "Join" is, measured once.
        private float? joinWide;

        /// <summary>
        /// A meeting's Join on its block <paramref name="box"/> (§206), in the px the block is in, and the room it
        /// leaves the words (<paramref name="room"/> across, <paramref name="down"/> down from their top):
        /// against the block's right edge, 24 DIP tall or the block's height, where the words keep 48 DIP
        /// beside it — "Join" with the camera where that holds it, else the camera alone; in a block too
        /// narrow for that, under a line of words at its foot; failing that, beside a little of them; else
        /// none, and the event's menu has "Join meeting".
        /// </summary>
        private (D2D_RECT_F Join, bool Worded) JoinIn(D2D_RECT_F box, float stripe, float line, ref float room, ref float down)
        {
            var high = box.bottom - box.top;
            var across = box.right - box.left;
            var right = box.right - D(2);
            var buttons = LineButton.Of(gfx, scale, textScale);
            var tall = MathF.Min(buttons.Height, high - D(2));
            joinWide ??= JoinButton.WordedWidth(gfx, buttons);
            var wide = joinWide.Value;
            // Beside the words, level with their first line; in a block no taller than the button, in its middle.
            float Top() => high <= tall + D(6) ? box.top + PopupPaint.Round((high - tall) / 2) : box.top + D(2);
            if (tall >= D(16))
            {
                foreach (var (width, worded) in new[] { (wide, true), (tall, false) })
                {
                    if (room - width - D(4) >= T(48))
                    {
                        room -= width + D(4);
                        return (PopupPaint.Rect(right - width, Top(), width, tall), worded);
                    }
                }
            }
            // At the block's foot, under a line of its words at the least.
            // A week's narrow column: the camera on a square as wide as the block leaves beside its stripe.
            var foot = MathF.Min(buttons.Height, across - stripe - D(4));

            if (foot >= D(16) && high >= D(2) + line + foot + D(2))
            {
                var worded = wide <= across - stripe - D(8);
                var width = worded ? wide : foot;
                var top = box.bottom - D(2) - foot;
                down = MathF.Max(0, top - (box.top + D(2)));
                return (PopupPaint.Rect(right - width, top, width, foot), worded);
            }
            // Beside a little of the words.
            if (tall >= D(16) && room - tall - D(4) >= T(24))
            {
                room -= tall + D(4);
                return (PopupPaint.Rect(right - tall, Top(), tall, tall), false);
            }
            return (default, false);
        }

        // --- Shared --------------------------------------------------------

        /// <summary>
        /// The scrolling part's window at <paramref name="y"/>: all of it where the line has the room
        /// (or nothing set its height), else what the line leaves after <paramref name="below"/>, and
        /// at least a little.
        /// </summary>
        private void Scrolling(PlanParts plan, ref float y, float content, ref List<AgendaRow> notes, ref float below)
        {
            var window = inputs.Height > 0 ? MathF.Min(content, Window(ref notes, ref below, y)) : content;
            plan.ViewportTop = y;
            plan.ViewportHeight = MathF.Max(0, PopupPaint.Round(window));
            plan.ContentHeight = content;
            y += plan.ViewportHeight;
        }

        /// <summary>
        /// The room the scrolling part has in a line of a set height, from <paramref name="y"/>: what the
        /// line leaves after the notes; where that is short of a little (96 DIP), the notes go and it is
        /// what the line leaves; and less than a row's worth (24 DIP) is none. A line is any height now
        /// (§166): nothing here may reach past it, the notes under the hours least of all.
        /// </summary>
        private float Window(ref List<AgendaRow> notes, ref float below, float y)
        {
            var room = inputs.Height - y - below;
            if (room < D(96) && notes.Count > 0)
            {
                notes = [];
                below = 0;
                room = inputs.Height - y;
            }
            return room >= D(24) ? room : 0;
        }

        /// <summary>
        /// Why there is no calendar to show, in words for the person: that it is being read, else
        /// what an account said (which is also where "add an account" is said), else that the
        /// accounts hold none.
        /// </summary>
        private string WithoutCalendars() =>
            inputs.Loading ? S.DockCalendarReadingCalendars
            : inputs.Problem is { Length: > 0 } problem ? problem
            : S.DockCalendarNoCalendars;

        /// <summary>The lines under a view: why there is no calendar, loading, a problem.</summary>
        private List<AgendaRow> NoteRows(float width)
        {
            var rows = new List<AgendaRow>();
            if (!inputs.HasCalendars)
            {
                rows.Add(new AgendaRow(PopupPaint.Rect(0, 0, width, T(28)), WithoutCalendars(), AgendaKind.Note));
                return rows;
            }
            if (inputs.Loading)
            {
                rows.Add(new AgendaRow(PopupPaint.Rect(0, 0, width, T(28)), S.DockCalendarLoading, AgendaKind.Note));
            }
            if (inputs.Problem is { Length: > 0 } problem)
            {
                rows.Add(new AgendaRow(PopupPaint.Rect(0, 0, width, T(28)), problem, AgendaKind.Note));
            }
            return rows;
        }

        /// <summary>The notes put at <paramref name="y"/>, one under another, over a hairline.</summary>
        private List<AgendaRow> Placed(List<AgendaRow> notes, ref float y)
        {
            if (notes.Count == 0)
            {
                return notes;
            }
            dividers.Add(y);
            y += Hairline;
            var placed = new List<AgendaRow>(notes.Count);
            foreach (var note in notes)
            {
                var height = note.Box.bottom - note.Box.top;
                var box = PopupPaint.Rect(note.Box.left, y, note.Box.right - note.Box.left, height);
                placed.Add(note with { Box = box });
                y += height;
            }
            return placed;
        }

        /// <summary>
        /// The events from <paramref name="first"/> on for <paramref name="days"/> days, by day: the
        /// colours of their calendars, each once, in the order the events come.
        /// </summary>
        private Dictionary<DateOnly, List<Color>> ByDay(DateOnly first, int days) =>
            CalendarLayout.ByDay(inputs.HasCalendars ? inputs.EventsIn : null, first, days);

        private static D2D_RECT_F Inset(D2D_RECT_F rect, float by) => new(rect.left + by, rect.top + by, rect.right - by, rect.bottom - by);
    }

    /// <summary>
    /// The events from <paramref name="first"/> on for <paramref name="days"/> days, by day: the
    /// colours of their calendars, each once, in the order the events come. None without
    /// <paramref name="eventsIn"/> (no calendars). Shared with the month's dots (MonthTile).
    /// </summary>
    internal static Dictionary<DateOnly, List<Color>> ByDay(Func<DateTime, DateTime, IReadOnlyList<CalendarEvent>>? eventsIn, DateOnly first, int days)
    {
        var found = new Dictionary<DateOnly, List<Color>>();
        if (eventsIn is null || days <= 0)
        {
            return found;
        }
        var origin = first.ToDateTime(TimeOnly.MinValue);
        foreach (var item in eventsIn(origin, origin.AddDays(days)))
        {
            // Each day the event is on in the range; an event without a length on its start's.
            var from = Math.Max(0, (int)Math.Floor((item.Start - origin).TotalDays));
            var to = item.End > item.Start
                ? Math.Min(days, (int)Math.Ceiling((item.End - origin).TotalDays))
                : from + 1;
            for (var day = from; day < to && day < days; day++)
            {
                var date = first.AddDays(day);
                if (!found.TryGetValue(date, out var colours))
                {
                    found[date] = colours = [];
                }
                if (!colours.Contains(item.Colour))
                {
                    colours.Add(item.Colour);
                }
            }
        }
        return found;
    }

    /// <summary>What each view puts into the plan.</summary>
    private sealed class PlanParts
    {
        public IReadOnlyList<AgendaRow> Notes { get; set; } = [];
        public YearPart? Year { get; set; }
        public GridPart? Grid { get; set; }
        public float ViewportTop { get; set; }
        public float ViewportHeight { get; set; }
        public float ContentHeight { get; set; }
        public float FirstScroll { get; set; }
        public float WheelStep { get; set; }
    }
}
