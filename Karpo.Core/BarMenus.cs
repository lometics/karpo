// The taskbar's own right-click menu: what a right click on the bar itself shows
// (Karpo.Dock\Dock.Menus.cs), as a list of entries the menu is drawn from (Lines).
//
// §532 (the person, 2026-11-07: "the right click context menu of the taskbar should be editable in the settings
// again. it should be scoped on a global level"): the person's own list again, one for Karpo as a whole —
// "barMenu" at the file's top, in no layout and for no display (Config.BarMenu) — changed on the settings
// panel's Taskbar page, its section "Taskbar menu" (§553; KarpoMenu.BarMenu.cs). An entry is a built-in row (Karpo's own and
// Windows' places, BarMenuAction), a shortcut (an app, a file, a folder, a link or a command), a divider or a
// heading. §534: Windows' "Lock the taskbar" a built-in row (BarMenuAction.LockTaskbar), ticked while the bars are
// locked (BarMoves.cs); in no default from §581, in it again since §663.
//
// Absent from the file it is the default (Default). §581 (the person, 2026-11-09: "the default right click menu on the
// taskbar should be like in windows: task manager and taskbar settings (the taskbar settings should open edit mode.) and
// the layout selector should be also there"): the layouts with no heading over them (§366; §561 a lone one too), then
// behind a divider Windows 11's own two rows in Windows' order, Task Manager and Taskbar settings, the last nearest a
// bottom bar as in Windows'. §663 (the person, 2026-11-10: "locking the taskbar should be shown in the default right
// click context menu again"): and "Lock the taskbar" with them, where Windows 10 had it — ruled off from Task Manager, over
// Taskbar settings. A list changed back into the default leaves the file again (Change), and "Reset to default" takes it
// out; no file holds the default.
//
// The way back: "Taskbar settings" is always in the menu (Required) — moved, never taken out (no Remove, why said under
// its name), and put back first where a hand edit lost it (Repair). It opens edit mode with the edit bar's Taskbar page
// open (Karpo.Dock\Dock.EditEntries.cs, EditEntry.TaskbarSettings, as Win+Alt+Enter on a bar's button does), where this
// menu is changed; the other ways into edit mode (Win+Alt+Enter, starting Karpo again, dragging a widget on a bar not
// locked) are not found by looking.
// "Quit Karpo" may be added (§532), asking first as the panel's own Quit Karpo does; it is in no default.
//
// What the menu draws is tidied as it is drawn (Lines, Tidy): a heading with nothing under it goes, and a
// divider at either end or beside another, so taking a row out never leaves an empty group or a double rule.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>What an entry of the taskbar's menu is.</summary>
[JsonConverter(typeof(TolerantEnumConverter<BarMenuKind>))]
public enum BarMenuKind
{
    /// <summary>One of Karpo's own rows, or one of Windows' places (<see cref="BarMenuAction"/>).</summary>
    [JsonStringEnumMemberName("action")] Action,

    /// <summary>The person's own: an app, a file, a folder, a link or a command.</summary>
    [JsonStringEnumMemberName("shortcut")] Shortcut,

    /// <summary>A rule between rows.</summary>
    [JsonStringEnumMemberName("divider")] Divider,

    /// <summary>A group's title, over the rows under it, as a jump list titles its groups.</summary>
    [JsonStringEnumMemberName("heading")] Heading,
}

/// <summary>A built-in row of the taskbar's menu.</summary>
[JsonConverter(typeof(TolerantEnumConverter<BarMenuAction>))]
public enum BarMenuAction
{
    // None is 0, which no entry names.
    [JsonStringEnumMemberName("taskManager")] TaskManager = 1,

    /// <summary>The named layouts, a row each with the bar's own ticked; §561 a lone one too.</summary>
    [JsonStringEnumMemberName("layouts")] Layouts,

