// The next meeting on the bar (docs\widgets.md §205): the meeting face of a folder (FolderFaces.Meeting).
//
// Across a horizontal bar, one row: its calendar's colour as a small mark, the title cut to the room
// and how soon it starts — "Standup · in 4 min", "now", "started 3 min ago" — and at the end a compact
// Join button, the camera on a 4-DIP-round pill as Windows 11's compact buttons are, where the meeting
// has a link the face knows and the face keeps room for it. From the face's lead before its start
// until it ends the meeting is lit: how soon in the accent and semibold, Join filled with the accent.
// With nothing coming, a calm "No more meetings today" in the quieter ink — or, set to show a meeting
// only shortly before it, nothing at rest: the face has nothing to show (NothingToShow, §327), and
// arranged it says so greyed, as every face with nothing to show is.
// Down a bar on its side the camera alone, in the accent while a meeting is lit.
//
// As wide as what it shows now (§244): the mark, its words fitted into the room of the width it is
// set to — when alone, a short title, a long one — and cut past it (MeetingText.Fit), and the Join
// button where its meeting has a link; the bar is laid out again as meetings come and go. It reads the calendars the
// folder follows (FolderIsland.Meetings) and is painted again in place each minute and whenever they
// change. A click on Join opens the meeting (FolderIsland.ClickAt, MeetingJoin); a click on the rest
// opens the folder, today's meetings.

using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.Panels;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>What a meeting face shows at a minute: its meeting and the words for it, or why not.</summary>
/// <param name="Shown">The meeting it shows; null for none.</param>
/// <param name="Lit">The meeting is on, or starts within the face's lead.</param>
/// <param name="Placeholder">What it says with no meeting: none left today, none ahead, no calendar; empty for nothing.</param>
/// <param name="Hidden">Nothing to show, and the face shows a meeting only shortly before it: off the bar at rest (<see cref="MeetingFaceDrawing.NothingToShow"/>).</param>
internal readonly record struct MeetingFaceState(Meeting? Shown, bool Lit, string Title, string When, string Placeholder, bool Hidden)
{
    /// <summary>Its meeting's link, where it shows one with a link Karpo knows.</summary>
    public Karpo.Accounts.MeetingLink? Link => Shown?.Link;

    /// <summary>What it shows, as one string: a new one paints the face again.</summary>
    public string Reading => $"{Title}|{When}|{Lit}|{Link?.Url}|{Shown?.Event.Colour}|{Placeholder}|{Hidden}";
}

internal sealed unsafe class MeetingFaceDrawing : IFaceDrawing
{
    // Segoe Fluent Icons' camera, which Teams', Zoom's and Outlook's Join buttons wear.
    internal const string JoinGlyph = "";

    // Templates for the words' room: Windows' own phrase for the time and a title of a dozen letters
    // (the short look) or two dozen (the long one), so the face is as wide in every language a run is in.
    private const string ShortTitle = "Design review"; // not-ui
    private const string LongTitle = "Quarterly planning review"; // not-ui

    public string Id => FolderFaces.MeetingId;

    public bool Seconds(FolderFace face) => false;

    /// <summary>
    /// What <paramref name="face"/> shows from <paramref name="store"/> at <paramref name="now"/> (a wall-clock
    /// time in <paramref name="zone"/>). Pure: the folder and the tests ask it alike.
    /// </summary>
    internal static MeetingFaceState StateOf(FolderFace face, CalendarStore? store, DateTime now, TimeZoneInfo zone)
    {
        var show = MeetingFace.ShowOf(face);
        if (store is null)
        {
            return new MeetingFaceState(null, false, "", "", S.DockMeetingNone, false);
        }
        if (!store.HasCalendars)
        {
            // Still reading: nothing yet, rather than a "none" that is not so.
            // Nothing to show: a dash, never a sentence (§388).
        var why = FaceDrawings.Dash;
            return new MeetingFaceState(null, false, "", "", why, show == MeetingFace.Soon);
        }
        var lead = MeetingFace.LeadOf(face);
        var ahead = Meetings.Ahead(store, now, show == MeetingFace.Next ? Meetings.DaysAhead : 1, zone);
        if (Meetings.Shown(ahead, show, lead, now, zone) is not { } meeting)
        {
            return new MeetingFaceState(null, false, "", "", show == MeetingFace.Next ? S.DockMeetingNoneAhead : S.DockMeetingNone, show == MeetingFace.Soon);
        }
        var lit = Meetings.Lit(meeting, Stamp.WallToUtc(now, zone), lead);
        return new MeetingFaceState(meeting, lit, meeting.Event.Title, MeetingText.When(meeting, now, zone), "", false);
    }

    /// <summary>The mark's width and the room after it, in px.</summary>
    private static (float Mark, float Gap) MarkOf(in Fit fit) => (MathF.Max(2, fit.Px(3)), fit.Px(6));

    /// <summary>The Join button's size: 24 DIP tall where the bar has room, a third wider than tall.</summary>
    internal static (float Width, float Height) JoinSize(in Fit fit)
    {
        var tall = MathF.Max(fit.Px(16), MathF.Min(fit.Px(24), fit.Cell - (2 * fit.Px(6))));
        return (TextLine.Round(tall * 4 / 3), TextLine.Round(tall));
    }

