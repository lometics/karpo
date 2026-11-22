// The actions widget's members: the pages of Windows' Settings an action can open (docs\widgets.md
// §9, "Actions").
//
// The person, 2026-10-09: the actions widget "replaces the buttons at the bottom of flyouts ('open
// calendar settings', 'open wifi settings' …). Two slots: a left action and a right action. For now
// an action is a Settings page (a curated list). Icon and text come from the chosen action (not
// editable)." So an action is one of these records: a page's ms-settings: address, the Segoe Fluent
// Icons glyph Settings itself uses for it, and its name in Settings' own words. Curated, because a
// free address would need a name and a glyph somebody types; a new page is one line here and its
// words in the five languages.
//
// One is not a page of Settings: the calendar app (2026-10-09), the left of the calendar flyout's two
// footer links ("Open Calendar"), which the calendar preset's footer is. It opens the app Windows has
// for the calendar's link (Karpo.Dock\Calendar\CalendarApp.cs), and where this PC has none its slot
// stands empty, as the flyout left the button out. Another is Settings' "Add device"
// (2026-10-10), the button Bluetooth's quick settings page ends with: the Bluetooth page, whose first
// button it is, since ms-settings-connectabledevices:devicediscovery opens Cast on Windows 11. And
// Task Manager (2026-10-10), the button the performance flyouts ended with, by its program: an app
// as the calendar is, which every Windows has. And Task view (2026-10-11), the button the desktops
// preset's footer has, as Windows' own taskbar has it: not a page and no program, but Win+Tab
// (Karpo.Dock\Islands\ActionsTile.cs), with Multitasking's page, where Windows keeps what the
// desktops show, the link beside it.
//
// The premade widgets (PremadeWidgets.cs) are actions lines set to the pairs Windows' flyouts end with.

namespace Karpo.Core;

/// <summary>One page of Windows' Settings an action opens.</summary>
/// <param name="Id">The word config.json stores, which never changes once released.</param>
/// <param name="Uri">The page's ms-settings: address.</param>
/// <param name="Glyph">A Segoe Fluent Icons code point standing for it.</param>
public sealed record SettingsAction(string Id, string Uri, string Glyph)
{
    /// <summary>The page's name, in Settings' own words: what the link says and the button's tooltip.</summary>
    public string Name => Strings.Text($"core.action.{Id}");
}

/// <summary>Every page an action can open, and the actions widget's options.</summary>
public static class SettingsActions
{
    /// <summary>Every page, in the order Settings offers them: the ones flyouts end with first.</summary>
    public static readonly SettingsAction[] All =
    [
        new("dateTime", "ms-settings:dateandtime", ""),
        new(CalendarId, "outlookcal:", ""),
        // Task Manager: the button a performance reading's flyout ended with, "Open Task Manager",
        // the readings' presets' footer now. Task Manager's own glyph, as the dock's menus have it.
        new(TaskManagerId, TaskManagerTarget, ""),
        new("notifications","ms-settings:notifications", ""),
        new("focus", "ms-settings:quiethours", ""),
        new("wifi", "ms-settings:network-wifi", ""),
        new("network", "ms-settings:network-status", ""),
        new("vpn", "ms-settings:network-vpn", ""),
        new("airplaneMode", "ms-settings:network-airplanemode", ""),
        new("mobileHotspot", "ms-settings:network-mobilehotspot", ""),
        new("bluetooth", "ms-settings:bluetooth", ""),
        // Settings' "Add device" heads the Bluetooth page: the Bluetooth flyout's button, which
        // opened the Devices Flow's Cast panel before (2026-10-10, "when i click add bluetooth device
        // the cast opens"). Settings' plus.
        new(AddDeviceId, "ms-settings:bluetooth", "\uE710"),
        // Nearby sharing (2026-10-11): the nearby sharing preset's footer, Windows' tile's "Nearby
        // sharing settings"; System > Nearby sharing, its share glyph.
        new(NearbySharingId, "ms-settings:crossdevice", "\uE72D"),
        new("sound", "ms-settings:sound",""),
        new("volumeMixer", "ms-settings:apps-volume", ""),
        new("display", "ms-settings:display",""),
        new("nightLight", "ms-settings:nightlight", ""),
        new("power", "ms-settings:powersleep", ""),
        new("energySaver", "ms-settings:batterysaver", ""),
        new("personalization", "ms-settings:personalization", ""),
        new("taskbar", "ms-settings:taskbar", ""),
        new("clipboard", "ms-settings:clipboard", ""),
        new("storage", "ms-settings:storagesense", ""),
        new("apps", "ms-settings:appsfeatures", ""),
        new("accessibility", "ms-settings:easeofaccess", ""),
        new("location", "ms-settings:privacy-location", ""),
        new("language", "ms-settings:regionlanguage", ""),
        new("windowsUpdate", "ms-settings:windowsupdate", ""),
        // desktops: Multitasking, where Windows keeps what the virtual desktops show; and Task view,
        // Win+Tab, the desktops preset's button, in Windows' own Task view glyph.
        new("multitasking", "ms-settings:multitasking", ""),
        new(TaskViewId, "", ""),
        // Windows' quick settings' gear and pencil (2026-10-14): Settings' own home, and the folder
        // the line is in arranged where it stands (Karpo.Dock\Islands\ActionsTile.cs).
        new(AllSettingsId, "ms-settings:", "\uE713"),
        new(EditId, "", "\uE70F"),
    ];

