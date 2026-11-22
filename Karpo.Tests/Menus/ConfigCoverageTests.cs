// edit: every setting of config.json has a row that writes it (docs\widgets.md §341; the settings-in-the-dock
// plan's §5 #14). With the settings app gone the edit bar's menus are the only place a setting is made, so
// this replaces the test that held the app's Appearance, Displays and Layouts pages to the bar's menu
// (BarLooksTests.EveryLookTheSettingsAppHasIsARowOfAMenu, which read the app's XAML).
//
// How: every row of every menu that writes Karpo's own settings — ⚙ Settings (KarpoMenu), the bar's whole
// menu in edit mode (BarLooks.Rows: the display's, the layout's — its taskbar, folders and behaviour, §352),
// a folder's room's (BarLooks.Appearance for the folders) — is acted on a copy of a config in
// every way it can be (each choice, a step either way, flipped, done, words typed), and what the saved file
// then says differently is a setting that row writes. Every settable property of Config, and of its formats
// and its holidays, is written so, or is named below with why no row does; and (§345) a layout's own behaviour,
// its "openOnHover" (§514), its "combine" and its "previews". A property added to Config, or to a layout's own,
// fails this until a row writes it or it is named here.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Karpo.Tests.Menus;

public sealed class ConfigCoverageTests
{
    private const string Display = "\\\\.\\DISPLAY1";

    /// <summary>The objects whose own properties are settings each (the formats, the holidays): held one level down.</summary>
    private static readonly string[] Nested = ["formats", "holidays", "appearance"];

    /// <summary>§345: a layout's own settings, held as "layouts.&lt;name&gt;" (§514: whether what its widgets open opens on hover, once its "look" and "folders" a level down).</summary>
    private static readonly string[] LayoutOwn = ["openOnHover", "combine", "previews"];

    /// <summary>What no menu row writes, and why: Karpo's own bookkeeping, and what edit mode does by hand.</summary>
    private static readonly Dictionary<string, string> NoRow = new(StringComparer.Ordinal)
    {
        ["version"] = "the file's shape, Karpo's own",
        ["recentColors"] = "the colours picked last, kept as a colour is picked in Windows' own dialog (a browse, no row)",
    };

    /// <summary>Windows' side of "Start with Windows", a test's: nothing of the PC's is read or written.</summary>
    private static readonly StartupSwitch Startup = new(
        () => new StartupState("\"C:\\Karpo\\Karpo.exe\" --startup", "C:\\Karpo\\Karpo.exe", true, StartupApproval.NotSet),
        () => true,
        () => true);

    private static KarpoServices Services() => new(null, () => { })
    {
        Startup = Startup,
        Windows = () => ["en-US"],
        Open = _ => { },
        Licence = new KarpoLicence(new LicenseStatus(LicenseTier.Free, LicenseBasis.StoreFree), HasStore: true, CanPurchase: true, () => { }, () => { }),
        Backup = new KarpoBackup(() => { }, () => { }, () => { }, _ => { }),
        About = new KarpoAbout("1.0.0", ".NET 10"),
    };

    /// <summary>Every row of every menu that writes Karpo's own settings, as <paramref name="config"/> has them, each level's rows among them.</summary>
    private static List<OptionRow> Rows(Config config)
    {
        IEnumerable<OptionRow> all =
        [
            .. KarpoMenu.Rows(config, Services()),
            // §553: the Taskbar page's, the taskbar menu ("barMenu") its Behavior's expander (§585).
            .. Karpo.Dock.Dock.LooksOf(config, new BarSpot(Display, "One"), dark: true, LooksScope.Taskbar, services: Services()),
            // §585: the displays' rows and the layouts (§595 each its Default layout switch) the Layouts page's.
            .. BarLooks.Rows(config, new BarSpot(Display, "One"), dark: true, scope: LooksScope.Layouts),
            // §567: Karpo's one look, the Appearance page's.
            .. BarLooks.Rows(config, new BarSpot(Display, "One"), dark: true, scope: LooksScope.Appearance),
            .. BarLooks.FolderRows(config.DefaultLayout),
        ];
        return [.. OptionRows.Flat(all)];
    }

