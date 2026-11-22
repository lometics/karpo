// folders: a widget's own actions, for its right click in a folder (the person, 2026-09-27:
// "implement rightclick context actions for all the grid and list widgets if applicable. (for
// all widgets on right click it should not close the folder anymore.)").
//
// A right click on a line opens that widget's own menu at the pointer, over the folder,
// which stays open behind it (Dock.Folders.cs, FolderMenu). At rest the menu is the kind's own
// actions, from this file, on the bar and in a folder alike, and nothing else: "Cut", "Copy",
// "Remove" and the widget's settings (OptionMenu.cs) are edit mode's menu's alone (the person,
// 2026-10-20: "the right click context menu actions that are copy, remove, duplicate etc whatever,
// or change the appearance of a widget should only appear in edit mode"; Dock.cs, WidgetMenu; §253).
// So no act of this table changes the widget itself.
//
// **What goes in.** One table over every list line (ActsFor), keyed on ListKind — never on the
// taskbar's kinds, which are other drawings of other things (the calendar's
// entry is the date) — and one answer to what each act does (RunAct). Every row either acts there and then (a refresh,
// play or pause, a mute, a countdown's start and stop, clearing the notifications, ejecting) or
// hands Windows something to open (a Settings page, Resource Monitor, File Explorer, the calendar
// app, a pin's file). None opens a popup of Karpo's: a menu's row is held to the folder rule
// (docs\widgets.md §15) exactly as a click is.
//
// **What a short line leaves out is here.** A line that has no room for a control
// the whole widget has still does it from its menu (docs\widgets.md §9, "A line keeps what it does at
// every height"): what is playing's previous and next, and its timeline — where the track stands,
// and a step back or on through it; the output devices and the microphones, each under its name,
// the one in use ticked, for choosing another; and each app's mute, for the apps a mixer too short
// for them leaves out.
//
// **What stays out.** A row that would do what a click on the widget already does: the switches'
// on and off, the graphs' Task Manager, Security's Windows Security, the volume line's mute, and
// the settings page that nearby sharing and accessibility open on a click. Mail is read here, never
// opened in an app (Pim\MailIsland.cs). And a row that can do nothing now: it is left out, not
// greyed (§308). A kind with nothing left has the floor's row, its folder's Edit folder
// (Dock.MenuFloor.cs), and only then.
//
// **What reads from the web** (§308): where it comes from — the weather's and the air quality's
// "Open forecast" (the weather site chosen, at the place), the tides' and the earthquakes' "Open on map"
// (the map chosen), the markets', the news' and the sports' "Open website" (a share's page on the finance
// site chosen, the feed's own site, the match on ESPN; only ever an http or https address the widget
// names; LinkProviders, §322). And "Refresh", the last row of every line that reads what it shows
// again and has something to read now (IRefreshable; the person, 2026-10-31: "the refresh buttons should
// be removed and they should go into the right click context menu"; §451), when it last read under it;
// left out while a read is on its way. Never on a face: a face is the folder's. Task view is the one
// exception, at the coordinator's word (2026-09-27): its menu says what the widget is — "Task view"
// (Win+Tab) — over what else Windows does with desktops from there, "New desktop" (Win+Ctrl+D).
// The Snipping Tool is the other, at the person's word (the backlog, item 10): its click snips at
// once, and its menu is the rest of what the Snipping Tool does from there — "Open Snipping Tool",
// a snip in 3 and in 10 seconds, and "Record screen" — on the bar and in a list alike,
// each left out where the Snipping Tool installed cannot do it (WindowsButtons). Settings has none of
// its own, its click being all there is to it, so its menu at rest is the floor's.
// Windows Update is the third, at the person's word (2026-10-04): "Windows Update settings" and
// "Restart options", on the bar and in a list, the rows the status icon for a restart
// waiting has too. Never a restart from Karpo: that is Windows Update's to do.
//
// **Where it is reused.** A widget's settings page in Windows is the one its record names
// (WidgetInfo.Settings), the link the settings app shows under it, word for word; the sound
// panel's pieces take the sound widget's. A pin's menu is the bar's pin menu (AppRows) with the
// three rows Windows has for a file — Run as administrator, Open file location, Properties. The
// clock's is the bar clock's page, "Date & time settings", without its "Notifications", which is a popup
// of Karpo's. The calendar's app and the notification list's settings are the links their taskbar
// partners' own panels carry, energy saver on the battery and do not disturb on the notification
// list are those widgets' own switches, and a drive's rows are Explorer's.
//
// **On the bar.** A taskbar entry's rows, and a folder's for each face it wears in turn, are
// Dock.FaceMenus.cs's, picked through the same RunAct: the sound face's "Open volume mixer", "Sound
// settings" and "Troubleshoot sound problems", as Windows 11's volume icon has them; the network
// face's "Diagnose network problems" and "Network & internet settings"; and so on. Windows' own
// troubleshooters are the Get Help app's now, which has no documented way in for a program, so both
// troubleshooting rows open Settings' troubleshooters, where the audio and the network ones are.
// Never the bar's own menu, which opens on the bar where it is not a widget (Dock.Menus.cs, RestMenuFor).
//
// **What a pick does to the folder** (FolderAfterMenu). An act that stays in Karpo leaves it open.
// One that hands the screen to another program closes it at rest, as that program taking the focus
// would a moment later anyway, so nothing Karpo shows is left standing over what was just opened.
// §664: that is the one rule — HandsAway lists what leaves Karpo, everything else acts in place —
// and the focus closes the folder only where another program has it (ForegroundTakenFromKarpo).
// While the bar is arranged the folder stays, as it does through everything then — but for a launch
// (§491): whatever is handed to Windows through the one launcher (Launch.cs) closes every folder
// first, arranged or not, edit mode staying on.

using Karpo.Core;
using Karpo.Dock.Pim;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Dock.Weather;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>What a row of a widget's own menu does (<see cref="Dock.ActsFor(ListKind)"/>).</summary>
internal enum WidgetAct
{
    /// <summary>What is playing: paused, or played again — the card's middle button.</summary>
    PlayPause,

    /// <summary>What is playing: the track before — the card's left button.</summary>
    Previous,

    /// <summary>What is playing: the track after — the card's right button.</summary>
    Next,

    /// <summary>What is playing, a step back through the track: the timeline a short line has not got.</summary>
    SeekBack,

    /// <summary>What is playing, a step on through the track.</summary>
    SeekForward,

    /// <summary>The app that is playing, brought forward.</summary>
    OpenPlayer,

    /// <summary>
    /// What is playing: each app playing under its name and icon, the one the line follows ticked —
    /// a pick follows it, as a row of the media flyout's list did; the picker on the line's app name
    /// opens this menu. Only where two apps or more play.
    /// </summary>
    FollowSession,

    /// <summary>What is playing: shuffle on or off, ticked while on — only where the app lets it be set.</summary>
    Shuffle,

    /// <summary>What is playing: repeat on to its next mode, saying the one set and ticked while on — only where the app lets it be set.</summary>
    Repeat,

    /// <summary>A drive, or each removable device, in File Explorer.</summary>
    OpenDrive,

    /// <summary>A drive Windows can eject, or each removable device, ejected with the shell's own verb.</summary>
    Eject,

    /// <summary>A mapped network drive, disconnected as Explorer's own row does.</summary>
    Disconnect,

    /// <summary>A drive's Properties, Windows' own.</summary>
    DriveProperties,

    /// <summary>Windows' calendar app, as the calendar's own panel opens it.</summary>
    CalendarApp,

    /// <summary>The sound output muted or not, ticked while it is.</summary>
    Mute,

    /// <summary>The microphone muted or not, ticked while it is.</summary>
    MuteMicrophone,

    /// <summary>
    /// Each output device, or each microphone, under its name, the one in use ticked: a pick makes it
    /// Windows' default, as a click on its row of the list does.
    /// </summary>
    ChooseDevice,

    /// <summary>Each app playing sound muted or not, under its name, ticked while it is: its icon's click in the mixer.</summary>
    MuteApp,

    /// <summary>
    /// The widget's own settings page in Windows: the one its record names (<see cref="WidgetInfo.Settings"/>),
    /// under the words the settings app shows for it.
    /// </summary>
    WindowsSettings,

    /// <summary>Energy saver on or off, from the battery: the energy saver widget's own switch, ticked while it is on.</summary>
    EnergySaver,

    /// <summary>Do not disturb on or off, from the notification list: the do not disturb widget's own switch, ticked while it is on.</summary>
    DoNotDisturb,

    /// <summary>Resource Monitor, the graphs' second opinion beside the Task Manager their click opens.</summary>
    ResourceMonitor,

    /// <summary>Settings' Date &amp; time page: the bar clock's own row.</summary>
    DateTimeSettings,

    /// <summary>A countdown's action button: start, pause, resume or start again, as the card says.</summary>
    StartPause,

    /// <summary>A countdown's clear button: the countdown ends.</summary>
    Stop,

    /// <summary>A countdown's plus: a minute longer (five from an hour up), which a line one row tall has no room for.</summary>
    Longer,

    /// <summary>A countdown's minus: a minute shorter (five from an hour up).</summary>
    Shorter,

    /// <summary>A stopwatch's lap button: a lap ends, while it runs.</summary>
    Lap,

    /// <summary>A stopwatch's reset button: back to nought, laps and all.</summary>
    Reset,

    /// <summary>The notification list emptied, as its own "Clear all".</summary>
    ClearAll,

    /// <summary>Settings' Notifications page, which the notification list's own panel links to.</summary>
    NotificationSettings,

    /// <summary>A pin's program, elevated.</summary>
    RunAsAdministrator,

    /// <summary>A pin's file, selected in File Explorer.</summary>
    OpenFileLocation,

    /// <summary>A pin's file's Properties, Windows' own.</summary>
    FileProperties,

    /// <summary>A pin's file's full path on the clipboard, in quotes, as Explorer's "Copy as path" puts it.</summary>
    CopyAsPath,

    /// <summary>A pin's web address on the clipboard.</summary>
    CopyLink,

    /// <summary>Task view, Win+Tab, as the Task view widget's own click (Desktops.TaskView).</summary>
    TaskView,

    /// <summary>A new desktop, Win+Ctrl+D, which Windows also switches to (Desktops.NewDesktop).</summary>
    NewDesktop,

    /// <summary>Windows' volume mixer, under the words Windows' own sound icon uses: the sound face's, the volume slider's, the sound lines'.</summary>
    OpenVolumeMixer,

    /// <summary>The bar's sound icon: Settings' Sound page.</summary>
    SoundSettings,

    /// <summary>The bar's sound icon: Windows' troubleshooters, where the audio one is.</summary>
    TroubleshootSound,

    /// <summary>The bar's network icon: Windows' troubleshooters, where the network one is.</summary>
    DiagnoseNetwork,

    /// <summary>The bar's network icon: Settings' Network &amp; internet page.</summary>
    NetworkSettings,

    /// <summary>The battery face, as the bar's battery had it: Settings' Power &amp; battery page.</summary>
    PowerSettings,

    /// <summary>The hotspot face: Settings' Mobile hotspot page, as Windows' own hotspot icon offers it.</summary>
    HotspotSettings,

    /// <summary>The Snipping Tool's own window, rather than a snip.</summary>
    OpenSnippingTool,

