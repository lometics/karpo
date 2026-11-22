// What is playing: the art, title and artist of the media session Windows
// shows in its own media flyout — or of the session the person picked — and,
// on a list's line, its controls: previous, play or pause and next, shuffle and
// repeat where the app has them, and the apps playing to pick from.
//
// It is a line of a list and nothing else on the bar (docs\widgets.md §96). The
// taskbar entry it was — the art and the words, the title over the artist (or the
// app) wherever the time-and-date face sets its time over its date, at its size
// and its lines' height (§497), and "title · artist" on one line
// where it is not (TwoLines) — is the media preset's now-playing face now, which
// this draws for it (PaintFace, NowPlayingFaceDrawing): the folder reads its
// first now-playing line, or a widget of its own where it holds none.
//
// Driven by the sessions' events, never by a timer. The one session manager
// is MediaSessions.Shared's, which the window previews use too; it says when
// anything changed, and the island then reads the followed session's details
// on the thread pool (all of it is async, and some of it crosses into the
// app's process) and hands them over; the dock is told with WM_KARPO_ISLAND
// and the next Refresh takes them. The art is the track's, not a face's: it
// is decoded once for every now-playing face (NowPlayingArt). The one tick is
// a list line's, while it shows the track's timeline and the track plays
// (MediaLine.cs).
//
// Which session it follows is its own: Windows' current one until the person
// picks another from the line's picker or its menu, and Windows' again when
// the picked app goes or is picked again. Never written down: a restart
// follows Windows' choice. With nothing playing the line stays, the same size,
// saying so quietly.

using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.Graphics.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;
using static TerraFX.Interop.Windows.Windows;
using System.Numerics;
using RepeatMode = Windows.Media.MediaPlaybackAutoRepeatMode;

namespace Karpo.Dock;

internal sealed partial class MediaIsland : CellIsland, IListContent
{
    // Segoe Fluent Icons: Previous, Play, Pause, Next, MusicNote where a session has no art,
    // Shuffle, RepeatAll, RepeatOne, and ChevronDown for the picker.
    private const string PreviousGlyph = "";
    private const string PlayGlyph = "";
    private const string PauseGlyph = "";
    private const string NextGlyph = "";
    private const string NoteGlyph = "";
    internal const string ShuffleGlyph = "";
    internal const string RepeatAllGlyph = "";
    internal const string RepeatOneGlyph = "";
    private const string ChevronGlyph = "";

    /// <summary>Art is decoded to at most this many px a side: enough for a 96-DIP cell at 200 %.</summary>
    private const uint ArtPx = 192;

    /// <summary>How long the art's decode may take before it is given up, and tried again on the next read.</summary>
    private static readonly TimeSpan ArtWait = TimeSpan.FromSeconds(10);

    private readonly CancellationTokenSource stop = new();
    private readonly Rereads rereads;
    private readonly MediaSessions sessions;
    private Playing? now;
    private IReadOnlyList<MediaEntry> playingIn = [];

    // The app the person picked to follow, the one pick every now-playing line, face and list of
    // the apps playing shares (MediaPick); its key is null while they follow whichever session
    // Windows leads with.
    private readonly MediaPick pick;

    /// <summary>Whether this is the list of the apps playing (ListKind.MediaSources) rather than the now-playing line.</summary>
    internal bool Sources { get; }

    // The last state a read made, on the pool: what the next read's gap is judged against
    // (MediaGap). Only the one read at a time that Rereads runs touches it.
    private Playing? last;

    // Handed over by the pool thread, taken by Refresh.
    private Box? arrived;

    // Whether nothing plays, as the face's hiding asks it: a stop counts only after its grace.
    private readonly MediaQuiet quiet;

    /// <summary>
    /// Tests only, on the thread that makes the island: what the widget shows, instead of whatever
    /// this PC happens to be playing. A picture of the widget is then the same on every machine,
    /// and no test has to start a player. Its buttons reach no app: there is no session behind it,
    /// and shuffle, repeat and the picker change the stand-in's own state.
    /// </summary>
    [ThreadStatic]
    internal static MediaStand? Stand;

    private readonly MediaStand? stand = Stand;