    /// <summary>Each way a row can be acted on: each choice and a step either way, flipped or done, words typed, a find picked.</summary>
    private static IEnumerable<Func<OptionRow, bool>> Ways(OptionRow row)
    {
        if (row.Set is { } set)
        {
            foreach (var value in new[] { 0, 1, 2, row.Choices.Count - 1, row.Value + 1, row.Value - 1, row.Least, row.Most }.Distinct())
            {
                yield return _ => set(value);
            }
        }
        if (row.SetText is { } text)
        {
            foreach (var words in new[] { "Work", "https://www.example.com/search?q={q}" })
            {
                yield return _ => text(words);
            }
        }
        // §631: a search's row (Language's) writes by a find picked: what it offers before anything is typed, and — a finder
        // that asks no service and needs no letters — what it finds for nothing typed, the first, the second and the last.
        if (row.Pick is { } pick && row.Finder is { } finder)
        {
            List<OptionFound> offered = [.. finder.Offered, .. finder.Suggested];
            if (finder.LeastLetters == 0 && !finder.Remote)
            {
                offered.AddRange(finder.FindAsync("", CancellationToken.None).GetAwaiter().GetResult());
            }
            foreach (var at in new[] { 0, 1, offered.Count - 1 }.Where(at => at >= 0 && at < offered.Count).Distinct())
            {
                yield return _ => pick(offered[at]);
            }
        }
    }

    /// <summary>The settings the file holds where they differ between <paramref name="before"/> and <paramref name="after"/>: a property, or one of a nested object's.</summary>
    private static IEnumerable<string> Changed(JsonObject before, JsonObject after)
    {
        foreach (var name in before.Select(pair => pair.Key).Union(after.Select(pair => pair.Key)))
        {
            var (was, now) = (before[name], after[name]);
            if (JsonNode.DeepEquals(was, now))
            {
                continue;
            }
            if (name == "layouts" && was is JsonArray layoutsBefore && now is JsonArray layoutsAfter)
            {
                foreach (var own in LayoutsOwn(layoutsBefore, layoutsAfter))
                {
                    yield return own;
                }
            }
            if (Nested.Contains(name) && (was ?? new JsonObject()) is JsonObject inner && (now ?? new JsonObject()) is JsonObject outer)
            {
                foreach (var child in inner.Select(pair => pair.Key).Union(outer.Select(pair => pair.Key)).Where(child => !JsonNode.DeepEquals(inner[child], outer[child])))
                {
                    yield return name + "." + child;
                }
            }
            else
            {
                yield return name;
            }
        }
    }

    /// <summary>§345: a layout's own settings (<see cref="LayoutOwn"/>) that differ between the layouts of the same id before and after.</summary>
    private static IEnumerable<string> LayoutsOwn(JsonArray before, JsonArray after)
    {
        var was = before.OfType<JsonObject>().ToDictionary(layout => (string)layout["id"]!);
        foreach (var layout in after.OfType<JsonObject>())
        {
            if (!was.TryGetValue((string)layout["id"]!, out var old))
            {
                continue;
            }
            foreach (var own in LayoutOwn.Where(own => !JsonNode.DeepEquals(old[own], layout[own])))
            {
                yield return "layouts." + own;
            }
        }
    }

    /// <summary>Config's settings as the file names them: each settable property, a nested object's one level down, a layout's own (§345).</summary>
    private static HashSet<string> Settable()
    {
        var options = ConfigJson.Default.Options;
        HashSet<string> names = [];
        foreach (var property in options.GetTypeInfo(typeof(TaskbarLayout)).Properties.Where(property => property.Set is not null && LayoutOwn.Contains(property.Name)))
        {
            names.Add("layouts." + property.Name);
        }
        foreach (var property in ConfigJson.Default.Config.Properties.Where(property => property.Set is not null))
        {
            if (Nested.Contains(property.Name))
            {
                var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                foreach (var inner in options.GetTypeInfo(type).Properties.Where(inner => inner.Set is not null))
                {
                    names.Add(property.Name + "." + inner.Name);
                }
            }
            else
            {
                names.Add(property.Name);
            }
        }
        return names;
    }

