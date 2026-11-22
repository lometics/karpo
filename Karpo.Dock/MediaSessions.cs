// The media sessions Windows knows of (GSMTC: what its own media flyout
// shows), read once for the whole dock. The now-playing island and the window
// previews' media buttons both ask here, so the process holds one session
// manager however many islands and previews there are.
//
// Everything that asks a session something runs on the thread pool, where
// the manager's and the sessions' events arrive anyway: each call may cross
// into another process. What was read is published as an immutable snapshot,
// which the dock thread only ever reads, and Changed tells the dock thread of
// a new one through its DispatcherQueue. Nothing here polls: no session, no
// event, no work. Each read is settled against the one before by the same
// rule the now-playing faces read by (MediaGap, Settle), so a preview's
// buttons do not blink off while a player changes tracks.

using Windows.Media.Control;
using Windows.System;
using PlaybackStatus = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;
using SessionManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;

namespace Karpo.Dock;

/// <summary>
/// One media session as the dock shows it: which app it belongs to, whether it
/// plays, and which of the three buttons it allows. Compared by those values
/// alone, so a snapshot read again with nothing new in it equals the old one.
/// </summary>
/// <param name="SourceAppId">The session's <c>SourceAppUserModelId</c>: an AUMID, or a desktop app's exe name ("Spotify.exe").</param>
public sealed record MediaControls(string SourceAppId, bool Playing, bool CanPrevious, bool CanPlayPause, bool CanNext)
{
    /// <summary>The session the buttons act on; left out of equality.</summary>
    internal Session? Session { get; init; }

    public void Previous() => Send(MediaCommand.Previous);

    public void PlayPause() => Send(MediaCommand.PlayPause);

    public void Next() => Send(MediaCommand.Next);

    public bool Equals(MediaControls? other) =>
        other is not null
        && SourceAppId == other.SourceAppId
        && Playing == other.Playing
        && CanPrevious == other.CanPrevious
        && CanPlayPause == other.CanPlayPause
        && CanNext == other.CanNext;

    public override int GetHashCode() => HashCode.Combine(SourceAppId, Playing, CanPrevious, CanPlayPause, CanNext);

    /// <summary>
    /// Fire and forget, off the dock's thread: the call reaches the app's
    /// process, and an app that hangs must not hang the dock. The session
    /// says what changed through its events.
    /// </summary>
    private void Send(MediaCommand command)
    {
        if (Session is not { } session)
        {
            return;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                _ = command switch
                {
                    MediaCommand.Previous => await session.TrySkipPreviousAsync(),
                    MediaCommand.Next => await session.TrySkipNextAsync(),
                    _ => await session.TryTogglePlayPauseAsync(),
                };
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // The app went away between the snapshot and the click.
                Console.Error.WriteLine($"Karpo: media {command} failed: {error.GetType().Name} 0x{error.HResult:X8}");
            }
        });
    }

    private enum MediaCommand
    {
        Previous,
        PlayPause,
        Next,
    }
}

public sealed class MediaSessions
{
    private static readonly Lock Creating = new();
    private static MediaSessions? shared;

    // The dock thread's queue, where Changed is raised; null on a thread
    // without one (a test), and then Changed comes on the reading thread.
    private readonly DispatcherQueue? dock;
    private readonly Lock gate = new();

    // Pool-thread state, under the gate: the manager and the sessions whose
    // events are followed.
    private SessionManager? manager;
    private readonly List<Session> followed = [];

    // What the dock thread reads.
    private IReadOnlyList<MediaControls> sessions = [];
    private Session? current;

    // A read is on its way; a Changed is queued on the dock thread.
    private int reading;
    private int raising;

    private MediaSessions(DispatcherQueue? dock)
    {
        this.dock = dock;
        if (dock is not null)
        {
            // The favicons of the sites a browser plays on (NowPlayingSource): one that arrives is
            // news for every now-playing face, as a session's own events are.
            NowPlayingSource.Start(Raise);
        }
        _ = Task.Run(Connect);
    }

    /// <summary>
    /// The one instance, made the first time something needs it; the first use
    /// has to be on the dock's thread, whose queue Changed is raised on.
    /// </summary>
    public static MediaSessions Shared
    {
        get
        {
            if (Volatile.Read(ref shared) is { } made)
            {
                return made;
            }
            lock (Creating)
            {
                if (shared is null)
                {
                    DispatcherQueue? queue = null;
                    try
                    {
                        queue = DispatcherQueue.GetForCurrentThread();
                    }
                    catch (Exception error) when (error is not OutOfMemoryException)
                    {
                        // No WinRT on this thread: Changed comes from the pool.
                    }
                    Volatile.Write(ref shared, new MediaSessions(queue));
                }
                return shared!;
            }
        }
    }

    /// <summary>
    /// Something about the sessions changed: one came or went, or one's state,
    /// title or picture. Raised on the dock thread, once for a burst.
    /// </summary>
    public event Action? Changed;