    /// <summary>A snip in 3 seconds, as the Snipping Tool's own delay.</summary>
    SnipIn3,

    /// <summary>A snip in 10 seconds.</summary>
    SnipIn10,

    /// <summary>The Snipping Tool's recorder: a recording of an area of the screen.</summary>
    RecordScreen,

    /// <summary>The Snipping Tool's page in the Microsoft Store, while it is missing.</summary>
    GetSnippingTool,

    /// <summary>
    /// "Open website": the site the widget reads, in the browser — a feed's own site, the match on ESPN, a
    /// share's page on the finance site chosen (LinkProviders.Quote) — only an http or https address, and only where there is one.
    /// </summary>
    OpenWebsite,

    /// <summary>weather, air quality: the forecast for the widget's place on the weather site chosen (LinkProviders.Forecast), in the browser.</summary>
    OpenForecast,

    /// <summary>tides: the place on a map; earthquakes: USGS's map of them, in the browser.</summary>
    OpenMap,

    /// <summary>
    /// screentime: the app under the pointer, else the one picked, opened by the pins' one rule —
    /// its windows forward, else started — which a click on it no longer does (it picks it).
    /// </summary>
    OpenApp,

    /// <summary>
    /// §641: a Start widget's own row (StartLine.MenuRows): an app or a file opened, run as administrator, shown in File
    /// Explorer, a folder, an account or power command — each handing the screen away.
    /// </summary>
    Start,

    /// <summary>§641: a Start widget's row that changes the config — a pin, an unpin, a pin to the front, All apps' view —; the folder stays.</summary>
    StartEdit,

    /// <summary>contacts: the person right-clicked on, written to with the default mail app (mailto:).</summary>
    ContactEmail,

    /// <summary>contacts: the person right-clicked on, called with the default calling app (tel:).</summary>
    ContactCall,

    /// <summary>contacts: an address, a number or the whole person on the clipboard; the folder stays.</summary>
    ContactCopy,

    /// <summary>
    /// calendar: the meeting right-clicked on joined, in its app or the browser (docs\widgets.md §206):
    /// only over a meeting with a Join, and the way in where its block is too small for the button.
    /// </summary>
    JoinMeeting,

    /// <summary>A clock's, a timer's, focus' or a stopwatch's face: Windows' Clock app (Dock.FaceMenus.cs).</summary>
    ClockApp,

    /// <summary>A folder's content or icon face, and the floor of a line in it: edit mode on, and the folder open in it, as a click on it there opens it.</summary>
    EditFolder,

    /// <summary>The Wi-Fi face: the Wi-Fi radio off, or on, as the "Wi-Fi" toggle switches it.</summary>
    WifiRadio,

    /// <summary>The Bluetooth face: the Bluetooth radio off, or on.</summary>
    BluetoothRadio,

    /// <summary>The removable face: Settings' AutoPlay page, what Windows does with a device plugged in.</summary>
    AutoPlaySettings,

    /// <summary>The cast face: Windows' own Cast flyout (Win+K), to connect to a wireless display.</summary>
    CastConnect,

    /// <summary>The battery face: a power mode, under its name, the one in force ticked.</summary>
    PowerMode,

    /// <summary>A performance reading's face: Task Manager, as its folder's footer opens it.</summary>
    TaskManager,

    /// <summary>The clipboard face: Windows' clipboard history (Win+V).</summary>
    ClipboardHistory,

    /// <summary>The mail and contacts faces: edit mode, ⚙ › Accounts (§340).</summary>
    ManageAccounts,

    /// <summary>The layouts face: edit mode, this bar's Display ▾, where the layouts are managed (§340, §361).</summary>
    LayoutSettings,

    /// <summary>The Settings button: Settings' Windows Update page.</summary>
    WindowsUpdate,

    /// <summary>The drive face: Settings' Storage page.</summary>
    StorageSettings,

    /// <summary>The network drive face: File Explorer, where drives are mapped and disconnected.</summary>
    FileExplorer,

    /// <summary>
    /// folder contents: the folder shown — a subfolder gone into, that one — in File Explorer, which no click on
    /// the line opens (§380, 2026-10-30: "the folder opens in file explorer when i click somewhere inside of it").
    /// </summary>
    OpenInExplorer,

    /// <summary>A to-do line: its list's done tasks gone, as its own "Clear completed".</summary>
    ClearCompleted,

    /// <summary>
    /// The top processes: the app right-clicked on ended, every process of it, after Windows' own
    /// question (Dock.EndProcesses); left out where Karpo could end none of them.
    /// </summary>
    EndTask,

    /// <summary>
    /// keep awake (§269): kept awake from now for a length, each under its name, the one running
    /// ticked — until turned off, half an hour, an hour, two or four (<see cref="KeepAwakeLengths"/>).
    /// </summary>
    AwakeFor,

    /// <summary>
    /// A line that reads what it shows (<see cref="IRefreshable"/>): read again now, however fresh — its menu's
    /// last row, when it last read under it; left out while a read is on its way or with nothing to read (§451).
    /// </summary>
    Refresh,
}

/// <summary>
/// A row of a widget's own menu as it was shown: its command, what it does, and what it does it
/// to — a drive's root or a pin's file — so a pick acts on what the row said even when the widget
/// has changed since. <see cref="Works"/> is the widget it works where the menu is of several (a
/// folder of several faces, each its own widget: Dock.FaceMenus.cs); null for the menu's one.
/// </summary>
internal sealed record WidgetActRow(uint Id, WidgetAct Act, string? On = null)
{
    public IIsland? Works { get; init; }
}

/// <summary>
/// Hands Windows a target: opened as it would be from File Explorer (<paramref name="verb"/>
/// null), or with one of <see cref="MenuVerbs"/>. The dock's hands it to the shell; a test's
/// writes it down.
/// </summary>
internal delegate void Opener(string target, string? verb);

/// <summary>What a widget's menu asks of Windows besides opening something (<see cref="Opener"/>).</summary>
internal static class MenuVerbs
{
    /// <summary>The shell's own verb for a drive, which Explorer's "Eject" runs.</summary>
    public const string Eject = "eject";

    /// <summary>A mapped drive's connection ended (DriveWatch.Disconnect).</summary>
    public const string Disconnect = "disconnect";

    /// <summary>Windows' Properties for a file, a folder or a drive (Launch.Properties).</summary>
    public const string Properties = "properties";

    /// <summary>The shell's elevation.</summary>
    public const string RunAs = "runas";

    /// <summary>The target's words on the clipboard, opened nowhere: a pin's "Copy as path" and "Copy link".</summary>
    public const string Copy = "copy";

    /// <summary>File Explorer at the folder holding the target, with it selected (Launch.Select).</summary>
    public const string Select = "select";

    /// <summary>File Explorer at the target, a folder, in a window of its own (Launch.Explorer).</summary>
    public const string Explorer = "explorer";
}

public sealed unsafe partial class Dock
{
    /// <summary>
    /// The first command of a widget's own acts; the rows count up from it. Above the layouts'
    /// hundred (<see cref="CMD_LAYOUT"/>) and below the Start button's quick links (QuickLinks.FirstId).
    /// </summary>
    internal const uint CMD_WIDGET_ACT = 5700;

    /// <summary>The most removable devices a menu names, each under its name.</summary>
    private const int MostDevices = 8;

    /// <summary>The most sound devices or apps a menu names, each under its name.</summary>
    private const int MostSounds = 12;

    /// <summary>How far a step through a track goes, back or on.</summary>
    internal static readonly TimeSpan SeekStep = TimeSpan.FromSeconds(10);

    internal const string ResourceMonitorTarget = "resmon.exe";
    internal const string CalendarAppTarget = "outlookcal:";
    internal const string VolumeMixerPage = "ms-settings:apps-volume";
    internal const string TroubleshootPage = "ms-settings:troubleshoot";
    internal const string ClockAppTarget = "ms-clock:";
    internal const string CastTarget = "ms-settings-connectabledevices:devicediscovery";
    internal const string TaskManagerTarget = "taskmgr.exe";
    internal const string FileExplorerTarget = "explorer.exe";

    /// <summary>earthquakes: USGS's map of the quakes it lists, the feed the widget reads (Quakes.cs).</summary>
    internal const string QuakeMap = "https://earthquake.usgs.gov/earthquakes/map/";

    // Segoe Fluent Icons.
    private const string GlyphPlay = "\uE768";
    private const string GlyphPause = "\uE769";
    private const string GlyphPrevious = "\uE892";
    private const string GlyphNext = "\uE893";
    private const string GlyphBack = "\uEB9E";
    private const string GlyphForward = "\uEB9D";
    private const string GlyphOpenApp = "\uE8A7";
    private const string GlyphFolderOpen = "\uE838";
    private const string GlyphUsb = "\uE88E";
    private const string GlyphDisconnect = "\uE8CD";
    private const string GlyphProperties = "\uE946";
    private const string GlyphCalendar = "\uE787";
    private const string GlyphMute = "\uE74F";
    private const string GlyphMicOff = "\uEC54";
    private const string GlyphMicrophone = "\uE720";
    private const string GlyphVolume = "\uE767";
    private const string GlyphMonitor = "\uE9D9";
    private const string GlyphClear = "\uE894";
    private const string GlyphAdministrator = "\uE7EF";
    private const string GlyphLeaf = "\uEC0A";
    private const string GlyphNewDesktop = "\uE710";
    private const string GlyphRepair = "\uE90F";
    private const string GlyphStopwatch = "\uE916";
    private const string GlyphRecord = "\uF7EE";
    private const string GlyphStore = "\uE719";
    private const string GlyphExplorer = "\uEC50";
    private const string GlyphGlobe = "\uE774";
    private const string GlyphNote = "\uEC4F";
    private const string GlyphWifi = "\uE701";
    private const string GlyphBluetooth = "\uE702";
    private const string GlyphAccount = "\uE77B";
    private const string GlyphUpdate = "\uE895";
    private const string GlyphDrive = "\uEDA2";
    private const string GlyphMap = "\uE707";
    private const string GlyphRefresh = "\uE72C";

