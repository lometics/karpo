// What the registries say about a widget in the same words (docs\widgets.md §3): the
// catalogue's group, the options record it carries, the class that draws it, how wide it starts,
// what a press on it may be, and where Windows keeps the settings behind it.
//
// A kind used to be a value of the Section enum, and everything about it was decided by switching
// on that value somewhere else, and then by one table of descriptors, one per kind (Widgets.All).
// The split made it types — a taskbar's entries (BarEntries.cs) and a list's lines
// (ListWidgets.cs), each with a record of what the widget is (WidgetInfos.cs); a grid's cards were
// a third until 2026-10-09 — and these are the facts they state alike.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// The group a kind sits in in the catalogue, in the order the catalogue shows them
/// (docs\widgets.md §7). Every kind belongs to exactly one, so a new kind has to say
/// where someone browsing would look for it.
/// </summary>
public enum WidgetCategory
{
    /// <summary>What the Windows taskbar itself has: Start, search, task view, the apps, the tray.</summary>
    Taskbar,

    /// <summary>
    /// windowsMenus: Windows' own menus and panels, a button each that opens Windows' popup — Start,
    /// search, task view, the widgets board, the quick link menu, the notifications, quick settings
    /// and its sound and Wi-Fi pages, cast, clipboard history and the emoji panel (§466). Together, as
    /// the person asked (2026-11-01: "they should be grouped together"), and twelve, the catalogue's cap.
    /// </summary>
    WindowsMenus,

    /// <summary>The time and what is going on: the clock, the countdowns, the notifications, the news.</summary>
    Time,

    /// <summary>
    /// calendar: the day and the days around it — the agenda, the month, the day's events, the day,
    /// week and year views. A group of its own once the calendar flyout's views became lines
    /// (2026-10-09): Time and information would have run past the catalogue's twelve, and the
    /// calendar's pieces are one family, the flyout's, so the cap and the meaning agree.
    /// </summary>
    Calendar,

    /// <summary>weather: the conditions now, the next hours and the week — the weather flyout's parts.</summary>
    Weather,

    /// <summary>Wi-Fi, Bluetooth and the radios around them.</summary>
    Network,

    /// <summary>The PC itself: power, the battery, the keyboard, the pen and touch.</summary>
    System,

    /// <summary>
    /// quick: the screen — its brightness, night light and rotation lock, which Windows' own Settings
    /// keeps together on its Display page.
    /// <para>
    /// A group of its own for the reason Performance is one: the brightness was the thirteenth row
    /// under System on the Taskbar page, past the catalogue's cap of twelve, and the three are one
    /// family in Windows' own words, so the cap and the meaning agree.
    /// </para>
    /// </summary>
    Display,

    /// <summary>
    /// perf: how busy the machine is — the processor, the graphics, the memory, the disks, the
    /// network.
    /// <para>
    /// A group of their own rather than five more rows under System, and the catalogue's own cap
    /// is what said so: no group on the Widgets page may run past twelve rows, because that page
    /// is what somebody scans while choosing what to place and a group long enough to be a page of
    /// its own cannot be scanned. Five meters would have taken System past it. They are
    /// also a family in their own right — one sampler each, one shape between them, and Task
    /// Manager's own name for the set — so the cap and the meaning agree, which is the only case
    /// in which adding a group is right rather than convenient.
    /// </para>
    /// </summary>
    Performance,

    /// <summary>The volume, and what is playing.</summary>
    Media,

    /// <summary>What a click opens: a pin, a drive, a search.</summary>
    Files,

    /// <summary>notes: what the person writes down — the to-do lists and the notes (Notebook.cs).</summary>
    Notes,

    /// <summary>The folders, which hold the others.</summary>
    Folders,
}

/// <summary>
/// Where a new widget of a kind goes when nobody has said: which run of the bar (<see cref="Sections.RunOf"/>).
/// The Windows 11 taskbar's own arrangement, which is why the first of them is a rule and not a
/// group: Windows puts Start and the apps in the centre, or at the left when the person moved them.
/// </summary>
public enum WidgetGroup
{
    /// <summary>With Windows' own taskbar buttons: the middle of the bar, as on Windows 11.</summary>
    WithTaskbar,