    /// <summary>Every session, as last read; empty until the manager answers.</summary>
    public IReadOnlyList<MediaControls> Sessions => Volatile.Read(ref sessions);

    /// <summary>
    /// The session Windows' own media flyout leads with, as last read: what the
    /// now-playing island shows. Its details (title, artist, art) are for the
    /// caller to ask for, off the dock's thread.
    /// </summary>
    internal Session? Current => Volatile.Read(ref current);

    /// <summary>
    /// The session of an app: the one whose <c>SourceAppUserModelId</c> is the
    /// app's AUMID, or for a desktop app its exe's name (see <see cref="Matches"/>).
    /// A playing one first, when an app has several (a browser).
    /// </summary>
    public MediaControls? For(string? appUserModelId, string? exeName) => Pick(Sessions, appUserModelId, exeName);

    /// <summary>The session of an app among <paramref name="sessions"/>; see <see cref="For"/>.</summary>
    public static MediaControls? Pick(IReadOnlyList<MediaControls> sessions, string? appUserModelId, string? exeName)
    {
        MediaControls? found = null;
        foreach (var session in sessions)
        {
            if (!Matches(session.SourceAppId, appUserModelId, exeName))
            {
                continue;
            }
            if (session.Playing)
            {
                return session;
            }
            found ??= session;
        }
        return found;
    }