    /// <summary>
    /// What a list folder's line of <paramref name="kind"/> offers on its right click at rest, in the
    /// order it shows them. Empty for a kind whose click is all there is to it, whose menu at rest is
    /// the floor's (Dock.MenuFloor.cs). A pin's are
    /// the rows its file adds to the bar's pin menu (<see cref="AppRows"/>).
    /// </summary>
    internal static IReadOnlyList<WidgetAct> ActsFor(ListKind kind) => kind switch
    {
        // What reads from the web: where it comes from, opened (§308); a line's as its face's
        // (Dock.FaceMenus.cs). Reading it again is every such line's last row (FolderMenuRows, §451).
        ListKind.WeatherNow or ListKind.WeatherHours or ListKind.WeatherHoursList or ListKind.WeatherDays or ListKind.WeatherDaysGraph
            or ListKind.AirQuality or ListKind.AirPollutants or ListKind.AirPollen or ListKind.AirForecast => ForecastActs,
        ListKind.Tides or ListKind.TideGraph or ListKind.Quakes => MapActs,
        ListKind.Markets or ListKind.Rss or ListKind.Sports or ListKind.SportsFixtures or ListKind.SportsTable or ListKind.SportsKnockout or ListKind.Camera => WebsiteActs,
        ListKind.Mail => [WidgetAct.ManageAccounts],
        // §641: a Start widget's own rows, for what is under the pointer or the keys, or the button clicked (StartLine.MenuRows).
        ListKind.StartPins or ListKind.StartApps or ListKind.StartRecommended or ListKind.StartProfile => [WidgetAct.Start],
        // contacts: the person under the pointer — write, call, copy — then the accounts.
        ListKind.Contacts => [.. ContactsActs, WidgetAct.ManageAccounts],
        ListKind.Cpu or ListKind.Gpu or ListKind.Memory or ListKind.Disk or ListKind.Network => [WidgetAct.ResourceMonitor],
        // perf: the details are the same reading's, so the same row.
        ListKind.CpuDetails or ListKind.GpuDetails or ListKind.MemoryDetails or ListKind.DiskDetails or ListKind.NetworkDetails => [WidgetAct.ResourceMonitor],
        // perf: the app right-clicked on, under its name — ended, or its file shown — then the graphs' row.
        ListKind.CpuProcesses => [WidgetAct.EndTask, WidgetAct.OpenFileLocation, WidgetAct.ResourceMonitor],
        // A pin's are its target's, on the bar as in a folder (PinRows).
        ListKind.Pin => [],
        ListKind.Media => MediaActs,
        ListKind.Drive => [WidgetAct.OpenDrive, WidgetAct.Eject, WidgetAct.DriveProperties],
        ListKind.NetworkDrive => [WidgetAct.OpenDrive, WidgetAct.Disconnect, WidgetAct.DriveProperties],
        ListKind.RemovableDevices => [WidgetAct.OpenDrive, WidgetAct.Eject, WidgetAct.DriveProperties],
        // §380: a folder's contents open their items with a click, and the folder shown in File Explorer from here alone.
        ListKind.FolderContents => [WidgetAct.OpenInExplorer],
        // calendar: "Join meeting" first, over a meeting with a Join (§206).
        ListKind.Calendar or ListKind.Events or ListKind.DayView or ListKind.WeekView or ListKind.Meeting => [WidgetAct.JoinMeeting, WidgetAct.CalendarApp],
        ListKind.YearView => [WidgetAct.CalendarApp],
        // A short line shows the device in use, or one app, alone: the others are here.
        ListKind.SoundOutput => [WidgetAct.ChooseDevice, WidgetAct.Mute, WidgetAct.OpenVolumeMixer, WidgetAct.WindowsSettings],
        ListKind.SoundVolume => [WidgetAct.Mute, WidgetAct.OpenVolumeMixer, WidgetAct.WindowsSettings],
        ListKind.SoundInput => [WidgetAct.ChooseDevice, WidgetAct.MuteMicrophone, WidgetAct.WindowsSettings],
        ListKind.SoundMixer => [WidgetAct.MuteApp, WidgetAct.OpenVolumeMixer, WidgetAct.WindowsSettings],
        ListKind.Brightness => [WidgetAct.WindowsSettings],
        ListKind.Battery => [WidgetAct.EnergySaver, WidgetAct.WindowsSettings],
        ListKind.AirplaneMode or ListKind.MobileHotspot or ListKind.Vpn or ListKind.EnergySaver or ListKind.NightLight or ListKind.SpatialSound
            or ListKind.RotationLock or ListKind.LiveCaptions or ListKind.DoNotDisturb or ListKind.NearbyShare
            or ListKind.WifiSwitch or ListKind.BluetoothSwitch or ListKind.SoundSwitch or ListKind.MicrophoneSwitch
            or ListKind.GameMode or ListKind.ColorFilters or ListKind.MonoAudio or ListKind.DarkMode
            or ListKind.Magnifier or ListKind.Narrator or ListKind.StickyKeys
            or ListKind.Cast or ListKind.Project => [WidgetAct.WindowsSettings],
        // keep awake (§269): the lengths and the screen, which a click has one of, then its page.
        ListKind.KeepAwake => AwakeActs,
        ListKind.Clock => [WidgetAct.DateTimeSettings],
        ListKind.Timer or ListKind.Focus => [WidgetAct.StartPause, WidgetAct.Stop, WidgetAct.Longer, WidgetAct.Shorter],
        // A narrow line folds its lap and reset buttons in here; they are here at every width.
        ListKind.Stopwatch => [WidgetAct.StartPause, WidgetAct.Lap, WidgetAct.Reset],
        ListKind.NotificationsList => [WidgetAct.ClearAll, WidgetAct.DoNotDisturb, WidgetAct.NotificationSettings],
        ListKind.TaskView => TaskViewActs,
        // desktops: what Windows does with desktops beside the line's own rows — Task view, and a new
        // desktop gone to at once, as Win+Ctrl+D does (the line's own "New desktop" stays where you are).
        ListKind.Desktops => TaskViewActs,
        ListKind.SnippingTool => SnippingActs,
        // The Settings button's own, on the bar and in a list.
        ListKind.WindowsSettings => [WidgetAct.WindowsUpdate],
        // A heading is a title and a divider a rule: there is nothing to do with either but name or
        // style it, which is edit mode's (§253), and arrange it, which the floor's Edit folder leads to
        // (Dock.MenuFloor.cs).
        ListKind.Heading or ListKind.Divider => [],
        // The clock flyout's header offers the clock's page; a month the calendar's app and the date's
        // page; a toggles row and a footer are their own controls, and their settings are the rest.
        ListKind.TimeHeader or ListKind.DateHeader => [WidgetAct.DateTimeSettings],
        ListKind.Month => [WidgetAct.CalendarApp, WidgetAct.DateTimeSettings],
        ListKind.Toggles or ListKind.Actions => [],
        ListKind.Todo => [WidgetAct.ClearCompleted],
        // The clipboard face's rows, and the line's own "Clear all".
        ListKind.Clipboard => [WidgetAct.ClearAll, WidgetAct.ClipboardHistory, WidgetAct.WindowsSettings],
        ListKind.Alarm => [WidgetAct.ClockApp],
        // The apps playing: what is playing's controls, which the list itself has not got.
        ListKind.MediaSources => [WidgetAct.PlayPause, WidgetAct.Previous, WidgetAct.Next, WidgetAct.OpenPlayer],
        ListKind.Layouts => [WidgetAct.LayoutSettings],
        // The app right-clicked on, opened, as a click on it did before it picked it (2026-10-10).
        ListKind.ScreenTime or ListKind.ScreenTimeHistory or ListKind.ScreenTimeApps or ListKind.ScreenTimeChart => [WidgetAct.OpenApp],
        // The network button's own rows, as Windows' network icon has them, where its page is now.
        ListKind.Wifi => [WidgetAct.DiagnoseNetwork, WidgetAct.NetworkSettings],
        ListKind.Bluetooth or ListKind.Hotspot or ListKind.PowerMode => [WidgetAct.WindowsSettings],
        ListKind.WirelessDisplays or ListKind.Projection or ListKind.NearbySharing or ListKind.Keyboard or ListKind.VpnConnections => [WidgetAct.WindowsSettings],
        // power: a button is its click; the ones Windows keeps a page for offer it (Power & battery).
        ListKind.Sleep or ListKind.Hibernate or ListKind.ShutDown or ListKind.Restart => [WidgetAct.WindowsSettings],
        ListKind.SignOut or ListKind.Lock => [],
        _ => [],
    };

    // A taskbar entry's own rows, and a folder's for each of its faces: Dock.FaceMenus.cs.

    /// <summary>What <paramref name="widget"/> offers where it stands: a line's acts in a list, none elsewhere.</summary>
    internal static IReadOnlyList<WidgetAct> ActsFor(WidgetItem? widget) => widget switch
    {
        CellItem { List: { } line } => ActsFor(line),
        _ => [],
    };

    // The apps to pick from first, where the picker opens this menu; shuffle and repeat, which a
    // short line has no room for; the timeline's steps too: only a line of four rows or more has the
    // timeline itself.
    private static readonly WidgetAct[] MediaActs =
    [
        WidgetAct.FollowSession, WidgetAct.PlayPause, WidgetAct.Previous, WidgetAct.Next, WidgetAct.Shuffle, WidgetAct.Repeat,
        WidgetAct.SeekBack, WidgetAct.SeekForward, WidgetAct.OpenPlayer,
    ];

    // desktops: the one order every desktop menu has — New desktop, Task view, Rename, Delete (§308;
    // DesktopRows has the last two).
    private static readonly WidgetAct[] TaskViewActs = [WidgetAct.NewDesktop, WidgetAct.TaskView];

    // The way to get it first: while it is missing, it is the one row that does anything.
    private static readonly WidgetAct[] SnippingActs = [WidgetAct.GetSnippingTool, WidgetAct.OpenSnippingTool, WidgetAct.SnipIn3, WidgetAct.SnipIn10, WidgetAct.RecordScreen];

    // What reads from the web (§308): where it comes from.
    internal static readonly WidgetAct[] ForecastActs = [WidgetAct.OpenForecast];

    internal static readonly WidgetAct[] MapActs = [WidgetAct.OpenMap];

    internal static readonly WidgetAct[] WebsiteActs = [WidgetAct.OpenWebsite];

    // keep awake: what its click does not choose — another length — and Power & battery, where
    // Windows' own timeouts are (§269).
    internal static readonly WidgetAct[] AwakeActs = [WidgetAct.AwakeFor, WidgetAct.WindowsSettings];

    private static readonly WidgetAct[] ContactsActs = [WidgetAct.ContactEmail, WidgetAct.ContactCall, WidgetAct.ContactCopy];

    /// <summary>
    /// Where Windows keeps the settings behind <paramref name="info"/>: the page its record names,
    /// which the settings app links to. The sound panel's pieces have none of their own and take the
    /// volume slider's, Sound, the page the panel they are pieces of linked to. Null for a widget
    /// Windows keeps none for.
    /// </summary>
    internal static WindowsLink? WindowsLinkOf(WidgetInfo? info) =>
        info?.Settings
        ?? (info == WidgetInfos.SoundOutput || info == WidgetInfos.SoundInput || info == WidgetInfos.SoundMixer || info == WidgetInfos.SoundVolume
            ? WidgetInfos.Volume.Settings
            : null);

