// A folder on the bar: its button — its faces (FolderOptions.Faces, each drawn by
// FaceDrawings) side by side: its icon, a clock, the date, the weather … — and no name (§427). A click opens its widgets (FolderFlyout) at the cell, as
// the quick-settings buttons open theirs, and a second click closes it.
//
// The widgets in it are islands of their own, keyed "{owner}/{folder id}/{id}",
// made, configured and disposed with the folder. They share the stores as the
// islands on the bar do (calendar feeds, notifications, media sessions) and post
// to the dock with the folder's token, so the folder's Refresh takes what any of
// them finished. They are ticked and drawn only while the folder is open: the
// flyout's own timer ticks them, nothing does while it is closed, and the bar
// cell never shows their content. Their data is another matter: it is a data
// source's (DataSources), gathered from the dock's start whether the folder is
// open or not, so a folder opened after an hour draws the hour.
//
// A click on a tile runs that widget's Click. In a folder a click on a widget
// opens no popup (docs\widgets.md §15), so what it can hand the dock is an app
// to open, and nothing else.
//
// §474: it draws Windows' own widgets' faces too (WindowsFaces: the Widgets button, quick settings'
// button, the clock), which are no folder (IsFolder): their faces are fixed (FacesOf), they hold
// nothing, and the dock opens Windows' menu for their click (Dock.WindowsButtonAt), never a folder.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Drives;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>A widget of a folder: its island and the cells it covers.</summary>
internal sealed record FolderTile(IIsland Island, FolderCell Cell);

internal sealed unsafe partial class FolderIsland : CellIsland, IIsland, IWidgetContainer, IWheelIsland, IMiddleClickIsland, INotificationActs
{
    /// <summary>
    /// What the widgets in it agree about while it is open: its channels (docs\widgets.md §9,
    /// "Channels"; Karpo.Core\Channels.cs) — the day a month picked, and whatever channel comes next.
    /// Transient — cleared when the folder closes, and nothing here is ever written to the config.
    /// </summary>
    private readonly ChannelValues shared = new();

    private readonly Dictionary<string, IIsland> widgets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<IIsland, long> ticked = [];

    /// <summary>§394: the widgets it holds, each by its own key (the folder's, then its id): whose failed reads are the dock's errors (Faults).</summary>
    internal IEnumerable<IIsland> Widgets => widgets.Values;

    private FolderOptions options = new();
    private List<FolderTile> tiles = [];
    private nint dock;
    private nuint token;
    private bool attached;

    /// <summary>calendar: the dock's countdowns, which a timer and a focus session in it run on; null for their own.</summary>
    private readonly Countdowns? countdowns;