    /// <summary>
    /// Whether a session reported as <paramref name="source"/> belongs to an app
    /// with <paramref name="appUserModelId"/> or running <paramref name="exe"/>
    /// (a path or a bare name). Windows reports a packaged or AUMID-carrying app
    /// by its AUMID, and a desktop app without one by its exe's file name;
    /// both compared without regard to case.
    /// </summary>
    public static bool Matches(string? source, string? appUserModelId, string? exe)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }
        source = source.Trim();
        if (!string.IsNullOrWhiteSpace(appUserModelId) && string.Equals(source, appUserModelId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (string.IsNullOrWhiteSpace(exe))
        {
            return false;
        }
        var name = Path.GetFileName(exe.Trim());
        if (name.Length == 0)
        {
            return false;
        }
        // Some apps report a path rather than the bare name.
        return string.Equals(source, name, StringComparison.OrdinalIgnoreCase)
            || (source.EndsWith(name, StringComparison.OrdinalIgnoreCase) && source[^(name.Length + 1)] is '\\' or '/');
    }

    /// <summary>
    /// The session to lead with where Windows names none as current, which it may while sessions are
    /// still there (the current one's app just closed, or re-registered its session): a playing one,
    /// else the first. Windows' own flyout still shows them, so the now-playing faces do too, with
    /// their buttons, rather than "Nothing playing".
    /// </summary>
    internal static MediaControls? Lead(IReadOnlyList<MediaControls> sessions)
    {
        foreach (var session in sessions)
        {
            if (session.Playing)
            {
                return session;
            }
        }
        return sessions.Count > 0 ? sessions[0] : null;
    }

    private async Task Connect()
    {
        try
        {
            var found = await SessionManager.RequestAsync().AsTask().ConfigureAwait(false);
            lock (gate)
            {
                manager = found;
                manager.SessionsChanged += OnSessionsChanged;
                manager.CurrentSessionChanged += OnCurrentChanged;
            }
            Read();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // No media flyout on this system (Windows N without the media
            // pack, a server): no sessions, ever.
            Console.Error.WriteLine($"Karpo: media sessions unavailable: {error.GetType().Name} 0x{error.HResult:X8}");
        }
    }

    private void OnSessionsChanged(SessionManager sender, SessionsChangedEventArgs args) => Read();

    private void OnCurrentChanged(SessionManager sender, CurrentSessionChangedEventArgs args) => Read();

    private void OnPlaybackChanged(Session sender, PlaybackInfoChangedEventArgs args) => Read();

    private void OnMediaChanged(Session sender, MediaPropertiesChangedEventArgs args) => Read();

    // The timeline arrives after the track does: skipping a song raises the media properties
    // first and the new timeline a moment later. Without this the flyout read the gap between
    // them as "this session has no timeline" and never asked again, so the scrubber vanished
    // until it was reopened.
    private void OnTimelineChanged(Session sender, TimelinePropertiesChangedEventArgs args) => Read();

    /// <summary>Reads everything again, once for a burst of events: apps send several for one change.</summary>
    private void Read()
    {
        if (Interlocked.Exchange(ref reading, 1) == 1)
        {
            return;
        }
        _ = Task.Run(async () =>
        {
            await Task.Delay(60).ConfigureAwait(false);
            Interlocked.Exchange(ref reading, 0);
            try
            {
                ReadNow();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: media sessions not read: {error.GetType().Name} 0x{error.HResult:X8}");
            }
            Raise();
        });
    }

    private void ReadNow()
    {
        lock (gate)
        {
            if (manager is null)
            {
                return;
            }
            var all = manager.GetSessions();
            // Followed before they are read: a change between the read and the following would
            // otherwise raise no event, and what was read before it would stand. A session an app
            // registers anew (Spotify does, now and then) is a new object, followed from here on.
            Follow(all);
            var before = Volatile.Read(ref sessions);
            var snapshot = new List<MediaControls>(all.Count);
            foreach (var session in all)
            {
                var (described, status) = Describe(session);
                snapshot.Add(Settle(described, status, before));
            }
            Volatile.Write(ref current, manager.GetCurrentSession() ?? Lead(snapshot)?.Session);
            Volatile.Write(ref sessions, snapshot);
        }
    }

    /// <summary>
    /// What the window previews show of a session just read (<paramref name="read"/>, which says
    /// <paramref name="status"/>), settled against the last snapshot (<paramref name="before"/>) by
    /// the rule the now-playing faces read by (MediaGap): a player in the middle of a change of
    /// track says all its controls are off, which is a gap and not an answer, so the buttons shown
    /// for that app stay until it says something else — unless it closed or stopped.
    /// </summary>
    internal static MediaControls Settle(MediaControls read, PlaybackStatus status, IReadOnlyList<MediaControls> before)
    {
        // The same session, else the same app's: an app may register its session anew.
        MediaControls? shown = null;
        foreach (var old in before)
        {
            if (read.Session is not null && ReferenceEquals(old.Session, read.Session))
            {
                shown = old;
                break;
            }
            if (shown is null && string.Equals(old.SourceAppId, read.SourceAppId, StringComparison.OrdinalIgnoreCase))
            {
                shown = old;
            }
        }
        var buttons = MediaGap.Buttons(
            read.SourceAppId,
            status,
            new MediaButtons(read.CanPrevious, read.CanPlayPause, read.CanNext),
            shown?.SourceAppId,
            shown is null ? default : new MediaButtons(shown.CanPrevious, shown.CanPlayPause, shown.CanNext));
        return read with { CanPrevious = buttons.Previous, CanPlayPause = buttons.Toggle, CanNext = buttons.Next };
    }

    private static (MediaControls Read, PlaybackStatus Status) Describe(Session session)
    {
        string source = "";
        GlobalSystemMediaTransportControlsSessionPlaybackInfo? playback = null;
        try
        {
            source = session.SourceAppUserModelId ?? "";
            playback = session.GetPlaybackInfo();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A session whose app just went: listed, with nothing allowed.
        }
        var controls = playback?.Controls;
        var status = playback?.PlaybackStatus ?? PlaybackStatus.Closed;
        return (new MediaControls(
            source,
            status == PlaybackStatus.Playing,
            controls?.IsPreviousEnabled ?? false,
            controls?.IsPlayPauseToggleEnabled ?? false,
            controls?.IsNextEnabled ?? false)
        {
            Session = session,
        }, status);
    }

    /// <summary>Follows the events of exactly these sessions.</summary>
    private void Follow(IReadOnlyList<Session> now)
    {
        for (var index = followed.Count - 1; index >= 0; index--)
        {
            var old = followed[index];
            if (!Contains(now, old))
            {
                Try(() =>
                {
                    old.PlaybackInfoChanged -= OnPlaybackChanged;
                    old.MediaPropertiesChanged -= OnMediaChanged;
                    old.TimelinePropertiesChanged -= OnTimelineChanged;
                });
                followed.RemoveAt(index);
            }
        }
        foreach (var session in now)
        {
            if (!Contains(followed, session))
            {
                Try(() =>
                {
                    session.PlaybackInfoChanged += OnPlaybackChanged;
                    session.MediaPropertiesChanged += OnMediaChanged;
                    session.TimelinePropertiesChanged += OnTimelineChanged;
                });
                followed.Add(session);
            }
        }
    }

    private static bool Contains(IReadOnlyList<Session> list, Session session)
    {
        foreach (var item in list)
        {
            if (ReferenceEquals(item, session))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Changed, on the dock thread, at most one waiting at a time.</summary>
    private void Raise()
    {
        if (dock is null)
        {
            Invoke();
            return;
        }
        if (Interlocked.Exchange(ref raising, 1) == 1)
        {
            return;
        }
        bool queued;
        try
        {
            queued = dock.TryEnqueue(() =>
            {
                Interlocked.Exchange(ref raising, 0);
                Invoke();
            });
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            queued = false;
        }
        if (!queued)
        {
            // The dock's thread is gone: nobody is left to tell.
            Interlocked.Exchange(ref raising, 0);
        }
    }

    // A handler's failure must not reach the queue that called it.
    private void Invoke()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: media change not handled: {error}");
        }
    }

    private static void Try(Action action)
    {
        try
        {
            action();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
        }
    }

}
