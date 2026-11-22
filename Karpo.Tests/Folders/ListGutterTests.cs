// Every line of a list folder on one side gutter (docs\widgets.md §9, "One gutter").
//
// A list line is drawn by whatever draws its widget — the list's own row of an icon and a name, a
// list form, a panel's region re-housed — and each of those used to keep the gutter of the place it
// came from: the weather's header the panel's 16, a day of the week its row's 8, a stock's day the
// width of its prices. Stacked in one folder they did not line up, and the person saw it at once.
// The rule now is the list's own inset (FolderFlyout.RowInset): every line's content starts that
// far from the line's leading edge and ends that far from its trailing edge, and a graph's band
// runs between the two.
//
// A line whose picture is a graph — the five readings, the day's temperatures, a stock's day — is
// two things across: its words keep to the gutter like every other line's, and its graph runs the
// line's whole width, edge to edge, with nothing behind it but its own fill (§572: the weather's dark
// hours no longer shaded). The test holds both, by ink: every row of pixels a line
// inks stays between the gutters unless it is the graph's, and the graph's reach the line's edges.
//
// Every kind a list holds is here, in five folders, and **everything they draw is fixed**: the
// clock is frozen, and each line draws from a stand-in rather than from this PC — the saved
// forecast, a sample battery and Security, sound devices and apps with no pictures of their own, a
// calendar and a stock of made-up answers (the stock's price already kept, so nothing is fetched on
// the pool and waited for), a track that is not playing anywhere, drives with no letter (so no
// shell is asked for a drive's picture), a pin drawn with a glyph, fed graphs, made-up
// notifications, and the shells, which draw their glyph and their title and nothing else. A line
// that read the machine drew differently on another one, or on this one in the afternoon: the
// screen-time graph did, from the tracker's live day, until screen time left lists (2026-09-26).
//
// The rule is held at several times of day, the clock frozen at each, because what the lines show
// moves with it — the time, the weather's mark and its hours, the agenda, the age of a reading —
// and the gutter may not. Needs Direct2D, so it runs only with KARPO_UI_TESTS=1; the sheets go to
// KARPO_PNG_DIR. Nothing is shown on the person's desktop.
//
// It runs in the shared samplers' collection, which runs alone: the graphs are fed through the
// samplers every graph widget shares, and the drives are stood in for through the one static every
// drive widget reads (DriveWatch.Source), so nothing else may be running while either is set.

using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Dock.Weather;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

[Collection(SamplerState.Collection)]
public sealed unsafe class ListGutterTests(ITestOutputHelper output)
{
    /// <summary>Every kind a list holds, in the folders the sheets show them in.</summary>
    internal static readonly (string Name, ListKind[] Kinds)[] Groups =
    [
        ("Weather, a stock and a feed", [ListKind.WeatherNow, ListKind.Markets, ListKind.Rss]),
        // sports: a team's line, its scores not come in a test (§239).
        ("A team's scores", [ListKind.Sports, ListKind.SportsFixtures, ListKind.SportsTable, ListKind.SportsKnockout]),
        ("Graphs and the machine", [ListKind.Cpu, ListKind.Gpu, ListKind.Memory, ListKind.Disk, ListKind.Network, ListKind.Battery, ListKind.Security]),
        ("The readings' details", [ListKind.CpuDetails, ListKind.GpuDetails, ListKind.MemoryDetails, ListKind.DiskDetails, ListKind.NetworkDetails]),
        // perf: the top processes, on the stand-in's apps (ProcessStand).
        ("The top processes", [ListKind.CpuProcesses]),
        ("Time, media and drives", [ListKind.Clock, ListKind.Timer, ListKind.Focus, ListKind.Stopwatch, ListKind.Calendar, ListKind.Media, ListKind.Drive, ListKind.NetworkDrive, ListKind.RemovableDevices, ListKind.FolderContents]),
        ("Sound", [ListKind.SoundOutput, ListKind.SoundInput, ListKind.SoundMixer, ListKind.SoundVolume, ListKind.Brightness]),
        ("Rows and notifications", [ListKind.Pin, ListKind.WifiSwitch, ListKind.BluetoothSwitch, ListKind.SoundSwitch, ListKind.MicrophoneSwitch, ListKind.GameMode, ListKind.ColorFilters, ListKind.MonoAudio, ListKind.Magnifier, ListKind.Narrator, ListKind.StickyKeys, ListKind.KeepAwake, ListKind.DarkMode, ListKind.AirplaneMode, ListKind.MobileHotspot, ListKind.Vpn, ListKind.EnergySaver, ListKind.NightLight, ListKind.RotationLock, ListKind.SpatialSound, ListKind.LiveCaptions, ListKind.NearbyShare, ListKind.Accessibility, ListKind.Cast, ListKind.Project, ListKind.DoNotDisturb, ListKind.TaskView, ListKind.SnippingTool, ListKind.WindowsSettings, ListKind.NotificationsList, ListKind.Mail]),
        ("Screen time", [ListKind.ScreenTime, ListKind.ScreenTimeHistory, ListKind.ScreenTimeApps, ListKind.ScreenTimeChart]),
        // A heading: its words on the gutter;
        // and a divider, where somebody split the list: it draws nothing at all.
        ("A heading and a divider", [ListKind.Heading, ListKind.Divider]),
        // A flyout made of widgets: the clock's header, the month, a row of toggles and a footer (§68).
        ("A flyout made of widgets", [ListKind.TimeHeader, ListKind.Month, ListKind.Toggles, ListKind.Actions]),
        // The weather's and the calendar's flyouts as lines (§80): the hours and the week, the
        // date's header, the day's events and the day, week and year views.
        ("The weather's and the calendar's flyouts", [ListKind.WeatherHours, ListKind.WeatherHoursList, ListKind.WeatherDays, ListKind.WeatherDaysGraph, ListKind.DateHeader, ListKind.Events, ListKind.Meeting, ListKind.DayView, ListKind.WeekView, ListKind.YearView, ListKind.AirQuality, ListKind.AirPollutants, ListKind.AirPollen, ListKind.AirForecast, ListKind.Tides, ListKind.TideGraph, ListKind.Quakes, ListKind.Camera]),
        // sun: its two styles and its times, a widget each (§404), Zürich's day (§368).
        ("The sun", [ListKind.SunArc, ListKind.SunDayBar, ListKind.Sunrise, ListKind.Sunset, ListKind.Daylight, ListKind.SolarNoon, ListKind.Dawn, ListKind.Dusk]),
        // notes: a to-do list and a note, as a new line shows them in an empty notebook (§87).
        ("A to-do list and a note", [ListKind.Todo, ListKind.Note]),
        // The network's, Bluetooth's and the mobile hotspot's quick-settings pages as lines (§88, §93), on the samples.
        ("The network's, Bluetooth's and the hotspot's pages", [ListKind.Wifi, ListKind.Bluetooth, ListKind.Hotspot]),
        // Cast's panel, Win+P's modes and nearby sharing as lines (§114, §115), on the samples.
        ("Cast's, Win+P's and nearby sharing's pages", [ListKind.WirelessDisplays, ListKind.Projection, ListKind.NearbySharing]),
        // The battery and power flyouts' rows as lines (§94), on the samples.
        ("The power mode", [ListKind.PowerMode]),
        ("The keyboard layouts", [ListKind.Keyboard]),
        ("The VPN connections", [ListKind.VpnConnections]),
        ("The taskbar layouts", [ListKind.Layouts]),
        // power: a button each, the list's own row (2026-10-11).
        ("The power buttons", [ListKind.Sleep, ListKind.Hibernate, ListKind.ShutDown, ListKind.Restart, ListKind.SignOut, ListKind.Lock]),
        // The clipboard history's flyout as a line (§101), on an empty stand-in history.
        ("The clipboard", [ListKind.Clipboard]),
        // The contacts' flyout as a line (§102), on the isolated run's account set, which has none.
        ("The contacts", [ListKind.Contacts]),
        // An alarm (§106), off on its own book.
        ("An alarm", [ListKind.Alarm]),
        // The apps playing to pick from (2026-10-11), with none on an isolated run.
        ("The apps playing", [ListKind.MediaSources]),
        // The desktop switcher's flyout as a line (§116): no desktops on an isolated run, its foot alone.
        ("The desktops", [ListKind.Desktops]),
        // §641's Start (beta) on Start's samples (StartSamples), and §642's Windows search on its own (WinSearchSamples): its home.
        ("Start (beta) and Windows search", [ListKind.StartPins, ListKind.StartApps, ListKind.StartRecommended, ListKind.StartProfile, ListKind.WinSearch]),
    ];