    /// <summary>The start of the bar: the left, or the top on a bar standing on a side.</summary>
    Start,

    /// <summary>The end of the bar, where the taskbar keeps its clock and its tray.</summary>
    End,
}

/// <summary>
/// Which options object on <see cref="WidgetItem"/> a kind carries. Each kind carries exactly one or
/// none, <see cref="WidgetItem.Normalize"/> gives it that one and clears the rest, and the tests hold
/// this answer against what <see cref="Sections.New"/> actually makes — so a kind that arrives
/// with an options class nobody wired up fails the day it is added.
/// </summary>
public enum WidgetOptionsKind
{
    /// <summary>Nothing to set beyond where it sits.</summary>
    None,

    /// <summary>A pin's target, name and icon (<see cref="PinOptions"/>).</summary>
    Pin,

    /// <summary>Which drive a drive widget shows (<see cref="DriveTargetOptions"/>).</summary>
    Drive,

    /// <summary><see cref="WeatherOptions"/>.</summary>
    Weather,

    /// <summary><see cref="CalendarOptions"/>.</summary>
    Calendar,

    /// <summary>The time zone and the label, shared by the clock and the date (<see cref="ClockOptions"/>).</summary>
    Clock,

    /// <summary><see cref="DesktopOptions"/>.</summary>
    Desktops,

    /// <summary>The list and the widgets in it (<see cref="FolderOptions"/>).</summary>
    Folder,

    /// <summary>The slider's direction and length (<see cref="VolumeOptions"/>).</summary>
    Volume,

    /// <summary>markets: what a markets widget tracks — a share, a rate or a coin — and how it shows it (<see cref="MarketOptions"/>).</summary>
    Market,

    /// <summary>
    /// perf: the graph, the pace and the chosen adapter, shared by the five performance kinds
    /// (<see cref="PerformanceOptions"/>) as the clock and the date share theirs.
    /// </summary>
    Performance,

    /// <summary>A one-button widget's options (<see cref="SwitchOptions"/>).</summary>
    Switch,


    /// <summary>
    /// When the pen menu or the virtual touchpad shows: where Windows would, or
    /// always (<see cref="InputButtonOptions"/>).
    /// </summary>
    InputButton,

    /// <summary>calendar: a timer's or a focus session's own length and name (<see cref="CountdownOptions"/>).</summary>
    Countdown,

    /// <summary>list: a heading's words (<see cref="HeadingOptions"/>).</summary>
    Heading,

    /// <summary>Windows' search button: the box or only the icon (<see cref="WindowsSearchOptions"/>).</summary>
    WindowsSearch,

    /// <summary>quick: which displays the brightness's slider sets (<see cref="BrightnessOptions"/>).</summary>
    Brightness,

    /// <summary>§302: Show desktop with its icon or as Windows 11's bare corner (<see cref="ShowDesktopOptions"/>).</summary>
    ShowDesktop,

    /// <summary>rss: the feed it follows and whether the bar says its newest headline (<see cref="RssOptions"/>).</summary>
    Rss,

    /// <summary>The status icons: where each shows, on the bar, behind its arrow or nowhere (<see cref="StatusIconOptions"/>).</summary>
    StatusIcons,

    /// <summary>list: the toggles a toggles row shows, in its order (<see cref="TogglesOptions"/>).</summary>
    Toggles,

    /// <summary>list: an actions line's two pages of Windows' Settings (<see cref="ActionsOptions"/>).</summary>
    Actions,

    /// <summary>notes: which of the notebook's to-do lists a to-do line shows (<see cref="TodoOptions"/>).</summary>
    Todo,

    /// <summary>notes: which of the notebook's notes a note line shows (<see cref="NoteOptions"/>).</summary>
    Note,