    /// <summary>
    /// A config with what makes every row stand: a second layout (so there is another to show and for new
    /// displays), both its surfaces in acrylic (so their opacity has its row, §334); §423 a holiday calendar (so its
    /// region and its colour have their rows: the one colour of them all, which wrote the holidays before, is gone).
    /// </summary>
    private static Config Seed()
    {
        var config = new Config();
        config.EditScreen(Display, "One");
        _ = config.ActiveLayout(Display);
        _ = BarLooks.Add(config, "Work");
        config.Holidays = new HolidayOptions { Calendars = [new HolidayCountry { Country = "CH" }] };
        return config;
    }

    [Fact]
    public void EverySettingIsWrittenByARowOfAMenu()
    {
        // The search engine one's own too, so its address has its row (§337).
        var custom = Seed();
        custom.SearchEngine = SearchEngine.Custom;
        Config[] seeds = [Seed(), custom];
        HashSet<string> written = [];
        foreach (var seed in seeds)
        {
            var before = JsonNode.Parse(seed.ToJson())!.AsObject();
            var count = Rows(seed.Clone()).Count;
            for (var at = 0; at < count; at++)
            {
                var ways = Ways(Rows(seed.Clone())[at]).Count();
                for (var way = 0; way < ways; way++)
                {
                    var copy = seed.Clone();
                    var rows = Rows(copy);
                    if (rows.Count != count)
                    {
                        continue;
                    }
                    try
                    {
                        _ = Ways(rows[at]).ElementAt(way)(rows[at]);
                    }
                    catch (ArgumentException)
                    {
                        // A value no choice has: nothing written.
                        continue;
                    }
                    written.UnionWith(Changed(before, JsonNode.Parse(copy.ToJson())!.AsObject()));
                }
            }
        }

        var settable = Settable();
        Assert.Empty(settable.Where(name => !written.Contains(name) && !NoRow.ContainsKey(name)).Order(StringComparer.Ordinal));
        // Nothing named here that a row writes after all, or that is no setting.
        var stale = NoRow.Keys.Where(written.Contains).Order(StringComparer.Ordinal).ToList();
        Assert.True(stale.Count == 0, "a row writes these after all: " + string.Join(", ", stale));
        var none = NoRow.Keys.Where(name => !settable.Contains(name)).ToList();
        Assert.True(none.Count == 0, "no setting of the file: " + string.Join(", ", none));
    }

    /// <summary>
    /// §347: where each tray icon shows is the tray widget's own ("trayIcons.states"), and its menu's Tray
    /// icons rows write it — into that tray, every choice of every row; its order is what a drag along it
    /// writes (TrayLayoutTests), as before.
    /// </summary>
    [Fact]
    public void TheTraysStatesAreWrittenByItsRows()
    {
        var seed = Seed();
        TrayMenuIcon[] icons = [new("spotify|0", "Spotify", AppHidden: false), new("onedrive|1", "OneDrive", AppHidden: true)];
        var devices = new OptionDevices { TrayIcons = icons };
        IReadOnlyList<OptionRow> Rows(Config config) =>
            [.. OptionRows.Flat(OptionRows.Of(config.DefaultLayout.Islands.First(island => island.Kind == BarKind.Tray), devices, new OptionPlace(config, Dark: true)))
                .Where(row => row.Key.StartsWith(OptionTray.Icon, StringComparison.Ordinal))];
        var count = Rows(seed).Count;
        Assert.Equal(icons.Length, count);

        HashSet<string> written = [];
        for (var at = 0; at < count; at++)
        {
            for (var choice = 0; choice < OptionTray.States.Count; choice++)
            {
                var copy = seed.Clone();
                _ = Rows(copy)[at].Set!(choice);
                // Written into the file, in the tray whose menu it is: every other tray says nothing still.
                var read = Karpo.Tests.Core.Configs.RoundTrip(copy, out _);
                var tray = read.DefaultLayout.Islands.First(island => island.Kind == BarKind.Tray);
                written.UnionWith((tray.TrayIcons?.States ?? []).Select(state => $"{state.Key}={state.Value}"));
                Assert.All(TrayLayout.Trays(read).Where(other => other != tray), other => Assert.Null(other.TrayIcons));
            }
        }

        Assert.Equal(["onedrive|1=Hidden", "onedrive|1=Taskbar", "spotify|0=Hidden", "spotify|0=Overflow"], written.Order(StringComparer.Ordinal));
    }

