// The agenda's events, one way wherever a line lists them (docs\widgets.md §413, §414, §418, §434, §447): the agenda
// (CalendarIsland) and the day's events (EventsTile) draw them, and take the pointer over them, with this one class.
//
// Each event is the kit's item of one compact row (32 DIP): its calendar's colour bar in the leading column, its title at
// the text edge and after it, on its line, whose it is for a public holiday of several calendars (§265), and when it is
// on that line against the trailing gutter — its part of the day, "09:00–10:00", "From 09:00", "Until 17:00" or "All day"
// (CalendarText.Span) — before a meeting's Join (§206). A day's heading is a compact row of its own, its name strong in
// the secondary ink on the gutter; "No events" the kit's Status row in one.
//
// Every event is an item (§434): its row edge to edge is its to the pointer, lit whole under it (LineItem, §428), and a
// click on it opens it in a calendar once the folder has gone (EventLinks); a meeting's Join is a button on it, its own
// across the row's height and a button's reach (§418): a click there joins it, Enter the lit one, and a right click on
// its row has "Join meeting".

using System.Globalization;
using System.Numerics;
using Karpo.Accounts;
using Karpo.Core;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>
/// A line's events as the agenda's rows, and what the pointer and the keys do with them; <paramref name="hidden"/> the
/// line's own test of a row scrolled out of sight (CellIsland.Hidden), which is not drawn.
/// </summary>
internal sealed unsafe class AgendaEvents(Func<float, float, bool> hidden)
{
    /// <summary>
    /// An event's row as last laid out, in the line's px, its event, its Join's link and where a press lands on the Join
    /// (none where the row has no button), whether the Join is lit, and what a click on the row opens (EventLinks.Target,
    /// null for nothing).
    /// </summary>
    internal readonly record struct Row(D2D_RECT_F Box, CalendarEvent Event, MeetingLink? Link, D2D_RECT_F Join, bool Lit, string? Opens);

    private readonly EventChoice joins = new();
    private readonly List<Row> rows = [];
    private RowPress press;

    // What is under the pointer: an event's row, from 1 (its place), or its Join (JoinButton.Target plus its place); the
    // row the pointer is on, from 1.
    private int hot;
    private int pointed;

    /// <summary>The rows as last laid out: what a click's target is an index into.</summary>
    public IReadOnlyList<Row> Rows => rows;

    /// <summary>A new layout: its rows are added as they are laid out.</summary>
    public void Clear() => rows.Clear();

    /// <summary>A row laid out by its line (the agenda's one-row form's next event).</summary>
    public void Add(in Row row) => rows.Add(row);

    /// <summary>Whether row <paramref name="index"/> (from 0) is under the pointer, on its Join too.</summary>
    public bool Lit(int index) => hot == index + 1 || hot == JoinButton.Target + index;

    /// <summary>Whether row <paramref name="index"/> (from 0) is pressed.</summary>
    public bool Pressed(int index) => press.On(index + 1);

    // --- The pointer and the keys --------------------------------------------------------------------

    /// <summary>
    /// A click on event <paramref name="index"/>'s row (from 1) opens the event in a calendar once the folder has gone
    /// (§434), on its Join (<see cref="JoinButton.Target"/> plus its place) joins the meeting; with no part, after
    /// Enter or Space, the lit one's.
    /// </summary>
    public IslandAction Click(int index)
    {
        _ = press.Up();
        if (index == 0 && joins.Keyed())
        {
            return joins.Choose(LitLink());
        }
        if (index >= JoinButton.Target)
        {
            var at = index - JoinButton.Target;
            return joins.Choose(at < rows.Count && TilePaint.Width(rows[at].Join) > 0 ? rows[at].Link : null);
        }
        return joins.Opens(index >= 1 && index <= rows.Count ? rows[index - 1].Opens : null);
    }

    /// <summary>The dock, once the folder has gone: opens the meeting a Join chose, or the event a row did, once.</summary>
    public void Open() => joins.Open();

    /// <summary>The first lit meeting listed: what Enter joins.</summary>
    private MeetingLink? LitLink() => rows.FirstOrDefault(row => row.Lit && row.Link is not null).Link;

    /// <summary>Enter and Space join the lit meeting, as an accent button is a dialog's default.</summary>
    public bool Key(int key) => joins.Key(key, LitLink());

    /// <summary>The meeting of the row under the pointer, where it has a Join.</summary>
    public (string Title, MeetingLink Link)? MenuMeeting() =>
        pointed >= 1 && pointed <= rows.Count && rows[pointed - 1] is { Link: { } link } row ? (row.Event.Title, link) : null;