    /// <summary>alarm: an alarm's time, days, switch and name (<see cref="AlarmOptions"/>).</summary>
    Alarm,

    /// <summary>sports: the team it follows (<see cref="SportsOptions"/>, docs\widgets.md §239).</summary>
    Sports,

    /// <summary>camera: the stream it shows and how (<see cref="CameraOptions"/>, docs\widgets.md §331).</summary>
    Camera,

    /// <summary>§347: a tray's own icons, where each shows and their order (<see cref="TrayIconOptions"/>).</summary>
    Tray,

    /// <summary>§435: a folder's divider, as the folders' look or growing (<see cref="DividerOptions"/>).</summary>
    Divider,

    /// <summary>§641: a Start widget's pins, view, folders or what it recommends (<see cref="StartOptions"/>).</summary>
    Start,

    /// <summary>§642, §652: Windows search's sources, one switch each, "Search in" (<see cref="WinSearchOptions"/>).</summary>
    WinSearch,
}

/// <summary>
/// What draws a kind. The bar's own parts are <see cref="None"/> — the scene draws them itself —
/// and everything else names the island class <c>Islands.Create</c> makes for it. Two of the
/// bar's own parts have a drawing of their own inside a folder, which is what the tile values are.
/// <para>
/// This is a name rather than the factory docs\widgets.md gives the descriptor, because the
/// classes live in Karpo.Dock and this list is shared with the settings app. Stage 4 turns it
/// into one paint method with a surface parameter, and the name goes with the two renderers.
/// </para>
/// </summary>
public enum WidgetSource
{
    /// <summary>The scene draws it: Start, search, the apps, the tray, the clock, the bar's own strips.</summary>
    None,

    /// <summary>drives: DriveIsland, for one fixed or network drive.</summary>
    Drive,

    /// <summary>drives: RemovableIsland, the line that lists what is plugged in.</summary>
    Removable,

    /// <summary>CalendarIsland: the calendar's line of a list, today and the events coming.</summary>
    Calendar,

    /// <summary>MediaIsland.</summary>
    Media,

    /// <summary>BatteryIsland: the battery's line of a list; on the bar the battery is the battery preset's face.</summary>
    Battery,

    /// <summary>quick: VolumeIsland, the slider on the bar itself.</summary>
    Volume,

    /// <summary>quick: BrightnessIsland, the display's brightness as a slider: on the bar, as a card and as a line.</summary>
    Brightness,

    /// <summary>markets: MarketIsland, a share's, a rate's or a coin's price and how far it has moved, or a currency converter (docs\widgets.md §246).</summary>
    Markets,

    /// <summary>rss: RssIsland, one feed's newest headlines: on the bar, as a card and as a line.</summary>
    Rss,

    /// <summary>quick: SwitchIsland, the one-button widgets.</summary>
    Switch,

    /// <summary>
    /// sound: SoundPartTile, the sound panel's three regions as lines of a folder's list. One source
    /// for the three, as the weather's regions share one: the same page's controls, a different
    /// region of them.
    /// </summary>
    SoundPart,

    /// <summary>SecurityIsland: whether this PC is protected.</summary>
    Security,

    /// <summary>IndicatorsIsland: the status icons Windows 11 shows beside the tray while something is so.</summary>
    Indicators,

    /// <summary>
    /// NotificationPartTile: one region of the notification panel as a line of a list folder -
    /// the list of notifications, and the "Clear all" that empties it. One class for the two,
    /// because they are one shape: the same plan, and a different region of it drawn by the
    /// painter that draws it in the panel.
    /// </summary>
    NotificationPart,

    /// <summary>folders: FolderIsland, the button that opens the folder's view.</summary>
    Folder,

    /// <summary>
    /// §474: Windows' own widgets that wear Windows' faces — the Widgets button, quick settings' button, the clock
    /// (WindowsFaces) —: the faces drawn as a folder's are (FolderIsland), fixed, with nothing inside; a click opens Windows' menu.
    /// </summary>
    WindowsFaces,