    /// <summary>
    /// §581: Windows' "Taskbar settings": edit mode with the edit bar's Taskbar page open (§366 to §580 "Edit", edit mode
    /// alone, written "editTaskbar"). Always in the menu (<see cref="BarMenus.Required"/>).
    /// </summary>
    [JsonStringEnumMemberName("taskbarSettings")] TaskbarSettings,
    [JsonStringEnumMemberName("showDesktop")] ShowDesktop,
    [JsonStringEnumMemberName("fileExplorer")] FileExplorer,
    [JsonStringEnumMemberName("run")] Run,
    [JsonStringEnumMemberName("terminal")] Terminal,
    [JsonStringEnumMemberName("terminalAdmin")] TerminalAdmin,
    [JsonStringEnumMemberName("deviceManager")] DeviceManager,
    [JsonStringEnumMemberName("diskManagement")] DiskManagement,
    [JsonStringEnumMemberName("computerManagement")] ComputerManagement,
    [JsonStringEnumMemberName("eventViewer")] EventViewer,
    [JsonStringEnumMemberName("networkConnections")] NetworkConnections,
    [JsonStringEnumMemberName("powerOptions")] PowerOptions,
    [JsonStringEnumMemberName("windowsSettings")] WindowsSettings,
    [JsonStringEnumMemberName("system")] System,
    [JsonStringEnumMemberName("display")] Display,
    [JsonStringEnumMemberName("sound")] Sound,
    [JsonStringEnumMemberName("installedApps")] InstalledApps,

    /// <summary>§532: Karpo quit, asked first as the settings panel's Quit Karpo asks (KarpoMenu.Quit); in no default menu.</summary>
    [JsonStringEnumMemberName("quitKarpo")] QuitKarpo,

    /// <summary>§534: Windows' "Lock the taskbar", ticked while the bars are locked (<see cref="Config.LockTaskbar"/>).</summary>
    [JsonStringEnumMemberName("lockTaskbar")] LockTaskbar,
}

/// <summary>One entry of the taskbar's menu (<see cref="BarMenus"/>).</summary>
public sealed class BarMenuEntry
{
    /// <summary>Unique in the list.</summary>
    public string Id { get; set; } = "";

    public BarMenuKind Kind { get; set; }

    /// <summary>Which built-in row an <see cref="BarMenuKind.Action"/> is; not written for the rest.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public BarMenuAction Action { get; set; }

    /// <summary>
    /// What a shortcut opens, as a pin's target is written (<see cref="PinOptions.Target"/>): a
    /// program, a shortcut, a file or a folder by its path, an app as "shell:AppsFolder\&lt;ID&gt;",
    /// a web address or any other link.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Target { get; set; }

    /// <summary>What a shortcut to a program hands it: a command's arguments. Null for none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Arguments { get; set; }

    /// <summary>A shortcut's name in the menu (null: the name Windows gives its target), or a heading's words.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    /// <summary>
    /// What a shortcut is drawn with instead of its target's own icon, as a pin's
    /// (<see cref="PinOptions.Icon"/>): a Segoe Fluent Icons glyph, or a file whose icon is taken.
    /// Null for the target's own.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; set; }

    /// <summary>Whether <see cref="Icon"/> names a glyph: one character of the icon font's private use area.</summary>
    [JsonIgnore]
    public bool IconIsGlyph => Icon is { Length: 1 } glyph && glyph[0] is >= '' and <= '';

    public static BarMenuEntry Of(BarMenuAction action, string? id = null) =>
        new() { Id = id ?? WidgetItem.NewId(), Kind = BarMenuKind.Action, Action = action };

    public static BarMenuEntry Shortcut(string target, string? name = null, string? icon = null, string? arguments = null) => new()
    {
        Id = WidgetItem.NewId(),
        Kind = BarMenuKind.Shortcut,
        Target = target,
        Name = name,
        Icon = icon,
        Arguments = arguments,
    };

    public static BarMenuEntry Divider(string? id = null) => new() { Id = id ?? WidgetItem.NewId(), Kind = BarMenuKind.Divider };

    public static BarMenuEntry Heading(string text) => new() { Id = WidgetItem.NewId(), Kind = BarMenuKind.Heading, Name = text };

