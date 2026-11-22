// §635: the add page's ghost shows its widget full of the samples, never the person's own place or accounts, never an empty
// state (the person, 2026-11-09: "for the tides widget there is "no tidal data here". i dont want the widgets to take the
// current place for just the ghost preview, for the ghost preview they should take a place that certainly has the data to show
// available."). Every widget that shows data from far away or from the accounts — each ready-made group that holds one, its
// face and its lines, and each such list widget alone — made as the ghost's (Samples.Ghost) is drawn without a word of
// "getting…", "set up", "no data here" or "can't be reached", and asks no store of the dock's; the widget put in is made
// again and reads the person's own.
//
// §643 (the person, 2026-11-09: "also widgets like the drive one show no drive choosen yet, in preview it should just choose one
// best effort or even make one up so that the user can see how the widget looks configured."): every pick of the add page, of
// any kind, is shown set up (AddGhost.Shown, Samples.Dress) and put in as it is; every ghost, each ready-made group and each list
// widget alone, is drawn without a word of a widget not set up — no drive chosen, no folder, nothing pinned, no camera, no
// toggles, no actions, an empty note —, none wanting setting up.
//
// The drawn tests need Direct2D: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.Markets;
using Karpo.Dock.Weather;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Edit;

public sealed unsafe class GhostSamplesTests
{
    private const string Owner = "layout:ghost";

    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>The widgets that show data from far away or from the person's accounts: what has a sample.</summary>
    private static readonly HashSet<WidgetSource> DataBacked =
    [
        WidgetSource.WeatherPart, WidgetSource.WeatherHours, WidgetSource.WeatherHoursList, WidgetSource.WeatherDays, WidgetSource.WeatherDaysGraph,
        WidgetSource.AirQuality, WidgetSource.Tides, WidgetSource.Sun, WidgetSource.Quakes, WidgetSource.Sports,
        WidgetSource.Markets, WidgetSource.Rss, WidgetSource.Calendar, WidgetSource.Events, WidgetSource.CalendarView, WidgetSource.Meeting,
        WidgetSource.Mail, WidgetSource.Contacts,
    ];

    /// <summary>What a widget says with nothing to show: waiting, unset, empty, failed. None of it is ever a ghost's.</summary>
    private static readonly string[] Empty =
    [
        "dock.tides.inland", "dock.tides.loading", "dock.tides.unreadable", "dock.tides.noneAhead",
        "dock.air.loading", "dock.air.unreadable", "dock.air.pollenNoForecast",
        "dock.sun.locating", "widget.weather.locationOff", "widget.weather.loading", "widget.weather.noPlaceText",
        "dock.quakes.loading", "dock.quakes.unreadable", "dock.quakes.noneWeek", "dock.quakes.noneDay",
        "dock.sports.noTeam", "dock.sports.noTeamText", "dock.sports.loading", "dock.sports.none", "dock.sports.unreachable", "dock.sports.unreadable",
        "dock.markets.noSymbolTitle", "dock.markets.loading", "dock.markets.loadingRate", "dock.markets.unreachable", "dock.markets.unreadable", "dock.markets.notQuoted",
        "widget.markets.loading", "widget.rss.noFeed", "widget.rss.noFeedShort", "widget.rss.loading", "widget.rss.nothing", "dock.rss.unreachable", "dock.rss.unreadable",
        "dock.calendar.noEvents", "dock.calendar.loading", "dock.calendar.noCalendars", "widget.calendar.nothingComing",
        "dock.meeting.none", "dock.meeting.noneAhead", "dock.meeting.noCalendar",
        "dock.mail.noAccount", "dock.mail.cannotRead", "dock.mail.inboxEmpty", "dock.contacts.none", "dock.setup.noContactsAccount",
        "dock.setup.button", "dock.setup.short",
    ];

    // --- Which widgets show the samples ------------------------------------------------------------------

    /// <summary>The ghost's widget and every widget inside it show the samples; another key, a key that only starts the same, none.</summary>
    [Fact]
    public void TheGhostAndWhatIsInsideItShowTheSamples()
    {
        var was = Samples.Ghost;
        try
        {
            Samples.Ghost = null;
            Assert.False(Samples.Of($"{Owner}/w1"));
            Samples.Ghost = $"{Owner}/w1";
            Assert.True(Samples.Of($"{Owner}/w1"));
            Assert.True(Samples.Of($"{Owner}/w1/c2"));
            Assert.False(Samples.Of($"{Owner}/w10"));
            Assert.False(Samples.Of($"{Owner}/w2"));
            Assert.False(Samples.On);
            using (Samples.Show($"{Owner}/w1"))
            {
                Assert.True(Samples.On);
                using (Samples.Show($"{Owner}/w2"))
                {
                    Assert.True(Samples.On);
                }
                Assert.True(Samples.On);
            }
            Assert.False(Samples.On);
        }
        finally
        {
            Samples.Ghost = was;
        }
    }

