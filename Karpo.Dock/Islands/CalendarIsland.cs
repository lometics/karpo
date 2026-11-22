// The calendar in a folder: today and the events coming, from every calendar.
// On the taskbar the calendar is a folder wearing the calendar face (the calendar
// preset, 2026-10-09), so this class draws no cell of a bar.
//
// In a list folder the calendar is an agenda: the next events of every calendar in one
// column (§378), on the kit's blocks (§401) — the kit's plain Header, the calendar's name and
// its refresh (§419), each event one compact row of one line (§413, §418): its calendar's colour bar, its title and whose
// holiday it is after the title, and when it is, always, on the right of that line ("09:00–10:00",
// §414); every day, today too, a heading over its events.
// Laid out for the height the line is given, as every list line is: one row is the next
// thing coming, and taller the header and the events, scrolling under it where there are
// more than the line holds (ListScroll.cs).
//
// The events are the shared CalendarStore's, which reads them from the
// person's accounts.
// THE EVENTS ARE READ FROM THE DOCK'S START. The dock opens the store at its
// start (Pim\PimSync.cs) whatever the config names — a line in a folder nobody
// opened, in a layout no display shows, or none — so a folder that opens shows
// the events at once. Making the
// island only makes the object, and Configure only says which settings it has;
// the widget asks the store as well the first time it is drawn as a folder's
// line, or clicked (Look), which is what opens a test's own store (Made)
// and starts only what is due in the shared one (docs\widgets.md §11).
//
// The first ask goes over the network to every account, so it must not block:
// Look posts the work and returns, and until the answer arrives the line draws
// today with nothing coming under it. A line that is briefly bare, never one that
// is briefly wrong. What keeps --smoke and the offscreen pictures away
// from the accounts is the store, not this: an isolated config's shared store
// holds no accounts at all (CalendarStore).
//
// Every event is an item (§434), drawn and taken by the pointer as the day's events' are (AgendaEvents, §447): lit
// edge to edge under the pointer (LineItem, §428), and a click on its row opens it in a calendar once the folder has
// gone (EventLinks: its page, its calendar's day, the calendar app). In the whole agenda a meeting not over has its
// Join at the trailing end of its row (JoinButton.cs, docs\widgets.md §206), a button on the item: a click there joins
// it, Enter the lit one, and a right click on its row has "Join meeting". The shorter forms have no room
// for the button; their menu has the row all the same.

using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using System.Globalization;
using System.Numerics;

namespace Karpo.Dock;

internal sealed partial class CalendarIsland : CellIsland, IListContent, IListScroll, ILinePieces, IMeetingLine, ILineTargets, IRefreshable
{
    // The events as last laid out, and what the pointer and the keys do with them.
    private readonly AgendaEvents agenda;

    /// <summary>Where the events come from: the shared store, or a test's own (<see cref="Made"/>).</summary>
    private readonly CalendarStore store;

    [ThreadStatic]
    private static CalendarStore? made;

    /// <summary>
    /// A test's own store, per thread, read as the widget is made: its events are the widget's, and
    /// the shared store — the accounts' — is never asked. Unset, the widget reads the shared store.
    /// </summary>
    internal static CalendarStore? Made
    {
        get => made;
        set => made = value;
    }

    // Whether anybody has looked at this widget yet (Look).
    private bool looked;

