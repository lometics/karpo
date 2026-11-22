// The add list's tree (docs\widgets.md §7b, "The tree"; 2026-10-11). The person, on how the add list
// is arranged: "you are 100% free to regroup completely how it works. dont get influenced by how the
// groups are at the moment. do it so that it makes the most sense to a new user." and "i dont think
// that ready made groups should really be a group now anymore".
//
// So the list asks the one question a new person has — what do I want to add? — and answers it by
// subject: Time and calendar, Weather and news, Music and sound, Connections, … each a folder of what
// there is of it, a ready-made folder (a preset) beside the single widgets it is made of, with no
// "ready-made" of its own. The registries' groups (WidgetCategory) and the families before them were
// the library's order, not a question anybody asks.
//
// One tree, declared here as data, for both pages: the bar's page and a folder's page each keep what
// they have of it (a taskbar entry, a list widget, a premade widget, a preset) and drop the rest. A
// folder left with nothing is not there; a folder left with one row is that row (the bar's
// "Mobile hotspot" is its preset alone, a folder's the switch, its details and its actions). Every
// widget, premade widget and preset stands in it exactly once, its home; two places only show some
// of them again, on purpose (Again): Popular, a fixed handful at the top (the person: "fixed list"),
// and Quick settings, the toggles together as Windows' quick settings have them (the person: "keep
// the quick settings folder"), each toggle's home being its subject's. AddTreeTests hold all of it,
// and that nothing is left out: a widget or a preset added to a registry and not here fails them, and
// meanwhile stands in "More" at the end of the list, never missing.
//
// The list shows the tree as cascading menus, as Windows' "New ›" is (Karpo.Dock\AddList.cs): its
// top in two columns, Popular and Browse, each folder a row opening a submenu. Its search lists what
// matches flat, each with the folders it is in (AddList.Results).
//
// §490 (the person, 2026-11-02: "go over the widget groups in the add dialog and make sure that there are
// generally speaking not more than 2 levels, only if required up to three but not more"): two levels — the
// list's top and a subject's submenu of picks. No subject holds a folder; a long one is parted by lines into
// runs (AddTreeTests: a third level only where named, none).

namespace Karpo.Core;

/// <summary>What one row of the add list's tree is: a folder, a pick or a line.</summary>
public abstract record AddNode;

/// <summary>A folder of the tree: a row opening a submenu of its rows; at the top, a column under its heading.</summary>
public sealed record AddShelf : AddNode
{
    /// <summary>Its name in code and in its words' key (<c>core.addTree.&lt;id&gt;.title</c>); never stored.</summary>
    public required string Id
    {
        get => id;
        init
        {
            id = value;
            titleKey = $"core.addTree.{value}.title";
        }
    }

    private readonly string id = "";
    private readonly string titleKey = "";

    /// <summary>What the list calls it, in the language Karpo speaks now.</summary>
    public string Title => Strings.Text(titleKey);

    /// <summary>Its English name, where Karpo speaks another language, for the search; nothing in English.</summary>
    public IEnumerable<string> EnglishWords => Strings.AlsoEnglish(titleKey);

    /// <summary>A Segoe Fluent Icons code point: its row's icon; empty for a column's heading.</summary>
    public string Glyph { get; init; } = "";

    /// <summary>Its rows, in the order the submenu lists them.</summary>
    public required IReadOnlyList<AddNode> Rows { get; init; }
}

/// <summary>What a pick of the tree names.</summary>
public enum AddWhat
{
    /// <summary>A widget of the page's own registry, by its word: a taskbar entry, or a list widget.</summary>
    Widget,

    /// <summary>A premade widget (<see cref="PremadeWidgets"/>), by its id: a folder's page only.</summary>
    Premade,

    /// <summary>A ready-made folder (<see cref="Presets"/>), by its id: the bar's page only.</summary>
    Preset,
}

/// <summary>
/// A row of the tree that adds something. <paramref name="Again"/>: it is shown here once more, its
/// home being elsewhere in the tree — Popular's and Quick settings' rows.
/// </summary>
public sealed record AddLeaf(AddWhat What, string Id, bool Again = false) : AddNode
{
    /// <summary>What it names across the pages: a widget's word on its page, a premade widget's or a preset's id.</summary>
    public string KeyOn(WidgetType page) => What switch
    {
        AddWhat.Premade => $"premade/{Id}",
        AddWhat.Preset => $"preset/{Id}",
        _ => CatalogueEntry.KeyOf(page, Id),
    };
}

/// <summary>A line between two runs of a submenu's rows.</summary>
public sealed record AddLine : AddNode;