    /// <summary>A copy to change without changing this one.</summary>
    public BarMenuEntry Copy() => (BarMenuEntry)MemberwiseClone();

    /// <summary>The same entry in every respect but its id.</summary>
    public bool SameAs(BarMenuEntry other) =>
        Kind == other.Kind && Action == other.Action && Target == other.Target && Arguments == other.Arguments
        && Name == other.Name && Icon == other.Icon;
}

/// <summary>A built-in row as the menu names and pictures it.</summary>
public sealed record BarMenuActionInfo(BarMenuAction Action, string Name, string Glyph);

/// <summary>What a line of the drawn menu is (<see cref="BarMenus.Lines"/>).</summary>
public enum BarMenuLine
{
    Row,
    Divider,
    Heading,
}

/// <summary>A named layout as the menu's "Layouts" rows show it.</summary>
public sealed record BarMenuLayoutRow(string Name, string Glyph, bool Active);

/// <summary>
/// One line of the menu as it is drawn: a row, a divider or a heading. A row is the entry's, and
/// for the layouts the layout's place among <c>layouts</c> (<see cref="Layout"/>).
/// </summary>
/// <param name="Glyph">The row's glyph; null for a shortcut drawn with its target's own icon.</param>
/// <param name="Enabled">False for a shortcut whose target this PC doesn't have; <see cref="Detail"/> says why.</param>
/// <param name="Layout">The layout this row chooses; -1 for every other line.</param>
public sealed record BarMenuLineInfo(
    BarMenuLine Kind,
    BarMenuEntry? Entry,
    string Text = "",
    string? Glyph = null,
    bool Enabled = true,
    string? Detail = null,
    bool? Checked = null,
    int Layout = -1);

public static class BarMenus
{
    /// <summary>The most entries a menu keeps (and numbers, Dock.BarMenu.cs); a longer list typed into the file is cut here.</summary>
    public const int MostEntries = 100;

    /// <summary>A heading's words, at most: a title, not a paragraph.</summary>
    public const int MostHeading = 60;

    /// <summary>A shortcut drawn without an icon of its own: Segoe Fluent Icons' Link.</summary>
    public const string ShortcutGlyph = "";

    /// <summary>
    /// Every built-in row. The names and glyphs of the rows the Start button's quick links share are
    /// those (QuickLinks): the same place has one name and one picture wherever Karpo offers it.
    /// <para>
    /// Made each time it is asked for, in the language Karpo speaks then (docs\widgets.md §36):
    /// a list kept from before a language change would name its rows in the old words.
    /// </para>
    /// </summary>
    public static IReadOnlyList<BarMenuActionInfo> Actions =>
    [
        new(BarMenuAction.TaskManager, S.CoreBarMenuTaskManager, ""),
        new(BarMenuAction.Layouts, S.CoreBarMenuLayouts, TaskbarLayouts.LayoutGlyph),
        new(BarMenuAction.TaskbarSettings, S.CoreBarMenuTaskbarSettings, OptionGlyphs.Settings),
        new(BarMenuAction.LockTaskbar, S.CoreBarMenuLockTaskbar, OptionGlyphs.Lock),
        new(BarMenuAction.ShowDesktop, S.CoreBarMenuShowDesktop, ""),
        new(BarMenuAction.QuitKarpo, S.SettingsAboutQuitHeader, OptionGlyphs.Power),
        new(BarMenuAction.FileExplorer, S.CoreBarMenuFileExplorer,""),
        new(BarMenuAction.Run, S.CoreBarMenuRun, ""),
        new(BarMenuAction.Terminal, S.CoreBarMenuTerminal, ""),
        new(BarMenuAction.TerminalAdmin, S.CoreBarMenuTerminalAdmin, ""),
        new(BarMenuAction.DeviceManager, S.CoreBarMenuDeviceManager, ""),
        new(BarMenuAction.DiskManagement, S.CoreBarMenuDiskManagement, ""),
        new(BarMenuAction.ComputerManagement, S.CoreBarMenuComputerManagement, ""),
        new(BarMenuAction.EventViewer, S.CoreBarMenuEventViewer, ""),
        new(BarMenuAction.WindowsSettings, S.CoreBarMenuWindowsSettings, ""),
        new(BarMenuAction.System, S.CoreBarMenuSystem, ""),
        new(BarMenuAction.Display, S.CoreBarMenuDisplay, ""),
        new(BarMenuAction.Sound, S.CoreBarMenuSound, ""),
        new(BarMenuAction.NetworkConnections, S.CoreBarMenuNetworkConnections, ""),
        new(BarMenuAction.PowerOptions, S.CoreBarMenuPowerOptions, ""),
        new(BarMenuAction.InstalledApps, S.CoreBarMenuInstalledApps, ""),
    ];

