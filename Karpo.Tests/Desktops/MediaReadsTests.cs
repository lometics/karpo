// Why Spotify's media buttons went missing (backlog 22), and the rules that keep them: a change that
// comes while a read runs is read again (Rereads), a change of track half done is a gap and not an
// answer (MediaGap), and where Windows names no current session one of those it lists leads
// (MediaSessions.Lead). The sessions here are fakes: nothing of this PC's media is read or touched.

using Karpo.Dock;
using PlaybackStatus = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;

namespace Karpo.Tests.DesktopsArea;

public sealed class MediaReadsTests
{
    private const string Spotify = "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify";

    private static readonly MediaButtons All = new(true, true, true);
    private static readonly MediaButtons None = default;

    private static SessionRead Track(string title, MediaButtons buttons, PlaybackStatus status = PlaybackStatus.Playing, string app = Spotify) =>
        new(app, title, title.Length > 0 ? "Gareth Emery" : "", "", status, buttons);

    /// <summary>
    /// A player as GSMTC shows it to Karpo: what a read finds now, and a read that can be held in the
    /// middle — between taking the controls and handing the state over, where the flyout's art decode
    /// sits — so an event can land in it, as Spotify's do.
    /// </summary>
    private sealed class FakeSession(SessionRead state)
    {
        private TaskCompletionSource? held;

        public SessionRead State { get; set; } = state;

        /// <summary>The next read stops after it has read, until <see cref="Release"/>.</summary>
        public TaskCompletionSource Hold()
        {
            held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return held;
        }

        public async Task<SessionRead> Read()
        {
            var read = State;
            if (held is { } wait)
            {
                held = null;
                Reading.TrySetResult();
                await wait.Task.ConfigureAwait(false);
            }
            return read;
        }

        /// <summary>Set once a held read has read.</summary>
        public TaskCompletionSource Reading { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// A now-playing face as the island and the flyout are one: every change asks Rereads for a read,
    /// and each read is settled against what was shown by MediaGap. What it showed, read by read.
    /// </summary>
    private sealed class Face
    {
        private readonly FakeSession session;
        private readonly Rereads rereads;
        private readonly Lock gate = new();

        public Face(FakeSession session)
        {
            this.session = session;
            rereads = new Rereads(ReadOnce, waitMs: 0);
        }

        public (string App, string Title, MediaButtons Buttons)? Shown { get; private set; }

        public List<(string Title, MediaButtons Buttons)?> History { get; } = [];

        public int Reads { get; private set; }

        public void Changed() => rereads.Ask();

        private async Task ReadOnce()
        {
            var read = await session.Read().ConfigureAwait(false);
            lock (gate)
            {
                Reads++;
                var before = Shown;
                Shown = MediaGap.Judge(read, before?.App) switch
                {
                    MediaVerdict.Nothing => null,
                    MediaVerdict.Hold => before,
                    _ => (read.App, read.Title, MediaGap.Buttons(read, before?.App, before?.Buttons ?? default)),
                };
                History.Add(Shown is { } now ? (now.Title, now.Buttons) : null);
            }
        }

        /// <summary>Waits for the reads asked for to be done.</summary>
        public void Settle(int reads)
        {
            Assert.True(SpinWait.SpinUntil(() => { lock (gate) { return Reads >= reads; } }, TimeSpan.FromSeconds(10)), $"only {Reads} of {reads} reads");
            // And nothing more after them.
            Thread.Sleep(50);
        }
    }

    /// <summary>
    /// Regression: Spotify skipping a track. Its words go, its controls go off while it is
    /// "changing", the next track's words come with the controls still off, and its controls come
    /// last — while the read of the words is still running. The buttons stay through all of it, and
    /// end as the app allows them; the change that landed in a read is read again rather than lost.
    /// </summary>
    [Fact]
    public async Task SkippingATrackNeverTakesTheButtonsAway()
    {
        var session = new FakeSession(Track("Carry On", All));
        var face = new Face(session);
        face.Changed();
        face.Settle(1);
        Assert.Equal(("Carry On", All), face.Shown is { } a ? (a.Title, a.Buttons) : default);

        // The words cleared, the controls all off, changing: the track shown stays, buttons and all.
        session.State = Track("", None, PlaybackStatus.Changing);
        face.Changed();
        face.Settle(2);
        Assert.Equal(("Carry On", All), (face.Shown!.Value.Title, face.Shown.Value.Buttons));

        // The next track's words, the controls still off: the words, and the buttons that were.
        session.State = Track("Saltwater", None, PlaybackStatus.Changing);
        var held = session.Hold();
        face.Changed();
        await session.Reading.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // While that read runs — it has already read the controls off — the controls arrive: next
        // allowed, previous not (the first track of a list), play or pause allowed.
        var allowed = new MediaButtons(false, true, true);
        session.State = Track("Saltwater", allowed);
        face.Changed();
        held.SetResult();
        face.Settle(4);

        Assert.Equal(("Saltwater", allowed), (face.Shown!.Value.Title, face.Shown.Value.Buttons));
        // At no read were the buttons gone, nor the face empty.
        Assert.All(face.History, shown =>
        {
            Assert.NotNull(shown);
            Assert.True(shown.Value.Buttons.Toggle, "play or pause went missing");
        });
    }

    /// <summary>
    /// What the flyout did before: a change that came while a read ran was dropped, "the read on its
    /// way will see it". The read had already read the controls, so the face kept them off.
    /// </summary>
    [Fact]
    public async Task AChangeDuringAReadIsReadAgain()
    {
        var reads = 0;
        var running = 0;
        var most = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rereads = new Rereads(async () =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref most, now);
            if (Interlocked.Increment(ref reads) == 1)
            {
                inside.TrySetResult();
                await gate.Task.ConfigureAwait(false);
            }
            Interlocked.Decrement(ref running);
        }, waitMs: 0);