    /// <summary>ClockTile: the clock drawn as a folder's card, which the bar draws in the scene.</summary>
    ClockTile,

    /// <summary>calendar: CountdownTile, the timer's line and the focus session's.</summary>
    Countdown,

    /// <summary>calendar: StopwatchTile, the stopwatch's line.</summary>
    Stopwatch,

    /// <summary>pins: PinTile, one pin drawn as a folder's card.</summary>
    PinTile,

    /// <summary>§380: FolderContentsLine, what one folder on this PC holds, as a line of a list.</summary>
    FolderContents,

    /// <summary>
    /// TaskViewTile: Task view as a folder's card and line, which the bar draws in the scene. Its
    /// click presses Win+Tab, as the bar's button does.
    /// </summary>
    TaskView,

    /// <summary>
    /// ShortcutTile: the Snipping Tool or Settings as a folder's card and line, which
    /// the bar draws in the scene as one of Windows' buttons. Its click opens what the bar's does.
    /// </summary>
    WindowsShortcut,

    /// <summary>clipboard: ClipboardLine, what was copied lately as a line of a list, a click pasting it.</summary>
    Clipboard,

    /// <summary>pim: ContactsLine, the contacts as a line of a list, a search box over them.</summary>
    Contacts,

    /// <summary>§642: WinSearchLine, Windows search as a line of a list: a search box over the index, the apps, Settings and answers.</summary>
    WinSearch,

    /// <summary>pim: MailIsland, the inbox's line: the unread count over the newest mail.</summary>
    Mail,

    /// <summary>layouts: LayoutsLine, the taskbar layouts as a line of a list, a row each, the one shown marked.</summary>
    Layouts,

    /// <summary>desktops: DesktopsLine, the virtual desktops as a line of a list, a row each, dragged into their order.</summary>
    Desktops,

    /// <summary>perf: ProcessesLine, the processes busiest on the processor now, a row per app.</summary>
    Processes,

    /// <summary>
    /// weather: WeatherPartTile, the weather panel's header — the conditions now — as a line of a
    /// list, drawn by the control that draws it in the panel.
    /// </summary>
    WeatherPart,

    /// <summary>
    /// perf: PerformanceIsland, one live graph of how busy the machine is. One class for the five,
    /// because they are one shape — a glyph, a reading and a graph of the readings before it — and
    /// only the sampler behind them differs.
    /// </summary>
    Performance,

    /// <summary>
    /// ShellIsland: a kind that is in its registry before its drawing is built — its glyph and its
    /// title, and nothing it does. One class for every shell, on every surface, so building a
    /// kind's own drawing is one word of its registry record (its <c>Drawing</c>) and its class.
    /// </summary>
    Shell,

    /// <summary>list: HeadingTile, a title over the lines under it, as Windows' Settings heads a group.</summary>
    Heading,

    /// <summary>list: DividerTile, a hairline across the list, put between two lines on purpose.</summary>
    Divider,

    /// <summary>list: TogglesTile, a row of toggle icons, each on or off as Windows' quick settings show it.</summary>
    Toggles,

    /// <summary>list: ActionsTile, two text buttons to pages of Windows' Settings, as a flyout's footer.</summary>
    Actions,

    /// <summary>list: MonthTile, a month as the clock flyout's calendar draws it, whose click sets the folder's day.</summary>
    Month,

    /// <summary>list: TimeHeaderTile, the time with its seconds over the long date, as the top of the clock's flyout.</summary>
    TimeHeader,

    /// <summary>weather: WeatherHoursTile, the hourly forecast's Graph: today's curve with its hours and glyphs (§590).</summary>
    WeatherHours,

    /// <summary>weather: WeatherHoursListTile, the hourly forecast's List: the next hours across, a column each (§590).</summary>
    WeatherHoursList,

    /// <summary>weather: WeatherDaysTile, the daily forecast's List: a day a row or a column with its range (§590).</summary>
    WeatherDays,

