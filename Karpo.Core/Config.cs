// Everything a person can change, in %APPDATA%\Karpo\config.json.
//
// The file is meant to be readable and hand-editable: the dock watches the
// folder and applies an edit as soon as it is saved, and the settings app
// writes the same file, so there is one source of truth.
//
// Every entry on a taskbar is an Island the person placed: Start, the apps,
// the folders and the widgets alike. An island stands at its place in one of its
// bar's three sections (BarSection, kept in one ordered list, the start's entries
// first), carries its own options, and most kinds may be placed more
// than once (two clock folders for two time zones). The entries belong to named
// layouts, each of a type that says what bars it has (TaskbarLayouts.cs); every
// display shows one of them, or none.
//
// There is one shape, today's: the file is read as it is, and a word this build does not know
// reads as its default (Config.Repair). Nothing of an earlier build is read or carried over.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>Which way a widget with a length runs.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SliderOrientation>))]
public enum SliderOrientation
{
    /// <summary>Along the bar: sideways on a bar at the bottom or top, upright on one at a side.</summary>
    [JsonStringEnumMemberName("auto")] Auto,
    [JsonStringEnumMemberName("horizontal")] Horizontal,
    [JsonStringEnumMemberName("vertical")] Vertical,
}

/// <summary>
/// An edge of the screen. A display's bar stands on any of the four (<see cref="Screen.Edge"/>; the
/// person, 2026-10-12: "i want to be able to show it on the left and right as well"). A word this build
/// does not know reads as the bottom (<see cref="Config.Repair"/>); a build from before the sides came
/// back reads a side as the bottom too.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<Edge>))]
public enum Edge
{
    [JsonStringEnumMemberName("bottom")] Bottom,
    [JsonStringEnumMemberName("top")] Top,
    [JsonStringEnumMemberName("left")] Left,
    [JsonStringEnumMemberName("right")] Right,
}

/// <summary>
/// The two looks a bar can wear, fixed by what it is (the person, 2026-10-08: "docks are Floating;
/// bars are Islands"): no choice of anybody's, and never in the file. The connected look, one flat
/// band, is gone.
/// <para>
/// The height still decides what a bar has <b>room</b> for (<c>Layout.Thin</c>): text sizes, the
/// cell, the grip's depth and how the desktop buttons draw themselves all follow the room and
/// always will. Only the look follows this.
/// </para>
/// </summary>
public enum BarStyle
{
    /// <summary>A bar's: a card behind each widget, gaps between them and rounded corners.</summary>
    Islands,

    /// <summary>
    /// A dock's: a rounded panel of the bar's material a little off the screen's edge, the desktop
    /// showing around it. Inside the panel the widgets stand flush, square and with no card each.
    /// </summary>
    Floating,
}

/// <summary>
/// The three sections of a bar, each laid out on its own (docs\widgets.md §13): the start, aligned to
/// the bar's start; the middle, centred on the bar; the end, aligned to the bar's end. On a bar at the
/// bottom or the top they are the left, the middle and the right; on a bar standing on a side, the
/// top, the middle and the bottom. A word this build does not know reads as a value outside the enum,
/// and <see cref="Config.Repair"/> makes it <see cref="Start"/>.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<BarSection>))]
public enum BarSection
{
    /// <summary>Aligned to the bar's start: the left, or the top on a bar standing on a side.</summary>
    [JsonStringEnumMemberName("start")] Start,

    /// <summary>Centred on the bar, whatever the other two hold, as long as it fits between them.</summary>
    [JsonStringEnumMemberName("middle")] Middle,

    /// <summary>Aligned to the bar's end: the right, or the bottom on a bar standing on a side.</summary>
    [JsonStringEnumMemberName("end")] End,
}

/// <summary>
/// A place on a bar: a section, and an index in the bar's one list counted among the other entries
/// (as <c>Layout.DropSlot</c> counts it). The list holds the start's entries, then the middle's, then
/// the end's (<see cref="Sections.Order"/>), so the index says where among its section's entries a
/// widget goes, and the section says which section an index on a boundary between two means: the end
/// of one and the start of the next are the same index and two different places.
/// </summary>
public readonly record struct BarSlot(BarSection Section, int Index);

public static class Sections
{
    // Every answer below is the kind's own, from its descriptor in Widgets.cs. These
    // helpers stay because a hundred callers say `kind.Title()`, but each is now one
    // lookup rather than a switch of its own that a new kind could be left out of.

    /// <summary>
    /// Which of a bar's three sections a kind stands in when nobody said, which is the Windows 11
    /// taskbar's own arrangement: Start, the apps and the boxes beside them in the middle; the
    /// widgets button's place the far left; the rest the end, with the clock and the tray.
    /// <para>
    /// Always the middle for Windows' own buttons, and not where Windows' taskbar is aligned
    /// (TaskbarAl): the person asked for a Windows 11 bar with them in the middle. A new bar has them
    /// at the left all the same, the middle empty (Sections.DefaultLayout, 2026-10-14).
    /// </para>
    /// </summary>
    internal static BarSection SectionOf(this BarKind kind) => BarEntries.Find(kind)?.Run switch
    {
        WidgetGroup.WithTaskbar => BarSection.Middle,
        WidgetGroup.Start => BarSection.Start,
        _ => BarSection.End,
    };

    /// <summary>
    /// What a section is called on a bar running sideways (<paramref name="vertical"/> false): the
    /// left, the middle, the right; and on one standing on a side: the top, the middle, the bottom.
    /// </summary>
    public static string Title(this BarSection section, bool vertical) => section switch
    {
        BarSection.Middle => "middle",
        BarSection.End => vertical ? "bottom" : "right",
        _ => vertical ? "top" : "left",
    };

    /// <summary>
    /// Puts <paramref name="list"/> in its sections' order — the start's entries, then the middle's,
    /// then the end's — each section keeping its own order. True when anything moved. A section
    /// this build does not know is the start's (<see cref="Config.Repair"/> writes that back).
    /// <para>
    /// The one list is kept rather than three, because everything that merges, finds and walks the
    /// bar's entries — the three-way merge entry by entry, a widget's id, pin sync, the folders —
    /// reads one list, and a widget moved to another section is then one value changed rather than
    /// an entry taken out of one list and put into another, which a merge would read as a removal
    /// and lose the other side's edit of it to.
    /// </para>
    /// </summary>
    public static bool Order(List<BarItem> list)
    {
        var ordered = list.Select((island, at) => (Island: island, At: at))
            .OrderBy(entry => Rank(entry.Island))
            .ThenBy(entry => entry.At)
            .Select(entry => entry.Island)
            .ToList();
        if (ordered.SequenceEqual(list))
        {
            return false;
        }
        list.Clear();
        list.AddRange(ordered);
        return true;
    }

    /// <summary><paramref name="section"/>, or the start's where it is a value this build does not know.</summary>
    public static BarSection Known(BarSection section) => Enum.IsDefined(section) ? section : BarSection.Start;

    /// <summary><paramref name="edge"/>, or the bottom where it is a value this build does not know.</summary>
    public static Edge Known(Edge edge) => Enum.IsDefined(edge) ? edge : Edge.Bottom;

    /// <summary>The section <paramref name="island"/> stands in, in values this build knows.</summary>
    public static BarSection Of(BarItem? island) => Known(island?.Section ?? BarSection.Start);

    private static int Rank(BarItem? island) => (int)Of(island);

    /// <summary>
    /// The half-open range of <paramref name="list"/>, kept in its sections' order
    /// (<see cref="Order"/>), that holds <paramref name="section"/>'s entries; an empty section's is
    /// empty, at the place between the sections either side of it.
    /// </summary>
    public static (int From, int To) Range(IReadOnlyList<BarItem> list, BarSection section)
    {
        var rank = (int)Known(section);
        var from = list.Count(island => Rank(island) < rank);
        return (from, from + list.Count(island => Rank(island) == rank));
    }

    /// <summary>
    /// The section a place given only as <paramref name="index"/> in <paramref name="list"/> is in:
    /// the section of the entry standing there, or of the last entry for a place after it; the
    /// start's on an empty list.
    /// </summary>
    public static BarSection SectionAt(IReadOnlyList<BarItem> list, int index) =>
        list.Count == 0 ? BarSection.Start : Of(list[Math.Clamp(index, 0, list.Count - 1)]);

    /// <summary>
    /// Puts <paramref name="island"/> into <paramref name="list"/> at <paramref name="slot"/>: into its
    /// section, before the entry that has its index, and then the list in its sections' order again,
    /// so an index outside the section's own range lands at the section's nearer end. Returns where
    /// it stands. A widget moved to another section is one value changed on the entry, never an entry
    /// taken out of one list and put into another, so a merge keeps the other app's edit of it.
    /// </summary>
    public static int Put(List<BarItem> list, BarItem island, BarSlot slot)
    {
        island.Section = Known(slot.Section);
        list.Insert(Math.Clamp(slot.Index, 0, list.Count), island);
        Order(list);
        return list.IndexOf(island);
    }

    /// <summary>
    /// What stands on the bar, in its sections' order: a new list, keeping <paramref name="list"/>'s
    /// order within each section.
    /// </summary>
    public static List<BarItem> Along(IReadOnlyList<BarItem> list) =>
        [.. list.Where(island => island is not null)
            .Select((island, at) => (Island: island, At: at))
            .OrderBy(entry => (int)Known(entry.Island.Section))
            .ThenBy(entry => entry.At)
            .Select(entry => entry.Island)];

    /// <summary>A new taskbar entry of <paramref name="kind"/> with default options.</summary>
    public static BarItem New(this BarKind kind)
    {
        // Its options are Normalize's to write, the same place a widget read from a file is given
        // them, so a new one and a loaded one cannot start differently. It has no size to write: a
        // taskbar entry takes the room it needs (WidgetBounds.For).
        var item = new BarItem { Kind = kind };
        item.Normalize();
        return item;
    }

