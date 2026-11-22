// The split's two registries (Karpo.Core\BarEntries.cs, ListWidgets.cs) and the record they share
// (WidgetInfos.cs): every member has its entry, every entry's answers hang together, and each
// registry holds exactly the widgets its surface takes. The grid's registry went with grid folders
// (2026-10-09).
//
// The membership table at the top is the product decision itself, written out: where each widget
// can stand. A widget added to a registry, or taken out of one, changes a line of it on purpose.
// The rest holds what the type system cannot say: that the answers are filled in and agree with
// each other, and the two rules a registry has to keep — a line cannot open anything
// (CellAct), and a list line with nothing on it is only there because its click acts.
//
// These took over from WidgetKindsTests, FolderShapeTests and the registry half of SectionsTests
// when the old table (Widgets.All, one Section per kind) went with the split (S5 step 6b).

using System.Reflection;
using System.Text.Json;

namespace Karpo.Tests.Core;

public sealed class TypedRegistryTests
{
    // --- Where each widget can stand ------------------------------------------------------------

    /// <summary>Every widget, and whether it is a taskbar entry and a list widget.</summary>
    private static readonly (string Id, bool Bar, bool List)[] Membership =
    [
        // Windows' own parts and the bar's own strips: the taskbar's alone.
        ("start", true, false),
        ("search", true, false),
        // Windows' buttons that came after those three: the taskbar's alone too, each opening one of
        // Windows' own popups, drawn by the bar as one glyph.
        ("widgetsBoard", true, false),
        ("penMenu", true, false),
        ("touchKeyboard", true, false),
        ("virtualTouchpad", true, false),
        ("apps", true, false),
        ("tray", true, false),
        // Its cells are what is so right now, beside the tray: the taskbar's alone.
        ("indicators", true, false),
        ("showDesktop", true, false),
        // windowsMenus: Windows' menus (§466), each opening Windows' popup, so the taskbar's alone.
        ("quickLink", true, false),
        ("windowsNotifications", true, false),
        ("windowsQuickSettings", true, false),
        ("windowsSound", true, false),
        ("windowsNetworks", true, false),
        ("windowsCast", true, false),
        ("windowsClipboard", true, false),
        ("emojiPanel", true, false),
        // The network's and Bluetooth's pages are lines since 2026-10-10; their buttons went to
        // presets, and so did the sound button (the sound preset, its panel's regions as lines).
        ("wifi", false, true),
        ("bluetooth", false, true),
        // A folder in a folder invites a regress with nothing to gain.
        ("folderList", true, false),
        // The clipboard history entry went to the clipboard preset (2026-10-10); what was copied is a line.
        ("clipboard", false, true),
        // The contacts entry went to the contacts preset (2026-10-11); the search over them is a line.
        ("contacts", false, true),
        // §642: Windows search, a line; on the bar its ready-made group.
        ("winSearch", false, true),
        // The mail entry went to the mail preset (2026-10-10); its inbox is a line.
        ("mail", false, true),
        // The now-playing entry went to the media preset's face (2026-10-10); what is playing is a line.
        ("media", false, true),
        // The apps playing to pick from, the flyout's list back as a line (2026-10-11).
        ("mediaSources", false, true),
        // screentime: lines of a list, and a ready-made folder of them on the bar (2026-10-09).
        ("screenTime", false, true),
        ("screenTimeApps", false, true),
        ("screenTimeChart", false, true),
        ("screenTimeHistory", false, true),
        // Taken out of folders by the person (2026-09-26): the layout switcher and the keyboard
        // layout. The notification bell was too, until it became the notifications preset's face
        // (2026-10-10), and the desktop switcher, the input indicator and the layout switcher, until
        // they became the desktops, keyboard and layouts presets' (2026-10-11), whose lines they
        // are now. The date went too, and then joined the calendar (2026-09-27), which shows it on
        // the bar.
        ("desktops", false, true),
        ("layout", false, true),
        ("keyboard", false, true),
        // On the bar, with no list line of its own id: the volume slider's is the sound panel's
        // volume row (soundVolume).
        ("volume", true, false),
        // On the bar and in a list, each with a line of its own: its list form, drawn by the class
        // that draws the entry — the brightness, a drive's capacity.
        // quick: the display's brightness, a slider in both.
        ("brightness", true, true),
        ("drive", false, true),
        ("networkDrive", false, true),
        // The battery is a line only: on the bar it is a folder wearing its face (the battery
        // preset, 2026-10-10); the power mode and the power commands, the battery and power
        // flyouts' rows, are lines of their own.
        // The markets widget and the feed are lines only (2026-10-10): on the bar each is a folder wearing
        // its face (the markets and news presets), the line its panel at its tallest (§246).
        ("markets", false, true),
        ("rss", false, true),
        ("battery", false, true),
        ("powerMode", false, true),
        ("sleep", false, true),
        ("hibernate", false, true),
        ("shutDown", false, true),
        ("restart", false, true),
        ("signOut", false, true),
        ("lock", false, true),
        // The calendar and the weather are lines only (2026-10-09): on the bar each is a folder
        // wearing its face (the calendar and weather presets), made of the flyout's pieces.
        ("calendar", false, true),
        ("weatherHours", false, true),
        ("weatherDays", false, true),
        // §590: the hourly forecast's list and the daily forecast's graph, lines as their other styles.
        ("weatherHoursList", false, true),
        ("weatherDaysGraph", false, true),
        // air: lines, the index and, since §590, its pollutants, its pollen and its day; on the bar the air-quality preset
        // wears the index as its face (§210).
        ("airQuality", false, true),
        ("airPollutants", false, true),
        ("airPollen", false, true),
        ("airForecast", false, true),
        // tides: a line; on the bar the tides preset wears the next turn as its face (§236).
        ("tides", false, true),
        ("tideGraph", false, true),
        // sports: lines, the score and its lists (§603); on the bar the sports preset wears its match as its face (§239).
        ("sports", false, true),
        ("sportsFixtures", false, true),
        ("sportsTable", false, true),
        ("sportsKnockout", false, true),
        // quakes: a line; on the bar the earthquake preset wears the latest as its face (§240).
        ("quakes", false, true),
        // sun: lines, its two styles and its times (§404); on the bar the sun preset wears the next sunrise or sunset as its face (§368).
        ("sunArc", false, true),
        ("sunDayBar", false, true),
        ("sunrise", false, true),
        ("sunset", false, true),
        ("daylight", false, true),
        ("solarNoon", false, true),
        ("dawn", false, true),
        ("dusk", false, true),
        // camera: a line alone; a live picture is no face for the bar (§331).
        ("camera", false, true),
        ("dateHeader", false, true),
        ("events", false, true),
        // The next meetings are a line; on the bar the meeting preset wears the next one as its face (§205).
        ("meeting", false, true),
        ("dayView", false, true),
        ("weekView", false, true),
        ("yearView", false, true),
        // The clock is a line only: the bar's clock is a folder wearing its face (the clock preset).
        ("clock", false, true),
        // The removable devices are a line only: their button went to a preset (2026-10-10).
        ("removableDevices", false, true),
        // On the bar and in a list (shells until they were built, 2026-09-26): the timer and focus time.
        ("timer", false, true),
        ("focus", false, true),
        ("stopwatch", false, true),
        ("alarm", false, true),
        // On the bar and in a list, because a line of them acts: a pin opens, a switch flips, a
        // graph opens Task Manager, the security line opens Windows Security, Task view presses
        // Win+Tab — the one Windows panel a folder opens, at the person's asking (2026-09-27).
        ("taskView", true, true),
        // The shortcuts to Windows' apps, each opening its app wherever it stands (items 10 and 18).
        ("snippingTool", true, true),
        ("windowsSettings", true, true),
        ("pin", true, true),
        // §380: what one folder holds, a line in a list; its row of buttons on the taskbar went (§492).
        ("folderContents", false, true),
        ("cpu", false, true),
        ("gpu", false, true),
        ("memory", false, true),
        ("disk", false, true),
        ("network", false, true),
        // perf: the readings' details, lines of their own beside the graphs (2026-10-11).
        ("cpuDetails", false, true),
        ("gpuDetails", false, true),
        ("memoryDetails", false, true),
        ("diskDetails", false, true),
        ("networkDetails", false, true),
        // perf: the processes busiest on the processor, a line of the processor's preset (2026-10-12).
        ("cpuProcesses", false, true),
        // quick: the radios' and the sound's toggles (§117).
        ("wifiSwitch", true, true),
        ("bluetoothSwitch", true, true),
        ("soundSwitch", true, true),
        ("microphoneSwitch", true, true),
        // quick: game mode, color filters and mono audio, toggles through Windows' Settings handlers (§117).
        ("gameMode", true, true),
        ("colorFilters", true, true),
        ("monoAudio", true, true),
        // quick: the accessibility page's magnifier, narrator and sticky keys, toggles as the others (§170).
        ("magnifier", true, true),
        ("narrator", true, true),
        ("stickyKeys", true, true),
        // quick: keep awake, a toggle of Karpo's own power request (§269).
        ("keepAwake", true, true),
        // darkMode: Windows' own mode, a toggle as the others (§403).
        ("darkMode", true, true),
        ("airplaneMode", true, true),
        // Its button became the mobile hotspot preset (§93): the switch is a line alone.
        ("mobileHotspot", false, true),
        ("vpn", false, true),
        ("vpnConnections", false, true),
        ("rotationLock", true, true),
        ("spatialSound", true, true),
        ("energySaver", true, true),
        ("nightLight", true, true),
        ("liveCaptions", true, true),
        // Its button became the nearby sharing preset (§115): the switch is a line alone.
        ("nearbyShare", false, true),
        ("accessibility", true, true),
        // Windows' Cast and Project tiles, lines whose page is all of them (2026-10-16): no button,
        // the cast and project presets are the bar's.
        ("cast", false, true),
        ("project", false, true),
        ("security", true, true),
        // Windows' own do not disturb, a switch like night light's on every type (2026-09-27).
        ("doNotDisturb", true, true),
        // The pieces of the weather and sound panels, drawn at the panel's own width and gutters:
        // lines of a list.
        ("weatherNow", false, true),
        ("soundInput", false, true),
        ("soundMixer", false, true),
        ("soundVolume", false, true),
        // A line of a list, and nothing on the taskbar (2026-09-26): the output device, drawn by the
        // sound panel's region, and the notification list, drawn by the notification panel's list
        // (NotificationPartTile).
        ("soundOutput", false, true),
        ("notificationsList", false, true),
        // A list's title over its lines, and nobody else's (backlog 29).
        ("heading", false, true),
        // A list's line between two lines, put there on purpose, and nobody else's (2026-10-05).
        ("divider", false, true),
        // A flyout made of widgets: a row of toggles, its footer, the month and the clock's header (§68).
        ("toggles", false, true),
        ("actions", false, true),
        ("month", false, true),
        ("timeHeader", false, true),
        // notes: a to-do list and a note, lines of a list, and a ready-made folder of each (§87).
        ("todo", false, true),
        ("note", false, true),
        // The mobile hotspot's page, a line of its preset (§93).
        ("hotspot", false, true),
        // Windows' Cast panel and Win+P's modes, the cast preset's lines (§114), and nearby
        // sharing's, its preset's (§115).
        ("wirelessDisplays", false, true),
        ("projection", false, true),
        ("nearbySharing", false, true),
        // §641: Start's parts, lines of the Start group.
        ("startPins", false, true),
        ("startApps", false, true),
        ("startRecommended", false, true),
        ("startProfile", false, true),
    ];

