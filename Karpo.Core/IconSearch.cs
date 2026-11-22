// The gallery of Segoe Fluent Icons glyphs a layout or a folder can be given, and
// the search over it: the words are matched against each glyph's words, all of them,
// ignoring case, in any order ("folder open" finds "Open folder").
//
// A glyph's words are a key of en.json each, settings.iconPicker.icon.<id>, a comma list in
// every language: its name first, which its tip and a screen reader say, then words it is found
// by besides ("Home, house"). The search reads them in the language Karpo speaks and in English,
// as the widgets' search does (Strings.List), so "folder" still finds "Ordner".
//
// A layout's icon and a folder's or a pin's icon in their menus (OptionPickers, its search field;
// §228) search it.

namespace Karpo.Core;

/// <summary>One glyph of the gallery, by the id its words are keyed by (settings.iconPicker.icon.&lt;id&gt;).</summary>
public sealed record IconChoice(string Glyph, string Id)
{
    /// <summary>The key of its words.</summary>
    public string Key => "settings.iconPicker.icon." + Id;

    /// <summary>What it is called in the language Karpo speaks: the first of its words.</summary>
    public string Name => Strings.Words(Strings.Text(Key)) is [var first, ..] ? first : Key;

    /// <summary>Every word it is found by: the language's, then English's.</summary>
    public IReadOnlyList<string> Words => Strings.List(Key);
}

public static class IconSearch
{
    /// <summary>
    /// Glyphs that make sense for a taskbar layout or a folder, by their names in the font's
    /// documentation (a few with plainer words added, so the search finds them).
    /// </summary>
    public static readonly IReadOnlyList<IconChoice> All =
    [
        new("\uE8A9", "viewAll"), new("\uE80A", "layout"), new("\uECA5", "tiles"), new("\uE71D", "allApps"),
        new("\uE80F", "home"), new("\uE821", "work"), new("\uE7BE", "education"), new("\uE8F1", "library"),
        new("\uE943", "code"), new("\uEC7A", "developerTools"), new("\uE756", "commandPrompt"), new("\uE8EF", "calculator"),
        new("\uE7FC", "gameController"), new("\uE990", "gameConsole"), new("\uE990", "xbox"), new("\uE7F4", "monitor"),
        new("\uE8B2", "movies"), new("\uE714", "video"), new("\uE93C", "musicAlbum"), new("\uEC4F", "musicNote"),
        new("\uE8D6", "audio"), new("\uE7F6", "headphones"), new("\uE95B", "headset"), new("\uE720", "microphone"),
        new("\uE722", "camera"), new("\uEB9F", "photo"), new("\uEB9F", "picture"), new("\uE790", "colorPalette"),
        new("\uE771", "personalize"), new("\uE929", "inking"), new("\uE70F", "edit"), new("\uE8D2", "font"),
        new("\uE8A5", "document"), new("\uE7C3", "page"), new("\uE8B7", "folder"), new("\uE838", "folderOpen"),
        new("\uEC50", "fileExplorer"), new("\uE8F4", "newFolder"), new("\uE7B8", "package"), new("\uE896", "download"),
        new("\uE715", "mail"), new("\uE8BD", "message"), new("\uE8F2", "chat"), new("\uE716", "people"),
        new("\uE77B", "contact"), new("\uE910", "accounts"), new("\uEA69", "admin"), new("\uE717", "phone"),
        new("\uE787", "calendar"), new("\uE8BF", "calendarDay"), new("\uE823", "recent"), new("\uE916", "stopwatch"),
        new("\uE81C", "history"), new("\uE708", "quietHours"), new("\uE706", "brightness"), new("\uEA80", "lightBulb"),
        new("\uE945", "lightning"), new("\uE7E8", "powerButton"), new("\uE83F", "battery"), new("\uE753", "cloud"),
        new("\uE774", "globe"), new("\uE909", "world"), new("\uE707", "mapPin"), new("\uE81D", "location"),
        new("\uE709", "airplane"), new("\uE804", "car"), new("\uE806", "bus"), new("\uE7C0", "train"),
        new("\uE719", "shop"), new("\uE7BF", "shoppingCart"), new("\uE825", "bank"), new("\uE8C7", "paymentCard"),
        new("\uE734", "star"), new("\uE735", "starFilled"), new("\uEB51", "heart"), new("\uEB52", "heartFilled"),
        new("\uE7C1", "flag"), new("\uE8EC", "tag"), new("\uE718", "pin"), new("\uE840", "pinned"),
        new("\uE713", "settings"), new("\uE90F", "repair"), new("\uE912", "manage"), new("\uE770", "system"),
        new("\uE9D9", "diagnostic"), new("\uE9D2", "areaChart"), new("\uEB05", "pieChart"), new("\uE9F9", "report"),
        new("\uEA18", "shield"), new("\uE72E", "lock"), new("\uE785", "unlock"), new("\uE928", "fingerprint"),
        new("\uE772", "devices"), new("\uE7F8", "laptop"), new("\uE8CC", "mobile"), new("\uE8EA", "cellPhone"),
        new("\uE977", "pc"), new("\uE765", "keyboard"), new("\uE962", "mouse"), new("\uE749", "print"),
        new("\uE701", "wifi"), new("\uE702", "bluetooth"), new("\uE839", "ethernet"), new("\uEDA3", "network"),
        new("\uEA8F", "notification"), new("\uE789", "megaphone"), new("\uE946", "info"), new("\uE897", "help"),
        new("\uE8BE", "leaf"), new("\uE95E", "health"), new("\uE822", "construction"), new("\uEA86", "puzzle"),
        new("\uE99A", "robot"), new("\uE8B0", "click"), new("\uE768", "play"), new("\uE8B1", "shuffle"),
        new("\uE895", "sync"), new("\uE72C", "refresh"), new("\uE8FD", "bulletedList"), new("\uE9D5", "checklist"),
        new("\uE73E", "checkMark"), new("\uE8C9", "important"), new("\uE7BA", "warning"), new("\uE7C4", "taskView"),
    ];

    /// <summary>
    /// The glyphs whose words hold every word of <paramref name="query"/> (any order, ignoring
    /// case), in the language Karpo speaks or in English; everything when it asks for nothing.
    /// </summary>
    public static IReadOnlyList<IconChoice> Search(string? query) => Search(All, query);

    /// <summary>The same over any gallery, for tests.</summary>
    public static IReadOnlyList<IconChoice> Search(IReadOnlyList<IconChoice> gallery, string? query) =>
        Words(query).Length == 0 ? gallery : [.. gallery.Where(choice => Matches(choice.Words, query))];

    /// <summary>
    /// Whether <paramref name="found"/> — an icon's words — hold every word of <paramref name="query"/>,
    /// as the search matches them; true for nothing asked. The menu's picker filters its tiles by it (§249).
    /// </summary>
    public static bool Matches(IReadOnlyList<string> found, string? query) =>
        Words(query).All(word => found.Any(each => each.Contains(word, StringComparison.CurrentCultureIgnoreCase)));

    private static string[] Words(string? query) => (query ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