    /// <summary>The room <paramref name="face"/>'s words keep, from its width alone.</summary>
    private static float WordsRoom(Gfx gfx, in Fit fit, FolderFace face)
    {
        var when = TextLine.Template(gfx, S.DockMeetingIn(S.WidgetTileHoursMinutes(2, 55)), fit.Primary, Weight.Semibold).Width;
        return MeetingFace.WidthOf(face) switch
        {
            MeetingFace.Compact => when,
            MeetingFace.Wide => TextLine.Measure(gfx, LongTitle + MeetingText.Dot, fit.Primary, Weight.Regular, null) + when,
            _ => TextLine.Measure(gfx, ShortTitle + MeetingText.Dot, fit.Primary, Weight.Regular, null) + when,
        };
    }

    public FaceSize Measure(in FaceContext context)
    {
        var fit = context.Fit;
        if (context.Vertical)
        {
            return new(fit.Glyph(), Square: true);
        }
        // What it shows now (§244), its words up to the room its width keeps, past which they are cut:
        // "No more meetings today" alone, or the mark, the title and how soon, and Join where it has a link.
        var state = StateOf(context);
        var gfx = context.Gfx;
        var px = fit.Primary;
        var most = WordsRoom(gfx, fit, context.Face);
        var join = MeetingFace.Joins(context.Face) && state.Link is not null ? fit.Gap + JoinSize(fit).Width : 0;
        if (state.Shown is null)
        {
            var placeholder = state.Placeholder.Length == 0 ? 0 : TextLine.Measure(gfx, state.Placeholder, px, Weight.Regular, null);
            return new(MathF.Ceiling(MathF.Min(most, placeholder) + join), Square: false);
        }
        var (mark, gap) = MarkOf(fit);
        var whenWeight = state.Lit ? Weight.Semibold : Weight.Regular;
        var (title, when) = MeetingText.Fit(state.Title, state.When, most,
            text => TextLine.Measure(gfx, text + MeetingTile.Spaced, px, Weight.Regular, null),
            text => TextLine.Measure(gfx, text, px, whenWeight, null));
        var words = (title.Length > 0 ? TextLine.Measure(gfx, title + MeetingText.Dot + MeetingTile.Spaced, px, Weight.Regular, null) : 0)
            + TextLine.Measure(gfx, when, px, whenWeight, null);
        return new(MathF.Ceiling(mark + gap + MathF.Min(most, words) + join), Square: false);
    }

    /// <summary>What the face shows now: its folder's meeting, or what it says with no calendars where it is only measured.</summary>
    private static MeetingFaceState StateOf(in FaceContext context) =>
        context.Folder?.MeetingOf(context.Face) ?? StateOf(context.Face, null, default, TimeZoneInfo.Local);

    /// <summary>
    /// Where the Join button stands in <paramref name="box"/>: at its end, in the middle of the bar's
    /// thickness. Nothing down a bar on its side, nor where the face keeps no room for it.
    /// </summary>
    internal static D2D_RECT_F JoinBox(in FaceContext context, D2D_RECT_F box)
    {
        if (context.Vertical || !MeetingFace.Joins(context.Face))
        {
            return default;
        }
        var (width, height) = JoinSize(context.Fit);
        return PopupPaint.Rect(box.right - width, TextLine.Round(((box.top + box.bottom) / 2) - (height / 2)), width, height);
    }

