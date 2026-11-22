// Premade widgets: one list widget, set up already (2026-10-10: "you should premade widgets (which
// are actually just configurations of the action widget), like bluetooth actions, wifi actions,
// clock action, calendar actions").
//
// A premade widget is a list widget's word and what it is set to — a ListKind and its options — and
// nothing more. The add list offers each in its subject (AddTree.cs), "Bluetooth
// actions" beside Bluetooth, and a pick adds an ordinary line of that widget with those options: no
// mark of where it came from is stored, and its settings change it as any line's. So it is not a
// preset (Presets.cs), which is a folder of several widgets; a preset names a premade widget for one
// of its lines rather than its own options (PresetItem.Premade), so the clock's footer and "Clock
// actions" are one record and cannot drift.
//
// The actions lines are Windows' flyout footers, each its link on the left and its button on the
// right (SettingsActions.cs): Bluetooth's quick settings page ends with Bluetooth's settings and
// "Add device", Wi-Fi's with Wi-Fi's settings, the clock's with Date & time, and so on. Another kind
// of premade widget is another options property here and one line in Apply.

namespace Karpo.Core;

/// <summary>One list widget, set up already: its word, its icon and what it is set to.</summary>
public sealed record PremadeWidget
{
    /// <summary>Its name in code, a test and its words' keys (<c>core.premade.&lt;id&gt;.title</c>); never stored.</summary>
    public required string Id
    {
        get => id;
        init
        {
            id = value;
            titleKey = $"core.premade.{value}.title";
        }
    }

    private readonly string id = "";
    private readonly string titleKey = "";

    /// <summary>The list widget it is.</summary>
    public required ListKind Kind { get; init; }

    /// <summary>A Segoe Fluent Icons code point: its row's icon in the add list.</summary>
    public required string Glyph { get; init; }

    /// <summary>What the add list calls it, in the language Karpo speaks now: "Bluetooth actions".</summary>
    public string Title => Strings.Text(titleKey);

    /// <summary>Its English name, where Karpo speaks another language, for the search; nothing in English.</summary>
    public IEnumerable<string> EnglishWords => Strings.AlsoEnglish(titleKey);

    /// <summary>actions: its two slots (<see cref="ActionsOptions"/>).</summary>
    public (string? Left, string? Right)? Actions { get; init; }

    /// <summary>
    /// What it says it holds, which the search reads: the names of its actions, so "add device"
    /// finds Bluetooth actions.
    /// </summary>
    public string Blurb => Actions is var (left, right)
        ? string.Join(" · ", new[] { left, right }.Select(SettingsActions.Find).OfType<SettingsAction>().Select(action => action.Name))
        : "";

    /// <summary>A new widget of it, with an id of its own, set up as it says.</summary>
    public CellItem New()
    {
        var island = Kind.New();
        Apply(island);
        return island;
    }

    /// <summary>Sets <paramref name="island"/>, a widget of <see cref="Kind"/>, to what it says.</summary>
    public void Apply(WidgetItem island)
    {
        if (Actions is var (left, right) && island.Actions is { } line)
        {
            (line.Left, line.Right) = (left, right);
        }
        island.Normalize();
    }
}

/// <summary>Every premade widget.</summary>
public static class PremadeWidgets
{
    /// <summary>The clock flyout's footer: Date &amp; time, and Focus for the focus session above it.</summary>
    public static readonly PremadeWidget ClockActions = new()
    {
        Id = "clockActions", Kind = ListKind.Actions, Glyph = "",
        Actions = ("dateTime", "focus"),
    };

    /// <summary>The calendar flyout's footer: Date &amp; time, and the calendar app.</summary>
    public static readonly PremadeWidget CalendarActions = new()
    {
        Id = "calendarActions", Kind = ListKind.Actions, Glyph = "",
        Actions = ("dateTime", SettingsActions.CalendarId),
    };

    /// <summary>Wi-Fi's quick settings page's footer: Wi-Fi's settings, and Network &amp; internet.</summary>
    public static readonly PremadeWidget WifiActions = new()
    {
        Id = "wifiActions", Kind = ListKind.Actions, Glyph = "",
        Actions = ("wifi", "network"),
    };

    /// <summary>Bluetooth's quick settings page's footer: Bluetooth &amp; devices, and Add device.</summary>
    public static readonly PremadeWidget BluetoothActions = new()
    {
        Id = "bluetoothActions", Kind = ListKind.Actions, Glyph = "",
        Actions = ("bluetooth", SettingsActions.AddDeviceId),
    };

    /// <summary>The volume's quick settings page's footer: Sound, and the volume mixer.</summary>
    public static readonly PremadeWidget SoundActions = new()
    {
        Id = "soundActions", Kind = ListKind.Actions, Glyph = "",
        Actions = ("sound", "volumeMixer"),
    };

    /// <summary>The mobile hotspot page's footer: its settings, and Network &amp; internet, where Windows keeps it.</summary>
    public static readonly PremadeWidget HotspotActions = new()
    {
        Id = "hotspotActions", Kind = ListKind.Actions, Glyph = "",
        Actions = ("mobileHotspot", "network"),
    };

    /// <summary>
    /// Windows' Cast panel's footer: Display, where its "More display settings" leads, and Add
    /// device, its "Find other devices", where a wireless display is set up the first time.
    /// </summary>
    public static readonly PremadeWidget CastActions = new()
    {
        Id = "castActions", Kind = ListKind.Actions, Glyph = "\uEC15",
        Actions = ("display", SettingsActions.AddDeviceId),
    };