    /// <summary>weather: WeatherDaysGraphTile, the daily forecast's Graph: the week's highs and lows on two curves (§590).</summary>
    WeatherDaysGraph,

    /// <summary>
    /// air: AirQualityTile, the air quality for a place, one piece of it a kind (§590): the index and its level, the
    /// pollutants, the pollen or the day's hours (docs\widgets.md §210).
    /// </summary>
    AirQuality,

    /// <summary>tides: TidesTile, the tides for a place: the sea now, the next high or low, the day's curve and the next turns (docs\widgets.md §236).</summary>
    Tides,
    /// <summary>sun: SunTile, the sun's day for a place, worked out on this PC: its arc, its day bar (docs\widgets.md §368), or one of its times, a row each (§404).</summary>
    Sun,

    /// <summary>quakes: QuakesLine, the earthquakes near a place: magnitude, distance, where and when (docs\widgets.md §240).</summary>
    Quakes,

    /// <summary>calendar: DateHeaderTile, today's date as the calendar flyout's header, whose click goes back to today.</summary>
    DateHeader,

    /// <summary>calendar: EventsTile, the folder's day's events as the calendar flyout listed them under its month.</summary>
    Events,

    /// <summary>calendar: CalendarViewTile, the calendar flyout's day, week or year view as a line; which is its kind.</summary>
    CalendarView,

    /// <summary>
    /// screentime: ScreenTimeLine, a day's screen time and the days before it as a graph, or the
    /// apps it went to — one class for the two lines, which read the one tracker
    /// (ScreenTimeTracker) and follow the folder's day.
    /// </summary>
    ScreenTime,

    /// <summary>screentime: ScreenTimeDonut, the folder's day's screen time as a ring split by app.</summary>
    ScreenTimeChart,

    /// <summary>notes: TodoTile, a to-do list of the notebook's (Notebook.cs), ticked, added to and arranged in place.</summary>
    Todo,

    /// <summary>notes: NoteTile, a note of the notebook's drawn as Markdown, edited in place as text.</summary>
    Note,

    /// <summary>
    /// quick: QuickPageTile, a quick-settings page — the network's, Bluetooth's, the mobile hotspot's, the power mode — as a line of a
    /// list, built and driven by the page's own code and controls; which page is its kind.
    /// </summary>
    QuickPage,

    /// <summary>
    /// power: PowerButtonTile, one command of Windows' power menu — sleep, hibernate, shut down,
    /// restart, sign out, lock — as the list's own row of its glyph and its name, whose click does it.
    /// </summary>
    PowerButton,

    /// <summary>alarm: AlarmTile, an alarm's line: its time, when it rings and its switch.</summary>
    Alarm,

    /// <summary>
    /// calendar: MeetingTile, the next meetings' line: today's meetings still to come, the one on now
    /// first, each with its time, its place and a Join button where it is online (docs\widgets.md §205).
    /// </summary>
    Meeting,

    /// <summary>sports: SportsTile, a team's live score or next match, and its matches around it (docs\widgets.md §239).</summary>
    Sports,

    /// <summary>camera: CameraLine, a live webcam's or IP camera's picture (docs\widgets.md §331).</summary>
    Camera,

    /// <summary>§641: StartAppsLine, the pinned apps or every app: one class for the two, which draw apps the one way.</summary>
    StartApps,

    /// <summary>§641: StartRecommendedLine, the files opened lately and the apps added lately.</summary>
    StartRecommended,

    /// <summary>§641: StartProfileLine, the account's picture and name, the folders beside power and the power button.</summary>
    StartProfile,
}