    /// <summary>
    /// The rows <paramref name="acts"/> make for <paramref name="widget"/> as it is now, numbered
    /// from <see cref="CMD_WIDGET_ACT"/>, with what each acts on. An act makes a row where it can
    /// mean something now: a drive's rows only while it is there, its eject only for a drive
    /// Windows can eject and its disconnect only for a mapped one, each device plugged in under its
    /// own name, a pin's file rows only for a file this PC has, the widget's page in Windows only
    /// where it has one (<paramref name="page"/>), a site only where the widget names one. A switch
    /// says what a pick does ("Turn energy saver on"). A control the widget's own would do nothing with
    /// now is left out, not greyed (the person, 2026-10-23: "hide what can't apply instead of
    /// greying"; §308): nothing is playing, the countdown is not running, the list is empty, there is
    /// no microphone to mute, this PC has no energy saver. Only a choice this PC has nothing for — the
    /// power modes, the output devices — stays greyed under its heading, saying so. A heading left over
    /// nothing goes.
    /// <paramref name="first"/> rows came before these in the same menu (another face's), so these
    /// are numbered after them. <paramref name="face"/> is the face whose rows these are, where
    /// <paramref name="widget"/> is a folder answering for several (Dock.FaceMenus.cs): its place, its share.
    /// </summary>
    internal static (List<FlyoutItem> Items, List<WidgetActRow> Rows) ActRows(IReadOnlyList<WidgetAct> acts, IIsland? widget, WindowsLink? page = null, int first = 0, FolderFace? face = null)
    {
        var items = new List<FlyoutItem>();
        var rows = new List<WidgetActRow>();
        var devices = false;
        void Row(WidgetAct act, string text, string glyph, string? on = null, bool enabled = true, bool? ticked = null) =>
            Pictured(act, text, Glyph(glyph), on, enabled, ticked);
        void Pictured(WidgetAct act, string text, FlyoutIcon icon, string? on, bool enabled, bool? ticked)
        {
            if (!enabled && !SaysNone(act))
            {
                return;
            }
            var id = CMD_WIDGET_ACT + (uint)(first + rows.Count);
            rows.Add(new WidgetActRow(id, act, on));
            // menus: a switch or a pick stays, the menu with it (MenuKeeps.cs, §219).
            items.Add(new FlyoutItem(id, text, icon, Enabled: enabled, Checked: ticked, Stays: MenuKeeps.Stays(act)));
        }
        foreach (var act in acts)
        {
            switch (act)
            {
                case WidgetAct.SeekBack or WidgetAct.SeekForward:
                {
                    var timeline = (widget as MediaIsland)?.Timeline;
                    if (act == WidgetAct.SeekBack && timeline is { Said: { Length: > 0 } where })
                    {
                        // Where the track stands, which a line shorter than the whole widget does not say.
                        items.Add(new FlyoutItem(0, where, Header: true));
                    }
                    Row(
                        act,
                        act == WidgetAct.SeekBack ? S.DockWidgetMenuSeekBack((int)SeekStep.TotalSeconds) : S.DockWidgetMenuSeekForward((int)SeekStep.TotalSeconds),
                        act == WidgetAct.SeekBack ? GlyphBack : GlyphForward,
                        enabled: timeline?.CanSeek ?? false);
                    break;
                }
                case WidgetAct.ChooseDevice:
                {
                    if (widget is not SoundPartTile sound || sound.MenuDevices is not { } choices)
                    {
                        break;
                    }
                    items.Add(new FlyoutItem(0, SoundPartTile.HeadingOf(sound.Region), Header: true));
                    if (choices.Count == 0)
                    {
                        // None to choose: the heading stays, over a greyed row saying so (§247).
                        Row(act, SoundPartTile.NoneOf(sound.Region), sound.Region == SoundPart.Input ? GlyphMicrophone : GlyphVolume, enabled: false);
                    }
                    foreach (var (id, name, inUse) in choices.Take(MostSounds))
                    {
                        Row(act, name, sound.Region == SoundPart.Input ? GlyphMicrophone : GlyphVolume, id, ticked: inUse);
                    }
                    items.Add(new FlyoutItem(0, "", Separator: true));
                    break;
                }
                case WidgetAct.MuteApp:
                {
                    if (widget is not SoundPartTile sound || sound.MenuApps is not { Count: > 0 } apps)
                    {
                        break;
                    }
                    foreach (var (key, name, muted) in apps.Take(MostSounds))
                    {
                        Row(act, S.DockWidgetMenuMuteApp(name), GlyphMute, key, ticked: muted);
                    }
                    items.Add(new FlyoutItem(0, "", Separator: true));
                    break;
                }
                case WidgetAct.FollowSession:
                {
                    // The apps playing, under a heading, as the media flyout listed them to pick from.
                    if (widget is not MediaIsland { PlayingIn: { Count: > 1 } apps } media)
                    {
                        break;
                    }
                    items.Add(new FlyoutItem(0, S.WidgetMediaPlayingIn, Header: true));
                    var follows = media.Follows;
                    foreach (var app in apps.Take(MostSounds))
                    {
                        // Each by its own icon where it has one, as the flyout's list showed them.
                        Pictured(act, app.App.Length > 0 ? app.App : app.Key, app.Icon is { } icon ? new FlyoutIcon.Picture(icon) : Glyph(GlyphNote), app.Key,
                            enabled: true, ticked: string.Equals(app.Key, follows, StringComparison.OrdinalIgnoreCase));
                    }
                    items.Add(new FlyoutItem(0, "", Separator: true));
                    break;
                }
                case WidgetAct.Shuffle or WidgetAct.Repeat:
                {
                    var modes = (widget as MediaIsland)?.Modes ?? default;
                    if (act == WidgetAct.Shuffle && modes.CanShuffle)
                    {
                        Row(act, S.WidgetMediaShuffle, MediaIsland.ShuffleGlyph, ticked: modes.Shuffle);
                    }
                    else if (act == WidgetAct.Repeat && modes.CanRepeat)
                    {
                        Row(act, MediaModes.Say(modes.Repeat), modes.Repeat == MediaRepeat.Track ? MediaIsland.RepeatOneGlyph : MediaIsland.RepeatAllGlyph,
                            ticked: modes.Repeat != MediaRepeat.None);
                    }
                    break;
                }
                case WidgetAct.PlayPause or WidgetAct.Previous or WidgetAct.Next or WidgetAct.OpenPlayer:
                {
                    var media = (widget as MediaIsland)?.Controls ?? default;
                    switch (act)
                    {
                        case WidgetAct.PlayPause:
                            Row(act, media.Playing ? S.DockWidgetMenuPause : S.DockWidgetMenuPlay, media.Playing ? GlyphPause : GlyphPlay, enabled: media.CanToggle);
                            break;
                        case WidgetAct.Previous:
                            Row(act, S.DockWidgetMenuPreviousTrack, GlyphPrevious, enabled: media.CanPrevious);
                            break;
                        case WidgetAct.Next:
                            Row(act, S.DockWidgetMenuNextTrack, GlyphNext, enabled: media.CanNext);
                            break;
                        default:
                            Row(act, media.App is { Length: > 0 } app ? S.DockWidgetMenuOpenApp(app) : S.DockWidgetMenuOpenPlayer, GlyphOpenApp, enabled: media.CanOpen);
                            break;
                    }
                    break;
                }
                // Each device plugged in under its name, its rows as a drive's: the device a row
                // names is the one it acts on, whatever else comes and goes before the pick.
                case WidgetAct.OpenDrive or WidgetAct.Eject or WidgetAct.Disconnect or WidgetAct.DriveProperties
                    when ((widget as RemovableIsland)?.Devices ?? (widget as FolderIsland)?.Devices) is { } plugged:
                    if (!devices)
                    {
                        devices = true;
                        foreach (var device in plugged.Take(MostDevices).Where(device => device.Root.Length > 0))
                        {
                            items.Add(new FlyoutItem(0, device.Title, Header: true));
                            foreach (var each in acts.Where(each => each is WidgetAct.OpenDrive or WidgetAct.Eject or WidgetAct.DriveProperties))
                            {
                                DriveRow(each, device, Row);
                            }
                        }
                    }
                    break;
                case WidgetAct.OpenDrive or WidgetAct.Eject or WidgetAct.Disconnect or WidgetAct.DriveProperties:
                    if (widget is DriveIsland drives && drives.At(0) is { Missing: false, Root.Length: > 0 } drive)
                    {
                        DriveRow(act, drive, Row);
                    }
                    break;
                // The sound face's output: muted or not, as a middle click on the face does.
                case WidgetAct.Mute when widget is FolderIsland sounding:
                {
                    var muted = sounding.OutputMuted;
                    Row(act, S.DockWidgetMenuMute, GlyphMute, enabled: muted is not null, ticked: muted == true);
                    break;
                }
                // The Wi-Fi and Bluetooth faces' radios, in the words of what a pick does; left out
                // until Windows has said whether the radio is on, or where there is none.
                case WidgetAct.WifiRadio or WidgetAct.BluetoothRadio:
                {
                    var wifi = act == WidgetAct.WifiRadio;
                    var radio = (widget as FolderIsland)?.Radio(wifi ? QuickSwitch.WifiRadio : QuickSwitch.BluetoothRadio);
                    var on = radio is { Available: true } ? radio.On : null;
                    Row(act, TurnText(act, on: on != true), wifi ? GlyphWifi : GlyphBluetooth, enabled: on is not null);
                    break;
                }
                // The battery face's power modes under their heading, the one in force ticked, as the
                // power mode line has them; greyed, none ticked, where Windows says of none (§247).
                case WidgetAct.PowerMode:
                    if (widget is FolderIsland powered)
                    {
                        var now = powered.PowerModes.Read().Mode;
                        items.Add(new FlyoutItem(0, S.DockQuickPowerMode, Header: true));
                        foreach (var (mode, name, glyph) in QuickSettings.PowerModePage.Modes)
                        {
                            Row(act, name, glyph, mode.ToString(), enabled: now is not null, ticked: mode == now);
                        }
                        items.Add(new FlyoutItem(0, "", Separator: true));
                    }
                    break;
                case WidgetAct.Mute or WidgetAct.MuteMicrophone:
                {
                    var mutes = (widget as SoundPartTile)?.Mutes;
                    Row(
                        act,
                        act == WidgetAct.Mute ? S.DockWidgetMenuMute : S.DockWidgetMenuMuteMicrophone,
                        act == WidgetAct.Mute ? GlyphMute : GlyphMicOff,
                        enabled: act == WidgetAct.Mute ? mutes?.Output ?? false : mutes?.Microphone ?? false,
                        ticked: act == WidgetAct.Mute ? mutes?.Muted ?? false : mutes?.MicrophoneMuted ?? false);
                    break;
                }
                case WidgetAct.StartPause when widget is StopwatchTile stopwatch:
                {
                    var word = stopwatch.MenuButtons.Action;
                    Row(act, word, word == S.DockClockPause ? CountdownCard.PauseGlyph : CountdownCard.PlayGlyph, enabled: true);
                    break;
                }
                case WidgetAct.Lap or WidgetAct.Reset:
                {
                    // A stopwatch's lap and reset, left out where its own buttons are greyed.
                    var buttons = (widget as StopwatchTile)?.MenuButtons;
                    if (act == WidgetAct.Lap)
                    {
                        Row(act, S.DockStopwatchLap, StopwatchCard.LapGlyph, enabled: buttons?.Laps ?? false);
                    }
                    else
                    {
                        Row(act, S.DockStopwatchReset, StopwatchCard.ResetGlyph, enabled: buttons?.Resets ?? false);
                    }
                    break;
                }
                case WidgetAct.StartPause or WidgetAct.Stop:
                {
                    var buttons = (widget as CountdownTile)?.MenuButtons;
                    if (act == WidgetAct.StartPause)
                    {
                        // The card's own word, in the language it is said in now (CountdownCard.Actions).
                        var word = buttons?.Action ?? S.DockClockStart;
                        var glyph = word == S.DockClockPause ? CountdownCard.PauseGlyph
                            : word == S.DockClockStartAgain ? CountdownCard.AgainGlyph
                            : CountdownCard.PlayGlyph;
                        Row(act, word, glyph, enabled: buttons is not null);
                    }
                    else
                    {
                        Row(act, S.DockWidgetMenuStop, CountdownCard.ClearGlyph, enabled: buttons?.Clears ?? false);
                    }
                    break;
                }
                case WidgetAct.Longer or WidgetAct.Shorter:
                {
                    // Its steppers, which a card or a line too small for them leaves to its menu: enabled
                    // where a step would change its length — not while it runs, nor past its least or most.
                    var steps = (widget as CountdownTile)?.Steps;
                    if (act == WidgetAct.Longer)
                    {
                        Row(act, S.DockWidgetMenuMinuteLonger, CountdownCard.PlusGlyph, enabled: steps?.Longer ?? false);
                    }
                    else
                    {
                        Row(act, S.DockWidgetMenuMinuteShorter, CountdownCard.MinusGlyph, enabled: steps?.Shorter ?? false);
                    }
                    break;
                }
                // The notification list's, and the clipboard line's: everything but what is pinned.
                case WidgetAct.ClearAll:
                    Row(act, S.WidgetNotificationsClearAll, GlyphClear,
                        enabled: (widget as INotificationActs)?.HasAny ?? (widget as ClipboardLine)?.Store.Items.Any(item => !item.Pinned) ?? false);
                    break;
                case WidgetAct.ClearCompleted:
                    Row(act, S.DockWidgetMenuClearCompleted, GlyphClear, enabled: widget is TodoTile { Remote: false } todo && todo.View.Completed.Count > 0);
                    break;
                // The site the widget reads, where it names one: a feed's own (only once the feed has
                // named one Karpo opens), the match on ESPN (§239), a share's page — a line's or a face's.
                case WidgetAct.OpenWebsite:
                    if (SiteOf(widget, face) is { } site)
                    {
                        Row(act, TextOf(act), GlyphOf(act), site);
                    }
                    break;
                // weather, air quality: the forecast for the place on the weather site chosen; tides the
                // place on the map chosen (LinkProviders, §319, §322), earthquakes the one under the pointer
                // there, else USGS's map of them all — only where the place is known (§308).
                case WidgetAct.OpenForecast or WidgetAct.OpenMap:
                {
                    var place = widget switch
                    {
                        WeatherLineTile weatherLine => weatherLine.Options,
                        AirQualityTile airLine => airLine.Options,
                        TidesTile tidesLine => tidesLine.Options,
                        _ => face?.Weather,
                    };
                    var air = widget is AirQualityTile || face?.Info == FolderFaces.AirQuality;
                    var quakes = widget is QuakesLine || face?.Info == FolderFaces.Quakes;
                    var url = act == WidgetAct.OpenForecast ? ForecastPage(place, air, WeatherStore.DeviceAt)
                        : quakes ? ((widget as QuakesLine)?.MenuQuake is { } quake ? LinkProviders.Place(quake.Latitude, quake.Longitude, QuakeZoom, quake.Place) : QuakeMap)
                        : MapPage(place, WeatherStore.DeviceAt);
                    if (url is not null)
                    {
                        Row(act, TextOf(act), GlyphOf(act), url);
                    }
                    break;
                }
                // contacts: the person right-clicked on under their name — Send email, Call, then what
                // of theirs can be copied — each only where they have it; nothing over no person.
                case WidgetAct.ContactEmail or WidgetAct.ContactCall or WidgetAct.ContactCopy:
                    if (act == WidgetAct.ContactEmail && widget is Pim.ContactsLine contacts && contacts.MenuContact() is { } contact)
                    {
                        items.Add(new FlyoutItem(0, contact.Person.DisplayName, Header: true));
                        if (Pim.ContactActs.MailUri(contact.Email) is { } mailto)
                        {
                            Row(WidgetAct.ContactEmail, S.DockContactsSendEmail, Pim.ContactActs.MailGlyph, mailto);
                        }
                        if (Pim.ContactActs.TelUri(contact.Phone) is { } tel)
                        {
                            Row(WidgetAct.ContactCall, S.DockContactsCall, Pim.ContactActs.PhoneGlyph, tel);
                        }
                        if (contact.Email is { } email)
                        {
                            Row(WidgetAct.ContactCopy, S.DockContactsCopyEmail, Pim.ContactActs.CopyGlyph, email);
                        }
                        if (contact.Phone is { } phone)
                        {
                            Row(WidgetAct.ContactCopy, S.DockContactsCopyPhone, Pim.ContactActs.CopyGlyph, phone);
                        }
                        Row(WidgetAct.ContactCopy, S.DockContactsCopyAll, Pim.ContactActs.CopyGlyph, Pim.ContactActs.AllText(contact.Person));
                        items.Add(new FlyoutItem(0, "", Separator: true));
                    }
                    break;
                // calendar: the meeting under the pointer, where it has a Join; nothing over anything
                // else (§206). The bar's meeting face: the meeting it shows.
                case WidgetAct.JoinMeeting:
                    if ((widget switch { IMeetingLine line => line.MenuMeeting(), FolderIsland folder => folder.MenuMeeting(), _ => null }) is { } meeting)
                    {
                        Row(act, S.DockMeetingJoinMeeting, MeetingFaceDrawing.JoinGlyph, meeting.Link.Url);
                        items.Add(new FlyoutItem(0, "", Separator: true));
                    }
                    break;
                // §641: a Start widget's rows as it gives them: an act's hands the screen away, a change's stays (StartEdit).
                case WidgetAct.Start:
                    if (widget is StartWidgets.StartLine start)
                    {
                        foreach (var each in start.MenuRows())
                        {
                            if (each.Separator)
                            {
                                if (items.Count > 0 && !items[^1].Separator)
                                {
                                    items.Add(new FlyoutItem(0, "", Separator: true));
                                }
                                continue;
                            }
                            Row(each.Edit ? WidgetAct.StartEdit : WidgetAct.Start, each.Text, each.Glyph, each.Value, ticked: each.Ticked);
                        }
                        if (items.Count > 0 && items[^1].Separator)
                        {
                            items.RemoveAt(items.Count - 1);
                        }
                    }
                    break;
                // screentime: "Open" and the app's name, for an app that can be opened; none for no app.
                case WidgetAct.OpenApp:
                    if (((widget as ScreenTimeLine)?.MenuApp ?? (widget as ScreenTimeDonut)?.MenuApp) is { } menuApp && ScreenTimeNames.Of(menuApp.Key) is { Item: not null } known)
                    {
                        Row(act, S.DockWidgetMenuOpenApp(known.Name), GlyphOpenApp, menuApp.Key);
                    }
                    break;
                // A line that reads what it shows: read again, when it last read under it (§451).
                case WidgetAct.Refresh:
                    if (widget is IRefreshable { CanRefresh: true, Refreshing: false } reads)
                    {
                        Row(act, TextOf(act), GlyphOf(act));
                        if (reads.UpdatedUtc is { } read)
                        {
                            items[^1] = items[^1] with { Detail = WeatherAge.Updated(read.ToLocalTime(), Clock.Now()) };
                        }
                    }
                    break;
                case WidgetAct.WindowsSettings:
                    if (page is { } link)
                    {
                        Row(act, link.Title, GlyphSettings, link.Uri);
                    }
                    break;
                // The Snipping Tool's own: left out where the one installed cannot — none at all, or
                // one too old to record through its address (WindowsButtons.Records).
                case WidgetAct.OpenSnippingTool or WidgetAct.SnipIn3 or WidgetAct.SnipIn10 or WidgetAct.RecordScreen:
                {
                    var version = WindowsButtons.VersionOf(WindowsButtons.SnippingFamily);
                    Row(act, TextOf(act), GlyphOf(act), TargetOf(act),
                        enabled: act == WidgetAct.RecordScreen ? WindowsButtons.Records(version) : version is not null);
                    break;
                }
                // The Snipping Tool's way to get it, while it is not installed and only then.
                case WidgetAct.GetSnippingTool:
                    if (WindowsButtons.Missing(BarKind.SnippingTool, WindowsButtons.InstalledNow) is not null)
                    {
                        Row(act, TextOf(act), GlyphOf(act), WindowsButtons.SnippingStore);
                    }
                    break;
                // The switches, in the words of what a pick does (TurnText), where the PC has them.
                case WidgetAct.EnergySaver:
                {
                    var saver = (widget as BatteryIsland)?.Saver ?? (widget as FolderIsland)?.Saver;
                    Row(act, TurnText(act, on: saver?.On != true), GlyphLeaf, enabled: saver?.Available ?? false);
                    break;
                }
                // keep awake (§269): the lengths, the one running ticked, each turning it on from now —
                // no heading over them, a menu's first row is an act.
                case WidgetAct.AwakeFor:
                {
                    var awake = KeepAwake.Shared;
                    var (on, running) = (awake.On, awake.Minutes);
                    foreach (var length in KeepAwakeLengths.All)
                    {
                        Row(act, KeepAwakeLengths.Name(length), KeepAwakeLengths.GlyphOf(length), KeepAwake.Word(length), ticked: on && running == length);
                    }
                    items.Add(new FlyoutItem(0, "", Separator: true));
                    break;
                }
                // notifications: Windows' own do not disturb, the one the bell and the switch share:
                // on, for an hour or until tomorrow, while off; off, saying till when, while on.
                // None where Windows won't let Karpo switch it, nor without a widget to ask, as every
                // control (§308): "Notification settings" is the way then.
                case WidgetAct.DoNotDisturb:
                    foreach (var (text, word, until) in (widget as INotificationActs)?.Dnd is { } dnd ? DoNotDisturbRows(dnd) : [])
                    {
                        Row(act, text, WidgetInfos.DoNotDisturb.Glyph, word);
                        if (until is { } end)
                        {
                            items[^1] = items[^1] with { Detail = S.DockDndUntil(Panels.Nls.Time(end.LocalDateTime)) };
                        }
                    }
                    break;
                // perf: the app under the pointer by its name, "End task" left out where none of its
                // processes would end; kept for the pick, which asks first (Dock.EndProcesses).
                case WidgetAct.EndTask:
                    if (widget is ProcessesLine { MenuGroup: { } ending } top)
                    {
                        ProcessesLine.Asked = ending;
                        items.Add(new FlyoutItem(0, top.Title(ending), Header: true));
                        Row(act, S.DockMenuEndTask, GlyphEndTask, ending.Key, enabled: Performance.ProcessReader.CanEnd(ending));
                    }
                    break;
                // folder contents: the folder shown, where it has one.
                case WidgetAct.OpenInExplorer:
                    if (widget is FolderContentsLine { Contents.FolderPath: { Length: > 0 } explored })
                    {
                        Row(act, S.DockDrivesOpenInExplorer, GlyphExplorer, explored);
                    }
                    break;
                // perf: the app right-clicked on's file, where it has one; nothing without it (a pin's is PinRows').
                case WidgetAct.OpenFileLocation:
                    if (widget is ProcessesLine { MenuGroup: { } shown } listed)
                    {
                        if (listed.NameOf(shown).Path is { } image)
                        {
                            Row(act, S.DockWidgetMenuOpenFileLocation, GlyphFolderOpen, image);
                        }
                        items.Add(new FlyoutItem(0, "", Separator: true));
                    }
                    break;
                default:
                    Row(act, TextOf(act), GlyphOf(act));
                    break;
            }
        }
        // A heading whose rows were all left out heads nothing, and a rule left at an end or beside
        // another parts nothing.
        for (var at = items.Count - 1; at >= 0; at--)
        {
            if (items[at].Header && (at == items.Count - 1 || items[at + 1].Header || items[at + 1].Separator))
            {
                items.RemoveAt(at);
            }
        }
        Tidy(items);
        return (items, rows);
    }

