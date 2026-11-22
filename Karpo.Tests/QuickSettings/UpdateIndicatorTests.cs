// The status icons' update icon (QuickSettings\Services\Indicators.cs, WindowsUpdate.cs,
// WindowsUpdateIcon.cs): Windows 11's own update icon, which stands beside the network, sound and
// battery icons while updates are waiting, downloading or installing, or a restart is waiting for
// them, and is gone otherwise (the person, 2026-10-05: "the update available button does not properly
// show on the bottom right on a default layout").
//
// Held here: each stage's visibility, glyph, hover and dot; no room with nothing to say; the
// orchestrator's snippet read into a stage (this PC's own, a driver waiting for a restart, among
// them); Windows' own tray icon as the fallback and left out of the tray where the status icons
// stand for it; its click; the default bar placing it; and the registries and the add list carrying
// it. Every state is made up: nothing reads this PC's Windows Update, and nothing is opened.
//
// KARPO_PNG_DIR\update-indicator-{scale}-{theme}.png: the update icon for updates waiting,
// downloading, installing, a restart for drivers and a restart for Windows, side by side, at 100 and
// 150 %, dark and light.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.QuickSettings;

public sealed unsafe class UpdateIndicatorTests
{
    private static UpdateState Doing(UpdateStage stage) => UpdateState.Nothing with { Stage = stage };

    private static Indicator? UpdateIcon(IndicatorState state) =>
        Indicators.Of(state).SingleOrDefault(indicator => indicator.Kind == IndicatorKind.WindowsUpdate);

    // --- What it shows -------------------------------------------------------------------------

    /// <summary>
    /// Each stage into what the taskbar shows: nothing at all while up to date or before anything is
    /// read; updates waiting, downloading and installing with Windows' blue dot; a restart waiting
    /// with its red dot, or its orange one where only drivers wait for it. Always Windows Update's
    /// arrows, and a hover as short as Windows' taskbar words.
    /// </summary>
    [Fact]
    public void EachStageIsShownAsWindowsShowsIt()
    {
        Assert.Null(UpdateIcon(new IndicatorState { Update = UpdateState.Nothing }));
        Assert.Null(UpdateIcon(new IndicatorState { Update = UpdateState.Unknown }));
        // Nothing read yet says nothing, whatever stage a made-up state carries.
        Assert.Null(UpdateIcon(new IndicatorState { Update = UpdateState.Unknown with { Stage = UpdateStage.Downloading } }));

        (UpdateState State, string Tip, UpdateBadge Badge)[] cases =
        [
            (Doing(UpdateStage.Available), "Updates available", UpdateBadge.Seeker),
            (Doing(UpdateStage.Downloading), "Downloading updates", UpdateBadge.Seeker),
            (Doing(UpdateStage.Installing), "Installing updates", UpdateBadge.Seeker),
            (UpdateState.Due with { DriversOnly = true }, "Restart required", UpdateBadge.Warning),
            (UpdateState.Due, "Restart required", UpdateBadge.Important),
            // A restart waiting is what it says, whatever else is under way.
            (UpdateState.Due with { Stage = UpdateStage.Downloading }, "Restart required", UpdateBadge.Important),
        ];
        foreach (var (state, tip, badge) in cases)
        {
            var icon = UpdateIcon(new IndicatorState { Update = state });
            Assert.NotNull(icon);
            Assert.Equal("\uE895", icon.Glyph);
            Assert.Equal(tip, icon.Tooltip);
            Assert.Equal(badge, icon.Badge);
            Assert.Equal(badge, WindowsUpdate.Badge(state));
        }
        Assert.Equal(UpdateBadge.None, WindowsUpdate.Badge(UpdateState.Nothing));
        Assert.Equal("", WindowsUpdate.Status(UpdateStage.None));
    }

    /// <summary>What stage there is to show: a restart first, then what it is doing, and nothing before anything is read.</summary>
    [Fact]
    public void ARestartIsShownBeforeWhatItIsDoing()
    {
        Assert.Equal(UpdateStage.None, WindowsUpdate.Shown(UpdateState.Unknown));
        Assert.Equal(UpdateStage.None, WindowsUpdate.Shown(UpdateState.Nothing));
        Assert.Equal(UpdateStage.Installing, WindowsUpdate.Shown(Doing(UpdateStage.Installing)));
        Assert.Equal(UpdateStage.Restart, WindowsUpdate.Shown(UpdateState.Due with { Stage = UpdateStage.Installing }));
    }