    /// <summary>The two display scales the sheets are drawn at: 100 %, and the 150 % the person's own bar is at.</summary>
    private static readonly float[] Scales = [1f, 1.5f];

    /// <summary>
    /// The rows a line whose content does not vary is given at the largest text: twenty, the most any
    /// such kind had before kinds lost their own (Nearby sharing's whole page at 225 % text; §192).
    /// </summary>
    private const int LargeTextRows = 20;

    /// <summary>How a line's content meets one of its edges.</summary>
    private enum Meets
    {
        /// <summary>
        /// Its words, or a plate or a meter that is part of them: the first letter, a right-aligned
        /// figure, a notification's card, a capacity bar's end. On the gutter, to the pixel.
        /// </summary>
        Words,

        /// <summary>
        /// The list's icon column, which starts on the gutter: a glyph centred in it by its ink, or a
        /// picture filling it. Its ink is somewhere in the column's first half.
        /// </summary>
        Icon,

        /// <summary>A button's box standing on the gutter, its glyph in the middle of it.</summary>
        Box,

        /// <summary>Nothing anchored there: the words end where they end, inside the gutter.</summary>
        Free,
    }

    /// <summary>
    /// What each kind's line is held to: how its content meets its leading and its trailing edge,
    /// and whether it is a graph that reaches the line's own left or right edge. A stock at its own
    /// height is the stock panel (docs\widgets.md §98), whose graph stands on the gutter with its
    /// times, as the weather's hours do.
    /// </summary>
    private static readonly Dictionary<ListKind, (Meets Lead, Meets End, bool BleedsLeft, bool BleedsRight)> Held = new()
    {
        // §607: the hero over the place, nothing standing on the trailing gutter (the age follows the place).
        [ListKind.WeatherNow] = (Meets.Words, Meets.Free, false, false),
        // stocks: the glyph in the icon column, the figures on the trailing gutter, the chart under
        // them from the left edge at every height (2026-10-11), as a reading's graph — a day's ending
        // where the session stands, short of the right edge while the market trades.
        [ListKind.Markets] = (Meets.Icon, Meets.Words, true, false),
        // rss: the news glyph in the icon column, the newest headline where the names begin, its age
        // ending where it ends.
        [ListKind.Rss] = (Meets.Icon, Meets.Free, false, false),
        // sports: the dot, the logo or the glyph in the icon column; the clock or the day ends inside the gutter (§239).
        [ListKind.Sports] = (Meets.Icon, Meets.Free, false, false),
        // §603: its lists, each match's logo in the icon column, a table's place before its logo.
        [ListKind.SportsFixtures] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.SportsTable] = (Meets.Free, Meets.Free, false, false),
        [ListKind.SportsKnockout] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.Cpu] = (Meets.Icon, Meets.Words, true, true),
        [ListKind.Gpu] = (Meets.Icon, Meets.Words, true, true),
        [ListKind.Memory] = (Meets.Icon, Meets.Words, true, true),
        [ListKind.Disk] = (Meets.Icon, Meets.Words, true, true),
        [ListKind.Network] = (Meets.Icon, Meets.Words, true, true),
        // perf: the details' facts begin where the graph's name does over them, the icon column left empty.
        [ListKind.CpuDetails] = (Meets.Free, Meets.Free, false, false),
        [ListKind.GpuDetails] = (Meets.Free, Meets.Free, false, false),
        [ListKind.MemoryDetails] = (Meets.Free, Meets.Free, false, false),
        [ListKind.DiskDetails] = (Meets.Free, Meets.Free, false, false),
        [ListKind.NetworkDetails] = (Meets.Free, Meets.Free, false, false),
        // perf: an app's icon in the icon column, its share ending on the trailing gutter.
        [ListKind.CpuProcesses] = (Meets.Icon, Meets.Words, false, false),
        [ListKind.Battery] = (Meets.Icon, Meets.Words, false, false),
        [ListKind.Security] = (Meets.Icon, Meets.Free, false, false),
        // The clock's glyph in the icon column, the time where the names begin (ClockTile.Line).
        [ListKind.Clock] = (Meets.Icon, Meets.Free, false, false),
        // A shell stands on the gutter like every line: its glyph in the icon column, its title
        // where the names begin.
        [ListKind.Timer] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.Focus] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.Stopwatch] = (Meets.Icon, Meets.Free, false, false),
        // The agenda's header: the calendar's glyph in the icon column (§401).
        [ListKind.Calendar] = (Meets.Icon, Meets.Free, false, false),
        // What's playing: its words where every name begins, its leading column empty; its art on the trailing gutter (§401).
        [ListKind.Media] = (Meets.Free, Meets.Box, false, false),
        // media: an app's icon in the icon column, its state ending inside its row's plate.
        [ListKind.MediaSources] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.Drive] = (Meets.Icon, Meets.Words, false, false),
        [ListKind.NetworkDrive] = (Meets.Icon, Meets.Words, false, false),
        // A device's picture in the icon column, and its eject button standing on the trailing gutter.
        [ListKind.RemovableDevices] = (Meets.Icon, Meets.Box, false, false),
        // §380: a folder's contents, none chosen: the folder's glyph in the icon column, what it asks for on the trailing gutter.
        [ListKind.FolderContents] = (Meets.Icon, Meets.Words, false, false),
        [ListKind.SoundOutput] = (Meets.Words, Meets.Free, false, false),
        [ListKind.SoundInput] = (Meets.Words, Meets.Box, false, false),
        [ListKind.SoundMixer] = (Meets.Words, Meets.Free, false, false),
        // quick: the volume's ">" on the trailing gutter, its button round it; no switch, so no empty column after it.
        [ListKind.SoundVolume] = (Meets.Box, Meets.Free, false, false),
        // quick: the volume's line, the sun on its button (SliderLine).
        [ListKind.Brightness] = (Meets.Box, Meets.Box, false, false),
        [ListKind.Pin] = (Meets.Icon, Meets.Free, false, false),
        // Task view's row, the list's own, as a pin's and the switches' are.
        [ListKind.TaskView] = (Meets.Icon, Meets.Free, false, false),
        // The shortcuts to Windows' apps: the list's own row, as Task view's.
        [ListKind.SnippingTool] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.WindowsSettings] = (Meets.Icon, Meets.Free, false, false),
        // power: a button is the list's own row, as Task view's.
        [ListKind.Sleep] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.Hibernate] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.ShutDown] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.Restart] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.SignOut] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.Lock] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.WifiSwitch] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.BluetoothSwitch] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.SoundSwitch] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.MicrophoneSwitch] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.GameMode] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.ColorFilters] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.MonoAudio] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.Magnifier] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.Narrator] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.StickyKeys] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.KeepAwake] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.DarkMode] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.AirplaneMode] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.MobileHotspot] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.Vpn] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.RotationLock] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.SpatialSound] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.EnergySaver] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.NightLight] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.LiveCaptions] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.NearbyShare] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.Accessibility] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.Cast] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.Project] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.DoNotDisturb] = (Meets.Icon, Meets.Free, false, false),
        // Its own list: the bell in the icon column at every height, the kit's Header (§437) taller,
        // "Clear all" ending on the gutter.
        [ListKind.NotificationsList] = (Meets.Icon, Meets.Words, false, false),
        // pim: the envelope in the icon column; the sender and the time end where they end.
        [ListKind.Mail] = (Meets.Icon, Meets.Free, false, false),
        // screentime: the glyph in the icon column; with nothing counted (no tracker in a test) its
        // steps are greyed and there is no graph to reach the edges, so the day and "0 min" end
        // where they end. With no apps, the quiet glyph and "No apps".
        [ListKind.ScreenTime] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.ScreenTimeApps] = (Meets.Icon, Meets.Free, false, false),
        // The ring stands on the gutter; its legend's times end on the other.
        [ListKind.ScreenTimeChart] = (Meets.Free, Meets.Free, false, false),
        // §603: the history's bars stand between the gutters; with nothing counted, none.
        [ListKind.ScreenTimeHistory] = (Meets.Free, Meets.Free, false, false),
        // list: a heading's words begin on the gutter, as a group header's begin on its cards' edge.
        [ListKind.Heading] = (Meets.Words, Meets.Free, false, false),
        // list: a divider is a gap between two cards of the list (2026-10-09, "NO line any more"):
        // it draws nothing, so it has neither a gutter to keep nor an edge to reach.
        [ListKind.Divider] = (Meets.Free, Meets.Free, false, false),
        // The clock flyout's header: the time in tabular figures, whose first figure's ink stands in
        // by its own bearing, which differs from one figure to the next; the date ends where it ends.
        [ListKind.TimeHeader] = (Meets.Free, Meets.Free, false, false),
        // The month's title on the gutter, its arrows' boxes standing on the other.
        [ListKind.Month] = (Meets.Words, Meets.Box, false, false),
        // The weather flyout's graph is drawn by the flyout's rule (its ends pulled in half a label),
        // its week's day names on the gutter; the calendar's header, the day's heading and its
        // events' bars on the gutter, and each view's title.
        [ListKind.WeatherHours] = (Meets.Free, Meets.Free, false, false),
        // The week's days on the kit's rows (§401): each day's glyph in the icon column.
        [ListKind.WeatherDays] = (Meets.Icon, Meets.Free, false, false),
        // §590: the next hours' columns and the week's graph, each column centred in its share between the gutters.
        [ListKind.WeatherHoursList] = (Meets.Free, Meets.Free, false, false),
        [ListKind.WeatherDaysGraph] = (Meets.Free, Meets.Free, false, false),
        [ListKind.DateHeader] = (Meets.Words, Meets.Free, false, false),
        [ListKind.Events] = (Meets.Free, Meets.Free, false, false),
        // meeting: its rows as the day's events', a Join's box at the trailing gutter where one is online (§205).
        [ListKind.Meeting] = (Meets.Free, Meets.Free, false, false),
        // air: the level's dot in the icon column; the age, a figure or the bands end inside the gutter (§210).
        [ListKind.AirQuality] = (Meets.Icon, Meets.Free, false, false),
        // §590: the pollutants' and the plants' names where the kit's names begin, their figures on the trailing gutter;
        // the day's bars from gutter to gutter.
        [ListKind.AirPollutants] = (Meets.Free, Meets.Words, false, false),
        [ListKind.AirPollen] = (Meets.Free, Meets.Words, false, false),
        [ListKind.AirForecast] = (Meets.Free, Meets.Free, false, false),
        // tides: the drop in the icon column, the height now inside the gutter; the tide graph's curve edge to edge (§236, §603).
        [ListKind.Tides] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.TideGraph] = (Meets.Free, Meets.Free, true, true),
        // sun: the arc's horizon and the day bar's track from gutter to gutter, the place's name on the
        // leading one (§368); the arc's sunset under the curve's foot, short of the trailing gutter now no row is
        // under it; a time's glyph in the icon column, its time on the trailing gutter (§404).
        [ListKind.SunArc] = (Meets.Words, Meets.Free, false, false),
        [ListKind.SunDayBar] = (Meets.Words, Meets.Words, false, false),
        [ListKind.Sunrise] = (Meets.Icon, Meets.Words, false, false),
        [ListKind.Sunset] = (Meets.Icon, Meets.Words, false, false),
        [ListKind.Daylight] = (Meets.Icon, Meets.Words, false, false),
        [ListKind.SolarNoon] = (Meets.Icon, Meets.Words, false, false),
        [ListKind.Dawn] = (Meets.Icon, Meets.Words, false, false),
        [ListKind.Dusk] = (Meets.Icon, Meets.Words, false, false),
        // quakes: a magnitude's dot in the icon column, the age and the rows' words end inside the gutter (§240).
        [ListKind.Quakes] = (Meets.Icon, Meets.Free, false, false),
        // camera: unset, its set-up prompt; set, its picture between the gutters or its thumbnail in the icon column (§331).
        [ListKind.Camera] = (Meets.Icon, Meets.Free, false, false),
        [ListKind.DayView] = (Meets.Words, Meets.Free, false, false),
        [ListKind.WeekView] = (Meets.Words, Meets.Free, false, false),
        [ListKind.YearView] = (Meets.Words, Meets.Free, false, false),
        // The toggles' boxes stand on the gutter; a row that is not full ends where it ends.
        [ListKind.Toggles] = (Meets.Box, Meets.Free, false, false),
        // A footer: its buttons' boxes on the gutters, their words inside them (2026-10-11); a new one
        // has no button, so it says so on the gutter and ends where its words do. Its ground is
        // Windows' footer band, edge to edge (2026-10-16), as a graph's is.
        [ListKind.Actions] = (Meets.Box, Meets.Free, true, true),
        // notes: the list's or the note's name on the gutter, the rest ending where it ends.
        [ListKind.Todo] = (Meets.Words, Meets.Free, false, false),
        [ListKind.Note] = (Meets.Words, Meets.Free, false, false),
        // quick: a page's title on the gutter; its switch's pill ends inside its box on the other.
        [ListKind.Wifi] = (Meets.Words, Meets.Free, false, false),
        // Its title starts on the gutter too, but the B's own bearing puts its ink 2 px in at 225 % text.
        [ListKind.Bluetooth] = (Meets.Free, Meets.Free, false, false),
        [ListKind.Hotspot] = (Meets.Words, Meets.Free, false, false),
        [ListKind.WirelessDisplays] = (Meets.Words, Meets.Free, false, false),
        [ListKind.Projection] = (Meets.Words, Meets.Free, false, false),
        [ListKind.NearbySharing] = (Meets.Words, Meets.Free, false, false),
        // The power mode's heading on the gutter, and the marked mode's plate with it.
        [ListKind.PowerMode] = (Meets.Words, Meets.Free, false, false),
        [ListKind.Keyboard] = (Meets.Icon, Meets.Free, false, false),
        // With none, the kit's Status: its glyph in the icon column (§401).
        [ListKind.VpnConnections] = (Meets.Icon, Meets.Free, false, false),
        // A layout's glyph in the icon column, the one shown on its plate.
        [ListKind.Layouts] = (Meets.Icon, Meets.Free, false, false),
        // The clipboard's glyph in the icon column, "Clear all" ending inside its box on the other.
        [ListKind.Clipboard] = (Meets.Icon, Meets.Free, false, false),
        // pim: the search box stands on both gutters, as a key's field does.
        [ListKind.Contacts] = (Meets.Box, Meets.Box, false, false),
        // alarm: its glyph in the icon column, its switch's pill ending inside its box on the other.
        [ListKind.Alarm] = (Meets.Icon, Meets.Free, false, false),
        // desktops: each row's gripper, and "New desktop"'s plus, in the icon column; the apps ending inside the gutter.
        [ListKind.Desktops] = (Meets.Icon, Meets.Free, false, false),
        // §641, §661: each heading on the gutter — Pinned's and Recommended's first letters keep 2 px of their own at the
        // largest text, held to nothing —; the tiles and rows edge to edge, their words between the gutters.
        [ListKind.StartPins] = (Meets.Free, Meets.Free, false, false),
        [ListKind.StartApps] = (Meets.Words, Meets.Free, false, false),
        [ListKind.StartRecommended] = (Meets.Free, Meets.Free, false, false),
        // §641, §661: the account's picture on the gutter, and the power button's box at the trailing one.
        [ListKind.StartProfile] = (Meets.Icon, Meets.Box, false, false),
        // §642, §655: the search box from gutter to gutter, to the pixel; its home's headings and rows under it.
        [ListKind.WinSearch] = (Meets.Words, Meets.Words, false, false),
    };

    /// <summary>
    /// The kinds that list the quick settings' rows (ListRow), whose chosen row's plate and pill stand
    /// 4 DIP from the line's edges, a row's pad outside the gutter, as Windows' quick settings mark one (§388).
    /// </summary>
    private static readonly HashSet<ListKind> RowPlates =
    [
        ListKind.SoundOutput, ListKind.SoundInput, ListKind.Wifi, ListKind.Bluetooth, ListKind.Hotspot, ListKind.WirelessDisplays,
        ListKind.Projection, ListKind.NearbySharing, ListKind.PowerMode, ListKind.Keyboard, ListKind.VpnConnections,
    ];

    private const string SystemVolume =@"\\?\Volume{11111111-1111-1111-1111-111111111111}\";
    private const string ShareVolume = @"\\?\Volume{44444444-4444-4444-4444-444444444444}\";
    private const string StickVolume = @"\\?\Volume{22222222-2222-2222-2222-222222222222}\";

    private static ulong Gb(double gigabytes) => (ulong)(gigabytes * 1024 * 1024 * 1024);

    /// <summary>
    /// The folders cover every kind a list holds, each once, and every kind is held to something:
    /// a kind added to the list's registry is a kind added here.
    /// </summary>
    [Fact]
    public void EveryListKindIsPictured()
    {
        Assert.Equal(
            ListWidgets.All.Select(widget => widget.Kind).Order(),
            Groups.SelectMany(group => group.Kinds).Order());
        Assert.Equal(
            ListWidgets.All.Select(widget => widget.Kind).Order(),
            Held.Keys.Order());
    }

    /// <summary>
    /// Every kind a list holds, stacked in list folders at 100 % and 150 %, dark and light: four
    /// sheets of the five folders side by side, drawn by the folder's own painters, at the saved
    /// forecast's own time.
    /// </summary>
    [UiFact]
    public void EveryLineIsPictured() =>
        Drawn(SavedWeather.Fixed, (gfx, scale, dark, made) =>
        {
            var look = LookFor(dark);
            Sheet(gfx, $"list-gutter-{scale * 100:0}-{(dark ? "dark" : "light")}.png", scale,
                [.. made.Select(entry => ($"{entry.Name}: {scale * 100:0} %, {(dark ? "dark" : "light")}", look, entry.Folder, entry.Plan))]);
        });

    /// <summary>
    /// Every line stands on the list's one gutter, at 100 % and 150 % and at the largest text, with
    /// the clock frozen at <paramref name="hour"/>:<paramref name="minute"/> on the saved forecast's
    /// day — just after midnight, in the morning, at the forecast's own time and late at night:
    /// <list type="bullet">
    /// <item>Nothing a line inks at rest reaches into a gutter but a graph, and a graph reaches the
    /// line's own edges (<see cref="Held"/>): a row of pixels that crosses a gutter is the graph's.</item>
    /// <item>Where words lead or end a line, their ink is on the gutter to the pixel; where the
    /// list's icon column leads it, the icon is in the column the gutter starts; where a button's
    /// box stands on the gutter, its glyph is in the box's first half.</item>
    /// </list>
    /// </summary>
    [UiTheory]
    [InlineData(0, 5)]
    [InlineData(8, 40)]
    [InlineData(14, 20)]
    [InlineData(22, 55)]
    public void EveryLineStandsOnTheGutter(int hour, int minute)
    {
        var failures = new List<string>();
        Drawn(SavedWeather.Fixed.Date.AddHours(hour).AddMinutes(minute), (gfx, scale, dark, made) =>
        {
            if (!dark)
            {
                return;
            }
            foreach (var textScale in (float[])[1f, 2.25f])
            {
                if (textScale > 1 && scale > 1)
                {
                    continue;
                }
                var look = LookFor(dark, textScale);
                foreach (var (_, folder, _) in made)
                {
                    // At the largest text a line whose content does not vary is given ample rows
                    // (LargeTextRows), as a person reading at that size sets it (docs\widgets.md §138):
                    // its fixed rows at 100 % text draw it shortened, which is not what this holds.
                    var heights = folder.Tiles.Select(tile => tile.Cell.Island.Height).ToList();
                    foreach (var tile in folder.Tiles)
                    {
                        if (textScale > 1 && tile.Cell.Island.List is { } fixedKind && !ListWidgets.Of(fixedKind).Grows && fixedKind.HasHeight())
                        {
                            tile.Cell.Island.Height = LineHeight.Exactly(LargeTextRows * LineHeight.Row);
                        }
                    }
                    var (_, plan) = ListFormPictures.Plan(folder, look, scale);
                    for (var index = 0; index < folder.Tiles.Count; index++)
                    {
                        folder.Tiles[index].Cell.Island.Height = heights[index];
                    }
                    var gutter = FolderFlyout.RowInset(plan.TextPx);
                    var icon = FolderFlyout.RowIcon(plan.TextPx);
                    // The first half of a button's box at its largest: a glyph in the middle of it.
                    var box = PopupPaint.Round(20 * scale * textScale);
                    // A right-aligned figure ends where its last letter's advance does, and a letter
                    // keeps a little air of its own inside that: up to a tenth of the largest figure
                    // a line ends with (a graph's reading, 20 DIP).
                    var bearing = MathF.Ceiling(0.1f * 20 * scale * textScale);
                    for (var index = 0; index < folder.Tiles.Count; index++)
                    {
                        var kind = folder.Tiles[index].Cell.Island.List!.Value;
                        var (lead, end, bleedsLeft, bleedsRight) = Held[kind];
                        var width = plan.Cards[index].right - plan.Cards[index].left;
                        // A divider is read at any alpha, faint ink included: it must draw none.
                        var least = kind == ListKind.Divider ? (byte)0 : InkAlpha;
                        var rows = RowsOf(gfx, folder, plan, index, look, scale, least);
                        // The words read without the graphs a line draws behind them (Graphs\GraphBackdrop.cs),
                        // whose rows run under the words and are the graph's, not the words'.
                        List<(float Left, float Right)> bare;
                        PaintWatch.NoBackdrops = true;
                        try
                        {
                            bare = RowsOf(gfx, folder, plan, index, look, scale, least);
                        }
                        finally
                        {
                            PaintWatch.NoBackdrops = false;
                        }
                        var at = $"{kind} at {scale * 100:0} % scale, {textScale * 100:0} % text, {hour:00}:{minute:00}";

                        // What crosses a gutter is the graph's, and only a graph crosses one — but for a list's
                        // chosen row, whose plate and pill stand from 4 DIP in, its words on the gutter (§388).
                        var plate = RowPlates.Contains(kind) ? PopupPaint.Round(Karpo.Dock.QuickSettings.Controls.ListRow.PadDip * scale) : 0;
                        var crossing = rows.Where(row => row.Left < gutter - plate - 1 || row.Right > width - gutter + plate + 1).ToList();
                        var words = bare.Where(row => row.Left >= gutter - 1 && row.Right <= width - gutter + 1).ToList();
                        var graph = bleedsLeft || bleedsRight;
                        if (!graph && crossing.Count > 0)
                        {
                            failures.Add($"{at}: ink in the gutter, {crossing.Min(row => row.Left):0}..{crossing.Max(row => row.Right):0} of a line {width:0} wide with a gutter of {gutter:0}");
                        }
                        if (bleedsLeft && !crossing.Any(row => row.Left <= 1))
                        {
                            failures.Add($"{at}: its graph does not reach the line's left edge");
                        }
                        if (bleedsRight && !crossing.Any(row => row.Right >= width - 1))
                        {
                            failures.Add($"{at}: its graph does not reach the line's right edge");
                        }
                        if (kind == ListKind.Divider)
                        {
                            // A gap: no ink at all.
                            if (rows.Count > 0)
                            {
                                failures.Add($"{at}: a divider draws {rows.Count} rows; it is a gap and draws nothing");
                            }
                            continue;
                        }
                        if (words.Count == 0)
                        {
                            failures.Add($"{at}: no words drawn inside the gutters");
                            continue;
                        }
                        var left = words.Min(row => row.Left);
                        var right = words.Max(row => row.Right);
                        output.WriteLine($"{at}: gutter {gutter:0} of {width:0}, words {left:0}..{right:0} (lead {left - gutter:+0;-0;0}, end {width - gutter - right:+0;-0;0}){(graph ? $", graph {(crossing.Count > 0 ? $"{crossing.Min(row => row.Left):0}..{crossing.Max(row => row.Right):0}" : "none")}" : "")}");
                        // A line not set up yet (a heading with no words, the mail with no account)
                        // is its "Set up in Settings", centred in the line (SetupFace) and held there
                        // by SetupLinkTests: inside the gutters, as above, but standing on neither.
                        if (folder.Tiles[index].Island is CellIsland { Unset: not null })
                        {
                            continue;
                        }
                        // A page's title leads where its chosen row's pill stood on the gutter before (§388): its first
                        // letter keeps its own air, as a right-aligned figure's last one does.
                        Check(failures, at, "leads", lead, left - gutter, icon, box, RowPlates.Contains(kind) ? bearing : 0);
                        Check(failures, at, "ends", end, width - gutter - right, icon, box, bearing);
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// How far in from the gutter a line's ink stands at one edge (<paramref name="inside"/>, px),
    /// held to how its content meets that edge: words to the pixel, give or take the
    /// <paramref name="bearing"/> a letter keeps inside its own advance.
    /// </summary>
    private static void Check(List<string> failures, string at, string edge, Meets meets, float inside, float icon, float box, float bearing)
    {
        var (least, most) = meets switch
        {
            Meets.Words => (-1f, 1f + bearing),
            // Centred by its ink in a column the gutter starts, or filling it.
            Meets.Icon => (-1f, icon / 2),
            // In the middle of a box whose edge is the gutter: its first half at the most.
            Meets.Box => (-1f, box),
            _ => (-1f, float.MaxValue),
        };
        if (inside < least || inside > most)
        {
            failures.Add($"{at}: {edge} {inside:0} px from the gutter, where its {meets.ToString().ToLowerInvariant()} should be {least:0}..{(most == float.MaxValue ? "anywhere inside" : $"{most:0}")}");
        }
    }

    /// <summary>
    /// Line <paramref name="index"/> drawn alone into a box of the height it was given, as the open
    /// folder draws it — a list's own row by the list's row painter, a line that is the widget's own
    /// drawing by the widget — and read back a row of pixels at a time: where each inked row starts
    /// and ends across it, in its own px: a pixel whose alpha is over <paramref name="least"/>.
    /// </summary>
    private static List<(float Left, float Right)> RowsOf(Gfx gfx, FolderIsland folder, FolderPlan plan, int index, Look look, float scale, byte least = InkAlpha)
    {
        var card = plan.Cards[index];
        var width = card.right - card.left;
        var height = card.bottom - card.top;
        var theme = FlyoutTheme.For(look);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var tile = folder.Tiles[index];
        var box = PopupPaint.Rect(0, 0, width, height);
        var rows = new List<(float, float)>();
        Bitmap(width, height,
            dc =>
            {
                if (tile.Cell.Island.List?.CardInList() != true)
                {
                    FolderFlyout.PaintListLine(dc, gfx, tile.Island, tile.Cell.Island, box, plan, theme, scale);
                }
                else
                {
                    FolderFlyout.PaintContent(dc, tile.Island, box, style);
                }
            },
            bitmap => rows = InkedRows(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height), least));
        return rows;
    }

    /// <summary>
    /// The alpha a pixel has to be over to be ink: the same threshold as the other ink measurements
    /// (<see cref="InkOf"/>), so a faint fill — a night's shading, a hover plate — is not ink and a
    /// letter is.
    /// </summary>
    private const byte InkAlpha = 24;

    /// <summary>
    /// Every row of pixels that holds ink, as where it starts and ends: a pixel whose alpha is over
    /// <paramref name="least"/>.
    /// </summary>
    private static List<(float Left, float Right)> InkedRows(IWICBitmap* bitmap, uint width, uint height, byte least)
    {
        IWICBitmapLock* locked = null;
        try
        {
            var all = new WICRect { X = 0, Y = 0, Width = (int)width, Height = (int)height };
            ComUtil.Check(bitmap->Lock(&all, (uint)WICBitmapLockFlags.WICBitmapLockRead, &locked));
            uint stride = 0;
            ComUtil.Check(locked->GetStride(&stride));
            uint size = 0;
            byte* data = null;
            ComUtil.Check(locked->GetDataPointer(&size, &data));
            var rows = new List<(float, float)>();
            for (var y = 0u; y < height; y++)
            {
                float left = width;
                float right = 0;
                for (var x = 0u; x < width; x++)
                {
                    // Premultiplied BGRA: the alpha is the fourth byte.
                    if (data[(y * stride) + (x * 4) + 3] > least)
                    {
                        left = MathF.Min(left, x);
                        right = MathF.Max(right, x + 1);
                    }
                }
                if (right > left)
                {
                    rows.Add((left, right));
                }
            }
            return rows;
        }
        finally
        {
            ComUtil.Release(ref locked);
        }
    }

    /// <summary>
    /// Makes the five folders at each scale and theme, on the drawing thread with the clock frozen at
    /// <paramref name="now"/> and every stand-in set, and hands each set to <paramref name="use"/>.
    /// </summary>
    private static void Drawn(DateTime now, Action<Gfx, float, bool, List<(string Name, FolderIsland Folder, FolderPlan Plan)>> use) =>
        StoodIn<Gfx>(now, OnStaThread, gfx =>
        {
            foreach (var scale in Scales)
            {
                foreach (var dark in new[] { true, false })
                {
                    var made = new List<(string Name, FolderIsland Folder, FolderPlan Plan)>();
                    try
                    {
                        foreach (var (name, kinds) in Groups)
                        {
                            var folder = ListFormPictures.Folder(gfx, Before, [.. kinds.Select(Made)]);
                            Ready(folder);
                            var (_, plan) = ListFormPictures.Plan(folder, LookFor(dark), scale);
                            made.Add((name, folder, plan));
                        }
                        use(gfx, scale, dark, made);
                    }
                    finally
                    {
                        foreach (var (_, folder, _) in made)
                        {
                            folder.Closed();
                            folder.Dispose();
                        }
                    }
                }
            }
        });

    /// <summary>
    /// Runs <paramref name="body"/> on the thread <paramref name="on"/> gives it, with the clock frozen at
    /// <paramref name="now"/> and every stand-in a line of any kind reads set: nothing of this PC's or the
    /// network's is shown — in the sheets' folders and in the real ones every kind is clicked in (§312).
    /// </summary>
    internal static void StoodIn<T>(DateTime now, Action<Action<T>> on, Action<T> body)
    {
        var drives = DriveWatch.Source;
        // No letter, so no root: a drive's picture is the shell's, read off the dock's thread when a
        // widget first asks, and it would be this PC's picture — or none yet, depending on how soon
        // it came back. A drive with no letter draws its kind's glyph, the same everywhere.
        Drive[] attached =
        [
            new() { Letter = -1, Kind = DriveKind.Fixed, Volume = SystemVolume, Label = "Windows", Total = Gb(931), Free = Gb(419) },
            new() { Letter = -1, Kind = DriveKind.Remote, Volume = ShareVolume, Label = "Team", Total = Gb(4096), Free = Gb(1200) },
            // A stick, for the removable devices' line. It keeps its letter, so its row has an eject
            // button to hold to the gutter; its picture is a stand-in all the same (DriveIcons.Source).
            new() { Letter = 4, Kind = DriveKind.Removable, Volume = StickVolume, Label = "SANDISK", Total = Gb(28.6), Free = Gb(11.9) },
        ];
        DriveWatch.Source = (_, remote) => [.. attached.Where(drive => remote || drive.Kind != DriveKind.Remote)];
        // The stick's picture is not asked of the shell: its letter may be a real volume here.
        var icons = Karpo.Dock.Drives.DriveIcons.Source;
        using var processes = ProcessStand.Use();
        Karpo.Dock.Drives.DriveIcons.Source = (_, _) => null;
        try
        {
            on(context =>
            {
                // Per thread, and this thread ends with the test.
                Clock.Now = () => now;
                CalendarIsland.Made = ListFormCases.Store(ListFormCases.Week);
                // An event with no site of its own opens in the calendar app (§434): one stood in, not this PC's.
                CalendarApp.Stand = () => "outlookcal:";
                BatteryIsland.Probe = () => new SystemStatus.Battery(76, false, false);
                BatteryIsland.Made = () => new SampleBattery();
                Security.Made = () => new SampleSecurity();
                MediaIsland.Stand = new MediaStand();
                SoundParts.Made = () => new FixedSound();
                MarketIsland.Made = MarketOffscreenTests.Settled(new() { ["AAPL"] = MarketOffscreenTests.Answer() });
                Karpo.Dock.Rss.RssIsland.Made = Karpo.Tests.Rss.RssSamples.Settled(now);
                // §641, §655: Start's and Windows search's made-up sources, never this PC's apps, files or index.
                Karpo.Dock.StartWidgets.StartSources.Stand = new Karpo.Dock.StartWidgets.StartSamples();
                Karpo.Dock.WinSearch.WinSearchLine.Stand = static () => Karpo.Dock.WinSearch.WinSearchSamples.Sources(new Config());
                Karpo.Dock.WinSearch.WinSearchLine.HistoryStand = Karpo.Dock.WinSearch.WinSearchSamples.History();
                Karpo.Dock.WinSearch.WinSearchLine.IconStand = static (_, _, _) => null;
                body(context);
            });
        }
        finally
        {
            DriveWatch.Source = drives;
            Karpo.Dock.Drives.DriveIcons.Source = icons;
        }
    }

    /// <summary>sports: Barcelona, a team of the table (§239).</summary>
    private static SportsOptions SportsTeam => new() { Sport = "soccer", League = "esp.1", Team = "83", Name = "Barcelona", Abbreviation = "BAR" };

    /// <summary>A line of <paramref name="kind"/> with what it shows set: a place, a symbol, a drive, a pin's target.</summary>
    internal static CellItem Made(ListKind kind) => Line(kind, island =>
    {
        if (island.Weather is not null)
        {
            island.Weather = SavedWeather.Options();
        }
        switch (kind)
        {
            case ListKind.Markets:
                island.Market = new MarketOptions { Symbol = "AAPL", Range = MarketRange.Day };
                break;
            case ListKind.Rss:
                // The sample feed, kept already (RssSamples.Settled): nothing is fetched.
                island.Rss = new RssOptions { Address = Karpo.Tests.Rss.RssSamples.Address, Title = "Example News" };
                break;
            case ListKind.Sports or ListKind.SportsFixtures or ListKind.SportsTable or ListKind.SportsKnockout:
                island.Sports = SportsTeam;
                break;
            case ListKind.Drive:
                island.Drive = new DriveTargetOptions { Volume = SystemVolume, Letter = "C:", Label = "Windows" };
                break;
            case ListKind.NetworkDrive:
                island.Drive = new DriveTargetOptions { Volume = ShareVolume, Letter = "Z:", Label = "Team" };
                break;
            case ListKind.Pin:
                // A link drawn with a glyph of its own: nothing the shell or the network has to
                // answer for its icon or its name.
                island.Pin = new PinOptions { Target = "https://example.com/notes", Name = "Notes", Icon = "\uE70B" };
                break;
        }
    });

    /// <summary>
    /// What a line has to have before the folder's first look: the weather's saved forecast, which it
    /// would otherwise ask the shared store for, and the notifications, which it would otherwise
    /// read from the shared store.
    /// </summary>
    internal static void Before(FolderIsland folder)
    {
        var source = SavedWeather.Source();
        foreach (var tile in folder.Tiles)
        {
            switch (tile.Island)
            {
                case WeatherLineTile weather:
                    weather.Source = source;
                    break;
                // §590: the air's lines read the saved answer, made at the frozen clock: a fair day in Zürich, its pollen in.
                case AirQualityTile air:
                    air.Source = SavedAir.Source(SavedAir.Cache(european: 46, us: 54, pollen: "12.0"));
                    break;
                case TidesTile tides:
                    tides.Source = SavedTides.Source(SavedTides.Cache());
                    tides.Imperial = false;
                    break;
                case SunTile sun:
                    sun.Stand = (47.3769, 8.5417);
                    break;
                case NotificationPartTile list:
                    list.Shown = Notifications(Clock.Now());
                    break;
            }
        }
    }

    /// <summary>
    /// What each line needs once the folder is open: the graphs' samples. Opening a folder ticks its
    /// widgets, and a graph's tick reads this PC's counters, so they are forgotten afterwards and the
    /// graph given a history of its own — long enough to reach across the widest line drawn here.
    /// </summary>
    internal static void Ready(FolderIsland folder)
    {
        for (var index = 0; index < folder.Tiles.Count; index++)
        {
            switch (folder.Tiles[index].Island)
            {
                case PerformanceIsland graph:
                    graph.Forgotten();
                    for (var second = 0; second < 400; second++)
                    {
                        var wave = (MathF.Sin((second + (index * 17)) / 11f) + 1) / 2;
                        if (folder.Tiles[index].Cell.Island.List == ListKind.Network)
                        {
                            graph.Feed(down: 900_000 * wave, up: 120_000 * wave);
                        }
                        else
                        {
                            graph.Feed(0.15f + (0.6f * wave));
                        }
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// A PC's sound with nothing read from this one: three outputs and two microphones, the chosen
    /// ones marked, and apps with no picture of their own — each mix wears the glyph an app without
    /// one gets, where the samples' apps would have the shell look up their icons on whatever machine
    /// runs this, and hand them over when that came back.
    /// </summary>
    private sealed class FixedSound : ISoundSource
    {
        private static readonly string Mic = char.ConvertFromUtf32(0xE720);

        public void Start(Changes changes)
        {
        }

        public SoundState Read(int news) => new(
            true,
            0.45f,
            false,
            [
                new("speakers", "Speakers (Realtek(R) Audio)", WindowsSound.GlyphFor(1), true),
                new("headphones", "Headphones (WH-1000XM5)", WindowsSound.GlyphFor(3), false),
                new("display", "LU28R55 (NVIDIA High Definition Audio)", WindowsSound.GlyphFor(9), false),
            ],
            [
                new("system", "System sounds", 1f, false, true, null, null),
                new("music", "Music", 0.8f, false, false, null, null),
                new("call", "Call", 0.6f, true, false, null, null),
                new("game", "Game", 0.35f, false, false, null, null),
            ],
            [
                new("array", "Microphone Array (Realtek(R) Audio)", Mic, true),
                new("headset", "Headset (WH-1000XM5 Hands-Free)", Mic, false),
            ],
            false);

        public void SetVolume(float value)
        {
        }

        public void SetMuted(bool muted)
        {
        }

        public void SetDefault(string id)
        {
        }

        public void SetDefaultInput(string id)
        {
        }

        public void SetInputMuted(bool muted)
        {
        }

        public void SetAppVolume(string key, float value)
        {
        }

        public void SetAppMuted(string key, bool muted)
        {
        }

        public void Dispose()
        {
        }
    }

    private static List<NotificationEntry> Notifications(DateTime at)
    {
        var said = new[]
        {
            ("Mail", "Vera Holm", "Re: the roof — I've asked them for a second quote, and they say Thursday at the earliest."),
            ("Calendar", "Standup in 10 minutes", "Daily · Team room"),
            ("Messages", "Dad", "Landed! Getting a coffee then I'll find the taxi rank."),
        };
        return [.. said.Select((line, index) => new NotificationEntry(
            index + 1, line.Item1.ToLowerInvariant(), line.Item1, null, line.Item2, line.Item3, at.AddMinutes(-7 * index * index), null))];
    }
}