    /// <summary>
    /// A new line of <paramref name="kind"/> for a list folder, with default options, standing in a
    /// list, at the height a new line starts at (<see cref="LineHeight.StartFor"/>): automatic, up
    /// to its line's measurement.
    /// </summary>
    public static CellItem New(this ListKind kind)
    {
        var line = new CellItem { Kind = kind.Id(), View = FolderView.List };
        line.Normalize();
        // weather: a new line is for where the device is (WeatherOptions.New); Normalize's empty
        // record is what a file without one is read as.
        if (line.Weather is not null)
        {
            line.Weather = WeatherOptions.New();
        }
        return line;
    }

    /// <summary>
    /// Whether <paramref name="kind"/> may be added to <paramref name="list"/>: any taskbar entry
    /// (<see cref="BarEntries"/>), however many of it the list holds already.
    /// <paramref name="list"/> is taken for the shape of the call, which the catalogue and the
    /// tests use, and because a kind that stops being offered stops being addable.
    /// </summary>
    public static bool CanAdd(IReadOnlyList<BarItem> list, BarKind kind) => BarEntries.Find(kind) is not null;

    /// <summary>
    /// Puts <paramref name="island"/> into <paramref name="list"/> where it belongs when nobody
    /// said where — a deep link, a pin from an app's menu with no bar to say where. It goes into the
    /// section its kind belongs in (<see cref="SectionOf"/>), and inside that section after the last
    /// widget that comes no later in <see cref="BarEntries.All"/>, else at the section's start.
    /// Returns the index.
    /// </summary>
    public static int Insert(List<BarItem> list, BarItem island)
    {
        Order(list);
        island.Section = island.Kind.SectionOf();
        var (from, to) = Range(list, island.Section);
        var rank = Rank(island.Kind);
        var at = from;
        for (var index = from; index < to; index++)
        {
            if (Rank(list[index].Kind) <= rank)
            {
                at = index + 1;
            }
        }
        list.Insert(at, island);
        return at;
    }

    /// <summary>Where <paramref name="kind"/> stands in <see cref="BarEntries.All"/>'s order, or -1 for one this build does not know.</summary>
    private static int Rank(BarKind kind) => BarEntries.Find(kind) is not null ? (int)kind : -1;

    /// <summary>
    /// What a new bar shows, a new install's and a reset's: Windows 11's own taskbar as 24H2 and
    /// 25H2 show it out of the box (<see cref="WindowsTaskbar"/>), button by button and in Windows'
    /// order (2026-10-13, the person: "for the default layout when a user first starts karpo ...
    /// match the windows 11 default behaviour / layout as close as possible"), but at the left
    /// (2026-10-14: "the start should be on the left together with the app pins and app widget as
    /// a default. although this is not default windows behaviour. center should be empty by
    /// default"): Start, the search box, Task view, the apps pinned to Windows' taskbar (each a
    /// <see cref="BarKind.Pin"/>) and the open apps at the left, one run as Windows' are; nothing in
    /// the middle; and at the right the "^" and the tray, the pen menu and the touch
    /// keyboard where Windows would show them, the status icons and the input indicator, the quick
    /// settings button (the network, the sound and the battery on one button, quick settings inside:
    /// the quick settings preset), the clock button (the time over the date beside the
    /// bell, the notifications over the calendar inside: the notification center preset) and show
    /// desktop (§511: the two separators the person put there, 2026-10-16, went with the bar's separator).
    /// No Widgets button and no weather in its place (2026-10-14: "dont actually show the widgets
    /// button"): the weather preset and Windows' board are in the add list. Only a config without layouts is given it
    /// (<see cref="Config.FirstLayout"/>): an existing one keeps its own. <paramref name="pins"/> stands in for the
    /// taskbar's pins (<see cref="TaskbarPins"/>) when given, <paramref name="inputs"/> for what
    /// this PC has (<see cref="InputHardware.Read"/>), and <paramref name="windows"/> for what
    /// Windows' taskbar shows (<see cref="WindowsTaskbar.Read"/>).
    /// </summary>
    public static List<BarItem> DefaultLayout(IReadOnlyList<string>? pins = null, InputHardware? inputs = null, WindowsTaskbar? windows = null)
    {
        // Built in the three sections Windows 11's taskbar has, each laid out on its own
        // (docs\widgets.md §13), and in the order Windows shows each section's entries. Every
        // button of the vanilla taskbar but the Widgets button, whatever the person switched off in Windows' own settings
        // (2026-10-10: "everything of the vanilla taskbar in the default layout"); nothing of
        // Karpo's own that Windows has no button for (2026-10-05). The person's own arrangement
        // (2026-10-11) is the presets' and the preset layout's still (PersonsPresetsTests).
        var taskbar = windows ?? WindowsTaskbar.Read();
        var machine = new PresetMachine(HasBattery: taskbar.Battery, FixedDrives: [], Rotates: taskbar.Rotates);
        var list = new List<BarItem>();
        void Add(BarItem island, BarSection section)
        {
            island.Section = section;
            list.Add(island);
        }
        BarItem Preset(string id) => Presets.Folder(Presets.Find(id)!, machine);
        // Start, the search box (Windows 11's own default since 22H2), Task view, then the pins and
        // the open apps: Windows' order, one run with nothing between them, so one card on an
        // islands bar. At the left whatever Windows' own alignment, the middle left empty.
        Add(BarKind.Start.New(), BarSection.Start);
        var search = BarKind.Search.New();
        search.WindowsSearch = new WindowsSearchOptions { ShowBox = true };
        Add(search, BarSection.Start);
        Add(BarKind.TaskView.New(), BarSection.Start);
        foreach (var target in pins ?? TaskbarPins())
        {
            var pin = BarKind.Pin.New();
            pin.Pin!.Target = target;
            Add(pin, BarSection.Start);
        }
        Add(BarKind.Apps.New(), BarSection.Start);
        // The system corner, one run as Windows' is: the "^" and the tray's icons, then the pen menu
        // and the touch keyboard where Windows would show them: on a PC with a pen, and on one with
        // a touch screen (InputButtons).
        Add(BarKind.Tray.New(), BarSection.End);
        foreach (var input in InputButtons.ForNewBar(inputs ?? InputHardware.Read()))
        {
            Add(input, BarSection.End);
        }
        // The icons Windows shows there while something is so — a microphone or the location in
        // use, a restart waiting — which take no room while nothing is (docs\widgets.md §21).
        Add(BarKind.Indicators.New(), BarSection.End);
        // The input indicator ("ENG US") where Windows 11 keeps it, left of quick settings. Windows
        // shows its own only with more than one keyboard layout installed; Karpo's always shows, the
        // layout in use (KeyboardIsland), so every new bar has it (2026-10-10: "why is there the
        // keyboard selector not in the default layout?"). The keyboard preset (Presets.KeyboardId).
        Add(Preset(Presets.KeyboardId), BarSection.End);
        // Windows' quick settings button: the network, the speaker and the battery (on a PC with
        // one) side by side on one button, whose click opens quick settings — Wi-Fi, Bluetooth, the
        // other toggles, brightness, the volume and the battery (Presets.QuickPanelId).
        Add(Preset(Presets.QuickPanelId), BarSection.End);
        // Windows' clock button: the time over the date, the bell to their right, whose click opens
        // the notification center over the calendar (Presets.NotificationCenterId). The seconds
        // follow Windows' own clock (DateTimeFace.Windows). Then the sliver at the very end.
        Add(Preset(Presets.NotificationCenterId), BarSection.End);
        Add(BarKind.ShowDesktop.New(), BarSection.End);
        Order(list);
        return list;
    }

    /// <summary>
    /// The apps pinned to Windows' own taskbar, in the taskbar's order, for a new bar's pins:
    /// desktop apps by their shortcuts, and packaged apps, which are pinned without one, by their
    /// app id (<see cref="WindowsTaskbarPins.ReadForNewBar"/>) — none where the person unpinned
    /// them all, as Windows' taskbar then shows none. Where they can't be read at all, the pins
    /// Windows gives a new taskbar, those of them this PC has (<see cref="WindowsTaskbar.DefaultPins"/>).
    /// None while the config is isolated (tests and the dock's smoke run), so what a new bar holds
    /// doesn't depend on the PC.
    /// </summary>
    public static IReadOnlyList<string> TaskbarPins()
    {
        if (Config.Isolated is not null)
        {
            return [];
        }
        return WindowsTaskbarPins.ReadForNewBar() ?? WindowsTaskbar.DefaultPins(PinTargets.System);
    }

    /// <summary>Left and right edges lay their contents out top to bottom.</summary>
    public static bool IsVertical(this Edge edge) => edge is Edge.Left or Edge.Right;

    public static string Title(this Edge edge) => edge switch
    {
        Edge.Top => S.CoreEdgeTop,
        Edge.Left => S.CoreEdgeLeft,
        Edge.Right => S.CoreEdgeRight,
        _ => S.CoreEdgeBottom,
    };