    // --- §351: one level each ---------------------------------------------------------------------------

    /// <summary>
    /// The settings <paramref name="rows"/> write, each with the key of a row that writes it: every way of
    /// acting on every row (each level's, flattened) of a copy of <paramref name="seed"/>, finer than
    /// <see cref="Changed"/> — each property of a layout and of its looks, of a nested object, and
    /// "accounts" for what is asked of the accounts (<paramref name="rows"/>' second argument, which a row's
    /// seam adds to).
    /// </summary>
    private static HashSet<(string Key, string Row)> WrittenBy(Config seed, Func<Config, List<string>, IEnumerable<OptionRow>> rows)
    {
        HashSet<(string Key, string Row)> written = [];
        var before = JsonNode.Parse(seed.ToJson())!.AsObject();
        List<OptionRow> Flat(Config config, List<string> asked) => [.. OptionRows.Flat(rows(config, asked))];
        var count = Flat(seed.Clone(), []).Count;
        for (var at = 0; at < count; at++)
        {
            var ways = Ways(Flat(seed.Clone(), [])[at]).Count();
            for (var way = 0; way < ways; way++)
            {
                var copy = seed.Clone();
                List<string> asked = [];
                var all = Flat(copy, asked);
                if (all.Count != count)
                {
                    continue;
                }
                try
                {
                    _ = Ways(all[at]).ElementAt(way)(all[at]);
                }
                catch (ArgumentException)
                {
                    continue;
                }
                foreach (var key in Keys(before, JsonNode.Parse(copy.ToJson())!.AsObject()).Concat(asked))
                {
                    written.Add((key, all[at].Key));
                }
            }
        }
        return written;
    }

    /// <summary>The settings that differ between <paramref name="before"/> and <paramref name="after"/>: a property, a nested object's, a layout's and its looks' (§345, §346), the layouts' list itself.</summary>
    private static IEnumerable<string> Keys(JsonObject before, JsonObject after)
    {
        static IEnumerable<string> Inner(string name, JsonNode? was, JsonNode? now) =>
            was is JsonObject inner && now is JsonObject outer
                ? inner.Select(pair => pair.Key).Union(outer.Select(pair => pair.Key)).Where(child => !JsonNode.DeepEquals(inner[child], outer[child])).Select(child => name + "." + child)
                : [name];
        foreach (var name in before.Select(pair => pair.Key).Union(after.Select(pair => pair.Key)).Where(name => !JsonNode.DeepEquals(before[name], after[name])))
        {
            if (name == "layouts" && before[name] is JsonArray was && after[name] is JsonArray now)
            {
                static string Id(JsonObject layout) => (string)layout["id"]!;
                if (!was.OfType<JsonObject>().Select(Id).SequenceEqual(now.OfType<JsonObject>().Select(Id)))
                {
                    yield return "layouts";
                }
                var old = was.OfType<JsonObject>().ToDictionary(Id);
                foreach (var layout in now.OfType<JsonObject>().Where(layout => old.ContainsKey(Id(layout))))
                {
                    var them = old[Id(layout)];
                    foreach (var own in them.Select(pair => pair.Key).Union(layout.Select(pair => pair.Key)).Where(own => !JsonNode.DeepEquals(them[own], layout[own])))
                    {
                        yield return "layouts." + own;
                    }
                }
                continue;
            }
            foreach (var key in Inner(name, before[name] ?? new JsonObject(), after[name] ?? new JsonObject()))
            {
                yield return key;
            }
        }
    }

    /// <summary>⚙'s services with the accounts too, each thing asked of them said as "accounts".</summary>
    private static KarpoServices Services(List<string> asked)
    {
        var forms = new AccountForms((_, _) => { asked.Add("accounts"); return Task.FromResult(new AccountTry("imap.example.com")); }, work => work(), () => { });
        OptionAccount[] all =
        [
            new("m1", "me@outlook.com", OptionAccountKind.Microsoft)
            {
                Groups = [new("Calendars", [new OptionAccountPart("cal", "Calendar", OptionGlyphs.DateAndTime, On: true) { Automatic = "#C42B1C" }])],
            },
        ];
        var accounts = new OptionAccounts(all, _ => asked.Add("accounts"), _ => asked.Add("accounts"), _ => asked.Add("accounts"), forms)
        {
            Sync = (_, _, _) => asked.Add("accounts"),
            Recolour = (_, _, _) => asked.Add("accounts"),
        };
        return Services() with { Accounts = accounts };
    }

