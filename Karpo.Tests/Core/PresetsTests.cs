// The ready-made groups (Karpo.Core\Presets.cs; 2026-10-05: "another additional section where you
// can add precompiled groups of stuff"). The recipes are data: every widget in one is a member of
// the list's registry; what a recipe makes is an ordinary folder with ids nobody else
// has, showing its own icon; and adding one at a "+" of the bar puts that **one folder** there, with
// its widgets inside (2026-10-05: "the ready made widgets should only ever add one folder with
// multiple widgets inside"). A folder offers none: folders do not nest. Plain code: no window, no
// Direct2D.

namespace Karpo.Tests.Core;

using Folders = Karpo.Core.Folders;

public sealed class PresetsTests
{
    /// <summary>A PC with a battery, two fixed drives, sleep and hibernate, so every recipe is made in full.</summary>
    internal static readonly PresetMachine Laptop = new(
        HasBattery: true,
        FixedDrives:
        [
            new DriveVolume(@"\\?\Volume{11111111-1111-1111-1111-111111111111}\", "C:", "Windows", DriveKind.Fixed, 0, 0),
            new DriveVolume(@"\\?\Volume{22222222-2222-2222-2222-222222222222}\", "D:", "Data", DriveKind.Fixed, 0, 0),
        ],
        Sleeps: true,
        Hibernates: true);

    /// <summary>A desktop PC: no battery, one fixed drive.</summary>
    internal static readonly PresetMachine Desktop = new(
        HasBattery: false,
        FixedDrives: [new DriveVolume(@"\\?\Volume{11111111-1111-1111-1111-111111111111}\", "C:", "Windows", DriveKind.Fixed, 0, 0)]);

    public static TheoryData<string> Ids()
    {
        var data = new TheoryData<string>();
        foreach (var preset in Presets.All)
        {
            data.Add(preset.Id);
        }
        return data;
    }

    /// <summary>Every preset at every "+" of the bar: the end of each section, and a place inside the left one, before what stood there.</summary>
    public static TheoryData<string, BarSection, bool> EveryPlace()
    {
        var data = new TheoryData<string, BarSection, bool>();
        foreach (var preset in Presets.All)
        {
            foreach (var section in new[] { BarSection.Start, BarSection.Middle, BarSection.End })
            {
                data.Add(preset.Id, section, false);
            }
            data.Add(preset.Id, BarSection.Start, true);
        }
        return data;
    }

    private static Preset Named(string id) => Presets.Find(id) ?? throw new InvalidOperationException($"no preset {id}");

    /// <summary>
    /// The preset that made <paramref name="folder"/> on <paramref name="machine"/>: the one whose folder wears
    /// the same faces — of two that do, the one with the same widgets inside. A folder keeps no name to tell
    /// it by (§427).
    /// </summary>
    internal static Preset MadeBy(BarItem folder, PresetMachine machine)
    {
        var wearing = Presets.All.Where(preset => Faces(Presets.Folder(preset, machine)) == Faces(folder)).ToList();
        return wearing.Count == 1 ? wearing[0] : wearing.Single(preset => Lines(Presets.Folder(preset, machine)) == Lines(folder));
    }

    /// <summary>The faces a folder wears, as the file writes them.</summary>
    private static string Faces(BarItem folder)
    {
        var written = System.Text.Json.Nodes.JsonNode.Parse(Configs.With(folder).ToJson())!["layouts"]![0]!["islands"]![0]!["folder"]!;
        return $"{written["face"]?.ToJsonString()} {written["faces"]?.ToJsonString()}";
    }

    /// <summary>The kinds of a folder's lines, list by list.</summary>
    private static string Lines(BarItem folder) =>
        string.Join("|", folder.Folder!.Lists.Select(list => string.Join(",", list.Select(cell => cell.Island.Kind))));

    /// <summary>A bar of a clock at the start, the apps in the middle and show desktop at the end, and a folder.</summary>
    private static List<BarItem> Bar()
    {
        var clock = BarKind.NightLight.New();
        clock.Id = "clock";
        var apps = BarKind.Apps.New();
        apps.Id = "apps";
        var desktop = BarKind.ShowDesktop.New();
        desktop.Id = "desktop";
        var list = BarKind.FolderList.New();
        list.Id = "list";
        clock.Section = BarSection.Start;
        list.Section = BarSection.Start;
        apps.Section = BarSection.Middle;
        desktop.Section = BarSection.End;
        List<BarItem> bar = [clock, list, apps, desktop];
        Sections.Order(bar);
        return bar;
    }

    // --- The recipes ------------------------------------------------------------------------------

    [Fact]
    public void ThePersonsTwoAndTheConnectionsAreThere()
    {
        // The person's: a list of the system's widgets, a list of stocks (Markets, §648).
        Assert.Equal(BarKind.FolderList, Named("systemGroup").Folder);
        Assert.Equal(BarKind.FolderList, Named(Presets.MarketId).Folder);
        // The connections are a folder too, a list of them, and no longer loose taskbar entries.
        Assert.Equal(BarKind.FolderList, Named("connectivityGroup").Folder);
        // Windows 11's quick settings and widgets board were grids, and went with them (2026-10-09):
        // every group is a list.
        Assert.Null(Presets.Find("quickSettingsGroup"));
        Assert.Null(Presets.Find("widgetsGroup"));
        Assert.All(Presets.All, preset => Assert.Equal(BarKind.FolderList, preset.Folder));
    }

    [Fact]
    public void EveryPresetHasANameAnIconAndAnIdOfItsOwn()
    {
        Assert.Equal(Presets.All.Length, Presets.All.Select(preset => preset.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(Presets.All.Length, Presets.All.Select(preset => preset.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var preset in Presets.All)
        {
            // Never a widget's word: a preset and a widget are never mistaken for each other.
            Assert.Null(WidgetInfos.Find(preset.Id));
            Assert.False(string.IsNullOrWhiteSpace(preset.Title));
            Assert.False(string.IsNullOrWhiteSpace(preset.Blurb));
            Assert.Single(preset.Glyph.EnumerateRunes());
            Assert.NotEmpty(preset.Items);
            Assert.Same(preset, Presets.Find(preset.Id));
        }
        Assert.Null(Presets.Find("nothing"));
        Assert.Null(Presets.Find(null));
    }

    /// <summary>
    /// Each folder a group makes shows an icon of its own, never the plain folder's, and no two
    /// groups share one: Connectivity the network's, Markets a chart (Stocks', §648), as the person asked
    /// ("the ready made folders should also get an icon per default").
    /// </summary>
    [Fact]
    public void EachGroupHasAFittingIconOfItsOwn()
    {
        Assert.Equal(Presets.All.Length, Presets.All.Select(preset => preset.Glyph).Distinct(StringComparer.Ordinal).Count());
        Assert.All(Presets.All, preset => Assert.NotEqual(FolderOptions.FolderGlyph, preset.Glyph));
        // Segoe Fluent Icons: Network, AreaChart.
        Assert.Equal("\uE968", Named("connectivityGroup").Glyph);
        Assert.Equal("\uE9D2", Named(Presets.MarketId).Glyph);
    }

    /// <summary>Every widget of every recipe is a member of the list's registry: a line of its folder.</summary>
    [Theory]
    [MemberData(nameof(Ids))]
    public void EveryWidgetIsInItsFoldersRegistry(string id)
    {
        var preset = Named(id);
        Assert.True(preset.Folder.IsFolder(), $"{preset.Title} makes no folder");
        Assert.Equal(WidgetType.List, preset.Type);
        foreach (var item in preset.Items)
        {
            Assert.NotNull(ListWidgets.Find(item.Form));
            Assert.True(Folders.Holds(FolderView.List, item.Form), item.Form);
            Assert.NotNull(WidgetInfos.Find(item.Form));
        }
    }

    /// <summary>The markets follow well-known symbols, so a new Markets folder shows prices at once and asks for nothing (§648).</summary>
    [Fact]
    public void TheStocksFollowWellKnownSymbols()
    {
        var watchlist = Presets.Cells(Named(Presets.MarketId), Desktop);
        Assert.Equal(["MSFT", "AAPL", "NVDA", "^GSPC"], watchlist.Select(cell => cell.Island.Market!.Symbol));
        Assert.Equal("S&P 500", watchlist[^1].Island.Market!.Label);
        Assert.All(watchlist, cell => Assert.Null(WidgetSetup.Needs(cell.Island)));
    }

    /// <summary>Connectivity is the connections a folder can hold, a line each, the network's activity first.</summary>
    [Fact]
    public void ConnectivityIsAListOfTheConnections()
    {
        var lines = Presets.Cells(Named("connectivityGroup"), Desktop);
        Assert.Equal(["network", "divider", "airplaneMode", "vpn", "mobileHotspot", "nearbyShare"], lines.Select(cell => cell.Island.Kind));
        Assert.All(lines, cell => Assert.Equal(FolderView.List, cell.Island.View));
        // The network's and Bluetooth's pages are lines of their own presets now (2026-10-10), not Connectivity's.
        Assert.True(Folders.Holds(FolderView.List, "wifi"));
        Assert.True(Folders.Holds(FolderView.List, "bluetooth"));
    }

    // --- What this PC has ----------------------------------------------------------------------------

    [Fact]
    public void TheBatteryIsInSystemOnlyWhereThePcHasOne()
    {
        var system = Named("systemGroup");
        Assert.Contains(Presets.Cells(system, Laptop), cell => cell.Island.Kind == "battery");
        Assert.DoesNotContain(Presets.Cells(system, Desktop), cell => cell.Island.Kind == "battery");
        Assert.Equal(["cpu", "divider", "memory", "divider", "disk", "network", "divider", "gpu", "actions"], Presets.Cells(system, Desktop).Select(cell => cell.Island.Kind));
    }

    [Fact]
    public void StorageHasALineForEachFixedDriveThenTheRemovableDevices()
    {
        var lines = Presets.Cells(Named("storageGroup"), Laptop);
        Assert.Equal(["drive", "drive", "divider", "removableDevices"], lines.Select(cell => cell.Island.Kind));
        Assert.Equal(["C:", "D:"], lines.Take(2).Select(cell => cell.Island.Drive!.Letter));
        Assert.Equal(Laptop.FixedDrives[1].Volume, lines[1].Island.Drive!.Volume);
        Assert.All(lines.Take(2), cell => Assert.True(cell.Island.Drive!.IsSet));
        // A PC whose drives could not be read still gets what is plugged in, and no divider over it.
        Assert.Equal(["removableDevices"], Presets.Cells(Named("storageGroup"), new PresetMachine(false, [])).Select(cell => cell.Island.Kind));
    }

    // --- Dividers (2026-10-08: "add proper dividers for the ready made widget groups") ----------------

    /// <summary>A PC with no battery and no fixed drive it could read: the most a recipe leaves out.</summary>
    private static readonly PresetMachine Bare = new(HasBattery: false, FixedDrives: []);

    /// <summary>
    /// <paramref name="kinds"/>' dividers each stand between two widgets: never first, never last,
    /// never two together.
    /// </summary>
    private static void AssertDividersOnlyBetween(string what, IReadOnlyList<string> kinds)
    {
        var divider = WidgetInfos.Divider.Id;
        Assert.NotEmpty(kinds);
        Assert.False(kinds[0] == divider, $"{what} starts with a divider");
        Assert.False(kinds[^1] == divider, $"{what} ends with a divider");
        for (var i = 1; i < kinds.Count; i++)
        {
            Assert.False(kinds[i] == divider && kinds[i - 1] == divider, $"{what} has two dividers together at {i}");
        }
    }

    /// <summary>The lines a group makes on <paramref name="machine"/>, its dividers as "|", a list beside the last as "/".</summary>
    private static string Lines(string id, PresetMachine machine) =>
        string.Join(" ", Presets.ItemsOn(Named(id), machine).Select(entry => entry.Item.IsDivider ? "|" : entry.Item.NewList ? $"/ {entry.Item.Form}" : entry.Item.Form));

    /// <summary>
    /// Every group's dividers stand only between two groups of its lines, in the recipe and in what
    /// it makes on any PC: with everything, with no battery, with no fixed drive.
    /// </summary>
    [Theory]
    [MemberData(nameof(Ids))]
    public void EveryGroupsDividersStandOnlyBetweenItsGroups(string id)
    {
        var preset = Named(id);
        AssertDividersOnlyBetween(id, [.. preset.Items.Select(item => item.Form)]);
        foreach (var machine in new[] { Laptop, Desktop, Bare })
        {
            AssertDividersOnlyBetween($"{id} on {machine}", [.. Presets.ItemsOn(preset, machine).Select(entry => entry.Item.Form)]);
            AssertDividersOnlyBetween($"{id}'s folder on {machine}", [.. Presets.Cells(preset, machine).Select(cell => cell.Island.Kind)]);
        }
    }

    /// <summary>
    /// Where each group is split into cards, only where a split clearly helps (the person,
    /// 2026-10-09): the focus lines over the calendar, the fixed drives over what is plugged in, the
    /// network's activity over the switches. Lines of one subject are one card with their footer in
    /// it, as the person's own clock (2026-10-11): the clock, the calendar, the weather, the sound,
    /// the notifications. What's playing is its line alone, as the person made it. A group of one kind
    /// of thing — the markets — is not split; System's readings are, a card each in two lists
    /// (2026-10-11: "make a divider in between"), its footer in the last. The sound is one column, its
    /// devices over its mixer, as the person rearranged it on their main bar (2026-10-11).
    /// </summary>
    [Theory]
    [InlineData("clockGroup", "timeHeader timer stopwatch focus actions")]
    [InlineData("systemGroup", "cpu | memory | disk / network | gpu | battery actions")]
    [InlineData(Presets.MarketId, "markets markets markets markets")]
    [InlineData("notificationsGroup", "notificationsList actions")]
    [InlineData("focusGroup", "clock timer focus doNotDisturb | calendar")]
    [InlineData("mediaGroup", "media")]
    [InlineData("driveGroup", "drive folderContents")]
    [InlineData("powerGroup", "signOut lock sleep restart shutDown")]
    [InlineData("storageGroup", "drive drive | removableDevices")]
    [InlineData("connectivityGroup", "network | airplaneMode vpn mobileHotspot nearbyShare")]
    [InlineData("calendarGroup", "dateHeader month events actions")]
    [InlineData("weatherGroup", "weatherNow weatherHours weatherDays")]
    [InlineData("soundGroup", "soundOutput soundInput | soundMixer soundVolume actions")]
    [InlineData("removableGroup", "removableDevices")]
    [InlineData("timerGroup", "timer")]
    [InlineData("focusTimeGroup", "focus")]
    [InlineData("stopwatchGroup", "stopwatch")]
    public void EachListGroupIsPartedWhereItsGroupsMeet(string id, string lines) =>
        Assert.Equal(lines, Lines(id, Laptop));

    /// <summary>
    /// A divider goes with the group this PC lacks: Storage with no fixed drive is the removable
    /// devices alone. System on a PC with no battery has the graphics over its footer, no divider
    /// between them.
    /// </summary>
    [Fact]
    public void ADividerGoesWithTheGroupThisPcLacks()
    {
        Assert.Equal("cpu | memory | disk / network | gpu actions", Lines("systemGroup", Desktop));
        Assert.Equal("drive | removableDevices", Lines("storageGroup", Desktop));
        Assert.Equal("removableDevices", Lines("storageGroup", Bare));
    }

    /// <summary>
    /// The tidy itself, on a recipe that would leave dividers first, last and together: each goes,
    /// and the one between two widgets stays.
    /// </summary>
    [Fact]
    public void ADividerLeftFirstLastOrDoubledGoes()
    {
        var odd = new Preset
        {
            Id = "oddGroup", Glyph = "", Folder = BarKind.FolderList,
            Items =
            [
                PresetItem.Divider,
                new() { Kind = "cpu" },
                PresetItem.Divider,
                PresetItem.Divider,
                new() { Kind = "battery", Needs = PresetNeed.Battery },
                PresetItem.Divider,
                new() { Kind = "drive", EachFixedDrive = true },
                PresetItem.Divider,
            ],
        };
        string Made(PresetMachine machine) =>
            string.Join(" ", Presets.ItemsOn(odd, machine).Select(entry => entry.Item.IsDivider ? "|" : entry.Item.Form));

        Assert.Equal("cpu | battery | drive drive", Made(Laptop));
        Assert.Equal("cpu | drive", Made(Desktop));
        Assert.Equal("cpu", Made(Bare));
    }

    /// <summary>A group is not found by its dividers: "divider" is the list's line, not anything a group is.</summary>
    [Fact]
    public void NoGroupIsFoundByItsDividers()
    {
        Assert.All(Presets.All, preset => Assert.False(
            CatalogueSearch.Matches(WidgetInfos.Divider.Id, preset.Title, Presets.WordsOf(preset)), $"\"divider\" finds {preset.Title}"));
    }

    // --- What a recipe makes -------------------------------------------------------------------------

    /// <summary>
    /// A preset makes an ordinary folder: its kind, its name and icon, its button showing that icon
    /// rather than its widgets', every widget standing in its view at the recipe's place with its own
    /// id, a list one column of its lines, nothing the folder's own tidy would move or drop.
    /// </summary>
    [Theory]
    [MemberData(nameof(Ids))]
    public void APresetMakesAnOrdinaryFolder(string id)
    {
        var preset = Named(id);
        var made = Presets.Folder(preset, Laptop);
        Assert.Equal(preset.Folder, made.Kind);
        var folder = made.Folder!;
        Assert.Equal(preset.Face is null ? preset.Glyph : null, folder.Face.Glyph);
        Assert.Equal(preset.Face?.Kind ?? FolderFaces.IconId, folder.Face.Kind);
        Assert.Equal(Presets.ItemsOn(preset, Laptop).Count, folder.Cells.Count);
        var view = Folders.View(preset.Folder);
        Assert.Equal(FolderView.List, view);
        Assert.All(folder.Cells, cell => Assert.Equal(view, cell.Island.View));
        Assert.Equal(folder.Cells.Count, folder.Cells.Select(cell => cell.Island.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        // Its lists, each of its lines in the recipe's order: one, but where a line heads another.
        Assert.Equal(1 + Presets.ItemsOn(preset, Laptop).Skip(1).Count(entry => entry.Item.NewList), folder.Lists.Count);
        // A line held to a height keeps it.
        foreach (var (cell, (item, _)) in folder.Cells.Zip(Presets.ItemsOn(preset, Laptop)))
        {
            if (item.Height is { } height)
            {
                Assert.Equal(height, LineHeight.For(cell.Island));
            }
        }
        // Its dividers are the list's own deliberate lines, each between two widgets.
        AssertDividersOnlyBetween(id, folder.Cells.Select(cell => cell.Island.Kind).ToList());
        // The folder's own tidy, as Config.Repair runs it on every read, moves and drops nothing,
        // and keeps the button as made.
        var places = folder.Cells.Select(cell => cell.Island.Id).ToList();
        var copy = made.Clone();
        Assert.False(Folders.Tidy(copy.Folder!));
        Assert.Equal(places, copy.Folder!.Cells.Select(cell => cell.Island.Id));
        Assert.Empty(Folders.Project(copy.Folder!, view));
        copy.Normalize();
        Assert.Equal(preset.Face?.Kind ?? FolderFaces.IconId, copy.Folder!.Face.Kind);
    }

    [Fact]
    public void EachMakingIsNewWidgets()
    {
        var first = Presets.Folder(Named("systemGroup"), Laptop);
        var second = Presets.Folder(Named("systemGroup"), Laptop);
        Assert.NotSame(first.Folder!.Cells[0].Island, second.Folder!.Cells[0].Island);
        Assert.Empty(first.Folder.Cells.Select(cell => cell.Island.Id).Intersect(second.Folder.Cells.Select(cell => cell.Island.Id)));
    }

    // --- Where they are offered ----------------------------------------------------------------------

    /// <summary>The bar offers every preset; a folder none, since a preset is one folder and folders do not nest.</summary>
    [Fact]
    public void TheBarOffersEveryPresetAndAFolderNone()
    {
        Assert.Equal(Presets.All, Presets.On(WidgetType.Bar));
        Assert.Empty(Presets.On(WidgetType.List));
    }

    // --- Search ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("system", "systemGroup")]
    [InlineData("cpu", "systemGroup")]
    [InlineData("task manager", "systemGroup")]
    [InlineData("stocks", Presets.MarketId)]
    [InlineData("watchlist", Presets.MarketId)]
    [InlineData("nvda", Presets.MarketId)]
    [InlineData("bluetooth", "connectivityGroup")]
    [InlineData("wi-fi", "connectivityGroup")]
    [InlineData("hotspot", "connectivityGroup")]
    [InlineData("drives", "storageGroup")]
    [InlineData("pomodoro", "focusGroup")]
    [InlineData("music", "mediaGroup")]
    [InlineData("notification center", "notificationsGroup")]
    public void APresetIsFoundByItsNameItsWordsAndItsWidgets(string query, string id)
    {
        var preset = Named(id);
        Assert.True(CatalogueSearch.Matches(query, preset.Title, Presets.WordsOf(preset)), $"\"{query}\" does not find {preset.Title}");
    }

    [Fact]
    public void APresetIsNotFoundByWhatItDoesNotHold()
    {
        var stocks = Named(Presets.MarketId);
        Assert.False(CatalogueSearch.Matches("bluetooth", stocks.Title, Presets.WordsOf(stocks)));
        Assert.False(CatalogueSearch.Matches("processor", stocks.Title, Presets.WordsOf(stocks)));
        // The names of the widgets inside count: "Processor" finds System although no keyword says it.
        Assert.True(CatalogueSearch.Matches("processor", Named("systemGroup").Title, Presets.WordsOf(Named("systemGroup"))));
    }

    // --- Adding one ------------------------------------------------------------------------------------

    /// <summary>
    /// <b>One folder, always</b>, wherever a group is added: at each section's "+" and at a "+" inside
    /// a section, every group adds exactly one entry to the bar — its folder, of its kind, in the
    /// section of the "+" at its place, showing its icon — with all its widgets inside and nothing
    /// else added, every id one no other island of the bar or its folders has. The regression:
    /// Connectivity put four loose buttons on the bar.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryPlace))]
    public void EveryGroupAddsExactlyOneFolderWithItsWidgetsInside(string id, BarSection section, bool inside)
    {
        var preset = Named(id);
        var bar = Bar();
        var before = Folders.All(bar).Select(entry => entry.Island.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var kept = bar.Select(island => island.Id).ToList();
        // The "+" at the end of the section, as Layout.AddButtons places it, or one before the clock.
        var slot = inside
            ? new BarSlot(section, bar.FindIndex(island => island.Id == "clock"))
            : new BarSlot(section, Sections.Range(bar, section).To);

        var path = Presets.AddToBar(bar, slot, preset, Laptop);

        // One entry more, and it is the folder.
        Assert.Equal(kept.Count + 1, bar.Count);
        Assert.Equal(kept, bar.Where(island => island.Id != path).Select(island => island.Id));
        var at = bar.FindIndex(island => island.Id == path);
        var folder = bar[at];
        Assert.Equal(preset.Folder, folder.Kind);
        Assert.True(folder.Kind.IsFolder());
        Assert.Equal(section, folder.Section);
        Assert.Equal(preset.Face?.Kind ?? FolderFaces.IconId, folder.Folder!.Face.Kind);
        Assert.Equal(preset.Face is null ? preset.Glyph : null, folder.Folder.Face.Glyph);
        // Where its "+" stood: last in its section, or where the clock was, the clock after it.
        if (inside)
        {
            Assert.Equal("clock", bar[at + 1].Id);
        }
        else
        {
            Assert.Equal(Sections.Range(bar, section).To - 1, at);
        }
        // Every widget of the group is inside it: N members, in one folder.
        Assert.Equal(Presets.ItemsOn(preset, Laptop).Count, folder.Folder.Cells.Count);
        Assert.Equal(preset.Items.Select(item => item.Form).Distinct(), folder.Folder.Cells.Select(cell => cell.Island.Kind).Distinct());
        var ids = Folders.All([folder]).Select(entry => entry.Island.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(ids, each => Assert.DoesNotContain(each, before));
    }

    /// <summary>The regression itself: Connectivity on the bar is one list folder, and no network, Bluetooth, VPN or airplane button.</summary>
    [Fact]
    public void ConnectivityOnTheBarIsOneFolderNotItsButtons()
    {
        var bar = Bar();
        var slot = new BarSlot(BarSection.End, Sections.Range(bar, BarSection.End).To);

        var path = Presets.AddToBar(bar, slot, Named("connectivityGroup"), Desktop);

        Assert.Equal(5, bar.Count);
        Assert.Equal(BarKind.FolderList, bar.Single(island => island.Id == path).Kind);
        Assert.DoesNotContain(bar, island => island.Kind is BarKind.AirplaneMode);
    }

    [Fact]
    public void TheSamePresetTwiceIsTwoFoldersWithIdsOfTheirOwn()
    {
        var bar = Bar();
        var slot = new BarSlot(BarSection.End, Sections.Range(bar, BarSection.End).To);
        var first = Presets.AddToBar(bar, slot, Named(Presets.MarketId), Desktop);
        var second = Presets.AddToBar(bar, slot with { Index = Sections.Range(bar, BarSection.End).To }, Named(Presets.MarketId), Desktop);

        Assert.NotEqual(first, second);
        Assert.Equal(6, bar.Count);
        var ids = Folders.All(bar).Select(entry => entry.Island.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// A recipe's lists side by side (PresetItem.NewList): each line that heads one starts a list
    /// right of the last, the widths go with them, and a list whose head this PC lacks starts at the
    /// line after it; a divider never ends a list, and the drives after the first stand under it.
    /// </summary>
    [Fact]
    public void ARecipesListsStandSideBySide()
    {
        var odd = new Preset
        {
            Id = "oddGroup", Glyph = "", Folder = BarKind.FolderList,
            Items =
            [
                new() { Kind = "cpu" },
                PresetItem.Divider,
                new() { Kind = "battery", Needs = PresetNeed.Battery, NewList = true },
                new() { Kind = "memory" },
                new() { Kind = "drive", EachFixedDrive = true, NewList = true },
                new() { Kind = "disk" },
            ],
            Widths = [FolderListWidth.Narrow, FolderListWidth.Wide],
        };
        string Made(PresetMachine machine) =>
            string.Join(" / ", Presets.Lists(odd, machine).Select(list => string.Join(" ", list.Select(cell => cell.Island.Kind))));

        Assert.Equal("cpu / battery memory / drive drive disk", Made(Laptop));
        Assert.Equal("cpu / memory / drive disk", Made(Desktop));
        Assert.Equal("cpu / memory / disk", Made(Bare));
        var folder = Presets.Folder(odd, Laptop).Folder!;
        Assert.Equal(3, folder.Lists.Count);
        Assert.Equal([FolderListWidth.Narrow, FolderListWidth.Wide], folder.Widths!);
        Assert.Equal(Presets.Cells(odd, Laptop).Select(cell => cell.Island.Kind), folder.Cells.Select(cell => cell.Island.Kind));
    }
}