    /// <summary>A row that is a choice this PC has nothing for stays greyed, saying so (the power modes, the devices); every other is left out (<see cref="ActRows"/>).</summary>
    private static bool SaysNone(WidgetAct act) => act is WidgetAct.ChooseDevice or WidgetAct.PowerMode;

    /// <summary>
    /// A switch's row, in the words of what a pick does — "Turn Wi-Fi on", "Turn energy saver off" —
    /// one wording for every switch a menu offers, a face's, a line's and a status icon's (§308).
    /// </summary>
    internal static string TurnText(WidgetAct act, bool on) => act switch
    {
        WidgetAct.WifiRadio => on ? S.DockWidgetMenuWifiOn : S.DockWidgetMenuWifiOff,
        WidgetAct.BluetoothRadio => on ? S.DockWidgetMenuBluetoothOn : S.DockWidgetMenuBluetoothOff,
        WidgetAct.EnergySaver => on ? S.DockQuickIndicatorEnergySaverTurnOn : S.DockQuickIndicatorEnergySaverOff,
        WidgetAct.DoNotDisturb => on ? S.DockQuickIndicatorDndTurnOn : S.DockQuickIndicatorDndOff,
        _ => TextOf(act),
    };

    /// <summary>
    /// The site <paramref name="widget"/> reads, for "Open website": a feed's own, the match on ESPN, a
    /// share's page on the finance site chosen; a folder's, the one its <paramref name="face"/> shows. Null where
    /// it names none.
    /// </summary>
    private static string? SiteOf(IIsland? widget, FolderFace? face) => widget switch
    {
        Rss.RssIsland feed => feed.Site,
        Sports.SportsTile line => line.Site,
        CameraLine camera => camera.Site,
        MarketIsland market => MarketPage(Markets.MarketAsk.Of(market.Options)),
        FolderIsland folder when face?.Info == FolderFaces.Markets => MarketPage(folder.MarketAsk),
        FolderIsland folder when face?.Info == FolderFaces.Sports => folder.SportsSite,
        _ => null,
    };