    // --- The samples are full -----------------------------------------------------------------------------

    /// <summary>Every sample has what its widget shows, now: a forecast, pollen, a sea that turns, a week of earthquakes, a match on.</summary>
    [Fact]
    public void EveryForecastSampleIsFullNow()
    {
        var now = Clock.Now().ToUniversalTime();
        var weather = Samples.Weather().Forecast!;
        Assert.NotNull(weather.Current);
        Assert.True(weather.Hourly?.Time?.Length >= 48);
        Assert.True(weather.Daily?.Time?.Length >= 7);

        var air = Samples.Air().Answer!.Current!;
        Assert.NotNull(air.EuropeanAqi);
        Assert.True(air.BirchPollen > 0 && air.RagweedPollen > 0, "no pollen in the sample's air");

        var tides = TideReading.From(Samples.Tides(), now, null, imperial: false);
        Assert.Equal(TideState.Ready, tides.State);
        Assert.NotNull(tides.Height);
        Assert.NotNull(tides.Next);
        Assert.NotEmpty(tides.Curve);

        var quakes = Samples.Quakes();
        Assert.NotNull(quakes.Device);
        Assert.Equal(8, quakes.Quakes!.Count);
        Assert.All(quakes.Quakes, quake => Assert.InRange(now - quake.TimeUtc, TimeSpan.Zero, TimeSpan.FromDays(7)));

        var sports = Samples.Sports();
        var reading = Karpo.Dock.Sports.SportsReading.From(Samples.SportsTeam(), sports, now, TimeSpan.FromMinutes(1));
        Assert.Equal(MatchState.Live, reading.View.Match?.State);
        Assert.NotEmpty(sports.Shown!.Table);
    }

    /// <summary>A price for whatever is asked, a share's, a rate's or a coin's, over any range, there the moment it is asked; nothing fetched.</summary>
    [Fact]
    public void AMarketSampleAnswersAnythingAtOnce()
    {
        var store = Samples.Markets();
        foreach (var symbol in new[] { "MSFT", "ZZZZ", "EURCHF=X", "BTC-USD" })
        {
            foreach (var range in new[] { MarketRange.Day, MarketRange.FiveYears })
            {
                var user = new object();
                store.Use(user, MarketAsk.Of(new MarketOptions { Symbol = symbol, Range = range }));
                store.Poll(user);
                var (shown, problem, fetching) = store.Read(user);
                Assert.True(shown?.Quote is { Price: > 0, Charted: true }, $"{symbol} {range}: no price");
                Assert.Null(problem);
                Assert.False(fetching, $"{symbol} {range}: fetched");
                store.Release(user);
            }
        }
    }

    /// <summary>The accounts' samples and a feed's: meetings with one still to come today, an inbox with unread mail, five people, six headlines.</summary>
    [Fact]
    public void EveryAccountSampleIsFullNow()
    {
        var calendar = Samples.Calendar();
        var now = DateTime.Now;
        var today = calendar.EventsIn(now, DateTime.Today.AddDays(1));
        Assert.Contains(today, each => each.Start > now && each.Join is not null);

        var mail = Samples.Mail();
        Assert.True(mail.HasAccount);
        Assert.Equal(3, mail.Unread);
        Assert.Equal(5, mail.Messages.Count);

        var contacts = Samples.Contacts();
        Assert.True(contacts.HasAccount);
        Assert.Equal(5, contacts.Contacts.Count);

        var feeds = Samples.Feeds();
        var user = new object();
        feeds.Use(user, new RssOptions { Address = Samples.FeedAddress });
        feeds.Poll(user);
        var read = feeds.Read(user);
        Assert.Equal(6, read.Feed?.Items.Count);
        Assert.False(read.Fetching);
        Assert.Equal(2, read.NewCount);
        feeds.Release(user);
    }

    // --- The ghosts drawn ---------------------------------------------------------------------------------