    /// <summary>Where the face draws a Join button now: its box where its meeting has a link, else nothing.</summary>
    internal static D2D_RECT_F JoinShown(in FaceContext context, D2D_RECT_F box, in MeetingFaceState state) =>
        state.Link is not null ? JoinBox(context, box) : default;

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var state = StateOf(context);
        var fit = context.Fit;
        var ink = new IslandInk(context.Style.Look);
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        var middle = (box.top + box.bottom) / 2;
        if (context.Vertical)
        {
            var glyph = fit.Glyph();
            var colour = state.Lit ? ink.Accent : state.Shown is not null ? ink.Text : ink.Disabled;
            PopupPaint.Glyph(dc, context.Gfx, JoinGlyph, glyph, colour, PopupPaint.Rect(box.left + TextLine.Round(((box.right - box.left) - glyph) / 2), middle - (glyph / 2), glyph, glyph));
            return;
        }
        var join = JoinShown(context, box, state);
        var right = TilePaint.Width(join) > 0 ? join.left - fit.Gap : box.right;
        var px = fit.Primary;
        if (state.Shown is not { } meeting)
        {
            Words(dc, context.Gfx, state.Placeholder, px, Weight.Regular, ink.Secondary, box.left, middle, MathF.Max(1, right - box.left));
            return;
        }
        var (mark, gap) = MarkOf(fit);
        var markTall = MathF.Max(fit.Px(8), TextLine.Round(px * 1.05f));
        PopupPaint.Fill(dc, PopupPaint.Rect(box.left, TextLine.Round(middle - (markTall / 2)), mark, markTall), mark / 2,
            meeting.Event.Colour.A == 0 || ink.HighContrast ? ink.Accent : PopupPaint.Ink(FlyoutTheme.Opaque(meeting.Event.Colour)));
        var left = box.left + mark + gap;
        var room = MathF.Max(1, right - left);
        var whenWeight = state.Lit ? Weight.Semibold : Weight.Regular;
        var gfx = context.Gfx;
        var (title, when) = MeetingText.Fit(state.Title, state.When, room,
            text => TextLine.Measure(gfx, text + MeetingTile.Spaced, px, Weight.Regular, null),
            text => TextLine.Measure(gfx, text, px, whenWeight, null));
        if (title.Length > 0)
        {
            var lead = title + MeetingText.Dot;
            Words(dc, gfx, lead, px, Weight.Regular, ink.Text, left, middle, room);
            var used = TextLine.Measure(gfx, lead + MeetingTile.Spaced, px, Weight.Regular, null);
            left += used;
            room = MathF.Max(1, room - used);
        }
        Words(dc, gfx, when, px, whenWeight, state.Lit ? ink.Accent : ink.Secondary, left, middle, room);
        if (TilePaint.Width(join) > 0)
        {
            PaintJoin(dc, context, join, state.Lit, ink);
        }
    }

    /// <summary>
    /// The Join button: the camera on a pill as Windows 11's compact button, the accent's fill with the
    /// ink that reads on it while the meeting is lit, a faint fill and the text's ink otherwise; in high
    /// contrast an outline in the system's colours.
    /// </summary>
    internal static void PaintJoin(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F join, bool lit, in IslandInk ink)
    {
        var fit = context.Fit;
        var radius = fit.Px(4);
        var look = context.Style.Look;
        DXGI_RGBA glyphInk;
        if (ink.HighContrast)
        {
            PopupPaint.Stroke(dc, join, radius, MathF.Max(1, fit.Px(1)), lit ? ink.Accent : ink.Text);
            glyphInk = lit ? ink.Accent : ink.Text;
        }
        else if (lit)
        {
            PopupPaint.Fill(dc, join, radius, ink.Accent);
            glyphInk = look.Dark ? new DXGI_RGBA(0, 0, 0, 1) : new DXGI_RGBA(1, 1, 1, 1);
        }
        else
        {
            PopupPaint.Fill(dc, join, radius, look.Dark ? new DXGI_RGBA(1, 1, 1, 0.08f) : new DXGI_RGBA(0, 0, 0, 0.06f));
            PopupPaint.Stroke(dc, join, radius, MathF.Max(1, fit.Px(1)), look.Dark ? new DXGI_RGBA(1, 1, 1, 0.07f) : new DXGI_RGBA(0, 0, 0, 0.08f));
            glyphInk = ink.Text;
        }
        var glyph = fit.Px(16);
        PopupPaint.Glyph(dc, context.Gfx, JoinGlyph, glyph, glyphInk,
            PopupPaint.Rect(TextLine.Round(((join.left + join.right) / 2) - (glyph / 2)), TextLine.Round(((join.top + join.bottom) / 2) - (glyph / 2)), glyph, glyph));
    }

    /// <summary>One run of words from <paramref name="left"/>, in the middle of the bar's thickness, cut to <paramref name="room"/>.</summary>
    private static void Words(ID2D1DeviceContext* dc, Gfx gfx, string text, float px, Weight weight, DXGI_RGBA colour, float left, float middle, float room)
    {
        if (text.Length == 0)
        {
            return;
        }
        var line = TextLine.Lay(gfx, text, px, weight, MathF.Max(1, room));
        ID2D1SolidColorBrush* brush = null;
        try
        {
            brush = Gfx.Brush(dc, colour);
            line.Draw(dc, brush, colour, TextLine.Round(left), TextLine.Round(middle - (line.Height / 2)));
        }
        finally
        {
            line.Release();
            ComUtil.Release(ref brush);
        }
    }

    public string Reading(in FaceContext context) => context.Folder?.MeetingOf(context.Face).Reading ?? "";

    /// <summary>
    /// Set to show a meeting only shortly before it, with none that close (<see cref="MeetingFaceState.Hidden"/>):
    /// off the bar at rest as any face with nothing to show; arranged there, greyed, saying there is none (§327).
    /// </summary>
    public bool NothingToShow(FolderFace face, FolderIsland folder) => folder.MeetingOf(face).Hidden;

    /// <summary>Its meeting, when and where it is, and how it is joined; or what it says with none.</summary>
    public string? Tooltip(in FaceContext context)
    {
        if (context.Folder?.MeetingOf(context.Face) is not { } state)
        {
            return null;
        }
        if (state.Shown is not { } meeting)
        {
            return state.Placeholder.Length > 0 ? state.Placeholder : null;
        }
        var now = Clock.Now();
        var lines = new List<string> { meeting.Event.Title, MeetingText.Detail(meeting, now) + MeetingText.Dot + state.When };
        if (state.Link is { } link && MeetingFace.Joins(context.Face))
        {
            lines.Add(S.DockMeetingJoinIn(Karpo.Accounts.MeetingLinks.Name(link.Service)));
        }
        return string.Join('\n', lines);
    }
}