    /// <summary>markets: a share's, a rate's or a coin's page on the finance site chosen (<see cref="LinkProviders.Quote(string?)"/>, §322); null where it asks nothing (a share with no symbol).</summary>
    private static string? MarketPage(Markets.MarketAsk? asked) =>
        asked is { } ask ? LinkProviders.Quote(ask.Symbol) : null;

    /// <summary>
    /// weather, air quality: the forecast for <paramref name="place"/> on the weather site chosen
    /// (<see cref="LinkProviders.Forecast(double, double, bool)"/>, §322) — its air quality's, where the site has it,
    /// <paramref name="air"/> — at its coordinates, <paramref name="device"/>'s where it follows the device
    /// (where a forecast last found it, <see cref="WeatherStore.DeviceAt"/>); null where the place is not known.
    /// </summary>
    internal static string? ForecastPage(WeatherOptions? place, bool air, (double Latitude, double Longitude)? device) =>
        Where(place, device) is (var latitude, var longitude)
            ? LinkProviders.Forecast(latitude, longitude, air)
            : null;

    /// <summary>
    /// tides: <paramref name="place"/> marked under its name on the map chosen (<see cref="LinkProviders.Place(double, double, int, string?)"/>,
    /// §319), at a town's zoom; null where the place is not known.
    /// </summary>
    internal static string? MapPage(WeatherOptions? place, (double Latitude, double Longitude)? device) =>
        Where(place, device) is (var latitude, var longitude)
            ? LinkProviders.Place(latitude, longitude, LinkProviders.TownZoom, place!.UseDeviceLocation ? null : PlaceNames.Whole(place))
            : null;

    /// <summary>earthquakes: the zoom one opens at, its region around it.</summary>
    internal const int QuakeZoom = 7;

    /// <summary>Where <paramref name="place"/> is: its coordinates, or <paramref name="device"/>'s where it follows the device; null for neither.</summary>
    private static (double Latitude, double Longitude)? Where(WeatherOptions? place, (double Latitude, double Longitude)? device) =>
        place is not { IsSet: true } ? null
        : place.UseDeviceLocation ? device
        : place is { Latitude: { } latitude, Longitude: { } longitude } ? (latitude, longitude)
        : null;

    /// <summary>
    /// A widget's own rows in a folder at rest: its kind's own acts for <paramref name="widget"/> as
    /// it is now (<see cref="ActRows"/>), and last "Refresh" where it reads what it shows again
    /// (<see cref="WidgetAct.Refresh"/>, §451), and nothing else — copying, removing and its settings are
    /// the menu's while the bar is arranged alone (<see cref="WidgetRowsFor"/>, §253). None where the
    /// kind has nothing, and none for a widget the file has lost.
    /// </summary>
    internal static (List<FlyoutItem> Items, List<WidgetActRow> Acts) FolderMenuRows(WidgetItem? island, IIsland? widget) =>
        island is null ? ([], []) : ActRows([.. ActsFor(island), WidgetAct.Refresh], widget, WindowsLinkOf(island.Info));

    /// <summary>A drive's row for <paramref name="act"/>, as Explorer has it for that drive; none where it has none.</summary>
    private static void DriveRow(WidgetAct act, Drive drive, Action<WidgetAct, string, string, string?, bool, bool?> row)
    {
        switch (act)
        {
            case WidgetAct.OpenDrive:
                row(act, S.DockWidgetMenuOpen, GlyphFolderOpen, drive.Root, true, null);
                break;
            // What Explorer offers an eject for: a stick, a card, a disc.
            case WidgetAct.Eject when drive.Kind.IsRemovable():
                row(act, S.DockWidgetMenuEject, GlyphUsb, drive.Root, true, null);
                break;
            case WidgetAct.Disconnect when drive.Kind == DriveKind.Remote:
                row(act, S.DockWidgetMenuDisconnect, GlyphDisconnect, drive.Root, true, null);
                break;
            case WidgetAct.DriveProperties:
                row(act, S.DockWidgetMenuProperties, GlyphProperties, drive.Root, true, null);
                break;
        }
    }

    /// <summary>
    /// The page in Windows' Settings app an act opens, where that is all it does: every such row reads
    /// as the page's one name (<see cref="WindowsPages"/>, "Sound settings"), on a face, a line and a
    /// status icon alike (§308). Null for any other act.
    /// </summary>
    internal static WindowsLink? PageOf(WidgetAct act) => act switch
    {
        WidgetAct.DateTimeSettings => WindowsPages.DateTime,
        WidgetAct.NotificationSettings => WindowsPages.Notifications,
        WidgetAct.SoundSettings => WindowsPages.Sound,
        WidgetAct.NetworkSettings => WindowsPages.Network,
        WidgetAct.PowerSettings => WindowsPages.Power,
        WidgetAct.HotspotSettings => WindowsPages.MobileHotspot,
        WidgetAct.AutoPlaySettings => WindowsPages.AutoPlay,
        WidgetAct.StorageSettings => WindowsPages.Storage,
        WidgetAct.WindowsUpdate => WindowsPages.WindowsUpdate,
        _ => null,
    };

    /// <summary>
    /// The words of an act whose row always reads the same: a page's its name, an app it opens "Open"
    /// and the app's name ("Open Task Manager"), everything else a verb first (§308).
    /// </summary>
    internal static string TextOf(WidgetAct act) => PageOf(act) is { } page ? page.Title : act switch
    {
        WidgetAct.CalendarApp => S.DockWidgetMenuOpenCalendar,
        WidgetAct.JoinMeeting => S.DockMeetingJoinMeeting,
        WidgetAct.ResourceMonitor => S.DockWidgetMenuOpenApp(S.DockWidgetMenuResourceMonitor),
        WidgetAct.TaskManager => S.DockWidgetMenuOpenApp(S.CoreBarMenuTaskManager),
        WidgetAct.FileExplorer => S.DockWidgetMenuOpenApp(S.CoreBarMenuFileExplorer),
        WidgetAct.TaskView => WidgetInfos.TaskView.Title,
        WidgetAct.NewDesktop => S.DockMenuNewDesktop,
        // The bar's system icons, in the words of Windows 11's own.
        WidgetAct.OpenVolumeMixer => S.DockWidgetMenuOpenVolumeMixer,
        WidgetAct.TroubleshootSound => S.DockWidgetMenuTroubleshootSound,
        WidgetAct.DiagnoseNetwork => S.DockWidgetMenuDiagnoseNetwork,
        // The Snipping Tool's, in the words of its own menu and buttons.
        WidgetAct.OpenSnippingTool => S.DockWidgetMenuOpenSnippingTool,
        WidgetAct.SnipIn3 => S.DockWidgetMenuSnipIn(3),
        WidgetAct.SnipIn10 => S.DockWidgetMenuSnipIn(10),
        WidgetAct.RecordScreen => S.DockWidgetMenuRecordScreen,
        WidgetAct.GetSnippingTool => S.DockWidgetMenuGetFromStore(WidgetInfos.SnippingTool.Title),
        WidgetAct.OpenWebsite => S.DockWidgetMenuOpenWebsite,
        WidgetAct.OpenForecast => S.DockWidgetMenuOpenForecast,
        WidgetAct.OpenMap => S.DockWidgetMenuOpenMap,
        // A face's own (Dock.FaceMenus.cs).
        WidgetAct.ClockApp => S.DockWidgetMenuOpenClock,
        WidgetAct.EditFolder => S.DockWidgetMenuEditFolder,
        WidgetAct.CastConnect => S.DockWidgetMenuCastConnect,
        WidgetAct.ClipboardHistory => S.DockWidgetMenuClipboardHistory,
        WidgetAct.ManageAccounts => S.DockWidgetMenuManageAccounts,
        WidgetAct.LayoutSettings => S.DockWidgetMenuLayoutSettings,
        WidgetAct.EndTask => S.DockMenuEndTask,
        WidgetAct.Refresh => S.DockWidgetMenuRefresh,
        _ => act.ToString(),
    };