        rereads.Ask();
        await inside.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // Three events during the first read: one more read for all of them, after it.
        rereads.Ask();
        rereads.Ask();
        rereads.Ask();
        Assert.Equal(1, Volatile.Read(ref reads));
        gate.SetResult();
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref reads) == 2 && Volatile.Read(ref running) == 0, TimeSpan.FromSeconds(10)));
        Thread.Sleep(50);
        Assert.Equal(2, Volatile.Read(ref reads));
        Assert.Equal(1, Volatile.Read(ref most));

        // Idle again: the next ask reads again.
        rereads.Ask();
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref reads) == 3, TimeSpan.FromSeconds(10)));
    }

    /// <summary>A burst of asks before the read begins is one read.</summary>
    [Fact]
    public void ABurstIsOneRead()
    {
        var reads = 0;
        var rereads = new Rereads(() =>
        {
            Interlocked.Increment(ref reads);
            return Task.CompletedTask;
        }, waitMs: 100);
        for (var ask = 0; ask < 20; ask++)
        {
            rereads.Ask();
        }
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref reads) >= 1, TimeSpan.FromSeconds(10)));
        Thread.Sleep(250);
        Assert.Equal(1, Volatile.Read(ref reads));
    }

    /// <summary>A read that throws ends its turn and leaves the next ask to read again.</summary>
    [Fact]
    public void AFailedReadDoesNotStopTheNext()
    {
        var reads = 0;
        var rereads = new Rereads(() =>
        {
            if (Interlocked.Increment(ref reads) == 1)
            {
                throw new InvalidOperationException("the app went away");
            }
            return Task.CompletedTask;
        }, waitMs: 0);
        rereads.Ask();
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref reads) == 1, TimeSpan.FromSeconds(10)));
        Thread.Sleep(50);
        rereads.Ask();
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref reads) == 2, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void ASessionWithWordsIsShown()
    {
        Assert.Equal(MediaVerdict.Show, MediaGap.Judge(Track("Carry On", All), null));
        Assert.Equal(MediaVerdict.Show, MediaGap.Judge(Track("Carry On", None, PlaybackStatus.Stopped), Spotify));
    }

    /// <summary>
    /// Without words: gone where it is closed or stopped; held where it is the app shown (a change
    /// half done, or the app registering its session anew); and another app shown by its name with
    /// its buttons — a player started with nothing loaded is still there to press play on.
    /// </summary>
    [Fact]
    public void ASessionWithoutWordsIsAGapOrTheApp()
    {
        Assert.Equal(MediaVerdict.Nothing, MediaGap.Judge(Track("", All, PlaybackStatus.Closed), Spotify));
        Assert.Equal(MediaVerdict.Nothing, MediaGap.Judge(Track("", All, PlaybackStatus.Stopped), null));
        Assert.Equal(MediaVerdict.Hold, MediaGap.Judge(Track("", None, PlaybackStatus.Changing), Spotify));
        Assert.Equal(MediaVerdict.Hold, MediaGap.Judge(Track("", All, PlaybackStatus.Paused), Spotify.ToUpperInvariant()));
        Assert.Equal(MediaVerdict.Show, MediaGap.Judge(Track("", All, PlaybackStatus.Paused), "Brave"));
        Assert.Equal(MediaVerdict.Show, MediaGap.Judge(Track("", new MediaButtons(false, true, false), PlaybackStatus.Opened), null));
    }

    /// <summary>
    /// All three off for the app whose buttons show is a change under way: they stay. Anything the
    /// app does allow is what it allows — an advert that forbids skipping shows next off — and
    /// another app's are its own.
    /// </summary>
    [Fact]
    public void TheButtonsReflectTheAppExceptInAGap()
    {
        Assert.Equal(All, MediaGap.Buttons(Track("Saltwater", None, PlaybackStatus.Changing), Spotify, All));
        Assert.Equal(All, MediaGap.Buttons(Track("Saltwater", None), Spotify, All));
        var advert = new MediaButtons(false, true, false);
        Assert.Equal(advert, MediaGap.Buttons(Track("Advertisement", advert), Spotify, All));
        Assert.Equal(None, MediaGap.Buttons(Track("WARDOGS on Steam", None, app: "Brave"), Spotify, All));
        Assert.Equal(None, MediaGap.Buttons(Track("Saltwater", None), null, default));
    }

    /// <summary>
    /// Regression: a session that closed or stopped is not changing tracks, so the buttons held
    /// through a gap go with it — whatever was shown before, dead buttons never stay.
    /// </summary>
    [Fact]
    public void AClosedOrStoppedSessionDropsItsHeldButtons()
    {
        Assert.Equal(None, MediaGap.Buttons(Track("Saltwater", None, PlaybackStatus.Stopped), Spotify, All));
        Assert.Equal(None, MediaGap.Buttons(Track("Saltwater", None, PlaybackStatus.Closed), Spotify, All));
        Assert.Equal(None, MediaGap.Buttons(Spotify, PlaybackStatus.Stopped, None, Spotify, All));
        Assert.Equal(All, MediaGap.Buttons(Spotify, PlaybackStatus.Paused, None, Spotify, All));
    }

    /// <summary>
    /// Regression: the window previews' buttons read the sessions raw, so a change of track blinked
    /// them off. Each snapshot is now settled against the last by the faces' own rule: all three off
    /// for the app shown is a gap (its buttons stay, the session read is the one they act on), the
    /// app's own answer otherwise, and a stopped or closed session's off buttons are off.
    /// </summary>
    [Fact]
    public void ThePreviewsButtonsHoldThroughAChangeOfTrack()
    {
        var shown = new MediaControls(Spotify, true, true, true, true);
        IReadOnlyList<MediaControls> before = [shown, new MediaControls("Brave", false, false, true, false)];

        var gap = MediaSessions.Settle(new MediaControls(Spotify, false, false, false, false), PlaybackStatus.Changing, before);
        Assert.Equal(new MediaControls(Spotify, false, true, true, true), gap);

        // The same app's session registered anew: still the app's buttons, by its id, case aside.
        var anew = MediaSessions.Settle(new MediaControls(Spotify.ToUpperInvariant(), true, false, false, false), PlaybackStatus.Playing, before);
        Assert.True(anew is { CanPrevious: true, CanPlayPause: true, CanNext: true });

        // What the app allows, once it says.
        var advert = MediaSessions.Settle(new MediaControls(Spotify, true, false, true, false), PlaybackStatus.Playing, before);
        Assert.Equal(new MediaControls(Spotify, true, false, true, false), advert);

        // Stopped or closed: off is off.
        foreach (var status in new[] { PlaybackStatus.Stopped, PlaybackStatus.Closed })
        {
            Assert.Equal(new MediaControls(Spotify, false, false, false, false),
                MediaSessions.Settle(new MediaControls(Spotify, false, false, false, false), status, before));
        }

        // An app never shown before has nothing to hold.
        Assert.Equal(new MediaControls("vlc.exe", false, false, false, false),
            MediaSessions.Settle(new MediaControls("vlc.exe", false, false, false, false), PlaybackStatus.Changing, before));
    }

    /// <summary>
    /// Windows names no current session now and then while sessions are listed (the current one's
    /// app went, or re-registered): a playing one leads, else the first, rather than nothing.
    /// </summary>
    [Fact]
    public void WithoutACurrentSessionAListedOneLeads()
    {
        var paused = new MediaControls("Brave", false, false, true, false);
        var playing = new MediaControls(Spotify, true, true, true, true);
        Assert.Same(playing, MediaSessions.Lead([paused, playing]));
        Assert.Same(paused, MediaSessions.Lead([paused, paused with { SourceAppId = "Spotify.exe" }]));
        Assert.Null(MediaSessions.Lead([]));
    }

    /// <summary>
    /// "Hide it while nothing plays" (§230): a paused player keeps its session, so a session is not
    /// something playing. What has not played since the face was made is nothing at once; a stop is
    /// nothing only after its grace, and playing again within it never was; a new stop starts the grace again.
    /// </summary>
    [Fact]
    public void NothingPlaysIsNoSessionOrAPauseOutlastingItsGrace()
    {
        var quiet = new MediaQuiet(TimeSpan.FromSeconds(5));
        Assert.True(quiet.Idle(0));
        // Paused from the start (Spotify left open): nothing plays, and no grace to wait out.
        Assert.False(quiet.Heard(false, 0));
        Assert.True(quiet.Idle(0));
        Assert.False(quiet.Heard(true, 1_000));
        Assert.False(quiet.Idle(1_000));
        Assert.False(quiet.Idle(60_000));
        // A stop starts the grace, once; a second read while still stopped does not move it on.
        Assert.True(quiet.Heard(false, 10_000));
        Assert.False(quiet.Heard(false, 12_000));
        Assert.False(quiet.Idle(14_999));
        Assert.True(quiet.Idle(15_000));
        // A change of track passes through "paused": playing again within the grace never left.
        Assert.False(quiet.Heard(true, 20_000));
        Assert.True(quiet.Heard(false, 30_000));
        Assert.False(quiet.Heard(true, 31_000));
        Assert.False(quiet.Idle(40_000));
        Assert.Equal(TimeSpan.FromSeconds(5), quiet.Wait);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while ((seen = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, seen) != seen)
        {
        }
    }
}