    public FolderIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, null)
    {
    }

    internal FolderIsland(string key, WidgetItem island, Gfx gfx, Config config, Countdowns? countdowns)
        // folders: the kind is the view — a list folder (Folders.View).
        : base(key, island, gfx)
    {
        this.countdowns = countdowns;
        Configure(island, config);
    }

    /// <summary>How it opens: its kind's view, chosen when the folder was made and fixed since.</summary>
    public FolderView View => BarEntries.Find(Info.Id)?.Opens ?? FolderView.List;

    /// <summary>
    /// §474: whether it is a folder — Karpo's own, a face with widgets inside — rather than one of Windows' own widgets
    /// wearing Windows' face (<see cref="WindowsFaces"/>), which holds nothing, opens Windows' menu and never a folder.
    /// </summary>
    internal bool IsFolder => BarEntries.Find(Info.Id)?.Opens is not null;

    /// <summary>
    /// The faces and lists <paramref name="item"/> is drawn with: Windows' own faces for one of Windows' widgets that wears
    /// them, on a PC with a battery or without (<paramref name="battery"/>, asked only for them; §474); else the folder's own, as
    /// the file has them.
    /// </summary>
    internal static FolderOptions FacesOf(WidgetItem item, Func<bool> battery) =>
        (item is BarItem { Kind: var kind } && kind.WearsWindowsFace() ? WindowsFaces.Of(kind, battery(), WindowsBatteryPercent.On) : null) ?? (item as BarItem)?.Folder ?? new FolderOptions();

    /// <summary>The widgets have news, or the folder changed: an open flyout draws again.</summary>
    public event Action? Changed;

    /// <summary>
    /// The open flyout ticks the widgets inside itself, and a face that tells the time is painted
    /// again on the dock's clock tick (<see cref="FaceTick"/>), on the minute or the second. A face
    /// that reads something ticks the folder at its own pace — a weather face every online widget's
    /// interval, a markets face the markets', a reading's face its pace … — and a folder of several
    /// faces at the quickest of theirs, each face's own reading done at its own (<see cref="Sample"/>).
    /// </summary>
    public override int IntervalMs
    {
        get
        {
            var every = 0;
            void Take(bool wears, int ms)
            {
                if (wears && ms > 0 && (every == 0 || ms < every))
                {
                    every = ms;
                }
            }
            Take(weatherPlace is not null, (int)WeatherStore.Shared.Every.TotalMilliseconds);
            Take(ShowsAir, (int)AirQualityStore.Shared.Every.TotalMilliseconds);
            Take(ShowsTides, (int)TideStore.Shared.Every.TotalMilliseconds);
            // sports: the face's team is asked for at its own pace, the quickest half a minute (§239).
            Take(ShowsSports, (int)SportsPace.Live.TotalMilliseconds);
            Take(ShowsQuakes, (int)QuakeStore.Every.TotalMilliseconds);
            Take(AsksMarket, (int)markets.Every.TotalMilliseconds);
            Take(ShowsDevices, DevicesEvery);
            Take(hotspot is not null, HotspotEvery);
            Take(ShowsBattery && battery is not null, BatteryEvery);
            foreach (var face in options.Faces)
            {
                Take(PerformanceFace.MetricOf(face.Info) is not null, PerformanceFace.Options(face).Seconds * 1000);
            }
            Take(ShowsDrive, DriveEvery);
            Take(true, CastNearbyEvery);
            return every;
        }
    }

    // When each face's own reading was last done on the folder's tick, which is the quickest of its
    // faces' paces: a slower face reads only once its own pace has passed (PaceDue).
    private readonly Dictionary<string, long> sampled = [];

    /// <summary>Whether <paramref name="what"/>'s pace of <paramref name="every"/> ms has passed since it was last read on a tick; it is read now where it has.</summary>
    private bool PaceDue(string what, int every)
    {
        var now = Environment.TickCount64;
        if (sampled.TryGetValue(what, out var last) && now - last + 500 < every)
        {
            return false;
        }
        sampled[what] = now;
        return true;
    }

    /// <summary>
    /// drives: how often a drive face reads its drive by itself — a minute, as the drive entry on the
    /// bar did, behind the dock's own reading of every drive as one comes or goes.
    /// </summary>
    private const int DriveEvery = 60_000;

    /// <summary>drives: whether it wears a drive face, which shows a drive and how full it is.</summary>
    internal bool ShowsDrive => options.Faces.Any(face => FolderFaces.DriveOf(face.Info) is not null);

    /// <summary>
    /// drives: the line a drive face shows — its own drive's (§477), else the folder's first line of the face's kind (a
    /// drive's or a network drive's), whose drive it draws, so the face and the line are one drive, set in one
    /// place. Null for another face, or where it has neither.
    /// </summary>
    internal DriveIsland? DriveLine =>
        options.Faces.Select(face => FolderFaces.DriveOf(face.Info)).FirstOrDefault(kind => kind is not null) is { } kind
            ? DriveLineOf(kind)
            : null;

    /// <summary>
    /// drives: what a drive face of <paramref name="kind"/> (a drive's or a network drive's) shows: its own drive's line
    /// (§477), else the folder's first line of that kind.
    /// </summary>
    internal DriveIsland? DriveLineOf(WidgetInfo kind) =>
        options.Faces.FirstOrDefault(face => FolderFaces.DriveOf(face.Info) == kind) is { } own && FaceSources.Owns(own)
            ? OwnSource(own) as DriveIsland
            : tiles.Select(tile => tile.Island).OfType<DriveIsland>().FirstOrDefault(line => line.Info == kind);

    /// <summary>
    /// power: how often a battery face reads the charge by itself — a minute, as the battery on the
    /// bar did, behind the power broadcasts Windows sends as the charger comes or goes and the
    /// charge falls (<see cref="System"/>). Without a battery nothing changes, and nothing ticks.
    /// </summary>
    private const int BatteryEvery = 60_000;

    // power: the charge a battery face shows, and where it is read from — this PC's, or a test's
    // (BatteryIsland.Probe, per thread, taken when the folder is made).
    private readonly Func<SystemStatus.Battery?> probe = BatteryIsland.Probe;
    private SystemStatus.Battery? battery;

    /// <summary>power: whether it wears the battery face, which shows the charge.</summary>
    internal bool ShowsBattery => options.Wears(FolderFaces.Battery);

    /// <summary>power: the charge the battery face shows; null without a battery, and for another face.</summary>
    internal SystemStatus.Battery? Battery => ShowsBattery ? battery : null;

    // pim: the mail a mail face shows, the store it is attached to while it wears the face — a
    // test's (MailIsland.Stand, taken when the folder is made), else the dock's, asked for only then.
    private readonly Pim.MailStore? mailStand = Pim.MailIsland.Stand ?? (Samples.On ? Samples.Mail() : null);
    private Pim.MailStore? mail;

    /// <summary>pim: whether it wears the mail face, which shows how many are unread.</summary>
    internal bool ShowsMail => options.Wears(FolderFaces.Mail);

    /// <summary>pim: the mail store the mail face shows; null for another face.</summary>
    internal Pim.MailStore? Mail => ShowsMail ? mail : null;

    /// <summary>
    /// §639: what the mail face counts: what the folder's first mail line shows (its "Show from"), every inbox where it
    /// holds none or that line picked nothing — so the badge says the unread of the folders the person picked.
    /// </summary>
    internal SourceFilter MailShows =>
        SourceFilter.Of(options.Cells.Select(cell => cell.Island).FirstOrDefault(item => WidgetSources.KindOf(item) == AccountShows.Mail));

    /// <summary>
    /// pim: the mail face follows the store — attached when the face is put on, so the store's news
    /// wakes the folder and the face is painted again in place, as it woke the entry; let go when it
    /// is taken off. The store reads on its own clock (PimSync), so the face needs no tick.
    /// </summary>
    private void FollowMail()
    {
        if (ShowsMail == mail is not null)
        {
            return;
        }
        if (mail is { } gone)
        {
            mail = null;
            gone.Release(this);
            return;
        }
        mail = mailStand ?? Pim.MailStore.Shared;
        mail.Attach(this);
    }

    // clipboard: the store the clipboard face hears the clipboard through while it wears the face — a
    // test's (ClipboardLine.Stand, taken when the folder is made), else the dock's.
    private readonly ClipboardStore? clipboardStand = ClipboardLine.Stand;
    private ClipboardStore? clipboard;

    /// <summary>clipboard: whether it wears the clipboard face.</summary>
    internal bool ShowsClipboard => options.Wears(FolderFaces.Clipboard);

    /// <summary>clipboard: the store the clipboard face is attached to; null for another face.</summary>
    internal ClipboardStore? Clipboard => ShowsClipboard ? clipboard : null;

    /// <summary>
    /// clipboard: the clipboard face attaches to the store while it is on, as the entry it replaced did
    /// while it stood on a bar: that is what starts the clipboard being heard where Karpo keeps its own
    /// history, so what was copied before the folder is first opened is there (docs\widgets.md §11).
    /// Let go when the face is taken off or the folder goes.
    /// </summary>
    private void FollowClipboard()
    {
        if (ShowsClipboard == clipboard is not null)
        {
            return;
        }
        if (clipboard is { } gone)
        {
            clipboard = null;
            gone.Release(this);
            return;
        }
        clipboard = clipboardStand ?? ClipboardStore.Shared;
        clipboard.Attach(this);
    }

    // notifications: what the bell face shows, the store it uses while it wears the face — a test's
    // (NotificationPartTile.Stand, taken when the folder is made), else the dock's — and do not
    // disturb, whose "zZ" it wears, followed from its first paint (docs\widgets.md §11).
    private readonly NotificationStore? notificationsStand = NotificationPartTile.Stand;
    private NotificationStore? notifications;
    private QuickSettings.Services.DoNotDisturb? quiet;

    /// <summary>notifications: whether it wears the bell face, which shows how many are new.</summary>
    internal bool ShowsNotifications => options.Wears(FolderFaces.Notifications);

    /// <summary>notifications: the store the bell face shows; null for another face.</summary>
    internal NotificationStore? Notifications => ShowsNotifications ? notifications : null;

    /// <summary>notifications: do not disturb as the bell face shows it — null until its first paint, and for another face.</summary>
    internal QuickSettings.Services.DoNotDisturb? Quiet => ShowsNotifications ? quiet : null;

    /// <summary>
    /// notifications: the bell face follows the store — used while the face is on, so the store's news
    /// wakes the folder and the face is painted again in place, as it woke the bell; let go when it
    /// is taken off. The store watches Windows' database, so the face needs no tick.
    /// </summary>
    private void FollowNotifications()
    {
        if (ShowsNotifications == notifications is not null)
        {
            return;
        }
        if (notifications is { } gone)
        {
            notifications = null;
            gone.Release(this);
            LetQuietGo();
            return;
        }
        notifications = notificationsStand ?? NotificationStore.Shared;
        notifications.Use(this);
    }

    /// <summary>notifications: do not disturb, followed from the bell face's first paint, on which it asks Windows.</summary>
    internal QuickSettings.Services.DoNotDisturb LookAtQuiet()
    {
        if (quiet is null)
        {
            quiet = QuickSettings.Services.DoNotDisturb.Shared;
            quiet.Changed += QuietChanged;
            quiet.Look();
        }
        return quiet;
    }

    private void QuietChanged() => Post();

    private void LetQuietGo()
    {
        if (quiet is not null)
        {
            quiet.Changed -= QuietChanged;
            quiet = null;
        }
    }

    /// <summary>
    /// notifications: the folder was opened to look at it, not to arrange it: everything its notification
    /// lists show — and the bell face — counts as seen, as the bell's own list did as it opened.
    /// </summary>
    internal void MarkSeen()
    {
        var lines = tiles.Select(tile => tile.Island).OfType<NotificationPartTile>().ToList();
        foreach (var line in lines)
        {
            line.Seen();
        }
        if (Notifications is { } store && !lines.Any(line => ReferenceEquals(line.Store, store)))
        {
            store.MarkSeen();
        }
    }

    // notifications: the bell face's right click, worked on the face's own store (INotificationActs).
    bool INotificationActs.HasAny => Notifications is { Items.Count: > 0 };

    void INotificationActs.ClearAll() => Notifications?.DismissAll();

    QuickSettings.Services.DoNotDisturb INotificationActs.Dnd => LookAtQuiet();

    /// <summary>
    /// Tests only, on the thread that makes the folder: the sources a performance face holds its
    /// reading from, instead of the dock's, so what it shows is the test's and nothing is read.
    /// </summary>
    [ThreadStatic]
    internal static DataSources? PerformanceSources;

    // perf: the sources a performance face holds its reading from — the dock's, or a test's — and
    // the readings it holds, one for each reading and device its faces show (a CPU face beside a
    // graphics face holds both).
    private readonly DataSources performanceSources = PerformanceSources ?? DataSources.Shared;
    private readonly Dictionary<SourceKey, PerformanceData> performance = [];

    /// <summary>perf: the reading its first performance face shows (<see cref="PerformanceOf"/>); null where it wears none.</summary>
    internal PerformanceData? Performance =>
        options.Faces.FirstOrDefault(face => PerformanceFace.MetricOf(face.Info) is not null) is { } first ? PerformanceOf(first) : null;

    /// <summary>
    /// perf: the reading the performance face <paramref name="face"/> shows: the folder's first line
    /// of that reading set to the same device — so the face and the line show one figure, a line fed
    /// by a test as well — else the source the face holds, the one every widget of that reading and
    /// device draws (DataSources). Null for another face.
    /// </summary>
    internal PerformanceData? PerformanceOf(FolderFace face)
    {
        if (SourceKinds.FaceLine(face) is not { } line || SourceKinds.KeyOf(line) is not { } key || !performance.TryGetValue(key, out var held))
        {
            return null;
        }
        return tiles.Select(tile => tile.Island).OfType<PerformanceIsland>().FirstOrDefault(each => each.Source.Key == held.Key)?.Source ?? held;
    }

    /// <summary>
    /// perf: each performance face holds the source of its reading and device, set as it is
    /// (SourceKinds.FaceLine), from the sources the dock gathers — taken when the face is put on or
    /// set to another adapter or disk, let go when it is taken off. Its pace is how often it is
    /// painted again (IntervalMs), never another source.
    /// </summary>
    private void FollowPerformance()
    {
        var wanted = new Dictionary<SourceKey, CellItem>();
        foreach (var face in options.Faces)
        {
            if (SourceKinds.FaceLine(face) is { } line && SourceKinds.KeyOf(line) is { } key)
            {
                wanted.TryAdd(key, line);
            }
        }
        foreach (var gone in performance.Keys.Where(key => !wanted.ContainsKey(key)).ToList())
        {
            performanceSources.Release(performance[gone]);
            performance.Remove(gone);
        }
        foreach (var (key, line) in wanted)
        {
            if (performance.ContainsKey(key))
            {
                continue;
            }
            var held = performanceSources.Hold(key, line);
            if (held is PerformanceData reading)
            {
                performance[key] = reading;
            }
            else
            {
                performanceSources.Release(held);
            }
        }
    }

    // quick: the now-playing face's own widget, where the folder holds no now-playing line to read.
    private MediaIsland? nowPlaying;

    /// <summary>quick: whether it wears the now-playing face, which shows what is playing.</summary>
    internal bool ShowsNowPlaying => options.Wears(FolderFaces.NowPlaying);

    /// <summary>
    /// quick: what the now-playing face shows: the folder's first now-playing line — so the face and
    /// the line follow one session, the one the person picked on the line — else a widget of the
    /// face's own. Null for another face.
    /// </summary>
    internal MediaIsland? NowPlaying =>
        ShowsNowPlaying ? tiles.Select(tile => tile.Island).OfType<MediaIsland>().FirstOrDefault(media => !media.Sources) ?? nowPlaying : null;

    /// <summary>
    /// quick: the face's own now-playing widget, made where it wears the face and holds no line, as a
    /// line of the folder's is made — its news posts with the folder's token, so the face is painted
    /// again in place — and let go where it does not.
    /// </summary>
    private void FollowNowPlaying(Config config)
    {
        var wants = ShowsNowPlaying && !tiles.Any(tile => tile.Island is MediaIsland { Sources: false });
        if (wants == nowPlaying is not null)
        {
            return;
        }
        if (nowPlaying is { } gone)
        {
            nowPlaying = null;
            gone.Dispose();
            return;
        }
        var item = ListKind.Media.New();
        item.Id = FaceMediaId;
        var made = new MediaIsland($"{Key}/{FaceMediaId}", item, Gfx, config);
        if (attached)
        {
            ((IIsland)made).Attach(dock, token);
        }
        nowPlaying = made;
    }

    // keyboard: the keyboard face's own indicator, which reads the layout in use.
    private KeyboardIsland? keyboard;

    /// <summary>keyboard: whether it wears the keyboard face, which shows the layout in use, first or among others.</summary>
    internal bool ShowsKeyboard => options.Wears(FolderFaces.Keyboard);

    /// <summary>keyboard: what the keyboard face shows, the layout in use; null for another face.</summary>
    internal KeyboardIsland? Keyboard => ShowsKeyboard ? keyboard : null;

    /// <summary>
    /// layouts: the taskbar layout the bar this folder stands on shows, which the layouts face wears;
    /// read from the config each time it is handed over, which a switch or a rename builds again.
    /// </summary>
    internal LayoutShown Shown { get; private set; } = new(TaskbarLayouts.DefaultName, TaskbarLayouts.LayoutGlyph, 1);

    /// <summary>
    /// keyboard: the face's own indicator, made where it wears the face — its news posts with the
    /// folder's token, so the face is painted again in place — and let go where it does not. The
    /// dock tells it when another window comes to the front (Dock.Keyboards).
    /// </summary>
    private void FollowKeyboard(Config config)
    {
        if (ShowsKeyboard == keyboard is not null)
        {
            return;
        }
        if (keyboard is { } gone)
        {
            keyboard = null;
            gone.Dispose();
            return;
        }
        var item = ListKind.Keyboard.New();
        item.Id = FaceKeyboardId;
        var made = new KeyboardIsland($"{Key}/{FaceKeyboardId}", item, Gfx, config);
        if (attached)
        {
            ((IIsland)made).Attach(dock, token);
        }
        keyboard = made;
    }

    /// <summary>keyboard: the id of the face's own indicator, under the folder's key; no line has it.</summary>
    private const string FaceKeyboardId = "face-keyboard";

    // quick: the switch a switch's face shows (VPN's), and which one it is.
    private QuickSettings.Services.IQuickSwitch? faceSwitch;
    private QuickSwitch? faceSwitchOf;

    /// <summary>quick: the switch the face shows on or off (<see cref="FolderFaces.SwitchOf"/>); null for another face.</summary>
    internal QuickSettings.Services.IQuickSwitch? FaceSwitch => WornSwitch is not null ? faceSwitch : null;

    /// <summary>quick: the switch of the first switch's face it wears, first or among others; null where it wears none.</summary>
    private QuickSwitch? WornSwitch => options.Faces.Select(face => FolderFaces.SwitchOf(face.Info)).FirstOrDefault(which => which is not null);

    /// <summary>
    /// quick: the face's own switch, made where it wears a switch's face — started at once, which
    /// asks Windows off the dock's thread, its news painting the face again in place — and let go
    /// where it does not.
    /// </summary>
    private void FollowSwitch()
    {
        var wanted = WornSwitch;
        if (wanted == faceSwitchOf)
        {
            return;
        }
        faceSwitch?.Dispose();
        faceSwitch = null;
        faceSwitchOf = wanted;
        if (wanted is { } which)
        {
            var made = QuickSettings.Services.QuickSwitches.For(which);
            faceSwitch = made;
            made.Start(Post);
        }
    }

    /// <summary>rss: whether it wears the news face, which shows a feed's newest headline.</summary>
    internal bool ShowsNews => options.Wears(FolderFaces.News);

    /// <summary>
    /// rss: the line the news face shows — its own feed's (§477), else the folder's first feed line. Null for another
    /// face, or where it has neither.
    /// </summary>
    internal Rss.RssIsland? Feed =>
        options.FaceOf(FolderFaces.NewsId) is not { } face ? null
        : FaceSources.Owns(face) ? OwnSource(face) as Rss.RssIsland
        : tiles.Select(tile => tile.Island).OfType<Rss.RssIsland>().FirstOrDefault();

    /// <summary>§477: the line <paramref name="face"/>'s own source is read through (<see cref="FaceSources.Own"/>); null where it has none.</summary>
    private IIsland? OwnSource(FolderFace face) => widgets.GetValueOrDefault($"{Key}/{FaceSources.LineId(face)}");

    /// <summary>The id the face's own now-playing widget is keyed by, which no line of a folder has.</summary>
    private const string FaceMediaId = "face-media";

    /// <summary>
    /// Off the bar while every face it wears has nothing to show (<see cref="FacesOnBar"/>: the
    /// removable devices with nothing plugged in, now playing set to hide while nothing plays): the
    /// bar is laid out again when that changes — a device plugged in or out (WM_DEVICECHANGE), a
    /// track starting, or a few seconds after it stops (MediaIsland.Idle: paused is nothing too).
    /// Beside other faces only the face goes (§237); where it was all it wore, the folder. Else
    /// always the one cell.
    /// </summary>
    protected override int Count => FacesOnBar.Count == 0 ? 0 : 1;

    // §237 (per-face hide): which faces are on the bar now. Kept apart from how long each face is.

    /// <summary>
    /// Its faces on the bar now, in their order: all of them, but those with nothing to show at rest
    /// (<see cref="IFaceDrawing.NothingToShow"/>, §237, §290). The others keep their room and their
    /// order, with no room between left where it stood, and the button is that much shorter. While
    /// the bar is arranged (<see cref="CellIsland.Arranged"/>) every face is on it, so a face with
    /// nothing to show can be found, moved and set.
    /// </summary>
    internal IReadOnlyList<FolderFace> FacesOnBar =>
        options.Faces.Any(HiddenNow) ? [.. options.Faces.Where(face => !HiddenNow(face))] : options.Faces;

    /// <summary>Whether <paramref name="face"/> has taken itself off the bar for now: at rest, with nothing to show.</summary>
    private bool HiddenNow(FolderFace face) => !Arranged && FaceDrawings.Of(face).NothingToShow(face, this);

    /// <summary>Its shape is measured over the faces on the bar now (<see cref="FacesOnBar"/>).</summary>
    private protected override ShapeContext Shaped(ShapeContext context)
    {
        var shown = FacesOnBar;
        return ReferenceEquals(shown, options.Faces) ? context : context with { Faces = shown };
    }

    /// <summary>
    /// quick: how often a hotspot face asks Windows again by itself. The tethering manager tells
    /// nobody when a device joins, so the face asks on a tick of its own, off the dock's thread,
    /// besides on every network change — an interface or an address too, which is what the hotspot
    /// switched in Windows' own Settings says (WindowsHotspot.Heard, §439) — and whenever a line in
    /// its folder had news.
    /// </summary>
    private const int HotspotEvery = 30_000;

    // quick: the mobile hotspot a hotspot face shows, a source of the face's own while it wears it;
    // what it heard; and whether the folder's last news was its own (IIsland.Refresh).
    private IHotspotSource? hotspot;
    private Changes? hotspotNews;
    private int hotspotHeard;

    /// <summary>quick: whether it wears the hotspot face, which shows whether the mobile hotspot is on and how many are on it.</summary>
    internal bool ShowsHotspot => options.Wears(FolderFaces.Hotspot);

    /// <summary>quick: the mobile hotspot as the hotspot face shows it; null for another face.</summary>
    internal HotspotState? Hotspot => hotspot?.Read();

    /// <summary>
    /// quick: the hotspot face's source, made and started when the face is put on — from the quick
    /// sources in force (QuickPageTile.SourcesNow: a test's, the samples, or Windows') — and let go
    /// when it is taken off. What it hears reaches the dock's thread as a widget's news does (Post).
    /// </summary>
    private void FollowHotspot()
    {
        if (ShowsHotspot == hotspot is not null)
        {
            return;
        }
        if (hotspot is { } gone)
        {
            hotspot = null;
            hotspotNews = null;
            gone.Dispose();
            return;
        }
        var news = new Changes(_ =>
        {
            Volatile.Write(ref hotspotHeard, 1);
            Post();
        });
        var made = QuickPageTile.SourcesNow.Hotspot();
        (hotspot, hotspotNews) = (made, news);
        made.Start(news);
    }

    /// <summary>
    /// drives: how often a removable face reads what is plugged in by itself — a minute, as the
    /// removable-devices button did. A device arriving or going sends WM_DEVICECHANGE, which the
    /// dock answers by refreshing every island; the tick is the safety net for what Windows does not
    /// announce, a card slipped into a reader.
    /// </summary>
    private const int DevicesEvery = 60_000;

    // drives: the removable devices plugged in now, for a removable face, and the DriveWatch.Changes
    // they were read at.
    private List<Drive> devices = [];
    private int devicesAt = -1;

    /// <summary>
    /// Tests only, on the thread that makes the folder: the devices a removable face shows, instead
    /// of what is plugged into the PC, so a picture of it is the same on every machine.
    /// </summary>
    [ThreadStatic]
    internal static IReadOnlyList<Drive>? DevicesStand;

    private readonly IReadOnlyList<Drive>? devicesStand = DevicesStand;

    /// <summary>drives: whether it wears the removable face, which shows what is plugged in.</summary>
    internal bool ShowsDevices => options.Wears(FolderFaces.Removable);

    /// <summary>
    /// drives: the removable devices plugged in now, for the removable face; read again only when a
    /// device came or went since (the dock forgets the drives then, DriveWatch.Changes) and on the
    /// face's tick. Empty for another face.
    /// </summary>
    internal IReadOnlyList<Drive> Devices
    {
        get
        {
            if (!ShowsDevices)
            {
                return [];
            }
            if (devicesStand is { } stand)
            {
                return stand;
            }
            if (devicesAt != DriveWatch.Changes)
            {
                ReadDevices();
            }
            return devices;
        }
    }

    private void ReadDevices()
    {
        devicesAt = DriveWatch.Changes;
        devices = RemovableIsland.Plugged();
    }

    // weather: the place the face shows the weather for (FolderFace.Weather), while it is the
    // weather face and has one set; and the forecast the store holds for it.
    private WeatherOptions? weatherPlace;
    private (WeatherCache? Shown, string? Problem) weather;

    /// <summary>
    /// Tests only, on the thread that makes the folder: the forecast a weather face shows, instead of
    /// whatever the store has, so a picture of it is the same on every machine and nothing is fetched.
    /// </summary>
    [ThreadStatic]
    internal static WeatherCache? WeatherStand;

    private readonly WeatherCache? weatherStand = WeatherStand ?? (Samples.On ? Samples.Weather() : null);

    /// <summary>weather: the forecast the weather face shows, and why a newer one is not there; null for none.</summary>
    internal (WeatherCache? Shown, string? Problem)? Weather =>
        weatherStand is { } stand ? (stand, null) : weatherPlace is null ? null : weather;

    /// <summary>What the face showed when it was last painted (<see cref="IFaceDrawing.Reading"/>).</summary>
    private string faceShown = "";

    /// <summary>Tests only: what the face showed when it was last painted.</summary>
    internal string FaceShown => faceShown;

    // quick: what Windows' network, Bluetooth and volume icons show now (SystemStatus), for a Wi-Fi,
    // a Bluetooth or a sound face; the dock hands it over as it changes (Dock.SystemChanged).
    private SystemState? status;

    /// <summary>
    /// Tests only, on the thread that makes the folder: the state a Wi-Fi, Bluetooth or sound face shows,
    /// instead of the dock's, so a picture of it is the same on every machine.
    /// </summary>
    [ThreadStatic]
    internal static SystemState? StatusStand;

    private readonly SystemState? statusStand = StatusStand;

    /// <summary>quick: the network, Bluetooth and volume state a Wi-Fi, Bluetooth or sound face shows; null before the dock has read one.</summary>
    internal SystemState? Status => statusStand ?? status;

    /// <summary>quick: whether its face shows the network, Bluetooth or volume state, which the dock then keeps reading.</summary>
    internal bool ShowsStatus => options.Wears(FolderFaces.Wifi) || options.Wears(FolderFaces.Bluetooth) || Sounds;

    /// <summary>quick: whether it wears the sound face, whose wheel and middle click are the volume's.</summary>
    internal bool Sounds => options.Wears(FolderFaces.Sound);

    // quick: the sound face's way to the output, once the wheel or a middle click has asked for it,
    // so a bar nobody turns the wheel over opens nothing of Core Audio's for it.
    private VolumeWatch? volume;

    /// <summary>
    /// quick: the wheel over the sound face, as over Windows 11's volume icon and the sound button
    /// that was: two a notch, as Windows' own slider steps, held between 0 and 100; the tick for the
    /// level it settled on, as the slider's notch plays. The other faces take no wheel.
    /// </summary>
    public bool Wheel(int delta)
    {
        if (Output() is not { } output)
        {
            return false;
        }
        var notches = delta / 120;
        if (notches == 0)
        {
            notches = delta > 0 ? 1 : -1;
        }
        var level = Math.Clamp(output.Level + (notches * 2), 0, 100);
        if (level != output.Level)
        {
            output.SetLevel(level);
            // One of a run of notches: the tick waits for the wheel to rest (Feedback.Notch).
            Feedback.Notch(output.Level, output.Muted);
        }
        return true;
    }

    /// <summary>quick: a middle click on the sound face mutes the output, and unmutes it; on another face it is a click.</summary>
    public bool MiddleClick(int index)
    {
        if (Output() is not { } output)
        {
            return false;
        }
        output.SetMuted(!output.Muted);
        return true;
    }

    /// <summary>
    /// The default output, read now â€” the level may have moved anywhere since â€” for the sound face;
    /// null for another face and where there is no output device. Nothing to redraw when it
    /// changes: the face follows SystemStatus.
    /// </summary>
    private VolumeWatch? Output()
    {
        if (!Sounds)
        {
            return null;
        }
        volume ??= new VolumeWatch(static () => { });
        _ = volume.Read();
        return volume.Present ? volume : null;
    }

    /// <summary>quick: the VPN connections up, by name, where its face is the network's and one is: Narrator says it, as Windows' icon does.</summary>
    internal string? Vpn => options.Wears(FolderFaces.Wifi) ? Status?.Vpn : null;

    /// <summary>
    /// quick: SystemStatus read again: a Wi-Fi or Bluetooth face is painted again in place where what
    /// it shows changed, as a clock's is on its tick (<see cref="FaceTick"/>).
    /// </summary>
    public bool SetStatus(SystemState value, Gfx gfx)
    {
        if (status == value)
        {
            return false;
        }
        status = value;
        return FaceTick(gfx);
    }

    /// <summary>
    /// <paramref name="face"/>, one of its faces, measured and drawn with the folder's settings and this island.
    /// </summary>
    private FaceContext FaceIn(in IslandStyle style, FolderFace face) => new(Gfx, style, options, this, Worn: face);

    /// <summary>Whether a face of it changes every second: the dock's clock then ticks every second.</summary>
    public bool FaceSeconds => options.Faces.Any(face => FaceDrawings.Of(face).Seconds(face));

    /// <summary>What its faces show now, the one's own reading where it wears one; several, each's in their order.</summary>
    private string Readings(in IslandStyle style)
    {
        if (options.Faces.Count == 1)
        {
            return FaceDrawings.Of(options.Face).Reading(FaceIn(style, options.Face));
        }
        var readings = new string[options.Faces.Count];
        for (var index = 0; index < readings.Length; index++)
        {
            var face = options.Faces[index];
            readings[index] = FaceDrawings.Of(face).Reading(FaceIn(style, face));
        }
        return string.Join('\u001F', readings);
    }

    /// <summary>
    /// The dock's clock ticked (a new minute, a second, a countdown that moved, the time changed):
    /// the faces are painted again in place where what one shows has changed. True only where the
    /// button now wants another size than the bar was laid out for (a face is as wide as what it
    /// shows now, docs\widgets.md §244): the dock then lays the bar out again.
    /// </summary>
    public bool FaceTick(Gfx gfx) => Readings(default) != faceShown && Redraw(gfx);

    /// <summary>A new minute: a clock or a date on its face follows it.</summary>
    public override bool MinuteChanged(Gfx gfx) => FaceTick(gfx);

    /// <summary>
    /// calendar: whether <paramref name="folder"/> holds a timer or a focus session: a clock face then
    /// keeps the room for what one has left. Its settings, never whether one is running.
    /// </summary>
    internal static bool HoldsCountdown(FolderOptions folder) =>
        folder.Cells.Any(cell => cell?.Island?.Info is { } info && (info == WidgetInfos.Timer || info == WidgetInfos.Focus || info == WidgetInfos.Stopwatch));

    /// <summary>
    /// calendar: what a timer or a focus session in it has left, five figures ("04:59"), or what a
    /// stopwatch in it has counted; empty where none is set going. What moves before what stands
    /// still, and a countdown before a stopwatch, since a countdown is a time somebody has to meet:
    /// a running countdown, then a running stopwatch, then a paused or finished countdown, then a
    /// paused stopwatch.
    /// </summary>
    internal string CountdownLeft()
    {
        if (countdowns is null)
        {
            return "";
        }
        var now = Clock.Now();
        var counted = StopwatchClock.Now();
        var (left, running, paused) = ("", "", "");
        foreach (var cell in options.Cells)
        {
            if (cell?.Island is not { } inside || inside.Info is not { } info)
            {
                continue;
            }
            if (info == WidgetInfos.Stopwatch)
            {
                var stopwatch = countdowns.Stopwatches.Of(inside.Id);
                if (stopwatch.Running && running.Length == 0)
                {
                    running = StopwatchText.Cell(stopwatch.Elapsed(counted));
                }
                else if (stopwatch.Phase == StopwatchPhase.Paused && paused.Length == 0)
                {
                    paused = StopwatchText.Cell(stopwatch.Elapsed(counted));
                }
                continue;
            }
            if (info != WidgetInfos.Timer && info != WidgetInfos.Focus)
            {
                continue;
            }
            var countdown = countdowns.Of(inside.Id);
            if (countdown.Phase == CountdownPhase.Running)
            {
                return countdown.Cell(now);
            }
            if (left.Length == 0)
            {
                left = countdown.Cell(now);
            }
        }
        return running.Length > 0 ? running : left.Length > 0 ? left : paused;
    }

    /// <summary>calendar: the dock's countdowns and stopwatches, which a timer, focus or stopwatch face reads; null for none.</summary>
    internal Countdowns? Countdowns => countdowns;

    /// <summary>
    /// calendar: the line a timer, focus or stopwatch face shows (<see cref="FolderFaces.LineOf"/>):
    /// the first of its kind in <paramref name="folder"/>, whose countdown or stopwatch the face
    /// reads in the dock's store — one per widget, so the face and the line are one timer, as a clock
    /// face reads the timers in its folder. Null for another face, or where the folder holds none.
    /// </summary>
    internal static CellItem? FaceLine(FolderOptions folder) => FaceLine(folder, ActingFace(folder) ?? folder.Face);

    /// <summary>calendar: the line the timer, focus or stopwatch face <paramref name="face"/> of <paramref name="folder"/> shows.</summary>
    internal static CellItem? FaceLine(FolderOptions folder, FolderFace face) =>
        FolderFaces.LineOf(face.Info) is { } info
            ? folder.Cells.Select(cell => cell?.Island).FirstOrDefault(inside => inside?.Info == info)
            : null;

    /// <summary>calendar: the line its acting face shows (<see cref="FaceLine(FolderOptions)"/>), as the config has it now.</summary>
    internal CellItem? TimeLine => FaceLine(options);

    /// <summary>
    /// The face of <paramref name="folder"/> whose widget <see cref="FaceWidget"/> is: its first that
    /// has one of its own — the
    /// sound's, the hotspot's, the battery's, the mail's, now playing's, a reading's, the markets',
    /// the news', a drive's, the bell's, the desktops', or a timer's, focus' or stopwatch's that has its line. Null
    /// where none of its faces has rows of its own.
    /// </summary>
    internal static FolderFace? ActingFace(FolderOptions folder) =>
        folder.Faces.FirstOrDefault(face => face.Info is var info
            && (info == FolderFaces.Sound || info == FolderFaces.Hotspot || info == FolderFaces.Battery || info == FolderFaces.Mail
                || info == FolderFaces.NowPlaying || PerformanceFace.MetricOf(info) is not null || info == FolderFaces.Markets
                || info == FolderFaces.News || info == FolderFaces.Sports || FolderFaces.DriveOf(info) is not null || info == FolderFaces.Notifications || info == FolderFaces.Desktops
                || (FolderFaces.LineOf(info) is not null && FaceLine(folder, face) is not null)));

    /// <summary>
    /// calendar: the island of the line its face shows — what the face's menu works (its start
    /// button, its lap, its reset), as the line's own menu does in the open folder; the folder
    /// itself for the mail and the markets faces, whose Refresh is their own store's; the feed line a
    /// news face shows, whose Refresh and site are the line's. Null where there is none.
    /// </summary>
    internal IIsland? FaceWidget =>
        ActingFace(options) is not { } face ? null
        : face.Info == FolderFaces.Mail || face.Info == FolderFaces.Notifications || face.Info == FolderFaces.Markets || face.Info == FolderFaces.Sports ? this
        : face.Info == FolderFaces.NowPlaying ? NowPlaying
        : face.Info == FolderFaces.News ? Feed
        : FolderFaces.DriveOf(face.Info) is { } drive ? DriveLineOf(drive)
        : FaceLine(options, face) is { } line ? widgets.GetValueOrDefault($"{Key}/{line.Id}")
        : null;

    /// <summary>The folder's list and name as the config has them now.</summary>
    public FolderOptions Options => options;

    /// <summary>The widgets, each with its cells, in the config's order; a page's where one stands open (FolderIsland.Pages.cs).</summary>
    public IReadOnlyList<FolderTile> Tiles => page?.Tiles ?? tiles;

    /// <summary>
    /// Whether no widget of it shows at rest: none in it, or every line gone while it has nothing to
    /// show (<see cref="CellIsland.HiddenAtRest"/>, §327). At rest it then does not open
    /// (OneFolder.Opens, §279), and a screen reader hears it is empty (<see cref="Tooltip"/>).
    /// </summary>
    internal bool Empty => tiles.All(tile => tile.Island is CellIsland { HiddenAtRest: true });

    /// <summary>
    /// The lines of <see cref="Tiles"/> that leave the folder at rest now (<see cref="CellIsland.HiddenAtRest"/>,
    /// §327), by their index: what the open folder lays out without at rest (FolderFlyout.Build's <c>resting</c>).
    /// </summary>
    internal IReadOnlyList<int> Resting =>
        [.. Tiles.Select((tile, index) => (tile, index)).Where(each => each.tile.Island is CellIsland { HiddenAtRest: true }).Select(each => each.index)];

    /// <summary>
    /// Its click at rest opens it, but for an empty one, which does not open (OneFolder.Opens): no press shows on it either
    /// (§287). One of Windows' own widgets holds nothing and always opens Windows' menu (§474).
    /// </summary>
    internal override bool ClickDoes(nint screen) => (!IsFolder || OneFolder.Opens(Empty, arranging: false)) && base.ClickDoes(screen);

    /// <summary>Whether the flyout shows the folder now: the widgets tick only then.</summary>
    public bool Showing { get; private set; }

    /// <summary>
    /// pins: what the pins in it have open, so the folder's button shows a running indicator of its
    /// own, as a pin group's tile did, and their apps' badges together (<see cref="PinBadge"/>). The
    /// dock sets it (Dock.Pins.cs).
    /// </summary>
    public Mark Running { get; private set; }

    /// <summary>pins: the badges its pins show now, together (<see cref="Badge.Together"/>); null for none.</summary>
    public Badge? PinBadge => Badge.Together(widgets.Values.OfType<PinTile>().Select(pin => pin.Mark.Signal.Badge));

    /// <summary>
    /// calendar: a timer or a focus session in it ran out while it was shut. Nothing in a folder opens
    /// a popup (docs\widgets.md §15), so the folder's button wears a mark — an accent dot on its
    /// corner — until it is opened and the card that rang is seen. The dock sets it (Dock.Countdowns.cs).
    /// </summary>
    public bool Due { get; private set; }

    /// <summary>calendar: sets or clears <see cref="Due"/>; true when it changed.</summary>
    public bool SetDue(bool due)
    {
        if (Due == due)
        {
            return false;
        }
        Due = due;
        Redraw(Gfx);
        return true;
    }

    /// <summary>calendar: whether the widget <paramref name="id"/> is one of its own.</summary>
    public bool Holds(string id) =>
        options.Cells.Any(cell => cell?.Island is { } inside && string.Equals(inside.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>pins: the folder's own running indicator; true when it changed.</summary>
    public bool SetRunning(Mark mark)
    {
        if (Running == mark)
        {
            return false;
        }
        Running = mark;
        Redraw(Gfx);
        return true;
    }

    /// <summary>
    /// pins: the running indicator of every pin in the folder, from <paramref name="markOf"/>. An
    /// open flyout draws them again when they changed.
    /// </summary>
    public void SetMarks(Func<Item, Mark> markOf)
    {
        var changed = false;
        foreach (var pin in widgets.Values.OfType<PinTile>())
        {
            var mark = markOf(pin.Item);
            changed |= pin.Mark != mark;
            pin.Mark = mark;
        }
        if (changed && Showing)
        {
            Changed?.Invoke();
        }
    }

    public override void Configure(WidgetItem island, Config config)
    {
        // The shape is a function of the settings, so the shape must be given the
        // settings just handed over: keeping the ones this island was born with
        // left the size measured for the old options while the drawing used the
        // new ones (a folder whose name was switched on kept its square).
        Settings = island;
        options = FacesOf(island, () => probe() is not null);
        Shown = LayoutShown.Of(config, Islands.OwnerOf(Key));
        lastConfig = config;
        var alive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // An open page's lines are its own, not the config's (FolderIsland.Pages.cs).
        foreach (var key in widgets.Keys.Where(key => key.StartsWith($"{Key}/page/", StringComparison.OrdinalIgnoreCase)))
        {
            alive.Add(key);
        }
        var next = new List<FolderTile>();
        foreach (var cell in options.Cells)
        {
            if (cell?.Island is { } inside && Placed(inside) is { } placed)
            {
                next.Add(new FolderTile(placed, cell));
            }
        }
        // §477: a face's own source is read through a line of the face's own, kept and made as a line of the
        // folder is, but no line of it: the news face's own feed, a drive face's own drive.
        foreach (var face in options.Faces)
        {
            if (FaceSources.Own(face) is { } own)
            {
                _ = Placed(own);
            }
        }
        foreach (var gone in widgets.Keys.Where(key => !alive.Contains(key)).ToList())
        {
            Drop(gone, widgets[gone]);
        }
        tiles = next;
        PageConfigured();

        // The widget of <inside>, kept and given its options where it is there, else made: attached and shown as the folder is.
        IIsland? Placed(CellItem inside)
        {
            var key = $"{Key}/{inside.Id}";
            alive.Add(key);
            // §635: a ghost's line put in, made again: it showed the samples, the line placed reads the person's own.
            if (widgets.TryGetValue(key, out var kept) && kept.Info == inside.Info && !Samples.Outlived(kept, key))
            {
                kept.Configure(inside, config);
                return kept;
            }
            if (kept is not null)
            {
                Drop(key, kept);
            }
            IIsland? made = null;
            try
            {
                made = Make(key, inside, config);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: a folder's {inside.Kind} not made: {error.Message}");
            }
            if (made is null)
            {
                return null;
            }
            widgets[key] = made;
            // folders: what the widgets in it agree about is the folder's (IWidgetContainer).
            if (made is CellIsland held)
            {
                held.Container = this;
            }
            if (attached)
            {
                made.Attach(dock, token);
            }
            if (Showing)
            {
                made.Visible(true);
            }
            return made;
        }
        // weather: the face follows its own place, as each weather line follows its own.
        Watch(options.Wears(FolderFaces.Weather) ? WeatherPlaces.OfFace(options) : null);
        // air: the air-quality face follows its own place, as the weather face does (§210).
        FollowAir();
        // tides: the tides face follows its own place, as the air-quality face does (§236).
        FollowTides();
        // sports: the sports face follows its first sports line's team (§239).
        FollowSports();
        // quakes: and the earthquake face, the store's one feed (§240).
        FollowQuakes();
        // markets: the face follows what it tracks of its own, whatever the folder's markets lines show (§246).
        FollowMarket();
        FollowHotspot();
        FollowCastNearby();
        FollowMail();
        FollowMeetings();
        FollowNotifications();
        FollowClipboard();
        FollowNowPlaying(config);
        FollowKeyboard(config);
        FollowSwitch();
        FollowRadios();
        FollowPerformance();
        if (ShowsBattery)
        {
            battery = probe();
        }
        Forget();
        Changed?.Invoke();
    }

    /// <summary>
    /// weather: the weather face follows <paramref name="place"/> — registered with the store, asked
    /// for where it is due, and read — or nothing, where the face is another or has no place set.
    /// </summary>
    private void Watch(WeatherOptions? place)
    {
        var next = place is null ? null : WeatherPlaces.Settled(place);
        if (next is not null && weatherPlace is not null && WeatherStore.KeyOf(next) == WeatherStore.KeyOf(weatherPlace))
        {
            return;
        }
        weatherPlace = next;
        if (weatherStand is not null)
        {
            // A test's forecast: nothing reaches the store.
            return;
        }
        if (next is null)
        {
            WeatherStore.Shared.Release(this);
            weather = default;
            return;
        }
        WeatherStore.Shared.Use(this, next);
        WeatherStore.Shared.Poll(this);
        var (shown, problem, _) = WeatherStore.Shared.Read(this);
        weather = (shown, problem);
    }

    /// <summary>
    /// A widget of this folder, made by its view's factory: its line in a list, and nothing for a
    /// widget the list does not hold.
    /// </summary>
    private IIsland? Make(string key, CellItem inside, Config config) =>
        ListWidgets.Find(inside.Kind) is { } line ? Islands.CreateLine(key, inside, line.Kind, Gfx, config, countdowns) : null;

    /// <summary>
    /// folders: what <paramref name="channel"/> holds while it is open. The folder knows nothing
    /// about what the value means — the channel is the registry's (Channels).
    /// </summary>
    T? IWidgetContainer.Read<T>(Channel<T> channel) => shared.Of(channel);

    /// <summary>
    /// folders: one of them published. The folder draws again — it is what draws them, so it is what
    /// knows when to — and a widget never asks to be redrawn itself.
    /// </summary>
    void IWidgetContainer.Publish<T>(Channel<T> channel, T value)
    {
        if (shared.Set(channel, value) && Showing)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>folders: one of them took a value back (<see cref="IWidgetContainer.Withdraw"/>); drawn again where that changed anything.</summary>
    void IWidgetContainer.Withdraw(Channel channel)
    {
        if (shared.Unset(channel) && Showing)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>notes: one of them changed by the person's hand: drawn again now, while it shows.</summary>
    void IWidgetContainer.Redraw()
    {
        if (Showing)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Escape in the open folder, once no line is typing (that Escape ends the typing): the choices
    /// made in it are taken back first (<see cref="Channel.Escapes"/>, an app picked), and true where
    /// there was one, when the folder stays open; false where there was none, when Escape closes it as ever.
    /// </summary>
    internal bool Escape()
    {
        // A page goes back to the folder's lines first, as Escape does on Windows' own.
        if (ClosePage())
        {
            return true;
        }
        if (!shared.Escape())
        {
            return false;
        }
        if (Showing)
        {
            Changed?.Invoke();
        }
        return true;
    }

    /// <summary>What <paramref name="channel"/> holds now, for a test; null when nobody has published.</summary>
    internal T? ChannelValue<T>(Channel<T> channel)
        where T : struct => shared.Of(channel);

    /// <summary>The widget keyed <paramref name="key"/>, or null.</summary>
    public IIsland? Widget(string key) => widgets.GetValueOrDefault(key);

    void IIsland.Attach(nint dock, nuint token)
    {
        Attach(dock, token);
        // The widgets post with the folder's token: the dock refreshes the folder, which takes their news.
        this.dock = dock;
        this.token = token;
        attached = true;
        foreach (var widget in widgets.Values)
        {
            widget.Attach(dock, token);
        }
        ((IIsland?)nowPlaying)?.Attach(dock, token);
        ((IIsland?)keyboard)?.Attach(dock, token);
    }

    bool IIsland.Refresh(Gfx gfx)
    {
        PageNews();
        foreach (var widget in widgets.Values)
        {
            // The drives are read on the flyout's ticks and when it opens: a refresh after
            // another widget's news (a song changing) would read every drive's label again.
            if (widget is DriveIsland)
            {
                continue;
            }
            Guard(widget, () => widget.Refresh(gfx));
        }
        // quick: the now-playing face's own widget takes its news, and the face is painted after.
        if (nowPlaying is { } playing)
        {
            Guard(playing, () => ((IIsland)playing).Refresh(gfx));
        }
        if (Showing)
        {
            Changed?.Invoke();
        }
        // quick: news of a line in the open folder — the hotspot's page switched it, or asked who is
        // on it — and the hotspot face asks again too, so it follows the line at once. Not after its
        // own news, which would ask for ever.
        if (hotspot is { } sharing && Interlocked.Exchange(ref hotspotHeard, 0) == 0 && Showing)
        {
            sharing.Poll();
        }
        return Refresh(gfx);
    }

    public override void System(uint msg, WPARAM wParam, LPARAM lParam)
    {
        // power: the charger, the charge or energy saver changed (a power setting: energy saver's
        // own status, which the dock keeps before it hands the broadcast on): a battery face reads
        // the charge again at once, as the battery on the bar did.
        const uint PBT_APMPOWERSTATUSCHANGE = 0x000A;
        if (ShowsBattery && msg == WM.WM_POWERBROADCAST && (uint)(nuint)wParam is PBT_APMPOWERSTATUSCHANGE or EnergySaver.PbtPowerSettingChange)
        {
            Post();
        }
        foreach (var widget in widgets.Values)
        {
            Guard(widget, () => widget.System(msg, wParam, lParam));
        }
    }

    /// <summary>
    /// The flyout opens: the widgets are shown, read everything again and take what arrived
    /// while nobody looked (a download that was due starts now).
    /// </summary>
    public void Opened(Gfx gfx)
    {
        Showing = true;
        // calendar: what rang in it while it was shut is seen now.
        SetDue(false);
        var now = Environment.TickCount64;
        foreach (var widget in widgets.Values)
        {
            Guard(widget, () =>
            {
                widget.Visible(true);
                widget.Refresh(gfx);
                widget.Tick(gfx);
            });
            ticked[widget] = now;
        }
    }

    /// <summary>The flyout closed: the widgets stop, and nothing ticks them until it opens again.</summary>
    public void Closed()
    {
        Showing = false;
        // A page is about this look at the folder: opening it again starts at its lines.
        ClosePage(tell: false);
        DropLeaving();
        // folders: what they agreed about goes with the showing. A day somebody stepped back to is
        // about this look at the folder, not about the folder: opening it again starts at today,
        // which is what the panel it replaced did, and nothing of it reaches the file.
        shared.Clear();
        foreach (var widget in widgets.Values)
        {
            Guard(widget, () => widget.Visible(false));
        }
    }

    /// <summary>
    /// §357: where the open folder is now, before it closes (<see cref="Closed"/> forgets it): each line's
    /// scroll and its own view, by its widget's key, what the widgets agreed on (the day picked), the line
    /// whose page stands open, and the line <paramref name="focused"/> with the keys (<paramref name="keyboard"/>:
    /// ringed for them).
    /// </summary>
    internal FolderMemory Remember(string? focused, bool keyboard)
    {
        var lines = new Dictionary<string, LineMemory>(StringComparer.OrdinalIgnoreCase);
        foreach (var tile in Tiles)
        {
            if (tile.Island is CellIsland line)
            {
                object? view = null;
                Guard(line, () => view = line.ViewMemory());
                lines[line.Key] = new LineMemory(line.ListScroll, view);
            }
        }
        return new FolderMemory(lines, shared.Snapshot(), PageOf, focused, keyboard);
    }

    /// <summary>§357: the page <paramref name="memory"/> had open opened again, before the lines are laid out; nothing where it had none or the line has none now.</summary>
    internal void RecallPage(FolderMemory memory)
    {
        if (memory.Page is { } line && !string.Equals(PageOf, line, StringComparison.OrdinalIgnoreCase))
        {
            _ = OpenPageOf(line);
        }
    }

    /// <summary>
    /// §357: the opened folder as <paramref name="memory"/> left it: what the widgets agreed on, and each
    /// line there still scrolled and seen as it was. A line gone since is left out; one come since opens at its start.
    /// </summary>
    internal void Recall(FolderMemory memory)
    {
        shared.Restore(memory.Channels);
        foreach (var tile in Tiles)
        {
            if (tile.Island is CellIsland line && memory.Lines.TryGetValue(line.Key, out var kept))
            {
                line.ListScroll = kept.Scroll;
                if (kept.View is { } view)
                {
                    Guard(line, () => line.RecallView(view));
                }
            }
        }
    }

    /// <summary>The shortest of the widgets' intervals, in ms; 0 when none ticks.</summary>
    public int Interval => widgets.Values.Select(widget => widget.IntervalMs).Where(ms => ms > 0).DefaultIfEmpty(0).Min();

    /// <summary>Whether a clock in it shows seconds: the flyout then draws every second.</summary>
    public bool Seconds => widgets.Values.OfType<ClockTile>().Any(clock => clock.Seconds) || widgets.Values.OfType<TimeHeaderTile>().Any();

    /// <summary>The open flyout's timer: every widget whose interval has passed ticks.</summary>
    public void TickWidgets(Gfx gfx)
    {
        var now = Environment.TickCount64;
        foreach (var widget in widgets.Values)
        {
            var interval = widget.IntervalMs;
            if (interval <= 0 || (ticked.TryGetValue(widget, out var last) && now - last + 500 < interval))
            {
                continue;
            }
            ticked[widget] = now;
            Guard(widget, () => widget.Tick(gfx));
        }
    }

    /// <summary>A new minute while the flyout is open: the widgets that show the time or a date follow it.</summary>
    public void MinuteWidgets(Gfx gfx)
    {
        foreach (var widget in widgets.Values)
        {
            Guard(widget, () => widget.MinuteChanged(gfx));
        }
    }

    /// <summary>
    /// The folder's own click opens the folder. It used to hand over a widget's action as well —
    /// the dock would set one here and click the folder to show it, so a widget in a folder could
    /// open a list of items at the folder's cell — and that road went with the folder rule
    /// (docs\widgets.md §15): in a folder, a click on a widget opens no popup.
    /// <para>
    /// markets: a button that wears a markets face alone is that widget on the bar, and its click —
    /// anywhere on it, and Narrator's Invoke — searches what it tracks in the browser instead
    /// (<see cref="SearchMarket"/>, §263); one with no symbol yet opens the folder as before. Among other
    /// faces only a click on the markets face's own room searches (<see cref="IPointClickIsland.ClickAt"/>).
    /// </para>
    /// </summary>
    public override IslandAction Click(int index) => !IsFolder || (MarketAlone && SearchMarket()) ? default : new(View: IslandView.Folder);

    /// <summary>
    /// Each face's own words, in their order, as Windows' one button of the network, the volume and
    /// the battery says each of the three on a line of its own; empty where none says anything.
    /// </summary>
    private List<string> FaceWords() =>
        FacesOnBar
            .Select(face => FaceDrawings.Of(face) is var drawing && FaceIn(default, face) is var context
                ? drawing.Unavailable(context) ?? drawing.Tooltip(context) : null)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The tooltip over the button at <paramref name="point"/>: what Windows' status icon there says on
    /// the taskbar — the network, the sound, Bluetooth, the battery, the bell (BarTooltips.IsStatusIcon,
    /// §382) — and that icon's room, the bar's whole thickness across it, which the tooltip stands over.
    /// None over any other face, nor past the faces: a folder and its faces have none (§367).
    /// </summary>
    internal (string Text, D2D_RECT_F Box)? StatusTipAt(System.Numerics.Vector2 point, System.Numerics.Vector2 size, in IslandStyle style) =>
        options.Faces.Any(face => BarTooltips.IsStatusIcon(face.Info))
            && FaceAt(point, size, style) is { } at && BarTooltips.IsStatusIcon(at.Face.Info)
            && FaceDrawings.Of(at.Face) is var drawing && FaceIn(style, at.Face) is var context
            && (drawing.Unavailable(context) ?? drawing.Tooltip(context)) is { } text
            ? (text, at.Box)
            : null;

    /// <summary>
    /// The face of the bar's button at <paramref name="point"/>, and its room: its box along the bar
    /// (<see cref="FaceBoxes"/>) and the bar's whole thickness across it. Null past the faces.
    /// </summary>
    private (FolderFace Face, D2D_RECT_F Box)? FaceAt(System.Numerics.Vector2 point, System.Numerics.Vector2 size, in IslandStyle style)
    {
        var shown = FacesOnBar;
        var boxes = FaceBoxes(Gfx, options, style, size.X, size.Y, shown, this);
        for (var at = 0; at < shown.Count && at < boxes.Length; at++)
        {
            var box = style.Vertical
                ? new D2D_RECT_F(0, boxes[at].top, size.X, boxes[at].bottom)
                : new D2D_RECT_F(boxes[at].left, 0, boxes[at].right, size.Y);
            if (point.X >= box.left && point.X < box.right && point.Y >= box.top && point.Y < box.bottom)
            {
                return (shown[at], box);
            }
        }
        return null;
    }

    /// <summary>
    /// What a screen reader says of its button (Dock.Uia.cs): what its faces say, else "Folder" (§427:
    /// it has no name) and what is in it — "Folder, 3 widgets". The bar shows no tooltip over it (§367).
    /// </summary>
    public override string? Tooltip(int index)
    {
        var said = FaceWords();
        // §474: one of Windows' own widgets is named as Windows' button is ("Quick settings"), what its faces say after it.
        if (!IsFolder)
        {
            return string.Join('\n', [Info.Title, .. said]);
        }
        if (said.Count > 0)
        {
            return string.Join('\n', said);
        }
        var what = Empty ? S.WidgetFolderEmpty : S.WidgetFolderWidgets(tiles.Count);
        return $"{S.CoreWidgetFolderListTitle}\n{what}";
    }

    /// <summary>The folder left the bar (its island was disposed): an open flyout of it closes.</summary>
    public bool Gone { get; private set; }

    public override void Dispose()
    {
        if (Gone)
        {
            return;
        }
        Gone = true;
        page = null;
        foreach (var (key, widget) in widgets.ToList())
        {
            Drop(key, widget);
        }
        DropLeaving();
        tiles = [];
        WeatherStore.Shared.Release(this);
        AirQualityStore.Shared.Release(this);
        TideStore.Shared.Release(this);
        // sun: a sun face waiting for this PC's position (§368).
        SunPlaces.Release(this);
        ReleaseSports();
        QuakeStore.Shared.Release(this);
        ReleaseMarket();
        volume?.Dispose();
        volume = null;
        hotspot?.Dispose();
        hotspot = null;
        hotspotNews = null;
        DropCastNearby();
        mail?.Release(this);
        mail = null;
        meetings?.Detach(this);
        meetings = null;
        notifications?.Release(this);
        notifications = null;
        clipboard?.Release(this);
        clipboard = null;
        LetQuietGo();
        nowPlaying?.Dispose();
        nowPlaying = null;
        foreach (var held in performance.Values)
        {
            performanceSources.Release(held);
        }
        performance.Clear();
        keyboard?.Dispose();
        keyboard = null;
        faceSwitch?.Dispose();
        faceSwitch = null;
        DropMenuSources();
        base.Dispose();
        Changed?.Invoke();
        Changed = null;
    }

    /// <summary>
    /// The name changes nothing the bar measures but its own text. A removable face reads what is
    /// plugged in on its tick, and is painted again in place after. A weather face reads: a tick
    /// starts a fetch that is due, and the store posting raises its news for every widget of the
    /// place and hands the face what arrived. Where a face now wants another width, the bar is laid out again (§244).
    /// </summary>
    protected override bool Sample(bool everything)
    {
        // drives: the face's own tick reads the devices again whatever the dock heard; a refresh
        // reads them where a device came or went (Devices).
        if (ShowsDevices && !everything && devicesStand is null && PaceDue("devices", DevicesEvery))
        {
            ReadDevices();
        }
        // pim: the mail store woke the folder: its news is raised now, and the face painted after.
        if (mail is { } inbox && everything)
        {
            inbox.Deliver();
        }
        // meeting: the calendars woke the folder: their news is raised now, and the face painted after.
        if (meetings is { } calendars && everything)
        {
            calendars.Deliver();
        }
        // clipboard: the store woke the folder: its news is raised now, for the line that shows it.
        if (clipboard is { } copied && everything)
        {
            copied.Deliver();
        }
        // notifications: the store woke the folder: what it read is taken now, and the face painted after.
        if (notifications is { } store && everything)
        {
            store.Deliver();
        }
        // power: the charge, read again on the face's tick and on a power broadcast (Post).
        if (ShowsBattery)
        {
            battery = probe();
        }
        // drives: the drive the face shows, read again on its tick and as the dock reads every
        // drive again (a device came or went), whether the folder is open or not.
        if (everything || PaceDue("drive", DriveEvery))
        {
            foreach (var face in options.Faces)
            {
                if (FolderFaces.DriveOf(face.Info) is { } kind)
                {
                    DriveLineOf(kind)?.Follow(everything);
                }
            }
        }
        // quick: what the hotspot face's source heard is read now (Hotspot reads it live); its own
        // tick asks Windows again, and the answer comes back as news.
        if (hotspot is { } sharing)
        {
            _ = hotspotNews?.Take();
            Volatile.Write(ref hotspotHeard, 0);
            if (!everything && PaceDue("hotspot", HotspotEvery))
            {
                sharing.Poll();
            }
        }
        SampleCastNearby(everything);
        SampleAir(everything);
        SampleTides(everything);
        SampleSports(everything);
        SampleQuakes(everything);
        SampleMarket(everything);
        if (weatherPlace is null || weatherStand is not null)
        {
            return false;
        }
        if (everything)
        {
            WeatherStore.Shared.Deliver(this);
        }
        WeatherStore.Shared.Poll(this);
        var (shown, problem, _) = WeatherStore.Shared.Read(this);
        weather = (shown, problem);
        return false;
    }

    // stage 3: the size is WidgetShapes.Folder. It was the last widget in the tree that sized
    // itself, and what it asked its own fields for are in its options, so the shape needed nothing
    // the instance had.

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        var shown = FacesOnBar;
        var sizes = FaceSizes(Gfx, options, style, faces: shown, folder: this);
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        var cell = new D2D_RECT_F(0, 0, width, height);
        dc->PushAxisAlignedClip(&cell, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            faceShown = Readings(style);
            var boxes = FaceBoxes(Gfx, options, style, width, height, shown, this);
            for (var at = 0; at < sizes.Count; at++)
            {
                var face = shown[at];
                var drawing = FaceDrawings.Of(face);
                var context = FaceIn(style, face) with { Room = cell };
                var box = boxes[at];
                // Not on this PC (§247), or on the bar only because it is arranged — with nothing to show,
                // which takes it off at rest (NothingToShow, §291, §319): drawn as it would be, greyed, as
                // Show desktop's bare corner is arranged (§302). One look for "not there now".
                if (drawing.Unavailable(context) is not null || drawing.NothingToShow(face, this))
                {
                    Unavailable.Paint(dc, style.Look, greyed => drawing.Paint(greyed, context, box));
                    continue;
                }
                drawing.Paint(dc, context, box);
            }
            PaintRunning(dc, fit, ink, width, height);
            // The mark sits on the last face's corner: a square's own, or a glyph's room at the end
            // of a face of text.
            var glyph = fit.Glyph();
            var last = boxes[^1];
            var corner = sizes[^1].Square ? last : PopupPaint.Rect(last.right - glyph, TextLine.Round((height - glyph) / 2), glyph, glyph);
            // pins: the badges of the apps in it, on the same corner's lower right, as on an app's icon.
            SignalPaint.Corner(dc, Gfx, Running.Signal, new RectF(corner.left, corner.top, corner.right - corner.left, corner.bottom - corner.top), SignalPaint.BadgeInk.Of(style.Look));
            PaintDue(dc, corner, fit, ink, width);
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>
    /// Where each face is drawn in the button's cell <paramref name="width"/> by <paramref name="height"/>
    /// px: one after another along the bar inside the button's ends (<see cref="Length"/>), each
    /// face's <see cref="FaceSize.Trail"/> kept after it. Across a horizontal bar left to right, a
    /// square as a glyph is in the middle of the bar's thickness and anything else the cell's whole
    /// height; down a bar on its side top to bottom, a square in the middle of the bar's width and
    /// anything else across it. The faces stand in the middle of the button without the last one's
    /// trail: a lone envelope is the button's middle and its count stands out over the button's end,
    /// which holds it (GlyphBadge.Place), as an app's badge stands on its icon — centred with its
    /// trail, the envelope stood off to one side (2026-11-07). Static, as <see cref="Along"/>: the tests hold a face's count inside the cell by it.
    /// <paramref name="faces"/>: the faces on the bar now where some have gone from it (<see cref="FacesOnBar"/>); all where null.
    /// </summary>
    internal static D2D_RECT_F[] FaceBoxes(Gfx gfx, FolderOptions options, in IslandStyle style, float width, float height,
        IReadOnlyList<FolderFace>? faces = null, FolderIsland? folder = null)
    {
        var fit = new Fit(style);
        var sizes = FaceSizes(gfx, options, style, faces: faces, folder: folder);
        var along = Along(gfx, options, style, faces: faces, folder: folder);
        var between = Between(fit);
        var boxes = new D2D_RECT_F[sizes.Count];
        if (style.Vertical)
        {
            var top = TextLine.Round((height - along) / 2);
            for (var at = 0; at < sizes.Count; at++)
            {
                var (length, square, trail) = sizes[at];
                boxes[at] = square
                    ? PopupPaint.Rect(TextLine.Round((width - length) / 2), top, length, length)
                    : PopupPaint.Rect(0, top, width, length);
                top += length + trail + between;
            }
            return boxes;
        }
        var left = TextLine.Round((width - (along - (sizes.Count > 0 ? sizes[^1].Trail : 0))) / 2);
        for (var at = 0; at < sizes.Count; at++)
        {
            var (length, square, trail) = sizes[at];
            boxes[at] = square
                ? PopupPaint.Rect(left, TextLine.Round((height - length) / 2), length, length)
                : PopupPaint.Rect(left, 0, length, height);
            left += length + trail + between;
        }
        return boxes;
    }

    /// <summary>
    /// calendar: the mark of a countdown that ran out in it while it was shut (<see cref="Due"/>): an
    /// accent dot on the glyph's top-right corner, inside the cell, as a badge stands on a glyph.
    /// </summary>
    private void PaintDue(ID2D1DeviceContext* dc, D2D_RECT_F square, in Fit fit, in IslandInk ink, float width)
    {
        if (!Due)
        {
            return;
        }
        var dot = MathF.Max(4, TextLine.Round(fit.Px(7)));
        var left = MathF.Min(square.right - (dot * 0.6f), width - dot - 1);
        var top = MathF.Max(1, square.top - (dot * 0.4f));
        PopupPaint.Fill(dc, PopupPaint.Rect(left, top, dot, dot), dot / 2, ink.Accent);
    }

    /// <summary>pins: what the pins in the folder have open, as a pinned app's own indicator.</summary>
    private void PaintRunning(ID2D1DeviceContext* dc, in Fit fit, in IslandInk ink, float width, float height)
    {
        if (Running.Count <= 0)
        {
            return;
        }
        var thickness = MathF.Max(2, TextLine.Round(3 * fit.Scale));
        var length = Running.Active ? fit.Px(16) : Running.Count > 1 ? fit.Px(10) : fit.Px(6);
        PopupPaint.Fill(
            dc,
            PopupPaint.Rect((width - length) / 2, height - thickness - MathF.Max(1, fit.Px(1)), length, thickness),
            thickness / 2,
            Running.Active ? ink.Accent : ink.Secondary);
    }

    /// <summary>
    /// Each of its faces' length along the bar and whether it is a glyph's square
    /// (<see cref="IFaceDrawing.Measure"/>), in their order: from what <paramref name="folder"/>'s faces
    /// show now (§244), or the settings alone where it is null, as <see cref="Parts"/>, which adds them up.
    /// </summary>
    internal static IReadOnlyList<FaceSize> FaceSizes(Gfx gfx, FolderOptions options, in IslandStyle style, WidgetMachine? machine = null,
        IReadOnlyList<FolderFace>? faces = null, FolderIsland? folder = null)
    {
        faces ??= options.Faces;
        var sizes = new FaceSize[faces.Count];
        for (var at = 0; at < sizes.Length; at++)
        {
            var face = faces[at];
            sizes[at] = FaceDrawings.Of(face).Measure(new FaceContext(gfx, style, options, folder, Machine: machine, Worn: face));
        }
        return sizes;
    }

    /// <summary>§506: the room between two faces side by side on one button, in DIP.</summary>
    private const float FaceGapDip = 5;

    /// <summary>§506: the least room a button keeps at each end, before its first face and after its last, in DIP.</summary>
    private const float EndsDip = 8;

    /// <summary>
    /// The room between two faces side by side (§506): 5 DIP, so the faces on one button read as one
    /// button — closer than two buttons' plates stand (Layout.WidgetGap, 6 DIP).
    /// </summary>
    internal static float Between(in Fit fit) => fit.Px(FaceGapDip);

    /// <summary>
    /// The room the button keeps at each end along the bar, before its first face and after its last:
    /// the room every text widget keeps (<see cref="Fit.Edge"/>), never under 8 DIP (§506: the faces
    /// stand inside their plate with room round them), across a bar and down one alike.
    /// </summary>
    internal static float Ends(in Fit fit) => MathF.Max(fit.Edge, fit.Px(EndsDip));

    /// <summary>
    /// The button's length along the bar (WidgetShapes.Folder): its faces side by side
    /// (<see cref="Along"/>) and its ends — exactly that, for a lone glyph as for many faces of words
    /// (docs\widgets.md §275). No name after them: a folder has none (§427).
    /// </summary>
    internal static float Length(Gfx gfx, FolderOptions options, in IslandStyle style, WidgetMachine? machine = null,
        IReadOnlyList<FolderFace>? faces = null, FolderIsland? folder = null) =>
        (2 * Ends(new Fit(style))) + Along(gfx, options, style, machine, faces, folder);

    /// <summary>
    /// The button's faces' length along the bar, side by side with the room between them and each
    /// face's <see cref="FaceSize.Trail"/> after it (<see cref="IFaceDrawing.Measure"/>). Static, because
    /// this is what the widget's shape is made of (<see cref="Length"/>) and the painting has to use the
    /// very same numbers; <paramref name="folder"/>
    /// is the folder whose faces say what they show now (§244), null for its settings alone. It never
    /// counts the folder's widgets: a folder with one widget is the same width as one with nine.
    /// </summary>
    internal static float Along(
        Gfx gfx, FolderOptions options, in IslandStyle style, WidgetMachine? machine = null, IReadOnlyList<FolderFace>? faces = null, FolderIsland? folder = null)
    {
        var fit = new Fit(style);
        var sizes = FaceSizes(gfx, options, style, machine, faces, folder);
        // Each face's trail is kept after it, a lone one's too: no cell's room about it holds its count.
        return sizes.Sum(size => size.Along + size.Trail) + (Between(fit) * Math.Max(0, sizes.Count - 1));
    }

    private void Drop(string key, IIsland widget)
    {
        widgets.Remove(key);
        if (widget is CellIsland held)
        {
            held.Container = null;
        }
        ticked.Remove(widget);
        try
        {
            widget.Dispose();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a folder's {widget.Info.Id} not closed: {error.Message}");
        }
    }

    /// <summary>A widget that fails leaves the others and the folder running.</summary>
    private static void Guard(IIsland widget, Action work)
    {
        try
        {
            work();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a folder's {widget.Info.Id} failed: {error.Message}");
        }
    }
}