    private static string GlyphOf(WidgetAct act) => act switch
    {
        WidgetAct.CalendarApp => GlyphCalendar,
        WidgetAct.JoinMeeting => MeetingFaceDrawing.JoinGlyph,
        WidgetAct.ResourceMonitor => GlyphMonitor,
        WidgetAct.DateTimeSettings => GlyphClock,
        WidgetAct.TaskView => WidgetInfos.TaskView.Glyph,
        WidgetAct.NewDesktop => GlyphNewDesktop,
        WidgetAct.OpenVolumeMixer => GlyphVolume,
        WidgetAct.TroubleshootSound or WidgetAct.DiagnoseNetwork => GlyphRepair,
        WidgetAct.OpenSnippingTool => WidgetInfos.SnippingTool.Glyph,
        WidgetAct.SnipIn3 or WidgetAct.SnipIn10 => GlyphStopwatch,
        WidgetAct.RecordScreen => GlyphRecord,
        WidgetAct.GetSnippingTool => GlyphStore,
        WidgetAct.OpenWebsite => GlyphGlobe,
        WidgetAct.OpenForecast => WidgetInfos.WeatherDays.Glyph,
        WidgetAct.OpenMap => GlyphMap,
        WidgetAct.ClockApp => GlyphClock,
        WidgetAct.EditFolder => GlyphEdit,
        WidgetAct.AutoPlaySettings => GlyphUsb,
        WidgetAct.CastConnect => WidgetInfos.WirelessDisplays.Glyph,
        WidgetAct.TaskManager => GlyphTaskManager,
        WidgetAct.ClipboardHistory => WidgetInfos.Clipboard.Glyph,
        WidgetAct.ManageAccounts => GlyphAccount,
        WidgetAct.LayoutSettings => TaskbarLayouts.LayoutGlyph,
        WidgetAct.WindowsUpdate => GlyphUpdate,
        WidgetAct.StorageSettings => GlyphDrive,
        WidgetAct.FileExplorer => GlyphExplorer,
        WidgetAct.EndTask => GlyphEndTask,
        WidgetAct.Refresh => GlyphRefresh,
        _ => GlyphSettings,
    };

    /// <summary>The page or the program an act that only opens something opens; null for the rest.</summary>
    internal static string? TargetOf(WidgetAct act) => PageOf(act) is { } page ? page.Uri : act switch
    {
        WidgetAct.CalendarApp => CalendarAppTarget,
        WidgetAct.ResourceMonitor => ResourceMonitorTarget,
        WidgetAct.OpenVolumeMixer => VolumeMixerPage,
        WidgetAct.TroubleshootSound or WidgetAct.DiagnoseNetwork => TroubleshootPage,
        WidgetAct.OpenSnippingTool => WindowsButtons.SnippingApp,
        WidgetAct.SnipIn3 => WindowsButtons.SnipIn(3),
        WidgetAct.SnipIn10 => WindowsButtons.SnipIn(10),
        WidgetAct.RecordScreen => WindowsButtons.Record,
        WidgetAct.GetSnippingTool => WindowsButtons.SnippingStore,
        WidgetAct.ClockApp => ClockAppTarget,
        WidgetAct.CastConnect => CastTarget,
        WidgetAct.TaskManager => TaskManagerTarget,
        WidgetAct.FileExplorer => FileExplorerTarget,
        _ => null,
    };

    /// <summary>
    /// folders: a dropdown's list of <paramref name="devices"/> (SoundPartTile.Choices): a row each, by
    /// its name, in the order given, the one in use ticked; each a <see cref="WidgetAct.ChooseDevice"/>
    /// of its device, which makes it Windows' default as the device menu's rows do (RunAct).
    /// </summary>
    internal static (List<FlyoutItem> Items, List<WidgetActRow> Rows) DeviceChoices(IReadOnlyList<(string Id, string Name, bool InUse)> devices)
    {
        var items = new List<FlyoutItem>();
        var rows = new List<WidgetActRow>();
        foreach (var (id, name, inUse) in devices)
        {
            var command = CMD_WIDGET_ACT + (uint)rows.Count;
            rows.Add(new WidgetActRow(command, WidgetAct.ChooseDevice, id));
            items.Add(new FlyoutItem(command, name, Checked: inUse));
        }
        return (items, rows);
    }

    /// <summary>
    /// folders: where a dropdown's list stands for its <paramref name="field"/> (screen px) at
    /// <paramref name="scale"/>: hung off the field's middle by half its height and 4 DIP more, so it
    /// clears the field above it or below it, centred on it and as wide as it.
    /// </summary>
    internal static (POINT Anchor, float GapDip, float Width) ChoicesPlacement(RECT field, float scale)
    {
        var anchor = new POINT { x = (field.left + field.right) / 2, y = (field.top + field.bottom) / 2 };
        var half = (field.bottom - field.top) / 2f / MathF.Max(scale, 0.01f);
        return (anchor, half + 4, field.right - field.left);
    }

    /// <summary>
    /// Whether an act leaves Karpo — hands the screen to another program: a page, an app, a file,
    /// File Explorer, a Properties window, a Windows panel or Settings, Karpo's own settings taking
    /// the folder over — rather than acting where it stands. §664: the one rule of what a pick does to
    /// the folder under its menu (<see cref="FolderAfterMenu"/>): only these close it; everything else
    /// acts in place — read again, switched, chosen, copied, played — and leaves it open, a new act
    /// too until it is listed here (WidgetMenuTests.EveryActIsClassifiedAsLeavingOrInPlace).
    /// </summary>
    internal static bool HandsAway(WidgetAct act) => act is
        // Opened: an app, a file, a place, a page.
        WidgetAct.OpenPlayer or WidgetAct.OpenDrive or WidgetAct.CalendarApp or WidgetAct.ClockApp or WidgetAct.OpenApp or WidgetAct.Start
        or WidgetAct.RunAsAdministrator or WidgetAct.OpenFileLocation or WidgetAct.FileExplorer or WidgetAct.OpenInExplorer
        or WidgetAct.OpenWebsite or WidgetAct.OpenForecast or WidgetAct.OpenMap or WidgetAct.ContactEmail or WidgetAct.ContactCall
        or WidgetAct.JoinMeeting or WidgetAct.ResourceMonitor or WidgetAct.TaskManager or WidgetAct.OpenVolumeMixer
        or WidgetAct.OpenSnippingTool or WidgetAct.GetSnippingTool or WidgetAct.SnipIn3 or WidgetAct.SnipIn10 or WidgetAct.RecordScreen
        // Windows' own windows and panels: Properties, Settings' pages, troubleshooters, Task view, clipboard history, casting.
        or WidgetAct.DriveProperties or WidgetAct.FileProperties or WidgetAct.WindowsSettings or WidgetAct.DateTimeSettings
        or WidgetAct.NotificationSettings or WidgetAct.SoundSettings or WidgetAct.TroubleshootSound or WidgetAct.DiagnoseNetwork
        or WidgetAct.NetworkSettings or WidgetAct.PowerSettings or WidgetAct.HotspotSettings or WidgetAct.AutoPlaySettings
        or WidgetAct.WindowsUpdate or WidgetAct.StorageSettings or WidgetAct.TaskView or WidgetAct.NewDesktop
        or WidgetAct.ClipboardHistory or WidgetAct.CastConnect or WidgetAct.EndTask
        // Karpo's own settings, which take the folder over (edit mode, its pages).
        or WidgetAct.EditFolder or WidgetAct.ManageAccounts or WidgetAct.LayoutSettings;

    /// <summary>
    /// Whether <paramref name="act"/> takes a picture of the screen — the Snipping Tool's snips in 3
    /// and in 10 seconds and its recorder — so every open folder goes off the screen before it is
    /// asked, as before the snip a click starts (Dock.Folders.cs, FolderForward).
    /// </summary>
    internal static bool PicturesTheScreen(WidgetAct act) => act is WidgetAct.SnipIn3 or WidgetAct.SnipIn10 or WidgetAct.RecordScreen;

    /// <summary>
    /// The row among <paramref name="rows"/> that <paramref name="id"/> picked, or null: by its id, never by where it
    /// stands, so a row a later face offers too, standing once (FaceRows), leaves every other row its own act.
    /// </summary>
    internal static WidgetActRow? ActRowOf(IReadOnlyList<WidgetActRow> rows, uint id) =>
        id >= CMD_WIDGET_ACT ? rows.FirstOrDefault(row => row.Id == id) : null;

