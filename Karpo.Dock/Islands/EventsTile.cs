// A day's events as a line of a list (docs\widgets.md §80, §447): the day's name ("Today", "Tomorrow", "Yesterday",
// else its date) over the day's events. The day is the folder's (Channels.Day) where something in it picks one — the
// month, a view, the date's header — else today; "Follow the folder's day" switched off, it is always today.
//
// The calendar preset stands it under the month, as the flyout had it. It is not the calendar's own line
// (CalendarIsland), the agenda of the days coming: this is one day, as tall as its events — the day's name in a row of
// its own (40 DIP), and under it the day's events drawn as the
// agenda draws them (AgendaEvents; the person, 2026-10-31: "for the days style, events should look similar as in agenda
// view"): one compact row each, its calendar's colour in the leading column, its title, whose holiday it is, when it is
// against the trailing gutter, a meeting's Join at its end; each lit edge to edge under the pointer and opening the event
// in a calendar once the folder has gone (§434). Held shorter, the events scroll under the day's name (ListScroll.cs).
// The day's name and the notes do nothing on a click.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class EventsTile : CalendarLineTile, IListContent, IListScroll, ILinePieces, IMeetingLine, ILineTargets
{
    // The events as last laid out, and what the pointer and the keys do with them.
    private readonly AgendaEvents events;

    public EventsTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        events = new AgendaEvents(Hidden);
        Configure(island, config);
    }

    /// <summary>
    /// A list of what is on: a click on an event's row (<paramref name="index"/>, from 1) opens the event in a calendar
    /// once the folder has gone (§434), on its Join (<see cref="JoinButton.Target"/> plus its place) joins the meeting;
    /// with no part, after Enter or Space, the lit one's.
    /// </summary>
    public override IslandAction Click(int index) => events.Click(index);

    /// <summary>The dock, once the folder has gone: opens the meeting a Join chose, or the event a row did, once.</summary>
    public void Open() => events.Open();

    /// <summary>Enter and Space join the lit meeting, as an accent button is a dialog's default.</summary>
    protected internal override bool CardKey(int key, WidgetSurface surface) => events.Key(key);

    /// <summary>The meeting of the row under the pointer, where it has a Join.</summary>
    public (string Title, Karpo.Accounts.MeetingLink Link)? MenuMeeting() => events.MenuMeeting();

    /// <summary>Closed, nothing is under the pointer, pressed or chosen.</summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            events.Forget();
        }
    }

    /// <summary>Its events and their Join buttons light themselves under the pointer: the list as a whole is not one thing to press.</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>The day it lists: the folder's, where it follows it, else today.</summary>
    internal DateOnly Day => Followed(Channels.Day) ?? Today;

    /// <summary>The events as last laid out: what a click's target is an index into.</summary>
    internal IReadOnlyList<AgendaEvents.Row> Rows => events.Rows;

    /// <summary>The day's name's row, and the one row's: the kit's row with no caption under it (40 DIP).</summary>
    private static float HeadingHeight(in TileStyle style) => LineRow.Height(style, caption: false);

    /// <summary>
    /// What the day's events are, in rows under the day's name: every event of the day, all-day ones first, or "No events"
    /// (the calendars still being read, "Loading…"); where there is no calendar, "No events" over why; and what went wrong
    /// reading one under the rest. Null for an event's row, else the words of a status row.
    /// </summary>
    private List<(CalendarEvent Event, string? Words)> Listed()
    {
        var listed = new List<(CalendarEvent Event, string? Words)>();
        if (!Store.HasCalendars)
        {
            // The day and its heading all the same: a calendar with nothing in it is still a calendar.
            listed.Add((default, S.DockCalendarNoEvents));
            listed.Add((default, Store.Loading ? S.DockCalendarReadingCalendars : Store.Problem is { Length: > 0 } problem ? problem : S.DockCalendarNoCalendars));
            return listed;
        }
        var day = Day;
        var start = day.ToDateTime(TimeOnly.MinValue);
        var found = CalendarStore.OnDay(Store.EventsIn(start, start.AddDays(1), shown: SourceFilter.Of(Settings)), day);
        if (found.Count == 0)
        {
            listed.Add((default, Store.Loading ? S.DockCalendarLoading : S.DockCalendarNoEvents));
        }
        listed.AddRange(found.Select(item => (item, (string?)null)));
        if (Store.Problem is { Length: > 0 } trouble)
        {
            listed.Add((default, trouble));
        }
        return listed;
    }

    /// <summary>As tall as the day's name and a compact row for every event and every note under it.</summary>
    public float ListHeight(float width, in TileStyle style)
    {
        Look();
        return HeadingHeight(style) + (AgendaEvents.Compact(style) * Listed().Count);
    }

    /// <summary>The whole day, where the line holds its name and a row: the rows scroll under the name (ListScroll.cs).</summary>
    public float ScrollHeight(float width, float height, in TileStyle style) =>
        height + 0.5f < HeadingHeight(style) + AgendaEvents.Compact(style) ? 0 : ListHeight(width, style);

    /// <summary>The day's name stays while its events scroll.</summary>
    public float ScrollHead(float width, float content, in TileStyle style) => HeadingHeight(style);

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        Line(new LineSketch(dc, Gfx), box, style);

    /// <summary>
    /// The line: the day's name strong on the gutter, where the colour bars' column begins, kept clear of the stale mark
    /// at the trailing gutter (StaleMark.cs); under it the day's
    /// events as the agenda's rows, or the status rows that stand for them. Every event is laid out; those under the
    /// line's foot are not drawn, and scroll into it.
    /// </summary>
    private void Line(LineSketch sketch, D2D_RECT_F box, in TileStyle style)
    {
        Look();
        events.Clear();
        var form = ListForm.Of(Gfx, style);
        var day = Day;
        var now = Clock.Now();
        var heading = HeadingHeight(style);
        LineRow.Draw(sketch, box.left + form.Inset - form.TextLeft, box.right, box.top, style, new RowSpec(CalendarText.DayName(day, Today))
        {
            Strong = true,
            Trailing = StaleRoom(style),
            NamePiece = "day",
        }, MathF.Min(heading, TilePaint.Height(box)));
        if (Stale)
        {
            StaleMark.Sketch(sketch, box, style, form, box.top, heading);
        }
        var y = box.top + heading;
        foreach (var (item, words) in Listed())
        {
            y = words is null ? events.Event(sketch, box, style, item, day, now, Store, y) : events.Status(sketch, box, style, y, words);
        }
    }

    /// <summary>
    /// A meeting's Join at <paramref name="point"/> — across its row's height and a button's reach (LineButton.Hit) — as
    /// <see cref="JoinButton.Target"/> plus its row's place; else the row of an event that opens somewhere, edge to edge,
    /// from 1; 0 anywhere else, which does nothing.
    /// </summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        // Laid out for this box, so the rows are where they are drawn.
        _ = LinePieces(box, style);
        return events.Target(point);
    }

    /// <summary>keys: its Joins and its events, laid out for this box (ILineTargets).</summary>
    public IReadOnlyList<(int Target, D2D_RECT_F Box)> LineTargets(D2D_RECT_F box, in TileStyle style)
    {
        _ = LinePieces(box, style);
        return events.Targets();
    }

    /// <summary>The event or the Join under the pointer lights; the event under it is the one a right click is about.</summary>
    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (events.Hover(point, point is { } at ? CardTarget(at, box, style, surface) : 0))
        {
            Redraw(Gfx);
        }
    }

    /// <summary>A press on an event or a Join lights it pressed; the press stays the folder's, which makes it the click.</summary>
    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (events.Down(CardTarget(point, box, style, surface)))
        {
            Redraw(Gfx);
        }
    }

    /// <summary>The stale mark's tooltip, over the mark; a Join's, the app it joins in.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        events.Tip(CardTarget(point, box, style, surface)) ?? StaleMark.Tip(LinePieces(box, style), point, StaleTip, style);

    // --- As pieces ----------------------------------------------------------------------------------

    public string MainPiece => "day";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(null, Gfx);
        Line(sketch, box, style);
        return sketch.Pieces;
    }
}