    /// <summary>
    /// Windows' nearby sharing tile's footer: its settings, "Nearby sharing settings", alone, on the
    /// right where a flyout's button stands; nothing on the left.
    /// </summary>
    public static readonly PremadeWidget NearbyActions = new()
    {
        Id = "nearbyActions", Kind = ListKind.Actions, Glyph = "\uE72D",
        Actions = (null, SettingsActions.NearbySharingId),
    };

    /// <summary>
    /// The battery and power flyouts' footer: Power &amp; battery, for their "Battery settings" and
    /// "Power settings", and Energy saver, whose row in them led to its page.
    /// </summary>
    public static readonly PremadeWidget PowerActions = new()
    {
        Id = "powerActions", Kind = ListKind.Actions, Glyph = "",
        Actions = ("power", "energySaver"),
    };

    /// <summary>The notification center's footer: Notifications, and Focus.</summary>
    public static readonly PremadeWidget NotificationActions = new()
    {
        Id = "notificationActions", Kind = ListKind.Actions, Glyph = "",
        Actions = ("notifications", "focus"),
    };

    /// <summary>
    /// The performance flyouts' footer: Task Manager, their "Open Task Manager", alone, on the right
    /// where the person put it (2026-10-11), as a flyout's button stands; nothing on the left.
    /// </summary>
    public static readonly PremadeWidget TaskManagerActions = new()
    {
        Id = "taskManagerActions", Kind = ListKind.Actions, Glyph = "",
        Actions = (null, SettingsActions.TaskManagerId),
    };

    /// <summary>
    /// The clipboard history flyout's footer: Windows' clipboard settings, its "Clipboard settings",
    /// alone, on the right where the person put it (2026-10-11); nothing on the left.
    /// </summary>
    public static readonly PremadeWidget ClipboardActions = new()
    {
        Id = "clipboardActions", Kind = ListKind.Actions, Glyph = "",
        Actions = (null, "clipboard"),
    };

    /// <summary>
    /// desktops: what the desktops preset ends with — Multitasking's page, where Windows keeps what
    /// the desktops show, on the left, and Task view, Win+Tab, every desktop and its windows at once,
    /// on the right. The switcher's flyout had "New desktop" under its list, which is the line's own foot.
    /// </summary>
    public static readonly PremadeWidget DesktopActions = new()
    {
        Id = "desktopActions", Kind = ListKind.Actions, Glyph = "",
        Actions = ("multitasking", SettingsActions.TaskViewId),
    };

    /// <summary>
    /// The input switcher's footer: Windows' language settings, its "More keyboard settings", alone,
    /// on the right as the other one-link footers stand; nothing on the left.
    /// </summary>
    public static readonly PremadeWidget KeyboardActions = new()
    {
        Id = "keyboardActions", Kind = ListKind.Actions, Glyph = "",
        Actions = (null, "language"),
    };

    /// <summary>
    /// Windows' VPN page's footer: VPN's settings, its "VPN settings", and Network &amp; internet,
    /// where Windows keeps it, as the hotspot's footer.
    /// </summary>
    public static readonly PremadeWidget VpnActions = new()
    {
        Id = "vpnActions", Kind = ListKind.Actions, Glyph = "",
        Actions = ("vpn", "network"),
    };

    /// <summary>
    /// The project page's footer: Display, its "More display settings", alone on the right.
    /// </summary>
    public static readonly PremadeWidget ProjectActions = new()
    {
        Id = "projectActions", Kind = ListKind.Actions, Glyph = "\uE8AF",
        Actions = (null, "display"),
    };

    /// <summary>The accessibility page's footer: Windows' accessibility settings, its "More accessibility settings", alone on the right.</summary>
    public static readonly PremadeWidget AccessibilityActions = new()
    {
        Id = "accessibilityActions", Kind = ListKind.Actions, Glyph = "\uE776",
        Actions = (null, "accessibility"),
    };

    /// <summary>
    /// Quick settings' footer (2026-10-14): Windows' pencil, Edit, which arranges the folder where it
    /// stands, and its gear, All settings, Windows' Settings.
    /// </summary>
    public static readonly PremadeWidget QuickActions = new()
    {
        Id = "quickActions", Kind = ListKind.Actions, Glyph = "\uE713",
        Actions = (SettingsActions.EditId, SettingsActions.AllSettingsId),
    };

    /// <summary>Every premade widget, in the order the add list shows them under their widget.</summary>
    public static readonly PremadeWidget[] All =
        [ClockActions, CalendarActions, WifiActions, BluetoothActions, SoundActions, HotspotActions, CastActions, NearbyActions, PowerActions, NotificationActions, TaskManagerActions, ClipboardActions, DesktopActions,
         KeyboardActions, VpnActions, ProjectActions, AccessibilityActions, QuickActions];

    /// <summary>The premade widgets of <paramref name="kind"/>, in <see cref="All"/>'s order.</summary>
    public static IEnumerable<PremadeWidget> Of(ListKind kind) => All.Where(premade => premade.Kind == kind);

    /// <summary>The premade widget <paramref name="id"/> names, or null.</summary>
    public static PremadeWidget? Find(string? id) =>
        All.FirstOrDefault(premade => string.Equals(premade.Id, id, StringComparison.Ordinal));
}
