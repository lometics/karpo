// Everything that can be a line of a list folder (the split, S5).
//
// A list is one column at the panel's width, each line as tall as its widget asks. A line is one
// of two drawings (ListLine): a widget's own card — the pieces of a panel, drawn by the painters
// that draw them there — or the list's own row of a glyph and a name.
//
// **No list line without content** (ruled 2026-09-26). The row is a name and nothing of what the
// widget shows, so a row was only here when its click did something: the pin and the switches that
// act. They are lines of their own now, which grow with the height they are given (the pin's icon
// and what it opens, a switch's state and what a click does); the row is kept for a kind that needs
// one: Task view, which presses Win+Tab as the bar's button does (2026-09-27), and the Snipping
// Tool and Settings, which open their apps as the bar's buttons do. A widget with more
// to show than its name has a line of its own instead — its list form, drawn by the class that
// draws its card, from the pieces its card and its flyout already paint (Karpo.Dock\Islands\ListForm.cs): the clock's face, the calendar's agenda, the
// battery's charge, a stock's header and its day, what is playing with its own controls, a drive's
// capacity, the five graphs and Security's standing. The volume slider's list form is the sound
// panel's volume row (SoundVolume). The desktops, the keyboard and the taskbar layouts had none
// until the desktop switcher, the input indicator and the layout switcher became presets
// (2026-10-11): their lines are their flyouts' lists, every desktop, keyboard layout and taskbar
// layout a row.
//
// Not here either: what only the taskbar has (Start, search, the apps, the tray, show desktop),
// the folders, and the widgets that left
// folders because each face is only a way into its flyout, which a folder does not open (Project); the weather, whose list form is its conditions (WeatherNow); the
// date (the person, 2026-09-26: "widgets to remove from list display: day switcher, date"), which
// is the taskbar's alone; and the weather panel's hours and week, which are no widgets at all
// ("weather hours, weather days, screen time, screen time graph, screen time app list, should be
// removed as widgets"). Screen time came back on 2026-10-09 as two lines of a list — the day's
// total with its graph, and its apps — and a ready-made folder of them (Presets.cs); it has no
// taskbar entry of its own.
//
// **Shells.** No line is a shell now: the timer and focus time were (WidgetSource.Shell, the
// kind's glyph and title with no act of its own) and are lines of their own, as removable devices'
// is. A kind added before its line is built is one again, until its record names its drawing.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// What a line of a list folder is. A word this build does not know reads as a value outside
/// the enum (<see cref="TolerantEnumConverter{T}"/>). The members are in
/// <see cref="ListWidgets.All"/>'s order, which is what lets <see cref="ListWidgets.Find(ListKind)"/>
/// be an index.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<ListKind>))]
public enum ListKind
{
    [JsonStringEnumMemberName("weatherNow")] WeatherNow,
    [JsonStringEnumMemberName("markets")] Markets,
    [JsonStringEnumMemberName("rss")] Rss,
    [JsonStringEnumMemberName("cpu")] Cpu,
    [JsonStringEnumMemberName("gpu")] Gpu,
    [JsonStringEnumMemberName("memory")] Memory,
    [JsonStringEnumMemberName("disk")] Disk,
    [JsonStringEnumMemberName("network")] Network,
    [JsonStringEnumMemberName("pin")] Pin,
    [JsonStringEnumMemberName("media")] Media,
    [JsonStringEnumMemberName("drive")] Drive,
    [JsonStringEnumMemberName("networkDrive")] NetworkDrive,
    [JsonStringEnumMemberName("removableDevices")] RemovableDevices,
    [JsonStringEnumMemberName("folderContents")] FolderContents,
    [JsonStringEnumMemberName("calendar")] Calendar,
    [JsonStringEnumMemberName("soundOutput")] SoundOutput,
    [JsonStringEnumMemberName("soundInput")] SoundInput,
    [JsonStringEnumMemberName("soundMixer")] SoundMixer,
    [JsonStringEnumMemberName("soundVolume")] SoundVolume,
    [JsonStringEnumMemberName("brightness")] Brightness,
    [JsonStringEnumMemberName("spatialSound")] SpatialSound,
    [JsonStringEnumMemberName("battery")] Battery,
    [JsonStringEnumMemberName("wifiSwitch")] WifiSwitch,
    [JsonStringEnumMemberName("bluetoothSwitch")] BluetoothSwitch,
    [JsonStringEnumMemberName("soundSwitch")] SoundSwitch,
    [JsonStringEnumMemberName("microphoneSwitch")] MicrophoneSwitch,
    [JsonStringEnumMemberName("gameMode")] GameMode,
    [JsonStringEnumMemberName("colorFilters")] ColorFilters,
    [JsonStringEnumMemberName("monoAudio")] MonoAudio,
    [JsonStringEnumMemberName("airplaneMode")] AirplaneMode,
    [JsonStringEnumMemberName("mobileHotspot")] MobileHotspot,
    [JsonStringEnumMemberName("vpn")] Vpn,
    [JsonStringEnumMemberName("energySaver")] EnergySaver,
    [JsonStringEnumMemberName("nightLight")] NightLight,
    [JsonStringEnumMemberName("rotationLock")] RotationLock,
    [JsonStringEnumMemberName("liveCaptions")] LiveCaptions,
    [JsonStringEnumMemberName("nearbyShare")] NearbyShare,
    [JsonStringEnumMemberName("accessibility")] Accessibility,
    [JsonStringEnumMemberName("doNotDisturb")] DoNotDisturb,
    [JsonStringEnumMemberName("security")] Security,
    [JsonStringEnumMemberName("clock")] Clock,
    [JsonStringEnumMemberName("timer")] Timer,
    [JsonStringEnumMemberName("focus")] Focus,
    [JsonStringEnumMemberName("stopwatch")] Stopwatch,
    [JsonStringEnumMemberName("notificationsList")] NotificationsList,
    [JsonStringEnumMemberName("mail")] Mail,
    [JsonStringEnumMemberName("screenTime")] ScreenTime,
    [JsonStringEnumMemberName("screenTimeApps")] ScreenTimeApps,
    [JsonStringEnumMemberName("screenTimeChart")] ScreenTimeChart,
    [JsonStringEnumMemberName("taskView")] TaskView,
    [JsonStringEnumMemberName("snippingTool")] SnippingTool,
    [JsonStringEnumMemberName("windowsSettings")] WindowsSettings,
    [JsonStringEnumMemberName("heading")] Heading,
    [JsonStringEnumMemberName("divider")] Divider,
    [JsonStringEnumMemberName("toggles")] Toggles,
    [JsonStringEnumMemberName("actions")] Actions,
    [JsonStringEnumMemberName("month")] Month,
    [JsonStringEnumMemberName("timeHeader")] TimeHeader,
    [JsonStringEnumMemberName("weatherHours")] WeatherHours,
    [JsonStringEnumMemberName("weatherDays")] WeatherDays,
    [JsonStringEnumMemberName("dateHeader")] DateHeader,
    [JsonStringEnumMemberName("events")] Events,
    [JsonStringEnumMemberName("dayView")] DayView,
    [JsonStringEnumMemberName("weekView")] WeekView,
    [JsonStringEnumMemberName("yearView")] YearView,
    [JsonStringEnumMemberName("todo")] Todo,
    [JsonStringEnumMemberName("note")] Note,
    [JsonStringEnumMemberName("wifi")] Wifi,
    [JsonStringEnumMemberName("bluetooth")] Bluetooth,
    [JsonStringEnumMemberName("hotspot")] Hotspot,
    [JsonStringEnumMemberName("wirelessDisplays")] WirelessDisplays,
    [JsonStringEnumMemberName("projection")] Projection,
    [JsonStringEnumMemberName("nearbySharing")] NearbySharing,
    [JsonStringEnumMemberName("powerMode")] PowerMode,
    [JsonStringEnumMemberName("clipboard")] Clipboard,
    [JsonStringEnumMemberName("contacts")] Contacts,
    [JsonStringEnumMemberName("alarm")] Alarm,
    [JsonStringEnumMemberName("mediaSources")] MediaSources,
    [JsonStringEnumMemberName("sleep")] Sleep,
    [JsonStringEnumMemberName("hibernate")] Hibernate,
    [JsonStringEnumMemberName("shutDown")] ShutDown,
    [JsonStringEnumMemberName("restart")] Restart,
    [JsonStringEnumMemberName("signOut")] SignOut,
    [JsonStringEnumMemberName("lock")] Lock,
    [JsonStringEnumMemberName("cpuDetails")] CpuDetails,
    [JsonStringEnumMemberName("gpuDetails")] GpuDetails,
    [JsonStringEnumMemberName("memoryDetails")] MemoryDetails,
    [JsonStringEnumMemberName("diskDetails")] DiskDetails,
    [JsonStringEnumMemberName("networkDetails")] NetworkDetails,
    [JsonStringEnumMemberName("desktops")] Desktops,
    [JsonStringEnumMemberName("keyboard")] Keyboard,
    [JsonStringEnumMemberName("vpnConnections")] VpnConnections,
    [JsonStringEnumMemberName("layout")] Layouts,
    [JsonStringEnumMemberName("cpuProcesses")] CpuProcesses,
    [JsonStringEnumMemberName("magnifier")] Magnifier,
    [JsonStringEnumMemberName("narrator")] Narrator,
    [JsonStringEnumMemberName("stickyKeys")] StickyKeys,
    [JsonStringEnumMemberName("cast")] Cast,
    [JsonStringEnumMemberName("project")] Project,
    [JsonStringEnumMemberName("meeting")] Meeting,
    [JsonStringEnumMemberName("airQuality")] AirQuality,
    [JsonStringEnumMemberName("tides")] Tides,
    [JsonStringEnumMemberName("sports")] Sports,
    [JsonStringEnumMemberName("quakes")] Quakes,
    [JsonStringEnumMemberName("keepAwake")] KeepAwake,
    [JsonStringEnumMemberName("camera")] Camera,
    [JsonStringEnumMemberName("sunArc")] SunArc,
    [JsonStringEnumMemberName("sunDayBar")] SunDayBar,
    [JsonStringEnumMemberName("darkMode")] DarkMode,
    [JsonStringEnumMemberName("sunrise")] Sunrise,
    [JsonStringEnumMemberName("sunset")] Sunset,
    [JsonStringEnumMemberName("daylight")] Daylight,
    [JsonStringEnumMemberName("solarNoon")] SolarNoon,
    [JsonStringEnumMemberName("dawn")] Dawn,
    [JsonStringEnumMemberName("dusk")] Dusk,
    [JsonStringEnumMemberName("weatherHoursList")] WeatherHoursList,
    [JsonStringEnumMemberName("weatherDaysGraph")] WeatherDaysGraph,
    [JsonStringEnumMemberName("airPollutants")] AirPollutants,
    [JsonStringEnumMemberName("airPollen")] AirPollen,
    [JsonStringEnumMemberName("airForecast")] AirForecast,
    [JsonStringEnumMemberName("screenTimeHistory")] ScreenTimeHistory,
    [JsonStringEnumMemberName("tideGraph")] TideGraph,
    [JsonStringEnumMemberName("sportsFixtures")] SportsFixtures,
    [JsonStringEnumMemberName("sportsTable")] SportsTable,
    [JsonStringEnumMemberName("sportsKnockout")] SportsKnockout,
    [JsonStringEnumMemberName("startPins")] StartPins,
    [JsonStringEnumMemberName("startApps")] StartApps,
    [JsonStringEnumMemberName("startRecommended")] StartRecommended,
    [JsonStringEnumMemberName("startProfile")] StartProfile,
    [JsonStringEnumMemberName("winSearch")] WinSearch,
}

