// What a widget is, wherever it stands (docs\widgets.md §3; the split, S5).
//
// Karpo had one kind per widget and one descriptor for it (Widgets.cs), and the kind said where
// it could be shown with a set of flags: the bar, a line of a list folder (and, until 2026-10-09,
// a card of a grid folder). The split makes those places types — a taskbar entry and a list
// widget — each with a registry of its own (BarEntries, ListWidgets), because the two differ in
// more than where they stand: only a taskbar entry can open a flyout, carry a tooltip or belong to
// a run of the bar, and a line can act or do nothing.
//
// What the two share is the subject. A clock on the bar and a clock line are one thing to the
// person: one name, one glyph, one line saying what it does, the same words to search for, and
// one options record. That is this record, one per id, and every registry's entry points at it,
// so the registries cannot disagree about a title.
//
// Nothing here says where a widget may stand. That is membership, and membership is the
// registries: a widget can stand on a surface exactly when that surface's registry has it.
//
// The one table that came before these (Widgets.All, one Section per kind) went with the split's
// last step (S5, step 6b): a placed widget is a taskbar's entry (BarItem, of a BarKind) or a
// folder's widget (CellItem, a line of its list), and asks this record what
// it is.

namespace Karpo.Core;

/// <summary>
/// perf: which reading a performance graph shows. One per graph, and the graph's record is the only
/// source (<see cref="WidgetInfo.Metric"/>), so a sampler, a shape and a settings card cannot
/// disagree about which graph is the processor.
/// </summary>
public enum Metric
{
    Cpu,
    Gpu,
    Memory,
    Disk,
    Network,
}

/// <summary>
/// quick: which of Windows' switches a one-button widget stands for
/// (<see cref="WidgetInfo.Switch"/>). One per button, whether the button is on the bar, a card or a
/// line: what it switches is the subject's, not the drawing's.
/// </summary>
public enum QuickSwitch
{
    AirplaneMode,
    MobileHotspot,
    EnergySaver,
    NightLight,
    DoNotDisturb,
    LiveCaptions,
    NearbyShare,
    Accessibility,
    Vpn,
    RotationLock,
    SpatialSound,
    WifiRadio,
    BluetoothRadio,
    Sound,
    Microphone,
    GameMode,
    ColorFilters,
    MonoAudio,
    Magnifier,
    Narrator,
    StickyKeys,
    Cast,
    Project,
    KeepAwake,
    DarkMode,
}

/// <summary>
/// power: which command of Windows' power menu a power button does (<see cref="WidgetInfo.Power"/>),
/// in the order Karpo lists them: the menu's own, then the account's.
/// </summary>
public enum PowerCommand
{
    Sleep,
    Hibernate,
    ShutDown,
    Restart,
    SignOut,
    Lock,
}

/// <summary>
/// Which list a switcher names the one you are on of, and switches between
/// (<see cref="WidgetInfo.Switcher"/>): the bar's own layouts. The virtual desktops were the other
/// until the desktop switcher became the desktops preset (2026-10-11), a folder wearing the
/// desktop's name whose list is the desktops' line.
/// </summary>
public enum Switcher
{
    Layouts,
}

/// <summary>
/// drives: which drives a drive widget may point at and lists (<see cref="WidgetInfo.Drives"/>):
/// the ones in this PC and the cards and sticks in it, a network drive or share, or what can be
/// taken out.
/// </summary>
public enum DriveFilter
{
    Local,
    Network,
    Removable,
}

/// <summary>
/// What a widget is, apart from where it stands: its name, its glyph, what it is for and what it
/// carries. One per id, shared by the taskbar entry and the list widget of that id
/// (<see cref="BarEntry.Info"/>, <see cref="ListWidget.Info"/>).
/// </summary>
public sealed record WidgetInfo
{
    /// <summary>
    /// The word config.json stores, on a bar and in a folder alike, which never changes once it is
    /// released: files outlive builds in both directions. One id may name a member of all three
    /// types ("clock"); the list an item stands in says which it is.
    /// </summary>
    public required string Id
    {
        get => id;
        init
        {
            id = value;
            words = $"core.widget.{value}";
            titleKey = words + ".title";
            blurbKey = words + ".blurb";
            keywordsKey = words + ".keywords";
        }
    }

    private readonly string id = "";
    private readonly string words = "";
    private readonly string titleKey = "";
    private readonly string blurbKey = "";
    private readonly string keywordsKey = "";

    /// <summary>
    /// Where its words are in Karpo.Core\Strings\*.json: <c>core.widget.&lt;id&gt;</c>, then
    /// <c>.title</c>, <c>.blurb</c> and <c>.keywords</c>. Keyed by the id, so the record holds no
    /// text and every word below is read in the language Karpo speaks when it is asked for
    /// (docs\widgets.md §36).
    /// </summary>
    public string WordsKey => words;

    /// <summary>What the catalogue, the side menu and the widget's own page call it.</summary>
    public string Title => Strings.Text(titleKey);

    /// <summary>
    /// What the add list calls it, and only the add list: a toggle's (<see cref="Flips"/>) says it is
    /// the switch, "Microphone on/off", so it stands apart from the thing's other widgets there; its
    /// line, its tooltip and its page say its <see cref="Title"/>, "Microphone" (the person,
    /// 2026-10-11: "dont actually change the text of the widget to Microphone on/off, it should just
    /// show up like that in the add widgets lists").
    /// </summary>
    public string AddTitle => Flips ? S.CoreAddTreeOnOff(Title) : Title;

    /// <summary>A Segoe Fluent Icons code point standing for it.</summary>
    public required string Glyph { get; init; }

    /// <summary>One line saying what it does, under its name in the catalogue.</summary>
    public string Blurb => Strings.Text(blurbKey);

    /// <summary>
    /// The words someone might search for that are not in the name or the blurb — "internet" for
    /// Wi-Fi, "disk" for a drive, "timer" for the clock. Every widget has them, so a widget cannot
    /// be added without saying what it is called when somebody is looking for something else. In
    /// another language, its own words and then English's: somebody who knows the widget's English
    /// name finds it by that too.
    /// </summary>
    public string[] Keywords => Strings.List(keywordsKey);

    /// <summary>Its English name and line, where Karpo speaks another language, for the search; nothing in English.</summary>
    public IEnumerable<string> EnglishWords => Strings.AlsoEnglish(titleKey, blurbKey);

    /// <summary>Its English add-list name where that is not its English name, where Karpo speaks another language; nothing else.</summary>
    public IEnumerable<string> EnglishAddWords => Flips
        ? Strings.AlsoEnglish("core.addTree.onOff").Select(words => words.Replace("{name}", Strings.English(titleKey), StringComparison.Ordinal))
        : [];

    /// <summary>Which group of the catalogue it is shown in.</summary>
    public required WidgetCategory Category { get; init; }

    /// <summary>
    /// Which options record it carries (<see cref="WidgetOptionsKind"/>). The same on every
    /// surface, because the record describes the subject — a place, a symbol, a target — and not
    /// the drawing: it is what goes with a widget when it moves from one type to another.
    /// </summary>
    public required WidgetOptionsKind Options { get; init; }

    /// <summary>Where Windows keeps the settings behind it, or null where Windows has none.</summary>
    public required WindowsLink? Settings { get; init; }

    // The subject facts: what shared code used to ask by switching on the kind. A class that draws
    // several widgets — one graph class for five readings, one switch class for every button — is
    // handed the fact, and a new widget is one record with its fact rather than an arm in every
    // switch that knew the old ones.

    /// <summary>perf: the reading it shows, for the five graphs and their details; null for everything else.</summary>
    public Metric? Metric { get; init; }

    /// <summary>
    /// perf: whether it shows the facts behind its reading's figure — the processor's speed, its
    /// processes, threads and handles, the memory committed — rather than the reading's graph. The
    /// details' five widgets (2026-10-11), each its reading's (<see cref="Metric"/>).
    /// </summary>
    public bool Facts { get; init; }

    /// <summary>quick: the switch it stands for, for the one-button widgets; null for everything else.</summary>
    public QuickSwitch? Switch { get; init; }

    /// <summary>
    /// quick: whether it is a toggle (2026-10-11: "the ones in the toggles group should only be the
    /// ones that you can click and click again to untoggle, directly on the taskbar without opening a
    /// flyout or folder or anything"): a click flips what it stands for on or off where it stands — on
    /// the bar and as a folder's line — opening nothing, and its glyph shows which. The add list's
    /// Quick settings (<see cref="AddTree.QuickSettings"/>). VPN (a list on the bar), live
    /// captions, accessibility and project (a shortcut or a page), and the hotspot's and nearby
    /// sharing's switches (lines only: on the bar each is its preset) are switches that are not.
    /// </summary>
    public bool Flips { get; init; }

    /// <summary>power: the command it does, for the power buttons; null for everything else.</summary>
    public PowerCommand? Power { get; init; }

    /// <summary>The list it switches between, for the two switchers; null for everything else.</summary>
    public Switcher? Switcher { get; init; }

    /// <summary>drives: the drives it may point at or lists, for the three drive widgets; null for everything else.</summary>
    public DriveFilter? Drives { get; init; }

    /// <summary>
    /// calendar: whether it shows the time of day. The clock and the date share one options record
    /// (<see cref="ClockOptions"/>), and the settings that are about the time itself mean nothing
    /// to a widget that shows only the date.
    /// </summary>
    public bool ShowsTime { get; init; }

    /// <summary>
    /// §641: new and still being shaped: the add page marks it "Beta" on its tile (the person, 2026-11-09: "mark them as
    /// beta in the add"). The Start group's widgets, and §642's Windows search.
    /// </summary>
    public bool Beta { get; init; }
}

