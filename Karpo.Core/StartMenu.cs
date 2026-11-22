// §641: the Start group's widgets, what they know without Windows (docs\widgets.md §641).
//
// The person, 2026-11-09: "create a widget group that looks like the start. for this you can also create new widgets to
// achieve the look and features that the start has (mark them as beta in the add) … make those widgets / groups feature
// rich." The ready-made group "Start (beta)" (Presets.StartId) is a folder of five widgets of their own, each a line of a
// list folder: the pinned apps (startPins), every app (startApps), the recommended files and new apps (startRecommended)
// and the account with the power menu (startProfile). §653: its search box (startSearch) went, the person, 2026-11-09:
// "start search folder widget does not seem to do anything. remove it."
//
// What is here is pure: the apps as Start lists them (StartApp, read off the shell's Apps folder by the dock), what was
// opened from a Start widget and the first time each app was seen, which is what "Recently added" is (StartUses), the
// A–Z and the category groups of All apps (StartLetters, StartCategories), the pins (StartPins) and what Recommended
// lists (StartRecommendations). The dock reads Windows and draws (Karpo.Dock\StartWidgets); the tests hold all of this without it.

using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>§641: how All apps lists them, as Windows 11's Start offers: in categories, a grid or a list, A to Z.</summary>
[JsonConverter(typeof(TolerantEnumConverter<StartView>))]
public enum StartView
{
    [JsonStringEnumMemberName("category")] Category,
    [JsonStringEnumMemberName("grid")] Grid,
    [JsonStringEnumMemberName("list")] List,
}

/// <summary>§641: a folder beside the power button, as Windows' Start › Folders offers them.</summary>
[JsonConverter(typeof(TolerantEnumConverter<StartPlace>))]
public enum StartPlace
{
    [JsonStringEnumMemberName("settings")] Settings,
    [JsonStringEnumMemberName("fileExplorer")] FileExplorer,
    [JsonStringEnumMemberName("documents")] Documents,
    [JsonStringEnumMemberName("downloads")] Downloads,
    [JsonStringEnumMemberName("music")] Music,
    [JsonStringEnumMemberName("pictures")] Pictures,
    [JsonStringEnumMemberName("videos")] Videos,
    [JsonStringEnumMemberName("network")] Network,
    [JsonStringEnumMemberName("personal")] Personal,
}

/// <summary>
/// §641: what a Start widget is set to. One record for the five, each reading its own part: the pins of the pinned apps
/// (null for this PC's usual ones until one is pinned, unpinned or moved), All apps' view, the folders beside power, and
/// what Recommended lists. Nothing is written for what nobody set.
/// </summary>
public sealed class StartOptions
{
    /// <summary>The pinned apps, by their target (<see cref="StartApp.Target"/>), in their order; null for <see cref="StartPins.Defaults"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Pins { get; set; }

    /// <summary>All apps' view; null for <see cref="StartView.Category"/>, Windows' own first.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StartView? View { get; set; }

    /// <summary>The folders beside the power button, in Windows' order; null for <see cref="StartPlaces.Defaults"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<StartPlace>? Places { get; set; }

    /// <summary>Recommended lists the files opened lately; null for yes.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Files { get; set; }

    /// <summary>Recommended lists the apps added lately; null for yes.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? NewApps { get; set; }

    /// <summary>The view All apps shows.</summary>
    [JsonIgnore]
    public StartView ViewShown => View is { } view && Enum.IsDefined(view) ? view : StartView.Category;

    /// <summary>
    /// As a line of <paramref name="kind"/> keeps it: each part only on the kind that reads it, a default not written,
    /// the pins each once and never empty strings, the places known ones each once in Windows' order; null where
    /// nothing is left.
    /// </summary>
    public StartOptions? Normalized(ListKind kind)
    {
        var kept = new StartOptions
        {
            Pins = kind == ListKind.StartPins && Pins is { } pins
                ? [.. pins.Where(pin => !string.IsNullOrWhiteSpace(pin)).Select(pin => pin.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)]
                : null,
            View = kind == ListKind.StartApps && View is { } view && Enum.IsDefined(view) && view != StartView.Category ? view : null,
            Places = kind == ListKind.StartProfile && Places is { } places
                ? [.. places.Where(Enum.IsDefined).Distinct().Order()]
                : null,
            Files = kind == ListKind.StartRecommended && Files == false ? false : null,
            NewApps = kind == ListKind.StartRecommended && NewApps == false ? false : null,
        };
        return kept.Pins is null && kept.View is null && kept.Places is null && kept.Files is null && kept.NewApps is null ? null : kept;
    }
}