    public static TheoryData<string, bool, bool> EveryWidget()
    {
        var data = new TheoryData<string, bool, bool>();
        foreach (var (id, bar, list) in Membership)
        {
            data.Add(id, bar, list);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(EveryWidget))]
    public void EachWidgetStandsWhereItsRegistriesSay(string id, bool bar, bool list)
    {
        Assert.True(bar == BarEntries.Find(id) is not null, $"{id} {(bar ? "is" : "is not")} a taskbar entry");
        Assert.True(list == ListWidgets.Find(id) is not null, $"{id} {(list ? "is" : "is not")} a list widget");
    }

    /// <summary>The table names every widget once, and every widget stands somewhere.</summary>
    [Fact]
    public void TheTableIsEveryWidgetOnce()
    {
        Assert.Equal(
            WidgetInfos.All.Select(info => info.Id).Order(StringComparer.Ordinal),
            Membership.Select(row => row.Id).Order(StringComparer.Ordinal));
        Assert.All(Membership, row => Assert.True(row.Bar || row.List, $"{row.Id} stands nowhere"));
    }

    // --- The shared record ----------------------------------------------------------------------

    /// <summary>
    /// A widget has to be findable by somebody who does not know what Karpo calls it, so the
    /// keywords are required and have to say something the name and the blurb do not.
    /// </summary>
    [Fact]
    public void EveryWidgetIsNamedDescribedAndFindable()
    {
        foreach (var info in WidgetInfos.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(info.Title), $"{info.Id} has no title");
            Assert.False(string.IsNullOrWhiteSpace(info.Blurb), $"{info.Id} has no blurb");
            // One code point of Segoe Fluent Icons, which is one char or a surrogate pair.
            Assert.InRange(info.Glyph.Length, 1, 2);
            Assert.True(info.Keywords.Length >= 3, $"{info.Id} has fewer than three keywords");
            Assert.Equal(info.Keywords.Length, info.Keywords.Distinct(StringComparer.Ordinal).Count());
            foreach (var word in info.Keywords)
            {
                Assert.False(string.IsNullOrWhiteSpace(word), $"{info.Id} has an empty keyword");
                Assert.Equal(word.ToLowerInvariant(), word);
            }
        }
    }

    /// <summary>
    /// The id is the word config.json stores, which never changes once released, and one id is one
    /// widget: the registries find a widget by it.
    /// </summary>
    [Fact]
    public void TheIdsAreDistinctAndFound()
    {
        var ids = WidgetInfos.All.Select(info => info.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        foreach (var info in WidgetInfos.All)
        {
            Assert.Same(info, WidgetInfos.Find(info.Id));
            Assert.Same(info, WidgetInfos.Of(info.Id));
        }
        // A word from a newer Karpo, or one taken out, is no widget, and asking for it by force
        // says so rather than handing back somebody else's answers.
        Assert.Null(WidgetInfos.Find("performance"));
        Assert.Null(WidgetInfos.Find(null));
        Assert.Throws<ArgumentOutOfRangeException>(() => WidgetInfos.Of("folderRow"));
    }

    /// <summary>
    /// <see cref="WidgetInfos.All"/> is every named record, once: a record declared and left out of
    /// the list would be a widget the registries could point at and nothing else could find.
    /// </summary>
    [Fact]
    public void AllIsEveryNamedRecordOnce()
    {
        var named = typeof(WidgetInfos).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(WidgetInfo))
            .Select(field => (WidgetInfo)field.GetValue(null)!)
            .ToList();

        Assert.Equal(named.Count, WidgetInfos.All.Length);
        Assert.All(named, info => Assert.Contains(info, WidgetInfos.All));
    }

    /// <summary>
    /// A widget's settings page shows its cards by asking which options record the widget carries
    /// rather than by listing widgets, so these are the families that answer stands for. A widget
    /// that joins a family gets that family's cards, and one that leaves it loses them.
    /// </summary>
    [Theory]
    // weather: the panel's conditions carry a place as the widget does, so the line can stand alone.
    [InlineData(WidgetOptionsKind.Weather, new[] { "weatherNow", "weatherHours", "weatherHoursList", "weatherDays", "weatherDaysGraph", "airQuality", "airPollutants", "airPollen", "airForecast", "tides", "tideGraph", "quakes", "sunArc", "sunDayBar", "sunrise", "sunset", "daylight", "solarNoon", "dawn", "dusk" })]
    [InlineData(WidgetOptionsKind.Calendar, new[] { "calendar" })]
    [InlineData(WidgetOptionsKind.Desktops, new[] { "desktops" })]
    [InlineData(WidgetOptionsKind.Volume, new[] { "volume" })]
    // §380: a folder's contents point at their folder with a pin's record.
    [InlineData(WidgetOptionsKind.Pin, new[] { "folderContents", "pin" })]
    [InlineData(WidgetOptionsKind.Market, new[] { "markets" })]
    // rss: the feed's address, its name and whether the bar says the newest headline.
    [InlineData(WidgetOptionsKind.Rss, new[] { "rss" })]
    // sports: the team it follows (§239).
    [InlineData(WidgetOptionsKind.Sports, new[] { "sports", "sportsFixtures", "sportsTable", "sportsKnockout" })]
    // camera: its stream and how it is shown (§331).
    [InlineData(WidgetOptionsKind.Camera, new[] { "camera" })]
    // drives: the two drive widgets differ in which drives their picker lists, not in their cards.
    [InlineData(WidgetOptionsKind.Drive, new[] { "drive", "networkDrive" })]
    // calendar: the clock and its date and time styles, which show today or the time in its zone (§230).
    [InlineData(WidgetOptionsKind.Clock, new[] { "clock", "dateHeader", "timeHeader" })]
    // calendar: a timer and a focus session, each with its own length and name.
    [InlineData(WidgetOptionsKind.Countdown, new[] { "timer", "focus" })]
    // perf: one options record for the five graphs; the reading is the widget, not a setting.
    [InlineData(WidgetOptionsKind.Performance, new[] { "cpu", "gpu", "memory", "disk", "network", "cpuDetails", "gpuDetails", "memoryDetails", "diskDetails", "networkDetails" })]
    // folders: a list folder, the one there is.
    [InlineData(WidgetOptionsKind.Folder, new[] { "folderList" })]
    // quick: the one-button widgets, and Security, which wears the same "show the name" option.
    [InlineData(WidgetOptionsKind.Switch, new[]
    {
        "airplaneMode", "mobileHotspot", "vpn", "energySaver", "nightLight", "rotationLock", "spatialSound", "liveCaptions",
        "nearbyShare", "accessibility", "security", "doNotDisturb", "wifiSwitch", "bluetoothSwitch", "soundSwitch", "microphoneSwitch",
        "gameMode", "colorFilters", "monoAudio", "magnifier", "narrator", "stickyKeys", "cast", "project", "keepAwake", "darkMode",
    })]
    // The pen menu and the virtual touchpad: when the button shows. The touch keyboard always shows.
    [InlineData(WidgetOptionsKind.InputButton, new[] { "penMenu", "virtualTouchpad" })]
    // list: a heading's words. (What a folder on this PC holds is a pin to it now: PinFolders.)
    [InlineData(WidgetOptionsKind.Heading, new[] { "heading" })]
    // list: a toggles row's toggles, an actions line's pages.
    [InlineData(WidgetOptionsKind.Toggles, new[] { "toggles" })]
    [InlineData(WidgetOptionsKind.Actions, new[] { "actions" })]
    // search: Windows' search button, the search box or only its icon.
    [InlineData(WidgetOptionsKind.WindowsSearch, new[] { "search" })]
    // §302: Show desktop, its icon or the bare corner.
    [InlineData(WidgetOptionsKind.ShowDesktop, new[] { "showDesktop" })]
    // quick: the brightness, every display or the one it is on.
    [InlineData(WidgetOptionsKind.Brightness, new[] { "brightness" })]
    // The status icons: where each icon shows, on the bar, behind its arrow or nowhere.
    [InlineData(WidgetOptionsKind.StatusIcons, new[] { "indicators" })]
    // notes: which list or which note of the notebook's a line shows.
    [InlineData(WidgetOptionsKind.Todo, new[] { "todo" })]
    [InlineData(WidgetOptionsKind.Note, new[] { "note" })]
    [InlineData(WidgetOptionsKind.Alarm, new[] { "alarm" })]
    // §347: a tray's own icons, where each shows and their order.
    [InlineData(WidgetOptionsKind.Tray, new[] { "tray" })]
    // §435: a folder's divider, the folders' look or growing.
    [InlineData(WidgetOptionsKind.Divider, new[] { "divider" })]
    // §641: a Start widget's pins, view, folders or what it recommends.
    [InlineData(WidgetOptionsKind.Start, new[] { "startPins", "startApps", "startRecommended", "startProfile" })]
    // Nothing to set beyond where it stands.
    [InlineData(WidgetOptionsKind.None, new[]
    {
        "start", "taskView", "widgetsBoard", "touchKeyboard", "apps", "wifi", "bluetooth", "hotspot", "battery", "powerMode", "keyboard", "vpnConnections",
        "sleep", "hibernate", "shutDown", "restart", "signOut", "lock", "contacts", "mail", "media", "mediaSources", "layout", "notificationsList",
        "snippingTool", "windowsSettings", "clipboard", "wirelessDisplays", "projection", "nearbySharing",
        "soundOutput", "soundInput", "soundMixer", "soundVolume", "month", "screenTime", "screenTimeApps", "screenTimeChart", "screenTimeHistory",
        "events", "meeting", "dayView", "weekView", "yearView", "stopwatch", "removableDevices", "cpuProcesses",
        "quickLink", "windowsNotifications", "windowsQuickSettings", "windowsSound", "windowsNetworks",
        "windowsCast", "windowsClipboard", "emojiPanel",
    })]
    public void AnOptionsAnswerStandsForTheseWidgets(WidgetOptionsKind options, string[] ids) =>
        Assert.Equal(
            ids.Order(StringComparer.Ordinal),
            WidgetInfos.All.Where(info => info.Options == options).Select(info => info.Id).Order(StringComparer.Ordinal));

    /// <summary>A link that leaves Karpo says where it goes, and says so in words.</summary>
    [Fact]
    public void AWindowsLinkIsWholeOrAbsent()
    {
        foreach (var info in WidgetInfos.All)
        {
            if (info.Settings is not { } link)
            {
                continue;
            }
            Assert.StartsWith("ms-settings:", link.Uri);
            Assert.EndsWith(" settings", link.Title);
        }
    }

    /// <summary>Every group of the catalogue has a header and something in it, and holds each widget once.</summary>
    [Fact]
    public void TheCategoriesCoverEveryWidgetOnce()
    {
        foreach (var category in Enum.GetValues<WidgetCategory>())
        {
            Assert.False(string.IsNullOrWhiteSpace(category.Title()));
            Assert.NotEmpty(WidgetInfos.In(category));
        }
        var grouped = Enum.GetValues<WidgetCategory>().SelectMany(WidgetInfos.In).ToList();
        Assert.Equal(WidgetInfos.All.Length, grouped.Count);
        Assert.Equal(WidgetInfos.All.Length, grouped.Distinct().Count());
    }

    /// <summary>
    /// The subject facts, written out: which widget reads which metric, stands for which switch,
    /// switches which list and shows which drives. A class that draws several widgets is handed
    /// these, so a fact on the wrong record is a graph of the wrong reading or a button that flips
    /// the wrong radio.
    /// </summary>
    [Fact]
    public void EachSubjectFactIsOnItsWidget()
    {
        var metrics = new Dictionary<string, Metric>
        {
            ["cpu"] = Metric.Cpu, ["gpu"] = Metric.Gpu, ["memory"] = Metric.Memory, ["disk"] = Metric.Disk, ["network"] = Metric.Network,
            ["cpuDetails"] = Metric.Cpu, ["gpuDetails"] = Metric.Gpu, ["memoryDetails"] = Metric.Memory, ["diskDetails"] = Metric.Disk, ["networkDetails"] = Metric.Network,
        };
        var switches = new Dictionary<string, QuickSwitch>
        {
            ["airplaneMode"] = QuickSwitch.AirplaneMode, ["mobileHotspot"] = QuickSwitch.MobileHotspot, ["vpn"] = QuickSwitch.Vpn, ["rotationLock"] = QuickSwitch.RotationLock, ["spatialSound"] = QuickSwitch.SpatialSound,
            ["energySaver"] = QuickSwitch.EnergySaver, ["nightLight"] = QuickSwitch.NightLight,
            ["liveCaptions"] = QuickSwitch.LiveCaptions,
            ["nearbyShare"] = QuickSwitch.NearbyShare, ["accessibility"] = QuickSwitch.Accessibility,
            ["doNotDisturb"] = QuickSwitch.DoNotDisturb,
            ["wifiSwitch"] = QuickSwitch.WifiRadio, ["bluetoothSwitch"] = QuickSwitch.BluetoothRadio,
            ["soundSwitch"] = QuickSwitch.Sound, ["microphoneSwitch"] = QuickSwitch.Microphone,
            ["gameMode"] = QuickSwitch.GameMode, ["colorFilters"] = QuickSwitch.ColorFilters, ["monoAudio"] = QuickSwitch.MonoAudio,
            ["magnifier"] = QuickSwitch.Magnifier, ["narrator"] = QuickSwitch.Narrator, ["stickyKeys"] = QuickSwitch.StickyKeys,
            ["cast"] = QuickSwitch.Cast, ["project"] = QuickSwitch.Project,
            ["keepAwake"] = QuickSwitch.KeepAwake,
            ["darkMode"] = QuickSwitch.DarkMode,
        };
        var switchers = new Dictionary<string, Switcher> { ["layout"] = Switcher.Layouts };
        var drives = new Dictionary<string, DriveFilter>
        {
            ["drive"] = DriveFilter.Local, ["networkDrive"] = DriveFilter.Network, ["removableDevices"] = DriveFilter.Removable,
        };
        foreach (var info in WidgetInfos.All)
        {
            Assert.Equal(Fact(metrics, info.Id), info.Metric);
            Assert.Equal(Fact(switches, info.Id), info.Switch);
            Assert.Equal(Fact(switchers, info.Id), info.Switcher);
            Assert.Equal(Fact(drives, info.Id), info.Drives);
        }
        // Every value is on one widget, and each way round finds it.
        Assert.All(Enum.GetValues<Metric>(), metric => Assert.Equal(metric, WidgetInfos.For(metric).Metric));
        Assert.All(Enum.GetValues<QuickSwitch>(), which => Assert.Equal(which, WidgetInfos.For(which).Switch));
        Assert.Equal(Enum.GetValues<Switcher>().Length, switchers.Count);
        Assert.Equal(Enum.GetValues<DriveFilter>().Length, drives.Count);

        static T? Fact<T>(Dictionary<string, T> facts, string id) where T : struct =>
            facts.TryGetValue(id, out var fact) ? fact : null;
    }

    /// <summary>
    /// A fact goes with the options the widget carries: a graph carries a graph's options, a
    /// switch a switch's, a drive a drive target (the removable devices' line, which lists whatever is
    /// plugged in, none).
    /// </summary>
    [Fact]
    public void ASubjectFactComesWithItsOptions()
    {
        foreach (var info in WidgetInfos.All)
        {
            if (info.Metric is not null)
            {
                Assert.Equal(WidgetOptionsKind.Performance, info.Options);
            }
            if (info.Switch is not null)
            {
                Assert.Equal(WidgetOptionsKind.Switch, info.Options);
            }
            if (info.Drives is { } filter)
            {
                Assert.Equal(filter == DriveFilter.Removable ? WidgetOptionsKind.None : WidgetOptionsKind.Drive, info.Options);
            }
        }
    }

    // --- Each registry --------------------------------------------------------------------------

    /// <summary>
    /// Each registry has one entry per member of its enum, in the enum's order — which is what lets
    /// a lookup be an index — and a value outside the enum has none.
    /// </summary>
    [Fact]
    public void EveryMemberHasItsEntryInItsPlace()
    {
        Assert.Equal(Enum.GetValues<BarKind>(), BarEntries.All.Select(entry => entry.Kind));
        Assert.Equal(Enum.GetValues<ListKind>(), ListWidgets.All.Select(widget => widget.Kind));

        Assert.Null(BarEntries.Find(TolerantEnumConverter<BarKind>.Unknown));
        Assert.Null(ListWidgets.Find(TolerantEnumConverter<ListKind>.Unknown));
        Assert.Null(BarEntries.Find((BarKind)BarEntries.All.Length));
        Assert.Throws<ArgumentOutOfRangeException>(() => BarEntries.Of(TolerantEnumConverter<BarKind>.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => ListWidgets.Of(TolerantEnumConverter<ListKind>.Unknown));
    }

    /// <summary>
    /// A member's word in config.json is its widget's id, both ways, and a word the registry does not
    /// know reads as a value outside the enum rather than failing the file.
    /// </summary>
    [Fact]
    public void EveryMembersWordIsItsWidgetsId()
    {
        foreach (var entry in BarEntries.All)
        {
            Words(entry.Kind, entry.Info);
            Assert.Same(entry, BarEntries.Find(entry.Info.Id));
            Assert.Equal(entry.Info.Id, entry.Kind.Id());
        }
        foreach (var widget in ListWidgets.All)
        {
            Words(widget.Kind, widget.Info);
            Assert.Same(widget, ListWidgets.Find(widget.Info.Id));
            Assert.Equal(widget.Info.Id, widget.Kind.Id());
        }
        Assert.Equal(TolerantEnumConverter<BarKind>.Unknown, JsonSerializer.Deserialize<BarKind>("\"folderRow\""));
        // desktops, layouts: the switchers' words are the lines' now, and no taskbar entry's.
        Assert.Equal(TolerantEnumConverter<BarKind>.Unknown, JsonSerializer.Deserialize<BarKind>("\"desktops\""));
        Assert.Equal(TolerantEnumConverter<BarKind>.Unknown, JsonSerializer.Deserialize<BarKind>("\"layout\""));

        static void Words<T>(T kind, WidgetInfo info) where T : struct, Enum
        {
            Assert.Equal($"\"{info.Id}\"", JsonSerializer.Serialize(kind));
            Assert.Equal(kind, JsonSerializer.Deserialize<T>($"\"{info.Id}\""));
        }
    }

    /// <summary>
    /// Every entry points at the one shared record of its id — the same object, so there is no
    /// second title to drift — and a member is named as its record is.
    /// </summary>
    [Fact]
    public void EveryEntryPointsAtItsWidgetsRecord()
    {
        var names = typeof(WidgetInfos).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(WidgetInfo))
            .ToDictionary(field => (WidgetInfo)field.GetValue(null)!, field => field.Name);

        foreach (var (kind, info) in BarEntries.All.Select(entry => (entry.Kind.ToString(), entry.Info))
            .Concat(ListWidgets.All.Select(widget => (widget.Kind.ToString(), widget.Info))))
        {
            Assert.Same(WidgetInfos.Of(info.Id), info);
            Assert.Equal(names[info], kind);
        }
    }

    // --- Taskbar entries ------------------------------------------------------------------------

    /// <summary>Only a folder opens a view, and the list folder opens the list.</summary>
    [Fact]
    public void OnlyAFolderOpensAView()
    {
        foreach (var entry in BarEntries.All)
        {
            Assert.Equal(entry.Info.Category == WidgetCategory.Folders, entry.Opens is not null);
        }
        Assert.Equal(FolderView.List, BarEntries.Of(BarKind.FolderList).Opens);
    }

    /// <summary>
    /// What the scene draws itself has no drawing of its own, and everything else names the island
    /// class that draws it.
    /// </summary>
    [Fact]
    public void WhatTheSceneDrawsNamesNoDrawing()
    {
        string[] scene =
        [
            "start", "search", "taskView", "penMenu", "touchKeyboard", "virtualTouchpad",
            "snippingTool", "windowsSettings",
            // §474, §490: not the Widgets button nor Windows' menus, which wear faces.
            "apps", "pin", "tray", "showDesktop",
        ];

        Assert.Equal(
            scene.Order(StringComparer.Ordinal),
            BarEntries.All.Where(entry => entry.Drawing == WidgetSource.None).Select(entry => entry.Info.Id).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// An entry's size is where a new one starts: a range that is a range, and a width to start at
    /// inside it where it has one. It says nothing about a folder: a line's rows are the list's own.
    /// </summary>
    [Fact]
    public void EveryEntrysSizeIsAPlaceToStart()
    {
        foreach (var entry in BarEntries.All)
        {
            var size = entry.Size;
            Assert.InRange(size.MinCells, WidgetItem.MinWidth, size.MaxCells);
            if (size.DefaultCells != WidgetItem.AutomaticWidth)
            {
                Assert.InRange(size.DefaultCells, size.MinCells, size.MaxCells);
            }
        }
    }

    /// <summary>
    /// A Windows panel is what Windows' own buttons open — Start, search and task view, the widgets
    /// board, the pen menu, the touch keyboard and the virtual touchpad: popups all the same on the
    /// bar, which is why seven of the eight are in no folder. Task view, which the person asked for in
    /// folders too, has a line whose click is an act. Project opens a page of Karpo's own, not Win+P;
    /// cast's button is the cast preset (2026-10-11, docs\widgets.md §114).
    /// </summary>
    [Fact]
    public void WhatOpensAWindowsPanel()
    {
        string[] panels =
        [
            "start", "search", "taskView", "widgetsBoard", "penMenu", "touchKeyboard", "virtualTouchpad",
            "quickLink", "windowsNotifications", "windowsQuickSettings", "windowsSound", "windowsNetworks",
            "windowsCast", "windowsClipboard", "emojiPanel",
        ];

        Assert.Equal(
            panels.Order(StringComparer.Ordinal),
            BarEntries.All.Where(entry => entry.Click == BarClick.Panel).Select(entry => entry.Info.Id).Order(StringComparer.Ordinal));
    }

    // --- List widgets ---------------------------------------------------------------------------

    /// <summary>
    /// A line does nothing or acts, and there is no third answer: a popup opening out of
    /// a folder, which is a popup itself, cannot be written (docs\widgets.md §15).
    /// </summary>
    [Fact]
    public void ALineCannotOpenAnything() =>
        Assert.Equal(new[] { "Nothing", "Action" }, Enum.GetNames<CellAct>());

    /// <summary>
    /// What acts in a folder: a pin opens, a switch flips, a graph opens Task Manager, the security
    /// widget opens Windows Security, Task view presses Win+Tab, an RSS headline opens its story.
    /// Everything else leaves the click alone, so its own controls still work.
    /// </summary>
    [Fact]
    public void WhatActsInAFolder()
    {
        string[] acting =
        [
            "pin", "folderContents", "cpu", "gpu", "memory", "disk", "network", "cpuDetails", "gpuDetails", "memoryDetails", "diskDetails", "networkDetails", "airplaneMode", "mobileHotspot", "vpn", "energySaver",
            "nightLight", "rotationLock", "spatialSound", "liveCaptions", "nearbyShare", "accessibility", "security", "doNotDisturb", "taskView",
            "wifiSwitch", "bluetoothSwitch", "soundSwitch", "microphoneSwitch", "gameMode", "colorFilters", "monoAudio",
            "magnifier", "narrator", "stickyKeys", "cast", "project", "keepAwake", "darkMode",
            "snippingTool", "windowsSettings",
            // A headline on an RSS line opens its story in the browser, as a pin opens its
            // website, and so does its site's link.
            "rss",
            // quakes: an earthquake's row opens its page on the USGS's site (§240).
            "quakes",
            // camera: a click opens the camera's page (§331).
            "camera",
            // drives: a drive's line opens the drive, and a row of its contents that item (§99).
            "drive", "networkDrive",
            // weather: a click searches the weather at the line's place in the browser (2026-10-11).
            "weatherNow", "weatherHours", "weatherHoursList", "weatherDays", "weatherDaysGraph",
            // air: a click searches the air where the line stands (2026-11-09, "i should also be able to click on the air quality widget to do a search for it").
            "airQuality", "airPollutants", "airPollen", "airForecast",
            // notifications: a card brings its app forward with the toast's arguments (§100).
            "notificationsList",
            // clipboard: a row chosen with the keys pastes its item where the person was (§101, §108).
            "clipboard",
            // pim: a message opens in its account's web mail (§108).
            "mail",
            // contacts: a contact's Email or Call hands Windows mailto: or tel: once the folder has gone (§102).
            "contacts",
            // §642: Windows search opens what it found, its location, as administrator, once the folder has gone.
            "winSearch",
            // power: a button does its command once the folder has gone (2026-10-11).
            "sleep", "hibernate", "shutDown", "restart", "signOut", "lock",
            // desktops: a desktop's row is switched to once the folder has gone (§116).
            "desktops",
            // layouts: a layout's row is switched to once the folder has gone (§127).
            "layout",
            // perf: the top processes open Task Manager, as the graphs do (§141).
            "cpuProcesses",
            // meeting: a meeting's Join opens it in its app once the folder has gone (§205).
            "meeting",
            // sports: the match opens on ESPN once the folder has gone (§239), a row of its lists its match or its team (§603).
            "sports", "sportsFixtures", "sportsTable", "sportsKnockout",
            // markets: a ticker's click searches what it tracks in the browser once the folder has gone (§263).
            "markets",
            // clocks: a click searches the time where the clock stands once the folder has gone (§377).
            "clock", "timeHeader",
            // calendar: today's date makes today the folder's day, anywhere on its line, which lights whole (§379).
            "dateHeader",
            // §641: Start's widgets open an app, a file, a folder or a power command once the folder has gone.
            "startPins", "startApps", "startRecommended", "startProfile",
            // screentime: an app's row picks the app in the folder and opens nothing (2026-10-10);
            // opening it is its right click's.
        ];

        Assert.Equal(
            acting.Order(StringComparer.Ordinal),
            ListWidgets.All.Where(widget => widget.Act == CellAct.Action).Select(widget => widget.Info.Id).Order(StringComparer.Ordinal));
    }

    /// <summary>Every line names the island class that draws it or acts for it.</summary>
    [Fact]
    public void EveryLineNamesWhatDrawsIt() =>
        Assert.All(ListWidgets.All, widget => Assert.NotEqual(WidgetSource.None, widget.Drawing));

    /// <summary>
    /// No list line without content (ruled 2026-09-26): the list's own row is a glyph and a name, so
    /// it is only there for a widget whose click acts, and it is one line whatever it is given.
    /// </summary>
    [Fact]
    public void AListRowIsThereToAct()
    {
        foreach (var widget in ListWidgets.All.Where(widget => widget.Line == ListLine.Row))
        {
            Assert.Equal(CellAct.Action, widget.Act);
            Assert.Equal(LineHeight.Row, widget.Cells);
            Assert.Equal((LineHeight.Row, LineHeight.Row), widget.Range);
        }
    }

    /// <summary>
    /// A card on a line starts at a height it can be worked in, up to no more than a list's own
    /// height, and says how tall it is where nothing has measured it.
    /// </summary>
    [Fact]
    public void ACardOnALineStartsWhereItCanBeWorked()
    {
        foreach (var widget in ListWidgets.All.Where(widget => widget.Line == ListLine.Card))
        {
            Assert.InRange(widget.Cells, 1, FolderOptions.Most);
            Assert.InRange(widget.Range.Least, 1, widget.Range.Most);
            Assert.InRange(widget.Range.Most, widget.Range.Least, FolderOptions.MostCells);
        }
    }

    // --- What a new widget carries --------------------------------------------------------------

    /// <summary>
    /// Which property an options answer stands for. The first ones every placed widget has; the
    /// last four only a taskbar's entry (<see cref="BarItem"/>).
    /// </summary>
    private static readonly (WidgetOptionsKind Kind, Func<WidgetItem, object?> Read)[] Carried =
    [
        (WidgetOptionsKind.Pin, widget => widget.Pin),
        (WidgetOptionsKind.Drive, widget => widget.Drive),
        (WidgetOptionsKind.Weather, widget => widget.Weather),
        (WidgetOptionsKind.Market, widget => widget.Market),
        (WidgetOptionsKind.Rss, widget => widget.Rss),
        (WidgetOptionsKind.Calendar, widget => widget.Calendar),
        (WidgetOptionsKind.Clock, widget => widget.Clock),
        (WidgetOptionsKind.Performance, widget => widget.Performance),
        (WidgetOptionsKind.Desktops, widget => widget.Desktops),
        (WidgetOptionsKind.Volume, widget => widget.Volume),
        (WidgetOptionsKind.Switch, widget => widget.Switch),
        (WidgetOptionsKind.Heading, widget => widget.Heading),
        (WidgetOptionsKind.Folder, widget => (widget as BarItem)?.Folder),
    ];

    /// <summary>
    /// A new widget of every registry carries exactly the options record its widget declares
    /// (<see cref="WidgetInfo.Options"/>) and none of the others, whichever type it is made as:
    /// <see cref="WidgetItem.Normalize"/> and the registry have to say the same thing, or a
    /// widget's settings page edits a record nothing reads.
    /// </summary>
    [Fact]
    public void ANewWidgetCarriesExactlyTheOptionsItsWidgetDeclares()
    {
        (string Name, WidgetItem Widget)[] made =
        [
            .. BarEntries.All.Select(entry => ($"taskbar {entry.Kind}", (WidgetItem)entry.Kind.New())),
            .. ListWidgets.All.Select(line => ($"line {line.Kind}", (WidgetItem)line.Kind.New())),
        ];
        foreach (var (name, widget) in made)
        {
            var declared = Assert.IsType<WidgetInfo>(widget.Info).Options;
            foreach (var (carried, read) in Carried)
            {
                var held = read(widget) is not null;
                Assert.True(held == (carried == declared), $"{name}: {carried} options are {(held ? "" : "not ")}there, and it declared {declared}");
            }
        }
    }

    /// <summary>
    /// The lookups a hundred callers use keep reading for a taskbar kind this build does not know
    /// (one a later build wrote): nothing is found, and each answers the fall-back its callers were
    /// written against, until the file is repaired and the entry goes.
    /// </summary>
    [Fact]
    public void AKindThisBuildDoesNotKnowStillReads()
    {
        var unknown = TolerantEnumConverter<BarKind>.Unknown;

        Assert.Null(BarEntries.Find(unknown));
        Assert.Equal("-1", unknown.Title());
        Assert.Equal("\uE917", unknown.Glyph());
        // A kind this build cannot draw has no size it could show, and as a taskbar entry it has no
        // size to keep: a width a file wrote for it goes, as every entry's does (BarItem.Normalize).
        Assert.False(unknown.HasSize());
        // The end of the bar, with the clock and the tray, as every kind Windows has no place for.
        Assert.Equal(BarSection.End, unknown.SectionOf());
        Assert.False(unknown.IsIsland());
        Assert.False(unknown.IsFolder());
        Assert.False(unknown.TakesDrags());
        Assert.False(unknown.TakesWheel());
        var entry = new BarItem { Id = "later", Kind = unknown, Height = LineHeight.Exactly(6) };
        entry.Normalize();
        Assert.Null(entry.Info);
        Assert.Null(entry.Height);
    }
}