    /// <summary>Windows' Settings, its home page: quick settings' gear.</summary>
    public const string AllSettingsId = "allSettings";

    /// <summary>
    /// Quick settings' pencil: not a page, the folder the line is in arranged where it stands, its
    /// bar's edit mode on, so its lines are added, taken out and moved as Windows' pencil lets them be.
    /// </summary>
    public const string EditId = "edit";

    /// <summary>The calendar app: not a page of Settings, but what the calendar flyout's footer opened first.</summary>
    public const string CalendarId = "calendar";

    /// <summary>Settings' "Add device", at the top of its Bluetooth page: the Bluetooth preset's button.</summary>
    public const string AddDeviceId = "addDevice";

    /// <summary>Settings' Nearby sharing page: the nearby sharing preset's footer.</summary>
    public const string NearbySharingId = "nearbySharing";

    /// <summary>Task Manager: an app, not a page of Settings, as the calendar is; the performance readings' footer.</summary>
    public const string TaskManagerId = "taskManager";

    /// <summary>
    /// desktops: Task view, Win+Tab: neither a page of Settings nor a program, so it has no address;
    /// the actions line presses the keys the taskbar's Task view button presses.
    /// </summary>
    public const string TaskViewId = "taskView";

    /// <summary>What Task Manager is opened by: its program, which the shell finds on the path.</summary>
    public const string TaskManagerTarget = "taskmgr.exe";

    /// <summary>The page whose id is <paramref name="id"/>, or null for none and for a word this build does not know.</summary>
    public static SettingsAction? Find(string? id) =>
        id is { Length: > 0 } ? All.FirstOrDefault(action => string.Equals(action.Id, id, StringComparison.Ordinal)) : null;