/// <summary>
/// How wide a kind is, in cells of the widget grid (a cell is a square of the bar's usable
/// thickness: docs\widgets.md §12.1). A widget's size follows its settings and never its live
/// state, so this is everything the size depends on that belongs to the kind rather than to
/// the instance.
/// </summary>
public readonly record struct WidgetSize
{
    public WidgetSize()
    {
    }

    /// <summary>
    /// The width a new widget of the kind is given, or <see cref="WidgetItem.AutomaticWidth"/> for
    /// the width its content works out to (<see cref="WidgetBounds.DefaultFor"/>).
    /// </summary>
    public required int DefaultCells { get; init; }

    /// <summary>
    /// The fewest cells a new widget of the kind starts with (<see cref="WidgetBounds.DefaultFor"/>).
    /// Only a start: a person may set anything from 1 to <see cref="WidgetItem.MostCells"/>.
    /// </summary>
    public int MinCells { get; init; } = WidgetItem.MinWidth;

    /// <summary>
    /// The most a new widget starts with, which is a wide widget even on a tall bar. Only a start,
    /// as <see cref="MinCells"/> is.
    /// </summary>
    public int MaxCells { get; init; } = WidgetItem.MaxWidth;

    /// <summary>As wide as the content works out to, and a width may be chosen.</summary>
    public static WidgetSize Automatic => new() { DefaultCells = WidgetItem.AutomaticWidth };

    /// <summary><paramref name="cells"/> cells to begin with, and a width may be chosen.</summary>
    public static WidgetSize Cells(int cells) => new() { DefaultCells = cells };

    /// <summary>
    /// One cell to start: a kind that draws a single glyph centred in its square and has nothing
    /// anywhere in its drawing that reads more room. A second cell does not make it bigger; the
    /// glyph stands in the middle of whatever it is given — which is the person's to set, as every
    /// size is (<see cref="WidgetBounds"/>), and one cell until they do.
    /// <para>
    /// It is a <b>measurement</b> and not a house style: the test of it is whether the drawing has
    /// a branch that uses the room, not whether a wider one would look odd. A kind that draws a
    /// label under a setting is not this, even with the label off, because the room is there to be
    /// used the moment somebody turns it on.
    /// </para>
    /// </summary>
    public static WidgetSize OneCell => new() { DefaultCells = WidgetItem.AutomaticWidth, MaxCells = 1 };
}

/// <summary>
/// pins: what a press and a wheel turn on a widget mean, which is what decides whether a press
/// may become a <b>move</b> of the widget along the bar. Every widget can be dragged to a new
/// place outside edit mode, so the bar has to know which widgets want a drag of their own first —
/// the volume slider's track is dragged to set the volume, and a track that moved the widget
/// instead would be a slider nobody can use.
/// <para>
/// The bar already asks the widget before it starts a move (<c>SliderPressed</c> runs before
/// <c>PinPressed</c>), so this is not what protects the slider: what it replaces is
/// <c>is ISliderIsland</c>, an answer a kind gave by omission. <see cref="Drag"/> is a
/// <b>gate</b>, not the answer — it says some of this widget takes drags, and the widget still
/// says where (its track, not its speaker). A kind that declares it must take drags, and a kind
/// that takes them must declare it; a test over the registry holds both directions, because the
/// second is the one that fails in silence.
/// </para>
/// </summary>
[Flags]
public enum Interaction
{
    /// <summary>A click, and nothing else. A press on it is free to become a move.</summary>
    Click = 0,

    /// <summary>A drag inside it may be its own: the bar asks before it starts a move.</summary>
    Drag = 1,

    /// <summary>A wheel turn over it is its own, and the surface under it does not scroll.</summary>
    Wheel = 2,
}

/// <summary>
/// A page of Windows' Settings app: a widget's (the link at the foot of its page), or one a menu's row
/// opens. Every one is a <see cref="WindowsPages"/> entry, so a page has one name wherever it is offered.
/// </summary>
/// <param name="Uri">An <c>ms-settings:</c> address.</param>
/// <param name="TitleKey">The key of what the link says (core.link.*): the page's name in Windows 11 and "settings", as "Sound settings".</param>
public readonly record struct WindowsLink(string Uri, string TitleKey)
{
    /// <summary>What the link says, in the language Karpo speaks now.</summary>
    public string Title => Strings.Text(TitleKey);
}

