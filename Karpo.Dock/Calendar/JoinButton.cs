// A meeting's Join wherever Karpo lists it (docs\widgets.md §206): the day's events, the day and week
// views, the calendar's agenda and the next meetings' line each give a meeting with a link the same
// button the meeting line has (MeetingTile) — Windows 11's standard button with the camera and "Join",
// the camera alone where there is no room for the word, filled with the accent from the lead before the
// meeting starts (MeetingTile.Lead) until it ends. A meeting over has none, nor an event with no link
// Karpo knows, nor an all-day event (a day, not a meeting).
//
// A click on a Join is the Join's and never the event's: the line answers it with an app opening
// (IslandView.App), which the folder hands to the dock as it hands a pin's click (FolderFlyout.ActOf),
// and the dock opens the meeting once the folder has gone (IMeetingLine.Open, MeetingJoin). The keys: the
// lit Join is the line's default button, as an accent button is a dialog's — Enter or Space on the line
// joins the meeting on now or about to start. A right click on a meeting with a Join has "Join meeting"
// in its menu (WidgetAct.JoinMeeting, IMeetingLine.MenuMeeting), which is the way in where a block is too
// small for the button.

using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock.Panels;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>A line that lists meetings with a Join of their own (docs\widgets.md §206).</summary>
internal interface IMeetingLine
{
    /// <summary>The dock, once the folder has gone: opens the meeting a click on a Join chose, or the event a click on its row did (§434), once.</summary>
    void Open();

    /// <summary>
    /// The meeting a right click is on and its Join's link, for the menu's "Join meeting"; null over
    /// anything else, and over a meeting with no Join.
    /// </summary>
    (string Title, MeetingLink Link)? MenuMeeting();
}

/// <summary>
/// What a line keeps between a click and the folder going: the meeting a Join chose, or the address an event's row
/// opens (EventLinks, §434).
/// </summary>
internal sealed class EventChoice
{
    private MeetingLink? chosen;
    private string? address;

    // Enter or Space on the line: its next click with no part of its own is the lit Join's.
    private bool keyed;

    /// <summary>A Join clicked: its meeting, opened once the folder has gone; nothing for no link.</summary>
    public IslandAction Choose(MeetingLink? link)
    {
        keyed = false;
        chosen = link;
        address = null;
        return link is not null ? new IslandAction(View: IslandView.App) : default;
    }

    /// <summary>An event's row clicked: <paramref name="target"/> (<see cref="EventLinks.Target"/>), opened once the folder has gone; nothing for none.</summary>
    public IslandAction Opens(string? target)
    {
        keyed = false;
        chosen = null;
        address = target;
        return target is not null ? new IslandAction(View: IslandView.App) : default;
    }

    /// <summary>
    /// Enter or Space on the line: false, so the folder clicks the line as a key does (no part), and
    /// that click is <paramref name="lit"/>'s Join (<see cref="Keyed"/>). Nothing where none is lit.
    /// </summary>
    public bool Key(int key, MeetingLink? lit)
    {
        keyed = key is TerraFX.Interop.Windows.VK.VK_RETURN or TerraFX.Interop.Windows.VK.VK_SPACE && lit is not null;
        return false;
    }

    /// <summary>Whether a click with no part is the keys' (<see cref="Key"/>): asked once.</summary>
    public bool Keyed()
    {
        var was = keyed;
        keyed = false;
        return was;
    }

    /// <summary>Opens the meeting or the event chosen, once.</summary>
    public void Open()
    {
        if (chosen is { } link)
        {
            MeetingJoin.Open(link);
        }
        else if (address is { } target)
        {
            EventLinks.Open(target);
        }
        chosen = null;
        address = null;
    }

    /// <summary>The folder closed: nothing is chosen.</summary>
    public void Forget()
    {
        chosen = null;
        address = null;
        keyed = false;
    }
}

internal static unsafe class JoinButton
{
    /// <summary>A Join's target on a line, plus its row's (or block's) place: past every row's and every day's, so a row is its event's and its Join its meeting's.</summary>
    internal const int Target = 4_000_000;

    /// <summary>
    /// The link <paramref name="item"/>'s Join opens at <paramref name="now"/> (a wall-clock time in
    /// <paramref name="zone"/>, this PC's when null): one Karpo knows, on an event with a time of its own
    /// that has not ended. Null: it has no Join.
    /// </summary>
    public static MeetingLink? Of(in CalendarEvent item, DateTime now, TimeZoneInfo? zone = null)
    {
        if (item.AllDay || MeetingLinks.Of(item.Join) is not { } link)
        {
            return null;
        }
        var frame = zone ?? TimeZoneInfo.Local;
        var end = item.End > item.Start ? item.End : item.Start;
        return Stamp.WallToUtc(end, frame) > Stamp.WallToUtc(now, frame) ? link : null;
    }

    /// <summary>
    /// Where a press lands on a row's Join <paramref name="join"/>: a button's reach across (LineButton.Hit), the row's whole
    /// height (§418) — at any text size, the row taller than a reach — the rest of the row its event's (§434).
    /// </summary>
    public static D2D_RECT_F Hit(in LineButton sizes, D2D_RECT_F join, float top, float bottom) =>
        sizes.Hit(join) with { top = top, bottom = bottom };

    /// <summary>Whether <paramref name="item"/>'s Join is lit at <paramref name="now"/>: on now, or starting within the lead.</summary>
    public static bool Lit(in CalendarEvent item, DateTime now, TimeZoneInfo? zone = null)
    {
        var frame = zone ?? TimeZoneInfo.Local;
        var start = Stamp.WallToUtc(item.Start, frame);
        var end = item.End > item.Start ? Stamp.WallToUtc(item.End, frame) : start;
        return Meetings.Lit(new Meeting(item, start, end), Stamp.WallToUtc(now, frame), MeetingTile.Lead);
    }

    /// <summary>How wide the button with the camera and "Join" is, px: the lines' one button (LineButton).</summary>
    public static float WordedWidth(Gfx gfx, in LineButton sizes) => sizes.Width(gfx, S.DockMeetingJoin, glyph: true);

    /// <summary>
    /// The button in <paramref name="box"/>: the camera and "Join" where <paramref name="worded"/>, else
    /// the camera alone; the accent's while <paramref name="lit"/>; under the pointer and pressed as told.
    /// </summary>
    public static void Paint(ID2D1DeviceContext* dc, Gfx gfx, in LineButton sizes, FlyoutTheme theme, D2D_RECT_F box, bool worded, bool lit, bool hot, bool pressed) =>
        sizes.Paint(dc, gfx, theme, box, worded ? S.DockMeetingJoin : "", MeetingFaceDrawing.JoinGlyph, lit ? ButtonStyle.Accent : ButtonStyle.Standard, hot, pressed);


    /// <summary>What the button is called for a screen reader and a test: "Join" and the meeting's title.</summary>
    public static string Name(string title) => S.DockMeetingJoinNamed(title);

    /// <summary>Its tooltip: the app it joins in ("Join in Microsoft Teams").</summary>
    public static string Tip(MeetingLink link) => S.DockMeetingJoinIn(MeetingLinks.Name(link.Service));
}
