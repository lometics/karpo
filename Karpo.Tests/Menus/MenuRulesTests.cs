// menus: the menu rules (2026-10-29, the person approving the menu audit's fifteen rules: "go with all your
// recommendations except for the point 5. the search fields should always already show stuff even if nothing is
// inputted"; docs\widgets.md §411), held over every settings menu Karpo has (MenuCorpus): §520 the settings panel's
// pages — Karpo, Display, Taskbar, Look, each widget kind's, each face's, each premade folder's, a folder's panel —, the
// folder room's right click, the bar's at rest, and the add lists.
//
// Each rule is read off the rows a menu shows (FlyoutItem), as the person sees them:
//
//   R1   no setting more than one level below the first: no row deeper than a third level (a form counts as one);
//        §517: Look ▾ › a section › a colour's swatches too (§507 to §513 Layout ▾ › Look went a level deeper)
//   R2   the first level of a menu never scrolls: its rows fit a configuration menu's one height; a right click's, as tall
//        as its rows, a 1920×1080 display's at 150 % under its bar and its edit bar (§520: a page of the settings panel
//        scrolls, the panel being one size)
//   R3   one control per kind of setting: no radio buttons; icon buttons in the row only for a place or a
//        direction; no choice of two with "Off" in it (a switch)
//   R4   every setting says its value at its row's end; §517 (the Sections design) a row opening a level says what it holds
//        where it holds anything set or to read (a choice, a switch, a number, a field, a note, the person's own things),
//        and nothing where it holds acts and ways out alone (§411 to §513: a row opening a page said nothing); §574 a
//        section of the Appearance shows what it holds in its preview, its row saying nothing
//   R5   a pick goes back: every choice's level, every Add's
//   R6   a search field is Find (empty, "Search …", a magnifier, something to pick before anything is typed) or
//        Type (its words, no magnifier)
//   R7   one word per concept (the glossary, §411); titles are nouns, not sentences
//   R8   sentence case, "and" not "&", "Add a …"
//   R10  §517: a heading stands over the rows of its section: never last on its level, never over a rule or another
//        heading (§411 to §513: no headings, but where the person's own things come from, §445, §455); §583 one showing a
//        look (an Appearance section's) its preview the first row under it, its parts' headings after
//   R11  "Reset all" the one name of a reset, last on its level (§532: the taskbar menu's list its "Reset to default")
//   R12  the switch that turns it on first, nothing destructive mid-level, no Quit Karpo on the Karpo page (§703)
//   R13  no icon twice on one level (§579: but one setting of several things, its name and icon each time)
//   R14  a value at most 20 characters (§518: a default's within its "Default (…)"; §556 a font's name its family's; §638 a thing's counts); a title never cut
//   R16  §517 (the person, 2026-11-05: "there does not have to be an entry all the way on the bottom as well. only if it
//        makes sense in terms of content"): a row stands at a page's foot only by what it is — a widget's Height (its rows) or Size and its Look, the
//        same on every widget's page (§423 to §513 every root menu had to end in a foot)
//   R17  §517, §520: the panel's pages' headings in one fixed order — Karpo General, Taskbar menu (§532), Services, Karpo; Display New
//        displays, This display; Layouts none; Taskbar Bar, Behavior —, a look's editor's Text over Colors
//   R18  §517, §520, §521: every setting has one page of the panel for its home: Karpo, Display, Layouts, Taskbar and Look share none, and
//        the folder room's right click's settings are each on one of them (MenuRulesTests.EverySettingHasOneHome)
//   R19  §520 (the person: "try to use similar or same sizes of texts or elements"): every control of every page, open
//        at every depth, is of its one size class — a value's 240 × 32 (§521: a text box's too), a segment 28 tall in it,
//        a button 32 tall (§521: a row's 120 to 240 wide), a number box's − and + 32 square and its value the rest of its
//        240; lines 48 or 60 tall, §604 a second line of more lines 16 taller for each (MenuRulesTests.EveryControlIsOfItsSizeClass)
// (R15, the bar's right click in edit mode as Taskbar ▾'s rows and the ways to Look ▾ and Display ▾, went with §520: it
// opens the panel's Taskbar page.)
//
// What breaks a rule today and is to be mended is Allowed (MenuRulesAllowed.cs), one line each; a line that no
// longer breaks its rule fails too, so the list only shrinks.

using System.Text.RegularExpressions;
using Karpo.Dock;

namespace Karpo.Tests.Menus;

public sealed partial class MenuRulesTests
{
    /// <summary>A rule broken: which, where (the menu's rows' titles from its first level), what.</summary>
    internal sealed record Offence(string Rule, string Menu, string Path, string What)
    {
        /// <summary>What the allowance names it by: the rule and the path, the same in every menu that shares the rows.</summary>
        public string Key => $"{Rule} {Path}";
    }

    /// <summary>A configuration menu's one height, less its padding and its top row (its name and buttons), in DIP.</summary>
    private const float RoomDip = Flyout.SettingsHeightDip - 6 - 44;

    /// <summary>
    /// §517: the most a right click's menu, as tall as its rows (Flyout.FitsContent), may be without scrolling, in DIP: a
    /// 1920×1080 display at 150 % (720 DIP) less its bar's 48, the edit bar's 48 and their margins.
    /// </summary>
    private const float EditBarRoomDip = 720 - 48 - 48 - (3 * 8) - 6;

    /// <summary>§517: the menus as tall as their rows (MenuCorpus' names): a folder room's right click.</summary>
    internal static readonly HashSet<string> EditBarMenus = ["folder room right click"];

    /// <summary>§520: the settings panel's own pages (MenuCorpus' names), which scroll (R2 is a menu's).</summary>
    internal static readonly HashSet<string> Pages = ["Karpo page", "Layouts page", "Taskbar page", "Appearance page"];