/// <summary>A row of the tree as a page has it: a pick, a folder with its rows, or a line.</summary>
public abstract record AddRow;

/// <summary>
/// A row that adds: a widget or a premade widget (<see cref="Entry"/>) or a ready-made folder
/// (<see cref="Preset"/>), exactly one of them. <see cref="Again"/> as <see cref="AddLeaf.Again"/>.
/// </summary>
public sealed record AddPick(CatalogueEntry? Entry, Preset? Preset, bool Again, string Key) : AddRow
{
    public string Title => Entry?.Title ?? Preset!.Title;

    public string Glyph => Entry?.Glyph ?? Preset!.Glyph;
}

/// <summary>A folder as a page has it: two rows or more.</summary>
public sealed record AddFolder(AddShelf Shelf, IReadOnlyList<AddRow> Rows) : AddRow;

/// <summary>A line as a page has it: always between two rows.</summary>
public sealed record AddRule : AddRow;

/// <summary>A column of the list's top as a page has it: its heading's shelf and its rows.</summary>
public sealed record AddSection(AddShelf Shelf, IReadOnlyList<AddRow> Rows);

/// <summary>The tree, and what a page has of it.</summary>
public static class AddTree
{
    private static AddLeaf W(string id) => new(AddWhat.Widget, id);

    private static AddLeaf M(string id) => new(AddWhat.Premade, id);

    private static AddLeaf P(string id) => new(AddWhat.Preset, id);

    private static AddLeaf Again(AddLeaf leaf) => leaf with { Again = true };

    private static readonly AddLine Line = new();

    private static AddShelf Shelf(string id, string glyph, params AddNode[] rows) => new() { Id = id, Glyph = glyph, Rows = rows };

    /// <summary>
    /// The toggles together, as Windows' quick settings show them: what they can be put in — the
    /// Quick settings ready-made folder, the row of toggles — then a line and every widget whose click flips it where it stands
    /// (<see cref="WidgetInfo.Flips"/>, 2026-10-11: "the ones in the toggles group should only be the
    /// ones that you can click and click again to untoggle"), each at home in its subject too. The one
    /// folder longer than twelve rows, since it is every toggle: lines part them by subject — the
    /// radios, the sound, the screen, the PC.
    /// </summary>
    public static readonly AddShelf QuickSettings = Shelf("quick", WidgetInfos.Toggles.Glyph,
        P(Presets.QuickPanelId), W("toggles"),
        Line,
        Again(W("wifiSwitch")), Again(W("bluetoothSwitch")), Again(W("airplaneMode")),
        Line,
        Again(W("soundSwitch")), Again(W("microphoneSwitch")), Again(W("spatialSound")), Again(W("monoAudio")),
        Line,
        Again(W("nightLight")), Again(W("darkMode")), Again(W("colorFilters")), Again(W("rotationLock")),
        Line,
        Again(W("energySaver")), Again(W("keepAwake")), Again(W("doNotDisturb")), Again(W("gameMode")),
        Line,
        Again(W("magnifier")), Again(W("narrator")), Again(W("stickyKeys")));

    /// <summary>
    /// The first column: what most people add first, each again where it is at home (the person:
    /// "fixed list"), then what a bar or a folder is arranged with — a folder, a folder's heading and
    /// its separator — whose home this is.
    /// </summary>
    public static readonly AddShelf Popular = Shelf("popular", "",
        Again(P(Presets.ClockId)), Again(W("clock")),
        Again(P(Presets.CalendarId)), Again(W("calendar")),
        Again(P(Presets.WeatherId)), Again(W("weatherNow")),
        Again(P(Presets.SoundId)), Again(W("soundVolume")),
        Again(P(Presets.WifiId)),
        Again(P(Presets.BatteryId)), Again(W("battery")),
        Again(P(Presets.MediaId)), Again(W("media")),
        Again(W("note")), Again(W("todo")),
        Again(W("pin")),
        Line,
        W("folderList"), W("heading"), W("divider"));