    /// <summary>The line closed: nothing is under the pointer, pressed or chosen.</summary>
    public void Forget()
    {
        hot = pointed = 0;
        _ = press.Up();
        joins.Forget();
    }

    /// <summary>
    /// A meeting's Join at <paramref name="point"/> (<see cref="JoinButton.Target"/> plus its row's place), else the row of
    /// an event that opens somewhere, its place from 1; 0 anywhere else, which opens nothing. On the rows as last laid out.
    /// </summary>
    public int Target(Vector2 point) => LineFocus.At(Targets(), point);

    /// <summary>keys: every target as last laid out, the Joins first, as <see cref="Target"/> reads them (ILineTargets).</summary>
    public IReadOnlyList<(int Target, D2D_RECT_F Box)> Targets()
    {
        var targets = new List<(int Target, D2D_RECT_F Box)>();
        for (var index = 0; index < rows.Count; index++)
        {
            if (TilePaint.Width(rows[index].Join) > 0)
            {
                targets.Add((JoinButton.Target + index, rows[index].Join));
            }
        }
        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].Opens is not null)
            {
                targets.Add((index + 1, rows[index].Box));
            }
        }
        return targets;
    }

    /// <summary>The pointer at <paramref name="point"/>, over <paramref name="target"/>: true where what is lit changed.</summary>
    public bool Hover(Vector2? point, int target)
    {
        pointed = 0;
        for (var index = 0; point is { } over && index < rows.Count; index++)
        {
            if (LineRows.Inside(rows[index].Box, over))
            {
                pointed = index + 1;
            }
        }
        var let = press.Over(target);
        if (target == hot && !let)
        {
            return false;
        }
        hot = target;
        return true;
    }

    /// <summary>A press on <paramref name="target"/>: true where it lights something pressed.</summary>
    public bool Down(int target) => press.Down(target);

    /// <summary>A Join's tooltip, the app it joins in, over it; null over anything else.</summary>
    public (string Text, D2D_RECT_F Part)? Tip(int target) =>
        target >= JoinButton.Target && target - JoinButton.Target < rows.Count && rows[target - JoinButton.Target].Link is { } link
            ? (JoinButton.Tip(link), rows[target - JoinButton.Target].Join)
            : null;

    // --- The rows --------------------------------------------------------------------------------------

    /// <summary>An event's height and a day's heading's: the kit's compact row (32 DIP, §418).</summary>
    public static float Compact(in TileStyle style) => LineRow.Compact(style);

    /// <summary>Whether <paramref name="item"/> fills <paramref name="day"/>: an all-day event, or a day in the middle of a longer one.</summary>
    public static bool FillsDay(CalendarEvent item, DateOnly day) => CalendarText.Span(item, day) == S.DockCalendarAllDay;

    /// <summary>Whether a row from <paramref name="top"/> to <paramref name="bottom"/> is drawn: inside the line and not scrolled out of sight.</summary>
    private bool Shows(LineSketch sketch, D2D_RECT_F box, float top, float bottom) =>
        bottom <= box.bottom + 0.5f && !(sketch.Draws && hidden(top, bottom));

    /// <summary>
    /// A day's heading at <paramref name="top"/>, today's as any other's (§419): a compact row, the day's name
    /// (<see cref="CalendarText.DayName"/>: "Today", "Tomorrow", the long date) strong in the secondary ink on the gutter,
    /// where the colour bars' column begins, a gap lower than the row's middle so it stands with its own events rather
    /// than half-way between them and what is over it. Where the row is under the line's foot it is not drawn. Its foot.
    /// </summary>
    public float Heading(LineSketch sketch, D2D_RECT_F box, in TileStyle style, DateOnly day, DateOnly today, float top)
    {
        var form = ListForm.Of(sketch.Gfx, style);
        var row = Compact(style);
        var gap = style.Px(LineSpace.GapDip);
        if (Shows(sketch, box, top, top + row))
        {
            LineRow.Draw(sketch, box.left + form.Inset - form.TextLeft, box.right, top + gap, style, new RowSpec(CalendarText.DayName(day, today))
            {
                Strong = true,
                NameInk = style.Theme.TextSecondary,
                NamePiece = $"day {day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
            }, row - gap);
        }
        return top + row;
    }

    /// <summary>
    /// <paramref name="words"/> ("No events", that the calendars are being read, what went wrong) in a compact row from
    /// <paramref name="top"/>: the kit's Status row (§401) at the text edge in the secondary ink — no item, nothing to press
    /// or light. Not drawn under the line's foot. Its foot.
    /// </summary>
    public float Status(LineSketch sketch, D2D_RECT_F box, in TileStyle style, float top, string words)
    {
        var row = Compact(style);
        if (Shows(sketch, box, top, top + row))
        {
            LineStatus.Draw(sketch, PopupPaint.Rect(box.left, top, TilePaint.Width(box), row), style, null, words, []);
        }
        return top + row;
    }

    /// <summary>
    /// <paramref name="item"/>, listed under <paramref name="day"/>, as the next row from <paramref name="top"/>: see the
    /// top of the file. Laid out whether drawn or not, so the rows keep their events' places; those under the line's foot
    /// are not drawn and scroll into it (ListScroll.cs). Its foot.
    /// </summary>
    public float Event(LineSketch sketch, D2D_RECT_F box, in TileStyle style, CalendarEvent item, DateOnly day, DateTime now, CalendarStore store, float top)
    {
        var gfx = sketch.Gfx;
        var theme = style.Theme;
        var form = ListForm.Of(gfx, style);
        var each = Compact(style);
        var buttons = LineButton.Of(gfx, style);
        var index = rows.Count;
        var rowBox = PopupPaint.Rect(box.left, top, TilePaint.Width(box), each);
        var link = JoinButton.Of(item, now);
        var lit = link is not null && JoinButton.Lit(item, now);
        var opens = EventLinks.Target(item, day, store);
        if (!Shows(sketch, box, top, top + each))
        {
            rows.Add(new Row(rowBox, item, link, default, lit, opens));
            return top + each;
        }
        // When it is wins: the title and whose are cut before it is (LineRow). Where that leaves the title too little
        // (225 % text in a narrow folder) the Join is the camera alone, and where still too little the time its start.
        var when = CalendarText.Span(item, day);
        var wide = JoinButton.WordedWidth(gfx, buttons);
        var worded = link is not null && wide <= TilePaint.Width(box) / 3 && TitleRoom(gfx, box, style, when, wide) >= wide;
        var button = link is null ? 0 : worded ? wide : buttons.Height;
        if (!TilePaint.Readable(gfx, item.Title, form.TextPx, Weight.Regular, TitleRoom(gfx, box, style, when, button)) && Starts(item, day) is { } start)
        {
            when = start;
        }
        // The event's row, edge to edge (LineItem): lit whole under the pointer, on its Join too.
        if (opens is not null && Lit(index))
        {
            LineItem.Light(sketch, rowBox, theme, Pressed(index));
        }
        var laid = LineRow.Draw(sketch, box.left, box.right, top, style, new RowSpec(item.Title)
        {
            Marker = PopupPaint.Ink(item.ColourOr(theme.Accent)),
            Caption = item.Whose,
            CaptionBeside = true,
            Reading = when,
            Look = RowReading.Meta,
            Controls = link is null ? [] : [RowControl.Room($"room {index}", button, buttons.Height)],
            GlyphPiece = $"colour {index}",
            NamePiece = $"event {index}",
            CaptionPiece = $"when {index}",
            ReadingPiece = $"time {index}",
        }, each);
        var join = link is null ? default : laid.Controls[0];
        if (link is not null)
        {
            if (sketch.Draws)
            {
                JoinButton.Paint(sketch.Device, gfx, buttons, theme, join, worded, lit, hot == JoinButton.Target + index, press.On(JoinButton.Target + index));
            }
            sketch.Placed($"join {index}", join, JoinButton.Name(item.Title));
        }
        // The Join takes a press across its row's height and a button's reach (LineButton.Hit, §418); the rest is the event's.
        rows.Add(new Row(rowBox, item, link, link is null ? default : JoinButton.Hit(buttons, join, rowBox.top, rowBox.bottom), lit, opens));
        return top + each;
    }

    /// <summary>An event's start where it starts on <paramref name="day"/> with a time: what its row says where its whole span leaves its title no room.</summary>
    private static string? Starts(CalendarEvent item, DateOnly day) =>
        !item.AllDay && DateOnly.FromDateTime(item.Start) == day ? Dates.Time(item.Start) : null;

    /// <summary>
    /// What an event's row leaves its title, px: the line less the text edge, <paramref name="when"/> in the kit's Meta
    /// against the trailing gutter, and a Join <paramref name="button"/> px wide (0: none), a gap between each.
    /// </summary>
    private static float TitleRoom(Gfx gfx, D2D_RECT_F box, in TileStyle style, string when, float button)
    {
        var form = ListForm.Of(gfx, style);
        var gap = style.Px(LineSpace.GapDip);
        var (px, weight) = LineRow.Type(style, RowReading.Meta);
        return TilePaint.Width(box) - form.TextLeft - form.Inset - PopupPaint.Measure(gfx, when, px, weight).X - gap - (button > 0 ? button + gap : 0);
    }
}
