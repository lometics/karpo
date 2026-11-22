// meeting: a folder wearing the next meeting's face (docs\widgets.md §205, MeetingFaceDrawing). The
// face reads the calendars every calendar widget reads (CalendarLines.Store: the dock's, or a test's),
// attached while it wears the face so a change wakes the folder and the face is painted again in place;
// the dock's minute tick moves "in 4 min" on. Its Join is a button of its own inside the folder's
// cell: a click there opens the meeting, anywhere else the folder.

using System.Numerics;
using Karpo.Core;

namespace Karpo.Dock;

internal sealed unsafe partial class FolderIsland : IPointClickIsland
{
    // The calendars the meeting face reads while it wears one, and whether it has asked them to read.
    private CalendarStore? meetings;
    private bool meetingsLooked;

    /// <summary>meeting: whether it wears the next meeting's face.</summary>
    internal bool ShowsMeeting => options.Wears(FolderFaces.Meeting);

    /// <summary>meeting: the calendars the meeting face reads; null for another face.</summary>
    internal CalendarStore? Meetings => ShowsMeeting ? meetings : null;

    /// <summary>
    /// meeting: the face follows the calendars — attached when it is put on, so their news wakes the
    /// folder, let go when it is taken off. Attaching reads nothing: the face's first look does (MeetingOf).
    /// </summary>
    private void FollowMeetings()
    {
        if (ShowsMeeting == meetings is not null)
        {
            return;
        }
        if (meetings is { } gone)
        {
            meetings = null;
            meetingsLooked = false;
            gone.Detach(this);
            return;
        }
        meetings = CalendarLines.Store;
        meetings.Attach(this);
    }

    /// <summary>
    /// meeting: what the meeting face <paramref name="face"/> shows now. Its first asking is the look that
    /// starts the calendars' first read where nothing has yet (docs\widgets.md §11).
    /// </summary>
    internal MeetingFaceState MeetingOf(FolderFace face)
    {
        if (Meetings is not { } store)
        {
            return MeetingFaceDrawing.StateOf(face, null, default, TimeZoneInfo.Local);
        }
        if (!meetingsLooked)
        {
            meetingsLooked = true;
            store.Open();
        }
        return MeetingFaceDrawing.StateOf(face, store, Clock.Now(), TimeZoneInfo.Local);
    }

    /// <summary>
    /// meeting: the meeting its right click's "Join meeting" joins (§206): the first of its meeting faces'
    /// with a link, whether the face keeps room for the button or not; null for none.
    /// </summary>
    internal (string Title, Karpo.Accounts.MeetingLink Link)? MenuMeeting()
    {
        if (!ShowsMeeting)
        {
            return null;
        }
        foreach (var face in options.Faces.Where(face => face.Info == FolderFaces.Meeting))
        {
            if (MeetingOf(face) is { Shown: { } meeting, Link: { } link })
            {
                return (meeting.Event.Title, link);
            }
        }
        return null;
    }

    /// <summary>
    /// A click at a point of the button that a face takes as its own: a meeting face's Join, or a markets
    /// face, which searches what it tracks (FolderIsland.Markets.cs, §263); anywhere else is the folder's click.
    /// </summary>
    public bool ClickAt(int index, Vector2 point, Vector2 size, in IslandStyle style) =>
        JoinAt(point, size, style) || MarketAt(point, size, style);

    /// <summary>
    /// meeting: a click on a meeting face's Join opens its meeting, there and then. Only where the face
    /// draws the button now: a meeting with a link it knows.
    /// </summary>
    private bool JoinAt(Vector2 point, Vector2 size, in IslandStyle style)
    {
        if (!ShowsMeeting || style.Vertical)
        {
            return false;
        }
        var shown = FacesOnBar;
        var boxes = FaceBoxes(Gfx, options, style, size.X, size.Y, shown, this);
        for (var at = 0; at < shown.Count && at < boxes.Length; at++)
        {
            var face = shown[at];
            if (face.Info != FolderFaces.Meeting)
            {
                continue;
            }
            var state = MeetingOf(face);
            var join = MeetingFaceDrawing.JoinShown(FaceIn(style, face), boxes[at], state);
            // The pill and a little round it, the whole of the bar's thickness: a target as tall as the bar.
            var slack = new Fit(style).Px(4);
            if (state.Link is { } link && TilePaint.Width(join) > 0
                && point.X >= join.left - slack && point.X < join.right + slack && point.Y >= 0 && point.Y < size.Y)
            {
                MeetingJoin.Open(link);
                return true;
            }
        }
        return false;
    }
}