    /// <summary>A built-in row's name and glyph; null for a value this build doesn't know.</summary>
    public static BarMenuActionInfo? Info(BarMenuAction action) => Actions.FirstOrDefault(info => info.Action == action);

    /// <summary>
    /// A built-in row's name in the menu: its own, except the terminal's, which is Windows
    /// PowerShell where Windows Terminal isn't installed, as Windows' own quick links say.
    /// </summary>
    public static string NameOf(BarMenuAction action, bool windowsTerminal) => action switch
    {
        BarMenuAction.Terminal when !windowsTerminal => S.CoreBarMenuPowerShell,
        BarMenuAction.TerminalAdmin when !windowsTerminal => S.CoreBarMenuPowerShellAdmin,
        _ => Info(action)?.Name ?? "",
    };

    /// <summary>
    /// What a built-in row opens, and the shell verb (null: the default one): the places Windows'
    /// own quick links open for the same rows. Null for the rows the dock does itself — Karpo's
    /// own (Quit Karpo among them), Run (its key) and Show desktop.
    /// </summary>
    public static (string File, string? Verb)? Target(BarMenuAction action, bool windowsTerminal)
    {
        string InSystem(string file) => Path.Combine(Environment.SystemDirectory, file);
        var terminal = windowsTerminal ? "wt.exe" : "powershell.exe";
        return action switch
        {
            BarMenuAction.FileExplorer => ("explorer.exe", null),
            BarMenuAction.Terminal => (terminal, null),
            BarMenuAction.TerminalAdmin => (terminal, "runas"),
            BarMenuAction.DeviceManager => (InSystem("devmgmt.msc"), null),
            BarMenuAction.DiskManagement => (InSystem("diskmgmt.msc"), null),
            BarMenuAction.ComputerManagement => (InSystem("compmgmt.msc"), null),
            BarMenuAction.EventViewer => (InSystem("eventvwr.msc"), null),
            BarMenuAction.WindowsSettings => ("ms-settings:", null),
            BarMenuAction.System => ("ms-settings:about", null),
            BarMenuAction.Display => ("ms-settings:display", null),
            BarMenuAction.Sound => ("ms-settings:sound", null),
            BarMenuAction.NetworkConnections => ("ms-settings:network", null),
            BarMenuAction.PowerOptions => ("ms-settings:powersleep", null),
            BarMenuAction.InstalledApps => ("ms-settings:appsfeatures", null),
            _ => null,
        };
    }

    /// <summary>
    /// The menu while the file says nothing of it (§581, see the top of the file): the layouts' rows under no heading, then
    /// behind a divider Task Manager, and behind another §663 "Lock the taskbar" over Taskbar settings, as Windows 10's
    /// menu ended (§534 to §580 under "Edit", §581 to §662 in no default). Quit Karpo and the rest are the person's to
    /// add. The ids are fixed, so two writers of the default write the same.
    /// </summary>
    public static List<BarMenuEntry> Default() =>
    [
        BarMenuEntry.Of(BarMenuAction.Layouts, "layouts"),
        BarMenuEntry.Divider("rule-0"),
        BarMenuEntry.Of(BarMenuAction.TaskManager, "task-manager"),
        BarMenuEntry.Divider("rule-1"),
        BarMenuEntry.Of(BarMenuAction.LockTaskbar, "lock"),
        BarMenuEntry.Of(BarMenuAction.TaskbarSettings, "taskbar-settings"),
    ];