    /// <summary>
    /// The second column: every subject, a folder each, in the order a new person looks for them. §490 (the person,
    /// 2026-11-02: "make sure that there are generally speaking not more than 2 levels, only if required up to three but
    /// not more"): each subject is its picks, no folder in it — what were folders of their own in a subject (the
    /// calendar, the sun, Wi-Fi, Bluetooth, VPN, the hotspot, the power buttons, nearby sharing, performance details,
    /// screen time) are runs of its rows between lines; Accessibility and Windows menus are subjects of their own, and
    /// Keyboard and touch's rows are Start and taskbar's.
    /// </summary>
    public static readonly AddShelf Browse = Shelf("browse", "",
        Shelf("time", WidgetInfos.Clock.Glyph,
            P(Presets.ClockId), W("clock"), P(Presets.ClockCalendarId), P(Presets.NotificationCenterId), P(Presets.TodayId), P(Presets.WorldClockId), W("alarm"),
            P(Presets.TimerId), W("timer"), P(Presets.FocusId), W("focus"), P(Presets.StopwatchId), W("stopwatch"),
            P("focusGroup"), M("clockActions"),
            Line,
            // The calendar's run. meeting: the next meeting's ready-made folder beside the calendar's (§205); its line is
            // the calendar's family. dateHeader: today's date, a widget of its own since §318 (a click goes back to today).
            P(Presets.CalendarId), W("calendar"), W("dateHeader"), P(Presets.MeetingId), W("meeting"), M("calendarActions")),
        // §590: the weather's three widgets — the current weather, the hourly and the daily forecast — after its ready-made
        // folder, a line, then the air's: its ready-made folder and its four (§210, §590), a line; tides: the tides' after
        // them (§236); the sun's after the earthquakes' (§368), its times widgets each (§404); a line, then the news.
        Shelf("weather", WidgetInfos.WeatherNow.Glyph,
            P(Presets.WeatherId), W("weatherNow"), W("weatherHours"), W("weatherDays"),
            Line,
            P(Presets.AirQualityId), W("airQuality"), W("airPollutants"), W("airPollen"), W("airForecast"),
            Line,
            P(Presets.TidesId), W("tides"), W("tideGraph"), P(Presets.QuakesId), W("quakes"),
            P(Presets.SunId), W("sunArc"), W("sunrise"), W("sunset"), W("daylight"), W("solarNoon"), W("dawn"), W("dusk"),
            Line,
            // camera: a live webcam's picture, a line of its own (§331).
            W("camera"),
            // markets: one ready-made folder, the shares (§648), and one line for a share, a rate and a coin (§246).
            P(Presets.NewsId), W("rss"), P(Presets.MarketId), W("markets"), P(Presets.MarketsNewsId),
            // sports: a team's ready-made folder and its widgets after the news and the markets (§239), a run of their
            // own: the score, its fixtures and results, its table and a cup's knockout, each a widget since §603.
            Line,
            P(Presets.SportsId), W("sports"), W("sportsFixtures"), W("sportsTable"), W("sportsKnockout")),
        Shelf("sound", WidgetInfos.SoundVolume.Glyph,
            P(Presets.MediaId), P(Presets.MediaCenterId), W("media"), W("mediaSources"),
            // The volume and the mixer, and the output devices and the microphones beside them, each a
            // widget of its own: which device is no level (§270), each app's level not the system's (§318).
            P(Presets.SoundId), W("volume"), W("soundVolume"), W("soundMixer"), W("soundOutput"), W("soundInput"),
            W("soundSwitch"), W("microphoneSwitch"), W("spatialSound"), W("monoAudio"), M("soundActions")),
        // The network's runs: Wi-Fi and VPN, Bluetooth, the hotspot with airplane mode.
        Shelf("connections", WidgetInfos.Wifi.Glyph,
            P(Presets.NetworkHubId),
            P(Presets.WifiId), W("wifi"), W("wifiSwitch"), M("wifiActions"),
            P(Presets.VpnId), W("vpn"), W("vpnConnections"), M("vpnActions"),
            Line,
            P(Presets.BluetoothId), P(Presets.DevicesId), W("bluetooth"), W("bluetoothSwitch"), M("bluetoothActions"),
            Line,
            P(Presets.HotspotId), W("mobileHotspot"), W("hotspot"), M("hotspotActions"),
            W("airplaneMode"), P("connectivityGroup")),
        QuickSettings,
        Shelf("power", WidgetInfos.ShutDown.Glyph,
            P(Presets.PowerBatteryId), P(Presets.BatteryId), W("battery"), W("energySaver"), W("powerMode"), W("keepAwake"), P(Presets.PowerId), M("powerActions"), M("quickActions"),
            Line,
            W("sleep"), W("hibernate"), W("shutDown"), W("restart"), W("signOut"), W("lock")),
        Shelf("screen", WidgetInfos.WirelessDisplays.Glyph,
            W("brightness"), W("nightLight"), W("darkMode"), W("rotationLock"),
            Line,
            P(Presets.CastId), W("cast"), W("wirelessDisplays"), M("castActions"),
            P(Presets.ProjectId), W("project"), W("projection"), M("projectActions"),
            P(Presets.NearbyId), W("nearbyShare"), W("nearbySharing"), M("nearbyActions")),
        Shelf("accessibility", WidgetInfos.Accessibility.Glyph,
            W("accessibility"), W("magnifier"), W("colorFilters"), W("narrator"), W("liveCaptions"), W("stickyKeys"), M("accessibilityActions")),
        Shelf("system", WidgetInfos.Cpu.Glyph,
            P("systemGroup"),
            // The readings are one widget, Performance, its reading and its style chosen on it
            // (WidgetStyles, §228), offered a row a reading (§283; the person: "it should still be
            // one widget but with different configurations depending on what i add"); each
            // reading's ready-made folder stays its own. Performance details, a widget of their own
            // since §318, the same: a row a reading, a run of their own after them.
            P(Presets.CpuId), P(Presets.GpuId), P(Presets.MemoryId), P(Presets.DiskId), P(Presets.NetworkActivityId),
            W(WidgetInfos.Cpu.Id), W(WidgetInfos.Gpu.Id), W(WidgetInfos.Memory.Id), W(WidgetInfos.Disk.Id), W(WidgetInfos.Network.Id),
            Line,
            W(WidgetInfos.CpuDetails.Id), W(WidgetInfos.GpuDetails.Id), W(WidgetInfos.MemoryDetails.Id), W(WidgetInfos.DiskDetails.Id), W(WidgetInfos.NetworkDetails.Id),
            Line,
            W("cpuProcesses"),
            M("taskManagerActions"), W("security"), W("gameMode"),
            // The person (2026-10-12): "screen time and app usage should go under one group". Today's and the
            // fortnight's, a widget each since §603.
            P("screenTimeGroup"), W("screenTime"), W("screenTimeHistory"), W("screenTimeChart"), W("screenTimeApps")),
        Shelf("notes", WidgetInfos.Note.Glyph,
            P(Presets.NotebookId), P("todoGroup"), W("todo"), P("notesGroup"), W("note"),
            P(Presets.InboxId), P(Presets.MailId), W("mail"), P(Presets.NotificationsId), W("notificationsList"), M("notificationActions"),
            W("doNotDisturb"), P(Presets.ContactsId), W("contacts")),
        Shelf("files", WidgetInfos.Drive.Glyph,
            W("pin"), W("folderContents"), P(Presets.DriveId), W("drive"), P(Presets.NetworkDriveId), W("networkDrive"),
            P(Presets.RemovableId), W("removableDevices"), P("storageGroup"),
            P(Presets.ClipboardId), W("clipboard"), M("clipboardActions"),
            W("snippingTool"), W("windowsSettings"), W("actions")),
        // Start's three first, as the taskbar has them; the keyboard's layout as Windows' taskbar shows it. §641 then a run of
        // Start's own: the ready-made Start (beta) and its four widgets, each marked Beta; §642 Windows search (beta) after
        // them, the folder whose face is a search box and its line, as the taskbar has search after Start.
        Shelf("taskbar", WidgetInfos.Start.Glyph,
            W("start"), W("search"), W("taskView"),
            Line,
            P(Presets.StartId), W(WidgetInfos.StartPins.Id), W(WidgetInfos.StartApps.Id), W(WidgetInfos.StartRecommended.Id),
            W(WidgetInfos.StartProfile.Id),
            P(Presets.WinSearchId), W(WidgetInfos.WinSearch.Id),
            Line,
            W("apps"), W("tray"), W("indicators"), W("showDesktop"),
            P(Presets.DesktopsId), W("desktops"), M("desktopActions"), P(Presets.LayoutsId), W("layout"),
            P(Presets.KeyboardId), W("keyboard"), M("keyboardActions")),
        // windowsMenus: Windows' own menus together, a button each that opens Windows' popup (§466; the person,
        // 2026-11-01: "they should be grouped together"), a subject of their own (§490: "the windows menus should get
        // their own top level entry and not be hidden three levels down"); after them Windows' input panels, which its
        // taskbar's buttons open as well.
        Shelf("windowsMenus", OptionGlyphs.Windows,
            W("widgetsBoard"), W("quickLink"), W("windowsNotifications"), W("windowsQuickSettings"), W("windowsSound"), W("windowsNetworks"),
            W("windowsCast"), W("windowsClipboard"), W("emojiPanel"),
            Line,
            W("touchKeyboard"), W("virtualTouchpad"), W("penMenu")));