/// <summary>§641: one app as Start lists it: its name and what opens it, the shell's Apps folder's item ("shell:AppsFolder\…").</summary>
/// <param name="Name">What Start calls it, in Windows' language.</param>
/// <param name="Target">What opens it, and what names it everywhere: the pins, the uses, the menus.</param>
public sealed record StartApp(string Name, string Target)
{
    /// <summary>The Apps folder's address an item's target starts with.</summary>
    public const string AppsFolder = @"shell:AppsFolder\";

    /// <summary>Apps by their names as a person reads them: folded first ("Écran" by "Edge"), then as written.</summary>
    public static readonly IComparer<StartApp> ByName = Comparer<StartApp>.Create((one, other) =>
        string.CompareOrdinal(CatalogueSearch.Fold(one.Name), CatalogueSearch.Fold(other.Name)) is var folded and not 0 ? folded : string.CompareOrdinal(one.Name, other.Name));

    /// <summary>Its id in the Apps folder: an AUMID, or a known folder's GUID and a path for an app without one.</summary>
    public string Id => Target.StartsWith(AppsFolder, StringComparison.OrdinalIgnoreCase) ? Target[AppsFolder.Length..] : Target;

    /// <summary>
    /// The program file it runs, for an app the Apps folder names by a path ("{6D809377-…}\Notepad++\notepad++.exe" or
    /// "C:\…\app.exe"): what Run as administrator and Open file location need. Null for a packaged app (an AUMID).
    /// </summary>
    public string? Program => StartPaths.ProgramOf(Id);
}

/// <summary>§641: the paths the Apps folder names desktop apps by.</summary>
public static class StartPaths
{
    // The known folders the Apps folder writes a desktop app's path from.
    private static readonly (string Guid, Environment.SpecialFolder Folder)[] Known =
    [
        ("{6D809377-6AF0-444B-8957-A3773F02200E}", Environment.SpecialFolder.ProgramFiles),
        ("{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}", Environment.SpecialFolder.ProgramFilesX86),
        ("{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}", Environment.SpecialFolder.System),
        ("{D65231B0-B2F1-4857-A4CE-A8E7C6EA7D27}", Environment.SpecialFolder.SystemX86),
        ("{F38BF404-1D43-42F2-9305-67DE0B28FC23}", Environment.SpecialFolder.Windows),
        ("{F1B32785-6FBA-4FCF-9D55-7B8E7F157091}", Environment.SpecialFolder.LocalApplicationData),
        ("{3EB685DB-65F9-4CF6-A03A-E3EF65729F3D}", Environment.SpecialFolder.ApplicationData),
    ];

    /// <summary>The program file an Apps folder id names, where it names one by a path; null for an AUMID.</summary>
    public static string? ProgramOf(string id)
    {
        foreach (var (guid, folder) in Known)
        {
            if (id.StartsWith(guid, StringComparison.OrdinalIgnoreCase))
            {
                var root = Environment.GetFolderPath(folder);
                return root.Length == 0 ? null : root + id[guid.Length..];
            }
        }
        return id.Length > 2 && id[1] == ':' && id[2] == '\\' ? id : null;
    }
}

/// <summary>
/// §641: how often and how lately each app was opened from a Start widget, and when it was first seen — Karpo's own
/// record, on this PC (Config.LocalFolder, start.tsv). The first sighting is "Recently added". The first time the apps are seen at all, every one is
/// seen long ago: an app is new only once Karpo knew the PC without it.
/// </summary>
public sealed class StartUses
{
    /// <summary>How long an app counts as recently added after it was first seen, as Windows' Start keeps it.</summary>
    public static readonly TimeSpan NewFor = TimeSpan.FromDays(7);

