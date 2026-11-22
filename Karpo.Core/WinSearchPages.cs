// §642: the pages of Windows' Settings Windows search finds, and the words it finds them by. The pages are Karpo's one
// list of them (WindowsPages, "Display settings" in every language Karpo speaks); what a person types for one that is
// not its name ("resolution", "wallpaper", "uninstall") is here, in English, beside its name in the language Karpo
// speaks and in English, as Windows' own Settings search finds a page by words that are not in its title.

namespace Karpo.Core;

/// <summary>§642: Settings' pages as Windows search finds them.</summary>
public static class WinSearchPages
{
    /// <summary>A Segoe Fluent Icons code point: Settings' gear, every page's glyph.</summary>
    public const string Glyph = "";

    /// <summary>§652: Windows' Settings app, by its Apps folder address: whose icon every page wears, as Windows' search shows them.</summary>
    public const string SettingsApp = StartApp.AppsFolder +"windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel";

    /// <summary>What else a page is searched for by, beside its name, by its address.</summary>
    private static readonly Dictionary<string, string[]> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        [WindowsPages.Taskbar.Uri] = ["taskbar", "system tray", "notification area"],
        [WindowsPages.Start.Uri] = ["start menu", "recommended", "pinned"],
        [WindowsPages.Search.Uri] = ["search permissions", "safe search", "history"],
        [WindowsPages.Multitasking.Uri] = ["snap", "alt tab", "virtual desktops", "windows"],
        [WindowsPages.Clipboard.Uri] = ["clipboard history", "copy paste", "win v"],
        [WindowsPages.Pen.Uri] = ["pen", "stylus", "ink"],
        [WindowsPages.Typing.Uri] = ["autocorrect", "spelling", "touch keyboard", "suggestions"],
        [WindowsPages.Touchpad.Uri] = ["trackpad", "gestures", "scrolling"],
        [WindowsPages.Network.Uri] = ["internet", "ethernet", "proxy", "ip", "dns", "adapter"],
        [WindowsPages.Bluetooth.Uri] = ["devices", "pair", "headphones", "mouse", "keyboard"],
        [WindowsPages.Sound.Uri] = ["audio", "volume", "speakers", "microphone", "output", "input"],
        [WindowsPages.Display.Uri] = ["screen", "resolution", "scale", "monitor", "brightness", "refresh rate", "hdr", "orientation"],
        [WindowsPages.Power.Uri] = ["battery", "sleep", "screen off", "power mode", "energy"],
        [WindowsPages.Language.Uri] = ["region", "keyboard layout", "input", "format", "locale"],
        [WindowsPages.DateTime.Uri] = ["clock", "time zone", "sync time"],
        [WindowsPages.AirplaneMode.Uri] = ["flight mode", "radios"],
        [WindowsPages.MobileHotspot.Uri] = ["hotspot", "tethering", "share internet"],
        [WindowsPages.Vpn.Uri] = ["vpn", "proxy"],
        [WindowsPages.DataUsage.Uri] = ["data limit", "metered"],
        [WindowsPages.EnergySaver.Uri] = ["battery saver", "power saving"],
        [WindowsPages.NightLight.Uri] = ["blue light", "warm", "night"],
        [WindowsPages.Colors.Uri] = ["dark mode", "light mode", "accent color", "theme", "transparency"],
        [WindowsPages.Captions.Uri] = ["subtitles", "live captions"],
        [WindowsPages.NearbySharing.Uri] = ["share", "phone link", "devices"],
        [WindowsPages.Accessibility.Uri] = ["ease of access", "vision", "hearing", "text size"],
        [WindowsPages.Security.Uri] = ["antivirus", "defender", "firewall", "virus"],
        [WindowsPages.Notifications.Uri] = ["alerts", "banners", "toasts"],
        [WindowsPages.DoNotDisturb.Uri] = ["focus", "quiet hours", "silence"],
        [WindowsPages.GameMode.Uri] = ["gaming", "games", "xbox"],
        [WindowsPages.ColorFilters.Uri] = ["color blind", "grayscale", "invert"],
        [WindowsPages.MonoAudio.Uri] = ["mono audio", "hearing"],
        [WindowsPages.Magnifier.Uri] = ["zoom", "magnify"],
        [WindowsPages.Narrator.Uri] = ["screen reader", "read aloud"],
        [WindowsPages.Keyboard.Uri] = ["sticky keys", "filter keys", "print screen", "on-screen keyboard"],
        [WindowsPages.Storage.Uri] = ["disk", "space", "storage sense", "cleanup", "free space", "drive"],
        [WindowsPages.AutoPlay.Uri] = ["usb", "removable", "memory card"],
        [WindowsPages.WindowsUpdate.Uri] = ["update", "upgrade", "patch", "restart"],
        [WindowsPages.InstalledApps.Uri] = ["apps", "programs", "uninstall", "remove", "add or remove programs", "features"],
        [WindowsPages.DefaultApps.Uri] = ["open with", "browser", "file associations", "default browser"],
        [WindowsPages.StartupApps.Uri] = ["startup", "autostart", "run at sign in"],
        [WindowsPages.Background.Uri] = ["wallpaper", "desktop background", "slideshow", "personalization"],
        [WindowsPages.Mouse.Uri] = ["cursor", "pointer", "scroll", "mouse speed", "buttons"],
        [WindowsPages.Printers.Uri] = ["printer", "scanner", "print"],
        [WindowsPages.SignIn.Uri] = ["password", "pin", "windows hello", "fingerprint", "face", "lock"],
        [WindowsPages.About.Uri] = ["pc name", "rename", "specifications", "version", "processor", "ram", "system info"],
        [WindowsPages.Privacy.Uri] = ["permissions", "location", "camera", "microphone", "diagnostics"],
        [WindowsPages.SearchingWindows.Uri] = ["indexing", "index", "indexer", "enhanced", "classic", "excluded folders"],
    };

    /// <summary>The words <paramref name="page"/> is found by beside its name: none where it has none.</summary>
    public static IReadOnlyList<string> WordsOf(WindowsLink page) => Words.TryGetValue(page.Uri, out var words) ? words : [];

    /// <summary>
    /// Every page that answers <paramref name="ask"/> — by its name in the language Karpo speaks, its English name, or one of
    /// its words —, scored; none where a filter other than Settings' is chosen, or nothing is typed.
    /// </summary>
    public static IEnumerable<WinSearchHit> Find(WinSearchAsk ask, DateTime now, WinSearchHistory? opened)
    {
        if (ask.Filter is not (null or WinSearchCategory.Settings) || ask.Empty)
        {
            yield break;
        }
        var words = ask.Words;
        foreach (var page in WindowsPages.All)
        {
            var hit = new WinSearchHit(WinSearchCategory.Settings, page.Title, page.Uri, WinSearchWords.SettingsDetail, Glyph) { Icon = SettingsApp };
            // A word of its own is a lesser answer than its name: at most what a word of the name is.
            var named = new[] { page.Title, Strings.English(page.TitleKey), Without(page.Title), Without(Strings.English(page.TitleKey)) }
                .Max(name => WinSearchRank.NameScore(name, words));
            // A page's own words answer words of three letters or more: "pa" is no question about passwords.
            named = words.All(word => word.Length >= 3) ? Math.Max(named, WordsOf(page).Select(word => Math.Min(70, WinSearchRank.NameScore(word, words))).DefaultIfEmpty(0).Max()) : named;
            var score = named > 0 ? WinSearchRank.Score(hit, words, now, opened, named: named) : 0;
            if (score > 0)
            {
                yield return hit with { Score = score };
            }
        }
    }

    /// <summary>A page's name without the word "settings" at its end, so "display" is the whole of "Display settings".</summary>
    private static string Without(string title)
    {
        var settings = title.LastIndexOf(' ');
        return settings > 0 && title[(settings + 1)..].Equals("settings", StringComparison.OrdinalIgnoreCase) ? title[..settings] : title;
    }
}