    /// <summary>
    /// A click opens Windows Update, as a click on Windows' own icon does; its menu is the Windows
    /// Update widget's two rows — its page and its restart options — never a restart. It is the last
    /// of the status icons, as it is the last of Windows' beside the network icon.
    /// </summary>
    [Fact]
    public void ItOpensWindowsUpdate()
    {
        foreach (var state in new[] { Doing(UpdateStage.Available), Doing(UpdateStage.Downloading), UpdateState.Due })
        {
            var icon = UpdateIcon(new IndicatorState { Update = state })!;
            Assert.Equal("ms-settings:windowsupdate", icon.Uri);
            Assert.Equal(
                [("Windows Update settings", "ms-settings:windowsupdate"), ("Restart options", "ms-settings:windowsupdate-options")],
                icon.Menu.Select(row => (row.Text, row.Uri)));
            Assert.All(icon.Menu, row => Assert.Equal(IndicatorAct.Open, row.Act));
        }
        var all = Indicators.Of(new IndicatorState { Microphone = ["Teams"], DoNotDisturb = true, Update = Doing(UpdateStage.Available) });
        Assert.Equal(IndicatorKind.WindowsUpdate, all[^1].Kind);
        // The status icons open Windows' pages, never a flyout of Karpo's.
        Assert.Equal(BarClick.Action, BarEntries.Of(BarKind.Indicators).Click);
    }

    // --- Windows' own icon -----------------------------------------------------------------------

    /// <summary>
    /// Where Karpo's reading has nothing to say but Windows has its update icon in the tray, the
    /// status icon is Windows' word for it: its hover, its dot (orange where none could be read), the
    /// same click and menu. Karpo's own reading, where it has one, says it instead.
    /// </summary>
    [Fact]
    public void WindowsOwnIconIsShownWhereKarpoReadsNothing()
    {
        var tip = "Your device needs to restart to install updates.\nSelect a time to restart.";
        var windows = UpdateIcon(new IndicatorState { WindowsIcon = new WindowsUpdateIcon(tip, UpdateBadge.Important) })!;
        Assert.Equal((tip, UpdateBadge.Important, "ms-settings:windowsupdate"), (windows.Tooltip, windows.Badge, windows.Uri));

        var unread = UpdateIcon(new IndicatorState { WindowsIcon = new WindowsUpdateIcon("  ", UpdateBadge.None) })!;
        Assert.Equal(("Windows Update", UpdateBadge.Warning), (unread.Tooltip, unread.Badge));

        var ours = UpdateIcon(new IndicatorState { Update = Doing(UpdateStage.Downloading), WindowsIcon = new WindowsUpdateIcon(tip, UpdateBadge.Important) })!;
        Assert.Equal(("Downloading updates", UpdateBadge.Seeker), (ours.Tooltip, ours.Badge));
        // One icon for updates, never two.
        Assert.Single(Indicators.Of(new IndicatorState { Update = UpdateState.Due, WindowsIcon = new WindowsUpdateIcon(tip, UpdateBadge.Warning) }));
    }