    /// <summary>
    /// Every ready-made group that holds a widget of data from far away or the accounts, and each such list widget alone, made
    /// as the add page's ghost: its face and its lines drawn without a word of an empty state, none of them wanting setting up,
    /// every one made showing the samples; and no store of the dock's asked for anything.
    /// </summary>
    [UiFact]
    public void EveryDataBackedGhostIsDrawnFull()
    {
        var failures = new List<string>();
        var empty = Empty.Select(key => (Key: key, Text: S.Lookup(key) ?? throw new InvalidOperationException($"no text {key}"))).ToList();
        var tides = TideStore.Shared.Started;
        OnStaThread(gfx =>
        {
            var look = ListFormPictures.LookFor(dark: true);
            foreach (var preset in Presets.All.Where(preset => Presets.ItemsOn(preset, Machine).Any(item => IsDataBacked(item.Item.Kind))))
            {
                var bar = new List<BarItem>();
                var id = Presets.AddToBar(bar, new BarSlot(BarSection.End, 0), preset, Machine);
                Check(gfx, look, preset.Id, bar.Single(island => island.Id == id), face: true, failures, empty);
            }
            foreach (var line in ListWidgets.All.Where(line => DataBacked.Contains(line.Drawing)))
            {
                var holder = BarKind.FolderList.New();
                Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, holder.Folder!, line.Kind.New()));
                Check(gfx, look, line.Kind.ToString(), holder, face: false, failures, empty);
            }
        });
        Assert.Equal(tides, TideStore.Shared.Started);
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>Whether a list widget of <paramref name="kind"/> shows data from far away or the accounts.</summary>
    private static bool IsDataBacked(string kind) => ListWidgets.Find(kind) is { } line && DataBacked.Contains(line.Drawing);

    /// <summary><paramref name="holder"/> made as the add page's ghost, its face (where <paramref name="face"/>) and its lines drawn: what they say.</summary>
    private static void Check(Gfx gfx, Look look, string what, BarItem holder, bool face, List<string> failures, List<(string Key, string Text)> empty)
    {
        var key = Config.IslandKey(Owner, holder.Id);
        var was = Samples.Ghost;
        Samples.Ghost = key;
        FolderIsland? folder = null;
        try
        {
            folder = Assert.IsType<FolderIsland>(Karpo.Dock.Islands.Create(key, holder, gfx, new Config()));
            folder.Opened(gfx);
            var said = new List<string>();
            PaintWatch.Said = (text, _) => said.Add(text);
            try
            {
                if (face)
                {
                    var style = new IslandStyle(1f, false, 48, 24, look);
                    var (width, height) = folder.CellSize(0, style);
                    // §643: a face with nothing to show at rest (now playing, nothing playing) is none to draw.
                    if (width > 0 && folder.FacesOnBar.Count > 0)
                    {
                        Bitmap(width, height, dc => folder.PaintCell(dc, 0, style, width, height), _ => { });
                    }
                }
                var (_, plan) = ListFormPictures.Plan(folder, look, 1f);
                var tileStyle = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
                for (var at = 0; at < folder.Tiles.Count; at++)
                {
                    var line = (CellIsland)folder.Tiles[at].Island;
                    if (!line.Sampled)
                    {
                        failures.Add($"{what}: {line.Info.Id} not made showing the samples");
                    }
                    if (line.Unset is { } need)
                    {
                        failures.Add($"{what}: {line.Info.Id} wants setting up ({need})");
                    }
                    // §643: a list's own row (a power button) is the folder's to draw: its glyph and its name, no state of its own.
                    if (ListWidgets.Find(folder.Tiles[at].Cell.Island.Kind)?.Line == ListLine.Row)
                    {
                        continue;
                    }
                    var card = plan.Cards[at];
                    var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                    Bitmap(box.right, box.bottom, dc => line.PaintOn(dc, box, tileStyle, WidgetSurface.List), _ => { });
                }
            }
            finally
            {
                PaintWatch.Said = null;
            }
            if (!folder.Sampled)
            {
                failures.Add($"{what}: not made showing the samples");
            }
            foreach (var text in said)
            {
                foreach (var (name, words) in empty)
                {
                    var head = words.TrimEnd('…', '.');
                    head = head[..Math.Min(head.Length, 14)];
                    if (text.StartsWith(head, StringComparison.Ordinal))
                    {
                        failures.Add($"{what}: says \"{text}\" ({name})");
                    }
                }
            }
        }
        finally
        {
            folder?.Dispose();
            Samples.Ghost = was;
        }
    }

    // --- The ghost put in ---------------------------------------------------------------------------------

    /// <summary>
    /// The ghost put in reads the person's own: the tides' ready-made group made as the ghost shows the samples' sea; the ghost
    /// gone, it is made again (Samples.Outlived), and made so, its face and its lines read the store, not the samples. A tides
    /// line a ghost in an open folder is made again as the folder is configured once it is put in.
    /// </summary>
    [UiFact]
    public void TheGhostPutInIsMadeAgainAndReadsThePersonsOwn() => OnStaThread(gfx =>
    {
        var was = Samples.Ghost;
        try
        {
            var bar = new List<BarItem>();
            var id = Presets.AddToBar(bar, new BarSlot(BarSection.End, 0), Presets.Find(Presets.TidesId)!, Machine);
            var holder = bar.Single(island => island.Id == id);
            var key = Config.IslandKey(Owner, holder.Id);

            Samples.Ghost = key;
            using (var ghost = Assert.IsType<FolderIsland>(Karpo.Dock.Islands.Create(key, holder, gfx, new Config())))
            {
                Assert.True(ghost.Sampled);
                Assert.NotNull(ghost.Tides?.Shown);
                var line = Assert.IsType<TidesTile>(ghost.Tiles[0].Island);
                Assert.True(line.Sampled);
                Assert.NotNull(line.Source);
                Assert.False(Samples.Outlived(ghost, key));

                // Put in: a ghost no more, so made again.
                Samples.Ghost = null;
                Assert.True(Samples.Outlived(ghost, key));
            }
            using (var placed = Assert.IsType<FolderIsland>(Karpo.Dock.Islands.Create(key, holder, gfx, new Config())))
            {
                Assert.False(placed.Sampled);
                Assert.False(Samples.Outlived(placed, key));
                // The store's, which in a test answers nothing: not the samples' sea.
                Assert.Null(placed.Tides?.Shown);
                var line = Assert.IsType<TidesTile>(placed.Tiles[0].Island);
                Assert.False(line.Sampled);
                Assert.Null(line.Source);
            }

            // A ghost line in an open folder: made showing the samples, and made again once it is put in.
            var folderKey = Config.IslandKey(Owner, holder.Id);
            using var folder = Assert.IsType<FolderIsland>(Karpo.Dock.Islands.Create(folderKey, holder, gfx, new Config()));
            var tides = ListKind.Tides.New();
            Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, holder.Folder!, tides));
            Samples.Ghost = $"{folderKey}/{tides.Id}";
            folder.Configure(holder, new Config());
            var ghostLine = folder.Tiles.Single(tile => tile.Cell.Island.Id == tides.Id).Island;
            Assert.True(((CellIsland)ghostLine).Sampled);
            Samples.Ghost = null;
            folder.Configure(holder, new Config());
            var placedLine = folder.Tiles.Single(tile => tile.Cell.Island.Id == tides.Id).Island;
            Assert.NotSame(ghostLine, placedLine);
            Assert.False(((CellIsland)placedLine).Sampled);
        }
        finally
        {
            Samples.Ghost = was;
        }
    });

    // --- §643 a pick not set up yet, shown set up ---------------------------------------------------------

    /// <summary>
    /// What a widget not set up yet says of itself: no drive, no folder, nothing pinned, no camera, no toggles, no actions, an
    /// empty note, "Set up" — and §635's no symbol, team, feed or place. None of it is ever a ghost's, of any kind.
    /// </summary>
    private static readonly string[] NotSetUp =
    [
        "core.drive.noneChosen", "core.drive.noneChosenLine", "widget.drive.notSet", "dock.setup.driveText",
        "dock.folder.unset", "dock.folder.unsetWord", "dock.pin.choose", "dock.pin.nothingYet", "dock.setup.noTarget", "dock.setup.pinText",
        "dock.camera.noStream", "dock.camera.noStreamText", "dock.camera.connecting",
        "widget.toggles.none", "widget.actions.none", "widget.note.empty",
        "dock.markets.noSymbolTitle", "widget.markets.noSymbol", "dock.sports.noTeam", "widget.rss.noFeed", "widget.weather.noPlaceTitle",
        "dock.setup.button", "dock.setup.short",
    ];

    /// <summary>
    /// Every pick of the add page — each widget and ready-made group on a bar, each list widget into a folder — shown set up:
    /// its ghost's <see cref="AddGhost.Shown"/> holds nothing that still needs a field (WidgetSetup.Needs), inside a group too,
    /// but what the samples give a widget as it is made (§635: a share's symbol, a feed, a team); the copy the click puts in
    /// (<see cref="AddGhost.Config"/>) is the pick as it is, still wanting what it wants; the config it was made of untouched.
    /// </summary>
    [Fact]
    public void EveryPickIsShownSetUpAndPutInAsItIs()
    {
        var config = new Config();
        var owner = Config.LayoutOwner(config.DefaultLayout.Id);
        var holder = BarKind.FolderList.New();
        holder.Id = "list";
        config.ListFor(owner).Add(holder);
        var before = config.ToJson();
        var failures = new List<string>();
        var dressed = new HashSet<string>(StringComparer.Ordinal);
        var drives = new PresetMachine(HasBattery: true, FixedDrives: [new DriveVolume("", "C:", "Windows", DriveKind.Fixed, 100, 50)]);
        var bar = AddList.Rows(WidgetType.Bar, turns: false, firstId: 100);
        for (var at = 0; at < bar.Entries.Count + bar.Presets.Count; at++)
        {
            See(AddList.Into(config, new AddSpot(owner, new BarSlot(BarSection.End, 0)), (uint)(100 + at), 100, bar.Entries, bar.Presets, () => drives));
        }
        var lines = AddList.Rows(WidgetType.List, turns: false, firstId: 100);
        for (var at = 0; at < lines.Entries.Count; at++)
        {
            See(AddList.Into(config, AddSpot.InFolder(owner, holder.Id, new FolderPlace(0, 0)), (uint)(100 + at), 100, lines.Entries, [], () => drives));
        }
        Assert.Equal(before, config.ToJson());
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        // The kinds a new one of which wants a field the samples do not give as it is made.
        Assert.Superset(new HashSet<string>(StringComparer.Ordinal) { "pin", "folderContents", "drive", "networkDrive", "camera" }, dressed);

        void See(AddGhost? ghost)
        {
            if (ghost is null)
            {
                return;
            }
            var path = ghost.Key[(owner.Length + 1)..];
            var put = Every(Karpo.Core.Folders.Find(ghost.Config.ListFor(owner), path)).ToList();
            var shown = Every(Karpo.Core.Folders.Find(ghost.Shown.ListFor(owner), path)).ToList();
            Assert.Equal(put.Count, shown.Count);
            for (var at = 0; at < put.Count; at++)
            {
                var wanted = WidgetSetup.Needs(put[at]);
                var still = WidgetSetup.Needs(shown[at]);
                if (still is { } field && !ByTheSamples(field))
                {
                    failures.Add($"{ghost.Key}: {shown[at].Info?.Id} shown wanting {field}");
                }
                if (wanted is { } needed && !ByTheSamples(needed))
                {
                    _ = dressed.Add(put[at].Info!.Id);
                }
            }
        }
    }

    /// <summary>
    /// What a ghost is set up with: this PC's own where it has an obvious one — its system drive, its Documents, File Explorer —,
    /// else made up — a share, a camera —; a drive's group's folder contents the drive's root, as its one question gives it; a
    /// setting already there stays.
    /// </summary>
    [Fact]
    public void AGhostIsSetUpWithThisPcsOwnElseMadeUp()
    {
        var drive = ListKind.Drive.New();
        var share = ListKind.NetworkDrive.New();
        var contents = ListKind.FolderContents.New();
        var pin = ListKind.Pin.New();
        var camera = ListKind.Camera.New();
        foreach (var item in new WidgetItem[] { drive, share, contents, pin, camera })
        {
            Samples.Dress(item);
            Assert.Null(WidgetSetup.Needs(item));
        }
        Assert.Equal(DriveTargets.Letter(Path.GetPathRoot(Environment.SystemDirectory)), drive.Drive!.Letter);
        Assert.Equal(Samples.Share, share.Drive!.Volume);
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), contents.Pin!.Target);
        Assert.EndsWith(@"\Microsoft.Windows.Explorer", pin.Pin!.Target, StringComparison.Ordinal);
        Assert.True(camera.Camera!.IsSet);

        // The drive group: the drive this PC's, its contents its root; the network drive's, the share's.
        foreach (var (id, root) in new[] { ("driveGroup", DriveTargets.Root(drive.Drive.Letter)), ("networkDriveGroup", Samples.Share) })
        {
            var group = Presets.Folder(Presets.Find(id)!, Machine);
            Samples.Dress(group);
            Assert.Equal(root, group.Folder!.Cells.Single(cell => PinFolders.Lists(cell.Island)).Island.Pin!.Target);
        }

        // Set already: stays.
        var mine = ListKind.Pin.New();
        mine.Pin!.Target = @"C:\Tools\mine.exe";
        Samples.Dress(mine);
        Assert.Equal(@"C:\Tools\mine.exe", mine.Pin.Target);
    }

    /// <summary>
    /// Every ghost, of any kind — each ready-made group, its face and its lines, and each list widget alone — set up as the dock
    /// shows it (Samples.Dress) and made as its ghost: drawn without a word of a widget not set up (<see cref="NotSetUp"/>),
    /// none wanting setting up. A drive's is this PC's or the samples' share (never asked of the network), a camera's the
    /// samples' picture (no stream opened). Put in, the drive's group made from the config wants its drive again.
    /// </summary>
    [UiFact]
    public void EveryGhostIsDrawnSetUp()
    {
        var failures = new List<string>();
        var words = NotSetUp.Select(key => (Key: key, Text: S.Lookup(key) ?? throw new InvalidOperationException($"no text {key}"))).ToList();
        var machine = new PresetMachine(HasBattery: true, FixedDrives: [new DriveVolume("", "C:", "Windows", DriveKind.Fixed, 100, 50)]);
        OnStaThread(gfx =>
        {
            var look = ListFormPictures.LookFor(dark: true);
            foreach (var preset in Presets.All)
            {
                var bar = new List<BarItem>();
                var id = Presets.AddToBar(bar, new BarSlot(BarSection.End, 0), preset, machine);
                var group = bar.Single(island => island.Id == id);
                Samples.Dress(group);
                Check(gfx, look, preset.Id, group, face: true, failures, words);
            }
            foreach (var line in ListWidgets.All)
            {
                var holder = BarKind.FolderList.New();
                Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, holder.Folder!, line.Kind.New()));
                Samples.Dress(holder);
                Check(gfx, look, line.Kind.ToString(), holder, face: false, failures, words);
            }

            // The made-up ones: drawn from the samples, nothing asked.
            var was = Samples.Ghost;
            var shares = BarKind.FolderList.New();
            Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, shares.Folder!, ListKind.NetworkDrive.New()));
            Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, shares.Folder!, ListKind.Camera.New()));
            Samples.Dress(shares);
            var key = Config.IslandKey(Owner, shares.Id);
            try
            {
                Samples.Ghost = key;
                using var ghost = Assert.IsType<FolderIsland>(Karpo.Dock.Islands.Create(key, shares, gfx, new Config()));
                ghost.Opened(gfx);
                var drive = Assert.IsType<DriveIsland>(ghost.Tiles[0].Island);
                Assert.Equal(Samples.Share, drive.At(0).Volume);
                Assert.True(drive.At(0).Total > 0);
                var camera = Assert.IsType<CameraLine>(ghost.Tiles[1].Island);
                Assert.NotNull(camera.Shot.Frame);
                Assert.Equal(CameraShot.None, camera.Stream.Shot);
            }
            finally
            {
                Samples.Ghost = was;
            }

            // Put in: made from the config, which has no drive: it wants one, as a new one does.
            var list = new List<BarItem>();
            var put = Presets.AddToBar(list, new BarSlot(BarSection.End, 0), Presets.Find("driveGroup")!, machine);
            var placed = list.Single(island => island.Id == put);
            using var made = Assert.IsType<FolderIsland>(Karpo.Dock.Islands.Create(Config.IslandKey(Owner, placed.Id), placed, gfx, new Config()));
            Assert.Equal(SetupField.Volume, ((CellIsland)made.Tiles[0].Island).Unset?.Link.Field);
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>§635: a field the samples give a widget as it is made, a ghost's no matter what its settings say: a share's symbol, a feed, a team, a place.</summary>
    private static bool ByTheSamples(SetupField field) => field is SetupField.Symbol or SetupField.Feed or SetupField.Team or SetupField.Place;

    /// <summary><paramref name="item"/> and every widget inside it.</summary>
    private static IEnumerable<WidgetItem> Every(WidgetItem? item)
    {
        if (item is null)
        {
            yield break;
        }
        yield return item;
        if (item is BarItem { Folder: { } folder })
        {
            foreach (var cell in folder.Cells)
            {
                yield return cell.Island;
            }
        }
    }
}