    private readonly Dictionary<string, Use> uses = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>One app's record: how many times opened, the last time, and when it was first seen (MinValue: before Karpo looked).</summary>
    public readonly record struct Use(int Count, DateTime LastUtc, DateTime SeenUtc);

    /// <summary>Whether the apps have been looked at once: until then nothing is new.</summary>
    public bool Looked { get; private set; }

    /// <summary>What is known of <paramref name="target"/>; null for nothing.</summary>
    public Use? Of(string target) => uses.TryGetValue(target, out var use) ? use : null;

    /// <summary>Opened now: one more time, the last time now.</summary>
    public void Opened(string target, DateTime nowUtc)
    {
        var was = Of(target) ?? new Use(0, default, Looked ? nowUtc : DateTime.MinValue);
        uses[target] = was with { Count = was.Count + 1, LastUtc = nowUtc };
    }

    /// <summary>
    /// The apps as they are now: one never seen is seen now (long ago, the first time ever); true where anything changed,
    /// so it is written.
    /// </summary>
    public bool Saw(IEnumerable<string> targets, DateTime nowUtc)
    {
        var changed = !Looked;
        foreach (var target in targets)
        {
            if (!uses.ContainsKey(target))
            {
                uses[target] = new Use(0, default, Looked ? nowUtc : DateTime.MinValue);
                changed = true;
            }
        }
        Looked = true;
        return changed;
    }

    /// <summary><paramref name="target"/> first seen at <paramref name="seenUtc"/>, whatever was known of it: the samples' app added lately.</summary>
    public void SeenAt(string target, DateTime seenUtc)
    {
        var was = Of(target) ?? new Use(0, default, seenUtc);
        uses[target] = was with { SeenUtc = seenUtc };
    }

    /// <summary>Whether <paramref name="target"/> was first seen within <see cref="NewFor"/> and Karpo had looked before.</summary>
    public bool IsNew(string target, DateTime nowUtc) =>
        Of(target) is { SeenUtc: var seen } && seen != DateTime.MinValue && nowUtc - seen <= NewFor && nowUtc >= seen;

    /// <summary>The file's text: "looked" first where the apps were looked at, then a line per app, tab-separated.</summary>
    public string Write()
    {
        var text = new StringBuilder();
        if (Looked)
        {
            text.Append("looked\n"); // not-ui: the file's own word
        }
        foreach (var (target, use) in uses.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            text.Append(target.Replace('\t', ' ').Replace('\n', ' ')).Append('\t')
                .Append(use.Count.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(use.LastUtc.Ticks.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(use.SeenUtc.Ticks.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        return text.ToString();
    }

    /// <summary>What <see cref="Write"/> wrote; a line that does not read is left out, nothing at all for nothing.</summary>
    public static StartUses Read(string? text)
    {
        var read = new StartUses();
        foreach (var line in (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Trim() == "looked")
            {
                read.Looked = true;
                continue;
            }
            var parts = line.Split('\t');
            if (parts.Length == 4 && parts[0].Length > 0
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count >= 0
                && long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var last) && last >= 0 && last <= DateTime.MaxValue.Ticks
                && long.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seen) && seen >= 0 && seen <= DateTime.MaxValue.Ticks)
            {
                read.uses[parts[0]] = new Use(count, new DateTime(last, DateTimeKind.Utc), new DateTime(seen, DateTimeKind.Utc));
            }
        }
        return read;
    }
}

/// <summary>§641: All apps A to Z, as Windows' list heads them: a letter each, "#" for a digit, "&amp;" for anything else first.</summary>
public static class StartLetters
{
    /// <summary>The heading of <paramref name="name"/>: its first letter, folded and in capitals; "#" for a digit; "&amp;" for a sign.</summary>
    public static string Of(string name)
    {
        var folded = CatalogueSearch.Fold(name.TrimStart());
        if (folded.Length == 0)
        {
            return "&";
        }
        var first = folded[0];
        return char.IsDigit(first) ? "#" : char.IsLetter(first) ? char.ToUpperInvariant(first).ToString() : "&";
    }

    /// <summary>The apps under their headings, "&amp;" then "#" then the letters, each heading's apps by name.</summary>
    public static List<(string Head, List<StartApp> Apps)> Group(IEnumerable<StartApp> apps) =>
        [.. apps
            .GroupBy(app => Of(app.Name))
            .OrderBy(group => group.Key switch { "&" => 0, "#" => 1, _ => 2 })
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (group.Key, group.OrderBy(app => app, StartApp.ByName).ToList()))];
}

/// <summary>§641: the categories of All apps' Category view, Windows' own (2025's Start).</summary>
public enum StartCategory
{
    Productivity,
    Creativity,
    Social,
    Entertainment,
    Games,
    Utilities,
    Other,
}

/// <summary>
/// §641: which category an app is in. Windows sorts by what the Store knows of an app, which no program can ask; Karpo
/// goes by the words in its name against a table of the apps people have (a browser is productivity, a game launcher a
/// game). As Windows does, a category of fewer than three apps folds into Other.
/// </summary>
public static class StartCategories
{
    /// <summary>The fewest apps a category shows on its own; fewer go to Other.</summary>
    public const int Fewest = 3;