    /// <summary>R17: the panel's pages' headings, in their one order.</summary>
    internal static Dictionary<string, string[]> HeadingOrder => new(StringComparer.Ordinal)
    {
        // §579: About Karpo a section of its own, the last.
        ["Karpo page"] = [S.SettingsGeneralHeader, S.SettingsSectionsServices, S.SettingsSectionsKarpo, S.DockSettingsAbout],
        // §585: the displays, then the layouts.
        ["Layouts page"] = [S.SettingsSectionsDisplays, S.CoreBarMenuLayouts],
        // §585: the taskbar menu an expander of Behavior's, no section of its own (§553 to §584).
        ["Taskbar page"] = [S.SettingsSectionsBar, S.DockTaskbarBehaviour],
        // §592: no heading, its parts tabs (levels).
        ["Appearance page"] = [],

    };

    /// <summary>R16: what may stand still at a menu's foot, by its content.</summary>
    private static HashSet<string> Feet => [S.SettingsAppearanceHeader, S.DockEditHeightTitle, S.DockEditSizeTitle, S.DockEditHeightRows, S.DockEditHeightMax];

    /// <summary>The most a value at a row's end says, in characters.</summary>
    private const int LongestValue = 24;

    /// <summary>The most a row's own words say before a row of a configuration menu cuts them, in characters (Segoe UI 14 DIP, about 6.5 DIP a letter), its control off it.</summary>
    private const int LongestTitle = 37;

    /// <summary>
    /// A choice that is a place or a direction, which stands as icon buttons in its row (R3); §613 and the Appearance's Mode,
    /// the switcher the person asked for ("a switcher at the top, either dark light or system").
    /// </summary>
    private static readonly HashSet<string> Places = ["Alignment", "Position", "Direction", "Mode"];

    /// <summary>Words that keep their capital mid-title (R8): names, and what Windows writes so.</summary>
    private static readonly HashSet<string> Proper = new(StringComparer.Ordinal)
    {
        "Windows", "Karpo", "Pro", "Microsoft", "IMAP", "CalDAV", "CardDAV", "Wi-Fi", "Bluetooth", "VPN", "PC", "Store", "Google", "Bing",
        "DuckDuckGo", "Ecosia", "Startpage", "Brave", "Search", "Maps", "Apple", "HERE", "WeGo", "OpenStreetMap", "MSN", "Yahoo", "Finance",
        "TradingView", "Yr", "Open-Meteo", "Lometics", "GmbH", "File", "Explorer", "Task", "Manager", "Snipping", "Tool", "OneDrive", "Steam",
        "Dolby", "Atmos", "DTS", "DTS:X", "Ultra", "Headphone:X", "Sonic", "AQI", "US", "European", "EU", "ECB", "CoinGecko", "RSS", "Atom",
        "RTSP", "MJPEG", "MPEG-DASH", "HLS", "JPEG", "PNG", "MP4", "GPU", "CPU", "NVIDIA", "GeForce", "RTX", "Intel", "UHD", "Graphics", "Disk",
        "C:", "D:", "(C:)", "(D:)", "Ti", "Local", "UTC", "Do", "Teams", "Outlook.com", "Hotmail", "Nextcloud", "Fastmail", "iCloud", "Gmail",
        "Narrator", "Magnifier", "Sticky", "Keys", "Mono", "Live", "Captions", "Start", "Settings", "Copilot", "Win+X", "Alt+Tab", "ESPN",
        "NASDAQ", "USD", "EUR", "CHF", "MSFT", "AAPL", "To", "Calculator", "Documents", "Downloads", "Pictures", "Join", "Update",
    };

    /// <summary>
    /// R8's exceptions the person wrote so (§626: "the appearance entries should be : Taskbar, Taskbar Widgets, Flyouts,
    /// Flyout Widgets"): the Appearance's tabs in title case.
    /// </summary>
    private static bool PersonsCapitals(string text) => text == S.SettingsAppearanceStylesWidgets || text == S.SettingsAppearanceStylesFolderWidgets;

    /// <summary>
    /// §411's glossary: the one word for each concept, and the words that may not stand for it (R7). The person's own
    /// things' rows are named by the person and are no title of Karpo's.
    /// </summary>
    internal static readonly (string Word, string[] Not)[] Glossary =
    [
        ("Name", ["Label", "Name on the bar", "Text", "Title"]),
        ("Show name", ["Show its name", "Show the name beside it", "Show label", "Show the label"]),
        ("Update interval", ["How often it updates", "Update online widgets", "Refresh"]),
        ("Reset all", ["Follow Windows for everything", "Reset to defaults", "Reset", "Reset colors"]),
        ("Direction", ["Which way the slider runs"]),
        ("Target", ["What it opens"]),
        ("Duration", ["How long"]),
        ("Place", ["Location"]),
        // §484: which of a line's views it shows (the sports line's Matches, Table, Knockout).
        ("View", ["Page", "Tab", "Shows", "Show as"]),
        // §489: whether anything of Karpo's moves, and how fast.
        ("Animations", ["Motion", "Transitions", "Folder animation"]),
        ("Animation speed", ["Animation duration", "Motion speed"]),
    ];

    private static readonly Regex Sentence = new(@"^(How|What|Which|When|Where|Who|Why)\b|\b(it|its|it's|you)\b|\?$", RegexOptions.Compiled);