    public CalendarIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        store = CalendarLines.Store;
        agenda = new AgendaEvents(Hidden);
        Configure(island, config);
    }

    /// <summary>Never ticks: the store reads on its own clock and wakes this widget when the events change.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    /// <summary>
    /// Asks the store for events, the first time anybody looks at this widget — drawn as a folder's
    /// line, or clicked. Once: the shared store has been open since the dock's start
    /// (Pim.PimSync), and asking starts only what is due; a test's own store (Made) opens here.
    /// </summary>
    /// <remarks>
    /// Called on the dock's own thread, so what it starts must not block — <see cref="CalendarStore.Open"/>
    /// posts its work and returns. There is no tick to hang this on: the store reads again on its own.
    /// </remarks>
    private void Look()
    {
        if (looked)
        {
            return;
        }
        looked = true;
        store.Open();
    }

    /// <summary>
    /// "Refresh" (IRefreshable): every account's calendars read again now, whatever is due
    /// (<see cref="CalendarStore.Refresh"/>): a read already running is not started twice.
    /// </summary>
    public void RefreshNow()
    {
        looked = true;
        store.Refresh();
    }

    /// <summary>Calendars to read again: an account's, or one of the store's own.</summary>
    public bool CanRefresh => store.CanReachAccounts || store.HasCalendars;

    /// <summary>The accounts' calendars are being read.</summary>
    public bool Refreshing => store.Reading;

    /// <summary>When the events shown were read.</summary>
    public DateTime? UpdatedUtc => store.Freshness.UpdatedUtc;

    /// <summary>Why the events could not be read: an account's or a calendar's problem, or "Last updated 14:05 — couldn't refresh".</summary>
    string? IRefreshable.Problem => store.Problem ?? store.Freshness.Tip(Clock.Now());

    public override void Configure(WidgetItem island, Config config)
    {
        // The shape is a function of the settings, so the shape must be given the
        // settings just handed over: keeping the ones this island was born with
        // left the size measured for the old options while the drawing used the
        // new ones (a folder whose name was switched on kept its square).
        Settings = island;
        // Nothing of the calendar's options is read here: how far ahead it lists (AgendaDays, §496) is
        // read from the settings as it lists, and the feed and its refresh are retired.
        // Only to be woken: attaching reads nothing and opens nothing.
        store.Attach(this);
        Forget();
    }

    /// <summary>No hover tooltip: the line says what it has.</summary>
    public override string? Tooltip(int index) => null;

    /// <summary>
    /// A line of a folder: a click on it is a look, and on event <paramref name="index"/>'s row (from 1) opens the
    /// event in a calendar once the folder has gone (§434); on its Join (<see cref="JoinButton.Target"/> plus its
    /// place) joins the meeting; with no part, after Enter or Space, the lit one's (§206).
    /// </summary>
    public override IslandAction Click(int index)
    {
        Look();
        return agenda.Click(index);
    }

    /// <summary>The dock, once the folder has gone: opens the meeting a Join chose, or the event a row did, once.</summary>
    public void Open() => agenda.Open();

    /// <summary>Enter and Space join the lit meeting, as an accent button is a dialog's default.</summary>
    protected internal override bool CardKey(int key, WidgetSurface surface) => agenda.Key(key);

    /// <summary>The meeting of the row under the pointer, in any form of the line, where it has a Join.</summary>
    public (string Title, Karpo.Accounts.MeetingLink Link)? MenuMeeting() => agenda.MenuMeeting();

    /// <summary>Closed, nothing is under the pointer, pressed or chosen.</summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            agenda.Forget();
        }
    }

    /// <summary>Its events and their Join buttons light themselves; the line as a whole is not one thing to press.</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>A new minute: what is coming, and when, is worded against it, so the line follows it.</summary>
    public override bool MinuteChanged(Gfx gfx) => Redraw(gfx);

    public override void Dispose()
    {
        store.Detach(this);
        base.Dispose();
    }

    /// <summary>
    /// The store posted: it raises Changed, so an open calendar shows what arrived. The redraw
    /// that follows picks up the events. Nothing is started here — the panel does that.
    /// </summary>
    protected override bool Sample(bool everything)
    {
        if (everything)
        {
            store.Deliver();
        }
        return false;
    }

    /// <summary>
    /// Never drawn on a bar: this is the calendar's line of a list, which is always its card
    /// (<see cref="CellIsland.CardOnly"/>), so <see cref="CellIsland.PaintOn"/> never asks for a cell.
    /// </summary>
    internal override unsafe void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is the calendar's line; on a bar it is the date.");

    // --- In a folder ------------------------------------------------------------
    // The calendar in a folder: a line of a list — the next
    // events of every calendar (CalendarStore), as many as the line has room for, each with its
    // calendar's colour. In a folder a click opens no popup, as no click in a folder does: on an event it
    // opens the event in a calendar once the folder has gone (§434); on the bar the calendar is the date,
    // whose click opens the calendar.
    //
    // It reads the store through EventsIn, HasCalendars, Loading and Problem, and words days and
    // times with CalendarText.

    protected internal override unsafe void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Look();
        Line(new LineSketch(dc, Gfx), box, style);
    }

    /// <summary>
    /// A meeting's Join at <paramref name="point"/> (<see cref="JoinButton.Target"/> plus its event's place), else the
    /// row of an event that opens somewhere, its place from 1; 0 anywhere else, which opens nothing.
    /// </summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        // Laid out for this box, so the rows are where they are drawn.
        _ = LinePieces(box, style);
        return agenda.Target(point);
    }

    /// <summary>keys: its Joins and its events, laid out for this box (ILineTargets).</summary>
    public IReadOnlyList<(int Target, D2D_RECT_F Box)> LineTargets(D2D_RECT_F box, in TileStyle style)
    {
        _ = LinePieces(box, style);
        return agenda.Targets();
    }

    /// <summary>The event or the Join under the pointer lights; the event under it is the one a right click is about.</summary>
    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (agenda.Hover(point, point is { } at ? CardTarget(at, box, style, surface) : 0))
        {
            Redraw(Gfx);
        }
    }

    /// <summary>A press on an event or a Join lights it pressed; the press stays the folder's, which makes it the click.</summary>
    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (agenda.Down(CardTarget(point, box, style, surface)))
        {
            Redraw(Gfx);
        }
    }

    /// <summary>"Last updated 14:05 — couldn't refresh" while a calendar could not be read and what it gave before is shown (StaleMark.cs).</summary>
    internal override string? StaleTip => store.Freshness.Tip(Clock.Now());

    /// <summary>The stale mark's tooltip, over the mark; a Join's, the app it joins in.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        agenda.Tip(CardTarget(point, box, style, surface)) ?? StaleMark.Tip(LinePieces(box, style), point, StaleTip, style);

    /// <summary>
    /// What stands where the events would, when there are none to show and none are known of: that the
    /// calendars are still being read, or what went wrong. Null when there are events, and when there are
    /// none to read: an empty day says so under its heading (<see cref="NoEvents"/>). Drawing this is a look,
    /// so the read is already on its way; until it lands the words say so, and never a count they have not got.
    /// </summary>
    private string? Message(List<(DateOnly Day, CalendarEvent Event)> events) =>
        events.Count > 0 ? null : store.Loading ? S.WidgetCalendarReading : store.Problem;

    /// <summary>Why the one row shows nothing coming: nothing in its days ahead, or nothing on the picked day.</summary>
    private string Nothing(DateTime now) => Picked(now) is null ? S.WidgetCalendarNothingComing : S.WidgetCalendarNothingOnDay;

    /// <summary>
    /// How many days the line lists events of, today the first: its "Days ahead" (§496, the person, 2026-11-03: "for the
    /// agenda you should be able to set how many days into the future it should show events"), a week where nobody set
    /// it (<see cref="CalendarOptions.DaysAheadDefault"/>) — every event of each, scrolling where the room holds fewer.
    /// The store reads it all: the calendars are read from last month to a year ahead (PimCache.Window), far past the
    /// most (<see cref="CalendarOptions.DaysAheadMost"/>), and asking for a range widens what is read (AccountCalendars.Want).
    /// </summary>
    internal int AgendaDays => CalendarOptions.DaysAheadOf(Settings);

    /// <summary>
    /// The events coming on today and the days after it, <see cref="AgendaDays"/> in all, that
    /// haven't ended, the ones going on now first.
    /// </summary>
    internal List<CalendarEvent> Upcoming(DateTime now)
    {
        var list = new List<CalendarEvent>();
        foreach (var item in store.EventsIn(now.Date, now.Date.AddDays(AgendaDays), shown: SourceFilter.Of(Settings)))
        {
            if (item.End > now || (item.End <= item.Start && item.Start >= now) || (item.AllDay && item.Start.Date == now.Date))
            {
                list.Add(item);
            }
        }
        return list;
    }

    // --- As a line of a list ------------------------------------------------------
    //
    // Laid out for the height it is given, as every list line is (docs\widgets.md §9, "A line at
    // every height"), in one column (2026-10-28, §378: "the agenda should not have the date on the side,
    // it should have it on top. (only one column)"), on the kit's blocks (§401). One row is the next thing
    // coming — what it is where every name begins, when it is against the trailing gutter. Taller, the
    // kit's plain Header, as mail's and the feed's — the calendar's glyph, its name in the Body strong, the stale mark
    // at its end — over the events, each one compact row of one line (§413, 32 DIP since §418): its calendar's colour bar in the leading
    // column, its title at the text edge, when it is against the trailing gutter (§414); batched by day, every day a heading
    // of its own over its events, today's too ("Today", "Tomorrow", "Saturday, 26 September"): the header names no day (§419;
    // the person, 2026-10-30: "the agenda view should show todays date just normally and not this special as it does now").
    // Past that the line shows more events and never larger ones; given less than they take, the events
    // scroll under the header (ListScroll.cs).

    /// <summary>What a line of one row shows first: when the next thing comes, or why nothing does.</summary>
    public string MainPiece => "next";

    public unsafe IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(null, Gfx);
        Line(sketch, box, style);
        return sketch.Pieces;
    }

    /// <summary>
    /// How tall the line is with nothing left out: the header over every event coming, or over why none
    /// is — where a new line starts.
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        Look();
        var now = Clock.Now();
        var events = Agenda(now);
        return RowHeight(style) + (Message(events) is not null
            ? RowHeight(style)
            : Compact(style) * (events.Count + events.Select(entry => entry.Day).Distinct().Count() + (FirstEmpty(events, now) ? 2 : 0)));
    }

    /// <summary>
    /// The header and the events, where the line is tall enough for the header and an event and shorter
    /// than all of them: the events scroll under the header, which stays (ListScroll.cs).
    /// </summary>
    public float ScrollHeight(float width, float height, in TileStyle style)
    {
        var events = Agenda(Clock.Now());
        if (events.Count == 0 || Message(events) is not null || OneRowOnly(height, style))
        {
            return 0;
        }
        return ListHeight(width, style);
    }

    /// <summary>The header stays where the events scroll under it.</summary>
    float IListScroll.ScrollHead(float width, float content, in TileStyle style) => RowHeight(style);

    // --- The folder's day -----------------------------------------------------------------------
    //
    // The line follows the folder's day (Channels.Day, docs\widgets.md §9, "Channels"): where a month
    // beside it has a day picked, the agenda is that day's — under the header that day's heading, as
    // any day's ("Tomorrow", "Yesterday", the long date), and under it every event of the day, the ones
    // over already too, or "No events". With no day picked or today picked, it is what it always
    // was: today and the events coming.

    /// <summary>The day somebody picked in the folder, where this line follows it; null for today and the days coming.</summary>
    internal DateOnly? Picked(DateTime now) =>
        Followed(Channels.Day) is { } day && day != DateOnly.FromDateTime(now) ? day : null;

    /// <summary>
    /// The events the line lists, each under a day it is on: the picked day's, every one of them; else the ones coming
    /// (<see cref="Upcoming"/>), one longer than a day under every day of it listed. By day, and on each what fills it
    /// first — an all-day event, a day in the middle of a longer one — then by start.
    /// </summary>
    internal List<(DateOnly Day, CalendarEvent Event)> Agenda(DateTime now)
    {
        var picked = Picked(now);
        var first = First(now);
        var end = first.AddDays(picked is null ? AgendaDays : 1);
        var from = first.ToDateTime(TimeOnly.MinValue);
        var listed = new List<(DateOnly Day, CalendarEvent Event)>();
        foreach (var item in picked is null ? Upcoming(now) : store.EventsIn(from, from.AddDays(1), shown: SourceFilter.Of(Settings)))
        {
            // Its last day: an end at midnight is the day before's.
            var last = DateOnly.FromDateTime(item.End > item.Start ? item.End.AddTicks(-1) : item.Start);
            var day = DateOnly.FromDateTime(item.Start);
            for (day = day < first ? first : day; day <= last && day < end; day = day.AddDays(1))
            {
                listed.Add((day, item));
            }
        }
        // Stable, so the store's order (by start) stands within each day.
        return [.. listed.OrderBy(entry => entry.Day).ThenBy(entry => !AgendaEvents.FillsDay(entry.Event, entry.Day))];
    }

    /// <summary>The day the agenda starts on, which always has its heading: the picked one, else today.</summary>
    private DateOnly First(DateTime now) => Picked(now) ?? DateOnly.FromDateTime(now);

    /// <summary>
    /// Whether nothing is on the first day: its heading stands all the same, over "No events" (<see cref="NoEvents"/>;
    /// the person, 2026-10-30: "in the agenda today should also appear if no event is there and then it should just say no event").
    /// </summary>
    private bool FirstEmpty(List<(DateOnly Day, CalendarEvent Event)> events, DateTime now) => events.Count == 0 || events[0].Day != First(now);

    /// <summary>The header's height, and the one row's: the kit's row with no caption under it (40 DIP).</summary>
    private static float RowHeight(in TileStyle style) => LineRow.Height(style, caption: false);

    /// <summary>
    /// An event's height and a day's heading's: the kit's compact row (32 DIP, §418; the person, 2026-10-30: "make the agenda
    /// view a little more dense"). An event is one line (§413): its title, whose holiday it is after it, when it is at its end (§414).
    /// </summary>
    private static float Compact(in TileStyle style) => AgendaEvents.Compact(style);

    /// <summary>
    /// Whether a line <paramref name="height"/> px tall is one row: shorter than the header, a day's heading and an event —
    /// the least that shows anything coming under its day; a header over a heading alone would show nothing.
    /// </summary>
    private bool OneRowOnly(float height, in TileStyle style) => height + 0.5f < RowHeight(style) + (2 * Compact(style));

    /// <summary>
    /// The line, at whatever height it was given (docs\widgets.md §9, "A line at every height"), drawn
    /// where the sketch draws, on the kit's blocks (§401):
    /// <list type="bullet">
    /// <item>One row: the calendar's glyph, what the next thing coming is where every name begins, when
    /// it is against the trailing gutter; with nothing coming, the day and why.</item>
    /// <item>Taller: the header — the glyph, the calendar's name, the stale mark at its
    /// end — over the days' headings and their events as the kit's items, as many as fit, an empty first day its heading over "No events"; over why there are none while the calendars are read or could not be.</item>
    /// </list>
    /// </summary>
    private unsafe void Line(LineSketch sketch, D2D_RECT_F box, in TileStyle style)
    {
        Look();
        agenda.Clear();
        var form = ListForm.Of(Gfx, style);
        var theme = style.Theme;
        var now = Clock.Now();
        var events = Agenda(now);
        var message = Message(events);
        // Stale, the mark at the trailing gutter: the first row kept clear of it (StaleMark.cs).
        var stale = store.Freshness.Stale;
        var mark = StaleMark.Room(style, form, stale);
        if (OneRowOnly(TilePaint.Height(box), style))
        {
            OneRow(sketch, box, style, now, events, events.Count == 0 ? message ?? Nothing(now) : null, mark);
        }
        else
        {
            // The kit's plain Header, as mail's and the feed's: the calendar's glyph and name, the stale mark at its end. No
            // date: today is a day like the others, a heading over its events (§419).
            var header = LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec(Info.Title)
            {
                Glyph = Info.Glyph,
                Strong = true,
                Trailing = mark,
                NamePiece = "head",
            });
            if (message is not null)
            {
                LineStatus.Draw(sketch, PopupPaint.Rect(box.left, header.Bottom, TilePaint.Width(box), LineRows.Px(1, style)), style, null, message, []);
            }
            else
            {
                Items(sketch, box, style, events, now, header.Bottom);
            }
        }
        if (stale)
        {
            StaleMark.Sketch(sketch, box, style, form, box.top, LineRows.Px(1, style));
        }
    }

    /// <summary>
    /// One row: the calendar's glyph in the list's leading column, what the next thing coming is where
    /// every name begins and when it is against the trailing gutter, <paramref name="mark"/> kept clear
    /// after it for the stale mark; with nothing coming, the day and why nothing is.
    /// </summary>
    private void OneRow(LineSketch sketch, D2D_RECT_F box, in TileStyle style, DateTime now, List<(DateOnly Day, CalendarEvent Event)> events, string? message, float mark)
    {
        var theme = style.Theme;
        // The day the line shows: the picked one, else today.
        var shown = Picked(now)?.ToDateTime(TimeOnly.MinValue) ?? now;
        var row = MathF.Min(TilePaint.Height(box), LineRows.Px(1, style));
        // The next event is the row's item (§434): lit under the pointer, opening it.
        var opens = message is null ? EventLinks.Target(events[0].Event, events[0].Day, store) : null;
        if (opens is not null && agenda.Lit(0))
        {
            LineItem.Light(sketch, box with { bottom = box.top + row }, theme, agenda.Pressed(0));
        }
        LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec(message is null ? events[0].Event.Title : Dates.Format(shown, "dddd, d MMMM"))
        {
            Glyph = Info.Glyph,
            Reading = message ?? When(events[0].Event, now),
            Look = RowReading.Body,
            ReadingInk = message is null ? theme.Text : theme.TextSecondary,
            Trailing = mark,
            NamePiece = message is null ? "title" : "day",
            ReadingPiece = "next",
        }, row);
        // The row is the next event's, which it opens, and for its menu's "Join meeting": no room here for the button.
        if (message is null)
        {
            var next = events[0].Event;
            agenda.Add(new AgendaEvents.Row(box with { bottom = box.top + row }, next, JoinButton.Of(next, now), default, JoinButton.Lit(next, now), opens));
        }
    }

    /// <summary>
    /// When an event is, as short as it is said: "Now" while it goes on, its start time today, "All
    /// day", and the day in front of anything not today ("Tomorrow 09:30", "Thu 08:00").
    /// </summary>
    private static string When(CalendarEvent item, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var on = DateOnly.FromDateTime(item.Start < now ? now : item.Start);
        var time = item.AllDay ? S.WidgetCalendarAllDay : item.Start <= now && item.End > now ? S.WidgetCalendarNow : Dates.Time(item.Start);
        if (on == today)
        {
            return time;
        }
        var name = on == today.AddDays(1) ? S.WidgetCalendarTomorrow : Dates.Format(on.ToDateTime(TimeOnly.MinValue), "ddd");
        return item.AllDay ? name : $"{name} {time}";
    }

    /// <summary>
    /// The events from <paramref name="top"/>, each the agenda's row (<see cref="AgendaEvents"/>: one compact row of one
    /// line, §413, §414, §418, an item that lights and opens, §434), batched by day: every day, today's too, a heading over
    /// its events, which say no day of their own; the first day, today or the picked one, stands even with nothing on it,
    /// its heading over "No events". Every event is laid out; those under the line's foot are not drawn, and scroll into
    /// it (ListScroll.cs).
    /// </summary>
    private void Items(LineSketch sketch, D2D_RECT_F box, in TileStyle style, List<(DateOnly Day, CalendarEvent Event)> events, DateTime now, float top)
    {
        var today = DateOnly.FromDateTime(now);
        var y = top;
        if (FirstEmpty(events, now))
        {
            y = agenda.Status(sketch, box, style, agenda.Heading(sketch, box, style, First(now), today, y), S.DockCalendarNoEvents);
        }
        for (var index = 0; index < events.Count; index++)
        {
            var (day, item) = events[index];
            // Every day starts with its heading, today's too.
            if (index == 0 || events[index - 1].Day != day)
            {
                y = agenda.Heading(sketch, box, style, day, today, y);
            }
            y = agenda.Event(sketch, box, style, item, day, now, store, y);
        }
    }
}