    // not-ui: app names and the words in them, which are names in every language.
    private static readonly (StartCategory Category, string[] Words)[] Table =
    [
        (StartCategory.Games, ["xbox", "steam", "epic games", "gog galaxy", "battle.net", "ea app", "ubisoft", "minecraft", "solitaire", "game bar", "riot client", "roblox", "games", "game"]),
        (StartCategory.Social, ["whatsapp", "discord", "telegram", "signal", "skype", "messenger", "instagram", "facebook", "linkedin", "zoom", "slack", "phone link", "threads", "mastodon", "teams"]),
        (StartCategory.Entertainment, ["spotify", "netflix", "media player", "movies", "prime video", "disney", "vlc", "itunes", "apple music", "apple tv", "youtube", "plex", "kodi", "audible", "tiktok", "twitch", "music", "podcasts"]),
        (StartCategory.Creativity, ["paint", "photos", "clipchamp", "photoshop", "illustrator", "premiere", "lightroom", "gimp", "blender", "inkscape", "affinity", "figma", "davinci", "obs studio", "audacity", "sound recorder", "camera", "canva", "krita", "designer", "3d"]),
        (StartCategory.Productivity, ["word", "excel", "powerpoint", "outlook", "onenote", "office", "microsoft 365", "copilot", "notepad", "onedrive", "to do", "calendar", "mail", "acrobat", "visual studio", "code", "notion", "obsidian", "todoist", "evernote", "libreoffice", "sticky notes", "whiteboard", "access", "publisher", "dropbox", "edge", "chrome", "firefox", "brave", "opera", "vivaldi", "browser", "pdf"]),
        (StartCategory.Utilities, ["settings", "calculator", "clock", "terminal", "powershell", "command prompt", "task manager", "file explorer", "control panel", "snipping tool", "windows tools", "registry", "device manager", "7-zip", "winrar", "powertoys", "everything", "windows security", "quick assist", "remote desktop", "magnifier", "narrator", "keyboard", "character map", "disk", "backup", "update", "store", "recorder", "uninstall", "help", "tips", "feedback", "manager", "tool", "tools", "driver", "console"]),
    ];

    /// <summary>The category <paramref name="name"/>'s words put it in: the first of the table's that one of them matches; Other else.</summary>
    public static StartCategory Of(string name)
    {
        var folded = " " + string.Join(' ', CatalogueSearch.Fold(name).Split([' ', '-', '_', '(', ')', ':'], StringSplitOptions.RemoveEmptyEntries)) + " ";
        foreach (var (category, words) in Table)
        {
            if (words.Any(word => folded.Contains(" " + word + " ", StringComparison.Ordinal)))
            {
                return category;
            }
        }
        return StartCategory.Other;
    }