/// <summary>
/// menus: every page of Windows' Settings app Karpo offers, each once, under Windows 11's own name for it
/// and "settings" ("Power &amp; battery settings"), never "in Windows": a widget's (<see cref="WidgetInfo.Settings"/>),
/// a menu's row (Dock.WidgetMenus.cs) and a status icon's (Indicators.cs) take the same entry, so one page
/// reads the same everywhere (the person, 2026-10-23: "same Windows page = same name everywhere").
/// </summary>
public static class WindowsPages
{
    public static readonly WindowsLink Taskbar = new("ms-settings:taskbar", "core.link.taskbar");
    public static readonly WindowsLink Start = new("ms-settings:personalization-start", "core.link.start");
    public static readonly WindowsLink Search = new("ms-settings:search", "core.link.search");
    public static readonly WindowsLink Multitasking = new("ms-settings:multitasking", "core.link.multitasking");
    public static readonly WindowsLink Clipboard = new("ms-settings:clipboard", "core.link.clipboard");
    public static readonly WindowsLink Pen = new("ms-settings:pen", "core.link.pen");
    public static readonly WindowsLink Typing = new("ms-settings:typing", "core.link.typing");
    public static readonly WindowsLink Touchpad = new("ms-settings:devices-touchpad", "core.link.touchpad");
    public static readonly WindowsLink Network = new("ms-settings:network", "core.link.network");
    public static readonly WindowsLink Bluetooth = new("ms-settings:bluetooth", "core.link.bluetooth");
    public static readonly WindowsLink Sound = new("ms-settings:sound", "core.link.sound");
    public static readonly WindowsLink Display = new("ms-settings:display", "core.link.display");
    public static readonly WindowsLink Power = new("ms-settings:powersleep", "core.link.power");
    public static readonly WindowsLink Language = new("ms-settings:regionlanguage", "core.link.language");
    public static readonly WindowsLink DateTime = new("ms-settings:dateandtime", "core.link.dateTime");
    public static readonly WindowsLink AirplaneMode = new("ms-settings:network-airplanemode", "core.link.airplaneMode");
    public static readonly WindowsLink MobileHotspot = new("ms-settings:network-mobilehotspot", "core.link.mobileHotspot");
    public static readonly WindowsLink Vpn = new("ms-settings:network-vpn", "core.link.vpn");
    public static readonly WindowsLink DataUsage = new("ms-settings:datausage", "core.link.dataUsage");
    public static readonly WindowsLink EnergySaver = new("ms-settings:batterysaver", "core.link.energySaver");
    public static readonly WindowsLink NightLight = new("ms-settings:nightlight", "core.link.nightLight");

    /// <summary>Personalization's Colors page: where Windows' mode, dark or light, is chosen.</summary>
    public static readonly WindowsLink Colors = new("ms-settings:colors", "core.link.colors");
    public static readonly WindowsLink Captions = new("ms-settings:easeofaccess-captions", "core.link.captions");
    public static readonly WindowsLink NearbySharing = new("ms-settings:crossdevice", "core.link.nearbySharing");
    public static readonly WindowsLink Accessibility = new("ms-settings:easeofaccess", "core.link.accessibility");
    public static readonly WindowsLink Security = new("ms-settings:windowsdefender", "core.link.security");
    public static readonly WindowsLink Notifications = new("ms-settings:notifications", "core.link.notifications");
    public static readonly WindowsLink DoNotDisturb = new("ms-settings:quietmomentsscheduled", "core.link.doNotDisturb");
    public static readonly WindowsLink GameMode = new("ms-settings:gaming-gamemode", "core.link.gameMode");
    public static readonly WindowsLink ColorFilters = new("ms-settings:easeofaccess-colorfilter", "core.link.colorFilters");
    public static readonly WindowsLink MonoAudio = new("ms-settings:easeofaccess-audio", "core.link.monoAudio");
    public static readonly WindowsLink Magnifier = new("ms-settings:easeofaccess-magnifier", "core.link.magnifier");
    public static readonly WindowsLink Narrator = new("ms-settings:easeofaccess-narrator", "core.link.narrator");