/// <summary>
/// What a click on a list line does: nothing, or an act. Two answers, and deliberately no third: a
/// line cannot open a popup, because a popup opening out of a folder, which is a popup itself, is
/// what the folder rule is against (docs\widgets.md §15). Only a taskbar entry has a flyout or a
/// Windows panel to open (<see cref="BarClick"/>).
/// </summary>
public enum CellAct
{
    /// <summary>
    /// The click does nothing to the widget as a whole. Its own controls still work — a slider,
    /// the media buttons, a countdown's steppers — because a press on those is not a click on
    /// the widget.
    /// </summary>
    Nothing,

    /// <summary>
    /// It does something to the world, there and then: a pin opens its app, a switch flips its
    /// radio, a graph opens Task Manager, the security line opens Windows Security.
    /// </summary>
    Action,
}

/// <summary>Which of the two drawings a list line is.</summary>
public enum ListLine
{
    /// <summary>
    /// The widget's own card, at the list's width: a piece of a panel, drawn by the painter that
    /// draws it in the panel. It is the only drawing such a widget has, so it is used at whatever
    /// height the line is given and cut at the edge where there is more than fits.
    /// </summary>
    Card,

    /// <summary>
    /// The list's own row of the widget's glyph and its name, one line tall whatever anybody asks
    /// for. Only for a widget whose click acts (<see cref="CellAct.Action"/>): a row that did
    /// nothing would be a name with nothing behind it.
    /// </summary>
    Row,
}

/// <summary>
/// Everything about one list widget: what it is (<see cref="Info"/>), which drawing its line is,
/// how tall, what draws it and what a click and a press on it mean. One per
/// <see cref="ListKind"/>, in <see cref="ListWidgets.All"/>.
/// </summary>
public sealed record ListWidget
{
    /// <summary>Which widget this is: the word a list folder's line stores.</summary>
    public required ListKind Kind { get; init; }

    /// <summary>What it is, wherever it stands: its name, glyph, blurb and options record.</summary>
    public required WidgetInfo Info { get; init; }

    /// <summary>Its own card, or the list's row (<see cref="ListLine"/>).</summary>
    public required ListLine Line { get; init; }

    /// <summary>
    /// How tall the line is where nothing has measured its content, in the folder's cells (half a row each):
    /// the plan a test or a picture builds without a device. A row, two, for a <see cref="ListLine.Row"/>.
    /// </summary>
    public required int Cells { get; init; }

    /// <summary>
    /// Where a new line starts, in the folder's cells: one row, so the content is its own
    /// floor, to the most its content can use, with the content deciding in between — and
    /// <see cref="Cells"/> inside it, so a new line's range contains its own content. A row to a row for
    /// a <see cref="ListLine.Row"/>, which is a line whatever it is given.
    /// <para>
    /// <b>It is only where a line starts.</b> The fewest and the most a person sets are theirs, and
    /// they are not clamped into this (<see cref="WidgetBounds"/>).
    /// </para>
    /// </summary>
    public required (int Least, int Most) Range { get; init; }

    /// <summary>
    /// Whether the line is always exactly <see cref="Cells"/> tall, whatever its content or a file
    /// says: a card with no height to choose (<see cref="Folders.HasHeight(ListKind)"/>), so Settings
    /// shows no height card for it and a height a file holds is dropped. The actions line, a footer.
    /// </summary>
    public bool FixedHeight { get; init; }

    /// <summary>
    /// Whether its content is a number of things that varies — mail, devices, events, apps — so its
    /// line may be Automatic, as tall as they are up to a most (<see cref="LineHeight"/>). False for
    /// a widget whose content is the same pieces whatever happens (a clock, a graph, a switch): its
    /// line is Fixed only, and a file's automatic height for it reads as fixed (<see cref="LineHeight.Within(ListKind)"/>).
    /// </summary>
    public bool Grows { get; init; } = true;

    /// <summary>
    /// §432: the sizes a line of it comes in, smallest first, each with the cells its form is where nothing
    /// has measured it — one for a kind of one form (a timer: no height to set at all), two or three for a
    /// kind of a few (now playing: Compact, Medium, Full). Null for a kind whose content grows, which has
    /// the Height (<see cref="LineHeight"/>). A sized line is always whole cells (<see cref="LineSizes"/>).
    /// </summary>
    public IReadOnlyList<LineForm>? Forms { get; init; }

    /// <summary>§432: the size a new line of a kind with <see cref="Forms"/> starts at.</summary>
    public LineSize Starts { get; init; } = LineSize.Full;

    /// <summary>What a click on the line does (<see cref="CellAct"/>).</summary>
    public required CellAct Act { get; init; }

    /// <summary>
    /// pins: whether a press or a wheel turn on the line is the widget's own before it is the
    /// folder's (<see cref="Karpo.Core.Interaction"/>): the mixer's sliders, the volume row's.
    /// </summary>
    public required Interaction Interaction { get; init; }

    /// <summary>
    /// What draws the line's card, or what the list's factory makes a row's click with: the island
    /// class for the widget.
    /// </summary>
    public required WidgetSource Drawing { get; init; }

    /// <summary>
    /// The folder's channels it follows (<see cref="Channels"/>): an agenda follows the day. Each is
    /// a switch on its Settings page, on unless somebody turned it off.
    /// </summary>
    public IReadOnlyList<Channel> Follows { get; init; } = [];

    /// <summary>The folder's channels it publishes to: a month calendar sets the day.</summary>
    public IReadOnlyList<Channel> Publishes { get; init; } = [];

    /// <summary>
    /// Where its line may leave the folder at rest while it has nothing to show (§327, the line's
    /// <c>NothingToShow</c>): the strings key of its menu's "hide while idle" switch, on unless set
    /// off (<see cref="CellItem.HidesIdle"/>). Null for a line that is always there.
    /// </summary>
    public string? IdleKey { get; init; }
}

/// <summary>Every list widget, and the answers each gave.</summary>
public static class ListWidgets
{
    // Heights (2026-10-11, docs\widgets.md §140): a kind whose content is the same pieces whatever
    // happens says Grows = false — Fixed only, starting at its Cells. Every kind may be one row
    // (2026-10-13, §166) and as many as the folder's room holds (2026-10-16, §192: "remove the max
    // height for widgets"): no kind has a most of its own any more, and what a drawing does not fill
    // stands at the line's top (2026-10-11, "top aligned and not center their content"). A kind of
    // a varying number of things starts Automatic up to its Range's most. The per-kind comments below
    // say where each started before; the fields are what holds now. Since §432 (2026-10-30) a kind of one
    // form or a few has Forms and no Height at all: its size's form, measured, in whole rows (LineSize.cs);
    // the charts that stretched to a most of their own are sized since §603.