    /// <summary>
    /// The apps in their categories, in the categories' order, each category's apps by name; a category of fewer than
    /// <see cref="Fewest"/> in Other, which stands last. Only categories that have apps.
    /// </summary>
    public static List<(StartCategory Category, List<StartApp> Apps)> Group(IEnumerable<StartApp> apps)
    {
        var all = apps.GroupBy(app => Of(app.Name)).ToDictionary(group => group.Key, group => group.ToList());
        var other = all.TryGetValue(StartCategory.Other, out var rest) ? rest : [];
        var groups = new List<(StartCategory, List<StartApp>)>();
        foreach (var category in Enum.GetValues<StartCategory>().Where(each => each != StartCategory.Other))
        {
            if (!all.TryGetValue(category, out var those))
            {
                continue;
            }
            if (those.Count < Fewest)
            {
                other.AddRange(those);
                continue;
            }
            groups.Add((category, [.. those.OrderBy(app => app, StartApp.ByName)]));
        }
        if (other.Count > 0)
        {
            groups.Add((StartCategory.Other, [.. other.OrderBy(app => app, StartApp.ByName)]));
        }
        return groups;
    }
}

/// <summary>§641: the pinned apps: this PC's usual ones until somebody changes them, and the changes.</summary>
public static class StartPins
{
    /// <summary>
    /// The apps pinned until somebody pins their own, in Windows 11's Start's order where this PC has them, by their
    /// Apps folder ids (Windows' own pins are kept in a file no program may read, so Karpo starts from the same apps).
    /// </summary>
    public static readonly string[] Usual =
    [
        "MSEdge",
        "Microsoft.MicrosoftOfficeHub_8wekyb3d8bbwe!Microsoft.MicrosoftOfficeHub",
        "Microsoft.Office.WINWORD.EXE.15",
        "Microsoft.Office.EXCEL.EXE.15",
        "Microsoft.Office.POWERPNT.EXE.15",
        "Microsoft.OutlookForWindows_8wekyb3d8bbwe!Microsoft.OutlookforWindows",
        "Microsoft.Copilot_8wekyb3d8bbwe!App",
        "Microsoft.WindowsStore_8wekyb3d8bbwe!App",
        "Microsoft.Windows.Photos_8wekyb3d8bbwe!App",
        "windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel",
        "Microsoft.Windows.Explorer",
        "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
        "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App",
        "Microsoft.WindowsAlarms_8wekyb3d8bbwe!App",
        "Microsoft.Paint_8wekyb3d8bbwe!App",
        "Microsoft.ScreenSketch_8wekyb3d8bbwe!App",
        "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App",
        "Microsoft.Todos_8wekyb3d8bbwe!App",
        "Microsoft.GamingApp_8wekyb3d8bbwe!Microsoft.Xbox.App",
        "Clipchamp.Clipchamp_yxz26nhyzhsrt!App",
        "Microsoft.YourPhone_8wekyb3d8bbwe!App",
    ];

    /// <summary>The most usual pins: three lines of six, as Start's default.</summary>
    public const int MostUsual = 18;

