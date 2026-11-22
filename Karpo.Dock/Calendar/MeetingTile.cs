// The next meetings as a line of a list (docs\widgets.md §205): today's meetings still to come from the
// accounts' calendars, the one on now first (Meetings.Ahead), each the kit's item (§401) — its calendar's
// colour bar in the leading column, its title where every name begins, and under it how soon it starts, its time
// and its place ("in 4 min · 10:00–10:30 · Room 3"; the service where it has no place but its link) —
// with a Join button at the trailing gutter where it is online: Windows 11's standard button with the
// camera and "Join", the accent's from the lead before its start until it ends, the camera alone where
// the line is too narrow for the word.
//
// One row (40 DIP) is the next meeting on one line: its title cut before how soon, and Join. From the
// height of an event's row (52 DIP) the rows are today's meetings, as many as the line holds, from its
// top; the rest is under the folder's own end. With none left today it says so, and under it the next
// meeting of the coming week where there is one, its day said ("Tomorrow, 09:00").
//
// All-day events are no meetings, nor those shown as free or declined (CalendarEvent.Free). Every meeting is
// an item (§434): its row lit edge to edge under the pointer (LineItem, §428), and a click on it opens the
// meeting in a calendar once the folder has gone (EventLinks); a click on its Join, a button on the item, joins
// it (CellAct.Action, IslandView.App, MeetingJoin). Enter joins the lit meeting and a right click on a row has
// "Join meeting", as every list of meetings has them (JoinButton.cs, §206). It never ticks: the folder's minute
// and the calendars' news redraw it.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Panels;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class MeetingTile : CalendarLineTile, IListContent, ILinePieces, IMeetingLine
{
    /// <summary>What a row of the line is.</summary>
    internal enum RowKind
    {
        Meeting,

        /// <summary>"No more meetings today", or the calendars still being read.</summary>
        Message,

        /// <summary>A small line: why there is no calendar, or what went wrong.</summary>
        Note,
    }

    /// <summary>One row, in the line's px: its meeting and words, its Join button where it has one, and what a click on it opens (<see cref="EventLinks.Target"/>; null for nothing).</summary>
    internal readonly record struct MeetingRow(D2D_RECT_F Box, RowKind Kind, Meeting? Meeting, string Title, string When, string Detail, bool Lit, D2D_RECT_F Join, bool Worded, string? Opens = null);

    /// <summary>The line laid out: one row on one line (<paramref name="Single"/>), or rows of two lines.</summary>
    internal readonly record struct MeetingPlan(IReadOnlyList<MeetingRow> Rows, bool Single);

    private readonly Func<DateTime>? now;
    private RowPress press;
    private int hot;
    private readonly EventChoice joins = new();

    // The row the pointer is on, from 1: whose meeting a right click's "Join meeting" joins.
    private int pointed;

    // The rows as last laid out, which a click's target is an index into.
    private IReadOnlyList<MeetingRow> laid = [];

    public MeetingTile(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, null)
    {
    }

    /// <param name="now">The wall-clock time, this PC's (<see cref="Clock.Now"/>) when null.</param>
    internal MeetingTile(string key, WidgetItem island, Gfx gfx, Config config, Func<DateTime>? now)
        : base(key, island, gfx)
    {
        this.now = now;
        Configure(island, config);
    }

    /// <summary>From how long before its start a meeting is lit: the face's default, ten minutes.</summary>
    internal static readonly TimeSpan Lead = TimeSpan.FromMinutes(10);

    private DateTime Now => now?.Invoke() ?? Clock.Now();

    /// <summary>
    /// A zero-width space after a text measured: a line's width leaves out the spaces at its end, and
    /// the space after the dot is room the next words stand after.
    /// </summary>
    internal const string Spaced = "\u200B";

    /// <summary>Its meetings and their Join buttons light under the pointer themselves: the line as a whole is not one thing to press.</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>
    /// Row <paramref name="index"/> (from 1): its meeting opens in a calendar once the folder has gone (§434); its Join
    /// (<see cref="JoinButton.Target"/> plus its place) joins it. With no part, after Enter or Space (<see cref="CardKey"/>),
    /// the lit meeting's Join.
    /// </summary>
    public override IslandAction Click(int index)
    {
        _ = press.Up();
        if (index == 0 && joins.Keyed())
        {
            return joins.Choose(LitLink());
        }
        if (index >= JoinButton.Target)
        {
            var at = index - JoinButton.Target;
            return joins.Choose(at < laid.Count && TilePaint.Width(laid[at].Join) > 0 ? laid[at].Meeting?.Link : null);
        }
        return joins.Opens(index >= 1 && index <= laid.Count ? laid[index - 1].Opens : null);
    }

    /// <summary>The dock, once the folder has gone: opens the meeting the click took, or joins it, once.</summary>
    public void Open() => joins.Open();

    /// <summary>The first lit meeting's link where its row has a Join: what Enter joins.</summary>
    private Karpo.Accounts.MeetingLink? LitLink() =>
        laid.FirstOrDefault(row => row.Lit && TilePaint.Width(row.Join) > 0).Meeting?.Link;

    /// <summary>Enter and Space join the lit meeting, as an accent button is a dialog's default (§206).</summary>
    protected internal override bool CardKey(int key, WidgetSurface surface) => joins.Key(key, LitLink());

    /// <summary>The meeting of the row under the pointer, where it has a Join.</summary>
    public (string Title, Karpo.Accounts.MeetingLink Link)? MenuMeeting() =>
        pointed >= 1 && pointed <= laid.Count && laid[pointed - 1] is { Meeting: { } meeting } row && TilePaint.Width(row.Join) > 0 && meeting.Link is { } link
            ? (meeting.Event.Title, link)
            : null;

    /// <summary>
    /// The meetings the line lists at <paramref name="at"/>: today's still to come, the one on now first;
    /// with none, the next of the coming week. Empty with none at all.
    /// </summary>
    internal List<Meeting> Rows(DateTime at)
    {
        var zone = TimeZoneInfo.Local;
        var ahead = Meetings.Ahead(Store, at, Meetings.DaysAhead, zone, SourceFilter.Of(Settings));
        var today = Meetings.Today(ahead, at, zone);
        return today.Count > 0 ? today : [.. ahead.Take(1)];
    }

    /// <summary>The height of a row of two lines: an event's 52 DIP, or what its two lines and a little air need at a large text.</summary>
    private float RowTall(in TileStyle style, in ListForm form) =>
        MathF.Max(PopupPaint.Round(52 * style.Scale * style.TextScale), form.Line + form.SmallLine + style.Px(12));

    /// <summary>The line in <paramref name="box"/>: one row on one line where it is shorter than a row of two, else rows of two from its top.</summary>
    internal MeetingPlan Lay(D2D_RECT_F box, in TileStyle style)
    {
        Look();
        var form = ListForm.Of(Gfx, style);
        var at = Now;
        var zone = TimeZoneInfo.Local;
        var nowUtc = Stamp.WallToUtc(at, zone);
        var height = box.bottom - box.top;
        var tall = RowTall(style, form);
        var single = height + 0.5f < tall;
        var rows = new List<MeetingRow>();
        var y = box.top;
        void Message(float rowHeight, string text, RowKind kind)
        {
            rows.Add(new MeetingRow(PopupPaint.Rect(box.left, y, box.right - box.left, rowHeight), kind, null, text, "", "", false, default, false));
            y += rowHeight;
        }
        var row = single ? MathF.Max(1, height) : tall;
        if (!Store.HasCalendars)
        {
            // Why there is no calendar is the message's caption, the kit's row with its caption (§401).
            var noted = !single && !Store.Loading;
            Message(single ? row : LineRow.Height(style, caption: noted), Store.Loading ? S.DockCalendarReadingCalendars : S.DockMeetingNone, RowKind.Message);
            if (noted)
            {
                Message(0, Store.Problem is { Length: > 0 } problem ? problem : S.DockCalendarNoCalendars, RowKind.Note);
            }
            return Laid(new MeetingPlan(rows, single));
        }
        var meetings = Rows(at);
        var today = meetings.Count > 0 && Meetings.Today(meetings, at, zone).Count > 0;
        if (!today)
        {
            Message(single ? row : LineRows.Px(1, style), S.DockMeetingNone, RowKind.Message);
            if (single)
            {
                return Laid(new MeetingPlan(rows, single));
            }
        }
        foreach (var meeting in meetings)
        {
            if (rows.Count > 0 && y + row > box.bottom + 0.5f)
            {
                break;
            }
            var lit = Meetings.Lit(meeting, nowUtc, Lead);
            var rowBox = PopupPaint.Rect(box.left, y, box.right - box.left, row);
            var (join, worded) = meeting.Link is not null ? JoinOf(rowBox, style, form) : (default, false);
            rows.Add(new MeetingRow(rowBox, RowKind.Meeting, meeting, meeting.Event.Title, single ? MeetingText.When(meeting, at, zone) : MeetingText.Soon(meeting, at, zone), MeetingText.Detail(meeting, at), lit, join, worded,
                EventLinks.Target(meeting.Event, DateOnly.FromDateTime(meeting.Event.Start), Store)));
            y += row;
            if (single)
            {
                break;
            }
        }
        return Laid(new MeetingPlan(rows, single));
    }

    /// <summary><paramref name="plan"/>, kept as what a click is aimed at.</summary>
    private MeetingPlan Laid(MeetingPlan plan)
    {
        laid = plan.Rows;
        return plan;
    }

    /// <summary>
    /// A row's Join: the lines' one button (LineButton), against the trailing gutter in the middle of
    /// the row, "Join" beside the camera where the line keeps two thirds of itself for the words, the
    /// camera alone in a square where it does not.
    /// </summary>
    private (D2D_RECT_F Box, bool Worded) JoinOf(D2D_RECT_F row, in TileStyle style, in ListForm form)
    {
        var buttons = LineButton.Of(Gfx, style);
        var tall = MathF.Min(buttons.Height, (row.bottom - row.top) - style.Px(4));
        var wide = JoinButton.WordedWidth(Gfx, buttons);
        var width = row.right - row.left;
        var worded = wide <= width / 3;
        var across = worded ? wide : tall;
        var right = row.right - form.Inset;
        return (PopupPaint.Rect(right - across, PopupPaint.Round(((row.top + row.bottom) / 2) - (tall / 2)), across, tall), worded);
    }


    /// <summary>The meetings of today, a row each, or the message and the next meeting; one row at the least.</summary>
    public float ListHeight(float width, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var tall = RowTall(style, form);
        var plan = Lay(PopupPaint.Rect(0, 0, width, 1000 * tall), style);
        return MathF.Ceiling(plan.Rows.Count == 0 ? tall : plan.Rows[^1].Box.bottom);
    }

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Lay(box, style);
        var theme = style.Theme;
        var sketch = new LineSketch(dc, Gfx);
        dc->PushAxisAlignedClip(&box, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            for (var index = 0; index < plan.Rows.Count; index++)
            {
                var row = plan.Rows[index];
                var at = row.Box;
                if (at.bottom > box.bottom + 0.5f && index > 0)
                {
                    break;
                }
                if (row.Kind == RowKind.Note)
                {
                    continue;
                }
                if (row.Kind == RowKind.Message)
                {
                    // Nothing to join: the kit's row in the quiet inks, why under it where the line says (§401).
                    var note = index + 1 < plan.Rows.Count && plan.Rows[index + 1].Kind == RowKind.Note ? plan.Rows[index + 1].Title : null;
                    LineRow.Draw(sketch, at.left, at.right, at.top, style, new RowSpec(row.Title)
                    {
                        Glyph = Info.Glyph,
                        GlyphInk = theme.TextTertiary,
                        NameInk = theme.TextSecondary,
                        Caption = note,
                    }, at.bottom - at.top);
                    continue;
                }
                // A meeting: the kit's item (§401) — its calendar's colour in the leading column, its title, how soon,
                // its time and its place under it, and its Join against the trailing gutter.
                var meeting = row.Meeting!.Value;
                var joined = TilePaint.Width(row.Join) > 0;
                // Its row, edge to edge (LineItem): lit whole under the pointer, on its Join too.
                if (row.Opens is not null && (hot == index + 1 || hot == JoinButton.Target + index))
                {
                    LineItem.Light(sketch, row.Box, theme, press.On(index + 1));
                }
                LineRow.Draw(sketch, at.left, at.right, at.top, style, new RowSpec(row.Title)
                {
                    Marker = CalendarPainter.Solid(meeting.Event.Colour, theme),
                    Caption = plan.Single ? null : LineRow.Join(row.When, row.Detail),
                    Reading = plan.Single ? row.When : null,
                    Look = RowReading.Body,
                    ReadingInk = theme.TextSecondary,
                    Controls = joined ? [RowControl.Room("join", TilePaint.Width(row.Join), TilePaint.Height(row.Join))] : [],
                }, at.bottom - at.top);
                if (joined)
                {
                    var target = JoinButton.Target + index;
                    JoinButton.Paint(dc, Gfx, LineButton.Of(Gfx, style), theme, row.Join, row.Worded, row.Lit, hot == target, press.On(target));
                }
            }
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>
    /// A row's Join at <paramref name="point"/>, as <see cref="JoinButton.Target"/> plus its place — a button's reach, kept in
    /// its row; else a meeting's row, from 1; 0 beside them.
    /// </summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Lay(box, style);
        var buttons = LineButton.Of(Gfx, style);
        for (var index = 0; index < plan.Rows.Count; index++)
        {
            var row = plan.Rows[index];
            if (TilePaint.Width(row.Join) > 0 && LineRows.Inside(JoinButton.Hit(buttons, row.Join, row.Box.top, row.Box.bottom), point))
            {
                return JoinButton.Target + index;
            }
        }
        for (var index = 0; index < plan.Rows.Count; index++)
        {
            if (plan.Rows[index].Opens is not null && LineRows.Inside(plan.Rows[index].Box, point))
            {
                return index + 1;
            }
        }
        return 0;
    }

    /// <summary>A Join says the app it joins in ("Join in Microsoft Teams"): the camera alone says nothing of it.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        CardTarget(point, box, style, surface) is >= JoinButton.Target and var target && laid[target - JoinButton.Target].Meeting?.Link is { } link
            ? (JoinButton.Tip(link), laid[target - JoinButton.Target].Join)
            : null;

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var target = point is { } at ? CardTarget(at, box, style, surface) : 0;
        pointed = 0;
        for (var index = 0; point is { } over && index < laid.Count; index++)
        {
            if (laid[index].Kind == RowKind.Meeting && LineRows.Inside(laid[index].Box, over))
            {
                pointed = index + 1;
            }
        }
        var let = press.Over(target);
        if (target == hot && !let)
        {
            return;
        }
        hot = target;
        Redraw(Gfx);
    }

    /// <summary>A press on a meeting or its Join lights it pressed; the press stays the folder's, which makes it the click.</summary>
    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (press.Down(CardTarget(point, box, style, surface)))
        {
            Redraw(Gfx);
        }
    }

    /// <summary>Closed, nothing is under the pointer or pressed.</summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            hot = pointed = 0;
            _ = press.Up();
            joins.Forget();
        }
    }

    // --- As pieces ----------------------------------------------------------------------------------

    public string MainPiece => "meeting 0";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var plan = Lay(box, style);
        var pieces = new List<LinePiece>();
        for (var index = 0; index < plan.Rows.Count; index++)
        {
            var row = plan.Rows[index];
            pieces.Add(new LinePiece(row.Kind == RowKind.Meeting ? $"meeting {index}" : $"note {index}", row.Box, row.Kind == RowKind.Meeting ? $"{row.Title} · {row.When}" : row.Title));
            if (TilePaint.Width(row.Join) > 0)
            {
                pieces.Add(new LinePiece($"join {index}", row.Join, JoinButton.Name(row.Title)));
            }
        }
        return pieces;
    }
}