    [Fact]
    public void EveryMenuKeepsTheRules()
    {
        var offences = Offences().ToList();
        var dump = Environment.GetEnvironmentVariable("KARPO_MENU_RULES");
        if (!string.IsNullOrEmpty(dump))
        {
            File.WriteAllLines(dump, offences.GroupBy(offence => offence.Key).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"        \"{group.Key.Replace("\"", "\\\"")}\", // {group.First().What} ({group.Count()}: {group.First().Menu})"));
        }
        var keys = offences.Select(offence => offence.Key).ToHashSet();
        var broken = offences.Where(offence => !Allowed.Contains(offence.Key) && !Exceptions.ContainsKey(offence.Key)).GroupBy(offence => offence.Key).Select(group => group.First()).ToList();
        var mended = Allowed.Where(key => !keys.Contains(key)).ToList();
        Assert.True(broken.Count == 0, "Rules broken:\n" + string.Join("\n", broken.Take(80).Select(offence => $"{offence.Key} — {offence.What} [{offence.Menu}]")));
        Assert.True(mended.Count == 0, "Kept now; take them off the allowance:\n" + string.Join("\n", mended));
    }

    /// <summary>
    /// §517: R1 has no exception any more — Look ▾ › a section › a colour's swatches is its third level (§507 to §513 Layout ▾
    /// › Look went a level deeper, allowed): a row four levels deep is R1's in every menu, Look ▾'s and a widget's too.
    /// </summary>
    [Fact]
    public void NoMenuGoesDeeperThanThreeLevels()
    {
        static FlyoutItem Chain(string title, int below) =>
            new(1, title, Children: [below == 0 ? new FlyoutItem(2, "Too deep") : Chain(title + " level", below - 1)]);
        IReadOnlyList<Offence> Of(string menu, string first) => [.. OffencesOf(new CorpusMenu(menu, [Chain(first, 4)])).Where(offence => offence.Rule == "R1")];

        Assert.NotEmpty(Of("Appearance page", LookParts.Name(LookPart.Widgets)));
        Assert.NotEmpty(Of("widget NightLight", S.SettingsAppearanceHeader));
        Assert.NotEmpty(Of("Karpo page", S.SettingsOnlineSection));
        Assert.Empty(Allowed);
    }

    /// <summary>
    /// R18 (§517: "every setting has exactly one obvious home"; §520): the settings panel's Karpo, §535 Accounts, Display, Taskbar (§553 the taskbar menu among it) and Look
    /// pages share no setting, and every setting of an arranged folder room's right click is one of theirs.
    /// </summary>
    [Fact]
    public void EverySettingHasOneHome()
    {
        var config = MenuCorpus.Fresh();
        var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
        static IEnumerable<string> Settings(IEnumerable<OptionRow> rows) =>
            OptionRows.Flat([.. rows]).Where(row => row.Kind is not (OptionRowKind.Heading or OptionRowKind.Link or OptionRowKind.Level)).Select(row => row.Key);
        var homes = new Dictionary<string, string[]>
        {
            ["Karpo page"] = [.. Settings(KarpoMenu.Rows(config, MenuCorpus.Services()))],
            ["Accounts page"] = [.. Settings(KarpoMenu.Accounts(config, MenuCorpus.Services()))],
            ["Layouts page"] = [.. Settings(BarLooks.Rows(config, bar, dark: true, scope: LooksScope.Layouts))],
            ["Taskbar page"] = [.. Settings(Karpo.Dock.Dock.LooksOf(config, bar, dark: true, scope: LooksScope.Taskbar, services: MenuCorpus.Services()))],
            ["Appearance page"] = [.. Settings(BarLooks.Rows(config, bar, dark: true, scope: LooksScope.Appearance))],
        };
        var all = homes.SelectMany(home => home.Value.Distinct().Select(key => (Key: key, Home: home.Key))).GroupBy(each => each.Key).ToList();
        var twice = all.Where(group => group.Count() > 1).Select(group => $"{group.Key}: {string.Join(", ", group.Select(each => each.Home))}").ToList();
        Assert.True(twice.Count == 0, "Settings with two homes:\n" + string.Join("\n", twice));
        var housed = all.Select(group => group.Key).ToHashSet();
        foreach (var scope in new[] { LooksScope.Folders })
        {
            var homeless = Settings(BarLooks.Rows(config, bar, dark: true, scope: scope)).Where(key => !housed.Contains(key)).ToList();
            Assert.True(homeless.Count == 0, $"{scope}'s settings no page of the panel has: {string.Join(", ", homeless)}");
        }
    }

    /// <summary>
    /// R19 (§520, the person: "try to use similar or same sizes of texts or elements"): every control of every page — every
    /// settings menu of the corpus read as the panel reads it, every expander open — is of its one size class: a value's
    /// box 240 × 32, a segment 28 tall in its box, a row of buttons' each 32 tall, a number box's − and + 32 square; every
    /// line 48 or 60 tall, §604 a second line of more lines (wrapped, or lines of its own) 16 taller for each; nothing of a
    /// control over its row's name.
    /// </summary>
    [Fact]
    public void EveryControlIsOfItsSizeClass()
    {
        const float scale = 1.5f;
        float P(float dip) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        var words = new Karpo.Tests.Edit.SettingsPanelTests.Words();
        var size = new System.Numerics.Vector2(0, 20_000);
        List<string> wrong = [];
        foreach (var menu in MenuCorpus.All().Where(menu => menu.Settings))
        {
            var page = PanelRows.Page(PanelPage.Widget, menu.Name, "", menu.Items);
            var open = PanelRows.All(page.Rows).Where(row => row.Kind == PanelRowKind.Expander).Select(row => row.Key).ToHashSet();
            var layout = new PanelLayout(words, scale, new PanelInput(0, new SettingsContent([page]), PanelPage.Widget, open), size);
            foreach (var line in layout.Blocks.SelectMany(block => block.Lines))
            {
                var where = $"[{menu.Name}] {line.Row.Key}";
                // §578: a row that only acts (Add an entry, Reset to default) its button alone, no card: 32 tall alone, 48 in
                // an expander's line; as wide as its words, §599 at the line's end.
                if (line.Row.Foot)
                {
                    if (line.Rect.Height != P(32) && line.Rect.Height != P(48))
                    {
                        wrong.Add($"{where}: its button alone {line.Rect.Height} px tall");
                    }
                    continue;
                }
                // §628: a tile picturing what it makes (Add a layout's): its picture a desktop strip of 1280 by 144 at its width,
                // its words under it a tile's of two lines (52, more for more), the whole of it its one control.
                if (line.Tile && layout.PictureOf(line) is { IsEmpty: false } picture)
                {
                    var under = layout.WordsOf(line).Rect.Height;
                    if (picture.Height != MathF.Round(picture.Width * 144 / 1280) || under < P(PanelMetrics.TileTwoLines) || (under - P(PanelMetrics.TileTwoLines)) % P(16) != 0
                        || layout.Controls(line) is not [{ Hit.Kind: PanelHitKind.Act } whole] || whole.Rect != line.Rect)
                    {
                        wrong.Add($"{where}: a pictured tile {line.Rect.Width} × {line.Rect.Height} px, its picture {picture.Width} × {picture.Height}, its words {under}");
                    }
                    continue;
                }
                // §592: a tab strip 40 tall, its tabs as tall as it; §673 its button (the palette's reset) a button, 32 tall.
                if (line.Row.Kind == PanelRowKind.Tabs)
                {
                    if (line.Rect.Height != P(PanelMetrics.Tabs) || layout.Controls(line).Any(each => each.Rect.Height != (each.Hit.Kind == PanelHitKind.Tab ? line.Rect.Height : P(PanelMetrics.Control))))
                    {
                        wrong.Add($"{where}: a tab strip {line.Rect.Height} px tall");
                    }
                    continue;
                }
                // §536: a look's preview is a picture sized to its sample, not a row of controls.
                var more = line.Rect.Height - P(60);
                if (line.Row.Kind != PanelRowKind.Preview && line.Rect.Height != P(48) && (more < 0 || more % P(16) != 0))
                {
                    wrong.Add($"{where}: a line {line.Rect.Height} px tall");
                }
                foreach (var (hit, rect) in layout.Controls(line))
                {
                    var (w, h) = hit.Kind switch
                    {
                        // §521: a way to add one (Add a layout) a button, its list under it.
                        PanelHitKind.Pop when line.Row.Adds => (Math.Clamp(rect.Width, P(120), P(240)), P(32)),
                        // §535: a colour a colour button.
                        PanelHitKind.Pop when line.Row.Kind == PanelRowKind.Color => (P(PanelMetrics.ColourWide), P(32)),
                        // §613: a value's reset beside its control an icon button, a 32 DIP square.
                        PanelHitKind.Act when line.Row.Pair is { Item.Subtle: true } reset && reset.Key == hit.Key => (P(32), P(32)),
                        PanelHitKind.Pop or PanelHitKind.Text => (P(240), P(32)),
                        // §521: a row's button 32 tall, 120 to 240 wide; a number box's value its box less − and +.
                        PanelHitKind.Act => (Math.Clamp(rect.Width, P(120), P(240)), P(32)),
                        PanelHitKind.Number => (P(240) - P(32) - P(32) - P(4), P(32)),
                        PanelHitKind.Fewer or PanelHitKind.More => (P(32), P(32)),
                        PanelHitKind.Tool => (rect.Width, P(32)),
                        _ => (rect.Width, rect.Height),
                    };
                    if (rect.Width != w || rect.Height != h)
                    {
                        wrong.Add($"{where}: its {hit.Kind} {rect.Width} × {rect.Height} px, not {w} × {h}");
                    }
                }
                if (layout.ControlLeft(line) < line.Rect.X + P(PanelMetrics.NameAt) + P(40))
                {
                    wrong.Add($"{where}: its control runs over its name");
                }
            }
        }
        Assert.True(wrong.Count == 0, string.Join("\n", wrong.Take(60)));
    }

    /// <summary>
    /// §645 (the person, 2026-11-09: "the widget settings should have normal sections like the other settings pages"): R17 and
    /// R19 on every widget's and line's page as the panel lays it (PanelGroups.Page), every expander open: its headings in their
    /// one order — What it shows (or the one level it holds, §594; §655 or the several levels and ordered lists it is), then Size —, each a heading's 20 DIP with its first card 8
    /// under it; every line of its sections 48 or 60 tall (16 more for each more line), as on the other pages; no group.
    /// </summary>
    [Fact]
    public void EveryWidgetsPageIsItsSectionsInOrderOfTheirSizes()
    {
        const float scale = 1.5f;
        float P(float dip) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        var words = new Karpo.Tests.Edit.SettingsPanelTests.Words();
        var size = new System.Numerics.Vector2(0, 20_000);
        var place = new OptionPlace(MenuCorpus.Fresh(), Dark: true);
        List<string> wrong = [];
        IEnumerable<WidgetItem> items = [.. Enum.GetValues<BarKind>().Select(kind => (WidgetItem)kind.New()), .. Enum.GetValues<ListKind>().Select(kind => (WidgetItem)kind.New())];
        foreach (var item in items.Where(item => (item as BarItem)?.Folder is null))
        {
            var name = IslandEdits.Kind(item);
            var page = PanelGroups.Page(PanelPage.Widget, name, "", MenuCorpus.Arranged(item, place));
            var headings = page.Rows.Where(row => row.Kind == PanelRowKind.Heading).Select(row => row.Text).ToList();
            // §655: What it shows may be several sections where each is a level of its menu or a list put in order (Windows
            // search's Search in, When nothing is typed, How many each shows), so: one heading at least, none twice, Size the last;
            // What it shows itself is then the one before Size (the levels are its sections, or it is theirs).
            var sized = headings.IndexOf(S.DockEditSizeTitle);
            var parts = headings.Count - (sized >= 0 ? 1 : 0);
            if (page.Rows.Count > 0 && (headings.Count < 1 || headings.Distinct(StringComparer.Ordinal).Count() != headings.Count || (sized >= 0 && sized != headings.Count - 1)
                || (parts > 1 && headings.Contains(S.DockPanelGroupShows))))
            {
                wrong.Add($"[{name}] R17: its headings are {string.Join(", ", headings)}");
            }
            if (page.Rows.Any(row => row.Kind == PanelRowKind.Group))
            {
                wrong.Add($"[{name}]: a group on its page");
            }
            var open = PanelRows.All(page.Rows).Where(row => row.Kind == PanelRowKind.Expander).Select(row => row.Key).ToHashSet();
            var layout = new PanelLayout(words, scale, new PanelInput(0, new SettingsContent([page]), PanelPage.Widget, open), size);
            var blocks = layout.Blocks.ToList();
            for (var at = 0; at < blocks.Count; at++)
            {
                var block = blocks[at];
                if (block.Row.Kind == PanelRowKind.Heading)
                {
                    if (block.Rect.Height != P(PanelMetrics.Heading) || at + 1 >= blocks.Count || blocks[at + 1].Rect.Y != block.Rect.Bottom + P(PanelMetrics.Gap))
                    {
                        wrong.Add($"[{name}] {block.Row.Text}: a heading {block.Rect.Height} px tall, its first card not 8 under it");
                    }
                    continue;
                }
                foreach (var line in block.Lines.Where(line => !line.Row.Foot && !line.Tile))
                {
                    var more = line.Rect.Height - P(60);
                    if (line.Rect.Height != P(48) && (more < 0 || more % P(16) != 0))
                    {
                        wrong.Add($"[{name}] {line.Row.Key}: a line {line.Rect.Height} px tall");
                    }
                }
            }
        }
        Assert.True(wrong.Count == 0, string.Join("\n", wrong.Take(60)));
    }

    /// <summary>
    /// §518 (the person, 2026-11-05: "it should always just say it like this: Default (Value) if it is the default value"):
    /// in every menu a value left unset says so in one form, "Default (…)" with what it comes to — never a bare "Default",
    /// "Layout" or "Inherited", never "Layout (…)", "Inherited (…)", "Follow Windows (…)", "Windows default (…)", "As in
    /// Windows (…)" or "… (default)". A colour row wearing what it comes to (its quiet swatch, §509) says it so; a choice
    /// whose default is ticked says at its end the very words of its choice; a colour's and an icon's first tile is it.
    /// </summary>
    [Fact]
    public void EveryUnsetValueSaysDefaultAndWhatItComesTo()
    {
        var form = S.SettingsValueDefault("\u0001").Split('\u0001');
        static bool Says(string? text) => text is { } said && Measured(said) is var inner && inner != said && inner.Trim().Length > 0;
        string[] bare = ["Default", "Layout", "Inherited"];
        string[] old = ["Layout (", "Inherited", "Follow Windows", "Windows default", "As in Windows", "As Windows shows them"];
        List<string> wrong = [];
        foreach (var menu in MenuCorpus.All())
        {
            foreach (var (item, path) in Deep(menu.Items, []))
            {
                var here = $"[{menu.Name}] {Path(path, item)}";
                foreach (var text in new[] { item.Text, item.Value?.Text }.OfType<string>())
                {
                    if (text.StartsWith(form[0], StringComparison.Ordinal) && !Says(text))
                    {
                        wrong.Add($"{here}: \"{text}\" says no value");
                    }
                    if (old.Any(each => text.StartsWith(each, StringComparison.Ordinal)) || text.EndsWith("(default)", StringComparison.OrdinalIgnoreCase))
                    {
                        wrong.Add($"{here}: \"{text}\", the old way");
                    }
                }
                // What applies where nothing is set is said greyed (§411, R4): never a bare word for it.
                if (item.Value is { Implied: true, Text: var value } && bare.Contains(value))
                {
                    wrong.Add($"{here}: a bare \"{value}\"");
                }
                if (item.Icon is FlyoutTile { Quiet: true } && !Says(item.Value?.Text))
                {
                    wrong.Add($"{here}: unset, it says \"{item.Value?.Text}\"");
                }
                if (item.Children is not { } children)
                {
                    continue;
                }
                // An icon's row shows the icon it is drawn with, no words (§228).
                if (item.Value is not { Icon: not null, Text: "" } && children.FirstOrDefault(child => child.Checked == true && Says(child.Text)) is { } ticked && ticked.Text != item.Value?.Text)
                {
                    wrong.Add($"{here}: its default ticked, it says \"{item.Value?.Text}\" for \"{ticked.Text}\"");
                }
                // A colour's level (its swatches) and an icon's (the cross): the first choice is the default, said so. §574: an
                // Appearance's colour has no default, a value as any (AppearanceValuesTests).
                var appearance = item.Setting?.StartsWith(LookRows.Key + ".", StringComparison.Ordinal) == true;
                var picker = !appearance && children.Any(child => child.Icon is FlyoutTile { Cell: true } or FlyoutTile { Code: OptionGlyphs.Clear });
                if (picker && children.FirstOrDefault(child => child.Checked is not null) is { } first && !Says(first.Text))
                {
                    wrong.Add($"{here}: its first choice \"{first.Text}\"");
                }
            }
        }
        Assert.True(wrong.Count == 0, "Unset values not said \"Default (…)\":\n" + string.Join("\n", wrong.Distinct().Take(60)));
    }

    /// <summary>Every rule broken in every menu of the corpus.</summary>
    internal static IEnumerable<Offence> Offences()
    {
        foreach (var menu in MenuCorpus.All())
        {
            foreach (var offence in OffencesOf(menu))
            {
                yield return offence;
            }
        }
    }

    internal static IEnumerable<Offence> OffencesOf(CorpusMenu menu)
    {
        List<Offence> found = [];
        void Add(string rule, string path, string what) => found.Add(new Offence(rule, menu.Name, path, what));
        if (menu.Settings && !Pages.Contains(menu.Name))
        {
            // R2: the first level of a menu never scrolls — a widget's is its right click's too (§520: a page of the panel scrolls).
            var tall = menu.Items.Where(item => !item.Primary).Sum(item => item.Separator ? 9f : item.Detail is { Length: > 0 } ? 50f : 32f);
            var room = EditBarMenus.Contains(menu.Name) ? EditBarRoomDip : RoomDip;
            if (tall > room)
            {
                Add("R2", $"[{menu.Name}]", $"its first level is {tall:0} DIP of {room:0}");
            }
        }
        if (menu.Settings)
        {
            Walk(menu.Items, [], 0, Add, menu.Name);
        }
        // R8, everywhere: "and", not "&".
        foreach (var (item, path) in Deep(menu.Items, []))
        {
            if (item.Text.Contains('&') && !menu.Name.StartsWith("bar right click (at rest)", StringComparison.Ordinal))
            {
                Add("R8", Path(path, item), "\"&\" for \"and\"");
            }
        }
        // R12: §703 no Quit Karpo on the Karpo page (§545 until §703 General's last; §409 to §544 the page's last, at its foot).
        if (menu.Name == "Karpo page" && Deep(menu.Items, []).Any(each => each.Item1.Text == S.SettingsAboutQuitHeader))
        {
            Add("R12", "[Karpo page]", "Quit Karpo is on the page");
        }
        // R16: a row stands still at the foot by what it is alone: a widget's height, size and look.
        foreach (var (item, path) in Deep(menu.Items, []))
        {
            if (item.Foot && !item.Separator && !Feet.Contains(item.Text))
            {
                Add("R16", Path(path, item), "standing still at the foot for no reason of its own");
            }
        }
        // R17: the panel's pages' headings in their one order; a look's editor's Text over Colors.
        if (HeadingOrder.TryGetValue(menu.Name, out var order) && !menu.Items.Where(item => item.Header).Select(item => item.Text).SequenceEqual(order))
        {
            Add("R17", $"[{menu.Name}]", $"its headings are {string.Join(", ", menu.Items.Where(item => item.Header).Select(item => item.Text))}");
        }
        foreach (var (item, path) in Deep(menu.Items, []))
        {
            if (item.Children is { } level && level.Where(each => each.Header).Select(each => each.Text).ToList() is var headings
                && (headings.Contains(S.SettingsSectionsText) || headings.Contains(S.SettingsSectionsColors))
                && !headings.SequenceEqual([S.SettingsSectionsText, S.SettingsSectionsColors]))
            {
                Add("R17", Path(path, item), $"a look's headings are {string.Join(", ", headings)}");
            }
        }
        return found;
    }

    /// <summary>Every row under <paramref name="items"/>, with the titles of the rows over it.</summary>
    private static IEnumerable<(FlyoutItem Item, IReadOnlyList<string> Path)> Deep(IReadOnlyList<FlyoutItem> items, IReadOnlyList<string> path)
    {
        foreach (var item in items)
        {
            yield return (item, path);
            if (item.Children is { } children)
            {
                foreach (var each in Deep(children, [.. path, item.Text]))
                {
                    yield return each;
                }
            }
        }
    }

    private static string Path(IReadOnlyList<string> path, FlyoutItem? item = null) =>
        string.Join(" › ", item is null ? path : [.. path, item.Text]);

    /// <summary>R14: what of a value counts: a default's within its "Default (…)" (§518), any other whole.</summary>
    private static string Measured(string value)
    {
        var form = S.SettingsValueDefault("\u0001").Split('\u0001');
        return value.Length > form[0].Length + form[1].Length && value.StartsWith(form[0], StringComparison.Ordinal) && value.EndsWith(form[1], StringComparison.Ordinal)
            ? value[form[0].Length..^form[1].Length]
            : value;
    }

    /// <summary>Whether <paramref name="level"/> is a choice's: its rows each a tick, picked and staying.</summary>
    private static bool ChoiceLevel(IReadOnlyList<FlyoutItem> level)
    {
        var rows = level.Where(item => !item.Separator && !item.Header && item.Control is not FlyoutField).ToList();
        return rows.Count > 0 && rows.Count(item => item.Checked is not null) >= rows.Count - 1 && rows.All(item => item.Children is null && item.Control is null);
    }

    /// <summary>Whether <paramref name="level"/> is a field's: a line of text or a search, what it offers under it.</summary>
    private static bool FieldLevel(IReadOnlyList<FlyoutItem> level) => level.Count > 0 && level.Any(item => item.Control is FlyoutField) && level.Count(item => item.Control is FlyoutField) == 1
        && level.All(item => item.Control is FlyoutField or FlyoutControl.Toggle || item.Checked is not null || item.Separator || item.Children is null);

    /// <summary>Whether a row opening <paramref name="level"/> is one setting (a choice, a field, a colour, an icon, a time, a group of switches), so says its value, rather than a page.</summary>
    private static bool OneSetting(FlyoutItem row, IReadOnlyList<FlyoutItem> level) =>
        row.PickOne || ChoiceLevel(level) || FieldLevel(level)
        || Switches(level)
        || level.Where(item => !item.Separator).All(item => item.Control is FlyoutControl.Stepper);

    /// <summary>
    /// Whether <paramref name="level"/> is a group of switches (an alarm's days, a widget's "Show from"): one setting, its
    /// switches maybe under the names of the places they come from (§455).
    /// </summary>
    private static bool Switches(IReadOnlyList<FlyoutItem> level) =>
        level.Any(item => item.Control is FlyoutControl.Toggle) && level.Where(item => !item.Separator && !item.Header).All(item => item.Control is FlyoutControl.Toggle);

    private static bool Find(FlyoutField field) => field.Finds;

    /// <summary>
    /// R4 (§517): whether <paramref name="level"/> holds anything set or to read — a choice's tick, a switch, a stepper, a
    /// place's icons, a field, a row saying a value, a note, the person's own things —, so its row says what at its end;
    /// a level of acts and ways out alone holds nothing.
    /// </summary>
    private static bool Holds(IReadOnlyList<FlyoutItem> level) =>
        level.Any(item => !item.Separator && !item.Header && !item.Primary
            && (item.Value is not null || item.Note || item.Checked is not null || item.Control is FlyoutControl.Toggle or FlyoutControl.Stepper or FlyoutControl.Segments or FlyoutControl.Tools or FlyoutField));

    private static void Walk(IReadOnlyList<FlyoutItem> level, IReadOnlyList<string> path, int depth, Action<string, string, string> add, string menu)
    {
        var rows = level.Where(item => !item.Separator && !item.Primary).ToList();
        // A level of the person's own things (one with a thing's buttons): a choice's things are its ticked rows, buttons or none.
        var own = rows.Any(item => item.Control is FlyoutControl.Tools);
        // R13: no icon twice on a level; the person's own things and a folder's faces wear their own. §579: one setting of
        // several things (each display's Layout and Position on the Display page), its name and icon each time, is one meaning.
        var glyphs = rows.Where(item => item.Reorder == 0 && item.Icon is FlyoutIcon.Glyph && item.Control is not (FlyoutField or FlyoutControl.Tools) && !(own && item.Checked is not null))
            .GroupBy(item => ((FlyoutIcon.Glyph)item.Icon!).Code).Where(group => group.Count() > 1 && group.Select(item => item.Text).Distinct(StringComparer.Ordinal).Count() > 1).ToList();
        if (glyphs.Count > 0 && path.Count > 0)
        {
            add("R13", Path(path), $"{glyphs.Sum(group => group.Count())} rows share {glyphs.Count} icon(s): {string.Join(", ", glyphs.SelectMany(group => group).Take(3).Select(item => item.Text))}");
        }
        else if (glyphs.Count > 0)
        {
            foreach (var group in glyphs)
            {
                add("R13", Path(path, group.First()), $"its icon is also {string.Join(", ", group.Skip(1).Select(item => item.Text))}'s");
            }
        }
        // R12: nothing destructive mid-level: a delete stands last (but the rows under it a rule away).
        var settings = rows.Where(item => !item.Header && item.Reorder == 0).ToList();
        for (var at = 0; at < settings.Count - 1; at++)
        {
            var item = settings[at];
            if (item.Icon is FlyoutIcon.Glyph { Code: OptionGlyphs.Delete } && item.Children is null && item.Control is null)
            {
                add("R12", Path(path, item), "a delete mid-level");
            }
        }
        // R12: the switch that turns it on first.
        var on = settings.FindIndex(item => item.Control is FlyoutControl.Toggle && item.Text.EndsWith(" on", StringComparison.Ordinal) && item.Text.Split(' ').Length == 2);
        if (on > 0)
        {
            add("R12", Path(path, settings[on]), "the switch that turns it on is not first");
        }
        // R11: "Reset all" the one name, last on its level. §532: the taskbar menu's "Reset to default" puts a list of the
        // person's own back as it came, as the menu's page said it until 2026-10-12 — no look's values, and its section's last; §577 a
        // palette's the same words, its values back to its design.
        foreach (var item in rows.Where(item => item.Icon is FlyoutIcon.Glyph { Code: OptionGlyphs.Reset } && !path.Contains("Back up and reset") && item.Text != S.SettingsBarMenuReset && item.Text != S.SettingsLookReset))
        {
            if (item.Text != "Reset all")
            {
                add("R11", Path(path, item), "a reset not called Reset all");
            }
            else if (!ReferenceEquals(item, rows[^1]))
            {
                add("R11", Path(path, item), "Reset all is not last");
            }
        }
        for (var at = 0; at < level.Count; at++)
        {
            var item = level[at];
            if (item.Separator || item.Primary)
            {
                continue;
            }
            var here = Path(path, item);
            // R10 (§517): a heading over its section's rows: never last, never over a rule or another heading.
            if (item.Header)
            {
                var next = at + 1 < level.Count ? level[at + 1] : null;
                if (next is null or { Separator: true } || (next is { Header: true } && item.Shows is null))
                {
                    add("R10", here, "a heading over no rows of its own");
                }
                continue;
            }
            // R1: no row deeper than a third level.
            if (depth >= 3)
            {
                add("R1", Path(path), $"a fourth level ({path.Count + 1} deep)");
                return;
            }
            // R3: no radio buttons; icon buttons only for a place or a direction.
            if (item.Control is FlyoutControl.Radio)
            {
                add("R3", here, "a radio button");
            }
            if (item.Control is FlyoutControl.Segments && !Places.Contains(item.Text))
            {
                add("R3", here, "icon buttons for no place or direction");
            }
            if (item.Children is { Count: > 0 } level2 && ChoiceLevel(level2))
            {
                var options = level2.Where(each => each.Checked is not null).Select(each => each.Text).ToList();
                if (options.Count == 2 && options.Any(option => option == "Off"))
                {
                    add("R3", here, $"a choice of two with Off ({string.Join(", ", options)}): a switch");
                }
                if (Places.Contains(item.Text) && options.Count <= 4)
                {
                    add("R3", here, "a place's choice as a level: icon buttons");
                }
            }
            // R14: a value at most so long; a title never cut.
            // The person's own words (a name typed) are theirs, whatever their length.
            // §518: a default's value counted within its "Default (…)", the same words around every one.
            // §556: a font's name (in its own face) is its family's, as long as its family calls itself.
            // §638: what one of the person's things holds (an account's items synced), a count of each kind it gives, as many as it gives.
            if (item.Value is { Face: null } value && item.Control is not FlyoutControl.Tools && Measured(value.Text) is { Length: > LongestValue } measured && !item.Note && item.Children?.Any(each => each.Control is FlyoutField { Finds: false }) != true)
            {
                add("R14", here, $"its value \"{value.Text}\" is {measured.Length} characters");
            }
            var control = item.Control switch
            {
                FlyoutControl.Toggle => 7,
                FlyoutControl.Stepper => 15,
                FlyoutControl.Segments segments => 5 * segments.Buttons.Count,
                FlyoutControl.Tools tools => (4 * tools.Buttons.Count) + (tools.Switch is null ? 0 : 7),
                _ => 0,
            };
            if (item.Control is not FlyoutField && !item.Note && item.Text.Length + control > LongestTitle && item.Reorder == 0 && item.Checked is null)
            {
                add("R14", here, $"its title is cut ({item.Text.Length} characters)");
            }
            // R7: the glossary's words, noun titles.
            foreach (var (word, not) in Glossary)
            {
                if (not.Contains(item.Text) && item.Checked is null && item.Reorder == 0)
                {
                    add("R7", here, $"\"{item.Text}\" for \"{word}\"");
                }
            }
            if (item.Children is not null && item.Reorder == 0 && item.Checked is null && item.Control is null && Sentence.IsMatch(item.Text))
            {
                add("R7", here, "a sentence for a title");
            }
            // R8: sentence case; "Add a …".
            if (item.Checked is null && item.Reorder == 0 && item.Control is not FlyoutField && !item.Note && item.Icon is not FlyoutIcon.Picture
                && !PersonsCapitals(item.Text) && item.Text.Split(' ').Skip(1).FirstOrDefault(word => word.Length > 1 && char.IsUpper(word[0]) && !Proper.Contains(word.TrimEnd(',', '…', ')').TrimStart('('))) is { } capital)
            {
                add("R8", here, $"\"{capital}\" in sentence case");
            }
            if (item.Text.StartsWith("Add ", StringComparison.Ordinal) && !Regex.IsMatch(item.Text, @"^Add (a|an) \w+( \w+)?$"))
            {
                add("R8", here, "not \"Add a …\"");
            }
            if (item.Children is not { Count: > 0 } children)
            {
                // R4: a line of text says its words at its end.
                continue;
            }
            var setting = OneSetting(item, children);
            // R4: a setting says its value; §517 a level what it holds where it holds anything set or to read, a level of acts
            // and ways out nothing; §574 a section of the Appearance (a look's level, Shows) shows it in its preview alone.
            if (item.Value is null && item.Reorder == 0 && item.Control is null && item.Shows is null && !item.Text.StartsWith("Add", StringComparison.Ordinal) && (setting || Holds(children)))
            {
                add("R4", here, setting ? "no value at its end" : "a level saying nothing of what it holds");
            }
            if (!setting && !Holds(children) && item.Value is not null && item.Reorder == 0)
            {
                add("R4", here, $"a level of acts saying \"{item.Value.Text}\"");
            }
            // R5: a pick goes back: a choice's level, an Add's.
            if (ChoiceLevel(children) && !item.PickOne && children.Any(each => each.Stays))
            {
                add("R5", here, "a pick stays on its level");
            }
            if (item.Text.StartsWith("Add", StringComparison.Ordinal) && !item.PickOne && children.Any(each => each.Children is not null))
            {
                add("R5", here, "Add opens levels in levels");
            }
            else if (item.Text.StartsWith("Add", StringComparison.Ordinal) && !item.PickOne && children.All(each => each.Separator || each.Stays && each.Control is null))
            {
                add("R5", here, "Add stays on its level after a pick");
            }
            // R6: Find or Type.
            foreach (var field in children.Select(each => each.Control).OfType<FlyoutField>().Where(field => !field.Masked))
            {
                if (Find(field))
                {
                    if (field.Text.Length > 0)
                    {
                        add("R6", here, $"Find opens with \"{field.Text}\" in it");
                    }
                    if (!field.Placeholder.StartsWith("Search ", StringComparison.Ordinal) || field.Placeholder.Contains(':'))
                    {
                        add("R6", here, $"Find says \"{field.Placeholder}\"");
                    }
                    if (!field.Filters && field.Finder is { } finder && finder.Offered.Count + (finder.LeastLetters <= 0 ? finder.FindAsync("", default).Result.Count : finder.Suggested.Count) == 0)
                    {
                        add("R6", here, "Find shows nothing before anything is typed");
                    }
                }
                else if (field.Placeholder.StartsWith("Search ", StringComparison.Ordinal))
                {
                    add("R6", here, $"Type says \"{field.Placeholder}\"");
                }
            }
            // §529, §554: a row of buttons whose own rows stand nested under it (Nests: a folder's face) or §609 its one choice in its row (that choice Beside: a holiday calendar's region) is no level of its own.
            Walk(children, [.. path, item.Text], item.Nests || item.Children is [{ Beside: true }] ? depth : depth + 1, add, menu);
        }
    }
}