    /// <summary>
    /// Does what <paramref name="row"/> says, to <paramref name="widget"/> — on the widget itself,
    /// through the method its own control's click calls, or through <paramref name="open"/> for
    /// whatever Windows is to open. Whether that handed the screen away is <see cref="HandsAway"/>'s.
    /// A widget that is not the kind the row was made for (the folder changed under the menu) is
    /// asked nothing.
    /// </summary>
    internal static void RunAct(WidgetActRow row, IIsland? widget, Opener open)
    {
        switch (row.Act)
        {
            case WidgetAct.PlayPause or WidgetAct.Previous or WidgetAct.Next:
                (widget as MediaIsland)?.Press(row.Act);
                return;
            case WidgetAct.SeekBack or WidgetAct.SeekForward:
                (widget as MediaIsland)?.SeekBy(row.Act == WidgetAct.SeekBack ? -SeekStep : SeekStep);
                return;
            case WidgetAct.ChooseDevice:
                if (row.On is { Length: > 0 } device)
                {
                    (widget as SoundPartTile)?.ChooseDevice(device);
                }
                return;
            case WidgetAct.MuteApp:
                if (row.On is { Length: > 0 } app)
                {
                    (widget as SoundPartTile)?.ToggleAppMute(app);
                }
                return;
            case WidgetAct.OpenPlayer:
                (widget as MediaIsland)?.OpenPlayer(open);
                return;
            case WidgetAct.FollowSession:
                (widget as MediaIsland)?.Follow(row.On);
                return;
            case WidgetAct.Shuffle:
                (widget as MediaIsland)?.ToggleShuffle();
                return;
            case WidgetAct.Repeat:
                (widget as MediaIsland)?.CycleRepeat();
                return;
            case WidgetAct.Mute:
                (widget as SoundPartTile)?.ToggleMute();
                (widget as FolderIsland)?.ToggleMute();
                return;
            case WidgetAct.WifiRadio:
                (widget as FolderIsland)?.Radio(QuickSwitch.WifiRadio)?.Act();
                return;
            case WidgetAct.BluetoothRadio:
                (widget as FolderIsland)?.Radio(QuickSwitch.BluetoothRadio)?.Act();
                return;
            case WidgetAct.PowerMode:
                if (Enum.TryParse<PowerMode>(row.On, out var mode))
                {
                    (widget as FolderIsland)?.PowerModes.SetMode(mode);
                }
                return;
            // Windows' clipboard history, by its key, as the clipboard button opened it.
            case WidgetAct.ClipboardHistory:
                QuickSwitches.Chord([VK.VK_LWIN, 'V']);
                return;
            // Karpo's own: the dock does them (Dock.FaceMenus.cs, FaceActChosen).
            case WidgetAct.EditFolder or WidgetAct.ManageAccounts or WidgetAct.LayoutSettings:
                return;
            case WidgetAct.ClearCompleted:
                (widget as TodoTile)?.ClearCompleted();
                return;
            // Read again, unless a read is on its way already.
            case WidgetAct.Refresh:
                if (widget is IRefreshable { CanRefresh: true, Refreshing: false } reads)
                {
                    reads.RefreshNow();
                }
                return;
            case WidgetAct.MuteMicrophone:
                (widget as SoundPartTile)?.ToggleMicrophone();
                return;
            case WidgetAct.StartPause:
                (widget as CountdownTile)?.Act();
                (widget as StopwatchTile)?.Act();
                return;
            case WidgetAct.Lap:
                (widget as StopwatchTile)?.Lap();
                return;
            case WidgetAct.Reset:
                (widget as StopwatchTile)?.Reset();
                return;
            case WidgetAct.Stop:
                (widget as CountdownTile)?.Stop();
                return;
            case WidgetAct.Longer:
                _ = (widget as CountdownTile)?.Longer();
                return;
            case WidgetAct.Shorter:
                _ = (widget as CountdownTile)?.Shorter();
                return;
            case WidgetAct.ClearAll:
                (widget as INotificationActs)?.ClearAll();
                (widget as ClipboardLine)?.ClearAll();
                return;
            case WidgetAct.EnergySaver:
                ((widget as BatteryIsland)?.Saver ?? (widget as FolderIsland)?.Saver)?.Act();
                return;
            case WidgetAct.DoNotDisturb:
            {
                if ((widget as INotificationActs)?.Dnd is not { } quiet)
                {
                    return;
                }
                switch (row.On)
                {
                    case DndHour:
                        quiet.TurnOnUntil(quiet.InAnHour());
                        break;
                    case DndTomorrow:
                        quiet.TurnOnUntil(quiet.Tomorrow());
                        break;
                    default:
                        quiet.Set(row.On != DndOff);
                        break;
                }
                return;
            }
            // keep awake (§269): on from now for the row's length, the screen with it (§586).
            case WidgetAct.AwakeFor:
                if (KeepAwake.Length(row.On) is { } length)
                {
                    KeepAwake.Shared.Turn(length);
                }
                return;
            // rss: a feed's site, and only where it is an http or https address: nothing else a feed
            // names is ever handed to Windows to open.
            case WidgetAct.OpenWebsite:
                if (row.On is { } site && Feeds.Opens(site))
                {
                    open(site, null);
                }
                return;
            // contacts: copied where the person was right-clicked, who says so; the folder stays.
            case WidgetAct.ContactCopy:
                if (row.On is { Length: > 0 } text)
                {
                    (widget as Pim.ContactsLine)?.CopyFromMenu(text);
                }
                return;
            // calendar: the meeting the row was made for, by its link (§206).
            case WidgetAct.JoinMeeting:
                if (Karpo.Accounts.MeetingLinks.Of(row.On) is { } link)
                {
                    MeetingJoin.Open(link);
                }
                return;
            // screentime: the dock's, by the pins' one rule (Dock.cs, the widget acts' pick), never
            // the shell's "open" of the path, which would start another copy of an app that is open.
            case WidgetAct.OpenApp:
                return;
            // §641: a Start widget's row, done by the widget: its act at once (the folder closing for it), its change asked of the dock.
            case WidgetAct.Start or WidgetAct.StartEdit:
                if (row.On is { Length: > 0 } said)
                {
                    (widget as StartWidgets.StartLine)?.MenuPicked(said);
                }
                return;
            // perf: the dock's, which asks first (Dock.EndProcesses).
            case WidgetAct.EndTask:
                return;
            // A folder in File Explorer, whatever else has taken folders, as the list's folder rows open it.
            case WidgetAct.OpenInExplorer:
                if (row.On is { Length: > 0 } folder)
                {
                    Drives.FolderSource.Current.Open(folder, true);
                }
                return;
            // Windows takes the screen for either, so the folder closes (HandsAway).
            case WidgetAct.TaskView:
                Desktops.TaskView();
                return;
            case WidgetAct.NewDesktop:
                Desktops.NewDesktop();
                return;
        }
        if (row.On is { Length: > 0 } on)
        {
            open(on, row.Act switch
            {
                WidgetAct.Eject => MenuVerbs.Eject,
                WidgetAct.Disconnect => MenuVerbs.Disconnect,
                WidgetAct.DriveProperties or WidgetAct.FileProperties => MenuVerbs.Properties,
                WidgetAct.RunAsAdministrator => MenuVerbs.RunAs,
                WidgetAct.OpenFileLocation => MenuVerbs.Select,
                WidgetAct.CopyAsPath or WidgetAct.CopyLink => MenuVerbs.Copy,
                _ => null,
            });
            return;
        }
        if (TargetOf(row.Act) is { } target)
        {
            open(target, null);
        }
    }

    /// <summary>
    /// perf: the app <paramref name="key"/> the top processes' menu offered (<see cref="ProcessesLine.Asked"/>)
    /// ended, every process of it, once Windows' own question over <paramref name="owner"/> is answered
    /// "End task" — Cancel the default, as for anything that loses what is not saved. Nothing where
    /// the menu offered another, or none.
    /// </summary>
    private void EndProcesses(DockWindow? owner, string key)
    {
        if (ProcessesLine.Asked is not { } group || !string.Equals(group.Key, key, StringComparison.Ordinal))
        {
            return;
        }
        ProcessesLine.Asked = null;
        var name = ProcessesLine.Names(group).Name;
        var question = new DeleteQuestion(S.DockProcessesEndTitle(name), S.DockProcessesEndContent(group.Count), S.DockMenuEndTask, S.DockDeleteCancel, 0);
        if (DeleteAsk.Ask(owner?.Hwnd ?? HWND.NULL, question))
        {
            _ = Performance.ProcessReader.End(group);
        }
    }

    /// <summary>
    /// A pin's file on this PC, which Windows' own rows for a file act on; null for an app by its
    /// id, a link, a shell place, and a pin whose target is gone (it has a menu of its own).
    /// </summary>
    internal static string? FileOf(Item item) =>
        item is { Broken: null, Kind: ItemKind.Shell } && Path.IsPathFullyQualified(item.Target) ? item.Target : null;

    /// <summary>
    /// pins: the rows Windows has for what a pin opens, one rule for a pin on the bar and one in a
    /// folder, under the app's own row (<see cref="AppRows"/>): for a file or a folder this PC has
    /// (<see cref="FileOf"/>) "Run as administrator" where it means something (<see cref="Elevates"/>),
    /// "Open file location", "Copy as path" and "Properties", as Explorer and Start have them; for a
    /// link "Copy link", as a browser's. Each acts on the target as the menu showed it; none for an app
    /// by its id, a shell place, and a pin whose target is gone (it has a menu of its own, BrokenRows).
    /// </summary>
    internal static (List<FlyoutItem> Items, List<WidgetActRow> Acts) PinRows(Item item)
    {
        var items = new List<FlyoutItem>();
        var acts = new List<WidgetActRow>();
        void Row(WidgetAct act, string text, string glyph, string on)
        {
            var id = CMD_WIDGET_ACT + (uint)acts.Count;
            acts.Add(new WidgetActRow(id, act, on));
            items.Add(new FlyoutItem(id, text, Glyph(glyph)));
        }
        if (FileOf(item) is { } file)
        {
            if (Elevates(item))
            {
                Row(WidgetAct.RunAsAdministrator, S.DockWidgetMenuRunAsAdministrator, GlyphAdministrator, file);
            }
            Row(WidgetAct.OpenFileLocation, S.DockWidgetMenuOpenFileLocation, GlyphFolderOpen, file);
            Row(WidgetAct.CopyAsPath, S.DockWidgetMenuCopyAsPath, GlyphCopy, $"\"{file}\"");
            Row(WidgetAct.FileProperties, S.DockWidgetMenuProperties, GlyphProperties, file);
        }
        else if (item is { Broken: null, Kind: ItemKind.Link, Target.Length: > 0 })
        {
            Row(WidgetAct.CopyLink, S.DockWidgetMenuCopyLink, GlyphLink, item.Target);
        }
        return (items, acts);
    }

    // notifications: what a do not disturb row does, as the row keeps it (WidgetActRow.On).
    private const string DndOn = "on";
    private const string DndHour = "hour";
    private const string DndTomorrow = "tomorrow";
    private const string DndOff = "off";

    /// <summary>
    /// notifications: do not disturb's rows in a notification menu, by <paramref name="quiet"/>'s state —
    /// each its words, what it does and, for one on for a time, when that ends. Off (or not known
    /// yet): "Turn do not disturb on", "Turn on for 1 hour", "Turn on until tomorrow"; on: "Turn do not
    /// disturb off". None where Windows won't let Karpo switch it: the menu's "Notification settings"
    /// is the way then. One-off commands, so a pick closes the menu.
    /// </summary>
    internal static IReadOnlyList<(string Text, string Word, DateTimeOffset? Until)> DoNotDisturbRows(DoNotDisturb quiet) => quiet.State switch
    {
        { CanSwitch: false } => [],
        { On: true } => [(TurnText(WidgetAct.DoNotDisturb, on: false), DndOff, quiet.Until)],
        _ => [(TurnText(WidgetAct.DoNotDisturb, on: true), DndOn, null), (S.DockDndForAnHour, DndHour, null), (S.DockDndUntilTomorrow, DndTomorrow, null)],
    };

    /// <summary>Segoe Fluent Icons' Link, a browser's "Copy link".</summary>
    private const string GlyphLink = "\uE71B";

    /// <summary>
    /// Whether "Run as administrator" means something for <paramref name="item"/>: what the bar's
    /// Ctrl+Shift+click runs as one (<see cref="AppClicks.CanElevate"/>) — a program, a script, a
    /// console — and a shortcut only where it leads to a program: for a document it would fail.
    /// </summary>
    internal static bool Elevates(Item item) =>
        AppClicks.CanElevate(item.Target)
        && (!item.Target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || item.Keys.Exists(key => key.StartsWith("exe:", StringComparison.Ordinal)));

    /// <summary>What becomes of a folder a widget's menu stood over, once the menu has gone.</summary>
    internal enum AfterMenu
    {
        /// <summary>It stays open, closing for a press elsewhere again (or, while arranged, staying through it).</summary>
        Stays,

        /// <summary>It closes too.</summary>
        Closes,
    }

    /// <summary>
    /// What becomes of the folder a widget's menu stood over when the menu goes. While the bar is
    /// arranged (<paramref name="arranging"/>) it stays, whatever happened: arranging keeps folders
    /// open through everything. At rest it closes when the pick handed the screen to another program
    /// (<paramref name="handedAway"/>), when the focus did not come back to the folder
    /// (<paramref name="focusElsewhere"/>: something took it from the menu — another window, the
    /// Windows key's Start — which the folder heard nothing of while the menu held it, and would
    /// have closed for). A pick that acted in Karpo, Escape and a press anywhere leave it open: a
    /// press outside the menu, on the folder or outside both, closes the menu alone and is used up
    /// for it (FlyoutHost.UsesUpPresses); the next press closes the folder (docs\widgets.md §185).
    /// </summary>
    internal static AfterMenu FolderAfterMenu(bool arranging, bool handedAway, bool focusElsewhere) =>
        arranging ? AfterMenu.Stays
        : handedAway || focusElsewhere ? AfterMenu.Closes
        : AfterMenu.Stays;

    /// <summary>
    /// §664: whether <paramref name="foreground"/> (the window in front as a menu over a folder ends) is another
    /// program's — something took the focus from Karpo while the menu held the folder. None in front, the menu
    /// still going, the folder or any other window of Karpo's is not: an in-place pick never closes the folder
    /// for where the focus happens to stand a moment after it.
    /// </summary>
    internal static bool ForegroundTakenFromKarpo(HWND foreground)
    {
        if (foreground == HWND.NULL)
        {
            return false;
        }
        uint process = 0;
        return TerraFX.Interop.Windows.Windows.GetWindowThreadProcessId(foreground, &process) != 0
            && process != TerraFX.Interop.Windows.Windows.GetCurrentProcessId();
    }

}