    /// <summary>The list's top: its two columns.</summary>
    public static readonly AddShelf[] Sections = [Popular, Browse];

    /// <summary>Where what the tree leaves out stands meanwhile: a folder at Browse's end, never missing (AddTreeTests fail first).</summary>
    public static readonly AddShelf More = new() { Id = "more", Glyph = "", Rows = [] };


    /// <summary>
    /// What <paramref name="page"/> has of the tree: each column with its rows, a folder only where it
    /// keeps two rows or more (one is that row; none, no folder), a line only between two rows. What
    /// the page has and the tree does not stands in <see cref="More"/>, at Browse's end.
    /// </summary>
    public static IReadOnlyList<AddSection> On(WidgetType page)
    {
        var members = Members(page);
        var placed = new HashSet<string>(StringComparer.Ordinal);
        var sections = new List<AddSection>();
        foreach (var section in Sections)
        {
            var rows = Resolve(section.Rows, page, members, placed);
            if (section == Browse)
            {
                var left = members.Where(member => !placed.Contains(member.Key)).Select(member => (AddRow)member.Value).ToList();
                if (Fold(More, left) is { } more)
                {
                    rows.Add(more);
                }
            }
            if (rows.Count > 0)
            {
                sections.Add(new AddSection(section, rows));
            }
        }
        return sections;
    }