    public MediaIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Sources = ListWidgets.Find(island.Info?.Id)?.Kind == ListKind.MediaSources;
        // A stand-in never touches the dock's pick: the tests' picks are their own.
        pick = stand is { } standing ? standing.Pick ?? new MediaPick() : MediaPick.Shared;
        Configure(island, config);
        // Made on the dock's thread, which is where the shared sessions have
        // to be made first: they raise Changed on it.
        if (stand is { } made)
        {
            // No sessions named: the track's own app is the one playing, as on a PC.
            (now, playingIn) = (Of(made, pick.Key), made.Sessions ?? (made.Nothing ? [] : [new MediaEntry(made.AppName, made.AppName, made.IsPlaying)]));
        }
        quiet = new MediaQuiet(stand?.QuietGrace ?? MediaQuiet.Grace);
        Heard();
        rereads = new Rereads(ReadOnce);
        sessions = MediaSessions.Shared;
        sessions.Changed += Changed;
        pick.Changed += Repick;
        Changed();
    }

    private enum Cell
    {
        Info,
        Previous,
        PlayPause,
        Next,
        Shuffle,
        Repeat,
        Picker,
    }

    /// <summary>
    /// Events only, but for a list's line that shows the track's timeline while it plays: the open
    /// folder draws it again each second so its time moves (MediaLine.cs). A paused track's time
    /// stands still, and the bar's face shows none.
    /// </summary>
    public override int IntervalMs => timelineShown && now is { IsPlaying: true } ? 1000 : 0;

    /// <summary>One cell: the line, whatever plays.</summary>
    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        Forget();
    }

    /// <summary>
    /// Nothing plays: what the face's hiding asks (NowPlayingFace.HidesIdle) — no session, or one
    /// paused, stopped or closed for longer than its grace (MediaQuiet). A paused player keeps its
    /// session for as long as the app runs, so a session alone was always "playing" and the folder
    /// never left the bar.
    /// </summary>
    internal bool Idle => quiet.Idle(Environment.TickCount64);

    /// <summary>The now-playing line while nothing plays (§327), as its face (§319); the apps playing are always there.</summary>
    internal override bool NothingToShow => !Sources && Idle;

    /// <summary>
    /// What it shows now goes to the quiet: where a track has just stopped playing, the folder is
    /// woken once its grace is over, so the bar is laid out again without it then (FolderIsland.Count).
    /// </summary>
    private void Heard()
    {
        if (quiet.Heard(now is { IsPlaying: true }, Environment.TickCount64))
        {
            var token = stop.Token;
            _ = Task.Delay(quiet.Wait + TimeSpan.FromMilliseconds(50), token).ContinueWith(
                _ => Troubles.Guard("Media: quiet after a track stopped", () => Wake()), // not-ui: a trouble's name
                token,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
        }
    }

    /// <summary>Tests only: <paramref name="next"/> arrives as a read would, taken at the next Refresh.</summary>
    internal void Hear(MediaStand next)
    {
        Interlocked.Exchange(ref arrived, new Box(Of(next, pick.Key), next.Sessions ?? (next.Nothing ? [] : [new MediaEntry(next.AppName, next.AppName, next.IsPlaying)])));
        _ = Wake();
    }

    public override string? Tooltip(int index)
    {
        if (Sources)
        {
            return SourceTip(index);
        }
        if (now is not { } playing)
        {
            return (Cell)index == Cell.Info ? S.WidgetMediaNothingPlaying : null;
        }
        return (Cell)index switch
        {
            Cell.Previous => S.WidgetMediaPrevious,
            Cell.PlayPause => playing.IsPlaying ? S.WidgetMediaPause : S.WidgetMediaPlay,
            Cell.Next => S.WidgetMediaNext,
            Cell.Shuffle => S.WidgetMediaShuffle,
            Cell.Repeat => MediaModes.Say(playing.Repeat),
            Cell.Picker => S.WidgetMediaPlayingIn,
            _ => Describe(playing),
        };
    }

    public override IslandAction Click(int index)
    {
        if (linePress.Up())
        {
            _ = Wake();
        }
        if (Sources)
        {
            PickSource(index);
            return default;
        }
        // The picker: the line's own menu, the apps playing first (Dock.WidgetMenus.cs), at the name.
        if ((Cell)index == Cell.Picker)
        {
            return playingIn.Count > 1 ? new IslandAction(View: IslandView.Menu) : default;
        }
        if (now is not { } playing || (Cell)index == Cell.Info)
        {
            // A line does nothing on a click beside its controls: the app comes forward from its menu.
            return default;
        }
        switch ((Cell)index)
        {
            case Cell.Shuffle:
                ToggleShuffle();
                return default;
            case Cell.Repeat:
                CycleRepeat();
                return default;
        }
        if (playing.Session is not { } session)
        {
            // A stand-in for the tests: there is no app to tell.
            return default;
        }
        try
        {
            switch ((Cell)index)
            {
                case Cell.Previous when playing.CanPrevious:
                    _ = session.TrySkipPreviousAsync();
                    break;
                case Cell.PlayPause when playing.CanToggle:
                    _ = session.TryTogglePlayPauseAsync();
                    break;
                case Cell.Next when playing.CanNext:
                    _ = session.TrySkipNextAsync();
                    break;
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // The app went away between the event and the click.
            Console.Error.WriteLine($"Karpo: media command failed: {error.GetType().Name}");
        }
        return default;
    }

    public override void Dispose()
    {
        stop.Cancel();
        sessions.Changed -= Changed;
        pick.Changed -= Repick;
        base.Dispose();
    }

    // --- Its menu (folders, Dock.WidgetMenus.cs) ------------------------------------------------

    /// <summary>
    /// What its menu can do now, as the card's own buttons answer it: whether something plays, which
    /// of previous, play or pause and next would act, and the app playing, which can be brought
    /// forward where there is a session to name it. Nothing at all while nothing plays.
    /// </summary>
    internal (bool Playing, bool CanPrevious, bool CanToggle, bool CanNext, string App, bool CanOpen) Controls =>
        now is { } playing
            ? (playing.IsPlaying, playing.CanPrevious, playing.CanToggle, playing.CanNext, playing.AppName,
                playing.Session is not null && playing.App.Length > 0)
            : default;

    /// <summary>
    /// Shuffle and repeat as the app has them: whether it lets them be set at all — a line and a
    /// menu offer neither where it does not — and how they are set now.
    /// </summary>
    internal (bool CanShuffle, bool Shuffle, bool CanRepeat, MediaRepeat Repeat) Modes =>
        now is { } playing ? (playing.CanShuffle, playing.Shuffle, playing.CanRepeat, playing.Repeat) : default;

    /// <summary>The apps playing, each by its name and icon, in Windows' order; the picker's rows where there are two or more.</summary>
    internal IReadOnlyList<MediaEntry> PlayingIn => playingIn;

    /// <summary>The app whose session it follows: the one picked, else the one Windows leads with; null with none.</summary>
    internal string? Follows => pick.Key ?? (now is { App.Length: > 0 } playing ? playing.App : null);

    /// <summary>Whether the person picked the app it follows, rather than it following Windows.</summary>
    internal bool Picked => pick.Key is not null;

    /// <summary>
    /// Follows <paramref name="key"/>'s session from now on, as a row of the picker does — this and
    /// every other now-playing line and face (MediaPick); the app already followed by the person's
    /// pick, picked again, goes back to Windows' own choice, as the media flyout's list did. Shown at
    /// once; the track follows when the read comes back.
    /// </summary>
    internal void Follow(string? key) =>
        pick.Set(key is { Length: > 0 } && !string.Equals(key, pick.Key, StringComparison.OrdinalIgnoreCase) ? key : null);

    /// <summary>The pick changed, here or on another island sharing it: what it follows is read again.</summary>
    private void Repick()
    {
        if (stand is { } made)
        {
            now = Of(made, pick.Key);
            Heard();
            _ = Wake();
            return;
        }
        Changed();
    }

    /// <summary>
    /// Shuffle on or off, as the line's button and its menu do, where the app allows it: asked of the
    /// app off the dock's thread — the call reaches its process, and an app that hangs must not hang
    /// the bar — and shown at once, until the app says what it did.
    /// </summary>
    internal void ToggleShuffle()
    {
        if (now is not { CanShuffle: true } playing)
        {
            return;
        }
        var on = !playing.Shuffle;
        now = playing with { Shuffle = on };
        Ask(playing.Session, session => session.TryChangeShuffleActiveAsync(on));
        _ = Wake();
    }

    /// <summary>Repeat on to its next mode — off, this track, all, and round again — as shuffle is set.</summary>
    internal void CycleRepeat()
    {
        if (now is not { CanRepeat: true } playing)
        {
            return;
        }
        var next = MediaModes.Next(playing.Repeat);
        now = playing with { Repeat = next };
        Ask(playing.Session, session => session.TryChangeAutoRepeatModeAsync(next switch
        {
            MediaRepeat.Track => RepeatMode.Track,
            MediaRepeat.List => RepeatMode.List,
            _ => RepeatMode.None,
        }));
        _ = Wake();
    }

    /// <summary>
    /// <paramref name="command"/> on <paramref name="session"/>, off the dock's thread; nothing for a
    /// stand-in, which has none.
    /// </summary>
    private void Ask(GlobalSystemMediaTransportControlsSession? session, Func<GlobalSystemMediaTransportControlsSession, Windows.Foundation.IAsyncOperation<bool>> command)
    {
        if (session is null)
        {
            return;
        }
        var token = stop.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                _ = await command(session).AsTask(token).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: media command failed: {error.GetType().Name}");
            }
        });
    }

    /// <summary>One of the card's three buttons, pressed from its menu: exactly what a click on it does.</summary>
    internal void Press(WidgetAct button) => _ = Click((int)(button switch
    {
        WidgetAct.Previous => Cell.Previous,
        WidgetAct.Next => Cell.Next,
        _ => Cell.PlayPause,
    }));

    /// <summary>The playing app brought forward from its menu, or its Start entry opened through <paramref name="open"/>.</summary>
    internal void OpenPlayer(Opener open)
    {
        if (now is { Session: not null, App.Length: > 0 } playing)
        {
            Bring(playing.App, open);
        }
    }

    protected override bool Sample(bool everything)
    {
        // A stand-in's arrives only by a test's hand (Hear): it is what is playing, whatever the PC says.
        if (Interlocked.Exchange(ref arrived, null) is { } box)
        {
            (now, playingIn) = (box.State, box.Sessions);
            Heard();
        }
        return false;
    }

    /// <summary>
    /// What a stand-in plays: its track, or nothing at all where it says nothing plays — by the app
    /// <paramref name="picked"/> where it is one of the stand-in's sessions, as a pick in the picker
    /// shows it.
    /// </summary>
    private static Playing? Of(MediaStand stand, string? picked)
    {
        if (stand.Nothing)
        {
            return null;
        }
        var app = stand.AppName;
        var key = stand.AppName;
        foreach (var entry in stand.Sessions ?? [])
        {
            if (string.Equals(entry.Key, picked, StringComparison.OrdinalIgnoreCase))
            {
                (app, key) = (entry.App, entry.Key);
            }
            else if (picked is null && key == stand.AppName && entry.App == stand.AppName)
            {
                key = entry.Key;
            }
        }
        var source = new MediaSource(stand.Site is { } site ? NowPlayingSource.Say(site) : app, stand.SourceIcon, app, stand.Site);
        return new(
            null,
            stand.Title,
            NowPlayingSource.Artist(stand.Artist, source),
            key,
            app,
            stand.IsPlaying,
            stand.CanPrevious,
            stand.CanToggle,
            stand.CanNext,
            stand.Art,
            Position: stand.Position,
            Duration: stand.Duration,
            CanSeek: stand.CanSeek,
            Source: source,
            CanShuffle: stand.CanShuffle,
            Shuffle: stand.Shuffle,
            CanRepeat: stand.CanRepeat,
            Repeat: stand.Repeat);
    }

    // --- The face (NowPlayingFaceDrawing) ----------------------------------------------------------
    // What the now-playing entry drew on the bar, drawn for the media preset's face. Its size is
    // the face's Measure, a function of the face's settings and the bar's numbers alone: there is no
    // Length here to read what is playing (docs\widgets.md §4).

    /// <summary>
    /// How much of the line the title keeps where the title and the artist do not both fit: three
    /// fifths, so the artist is never cut away entirely, as it could not be when it had a line of
    /// its own. A title shorter than that leaves the artist the difference.
    /// </summary>
    private const float TitleShare = 0.6f;

    /// <summary>
    /// §497 (the person, 2026-11-03: "the now playing widget in the taskbar widgets should also be two rows like the
    /// default time and date taskbar folder face"): the title over the artist wherever the time-and-date face sets its
    /// time over its date, at its text size and its lines' height (<see cref="DateTimeFaceDrawing.TwoLines"/>) — one rule
    /// for both, so the two faces side by side are one pair of lines; null where the bar is too thin for two at any of
    /// the text's sizes, which keeps one line, and down a vertical bar, which shows the art alone. Measured on a
    /// template, never on the track, so the lines cannot come and go when the track changes.
    /// </summary>
    internal static (float Px, float Line)? TwoLines(Gfx gfx, in IslandStyle style) => DateTimeFaceDrawing.TwoLines(gfx, style);

    /// <summary>
    /// The face's length along the bar, its ends aside (the folder keeps those): across a bar the
    /// <paramref name="width"/> it is set to (<see cref="NowPlayingFace.Dips"/>, §503), times the scale and
    /// the text's, so a larger text keeps the same characters — the art, where it is shown, and the words in
    /// it, the words cut past their room (<see cref="PaintFace"/>), whatever plays: the bar never moves for a
    /// track. Never shorter than the art. Down a bar on its side the art alone, a square across the bar: a
    /// title is three or four letters across a side bar at any thickness, and text is never turned
    /// (docs\widgets.md §163), so the words are the tooltip's and the flyout's there.
    /// </summary>
    internal static float FaceLength(in IslandStyle style, string width)
    {
        var fit = new Fit(style);
        return style.Vertical
            ? ArtSize(style)
            : MathF.Max(ArtSize(style), TextLine.Round(NowPlayingFace.Dips(width) * fit.Scale * fit.TextScale));
    }

    /// <summary>The art and the gap before it along a horizontal bar, or nothing where it is not shown.</summary>
    private static float ArtWidth(in IslandStyle style, in Fit fit, bool art) =>
        art || style.Vertical ? ArtSize(style) + fit.Gap : 0;

    /// <summary>What the face shows now, to tell when it must be painted again: the track, the art, whether it plays, the app.</summary>
    internal string FaceReading =>
        now is { } playing
            ? $"{playing.Title}|{playing.Artist}|{playing.IsPlaying}|{playing.Art is not null}|{playing.App}"
            : "-";

    /// <summary>What the face says to a screen reader: the track and where it plays, or that nothing plays.</summary>
    internal string FaceTip => now is { } playing ? Describe(playing) : S.WidgetMediaNothingPlaying;

    /// <summary>A line of a list is never a widget of a bar (<see cref="ListWidgets"/>): the bar shows the preset's face.</summary>
    internal override unsafe void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    /// <summary>
    /// The now-playing face in <paramref name="box"/>, its length along the bar (<see cref="FaceLength"/>)
    /// and the cell across it, as the entry drew itself: the art — the note where there is none,
    /// a pause mark on it while the track is paused — on the box's trailing end and the words from
    /// its leading one along a horizontal bar, the art alone down one on its side. Drawn where the box is, rather than
    /// moved there, so what watches the drawing (PaintWatch) sees where each piece lands.
    /// </summary>
    internal unsafe void PaintFace(ID2D1DeviceContext* dc, in IslandStyle style, D2D_RECT_F box, bool art)
    {
        if (now is { } playing)
        {
            PaintPlaying(dc, style, box, art, playing);
        }
        else
        {
            PaintIdle(dc, style, box, art);
        }
    }

    private unsafe void PaintPlaying(ID2D1DeviceContext* dc, in IslandStyle style, D2D_RECT_F box, bool showArt, Playing playing)
    {
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        var (width, height) = (box.right - box.left, box.bottom - box.top);
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        dc->PushAxisAlignedClip(&box, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        ID2D1SolidColorBrush* brush = null;
        TextLine title = default;
        TextLine artist = default;
        TextLine glyph = default;
        try
        {
            brush = Gfx.Brush(dc, ink.Text);
            // The title in the quiet ink while the track is paused, where no art carries the mark.
            var titleInk = playing.IsPlaying || showArt || style.Vertical ? ink.Text : ink.Secondary;
            // Across the bar the words start on the face's leading end and the art holds its trailing
            // one, the words cut where they reach it (2026-10-11: "align the image to the right side
            // of the widget and the text on the left ... right now they are centered").
            var x = box.left;
            var artRoom = ArtWidth(style, fit, showArt);
            if (showArt || style.Vertical)
            {
                var size = ArtSize(style);
                var top = box.top + TextLine.Round((height - size) / 2);
                var left = style.Vertical ? box.left + TextLine.Round((width - size) / 2) : box.right - size;
                var square = new D2D_RECT_F(left, top, left + size, top + size);
                PaintWatch.Shape(square);
                if (playing.Art is { } pixels)
                {
                    // The art, cropped to a square like a cover, in a rounded rectangle.
                    PopupPaint.Covered(dc, pixels, square, MathF.Min(fit.Px(4), size / 4));
                }
                else
                {
                    glyph = TextLine.Lay(Gfx, NoteGlyph, fit.GlyphFor(size * 0.6f), Weight.Regular, null, Gfx.IconFamily);
                    glyph.Draw(dc, brush, ink.Secondary, left + (size - glyph.Width) / 2, top + (size - glyph.Height) / 2);
                }
                if (!playing.IsPlaying)
                {
                    PauseMark(dc, square, fit);
                }
            }
            if (style.Vertical)
            {
                return;
            }
            var widest = box.right - x - artRoom;
            if (widest < LeastWords(fit))
            {
                return;
            }
            if (TwoLines(Gfx, style) is var (px, pitch))
            {
                // §497: the title over the artist (or the app), each cut to the room on its own line, as the
                // time-and-date face sets its time over its date: its size, its lines' height, its ink, the two
                // as one block in the middle of the bar; a lone title on the bar's middle.
                var stacked = Stacked(px, playing, widest);
                title = TextLine.Lay(Gfx, stacked.Title, px, Weight.Regular, MathF.Max(1, stacked.TitleRoom));
                if (stacked.Artist is { } name && stacked.ArtistRoom >= LeastWords(fit))
                {
                    artist = TextLine.Lay(Gfx, name, px, Weight.Regular, stacked.ArtistRoom);
                }
                var lines = artist.IsEmpty ? pitch : 2 * pitch;
                var top = TextLine.Round(box.top + ((height - lines) / 2));
                title.Draw(dc, brush, titleInk, x, top);
                if (!artist.IsEmpty)
                {
                    artist.Draw(dc, brush, ink.Text, x, top + pitch);
                }
                return;
            }
            // One line where two do not fit: the title, and the artist after it in the quiet ink.
            // The cell never grows for them; they are cut where they do not fit.
            var words = Words(fit, playing, widest);
            title = TextLine.Lay(Gfx, words.Title, fit.Primary, Weight.Regular, MathF.Max(1, words.TitleRoom));
            var line = box.top + TextLine.Round((height - title.Height) / 2);
            title.Draw(dc, brush, titleInk, x, line);
            if (words.Artist is { } rest && words.ArtistRoom >= LeastWords(fit))
            {
                artist = TextLine.Lay(Gfx, rest, fit.Primary, Weight.Regular, words.ArtistRoom);
                artist.Draw(dc, brush, ink.Secondary, x + title.Width, line);
            }
        }
        finally
        {
            glyph.Release();
            artist.Release();
            title.Release();
            ComUtil.Release(ref brush);
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>
    /// The paused track's mark: the pause glyph on a dark plate in the art's lower corner, as a
    /// player's thumbnail says it, so a glance tells a paused track from a playing one. Nothing
    /// while it plays, which is what a now-playing face is for.
    /// </summary>
    private unsafe void PauseMark(ID2D1DeviceContext* dc, D2D_RECT_F art, in Fit fit)
    {
        var side = art.right - art.left;
        var plate = MathF.Max(fit.Px(10), TextLine.Round(side * 0.5f));
        var box = new D2D_RECT_F(art.right - plate, art.bottom - plate, art.right, art.bottom);
        PopupPaint.Fill(dc, box, MathF.Min(fit.Px(4), plate / 4), Gfx.Ink(0, 0, 0, 0.62f));
        PopupPaint.Glyph(dc, Gfx, PauseGlyph, TextLine.Round(plate * 0.62f), Gfx.Ink(1, 1, 1, 1), box);
    }

    /// <summary>
    /// Nothing is playing: the note where the art goes and "Nothing playing" where the track goes,
    /// placed as a track's are, so only the words change when one starts.
    /// </summary>
    private unsafe void PaintIdle(ID2D1DeviceContext* dc, in IslandStyle style, D2D_RECT_F box, bool showArt)
    {
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        var (width, height) = (box.right - box.left, box.bottom - box.top);
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        ID2D1SolidColorBrush* brush = null;
        TextLine glyph = default;
        TextLine words = default;
        try
        {
            brush = Gfx.Brush(dc, ink.Disabled);
            var x = box.left;
            var art = ArtWidth(style, fit, showArt);
            if (showArt || style.Vertical)
            {
                // On the trailing end, where a track's art is.
                var size = ArtSize(style);
                var top = box.top + TextLine.Round((height - size) / 2);
                var left = style.Vertical ? box.left + TextLine.Round((width - size) / 2) : box.right - size;
                PaintWatch.Shape(new D2D_RECT_F(left, top, left + size, top + size));
                glyph = TextLine.Lay(Gfx, NoteGlyph, fit.GlyphFor(size * 0.6f), Weight.Regular, null, Gfx.IconFamily);
                glyph.Draw(dc, brush, ink.Secondary, left + (size - glyph.Width) / 2, top + (size - glyph.Height) / 2);
            }
            if (style.Vertical)
            {
                return;
            }
            // Where a track's words would be, in the same room, so nothing but the words changes
            // when one starts.
            var room = box.right - x - art;
            if (room < LeastWords(fit))
            {
                return;
            }
            words = TextLine.Lay(Gfx, S.WidgetMediaNothingPlaying, fit.Primary, Weight.Regular, room);
            words.Draw(dc, brush, ink.Secondary, x, box.top + TextLine.Round((height - words.Height) / 2));
        }
        finally
        {
            words.Release();
            glyph.Release();
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>
    /// The one line a horizontal bar too thin for two (<see cref="TwoLines"/>) shows, in
    /// <paramref name="room"/> px: the title, and after it " · " and the artist where there is one,
    /// in the quiet ink the artist's second line wears. Where both do not fit, the title keeps at
    /// most <see cref="TitleShare"/> of the room and the artist the rest, each cut with an ellipsis,
    /// so the line says whose the track is as well as what it is — which the two lines do. Titles are
    /// content, so they are measured, not templated.
    /// </summary>
    private (string Title, float TitleRoom, string? Artist, float ArtistRoom) Words(in Fit fit, Playing playing, float room)
    {
        var title = playing.Title.Length > 0 ? playing.Title : playing.AppName;
        var titleWidth = TextLine.Measure(Gfx, title, fit.Primary, Weight.Regular, null);
        if (playing.Artist.Length == 0)
        {
            return (title, MathF.Min(titleWidth, room), null, 0);
        }
        var artist = $" · {playing.Artist}";
        var artistWidth = TextLine.Measure(Gfx, artist, fit.Primary, Weight.Regular, null);
        if (titleWidth + artistWidth <= room)
        {
            return (title, titleWidth, artist, artistWidth);
        }
        var titleRoom = MathF.Min(titleWidth, MathF.Max(room * TitleShare, room - artistWidth));
        return (title, titleRoom, artist, MathF.Max(0, room - titleRoom));
    }

    /// <summary>
    /// The two lines a thick enough horizontal bar shows (<see cref="TwoLines"/>), at the clock's two
    /// lines' <paramref name="px"/>, each in all of <paramref name="room"/> px: the title, and under it
    /// the artist, without the " · " that joins them on one line — or, with no artist, the app it plays
    /// in (§497), so the face is two lines whatever plays; a title that is the app's name alone stands
    /// alone. Each is cut with an ellipsis on its own line.
    /// </summary>
    private (string Title, float TitleRoom, string? Artist, float ArtistRoom) Stacked(float px, Playing playing, float room)
    {
        var title = playing.Title.Length > 0 ? playing.Title : playing.AppName;
        var titleRoom = MathF.Min(TextLine.Measure(Gfx, title, px, Weight.Regular, null), room);
        var under = playing.Artist.Length > 0 ? playing.Artist : playing.Title.Length > 0 ? playing.AppName : "";
        if (under.Length == 0)
        {
            return (title, titleRoom, null, 0);
        }
        var underRoom = MathF.Min(TextLine.Measure(Gfx, under, px, Weight.Regular, null), room);
        return (title, titleRoom, under, underRoom);
    }

    /// <summary>
    /// The least room a piece of the words is drawn in: under it, "W…" or " · J…" says nothing, so
    /// it is left out rather than drawn as an ellipsis.
    /// </summary>
    private static float LeastWords(in Fit fit) => fit.Px(16) * fit.TextScale;

    /// <summary>The art: square, as tall as the bar leaves after a margin that grows with it.</summary>
    internal static float ArtSize(in IslandStyle style)
    {
        var fit = new Fit(style);
        var margin = style.Vertical ? fit.Px(4) : MathF.Max(MathF.Max(1, fit.Px(2)), TextLine.Round(style.Thickness * 0.1f));
        return MathF.Max(style.Thickness - 2 * margin, 1);
    }

    /// <summary>"Title — Artist · App", or "Title · site · Browser": the site said once, by the source.</summary>
    private static string Describe(Playing playing)
    {
        var text = playing.Title.Length > 0 ? playing.Title : S.WidgetMediaMedia;
        if (playing.Artist.Length > 0 && playing.Source is not { Site: not null })
        {
            text += $" — {playing.Artist}";
        }
        // Where it plays: the app, or a browser's site and the browser (NowPlayingSource).
        var source = playing.Source?.Line is { Length: > 0 } line ? line : playing.AppName;
        return source.Length > 0 ? $"{text} · {source}" : text;
    }

    /// <summary>
    /// Something changed (the shared sessions said so, on the dock thread, or a pick in the picker):
    /// read the state again off the dock's thread, once for a burst of events, and once more for a
    /// change that came while a read ran (Rereads) — that read may have read the controls before
    /// they changed.
    /// </summary>
    private void Changed()
    {
        if (!stop.IsCancellationRequested && stand is null)
        {
            rereads.Ask();
        }
    }

    /// <summary>One read, handed over to the dock's thread.</summary>
    private async Task ReadOnce()
    {
        if (stop.IsCancellationRequested)
        {
            return;
        }
        try
        {
            var list = Sessions();
            var playing = await Read().ConfigureAwait(false);
            last = playing;
            Interlocked.Exchange(ref arrived, new Box(playing, list));
            Post();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (!stop.IsCancellationRequested)
            {
                Console.Error.WriteLine($"Karpo: media state not read: {error.GetType().Name}");
            }
        }
    }

    /// <summary>
    /// Every app with a session, each by its name and its icon (NowPlayingSource.AppIcon, kept once
    /// found, so later reads look nothing up), as the media flyout listed them to pick from.
    /// </summary>
    private List<MediaEntry> Sessions()
    {
        var list = new List<MediaEntry>();
        foreach (var entry in sessions.Sessions)
        {
            if (entry.SourceAppId.Length > 0)
            {
                list.Add(new MediaEntry(entry.SourceAppId, MediaNames.Of(entry.SourceAppId), entry.Playing, NowPlayingSource.AppIcon(entry.SourceAppId)));
            }
        }
        return list;
    }

    /// <summary>
    /// The session it follows: the picked app's, while it has one — the app it belonged to may go,
    /// and then it is whatever Windows leads with again — else Windows' current one.
    /// </summary>
    private GlobalSystemMediaTransportControlsSession? Followed()
    {
        if (pick.Key is { Length: > 0 } key)
        {
            foreach (var entry in sessions.Sessions)
            {
                if (string.Equals(entry.SourceAppId, key, StringComparison.OrdinalIgnoreCase) && entry.Session is { } session)
                {
                    return session;
                }
            }
        }
        return sessions.Current;
    }

    private async Task<Playing?> Read()
    {
        if (Followed() is not { } session)
        {
            return null;
        }
        var read = await SessionRead.Of(session, stop.Token).ConfigureAwait(false);
        var before = last;
        switch (MediaGap.Judge(read, before?.App))
        {
            case MediaVerdict.Nothing:
                return null;
            case MediaVerdict.Hold:
                // A change of track half done: the track shown stays, its buttons with it — acting
                // on the session read, which may be one the app has just registered anew.
                return before! with { Session = session };
        }
        var shown = before is null ? default : new MediaButtons(before.CanPrevious, before.CanToggle, before.CanNext);
        var buttons = MediaGap.Buttons(read, before?.App, shown);
        var app = read.App;
        var (title, artist) = (read.Title, read.Artist);
        var art = await ArtOf(string.Join('\n', app, title, artist, read.Album), read.Thumbnail).ConfigureAwait(false);
        var source = NowPlayingSource.Of(read);
        // A browser's site in the artist's place is said as every face says it: "youtube.com".
        artist = NowPlayingSource.Artist(artist, source);
        var timeline = read.Timeline;
        // The timeline, where the app gives one (a live stream or a browser tab may not): how long
        // the track is, where it stood and when.
        var start = timeline?.StartTime ?? TimeSpan.Zero;
        TimeSpan? duration = timeline is not null && timeline.EndTime - start > TimeSpan.Zero ? timeline.EndTime - start : null;
        TimeSpan? position = duration is null ? null : TimeSpan.FromTicks(Math.Max(0, (timeline!.Position - start).Ticks));
        // An app that never says when leaves the year 1; then it is now, where the read happened.
        var taken = timeline is { LastUpdatedTime.Year: > 2000 } ? timeline.LastUpdatedTime : Clock.RealOffsetUtc();
        return new Playing(
            session,
            title,
            artist,
            app,
            AppName(app),
            read.Playing,
            buttons.Previous,
            buttons.Toggle,
            buttons.Next,
            art,
            position,
            duration,
            taken,
            start,
            read.CanSeek && duration is not null,
            source,
            read.CanShuffle,
            read.Shuffle,
            read.CanRepeat,
            read.Repeat switch
            {
                RepeatMode.Track => MediaRepeat.Track,
                RepeatMode.List => MediaRepeat.List,
                _ => MediaRepeat.None,
            });
    }

    /// <summary>
    /// The art of the track <paramref name="key"/> names, from the session's
    /// <paramref name="thumbnail"/>: decoded once for every now-playing face, and decoded again on
    /// a later read where it found nothing (<see cref="NowPlayingArt"/>). Null where the session
    /// has no thumbnail, or none that reads yet.
    /// </summary>
    internal static Task<Pixels?> ArtOf(string key, IRandomAccessStreamReference? thumbnail) =>
        NowPlayingArt.Shared.For(key, thumbnail is null ? null : () => Decode(thumbnail));

    /// <summary>
    /// The art as premultiplied BGRA, scaled down once; null when it can't be read. Not tied to any
    /// one face — every face waits on it — so no face going away stops it; it gives up after
    /// <see cref="ArtWait"/>, and the next read tries again.
    /// </summary>
    private static async Task<Pixels?> Decode(IRandomAccessStreamReference thumbnail)
    {
        using var wait = new CancellationTokenSource(ArtWait);
        try
        {
            using var stream = await thumbnail.OpenReadAsync().AsTask(wait.Token).ConfigureAwait(false);
            var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(wait.Token).ConfigureAwait(false);
            var (width, height) = (decoder.OrientedPixelWidth, decoder.OrientedPixelHeight);
            if (width == 0 || height == 0)
            {
                return null;
            }
            var fit = Math.Min(1.0, (double)ArtPx / Math.Min(width, height));
            var transform = new BitmapTransform
            {
                ScaledWidth = (uint)Math.Max(1, Math.Round(width * fit)),
                ScaledHeight = (uint)Math.Max(1, Math.Round(height * fit)),
                InterpolationMode = BitmapInterpolationMode.Fant,
            };
            var data = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.DoNotColorManage)
                .AsTask(wait.Token)
                .ConfigureAwait(false);
            var bytes = data.DetachPixelData();
            var (outWidth, outHeight) = ((int)transform.ScaledWidth, (int)transform.ScaledHeight);
            return bytes.Length >= outWidth * outHeight * 4 ? new Pixels { Data = bytes, Width = outWidth, Height = outHeight } : null;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Unreadable, or not handed over in time: no art this time.
            return null;
        }
    }

    /// <summary>The app's name as Start shows it; the executable's name for a desktop app without one.</summary>
    private static string AppName(string aumid)
    {
        if (aumid.Length == 0)
        {
            return "";
        }
        try
        {
            if (Windows.ApplicationModel.AppInfo.GetFromAppUserModelId(aumid)?.DisplayInfo?.DisplayName is { Length: > 0 } name)
            {
                return name;
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Not a packaged app, or a Windows without this lookup.
        }
        var bare = Path.GetFileNameWithoutExtension(aumid.Split('!')[0]);
        return bare.Length > 0 ? char.ToUpperInvariant(bare[0]) + bare[1..] : aumid;
    }

    /// <summary>
    /// Brings the playing app forward: one of its windows when it has one,
    /// found the way the taskbar groups windows (the window's own AUMID, or
    /// the executable a desktop app names itself by); else Start's entry,
    /// handed to <paramref name="open"/>.
    /// </summary>
    private static unsafe void Bring(string aumid, Opener open)
    {
        if (aumid.Length == 0)
        {
            return;
        }
        var exe = aumid.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? aumid : aumid + ".exe";
        for (var hwnd = GetTopWindow(HWND.NULL); hwnd != HWND.NULL; hwnd = GetWindow(hwnd, GW_HWNDNEXT))
        {
            if (!IsWindowVisible(hwnd) || GetWindow(hwnd, GW_OWNER) != HWND.NULL
                || (GetWindowLongPtrW(hwnd, GWL.GWL_EXSTYLE) & WS.WS_EX_TOOLWINDOW) != 0)
            {
                continue;
            }
            if (string.Equals(WindowAumid(hwnd), aumid, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(ProcessPath(hwnd)), exe, StringComparison.OrdinalIgnoreCase))
            {
                Apps.Activate(hwnd);
                return;
            }
        }
        open($"shell:AppsFolder\\{aumid}", null);
    }

    private static unsafe string? WindowAumid(HWND hwnd)
    {
        IPropertyStore* store = null;
        if (SHGetPropertyStoreForWindow(hwnd, __uuidof<IPropertyStore>(), (void**)&store).FAILED || store == null)
        {
            return null;
        }
        try
        {
            var key = PKEY.PKEY_AppUserModel_ID;
            PROPVARIANT value = default;
            if (store->GetValue(&key, &value).FAILED)
            {
                return null;
            }
            char* text = null;
            var converted = PropVariantToStringAlloc(&value, &text);
            _ = PropVariantClear(&value);
            return converted.SUCCEEDED ? ComUtil.FromTaskMem(text) : null;
        }
        finally
        {
            store->Release();
        }
    }

    private static unsafe string? ProcessPath(HWND hwnd)
    {
        uint pid;
        _ = GetWindowThreadProcessId(hwnd, &pid);
        var process = OpenProcess((uint)PROCESS.PROCESS_QUERY_LIMITED_INFORMATION, BOOL.FALSE, pid);
        if (process == HANDLE.NULL)
        {
            return null;
        }
        var buffer = stackalloc char[1024];
        uint size = 1024;
        var read = QueryFullProcessImageNameW(process, 0, buffer, &size);
        _ = CloseHandle(process);
        return read ? new string(buffer, 0, (int)size) : null;
    }

    /// <summary>
    /// What is playing. <paramref name="Position"/> is where the track stood at
    /// <paramref name="Taken"/>, of its <paramref name="Duration"/> from <paramref name="Start"/> —
    /// null where the app gives no timeline — and <paramref name="Taken"/> is null where the
    /// position does not move with the clock (a stand-in for the tests). <paramref name="App"/> is
    /// the session's source app id, which the picker follows it by.
    /// </summary>
    private sealed record Playing(
        GlobalSystemMediaTransportControlsSession? Session,
        string Title,
        string Artist,
        string App,
        string AppName,
        bool IsPlaying,
        bool CanPrevious,
        bool CanToggle,
        bool CanNext,
        Pixels? Art,
        TimeSpan? Position = null,
        TimeSpan? Duration = null,
        DateTimeOffset? Taken = null,
        TimeSpan Start = default,
        bool CanSeek = false,
        MediaSource? Source = null,
        bool CanShuffle = false,
        bool Shuffle = false,
        bool CanRepeat = false,
        MediaRepeat Repeat = MediaRepeat.None)
    {
        /// <summary>
        /// Where the track stands at <paramref name="moment"/>: what was read, and the time since while
        /// it plays. Never past the end, never before the start.
        /// </summary>
        public TimeSpan At(DateTimeOffset moment)
        {
            if (Position is not { } position)
            {
                return TimeSpan.Zero;
            }
            var since = IsPlaying && Taken is { } taken ? moment - taken : TimeSpan.Zero;
            var at = position + (since > TimeSpan.Zero ? since : TimeSpan.Zero);
            return Duration is { } whole && at > whole ? whole : at;
        }

        /// <summary>The share of the track that is behind at <paramref name="moment"/>, 0 to 1; 0 without a timeline.</summary>
        public float Fraction(DateTimeOffset moment) =>
            Duration is { Ticks: > 0 } whole ? (float)Math.Clamp(At(moment).TotalSeconds / whole.TotalSeconds, 0, 1) : 0;
    }

    /// <summary>A handed-over state, which may be "nothing plays", and the apps playing.</summary>
    private sealed record Box(Playing? State, IReadOnlyList<MediaEntry> Sessions);

    // --- In a folder ------------------------------------------------------------
    // What's playing, as a line of a list: the art, the title and the artist, and previous, play or
    // pause, and next — shuffle, repeat and the picker where the line has the room. A click on a
    // button does what the button says and the folder stays open; anywhere else does nothing.

    /// <summary>A list's line: its own design at every height it is given (MediaLine.cs).</summary>
    protected internal override unsafe void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (Sources)
        {
            PaintSources(dc, box, style);
            return;
        }
        PaintLine(dc, box, style);
    }

    /// <summary>Where the line's parts are, for the tests: the very layout it draws and hit-tests with.</summary>
    internal TileLayout CardParts(D2D_RECT_F box, in TileStyle style) => LineParts(box, style);

    /// <summary>
    /// The line's form for its size (§432, MediaLine.cs): Compact one row — the title, play or pause, the art;
    /// Medium the kit's row with its caption (§401) — the title over the artist, the transport, the art; Full the
    /// whole widget — that row, the timeline with its times and the app's row under it, where the row keeps its
    /// three buttons (a narrow line at a large text keeps play alone, and is the row). The apps playing are a
    /// list of their own.
    /// </summary>
    public float ListHeight(float width, in TileStyle style) => Sources ? SourcesHeight(style) : LineSizes.Of(Settings) switch
    {
        LineSize.Compact => LineRow.Height(style, caption: false),
        LineSize.Full when Row(PopupPaint.Rect(0, 0, width, 0), style).Three => LineRow.Height(style, caption: true) + LineRows.Cell(style) + LineRows.Px(1, style),
        _ => LineRow.Height(style, caption: true),
    };

    /// <summary>
    /// A control's button: its cell index (previous, play or pause, next; shuffle and repeat where the
    /// app has them; the picker where two apps or more play), or the info cell anywhere else.
    /// </summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (Sources)
        {
            return SourceTargetAt(point, box, style);
        }
        var layout = LineOf(box, style);
        if (playingIn.Count > 1 && Inside(layout.Picker, point))
        {
            return (int)Cell.Picker;
        }
        if (now is not { } playing)
        {
            return (int)Cell.Info;
        }
        // A button takes the lines' reach round it (LineButton.Hit): a 24 DIP button, 32 to the pointer.
        var reach = LineButton.Of(Gfx, style);
        return Inside(reach.Hit(layout.Previous), point) ? (int)Cell.Previous
            : Inside(reach.Hit(layout.Play), point) ? (int)Cell.PlayPause
            : Inside(reach.Hit(layout.Next), point) ? (int)Cell.Next
            : playing.CanShuffle && Inside(reach.Hit(layout.Shuffle), point) ? (int)Cell.Shuffle
            : playing.CanRepeat && Inside(reach.Hit(layout.Repeat), point) ? (int)Cell.Repeat
            : (int)Cell.Info;

    }

    private static bool Inside(D2D_RECT_F rect, Vector2 point) =>
        point.X >= rect.left && point.X < rect.right && point.Y >= rect.top && point.Y < rect.bottom;

    /// <summary>
    /// The line's parts: the art, the words' room, the buttons, shuffle and repeat, and the picker
    /// on the app's name. A part the line has no room for is an empty rectangle — it is not drawn,
    /// and no click lands on it.
    /// </summary>
    internal readonly record struct TileLayout(
        D2D_RECT_F Art, D2D_RECT_F Text, D2D_RECT_F Previous, D2D_RECT_F Play, D2D_RECT_F Next,
        D2D_RECT_F Shuffle = default, D2D_RECT_F Repeat = default, D2D_RECT_F Picker = default);
}

/// <summary>
/// A make-believe track for the tests (<see cref="MediaIsland.Stand"/>): what the widget draws,
/// with no player and no session behind it.
/// </summary>
/// <param name="Nothing">
/// Nothing plays: the widget's quiet state, whatever the machine running the tests is playing.
/// </param>
/// <param name="Art">The track's art, as a session's thumbnail decodes to; none where it has none.</param>
/// <param name="Position">Where the track stands, still: a stand-in's time does not move with the clock.</param>
/// <param name="Duration">How long the track is; null for one without a timeline.</param>
/// <param name="CanSeek">Whether the timeline shows it can be pressed. Nothing seeks: there is no app.</param>
/// <param name="SourceIcon">The playing app's icon, or the site's favicon where <paramref name="Site"/> names one.</param>
/// <param name="Site">The host a browser session plays on, as the session's artist names it; null for an app's.</param>
/// <param name="CanShuffle">Whether the app lets shuffle be set; <paramref name="Shuffle"/> whether it is on.</param>
/// <param name="CanRepeat">Whether the app lets repeat be set; <paramref name="Repeat"/> how it is set.</param>
/// <param name="Sessions">The apps playing, to pick from; none but the track's own where null.</param>
/// <param name="Pick">The pick the islands made over this stand share; each its own where null, never the dock's.</param>
/// <param name="QuietGrace">How long a stop counts as playing (MediaQuiet); its own where null.</param>
internal sealed record MediaStand(
    string Title = "Air on the G String", // not-ui: test stand-in
    string Artist = "Johann Sebastian Bach", // not-ui: test stand-in
    string AppName = "Sample Player", // not-ui: test stand-in
    bool IsPlaying = true,
    bool CanPrevious = true,
    bool CanToggle = true,
    bool CanNext = true,
    bool Nothing = false,
    Pixels? Art = null,
    TimeSpan? Position = null,
    TimeSpan? Duration = null,
    bool CanSeek = false,
    Pixels? SourceIcon = null,
    string? Site = null,
    bool CanShuffle = false,
    bool Shuffle = false,
    bool CanRepeat = false,
    MediaRepeat Repeat = MediaRepeat.None,
    IReadOnlyList<MediaEntry>? Sessions = null,
    MediaPick? Pick = null,
    TimeSpan? QuietGrace = null);