    /// <summary>§532: the rows always in the menu: §581 "Taskbar settings", the way into edit mode, where the menu is changed (see the top of the file).</summary>
    public static bool Required(BarMenuAction action) => action == BarMenuAction.TaskbarSettings;

    /// <summary>§532: whether <paramref name="entry"/> may be taken out of the menu; null where it may, else why not.</summary>
    public static string? WhyNotRemove(BarMenuEntry entry) =>
        entry is { Kind: BarMenuKind.Action } && Required(entry.Action) ? S.SettingsBarMenuRequired : null;

    /// <summary>§532: whether <paramref name="entries"/> is the default menu, ids aside.</summary>
    public static bool IsDefault(IReadOnlyList<BarMenuEntry> entries)
    {
        var defaults = Default();
        return entries.Count == defaults.Count && entries.Zip(defaults).All(pair => pair.First.SameAs(pair.Second));
    }

    /// <summary>
    /// §532: <paramref name="config"/>'s menu changed by <paramref name="change"/> on a copy of it (the default where the file
    /// has none), written back where it says it changed: repaired (<see cref="Repair"/>), and out of the file again where it
    /// is the default once more. False where nothing changed.
    /// </summary>
    public static bool Change(Config config, Func<List<BarMenuEntry>, bool> change)
    {
        List<BarMenuEntry> entries = [.. config.BarMenuEntries.Select(entry => entry.Copy())];
        if (!change(entries))
        {
            return false;
        }
        _ = Repair(entries);
        config.BarMenu = IsDefault(entries) ? null : entries;
        return true;
    }

    /// <summary>§532: the entry at <paramref name="from"/> put at place <paramref name="to"/> (held to the list); false where it stood there.</summary>
    public static bool Move(List<BarMenuEntry> entries, int from, int to)
    {
        if (from < 0 || from >= entries.Count)
        {
            return false;
        }
        to = Math.Clamp(to, 0, entries.Count - 1);
        if (from == to)
        {
            return false;
        }
        var entry = entries[from];
        entries.RemoveAt(from);
        entries.Insert(to, entry);
        return true;
    }

    /// <summary>§532: the entry at <paramref name="at"/> taken out, unless the menu always has it (<see cref="WhyNotRemove"/>).</summary>
    public static bool Remove(List<BarMenuEntry> entries, int at)
    {
        if (at < 0 || at >= entries.Count || WhyNotRemove(entries[at]) is not null)
        {
            return false;
        }
        entries.RemoveAt(at);
        return true;
    }

    /// <summary>
    /// §532: <paramref name="entry"/> at the menu's end, where "Add an entry" stands; refused past <see cref="MostEntries"/>,
    /// and for a built-in row the menu has already.
    /// </summary>
    public static bool Add(List<BarMenuEntry> entries, BarMenuEntry entry)
    {
        if (entries.Count >= MostEntries || (entry.Kind == BarMenuKind.Action && entries.Any(each => each.Kind == BarMenuKind.Action && each.Action == entry.Action)))
        {
            return false;
        }
        entries.Add(entry);
        return true;
    }

    /// <summary>§532: the built-in rows <paramref name="entries"/> has not, in <see cref="Actions"/>' order: what "Add an entry" offers.</summary>
    public static List<BarMenuActionInfo> Addable(IReadOnlyList<BarMenuEntry> entries) =>
        [.. Actions.Where(info => !entries.Any(entry => entry.Kind == BarMenuKind.Action && entry.Action == info.Action))];

