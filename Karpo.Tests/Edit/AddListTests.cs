// The add list (AddList.cs, backlog items 40, 3 and 24): one list of widgets, the add tree's share
// for the place (Karpo.Core\AddTree.cs), each row its icon and its name, offering what fits where the add was pressed — taskbar entries on the bar, list
// widgets in a list folder, and never the other type there (Partners) — the same rows as the settings app's
// library page of that type; and a pick adds that widget at that place.

using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class AddListTests
{
    private const uint First = TheDock.CMD_ADD_WIDGET;

    /// <summary>A bar of a clock, a battery and a list folder ("list", named Glance).</summary>
    private static List<BarItem> Bar()
    {
        var clock = BarKind.NightLight.New();
        clock.Id = "clock";
        var battery = BarKind.EnergySaver.New();
        battery.Id = "battery";
        var list = BarKind.FolderList.New();
        list.Id = "list";
        return [clock, battery, list];
    }

    /// <summary>
    /// The add tree as the list shows it (Karpo.Core\AddTree.cs): two columns under their headings,
    /// Popular and Browse; a subject a row opening its submenu, a folder in it another, the row
    /// itself picking nothing. A row shown again (Popular's) has its home's command, and nothing for
    /// the search to find it by, so the search lists it once.
    /// </summary>
    [Fact]
    public void ASubjectIsARowOpeningItsSubmenu()
    {
        var (items, entries, _) = AddList.Rows(WidgetType.List, turns: true, First);
        Assert.Equal([AddTree.Popular.Title, AddTree.Browse.Title], items.Where(item => item.Header).Select(item => item.Text));
        var connections = items.Single(item => item.Text == "Connections");
        Assert.True(connections.Id >= First + (uint)entries.Count);
        // §490: a subject's submenu is its picks, no folder in it: what were Wi-Fi's and the power buttons' folders are
        // runs of its rows, the latter after a line.
        string[] Picks(FlyoutItem subject) => [.. subject.Children!.Where(child => child.Id >= First && child.Id < First + (uint)entries.Count).Select(child => entries[(int)(child.Id - First)].Id)];
        Assert.Equal(["wifi", "wifiSwitch", "actions"], Picks(connections).Take(3));
        Assert.DoesNotContain(connections.Children!, child => child.Children is not null);
        var power = items.Single(item => item.Text == "Battery and power");
        Assert.Equal(["sleep", "hibernate", "shutDown", "restart", "signOut", "lock"], Picks(power).TakeLast(6));
        Assert.Contains(power.Children!, child => child.Separator);

        // Popular's clock is the clock in Time and calendar: one command, words on its home alone.
        var popular = items.Single(item => item.Header && item.Text == AddTree.Popular.Title);
        var clock = items[items.IndexOf(popular) + 1];
        var home = items.Single(item => item.Text == "Time and calendar").Children!.Single(item => item.Text == clock.Text);
        Assert.Equal(home.Id, clock.Id);
        Assert.Null(clock.Words);
        Assert.NotNull(home.Words);
    }

    /// <summary>
    /// §511: the bar has no separator. Its add list offers none — by the bar's word ("separator"), by the folder's
    /// ("divider", a list's alone), or by the name both wore —, and the catalogue knows no widget "separator".
    /// </summary>
    [Fact]
    public void TheTaskbarsAddListOffersNoSeparator()
    {
        var (items, entries, _) = AddList.Rows(WidgetType.Bar, turns: true, First);

        Assert.NotEmpty(entries);
        Assert.DoesNotContain(entries, entry => entry.Id is "separator" or "divider");
        Assert.DoesNotContain(AddList.Picks(items), item => item.Text == WidgetInfos.Divider.Title);
        Assert.DoesNotContain(WidgetInfos.All, info => info.Id == "separator");
        Assert.DoesNotContain(BarEntries.All, entry => entry.Info.Id == "separator");
        // A folder's divider is still a list's to add.
        Assert.Contains(AddList.Rows(WidgetType.List, turns: true, First).Entries, entry => entry.Id == "divider");
    }

    [Fact]
    public void ThePlaceDecidesTheLibrary()
    {
        var bar = Bar();

        Assert.Equal(WidgetType.Bar, AddList.TypeOf(new AddSpot("default", new BarSlot(BarSection.Start, 1)), bar));
        Assert.Equal(WidgetType.List, AddList.TypeOf(new AddSpot("default", new BarSlot(BarSection.Start, 0), "list"), bar));
        // A folder that has gone, and a widget that is no folder, are no place to add to.
        Assert.Null(AddList.TypeOf(new AddSpot("default", new BarSlot(BarSection.Start, 0), "gone"), bar));
        Assert.Null(AddList.TypeOf(new AddSpot("default", new BarSlot(BarSection.Start, 0), "clock"), bar));
    }

    /// <summary>
    /// One list per page, the page's share of the add tree: every widget the page has once in
    /// <c>Entries</c>, the bar's ready-made folders in <c>Presets</c>, each with its own command —
    /// a widget's its place in Entries, a preset's after the last — and a row for each, its icon and
    /// its name; nothing of another page.
    /// </summary>
    [Theory]
    [InlineData(WidgetType.Bar)]
    [InlineData(WidgetType.List)]
    public void TheRowsAreThatLibrarysOwn(WidgetType type)
    {
        var (items, entries, presets) = AddList.Rows(type, turns: true, First);

        var members = Catalogue.On(type).Select(entry => entry.Key)
            .Concat(type == WidgetType.List ? PremadeWidgets.All.Select(premade => CatalogueEntry.Of(premade).Key) : []);
        Assert.Equal(members.Order(StringComparer.Ordinal), entries.Select(entry => entry.Key).Order(StringComparer.Ordinal));
        Assert.Equal(Presets.On(type).Select(preset => preset.Id).Order(StringComparer.Ordinal), presets.Select(preset => preset.Id).Order(StringComparer.Ordinal));
        Assert.All(entries, entry => Assert.Equal(type, entry.Type));
        // Every one has a row with its name and icon, found by the search there (its home).
        var homes = AddList.Picks(items).Where(row => row.Words is not null).ToList();
        Assert.Equal(Enumerable.Range(0, entries.Count + presets.Count).Select(index => First + (uint)index), homes.Select(row => row.Id).Order());
        foreach (var row in homes)
        {
            var at = (int)(row.Id - First);
            var (title, glyph) = at < entries.Count ? (entries[at].Title, entries[at].Glyph) : (presets[at - entries.Count].Title, presets[at - entries.Count].Glyph);
            Assert.Equal(title, row.Text);
            Assert.Equal(glyph, Assert.IsType<FlyoutIcon.Glyph>(row.Icon).Code);
        }
        // A row shown again is one of those.
        Assert.All(AddList.Picks(items).Where(row => row.Words is null), row => Assert.Contains(homes, home => home.Id == row.Id && home.Text == row.Text));
    }

    /// <summary>Availability is shown, never hidden: a widget this PC has not got is greyed in its place, and says why.</summary>
    [Fact]
    public void WhatThisPcHasNotGotIsGreyedAndSaysWhy()
    {
        var (items, entries, _) = AddList.Rows(WidgetType.Bar, turns: false, First);
        var at = entries.FindIndex(entry => WidgetInfos.Find(entry.Id)?.Switch == QuickSwitch.RotationLock);
        Assert.True(at >= 0, "the library has rotation lock");
        // In Screen and sharing's submenu, and again in Quick settings': greyed in both.
        var rows = AddList.Picks(items).Where(item => item.Id == First + (uint)at).ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, each => Assert.False(each.Enabled));
        var row = rows[0];

        Assert.False(row.Enabled);
        Assert.Equal(Catalogue.WhyNotHere(WidgetInfos.Find(entries[at].Id), turns: false), row.Detail);
        Assert.All(AddList.Picks(AddList.Rows(WidgetType.Bar, turns: true, First).Items).Where(item => item.Id == row.Id), item => Assert.True(item.Enabled));
    }

    [Fact]
    public void ThePickOnTheBarGoesWhereThePlusWas()
    {
        var bar = Bar();
        var weather = Catalogue.On(WidgetType.Bar).Single(entry => entry.Bar == BarKind.WidgetsBoard);

        var id = AddList.Add(bar, new AddSpot("default", new BarSlot(BarSection.Start, 1)), weather);

        Assert.NotNull(id);
        Assert.Equal(BarKind.WidgetsBoard, bar[1].Kind);
        Assert.Equal(id, bar[1].Id);
        // A second one of the same kind is its own widget, with an id of its own.
        var again = AddList.Add(bar, new AddSpot("default", new BarSlot(BarSection.Start, bar.Count)), weather);
        Assert.NotEqual(id, again);
        Assert.Equal(again, bar[^1].Id);
    }

    [Fact]
    public void ThePickInAListGoesAfterTheLastLine()
    {
        var bar = Bar();
        var lines = Catalogue.On(WidgetType.List);

        var first = AddList.Add(bar, new AddSpot("default", new BarSlot(BarSection.Start, 0), "list"), lines.Single(entry => entry.List == ListKind.Clock));
        var second = AddList.Add(bar, new AddSpot("default", new BarSlot(BarSection.Start, 0), "list"), lines.Single(entry => entry.List == ListKind.Battery));

        Assert.NotNull(first);
        Assert.NotNull(second);
        var folder = Karpo.Core.Folders.FolderIn(bar, "list")!;
        Assert.Equal([first, second], Karpo.Core.Folders.ReadingOrder(folder).Select(cell => Karpo.Core.Folders.Path("list", cell.Island.Id)));
        Assert.All(folder.Cells, cell => Assert.Equal(FolderView.List, cell.Island.View));
    }

    /// <summary>
    /// §283 (the person: "performance should be split into multiple entries in the add widgets menu,
    /// it should still be one widget but with different configurations depending on what i add"): a
    /// folder's list has a row a reading in System info, each its reading's name and icon, and each
    /// adds the one performance widget measuring that reading, in its graph, the reading still its to
    /// change. The bar's has each reading's ready-made folder, as before.
    /// </summary>
    [Fact]
    public void EachReadingIsARowAddingThePerformanceWidgetMeasuringIt()
    {
        var (items, entries, _) = AddList.Rows(WidgetType.List, turns: true, First);
        var system = items.Single(item => item.Text == "System info").Children!.ToList();
        var readings = system.Where(item => item.Id >= First && item.Id < First + (uint)entries.Count && entries[(int)(item.Id - First)].List is { } form && WidgetStyles.Of(form) is { Style.Reading: not null } styled && styled.Widget == WidgetStyles.Performance).ToList();
        Assert.Equal(["Processor", "Graphics", "Memory", "Disk", "Network activity"], readings.Select(item => item.Text));
        Assert.Equal([OptionGlyphs.Processor, OptionGlyphs.Graphics, OptionGlyphs.Memory, OptionGlyphs.Disk, OptionGlyphs.Network], readings.Select(item => Assert.IsType<FlyoutIcon.Glyph>(item.Icon).Code));
        // One after another, as the reading choice lists them.
        Assert.Equal(Enumerable.Range(system.IndexOf(readings[0]), 5), readings.Select(item => system.IndexOf(item)));

        var bar = Bar();
        string[] words = ["cpu", "gpu", "memory", "disk", "network"];
        foreach (var (item, reading) in readings.Zip(words))
        {
            var path = AddList.Add(bar, new AddSpot("default", new BarSlot(BarSection.Start, 0), "list"), entries[(int)(item.Id - First)]);
            var line = Assert.IsType<CellItem>(Karpo.Core.Folders.Find(bar, path!));
            Assert.Equal((WidgetStyles.Performance.Id, reading, WidgetStyles.GraphWord), (WidgetStyles.FileKind(line.Kind), WidgetStyles.FileReading(line.Kind), WidgetStyles.FileStyle(line.Kind)));
        }
        var memory = Assert.IsType<CellItem>(Karpo.Core.Folders.ReadingOrder(Karpo.Core.Folders.FolderIn(bar, "list")!).ElementAt(2).Island);
        Assert.True(WidgetStyles.ChooseReading(memory, 1));
        Assert.Equal("gpu", WidgetStyles.FileReading(memory.Kind));

        var (barItems, barEntries, _) = AddList.Rows(WidgetType.Bar, turns: true, First);
        var barSystem = barItems.Single(item => item.Text == "System info").Children!;
        Assert.Contains(barSystem, item => item.Text == "Memory" && item.Id >= First + (uint)barEntries.Count);

        // §318: the details, a widget of their own, a row a reading — §490: a run of System info's rows of their own,
        // after a line —, each its form's name and icon.
        var details = system.Where(item => item.Id >= First && item.Id < First + (uint)entries.Count && entries[(int)(item.Id - First)].List is { } form && WidgetStyles.Of(form) is { } styled && styled.Widget == WidgetStyles.PerformanceDetails).ToList();
        Assert.True(system[system.IndexOf(details[0]) - 1].Separator);
        Assert.Equal(["Processor details", "Graphics details", "Memory details", "Disk details", "Network details"], details.Select(item => item.Text));
        Assert.Equal([WidgetInfos.CpuDetails.Glyph, WidgetInfos.GpuDetails.Glyph, WidgetInfos.MemoryDetails.Glyph, WidgetInfos.DiskDetails.Glyph, WidgetInfos.NetworkDetails.Glyph],
            details.Select(item => Assert.IsType<FlyoutIcon.Glyph>(item.Icon).Code));
        foreach (var (item, reading) in details.Zip(words))
        {
            var path = AddList.Add(bar, new AddSpot("default", new BarSlot(BarSection.Start, 0), "list"), entries[(int)(item.Id - First)]);
            var line = Assert.IsType<CellItem>(Karpo.Core.Folders.Find(bar, path!));
            Assert.Equal((WidgetStyles.PerformanceDetails.Id, reading, (string?)null), (line.FileKind, line.FileReading, line.FileStyle));
        }
    }

    /// <summary>
    /// Every row that adds has its icon: §228 had lost the calendar's and the performance widget's
    /// (their code points dropped from the source), so Popular's Calendar stood bare.
    /// </summary>
    [Theory]
    [InlineData(WidgetType.Bar)]
    [InlineData(WidgetType.List)]
    public void EveryRowHasItsIcon(WidgetType type)
    {
        var (items, _, _) = AddList.Rows(type, turns: true, First);
        Assert.All(AddList.Picks(items), row => Assert.NotEmpty(Assert.IsType<FlyoutIcon.Glyph>(row.Icon).Code));
        Assert.All(WidgetStyles.All, widget => Assert.NotEmpty(widget.Glyph));
    }

    /// <summary>No two widgets of a page's list share a name: a row is told apart by what it says (a ready-made folder by its badge too).</summary>
    [Theory]
    [InlineData(WidgetType.Bar)]
    [InlineData(WidgetType.List)]
    public void NoTwoWidgetsOfAPageShareAName(WidgetType type)
    {
        var (_, entries, _) = AddList.Rows(type, turns: true, First);
        Assert.Empty(entries.GroupBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase).Where(same => same.Count() > 1).Select(same => same.Key));
    }

    /// <summary>
    /// A row of another library than the place's is never added there — a taskbar entry is not put
    /// in a folder, nor a list's line on the bar (Partners) — and a folder that has gone takes nothing.
    /// </summary>
    [Fact]
    public void ARowOfAnotherLibraryAddsNothing()
    {
        var bar = Bar();
        var count = bar.Count;
        var barClock = Catalogue.On(WidgetType.Bar).Single(entry => entry.Bar == BarKind.NightLight);
        var listClock = Catalogue.On(WidgetType.List).Single(entry => entry.List == ListKind.Clock);

        Assert.Null(AddList.Add(bar, new AddSpot("default", new BarSlot(BarSection.Start, 0), "list"), barClock));
        Assert.Null(AddList.Add(bar, new AddSpot("default", new BarSlot(BarSection.Start, 1)), listClock));
        Assert.Null(AddList.Add(bar, new AddSpot("default", new BarSlot(BarSection.Start, 0), "gone"), listClock));
        Assert.Equal(count, bar.Count);
        Assert.Empty(Karpo.Core.Folders.FolderIn(bar, "list")!.Cells);
    }

    /// <summary>
    /// The add list offers only the place's own type: the bar's offers taskbar entries alone, a list
    /// folder's list widgets alone — whatever subject has a form in both.
    /// </summary>
    [Fact]
    public void TheAddListOffersOnlyThePlacesType()
    {
        Assert.All(AddList.Rows(WidgetType.Bar, turns: true, First).Entries, entry => Assert.Equal((WidgetType.Bar, (ListKind?)null), (entry.Type, entry.List)));
        Assert.All(AddList.Rows(WidgetType.List, turns: true, First).Entries, entry => Assert.Equal((WidgetType.List, (BarKind?)null), (entry.Type, entry.Bar)));
    }

    /// <summary>A greyed row says why in the row itself, in a few words that fit under its name.</summary>
    [Fact]
    public void AGreyedRowsReasonIsShort()
    {
        var turns = AddList.Picks(AddList.Rows(WidgetType.Bar, turns: false, First).Items).First(item => !item.Enabled);
        Assert.Equal("This screen doesn't turn", turns.Detail);
    }

    /// <summary>
    /// A list's divider is put there on purpose, and only there (2026-10-05: "you have to add it
    /// deliberately and can also move it around then"): a list folder's add list offers "Separator" (the bar's name for it, 2026-10-11)
    /// under Folders, with the heading; the bar's offers none. A pick adds one line, a
    /// divider, after the last; it moves as any line does, a drop landing it at a place in the order
    /// (Folders.InsertLine, which the drag lands through). Nothing else adds one: widgets added to a
    /// list come with no divider between them.
    /// </summary>
    [Fact]
    public void AListsDividerIsAddedOnPurposeAndMoves()
    {
        var bar = Bar();
        var (items, entries, _) = AddList.Rows(WidgetType.List, turns: true, First);
        var row = items.Single(item => item.Text == "Separator" && !item.Header);
        Assert.True(row.Enabled);
        var entry = entries[(int)(row.Id - First)];
        Assert.Equal(WidgetCategory.Folders, entry.Category);
        Assert.DoesNotContain(AddList.Rows(WidgetType.Bar, true, First).Entries, each => each.Id == WidgetInfos.Divider.Id);

        var list = AddList.Add(bar, new AddSpot("default", default, "list"), Catalogue.On(WidgetType.List).Single(each => each.List == ListKind.Clock));
        var battery = AddList.Add(bar, new AddSpot("default", default, "list"), Catalogue.On(WidgetType.List).Single(each => each.List == ListKind.Battery));
        Assert.NotNull(list);
        Assert.NotNull(battery);
        var folder = Karpo.Core.Folders.FolderIn(bar, "list")!;
        // Two widgets, and nothing between them.
        Assert.Equal(["clock", "battery"], Karpo.Core.Folders.ReadingOrder(folder).Select(cell => cell.Island.Kind));

        var path = AddList.Add(bar, new AddSpot("default", default, "list"), entry);
        Assert.NotNull(path);
        Assert.Equal(["clock", "battery", "divider"], Karpo.Core.Folders.ReadingOrder(folder).Select(cell => cell.Island.Kind));

        // Carried up between the two, as a drag lands it: let go above the second line.
        var divider = Karpo.Core.Folders.CellOf(folder, path[(path.IndexOf('/') + 1)..])!;
        Assert.True(Karpo.Core.Folders.Move(folder, divider, new FolderPlace(0, 1)));
        Assert.Equal(["clock", "divider", "battery"], Karpo.Core.Folders.ReadingOrder(folder).Select(cell => cell.Island.Kind));
    }

    /// <summary>
    /// A place per add (§297, §593 always one): a folder's "+", a column's or a slot's, its pick going in there at once; a
    /// folder's add with no place of its own goes in where it makes no column — the end of the last list.
    /// </summary>
    [Fact]
    public void APlaceTakesThePickAtOnce()
    {
        var bar = Bar();
        var folder = Karpo.Core.Folders.FolderIn(bar, "list")!;
        folder.Lists.Add([]);
        var clock = Catalogue.On(WidgetType.List).Single(each => each.List == ListKind.Clock);

        var into = AddList.Add(bar, AddSpot.InFolder("default", "list", new FolderPlace(1, 0)), clock)!;
        Assert.Equal(new FolderPlace(1, 0), Karpo.Core.Folders.PlaceOf(folder, Karpo.Core.Folders.CellOf(folder, into[(into.IndexOf('/') + 1)..])!));

        var made = AddList.Add(bar, AddSpot.InFolder("default", "list", FolderPlace.Slot(2, 0)), clock)!;
        Assert.Equal(3, folder.Lists.Count);
        Assert.Equal(new FolderPlace(2, 0), Karpo.Core.Folders.PlaceOf(folder, Karpo.Core.Folders.CellOf(folder, made[(made.IndexOf('/') + 1)..])!));

        var last = AddList.Add(bar, AddSpot.InFolder("default", "list", null), clock)!;
        Assert.Equal(3, folder.Lists.Count);
        Assert.Equal(2, Karpo.Core.Folders.PlaceOf(folder, Karpo.Core.Folders.CellOf(folder, last[(last.IndexOf('/') + 1)..])!)?.List);
    }

    /// <summary>
    /// §593: the add page's ghost (AddList.Into): the pick put in at the page's place in a copy — the config it was made of
    /// untouched, so nothing is saved by a hover —, the key it went in under; a click puts in that very copy, so the bar the
    /// ghost laid out is the one the click leaves. A widget on the bar at a "+"'s slot, a list widget into a folder's column, a
    /// ready-made group as its one folder (its machine read for it alone); nothing for a command of neither, another type's row.
    /// </summary>
    [Fact]
    public void AGhostIsThePickPutInACopy()
    {
        var config = new Config();
        var owner = Config.LayoutOwner(config.DefaultLayout.Id);
        var list = config.ListFor(owner);
        list.Clear();
        list.AddRange(Bar());
        var before = config.ToJson();
        string PathOf(string key) => key[(owner.Length + 1)..];
        var (_, entries, presets) = AddList.Rows(WidgetType.Bar, turns: false, firstId: 100);
        var clock = entries.FindIndex(entry => entry.Bar is { } kind && !kind.IsFolder());
        Assert.True(clock >= 0);
        var asked = 0;
        PresetMachine Machine()
        {
            asked++;
            return new PresetMachine(HasBattery: false, FixedDrives: []);
        }

        var spot = new AddSpot(owner, new BarSlot(BarSection.End, 0));
        var ghost = AddList.Into(config, spot, (uint)(100 + clock), 100, entries, presets, Machine)!;
        Assert.Equal(before, config.ToJson());
        Assert.Same(config, ghost.Base);
        Assert.NotSame(config, ghost.Config);
        Assert.Equal((uint)(100 + clock), ghost.Id);
        var added = ghost.Config.FindIsland(owner, PathOf(ghost.Key))!;
        Assert.Equal(entries[clock].Bar, ((BarItem)added).Kind);
        Assert.Equal(BarSection.End, ((BarItem)added).Section);
        Assert.Equal(list.Count + 1, ghost.Config.ListFor(owner).Count);
        Assert.Equal(0, asked);
        // As the same pick added at once: the same list but the new widget's id.
        var direct = config.Clone();
        var path = AddList.Add(direct.ListFor(owner), spot, entries[clock])!;
        Assert.Equal(
            direct.ListFor(owner).Select(item => item.Id == path ? "new" : item.Id),
            ghost.Config.ListFor(owner).Select(item => item.Id == PathOf(ghost.Key) ? "new" : item.Id));

        // A ready-made group: its folder, the machine read for it.
        var group = AddList.Into(config, spot, (uint)(100 + entries.Count), 100, entries, presets, Machine)!;
        Assert.Equal(1, asked);
        Assert.True(((BarItem)group.Config.FindIsland(owner, PathOf(group.Key))!).Kind.IsFolder());
        Assert.Equal(before, config.ToJson());

        // A list widget into a folder's column.
        var lists = AddList.Rows(WidgetType.List, turns: false, firstId: 100).Entries;
        var line = lists.FindIndex(entry => entry.List == ListKind.Clock);
        var inFolder = AddList.Into(config, AddSpot.InFolder(owner, "list", new FolderPlace(0, 0)), (uint)(100 + line), 100, lists, [], Machine)!;
        Assert.StartsWith(owner + "/list/", inFolder.Key, StringComparison.Ordinal);
        Assert.Equal(before, config.ToJson());

        // Nothing: a command past the list's, a bar's widget into a folder.
        Assert.Null(AddList.Into(config, spot, 99, 100, entries, presets, Machine));
        Assert.Null(AddList.Into(config, spot, (uint)(100 + entries.Count + presets.Count), 100, entries, presets, Machine));
        Assert.Null(AddList.Into(config, AddSpot.InFolder(owner, "list", null), (uint)(100 + clock), 100, entries, presets, Machine));
    }

    /// <summary>
    /// §617 (the person, 2026-11-09: "when adding a widget it still first shows just a blue line, this should not be. it should
    /// from teh beginning show an empty blue rectangle"): the empty ghost is one standard room put in at the page's place in a
    /// copy, as a pick's ghost is — one button on a bar, in the "+"'s section at its index; one line in a folder's column — no
    /// pick's (Id 0), the config untouched; nothing for a folder that has gone.
    /// </summary>
    [Fact]
    public void TheEmptyGhostIsOneRoomPutInACopy()
    {
        var config = new Config();
        var owner = Config.LayoutOwner(config.DefaultLayout.Id);
        var list = config.ListFor(owner);
        list.Clear();
        list.AddRange(Bar());
        var before = config.ToJson();
        string PathOf(string key) => key[(owner.Length + 1)..];

        // A bar's "+" (and the edit bar's Add, which adds at the bar's last "+"): one cell, where the pick would go.
        var spot = new AddSpot(owner, new BarSlot(BarSection.End, 0));
        var empty = AddList.Empty(config, spot)!;
        Assert.True(empty.Empty);
        Assert.Equal(0u, empty.Id);
        Assert.Same(config, empty.Base);
        Assert.Equal(before, config.ToJson());
        var room = (BarItem)empty.Config.FindIsland(owner, PathOf(empty.Key))!;
        Assert.Equal(BarKind.TaskView, room.Kind);
        Assert.Equal(WidgetSize.OneCell, BarEntries.Find(room.Kind)!.Size);
        Assert.Equal(BarSection.End, room.Section);
        Assert.Equal(list.Count + 1, empty.Config.ListFor(owner).Count);
        // Where a pick at that place stands.
        var (_, entries, presets) = AddList.Rows(WidgetType.Bar, turns: false, firstId: 100);
        var clock = entries.FindIndex(entry => entry.Bar is { } kind && !kind.IsFolder());
        var pick = AddList.Into(config, spot, (uint)(100 + clock), 100, entries, presets, () => new PresetMachine(HasBattery: false, FixedDrives: []))!;
        Assert.Equal(
            pick.Config.ListFor(owner).Select(item => item.Id == PathOf(pick.Key) ? "new" : item.Id),
            empty.Config.ListFor(owner).Select(item => item.Id == PathOf(empty.Key) ? "new" : item.Id));
        Assert.False(pick.Empty);

        // A folder column's "+": one line in that column.
        var inFolder = AddList.Empty(config, AddSpot.InFolder(owner, "list", new FolderPlace(0, 0)))!;
        Assert.True(inFolder.Empty);
        Assert.StartsWith(owner + "/list/", inFolder.Key, StringComparison.Ordinal);
        Assert.IsType<CellItem>(inFolder.Config.FindIsland(owner, PathOf(inFolder.Key)));
        Assert.Equal(before, config.ToJson());

        // A folder gone: nothing.
        Assert.Null(AddList.Empty(config, AddSpot.InFolder(owner, "gone", null)));
    }

    /// <summary>
    /// §617 (the person, 2026-11-09: "it also does not show the folder contents as a preview when adding the air quality for
    /// example"): a ready-made folder's pick — the air quality's, the weather's — puts in its folder whole, its lines the
    /// preset's, which the ghost's folder opens with (Dock.GhostFolder).
    /// </summary>
    [Theory]
    [InlineData(Presets.AirQualityId)]
    [InlineData(Presets.WeatherId)]
    public void APresetsGhostIsItsFolderWithItsLines(string id)
    {
        var config = new Config();
        var owner = Config.LayoutOwner(config.DefaultLayout.Id);
        var (_, entries, presets) = AddList.Rows(WidgetType.Bar, turns: false, firstId: 100);
        var preset = presets.FindIndex(each => each.Id == id);
        Assert.True(preset >= 0, $"{id} not offered on a bar");
        var ghost = AddList.Into(config, new AddSpot(owner, new BarSlot(BarSection.End, 0)), (uint)(100 + entries.Count + preset), 100, entries, presets, () => new PresetMachine(HasBattery: false, FixedDrives: []))!;
        var folder = (BarItem)ghost.Config.FindIsland(owner, ghost.Key[(owner.Length + 1)..])!;
        Assert.True(folder.Kind.IsFolder());
        Assert.Equal(presets[preset].Items.Count, Karpo.Core.Folders.ReadingOrder(folder.Folder!).Count);
    }
}