    /// <summary>
    /// §351 (the person, 2026-10-25: "some settings appear multiple times like account, then i can also change
    /// account settings in a widget for example. i would rather not have that"): every setting is set at one
    /// level — Karpo's (⚙ Settings), a display's (Display ▾), a layout's (Layout ▾, §352; a folder's room's right click) or a
    /// widget's (its menu, every widget and every face) —, a lower one linking to it. Each row of each level is
    /// acted on in every way it can be, and what it writes — of the file, or asked of the accounts — is that
    /// level's: nothing is written from two, and a widget's rows write nothing
    /// but the widget (a widget here is no widget of the file, so anything they write is another level's).
    /// </summary>
    [Fact]
    public void NoSettingIsSetAtTwoLevels()
    {
        var seed = Seed();
        var bar = new BarSpot(Display, "One");
        var place = (Config config) => new OptionPlace(config, Dark: true);
        var widgets = Enum.GetValues<BarKind>().Select(kind => (Func<WidgetItem>)(() => kind.New()))
            .Concat(Enum.GetValues<ListKind>().Select(kind => (Func<WidgetItem>)(() => kind.New())))
            .Where(make => make().Info is not null)
            .Select(make => (Func<Config, List<string>, IEnumerable<OptionRow>>)((config, _) => OptionRows.Of(make(), null, place(config))))
            .Concat(FolderFaces.All.Select(face => (Func<Config, List<string>, IEnumerable<OptionRow>>)((config, _) =>
            {
                var folder = BarKind.FolderList.New();
                folder.Folder!.Faces = [FolderFace.Of(face.Id)];
                return OptionRows.OfFace(folder.Folder, 0, null, place(config));
            })));
        (string Level, Func<Config, List<string>, IEnumerable<OptionRow>>[] Menus)[] levels =
        [
            // §535: Karpo's page and its Accounts page, one level.
            ("Karpo", [(config, asked) => KarpoMenu.Rows(config, Services(asked)), (config, asked) => KarpoMenu.Accounts(config, Services(asked)),
                (config, _) => BarLooks.Rows(config, bar, dark: true, scope: LooksScope.Appearance)]),
            // §585: the Layouts page, the one level Display ▾ was: which layout a display shows, the layouts made,
            // copied, named, drawn and deleted — what that does to the displays —, what new displays show.
            ("display", [(config, _) => BarLooks.Rows(config, bar, dark: true, scope: LooksScope.Layouts)]),
            ("layout", [.. new[] { LooksScope.Taskbar, LooksScope.Folders }.Select(scope => (Func<Config, List<string>, IEnumerable<OptionRow>>)((config, _) => BarLooks.Rows(config, bar, dark: true, scope: scope)))]),
            ("widget", [.. widgets]),
        ];
        List<(string Key, string Level, string Row)> all = [];
        foreach (var (level, menus) in levels)
        {
            foreach (var menu in menus)
            {
                all.AddRange(WrittenBy(seed, menu).Select(each => (each.Key, level, each.Row)));
            }
        }
        var by = all.GroupBy(each => each.Key).ToDictionary(group => group.Key, group => group.Select(each => (each.Level, each.Row)).ToList());

        // The widgets' write nothing of the file's but the widget, and ask nothing of the accounts.
        Assert.Empty(all.Where(each => each.Level == "widget").Select(each => $"{each.Key} by {each.Row}").Distinct());
        // Nothing is set at two levels (§361: what a layout made, copied or deleted does to the displays is the
        // display's own, the layouts managed there).
        var twice = all.GroupBy(each => each.Key)
            .Where(group => group.Select(each => each.Level).Distinct().Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(each => $"{each.Level} ({each.Row})").Distinct())}")
            .ToList();
        Assert.True(twice.Count == 0, "set at two levels: " + string.Join("; ", twice));
        // The accounts are Karpo's, the holidays too, §567 the Appearance (one look, Karpo-wide, no layout's), which layout a
        // display shows the display's.
        Assert.Equal(["Karpo"], by["accounts"].Select(each => each.Level).Distinct());
        Assert.Equal(["Karpo"], by["holidays.calendars"].Select(each => each.Level).Distinct());
        Assert.Equal(["Karpo"], by.Where(pair => pair.Key.StartsWith("appearance.", StringComparison.Ordinal)).SelectMany(pair => pair.Value).Select(each => each.Level).Distinct());
        Assert.DoesNotContain(by.Keys, key => key == "layouts.looks");
        Assert.Equal(["display"], by["screens"].Select(each => each.Level).Distinct());
        // §361: the layouts made, copied, put in order, deleted, named and drawn are the display's, with which
        // one it shows; a layout's own look and behaviour stay the layout's.
        Assert.Equal(["display"], by["layouts"].Select(each => each.Level).Distinct());
        Assert.Equal(["display"], by["layouts.name"].Select(each => each.Level).Distinct());
        Assert.Equal(["display"], by["layouts.icon"].Select(each => each.Level).Distinct());
        Assert.Equal(["display"], by["defaultLayoutId"].Select(each => each.Level).Distinct());
        Assert.Equal(["layout"], by["layouts.type"].Select(each => each.Level).Distinct());
    }

    /// <summary>
    /// §364 (the settings scope audit's a4): within a folder the same holds one level down — a face's rows
    /// write that face alone (the file's "face", or "faces" beside others), and the folder's own rows over
    /// its faces the folder's own: where it stands (§385; a name and "Show the name beside
    /// it" went with §427). Every row of every face, alone
    /// and beside another, acted every way, on a folder of its own.
    /// </summary>
    [Fact]
    public void AFacesRowsWriteTheFaceAndAFoldersRowsTheFolder()
    {
        var place = new OptionPlace(Seed(), Dark: true);
        static JsonObject Folder(BarItem item) =>
            JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(item, ConfigJson.Default.BarItem))!["folder"]!.AsObject();
        static HashSet<string> Written(Func<BarItem> make, Func<BarItem, IEnumerable<OptionRow>> rows)
        {
            var before = Folder(make());
            var count = OptionRows.Flat(rows(make())).Count();
            HashSet<string> written = [];
            for (var at = 0; at < count; at++)
            {
                var ways = Ways(OptionRows.Flat(rows(make())).ElementAt(at)).Count();
                for (var way = 0; way < ways; way++)
                {
                    var item = make();
                    var all = OptionRows.Flat(rows(item)).ToList();
                    if (all.Count != count)
                    {
                        continue;
                    }
                    try
                    {
                        _ = Ways(all[at]).ElementAt(way)(all[at]);
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                    var after = Folder(item);
                    written.UnionWith(before.Select(pair => pair.Key).Union(after.Select(pair => pair.Key)).Where(key => !JsonNode.DeepEquals(before[key], after[key])));
                }
            }
            return written;
        }
        HashSet<string> faces = [];
        foreach (var face in FolderFaces.All)
        {
            foreach (var worn in new[] { new[] { face.Id }, [face.Id, FolderFaces.WifiId] })
            {
                BarItem Make()
                {
                    var folder = BarKind.FolderList.New();
                    folder.Folder!.Faces = [.. worn.Select(FolderFace.Of)];
                    return folder;
                }
                faces.UnionWith(Written(Make, item => OptionRows.OfFace(item.Folder!, 0, null, place)));
            }
        }
        Assert.Subset(new HashSet<string>(["face", "faces"]), faces);
        Assert.Contains("face", faces);

        // The folder's own: §385, where it stands; no name (§427).
        // §647: how it stands beside its bar.
        static BarItem Own() => BarKind.FolderList.New();
        Assert.Equal(["align", "style"], Written(Own, item => OptionRows.Of(item, null, place)).Order(StringComparer.Ordinal));
        // §474: one whose face is the clock the same: a folder opens itself (§472's "opens" gone).
        static BarItem Clock()
        {
            var folder = BarKind.FolderList.New();
            folder.Folder!.Faces = [FolderFace.Of(FolderFaces.DateTimeId)];
            return folder;
        }
        Assert.Equal(["align", "style"], Written(Clock, item => OptionRows.Of(item, null, place)).Order(StringComparer.Ordinal));
    }
}
