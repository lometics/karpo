// What a now-playing face reads of a media session, and how it settles each read against what it
// showed before. The now-playing line (and the face that reads it) reads a session through
// here, on the thread pool; the media flyout did too, until it went (docs\widgets.md §96).
//
// Why there are rules at all (backlog 22, Spotify's buttons going missing): a player does not change
// tracks in one step. Spotify clears its words, says its controls are all off and "changing", and
// only then hands over the next track and its controls, each with an event of its own. A read that
// landed in that gap showed "Nothing playing" with the buttons quiet, or the flyout without any
// buttons at all; and the flyout dropped any event that came while a read was running (its art
// decode takes a moment), so the controls that arrived during it were never read, and the buttons
// stayed off until the next event — which, on a paused player, may be never. Now:
//
//   * Rereads: a change that comes while a read runs asks for one more read after it. Never two at
//     once, never one lost.
//   * MediaGap: a gap is not an answer. Words gone while the app still holds its session keep the
//     track that was shown; all three controls off for a track of the app that just allowed some
//     keep the controls that were shown, until the app says something else. An app that has never
//     shown words is shown by its name, with whatever it allows, rather than as nothing.

using Windows.Media.Control;
using Windows.Storage.Streams;
using PlaybackStatus = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;
using RepeatMode = Windows.Media.MediaPlaybackAutoRepeatMode;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;

namespace Karpo.Dock;

/// <summary>Which of previous, play or pause, and next a session allows.</summary>
internal readonly record struct MediaButtons(bool Previous, bool Toggle, bool Next)
{
    /// <summary>Any of the three: an app that allows none says nothing about its controls.</summary>
    public bool Any => Previous || Toggle || Next;
}

/// <summary>
/// One read of a session: its app, its words, its state and its controls, and the rest a face may
/// want — the thumbnail, the timeline, shuffle and repeat. Made by <see cref="Of"/> from Windows'
/// session, or by a test from nothing at all.
/// </summary>
/// <param name="App">The session's <c>SourceAppUserModelId</c>.</param>
internal sealed record SessionRead(string App, string Title, string Artist, string Album, PlaybackStatus Status, MediaButtons Buttons)
{
    /// <summary>The session read, which a face's buttons act on; null for a test's.</summary>
    public Session? Session { get; init; }

    public IRandomAccessStreamReference? Thumbnail { get; init; }

    /// <summary>The timeline, where the app gives one; null where it gives none (a live stream, some tabs).</summary>
    public GlobalSystemMediaTransportControlsSessionTimelineProperties? Timeline { get; init; }

    public bool CanSeek { get; init; }

    public bool CanShuffle { get; init; }

    public bool Shuffle { get; init; }

    public bool CanRepeat { get; init; }

    public RepeatMode? Repeat { get; init; }

    /// <summary>Neither a title nor an artist: nothing to name the track by.</summary>
    public bool Blank => Title.Length == 0 && Artist.Length == 0;

    public bool Playing => Status == PlaybackStatus.Playing;

    /// <summary>
    /// Reads <paramref name="session"/>, off the dock's thread: each call may cross into the app's
    /// process. Throws where the app went away in the middle of it.
    /// </summary>
    public static async Task<SessionRead> Of(Session session, CancellationToken cancel)
    {
        var properties = await session.TryGetMediaPropertiesAsync().AsTask(cancel).ConfigureAwait(false);
        // The playback state after the words: a change of track sends the words first, so what is
        // read here is at least as new as they are.
        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        var controls = playback?.Controls;
        return new SessionRead(
            session.SourceAppUserModelId ?? "",
            properties?.Title?.Trim() ?? "",
            properties?.Artist?.Trim() ?? "",
            properties?.AlbumTitle?.Trim() ?? "",
            playback?.PlaybackStatus ?? PlaybackStatus.Closed,
            new MediaButtons(
                controls?.IsPreviousEnabled ?? false,
                controls?.IsPlayPauseToggleEnabled ?? false,
                controls?.IsNextEnabled ?? false))
        {
            Session = session,
            Thumbnail = properties?.Thumbnail,
            Timeline = timeline,
            CanSeek = controls?.IsPlaybackPositionEnabled ?? false,
            CanShuffle = controls?.IsShuffleEnabled ?? false,
            Shuffle = playback?.IsShuffleActive ?? false,
            CanRepeat = controls?.IsRepeatEnabled ?? false,
            Repeat = playback?.AutoRepeatMode,
        };
    }
}

/// <summary>What a face does with a read, given what it showed (<see cref="MediaGap.Judge"/>).</summary>
internal enum MediaVerdict
{
    /// <summary>Nothing plays: the session is gone, or stopped with nothing to show.</summary>
    Nothing,

    /// <summary>A gap in the middle of a change: what was shown stays.</summary>
    Hold,

    /// <summary>The read is what is shown.</summary>
    Show,
}

