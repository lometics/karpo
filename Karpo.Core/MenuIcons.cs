// menus: the icons a menu offers a folder, a pin or a feed (2026-10-19, docs\widgets.md §224, §249,
// §306): the whole gallery of Segoe Fluent Icons glyphs Settings' picker has (IconSearch), each glyph
// once, in one grid — files, places, media, people and time, devices, then tools and symbols, one
// after the other with no headings between them (§306, the person: "icons should not be grouped into
// sections / groups anymore"). Each is named by the gallery's own words (settings.iconPicker.icon.<id>),
// so its tooltip says what the page says, and found by them and by the words of any other id the
// gallery gives the same glyph ("xbox" finds the console).

namespace Karpo.Core;

/// <summary>A glyph of the menu's icons, by the gallery's id its words are keyed by.</summary>
public sealed record MenuIcon(string Glyph, string Id)
{
    /// <summary>What it is called in the language Karpo speaks: the first of the gallery's words for it.</summary>
    public string Name => Strings.Words(Strings.Text("settings.iconPicker.icon." + Id)) is [var first, ..] ? first : Id;

    /// <summary>Every word it is found by: each gallery entry's of its glyph, in the language Karpo speaks and in English.</summary>
    public IReadOnlyList<string> Words => [.. IconSearch.All.Where(choice => choice.Glyph == Glyph).SelectMany(choice => choice.Words)];
}

/// <summary>The menu's icons; see the top of the file.</summary>
public static class MenuIcons
{
    // The ids in the order shown, alike ones together; every glyph of the gallery once.
    private static readonly string[] Ids =
    [
        "folder", "folderOpen", "newFolder", "fileExplorer", "document", "page", "package", "download", "library", "edit", "font",
            "tag", "bulletedList", "checklist", "viewAll", "layout", "tiles", "allApps",
        "home", "work", "education", "globe", "world", "mapPin", "location", "airplane", "car", "bus", "train", "shop",
            "shoppingCart", "bank", "paymentCard", "cloud", "leaf",
        "movies", "video", "musicNote", "musicAlbum", "audio", "headphones", "headset", "microphone", "camera", "photo",
            "colorPalette", "personalize", "inking", "gameController", "gameConsole", "play", "shuffle",
        "mail", "message", "chat", "people", "contact", "accounts", "admin", "phone", "calendar", "calendarDay", "recent",
            "stopwatch", "history", "quietHours", "notification", "megaphone", "star", "starFilled", "heart", "heartFilled", "flag",
        "monitor", "devices", "laptop", "mobile", "cellPhone", "pc", "keyboard", "mouse", "print", "wifi", "bluetooth", "ethernet",
            "network", "battery", "powerButton", "brightness", "lightBulb", "lightning", "taskView", "click",
        "settings", "system", "repair", "manage", "construction", "developerTools", "code", "commandPrompt", "calculator",
            "diagnostic", "areaChart", "pieChart", "report", "shield", "lock", "unlock", "fingerprint", "info", "help", "important",
            "warning", "checkMark", "health", "puzzle", "robot", "sync", "refresh", "pin", "pinned",
    ];

    public static readonly IReadOnlyList<MenuIcon> All =
    [
        .. Ids.Select(id => new MenuIcon(IconSearch.All.First(choice => choice.Id == id).Glyph, id)),
    ];
}
