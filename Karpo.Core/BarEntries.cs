// Everything that can stand on a taskbar, and what each does there (the split, S5).
//
// A taskbar entry is the one type that can open something: one of Karpo's flyouts, or one of
// Windows' own panels (BarClick). It has a run of the bar a new one goes into, and a width in
// cells. A list widget has none of these (ListWidgets.cs): a line acts or
// does nothing, and that is all.
//
// Every widget with a drawing of its own on the bar is here, and so is everything the bar draws
// itself — Start, the apps, the tray and a pin — because
// all of them are items of a layout that a person places, moves and removes the same way.
//
// The clock is not one (2026-10-09, branch folder-faces): "the clock taskbar widget is actually
// just a premade widget that is actually a list folder with a face and correct widgets inside the
// folder". It is the clock preset (Presets.cs), a list folder wearing the clock face (FolderFaces),
// and the entry, the scene's drawing of it and Karpo's clock flyout went. The weather and the
// calendar went the same way the same day: presets wearing the weather and the calendar faces, made
// of their flyouts' parts as widgets. So did the network and the Bluetooth buttons (2026-10-10):
// presets wearing the Wi-Fi and Bluetooth faces, their status glyphs, holding their pages as lines.
// And the sound button: the sound preset, wearing the speaker as the button did, its panel's
// regions as lines; and the removable-devices button: the removable-devices preset, wearing the
// stick as the button did, its list the line it always had in a folder. And the timer, the focus
// session and the stopwatch: presets wearing their countdown and stopwatch faces, what is left or
// what is counted, each holding its card at full size as a line. And the mobile hotspot's
// button: the mobile hotspot preset, wearing the hotspot's glyph, its page a line; the switch it
// was in a folder stays a line of its own. And the battery and the power button: the battery
// preset, wearing the charge as the battery did, and the power preset, wearing the power glyph,
// their flyouts' rows as lines (the battery's line, energy saver, the power mode, the commands).
// And the mail: the mail preset, wearing the envelope and its unread count as the entry did, its
// list the inbox's line. And now playing: the media preset, wearing the art and the track as the
// entry did, its list the now-playing line (which picks the session, and shuffles and repeats).
// And the five performance readings: the processor, graphics, memory, disk and network activity
// presets, each wearing its reading's figure as the entry did, its list the reading's line, whose
// tallest height is the flyout's figure, minute and facts, over Task Manager. And the stock and
// the RSS feed: the stock and news presets, wearing the ticker and the headline as the entries
// did, their lists the stock's line (its tallest the panel: the header, the day, its age and
// refresh) and the feed's line (its whole widget the headlines panel: pictures, refresh, the site).
// And the drive and the network drive: the drive and network drive presets, wearing the drive's
// picture, name and capacity bar as the entries did, their list the drive's line with its
// contents under it, which is the flyout's listing. And the bell: the notifications preset,
// wearing the bell, its count and do not disturb's "zZ" as the entry did, its list the
// notification list's line, grouped by app as the flyout was, its cards opened and answered there.
// And the clipboard history: the clipboard preset, wearing the clipboard's glyph as the entry did,
// its list the clipboard's line, Win+V's cards as rows, a click pasting where the person was.
// And the contacts: the contacts preset, wearing the contact's glyph as the entry did, its list
// the contacts' line, the search box in it, Enter or a click copying an address as the flyout did.
// And cast and nearby sharing (2026-10-11): the cast preset, wearing the cast glyph with a dot
// while this PC shows on a wireless display, its list Windows' Cast panel and Win+P's modes as
// lines; and the nearby sharing preset, wearing the share glyph dimmed while it is off, its list
// Windows' nearby sharing as a line; nearby sharing's switch stays a folder's line.
// And the desktop switcher: the desktops preset, wearing the glyph and the name of the desktop you
// are on as the entry did, its list the desktops' line, a desktop switched to, renamed, made,
// closed and dragged into another place there (docs\widgets.md §116).
// And the input indicator: the keyboard preset (2026-10-11, "all the remaining flyouts should become
// folders with a face now"), wearing the language and region as the entry did, its list the
// keyboard's line, the layouts to switch the window in front to. And VPN: the VPN preset, wearing the shield, lit while
// a connection is up, its list the connections' line, each connected or disconnected from its row.
// And project: the project preset, wearing its icon, its list Win+P's four modes, the line the cast
// preset has too. And the layout switcher: the layouts preset, wearing the glyph and the name of the
// layout this bar shows as the entry did, its list the layouts' line (docs\widgets.md §127).
//
// The ids are config.json's own words (the members' JsonStringEnumMemberName), the words the
// one kind enum before the split (Section) had, so a file does not change with the split.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// What an item of a layout is. A word this build does not know — an entry from a newer Karpo,
/// or one taken out — reads as a value outside the enum rather than failing the file
/// (<see cref="TolerantEnumConverter{T}"/>), and the reader drops the item.
/// <para>
/// The members are in <see cref="BarEntries.All"/>'s order, which is what lets
/// <see cref="BarEntries.Find(BarKind)"/> be an index.
/// </para>
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<BarKind>))]
public enum BarKind
{
    [JsonStringEnumMemberName("widgetsBoard")] WidgetsBoard,
    [JsonStringEnumMemberName("start")] Start,
    [JsonStringEnumMemberName("search")] Search,
    [JsonStringEnumMemberName("taskView")] TaskView,
    [JsonStringEnumMemberName("snippingTool")] SnippingTool,
    [JsonStringEnumMemberName("windowsSettings")] WindowsSettings,
    [JsonStringEnumMemberName("pin")] Pin,
    [JsonStringEnumMemberName("apps")] Apps,
    [JsonStringEnumMemberName("folderList")] FolderList,
    [JsonStringEnumMemberName("indicators")] Indicators,
    [JsonStringEnumMemberName("tray")] Tray,
    [JsonStringEnumMemberName("penMenu")] PenMenu,
    [JsonStringEnumMemberName("touchKeyboard")] TouchKeyboard,
    [JsonStringEnumMemberName("virtualTouchpad")] VirtualTouchpad,
    [JsonStringEnumMemberName("volume")] Volume,
    [JsonStringEnumMemberName("brightness")] Brightness,
    [JsonStringEnumMemberName("spatialSound")] SpatialSound,
    [JsonStringEnumMemberName("wifiSwitch")] WifiSwitch,
    [JsonStringEnumMemberName("bluetoothSwitch")] BluetoothSwitch,
    [JsonStringEnumMemberName("soundSwitch")] SoundSwitch,
    [JsonStringEnumMemberName("microphoneSwitch")] MicrophoneSwitch,
    [JsonStringEnumMemberName("gameMode")] GameMode,
    [JsonStringEnumMemberName("colorFilters")] ColorFilters,
    [JsonStringEnumMemberName("monoAudio")] MonoAudio,
    [JsonStringEnumMemberName("airplaneMode")] AirplaneMode,
    [JsonStringEnumMemberName("energySaver")] EnergySaver,
    [JsonStringEnumMemberName("nightLight")] NightLight,
    [JsonStringEnumMemberName("rotationLock")] RotationLock,
    [JsonStringEnumMemberName("liveCaptions")] LiveCaptions,
    [JsonStringEnumMemberName("accessibility")] Accessibility,
    [JsonStringEnumMemberName("magnifier")] Magnifier,
    [JsonStringEnumMemberName("narrator")] Narrator,
    [JsonStringEnumMemberName("stickyKeys")] StickyKeys,
    [JsonStringEnumMemberName("keepAwake")] KeepAwake,
    [JsonStringEnumMemberName("darkMode")] DarkMode,
    [JsonStringEnumMemberName("security")] Security,
    [JsonStringEnumMemberName("doNotDisturb")] DoNotDisturb,
    [JsonStringEnumMemberName("quickLink")] QuickLink,
    [JsonStringEnumMemberName("windowsNotifications")] WindowsNotifications,
    [JsonStringEnumMemberName("windowsQuickSettings")] WindowsQuickSettings,
    [JsonStringEnumMemberName("windowsSound")] WindowsSound,
    [JsonStringEnumMemberName("windowsNetworks")] WindowsNetworks,
    [JsonStringEnumMemberName("windowsCast")] WindowsCast,
    [JsonStringEnumMemberName("windowsClipboard")] WindowsClipboard,
    [JsonStringEnumMemberName("emojiPanel")] EmojiPanel,
    [JsonStringEnumMemberName("showDesktop")] ShowDesktop,
}