/// <summary>
/// Every widget Karpo has, one <see cref="WidgetInfo"/> each, in the order a bar shows them when
/// nobody has moved anything (the registries keep this order among their own members).
/// </summary>
public static class WidgetInfos
{
    /// <summary>
    /// Windows' widgets board (Win+W): the news, the weather and the widgets Windows keeps. A new bar
    /// puts Karpo's own weather (the weather preset) where Windows keeps this button, and this is
    /// Windows' board for whoever wants it as well.
    /// </summary>
    public static readonly WidgetInfo WidgetsBoard = new()
    {
        Id = "widgetsBoard", Glyph = "\uF246",
        Category = WidgetCategory.WindowsMenus, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Taskbar,
    };

    /// <summary>The conditions now: the place, the temperature with its trend, and how fresh it is.</summary>
    public static readonly WidgetInfo WeatherNow = new()
    {
        Id = "weatherNow", Glyph = "\uE753",
        Category = WidgetCategory.Weather, Options = WidgetOptionsKind.Weather, Settings = null,
    };

    /// <summary>
    /// markets: a share, a currency's rate or a coin the person follows — its price, how far it has moved
    /// over its range and its chart, or a currency converter (<see cref="MarketOptions"/>, docs\widgets.md
    /// §246). One widget per thing followed, so several stand side by side.
    /// </summary>
    public static readonly WidgetInfo Markets = new()
    {
        // markets: Yahoo's public chart endpoint, the ECB's rates and CoinGecko's prices, no account and no key.
        Id = "markets", Glyph = "\uE9D2",
        Category = WidgetCategory.Time, Options = WidgetOptionsKind.Market, Settings = null,
    };

    /// <summary>
    /// rss: one news feed the person follows, RSS or Atom — its newest headlines, each with where it
    /// is from and how old it is, and a click that opens the story (<see cref="RssOptions"/>). One
    /// widget per feed, as one per symbol, so several stand side by side.
    /// </summary>
    public static readonly WidgetInfo Rss = new()
    {
        // rss: the feed's own address, no account and no key; a click on a headline opens the story.
        Id = "rss", Glyph = "",
        Category = WidgetCategory.Time, Options = WidgetOptionsKind.Rss, Settings = null,
    };

    // perf: the five performance graphs, one widget each. They were one widget of four cells with a
    // checkbox per metric until commit dbc9e2f took them out; a widget is the unit everything else
    // is built on — a row in the catalogue, a shape, a card in a folder — and a widget whose
    // contents were checkboxes could have none of those. They are lines of a list only since
    // 2026-10-10 (docs\widgets.md §97): on the bar each is its preset, a folder wearing the reading's
    // figure as its face, which still lets somebody put the processor on the left and the network
    // by the tray.
    //
    // They share one options class, as the clock and the date do, because they are one widget with
    // five readings behind it.

    /// <summary>How busy the processor is, over all cores (<see cref="PerformanceOptions"/>).</summary>
    public static readonly WidgetInfo Cpu = new()
    {
        Id = "cpu", Glyph = "\uE950",
        Category = WidgetCategory.Performance, Options = WidgetOptionsKind.Performance,
        Settings = null,
        Metric = Metric.Cpu,
    };

    /// <summary>How busy the busiest engine of the graphics adapter is.</summary>
    public static readonly WidgetInfo Gpu = new()
    {
        Id = "gpu", Glyph = "\uE7F4",
        Category = WidgetCategory.Performance, Options = WidgetOptionsKind.Performance,
        Settings = null,
        Metric = Metric.Gpu,
    };

    /// <summary>How much of the physical memory is in use.</summary>
    public static readonly WidgetInfo Memory = new()
    {
        Id = "memory", Glyph = "\uE964",
        Category = WidgetCategory.Performance, Options = WidgetOptionsKind.Performance,
        Settings = null,
        Metric = Metric.Memory,
    };

    /// <summary>
    /// How busy a physical disk is — one the person picked, or the busiest of them — as its active
    /// time, the graph Task Manager draws for a disk. Its activity, never its space: how full a drive
    /// is, is the <see cref="Drive"/> widget's.
    /// </summary>
    public static readonly WidgetInfo Disk = new()
    {
        Id = "disk", Glyph = "\uEDA2",
        Category = WidgetCategory.Performance, Options = WidgetOptionsKind.Performance,
        Settings = null,
        Metric = Metric.Disk,
    };

    /// <summary>How fast data is moving through the network adapters, up and down together.</summary>
    public static readonly WidgetInfo Network = new()
    {
        // In Performance rather than Network: the Network group is the radios — Wi-Fi, Bluetooth,
        // airplane mode — and this is a meter of the machine, which is what sits beside it in Task
        // Manager and what somebody adding a processor graph is looking for next. Its keywords
        // carry "internet" and "wifi" so a search from the other direction still finds it, which is
        // the mechanism the catalogue has for exactly this.
        //
        // Titled "Network activity" in the catalogue, since the network button is "Network" (its
        // id "wifi"): two rows of one name in one list would be a choice nobody could make. What it
        // draws still says "Network" (PerformanceIsland.Drawn), beside the reading it names.
        Id = "network", Glyph = "\uE774",
        Category = WidgetCategory.Performance, Options = WidgetOptionsKind.Performance,
        Settings = null,
        Metric = Metric.Network,
    };

    // perf: the five readings' details, a widget each (2026-10-11: "the graphics / cpu / network
    // graphs and details should be separate widgets"): the facts behind a reading's figure, which
    // were the bottom of its flyout and then of its line at its tallest \u2014 the processor's speed, its
    // processes, threads and handles and how long the system has run; the memory in use, available,
    // committed and cached; the adapter; a disk's read and write; the network's send and receive and
    // its adapter. Each its reading's (Metric) and its graph's options, so a graphics card's details
    // follow the card its graph does. The glyph is the reading's: the two stand one over the other.

    /// <summary>perf: the processor's facts: its load, base speed, logical processors, processes, threads, handles and up time.</summary>
    public static readonly WidgetInfo CpuDetails = Details("cpuDetails", Metric.Cpu, "\uE950");

    /// <summary>perf: the graphics' facts: the load and the card.</summary>
    public static readonly WidgetInfo GpuDetails = Details("gpuDetails", Metric.Gpu, "\uE7F4");

    /// <summary>perf: the memory's facts: in use, available, committed and cached.</summary>
    public static readonly WidgetInfo MemoryDetails = Details("memoryDetails", Metric.Memory, "\uE964");

    /// <summary>perf: the disk's facts: its active time and what it reads and writes.</summary>
    public static readonly WidgetInfo DiskDetails = Details("diskDetails", Metric.Disk, "\uEDA2");

    /// <summary>perf: the network's facts: what it sends and receives, and the adapter carrying it.</summary>
    public static readonly WidgetInfo NetworkDetails = Details("networkDetails", Metric.Network, "\uE774");

    /// <summary>
    /// perf: the processes busiest on the processor now, Task Manager's processes sorted by CPU
    /// (2026-10-12: "for the processor add top running processes by cpu as a widget as well"): a row
    /// per app, its processes together, its icon, its name and its share of the whole processor. No
    /// reading of the graphs' (Metric): it reads the processes, not a counter, and has nothing to set.
    /// </summary>
    public static readonly WidgetInfo CpuProcesses = new()
    {
        Id = "cpuProcesses", Glyph = "",
        Category = WidgetCategory.Performance, Options = WidgetOptionsKind.None,
        Settings = null,
    };

    /// <summary>A reading's details: in Performance, with the graphs' options, its reading and its facts.</summary>
    private static WidgetInfo Details(string id, Metric metric, string glyph) => new()
    {
        Id = id, Glyph = glyph,
        Category = WidgetCategory.Performance, Options = WidgetOptionsKind.Performance,
        Settings = null,
        Metric = metric, Facts = true,
    };

    /// <summary>The Windows button: opens Start, the way the hidden taskbar's would.</summary>
    public static readonly WidgetInfo Start = new()
    {
        Id = "start", Glyph = "\uE8A9",
        Category = WidgetCategory.WindowsMenus, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Start,
    };

    /// <summary>The taskbar's search button (Win+S).</summary>
    public static readonly WidgetInfo Search = new()
    {
        Id = "search", Glyph = "\uE721",
        Category = WidgetCategory.WindowsMenus, Options = WidgetOptionsKind.WindowsSearch,
        Settings = WindowsPages.Search,
    };