    /// <summary>The usual pins this PC has (<paramref name="installed"/>), their targets, in <see cref="Usual"/>'s order.</summary>
    public static List<string> Defaults(IEnumerable<StartApp> installed)
    {
        var byId = installed.GroupBy(app => app.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        return [.. Usual.Select(id => byId.TryGetValue(id, out var app) ? app.Target : null).OfType<string>().Take(MostUsual)];
    }

    /// <summary>
    /// §655: the pinned apps line's own record in <paramref name="config"/> — the first Start (beta) pinned apps line of any
    /// layout's folder —, whose pins Windows search's home shows; null where there is none (this PC's usual ones, then).
    /// </summary>
    public static StartOptions? OfConfig(Config config) =>
        config.AllIslands.SelectMany(island => island.Folder?.Cells ?? []).Select(cell => cell?.Island)
            .FirstOrDefault(island => island?.List == ListKind.StartPins)?.Start;

    /// <summary>The pins a line shows: its own, else this PC's usual ones.</summary>
    public static List<string> Of(StartOptions? options, IEnumerable<StartApp> installed) =>
        options?.Pins is { } pins ? [.. pins] : Defaults(installed);

    /// <summary>The apps <paramref name="pins"/> name that this PC has, in their order; a pin of an app gone is not shown (and kept).</summary>
    public static List<StartApp> Apps(IEnumerable<string> pins, IEnumerable<StartApp> installed)
    {
        var byTarget = installed.GroupBy(app => app.Target, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        return [.. pins.Select(pin => byTarget.TryGetValue(pin, out var app) ? app : null).OfType<StartApp>()];
    }

    /// <summary><paramref name="target"/> pinned at the end, where it is not pinned already.</summary>
    public static List<string> Pin(IReadOnlyList<string> pins, string target) =>
        pins.Contains(target, StringComparer.OrdinalIgnoreCase) ? [.. pins] : [.. pins, target];

    /// <summary><paramref name="target"/> unpinned.</summary>
    public static List<string> Unpin(IReadOnlyList<string> pins, string target) =>
        [.. pins.Where(pin => !string.Equals(pin, target, StringComparison.OrdinalIgnoreCase))];

    /// <summary><paramref name="target"/> first, as Start's "Move to front".</summary>
    public static List<string> ToFront(IReadOnlyList<string> pins, string target) =>
        pins.Contains(target, StringComparer.OrdinalIgnoreCase) ? [target, .. Unpin(pins, target)] : [.. pins];

    /// <summary><paramref name="target"/> moved to <paramref name="index"/> (held to the list), the others keeping their order: a drag, or Alt+Shift and an arrow.</summary>
    public static List<string> Move(IReadOnlyList<string> pins, string target, int index)
    {
        var rest = Unpin(pins, target);
        if (rest.Count == pins.Count)
        {
            return [.. pins];
        }
        rest.Insert(Math.Clamp(index, 0, rest.Count), pins.First(pin => string.Equals(pin, target, StringComparison.OrdinalIgnoreCase)));
        return rest;
    }
}

/// <summary>
/// §641: what a Start widget's menu or a drag changes in the config: a pin, an unpin, a pin moved, All apps' view. The pins
/// changed are the pinned apps line's itself, or, from another Start line (All apps' "Pin to Start", Recommended's), the
/// first pinned apps line of the same folder. Each a change of the config the dock applies (one step of the undo).
/// </summary>
public static class StartEdits
{
    /// <summary>The words a menu's row or a drag says what it changes with.</summary>
    public const string Pin = "pin", Unpin = "unpin", Front = "front", Move = "move", View = "view";

    /// <summary>
    /// The pinned apps line <paramref name="id"/> (a folder's line, "folder/line") pins to in <paramref name="config"/>'s
    /// bar <paramref name="owner"/>: itself, else the first of its folder; null for none.
    /// </summary>
    public static CellItem? PinsOf(Config config, string? owner, string id)
    {
        if (config.FindIsland(owner, id) is not CellItem line)
        {
            return null;
        }
        if (line.List == ListKind.StartPins)
        {
            return line;
        }
        var cut = id.IndexOf('/');
        return cut > 0 && config.FindIsland(owner, id[..cut]) is BarItem { Folder: { } folder }
            ? folder.Cells.Select(cell => cell.Island).FirstOrDefault(island => island.List == ListKind.StartPins)
            : null;
    }

    /// <summary>
    /// Changes <paramref name="config"/> as <paramref name="verb"/> says for line <paramref name="id"/> of bar
    /// <paramref name="owner"/>: <paramref name="target"/> pinned, unpinned, moved to the front or to <paramref name="index"/>,
    /// or All apps' view set to <paramref name="target"/>'s word. The pins start from what the line shows (this PC's usual
    /// ones, <paramref name="installed"/>, until somebody changed them). False where nothing changed.
    /// </summary>
    public static bool Apply(Config config, string? owner, string id, string verb, string target, IReadOnlyList<StartApp> installed, int index = 0)
    {
        if (verb == View)
        {
            if (config.FindIsland(owner, id) is not CellItem { List: ListKind.StartApps } apps
                || !Enum.TryParse<StartView>(target, ignoreCase: true, out var view) || apps.Start?.ViewShown == view)
            {
                return false;
            }
            (apps.Start ??= new StartOptions()).View = view;
            apps.Normalize();
            return true;
        }
        if (PinsOf(config, owner, id) is not { } line)
        {
            return false;
        }
        var pins = StartPins.Of(line.Start, installed);
        var next = verb switch
        {
            Pin => StartPins.Pin(pins, target),
            Unpin => StartPins.Unpin(pins, target),
            Front => StartPins.ToFront(pins, target),
            Move => StartPins.Move(pins, target, index),
            _ => pins,
        };
        if (next.SequenceEqual(pins, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }
        (line.Start ??= new StartOptions()).Pins = next;
        line.Normalize();
        return true;
    }
}

/// <summary>§641: the folders beside the power button.</summary>
public static class StartPlaces
{
    /// <summary>Those a new line shows: Settings and File Explorer, the two most people add.</summary>
    public static readonly StartPlace[] Defaults = [StartPlace.Settings, StartPlace.FileExplorer];

    /// <summary>The folders <paramref name="options"/> shows, in Windows' order.</summary>
    public static IReadOnlyList<StartPlace> Of(StartOptions? options) => options?.Places is { } places ? places : Defaults;

    /// <summary>What a folder opens: a Settings address, File Explorer, a known folder's shell address.</summary>
    public static string Target(StartPlace place) => place switch
    {
        StartPlace.Settings => "ms-settings:",
        StartPlace.FileExplorer => "shell:MyComputerFolder",
        StartPlace.Documents => "shell:Personal",
        StartPlace.Downloads => "shell:Downloads",
        StartPlace.Music => "shell:My Music",
        StartPlace.Pictures => "shell:My Pictures",
        StartPlace.Videos => "shell:My Video",
        StartPlace.Network => "shell:NetworkPlacesFolder",
        _ => "shell:UsersFilesFolder",
    };

    /// <summary>Its Segoe Fluent Icons glyph, Start's own.</summary>
    public static string Glyph(StartPlace place) => place switch
    {
        StartPlace.Settings => "\uE713",
        StartPlace.FileExplorer => "\uEC50",
        StartPlace.Documents => "\uE8A5",
        StartPlace.Downloads => "\uE896",
        StartPlace.Music => "\uEC4F",
        StartPlace.Pictures => "\uEB9F",
        StartPlace.Videos => "\uE8B2",
        StartPlace.Network => "\uE968",
        _ => "\uEC25",
    };
}

/// <summary>§641: one of Recommended's: a file opened lately, or an app added lately.</summary>
/// <param name="Name">What it is called: the file's name, the app's.</param>
/// <param name="Target">What opens it: the file's path, the app's target.</param>
/// <param name="WhenUtc">When the file was opened, the app first seen.</param>
/// <param name="App">An app added lately, not a file.</param>
public sealed record StartRecommendation(string Name, string Target, DateTime WhenUtc, bool App);

/// <summary>§641: what Recommended lists.</summary>
public static class StartRecommendations
{
    /// <summary>The most it lists: Start's six, two lines of three; more scroll in a taller line.</summary>
    public const int Most = 12;

    /// <summary>
    /// The newest first of the files (<paramref name="files"/>) and the apps added lately (<paramref name="apps"/>) that
    /// <paramref name="options"/> lists, at most <paramref name="most"/>.
    /// </summary>
    public static List<StartRecommendation> Of(StartOptions? options, IEnumerable<StartRecommendation> files, IEnumerable<StartRecommendation> apps, int most = Most) =>
        [.. (options?.Files == false ? [] : files)
            .Concat(options?.NewApps == false ? [] : apps)
            .OrderByDescending(each => each.WhenUtc)
            .Take(most)];

    /// <summary>
    /// When a file was opened, as Start says it: the time today ("14:32"), the day and the time this week ("Monday 09:10"),
    /// else the date — in the region's own formats and words (the dock's Nls: <paramref name="time"/>, <paramref name="day"/>,
    /// <paramref name="date"/>), no more of Karpo's.
    /// </summary>
    public static string When(DateTime whenUtc, DateTime nowUtc, Func<DateTime, string> time, Func<DayOfWeek, string> day, Func<DateOnly, string> date)
    {
        var when = whenUtc.ToLocalTime();
        var now = nowUtc.ToLocalTime();
        if (when.Date == now.Date)
        {
            return time(when);
        }
        if (now.Date - when.Date < TimeSpan.FromDays(7) && when <= now)
        {
            return $"{day(when.DayOfWeek)} {time(when)}";
        }
        return date(DateOnly.FromDateTime(when));
    }
}