internal static class MediaGap
{
    /// <summary>
    /// What a face does with <paramref name="read"/>, having shown <paramref name="shownApp"/>'s
    /// track (null for nothing). A session with words is shown. One without: nothing where it is
    /// closed or stopped; held where it is the app already shown, which is a change of track half
    /// done; else the app itself, by its name, with what it allows — Windows' own flyout shows it,
    /// and its play button is how the person starts it.
    /// </summary>
    public static MediaVerdict Judge(SessionRead read, string? shownApp)
    {
        if (!read.Blank)
        {
            return MediaVerdict.Show;
        }
        if (read.Status is PlaybackStatus.Closed or PlaybackStatus.Stopped)
        {
            return MediaVerdict.Nothing;
        }
        return Same(read.App, shownApp) ? MediaVerdict.Hold : MediaVerdict.Show;
    }

    /// <summary>
    /// The buttons a face shows for <paramref name="read"/>: what it says, except that all three off
    /// for the app whose buttons are shown (<paramref name="shownApp"/>, <paramref name="shown"/>) is
    /// a change under way and not the app forbidding everything — the shown ones stay until the app
    /// says something else. A session that closed or stopped is not changing: its buttons go.
    /// </summary>
    public static MediaButtons Buttons(SessionRead read, string? shownApp, MediaButtons shown) =>
        Buttons(read.App, read.Status, read.Buttons, shownApp, shown);

    /// <summary>
    /// <see cref="Buttons(SessionRead, string?, MediaButtons)"/> for what a read found of
    /// <paramref name="app"/>'s session: its <paramref name="status"/> and <paramref name="buttons"/>.
    /// The window previews' buttons, which read no words, are settled by this too.
    /// </summary>
    public static MediaButtons Buttons(string app, PlaybackStatus status, MediaButtons buttons, string? shownApp, MediaButtons shown) =>
        !buttons.Any && shown.Any && Same(app, shownApp) && status is not (PlaybackStatus.Closed or PlaybackStatus.Stopped)
            ? shown
            : buttons;

    private static bool Same(string app, string? shown) =>
        app.Length > 0 && string.Equals(app, shown, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Whether nothing plays, as the now-playing face's "Hide it while nothing plays" asks it
/// (NowPlayingFace.HidesIdle, docs\widgets.md §230): no session, or one that is paused, stopped or
/// closed — Windows keeps a paused player's session for as long as the app runs, so a session alone
/// is not something playing. A track that stops playing counts as nothing only <see cref="Grace"/>
/// later: a change of track passes through "changing" and "paused", and a pause and play again a
/// moment later should not take the folder off the bar and put it back. What has not played since
/// the face was made is nothing at once. Times are a monotonic clock's, in ms.
/// </summary>
internal sealed class MediaQuiet(TimeSpan grace)
{
    /// <summary>How long a track that stopped playing still counts as playing.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(5);

    private readonly long graceMs = (long)grace.TotalMilliseconds;
    private bool playing;

    // When it last stopped playing; null where it has not played since it was made.
    private long? stopped;

    /// <summary>What a read found: whether the followed session plays, at <paramref name="ms"/>. True where the grace started now.</summary>
    public bool Heard(bool plays, long ms)
    {
        if (plays)
        {
            playing = true;
            return false;
        }
        if (!playing)
        {
            return false;
        }
        playing = false;
        stopped = ms;
        return true;
    }

    /// <summary>Whether nothing plays at <paramref name="ms"/>.</summary>
    public bool Idle(long ms) => !playing && (stopped is not { } at || ms - at >= graceMs);

    /// <summary>The grace, the time from a stop to nothing playing.</summary>
    public TimeSpan Wait => TimeSpan.FromMilliseconds(graceMs);
}

/// <summary>
/// Reads something again whenever asked, on the thread pool, a short moment after the ask so a burst
/// of events is one read: never two reads at once, and never an ask lost. An ask while a read runs
/// is one more read after it — the read under way may have read what changed before it changed.
/// </summary>
internal sealed class Rereads
{
    private const int Idle = 0;
    private const int Reading = 1;
    private const int Again = 2;

    private readonly Func<Task> read;
    private readonly int waitMs;
    private int state;

    /// <param name="read">The read; its own failures are its own to handle, and any that escapes is logged.</param>
    /// <param name="waitMs">How long an ask waits for the rest of its burst.</param>
    public Rereads(Func<Task> read, int waitMs = 60)
    {
        this.read = read;
        this.waitMs = waitMs;
    }

    /// <summary>Asks for a read. From any thread.</summary>
    public void Ask()
    {
        while (true)
        {
            switch (Volatile.Read(ref state))
            {
                case Idle when Interlocked.CompareExchange(ref state, Reading, Idle) == Idle:
                    _ = Task.Run(Loop);
                    return;
                case Reading when Interlocked.CompareExchange(ref state, Again, Reading) == Reading:
                case Again:
                    return;
            }
        }
    }

    private async Task Loop()
    {
        while (true)
        {
            if (waitMs > 0)
            {
                await Task.Delay(waitMs).ConfigureAwait(false);
            }
            // From here on an ask is after this read began, and asks for another.
            Interlocked.Exchange(ref state, Reading);
            try
            {
                await read().ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: media not read: {error.GetType().Name}");
            }
            if (Interlocked.CompareExchange(ref state, Idle, Reading) == Reading)
            {
                return;
            }
        }
    }
}