    /// <summary>
    /// pim: the contacts' line of a list: a search box over the people in the person's accounts,
    /// which Karpo reads itself (the settings app's Accounts page); Enter or a click copies one's
    /// address. It was a taskbar entry too, a glyph whose click opened the list in a flyout of its
    /// own, until that became the contacts preset (2026-10-11), a folder wearing the glyph and
    /// holding this line; a line has no flyout height.
    /// </summary>
    public static readonly WidgetInfo Contacts = new()
    {
        Id = "contacts", Glyph = "",
        Category = WidgetCategory.Files, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>
    /// pim: the inbox's line of a list: how many are unread over the newest mail of every account.
    /// Read-only: nothing is marked read and nothing is opened. It was a taskbar entry too, the
    /// envelope whose click opened the newest mail, until that became the mail preset (2026-10-10),
    /// a folder wearing the envelope and holding this line; a line has no flyout height.
    /// </summary>
    public static readonly WidgetInfo Mail = new()
    {
        Id = "mail", Glyph = "",
        Category = WidgetCategory.Files, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>
    /// Task View (Win+Tab): the taskbar's button, and a line of a folder too (the person,
    /// 2026-09-27: "add a shortcut widget to the windows windows and desktop overview as well").
    /// </summary>
    public static readonly WidgetInfo TaskView = new()
    {
        Id = "taskView", Glyph = "\uE7C4",
        Category = WidgetCategory.WindowsMenus, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Multitasking,
    };

    /// <summary>
    /// The Snipping Tool, whose click starts a screenshot straight away, as Win+Shift+S does: the
    /// overlay to drag a rectangle on (the backlog, item 10). Its menu has the app itself, a snip
    /// in 3 and in 10 seconds, and a recording of the screen (Karpo.Dock\WindowsButtons.cs).
    /// </summary>
    public static readonly WidgetInfo SnippingTool = new()
    {
        Id = "snippingTool", Glyph = "",
        Category = WidgetCategory.Files, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Keyboard,
    };

    /// <summary>Windows' Settings app (ms-settings:), a one-icon shortcut (the backlog, item 18).</summary>
    public static readonly WidgetInfo WindowsSettings = new()
    {
        Id = "windowsSettings", Glyph = "",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>
    /// clipboard: what was copied lately, text and pictures, as a line of a list — Windows' own Win+V
    /// panel as rows; a click pastes an item where the person was (the backlog, item 13). It was a
    /// taskbar entry too, a glyph whose click opened it in a flyout of its own, until that became the
    /// clipboard preset (2026-10-10), a folder wearing the glyph and holding this line; a line has no
    /// flyout height.
    /// </summary>
    public static readonly WidgetInfo Clipboard = new()
    {
        Id = "clipboard", Glyph = "",
        Category = WidgetCategory.Files, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Clipboard,
    };

    /// <summary>
    /// desktops: the virtual desktops as a line of a list, a row each with the apps open on it
    /// (<see cref="DesktopOptions"/>): a click switches to one, its gripper drags it into another
    /// place, its own buttons rename and close it, and "New desktop" makes one. It was a taskbar
    /// entry, the name of the desktop you are on whose click opened the list in a flyout of its own,
    /// until that became the desktops preset (2026-10-11), a folder wearing the name and holding this
    /// line; before that a button per desktop in one line, until the user asked for a dropdown.
    /// </summary>
    public static readonly WidgetInfo Desktops = new()
    {
        Id = "desktops", Glyph = "\uE8AB",
        Category = WidgetCategory.Taskbar, Options = WidgetOptionsKind.Desktops,
        Settings = WindowsPages.Multitasking,
    };

    /// <summary>
    /// layouts: the taskbar layouts as a line of a list, a row each, the one this bar shows marked;
    /// a click switches to another — the same choice the bar's own right-click menu offers
    /// (<c>Dock.LayoutRows</c>), in the same order. It was a taskbar entry, the name of the layout
    /// on a button whose click opened the list in a flyout of its own, until that became the layouts
    /// preset (2026-10-11), a folder wearing the name and holding this line.
    /// </summary>
    public static readonly WidgetInfo Layouts = new()
    {
        Id = "layout", Glyph = TaskbarLayouts.LayoutGlyph,
        Category = WidgetCategory.Taskbar, Options = WidgetOptionsKind.None, Settings = null,
        Switcher = Switcher.Layouts,
    };

    /// <summary>One app, file, folder or web link, opened with a click, as a pinned app on the taskbar (<see cref="PinOptions"/>).</summary>
    public static readonly WidgetInfo Pin = new()
    {
        Id = "pin", Glyph = "\uE718",
        Category = WidgetCategory.Files, Options = WidgetOptionsKind.Pin, Settings = null,
    };

    /// <summary>
    /// \u00A7373: what one folder on this PC holds, each item a click away, a line of a list (its row of
    /// buttons on the taskbar went, \u00a7492); a subfolder opens as its navigation says (<see cref="PinFolders"/>). It
    /// points at its folder with a pin's record (<see cref="PinOptions"/>). Not a Karpo folder, which
    /// holds widgets (<see cref="FolderList"/>).
    /// </summary>
    public static readonly WidgetInfo FolderContents = new()
    {
        Id = "folderContents", Glyph = "\uE838",
        Category = WidgetCategory.Files, Options = WidgetOptionsKind.Pin, Settings = null,
    };

    /// <summary>Pinned apps and open windows.</summary>
    public static readonly WidgetInfo Apps = new()
    {
        Id = "apps", Glyph = "\uECA5",
        Category = WidgetCategory.Taskbar, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>
    /// The now-playing line of a list: what is playing, with its controls, the session it follows
    /// and its shuffle and repeat. It was a taskbar entry too, the art and the track whose click
    /// opened the media flyout, until that became the media preset (2026-10-10), a folder wearing
    /// the now-playing face (whose options the entry's were) and holding this line.
    /// </summary>
    public static readonly WidgetInfo Media = new()
    {
        Id = "media", Glyph = "\uEC4F",
        Category = WidgetCategory.Media, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>
    /// The apps playing, a line of a list to pick which one now playing follows: the media flyout's
    /// "Playing in" list, back in the media preset (2026-10-11). Segoe has no glyph for a media
    /// source, so the bulleted list: it is a list to pick from.
    /// </summary>
    public static readonly WidgetInfo MediaSources = new()
    {
        Id = "mediaSources", Glyph = "\uE8FD",
        Category = WidgetCategory.Media, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>
    /// drives: one drive the person picked — a local disk, a card, a stick — and nothing else
    /// (<see cref="DriveTargetOptions"/>). It replaced the combined drives widget, which showed
    /// whatever happened to be connected and so changed size under the person's hands.
    /// </summary>
    public static readonly WidgetInfo Drive = new()
    {
        Id = "drive", Glyph = "\uEC59",
        Category = WidgetCategory.Files, Options = WidgetOptionsKind.Drive, Settings = null,
        Drives = DriveFilter.Local,
    };

    /// <summary>drives: the same for one network drive or mapped share (<see cref="DriveTargetOptions"/>).</summary>
    public static readonly WidgetInfo NetworkDrive = new()
    {
        Id = "networkDrive", Glyph = "\uE8CE",
        Category = WidgetCategory.Files, Options = WidgetOptionsKind.Drive, Settings = null,
        Drives = DriveFilter.Network,
    };

    /// <summary>
    /// drives: the removable devices plugged in right now, a line of a list: a row each with what
    /// it holds and its Eject. It was a taskbar button too, whose flyout listed them, until that
    /// became the removable-devices preset (2026-10-10), a folder wearing the stick and holding
    /// this line; a line has no flyout height.
    /// </summary>
    public static readonly WidgetInfo RemovableDevices = new()
    {
        Id = "removableDevices", Glyph = "\uE88E",
        Category = WidgetCategory.Files, Options = WidgetOptionsKind.None, Settings = null,
        Drives = DriveFilter.Removable,
    };

    /// <summary>
    /// The calendar's agenda in a folder: today and the events coming, from the person's accounts.
    /// It was the calendar's taskbar entry too (the date, whose click opened the calendar) until the
    /// calendar became a preset folder wearing the calendar face (2026-10-09).
    /// </summary>
    public static readonly WidgetInfo Calendar = new()
    {
        Id = "calendar", Glyph = "\uE787",
        Category = WidgetCategory.Calendar, Options = WidgetOptionsKind.Calendar, Settings = null,
    };

    /// <summary>
    /// screentime: how long this PC was used on a day — the session unlocked, a display on and
    /// somebody at it — and the days before it as a graph (Karpo.Dock\ScreenTime). A list's line.
    /// </summary>
    public static readonly WidgetInfo ScreenTime = new()
    {
        Id = "screenTime", Glyph = "",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>screentime: the apps a day's screen time went to, most used first. A list's line.</summary>
    public static readonly WidgetInfo ScreenTimeApps = new()
    {
        Id = "screenTimeApps", Glyph = "",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>screentime: a day's screen time as a ring split by app, the total in its middle. A list's line.</summary>
    public static readonly WidgetInfo ScreenTimeChart = new()
    {
        Id = "screenTimeChart", Glyph = "",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>
    /// notes: a to-do list of the notebook's (Notebook.cs) as a line: its tasks ticked off, added,
    /// edited, deleted and dragged into order in place. Which list is the line's (<see cref="TodoOptions"/>).
    /// </summary>
    public static readonly WidgetInfo Todo = new()
    {
        Id = "todo", Glyph = "",
        Category = WidgetCategory.Notes, Options = WidgetOptionsKind.Todo, Settings = null,
    };

    /// <summary>
    /// notes: a note of the notebook's as a line, drawn as Markdown and edited in place as its text.
    /// Which note is the line's (<see cref="NoteOptions"/>).
    /// </summary>
    public static readonly WidgetInfo Note = new()
    {
        Id = "note", Glyph = "",
        Category = WidgetCategory.Notes, Options = WidgetOptionsKind.Note, Settings = null,
    };

    /// <summary>A folder whose widgets are a list: one row each, the name always beside the content.</summary>
    public static readonly WidgetInfo FolderList = new()
    {
        Id = "folderList", Glyph = "\uE8FD",
        Category = WidgetCategory.Folders, Options = WidgetOptionsKind.Folder, Settings = null,
    };

    /// <summary>
    /// list: a title over the lines under it, which groups them, as a group header of Windows'
    /// Settings titles the cards under it (<see cref="HeadingOptions"/>). A list's and nothing else's,
    /// and one level: a heading heads lines, never other headings.
    /// </summary>
    public static readonly WidgetInfo Heading = new()
    {
        Id = "heading", Glyph = "\uE8D2",
        Category = WidgetCategory.Folders, Options = WidgetOptionsKind.Heading, Settings = null,
    };

    /// <summary>
    /// list: empty space across the list between the lines over it and the lines under it, which
    /// somebody puts there on purpose and moves as any line (2026-10-05: "the divider in between
    /// widgets ... should not be there per default, you have to add it deliberately and can also
    /// move it around then"). A list's and nothing else's (§511: the bar has no separator). It is called
    /// a separator where anybody sees it, its glyph a rule across as a menu's separator is (2026-10-11);
    /// the file keeps its word, "divider", so nobody's folders change. §435: its fixed room, or it grows
    /// (<see cref="DividerOptions"/>).
    /// </summary>
    public static readonly WidgetInfo Divider = new()
    {
        Id = "divider", Glyph = "\uE921",
        Category = WidgetCategory.Folders, Options = WidgetOptionsKind.Divider, Settings = null,
    };

    /// <summary>
    /// list: a row of toggle icons — Wi-Fi, Bluetooth, airplane mode, night light and the rest the
    /// person picks, in their order (<see cref="TogglesOptions"/>, Toggles.cs) — each lit with the
    /// accent while it is on, as Windows' quick settings show them, and switched by a click.
    /// </summary>
    public static readonly WidgetInfo Toggles = new()
    {
        Id = "toggles", Glyph = "",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.Toggles, Settings = null,
    };

    /// <summary>
    /// list: a flyout's footer as a line — a text button on the left and one on the right,
    /// each opening a page of Windows' Settings (<see cref="ActionsOptions"/>, SettingsActions.cs).
    /// </summary>
    public static readonly WidgetInfo Actions = new()
    {
        Id = "actions", Glyph = "",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.Actions, Settings = null,
    };

    /// <summary>
    /// list: a month as the clock flyout's calendar draws it — the weeks from the day this region
    /// starts them on, today ringed, the folder's day filled — whose click sets the folder's day
    /// (<see cref="Channels.Day"/>) for an agenda beside it to follow.
    /// </summary>
    public static readonly WidgetInfo Month = new()
    {
        Id = "month", Glyph = "",
        Category = WidgetCategory.Calendar, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>
    /// list: the time with its seconds over the long date, as the top of the clock's flyout; in the
    /// clock's zone (<see cref="ClockOptions.TimeZone"/>, §230), this PC's unless set.
    /// </summary>
    public static readonly WidgetInfo TimeHeader = new()
    {
        Id = "timeHeader", Glyph = "",
        Category = WidgetCategory.Time, Options = WidgetOptionsKind.Clock, Settings = null,
    };

    /// <summary>list: the next hours, the weather flyout's curve with their glyphs, for a place (<see cref="WeatherOptions"/>).</summary>
    public static readonly WidgetInfo WeatherHours = new()
    {
        Id = "weatherHours", Glyph = "",
        Category = WidgetCategory.Weather, Options = WidgetOptionsKind.Weather, Settings = null,
    };

    /// <summary>list: the week ahead, the weather flyout's rows of days with their highs and lows.</summary>
    public static readonly WidgetInfo WeatherDays = new()
    {
        Id = "weatherDays", Glyph = "",
        Category = WidgetCategory.Weather, Options = WidgetOptionsKind.Weather, Settings = null,
    };

    /// <summary>
    /// air: the air quality for a place (<see cref="WeatherOptions"/>, its index among them;
    /// docs\widgets.md §210) — the index and its level in the level's colour, the main pollutants,
    /// the pollen where the place has them and the next hours — from Open-Meteo's air-quality
    /// service, the weather's own. A line of a list; on the bar it is the air-quality preset, a folder
    /// wearing the index as its face. Its own widget, not a style of the weather's: another subject.
    /// </summary>
    public static readonly WidgetInfo AirQuality = new()
    {
        Id = "airQuality", Glyph = "",
        Category = WidgetCategory.Weather, Options = WidgetOptionsKind.Weather, Settings = null,
    };

    /// <summary>
    /// tides:the tides for a place (<see cref="WeatherOptions"/>; docs\widgets.md §236) — the sea's
    /// height now and which way it goes, the next high or low and how soon, the day's curve and the
    /// next turns — from Open-Meteo's marine service, the weather's own provider; a model, not for
    /// navigation. A line of a list; on the bar it is the tides preset, a folder wearing the next turn.
    /// </summary>
    public static readonly WidgetInfo Tides = new()
    {
        Id = "tides", Glyph = "",
        Category = WidgetCategory.Weather, Options = WidgetOptionsKind.Weather, Settings = null,
    };

    /// <summary>
    /// sports: a favourite team's live score or next match (<see cref="SportsOptions"/>; docs\widgets.md
    /// §239), and its matches around it, from ESPN's public scores. A line of a list; on the bar it is
    /// the sports preset, a folder wearing the score as its face. A click opens the match on ESPN.
    /// </summary>
    public static readonly WidgetInfo Sports = new()
    {
        // sports: ESPN's public site API, no account and no key. Segoe Fluent Icons' running figure.
        Id = "sports", Glyph = "",
        Category = WidgetCategory.Time, Options = WidgetOptionsKind.Sports, Settings = null,
    };

    /// <summary>
    /// quakes: the earthquakes near a place (<see cref="WeatherOptions"/>, its radius, least
    /// magnitude and alert among them; docs\widgets.md §240) — each one's magnitude in its colour,
    /// how far and which way, where and when — from the USGS's feed, and a sound and a mark on its
    /// folder's button as a new one comes. A line of a list; on the bar it is the earthquake preset,
    /// a folder wearing the latest as its face.
    /// </summary>
    public static readonly WidgetInfo Quakes = new()
    {
        Id = "quakes", Glyph = "",
        Category = WidgetCategory.Weather, Options = WidgetOptionsKind.Weather, Settings = null,
    };

    /// <summary>
    /// camera: a live public webcam's or IP camera's picture (<see cref="CameraOptions"/>; docs\widgets.md
    /// §331) — an MJPEG stream, a snapshot asked for again, HLS, MPEG-DASH, a video over HTTP or RTSP —
    /// read only while its folder shows it. A line of a list; a click opens the camera's page.
    /// </summary>
    public static readonly WidgetInfo Camera = new()
    {
        // camera: Segoe Fluent Icons' Video, a video camera.
        Id = "camera", Glyph = OptionGlyphs.Camera,
        Category = WidgetCategory.Weather, Options = WidgetOptionsKind.Camera, Settings = null,
    };

    /// <summary>
    /// list: today's date, as the calendar flyout's header; a click goes back to today. Today in the
    /// clock's zone (<see cref="ClockOptions.TimeZone"/>, §230; the clock widget's date style until
    /// §318, a widget of its own now) — this PC's unless set.
    /// </summary>
    public static readonly WidgetInfo DateHeader = new()
    {
        Id = "dateHeader", Glyph = "",
        Category = WidgetCategory.Calendar, Options = WidgetOptionsKind.Clock, Settings = null,
    };

    /// <summary>list: the events of a day — the folder's, else today — as the calendar flyout listed them.</summary>
    public static readonly WidgetInfo Events = new()
    {
        Id = "events", Glyph = "",
        Category = WidgetCategory.Calendar, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>
    /// calendar: the next meetings (docs\widgets.md §205): today's still to come from the accounts'
    /// calendars, the one on now first, each with how soon it starts and a Join button where it is a
    /// Teams, Zoom, Google Meet or Webex meeting (MeetingLinks). All-day events, and those shown as free
    /// or declined, are passed by. A line of a list; on the bar it is the meeting preset, a folder
    /// wearing the next meeting as its face.
    /// </summary>
    public static readonly WidgetInfo Meeting = new()
    {
        Id = "meeting", Glyph = "",
        Category = WidgetCategory.Calendar, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>list: one day's hours with its events, the calendar flyout's day view.</summary>
    public static readonly WidgetInfo DayView = new()
    {
        Id = "dayView", Glyph = "",
        Category = WidgetCategory.Calendar, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>list: a week's days side by side with their hours, the calendar flyout's week view.</summary>
    public static readonly WidgetInfo WeekView = new()
    {
        Id = "weekView", Glyph = "",
        Category = WidgetCategory.Calendar, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>list: the year's twelve months, the calendar flyout's year view.</summary>
    public static readonly WidgetInfo YearView = new()
    {
        Id = "yearView", Glyph = "",
        Category = WidgetCategory.Calendar, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>Notification-area icons, with the overflow chevron.</summary>
    public static readonly WidgetInfo Tray = new()
    {
        Id = "tray", Glyph = "\uE70E",
        Category = WidgetCategory.Taskbar, Options = WidgetOptionsKind.Tray, Settings = null,
    };

    /// <summary>
    /// Windows' pen menu: the pen's shortcuts, which Windows shows beside the tray on a PC with a pen
    /// (<see cref="InputButtons"/>).
    /// </summary>
    public static readonly WidgetInfo PenMenu = new()
    {
        Id = "penMenu", Glyph = "\uEDC6",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.InputButton,
        Settings = WindowsPages.Pen,
    };

    /// <summary>
    /// The touch keyboard, which Windows' button shows and hides; beside the tray, and shown wherever
    /// it is placed, so it has no option (<see cref="InputButtons"/>).
    /// </summary>
    public static readonly WidgetInfo TouchKeyboard = new()
    {
        Id = "touchKeyboard", Glyph = "\uE765",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Typing,
    };

    /// <summary>
    /// The virtual touchpad: a touchpad drawn on a touch screen, to move the pointer by it; beside the
    /// tray on a touch screen, where somebody turns it on (<see cref="InputButtons"/>).
    /// </summary>
    public static readonly WidgetInfo VirtualTouchpad = new()
    {
        Id = "virtualTouchpad", Glyph = "\uEFA5",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.InputButton,
        Settings = WindowsPages.Touchpad,
    };

    /// <summary>
    /// The status icons Windows 11's taskbar shows while something is so: the microphone, the camera,
    /// the location or presence sensing in use and by which apps, airplane mode, the mobile hotspot
    /// on, a metered connection, energy saver, do not disturb, and Windows 11's update icon (updates
    /// waiting, downloading or installing, or a restart waiting for them; a VPN connected is the
    /// network icon's badge, as in Windows). Each is shown only while it is so,
    /// and with nothing to say it takes no room. The taskbar's alone. Each icon may be set to show
    /// behind the entry's own arrow instead, or nowhere (<see cref="StatusIconOptions"/>).
    /// </summary>
    public static readonly WidgetInfo Indicators = new()
    {
        Id = "indicators", Glyph = "\uE720",
        Category = WidgetCategory.Time, Options = WidgetOptionsKind.StatusIcons, Settings = null,
    };

    /// <summary>
    /// list: the network page of Windows 11's quick settings as a line (2026-10-10): "Wi-Fi" with the
    /// radio's switch, the cables that are in, and the networks in range, each opening in place to
    /// connect, disconnect or take its key. Titled Network since it has the cables too. It was the
    /// network button's flyout; the button is the Wi-Fi preset now, a folder wearing the Wi-Fi face.
    /// </summary>
    public static readonly WidgetInfo Wifi = new()
    {
        Id = "wifi", Glyph = "\uE701",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Network,
    };

    /// <summary>
    /// list: the Bluetooth page of Windows 11's quick settings as a line (2026-10-10): "Bluetooth"
    /// with the radio's switch and the paired devices, each opening in place to connect, disconnect
    /// or remove it. It was the Bluetooth button's flyout; the button is the Bluetooth preset now.
    /// </summary>
    public static readonly WidgetInfo Bluetooth = new()
    {
        Id = "bluetooth", Glyph = "\uE702",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Bluetooth,
    };

    /// <summary>
    /// list: the mobile hotspot's page as a line (2026-10-10): "Mobile hotspot" with its switch, the
    /// network's name, password and band, how many devices are on it, and the devices themselves. It
    /// was the mobile hotspot button's flyout; the button is the mobile hotspot preset now. The
    /// switch alone is <see cref="MobileHotspot"/>, a line of its own.
    /// </summary>
    public static readonly WidgetInfo Hotspot = new()
    {
        Id = "hotspot", Glyph = "",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.MobileHotspot,
    };

    /// <summary>
    /// list: Windows' Cast panel as a line (2026-10-11): "Cast" and the wireless displays in range, the
    /// one this PC shows on marked "Connected"; a display's row opens Windows' own panel, which alone
    /// may connect to it. It was the cast button, whose click opened that panel; the button is the
    /// cast preset now.
    /// </summary>
    public static readonly WidgetInfo WirelessDisplays = new()
    {
        Id = "wirelessDisplays", Glyph = "\uEC15",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Display,
    };

    /// <summary>
    /// list: Win+P's four modes as a line (2026-10-11), the one in force marked and a click putting
    /// another in force. The cast preset's, under its displays.
    /// </summary>
    public static readonly WidgetInfo Projection = new()
    {
        Id = "projection", Glyph = "\uE8AF",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Display,
    };

    /// <summary>
    /// list: Windows' Nearby sharing as a line (2026-10-11): who may share with this PC — off, its
    /// own devices, everyone nearby — where what they share is saved, and the name it is found by.
    /// The switch alone is <see cref="NearbyShare"/>, a line of its own.
    /// </summary>
    public static readonly WidgetInfo NearbySharing = new()
    {
        Id = "nearbySharing", Glyph = "\uE72D",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.NearbySharing,
    };

    /// <summary>The volume as a slider on the bar itself, with a speaker that mutes (<see cref="VolumeOptions"/>).</summary>
    public static readonly WidgetInfo Volume = new()
    {
        Id = "volume", Glyph = "\uE995",
        Category = WidgetCategory.Media, Options = WidgetOptionsKind.Volume,
        Settings = WindowsPages.Sound,
    };

    /// <summary>
    /// quick: the displays' brightness as one slider — a built-in screen as Windows 11's quick
    /// settings has it, and an external monitor over DDC/CI — setting every display together, or the
    /// one it stands on where somebody chose that (<see cref="BrightnessOptions"/>).
    /// </summary>
    public static readonly WidgetInfo Brightness = new()
    {
        Id = "brightness", Glyph = "\uE706",
        Category = WidgetCategory.Display, Options = WidgetOptionsKind.Brightness,
        Settings = WindowsPages.Display,
    };

    /// <summary>
    /// sound: the sound panel's output devices, the one in use selected, as a line of a folder's
    /// list (docs\widgets.md §9, the fourth conversion).
    /// </summary>
    public static readonly WidgetInfo SoundOutput = new()
    {
        Id = "soundOutput", Glyph = "\uE7F5",
        Category = WidgetCategory.Media, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>sound: the microphones, with their mute, the same way.</summary>
    public static readonly WidgetInfo SoundInput = new()
    {
        Id = "soundInput", Glyph = "\uE720",
        Category = WidgetCategory.Media, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>sound: each app's own level and mute, the same way.</summary>
    public static readonly WidgetInfo SoundMixer = new()
    {
        Id = "soundMixer", Glyph = "\uE9E9",
        Category = WidgetCategory.Media, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>
    /// sound: the output's own level and mute — the panel's volume row — the same way. It is also
    /// the volume slider's (<see cref="Volume"/>) line in a list: a slider carried into a list
    /// becomes this row, which the list's library calls "Volume" (Partners).
    /// </summary>
    public static readonly WidgetInfo SoundVolume = new()
    {
        Id = "soundVolume", Glyph = "\uE995",
        Category = WidgetCategory.Media, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>
    /// quick: spatial sound for the output in use as a toggle (2026-10-11): a click turns it off, or on
    /// in the format its Settings card names — Windows Sonic, or a Dolby Atmos or DTS format a person
    /// has installed — else the one last used here (<see cref="SwitchOptions.SpatialFormat"/>). It
    /// listed the formats on the bar until then; Windows' sound page still does.
    /// </summary>
    public static readonly WidgetInfo SpatialSound = new()
    {
        Id = "spatialSound", Glyph = "\uED33",
        Category = WidgetCategory.Media, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Sound,
        Switch = QuickSwitch.SpatialSound,
        Flips = true,
    };

    public static readonly WidgetInfo Battery = new()
    {
        Id = "battery", Glyph = "\uE83F",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Power,
    };

    /// <summary>
    /// keyboard: the keyboard layouts' line of a list: a row per layout installed, the one in use
    /// marked, a click switching the window in front to it. It was a taskbar entry too, the input
    /// indicator ("ENG US"), whose click opened the layouts in a flyout of its own, until that became
    /// the keyboard preset (2026-10-11), a folder wearing the indicator and holding this line; a line
    /// has no flyout height.
    /// </summary>
    public static readonly WidgetInfo Keyboard = new()
    {
        Id = "keyboard", Glyph = "\uE765",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Language,
    };

    // quick: the toggles of the radios and the sound (2026-10-11: "wifi on / off should also get an
    // own toggle widget, same for bluetooth, same for sound where you can mute / unmute, same for
    // microphone"). Each a glyph whose click flips it there and then, on the bar and as a line, lit
    // while it is on as the other toggles are; the sound's and the microphone's glyph is struck
    // through while muted. The radios are switched as the Wi-Fi and Bluetooth lines switch them
    // (Windows.Devices.Radios), the sound and the microphone are the default output's and the default
    // microphone's mute (Core Audio). The whole of each is its preset: the Wi-Fi, Bluetooth and sound
    // folders.

    /// <summary>quick: the Wi-Fi radio on and off with a click, as Windows' Wi-Fi tile switches it.</summary>
    public static readonly WidgetInfo WifiSwitch = new()
    {
        Id = "wifiSwitch", Glyph = "\uE701",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Network,
        Switch = QuickSwitch.WifiRadio,
        Flips = true,
    };

    /// <summary>quick: the Bluetooth radio on and off with a click, as Windows' Bluetooth tile switches it.</summary>
    public static readonly WidgetInfo BluetoothSwitch = new()
    {
        Id = "bluetoothSwitch", Glyph = "\uE702",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Bluetooth,
        Switch = QuickSwitch.BluetoothRadio,
        Flips = true,
    };

    /// <summary>quick: the sound muted and unmuted with a click: the default output's mute. On is unmuted.</summary>
    public static readonly WidgetInfo SoundSwitch = new()
    {
        Id = "soundSwitch", Glyph = "\uE995",
        Category = WidgetCategory.Media, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Sound,
        Switch = QuickSwitch.Sound,
        Flips = true,
    };

    /// <summary>quick: the microphone muted and unmuted with a click: the default microphone's mute. On is unmuted.</summary>
    public static readonly WidgetInfo MicrophoneSwitch = new()
    {
        Id = "microphoneSwitch", Glyph = "\uE720",
        Category = WidgetCategory.Media, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Sound,
        Switch = QuickSwitch.Microphone,
        Flips = true,
    };

    // quick: game mode, color filters and mono audio (2026-10-11), toggles as the others are, each
    // switched through the Settings handler Windows' own switch for it goes through (the dock's
    // SettingToggles.cs) and followed where Windows keeps it. Color filters and mono audio are tiles
    // of Windows' quick settings; game mode is the switch on its Settings page.

    /// <summary>quick: game mode on and off with a click, as its switch in Windows' Settings turns it.</summary>
    public static readonly WidgetInfo GameMode = new()
    {
        Id = "gameMode", Glyph = "",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.GameMode,
        Switch = QuickSwitch.GameMode,
        Flips = true,
    };

    /// <summary>
    /// quick: color filters on and off with a click, as Windows' tile turns them: on in the filter its
    /// Settings card names (<see cref="SwitchOptions.ColorFilter"/>), grayscale unless another.
    /// </summary>
    public static readonly WidgetInfo ColorFilters = new()
    {
        Id = "colorFilters", Glyph = "",
        Category = WidgetCategory.Display, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.ColorFilters,
        Switch = QuickSwitch.ColorFilters,
        Flips = true,
    };

    /// <summary>quick: mono audio on and off with a click, as Windows' tile turns it: the left and right channels as one.</summary>
    public static readonly WidgetInfo MonoAudio = new()
    {
        Id = "monoAudio", Glyph = "",
        Category = WidgetCategory.Media, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.MonoAudio,
        Switch = QuickSwitch.MonoAudio,
        Flips = true,
    };

    /// <summary>One button that turns every radio off and on, as the quick settings' airplane mode does.</summary>
    public static readonly WidgetInfo AirplaneMode = new()
    {
        Id = "airplaneMode", Glyph = "\uE709",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.AirplaneMode,
        Switch = QuickSwitch.AirplaneMode,
        Flips = true,
    };

    /// <summary>
    /// The mobile hotspot's switch, a line of a list. It was a taskbar button too, whose click opened
    /// the hotspot's page, until that became the mobile hotspot preset (2026-10-10).
    /// </summary>
    public static readonly WidgetInfo MobileHotspot = new()
    {
        Id = "mobileHotspot", Glyph = "\uEBC4",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.MobileHotspot,
        Switch = QuickSwitch.MobileHotspot,
    };

    /// <summary>
    /// quick: VPN as Windows' VPN tile has it, a switch line of a list: a click connects the last one
    /// used, or disconnects the one that is connected. It was a taskbar entry too, whose click listed
    /// the connections, until that became the VPN preset (2026-10-11); the list is
    /// <see cref="VpnConnections"/>.
    /// </summary>
    public static readonly WidgetInfo Vpn = new()
    {
        Id = "vpn", Glyph = "\uE705",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Vpn,
        Switch = QuickSwitch.Vpn,
    };

    /// <summary>
    /// quick: the PC's VPN connections as a line (2026-10-11), as Windows' VPN page lists them, each
    /// connected or disconnected from its row. It was the VPN entry's flyout; the entry is the VPN
    /// preset now.
    /// </summary>
    public static readonly WidgetInfo VpnConnections = new()
    {
        Id = "vpnConnections", Glyph = "\uE705",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Vpn,
    };

    /// <summary>One button that turns energy saver on and off, as the quick settings' tile does.</summary>
    public static readonly WidgetInfo EnergySaver = new()
    {
        Id = "energySaver", Glyph = "\uEC0A",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.EnergySaver,
        Switch = QuickSwitch.EnergySaver,
        Flips = true,
    };

    /// <summary>One button that turns night light on and off, as the quick settings' tile does.</summary>
    public static readonly WidgetInfo NightLight = new()
    {
        Id = "nightLight", Glyph = "\uE708",
        Category = WidgetCategory.Display, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.NightLight,
        Switch = QuickSwitch.NightLight,
        Flips = true,
    };

    /// <summary>
    /// darkMode: Windows' own mode, dark or light, as Settings' Colors page sets it (§403). Windows 11
    /// has no quick settings tile for it, so it is named as Settings says it, with the sun half dark
    /// that Windows' own apps switch their theme with. On while the apps are dark; a click sets the
    /// apps and Windows together (the dock's QuickSettings\Services\DarkMode.cs).
    /// </summary>
    public static readonly WidgetInfo DarkMode = new()
    {
        Id = "darkMode", Glyph = "\uE793",
        Category = WidgetCategory.Display, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Colors,
        Switch = QuickSwitch.DarkMode,
        Flips = true,
    };

    /// <summary>
    /// quick: rotation lock, which keeps the screen from turning with the PC, as the quick settings'
    /// tile does — only where the PC turns its screen at all; elsewhere Windows has no such tile.
    /// </summary>
    public static readonly WidgetInfo RotationLock = new()
    {
        Id = "rotationLock", Glyph = "\uE755",
        Category = WidgetCategory.Display, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Display,
        Switch = QuickSwitch.RotationLock,
        Flips = true,
    };

    // quick: the accessibility page's other three (2026-10-14, Windows' accessibility tile's ">"):
    // the magnifier and the narrator, started and stopped as Windows' own switches do, and sticky
    // keys, switched where Windows keeps it (QuickSettings\Services\Assistive.cs). Lines of a list.

    /// <summary>quick: the magnifier on and off, as the accessibility page's switch does.</summary>
    public static readonly WidgetInfo Magnifier = new()
    {
        Id = "magnifier", Glyph = "\uE71E",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Magnifier,
        Switch = QuickSwitch.Magnifier,
        Flips = true,
    };

    /// <summary>quick: the narrator on and off (Win+Ctrl+Enter), as the accessibility page's switch does.</summary>
    public static readonly WidgetInfo Narrator = new()
    {
        Id = "narrator", Glyph = "\uEFA9",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Narrator,
        Switch = QuickSwitch.Narrator,
        Flips = true,
    };

    /// <summary>quick: sticky keys on and off, as the accessibility page's switch does.</summary>
    public static readonly WidgetInfo StickyKeys = new()
    {
        Id = "stickyKeys", Glyph = "\uE765",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Keyboard,
        Switch = QuickSwitch.StickyKeys,
        Flips = true,
    };

    /// <summary>
    /// quick: keep awake (2026-10-20, §269), as PowerToys Awake keeps a PC awake: while it is on,
    /// Windows' idle timer puts the PC to sleep no more — and with the screen kept on, turns the
    /// screen off no more — until it is turned off, or for as long as it was turned on for
    /// (<see cref="SwitchOptions.AwakeMinutes"/>). A power request of Karpo's own, gone with Karpo
    /// (the dock's QuickSettings\Services\KeepAwake.cs); Windows' power plan is never changed, so its
    /// page is where the timeouts themselves are set. A coffee cup, as PowerToys Awake's.
    /// </summary>
    public static readonly WidgetInfo KeepAwake = new()
    {
        Id = "keepAwake", Glyph = "",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Power,
        Switch = QuickSwitch.KeepAwake,
        Flips = true,
    };

    /// <summary>One button that starts live captions (Win+Ctrl+L).</summary>
    public static readonly WidgetInfo LiveCaptions = new()
    {
        Id = "liveCaptions", Glyph = "\uE7F0",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Captions,
        Switch = QuickSwitch.LiveCaptions,
    };

    /// <summary>
    /// Nearby sharing's switch, a line of a list: off, or on as it was last set, as Windows' own tile
    /// switches it. It was a taskbar button too, whose click opened its settings, until that became
    /// the nearby sharing preset (2026-10-11).
    /// </summary>
    public static readonly WidgetInfo NearbyShare = new()
    {
        Id = "nearbyShare", Glyph = "\uE72D",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.NearbySharing,
        Switch = QuickSwitch.NearbyShare,
    };

    /// <summary>One button that opens Windows' accessibility settings.</summary>
    public static readonly WidgetInfo Accessibility = new()
    {
        Id = "accessibility", Glyph = "\uE776",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Accessibility,
        Switch = QuickSwitch.Accessibility,
    };

    // quick: Windows' Cast and Project tiles (2026-10-16, the person: "add all of the windows default
    // items in the quick settings"), lines of a list as accessibility's is: no state Windows tells an
    // app, so no switch, and in a folder the whole line opens its page (FolderPages) — the wireless
    // displays, Win+P's modes — as the tile's ">" swaps Windows' panel for them. Elsewhere a click
    // presses the tile's shortcut, Win+K or Win+P.

    /// <summary>quick: Windows' Cast tile: in a folder its page, the wireless displays in range; elsewhere Win+K.</summary>
    public static readonly WidgetInfo Cast = new()
    {
        Id = "cast", Glyph = "",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Display,
        Switch = QuickSwitch.Cast,
    };

    /// <summary>quick: Windows' Project tile: in a folder its page, Win+P's four modes; elsewhere Win+P.</summary>
    public static readonly WidgetInfo Project = new()
    {
        Id = "project", Glyph = "",
        Category = WidgetCategory.Network, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Display,
        Switch = QuickSwitch.Project,
    };

    /// <summary>
    /// Whether this PC is protected: the antivirus and the firewall as Windows' own Security
    /// Center reports them, and a click that opens Windows Security
    /// (<c>QuickSettings\Services\Security.cs</c>). It names whichever product is in charge, which
    /// is why it is Security and not Defender.
    /// </summary>
    public static readonly WidgetInfo Security = new()
    {
        // Called Security and not Defender on purpose: Security Center reports whichever antivirus
        // is registered, and on a PC running somebody else's this widget would otherwise have to
        // show a green shield for work Defender is not doing. It names who is in charge instead.
        // QuickSettings\Services\Security.cs has the long version.
        Id = "security", Glyph = "\uEA18",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.Security,
    };

    /// <summary>
    /// list: the power mode of Windows' "Power &amp; battery" page as a line (2026-10-10): its heading
    /// and the three modes, the one in force marked. It was a part of the battery and power widgets'
    /// flyouts; those are the battery and power presets now.
    /// </summary>
    public static readonly WidgetInfo PowerMode = new()
    {
        Id = "powerMode", Glyph = "\uEC49",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Power,
    };

    // power: the commands of Windows' power menu, a widget each (2026-10-11: "the power menu should
    // actually be separate widgets which are just single buttons"): a line of a list, the list's own
    // row of the command's glyph and its name, whose click does it. The glyphs are the quick-link menu's, so a command looks the
    // same everywhere in Karpo.

    /// <summary>power: puts the PC to sleep, where Windows' power menu offers it.</summary>
    public static readonly WidgetInfo Sleep = new()
    {
        Id = "sleep", Glyph = "\uE708",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Power,
        Power = PowerCommand.Sleep,
    };

    /// <summary>power: hibernates the PC, where Windows' power menu offers it (off unless somebody turned it on).</summary>
    public static readonly WidgetInfo Hibernate = new()
    {
        Id = "hibernate", Glyph = "\uE78C",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Power,
        Power = PowerCommand.Hibernate,
    };

    /// <summary>power: shuts the PC down, the fast-startup kind where that is on.</summary>
    public static readonly WidgetInfo ShutDown = new()
    {
        Id = "shutDown", Glyph = "\uE7E8",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Power,
        Power = PowerCommand.ShutDown,
    };

    /// <summary>power: restarts the PC.</summary>
    public static readonly WidgetInfo Restart = new()
    {
        Id = "restart", Glyph = "\uE777",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Power,
        Power = PowerCommand.Restart,
    };

    /// <summary>power: signs the person out, the apps asked to close.</summary>
    public static readonly WidgetInfo SignOut = new()
    {
        Id = "signOut", Glyph = "\uF3B1",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.None, Settings = null,
        Power = PowerCommand.SignOut,
    };

    /// <summary>power: locks the PC, everything left running, as Win+L does.</summary>
    public static readonly WidgetInfo Lock = new()
    {
        Id = "lock", Glyph = "\uE72E",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.None, Settings = null,
        Power = PowerCommand.Lock,
    };

    /// <summary>
    /// The time, in a zone of its own and with a label (<see cref="ClockOptions"/>): a folder's card
    /// and line. The bar's clock is the clock preset, a folder wearing the clock face.
    /// </summary>
    public static readonly WidgetInfo Clock = new()
    {
        Id = "clock", Glyph = "\uE823",
        Category = WidgetCategory.Time, Options = WidgetOptionsKind.Clock, Settings = null,
        ShowsTime = true,
    };

    /// <summary>
    /// calendar: a countdown of whole minutes, set on its face and counting down in place \u2014 a line of
    /// a list. Each one is its own countdown, with its own length and name (<see cref="CountdownOptions"/>).
    /// It was a pane of Karpo's clock until the user asked for a widget, and an entry of the taskbar
    /// too until that became the timer preset (2026-10-10), a folder wearing the timer face and
    /// holding this line.
    /// </summary>
    public static readonly WidgetInfo Timer = new()
    {
        Id = "timer", Glyph = "\uF739",
        Category = WidgetCategory.Time, Options = WidgetOptionsKind.Countdown, Settings = null,
    };

    /// <summary>calendar: the same for a focus session, with a length and a name of its own.</summary>
    public static readonly WidgetInfo Focus = new()
    {
        Id = "focus", Glyph = "\uF272",
        Category = WidgetCategory.Time, Options = WidgetOptionsKind.Countdown, Settings = null,
    };

    /// <summary>
    /// calendar: a stopwatch, counting up in tenths with laps: a line of a list, worked as a timer is.
    /// Each one counts on its own, and there is nothing to set. Its taskbar entry became the
    /// stopwatch preset (2026-10-10), as the timer's did.
    /// </summary>
    public static readonly WidgetInfo Stopwatch = new()
    {
        Id = "stopwatch", Glyph = "",
        Category = WidgetCategory.Time, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>
    /// alarm: one alarm, a line of a list: a time of day, switched on on its line, ringing once or on
    /// the days it repeats on (<see cref="AlarmOptions"/>, docs\widgets.md §106). The clock with its
    /// arrow round: Segoe has no alarm clock, and the bell is the notifications'.
    /// </summary>
    public static readonly WidgetInfo Alarm = new()
    {
        Id = "alarm", Glyph = "",
        Category = WidgetCategory.Time, Options = WidgetOptionsKind.Alarm, Settings = null,
    };

    /// <summary>
    /// The notifications themselves, grouped by app as Windows 11's notification center groups them,
    /// each opened, expanded, answered or dismissed on its line. It was the list of a taskbar entry
    /// too, the bell whose click opened it, until that became the notifications preset (2026-10-10),
    /// a folder wearing the bell and holding this line.
    /// </summary>
    public static readonly WidgetInfo NotificationsList = new()
    {
        Id = "notificationsList", Glyph = "\uEA8F",
        Category = WidgetCategory.Time, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>
    /// Windows' own do not disturb as one button (Karpo.Dock\QuickSettings\Services\DoNotDisturb.cs):
    /// the bell with "zZ" Windows 11 shows for it, lit while it is on, and a click that switches it —
    /// the setting the notification list's header switches, and the bell on the bar shows.
    /// </summary>
    public static readonly WidgetInfo DoNotDisturb = new()
    {
        Id = "doNotDisturb", Glyph = "\uF285",
        Category = WidgetCategory.Time, Options = WidgetOptionsKind.Switch,
        Settings = WindowsPages.DoNotDisturb,
        Switch = QuickSwitch.DoNotDisturb,
        Flips = true,
    };

    // windowsMenus: Windows' own menus, a taskbar button each (§466): the click opens Windows' popup
    // (Karpo.Dock\WindowsButtons.cs), never one of Karpo's.

    /// <summary>windowsMenus: Windows' quick link menu (Win+X), Start's right click.</summary>
    public static readonly WidgetInfo QuickLink = new()
    {
        Id = "quickLink", Glyph = "\uE8FD",
        Category = WidgetCategory.WindowsMenus, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>windowsMenus: Windows' notifications and calendar (Win+N), which its clock opens.</summary>
    public static readonly WidgetInfo WindowsNotifications = new()
    {
        Id = "windowsNotifications", Glyph = "\uEA8F",
        Category = WidgetCategory.WindowsMenus, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Notifications,
    };

    /// <summary>windowsMenus: Windows' quick settings (Win+A).</summary>
    public static readonly WidgetInfo WindowsQuickSettings = new()
    {
        Id = "windowsQuickSettings", Glyph = "\uEC11",
        Category = WidgetCategory.WindowsMenus, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>windowsMenus: quick settings' sound output page (Win+Ctrl+V): which speakers play.</summary>
    public static readonly WidgetInfo WindowsSound = new()
    {
        Id = "windowsSound", Glyph = "\uE995",
        Category = WidgetCategory.WindowsMenus, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Sound,
    };

    /// <summary>windowsMenus: quick settings' Wi-Fi page, the networks around.</summary>
    public static readonly WidgetInfo WindowsNetworks = new()
    {
        Id = "windowsNetworks", Glyph = "\uE701",
        Category = WidgetCategory.WindowsMenus, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Network,
    };

    /// <summary>windowsMenus: Windows' cast flyout (Win+K), the wireless displays.</summary>
    public static readonly WidgetInfo WindowsCast = new()
    {
        Id = "windowsCast", Glyph = "\uEC15",
        Category = WidgetCategory.WindowsMenus, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Display,
    };

    /// <summary>windowsMenus: Windows' clipboard history (Win+V), its own panel rather than Karpo's lines.</summary>
    public static readonly WidgetInfo WindowsClipboard = new()
    {
        Id = "windowsClipboard", Glyph = "\uF0E3",
        Category = WidgetCategory.WindowsMenus, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Clipboard,
    };

    /// <summary>windowsMenus: Windows' emoji panel (Win+.): emoji, GIFs, kaomoji and symbols.</summary>
    public static readonly WidgetInfo EmojiPanel = new()
    {
        Id = "emojiPanel", Glyph = "\uE76E",
        Category = WidgetCategory.WindowsMenus, Options = WidgetOptionsKind.None,
        Settings = WindowsPages.Typing,
    };

    /// <summary>The sliver at the very end that shows the desktop.</summary>
    public static readonly WidgetInfo ShowDesktop = new()
    {
        Id = "showDesktop", Glyph = "\uE977",
        Category = WidgetCategory.Taskbar, Options = WidgetOptionsKind.ShowDesktop, Settings = null,
    };

    /// <summary>
    /// sun: the sun's day for a place (<see cref="WeatherOptions"/>; docs\widgets.md §368), worked out on this
    /// PC with no network, as its path across the sky (this) or the day as a bar (<see cref="SunDayBar"/>): the
    /// sun widget's two styles, its sunrise, daylight and sunset under the picture. Its times are widgets of
    /// their own (§404: <see cref="Sunrise"/> and on). On the bar it is the sun preset, a folder wearing the
    /// next sunrise or sunset.
    /// </summary>
    public static readonly WidgetInfo SunArc = new()
    {
        // Segoe Fluent Icons' sun on the horizon, its rays long: a sunrise.
        Id = "sunArc", Glyph = "\uED39",
        Category = WidgetCategory.Weather, Options = WidgetOptionsKind.Weather, Settings = null,
    };

    /// <summary>sun: the sun's day as a bar, midnight to midnight, the daylight lit between its sunrise and sunset (§368).</summary>
    public static readonly WidgetInfo SunDayBar = new()
    {
        Id = "sunDayBar", Glyph = "\uED39",
        Category = WidgetCategory.Weather, Options = WidgetOptionsKind.Weather, Settings = null,
    };

    // sun, §404 (the person, 2026-10-29: "the daylight, sunset, solar noon, sunrise, etc. metrics should actually be
    // individual widgets"): each of the sun's times a widget of its own, a row of a place (WeatherOptions), worked
    // out on this PC as the sun widget is; their glyphs the sun line's rows' (SunText).

    /// <summary>sun: the next sunrise and how soon (§404).</summary>
    public static readonly WidgetInfo Sunrise = Time("sunrise", "\uED39");

    /// <summary>sun: the next sunset and how soon (§404).</summary>
    public static readonly WidgetInfo Sunset = Time("sunset", "\uED3A");

    /// <summary>sun: today's daylight, how much of it is left while the sun is up, else its change since yesterday (§404).</summary>
    public static readonly WidgetInfo Daylight = Time("daylight", "\uE823");

    /// <summary>sun: solar noon and how high the sun stands then (§404).</summary>
    public static readonly WidgetInfo SolarNoon = Time("solarNoon", "\uE706");

    /// <summary>sun: civil dawn, the sun 6° under the horizon, coming (§404).</summary>
    public static readonly WidgetInfo Dawn = Time("dawn", "\uE793");

    /// <summary>sun: civil dusk, the sun 6° under the horizon, gone (§404).</summary>
    public static readonly WidgetInfo Dusk = Time("dusk", "\uE708");

    // §590 (the person, 2026-11-09: "these widgets should be broken down into smaller ones that always show the same
    // amount of data"): the weather's and the air's lines split into kinds of fixed content, each with a few sizes.

    /// <summary>weather: the next hours across, a column each — its time, its glyph and its temperature (§590); the hourly forecast's List style.</summary>
    public static readonly WidgetInfo WeatherHoursList = Time("weatherHoursList", "");

    /// <summary>weather: the week as a graph, a column a day — its name, its glyph, its high and low on two curves (§590); the daily forecast's Graph style.</summary>
    public static readonly WidgetInfo WeatherDaysGraph = Time("weatherDaysGraph", "");

    /// <summary>air: the main pollutants now, PM2.5, PM10, ozone and nitrogen dioxide, for a place (§590).</summary>
    public static readonly WidgetInfo AirPollutants = Time("airPollutants", "");

    /// <summary>air: the pollen of six plants now, for a place with a pollen forecast (Europe, §590).</summary>
    public static readonly WidgetInfo AirPollen = Time("airPollen", "");

    /// <summary>air: the place's day of the air quality index, a bar an hour in its level's colour (§590).</summary>
    public static readonly WidgetInfo AirForecast = Time("airForecast", "");

    // §603 (the person, 2026-11-09: "split all of them"): the widgets that still showed more as they grew split into
    // kinds of fixed content, each with a few sizes, as the weather's and the air's were (§590).

    /// <summary>screentime: the fortnight's screen time ending at the day shown, a bar a day split by app (§603). A list's line.</summary>
    public static readonly WidgetInfo ScreenTimeHistory = new()
    {
        Id = "screenTimeHistory", Glyph = OptionGlyphs.Graph,
        Category = WidgetCategory.System, Options = WidgetOptionsKind.None, Settings = null,
    };

    /// <summary>tides: the day's tide curve for a place, its highs and lows marked (§603). A list's line.</summary>
    public static readonly WidgetInfo TideGraph = Time("tideGraph", OptionGlyphs.Graph);

    /// <summary>sports: a team's or a league's matches to come and played, a list (§603).</summary>
    public static readonly WidgetInfo SportsFixtures = SportsList("sportsFixtures", "\uE163");

    /// <summary>sports: the competition's table, every group of it, a list (§603).</summary>
    public static readonly WidgetInfo SportsTable = SportsList("sportsTable", "\uE80A");

    /// <summary>sports: a cup's knockout, round by round, a list (§603).</summary>
    public static readonly WidgetInfo SportsKnockout = SportsList("sportsKnockout", "\uF003");

    // §641 (the person, 2026-11-09: "create a widget group that looks like the start"): Start's parts, each a widget of a
    // list folder's, the Start group's lines (Presets.StartId). Beta. §653: no search box of their own any more.

    /// <summary>§641: Start's pinned apps, a grid: opened, pinned, unpinned, moved to the front or dragged into their order.</summary>
    public static readonly WidgetInfo StartPins = new()
    {
        Id = "startPins", Glyph = "",
        Category = WidgetCategory.Files, Options = WidgetOptionsKind.Start, Settings = WindowsPages.Start, Beta = true,
    };

    /// <summary>§641: every app on this PC, by category, in a grid or a list A to Z, as Start's All.</summary>
    public static readonly WidgetInfo StartApps = new()
    {
        Id = "startApps", Glyph = "",
        Category = WidgetCategory.Files, Options = WidgetOptionsKind.Start, Settings = WindowsPages.Start, Beta = true,
    };

    /// <summary>§641: Start's Recommended: the files opened lately and the apps added lately.</summary>
    public static readonly WidgetInfo StartRecommended = new()
    {
        Id = "startRecommended", Glyph = "",
        Category = WidgetCategory.Files, Options = WidgetOptionsKind.Start, Settings = WindowsPages.Start, Beta = true,
    };

    /// <summary>§641: Start's foot: the account's picture and name with its menu, the folders beside power, and the power menu.</summary>
    public static readonly WidgetInfo StartProfile = new()
    {
        Id = "startProfile", Glyph = "",
        Category = WidgetCategory.System, Options = WidgetOptionsKind.Start, Settings = WindowsPages.Power, Beta = true,
    };

    /// <summary>
    /// §642: Windows search (beta), a line of a list: a search box over Windows' own index (Windows Search's OLE DB provider),
    /// Start's apps, Settings' pages and answers worked out on the PC, everything found as it is typed, a best match and a group
    /// each, chips, quick actions and the keys for all of it (Karpo.Dock\WinSearch\WinSearchLine.cs). On the bar it is its
    /// ready-made group, a folder whose face is a search box.
    /// </summary>
    public static readonly WidgetInfo WinSearch = new()
    {
        Id = "winSearch", Glyph = "",
        Category = WidgetCategory.Files, Options = WidgetOptionsKind.WinSearch,
        Settings = WindowsPages.SearchingWindows, Beta = true,
    };

    private static WidgetInfo SportsList(string id, string glyph) => new()
    {
        Id = id, Glyph = glyph,
        Category = WidgetCategory.Time, Options = WidgetOptionsKind.Sports, Settings = null,
    };

    private static WidgetInfo Time(string id, string glyph) => new()
    {
        Id = id, Glyph = glyph,
        Category = WidgetCategory.Weather, Options = WidgetOptionsKind.Weather, Settings = null,
    };

    /// <summary>Every widget, in the order above.</summary>
    public static readonly WidgetInfo[] All =
    [
        WidgetsBoard, WeatherNow, Markets, Rss, Cpu, Gpu, Memory, Disk, Network,
        CpuDetails, GpuDetails, MemoryDetails, DiskDetails, NetworkDetails, CpuProcesses,
        Start, Search, Contacts, Mail, TaskView, SnippingTool, WindowsSettings,
        Desktops, Layouts, Pin, FolderContents,
        Apps, Media, MediaSources, Drive, NetworkDrive, RemovableDevices, Calendar, ScreenTime, ScreenTimeApps, ScreenTimeChart, FolderList, Heading, Divider,
        Clipboard, Indicators, Tray, PenMenu, TouchKeyboard, VirtualTouchpad, Wifi, Bluetooth, Hotspot, WirelessDisplays, Projection, NearbySharing,
        Volume, Brightness, SoundOutput, SoundInput, SoundMixer, SoundVolume, SpatialSound, Battery, Keyboard,
        WifiSwitch, BluetoothSwitch, SoundSwitch, MicrophoneSwitch, GameMode, ColorFilters, MonoAudio,
        AirplaneMode, MobileHotspot, Vpn, VpnConnections, EnergySaver, NightLight, DarkMode, RotationLock, LiveCaptions,
        NearbyShare, Accessibility, Cast, Project, Magnifier, Narrator, StickyKeys, KeepAwake, Security, PowerMode, Sleep, Hibernate, ShutDown, Restart, SignOut, Lock, Clock, Timer, Focus, Stopwatch, Alarm,
        NotificationsList, DoNotDisturb,
        QuickLink, WindowsNotifications, WindowsQuickSettings, WindowsSound, WindowsNetworks, WindowsCast, WindowsClipboard, EmojiPanel,
        ShowDesktop, Toggles, Actions, Month, TimeHeader,
        WeatherHours, WeatherDays, DateHeader, Events, Meeting, DayView, WeekView, YearView,
        Todo, Note, AirQuality, Tides,
        Sports, Quakes, Camera, SunArc, SunDayBar, Sunrise, Sunset, Daylight, SolarNoon, Dawn, Dusk,
        WeatherHoursList, WeatherDaysGraph, AirPollutants, AirPollen, AirForecast,
        ScreenTimeHistory, TideGraph, SportsFixtures, SportsTable, SportsKnockout,
        StartPins, StartApps, StartRecommended, StartProfile,
        WinSearch,
    ];

    /// <summary>The widget whose id is <paramref name="id"/>, or null for a word this build does not know.</summary>
    public static WidgetInfo? Find(string? id)
    {
        foreach (var info in All)
        {
            if (string.Equals(info.Id, id, StringComparison.Ordinal))
            {
                return info;
            }
        }
        return null;
    }

    /// <summary>The widget whose id is <paramref name="id"/>; throws for a word this build does not know.</summary>
    public static WidgetInfo Of(string id) =>
        Find(id) ?? throw new ArgumentOutOfRangeException(
            nameof(id), id, $"No widget has the id \"{id}\" (Karpo.Core\\WidgetInfos.cs).");

    /// <summary>The graph that shows <paramref name="metric"/>: <see cref="WidgetInfo.Metric"/> the other way round.</summary>
    public static WidgetInfo For(Metric metric) =>
        All.FirstOrDefault(info => info.Metric == metric && !info.Facts)
        ?? throw new ArgumentOutOfRangeException(nameof(metric), metric, "No graph shows this reading.");

    /// <summary>perf: the details of <paramref name="metric"/>, the facts behind its figure (<see cref="WidgetInfo.Facts"/>).</summary>
    public static WidgetInfo DetailsOf(Metric metric) =>
        All.FirstOrDefault(info => info.Metric == metric && info.Facts)
        ?? throw new ArgumentOutOfRangeException(nameof(metric), metric, "No details show this reading.");

    /// <summary>The button that stands for <paramref name="which"/>: <see cref="WidgetInfo.Switch"/> the other way round.</summary>
    public static WidgetInfo For(QuickSwitch which) =>
        All.FirstOrDefault(info => info.Switch == which)
        ?? throw new ArgumentOutOfRangeException(nameof(which), which, "No button stands for this switch.");

    /// <summary>The button that does <paramref name="command"/>: <see cref="WidgetInfo.Power"/> the other way round.</summary>
    public static WidgetInfo For(PowerCommand command) =>
        All.FirstOrDefault(info => info.Power == command)
        ?? throw new ArgumentOutOfRangeException(nameof(command), command, "No button does this command.");

    /// <summary>The widgets of <paramref name="category"/>, in the order <see cref="All"/> has them.</summary>
    public static IEnumerable<WidgetInfo> In(WidgetCategory category) =>
        All.Where(info => info.Category == category);
}
