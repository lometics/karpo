// The add list's ready-made groups (AddList.cs, Karpo.Core\Presets.cs; 2026-10-05: "in the add
// widget menu there should also be another additional section where you can add precompiled groups
// of stuff"): since 2026-10-11 a row each in its subject of the add tree (Karpo.Core\AddTree.cs), its
// icon and its name, found by the same search field, offered on the bar alone, and a pick adds the
// group's one folder at the "+" ("the ready made widgets should only ever add one folder with
// multiple widgets inside"). Plain code: no window.

using Karpo.Dock;
using Karpo.Tests.Core;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class AddListPresetsTests
{
    private const uint First = TheDock.CMD_ADD_WIDGET;

    /// <summary>A bar of a clock at the start, the apps in the middle, show desktop at the end, and a list folder ("list").</summary>
    private static List<BarItem> Bar()
    {
        var clock = BarKind.NightLight.New();
        clock.Id = "clock";
        clock.Section = BarSection.Start;
        var list = BarKind.FolderList.New();
        list.Id = "list";
        list.Section = BarSection.Start;
        var apps = BarKind.Apps.New();
        apps.Id = "apps";
        apps.Section = BarSection.Middle;
        var desktop = BarKind.ShowDesktop.New();
        desktop.Id = "desktop";
        desktop.Section = BarSection.End;
        return [clock, list, apps, desktop];
    }

    /// <summary>The whole list as the dock shows it for a place of <paramref name="type"/>: the tree's rows, its widgets and its ready-made folders.</summary>
    internal static (List<FlyoutItem> Items, List<CatalogueEntry> Entries, List<Preset> Presets) Whole(WidgetType type) =>
        AddList.Rows(type, turns: true, First);

    // --- Where they stand --------------------------------------------------------------------------

    /// <summary>
    /// A ready-made folder is a row as a widget is, in its subject beside the widgets it is made of
    /// (the person, 2026-10-11: "i dont think that ready made groups should really be a group now
    /// anymore"): no heading of their own, each its icon and its name, its command counted on from
    /// the widgets' last.
    /// </summary>
    [Fact]
    public void AReadyMadeFolderStandsInItsSubject()
    {
        var (items, entries, presets) = Whole(WidgetType.Bar);

        Assert.Equal([AddTree.Popular.Title, AddTree.Browse.Title], items.Where(item => item.Header).Select(item => item.Text));
        var rows = AddList.Picks(items).Where(row => row.Id >= First + (uint)entries.Count && row.Words is not null).ToList();
        Assert.Equal(presets.Select(preset => preset.Title).Order(), rows.Select(row => row.Text).Order());
        foreach (var row in rows)
        {
            var preset = presets[(int)(row.Id - First) - entries.Count];
            Assert.Equal(preset.Title, row.Text);
            Assert.Equal(preset.Glyph, Assert.IsType<FlyoutIcon.Glyph>(row.Icon).Code);
            Assert.True(row.Enabled);
        }
        var weather = items.Single(item => item.Text == "Weather and news");
        Assert.Contains(weather.Children!, row => row.Text == Presets.Find(Presets.MarketId)!.Title);
        var system = items.Single(item => item.Text == "System info");
        Assert.Contains(system.Children!, row => row.Text == Presets.Find("systemGroup")!.Title);
    }

    /// <summary>The bar offers every ready-made folder; a folder's list none: a group is one folder, and folders do not nest.</summary>
    [Fact]
    public void OnlyTheBarOffersGroups()
    {
        var (_, _, presets) = Whole(WidgetType.Bar);
        Assert.Equal(Presets.All.Select(preset => preset.Id).Order(StringComparer.Ordinal), presets.Select(preset => preset.Id).Order(StringComparer.Ordinal));
        var (items, entries, none) = Whole(WidgetType.List);
        Assert.Empty(none);
        Assert.All(AddList.Picks(items), row => Assert.True(row.Id < First + (uint)entries.Count));
    }

    // --- Search -----------------------------------------------------------------------------------------

    /// <summary>The field finds a ready-made folder as it finds a widget: by its name, its line, its words and its widgets' names.</summary>
    [Theory]
    [InlineData("stocks", Presets.MarketId)]
    [InlineData("bluetooth", "connectivityGroup")]
    [InlineData("task manager", "systemGroup")]
    public void TheSearchFindsGroups(string query, string id)
    {
        var (items, _, _) = Whole(WidgetType.Bar);
        Assert.Contains(AddList.Results(items, query), result => result.Text == Presets.Find(id)!.Title);
    }

    /// <summary>Enter on the field adds a ready-made folder that is the one match, as it adds a lone widget.</summary>
    [Fact]
    public void EnterAddsTheOneGroupThatMatches()
    {
        var (items, entries, presets) = Whole(WidgetType.Bar);
        var results = AddList.Results(items, "watchlist");

        Assert.Single(results);
        var stocks = presets.FindIndex(preset => preset.Id == Presets.MarketId);
        Assert.Equal(First + (uint)(entries.Count + stocks), AddList.Single(results));
    }

    // --- Adding one ----------------------------------------------------------------------------------

    /// <summary>
    /// At each section's "+" every group is one folder there, of the right kind and section, its
    /// widgets inside, with ids of its own, and nothing else on the bar.
    /// </summary>
    [Theory]
    [InlineData(BarSection.Start)]
    [InlineData(BarSection.Middle)]
    [InlineData(BarSection.End)]
    public void OnTheBarEveryGroupIsOneFolderInTheSectionOfThePlus(BarSection section)
    {
        foreach (var preset in Presets.All)
        {
            var bar = Bar();
            var before = Karpo.Core.Folders.All(bar).Select(entry => entry.Island.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var slot = new BarSlot(section, Sections.Range(bar, section).To);

            var paths = AddList.Add(bar, new AddSpot("default", slot), preset, PresetsTests.Laptop);

            var id = Assert.Single(paths);
            Assert.Equal(5, bar.Count);
            var folder = bar.Single(island => island.Id == id);
            Assert.Equal(preset.Folder, folder.Kind);
            Assert.Equal(section, folder.Section);
            Assert.Equal(preset.Face?.Kind ?? FolderFaces.IconId, folder.Folder.Face.Kind);
            Assert.Equal(Presets.ItemsOn(preset, PresetsTests.Laptop).Count, folder.Folder.Cells.Count);
            Assert.All(Karpo.Core.Folders.All([folder]), entry => Assert.DoesNotContain(entry.Island.Id, before));
        }
        // System's lines in Task Manager's order, the battery last, over its footer.
        var system = Bar();
        var at = new AddSpot("default", new BarSlot(section, Sections.Range(system, section).To));
        // Added once, before the bar is searched for it: in the search's predicate it was added again
        // for every entry asked about.
        var added = Assert.Single(AddList.Add(system, at, Presets.Find("systemGroup")!, PresetsTests.Laptop));
        var made = system.Single(island => island.Id == added);
        Assert.Equal(["cpu", "divider", "memory", "divider", "disk", "network", "divider", "gpu", "divider", "battery", "actions"], Karpo.Core.Folders.ReadingOrder(made.Folder!).Select(cell => cell.Island.Kind));
    }

    /// <summary>
    /// In a folder a group adds nothing — not its widgets loose, not a folder in the folder — and no
    /// group added anywhere: a folder's list does not offer them. The same for a folder that has gone.
    /// </summary>
    [Fact]
    public void InAFolderAGroupAddsNothing()
    {
        var bar = Bar();

        foreach (var preset in Presets.All)
        {
            Assert.Empty(AddList.Add(bar, new AddSpot("default", default, "list"), preset, PresetsTests.Laptop));
            Assert.Empty(AddList.Add(bar, new AddSpot("default", default, "gone"), preset, PresetsTests.Laptop));
        }
        Assert.Empty(Karpo.Core.Folders.FolderIn(bar, "list")!.Cells);
        Assert.Equal(4, bar.Count);
    }

    /// <summary>
    /// The settings that open after a group is added: the widget that needs setting up, else the
    /// folder's own page — every group there is now sets itself up, so its folder's page.
    /// </summary>
    [Fact]
    public void TheSettingsOpenOnTheGroupsFolder()
    {
        var bar = Bar();
        var end = new AddSpot("default", new BarSlot(BarSection.End, Sections.Range(bar, BarSection.End).To));

        var system = AddList.Add(bar, end, Presets.Find("systemGroup")!, PresetsTests.Desktop);
        Assert.Equal(system[0], AddList.SettingsPath(bar, system));

        var radios = AddList.Add(bar, end, Presets.Find("connectivityGroup")!, PresetsTests.Desktop);
        Assert.Equal(radios[0], AddList.SettingsPath(bar, radios));
        Assert.Equal(BarKind.FolderList, bar.Single(island => island.Id == radios[0]).Kind);
        Assert.Null(AddList.SettingsPath(bar, []));
    }
}