/// <summary>
/// What a click on a taskbar entry asks for. Four answers, and no fifth that means "nobody has
/// said yet": an entry that opens nothing says so, and the type will not let it be confused with
/// one whose flyout somebody forgot to name.
/// <para>
/// Two of them open a <b>popup</b> — <see cref="Flyout"/> (one of Karpo's) and
/// <see cref="Panel"/> (one of Windows') — and only a taskbar entry has either. A list line
/// answers with <see cref="CellAct"/>, which has no member that opens anything, so the
/// folder rule (docs\widgets.md §15) is a word that cannot be written rather than a check.
/// </para>
/// </summary>
public enum BarClick
{
    /// <summary>
    /// The entry answers its own click and asks the dock for nothing: the volume slider takes the
    /// pointer.
    /// </summary>
    Nothing,

    /// <summary>
    /// One of Karpo's own flyouts at the entry — the calendar, the notification list, a
    /// quick-settings page, a folder, or the generic list of items a widget hands over. Which one
    /// is the dock's answer (<c>Karpo.Dock\Dock.Widgets.cs</c>): the flyouts are dock types, and
    /// this registry is shared with the settings app, which must never see one.
    /// </summary>
    Flyout,

    /// <summary>
    /// It does something to the world, there and then, with nothing to show at the entry: show
    /// desktop minimises the windows, a pin opens its app, a switch flips its radio, a graph opens
    /// Task Manager. An app or a window opening is an act, not a popup.
    /// </summary>
    Action,