    /// <summary>Windows' update icon is known by its app: the update orchestrator's notifications host, or Windows 10's MusNotifyIcon.</summary>
    [Theory]
    [InlineData(@"C:\Windows\UUS\amd64\MoNotificationUx.exe", true)]
    [InlineData(@"C:\WINDOWS\System32\monotificationux.exe", true)]
    [InlineData(@"C:\Windows\System32\MusNotifyIcon.exe", true)]
    [InlineData(@"C:\Windows\System32\SecurityHealthSystray.exe", false)]
    [InlineData(@"C:\Program Files\Tracker Software\Update\TrackerUpdate.exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void WindowsIconIsKnownByItsApp(string? path, bool windows) => Assert.Equal(windows, WindowsUpdateTray.Is(path));

    /// <summary>
    /// The tray leaves Windows' icon out where a bar shows the status icons, which stand for it, and
    /// keeps it, as any other icon, where none does; the others are never touched, and where Windows'
    /// icon is not there the bar is not even asked.
    /// </summary>
    [Fact]
    public void TheTrayLeavesWindowsIconToTheStatusIcons()
    {
        string[] icons = [@"C:\Apps\Spotify.exe", @"C:\Windows\UUS\amd64\MoNotificationUx.exe", @"C:\Windows\System32\SecurityHealthSystray.exe"];

        Assert.Equal([icons[0], icons[2]], WindowsUpdateTray.Held(icons, path => path, () => true));
        Assert.Same(icons, WindowsUpdateTray.Held(icons, path => path, () => false));
        string[] without = [icons[0], icons[2]];
        Assert.Same(without, WindowsUpdateTray.Held(without, path => path, () => throw new InvalidOperationException("asked for nothing")));
    }

    /// <summary>
    /// The dot on Windows' icon is read from its picture where every one of its icon files has it, three
    /// quarters across and down: its files' blue, orange and red, each the right badge; nothing drawn
    /// there, or a picture too small to hold one, none.
    /// </summary>
    [Fact]
    public void TheDotIsReadFromWindowsPicture()
    {
        foreach (var size in new[] { 16, 24, 32 })
        {
            Assert.Equal(UpdateBadge.Seeker, WindowsUpdateTray.BadgeOf(Picture(size, 0x0078D7), size, size));
            Assert.Equal(UpdateBadge.Warning, WindowsUpdateTray.BadgeOf(Picture(size, 0xFC9A03), size, size));
            Assert.Equal(UpdateBadge.Important, WindowsUpdateTray.BadgeOf(Picture(size, 0xFF0000), size, size));
            Assert.Equal(UpdateBadge.None, WindowsUpdateTray.BadgeOf(new byte[size * size * 4], size, size));
        }
        Assert.Equal(UpdateBadge.None, WindowsUpdateTray.BadgeOf(new byte[8], 16, 16));

        // White arrows round a dot of the colour in the bottom-right quarter, premultiplied BGRA, half
        // the picture's alpha where it is drawn at all, as an icon's edges are.
        static byte[] Picture(int size, uint rgb)
        {
            var pixels = new byte[size * size * 4];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var at = ((y * size) + x) * 4;
                    var dot = x >= size / 2 && y >= size / 2;
                    var alpha = dot ? 255 : (x + y) % 3 == 0 ? 128 : 0;
                    var (r, g, b) = dot ? ((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF) : (255u, 255u, 255u);
                    pixels[at] = (byte)(b * alpha / 255);
                    pixels[at + 1] = (byte)(g * alpha / 255);
                    pixels[at + 2] = (byte)(r * alpha / 255);
                    pixels[at + 3] = (byte)alpha;
                }
            }
            return pixels;
        }
    }

    // --- What it reads -------------------------------------------------------------------------

    private static readonly DateTime Utc = new(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime Boot = Utc.AddDays(-2);

    /// <summary>A time as the snippet writes it: 100 ns since 1970.</summary>
    private static long Ticks(DateTime utc) => (utc - DateTime.UnixEpoch).Ticks;

    private static string Snippet(string? os, params string[] drivers)
    {
        var at = Ticks(Utc.AddHours(-1));
        var parts = new List<string> { $"\"DriverUpdateCount\":{drivers.Length}" };
        parts.Add($"\"Drivers\":[{string.Join(",", drivers.Select((state, index) => $"{{\"ID\":\"d{index}:1\",\"State\":\"{state}\",\"Time\":{at}}}"))}]");
        if (os is not null)
        {
            parts.Add($"\"OS\":{{\"ID\":\"os:1\",\"State\":\"{os}\",\"Time\":{at}}}");
        }
        return "{" + string.Join(",", parts) + "}";
    }

    /// <summary>
    /// This PC's own snippet, as the orchestrator wrote it on 2026-10-05 while Windows' tray showed its
    /// orange update icon and no RebootRequired key was there: a driver installed and waiting for a
    /// restart, Windows itself done. A restart waiting, for drivers alone — which the RebootRequired
    /// key alone, all Karpo read before, never said.
    /// </summary>
    [Fact]
    public void ADriverWaitingForARestartIsARestart()
    {
        const string ThisPc = """{"DriverUpdateCount":1,"Drivers":[{"ID":"8130daa0-bd21-4a27-a243-ccb3d7c4c21e:1","State":"Installed-PendingReboot","Time":17912232356520000}],"OS":{"ID":"916031e1-9d13-48e9-a262-c8a0db93fbac:1","State":"PostReboot-Completed","Time":17894488210050000}}""";
        var written = WindowsUpdateRegistry.SnippetTime(17912232356520000)!.Value;
        Assert.Equal(new DateTime(2026, 10, 5), written.Date);

        var read = WindowsUpdateRegistry.OfSnippet(ThisPc, written.AddMinutes(30), written.AddDays(-2));
        Assert.Equal(new Orchestrated(UpdateStage.Restart, ForWindows: false), read);

        var state = WindowsUpdateRegistry.Of(false, read, _ => null, written.AddMinutes(30));
        Assert.True(state is { Known: true, Restart: true, DriversOnly: true });
        Assert.Equal(UpdateBadge.Warning, WindowsUpdate.Badge(state));
        Assert.Equal("Restart required", WindowsUpdate.Status(WindowsUpdate.Shown(state)));

        // Restarted since: the same snippet, not yet written again, is believed no more.
        Assert.Equal(default, WindowsUpdateRegistry.OfSnippet(ThisPc, written.AddDays(1), written.AddHours(2)));
    }

    /// <summary>
    /// Each of the orchestrator's states: the furthest of Windows' own and its drivers' is what is
    /// shown; a restart waiting for Windows itself is for Windows (red), whatever its drivers wait
    /// for; done and unknown are nothing.
    /// </summary>
    [Fact]
    public void TheSnippetsStatesAreRead()
    {
        Orchestrated Read(string json) => WindowsUpdateRegistry.OfSnippet(json, Utc, Boot);

        Assert.Equal(new Orchestrated(UpdateStage.Downloading, false), Read(Snippet("Downloading")));
        Assert.Equal(new Orchestrated(UpdateStage.Installing, false), Read(Snippet("Installing", "Downloading")));
        Assert.Equal(new Orchestrated(UpdateStage.Available, false), Read(Snippet("Downloaded")));
        Assert.Equal(new Orchestrated(UpdateStage.Available, false), Read(Snippet("DownloadBlocked")));
        Assert.Equal(new Orchestrated(UpdateStage.Available, false), Read(Snippet("Installed-Completed", "DownloadFailed")));
        Assert.Equal(new Orchestrated(UpdateStage.Available, false), Read(Snippet(null, "InstallFailed")));
        Assert.Equal(new Orchestrated(UpdateStage.Restart, true), Read(Snippet("Installed-PendingReboot", "Installed-PendingReboot")));
        Assert.Equal(new Orchestrated(UpdateStage.Restart, false), Read(Snippet("Downloading", "Installed-PendingReboot")));
        Assert.Equal(default, Read(Snippet("PostReboot-Completed", "Installed-Completed")));
        Assert.Equal(default, Read(Snippet("SomethingNew")));
        Assert.Equal(default, Read(Snippet(null)));
    }

    /// <summary>Anything that is not the orchestrator's JSON is nothing, never an exception.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"OS\":\"Installing\"}")]
    [InlineData("{\"OS\":{\"State\":7}}")]
    [InlineData("{\"Drivers\":{\"State\":\"Installing\"}}")]
    [InlineData("{\"OS\":{\"State\":\"Installing\",\"Time\":\"soon\"}")]
    public void ASnippetThatIsNotJsonIsNothing(string? json) =>
        Assert.Equal(default, WindowsUpdateRegistry.OfSnippet(json, Utc, Boot));

    /// <summary>
    /// What is believed of an old state: work in hand and a restart waiting only from this start of
    /// Windows on — a restart since has made it, or ended the work — and updates waiting for a week
    /// from when the orchestrator last wrote them; a state without a time is believed.
    /// </summary>
    [Fact]
    public void AnOldStateIsNotBelieved()
    {
        UpdateStage At(string state, DateTime? at) => WindowsUpdateRegistry.StageOf(state, at, Utc, Boot);

        Assert.Equal(UpdateStage.Restart, At("Installed-PendingReboot", Boot.AddMinutes(1)));
        Assert.Equal(UpdateStage.None, At("Installed-PendingReboot", Boot.AddMinutes(-1)));
        Assert.Equal(UpdateStage.Downloading, At("Downloading", Boot.AddHours(1)));
        Assert.Equal(UpdateStage.None, At("Installing", Boot.AddHours(-1)));
        Assert.Equal(UpdateStage.Available, At("Downloaded", Boot.AddDays(-3)));
        Assert.Equal(UpdateStage.None, At("DownloadBlocked", Utc.AddDays(-8)));
        Assert.Equal(UpdateStage.Restart, At("Installed-PendingReboot", null));
        Assert.Null(WindowsUpdateRegistry.SnippetTime(0));
        Assert.Null(WindowsUpdateRegistry.SnippetTime(-5));
        Assert.Null(WindowsUpdateRegistry.SnippetTime(long.MaxValue));
    }

    /// <summary>
    /// The restart keys and the snippet together: either says a restart is waiting, for Windows where
    /// the keys say so or the snippet says it of Windows itself, and for drivers alone where only the
    /// snippet says it of drivers; with none waiting, what the orchestrator is doing. The times are
    /// read as before.
    /// </summary>
    [Fact]
    public void TheKeysAndTheSnippetMakeOneState()
    {
        var later = Utc.AddHours(10);
        object? Value(string name) => name == "ScheduledRebootTime" ? later.ToFileTimeUtc() : null;

        Assert.Equal(UpdateState.Nothing, WindowsUpdateRegistry.Of(false, default, Value, Utc));
        Assert.Equal(Doing(UpdateStage.Installing), WindowsUpdateRegistry.Of(false, new Orchestrated(UpdateStage.Installing, false), Value, Utc));

        var keys = WindowsUpdateRegistry.Of(true, default, Value, Utc);
        Assert.True(keys is { Restart: true, DriversOnly: false });
        Assert.Equal(later.ToLocalTime(), keys.Scheduled);
        Assert.False(WindowsUpdateRegistry.Of(true, new Orchestrated(UpdateStage.Restart, false), Value, Utc).DriversOnly);
        Assert.False(WindowsUpdateRegistry.Of(false, new Orchestrated(UpdateStage.Restart, true), Value, Utc).DriversOnly);
        Assert.True(WindowsUpdateRegistry.Of(false, new Orchestrated(UpdateStage.Restart, false), Value, Utc) is { Restart: true, DriversOnly: true });
        // A restart waiting is the state; what else was under way is not carried with it.
        Assert.Equal(UpdateStage.None, WindowsUpdateRegistry.Of(true, new Orchestrated(UpdateStage.Downloading, false), Value, Utc).Stage);
    }

    /// <summary>
    /// The orchestrator's working key is watched with what is under it, so its Diagnostics key is
    /// heard even where it comes after the watch; read only, in HKLM's software keys, nothing online.
    /// </summary>
    [Fact]
    public void TheOrchestratorsWorkIsWatched()
    {
        Assert.Contains((@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Orchestrator", true), WindowsUpdateRegistry.Watched);
        Assert.Equal(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Orchestrator\Diagnostics", WindowsUpdateRegistry.Diagnostics);
        Assert.Equal("UpdateStateSnippet", WindowsUpdateRegistry.Snippet);
        Assert.Equal(TimeSpan.FromDays(7), WindowsUpdateRegistry.Waiting);
    }

    // --- Where it stands -----------------------------------------------------------------------

    /// <summary>
    /// A new install's bar, and a reset's, has the status icons in the right section where Windows
    /// puts its update icon: after the tray (and the pen and touch keyboard buttons, where Windows
    /// shows them), before the input indicator, network, sound and battery, and the clock — with a
    /// battery or without. A bar somebody already has is not changed: nothing
    /// in the config's repair adds it.
    /// </summary>
    [Fact]
    public void TheDefaultBarHasItBesideTheNetworkIcon()
    {
        var hardware = new InputHardware(Pen: true, Touch: true, Slate: false);
        foreach (var windows in new[] { WindowsTaskbar.OutOfTheBox, WindowsTaskbar.OutOfTheBox with { Battery = false } })
        {
            var bar = Sections.DefaultLayout([], hardware, windows);
            var end = bar.Where(item => item.Section == BarSection.End).Select(item => item.Kind).ToList();
            var at = end.IndexOf(BarKind.Indicators);
            Assert.True(at > 0);
            Assert.Single(bar, item => item.Kind == BarKind.Indicators);
            Assert.True(end.IndexOf(BarKind.Tray) < at);
            Assert.True(end.IndexOf(BarKind.TouchKeyboard) < at);
            Assert.Equal(FolderFaces.KeyboardId, bar.Where(item => item.Section == BarSection.End).ElementAt(at + 1).Folder?.Face.Kind);
            // Then quick settings, wearing the network first (2026-10-13).
            Assert.Equal(FolderFaces.WifiId, bar.Where(item => item.Section == BarSection.End).ElementAt(at + 2).Folder?.Face.Kind);
            // The clock, the last folder.
            Assert.True(at < end.LastIndexOf(BarKind.FolderList));
        }
        // An existing bar without it keeps its own entries: the repair a config gets on reading adds nothing.
        var config = new Config();
        config.DefaultLayout.Islands = [BarKind.Start.New(), BarKind.Apps.New(), BarKind.NightLight.New()];
        config.Repair();
        Assert.DoesNotContain(config.DefaultLayout.Islands, item => item.Kind == BarKind.Indicators);
    }

    /// <summary>
    /// The status icons are in every registry an entry needs and in the add list, on the taskbar
    /// alone, found by "update"; their right click is the cell's own rows (the dock's menu table gives
    /// the kind none of its own), so the update icon's menu is its two Windows Update rows.
    /// </summary>
    [Fact]
    public void TheStatusIconsAreInTheRegistriesAndTheAddList()
    {
        var entry = BarEntries.Of(BarKind.Indicators);
        Assert.Equal((WidgetSource.Indicators, BarClick.Action), (entry.Drawing, entry.Click));
        Assert.Same(WidgetInfos.Indicators, entry.Info);
        Assert.Contains(Catalogue.On(WidgetType.Bar), row => row.Id == WidgetInfos.Indicators.Id);
        Assert.Contains("windows update", WidgetInfos.Indicators.Keywords);
        Assert.Contains("updates available", WidgetInfos.Indicators.Keywords);
        Assert.Empty(TheDock.ActsFor(BarKind.Indicators));
        Assert.True(Sections.CanAdd([], BarKind.Indicators));
    }

    // --- On the bar ----------------------------------------------------------------------------

    private static IslandStyle Style(float scale, Look look) =>
        new(scale, false, Layout.CellPx(96, new Config { Height = 48 }) * scale, Layout.IconPx(96, new Config { Height = 48 }) * scale, look);

    private static IndicatorsIsland Make(SampleIndicators source, Gfx gfx, List<string>? opened = null)
    {
        IndicatorSources.Made = () => source;
        try
        {
            var entry = BarKind.Indicators.New();
            entry.Id = "indicators";
            return new IndicatorsIsland(Config.IslandKey(null, entry.Id), entry, gfx, new Config()) { Opened = opened is null ? _ => { } : opened.Add };
        }
        finally
        {
            IndicatorSources.Made = null;
        }
    }

    /// <summary>
    /// Up to date, the entry takes no room at all; updates arriving give it one cell, the network
    /// icon's square, with its short hover; a click opens Windows Update; done, it is gone again.
    /// </summary>
    [UiFact]
    public void ItTakesNoRoomUntilThereIsSomethingToSay()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var source = new SampleIndicators();
            var opened = new List<string>();
            using var island = Make(source, gfx, opened);
            IIsland entry = island;
            var style = Style(1f, Karpo.Tests.Folders.TestLooks.LookFor(true));
            Assert.Equal(0, entry.Measure(style).Count);

            source.Tell(new IndicatorState { Update = Doing(UpdateStage.Available) });
            Assert.True(entry.Refresh(gfx));
            Assert.Equal(1, entry.Measure(style).Count);
            Assert.Equal("Updates available", entry.Tooltip(0));
            Assert.Equal(default, entry.Click(0));
            Assert.Equal(["ms-settings:windowsupdate"], opened);

            source.Tell(new IndicatorState { Update = Doing(UpdateStage.Downloading) });
            Assert.False(entry.Refresh(gfx));
            Assert.Equal("Downloading updates", entry.Tooltip(0));

            // The source's news is posted to the dock's thread: the entry reads it on its next refresh.
            source.Tell(new IndicatorState { Update = UpdateState.Due });
            Assert.False(entry.Refresh(gfx));
            Assert.Equal("Restart required", entry.Tooltip(0));

            source.Tell(IndicatorState.Nothing);
            Assert.True(entry.Refresh(gfx));
            Assert.Equal(0, entry.Measure(style).Count);
        });
    }

    /// <summary>
    /// Drawn offscreen for each stage, at 100 and 150 %, dark and light: the arrows inside their cell,
    /// and the dot's middle in its colour — blue, orange or red as Windows' — and the sheet written.
    /// </summary>
    [UiFact]
    public void EachStageIsDrawnWithItsDot()
    {
        UpdateState[] stages =
        [
            Doing(UpdateStage.Available), Doing(UpdateStage.Downloading), Doing(UpdateStage.Installing),
            UpdateState.Due with { DriversOnly = true }, UpdateState.Due,
        ];
        var wrong = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (scale, dark) in new[] { (1f, true), (1f, false), (1.5f, true), (1.5f, false) })
            {
                var look = Karpo.Tests.Folders.TestLooks.LookFor(dark);
                var style = Style(scale, look);
                var ink = new IslandInk(look);
                var islands = stages.Select(state => Make(new SampleIndicators(new IndicatorState { Update = state }), gfx)).ToList();
                try
                {
                    foreach (IIsland measured in islands)
                    {
                        Assert.Equal(1, measured.Measure(style).Count);
                    }
                    var (width, height) = islands[0].CellSize(0, style);
                    Offscreen.Bitmap(width * islands.Count, height, dc =>
                    {
                        var background = dark ? new DXGI_RGBA(0.13f, 0.13f, 0.13f, 1) : new DXGI_RGBA(0.95f, 0.95f, 0.95f, 1);
                        dc->Clear(&background);
                        for (var index = 0; index < islands.Count; index++)
                        {
                            var island = islands[index];
                            Offscreen.Shifted(dc, index * width, 0, () => island.PaintCell(dc, 0, style, width, height));
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"update-indicator-{scale * 100:0}-{(dark ? "dark" : "light")}.png")));
                    for (var index = 0; index < islands.Count; index++)
                    {
                        var island = islands[index];
                        var want = IndicatorsIsland.DotOf(island.Shown[0], ink)!.Value;
                        var (centre, _, _) = RestartDot.In(RestartDot.Centred(width, height, new Fit(style).SystemIcon));
                        Offscreen.Bitmap(width, height, dc => island.PaintCell(dc, 0, style, width, height), cell =>
                        {
                            var (left, top, right, bottom) = Offscreen.InkOf(cell, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height));
                            if (right <= left || left < 1 || top < 1 || right > width - 1 || bottom > height - 1)
                            {
                                wrong.Add($"{stages[index]} at {scale * 100:0} % {(dark ? "dark" : "light")}: ink {left},{top} to {right},{bottom} in {width}x{height}");
                            }
                            // The dot: about π r² pixels of its colour, r a quarter of the tray's square.
                            var side = new Fit(style).SystemIcon;
                            var least = (int)(MathF.PI * (side / 4) * (side / 4) * 0.5f);
                            var dots = RestartDotTests.Reds(cell, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height), want);
                            if (dots < least)
                            {
                                wrong.Add($"{stages[index]} at {scale * 100:0} % {(dark ? "dark" : "light")}: {dots} pixels of the dot's {want.r:0.00},{want.g:0.00},{want.b:0.00} round {centre}, fewer than {least}");
                            }
                        });
                    }
                }
                finally
                {
                    islands.ForEach(island => island.Dispose());
                }
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }
}