    /// <summary>§425: the pages of Settings a pin may open, each address once, in <see cref="All"/>'s order: the apps and Task view are no page.</summary>
    public static IReadOnlyList<SettingsAction> Pages { get; } =
        [.. All.Where(action => action.Uri.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase)).DistinctBy(action => action.Uri, StringComparer.OrdinalIgnoreCase)];

    /// <summary>§425: the page a pin's <paramref name="target"/> opens, by its address; null for none.</summary>
    public static SettingsAction? Page(string? target) =>
        target?.Trim() is { Length: > 0 } address ? Pages.FirstOrDefault(page => string.Equals(page.Uri, address, StringComparison.OrdinalIgnoreCase)) : null;

    // --- Sections (2026-10-11: "the actions in the action widget should be searchable and shown as
    // subsections") ---------------------------------------------------------------------------------
    //
    // Where the picker of an actions line's slot lists each action: under the page of Windows'
    // Settings it is on, in Settings' own order and words — System, Bluetooth & devices, Network &
    // internet, … — so somebody who knows where a page is in Settings finds it there. The calendar
    // app and Task Manager are apps, and stand under Apps and System, where Settings keeps their kind.

    private static readonly Dictionary<string, ActionSection> Sections = new(StringComparer.Ordinal)
    {
        ["notifications"] = ActionSection.System, ["focus"] = ActionSection.System, ["sound"] = ActionSection.System,
        ["volumeMixer"] = ActionSection.System, ["display"] = ActionSection.System, ["nightLight"] = ActionSection.System,
        ["power"] = ActionSection.System, ["energySaver"] = ActionSection.System, ["storage"] = ActionSection.System,
        ["clipboard"] = ActionSection.System, [TaskManagerId] = ActionSection.System, [NearbySharingId] = ActionSection.System,
        ["multitasking"] = ActionSection.System, [TaskViewId] = ActionSection.System,
        [AllSettingsId] = ActionSection.System, [EditId] = ActionSection.System,
        ["bluetooth"] = ActionSection.Devices, [AddDeviceId] = ActionSection.Devices,
        ["wifi"] = ActionSection.Network, ["network"] = ActionSection.Network, ["vpn"] = ActionSection.Network,
        ["airplaneMode"] = ActionSection.Network, ["mobileHotspot"] = ActionSection.Network,
        ["personalization"] = ActionSection.Personalization, ["taskbar"] = ActionSection.Personalization,
        ["apps"] = ActionSection.Apps, [CalendarId] = ActionSection.Apps,
        ["dateTime"] = ActionSection.Time, ["language"] = ActionSection.Time,
        ["accessibility"] = ActionSection.Accessibility,
        ["location"] = ActionSection.Privacy,
        ["windowsUpdate"] = ActionSection.Update,
    };

    /// <summary>The section of Windows' Settings <paramref name="action"/> is listed under.</summary>
    public static ActionSection SectionOf(SettingsAction action) =>
        Sections.TryGetValue(action.Id, out var section) ? section : ActionSection.System;

    /// <summary>What a section's heading says, in Settings' own words.</summary>
    public static string Title(this ActionSection section) => Strings.Text($"core.actionSection.{char.ToLowerInvariant(section.ToString()[0])}{section.ToString()[1..]}");

    /// <summary>
    /// The actions <paramref name="query"/> finds, in their sections, in Settings' order, each
    /// section's in <see cref="All"/>'s; a section none of whose actions is found is left out. An
    /// action is found by its name, its section's name and, where Karpo speaks another language,
    /// its English name (<see cref="CatalogueSearch.Matches"/>); an empty query finds every one.
    /// </summary>
    public static IReadOnlyList<(ActionSection Section, IReadOnlyList<SettingsAction> Actions)> Search(string? query) =>
    [
        .. Enum.GetValues<ActionSection>()
            .Select(section => (section, (IReadOnlyList<SettingsAction>)[.. All.Where(action => SectionOf(action) == section && Finds(query, action, section))]))
            .Where(group => group.Item2.Count > 0),
    ];

    private static bool Finds(string? query, SettingsAction action, ActionSection section) =>
        CatalogueSearch.Matches(query, action.Name, [section.Title(), .. Strings.AlsoEnglish($"core.action.{action.Id}")]);
}

/// <summary>
/// A section of Windows' Settings an action's page is on, in Settings' own order: what the picker
/// of an actions line's slot lists it under (<see cref="SettingsActions.Search"/>).
/// </summary>
public enum ActionSection
{
    System,
    Devices,
    Network,
    Personalization,
    Apps,
    Time,
    Accessibility,
    Privacy,
    Update,
}

/// <summary>
/// list: the two actions of an actions line (<see cref="WidgetInfos.Actions"/>): a text button on
/// the left and one on the right, each a page's id or null for an empty slot.
/// </summary>
public sealed class ActionsOptions
{
    /// <summary>
    /// The text button at the line's start. A new line opens Date &amp; time, as the clock's flyout did
    /// (<see cref="New"/>); null here, so a slot somebody emptied reads back empty.
    /// </summary>
    public string? Left { get; set; }

    /// <summary>A new line's actions: Date &amp; time on the left, nothing on the right.</summary>
    public static ActionsOptions New() => new() { Left = "dateTime" };

    /// <summary>The text button at the line's end; empty on a new line.</summary>
    public string? Right { get; set; }

    /// <summary>Each slot a page this build knows, or empty.</summary>
    internal void Normalize()
    {
        Left = SettingsActions.Find(Left)?.Id;
        Right = SettingsActions.Find(Right)?.Id;
    }
}