    /// <summary>
    /// One of <b>Windows'</b> own popups, opened at the edge of the screen: Start, Search, task
    /// view, the widgets board (Win+W), the pen menu, the touch keyboard, the virtual touchpad, and
    /// Windows' menus (§466): the quick link menu, the notifications, quick settings and two of its
    /// pages, cast, clipboard history and the emoji panel.
    /// </summary>
    Panel,
}

/// <summary>
/// Everything about one taskbar entry: what it is (<see cref="Info"/>), how wide it starts, what
/// draws it, what a click and a press on it mean. One per <see cref="BarKind"/>, in
/// <see cref="BarEntries.All"/>, and every field is required: an entry cannot compile until it has
/// said all of it.
/// </summary>
public sealed record BarEntry
{
    /// <summary>Which entry this is: the word a layout stores.</summary>
    public required BarKind Kind { get; init; }

    /// <summary>What it is, wherever it stands: its name, glyph, blurb and options record.</summary>
    public required WidgetInfo Info { get; init; }

    /// <summary>The view a folder opens in; null for everything that is not a folder.</summary>
    public required FolderView? Opens { get; init; }

    /// <summary>
    /// Which of the bar's three runs a new one goes into when nobody said
    /// (<see cref="WidgetGroup"/>): the Windows 11 taskbar's own arrangement.
    /// </summary>
    public required WidgetGroup Run { get; init; }

    /// <summary>
    /// Where a new one starts along the bar, in cells (<see cref="WidgetSize"/>): the width it is
    /// made at where it has one, or else the entry's measurement, its fewest
    /// cells to its most, with its content deciding in between.
    /// <para>
    /// <b>It is only where a widget starts.</b> The fewest and the most a person sets are theirs,
    /// and they are not clamped into this (<see cref="WidgetBounds"/>): a measurement says what
    /// the content needs, and a person who wants more room or less is given it.
    /// </para>
    /// </summary>
    public required WidgetSize Size { get; init; }

    /// <summary>What a click on it asks for (<see cref="BarClick"/>).</summary>
    public required BarClick Click { get; init; }