    /// <summary>
    /// Everything <paramref name="page"/> offers, each once, by its key: the page's registry
    /// (<see cref="Catalogue.On"/>), a folder's premade widgets, the bar's presets.
    /// </summary>
    public static IReadOnlyDictionary<string, AddPick> Members(WidgetType page)
    {
        var members = new Dictionary<string, AddPick>(StringComparer.Ordinal);
        foreach (var entry in Catalogue.On(page))
        {
            members[entry.Key] = new AddPick(entry, null, false, entry.Key);
        }
        if (page == WidgetType.List)
        {
            foreach (var premade in PremadeWidgets.All)
            {
                var key = new AddLeaf(AddWhat.Premade, premade.Id).KeyOn(page);
                members[key] = new AddPick(CatalogueEntry.Of(premade), null, false, key);
            }
        }
        foreach (var preset in Presets.On(page))
        {
            var key = new AddLeaf(AddWhat.Preset, preset.Id).KeyOn(page);
            members[key] = new AddPick(null, preset, false, key);
        }
        return members;
    }

    /// <summary>Every pick of <paramref name="rows"/>, a folder's in its place, in order.</summary>
    public static IEnumerable<AddPick> Picks(IEnumerable<AddRow> rows)
    {
        foreach (var row in rows)
        {
            if (row is AddPick pick)
            {
                yield return pick;
            }
            else if (row is AddFolder folder)
            {
                foreach (var inner in Picks(folder.Rows))
                {
                    yield return inner;
                }
            }
        }
    }

    private static List<AddRow> Resolve(IReadOnlyList<AddNode> nodes, WidgetType page, IReadOnlyDictionary<string, AddPick> members, HashSet<string> placed)
    {
        var rows = new List<AddRow>();
        foreach (var node in nodes)
        {
            switch (node)
            {
                case AddLeaf leaf when members.TryGetValue(leaf.KeyOn(page), out var pick):
                    if (!leaf.Again)
                    {
                        placed.Add(pick.Key);
                    }
                    rows.Add(pick with { Again = leaf.Again });
                    break;
                case AddLine:
                    rows.Add(new AddRule());
                    break;
                case AddShelf shelf when Fold(shelf, Resolve(shelf.Rows, page, members, placed)) is { } folded:
                    rows.Add(folded);
                    break;
            }
        }
        return Tidy(rows);
    }

    /// <summary><paramref name="shelf"/> with <paramref name="rows"/>: a folder of two or more, the row itself of one, nothing of none.</summary>
    private static AddRow? Fold(AddShelf shelf, List<AddRow> rows)
    {
        rows = Tidy(rows);
        return rows.Count(row => row is not AddRule) switch
        {
            0 => null,
            1 => rows.Single(row => row is not AddRule),
            _ => new AddFolder(shelf, rows),
        };
    }

    /// <summary>The lines that stand between two rows; none first, last or beside another.</summary>
    private static List<AddRow> Tidy(List<AddRow> rows)
    {
        var tidy = new List<AddRow>(rows.Count);
        foreach (var row in rows)
        {
            if (row is AddRule && (tidy.Count == 0 || tidy[^1] is AddRule))
            {
                continue;
            }
            tidy.Add(row);
        }
        if (tidy.Count > 0 && tidy[^1] is AddRule)
        {
            tidy.RemoveAt(tidy.Count - 1);
        }
        return tidy;
    }
}