    /// <summary>Accessibility's Keyboard page: sticky keys, and the Print screen key the Snipping Tool's is set on.</summary>
    public static readonly WindowsLink Keyboard = new("ms-settings:easeofaccess-keyboard", "core.link.keyboard");
    public static readonly WindowsLink Storage = new("ms-settings:storagesense", "core.link.storage");
    public static readonly WindowsLink AutoPlay = new("ms-settings:autoplay", "core.link.autoPlay");
    public static readonly WindowsLink WindowsUpdate = new("ms-settings:windowsupdate", "core.link.windowsUpdate");

    // §642: pages Windows search (beta) finds as well, the ones searched for most.
    public static readonly WindowsLink InstalledApps = new("ms-settings:appsfeatures", "core.link.installedApps");
    public static readonly WindowsLink DefaultApps = new("ms-settings:defaultapps", "core.link.defaultApps");
    public static readonly WindowsLink StartupApps = new("ms-settings:startupapps", "core.link.startupApps");
    public static readonly WindowsLink Background = new("ms-settings:personalization-background", "core.link.background");
    public static readonly WindowsLink Mouse = new("ms-settings:mousetouchpad", "core.link.mouse");
    public static readonly WindowsLink Printers = new("ms-settings:printers", "core.link.printers");
    public static readonly WindowsLink SignIn = new("ms-settings:signinoptions", "core.link.signIn");
    public static readonly WindowsLink About = new("ms-settings:about", "core.link.about");
    public static readonly WindowsLink Privacy = new("ms-settings:privacy", "core.link.privacy");

    /// <summary>§642: Privacy &amp; security › Searching Windows: what the index holds, Classic or Enhanced, the folders left out.</summary>
    public static readonly WindowsLink SearchingWindows = new("ms-settings:cortana-windowssearch", "core.link.searchingWindows");

    /// <summary>Every page, for the tests that hold their names to the one rule.</summary>
    public static IReadOnlyList<WindowsLink> All { get; } =
    [
        Taskbar, Start, Search, Multitasking, Clipboard, Pen, Typing, Touchpad, Network, Bluetooth, Sound, Display, Power,
        Language, DateTime, AirplaneMode, MobileHotspot, Vpn, DataUsage, EnergySaver, NightLight, Colors, Captions, NearbySharing,
        Accessibility, Security, Notifications, DoNotDisturb, GameMode, ColorFilters, MonoAudio, Magnifier, Narrator,
        Keyboard, Storage, AutoPlay, WindowsUpdate,
        InstalledApps, DefaultApps, StartupApps, Background, Mouse, Printers, SignIn, About, Privacy, SearchingWindows,
    ];
}

/// <summary>How the catalogue names its groups.</summary>
public static class WidgetCategories
{
    /// <summary>What the catalogue's sticky header says for a group (docs\widgets.md §7).</summary>
    public static string Title(this WidgetCategory category) => category switch
    {
        WidgetCategory.Taskbar => S.CoreCategoryTaskbar,
        WidgetCategory.WindowsMenus => S.CoreCategoryWindowsMenus,
        WidgetCategory.Time => S.CoreCategoryTime,
        WidgetCategory.Calendar => S.CoreCategoryCalendar,
        WidgetCategory.Weather => S.CoreCategoryWeather,
        WidgetCategory.Network => S.CoreCategoryNetwork,
        WidgetCategory.System => S.CoreCategorySystem,
        WidgetCategory.Display => S.CoreCategoryDisplay,
        WidgetCategory.Performance => S.CoreCategoryPerformance,
        WidgetCategory.Media => S.CoreCategoryMedia,
        WidgetCategory.Files => S.CoreCategoryFiles,
        WidgetCategory.Notes => S.CoreCategoryNotes,
        _ => S.CoreCategoryFolders,
    };
}