    /// <summary>
    /// pins: whether a press or a wheel turn on it is the entry's own before it is the bar's
    /// (<see cref="Karpo.Core.Interaction"/>): the gate that keeps the volume slider's track a
    /// slider rather than a handle for moving the widget.
    /// </summary>
    public required Interaction Interaction { get; init; }

    /// <summary>
    /// What draws it on the bar: the island class the bar's factory makes, or
    /// <see cref="WidgetSource.None"/> for what the scene draws itself (Start, search, task view,
    /// Windows' other glyph buttons (<see cref="BarEntries.IsWindowsButton"/>), the apps, a pin, the
    /// tray, show desktop).
    /// </summary>
    public required WidgetSource Drawing { get; init; }

}

/// <summary>Every taskbar entry, and the answers each gave.</summary>
public static class BarEntries
{
    /// <summary>
    /// Every entry, in the order a bar shows them when nobody has moved anything — the order a new
    /// one is inserted by — and in <see cref="BarKind"/>'s order.
    /// </summary>
    public static readonly BarEntry[] All =
    [
        // Windows' widgets board, where Windows keeps its button: first on the bar. A new bar has
        // neither it nor Karpo's weather (the weather preset; Sections.DefaultLayout), both in the
        // add list for whoever wants one. §474: Windows' own Widgets button, the weather's glyph and
        // the temperature over the conditions (WindowsFaces), opening the board.
        WindowsFace(BarKind.WidgetsBoard, WidgetInfos.WidgetsBoard, WidgetGroup.Start, Interaction.Click),
        new()
        {
            Kind = BarKind.Start, Info = WidgetInfos.Start, Opens = null,
            Run = WidgetGroup.WithTaskbar, Size = WidgetSize.OneCell,
            Click = BarClick.Panel, Interaction = Interaction.Click, Drawing = WidgetSource.None,
        },
        new()
        {
            Kind = BarKind.Search, Info = WidgetInfos.Search, Opens = null,
            Run = WidgetGroup.WithTaskbar, Size = WidgetSize.OneCell,
            Click = BarClick.Panel, Interaction = Interaction.Click, Drawing = WidgetSource.None,
        },
        new()
        {
            Kind = BarKind.TaskView, Info = WidgetInfos.TaskView, Opens = null,
            Run = WidgetGroup.WithTaskbar, Size = WidgetSize.OneCell,
            Click = BarClick.Panel, Interaction = Interaction.Click, Drawing = WidgetSource.None,
        },
        // Copilot stood here, beside task view, until the person asked for it gone (2026-10-09):
        // "copilot" is a word this build does not know, and a bar that had one drops it (BarKind).
        // Two shortcuts to Windows' own apps, drawn as Windows' buttons are — one glyph in a
        // button's well — beside them (the backlog, items 10 and 18): the Snipping Tool, whose
        // click starts a snip at once, and Settings. Each opens an app, an act as a pin's click
        // is, and has a tooltip as Windows' buttons have (BarTooltips). Phone Link was a third (item 23) and was
        // removed at the person's asking: a pin to it does the same.
        new()
        {
            Kind = BarKind.SnippingTool, Info = WidgetInfos.SnippingTool, Opens = null,
            Run = WidgetGroup.WithTaskbar, Size = WidgetSize.OneCell,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.None,
        },
        new()
        {
            Kind = BarKind.WindowsSettings, Info = WidgetInfos.WindowsSettings, Opens = null,
            Run = WidgetGroup.WithTaskbar, Size = WidgetSize.OneCell,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.None,
        },
        new()
        {
            Kind = BarKind.Pin, Info = WidgetInfos.Pin, Opens = null,
            Run = WidgetGroup.WithTaskbar, Size = WidgetSize.OneCell,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.None,
        },
        // Its width is how many apps are open, a button each; where a new one starts must not cut
        // that off at eight, so its most starts at the most there is.
        new()
        {
            Kind = BarKind.Apps, Info = WidgetInfos.Apps, Opens = null,
            Run = WidgetGroup.WithTaskbar, Size = WidgetSize.Automatic with { MaxCells = WidgetItem.MostCells },
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.None,
        },
        // folders: a folder in a folder invites a regress with nothing to gain, so a folder is not
        // a list widget. The grid folder is gone (2026-10-09); only the list folder is left. The
        // wheel is its face's: the sound face steps the volume, as the sound button it was did
        // (FolderIsland.Wheel); every other face leaves it to the bar. Its width is its faces side
        // by side, however many (§259), so its most is the most there is: eight cells cut the
        // button of many faces off.
        new()
        {
            Kind = BarKind.FolderList, Info = WidgetInfos.FolderList, Opens = FolderView.List,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic with { MaxCells = WidgetItem.MostCells },
            Click = BarClick.Flyout, Interaction = Interaction.Click | Interaction.Wheel, Drawing = WidgetSource.Folder,
        },
        // The status icons Windows 11 shows beside the tray — the microphone, the camera and the
        // location in use, airplane mode, the hotspot, energy saver, do not disturb, Windows' update
        // icon (a VPN is the network icon's shield, as in Windows) — a cell each while it is so,
        // and no room at all while nothing is (IndicatorsIsland), as many cells as there are, so its
        // most starts at the most there is. A click opens the page
        // Windows opens for it, which is not one of Karpo's flyouts; which apps have the microphone
        // is said to a screen reader (no tooltip over a widget, §367).
        new()
        {
            Kind = BarKind.Indicators, Info = WidgetInfos.Indicators, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic with { MaxCells = WidgetItem.MostCells },
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Indicators,
        },
        // The second entry that takes a drag of its own, and the one that shows why this is a field
        // and not an interface: an icon is dragged along the tray to reorder it or onto the chevron
        // to hide it (Dock.Tray.cs, TrayDrags), and the tray is drawn by the scene with no island
        // at all — so it has nothing in common, type-wise, with the slider. Every icon owns its
        // square and the gap after it (Layout.HitTest), so what is left to grab the widget by is
        // the chevron before them, and the whole part when the tray is empty. That is enough to
        // move it, and edit mode reaches all of it either way.
        new()
        {
            Kind = BarKind.Tray, Info = WidgetInfos.Tray, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Flyout, Interaction = Interaction.Drag, Drawing = WidgetSource.None,
        },
        // The pen menu, the touch keyboard and the virtual touchpad, where Windows keeps them: after
        // the tray's icons, before the quick settings. The pen menu and the touchpad show only where
        // Windows would show them — on a PC with a pen, or a touch screen — or where the person chose
        // Always (InputButtons), and take no room where they do not; the touch keyboard always shows.
        // Windows' popups all three, drawn by the bar.
        new()
        {
            Kind = BarKind.PenMenu, Info = WidgetInfos.PenMenu, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.OneCell,
            Click = BarClick.Panel, Interaction = Interaction.Click, Drawing = WidgetSource.None,
        },
        new()
        {
            Kind = BarKind.TouchKeyboard, Info = WidgetInfos.TouchKeyboard, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.OneCell,
            Click = BarClick.Panel, Interaction = Interaction.Click, Drawing = WidgetSource.None,
        },
        new()
        {
            Kind = BarKind.VirtualTouchpad, Info = WidgetInfos.VirtualTouchpad, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.OneCell,
            Click = BarClick.Panel, Interaction = Interaction.Click, Drawing = WidgetSource.None,
        },
        // The one widget whose drag is its own: the track is dragged to set the volume, and the
        // wheel over it steps the level. Its speaker is not its track, so a press there still
        // becomes a move — the widget says where, this only says that it does.
        new()
        {
            Kind = BarKind.Volume, Info = WidgetInfos.Volume, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Nothing, Interaction = Interaction.Drag | Interaction.Wheel,
            Drawing = WidgetSource.Volume,
        },
        // quick: the displays' brightness as one slider, the volume slider's family: the track is
        // dragged and turned (2026-10-11, the person: "remove the flyout"); beside it Windows' light or
        // dark mode, a button that takes its press itself (§605), so the widget's click is nothing.
        new()
        {
            Kind = BarKind.Brightness, Info = WidgetInfos.Brightness, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Nothing, Interaction = Interaction.Drag | Interaction.Wheel,
            Drawing = WidgetSource.Brightness,
        },
        // quick: spatial sound is a toggle (2026-10-11: "remove the spacial sound flyout and just make
        // the spacial sound widget into a toggle"): a click turns it off, or on in the format its
        // Settings card names. It listed the formats until then.
        new()
        {
            Kind = BarKind.SpatialSound, Info = WidgetInfos.SpatialSound, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        // quick: the radios' and the sound's toggles, each a click that flips it (WidgetInfos.WifiSwitch).
        new()
        {
            Kind = BarKind.WifiSwitch, Info = WidgetInfos.WifiSwitch, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = BarKind.BluetoothSwitch, Info = WidgetInfos.BluetoothSwitch, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = BarKind.SoundSwitch, Info = WidgetInfos.SoundSwitch, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = BarKind.MicrophoneSwitch, Info = WidgetInfos.MicrophoneSwitch, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        // quick: game mode, color filters and mono audio, each a click that flips it (WidgetInfos.GameMode).
        new()
        {
            Kind = BarKind.GameMode, Info = WidgetInfos.GameMode, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = BarKind.ColorFilters, Info = WidgetInfos.ColorFilters, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = BarKind.MonoAudio, Info = WidgetInfos.MonoAudio, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = BarKind.AirplaneMode, Info = WidgetInfos.AirplaneMode, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = BarKind.EnergySaver, Info = WidgetInfos.EnergySaver, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = BarKind.NightLight, Info = WidgetInfos.NightLight, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        // quick: on a PC whose screen does not turn it takes itself off the bar, as Windows leaves
        // its tile out (RotationSwitch.Offered).
        new()
        {
            Kind = BarKind.RotationLock, Info = WidgetInfos.RotationLock, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = BarKind.LiveCaptions, Info = WidgetInfos.LiveCaptions, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        new()
        {
            Kind = BarKind.Accessibility, Info = WidgetInfos.Accessibility, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        // quick: the accessibility page's magnifier, narrator and sticky keys (2026-10-14), each a
        // click that flips it, as the other toggles are on the bar.
        Toggle(BarKind.Magnifier, WidgetInfos.Magnifier),
        Toggle(BarKind.Narrator, WidgetInfos.Narrator),
        Toggle(BarKind.StickyKeys, WidgetInfos.StickyKeys),
        // quick: keep awake (2026-10-20, §269), a click that keeps the PC awake and lets it sleep again.
        Toggle(BarKind.KeepAwake, WidgetInfos.KeepAwake),
        // darkMode: Windows' own mode, dark or light (§403), a click that switches it.
        Toggle(BarKind.DarkMode, WidgetInfos.DarkMode),
        // Automatic, not OneCell: a glyph on its own would fit one cell, but the same label option
        // the one-button widgets have needs room for the name beside it.
        new()
        {
            Kind = BarKind.Security, Info = WidgetInfos.Security, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click,
            Drawing = WidgetSource.Security,
        },
        // Windows' own do not disturb, beside the bell that wears its "zZ": a one-button widget in
        // night light's family, lit while it is on. A click switches it rather than opening
        // anything.
        new()
        {
            Kind = BarKind.DoNotDisturb, Info = WidgetInfos.DoNotDisturb, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.Automatic,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
        },
        // windowsMenus: Windows' own menus, a button each (§466), opened as Windows opens them
        // (WindowsButtons.Target). The quick link menu is Start's right click, so it goes beside
        // Start; the rest where Windows keeps the clock and quick settings, at the end. §474, §490:
        // each wears the faces of the Karpo folder that stands for its menu (WindowsFaces), drawn as
        // that folder's are: the clock and the bell; the network, the volume and the battery; the
        // volume; the network; cast; the clipboard; the quick link menu's and the emoji panel's own
        // glyph. A face of the volume takes the wheel, as Windows' volume icon does.
        WindowsFace(BarKind.QuickLink, WidgetInfos.QuickLink, WidgetGroup.WithTaskbar, Interaction.Click),
        WindowsFace(BarKind.WindowsNotifications, WidgetInfos.WindowsNotifications, WidgetGroup.End, Interaction.Click),
        WindowsFace(BarKind.WindowsQuickSettings, WidgetInfos.WindowsQuickSettings, WidgetGroup.End, Interaction.Click | Interaction.Wheel),
        WindowsFace(BarKind.WindowsSound, WidgetInfos.WindowsSound, WidgetGroup.End, Interaction.Click | Interaction.Wheel),
        WindowsFace(BarKind.WindowsNetworks, WidgetInfos.WindowsNetworks, WidgetGroup.End, Interaction.Click),
        WindowsFace(BarKind.WindowsCast, WidgetInfos.WindowsCast, WidgetGroup.End, Interaction.Click),
        WindowsFace(BarKind.WindowsClipboard, WidgetInfos.WindowsClipboard, WidgetGroup.End, Interaction.Click),
        WindowsFace(BarKind.EmojiPanel, WidgetInfos.EmojiPanel, WidgetGroup.End, Interaction.Click),
        new()
        {
            Kind = BarKind.ShowDesktop, Info = WidgetInfos.ShowDesktop, Opens = null,
            Run = WidgetGroup.End, Size = WidgetSize.OneCell,
            Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.None,
        },
    ];

    /// <summary>A toggle's button: a click flips it.</summary>
    private static BarEntry Toggle(BarKind kind, WidgetInfo info) => new()
    {
        Kind = kind, Info = info, Opens = null,
        Run = WidgetGroup.End, Size = WidgetSize.Automatic,
        Click = BarClick.Action, Interaction = Interaction.Click, Drawing = WidgetSource.Switch,
    };

    /// <summary>
    /// §474, §490: one of Windows' menus (§466), wearing faces (<see cref="WindowsFaces"/>): its faces, fixed, side by
    /// side as a folder's are and exactly as long, a click that opens Windows' popup.
    /// </summary>
    private static BarEntry WindowsFace(BarKind kind, WidgetInfo info, WidgetGroup run, Interaction interaction) => new()
    {
        Kind = kind, Info = info, Opens = null,
        Run = run, Size = WidgetSize.Automatic with { MaxCells = WidgetItem.MostCells },
        Click = BarClick.Panel, Interaction = interaction, Drawing = WidgetSource.WindowsFaces,
    };

    /// <summary>
    /// The entry for <paramref name="kind"/>, or null for a value outside the enum — a word from a
    /// newer Karpo, which reads as <see cref="TolerantEnumConverter{T}.Unknown"/>.
    /// </summary>
    public static BarEntry? Find(BarKind kind) =>
        (uint)kind < (uint)All.Length ? All[(int)kind] : null;

    /// <summary>The entry for <paramref name="kind"/>; throws for a value outside the enum.</summary>
    public static BarEntry Of(BarKind kind) =>
        Find(kind) ?? throw new ArgumentOutOfRangeException(
            nameof(kind), kind, $"{kind} is not a taskbar entry (Karpo.Core\\BarEntries.cs).");

    /// <summary>The entry whose id is <paramref name="id"/>, or null where no taskbar entry has it.</summary>
    public static BarEntry? Find(string? id)
    {
        foreach (var entry in All)
        {
            if (string.Equals(entry.Info.Id, id, StringComparison.Ordinal))
            {
                return entry;
            }
        }
        return null;
    }

    /// <summary>The word config.json stores for <paramref name="kind"/>.</summary>
    public static string Id(this BarKind kind) => Of(kind).Info.Id;

    // The answers the bar's own code asks of an entry, by kind. Each is one lookup, and a value
    // outside the enum — an entry from a newer Karpo, before the reader drops it — reads as the
    // plainest answer rather than a throw, because these are asked on the pointer's path and on
    // every layout.

    /// <summary>What the edit rail, the menus and the widget's own page call it.</summary>
    public static string Title(this BarKind kind) => Find(kind)?.Info.Title ?? kind.ToString();

    /// <summary>A Segoe Fluent Icons code point standing for it.</summary>
    public static string Glyph(this BarKind kind) => Find(kind)?.Info.Glyph ?? "";

    /// <summary>
    /// Whether an island class draws it (<see cref="BarEntry.Drawing"/>), rather than the scene:
    /// the bar's own parts — Start, the apps, a pin, the tray, the strips — are measured,
    /// hit and highlighted by the bar.
    /// </summary>
    public static bool IsIsland(this BarKind kind) => Find(kind) is { Drawing: not WidgetSource.None };

    /// <summary>Whether it is one of the two folders (<see cref="BarEntry.Opens"/>).</summary>
    public static bool IsFolder(this BarKind kind) => Find(kind)?.Opens is not null;

    /// <summary>
    /// app buttons: whether it stands on the bar as app buttons, one or a run of them — the open apps,
    /// a pin — which the bar lays out, folds behind "…" and hits as the taskbar's buttons.
    /// </summary>
    public static bool IsButtons(this BarKind kind) => kind is BarKind.Apps or BarKind.Pin;

    /// <summary>
    /// Whether it is one of Windows' own buttons that came to the bar after Start, search and task
    /// view — the pen menu, the touch keyboard, the virtual touchpad —
    /// or a shortcut to one of Windows' apps drawn as they are — the Snipping Tool, Settings —
    /// which the bar draws the way it draws those three: one glyph in a button's well (at a face's icon size, §497), a click
    /// that opens something of Windows'. The three older ones keep kinds of hit of their own.
    /// Not the Widgets button and Windows' menus (§466), which wear faces (§474, §490,
    /// <see cref="WindowsFaces"/>), drawn as a folder's faces are and hit as a widget is.
    /// </summary>
    public static bool IsWindowsButton(this BarKind kind) => kind.IsWindowsOwn() && !kind.WearsWindowsFace();

    /// <summary>
    /// Whether a click on it opens what Windows' own button opens (Karpo.Dock's WindowsButtons.Target): the widgets
    /// board, the pen menu, the touch keyboard, the virtual touchpad, the Snipping Tool, Settings and Windows' menus
    /// (§466) — a glyph button (<see cref="IsWindowsButton"/>) or a face of Windows' own (§474).
    /// </summary>
    public static bool IsWindowsOwn(this BarKind kind) =>
        kind is BarKind.WidgetsBoard or BarKind.PenMenu or BarKind.TouchKeyboard or BarKind.VirtualTouchpad
            or BarKind.SnippingTool or BarKind.WindowsSettings || kind.IsWindowsMenu();

    /// <summary>windowsMenus: whether it is one of Windows' menus the bar opens (§466).</summary>
    public static bool IsWindowsMenu(this BarKind kind) =>
        kind is BarKind.QuickLink or BarKind.WindowsNotifications or BarKind.WindowsQuickSettings or BarKind.WindowsSound
            or BarKind.WindowsNetworks or BarKind.WindowsCast or BarKind.WindowsClipboard or BarKind.EmojiPanel;

    /// <summary>
    /// Whether its width is whole cells held in its kind's range (<see cref="WidgetBounds.DefaultFor"/>):
    /// the fewest and the most cells it may be given, the content deciding in between. Every entry
    /// measured in cells, the one-cell buttons included, but those whose size is of their own kind:
    /// the tray's is how many icons it shows, show desktop is a sliver, and the status icons are as
    /// many as are showing. Nobody sets it on the bar (<see cref="BarItem.HasSize"/>); a folder's
    /// widget of the kind keeps its numbers.
    /// </summary>
    public static bool HasSize(this BarKind kind) =>
        Find(kind) is not null && kind is not (BarKind.Tray or BarKind.ShowDesktop or BarKind.Indicators);

    /// <summary>
    /// pins: whether a drag that starts inside it may be its own before it is a move along the bar
    /// (<see cref="BarEntry.Interaction"/>): a gate, the widget still says where.
    /// </summary>
    public static bool TakesDrags(this BarKind kind) => Find(kind)?.Interaction.HasFlag(Interaction.Drag) ?? false;

    /// <summary>pins: whether a wheel turn over it is its own, and then the bar under it does not scroll.</summary>
    public static bool TakesWheel(this BarKind kind) => Find(kind)?.Interaction.HasFlag(Interaction.Wheel) ?? false;
}