    /// <summary>
    /// §532: makes <paramref name="entries"/> a list the menu can draw, as <see cref="Config.Repair"/> does for the rest of the
    /// file: entries of a kind or an action this build doesn't know go, as does a built-in row's second showing; at most
    /// <see cref="MostEntries"/>; "Taskbar settings" first where the list had lost it (<see cref="Required"/>); every id there and unique;
    /// words trimmed, empty ones left out, a heading's held to <see cref="MostHeading"/>. A shortcut with nothing to open yet
    /// stays (just added, its target still to choose): the menu draws nothing of it. True where it changed anything.
    /// </summary>
    public static bool Repair(List<BarMenuEntry> entries)
    {
        var changed = false;
        var actions = new HashSet<BarMenuAction>();
        changed |= entries.RemoveAll(entry =>
            entry is null
            || !Enum.IsDefined(entry.Kind)
            || (entry.Kind == BarMenuKind.Action && (Info(entry.Action) is null || !actions.Add(entry.Action)))) > 0;
        if (entries.Count > MostEntries)
        {
            entries.RemoveRange(MostEntries, entries.Count - MostEntries);
            changed = true;
        }
        foreach (var required in Actions.Select(info => info.Action).Where(Required))
        {
            if (!entries.Any(entry => entry.Kind == BarMenuKind.Action && entry.Action == required))
            {
                if (entries.Count >= MostEntries)
                {
                    entries.RemoveAt(entries.Count - 1);
                }
                entries.Insert(0, BarMenuEntry.Of(required));
                changed = true;
            }
        }
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            while (string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id))
            {
                entry.Id = WidgetItem.NewId();
                changed = true;
            }
            changed |= Tidy(entry);
        }
        return changed;
    }

    /// <summary>§532: an entry's words trimmed, and the ones its kind has no use for left out.</summary>
    private static bool Tidy(BarMenuEntry entry)
    {
        var before = (entry.Action, entry.Target, entry.Arguments, entry.Name, entry.Icon);
        static string? Text(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        switch (entry.Kind)
        {
            case BarMenuKind.Action:
                entry.Target = entry.Arguments = entry.Name = entry.Icon = null;
                break;
            case BarMenuKind.Shortcut:
                entry.Action = 0;
                entry.Target = Text(entry.Target);
                entry.Arguments = Text(entry.Arguments);
                entry.Name = Text(entry.Name);
                entry.Icon = Text(entry.Icon);
                break;
            case BarMenuKind.Heading:
                entry.Action = 0;
                entry.Target = entry.Arguments = entry.Icon = null;
                entry.Name = Text(entry.Name) is { } words && words.Length > MostHeading ? words[..MostHeading].TrimEnd() : Text(entry.Name);
                break;
            default:
                entry.Action = 0;
                entry.Target = entry.Arguments = entry.Name = entry.Icon = null;
                break;
        }
        return before != (entry.Action, entry.Target, entry.Arguments, entry.Name, entry.Icon);
    }

    /// <summary>
    /// A shortcut's name where it has none of its own and the shell gave none either: a link's
    /// host, a file's name without its extension, a folder's name, an app's ID after its "!".
    /// </summary>
    public static string FallbackName(string target)
    {
        var text = target.Trim().Trim('"');
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme > 0)
        {
            var rest = text[(scheme + 3)..];
            var end = rest.AsSpan().IndexOfAny('/', '?', '#');
            var host = end < 0 ? rest : rest[..end];
            return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host.Length > 0 ? host : text;
        }
        if (text.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase))
        {
            var id = text[@"shell:AppsFolder\".Length..];
            var bang = id.LastIndexOf('!');
            var family = bang > 0 ? id[..bang] : id;
            var cut = family.IndexOf('_');
            family = cut > 0 ? family[..cut] : family;
            var dot = family.LastIndexOf('.');
            return dot >= 0 && dot < family.Length - 1 ? family[(dot + 1)..] : family;
        }
        // Any other link ("ms-settings:display", "mailto:…"): as written, there is nothing shorter to say.
        var colon = text.IndexOf(':');
        if (colon > 1 && text.AsSpan(0, colon).IndexOfAny('\\', '/') < 0)
        {
            return text;
        }
        var trimmed = text.TrimEnd('\\', '/');
        if (trimmed.Length == 2 && trimmed[1] == ':')
        {
            return trimmed + "\\";
        }
        var name = Path.GetFileName(trimmed);
        if (name.Length == 0)
        {
            return text;
        }
        var stem = Path.GetFileNameWithoutExtension(name);
        return stem.Length > 0 ? stem : name;
    }

    /// <summary>
    /// The menu <paramref name="entries"/> make, line by line, as the dock draws it and the settings
    /// app's preview shows it: each built-in row named and pictured, the layouts a row each (§561 a
    /// lone one too: <paramref name="layouts"/> is what the bar offers), a shortcut by its
    /// name — its own, else the shell's (<paramref name="shellName"/>), else <see cref="FallbackName"/>
    /// — and greyed with the reason where <paramref name="check"/> finds its target missing. Then
    /// tidied (<see cref="Tidy{T}"/>): no heading without rows, no divider at either end or beside another. §534: "Lock
    /// the taskbar" ticked where the bars are <paramref name="locked"/>.
    /// </summary>
    public static List<BarMenuLineInfo> Lines(
        IReadOnlyList<BarMenuEntry> entries,
        IReadOnlyList<BarMenuLayoutRow> layouts,
        bool windowsTerminal,
        Func<string, TargetCheck> check,
        Func<string, string?>? shellName = null,
        bool locked = false)
    {
        var lines = new List<BarMenuLineInfo>();
        foreach (var entry in entries)
        {
            switch (entry.Kind)
            {
                case BarMenuKind.Divider:
                    lines.Add(new BarMenuLineInfo(BarMenuLine.Divider, entry));
                    break;
                case BarMenuKind.Heading when entry.Name is { Length: > 0 } words:
                    lines.Add(new BarMenuLineInfo(BarMenuLine.Heading, entry, words));
                    break;
                case BarMenuKind.Action when entry.Action == BarMenuAction.Layouts:
                    // §561: every layout the bar offers, a lone one too, ticked. §366: no heading over them ("the title
                    // for the different layouts should also not be shown").
                    lines.AddRange(layouts.Select((layout, index) =>
                        new BarMenuLineInfo(BarMenuLine.Row, entry, layout.Name, layout.Glyph, Checked: layout.Active, Layout: index)));
                    break;
                case BarMenuKind.Action when Info(entry.Action) is { } info:
                    lines.Add(new BarMenuLineInfo(
                        BarMenuLine.Row,
                        entry,
                        NameOf(entry.Action, windowsTerminal),
                        info.Glyph,
                        Checked: entry.Action == BarMenuAction.LockTaskbar ? locked : null));
                    break;
                case BarMenuKind.Shortcut when entry.Target is { Length: > 0 } target:
                {
                    var name = entry.Name ?? shellName?.Invoke(target) ?? FallbackName(target);
                    var found = check(target);
                    lines.Add(new BarMenuLineInfo(
                        BarMenuLine.Row,
                        entry,
                        name,
                        entry.IconIsGlyph ? entry.Icon : null,
                        Enabled: !found.Missing,
                        Detail: found.Missing ? found.Reason : null));
                    break;
                }
            }
        }
        return Tidy(lines, line => line.Kind);
    }

    /// <summary>
    /// <paramref name="lines"/> as a menu draws them: a heading goes where no row stands under it
    /// before the next heading, divider or the end ("no empty groups"); then a divider goes at
    /// either end and where another one, or nothing but a dropped heading, came before it.
    /// </summary>
    public static List<T> Tidy<T>(IReadOnlyList<T> lines, Func<T, BarMenuLine> kindOf)
    {
        var headed = new List<T>(lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            if (kindOf(lines[index]) == BarMenuLine.Heading && (index + 1 >= lines.Count || kindOf(lines[index + 1]) != BarMenuLine.Row))
            {
                continue;
            }
            headed.Add(lines[index]);
        }
        var tidy = new List<T>(headed.Count);
        foreach (var line in headed)
        {
            if (kindOf(line) == BarMenuLine.Divider && (tidy.Count == 0 || kindOf(tidy[^1]) == BarMenuLine.Divider))
            {
                continue;
            }
            tidy.Add(line);
        }
        while (tidy.Count > 0 && kindOf(tidy[^1]) == BarMenuLine.Divider)
        {
            tidy.RemoveAt(tidy.Count - 1);
        }
        return tidy;
    }
}