    internal static int? ReadAdvanced(string value)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            return key?.GetValue(value) is int number ? number : null;
        }
        catch (Exception error) when (error is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>
/// drives: the one drive a <see cref="ListKind.Drive"/> or <see cref="ListKind.NetworkDrive"/> line
/// shows. Drive letters move — a stick that was E: comes back as F:, a share is remapped — so what
/// is kept is the volume's own durable name (<see cref="Volume"/>); the letter is only what the
/// file had before there was one, and the label is what to call the drive in the settings and on
/// the bar while it is not connected. A drive that isn't there is shown broken, never dropped, and
/// never changes the widget's size.
/// </summary>
public sealed class DriveTargetOptions
{
    /// <summary>
    /// The drive's durable name: a local volume's GUID path (<c>\\?\Volume{…}\</c>, which
    /// <c>GetVolumeNameForVolumeMountPointW</c> gives and which survives a new letter), or a share's
    /// UNC path (<c>\\server\share</c>). Empty in a hand-written file, and then <see cref="Letter"/>
    /// alone says which drive is meant.
    /// </summary>
    public string Volume { get; set; } = "";

    /// <summary>The drive letter it was last seen at, with its colon ("C:"); empty for none.</summary>
    public string Letter { get; set; } = "";

    /// <summary>What it is called ("Local Disk"), so a drive that isn't connected still has a name.</summary>
    public string Label { get; set; } = "";

    /// <summary>Whether a drive has been chosen at all; an unset widget is shown broken.</summary>
    [JsonIgnore]
    public bool IsSet => Volume.Length > 0 || Letter.Length > 0;
}

/// <summary>
/// perf: what the five performance graphs are set to. One options class for all of them, the way
/// the clock and the date share one: they are one widget with five readings behind it, and a
/// setting that meant something different per kind would be a different class.
/// <para>
/// Only <see cref="ShowGraph"/> changes a widget's <b>size</b>; the rest change what is drawn into
/// the room the size already gave (docs\widgets.md §4). That is why the interval can be anything
/// and the bar never moves.
/// </para>
/// </summary>
public sealed class PerformanceOptions
{
    /// <summary>
    /// Whether the reading is drawn over a graph of the last little while, or stands alone as a
    /// figure. It is the one setting the shape reads, so switching it resizes the widget once, on
    /// purpose, rather than while somebody is looking at it. Chosen as a style since §228: the
    /// performance widget's "Numbers" is its graph's form with this off (WidgetStyles.ChooseLook).
    /// </summary>
    public bool ShowGraph { get; set; } = true;

    /// <summary>
    /// How often the widget steps, one of <see cref="PerformanceChoices.Paces"/> in seconds (one to
    /// sixty; any other number reads as the nearest, §228). One second is Task Manager's own pace and the
    /// default. The reading itself is taken every second whatever this says (the dock's one source
    /// per reading); a slower widget draws a step every so many seconds, each the mean of them, so
    /// its graph spans a longer stretch. It saves nothing.
    /// </summary>
    public int Seconds { get; set; } = 1;

    /// <summary>
    /// Which graphics adapter a <see cref="WidgetInfos.Gpu"/> widget follows, as "GPU 0: &lt;name&gt;";
    /// empty means the main one, the card with the most video memory of its own (GpuSampler.MainOf).
    /// One adapter always, never all of them together.
    /// Means nothing for the other four kinds.
    /// </summary>
    public string GpuAdapter { get; set; } = "";

    /// <summary>
    /// How an adapter is named in the settings file and matched back to a reading: "GPU 0: Intel
    /// UHD 770", as Task Manager names them. Two adapters of one model are told apart by the
    /// number, which is why the name on its own would not do, and the number is the position in
    /// the bar's own list of adapters rather than anything the driver says.
    ///
    /// It lives here because the bar writes these names and the settings card offers them, the two
    /// run in different processes, and a name written by one and not recognised by the other is a
    /// widget that shows nothing and never says why.
    /// </summary>
    public static string AdapterName(int index, string name) => $"GPU {index}: {name}"; // not-ui: a name the config stores, as Task Manager writes it

    /// <summary>
    /// Which physical disk a <see cref="WidgetInfos.Disk"/> widget follows, as "Disk 0", Task
    /// Manager's own name for it; null means every disk together — the busiest one's active time,
    /// and what all of them read and write — which on a PC with one disk is that disk. Means
    /// nothing for the other four kinds, and is not written for them.
    /// <para>
    /// The number and not the letters: "Disk 0 (C:)" is what the settings card shows, but the
    /// letters on a disk change as volumes are added and removed, and the number is what
    /// <c>\\.\PhysicalDrive0</c> is.
    /// </para>
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Disk { get; set; }

    /// <summary>A disk as the settings file names it: "Disk 0".</summary>
    public static string DiskName(int number) => $"Disk {number}"; // not-ui: the word the config stores

    /// <summary>
    /// A disk as Task Manager names it, with the letters of the volumes on it: "Disk 0 (C: D:)",
    /// and "Disk 1" alone for a disk that has no lettered volume.
    /// </summary>
    public static string DiskLabel(int number, IEnumerable<string> letters)
    {
        var shown = string.Join(" ", letters.Where(letter => letter.Length > 0));
        return shown.Length > 0 ? $"{DiskName(number)} ({shown})" : DiskName(number);
    }

    /// <summary>
    /// The disk number a name or a label means — "Disk 0", "disk 2", "Disk 1 (E:)" — or null for
    /// anything else, which reads as every disk together rather than as a disk nobody has.
    /// </summary>
    public static int? DiskNumber(string? name)
    {
        var text = (name ?? "").Trim();
        if (!text.StartsWith("Disk ", StringComparison.OrdinalIgnoreCase)) // not-ui: the config's word
        {
            return null;
        }
        var digits = text[5..].TrimStart();
        var end = 0;
        while (end < digits.Length && char.IsAsciiDigit(digits[end]))
        {
            end++;
        }
        if (end == 0 || end > 3 || (end < digits.Length && digits[end] != ' '))
        {
            return null;
        }
        return int.Parse(digits[..end], System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// The weather island. Nothing is fetched until a place is set or device
/// location is on, which a new weather widget's is (<see cref="New"/>), and
/// then only while it is looked at. Data comes from Open-Meteo, which needs no
/// account or key.
/// </summary>
public sealed class WeatherOptions
{
    /// <summary>A place name as the user typed it, shown in the tooltip; empty = not set.</summary>
    public string Place { get; set; } = "";

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Ask Windows for the device's location instead (needs location access for desktop apps).</summary>
    public bool UseDeviceLocation { get; set; }

    // The scale it shows temperatures in is Localization's (LocalizationOptions.Temperature), and
    // how often it asks again is every online widget's (Config.OnlineUpdateMinutes).

    /// <summary>
    /// air: the index an air-quality line shows (<see cref="AirIndexes"/>), "european" or "us"; null,
    /// and not written, for the place's region's — and on every other kind, which shows no index
    /// (docs\widgets.md §210). The air-quality face keeps its own among its options instead.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AirIndex { get; set; }

    /// <summary>
    /// quakes: an earthquake line's radius, least magnitude and alert (<see cref="QuakeOptions"/>);
    /// null, and not written, where all three are their defaults — and on every other kind
    /// (docs\widgets.md §240). The earthquake face keeps its own among its options instead.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public QuakeOptions? Quakes { get; set; }

    public bool IsSet => UseDeviceLocation || (Latitude is not null && Longitude is not null);

    /// <summary>
    /// A new weather widget's place: where the device is (the person, 2026-10-11: "as default in
    /// weather widgets take current location"). Only what is made new: a record read from a file
    /// keeps what it says, and one with no "useDeviceLocation" in it is off, as it always was.
    /// </summary>
    public static WeatherOptions New() => new() { UseDeviceLocation = true };
}

/// <summary>
/// The calendar's views, as Windows' and Outlook's calendars have them. They were the calendar
/// flyout's, one of them the widget's setting; since the calendar became a preset folder
/// (2026-10-09) each is a line of its own (the month, and the day, week and year views), and this
/// names which one a line draws and how its keys move through it. Kept in the file only where an
/// older one wrote it (<see cref="ClockOptions.View"/>).
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<CalendarView>))]
public enum CalendarView
{
    /// <summary>One day's hours, with its events in them.</summary>
    [JsonStringEnumMemberName("day")] Day,

    /// <summary>A week's days side by side, with their hours.</summary>
    [JsonStringEnumMemberName("week")] Week,

    /// <summary>The month's grid, and the selected day's events under it: what Windows' own calendar shows.</summary>
    [JsonStringEnumMemberName("month")] Month,

    /// <summary>The year's twelve small months.</summary>
    [JsonStringEnumMemberName("year")] Year,
}

/// <summary>
/// The calendar's agenda in a folder: today and the events coming. The events come from the
/// person's accounts, which Karpo reads itself and keeps apart from the config (docs\accounts.md),
/// so there is nothing here that names one.
/// <para>
/// It was the calendar's taskbar entry's too, with the view its flyout opened in and the zone its
/// date was read in, until the calendar became a preset folder wearing the calendar face
/// (2026-10-09); both went with the entry.
/// </para>
/// <para>
/// Three settings went on 2026-10-05, the person's words: "show events for shoudl not be a
/// configuration option. it should list all of them if the view shows the date for it", "there
/// should not be a label setting for the calendar", "there should not be a calendar size option".
/// How far ahead the agenda lists came back as its own setting on 2026-11-03 (<see cref="DaysAhead"/>, §496): "for
/// the agenda you should be able to set how many days into the future it should show events".
/// </para>
/// </summary>
public sealed class CalendarOptions
{
    /// <summary>
    /// <b>Retired, and deliberately still here.</b> The iCalendar (.ics) address Karpo used to
    /// download itself, before calendars came from accounts.
    /// </summary>
    /// <remarks>
    /// <b>Nothing in Karpo reads this.</b> The one place that touches it is the widget's settings
    /// page, which reads it only to say it is there and to put it on the clipboard; no widget, no
    /// store and no view asks for it, and <c>Repair</c> does not rewrite it. If you came here
    /// wondering why a live-looking property has no reader, that is why.
    /// <para>
    /// It is kept, and written back out, because it is often a secret address a person cannot get
    /// again, and a Karpo that quietly dropped it from their file would be taking it from them.
    /// The settings card says it is no longer read, offers to copy it and has a button that
    /// clears it. <b>Only the person clears
    /// it.</b> There is no migration and nothing to migrate: no value moves, and a file that has
    /// been through this build is the same file it was.
    /// </para>
    /// </remarks>
    public string FeedUrl { get; set; } = "";

    /// <summary>
    /// <b>Retired</b> with the calendar's badge on the bar (2026-09-27), and read by nothing: whether
    /// the badge had the next event beside it. The bar shows the date now, and a card and a line
    /// show the events whatever this says. Kept, and written back, so the file round-trips exactly
    /// as it was.
    /// </summary>
    public bool ShowNextEvent { get; set; } = true;

    /// <summary>
    /// <b>Retired</b> with <see cref="FeedUrl"/>, and read by nothing: how often that feed was
    /// downloaded again. The accounts are read on Karpo's own clock, so there is nothing left to set.
    /// Kept, unclamped and unrewritten, so the file round-trips exactly as it was.
    /// </summary>
    public int RefreshMinutes { get; set; } = 30;

    /// <summary>
    /// §496: how many days the agenda lists events of, today the first ("Days ahead"); null for
    /// <see cref="DaysAheadDefault"/>, which is not written. Between <see cref="DaysAheadFewest"/> and
    /// <see cref="DaysAheadMost"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DaysAhead { get; set; }

    /// <summary>§496: what an agenda nobody set lists: today and the six days after, a week, as it always did.</summary>
    public const int DaysAheadDefault = 7;

    /// <summary>§496: the least, today alone.</summary>
    public const int DaysAheadFewest = 1;

    /// <summary>
    /// §496: the most, about two months: well inside what the calendars are read and kept for (PimCache.Window, last
    /// month to a year ahead), so every day the agenda may list is already read.
    /// </summary>
    public const int DaysAheadMost = 60;

    /// <summary>§496: <paramref name="days"/> as it is kept: held to the range, null for the default.</summary>
    public static int? KeptDaysAhead(int? days) =>
        days is { } some && Math.Clamp(some, DaysAheadFewest, DaysAheadMost) is var held && held != DaysAheadDefault ? held : null;

    /// <summary>§496: how many days <paramref name="item"/>'s agenda lists, today the first.</summary>
    public static int DaysAheadOf(WidgetItem? item) =>
        Math.Clamp(item?.Calendar?.DaysAhead ?? DaysAheadDefault, DaysAheadFewest, DaysAheadMost);
}

/// <summary>
/// A clock's line in a list folder, in a time zone of its own. The bar's clock is a folder's
/// clock face (<see cref="FolderFaces.Clock"/>), whose options are the face's.
/// </summary>
public sealed class ClockOptions
{
    /// <summary>Whether it shows the seconds.</summary>
    public bool Seconds { get; set; }

    /// <summary>A Windows time zone id ("Pacific Standard Time"); empty = this PC's.</summary>
    public string TimeZone { get; set; } = "";

    /// <summary>Shown after the time when set, such as a city ("Tokyo").</summary>
    public string Label { get; set; } = "";

    /// <summary>
    /// §443: how its line shows the time (<see cref="ClockStyles"/>): the time and the date beside a face, which a file
    /// that names none is and which is not written; the time alone; the hours over the minutes; the face alone.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ClockStyle Style { get; set; }

    /// <summary>
    /// <b>Retired</b> with the date entry (2026-09-27), and read by nothing: the view the calendar
    /// opened in for a date, when the date kept its options here; a clock never read it. Kept, and
    /// written back, so a file round-trips exactly as it was.
    /// </summary>
    public CalendarView View { get; set; } = CalendarView.Month;
}

/// <summary>
/// calendar: a timer's or a focus session's own settings — the length it starts at and its name —
/// on the taskbar and in a list alike (<see cref="WidgetInfos.Timer"/>,
/// <see cref="WidgetInfos.Focus"/>). Each widget has its own, so two timers are two lengths: "you
/// should be able to add many different timers with their own default times" (2026-09-27). What is
/// running is no setting and is not here: it is kept in memory, one countdown per widget, and a
/// restart clears it. A widget nobody set has none (<see cref="WidgetItem.Countdown"/> is null) and
/// starts at its kind's length; the record is made the first time a length or a name is set.
/// </summary>
public sealed class CountdownOptions
{
    /// <summary>The shortest and the longest a length may be, in minutes: a minute to twelve hours.</summary>
    public const int FewestMinutes = 1;

    public const int MostMinutes = 12 * 60;

    /// <summary>What a new timer and a new focus session start at, in minutes.</summary>
    public const int TimerMinutes = 5;

    public const int FocusMinutes = 25;

    /// <summary>
    /// How long it runs when it is started, in whole minutes (<see cref="FewestMinutes"/> to
    /// <see cref="MostMinutes"/>). Its card's steppers and the wheel set it too, and it is written
    /// once they have settled.
    /// </summary>
    public int Minutes { get; set; }

    /// <summary>
    /// Its own name ("Tea", "Deep work"), shown where its face has room and when it runs out; empty
    /// for its kind's ("Timer", "Focus time").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// A focus session's "Turn on do not disturb": Windows' do not disturb is on while it runs and
    /// handed back when it ends, as Windows 11's own Focus does. On unless somebody turned it off, so
    /// null is on and only false is written. A timer has none.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DoNotDisturb { get; set; }

    /// <summary>
    /// <paramref name="minutes"/> as a length may be: <paramref name="fallback"/> for none or for one
    /// that makes no sense (nought or less), and never more than twelve hours.
    /// </summary>
    public static int Clamp(int? minutes, int fallback) =>
        minutes is { } chosen && chosen >= FewestMinutes ? Math.Min(chosen, MostMinutes) : fallback;
}

/// <summary>The desktops' line (<see cref="WidgetInfos.Desktops"/>), the desktop switcher's list until 2026-10-11.</summary>
public sealed class DesktopOptions
{
    /// <summary>
    /// The icons of the apps open on each desktop, at the end of its row. They were on the buttons
    /// themselves while the widget was a button per desktop, and in the switcher's flyout after.
    /// </summary>
    public bool ShowApps { get; set; } = true;
}

/// <summary>
/// A one-button widget that switches something on or off, or opens what Windows shows for it
/// (airplane mode, the mobile hotspot, energy saver, live captions, project, cast, night light,
/// nearby sharing, accessibility).
/// </summary>
public sealed class SwitchOptions
{
    /// <summary>The widget's name beside its glyph, as Windows 11's quick-settings tiles have it.</summary>
    public bool ShowLabel { get; set; }

    /// <summary>
    /// Spatial sound's alone: the format a click turns it on in, as Windows writes it ("{…}", one of
    /// <see cref="SpatialSoundFormats.Known"/> or another), or null for the one last used here — Windows
    /// Sonic until one has been. Where the output does not offer it, the click turns on that one.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SpatialFormat { get; set; }

    /// <summary>Color filters' alone: the filter a click turns them on in; grayscale, which is not written, unless another.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ColorFilter ColorFilter { get; set; }

    /// <summary>
    /// Keep awake's alone (§269): how long a click keeps the PC awake, in minutes, one of
    /// <see cref="KeepAwakeLengths.All"/>; 0, which is not written, until it is turned off.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int AwakeMinutes { get; set; }
}

/// <summary>
/// How long keep awake keeps the PC awake (<see cref="SwitchOptions.AwakeMinutes"/>), as PowerToys
/// Awake offers it: until it is turned off, or half an hour, an hour, two or four; in minutes, 0 for
/// until it is turned off. Its widget's settings and its menu's rows both offer these.
/// </summary>
public static class KeepAwakeLengths
{
    /// <summary>Until turned off, then the shortest first.</summary>
    public static readonly int[] All = [0, 30, 60, 120, 240];

    /// <summary><paramref name="minutes"/> where it is one of <see cref="All"/>; until turned off for anything else (a newer file's, a hand-written one's).</summary>
    public static int Known(int minutes) => Array.IndexOf(All, minutes) >= 0 ? minutes : 0;

    /// <summary>What a row says for <paramref name="minutes"/>: "Until turned off", "For 30 minutes", "For 2 hours".</summary>
    public static string Name(int minutes) => minutes switch
    {
        30 => S.DockQuickKeepAwakeFor30Minutes,
        60 => S.DockQuickKeepAwakeFor1Hour,
        120 => S.DockQuickKeepAwakeFor2Hours,
        240 => S.DockQuickKeepAwakeFor4Hours,
        _ => S.DockQuickKeepAwakeUntilOff,
    };

    /// <summary>The icon of its row (§249): the widget's cup for until turned off, then a clock a step longer each.</summary>
    public static string GlyphOf(int minutes) => minutes switch
    {
        30 => OptionGlyphs.Seconds,
        60 => OptionGlyphs.Time,
        120 => OptionGlyphs.Clock,
        240 => OptionGlyphs.History,
        _ => WidgetInfos.KeepAwake.Glyph,
    };
}

/// <summary>
/// Windows' color filters (<see cref="SwitchOptions.ColorFilter"/>), each the number Windows keeps in
/// HKCU\Software\Microsoft\ColorFiltering's FilterType for it.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<ColorFilter>))]
public enum ColorFilter
{
    [JsonStringEnumMemberName("grayscale")] Grayscale = 0,
    [JsonStringEnumMemberName("inverted")] Inverted = 1,
    [JsonStringEnumMemberName("grayscaleInverted")] GrayscaleInverted = 2,
    [JsonStringEnumMemberName("deuteranopia")] Deuteranopia = 3,
    [JsonStringEnumMemberName("protanopia")] Protanopia = 4,
    [JsonStringEnumMemberName("tritanopia")] Tritanopia = 5,
}

/// <summary>The color filters as Windows' Settings lists them: what color filters' Settings card offers.</summary>
public static class ColorFilterChoices
{
    /// <summary>Settings' order: the three for color blindness, then grayscale, grayscale inverted and inverted.</summary>
    public static readonly ColorFilter[] All =
    [
        ColorFilter.Deuteranopia, ColorFilter.Protanopia, ColorFilter.Tritanopia,
        ColorFilter.Grayscale, ColorFilter.GrayscaleInverted, ColorFilter.Inverted,
    ];

    /// <summary><paramref name="filter"/> where it is one of Windows' filters; grayscale for anything else (a newer file's, an unknown name's).</summary>
    public static ColorFilter Known(ColorFilter filter) => Enum.IsDefined(filter) ? filter : ColorFilter.Grayscale;
}

/// <summary>
/// The spatial sound formats Windows knows by name, which spatial sound's Settings card offers as the
/// one to turn on (<see cref="SwitchOptions.SpatialFormat"/>): Windows' own, and the Dolby and DTS
/// apps' as their GUIDs are written in an output's list of formats (read on Windows 11 25H2). Their
/// names are the products', in every language.
/// </summary>
public static class SpatialSoundFormats
{
    /// <summary>Windows Sonic for Headphones, which ships with Windows.</summary>
    public const string WindowsSonic = "{B53D940C-B846-4831-9F76-D102B9B725A0}";

    /// <summary>The formats, Windows' own first.</summary>
    public static readonly (string Id, string Name)[] Known =
    [
        (WindowsSonic, "Windows Sonic for Headphones"), // not-ui: a product's name
        ("{1459AC38-3875-49BF-BB59-0FE80F4D395D}", "Dolby Atmos for Headphones"), // not-ui: a product's name
        ("{4C81E564-C8EF-4AD9-9F2C-9EF995533790}", "Dolby Atmos for Speakers"), // not-ui: a product's name
        ("{4444ACB0-8DC0-4C2C-A0D8-2C76DB470F86}", "DTS Headphone:X"), // not-ui: a product's name
        ("{ADAFD3C6-AC4C-404A-836A-9615E9060564}", "DTS:X Ultra"), // not-ui: a product's name
    ];
}

/// <summary>
/// §435: one divider in a folder's list of its own (<see cref="CellItem.Divider"/>, the file's "divider"): Grow where somebody
/// chose it. Absent means a divider's fixed room (§505: every separator is empty space); only Grow is ever written.
/// </summary>
public sealed class DividerOptions
{
    public DividerStyle Style { get; set; }

    /// <summary>These options, or null — the options dropped — where the fixed room is chosen.</summary>
    public DividerOptions? Normalized() => Style == DividerStyle.Grow ? this : null;
}

/// <summary>§435, §505: how much room one divider in a folder's list takes: its fixed room, or a gap that grows.</summary>
[JsonConverter(typeof(TolerantEnumConverter<DividerStyle>))]
public enum DividerStyle
{
    /// <summary>The room every divider keeps, empty (§505): the default.</summary>
    [JsonStringEnumMemberName("fixed")] Fixed,

    /// <summary>
    /// The fixed room made as tall as the room its column has free: the lines after it at the column's foot, its
    /// column parted into two sections, several sharing the room evenly, and Fill lines with them (docs\widgets.md §435).
    /// </summary>
    [JsonStringEnumMemberName("grow")] Grow,
}

public sealed class VolumeOptions
{
    /// <summary>The shortest and longest the slider may be, in DIPs.</summary>
    public const int ShortestDip = 48;

    public const int LongestDip = 400;

    /// <summary>Which way the slider runs; Auto follows the bar's edge.</summary>
    public SliderOrientation Orientation { get; set; } = SliderOrientation.Auto;

    /// <summary>How long the slider itself is, in DIPs (the speaker comes before it).</summary>
    public int Length { get; set; } = 120;

    /// <summary>The speaker that mutes and unmutes, before the slider.</summary>
    public bool ShowSpeaker { get; set; } = true;
}

/// <summary>quick: which displays a brightness widget's one slider sets.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BrightnessScreens>))]
public enum BrightnessScreens
{
    /// <summary>Every display whose brightness can be set, to the same level: what a widget nobody set does.</summary>
    [JsonStringEnumMemberName("all")] All,

    /// <summary>The display the widget is on alone: its bar's, or for a folder's widget the bar the folder belongs to.</summary>
    [JsonStringEnumMemberName("own")] Own,
}

/// <summary>
/// quick: the brightness widget's own setting, which displays its slider dims ("it should dimm the
/// screens brigness together with one slider. optionally you can set it so that it only dimms the
/// brigtness of the current screen"). Every display is no options at all, so a widget nobody set
/// writes nothing, and a file from before this reads and writes as it did.
/// </summary>
public sealed class BrightnessOptions
{
    public BrightnessScreens Screens { get; set; }

    /// <summary>These options, or null — the options dropped — where every display is chosen.</summary>
    public BrightnessOptions? Normalized() => Screens == BrightnessScreens.Own ? this : null;
}

/// <summary>
/// A pin: one app, file, folder or link, on the bar as a pinned app is on the taskbar, or in a
/// folder. What it opens is <see cref="Target"/>; the rest only changes how it looks. §380: a
/// folder's contents (<see cref="WidgetInfos.FolderContents"/>) point at their folder with the same
/// record, and only they read <see cref="Navigation"/>: a pin opens what it points at and nothing
/// more (<see cref="PinFolders"/>).
/// </summary>
public sealed class PinOptions
{
    /// <summary>
    /// What it opens, as the shell takes it: a program, a shortcut, a file or a folder by its
    /// path; an app by its ID in the Apps folder ("shell:AppsFolder\&lt;AUMID&gt;"); a web address
    /// or any other link. Empty until one is chosen. A target this PC doesn't have is kept, and
    /// the bar shows it as broken (<see cref="PinTargets.Check"/>).
    /// </summary>
    public string Target { get; set; } = "";

    /// <summary>Its name on the bar and in folders; empty = the name Windows gives the target.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// What it is drawn with instead of the target's own icon: a Segoe Fluent Icons glyph (one
    /// character), or a file whose icon is taken (an icon file, a program, a shortcut); empty =
    /// the target's own.
    /// </summary>
    public string Icon { get; set; } = "";

    /// <summary>Whether <see cref="Icon"/> names a glyph: one character of the icon font's private use area.</summary>
    [JsonIgnore]
    public bool IconIsGlyph => Icon.Length == 1 && Icon[0] is >= '\uE000' and <= '\uF8FF';

    /// <summary>
    /// folder contents: what a click on a subfolder does (<see cref="PinFolderNavigation"/>): open it in
    /// File Explorer, or show its contents in place of its parent's, as deep as they go
    /// (<see cref="PinFolders.NavigationOf"/>). Never a pin's, whose folder always opens in File
    /// Explorer; not written while it is <see cref="PinFolderNavigation.Explorer"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public PinFolderNavigation Navigation { get; set; }

    /// <summary>
    /// folder contents (§498): how a line of a list lays its items out, as File Explorer's View
    /// (<see cref="PinFolderView"/>). Never a pin's; not written while it is <see cref="PinFolderView.List"/>, what every
    /// line was before it.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public PinFolderView View { get; set; }
}

/// <summary>
/// list: a heading's own words (<see cref="WidgetInfos.Heading"/>): the title of the lines under it,
/// as a group header of Windows' Settings titles the cards under it.
/// </summary>
public sealed class HeadingOptions
{
    /// <summary>The most a heading says, in characters: a title, not a paragraph.</summary>
    public const int MostLength = 60;

    /// <summary>What it says; empty says its kind's name, "Heading", in the language Karpo speaks, until somebody sets it.</summary>
    public string Text { get; set; } = "";
}

/// <summary>
/// notes: which to-do list a to-do line shows: one of the notebook's (<see cref="Notebook"/>), on this
/// PC, or a Microsoft account's To Do list (docs\accounts.md, "To Do").
/// </summary>
public sealed class TodoOptions
{
    /// <summary>The notebook list's id; empty, or one that has gone, shows the first list.</summary>
    public string List { get; set; } = "";

    /// <summary>
    /// The Microsoft account whose To Do list it shows, by its address — which signing in to it again
    /// keeps, where its id would not; null (left out of the file, as every line before 2026-10-16) for
    /// the notebook's list on this PC.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Account { get; set; }

    /// <summary>That account's To Do list, by Graph's id; null, or one that has gone, shows its default "Tasks".</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TaskList { get; set; }

    /// <summary>Whether it shows a Microsoft To Do list rather than the notebook's.</summary>
    [JsonIgnore]
    public bool IsToDo => !string.IsNullOrEmpty(Account);
}

/// <summary>notes: which of the notebook's notes a note line shows (<see cref="Notebook"/>).</summary>
public sealed class NoteOptions
{
    /// <summary>The note's id; empty, or one that has gone, shows the first note.</summary>
    public string Note { get; set; } = "";
}

/// <summary>
/// One display: whether Karpo is on it, and which named layout it shows (TaskbarLayouts.cs). Displays
/// are told apart by <see cref="Id"/>, see <see cref="Monitors"/>. A display Karpo sees for the first
/// time shows the default layout (<see cref="Config.AddDisplay"/>).
/// </summary>
public sealed class Screen
{
    public string Id { get; set; } = "";

    /// <summary>The display's friendly name when it was last seen, so the file is readable.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Whether Karpo shows on it: false is the layout "None", Karpo hidden there. At least one
    /// attached display always shows it (<see cref="Config.CanHide"/>).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The named layout (<see cref="Config.Layouts"/>) it shows; null or stale is the default one.</summary>
    public string? ActiveLayoutId { get; set; }

    /// <summary>
    /// The edge of this display its layout's bar stands on: any of the four, whatever layout it shows
    /// (the position is the display's, not the layout's). A word this build does not know reads as the
    /// bottom (<see cref="Config.Repair"/>).
    /// </summary>
    [JsonConverter(typeof(TolerantEnumConverter<Edge>))]
    public Edge Edge { get; set; } = Edge.Bottom;
}

public sealed partial class Config
{
    /// <summary>
    /// The shape this file is written in. A Karpo that reads a file from the future leaves it alone
    /// rather than saving its own, narrower idea of it over the top (<see cref="FromNewerKarpo"/>).
    /// There is no older shape to read: the file is today's or a newer build's.
    /// </summary>
    public const int Shape = 89;

    /// <summary>The <see cref="Shape"/> the file was written in; a file with none is this build's.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = Shape;

    /// <summary>A file from a newer Karpo: read, shown, never written (<see cref="Save"/> refuses).</summary>
    [JsonIgnore]
    public bool FromNewerKarpo => Version > Shape;

    /// <summary>
    /// Which of the two looks a bar is laid out in (<see cref="BarStyle"/>): the look of the edge
    /// it stands on wears (<see cref="EdgeBar.Style"/>, its layout's), in the copy of this config a bar
    /// is laid out with (<see cref="WithEdge"/>): a dock floats, a bar wears islands. Never in the file.
    /// Null in a config made in code, which wears <see cref="DefaultStyle"/>.
    /// </summary>
    [JsonIgnore]
    public BarStyle? Style { get; set; }

    /// <summary>Below this a bar is thin: its text, cell, grip and desktop buttons follow the room. <c>Layout.ThinDip</c> is this. (Its gaps and corners are the layout's spacing's, at any height, since §533.)</summary>
    public const int ThinHeight = 36;

    /// <summary>The room a bar keeps above and below its icons. <c>Layout.IconMarginDip</c> is this.</summary>
    public const int IconMargin = 4;

    /// <summary>
    /// The bar's thickness in DIPs as the layout works it out: the height somebody chose, or what
    /// the icon needs when that is more. Asked here rather than worked out twice, so the dock's
    /// answer and <see cref="Repair"/>'s cannot drift apart. Never in the file: it is a bar's, laid
    /// out along one edge (<see cref="WithEdge"/>).
    /// </summary>
    [JsonIgnore]
    public float BarThickness =>
        MathF.Max(Math.Clamp(Height > 0 ? Height : 48, MinHeight, MaxHeight), IconDips() + (2f * IconMargin));

    /// <summary>
    /// The look a bar laid out with no edge of its own wears, as a config made in code is: a bar's,
    /// islands (the person, 2026-10-08: "bars are Islands").
    /// </summary>
    public const BarStyle DefaultStyle = BarStyle.Islands;

    /// <summary>
    /// The look this bar wears: its edge's (<see cref="WithEdge"/>), or <see cref="DefaultStyle"/> in
    /// a config made in code that says none. Never in the file, as <see cref="Style"/> isn't.
    /// </summary>
    [JsonIgnore]
    public BarStyle StyleOrDefault => Style ?? DefaultStyle;

    /// <summary>
    /// The thickness in DIPs a bar is laid out at, <see cref="MinHeight"/> to <see cref="MaxHeight"/>:
    /// the size of the edge it stands on (<see cref="EdgeBar.Size"/>), in the copy of this config a
    /// bar is laid out with (<see cref="WithEdge"/>): its layout's (<see cref="TaskbarLayout.Size"/>).
    /// Never in the file at the top level. It never goes below what the icon size needs.
    /// </summary>
    [JsonIgnore]
    public int Height { get; set; } = DefaultHeight;

    /// <summary>The thickness of a bar nobody sized: 48, the Windows 11 taskbar's.</summary>
    public const int DefaultHeight = 48;

    /// <summary>
    /// §345: the layout whose bar this copy of the config is laid out as (<see cref="WithEdge"/>, the
    /// layout's <see cref="EdgeBar.Of"/>), whose look and app buttons it wears. Never in the file. Null in
    /// the person's own config, and in a copy laid out for no layout's bar.
    /// </summary>
    [JsonIgnore]
    public TaskbarLayout? Bar { get; private set; }

    /// <summary>§345: the layout a bar laid out with this config wears: <see cref="Bar"/>, or the default layout in a config that is no bar's (one made in code).</summary>
    [JsonIgnore]
    public TaskbarLayout BarLayout => Bar ?? DefaultLayout;

    /// <summary>
    /// The colours lately mixed in a custom colour dialog, newest first, as Windows' own Colors page
    /// keeps its "Recent colors": "#AARRGGBB" each, for any colour, either mode and either surface
    /// (<see cref="ColorPalette.Recent"/>). The bar draws none of them. Null while there are none.
    /// </summary>
    [JsonPropertyName("recentColors"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(ColorListConverter))]
    public List<string>? RecentColors { get; set; }

    /// <summary>
    /// calendar: how Karpo writes dates, times and units (<see cref="LocalizationOptions"/>). Every
    /// one of them follows Windows' regional format until somebody sets it here, which is what
    /// Karpo did before the setting existed, so an older file reads as following everything.
    /// </summary>
    public LocalizationOptions Formats { get; set; } = new();

    /// <summary>
    /// The language Karpo speaks: a code of <see cref="Languages.All"/> ("en", "de", "pt-BR",
    /// "zh-Hant" …), or the hidden "qps-ploc".
    /// Null, and left out of the file, follows Windows' display language (<see cref="Languages"/>).
    /// A code this build has no table for — one a newer Karpo offers — is kept as it is and reads
    /// as following Windows until that build is back.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; set; }

    /// <summary>
    /// How many minutes apart everything online updates: the weather, the stocks, and the
    /// accounts' calendars, mail and contacts, each held to its own provider's floor
    /// (<see cref="OnlineUpdates"/>). One setting for all of them, on the General page; none of
    /// them has one of its own. <see cref="OnlineUpdates.DefaultMinutes"/> in a new config.
    /// </summary>
    public int OnlineUpdateMinutes { get; set; } = OnlineUpdates.DefaultMinutes;

    /// <summary>
    /// The engine every web search Karpo opens searches with — a click on the weather, and anything
    /// else that searches the web (<see cref="SearchUrls.Search(Config, string)"/>). One setting for
    /// the whole app, on the General page (2026-10-11: "not per widget anymore"). Null, and left out
    /// of the file, until somebody chooses one: <see cref="SearchUrls.Default"/>, Google.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SearchEngine? SearchEngine { get; set; }

    /// <summary>The engine searches go to: <see cref="SearchEngine"/>, or <see cref="SearchUrls.Default"/> while there is none.</summary>
    [JsonIgnore]
    public SearchEngine SearchEngineOrDefault => SearchEngine ?? SearchUrls.Default;

    /// <summary>
    /// The address <see cref="Core.SearchEngine.Custom"/> searches with, with <see cref="SearchUrls.Placeholder"/>
    /// where the query goes; left out of the file while there is none.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SearchCustomUrl { get; set; }

    /// <summary>
    /// The map every "Open on map" Karpo offers opens a place on — the tides', an earthquake's
    /// (<see cref="LinkProviders.Place(double, double, int, string?)"/>, §319). One setting for the whole app, under
    /// Links on the General page. Null, and left out of the file, until somebody chooses one:
    /// <see cref="LinkProviders.DefaultMap"/>, OpenStreetMap, what Karpo always opened.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MapProvider? MapProvider { get; set; }

    /// <summary>The map places open on: <see cref="MapProvider"/>, or <see cref="LinkProviders.DefaultMap"/> while there is none.</summary>
    [JsonIgnore]
    public MapProvider MapProviderOrDefault => MapProvider ?? LinkProviders.DefaultMap;

    /// <summary>
    /// The weather site "Open forecast" opens a place's forecast on — the weather's, the air quality's
    /// (<see cref="LinkProviders.Forecast(double, double, bool)"/>, §322). Null, and left out of the file, until
    /// somebody chooses one: <see cref="LinkProviders.DefaultWeather"/>, weather.com, what Karpo always opened.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WeatherProvider? WeatherProvider { get; set; }

    /// <summary>The weather site forecasts open on: <see cref="WeatherProvider"/>, or <see cref="LinkProviders.DefaultWeather"/> while there is none.</summary>
    [JsonIgnore]
    public WeatherProvider WeatherProviderOrDefault => WeatherProvider ?? LinkProviders.DefaultWeather;

    /// <summary>
    /// The finance site a share's, a rate's or a coin's "Open website" opens on (<see cref="LinkProviders.Quote(string?)"/>,
    /// §322). Null, and left out of the file, until somebody chooses one: <see cref="LinkProviders.DefaultMarkets"/>,
    /// Yahoo Finance, what Karpo always opened.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MarketsProvider? MarketsProvider { get; set; }

    /// <summary>The finance site shares open on: <see cref="MarketsProvider"/>, or <see cref="LinkProviders.DefaultMarkets"/> while there is none.</summary>
    [JsonIgnore]
    public MarketsProvider MarketsProviderOrDefault => MarketsProvider ?? LinkProviders.DefaultMarkets;

    /// <summary>
    /// holidays: countries' public holidays as calendars of their own (<see cref="HolidayOptions"/>,
    /// docs\widgets.md §237, several since §265). Null, and left out of the file, until somebody adds a
    /// country, so every file before reads as it did; read tolerantly, so a hand edit there never costs
    /// the config.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HolidayOptions? Holidays { get; set; }

    /// <summary>
    /// §532: the taskbar's own right-click menu, entry by entry (<see cref="BarMenus"/>), one for Karpo as a whole: no
    /// layout's and no display's. Null, and left out of the file, while it is the default (<see cref="BarMenus.Default"/>):
    /// read it through <see cref="BarMenuEntries"/>, change it through <see cref="BarMenus.Change"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<BarMenuEntry>? BarMenu { get; set; }

    /// <summary>§532: the taskbar's menu as the file has it, the default where it says nothing. Read only.</summary>
    [JsonIgnore]
    public IReadOnlyList<BarMenuEntry> BarMenuEntries => BarMenu ?? BarMenus.Default();

    /// <summary>
    /// The icon size in DIPs this bar uses: half the bar's height, on a 2-DIP
    /// grid, 16 at least, as on Windows' own 48-DIP taskbar. There is no choice
    /// to make. An icon that did not follow the bar put the app buttons out of
    /// step with the widgets beside them, which are measured on the bar's own
    /// square (Layout.CellPx), and a person dragging the bar taller expects its
    /// icons to grow with it: one held at 24 DIPs was a third of a 72-DIP bar
    /// beside widgets that had grown.
    /// </summary>
    public float IconDips() =>
        Math.Clamp(MathF.Round(Height / 4f, MidpointRounding.AwayFromZero) * 2f, 16f, MaxHeight / 2f);

    /// <summary>
    /// tablet: this config as a bar is laid out at another height — a copy of its settings, sharing
    /// its lists, at <paramref name="height"/> DIPs —, for the bar at touch size in tablet posture.
    /// Read only: it is never edited, and never saved.
    /// </summary>
    public Config WithHeight(int height)
    {
        var copy = (Config)MemberwiseClone();
        copy.Height = height;
        return copy;
    }

    /// <summary>
    /// This config as a bar is laid out in another look — a copy of its settings, sharing its lists,
    /// wearing <paramref name="style"/>. Read only, as <see cref="WithHeight"/>'s.
    /// </summary>
    public Config WithStyle(BarStyle style)
    {
        if (StyleOrDefault == style)
        {
            return this;
        }
        var copy = (Config)MemberwiseClone();
        copy.Style = style;
        return copy;
    }

    /// <summary>
    /// This config as a bar along an edge is laid out — a copy of its settings, sharing its lists, at
    /// <paramref name="edge"/>'s thickness and in its look, wearing its layout's (§345, <see cref="Bar"/>).
    /// Read only, as <see cref="WithHeight"/>'s.
    /// </summary>
    public Config WithEdge(EdgeBar edge)
    {
        var bar = edge.Of ?? Bar;
        if (Height == edge.Size && Style == edge.Style && Bar == bar)
        {
            return this;
        }
        var copy = (Config)MemberwiseClone();
        copy.Height = edge.Size;
        copy.Style = edge.Style;
        copy.Bar = bar;
        return copy;
    }

    public const int MinHeight = 28;
    public const int MaxHeight = 160;

    /// <summary>
    /// §589: the room between two widgets of a bar nobody spaced, in DIPs: 6, §515's gap (over the 5 between two faces on one
    /// button, so faces on one button read as one and two buttons as two).
    /// </summary>
    public const int DefaultSpacing = 6;

    /// <summary>§589: the least room between two widgets: none, the widgets touching (an islands bar flush).</summary>
    public const int LeastSpacing = 0;

    /// <summary>§589: the most room between two widgets, in DIPs: half Windows' 48-DIP bar.</summary>
    public const int MostSpacing = 24;

    /// <summary>Per display: whether Karpo is on it, and the layout it shows.</summary>
    public List<Screen> Screens { get; set; } = [];

    // --- Where things live ------------------------------------------------------

    /// <summary>Set by tests, so they never read or write the real files.</summary>
    internal static string? Isolated =>
        Environment.GetEnvironmentVariable("KARPO_CONFIG_DIR") is { Length: > 0 } folder ? folder : null;

    /// <summary>
    /// Whether this Karpo runs on a config of its own rather than the person's: a test, or
    /// <c>Karpo.exe --smoke</c>. It is the answer to "may this reach the person's own world?" and
    /// is already what decides that Karpo reads none of their taskbar pins
    /// (<see cref="Sections.TaskbarPins"/>), none of their drives and none of their startup
    /// entries. The dock's calendar, contacts and mail read it too, so an isolated run reaches none
    /// of the person's accounts (<c>AccountSet</c>, <c>docs\accounts.md</c>).
    /// </summary>
    public static bool IsIsolated => Isolated is not null;

    private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string Folder => Isolated ?? Path.Combine(Roaming, "Karpo");

    public static string FilePath => Path.Combine(Folder, "config.json");

    /// <summary>
    /// Machine-local state that is not a setting: caches, the days screen time counted
    /// (screentime.json, screentime-history.json), and the taskbar's state before Karpo changed it.
    /// </summary>
    public static string LocalFolder => Isolated is { } folder ? Path.Combine(folder, "local") : Path.Combine(Local, "Karpo");

    // Load, Save and Reload, and how two apps share the file: ConfigFile.cs.

    /// <summary>
    /// A file's text as it reads, before <see cref="Repair"/>. Throws <see cref="JsonException"/> for
    /// text that isn't a config.
    /// </summary>
    internal static Config Parse(string text) =>
        JsonSerializer.Deserialize(text.TrimStart('﻿'), ConfigJson.Default.Config) ?? new Config();

    /// <summary>The text <see cref="Save"/> writes.</summary>
    internal string ToJson() => JsonSerializer.Serialize(this, ConfigJson.Default.Config) + "\n";

    /// <summary>
    /// Makes every layout, display and folder consistent, whatever the file said: unique ids, only
    /// kinds this build knows, each island's own options, every layout's widgets in its places and in
    /// their order, every display showing a layout that exists. True when it changed something the
    /// file should get back — it dropped something, or made up an id the file lacked — so that
    /// <see cref="Load"/> saves once and the dock and the settings app read the same islands with the
    /// same ids. Values it only clamps, and options it only fills in, don't count: they come out the
    /// same in both apps.
    /// </summary>
    public bool Repair()
    {
        var changed = false;
        // A version below this build's (a hand edit) is this build's. Never downward: a file from a
        // newer Karpo keeps its own shape and is not saved at all (FromNewerKarpo).
        if (Version < Shape)
        {
            Version = Shape;
            changed = true;
        }
        Screens ??= [];
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        changed |= Screens.RemoveAll(screen => screen is null || !IsDisplayId(screen.Id) || !ids.Add(screen.Id)) > 0;
        foreach (var screen in Screens)
        {
            screen.Name ??= "";
            if (!Enum.IsDefined(screen.Edge))
            {
                // A word this build does not know: the bottom.
                screen.Edge = Edge.Bottom;
                changed = true;
            }
        }
        changed |= RepairLayouts();
        // §567, §577: the Appearance held to what it may say: a palette known worn, each known palette's sections, one that says nothing left out.
        Appearance = Appearance?.Normalized();
        // tray: the identities as they are written now (TrayLayout.Repair).
        changed |= TrayLayout.Repair(this);

        Height = Math.Clamp(Height == 0 ? DefaultHeight : Height, MinHeight, MaxHeight);
        OnlineUpdateMinutes = OnlineUpdates.Clamp(OnlineUpdateMinutes);
        // §489, §549: animations of a newer build read as Normal.
        if (!Enum.IsDefined(Animation))
        {
            Animation = Animation.Normal;
            changed = true;
        }
        // An engine of a newer build reads as the default; an empty custom address is none.
        if (SearchEngine is { } engine && !Enum.IsDefined(engine))
        {
            SearchEngine = null;
        }
        SearchCustomUrl = SearchCustomUrl?.Trim() is { Length: > 0 } custom ? custom : null;
        // A map, a weather or a finance site of a newer build reads as the default.
        MapProvider = LinkProviders.Known(MapProvider);
        WeatherProvider = LinkProviders.Known(WeatherProvider);
        MarketsProvider = LinkProviders.Known(MarketsProvider);
        Holidays = Holidays?.Normalized();
        // §532: the taskbar's menu: known entries, unique ids, "Edit" kept; the default is no list in the file.
        if (BarMenu is { } barMenu)
        {
            changed |= BarMenus.Repair(barMenu);
            if (BarMenus.IsDefault(barMenu))
            {
                BarMenu = null;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>
    /// Ids none of <paramref name="ids"/> has — the ids of the layouts repaired before this one, and
    /// of this one's, which it adds to — only kinds this build knows, one of each kind that may appear
    /// only once, and each island's own options; each folder's widgets repaired
    /// as well (see <see cref="RepairFolder"/>). True when it changed more than
    /// options and clamps (see <see cref="Repair"/>).
    /// </summary>
    internal static bool RepairList(List<BarItem> list, HashSet<string> ids)
    {
        var changed = false;
        // A kind from a newer Karpo, or one typed by hand, goes, and so does one no taskbar takes
        // (BarEntries) — a folder's piece typed onto a bar by hand; everything else stays as it was.
        changed |= list.RemoveAll(island => island is null || BarEntries.Find(island.Kind) is null) > 0;
        foreach (var island in list)
        {
            changed |= !island.HasId || island.Id.Contains('/');
            island.Normalize();
            // A '/' would break the island's key and the "<folder>/<widget>" paths.
            while (island.Id.Contains('/') || !ids.Add(island.Id))
            {
                island.Id = WidgetItem.NewId();
                changed = true;
            }
        }
        // Then the folders' widgets, whose ids are unique among the list's too:
        // the settings app finds a widget in a folder by "<folder id>/<its id>",
        // and the dock keys it "<owner>/<folder id>/<its id>".
        foreach (var island in list)
        {
            if (island.Folder is { } folder)
            {
                changed |= RepairFolder(island.Kind, folder, ids);
            }
        }
        return changed;
    }

    /// <summary>
    /// A folder's widgets: only kinds a folder of that kind may hold (<see cref="Core.Folders.CanAdd"/>),
    /// each once where it may be only once, each with its own options and an id unique
    /// among <paramref name="ids"/> (the list's and the other folders'), and lists that keep
    /// the folder's rule (<see cref="Core.Folders.Tidy"/>). True when anything but options and
    /// clamps changed.
    /// </summary>
    private static bool RepairFolder(BarKind folderKind, FolderOptions folder, HashSet<string> ids)
    {
        var changed = Core.Folders.Tidy(folder);
        // The projection (S5): each widget stands in the folder's own view, which is what that
        // view's registry has of it (ListWidgets). One it has no form of — Wi-Fi in any folder, the
        // weather in a list, a kind from a newer Karpo — goes, and the file is written back without
        // it; a list it leaves empty stays, an empty column.
        var misses = Core.Folders.Project(folder, Core.Folders.View(folderKind));
        foreach (var list in folder.Lists)
        {
            changed |= list.RemoveAll(misses.Contains) > 0;
        }
        changed |= Core.Folders.Tidy(folder);
        foreach (var cell in folder.Cells)
        {
            var island = cell.Island;
            changed |= !island.HasId || island.Id.Contains('/');
            island.Normalize();
            while (island.Id.Contains('/') || !ids.Add(island.Id))
            {
                island.Id = WidgetItem.NewId();
                changed = true;
            }
        }
        return changed;
    }

    // --- The lists: a layout's, and the one a display shows ------------------------------

    /// <summary>
    /// The key a bar gives an island: its list's owner (<see cref="DisplayOwner"/>,
    /// "display:&lt;id&gt;", or <see cref="LayoutOwner"/>) and its id. A widget in a folder is keyed
    /// "&lt;owner&gt;/&lt;folder id&gt;/&lt;its id&gt;".
    /// </summary>
    public static string IslandKey(string? owner, string id) => $"{owner}/{id}";

    /// <summary>How an owner (<see cref="DisplayOwner"/>) names the list a display shows: this, then the display's id.</summary>
    public const string OwnerPrefix = "display:";

    /// <summary>How an owner (<see cref="LayoutOwner"/>) names a layout's list: this, then the layout's id.</summary>
    public const string LayoutPrefix = "layout:";

    /// <summary>
    /// The list the display <paramref name="screenId"/> shows, as the dock's island keys name it:
    /// "display:&lt;id&gt;", the list of the layout the display shows (<see cref="ActiveLayout"/>).
    /// <c>ListFor(DisplayOwner(id))</c> is always <c>IslandsFor(id)</c>. A display's id has no '/', so
    /// the key's first '/' ends the owner.
    /// </summary>
    public static string DisplayOwner(string screenId) => OwnerPrefix + screenId;

    /// <summary>A layout's own list, whichever displays show it: "layout:&lt;id&gt;".</summary>
    public static string LayoutOwner(string layoutId) => LayoutPrefix + layoutId;

    private Screen? Entry(string? screenId) =>
        Screens.FirstOrDefault(screen => string.Equals(screen.Id, screenId, StringComparison.OrdinalIgnoreCase));

    /// <summary>What the display shows: the widgets of its layout (<see cref="ActiveLayout"/>).</summary>
    public List<BarItem> IslandsFor(string screenId) => ActiveLayout(screenId).Islands;

    /// <summary>The display <paramref name="owner"/> ("display:&lt;id&gt;") stands for; null for anything else.</summary>
    public Screen? ScreenOf(string? owner) =>
        owner is not null && owner.StartsWith(OwnerPrefix, StringComparison.Ordinal)
            ? Entry(owner[OwnerPrefix.Length..])
            : null;

    /// <summary>
    /// The layout whose list <paramref name="owner"/> is: "layout:&lt;id&gt;" that layout,
    /// "display:&lt;id&gt;" the one that display shows; null for anything else.
    /// </summary>
    public TaskbarLayout? LayoutOf(string? owner) =>
        owner is null ? null
        : owner.StartsWith(LayoutPrefix, StringComparison.Ordinal) ? Layout(owner[LayoutPrefix.Length..])
        : owner.StartsWith(OwnerPrefix, StringComparison.Ordinal) && IsDisplayId(owner[OwnerPrefix.Length..]) ? ActiveLayout(owner[OwnerPrefix.Length..])
        : null;

    /// <summary>
    /// The list <paramref name="owner"/> stands for: a layout's (<see cref="LayoutOf"/>). Anything
    /// else is a new, empty list that belongs to nothing, so a late edit can't land in another list.
    /// </summary>
    public List<BarItem> ListFor(string? owner) => LayoutOf(owner)?.Islands ?? [];

    /// <summary>
    /// The widget <paramref name="id"/> of <paramref name="owner"/>'s list — a taskbar's entry
    /// (<see cref="BarItem"/>); a path "&lt;folder id&gt;/&lt;id&gt;" finds a widget in one of its
    /// folders (<see cref="CellItem"/>).
    /// </summary>
    public WidgetItem? FindIsland(string? owner, string id) => Core.Folders.Find(ListFor(owner), id);

    /// <summary>Every layout's entries, the layouts in the file's order: what a rule over every widget walks.</summary>
    [JsonIgnore]
    public IEnumerable<BarItem> AllIslands => Layouts.Where(layout => layout?.Islands is not null).SelectMany(layout => layout.Islands);

    /// <summary>An owner ("display:…") or nothing is no display.</summary>
    private static bool IsDisplayId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && !id.StartsWith(OwnerPrefix, StringComparison.Ordinal) && !id.Contains('/');

    /// <summary>
    /// Records a display the first time a bar stands on it, showing the default layout
    /// (<see cref="DefaultLayout"/>). Also keeps the name it was last seen with. True when it added or
    /// renamed the entry: the caller saves.
    /// </summary>
    public bool AddDisplay(string id, string name)
    {
        if (!IsDisplayId(id))
        {
            return false;
        }
        if (Entry(id) is { } known)
        {
            if (string.IsNullOrEmpty(name) || known.Name == name)
            {
                return false;
            }
            known.Name = name;
            return true;
        }
        Screens.Add(NewScreen(id, name));
        return true;
    }

    /// <summary>The settings for a display, or what a new display gets when it has none yet. Does not add an entry.</summary>
    public Screen ScreenFor(string id) => Entry(id) ?? NewScreen(id, "");

    /// <summary>What a display Karpo sees for the first time starts as: showing the default layout, Karpo on (§636: always).</summary>
    private Screen NewScreen(string id, string name) => new() { Id = id, Name = name, ActiveLayoutId = DefaultLayout.Id };

    /// <summary>The settings for a display, added as a new display gets them if missing, for editing.</summary>
    public Screen EditScreen(string id, string name)
    {
        var screen = Entry(id);
        if (screen is null)
        {
            screen = NewScreen(id, name);
            Screens.Add(screen);
        }
        else if (!string.IsNullOrEmpty(name))
        {
            screen.Name = name;
        }
        return screen;
    }

    /// <summary>
    /// A deep copy, which saves as this config would: merged against the same file (ConfigFile.cs).
    /// Every folder's widgets are stood in its view again (<see cref="Core.Folders.Project"/>), as
    /// <see cref="BarItem.Clone"/> does: which view that is is not in the file, and a copy without it
    /// read every line as no line — a day view, after any edit the dock made, as a week view (§205).
    /// </summary>
    /// <summary>
    /// §359: the settings every widget may draw from besides its own — the config without its layouts and
    /// its displays — as text, so the dock draws a widget again only where they or its own changed
    /// (<see cref="Said(WidgetItem)"/>).
    /// </summary>
    public string SharedSaid()
    {
        var copy = (Config)MemberwiseClone();
        copy.Layouts = [];
        copy.Screens = [];
        return JsonSerializer.Serialize(copy, ConfigJson.Default.Config);
    }

    /// <summary>§678: <paramref name="of"/>'s layouts as text: the same text, the same bars.</summary>
    public static string LayoutsSaid(Config of) =>
        string.Join(';', of.Layouts.Select(layout => JsonSerializer.Serialize(layout, ConfigJson.Default.TaskbarLayout)));

    /// <summary>§678: the screen of <paramref name="display"/>, as text: its edge, its layout, whether it shows Karpo.</summary>
    public static string ScreenSaid(Config of, string display) =>
        string.Join(',', of.EdgeOfScreen(display), of.ScreenFor(display).Enabled, of.ActiveLayout(display).Id);

    /// <summary>§675: <paramref name="of"/>'s Appearance as text: the same text, the same look.</summary>
    public static string AppearanceSaid(Config of) =>
        of.Appearance is { } appearance ? JsonSerializer.Serialize(appearance, ConfigJson.Default.Appearance) : "";

    /// <summary>§359: a widget's own settings, as text (<see cref="SharedSaid"/>).</summary>
    public static string Said(WidgetItem item) => item switch
    {
        BarItem bar => JsonSerializer.Serialize(bar, ConfigJson.Default.BarItem),
        CellItem cell => JsonSerializer.Serialize(cell, ConfigJson.Default.CellItem),
        _ => "",
    };

    public Config Clone()
    {
        var copy = SameFile(JsonSerializer.Deserialize(JsonSerializer.Serialize(this, ConfigJson.Default.Config), ConfigJson.Default.Config)!);
        foreach (var island in copy.Layouts is null ? [] : copy.AllIslands)
        {
            if (island?.Folder is { } folder)
            {
                Core.Folders.Project(folder, Core.Folders.View(island.Kind));
            }
        }
        return copy;
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(Config))]
[JsonSerializable(typeof(BarItem))]
[JsonSerializable(typeof(CellItem))]
internal sealed partial class ConfigJson : JsonSerializerContext;