    /// <summary>Every list widget, in <see cref="WidgetInfos.All"/>'s order and <see cref="ListKind"/>'s.</summary>
    public static readonly ListWidget[] All =
    [
        // weather, §590: the current weather — the place, the conditions' glyph, the temperature, the conditions in words,
        // what it feels like with the day's high and low — the same at both sizes (WeatherPartTile): Compact the kit's row
        // with its caption, Full a hero over the place (§607). No Height: the hourly and the daily forecast are widgets of their
        // own, where a taller line once showed more of them (§192). A click anywhere on it searches the weather at its place
        // (2026-10-11, WeatherLineTile.SearchTheWeb), the line one thing to press (§379).
        new()
        {
            Kind = ListKind.WeatherNow, Info = WidgetInfos.WeatherNow,
            Line = ListLine.Card, Cells = 5, Range = (2, 12), Grows = false,
            Forms = [new(LineSize.Compact, 3), new(LineSize.Full, 5)],
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.WeatherPart,
        },
        // markets: a share's, a rate's or a coin's line is its ticker at every height (2026-10-11): one
        // row, its name, the figures and the chart behind them; taller, the name and what it is over the
        // figures at the top and the chart at the foot, no taller than MarketIsland.ChartDip. A currency
        // in the converter style is the converter instead (§246): its two fields, the swap, the rate and
        // the chart, whose fields take a press and a drag. Three rows where nothing has measured it; a new
        // line one row to eleven. A ticker's click searches what it tracks in the browser once the folder
        // has gone (§263; it did nothing before), so it lights as a whole; a converter's parts light themselves.
        new()
        {
            Kind = ListKind.Markets, Info = WidgetInfos.Markets,
            Line = ListLine.Card, Cells = 6, Range = (2, 22), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 6)],
            Act = CellAct.Action, Interaction = Interaction.Click | Interaction.Drag,
            Drawing = WidgetSource.Markets,
        },
        // rss: a feed's line, laid out for its height as the mail's is: the newest headline and how
        // old it is on one row; taller, the feed's name over its newest headlines a line each; taller
        // still, the headlines panel (§98): each headline over where it is from and how old it is with
        // its picture, the refresh, the site's link. A click on a headline opens its story in the browser. Four rows unmeasured, the name and about two headlines; a new
        // line may grow to twelve, eight headlines, and says no more.
        new()
        {
            Kind = ListKind.Rss, Info = WidgetInfos.Rss,
            Line = ListLine.Card, Cells = 8, Range = (2, 24),
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.Rss,
        },
        // perf: the five graphs, each a line of its own (PerformanceIsland), the same at both sizes (§603): Compact the
        // glyph, the reading's name and its figure on the list's gutter, its second reading under them, and the graph a band
        // under it edge to edge; Full the top of what the reading's flyout was (§97) — the name, the figure large with what
        // the graph's top stands for, the last minute on its quarters' hairlines and how far back it reaches. The facts
        // behind the figure are the reading's details, a widget of their own (below). A click opens Task Manager: an app
        // opening is what a click in a folder may do. No Height: what a taller line showed more of went with §603.
        new()
        {
            Kind = ListKind.Cpu, Info = WidgetInfos.Cpu,
            Line = ListLine.Card, Cells = 5, Range = (2, 10), Grows = false,
            Forms = [new(LineSize.Compact, 5), new(LineSize.Full, 9)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.Performance,
        },
        new()
        {
            Kind = ListKind.Gpu, Info = WidgetInfos.Gpu,
            Line = ListLine.Card, Cells = 5, Range = (2, 10), Grows = false,
            Forms = [new(LineSize.Compact, 5), new(LineSize.Full, 9)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.Performance,
        },
        new()
        {
            Kind = ListKind.Memory, Info = WidgetInfos.Memory,
            Line = ListLine.Card, Cells = 5, Range = (2, 10), Grows = false,
            Forms = [new(LineSize.Compact, 5), new(LineSize.Full, 9)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.Performance,
        },
        new()
        {
            Kind = ListKind.Disk, Info = WidgetInfos.Disk,
            Line = ListLine.Card, Cells = 5, Range = (2, 10), Grows = false,
            Forms = [new(LineSize.Compact, 5), new(LineSize.Full, 9)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.Performance,
        },
        new()
        {
            Kind = ListKind.Network, Info = WidgetInfos.Network,
            Line = ListLine.Card, Cells = 5, Range = (2, 10), Grows = false,
            Forms = [new(LineSize.Compact, 5), new(LineSize.Full, 9)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.Performance,
        },
        // pins: its icon with the running indicator under it and its name on one row, and from two
        // rows, under the name, whether it is open and what it opens (PinTile). The whole line opens
        // it. One row unmeasured, and a new line may be one row to six.
        new()
        {
            Kind = ListKind.Pin, Info = WidgetInfos.Pin,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 4)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.PinTile,
        },
        // What is playing: the art, the track and the artist, and its own controls — previous,
        // play or pause, next — each its own target: a click plays, pauses or skips there and the
        // folder stays open (MediaIsland). Anywhere else asks for the flyout, which a folder
        // refuses. One row keeps play or pause; taller, it is the whole widget, the track's timeline
        // too, which a press seeks and a drag scrubs (MediaLine.cs). Two rows unmeasured; measured,
        // the kit's row with its caption, 52 DIP (§401); a new line starts automatic, one row to six.
        new()
        {
            Kind = ListKind.Media, Info = WidgetInfos.Media,
            Line = ListLine.Card, Cells = 3, Range = (2, 12), Forms = [new(LineSize.Compact, 2), new(LineSize.Medium, 3), new(LineSize.Full, 6)], Starts = LineSize.Medium,
            Act = CellAct.Nothing, Interaction = Interaction.Click | Interaction.Drag,
            Drawing = WidgetSource.Media,
            // §327: gone from the folder at rest while nothing plays, as the face is (§319), in the face's words.
            IdleKey = "core.face.nowPlaying.hideIdle.title",
        },
        // drives: a drive's line is the kit's row with its caption and the meter (§401): its picture,
        // its name, its kind and how much is used, how much is free, the capacity meter in the alarm
        // colour once nearly nothing is left. Three rows unmeasured; a new line starts automatic, as
        // tall as those (64 DIP); one row is the picture, the name and the bar filling the room after
        // it. What the drive holds is a folder pin's to show, a line of its own under it (§119). A
        // click opens the drive in File Explorer.
        new()
        {
            Kind = ListKind.Drive, Info = WidgetInfos.Drive,
            Line = ListLine.Card, Cells = 3, Range = (2, FolderOptions.MostCells), Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)],
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.Drive,
        },
        new()
        {
            Kind = ListKind.NetworkDrive, Info = WidgetInfos.NetworkDrive,
            Line = ListLine.Card, Cells = 3, Range = (2, FolderOptions.MostCells), Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)],
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.Drive,
        },
        // drives: what is plugged in, as a line — a row per device with its picture, its name, what
        // is free and its eject button, the list's own row each (RemovableIsland.Cards.cs). It is the
        // removable-devices preset's one line, what the taskbar button's flyout was. Two rows where nothing has measured it, as tall as there are
        // devices up to a list's most, and past that it says how many more (WidgetFill). A click on
        // the line does nothing; a device's row and its button are the line's own controls, which
        // open the device and eject it.
        new()
        {
            Kind = ListKind.RemovableDevices, Info = WidgetInfos.RemovableDevices,
            Line = ListLine.Card, Cells = 4, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Nothing, Interaction = Interaction.Click, Drawing = WidgetSource.Removable,
        },
        // §380: what one folder on this PC holds, as a line (FolderContentsLine): its picture, its name
        // and how many items it holds on one row; taller, its items under that, a row each as the drive
        // flyout draws them, and what does not fit said in the last slot (WidgetFill). A file opens with
        // a click, a subfolder as the navigation says; the line anywhere else opens the folder itself.
        // Four rows where nothing has measured it — the name and three items — as tall as it has items
        // up to the room its folder gives it.
        new()
        {
            Kind = ListKind.FolderContents, Info = WidgetInfos.FolderContents,
            Line = ListLine.Card, Cells = 8, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.FolderContents,
        },
        // calendar: an agenda — the calendar's header and, under it in one column, every event coming in the
        // days the widget looks ahead, each in its calendar's colour, every day, today too, a heading
        // over its events (CalendarIsland.Line, §378, §419). It is a list that grows: the whole agenda scrolls
        // in its line, and the compact line says how many more there are. Six rows unmeasured, which is
        // a few events; a new line starts at one row to the panel's height.
        new()
        {
            Kind = ListKind.Calendar, Info = WidgetInfos.Calendar,
            Line = ListLine.Card, Cells = 12, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Nothing, Interaction = Interaction.Click,
            Drawing = WidgetSource.Calendar,
            // The folder's day: where a month beside it has a day picked, the agenda is that day's.
            Follows = [Channels.Day],
        },
        // sound: the sound panel's regions (docs\widgets.md §9, the fourth conversion), drawn by
        // the page's own controls, placed on the list's one gutter (SoundPartTile). A list and
        // nothing else, as the weather's conditions are — but for the volume row, which is the
        // volume slider's line. A device list is
        // five rows where nothing has measured it — the heading and three devices, or two and
        // "2 other devices" — and says what does not fit rather than scrolling.
        //
        // The heading is 52 DIP with its foot and a device is 42; a new line starts at one row to
        // the panel's height, is as many as there are, and fills and says the rest past that.
        new()
        {
            Kind = ListKind.SoundOutput, Info = WidgetInfos.SoundOutput,
            Line = ListLine.Card, Cells = 10, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Nothing, Interaction = Interaction.Click,
            Drawing = WidgetSource.SoundPart,
        },
        // Four rows: the heading with the mute at its end, and two microphones, which is what most
        // PCs have when they have more than one. It starts where the output does: one row to the
        // panel's height, as many as there are.
        new()
        {
            Kind = ListKind.SoundInput, Info = WidgetInfos.SoundInput,
            Line = ListLine.Card, Cells = 8, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Nothing, Interaction = Interaction.Click,
            Drawing = WidgetSource.SoundPart,
        },
        // Eight rows: the heading and six apps, or five and "N other apps". The one piece of the
        // panel that takes a drag — a mix's slider is dragged and turned with the wheel, exactly as
        // in the flyout — so it declares both, and the card says which of its parts are its own
        // (SoundPartTile.CardPress): the sliders, and not the icons that mute.
        new()
        {
            Kind = ListKind.SoundMixer, Info = WidgetInfos.SoundMixer,
            Line = ListLine.Card, Cells = 16, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Nothing, Interaction = Interaction.Drag | Interaction.Wheel,
            Drawing = WidgetSource.SoundPart,
        },
        // The panel's volume row, as a line of the list, and the volume slider's line: a slider
        // carried into a list becomes this row (Partners). One row: it is one line of controls, and
        // a list line of one row is 40 DIP — the flyout's row is 44, and
        // its controls centre in either. A new line may be one row to six: from two rows the output
        // it sets stands over the slider (SoundPartTile). Dragged and turned, like the mixer's
        // sliders and the volume slider on the bar.
        new()
        {
            Kind = ListKind.SoundVolume, Info = WidgetInfos.SoundVolume,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Medium, 4), new(LineSize.Full, 7)], Starts = LineSize.Compact,
            Act = CellAct.Nothing, Interaction = Interaction.Drag | Interaction.Wheel,
            Drawing = WidgetSource.SoundPart,
        },
        // quick: the brightness's line — the sun, its one slider and the level of the display the
        // folder opened on, however many displays there are (BrightnessIsland), in the middle of
        // the line however tall it is set; a new line starts at one row to four.
        new()
        {
            Kind = ListKind.Brightness, Info = WidgetInfos.Brightness,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Medium, 4), new(LineSize.Full, 7)], Starts = LineSize.Compact,
            Act = CellAct.Nothing, Interaction = Interaction.Drag | Interaction.Wheel,
            Drawing = WidgetSource.Brightness,
        },
        // quick: spatial sound, a toggle like the others, drawn as the switches' line at every height
        // (SwitchIsland); its click turns it off, or on in the format its Settings card names.
        new()
        {
            Kind = ListKind.SpatialSound, Info = WidgetInfos.SpatialSound,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 4)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        // quick: the battery's line — its level's glyph, the percentage and what it is doing, the
        // charge as a bar, and the time left in the battery flyout's own words (BatteryIsland,
        // BatteryText.TimeText). Three rows unmeasured for 83 DIP; a new line starts at one row to
        // six, each height drawn for itself: one row is the glyph, the bar and the percentage. Automatic
        // where it starts (§401): the kit's row with its caption and the meter, as tall as those.
        new()
        {
            Kind = ListKind.Battery, Info = WidgetInfos.Battery,
            Line = ListLine.Card, Cells = 3, Range = (2, 12), Forms = [new(LineSize.Compact, 2), new(LineSize.Medium, 3), new(LineSize.Full, 5)], Starts = LineSize.Medium,
            Act = CellAct.Nothing, Interaction = Interaction.Click,
            Drawing = WidgetSource.Battery,
        },
        // quick: the radios' and the sound's toggles, the switches' line as the others (SwitchIsland).
        new()
        {
            Kind = ListKind.WifiSwitch, Info = WidgetInfos.WifiSwitch,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = ListKind.BluetoothSwitch, Info = WidgetInfos.BluetoothSwitch,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = ListKind.SoundSwitch, Info = WidgetInfos.SoundSwitch,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = ListKind.MicrophoneSwitch, Info = WidgetInfos.MicrophoneSwitch,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        // quick: game mode, color filters and mono audio, the switches' line as the others (SwitchIsland).
        new()
        {
            Kind = ListKind.GameMode, Info = WidgetInfos.GameMode,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 4)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = ListKind.ColorFilters, Info = WidgetInfos.ColorFilters,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 4)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = ListKind.MonoAudio, Info = WidgetInfos.MonoAudio,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        // quick: the one-button widgets that act, each a line whose click flips what it stands for or
        // opens its page of Windows' settings: the glyph, the name and the switch on one row, and
        // taller its state and what a click does under the name (SwitchIsland). One row unmeasured,
        // and a new line may be one row to six. Project is not here: its face is only a way into its
        // own flyout, the bar's button alone. Cast is the cast preset's lines (below).
        new()
        {
            Kind = ListKind.AirplaneMode, Info = WidgetInfos.AirplaneMode,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 4)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = ListKind.MobileHotspot, Info = WidgetInfos.MobileHotspot,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = ListKind.Vpn, Info = WidgetInfos.Vpn,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = ListKind.EnergySaver, Info = WidgetInfos.EnergySaver,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 4)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = ListKind.NightLight, Info = WidgetInfos.NightLight,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = ListKind.RotationLock, Info = WidgetInfos.RotationLock,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = ListKind.LiveCaptions, Info = WidgetInfos.LiveCaptions,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = ListKind.NearbyShare, Info = WidgetInfos.NearbyShare,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = ListKind.Accessibility, Info = WidgetInfos.Accessibility,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        // Do not disturb, a switch's line as the other switches have (SwitchIsland): the glyph on the
        // accent while it is on, and its click switches it. One row unmeasured, and a new line may
        // be one row to six.
        new()
        {
            Kind = ListKind.DoNotDisturb, Info = WidgetInfos.DoNotDisturb,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        // Security's line: where the machine stands where the list puts a name, and more of what
        // Windows says the taller the line is — who is in charge under it from two rows, both
        // protections from three, each with its state from four (SecurityIsland). Its click opens
        // Windows Security, an app. Two rows unmeasured for 57 DIP; a new line starts at one row to
        // six, as every list line does at the least (2026-09-27). Automatic where it starts (§401): the
        // kit's row with its caption, as tall as that.
        new()
        {
            Kind = ListKind.Security, Info = WidgetInfos.Security,
            Line = ListLine.Card, Cells = 3, Range = (2, 12), Forms = [new(LineSize.Compact, 2), new(LineSize.Medium, 3), new(LineSize.Full, 7)], Starts = LineSize.Medium,
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.Security,
        },
        // The clock's line: the time as the Display on the gutter, the long date under it, and the zone or
        // label it stands for, a face beside them (ClockTile, §401). Three rows unmeasured; a new line starts
        // automatic up to six, as tall as its time and its date (about two rows). A click searches the time
        // where it stands in the browser once the folder has gone (2026-10-28, §377), so the line lights as a whole.
        new()
        {
            Kind = ListKind.Clock, Info = WidgetInfos.Clock,
            Line = ListLine.Card, Cells = 5, Range = (2, 12), Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 5)],
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.ClockTile,
        },
        // calendar: the timer and focus time as lines (CountdownTile): the kit's one row at every height
        // (§401) — the glyph, the name, the time, a subtle minus and plus and the accent start button; a
        // taller line has the row at its top. The minus, the plus and the start button are the line's own,
        // so a press on a stepper and the wheel are declared; a click beside them does nothing.
        new()
        {
            Kind = ListKind.Timer, Info = WidgetInfos.Timer,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Full, 2)],
            Act = CellAct.Nothing, Interaction = Interaction.Drag | Interaction.Wheel, Drawing = WidgetSource.Countdown,
        },
        new()
        {
            Kind = ListKind.Focus, Info = WidgetInfos.Focus,
            Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false, Forms = [new(LineSize.Full, 2)],
            Act = CellAct.Nothing, Interaction = Interaction.Drag | Interaction.Wheel, Drawing = WidgetSource.Countdown,
        },
        // calendar: the stopwatch as a line (StopwatchTile): the kit's row — the glyph, the name, the time
        // and its lap, reset and start buttons, as a countdown's row is (§401); taller, its laps under it,
        // newest first, a row each. Its buttons are plain clicks. As tall as its laps, up to its rows
        // (§388): one row with none.
        new()
        {
            Kind = ListKind.Stopwatch, Info = WidgetInfos.Stopwatch,
            Line = ListLine.Card, Cells = 2, Range = (2, 12),
            Act = CellAct.Nothing, Interaction = Interaction.Click, Drawing = WidgetSource.Stopwatch,
        },
        // folders: the notification panel as one widget of its own (docs\widgets.md §9), and since
        // the bell went (2026-10-10, §100) the whole of what its flyout did: grouped by app, a card
        // expanded to its buttons and its reply box, answered there. A click on a card brings its
        // app forward (CellAct.Action): the folder goes and the dock hands the toast's activation on.
        //
        // **One, and that took three tries.** Three pieces first (a header, the list, the footer's
        // link), then two (the list and a "Clear all" beside it), then the person: "dont show a
        // clear button of the notifications widget by itself". "Clear all" is a control **of** the
        // list, which is where the panel has it; a piece that cannot stand alone was never a piece.
        // The title is not one either — the open folder's own footer says what the folder is — and
        // "Notification settings" is a way out to Windows rather than part of the list.
        //
        // A list and nothing else. A notification's card is a logo, a title, three lines of body
        // and a time laid out across the panel's own width; squeezed into a smaller box every
        // notification is an ellipsis. That is the "squashed to fit" a widget declines.
        //
        // Twelve rows is 487 DIP where nothing has measured it: a 48 DIP band for "Clear all" and
        // about four notifications under it, which is what the panel this replaces stands at on an
        // ordinary day. The band with Clear all is 49 DIP and a notification one of two to four
        // lines; a new line starts at one row to the twelve it was declared at before it could
        // grow, is as many as there are, and fills and says the rest past that. Nothing inside a
        // folder scrolls (docs\widgets.md §9), so a turn of the wheel over this has nothing to do
        // here and belongs to the panel under it.
        new()
        {
            Kind = ListKind.NotificationsList, Info = WidgetInfos.NotificationsList,
            Line = ListLine.Card, Cells = 24, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Action, Interaction = Interaction.Click | Interaction.Drag,
            Drawing = WidgetSource.NotificationPart,
        },
        // pim: the inbox's line — the envelope beside the unread count, the newest messages under
        // it, each its sender and its subject, as many as its height holds. Four rows is the count
        // and about two messages; it grows to eight messages and says no more. A message's click
        // opens it in the account's web mail, the folder gone first (CellAct.Action).
        new()
        {
            Kind = ListKind.Mail, Info = WidgetInfos.Mail,
            Line = ListLine.Card, Cells = 8, Range = (2, 24),
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.Mail,
        },
        // screentime, §603: screen time today — the day's total and the daily average, the day the folder's (its Day
        // channel, today where nothing chose another) — the same at both sizes: Compact the kit's row, the day and its
        // total with the average as its caption; Full the day's name with a step back and on at the trailing gutter over
        // the total large, the average beside it. The fortnight's bars are screen time history, a widget of its own
        // (below), where a taller line once showed them under the words. The app is the folder's too (App, AppHover):
        // picked, the total is that app's.
        new()
        {
            Kind = ListKind.ScreenTime, Info = WidgetInfos.ScreenTime,
            Line = ListLine.Card, Cells = 5, Range = (2, 8), Grows = false,
            Forms = [new(LineSize.Compact, 3), new(LineSize.Full, 5)],
            Act = CellAct.Nothing, Interaction = Interaction.Click,
            Drawing = WidgetSource.ScreenTime,
            Follows = [Channels.Day, Channels.App, Channels.AppHover], Publishes = [Channels.Day],
        },
        // The apps the day went to, most used first: a row each — its icon, its name with a bar of
        // its share under it, and its time on the trailing gutter — lit under the pointer, and a
        // click picks it as the folder's app (2026-10-10: "it should not open it but select it"),
        // a second click or Escape lets it go; opening it is its right click's. The app under the
        // pointer and the app picked are the folder's (AppHover, App), so the total's bars light
        // them and a row lights for the bars' pointer too. As many as the line holds and how many
        // more in the last row (WidgetFill). Five rows unmeasured; a new line is as many as the day
        // has, up to the panel's height.
        new()
        {
            Kind = ListKind.ScreenTimeApps, Info = WidgetInfos.ScreenTimeApps,
            Line = ListLine.Card, Cells = 10, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Nothing, Interaction = Interaction.Click,
            Drawing = WidgetSource.ScreenTime,
            Follows = [Channels.Day, Channels.App, Channels.AppHover], Publishes = [Channels.App, Channels.AppHover],
        },
        // The day's screen time as a ring (2026-10-12: "a screen time donut chart as well, which shows
        // the current day / selected day / hovered over day"): the day's top apps and the rest as
        // "Other" round it, the total and the day in its middle, a legend beside it or under it. The
        // day is the folder's (Day), and while the pointer is on a day of the total's bars that day
        // (DayHover); a piece under the pointer lights and says its app, time and share, and is the
        // folder's app pointed at (AppHover), a click picks it (App). Always the same pieces, so
        // fixed: four rows hold the ring with the total in it and six legend rows at the usual text;
        // three the smallest ring, the total heading its legend; six the largest ring there is.
        new()
        {
            Kind = ListKind.ScreenTimeChart, Info = WidgetInfos.ScreenTimeChart,
            Line = ListLine.Card, Cells = 8, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 8)],
            Act = CellAct.Nothing, Interaction = Interaction.Click,
            Drawing = WidgetSource.ScreenTimeChart,
            Follows = [Channels.Day, Channels.DayHover, Channels.App, Channels.AppHover], Publishes = [Channels.App, Channels.AppHover],
        },
        // Task view as a line: the list's own row of its glyph and its name, as a pin's and a
        // switch's are, whose click presses Win+Tab through the bar's own seam (TaskViewTile).
        new()
        {
            Kind = ListKind.TaskView, Info = WidgetInfos.TaskView,
            Line = ListLine.Row, Cells = 2, Range = (2, 2),
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.TaskView,
        },
        // The Snipping Tool and Settings as lines: the list's own row of the glyph and the
        // name, as Task view's is, whose click opens what the bar's button opens (ShortcutTile).
        new()
        {
            Kind = ListKind.SnippingTool, Info = WidgetInfos.SnippingTool,
            Line = ListLine.Row, Cells = 2, Range = (2, 2),
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.WindowsShortcut,
        },
        new()
        {
            Kind = ListKind.WindowsSettings, Info = WidgetInfos.WindowsSettings,
            Line = ListLine.Row, Cells = 2, Range = (2, 2),
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.WindowsShortcut,
        },
        // list: a heading over the lines under it, which groups them, in Windows' Settings' own
        // words for it: a group header, its text strong and at the body's size, standing on the
        // list's gutter with the air over it that sets it apart from the group above (HeadingTile).
        // Its own height, less than a row's and set by nobody (HeadingTile.Height, Folders.HasHeight):
        // a title is one line. A click on it does nothing, and it does not light under the pointer,
        // because it is not a thing to press.
        new()
        {
            Kind = ListKind.Heading, Info = WidgetInfos.Heading,
            Line = ListLine.Card, Cells = 2, Range = (2, 2),
            Act = CellAct.Nothing, Interaction = Interaction.Click, Drawing = WidgetSource.Heading,
        },
        // list: a divider, the one line between two lines a list draws, and only where somebody put
        // one (DividerTile). The list draws none of its own between its lines, as the bar draws no
        // separator of its own. Thinner than any row whatever its range says: empty room (§505),
        // in edit mode striped (§524) and taken hold of over a taller grab (FolderFlyout.DividerDip).
        // A click on it does nothing and it does not light, as a heading does not; in edit mode it is
        // carried, selected and removed as any line.
        new()
        {
            Kind = ListKind.Divider, Info = WidgetInfos.Divider,
            Line = ListLine.Card, Cells = 2, Range = (2, 2),
            Act = CellAct.Nothing, Interaction = Interaction.Click, Drawing = WidgetSource.Divider,
        },
        // quick: a row of toggle icons, as the top of Windows 11's quick settings without the
        // words (TogglesTile): each toggle the person picked, in their order, its glyph on a button
        // filled with the accent while it is on; as many to a row as the list's width holds, and
        // the rest wrapped under them. Each button is its own target: a click switches that toggle
        // and the folder stays open, so the line itself does nothing. As tall as its rows of buttons, up
        // to six (§388): two rows unmeasured, one row of buttons, the six it starts with.
        new()
        {
            Kind = ListKind.Toggles, Info = WidgetInfos.Toggles,
            Line = ListLine.Card, Cells = 3, Range = (2, 12), Forms = [new(LineSize.Full, 3)],
            Act = CellAct.Nothing, Interaction = Interaction.Click, Drawing = WidgetSource.Toggles,
        },
        // A flyout's footer as a line (ActionsTile): two text buttons, one at its start and one at
        // its end, each its own target opening a page of Windows' Settings, which takes the focus
        // and so closes the folder as it would any flyout. The line itself does nothing. Always one
        // row, the list's line, whatever anybody asks: nothing in it grows (FixedHeight).
        new()
        {
            Kind = ListKind.Actions, Info = WidgetInfos.Actions,
            Line = ListLine.Card, Cells = 2, Range = (2, 2), FixedHeight = true,
            Act = CellAct.Nothing, Interaction = Interaction.Click, Drawing = WidgetSource.Actions,
        },
        // calendar: the month (MonthTile), as the clock flyout's calendar in Windows 11 draws it —
        // the month's name with the arrows that step it, the weekdays from the region's first, six
        // weeks. A click on a day publishes it as the folder's day (Channels.Day), and the month
        // follows the day too, so two months in a folder agree; the wheel steps the month. Eight
        // rows unmeasured; the header and six weeks are about 300 DIP at 100 % text.
        new()
        {
            Kind = ListKind.Month, Info = WidgetInfos.Month,
            Line = ListLine.Card, Cells = 16, Range = (2, FolderOptions.MostCells), Grows = false,
            Act = CellAct.Nothing, Interaction = Interaction.Click | Interaction.Wheel, Drawing = WidgetSource.Month,
            Follows = [Channels.Day], Publishes = [Channels.Day],
        },
        // calendar: the top of the clock's flyout as a line (TimeHeaderTile): the time large with its
        // seconds, in tabular figures so it never moves, over the long date. Three rows unmeasured
        // for 92 DIP; at 225 % text it is 175, which six rows hold. A click searches the time in its
        // zone, as the clock's line does (§377).
        new()
        {
            Kind = ListKind.TimeHeader, Info = WidgetInfos.TimeHeader,
            Line = ListLine.Card, Cells = 5, Range = (2, 12), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Medium, 4), new(LineSize.Full, 5)],
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.TimeHeader,
        },
        // weather, §590: the hourly forecast's Graph — today's curve, midnight to midnight, with its hours, their glyphs and
        // now marked (WeatherHoursTile, the weather flyout's own graph since §80; no night shaded since §572), starting
        // Compact, the flyout's own band, as it always stood — and the
        // daily forecast's List — the seven days, each with its glyph, its chance of rain and its high and low
        // (WeatherDaysTile). The same at each size: the graph's band shorter at Compact; the days across in columns at
        // Compact, a row each with its range on the week's scale at Full. Each day and each hour is pressed by itself (§131).
        new()
        {
            Kind = ListKind.WeatherHours, Info = WidgetInfos.WeatherHours,
            Line = ListLine.Card, Cells = 7, Range = (2, 16), Grows = false,
            Forms = [new(LineSize.Compact, 7), new(LineSize.Full, 9)], Starts = LineSize.Compact,
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.WeatherHours,
        },
        new()
        {
            Kind = ListKind.WeatherDays, Info = WidgetInfos.WeatherDays,
            Line = ListLine.Card, Cells = 14, Range = (2, 16), Grows = false,
            Forms = [new(LineSize.Compact, 6), new(LineSize.Full, 14)],
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.WeatherDays,
        },
        // calendar: the calendar flyout's header as a line (DateHeaderTile): today's date in its
        // longest form that fits, on a 48-DIP band; a click on it makes today the folder's day, as a
        // click on the flyout's header went back to today — anywhere on the line, which lights whole (§379). One row.
        new()
        {
            Kind = ListKind.DateHeader, Info = WidgetInfos.DateHeader,
            Line = ListLine.Card, Cells = 2, Range = (2, 4), Grows = false, Forms = [new(LineSize.Full, 2)],
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.DateHeader,
            Publishes = [Channels.Day],
        },
        // calendar: the day's events as the calendar flyout listed them under its month (EventsTile):
        // the day's name ("Today", "Tomorrow", its weekday) over a row per event, its calendar's
        // colour bar beside its title and its time and place — the folder's day where something
        // picks one. As tall as the day's events. Five rows unmeasured.
        new()
        {
            Kind = ListKind.Events, Info = WidgetInfos.Events,
            Line = ListLine.Card, Cells = 10, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Nothing, Interaction = Interaction.Click, Drawing = WidgetSource.Events,
            Follows = [Channels.Day],
        },
        // calendar: the calendar flyout's other views as lines (CalendarViewTile), each with its page's
        // title and arrows: the day's hours and the week's days, the whole day scrolling in them, and
        // the year's twelve months. The day picked is the folder's: a click on a day, a heading or
        // the arrows publish it, and each follows it, so a month or the day's events beside one agree.
        // The day and week are about 600 DIP, as the flyout stood; the year as tall as its months.
        new()
        {
            Kind = ListKind.DayView, Info = WidgetInfos.DayView,
            Line = ListLine.Card, Cells = 24, Range = (2, FolderOptions.MostCells), Grows = false,
            Act = CellAct.Nothing, Interaction = Interaction.Click | Interaction.Wheel, Drawing = WidgetSource.CalendarView,
            Follows = [Channels.Day], Publishes = [Channels.Day],
        },
        new()
        {
            Kind = ListKind.WeekView, Info = WidgetInfos.WeekView,
            Line = ListLine.Card, Cells = 24, Range = (2, FolderOptions.MostCells), Grows = false,
            Act = CellAct.Nothing, Interaction = Interaction.Click | Interaction.Wheel, Drawing = WidgetSource.CalendarView,
            Follows = [Channels.Day], Publishes = [Channels.Day],
        },
        new()
        {
            Kind = ListKind.YearView, Info = WidgetInfos.YearView,
            Line = ListLine.Card, Cells = 24, Range = (2, FolderOptions.MostCells), Grows = false,
            Act = CellAct.Nothing, Interaction = Interaction.Click | Interaction.Wheel, Drawing = WidgetSource.CalendarView,
            Follows = [Channels.Day], Publishes = [Channels.Day],
        },
        // notes: a to-do list (TodoTile, docs\widgets.md §87): its name, "Add a task", the tasks not
        // done, and "Completed" with the done ones under it once opened. Every part is the line's own
        // — a box ticks, a row's text is edited in place, a row is dragged into order — so the press
        // is declared (Drag), and a click on the line as a whole does nothing. As tall as the list,
        // up to the panel's height; a line held shorter shows the tasks that fit and how many more.
        new()
        {
            Kind = ListKind.Todo, Info = WidgetInfos.Todo,
            Line = ListLine.Card, Cells = 12, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Nothing, Interaction = Interaction.Click | Interaction.Drag,
            Drawing = WidgetSource.Todo,
        },
        // notes: a note (NoteTile): its title and its body, one live Markdown editor (Obsidian's live
        // preview), a link opened and a task's box ticked by a click, the caret put by a click
        // anywhere else. The press selects (Drag) and the wheel scrolls a note longer than its line
        // (Wheel), the list's once it can go no further. As tall as the note, up to twelve rows.
        new()
        {
            Kind = ListKind.Note, Info = WidgetInfos.Note,
            Line = ListLine.Card, Cells = 12, Range = (2, 24),
            Act = CellAct.Nothing, Interaction = Interaction.Click | Interaction.Drag | Interaction.Wheel,
            Drawing = WidgetSource.Note,
        },
        // quick: the network and Bluetooth pages of the quick settings as lines (2026-10-10, as the
        // calendar's and the weather's flyouts went): the page itself, its title with the radio's
        // switch and its rows, drawn and driven by the page's own controls on the list's gutter
        // (QuickPageTile), without the page's footer, which is the actions line under it. As tall
        // as what the page shows, a network or a device opened in place included. A press on a
        // control is the line's (a key's field takes a drag to select); a click beside them does
        // nothing. Nine and seven rows unmeasured: the title and five networks, four devices.
        new()
        {
            Kind = ListKind.Wifi, Info = WidgetInfos.Wifi,
            Line = ListLine.Card, Cells = 18, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Nothing, Interaction = Interaction.Click | Interaction.Drag, Drawing = WidgetSource.QuickPage,
        },
        new()
        {
            Kind = ListKind.Bluetooth, Info = WidgetInfos.Bluetooth,
            Line = ListLine.Card, Cells = 14, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Nothing, Interaction = Interaction.Click | Interaction.Drag, Drawing = WidgetSource.QuickPage,
        },
        // quick: the mobile hotspot's page the same way (2026-10-10, after the timers): its title
        // with the hotspot's switch, the network's name, its password behind the eye and Copy, its
        // band and how many are on it, and the devices on it while it is on (QuickPageTile). It was
        // the mobile hotspot button's flyout; the button is the mobile hotspot preset now. Eight rows
        // unmeasured: the title, the four properties and two devices. The switch alone stays the
        // mobileHotspot line, as before.
        new()
        {
            Kind = ListKind.Hotspot, Info = WidgetInfos.Hotspot,
            Line = ListLine.Card, Cells = 16, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Nothing, Interaction = Interaction.Click | Interaction.Drag, Drawing = WidgetSource.QuickPage,
        },
        // quick: Windows' Cast panel as a line (2026-10-11, "is it hard to implement everything from
        // cast properly"): "Cast", the wireless displays in range as Windows finds them, the one this
        // PC shows on first and marked; a display's row opens Windows' own Cast panel (Win+K), which
        // alone may connect and disconnect (QuickSettings\Services\Cast.cs says why). Six rows
        // unmeasured: the title, three displays and the line under them.
        new()
        {
            Kind = ListKind.WirelessDisplays, Info = WidgetInfos.WirelessDisplays,
            Line = ListLine.Card, Cells = 12, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Nothing, Interaction = Interaction.Click | Interaction.Drag, Drawing = WidgetSource.QuickPage,
        },
        // quick: Win+P's four modes as a line (2026-10-11, the cast preset's): "Projection mode", PC
        // screen only, Duplicate, Extend and Second screen only, the one in force marked, a click
        // putting it in force as Win+P does (QuickSettings\Services\Projection.cs). Six rows; at most
        // thirteen, the page whole at 225 % text (measured, §166: nine held two of its four modes).
        new()
        {
            Kind = ListKind.Projection, Info = WidgetInfos.Projection,
            Line = ListLine.Card, Cells = 12, Range = (2, FolderOptions.MostCells), Grows = false, Forms = [new(LineSize.Full, 12)],
            Act = CellAct.Nothing, Interaction = Interaction.Click | Interaction.Drag, Drawing = WidgetSource.QuickPage,
        },
        // quick: Windows' Nearby sharing as a line (2026-10-11, "is it possible to actually make a
        // proper flyout out of this"): Off, My devices only and Everyone nearby, the one in force
        // marked, where received files are saved with Change, and the name this PC is found by
        // (QuickSettings\Services\Nearby.cs). Eight rows unmeasured; at most
        // twenty, the page whole at 225 % text (measured, §166).
        new()
        {
            Kind = ListKind.NearbySharing, Info = WidgetInfos.NearbySharing,
            Line = ListLine.Card, Cells = 18, Range = (2, FolderOptions.MostCells), Grows = false, Forms = [new(LineSize.Full, 18)],
            Act = CellAct.Nothing, Interaction = Interaction.Click | Interaction.Drag, Drawing = WidgetSource.QuickPage,
        },
        // quick: the battery flyout's power mode as a line (2026-10-10, after the hotspot): the power
        // mode of Windows' "Power & battery" page, its heading and the three modes, the one in force
        // marked, a click setting it (Windows asked off the dock's thread). As tall as its rows
        // (QuickPageTile): five rows unmeasured, eleven at most, the page whole at 225 % text (§166).
        // Shorter, it fits itself down to one row: the modes listed, then as a segmented control (§248).
        // The power commands beside it went their own way
        // (2026-10-11): each is a power button of its own, at the list's end.
        new()
        {
            Kind = ListKind.PowerMode, Info = WidgetInfos.PowerMode,
            Line = ListLine.Card, Cells = 10, Range = (2, FolderOptions.MostCells), Grows = false, Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 10)],
            Act = CellAct.Nothing, Interaction = Interaction.Click | Interaction.Drag, Drawing = WidgetSource.QuickPage,
        },
        // clipboard: what was copied lately (2026-10-10, the clipboard history entry's flyout as a
        // line): how many items and "Clear all", then a row per item, the newest first — text in its
        // first lines, a picture as its thumbnail — with its pin and delete under the pointer; while
        // Windows' own history is off, why, and the way to turn it on. A row's click copies it and
        // the row says "Copied", the folder staying; Enter on a row pastes it where the person was,
        // the folder gone first (CellAct.Action). Six rows unmeasured; a new line grows to the most a
        // line may be.
        new()
        {
            Kind = ListKind.Clipboard, Info = WidgetInfos.Clipboard,
            Line = ListLine.Card, Cells = 12, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Clipboard,
        },
        // pim: the people in the person's accounts (2026-10-11, the contacts entry's flyout as a
        // line): a search box, what it is typing narrowing the people under it at once, each its
        // name over its address or number; a click opens a row in place, its details a line each with
        // Copy, and Email or Call (2026-10-11). Copying acts in place and the folder stays; Email and
        // Call hand Windows mailto: or tel: once the folder has gone (CellAct.Action). The box takes a
        // press and a drag (Drag). Eight rows unmeasured; a new line grows to the most a line may be.
        new()
        {
            Kind = ListKind.Contacts, Info = WidgetInfos.Contacts,
            Line = ListLine.Card, Cells = 16, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Action, Interaction = Interaction.Click | Interaction.Drag, Drawing = WidgetSource.Contacts,
        },
        // alarm: one alarm (2026-10-11, §106): the kit's row with its caption (§401) — its glyph, its name
        // with what it is doing and when it rings under it, its time and its switch at the end; one row
        // has no caption. A new line starts automatic, the row's 52 DIP. Ringing, its Snooze and Dismiss
        // stand where the switch was. Only the switch and those buttons act; a click beside them does
        // nothing, so a click meant for the folder never sets anybody's alarm.
        new()
        {
            Kind = ListKind.Alarm, Info = WidgetInfos.Alarm,
            Line = ListLine.Card, Cells = 3, Range = (2, 12), Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)],
            Act = CellAct.Nothing, Interaction = Interaction.Click, Drawing = WidgetSource.Alarm,
        },
        // media: the apps playing, to pick which one now playing follows (2026-10-11: "the now
        // playing folder should show the selector for the source again. where i can choose brave or
        // spotify"), the media flyout's "Playing in" list as a line: a row per app, its icon, its
        // name and whether it plays, the one followed marked as the output device's is. A click on a
        // row follows that app, in every now-playing line and face (MediaPick). As tall as there are
        // apps; with none it says nothing plays.
        new()
        {
            Kind = ListKind.MediaSources, Info = WidgetInfos.MediaSources,
            Line = ListLine.Card, Cells = 4, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Nothing, Interaction = Interaction.Click, Drawing = WidgetSource.Media,
        },
        // power: the commands of Windows' power menu, a button each (2026-10-11: "the power menu should
        // actually be separate widgets which are just single buttons"): the list's own row of the
        // command's glyph and its name, as Task view's is, whose click closes the folder and does it
        // at once, as a row of Windows' menu does (PowerButtonTile). Sleep and hibernate where
        // Windows' power menu offers them; where it does not, the row says so and does nothing.
        PowerButton(ListKind.Sleep, WidgetInfos.Sleep),
        PowerButton(ListKind.Hibernate, WidgetInfos.Hibernate),
        PowerButton(ListKind.ShutDown, WidgetInfos.ShutDown),
        PowerButton(ListKind.Restart, WidgetInfos.Restart),
        PowerButton(ListKind.SignOut, WidgetInfos.SignOut),
        PowerButton(ListKind.Lock, WidgetInfos.Lock),
        // perf: the readings' details (2026-10-11): the facts behind a reading's figure in two columns,
        // each its name over its figure, as the flyout had them under its minute (PerformanceIsland,
        // the graphs' class). As tall as the facts, two to five rows of the folder's at the list's own text and up to twelve at its largest; a
        // click opens Task Manager, as the graph's does.
        Details(ListKind.CpuDetails, WidgetInfos.CpuDetails),
        Details(ListKind.GpuDetails, WidgetInfos.GpuDetails),
        Details(ListKind.MemoryDetails, WidgetInfos.MemoryDetails),
        Details(ListKind.DiskDetails, WidgetInfos.DiskDetails),
        Details(ListKind.NetworkDetails, WidgetInfos.NetworkDetails),
        // desktops: the virtual desktops (2026-10-11, the desktop switcher's flyout as a line, §116):
        // a row per desktop in Windows' order, the one you are on marked, the apps open on each at
        // its end; a row's gripper drags it into another place, its pencil renames it there and its
        // cross closes it; "New desktop" at the foot. A click on a row switches to that desktop, the
        // folder gone first without handing the focus back (CellAct.Action, IslandView.Desktops):
        // the window that had it is on the desktop being left. The gripper takes a press and a drag
        // (Drag). Five rows unmeasured; a new line grows to the most a line may be.
        new()
        {
            Kind = ListKind.Desktops, Info = WidgetInfos.Desktops,
            Line = ListLine.Card, Cells = 10, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Action, Interaction = Interaction.Click | Interaction.Drag, Drawing = WidgetSource.Desktops,
        },
        // keyboard: the keyboard layouts to switch to (2026-10-11, the input indicator's flyout as a
        // line: "all the remaining flyouts should become folders with a face now"): a row per layout
        // installed, its language over the layout, the one in use marked; a click switches the window
        // the folder opened over to it and closes the folder, as Windows' input switcher does
        // (QuickPageTile, KeyboardPage). As tall as there are layouts; three rows unmeasured.
        new()
        {
            Kind = ListKind.Keyboard, Info = WidgetInfos.Keyboard,
            Line = ListLine.Card, Cells = 6, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Nothing, Interaction = Interaction.Click, Drawing = WidgetSource.QuickPage,
        },
        // quick: the VPN connections (2026-10-11, the VPN entry's flyout as a line): "VPN" over a row
        // per connection, by name, with where it stands ("Connected", "Connecting…", why it could
        // not); a click connects it or disconnects it, as a row of Windows' VPN page does (QuickPageTile,
        // ChoicesPage). The vpn switch beside it stays a line of its own. As tall as there are
        // connections; four rows unmeasured.
        new()
        {
            Kind = ListKind.VpnConnections, Info = WidgetInfos.VpnConnections,
            Line = ListLine.Card, Cells = 8, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Nothing, Interaction = Interaction.Click, Drawing = WidgetSource.QuickPage,
        },
        // layouts: the taskbar layouts (2026-10-11, the layout switcher's flyout as a line): a row
        // per layout in the config's order, its glyph and its name, the one this bar shows marked.
        // A click on another switches this display to it — the bar's own menu's act — once the
        // folder has gone, since the switch builds the bar again (CellAct.Action, IslandView.Layouts).
        // As tall as there are layouts; three rows unmeasured.
        new()
        {
            Kind = ListKind.Layouts, Info = WidgetInfos.Layouts,
            Line = ListLine.Card, Cells = 6, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Layouts,
        },
        // perf: the processes busiest on the processor now (2026-10-12, §141), Task Manager's
        // processes sorted by CPU: a row per app — its processes together, as Task Manager groups an
        // app's — its icon, its name with how many processes, its share of the whole processor and a
        // thin bar of it, as many rows as the line holds, read every two seconds while it shows. A
        // click opens Task Manager, as the graph's does; a row's right click ends its task, after
        // Windows' own question. Five rows unmeasured; a new line is as many as twelve rows hold.
        new()
        {
            Kind = ListKind.CpuProcesses, Info = WidgetInfos.CpuProcesses,
            Line = ListLine.Card, Cells = 10, Range = (2, 24),
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Processes,
        },
        // quick: the accessibility page's magnifier, narrator and sticky keys (2026-10-14), a
        // switch's line as the others' (SwitchIsland).
        Switch(ListKind.Magnifier, WidgetInfos.Magnifier),
        Switch(ListKind.Narrator, WidgetInfos.Narrator),
        Switch(ListKind.StickyKeys, WidgetInfos.StickyKeys),
        // quick: Windows' Cast and Project tiles (2026-10-16), a switch's line as accessibility's,
        // all of it its page in a folder (FolderPages).
        Switch(ListKind.Cast, WidgetInfos.Cast),
        Switch(ListKind.Project, WidgetInfos.Project),
        // calendar: the next meetings (MeetingTile, docs\widgets.md §205): one row, the next meeting —
        // the one on now, else the next today — its title and how soon, its Join; taller, today's
        // meetings still to come a row each, the time and the place under the title. Only a Join acts:
        // it opens the meeting in its app once the folder has gone (CellAct.Action, IslandView.App);
        // the rest of the line does nothing. As tall as today's meetings; two rows unmeasured.
        new()
        {
            Kind = ListKind.Meeting, Info = WidgetInfos.Meeting,
            Line = ListLine.Card, Cells = 4, Range = (2, 16),
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Meeting,
        },
        // air, §590: the air quality index for a place of its own (AirQualityTile, docs\widgets.md §210) — its level's
        // dot, the level, the index, the place and the index's name — at both sizes: Compact the kit's row with its caption,
        // Full the same over the scale's bands with a mark where it stands. Its pollutants, its pollen and its day are
        // widgets of their own (below), where a taller line once showed more of them. §614 each one thing to press: its click
        // searches the air quality at its place once the folder has gone (CellAct.Action, IslandView.App), as a clock's.
        new()
        {
            Kind = ListKind.AirQuality, Info = WidgetInfos.AirQuality,
            Line = ListLine.Card, Cells = 5, Range = (2, 8), Grows = false,
            Forms = [new(LineSize.Compact, 3), new(LineSize.Full, 5)],
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.AirQuality,
        },
        // tides, §603: the tides now for a place of its own (TidesTile, docs\widgets.md §236) — the way the sea goes, the
        // next high or low and how soon, the height now — the same at both sizes: Compact the kit's row, Full the row with
        // its caption. The day's curve is the tide graph, a widget of its own (below), where a taller line once showed it.
        // Nothing is pressed (its menu refreshes it).
        new()
        {
            Kind = ListKind.Tides, Info = WidgetInfos.Tides,
            Line = ListLine.Card, Cells = 3, Range = (2, 4), Grows = false,
            Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)],
            Act = CellAct.Nothing, Interaction = Interaction.Click, Drawing = WidgetSource.Tides,
        },
        // sports, §603: the score — a team's match now, last or next, or a league's day (SportsTile, docs\widgets.md §239,
        // §369) — the same at both sizes: Compact the kit's row with its caption, the score or the next match with its
        // clock or its day at the end, the competition and the round under it; Full the scoreboard, the competition and
        // the clock over the two sides with their logos either side of the score, the round under it. Its fixtures and results,
        // its table and a cup's knockout are widgets of their own (below), where a taller line once scrolled to them. A
        // click opens the match on ESPN.
        new()
        {
            Kind = ListKind.Sports, Info = WidgetInfos.Sports,
            Line = ListLine.Card, Cells = 5, Range = (2, 8), Grows = false,
            Forms = [new(LineSize.Compact, 3), new(LineSize.Full, 5)],
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Sports,
        },
        // quakes: the earthquakes near a place of its own (QuakesLine, docs\widgets.md §240): one row,
        // the latest's dot, magnitude, distance and age; taller, how far it looks and the feed's age
        // over a row per earthquake of the past week, newest first. A row's click opens its page on
        // the USGS's site once the folder has gone (CellAct.Action, IslandView.App). As tall as its rows,
        // up to its own (§388; one row with nothing to list); four rows unmeasured, one to eight.
        new()
        {
            Kind = ListKind.Quakes, Info = WidgetInfos.Quakes,
            Line = ListLine.Card, Cells = 8, Range = (2, 16),
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Quakes,
        },
        // quick: keep awake (2026-10-20, §269), a switch's line as the others' (SwitchIsland): on, the
        // time a timed one has left under its name.
        Switch(ListKind.KeepAwake, WidgetInfos.KeepAwake),
        // camera: a live webcam's or IP camera's picture (CameraLine, docs\widgets.md §331): one row, a
        // small picture in the leading column, its name and how it is; taller, the picture fills the line,
        // its name over it. Five rows unmeasured — about 16:9 at the list's width — one to twelve. A click
        // opens the camera's page (or its address) in the browser once the folder has gone. Automatic where it
        // starts (§401): one row while it has no picture, then the picture's own shape at the list's width.
        new()
        {
            Kind = ListKind.Camera, Info = WidgetInfos.Camera,
            Line = ListLine.Card, Cells = 10, Range = (2, 24),
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Camera,
        },
        // sun: the sun's day for a place of its own (SunTile, docs\widgets.md §368), worked out on this
        // PC: the arc over the sunrise, the daylight and the sunset at both of its sizes (§603), or the day bar over
        // them, its next event alone on one row at Compact. Nothing is pressed.
        Sun(ListKind.SunArc, WidgetInfos.SunArc),
        Sun(ListKind.SunDayBar, WidgetInfos.SunDayBar),
        // darkMode: Windows' own mode (§403), a switch's line as the others' (SwitchIsland).
        Switch(ListKind.DarkMode, WidgetInfos.DarkMode),
        // sun, §404: each of its times a widget of its own, one row of the kit — its glyph, its name, its
        // note under it, its time at the end —, Automatic where it starts, as tall as that row; one or two.
        Time(ListKind.Sunrise, WidgetInfos.Sunrise),
        Time(ListKind.Sunset, WidgetInfos.Sunset),
        Time(ListKind.Daylight, WidgetInfos.Daylight),
        Time(ListKind.SolarNoon, WidgetInfos.SolarNoon),
        Time(ListKind.Dawn, WidgetInfos.Dawn),
        Time(ListKind.Dusk, WidgetInfos.Dusk),
        // weather, §590: the hourly forecast's List — the next five hours across, each its time, its glyph and its
        // temperature (WeatherHoursListTile): Compact the glyph beside the temperature, Full the glyph large over it.
        new()
        {
            Kind = ListKind.WeatherHoursList, Info = WidgetInfos.WeatherHoursList,
            Line = ListLine.Card, Cells = 5, Range = (2, 8), Grows = false,
            Forms = [new(LineSize.Compact, 3), new(LineSize.Full, 5)],
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.WeatherHoursList,
        },
        // weather, §590: the daily forecast's Graph — the seven days a column each, its name and glyph over the week's
        // highs and lows on two curves, each written at its point, its chance of rain under them (WeatherDaysGraphTile):
        // the curves' band shorter at Compact.
        new()
        {
            Kind = ListKind.WeatherDaysGraph, Info = WidgetInfos.WeatherDaysGraph,
            Line = ListLine.Card, Cells = 9, Range = (2, 12), Grows = false,
            Forms = [new(LineSize.Compact, 7), new(LineSize.Full, 9)],
            Act = CellAct.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.WeatherDaysGraph,
        },
        // air, §590: the place's pollutants (PM2.5, PM10, ozone, nitrogen dioxide) in two columns at Compact, a row each
        // at Full; its pollen, the six plants in three columns at Compact, a row each at Full; its day, a bar an hour in
        // its level's colour, short at Compact, tall at Full. Each the air quality line's piece it was (AirQualityTile).
        Air(ListKind.AirPollutants, WidgetInfos.AirPollutants, compact: 5, full: 8),
        Air(ListKind.AirPollen, WidgetInfos.AirPollen, compact: 5, full: 12),
        Air(ListKind.AirForecast, WidgetInfos.AirForecast, compact: 4, full: 6),
        // screentime, §603: screen time history — the fortnight's days ending at the day shown, a bar a day split by app
        // with its legend under them, the day shown marked, a day lit under the pointer and shown by a click, a piece of a
        // bar saying its app — always bars, never an area (the person, 2026-11-09: "it should always show the bar chart").
        // The same at both sizes, the bars' band shorter at Compact.
        new()
        {
            Kind = ListKind.ScreenTimeHistory, Info = WidgetInfos.ScreenTimeHistory,
            Line = ListLine.Card, Cells = 7, Range = (2, 12), Grows = false,
            Forms = [new(LineSize.Compact, 4), new(LineSize.Full, 7)],
            Act = CellAct.Nothing, Interaction = Interaction.Click,
            Drawing = WidgetSource.ScreenTime,
            Follows = [Channels.Day, Channels.App, Channels.AppHover], Publishes = [Channels.Day, Channels.DayHover, Channels.App, Channels.AppHover],
        },
        // tides, §603: the tide graph — the curve from three hours ago to a day on, its area filled, now marked, each high's
        // and low's time by it (TidesTile) — the same at both sizes, its band shorter at Compact.
        new()
        {
            Kind = ListKind.TideGraph, Info = WidgetInfos.TideGraph,
            Line = ListLine.Card, Cells = 6, Range = (2, 8), Grows = false,
            Forms = [new(LineSize.Compact, 4), new(LineSize.Full, 6)],
            Act = CellAct.Nothing, Interaction = Interaction.Click, Drawing = WidgetSource.Tides,
        },
        // sports, §603: what the score's line scrolled to, each a list of its own (SportsTile): its fixtures and results —
        // a team's matches to come and played, a league's round by its days —, its table, every group, and a cup's
        // knockout, round by round. Lists: as tall as what they have, up to their most, the rest scrolling (§371, §479).
        SportsList(ListKind.SportsFixtures, WidgetInfos.SportsFixtures),
        SportsList(ListKind.SportsTable, WidgetInfos.SportsTable),
        SportsList(ListKind.SportsKnockout, WidgetInfos.SportsKnockout),
        // §641: Start's parts (StartMenu.cs; Karpo.Dock\StartWidgets). The pinned apps: "Pinned" over a grid of tiles, an icon over its name, as many across as the line is wide. Compact
        // one line of tiles, Full three, Start's own; more scroll in it. A tile's click opens its app once the folder has gone,
        // a drag moves it among the others (Drag), its right click pins, unpins, moves to the front.
        new()
        {
            Kind = ListKind.StartPins, Info = WidgetInfos.StartPins,
            Line = ListLine.Card, Cells = 16, Range = (2, FolderOptions.MostCells), Grows = false,
            Forms = [new(LineSize.Compact, 7), new(LineSize.Full, 16)],
            Act = CellAct.Action, Interaction = Interaction.Click | Interaction.Drag, Drawing = WidgetSource.StartApps,
        },
        // Every app: "All apps" and its view over the apps in categories, a grid or a list A to Z, scrolling in the line; as
        // tall as the room it is given.
        new()
        {
            Kind = ListKind.StartApps, Info = WidgetInfos.StartApps,
            Line = ListLine.Card, Cells = 24, Range = (2, FolderOptions.MostCells),
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.StartApps,
        },
        // Recommended: the files opened lately and the apps added lately, two across, a row each its icon, its name and
        // when. Compact a line of two, Full three lines.
        new()
        {
            Kind = ListKind.StartRecommended, Info = WidgetInfos.StartRecommended,
            Line = ListLine.Card, Cells = 10, Range = (2, FolderOptions.MostCells), Grows = false,
            Forms = [new(LineSize.Compact, 5), new(LineSize.Full, 10)],
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.StartRecommended,
        },
        // The account and power: one form, Start's foot — the picture and the name (its menu), the folders, the power button
        // (its menu).
        new()
        {
            Kind = ListKind.StartProfile, Info = WidgetInfos.StartProfile,
            Line = ListLine.Card, Cells = 3, Range = (2, 4), Grows = false, Forms = [new(LineSize.Full, 3)],
            Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.StartProfile,
        },
        // §642: Windows search: a search box and, under it, the home or what it found, the rest scrolling under the box. The
        // box takes a press and a drag; a click opens once the folder has gone. §652 (the person, 2026-11-09: "the search
        // widget should have a fixed height"): Fixed only (Grows false), twelve rows, 480 DIP — a panel's, as Windows' search
        // stands at one height whatever it finds: what it finds scrolls inside it, a short list leaves room under it.
        new()
        {
            Kind = ListKind.WinSearch, Info = WidgetInfos.WinSearch,
            Line = ListLine.Card, Cells = FolderOptions.MostCells, Range = (2, FolderOptions.MostCells), Grows = false,
            Act = CellAct.Action, Interaction = Interaction.Click | Interaction.Drag, Drawing = WidgetSource.WinSearch,
        },
    ];

    /// <summary>sports, §603: one of a team's or a league's lists, its own kind: a list as tall as what it has.</summary>
    private static ListWidget SportsList(ListKind kind, WidgetInfo info) => new()
    {
        Kind = kind, Info = info,
        Line = ListLine.Card, Cells = 8, Range = (2, 24),
        Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Sports,
    };

    /// <summary>air, §590: a piece of the air quality of a place, its own kind, in two sizes of the same content.</summary>
    private static ListWidget Air(ListKind kind, WidgetInfo info, int compact, int full) => new()
    {
        Kind = kind, Info = info,
        Line = ListLine.Card, Cells = full, Range = (2, 16), Grows = false,
        Forms = [new(LineSize.Compact, compact), new(LineSize.Full, full)],
        Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.AirQuality,
    };

    /// <summary>
    /// sun: one of the sun widget's two styles, each its own form (§368): the arc with its times, the same at both sizes,
    /// its picture shorter at Compact (§603); the day bar its next event on a row, or the bar with the times and the place.
    /// </summary>
    private static ListWidget Sun(ListKind kind, WidgetInfo info) => new()
    {
        Kind = kind, Info = info,
        Line = ListLine.Card, Cells = 6, Range = (2, kind == ListKind.SunArc ? 6 : 16),
        Forms = kind == ListKind.SunDayBar ? [new(LineSize.Compact, 2), new(LineSize.Full, 6)] : [new(LineSize.Compact, 4), new(LineSize.Full, 6)],
        Act = CellAct.Nothing, Interaction = Interaction.Click, Drawing = WidgetSource.Sun,
    };

    /// <summary>
    /// sun: one of the sun's times, a row (§404), drawn by the sun's line: its row (Compact) or with its note under it
    /// (Full, §432); dawn and dusk, which have no note, the row alone.
    /// </summary>
    private static ListWidget Time(ListKind kind, WidgetInfo info) => new()
    {
        Kind = kind, Info = info,
        Line = ListLine.Card, Cells = kind is ListKind.Dawn or ListKind.Dusk ? 2 : 3, Range = (2, 4), Grows = true,
        Forms = kind is ListKind.Dawn or ListKind.Dusk ? [new(LineSize.Full, 2)] : [new(LineSize.Compact, 2), new(LineSize.Full, 3)],
        Act = CellAct.Nothing, Interaction = Interaction.Click, Drawing = WidgetSource.Sun,
    };

    /// <summary>
    /// A switch's line, its click switching it: its row (Compact, where it starts), or its name over what a
    /// click does (Full), as many rows as that takes (§432).
    /// </summary>
    private static ListWidget Switch(ListKind kind, WidgetInfo info) => new()
    {
        Kind = kind, Info = info,
        Line = ListLine.Card, Cells = 2, Range = (2, 12), Grows = false,
        Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 3)], Starts = LineSize.Compact,
        Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
    };

    /// <summary>A reading's details' line: the graphs' card, its first fact on a row (Compact) or as tall as its facts (Full, §432).</summary>
    private static ListWidget Details(ListKind kind, WidgetInfo info) => new()
    {
        Kind = kind, Info = info,
        Line = ListLine.Card, Cells = 6, Range = (2, 24),
        Forms = [new(LineSize.Compact, 2), new(LineSize.Full, 6)],
        Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Performance,
    };

    /// <summary>A power button's line: the list's own row, one line tall, whose click acts.</summary>
    private static ListWidget PowerButton(ListKind kind, WidgetInfo info) => new()
    {
        Kind = kind, Info = info,
        Line = ListLine.Row, Cells = 2, Range = (2, 2),
        Act = CellAct.Action, Interaction = Interaction.Click, Drawing = WidgetSource.PowerButton,
    };

    /// <summary>The widget for <paramref name="kind"/>, or null for a value outside the enum.</summary>
    public static ListWidget? Find(ListKind kind) =>
        (uint)kind < (uint)All.Length ? All[(int)kind] : null;

    /// <summary>The widget for <paramref name="kind"/>; throws for a value outside the enum.</summary>
    public static ListWidget Of(ListKind kind) =>
        Find(kind) ?? throw new ArgumentOutOfRangeException(
            nameof(kind), kind, $"{kind} is not a list widget (Karpo.Core\\ListWidgets.cs).");

    /// <summary>The widget whose id is <paramref name="id"/>, or null where no list widget has it.</summary>
    public static ListWidget? Find(string? id)
    {
        foreach (var widget in All)
        {
            if (string.Equals(widget.Info.Id, id, StringComparison.Ordinal))
            {
                return widget;
            }
        }
        return null;
    }

    /// <summary>The word config.json stores for <paramref name="kind"/>.</summary>
    public static string Id(this ListKind kind) => Of(kind).Info.Id;
}
